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

    if modelConfig.x == nil or modelConfig.y == nil or modelConfig.w == nil or modelConfig.h == nil then
        local r = calcWindowSize(ffi, sp, animationState, animationStateData, skeleton)
        if r.x * 2 < r.width then
            r.x = r.width - r.x
            r.width = r.x * 2
        end
        modelConfig.x = r.x
        modelConfig.y = r.y
        modelConfig.w = r.width
        modelConfig.h = r.height
        if modelConfig.save ~= nil then modelConfig:save() end
    end

    ---将窗口设置为适应本模型
    M.setWindowSize = function()
        windowMan.setSize(math.ceil(modelConfig.w * scale), math.ceil(modelConfig.h * scale))
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

        sp.spAnimationState_update(animationState, dp.frameTime())
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
