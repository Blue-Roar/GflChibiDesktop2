local ffi = require 'ffi'

local packageStack = {{'.', {}}}
local packageTop = 1
local sharedModules = {}

local stderr = io.stderr
_G['log'] = function(x)
    stderr:write(tostring(x))
    stderr:write('\n')
end

--[[
    package stack:
    [{
        1: path(to load module relatively)
        2: local modules(one shared module use this)
        3: shared checker(function, f(name) = true refers to shared module)
    }, ...]

    notice that shared modules has a unique instance
]]

local function returnFalse() return false end

local req = require

local _M = {}

_M.genRequire = function(checkGlobal, path)
    local localLibs = {}
    return function(name)
        if checkGlobal(name) then return _M.reqShared(name)
        else
            if localLibs[name] == nil then
                local f, err = loadfile(path .. "/" .. name .. ".lua")
                if err ~= nil then error(err) end
                if f == nil then error('unknown error') end
                localLibs[name] = f()
            end
            return localLibs[name]
        end
    end
end

local function push(path)
    local f = loadfile(path .. "/hdtmodule.lua")
    require = _M.genRequire(type(f) == "function" and f() or returnFalse, path)
    _G['hdtLoadFFI'] = function(filename) return ffi.load(path .. '/' .. filename) end
end

_M.reqShared = function(name)
    if sharedModules[name] == nil then
        local f, err = loadfile("lua/" .. name .. '/init.lua')
        if err ~= nil then
            sharedModules[name] = req(name)
        elseif f == nil then
            error('unknown error')
        else
            local lastReq = require
            local lastFFI = _G['hdtLoadFFI']
            push("lua/" .. name)
            log(string.format('model %s require = %s', name, tostring(require)))
            sharedModules[name] = f()
            require = lastReq
            _G['hdtLoadFFI'] = lastFFI
        end
    end
    return sharedModules[name]
end

_M.reqLocal = function(name)
    if packageStack[packageTop][2][name] == nil then
        local f, err = loadfile(packageStack[packageTop][1] .. "/" .. name ..
                                    ".lua")
        if err ~= nil then error(err) end
        if f == nil then error('unknown error') end
        packageStack[packageTop][2][name] = f()
    end
    return packageStack[packageTop][2][name]

end

require = _M.genRequire(dofile("hdtmodule.lua"), '.')
