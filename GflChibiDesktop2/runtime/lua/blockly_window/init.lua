local raylib = require("raylib")
local win32 = require("win32")
local cached = require("cached")
local ev = require("eventize")

local rl = raylib.lib
raylib = raylib.struct

local M = {}
M.windowSize = { width = 400, height = 300 }

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
    if param.transparent then rl.SetConfigFlags(rl.FLAG_WINDOW_TRANSPARENT) end
    if param.topmost then rl.SetConfigFlags(rl.FLAG_WINDOW_TOPMOST) end
    rl.InitWindow(400, 300, "HuiDesktop Light Renderer")
    win32.directSetExStyle(0x80180)

    if not param.culling then rl.rlDisableBackfaceCulling() end -- Normally we do not use backface culling

    if param.settings.x ~= nil and param.settings.y ~= nil then
        rl.SetWindowPosition(param.settings.x, param.settings.y)
    else
        local p = rl.GetWindowPosition()
        param.settings:default({ x = p.x, y = p.y })
    end

    param.settings:default({ fps = 0, drawFps = true })
    M.setFPS(param.settings.fps)
end

M.setSize = function(width, height)
    M.windowSize.width = width
    M.windowSize.height = height
    rl.SetWindowSize(width, height)
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
    while not rl.WindowShouldClose() do
        M.frameTime:reset()
        M.mousePos:reset()
        M.windowPos:reset()

        ev.trigger(M.before_draw)

        rl.BeginDrawing()
        rl.pClearBackground(transparent)

        ev.trigger(M.draw)
        if M.param.settings.drawFps then rl.DrawFPS(10, 10) end

        rl.EndDrawing()

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
