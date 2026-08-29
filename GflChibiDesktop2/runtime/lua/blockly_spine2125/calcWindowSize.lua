local function ret(ffi, sp, state, stateData, skeleton)
    stateData.defaultMix = 0
    local newRect = ffi.new("Rectangle")
    local rect = nil
    -- 单动画最大采样步数（240Hz 下约 50 秒）：防止超长/病态动画导致启动卡死
    local MAX_STEPS = 12000
    for i = 0, skeleton.data.animationsCount - 1 do
        local r = sp.spAnimationState_setAnimation(state, 0, skeleton.data.animations[i], false)
        -- 以动画时长决定采样步数；用步数上限代替浮点相等的 while 循环，避免死循环
        local dur = tonumber(r.endTime)
        if dur == nil or dur < 0 then dur = 0 end
        local steps = math.min(math.ceil(dur * 240) + 2, MAX_STEPS)
        for _ = 1, steps do
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
    if rect == nil then
        sp.spAnimationState_apply(state, skeleton)
        sp.spSkeleton_updateWorldTransform(skeleton)
        sp.spSkeleton_getAabbBox(skeleton, newRect)
        rect = newRect
    end
    rect.x = rect.x * -1
    rect.y = rect.y * -1
    return rect
end

return ret
