local ev = require("eventize")

local _M = {}

---@param p { checkCanStart: fun():boolean ; getMouseStatus: fun():boolean ; getMousePosition: fun():{x: number, y:number} ; getWindowPosition: fun():{x: number, y:number} ; setWindowPosition: fun(x: number, y:number) ; getFrameTime: fun():number ; getReferenceY: fun():number }
_M.create = function(p)
    local M = {}

    M.status = false
    M.moved = true
    M.event_prefix = ev.unique()
    M.dragging = M.event_prefix .. "dragging"
    M.dragged = M.event_prefix .. "dragged"
    M.dropped = M.event_prefix .. "dropped"
    M.clicked = M.event_prefix .. "clicked"
    M.drop = false
    M.ground = 0

    local windowPos = nil
    local startMousePos = nil
    local v = 0
    local dropDown = true
    local dropRev = false
    local g = 1000

    M.trigger = function()
        if M.status then
            local currentMousePos = p.getMousePosition()
            local deltaX = currentMousePos.x - startMousePos.x
            local deltaY = currentMousePos.y - startMousePos.y
            if deltaX ~= 0 or deltaY ~= 0 then
                if not M.moved then
                    M.moved = true
                    ev.trigger(M.dragging)
                end
                windowPos.x = windowPos.x + deltaX
                windowPos.y = windowPos.y + deltaY
                p.setWindowPosition(windowPos.x, windowPos.y)
            end
            if not p.getMouseStatus() then
                M.status = false
                ev.trigger(M.moved and M.dragged or M.clicked)
                if M.moved and M.drop and windowPos.x ~= M.ground then
                    dropDown = windowPos.y + p.getReferenceY() < M.ground
                    dropRev = false
                    v = 0
                else M.drop = false end
            end
        elseif p.checkCanStart() and p.getMouseStatus() then
            M.status = true
            M.moved = false
            M.drop = false
            windowPos = p.getWindowPosition()
            startMousePos = p.getMousePosition()
        elseif M.drop then
            if dropRev then
                v = v - g * p.getFrameTime()
                if v < 0 then
                    v = 0
                    dropRev = false
                else
                    windowPos.y = windowPos.y - (v + g * p.getFrameTime() / 2) * p.getFrameTime()
                end
            else
                windowPos.y = windowPos.y + (v + g * p.getFrameTime() / 2) * p.getFrameTime() * (dropDown and 1 or -1)
                v = v + g * p.getFrameTime()
                if (windowPos.y + p.getReferenceY() < M.ground) ~= dropDown then
                    windowPos.y = M.ground - p.getReferenceY()
                    if dropDown and v > 100 then
                        dropRev = true
                        v = v * 0.5
                    else
                        M.drop = false
                        ev.trigger(M.dropped)
                        return
                    end
                end
            end
            p.setWindowPosition(windowPos.x, windowPos.y)
        end
    end

    return M
end

local req = require

_M.createWithWindowDefault = function (p)
    local window = req("blockly_window")
    return _M.create{
        checkCanStart = p.checkCanStart,
        getMouseStatus = function() return window.isMouseButtonDown(p.key) end,
        getMousePosition = window.mousePos,
        getWindowPosition = window.windowPos,
        setWindowPosition = window.setPosition,
        getFrameTime = window.frameTime,
        getReferenceY = function () return p.model.skeleton.y end
    }
end

return _M
