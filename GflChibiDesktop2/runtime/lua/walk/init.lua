local ev = require("eventize")

local function getHorizontalDistance(p1, p2)
    return p1.x - p2.x
end

local _M = {}

---@param p { checkCanStart: fun():boolean ; getMousePosition: fun():{x: number, y:number} ; getReferencePosition: fun():{x: number, y:number} ; getFrameTime: fun():number ; getPositionChanger: fun():fun(dx: number, dy:number) ; startDistance: number, stopDistance: number, walkSpeed: number }
_M.create = function(p)
    local M = {}
    M.event_prefix = ev.unique()
    M.walking = M.event_prefix .. 'walking'
    M.walked = M.event_prefix .. 'walked'
    M.directionChanged = M.event_prefix .. 'directionChanged'

    M.startDistance = p.startDistance
    M.stopDistance = p.stopDistance
    M.status = false
    M.direction = -1

    local remaining = 0
    local changer = nil

    M.trigger = function()
        local distance = getHorizontalDistance(p.getMousePosition(), p.getReferencePosition())
        if M.status then
            if math.abs(distance) < M.stopDistance then
                M.status = false
                ev.trigger(M.walked)
            else
                local d = distance > 0 and 1 or -1
                remaining = remaining + p.walkSpeed * p.getFrameTime()
                local thisWalk = math.floor(remaining)
                remaining = remaining - thisWalk
                if M.direction ~= d then
                    M.status = false
                    ev.trigger(M.walked)
                else
                    changer(thisWalk * d, 0)
                end
            end
        else
            if p.checkCanStart() and math.abs(distance) > M.startDistance then
                M.status = true
                remaining = 0
                changer = p.getPositionChanger()
                M.direction = distance > 0 and 1 or -1
                ev.trigger(M.walking)
            end
        end
    end

    return M
end

local req = require

_M.createWithWindowDefault = function(p)
    local window = req("blockly_window")
    return _M.create {
        getMousePosition = window.mousePos,
        getFrameTime = window.frameTime,
        getPositionChanger = function()
            return function(dx, dy)
                window.setPosition(window.windowPos().x + dx, window.windowPos().y + dy)
            end
        end,
        checkCanStart = p.checkCanStart,
        getReferencePosition = p.model.getRawPosition,
        startDistance = p.startDistance,
        stopDistance = p.stopDistance,
        walkSpeed = p.walkSpeed
    }
end

return _M
