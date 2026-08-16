local ev = require("eventize")

local ffi = require "ffi"
---@type any
local ipc = require "cdef"

local pintType = ffi.typeof("int32_t*")
local puint8Type = ffi.typeof("uint8_t*")

local M = { lib = ipc }

M.writeInt32 = function (inst, value, no_ensure)
    if not no_ensure then ipc.hiMQ_ensure(inst, 4) end
    local c = ffi.cast(pintType, inst.current)
    c[0] = value
    inst.current = c + 1;
end

M.writeInt8 = function (inst, value, no_ensure)
    if not no_ensure then ipc.hiMQ_ensure(inst, 1) end
    local c = ffi.cast(puint8Type, inst.current)
    c[0] = value
    inst.current = c + 1;
end

M.writeString = function (inst, value)
    local len = #value
    ipc.hiMQ_ensure(inst, 5 + len)
    M.writeInt32(inst, #value + 1, true)
    for i = 1, #value do M.writeInt8(inst, string.byte(value, i), true) end
    M.writeInt8(inst, 0, true)
end

M.wrote = function (inst, bell)
    ipc.hiMQ_end(inst, 0, bell and 1 or 0)
end

M.wroteL = function (inst, len, bell)
    ipc.hiMQ_end(inst, len, bell and 1 or 0)
end

M.readInt32 = function (inst)
    local c = ffi.cast(pintType, inst.current)
    local v = c[0]
    inst.current = c + 1;
    return v
end

M.after_read = 'ipc:read.after'

--[[
    数字：
    { type = 'single', valueType = 'number', prompt = '', hint = '', min = 0, max = 100 }
    布尔：
    { type = 'bool', prompt = '', hint = '' }
    文本：
    { type = 'readonly', text = '' }
    按钮：
    { type = 'button', prompt = '', hint = '' }
]]

M.getV = function(param)
    if param.type == 'single' then
        if param.valueType == 'number' then
            return
            function(r)
                return function(inst)
                    local v = M.readInt32(inst)
                    if param.max ~= nil and v > param.max then r(param.max) end
                    if param.min ~= nil and v < param.min then r(param.min) end
                    r(v)
                end
            end,
            function(r)
                return function(inst, wrote)
                    ipc.hiMQ_begin(inst)
                    M.writeInt32(inst, 1) -- single line
                    M.writeString(inst, param.prompt) -- prompt text
                    M.writeString(inst, param.hint) -- hint text
                    M.writeInt32(inst, 1) -- number input only
                    M.writeInt32(inst, r()) -- default value
                    M.wrote(inst, wrote)
                end
            end
        end
    elseif param.type == 'bool' then return
        function(r)
            return function(inst)
                r(M.readInt32(inst) ~= 0)
            end
        end,
        function (r)
            return function(inst, wrote)
                ipc.hiMQ_begin(inst)
                M.writeInt32(inst, 2) -- bool choice
                M.writeString(inst, param.prompt) -- prompt text
                M.writeString(inst, param.hint) -- hint text
                M.writeInt32(inst, r() and 1 or 0) -- choice
                M.wrote(inst, wrote)
            end
        end
    elseif param.type == 'readonly' then return
        function(r)
            return function(inst)
                log('Impossible @ ipc/init.lua')
            end
        end,
        function (r)
            return function(inst, wrote)
                ipc.hiMQ_begin(inst)
                M.writeInt32(inst, 3) -- readonly text
                M.writeString(inst, param.text) -- text
                M.wrote(inst, wrote)
            end
        end
    elseif param.type == 'button' then return
        function(r)
            return function(inst)
                r()
            end
        end,
        function (r)
            return function(inst, wrote)
                ipc.hiMQ_begin(inst)
                M.writeInt32(inst, 4) -- button
                M.writeString(inst, param.prompt) -- prompt text
                M.writeString(inst, param.hint) -- hint text
                M.wrote(inst, wrote)
            end
        end
    end
end

M.panel = {}

---添加面板项目
---@param param any 参数
---@param gv function 读取之后的操作
---@param sv function 获取值的操作
M.addPanelItem = function(param, gv, sv)
    local gvw, svw;
    gvw, svw = M.getV(param)
    table.insert(M.panel, { param = param, gv = gvw(gv), sv = svw(sv)})
end

M.resetPanel=function(inst, wrote)
    if wrote == nil then wrote = true end
    ipc.hiMQ_begin(inst)
    M.writeInt32(inst, 0)
    M.wrote(inst, wrote)
end

M.sendPanelStructure = function(inst)
    M.resetPanel(inst, false)
    for i = 1, #M.panel do
        M.panel[i].sv(inst, i == #M.panel)
    end
end

M.read = function (rxInst, txInst)
    if rxInst ~= nil and ipc.hiMQ_get(rxInst) ~= 0 then
        repeat
            local id = M.readInt32(rxInst)
            if id == 1 then -- reserved
                -- skeleton.scaleX = -skeleton.scaleX
            elseif id == 2 then -- panel
                local itemId = M.readInt32(rxInst)
                while itemId > 0 do
                    M.panel[itemId].gv(rxInst)
                    itemId = M.readInt32(rxInst)
                end
                ev.trigger(M.after_read)
                M.sendPanelStructure(txInst)
            elseif id == 3 then
                M.sendPanelStructure(txInst)
            end
        until (ipc.hiMQ_next(rxInst))
    end
end

return M
