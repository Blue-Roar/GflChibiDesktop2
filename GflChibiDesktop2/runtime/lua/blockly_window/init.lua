local ffi = require("ffi")
local raylib = require("raylib")
local win32 = require("win32")
local cached = require("cached")
local ev = require("eventize")

local rl = raylib.lib
raylib = raylib.struct

local M = {}
M.windowSize = { width = 400, height = 300 }

-- 透明实现模式：
--   默认：raylib 原生（FLAG_WINDOW_TRANSPARENT，DWM blur-behind 逐像素 alpha），
--         在窗口精确铺满显示器或部分环境（如 Win11+新N卡驱动）下会失效显示黑底；
--   "ulw"：UpdateLayeredWindow 逐像素 alpha，不依赖 DWM blur-behind，兼容性更好。
-- 通过 transparent_mode.conf（实例目录或 app 根目录，内容为 "ulw"）启用。
local ulwMode = (function()
    local function readMode(path)
        local f = io.open(path, "r")
        if f then
            local m = f:read("l")
            f:close()
            if m then
                -- 去除可能的 UTF-8 BOM
                m = m:gsub("^\239\187\191", "")
                return m:match("^%s*([%w_]+)%s*$")
            end
        end
        return nil
    end
    -- 工作目录为 app/instances/{id}，app 根需要 ../../transparent_mode.conf
    local m = readMode("transparent_mode.conf") or readMode("../../transparent_mode.conf") or readMode("../transparent_mode.conf")
    if m ~= nil then return m end
    return nil
end)()

M.isUlw = function() return ulwMode == "ulw" end

-- UpdateLayeredWindow 透明实现（逐像素 alpha）相关类型声明。
-- 注意：必须无条件声明，因为透明模式可能在 create 时才由设置决定（settings 注入路径下 require 时 isUlw() 仍为 false）。
ffi.cdef[[
    typedef struct ULW_POINT { long x; long y; } ULW_POINT;
    typedef struct ULW_SIZE { long cx; long cy; } ULW_SIZE;
    typedef struct ULW_BLEND { unsigned char BlendOp; unsigned char BlendFlags; unsigned char SourceConstantAlpha; unsigned char AlphaFormat; } ULW_BLEND;
    typedef struct ULW_BITMAPINFOHEADER {
        unsigned long biSize; long biWidth; long biHeight;
        unsigned short biPlanes; unsigned short biBitCount;
        unsigned long biCompression; unsigned long biSizeImage;
        long biXPelsPerMeter; long biYPelsPerMeter;
        unsigned long biClrUsed; unsigned long biClrImportant;
    } ULW_BITMAPINFOHEADER;
    typedef struct ULW_BITMAPINFO { ULW_BITMAPINFOHEADER bmiHeader; unsigned long bmiColors[1]; } ULW_BITMAPINFO;
    void* GetDC(void* hWnd);
    int ReleaseDC(void* hWnd, void* hDC);
    void* CreateCompatibleDC(void* hdc);
    void* CreateDIBSection(void* hdc, const ULW_BITMAPINFO* pbmi, unsigned int usage, void** ppvBits, void* hSection, unsigned long offset);
    void* SelectObject(void* hdc, void* h);
    int DeleteObject(void* h);
    int DeleteDC(void* hdc);
    int UpdateLayeredWindow(void* hwnd, void* hdcDst, const ULW_POINT* pptDst, const ULW_SIZE* psize, void* hdcSrc, const ULW_POINT* pptSrc, unsigned long crKey, const ULW_BLEND* pblend, unsigned long dwFlags);
]]

local user32 = ffi.load("user32")
local gdi32 = ffi.load("gdi32")
local kernel32 = ffi.load("kernel32")
local ulwState = nil
local ulwErrCount = 0
local ulwFrameCount = 0
local ulwAlphaMin = 255
local ulwAlphaMax = 0

