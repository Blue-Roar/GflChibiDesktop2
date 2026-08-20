require("hdtbase")
local ev = require("eventize")
local window = require("blockly_window")
local blockly_spine = require("blockly_spine2125")
local dragmove = require("dragmove")
local settingsMan = require("settings")
local walkMan = require("walk")
local ipc = require("ipc")
local win32 = require("win32")

local modelNameFile = io.open("assets/name.txt", "r")
if modelNameFile == nil then log("failed to load model name") os.exit(1, true) return end
local modelName = modelNameFile:read("l")
modelNameFile:close()

ipc.addPanelItem({ type = "readonly", text = "HuiDesktop Light 少女前线模块"}, function() end, function() end)
ipc.addPanelItem({ type = "readonly", text = modelName }, function() end, function() end)

-- 工具函数
local function setPropertyValues(self, values)
    for k, v in pairs(values) do
        self[k](v)
    end
end

local state = "idle"
local enterStateThen = {}
local enterState = function(name)
    state = name
    enterStateThen[name]()
end

-- 加载设置
local settings = settingsMan.load("settings.json", true)
settings:default({ walk = true, drag = true, startDistance = 500, stopDistance = 200, scale = 100, drop = true, transparency = 255, autoHide = true, idleMotion = 1, simulateDorm = false, simulateInterval = 30 })
-- 互斥保证：跟随鼠标与宿舍模拟不能同时启用
if settings.walk and settings.simulateDorm then
    settings.simulateDorm = false
end
settings:save()

-- 新建窗口
window.create { vsync = true, topmost = true, transparent = true, autoHide = true, settings = settings:access("window") }
win32.setTransparency(settings.transparency)
-- 窗口关闭前保存设置（含人形坐标），避免退出后位置丢失
ev.on(window.window_closing, function() settings:save() end)
ipc.addPanelItem(
    { type = "single", valueType = "number", prompt = "帧率", hint = "0为不限制（匹配屏幕刷新率）", min = 0, max = 114514 },
    function(v) settings.window.fps = v window.setFPS(v) settings:save() end,
    function() return settings.window.fps end)
ipc.addPanelItem(
    { type = "bool", prompt = "显示帧率", hint = "左上角数字" },
    function(v) settings.window.drawFps = v settings:save() end,
    function() return settings.window.drawFps end)
ipc.addPanelItem(
    { type = "bool", prompt = "全屏隐藏", hint = "有窗口全屏时是否隐藏自己" },
    function(v) settings.autoHide = v window.param.autoHide = v settings:save() end,
    function() return settings.autoHide end)
ipc.addPanelItem(
    { type = "single", valueType = "number", prompt = "透明度", hint = "0（透明） ~ 255（不透明） 注意设置为0时窗口仍然响应点击", min = 0, max = 255 },
    function(v) settings.transparency = v win32.setTransparency(v) settings:save() end,
    function() return settings.transparency end)

-- 加载模型
local model = blockly_spine.createFromDefaultConfigFile { hittest = true, pma = true }
setPropertyValues(model, {
    scale = settings.scale / 100,
    defaultMix = 0.2,
    listenEvent = true })
model.setWindowSize() -- 将窗口大小设置为适合模型的状态
ipc.addPanelItem(
    { type = "single", valueType = "number", prompt = "缩放(%)", hint = "e.g. 100 为一倍缩放", min = 0, max = 114514 },
    function(v) settings.scale = v model.keepSetScale(v / 100) model.setWindowSize() settings:save() end,
    function() return settings.scale end)

-- 绑定事件
ev.on(window.draw, model.draw)

if ((model.findAnimation("sit") ~= nil) and (model.findAnimation("lying") ~= nil)) then
    -- 处理idle
    enterStateThen["idle"] = function()
        if settings.idleMotion == 2 then model.loop("sit")
        elseif settings.idleMotion == 3 then model.loop("lying")
        else model.loop("wait") end
    end
    ipc.addPanelItem(
        { type = "button", prompt = "切换站坐躺", hint = "变换小人的姿势（会保存下来）" },
        function()
            settings.idleMotion = (settings.idleMotion == 3 and 1 or settings.idleMotion + 1)
            settings:save()
            if state == "idle" then enterState("idle") end
        end,
        function() end);
