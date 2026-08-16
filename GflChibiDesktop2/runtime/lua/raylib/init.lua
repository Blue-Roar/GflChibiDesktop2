local ffi = require "ffi"
---@type any
local raylib = require "cdef"
return { lib = raylib, struct = {
    Vector2 = function(x, y)
        local r = ffi.new("Vector2")
        r.x = x
        r.y = y
        return r
    end,
    Vector3 = function(x, y, z)
        local r = ffi.new("Vector3")
        r.x = x
        r.y = y
        r.z = z
        return r
    end,
    Camera3D = function(position, target, up, fovy, projection)
        local r = ffi.new("Camera3D")
        r.position = position
        r.target = target
        r.up = up
        r.fovy = fovy
        r.projection = projection
        return r
    end,
    Color = function(r, g, b, a)
        local ret = ffi.new("Color")
        ret.r = r
        ret.g = g
        ret.b = b
        ret.a = a
        return ret
    end,
    Camera2D = function(offset, target, rotation, zoom)
        local r = ffi.new("Camera2D")
        r.offset = offset
        r.target = target
        r.rotation = rotation
        r.zoom = zoom
        return r
    end
}}