M.ulwUpdate = function()
    local w = M.windowSize.width
    local h = M.windowSize.height
    if w <= 0 or h <= 0 then return end
    -- 尺寸变化时重建 DIB
    if ulwState == nil or ulwState.w ~= w or ulwState.h ~= h then
        if ulwState ~= nil then
            gdi32.DeleteObject(ulwState.hbmp)
            gdi32.DeleteDC(ulwState.hdcMem)
            user32.ReleaseDC(nil, ulwState.hdcScreen)
        end
        local hdcScreen = user32.GetDC(nil)
        local hdcMem = gdi32.CreateCompatibleDC(hdcScreen)
        local bi = ffi.new("ULW_BITMAPINFO")
        bi.bmiHeader.biSize = ffi.sizeof("ULW_BITMAPINFOHEADER")
        bi.bmiHeader.biWidth = w
        bi.bmiHeader.biHeight = h
        bi.bmiHeader.biPlanes = 1
        bi.bmiHeader.biBitCount = 32
        bi.bmiHeader.biCompression = 0
        local bits = ffi.new("void*[1]")
        local hbmp = gdi32.CreateDIBSection(hdcScreen, bi, 0, bits, nil, 0)
        gdi32.SelectObject(hdcMem, hbmp)
        ulwState = { w = w, h = h, hdcScreen = hdcScreen, hdcMem = hdcMem, hbmp = hbmp, bits = bits[0] }
        log("[window] ulw dib created " .. w .. "x" .. h .. " hbmp=" .. tostring(hbmp) .. "\n")
    end
    -- 读渲染纹理像素（RGBA，bottom-up，与 glGetTexImage 一致；RL_PIXELFORMAT_UNCOMPRESSED_R8G8B8A8 = 7）
    local px = ffi.cast("unsigned char*", rl.rlReadTexturePixels(M.ulwRT.texture.id, w, h, 7))
    if px == nil or px == ffi.NULL then
        log("[window] ulw rlReadTexturePixels returned NULL\n")
        return
    end
    local dst = ffi.cast("unsigned char*", ulwState.bits)
    -- RGBA → BGRA（DIB 32 位为 BGRA 顺序），并统计 alpha 范围
    local total = w * h
    for i = 0, total - 1 do
        local o = i * 4
        local r = px[o]
        local g = px[o + 1]
        local b = px[o + 2]
        local a = px[o + 3]
        dst[o] = b
        dst[o + 1] = g
        dst[o + 2] = r
        dst[o + 3] = a
        if a < ulwAlphaMin then ulwAlphaMin = a end
        if a > ulwAlphaMax then ulwAlphaMax = a end
    end
    rl.MemFree(px)
    -- 窗口位置（屏幕坐标）
    local pos = rl.GetWindowPosition()
    local ppt = ffi.new("ULW_POINT")
    ppt.x = math.floor(pos.x)
    ppt.y = math.floor(pos.y)
    local psize = ffi.new("ULW_SIZE")
    psize.cx = w
    psize.cy = h
    local src0 = ffi.new("ULW_POINT")
    local blend = ffi.new("ULW_BLEND")
    blend.BlendOp = 0      -- AC_SRC_OVER
    blend.SourceConstantAlpha = 255
    blend.AlphaFormat = 1  -- AC_SRC_ALPHA
    local ret = user32.UpdateLayeredWindow(ffi.cast("void*", rl.GetWindowHandle()), nil, ppt, psize, ulwState.hdcMem, src0, 0, blend, 2)
    if ret == 0 and ulwErrCount < 5 then
        ulwErrCount = ulwErrCount + 1
        log("[window] ulw UpdateLayeredWindow failed, GetLastError=" .. tostring(user32.GetLastError()) .. "\n")
    end
    -- 打印 alpha 统计：前 5 帧（启动即诊断该环境渲染纹理是否带 alpha）与之后每 600 帧（约 10 秒）
    ulwFrameCount = ulwFrameCount + 1
    if ulwFrameCount <= 5 or ulwFrameCount % 600 == 0 then
        log("[window] ulw alpha stats frame=" .. ulwFrameCount .. " alphaMin=" .. ulwAlphaMin .. " alphaMax=" .. ulwAlphaMax .. "\n")
    end
end

---@class M.param
---@field vsync boolean
---@field transparent boolean
---@field topmost boolean
---@field autoHide boolean
---@field settings table

---Create a managed window
---@param param M.param
M.create = function(param)
    M.param = param

    rl.SetConfigFlags(rl.FLAG_MSAA_4X_HINT)
    rl.SetConfigFlags(rl.FLAG_WINDOW_UNDECORATED)
    rl.SetConfigFlags(rl.FLAG_WINDOW_ALWAYS_RUN)
    rl.SetConfigFlags(rl.FLAG_WINDOW_RESIZABLE)
    if param.vsync then rl.SetConfigFlags(rl.FLAG_VSYNC_HINT) end
    if param.transparent then
        if M.isUlw() then
            -- ULW 模式：不启用 GLFW 透明/blur-behind，窗口内容完全由 UpdateLayeredWindow 位图提供
            log("[window] transparent mode = ulw (UpdateLayeredWindow)\n")
        else
            rl.SetConfigFlags(rl.FLAG_WINDOW_TRANSPARENT)
            log("[window] transparent mode = dwm (raylib default)\n")
        end
    end
    if param.topmost then rl.SetConfigFlags(rl.FLAG_WINDOW_TOPMOST) end
    rl.InitWindow(400, 300, "HuiDesktop Light Renderer")
    win32.directSetExStyle(0x80180)
    
    -- 窗口创建后立即设置透明度（ULW 模式下由位图 alpha 提供，跳过 LWA_ALPHA，避免破坏 ULW）
    if param.settings and param.settings.transparency and not M.isUlw() then
        win32.setTransparency(param.settings.transparency)
    end

    if not param.culling then rl.rlDisableBackfaceCulling() end -- Normally we do not use backface culling

    if param.settings.x ~= nil and param.settings.y ~= nil then
        rl.SetWindowPosition(param.settings.x, param.settings.y)
    else
        local p = rl.GetWindowPosition()
        param.settings:default({ x = p.x, y = p.y })
    end

    param.settings:default({ fps = 0, drawFps = true })
    if M.isUlw() and (param.settings.fps or 0) <= 0 then
        -- ULW 模式无 SwapBuffers/vsync，默认限帧 60，避免渲染+位图上传空耗
        param.settings.fps = 60
        log("[window] ulw no-vsync, fps defaulted to 60\n")
    end
    M.setFPS(param.settings.fps)

    -- ULW 模式：创建带 alpha 的渲染纹理（默认 framebuffer 无 alpha 通道，读回 alpha 恒 255）
    if M.isUlw() then
        M.ulwRT = rl.LoadRenderTexture(M.windowSize.width, M.windowSize.height)
        log("[window] ulw render texture created " .. M.windowSize.width .. "x" .. M.windowSize.height .. "\n")
    end
