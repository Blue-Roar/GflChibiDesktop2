--[[
    HuiDesktop Light Spine封装

    事件说明：
    draw被调用后：
        1. 触发before_draw
        2. 绘制skeleton并更新点击测试结果
        3. 触发spine事件: start, interrupt, end, complete, dispose, event
        4. 触发after_draw
]]
local ffi = require("ffi")
local raylib = require("raylib")
local ipc = require("ipc")
local sp = require('spine2125').lib
local calcWindowSize = require('calcWindowSize')
local windowMan = require("blockly_window")
local ev = require("eventize")
local hit_collector = require("hit_collector")
raylib = raylib.lib
ipc = ipc.lib

local eventRecorderAtomPointType = ffi.typeof("eventRecorderAtom*")

sp.spBone_setYDown(true)

local _M = {}

_M.create = function (p, modelConfig)
    local M = {}
    local atlas = sp.spAtlas_createFromFile(modelConfig.atlas, ffi.NULL)
    local skeletonData = nil
    local scale = 1
    
    if modelConfig.type == 'json' then
        local skelFile = sp.spSkeletonJson_create(atlas)
        skeletonData = sp.spSkeletonJson_readSkeletonDataFile(skelFile, modelConfig.skeleton)
        if skeletonData == ffi.NULL then
            log(skelFile.error)
            sp.spSkeletonJson_dispose(skelFile)
            log("ERROR!")
        end
    else
        local skelFile = sp.spSkeletonBinary_create(atlas)
            skeletonData = sp.spSkeletonBinary_readSkeletonDataFile(skelFile, modelConfig.skeleton)
        if skeletonData == ffi.NULL then
            log(skelFile.error)
            sp.spSkeletonBinary_dispose(skelFile)
            log("ERROR!")
        end
    end

    local skeleton = sp.spSkeleton_create(skeletonData)
    local animationStateData = sp.spAnimationStateData_create(skeletonData)
    local animationState = sp.spAnimationState_create(animationStateData)

    -- 画布模式：0=小(448x448)、1=大(768x768)、2=动态（按全部动画并集包围盒，conf 缓存）
    -- 由 main.lua 的 settings.canvasMode 传入（默认动态）。
    local canvasMode = p.canvasMode or 2

    ---按画布模式设置 modelConfig 的 x/y/w/h（未缩放基础）。
    ---动态模式：conf 已有则沿用（避免每次启动重算包围盒卡顿），缺失或 force 时采样计算并写回；
    ---固定模式（小/大）：画布固定、模型偏移 = 画布中心，不使用/不修改 conf。
    ---force=true 用于运行时切回动态：忽略内存/conf 缓存直接重算（模型未变，结果与 conf 一致）。
    local function applyCanvasMode(mode, force)
        local recomputed = false
        if mode == 0 then
            modelConfig.x, modelConfig.y, modelConfig.w, modelConfig.h = 224, 224, 448, 448
        elseif mode == 1 then
            modelConfig.x, modelConfig.y, modelConfig.w, modelConfig.h = 384, 384, 768, 768
        elseif force or modelConfig.x == nil or modelConfig.y == nil or modelConfig.w == nil or modelConfig.h == nil then
            -- 动态：计算实际动画包围盒并写回。部分模型（如完整立绘）动画范围超出默认 448 画布会被裁切，
            -- 按实际包围盒向外扩展（force 时忽略已存尺寸，按真实并集重算）。
            --
            -- 采样前注意：calcWindowSize 遍历全部动画会改写当前动画轨道并堆积事件；
            -- 根骨骼 scale（用户缩放）与 skeleton.x/y（模型偏移）都会放大/平移采样结果，
            -- 必须先归一为 scale=1、原点 (0,0) 采样，之后恢复，否则 conf 写入带缩放/偏移的尺寸，
            -- 运行时再乘 scale 会双重放大/错位（create 首次调用时 scale=1、x/y=0，故只有运行时 force 出问题）。
            local rootBone = skeleton.bones[0]
            local savedSX, savedSY = rootBone.scaleX, rootBone.scaleY
            local savedX, savedY = skeleton.x, skeleton.y
            rootBone.scaleX, rootBone.scaleY = 1, 1
            skeleton.x, skeleton.y = 0, 0
            local r = calcWindowSize(ffi, sp, animationState, animationStateData, skeleton)
            rootBone.scaleX, rootBone.scaleY = savedSX, savedSY
            skeleton.x, skeleton.y = savedX, savedY
            -- x 方向以骨架原点为对称中心对称扩展：半径取左右延伸较大者，画布宽 = 2×半径。
            -- 模型水平翻转（model.direction → skeleton.flipX）是绕骨架原点镜像，
            -- 画布两侧对称才能容纳镜像后的范围 [-maxX, -minX]，避免翻转后部分动画超出画布。
            local right = r.width - r.x   -- r.x = -minX = 左侧延伸，width-r.x = maxX = 右侧延伸
            if r.x < right then r.x = right end
            r.width = r.x * 2
            local cw = (not force) and (modelConfig.w or 0) or 0
            local ch = (not force) and (modelConfig.h or 0) or 0
            local nw = math.max(cw, r.width)
            local nh = math.max(ch, r.height)
            modelConfig.x = (nw - r.width) / 2 + r.x
            modelConfig.y = (nh - r.height) / 2 + r.y
            modelConfig.w = nw
            modelConfig.h = nh
            recomputed = true
        end
        -- 动态画布最小尺寸：与 legacy 一致，宽/高小于 448 时按 448，偏移 X/Y 不动
        -- （模型原点相对窗口位置不变，仅画布变大）；固定模式（小/大）不在此列。
        if mode == 2 and modelConfig.w ~= nil and modelConfig.h ~= nil then
            if modelConfig.w < 448 then modelConfig.w = 448 end
            if modelConfig.h < 448 then modelConfig.h = 448 end
        end
        if recomputed and modelConfig.save ~= nil then modelConfig:save() end
    end
    applyCanvasMode(canvasMode, false)

    ---将窗口设置为适应本模型
    M.setWindowSize = function()
        windowMan.setSize(math.ceil(modelConfig.w * scale), math.ceil(modelConfig.h * scale))
    end

    ---设置画布模式（面板"画布"下拉）：0=小(448x448)、1=大(768x768)、2=动态（按模型动画并集）
    M.setCanvasMode = function(mode)
        if mode == nil then mode = 2 end
        applyCanvasMode(mode, mode == 2)
        -- 重新应用：模型偏移 × 缩放、窗口尺寸
        M.scale(scale)
        M.setWindowSize()
    end

    ---设置骨骼缩放值
    ---@param v number 缩放值
    M.scale = function(v)
        if v == nil then return scale end
        scale = v
        skeleton.bones[0].scaleX = scale
        skeleton.bones[0].scaleY = scale
        skeleton.x = modelConfig.x * scale
        skeleton.y = modelConfig.y * scale
    end

    M.keepSetScale = function(v)
        local dx = modelConfig.x * (v - scale)
        local dy = modelConfig.y * (v - scale)
        local win = windowMan.windowPos()
        windowMan.setPosition(win.x - dx, win.y - dy)
        M.scale(v)
    end

    M.skeleton = skeleton

    M.setPosition = function(x, y)
        skeleton.x = x
        skeleton.y = y
    end

    M.setRawScale = function(v)
        scale = v
        skeleton.bones[0].scaleX = scale
        skeleton.bones[0].scaleY = scale
    end

    M.setRawPosition = function(x, y)
        skeleton.x = x
        skeleton.y = y
    end

    M.defaultMix = function(v)
        animationStateData.defaultMix = v
    end

    M.event_prefix = ev.unique()
    M.before_draw = M.event_prefix .. 'draw.before'
    M.after_draw = M.event_prefix .. 'draw.after'
    M.spine_start = M.event_prefix .. 'spine.start'
    -- M.spine_interrupt = M.event_prefix .. 'spine.interrupt'
    M.spine_end = M.event_prefix .. 'spine.end'
    M.spine_complete = M.event_prefix .. 'spine.complete'
    -- M.spine_dispose = M.event_prefix .. 'spine.dispose'
    M.spine_event = M.event_prefix .. 'spine.event'

    M.containsRec = ffi.new("HitTestRecorder")
    local mouseHit = false
    local dp = windowMan

    ---绘制
    M.draw = function()
        ev.trigger(M.before_draw)

        -- 限制单帧动画推进：多开/卡顿时 GetFrameTime 可能异常增大，
        -- 动画 time 大幅跳跃会使关键帧插值越过异常姿态（scale=0/transform约束），
        -- 导致骨骼被错误拉长。clamp 到 0.1s 以内并防御 NaN/负值。
        local dt = dp.frameTime()
        if dt ~= dt then dt = 0 end -- NaN
        if dt < 0 then dt = 0 end
        if dt > 0.1 then dt = 0.1 end
        sp.spAnimationState_update(animationState, dt)
        sp.spAnimationState_apply(animationState, skeleton)
        sp.spSkeleton_updateWorldTransform(skeleton)

        sp.drawSkeleton(skeleton, p.pma)
        if p.hittest and mouseHit == (sp.spSkeleton_containsPoint(skeleton, dp.mousePos().x, dp.mousePos().y, M.containsRec) ~= 1) then
            mouseHit = not mouseHit
        end
        if mouseHit then hit_collector.hit() end
        
        if animationState.userData ~= ffi.NULL then
            local event = ffi.cast(eventRecorderAtomPointType, animationState.userData)
            while event ~= ffi.NULL do
                if event.type == sp.SP_ANIMATION_START then ev.trigger(M.spine_start, event)
                -- elseif event.type == sp.SP_ANIMATION_INTERRUPT then ev.trigger(M.spine_interrupt, event)
                elseif event.type == sp.SP_ANIMATION_END then ev.trigger(M.spine_end, event)
                elseif event.type == sp.SP_ANIMATION_COMPLETE then ev.trigger(M.spine_complete, event)
                -- elseif event.type == sp.SP_ANIMATION_DISPOSE then ev.trigger(M.spine_dispose, event)
                elseif event.type == sp.SP_ANIMATION_EVENT then ev.trigger(M.spine_event, event) end
                event = event.next
            end
            sp.releaseAllEvents(animationState)
        end

        ev.trigger(M.after_draw)
    end

    ---取得一个附件，可用于在点击测试中测试鼠标是否在一个附件中
    ---@param slotName string 插槽名称
    ---@param attachmentName string | nil 附件名称，留空则为插槽名称
    ---@return Attachment | nil
    M.findAttachment = function(slotName, attachmentName)
        local attachment = nil
        local slot = sp.spSkeletonData_findSlotIndex(skeletonData, slotName)
        if slot >= 0 then attachment = sp.spSkin_getAttachment(skeletonData.defaultSkin, slot, attachmentName or slotName) end
        if attachment == ffi.NULL then attachment = nil end
        return attachment
    end

    ---取得模型在世界的坐标
    M.getRawPosition = function()
        return { x = skeleton.x, y = skeleton.y }
    end

    M.loop = function(animation, track)
        sp.spAnimationState_setAnimationByName(animationState, track or 0, animation, true)
    end

    M.once = function (animation, track)
        sp.spAnimationState_setAnimationByName(animationState, track or 0, animation, false)
    end

    M.direction = function(change)
        if change == nil then return skeleton.flipX and 1 or -1 end
        skeleton.flipX = change == -1
    end

    M.listenEvent = function(enabled)
        animationState.listener = enabled and sp.eventListenerFunc or ffi.NULL
    end

    M.findAnimation = function (name)
        return sp.spSkeletonData_findAnimation(skeletonData, name)
    end

    M.containsAttachment = function(attachment)
        if M.containsRec.count == 0 then return end
        for i = 0, tonumber(M.containsRec.count - 1) do
            if M.containsRec.list[i] == attachment then return true end
        end
        return false
    end

    return M
end

_M.createFromConfigFile = function(p, file)
    local M = {}
    M.event_prefix = ev.unique()
    M.mousein = M.event_prefix .. 'mouse.in'
    M.mouseout = M.event_prefix .. 'mouse.out'
    local modelConfig = require("settings").load(file, false)
    return _M.create(p, modelConfig)
end

---create a managed spine model
---@param p any
_M.createFromDefaultConfigFile = function(p)
    return _M.createFromConfigFile(p, "assets/model.conf.json")
end

return _M;
