local ffi = require("ffi")
local raylib = require("raylib").lib
local C = require("cdef")

local M = {}

local handleType = ffi.typeof("uint32_t")

-- Check is something fullscreen
M.isFullscreen = (function()
    local rcApp = ffi.new("RECT")
    local rcDesktop = ffi.new("RECT")
    return function()
        local hwndApp = C.GetForegroundWindow()
        local hwndDesktop = C.GetDesktopWindow()
        C.GetWindowRect(hwndApp, rcApp)
        C.GetWindowRect(hwndDesktop, rcDesktop)
        if hwndApp ~= ffi.cast(handleType, raylib.GetWindowHandle()) and hwndApp ~= hwndDesktop and hwndApp ~= C.GetShellWindow() then
            local s = ffi.new("char[1024]")
            local hwndParent = hwndApp
            while ((hwndParent ~= hwndDesktop) and (hwndParent ~= 0)) do
                C.GetClassNameA(hwndParent, s, 1023)
                if C.strcmp(s, "WorkerW") == 0 then return false end
                hwndParent = C.GetParent(hwndParent)
            end
            return
                rcApp.left <= rcDesktop.left and rcApp.top <= rcDesktop.top and
                    rcApp.right >= rcDesktop.right and rcApp.bottom >=
                    rcDesktop.bottom
        end
        return false
    end
end)()

M.directGetExStyle = function()
    return C.GetWindowLongW(ffi.cast(handleType, raylib.GetWindowHandle()), -20)
end

M.directSetExStyle = function(exStyle)
    return C.SetWindowLongW(ffi.cast(handleType, raylib.GetWindowHandle()), -20, exStyle)
end

-- 设置扩展样式后让 Shell 按新样式重建窗口边框/任务栏按钮。
-- 仅 SetWindowLongW 改样式不够：部分系统（Win7、部分 Win10）需先隐藏再显示窗口，
-- Shell 才会移除已按旧样式创建的任务栏按钮；SWP_FRAMECHANGED 一并触发边框重算。
M.refreshExStyle = function()
    local hwnd = ffi.cast(handleType, raylib.GetWindowHandle())
    -- SWP_NOSIZE=0x1 SWP_NOMOVE=0x2 SWP_NOZORDER=0x4 SWP_NOACTIVATE=0x10 SWP_FRAMECHANGED=0x20
    C.SetWindowPos(hwnd, 0, 0, 0, 0, 0, 0x1 + 0x2 + 0x4 + 0x10 + 0x20)
    -- 隐藏再显示（不激活），强制任务栏按新的 TOOLWINDOW 样式重建按钮
    C.ShowWindow(hwnd, 0)      -- SW_HIDE
    C.ShowWindow(hwnd, 8)      -- SW_SHOWNA
end

M.setTransparent = function(enabled)
    local exStyle = M.directGetExStyle()
    if (bit.band(exStyle, 0x20) == 0) ~= enabled then -- WS_EX_TRANSPARENT 0x20L
        exStyle = bit.bxor(exStyle, 0x20)
        M.directSetExStyle(exStyle)
    end
end

M.getMousePos = (function()
    local mp = ffi.new("POINT")
    local wp = ffi.new("Vector2")
    return function()
        raylib.pGetWindowPosition(wp)
        C.GetCursorPos(mp)
        return {x = mp.x - wp.x, y = mp.y - wp.y}
    end
end)()

M.setDesktopParent = function()
    local programIntPtr = C.FindWindowA("Progman", "Program Manager")
    if programIntPtr ~= 0 then
        C.SendMessageTimeoutA(programIntPtr, 0x52c, ffi.NULL, ffi.NULL, 0, 1000, ffi.NULL);
        local p = 0
        repeat
            p = C.FindWindowExA(0, p, "WorkerW", ffi.NULL);
            if p ~= 0 then
                if 0 ~= C.FindWindowExA(p, 0, "SHELLDLL_DefView", ffi.NULL) then
                    C.ShowWindow(C.FindWindowExA(0, p, "WorkerW", ffi.NULL), 0);
                end
            end
        until (p == 0)
        C.SetParent(raylib.GetWindowHandle(), programIntPtr);
        raylib.MaximizeWindow()
    end
end

M.getGround = function()
    local rect = ffi.new('RECT')
    C.SystemParametersInfoA(0x30, 0, rect, 0)
    return rect.bottom
end

M.getWorkArea = function()
    local rect = ffi.new('RECT')
    C.SystemParametersInfoA(0x30, 0, rect, 0)
    return { left = rect.left, top = rect.top, right = rect.right, bottom = rect.bottom }
end

M.setTransparency = function(v)
    C.SetLayeredWindowAttributes(raylib.GetWindowHandle(), 0, v, 2)
end

-- 检查系统是否支持透明窗口
M.isTransparencySupported = function()
    local success, result = pcall(function()
        local hwnd = raylib.GetWindowHandle()
        if hwnd == 0 then return false end
        
        -- 检查是否支持WS_EX_LAYERED
        local exStyle = C.GetWindowLongW(ffi.cast(handleType, hwnd), -20)
        if bit.band(exStyle, 0x80000) == 0 then return false end
        
        -- 尝试设置透明度测试
        local original = C.GetLayeredWindowAttributes(hwnd, ffi.new("DWORD"), ffi.new("BYTE"), ffi.new("DWORD"))
        if original == 0 then return false end
        
        return true
    end)
    
    if not success then
        log("[ERROR] 检查透明支持失败: " .. tostring(result))
        return false
    end
    
    return result
end

-- 获取当前GPU信息
M.getGPUInfo = function()
    local info = {}
    local success = pcall(function()
        local hwnd = raylib.GetWindowHandle()
        
        if hwnd ~= 0 then
            -- 获取设备上下文
            local hdc = C.GetDC(hwnd)
            if hdc ~= 0 then
                -- 获取设备信息
                local devInfo = ffi.new("DEVMODEA[1]")
                if C.EnumDisplaySettingsA(nil, -1, devInfo) ~= 0 then
                    info.colorBits = devInfo[0].dmBitsPerPel
                    info.width = devInfo[0].dmPelsWidth
                    info.height = devInfo[0].dmPelsHeight
                    info.refreshRate = devInfo[0].dmDisplayFrequency
                end
                C.ReleaseDC(hwnd, hdc)
            end
        end
    end)
    
    if not success then
        log("[ERROR] 获取GPU信息失败")
        return nil
    end
    
    return info
end

return M