else
    -- 处理idle
    enterStateThen["idle"] = function()
        model.loop("wait")
    end
end



-- 处理walk
(function()
    local walker = walkMan.createWithWindowDefault({
        checkCanStart = function() return settings.walk and not settings.simulateDorm and state == "idle" end,
        model = model, startDistance = settings.startDistance, stopDistance = settings.stopDistance, walkSpeed = 80
    })
    ev.on(window.after_draw, walker.trigger) -- 走动

    ev.on(walker.walking, function() enterState("walk") end)
    ev.on(walker.walked, function() enterState("idle") end)
    ev.on(walker.directionChanged, function() model.direction(walker.direction) end)

    enterStateThen["walk"] = function()
        model.direction(walker.direction)
        model.loop("move")
    end

    -- 宿舍模拟（参考旧版 toggleSimulation：定时随机 走动/坐下/躺下/站立）
    -- 声明先于"跟随鼠标"面板项，供其互斥逻辑引用
    local simTimer = 0
    local simMoving = false
    local simDir = -1
    local simRemain = 0
    local simStartX = 0
    local simTraveled = 0
    local SIM_MOVE_SPEED = 80 -- 走动速度（像素/秒）
    local workArea = win32.getWorkArea()

    local function simStartMove()
        simDir = math.random(2) == 1 and 1 or -1
        simRemain = math.random(200, 700)
        simStartX = window.windowPos().x
        simTraveled = 0
        simMoving = true
        model.direction(simDir)
        model.loop("move")
        state = "walk"
    end

    local function simStopMove()
        simMoving = false
        simRemain = 0
        simTraveled = 0
        enterState("idle")
    end

    local function simAct()
        local r = math.random(1, 10)
        if r == 2 or r == 4 then
            simStartMove()
        elseif r == 6 or r == 8 or r == 10 then
            if model.findAnimation("lying") ~= nil then model.loop("lying") end
        elseif r == 7 or r == 9 then
            if model.findAnimation("sit") ~= nil then model.loop("sit") end
        else
            model.loop("wait")
        end
    end

    local function simTick()
        if not settings.simulateDorm then return end
        if simMoving then
            simTraveled = simTraveled + SIM_MOVE_SPEED * window.frameTime()
            if simTraveled >= simRemain then
                simStopMove()
                return
            end
            local wp = window.windowPos()
            -- 基于起始坐标 + 累积位移计算目标位置，避免亚像素被整数取整吃掉
            local nx = simStartX + simDir * simTraveled
            -- 不越出工作区：到达边界立即结束本次走动，避免原地空跑
            if nx < workArea.left then
                nx = workArea.left
                simStopMove()
                return
            elseif nx + window.windowSize.width > workArea.right then
                nx = workArea.right - window.windowSize.width
                simStopMove()
                return
            end
            window.setPosition(nx, wp.y)
        elseif state == "idle" then
            simTimer = simTimer + window.frameTime()
            if simTimer >= settings.simulateInterval then
                simTimer = 0
                simAct()
            end
        end
    end
    ev.on(window.after_draw, simTick)

    ipc.addPanelItem(
        { type = "bool", prompt = "跟随鼠标", hint = "与鼠标距离太远会接近（与宿舍模拟互斥）" },
        function(v)
            settings.walk = v
            -- 与宿舍模拟互斥：开启跟随鼠标时关闭宿舍模拟
            if v and settings.simulateDorm then
                settings.simulateDorm = false
                simStopMove()
            end
            settings:save()
        end,
        function() return settings.walk end)

    ipc.addPanelItem(
        { type = "bool", prompt = "宿舍模拟", hint = "开启后小人会随机活动：走动、坐下、躺下（与跟随鼠标互斥）" },
        function(v)
            settings.simulateDorm = v
            -- 与跟随鼠标互斥：开启宿舍模拟时关闭跟随鼠标
            if v and settings.walk then settings.walk = false end
            settings:save()
            if v then
                -- 打开立即有动作（非 idle 状态（如拖拽中）则等空闲后再触发）
                if state == "idle" then simAct() end
            else
                -- 关闭立即停
                simStopMove()
                simTimer = 0
            end
        end,
        function() return settings.simulateDorm end)
    ipc.addPanelItem(
        { type = "single", valueType = "number", prompt = "模拟间隔(秒)", hint = "宿舍模拟每隔多少秒随机切换一次动作", min = 1, max = 3600 },
        function(v) settings.simulateInterval = v settings:save() end,
        function() return settings.simulateInterval end)
    ipc.addPanelItem(
        { type = "single", valueType = "number", prompt = "最远距离", hint = "水平超过这个距离，桌宠就会走向鼠标，若不想频繁跟随就调大一些" },
        function(v) v = math.max(v, settings.stopDistance) settings.startDistance = v walker.startDistance = v settings:save() end,
        function() return settings.startDistance end)
    ipc.addPanelItem(
        { type = "single", valueType = "number", prompt = "停止距离", hint = "水平小于这个距离，桌宠就会停止走动，调大一点可以避免走得太近" },
        function(v) settings.stopDistance = v walker.stopDistance = v settings:save() end,
        function() return settings.stopDistance end)
end)();