end

M.setSize = function(width, height)
    M.windowSize.width = width
    M.windowSize.height = height
    rl.SetWindowSize(width, height)
    -- ULW 模式：渲染纹理随窗口尺寸重建
    if M.isUlw() then
        if M.ulwRT ~= nil then
            rl.UnloadRenderTexture(M.ulwRT)
            M.ulwRT = nil
        end
        M.ulwRT = rl.LoadRenderTexture(width, height)
        log("[window] ulw render texture resized to " .. width .. "x" .. height .. "\n")
    end
end

M.setPosition = function(x, y)
    rl.SetWindowPosition(x, y)
    M.param.settings.x = x
    M.param.settings.y = y
end

M.setFPS = function(fps)
    rl.SetTargetFPS(fps)
end

M.restore = function()
    if rl.IsWindowMinimized() then
        rl.RestoreWindow()
    end
end

M.before_draw = 'window:draw.before'
M.draw = 'window:draw'
M.after_draw = 'window:draw.after'
M.window_closing = 'window:closing'
M.window_closed = 'window:closed'

local transparent = raylib.Color(0, 0, 0, 0)

M.mouseButton = {
    left    = 0,
    right   = 1,
    middle  = 2,
    side    = 3,
    extra   = 4,
    forward = 5,
    back    = 6,
}

M.frameTime = cached(function() return rl.GetFrameTime() end)
M.mousePos = cached(function() return win32.getMousePos() end)
M.windowPos = cached(function() return rl.GetWindowPosition() end)
M.isMouseButtonPressed = function(k) return rl.IsMouseButtonPressed(k) end
M.isMouseButtonDown = function(k) return rl.IsMouseButtonDown(k) end
M.isMouseButtonUp = function(k) return rl.IsMouseButtonUp(k) end

-- auto hide
local hidden = false

M.run = function()
    M.hasHitHead = false
    local frameStart = rl.GetTime()
    while not rl.WindowShouldClose() do
        M.frameTime:reset()
        M.mousePos:reset()
        M.windowPos:reset()

        ev.trigger(M.before_draw)

        if M.isUlw() then
            -- ULW 模式：渲染到带 alpha 的纹理，先清除避免残影叠加，再交给 UpdateLayeredWindow
            rl.BeginTextureMode(M.ulwRT)
            rl.pClearBackground(transparent)
        else
            rl.BeginDrawing()
            rl.pClearBackground(transparent)
        end

        ev.trigger(M.draw)
        if M.param.settings.drawFps then rl.DrawFPS(10, 10) end

        if M.isUlw() then
            rl.EndTextureMode()
            M.ulwUpdate()
            -- 保留 raylib 正常 SwapBuffers 路径：GLFW 窗口消息循环/绘制（点击、WM_PAINT）依赖它；
            -- 显示内容由 UpdateLayeredWindow 位图覆盖，此处仅为维持窗口机制正常
            rl.BeginDrawing()
            rl.pClearBackground(transparent)
            rl.EndDrawing()
            -- ULW 模式无 vsync，raylib 内置限帧不生效；且其 WaitTime 为空操作，用系统 Sleep 限帧
            local fps = M.param.settings.fps
            if fps == nil or fps <= 0 then fps = 60 end
            local elapsed = (rl.GetTime() - frameStart) * 1000.0
            local wait = math.floor(1000.0 / fps - elapsed)
            if wait > 0 then kernel32.Sleep(wait) end
            frameStart = rl.GetTime()
        else
            rl.EndDrawing()
        end

        if M.param.autoHide then
            if hidden ~= win32.isFullscreen() then
                hidden = not hidden
                if hidden then rl.SetWindowState(rl.FLAG_WINDOW_HIDDEN)
                else rl.ClearWindowState(rl.FLAG_WINDOW_HIDDEN) end
            end
        end

        ev.trigger(M.after_draw)
    end

    ev.trigger(M.window_closing)
    rl.CloseWindow()
    ev.trigger(M.window_closed)
end

return M
