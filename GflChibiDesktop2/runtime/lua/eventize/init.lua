local M = {}
local events = {}
local event_start = {}
local unique_now = 0

M.create_if = function(name)
    if events[name] == nil then
        event_start[name] = {
            tag = 'start',
            next = nil
        }
        events[name] = {
            tag = 'end',
            prev = event_start[name],
            next = nil,
            fun = function() end
        }
        events[name].prev.next = events[name]
    end
end

M.add_fun = function(name)
    M.create_if(name)
    local cur = events[name]
    return function(fun)
        cur.prev = {
            next = cur,
            prev = cur.prev,
            fun = fun,
        }
        cur.prev.prev.next = cur.prev
        return cur.prev
    end
end

M.on = function(name, fun)
    M.create_if(name)
    local cur = events[name]
    cur.prev = {
        next = cur,
        prev = cur.prev,
        fun = fun,
    }
    cur.prev.prev.next = cur.prev
    return cur.prev
end

M.trigger = function(name, ...)
    local cur = event_start[name]
    if cur ~= nil then
        while cur.next ~= nil do
            cur = cur.next
            cur.fun(...)
        end
    end
end

M.unique = function()
    unique_now = unique_now + 1
    return 'uniq.' .. tostring(unique_now) .. ':'
end

M.init = 'huidesktop:init'
M.loop = 'huidesktop:loop'

return M
