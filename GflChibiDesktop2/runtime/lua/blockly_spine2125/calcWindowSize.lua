local function ret(ffi, sp, state, stateData, skeleton)
    stateData.defaultMix = 0
    local newRect = ffi.new("Rectangle")
    local rect = nil
    for i = 0, skeleton.data.animationsCount - 1 do
        local r = sp.spAnimationState_setAnimation(state, 0, skeleton.data.animations[i], false)
        while r.endTime ~= r.lastTime do
            sp.spAnimationState_update(state, 1 / 240)
            sp.spAnimationState_apply(state, skeleton)
            sp.spSkeleton_updateWorldTransform(skeleton)
            sp.spSkeleton_getAabbBox(skeleton, newRect)
            if rect ~= nil then
                if rect.x > newRect.x then
                    rect.width = rect.width + rect.x - newRect.x
                    rect.x = newRect.x
                end
                if rect.y > newRect.y then
                    rect.height = rect.height + rect.y - newRect.y
                    rect.y = newRect.y
                end
                if rect.x + rect.width < newRect.x + newRect.width then
                    rect.width = newRect.x + newRect.width - rect.x
                end
                if rect.y + rect.height < newRect.y + newRect.height then
                    rect.height = newRect.y + newRect.height - rect.y
                end
            else
                rect = newRect
                newRect = ffi.new("Rectangle")
            end
        end
    end
    rect.x = rect.x * -1
    rect.y = rect.y * -1
    return rect
end

return ret
