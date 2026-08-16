local ev = require("eventize")
local window = require("blockly_window")
local win32 = require("win32")

local storedHit = false
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
