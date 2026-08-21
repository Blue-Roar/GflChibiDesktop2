local ev = require("eventize")
local window = require("blockly_window")
local win32 = require("win32")

local storedHit = true -- 初始置 true，使首帧强制设置点击穿透（storedHit ~= hit 触发 setTransparent(false)）
local hit = false

ev.on(window.before_draw, function()
    hit = false
end)

ev.on(window.after_draw, function()
    if storedHit ~= hit then
        storedHit = hit
        win32.setTransparent(storedHit)
    end
end)

return {
    hit = function ()
        hit = true
    end,
    isHit = function ()
        return hit
    end
}
