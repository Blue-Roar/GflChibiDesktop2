local null = {}

return function(getter)
    local v = {
        val = null,
        getter = getter,
    }
    setmetatable(v, {
        __call = function(table)
            if table.val == null then table.val = table.getter() end
            return table.val
        end,
        __index = {
            reset = function (table)
                table.val = null
            end
        },
    })
    return v
end
