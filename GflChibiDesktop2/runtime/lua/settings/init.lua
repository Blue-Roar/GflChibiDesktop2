local json = require("json")

M = {
    global = nil,
    makeDefault = function(src, default)
        for k, v in pairs(default) do
            if src[k] == nil then
                src[k] = v
            end
        end
    end
}

M.load = function(filename, globalize)
    local file = io.open(filename, "r")
    ---@type any
    local r = {}
    if file and type(file) ~= "string" then r = json.decode(file:read('*a')) file:close() end
    r = r or {}
    local metatable = {}
    metatable.__index = {
        save = function (self)
            file = io.open(filename, "w")
            if file and type(file) ~= "string" then file:write(json.encode(r)) file:close()
            else log("[ERROR] Cannot save settings file: "..filename) end
        end,
        access = function (self, item)
            if type(self[item]) ~= "table" then self[item] = {} end
            if getmetatable(self[item] ~= nil) then return self[item] end
            setmetatable(self[item], metatable)
            return self[item]
        end,
        default = function(self, default)
            for k, v in pairs(default) do
                if self[k] == nil then
                    self[k] = v
                end
            end
        end
    }
    setmetatable(r, metatable)
    if globalize then M.global = r end
    return r
end

return M
