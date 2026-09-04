--[[
    动态画布尺寸采样：遍历全部动画，按帧逐附件累加世界顶点包围盒（与 legacy CanvasCalculator 一致）。
    依赖 spine2125.cdef 已暴露的 spSkeleton/槽位/附件 computeWorldVertices 符号。
    调用约定：ret(ffi, sp, animationState, animationStateData, skeleton)
    返回：{x=-minX, y=-minY, width, height}（取负转画布偏移；宽已按骨架原点对称扩展交由 init.lua 处理）。
    全程在 pcall 内运行：若逐附件遍历因 FFI 结构/符号异常，回退到 spSkeleton_getAabbBox。
]]
local ffi = require("ffi")

-- 附件类型（与 cdef.lua 的 spAttachmentType 枚举一致）
local T_REGION, T_BOUNDING, T_MESH, T_SKINNED = 0, 1, 2, 3

local function ret(_, sp, state, stateData, skeleton)
    local savedMix = stateData.defaultMix
    stateData.defaultMix = 0

    local ok, res = pcall(function()
        -- 遍历当前姿态全部 slot（drawOrder）的可渲染附件，累加世界顶点包围盒。
        -- 与 legacy CanvasCalculator.AccumulateBounds 同构：region=8 顶点，mesh/skinned 全顶点，
        -- 不含 bounding box（命中附件非渲染体）。
        local function accumulateBounds(out)
            local minX, minY, maxX, maxY = math.huge, math.huge, -math.huge, -math.huge
            local found = false
            local drawOrder = skeleton.drawOrder
            local count = tonumber(skeleton.slotsCount)
            for i = 0, count - 1 do
                local slot = drawOrder[i]
                if slot ~= nil and slot ~= ffi.NULL then
                    local attach = slot.attachment
                    if attach ~= nil and attach ~= ffi.NULL then
                        local typ = attach.type
                        if typ == T_REGION then
                            local reg = ffi.cast("spRegionAttachment*", attach)
                            local buf = ffi.new("float[8]")
                            sp.spRegionAttachment_computeWorldVertices(reg, slot.bone, buf)
                            for v = 0, 3 do
                                local x, y = buf[v * 2], buf[v * 2 + 1]
                                if x < minX then minX = x end
                                if y < minY then minY = y end
                                if x > maxX then maxX = x end
                                if y > maxY then maxY = y end
                            end
                            found = true
                        elseif typ == T_MESH then
                            local m = ffi.cast("spMeshAttachment*", attach)
                            local n = tonumber(m.verticesCount)
                            if n > 0 then
                                local buf = ffi.new("float[?]", n)
                                sp.spMeshAttachment_computeWorldVertices(m, slot, buf)
                                local h = math.floor(n / 2) - 1
                                for v = 0, h do
                                    local x, y = buf[v * 2], buf[v * 2 + 1]
                                    if x < minX then minX = x end
                                    if y < minY then minY = y end
                                    if x > maxX then maxX = x end
                                    if y > maxY then maxY = y end
                                end
                                found = true
                            end
                        elseif typ == T_SKINNED then
                            local m = ffi.cast("spSkinnedMeshAttachment*", attach)
                            -- 加权网格世界顶点数 = uvsCount（每顶点一对 uv），与 legacy 用 UVs.Length 一致
                            local n = tonumber(m.uvsCount)
                            if n > 0 then
                                local buf = ffi.new("float[?]", n)
                                sp.spSkinnedMeshAttachment_computeWorldVertices(m, slot, buf)
                                local h = math.floor(n / 2) - 1
                                for v = 0, h do
                                    local x, y = buf[v * 2], buf[v * 2 + 1]
                                    if x < minX then minX = x end
                                    if y < minY then minY = y end
                                    if x > maxX then maxX = x end
                                    if y > maxY then maxY = y end
                                end
                                found = true
                            end
                        end
                        -- SP_ATTACHMENT_BOUNDING_BOX / 其它：非渲染体，跳过（与 legacy 一致）
                    end
                end
            end
            if found then
                out[0] = minX
                out[1] = minY
                out[2] = maxX
                out[3] = maxY
                return true
            end
            return false
        end

        local rect = nil   -- 已对称化说明：x=-minX,width; y=-minY,height 留给 init.lua
        local minX, minY, maxX, maxY = 0, 0, 0, 0
        -- setup pose（静止/待机姿态）也纳入，覆盖只在静止出现的最高点（发饰/武器）
        local setupOk = pcall(function()
            sp.spSkeleton_setToSetupPose(skeleton)
            sp.spSkeleton_updateWorldTransform(skeleton)
            local b = ffi.new("float[4]")
            if accumulateBounds(b) then
                minX, minY, maxX, maxY = b[0], b[1], b[2], b[3]
                rect = { x = minX, y = minY, width = maxX - minX, height = maxY - minY }
            end
        end)
        if not setupOk then rect = nil end

        -- 单动画最大采样步数（240Hz 下约 50 秒）：防止超长/病态动画导致启动卡死
        local MAX_STEPS = 12000
        for i = 0, tonumber(skeleton.data.animationsCount) - 1 do
            -- 独立采样前复位姿势，保证范围准确（与 legacy CanvasCalculator 一致）
            local animOk = pcall(function()
                sp.spSkeleton_setToSetupPose(skeleton)
                local r = sp.spAnimationState_setAnimation(state, 0, skeleton.data.animations[i], false)
                local dur = tonumber(r.endTime)
                if dur == nil or dur < 0 then dur = 0 end
                local steps = math.min(math.ceil(dur * 240) + 2, MAX_STEPS)
                for _ = 1, steps do
                    sp.spAnimationState_update(state, 1 / 240)
                    sp.spAnimationState_apply(state, skeleton)
                    sp.spSkeleton_updateWorldTransform(skeleton)
                    local b = ffi.new("float[4]")
                    if accumulateBounds(b) then
                        local bx, by = b[0], b[1]
                        local bx2, by2 = b[2], b[3]
                        if rect == nil then
                            minX, minY, maxX, maxY = bx, by, bx2, by2
                            rect = { x = bx, y = by, width = bx2 - bx, height = by2 - by }
                        else
                            if bx < minX then minX = bx end
                            if by < minY then minY = by end
                            if bx2 > maxX then maxX = bx2 end
                            if by2 > maxY then maxY = by2 end
                            rect.x = minX
                            rect.y = minY
                            rect.width = maxX - minX
                            rect.height = maxY - minY
                        end
                    end
                end
            end)
            if not animOk then
                -- 单个动画采样异常：跳过（rect 保留前面累加结果）
            end
        end

        if rect == nil then
            -- 全部动画均无可渲染附件：回退到 getAabbBox
            local fb = ffi.new("Rectangle")
            sp.spSkeleton_getAabbBox(skeleton, fb)
            rect = { x = fb.x, y = fb.y, width = fb.width, height = fb.height }
        end

        -- 取负转画布偏移（minX/minY 相对骨架原点）：r.x=-minX, r.y=-minY
        rect.x = rect.x * -1
        rect.y = rect.y * -1
        return rect
    end)

    stateData.defaultMix = savedMix   -- 恢复默认过渡时长（采样用 0 加快）
    if not ok then error(res) end
    return res
end

return ret