local dragger = nil;

-- 处理drag
(function()
    local updating = false
    dragger = dragmove.createWithWindowDefault {
        checkCanStart = function() return state == "idle" or state == "drop" end,
        key = window.mouseButton.left,
        model = model
    }

    ev.on(window.after_draw, dragger.trigger) -- 拖拽响应
    ev.on(dragger.dragging, function() enterState("drag") end)
    
    if (model.findAnimation("pick") ~= nil) then
        enterStateThen["drag"] = function() model.loop("pick") end
    else
        enterStateThen["drag"] = function() model.loop("wait") end
    end

    if settings.ground == nil then
        settings.ground = win32.getGround()
        settings:save()
    end
    dragger.ground = settings.ground
    enterStateThen["drop"] = function() model.loop("wait") end
    ev.on(dragger.dragged, function()
        dragger.drop = settings.drop and not updating
        enterState((settings.drop and not updating) and "drop" or "idle")
        if not dragger.drop then settings:save() end
    end)
    ev.on(dragger.dropped, function() enterState("idle") settings:save() end)

    ipc.addPanelItem(
        { type = "bool", prompt = "拖动后落地", hint = "地面默认是任务栏" },
        function(v) settings.drop = v settings:save() end,
        function() return settings.drop end)

    local updateGroundPanelItem = { type = "button", prompt = "点击开始设定地面位置", hint = "如果想改变落地的位置，请按此按钮并根据之后提示操作" }
    ipc.addPanelItem(
        updateGroundPanelItem,
        function()
            if updating then
                updating = false
                settings.ground = math.floor(window.windowPos().y + model.getRawPosition().y)
                dragger.ground = settings.ground
                settings:save()
                updateGroundPanelItem.prompt = "点击开始设定地面位置"
                updateGroundPanelItem.hint = "如果想改变落地的位置，请按此按钮并根据之后提示操作"
            else
                updating = true
                updateGroundPanelItem.prompt = "点击结束设定地面位置"
                updateGroundPanelItem.hint = "将小人拖到你觉得合适的地面位置，然后点击结束即可"
            end
        end,
        function() end)

    if settings.drop then
        window.setPosition(window.windowPos().x, settings.ground - model.getRawPosition().y)
    end
end)();

ipc.addPanelItem(
        { type = "button", prompt = "重置小人坐标", hint = "如屏幕里面找不到小人，请最小化所有窗口然后点击此按钮" },
        function()
            window.setPosition(0, 0)
            settings:save()
            enterState("idle")
        end,
        function() end)

-- ipc
if #arg > 1 then
    local rxIpcInst = nil
    local txIpcInst = nil
    -- open ipc
    rxIpcInst = ipc.lib.hiMQ_openIPC(arg[1])
    txIpcInst = ipc.lib.hiMQ_openIPC(arg[2])

    ev.on(window.after_draw, function() ipc.read(rxIpcInst, txIpcInst) end)
    ipc.sendPanelStructure(txIpcInst)
end

enterState("idle")
window.run()
