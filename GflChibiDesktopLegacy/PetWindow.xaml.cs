using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;

namespace GflChibiDesktop
{
    /// <summary>
    /// 桌宠本体窗口（备用渲染模块，类似 luajit 子进程的桌宠渲染端）。
    /// 透明置顶渲染 spine 模型，支持拖拽移动、双击打开 Options、动态模拟。
    /// </summary>
    public partial class PetWindow : Window
    {
        private IPlayer player;
        /// <summary>与 V2 主程序通信的控制面板（无 IPC 参数时为空，独立运行）。</summary>
        public LegacyIpc Ipc;

        System.Windows.Threading.DispatcherTimer timerEventsSimulation = new System.Windows.Threading.DispatcherTimer();
        System.Windows.Threading.DispatcherTimer timerSimulationMoveX = new System.Windows.Threading.DispatcherTimer();
        System.Windows.Threading.DispatcherTimer timerSimulationS = new System.Windows.Threading.DispatcherTimer();
        System.Windows.Threading.DispatcherTimer timerSimulationReload = new System.Windows.Threading.DispatcherTimer();

        private int moveDistanceX;
        private int movedDistanceX;
        private bool moveXDirection;
        /// <summary>是否从 legacy_position.json 恢复了窗口位置（首次启动画布调整时需重新居中）。</summary>
        private bool _positionRestored = false;
        /// <summary>是否已应用过一次画布（首次加载才允许居中；之后动画切换只调整尺寸/平移）。</summary>
        private bool _canvasApplied = false;

        public PetWindow()
        {
            InitializeComponent();

            App.globalValues.PosX = LegacyArgs.PosX;
            App.globalValues.PosY = LegacyArgs.PosY;
            App.globalValues.EnableInteraction = true;
            Topmost = LegacyArgs.Topmost;

            Player.Width = App.globalValues.FrameWidth;
            Player.Height = App.globalValues.FrameHeight;
            Width = App.globalValues.FrameWidth;
            Height = App.globalValues.FrameHeight;

            SetBinding(OpacityProperty, new Binding() { Source = App.globalValues, Path = new PropertyPath("Opacity"), Mode = BindingMode.OneWay });

            timerEventsSimulation.Interval = new TimeSpan(0, 0, 0, 30);
            timerEventsSimulation.Tick += timerEventsSimulation_Tick;
            timerSimulationMoveX.Interval = new TimeSpan(0, 0, 0, 0, 10);
            timerSimulationMoveX.Tick += timerSimulationMoveX_Tick;
            timerSimulationS.Tick += timerSimulationS_Tick;
            timerSimulationReload.Tick += timerSimulationReload_Tick;

            LoadPosition();
            // 恢复持久化设置（settings1.json）：画布模式、动画、数值与开关；LoadContent 前生效
            App.globalValues.CanvasMode = LegacySettings.LoadCanvasMode();
            string savedAnime = LegacySettings.GetString("anime", string.Empty);
            if (!string.IsNullOrEmpty(savedAnime))
            {
                App.globalValues.SelectAnimeName = savedAnime;   // LoadContent 会校验当前模型是否存在该动画
            }
            App.globalValues.Scale = (float)LegacySettings.GetDouble("scale", 1.0);
            App.globalValues.Opacity = LegacySettings.GetDouble("opacity", 1.0);
            App.globalValues.Speed = LegacySettings.GetInt("speed", 0);
            App.globalValues.Rotation = (float)LegacySettings.GetDouble("rotation", 0);
            App.globalValues.IsLoop = LegacySettings.GetBool("loop", true);
            App.globalValues.MoveFlip = LegacySettings.GetBool("moveFlip", false);
            double simSec = LegacySettings.GetDouble("simulateInterval", 30);
            if (simSec > 0) timerEventsSimulation.Interval = TimeSpan.FromSeconds(simSec);
            Topmost = LegacySettings.GetBool("topmost", LegacyArgs.Topmost);
            Closed += (_, _) => SavePosition();
        }

        /// <summary>启动时恢复上次退出的窗口位置（读取成功则禁用居中对齐）。数据存于 settings1.json。</summary>
        private void LoadPosition()
        {
            if (LegacySettings.TryLoadWindowPosition(out double l, out double t))
            {
                Left = l;
                Top = t;
                WindowStartupLocation = WindowStartupLocation.Manual;
                _positionRestored = true;
            }
        }

        /// <summary>退出时把当前全部状态（窗口位置 + 所有面板项）保存到 settings1.json，供下次启动恢复。</summary>
        private void SavePosition()
        {
            LegacySettings.SaveWindowPosition(Left, Top);
            LegacySettings.SaveCanvasMode(App.globalValues.CanvasMode);
            LegacySettings.SetString("anime", App.globalValues.SelectAnimeName ?? string.Empty);
            LegacySettings.SetDouble("scale", App.globalValues.Scale);
            LegacySettings.SetDouble("opacity", App.globalValues.Opacity);
            LegacySettings.SetInt("speed", App.globalValues.Speed);
            LegacySettings.SetDouble("rotation", App.globalValues.Rotation);
            LegacySettings.SetBool("loop", App.globalValues.IsLoop);
            LegacySettings.SetBool("moveFlip", App.globalValues.MoveFlip);
            LegacySettings.SetBool("simulation", App.globalValues.Simulation);
            LegacySettings.SetDouble("simulateInterval", timerEventsSimulation.Interval.TotalSeconds);
            LegacySettings.SetBool("topmost", Topmost);
            LegacySettings.SetBool("clickThrough", _clickThrough);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WndProc);
        }

        /// <summary>拦截系统关闭（Alt+F4/系统菜单），防止用户关闭 legacy 进程被 V2 误判为异常退出。</summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_CLOSE = 0xF060;
            if (msg == WM_SYSCOMMAND && (wParam.ToInt32() & 0xFFF0) == SC_CLOSE)
            {
                handled = true;
            }
            return IntPtr.Zero;
        }

        /// <summary>加载模型（Show 之前调用，确保渲染控件挂载后即可渲染）。</summary>
        public void LoadModel()
        {
            string atlas = LegacyArgs.ModelFile;
            if (string.IsNullOrEmpty(atlas) || !File.Exists(atlas))
            {
                App.NotifyError?.Invoke(new Exception(string.IsNullOrEmpty(atlas)
                    ? "未指定模型（--model），且未找到默认模型。"
                    : "模型文件不存在：" + atlas));
                return;
            }

            App.globalValues.SelectAtlasFile = atlas;
            string skel = atlas.Replace(".atlas", ".skel");
            if (!File.Exists(skel)) skel = atlas.Replace(".atlas", ".json");
            if (!File.Exists(skel))
            {
                App.NotifyError?.Invoke(new Exception("找不到骨骼数据文件：" + skel));
                return;
            }
            App.globalValues.SelectSpineFile = skel;
            App.globalValues.DummyDisplayName = string.IsNullOrEmpty(LegacyArgs.DisplayName)
                ? Path.GetFileNameWithoutExtension(atlas) : LegacyArgs.DisplayName;

            App.isNew = true;
            App.globalValues.SelectSpineVersion = "2.1.25";
            LoadPlayer();
        }

        private void LoadPlayer()
        {
            if (App.appXC == null) App.appXC = new MonoGameControl();
            player = new Player_2_1_25();

            App.appXC.Initialize += player.Initialize;
            App.appXC.Update += player.Update;
            App.appXC.LoadContent += player.LoadContent;
            App.appXC.Draw += player.Draw;
            App.appXC.Width = App.globalValues.FrameWidth;
            App.appXC.Height = App.globalValues.FrameHeight;
            Player.Content = App.appXC;
        }

        /// <summary>模型加载完成回调（App.NotifyModelLoaded 目标）：订阅事件、按画布调整窗口，再初始化 V2 控制面板。</summary>
        public void OnModelLoaded()
        {
            App.CanvasChanged -= OnCanvasChanged;
            App.CanvasChanged += OnCanvasChanged;
            App.OnceAnimationFinished -= HandleOnceAnimationFinished;
            App.OnceAnimationFinished += HandleOnceAnimationFinished;
            ApplyCanvas();
            InitIpc();
            // 恢复持久化开关：动态模拟 / 点击穿透（面板/渲染就绪后生效）
            if (LegacySettings.GetBool("simulation", false))
            {
                toggleSimulation(true);
            }
            if (LegacySettings.GetBool("clickThrough", false))
            {
                SetClickThrough(true);
            }
        }

        /// <summary>
        /// 一次性动画播完（Player 的 AnimationState.Complete 通知）：victory（非循环）播完接续 victoryloop。
        /// 替代原先按 AnimeDuration 猜测时长的 DispatcherTimer，播完即接续、不再静止。
        /// </summary>
        private void HandleOnceAnimationFinished()
        {
            if (!App.globalValues.Simulation)
            {
                return;
            }
            if (App.globalValues.SelectAnimeName == "victory" &&
                App.globalValues.AnimeList != null &&
                App.globalValues.AnimeList.Contains("victoryloop"))
            {
                App.globalValues.SelectAnimeName = "victoryloop";
                App.globalValues.SetAnime = true;
                App.globalValues.IsLoop = true;
            }
        }

        /// <summary>画布模式/尺寸变化（Player 已更新画布），调整窗口。</summary>
        private void OnCanvasChanged()
        {
            ApplyCanvas();
        }

        /// <summary>
        /// 按基础画布 × 当前缩放调整窗口与渲染控件尺寸（画布固定为模型并集包围盒）。
        /// 首次启动（未应用过且无保存位置）在新尺寸下重新居中，避免模型超出屏幕。
        /// </summary>
        public void ApplyCanvas()
        {
            double w = Math.Ceiling(App.CanvasW * App.globalValues.Scale);
            double h = Math.Ceiling(App.CanvasH * App.globalValues.Scale);
            if (w <= 0 || h <= 0)
                return;
            App.globalValues.FrameWidth = w;
            App.globalValues.FrameHeight = h;
            Width = w;
            Height = h;
            Player.Width = w;
            Player.Height = h;
            if (App.appXC != null)
            {
                App.appXC.Width = w;
                App.appXC.Height = h;
            }
            if (!_canvasApplied && !_positionRestored)
            {
                Left = (int)((SystemParameters.WorkArea.Width - w) / 2);
                Top = (int)((SystemParameters.WorkArea.Height - h) / 2);
            }
            _canvasApplied = true;
            // 动态画布随动画扩大/缩小时的反向平移：保持模型原点屏幕位置不变
            if (App.CanvasShiftX != 0 || App.CanvasShiftY != 0)
            {
                Left += App.CanvasShiftX;
                Top += App.CanvasShiftY;
                App.CanvasShiftX = 0;
                App.CanvasShiftY = 0;
            }
        }

        /// <summary>
        /// 设置缩放：画布随缩放同步调整（基础尺寸 × 缩放），模型偏移等比缩放，
        /// 窗口反向移动以保持模型在屏幕上的位置稳定（与 raylib keepSetScale + setWindowSize 一致）。
        /// </summary>
        private void SetScale(float newScale)
        {
            float oldScale = App.globalValues.Scale;
            if (oldScale <= 0) oldScale = 1;
            float oldX = App.globalValues.PosX;
            float oldY = App.globalValues.PosY;
            float ratio = newScale / oldScale;
            App.globalValues.Scale = newScale;
            App.globalValues.PosX = oldX * ratio;
            App.globalValues.PosY = oldY * ratio;
            // 窗口随模型偏移变化反向移动，使模型在屏幕上的位置不变
            Left -= App.globalValues.PosX - oldX;
            Top -= App.globalValues.PosY - oldY;
            ApplyCanvas();
            LegacySettings.SetDouble("scale", newScale);   // 持久化（重启恢复）
        }

        /// <summary>
        /// 初始化与 V2 主程序的控制面板通信（复用 luajit IPC 面板协议）。
        /// 独立运行（无 IPC 参数）时静默跳过。
        /// </summary>
        private void InitIpc()
        {
            if (string.IsNullOrEmpty(LegacyArgs.ReadIpcName) || string.IsNullOrEmpty(LegacyArgs.WriteIpcName))
            {
                return;
            }
            try
            {
                // 面板命令在后台线程读取，回调操作 App.globalValues/Topmost 等 UI 依赖，须派发到 UI 线程
                Ipc = new LegacyIpc(LegacyArgs.ReadIpcName, LegacyArgs.WriteIpcName, a => Dispatcher.Invoke(a));

                Ipc.AddReadonly(() => "少女前线桌面Q宠 MonoGame 运行模块");
                Ipc.AddReadonly(() => Path.GetFileName(App.globalValues.SelectAtlasFile));

                Ipc.AddCombo("动画", "选择当前播放的动画",
                    () => App.globalValues.AnimeList ?? new List<string>(),
                    () =>
                    {
                        var l = App.globalValues.AnimeList;
                        return l == null ? -1 : l.IndexOf(App.globalValues.SelectAnimeName);
                    },
                    idx =>
                    {
                        var l = App.globalValues.AnimeList;
                        if (l != null && idx >= 0 && idx < l.Count)
                        {
                            App.globalValues.SelectAnimeName = l[idx];
                            App.globalValues.SetAnime = true;
                            LegacySettings.SetString("anime", l[idx]);   // 持久化（重启恢复）
                        }
                    });

                Ipc.AddNumeric("缩放(%)", "100 为一倍缩放",
                    () => (int)(App.globalValues.Scale * 100),
                    v => SetScale(v / 100f), 1, 200);

                // 画布尺寸：与新版 raylib 的"画布"下拉一致——小/大固定窗口，动态按模型动画并集自动
                Ipc.AddCombo("画布", "画布尺寸：小(448x448)、大(768x768)、动态(按模型自动)",
                    () => new List<string> { "小(448x448)", "大(768x768)", "动态" },
                    () => App.globalValues.CanvasMode,
                    idx =>
                    {
                        if (idx >= 0 && idx <= 2)
                        {
                            App.globalValues.CanvasMode = idx;   // Player.Update 检测后应用
                            LegacySettings.SaveCanvasMode(idx);   // 持久化（重启恢复）
                        }
                    });

                Ipc.AddNumeric("透明度", "0（透明）~255（不透明）",
                    () => (int)(App.globalValues.Opacity * 255),
                    v => { App.globalValues.Opacity = v / 255.0; LegacySettings.SetDouble("opacity", App.globalValues.Opacity); }, 0, 255);

                Ipc.AddNumeric("帧率", "0 为不限制，1~240",
                    () => App.globalValues.Speed,
                    v => { App.globalValues.Speed = v; LegacySettings.SetInt("speed", v); }, 0, 240);

                Ipc.AddNumeric("旋转角度", "人形在画布上的旋转角度",
                    () => (int)App.globalValues.Rotation,
                    v => { App.globalValues.Rotation = v; LegacySettings.SetDouble("rotation", v); }, 0, 359);

                Ipc.AddBool("循环播放", "循环播放选中的动画",
                    () => App.globalValues.IsLoop,
                    v => { App.globalValues.IsLoop = v; App.globalValues.SetAnime = true; LegacySettings.SetBool("loop", v); });

                // 翻转朝向：与新版 raylib 同名同描述（settings.moveFlip）——
                // 部分初始面向左边的人形移动时会倒着跑，开启后修正。
                // FilpX（渲染朝向）由走动逻辑按移动方向自动设置，此开关仅决定是否取反。
                Ipc.AddBool("翻转朝向", "部分初始面向左边的人形移动时会倒着跑，开启后修正",
                    () => App.globalValues.MoveFlip,
                    v => { App.globalValues.MoveFlip = v; LegacySettings.SetBool("moveFlip", v); });

                Ipc.AddBool("动态模拟", "定时随机播放动作/走动",
                    () => App.globalValues.Simulation,
                    v => toggleSimulation(v));

                Ipc.AddNumeric("模拟间隔(秒)", "动态模拟的间隔时间",
                    () => (int)timerEventsSimulation.Interval.TotalSeconds,
                    v => setSimulationInterval(v), 1, 120);

                Ipc.AddBool("窗口置顶", "将桌宠置于最上层",
                    () => Topmost,
                    v => { Topmost = v; LegacySettings.SetBool("topmost", v); });

                Ipc.AddBool("点击穿透", "启用后鼠标点击穿过桌宠",
                    () => _clickThrough,
                    v => SetClickThrough(v));

                Ipc.AddButton("重置小人坐标", "屏幕中找不到小人时点击，回到画布中心", ResetDummy);

                Ipc.Start();
            }
            catch
            {
                // 独立运行（无 V2 主程序）或 IPC 不可用时静默禁用面板
                try
                {
                    Ipc?.Dispose();
                }
                catch
                {
                }
                Ipc = null;
            }
        }

        /// <summary>重置桌宠：回到画布尺寸 100% 与模型默认偏移，窗口居中。</summary>
        public void ResetDummy()
        {
            App.globalValues.Scale = 1;
            App.globalValues.PosX = App.CanvasX;
            App.globalValues.PosY = App.CanvasY;
            ApplyCanvas();
            WindowState = WindowState.Normal;
            Left = (int)((SystemParameters.WorkArea.Width - Width) / 2);
            Top = (int)((SystemParameters.WorkArea.Height - Height) / 2);
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern uint SetWindowLong(IntPtr hWnd, int nIndex, long dwNewLong);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        private bool _dragging;
        private long _oldExtStyle;
        private bool _clickThrough;

        /// <summary>
        /// 点击穿透：启用后窗口不响应鼠标（WS_EX_TRANSPARENT），交互事件（拖拽/双击）同时失效。
        /// </summary>
        public void SetClickThrough(bool enabled)
        {
            if (enabled == _clickThrough)
            {
                return;
            }
            _clickThrough = enabled;
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (enabled)
                {
                    IsHitTestVisible = false;
                    _oldExtStyle = GetWindowLong(hwnd, -20); // GWL_EXSTYLE
                    SetWindowLong(hwnd, -20, 0x20);          // WS_EX_TRANSPARENT
                }
                else
                {
                    IsHitTestVisible = true;
                    SetWindowLong(hwnd, -20, _oldExtStyle);
                }
            }
            catch
            {
            }
            LegacySettings.SetBool("clickThrough", enabled);   // 持久化（重启恢复）
        }

        private void GridPlayer_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragging)
            {
                return;
            }
            _dragging = true;
            try
            {
                bool hasPick = App.globalValues.AnimeList != null && App.globalValues.AnimeList.Contains("pick");
                bool wasSimulation = App.globalValues.Simulation;
                string oldAnime = App.globalValues.SelectAnimeName;
                if (string.IsNullOrEmpty(oldAnime)) oldAnime = "wait";

                // 拖动开始：阻止动态模拟（停止随机动画与走动定时器）
                if (wasSimulation)
                {
                    timerEventsSimulation.Stop();
                    timerSimulationMoveX.Stop();
                    App.globalValues.Simulation_Moving = false;
                }

                if (hasPick)
                {
                    // 有 pick 动画（宿舍模型）：拖动时播放 pick
                    App.globalValues.SelectAnimeName = "pick";
                    App.globalValues.SetAnime = true;
                }
                ReleaseCapture();
                // 阻塞直到拖动结束（鼠标松开）
                SendMessage(new WindowInteropHelper(this).Handle, 0x00A1, 0x0002, 0); // WM_NCLBUTTONDOWN + HTCAPTION
                if (hasPick)
                {
                    // 松开后恢复原动画（动态模拟开启时回 wait）
                    App.globalValues.SelectAnimeName = wasSimulation ? "wait" : oldAnime;
                    App.globalValues.SetAnime = true;
                }
                // 拖动结束：恢复动态模拟
                if (wasSimulation)
                {
                    timerEventsSimulation.Start();
                }
            }
            catch
            {
            }
            finally
            {
                _dragging = false;
            }
        }

        // ===================== 动态模拟 =====================

        public void UpdateSpine()
        {
            // 动态模拟切换动画不再整包重载（dispose ContentManager + 重建 AnimationState），
            // 而是交由 Player_2_1_25.Update 的 SetAnime 分支做带 DefaultMix 的交叉过渡，
            // 与新版 raylib 的 setAnimationByName 行为一致，切换平滑不卡顿。
            if (player != null)
            {
                App.globalValues.SetAnime = true;
            }
        }

        public void toggleSimulation(bool toggleSwitch)
        {
            if (toggleSwitch)
            {
                App.globalValues.Simulation = true;
                timerEventsSimulation.Start();
                timerEventsSimulation_Tick(this, EventArgs.Empty);
                App.globalValues.Speed = 0;
            }
            else
            {
                App.globalValues.Simulation = false;
                timerEventsSimulation.Stop();
                timerSimulationMoveX.Stop();
                App.globalValues.Simulation_Moving = false;
                // 不强制回 wait（否则会覆盖用户同时选择的动画）；仅当停在 move 移动状态时回待机
                if (App.globalValues.SelectAnimeName == "move")
                {
                    eventSimulation_wait();
                }
            }
            LegacySettings.SetBool("simulation", toggleSwitch);   // 持久化（重启恢复）
        }

        public void setSimulationInterval(double intervalsec)
        {
            timerEventsSimulation.Interval = new TimeSpan(0, 0, 0, (int)intervalsec);
            LegacySettings.SetDouble("simulateInterval", intervalsec);   // 持久化
        }

        private void timerEventsSimulation_Tick(object sender, EventArgs e)
        {
            StopMove();
            timerSimulationMoveX.Stop();
            timerSimulationReload.Stop();
            timerSimulationS.Stop();
            Random rand = new Random();
            if (App.globalValues.IsDormMode)
            {
                App.globalValues.IsLoop = true;
                UpdateSpine();
                int i = rand.Next(1, 11);
                switch (i)
                {
                    case 1: eventSimulation_wait(); break;
                    case 2: eventSimulation_forward(); break;
                    case 3: eventSimulation_wait(); break;
                    case 4: eventSimulation_backward(); break;
                    case 5: eventSimulation_wait(); break;
                    case 6: eventSimulation_lie(); break;
                    case 7: eventSimulation_sit(); break;
                    case 8: eventSimulation_lie(); break;
                    case 9: eventSimulation_sit(); break;
                    case 10: eventSimulation_lie(); break;
                    default: eventSimulation_wait(); break;
                }
            }
            else
            {
                int i = rand.Next(1, 16);
                switch (i)
                {
                    case 1: eventSimulation_wait(); break;
                    case 2: eventSimulation_forward(); break;
                    case 3: eventSimulation_wait(); break;
                    case 4: eventSimulation_backward(); break;
                    case 5: eventSimulation_wait(); break;
                    case 6: eventSimulation_attack(); break;
                    case 7: eventSimulation_victory(); break;
                    case 8: eventSimulation_s(); break;
                    case 9: eventSimulation_skill(); break;
                    case 10: eventSimulation_die(); break;
                    case 11: eventSimulation_attack2(); break;
                    case 12: eventSimulation_wait(); break;
                    case 13: eventSimulation_reload(); break;
                    case 14: eventSimulation_victory(); break;
                    case 15: eventSimulation_wait(); break;
                    default: eventSimulation_wait(); break;
                }
            }
        }

        private void eventSimulation_forward()
        {
            if (App.globalValues.AnimeList.Contains("move"))
            {
                // 向右移动：默认朝右（FilpX=false）；素材初始面朝左（翻转朝向开启）时取反，与 raylib model.direction 一致
                App.globalValues.FilpX = App.globalValues.MoveFlip;
                App.globalValues.IsLoop = true;
                UpdateSpine();
                if (((int)(SystemParameters.PrimaryScreenWidth - Left - Width) >= 10) && ((SystemParameters.PrimaryScreenWidth - Left - (int)(Width / 2)) > 100))
                {
                    Random rand = new Random();
                    moveDistanceX = rand.Next(10, (int)(SystemParameters.PrimaryScreenWidth - Left - Width));
                    App.globalValues.SelectedAnime = App.globalValues.SelectAnimeName;
                    App.globalValues.SelectAnimeName = "move";
                    App.globalValues.SetAnime = true;
                    App.globalValues.Simulation_Moving = true;
                    movedDistanceX = 0;
                    moveXDirection = true;
                    timerSimulationMoveX.Start();
                }
                else
                {
                    eventSimulation_backward();
                }
            }
        }

        private void eventSimulation_backward()
        {
            if (App.globalValues.AnimeList.Contains("move"))
            {
                // 向左移动：默认朝左（FilpX=true）；翻转朝向开启时取反，与 raylib model.direction 一致
                App.globalValues.FilpX = !App.globalValues.MoveFlip;
                App.globalValues.IsLoop = true;
                UpdateSpine();
                if (Left > 100)
                {
                    Random rand = new Random();
                    moveDistanceX = rand.Next(10, (int)Left);
                    App.globalValues.SelectedAnime = App.globalValues.SelectAnimeName;
                    App.globalValues.SelectAnimeName = "move";
                    App.globalValues.SetAnime = true;
                    App.globalValues.Simulation_Moving = true;
                    movedDistanceX = 0;
                    moveXDirection = false;
                    timerSimulationMoveX.Start();
                }
                else
                {
                    eventSimulation_forward();
                }
            }
        }

        private void eventSimulation_sit()
        {
            if (App.globalValues.AnimeList.Contains("sit"))
            {
                if (App.globalValues.SelectAnimeName == "sit")
                    return;   // 与当前动画相同，不重建避免抽搐
                App.globalValues.IsLoop = true;
                App.globalValues.SelectAnimeName = "sit";
                App.globalValues.SetAnime = true;
                UpdateSpine();
            }
        }

        private void eventSimulation_wait()
        {
            if (App.globalValues.AnimeList.Contains("wait"))
            {
                if (App.globalValues.SelectAnimeName == "wait")
                    return;   // 与当前动画相同，不重建避免抽搐
                App.globalValues.SelectAnimeName = "wait";
                App.globalValues.SetAnime = true;
                App.globalValues.IsLoop = true;
                UpdateSpine();
            }
        }

        private void eventSimulation_attack()
        {
            if (App.globalValues.AnimeList.Contains("attack"))
            {
                if (App.globalValues.SelectAnimeName == "attack")
                    return;   // 与当前动画相同，不重建避免抽搐
                App.globalValues.SelectAnimeName = "attack";
                App.globalValues.SetAnime = true;
                App.globalValues.IsLoop = true;
                UpdateSpine();
            }
            else
            {
                eventSimulation_wait();
            }
        }

        private void eventSimulation_attack2()
        {
            if (App.globalValues.AnimeList.Contains("attack2"))
            {
                if (App.globalValues.SelectAnimeName == "attack2")
                    return;   // 与当前动画相同，不重建避免抽搐
                App.globalValues.SelectAnimeName = "attack2";
                App.globalValues.SetAnime = true;
                App.globalValues.IsLoop = true;
                UpdateSpine();
            }
            else
            {
                eventSimulation_attack();
            }
        }

        private void eventSimulation_s()
        {
            if (App.globalValues.AnimeList.Contains("s"))
            {
                if (App.globalValues.SelectAnimeName == "s")
                    return;   // 与当前动画相同，不重建避免抽搐
                App.globalValues.SelectAnimeName = "s";
                App.globalValues.SetAnime = true;
                App.globalValues.IsLoop = false;   // s 播一次，定时器到点接 attack2
                UpdateSpine();
                timerSimulationS.Interval = new TimeSpan(0, 0, 0, (int)App.globalValues.AnimeDuration);
                timerSimulationS.Start();
            }
            else
            {
                eventSimulation_attack();
            }
        }

        private void timerSimulationS_Tick(object sender, EventArgs e)
        {
            eventSimulation_attack2();
            timerSimulationS.Stop();
        }

        private void eventSimulation_reload()
        {
            if (App.globalValues.AnimeList.Contains("reload"))
            {
                if (App.globalValues.SelectAnimeName == "reload")
                    return;   // 与当前动画相同，不重建避免抽搐
                App.globalValues.SelectAnimeName = "reload";
                App.globalValues.SetAnime = true;
                App.globalValues.IsLoop = false;   // reload 播一次，定时器到点接 attack2
                UpdateSpine();
                timerSimulationReload.Interval = new TimeSpan(0, 0, 0, (int)App.globalValues.AnimeDuration, 0);
                timerSimulationReload.Start();
            }
            else
            {
                eventSimulation_attack2();
            }
        }

        private void timerSimulationReload_Tick(object sender, EventArgs e)
        {
            eventSimulation_attack2();
            timerSimulationReload.Stop();
        }

        private void eventSimulation_skill()
        {
            if (App.globalValues.AnimeList.Contains("skill"))
            {
                if (App.globalValues.SelectAnimeName == "skill")
                    return;   // 与当前动画相同，不重建避免抽搐
                App.globalValues.SelectAnimeName = "skill";
                App.globalValues.SetAnime = true;
                App.globalValues.IsLoop = true;
                UpdateSpine();
            }
            else
            {
                eventSimulation_attack();
            }
        }

        private void eventSimulation_die()
        {
            if (App.globalValues.AnimeList.Contains("die"))
            {
                if (App.globalValues.SelectAnimeName == "die")
                    return;   // 与当前动画相同，不重建避免抽搐
                App.globalValues.SelectAnimeName = "die";
                App.globalValues.SetAnime = true;
                App.globalValues.IsLoop = false;   // die 播一次（死亡动画），下次随机切换
                UpdateSpine();
            }
        }

        private void eventSimulation_victory()
        {
            if (App.globalValues.AnimeList.Contains("victory"))
            {
                if (App.globalValues.SelectAnimeName == "victory")
                    return;   // 与当前动画相同，不重建避免抽搐
                if (App.globalValues.AnimeList.Contains("victoryloop"))
                {
                    App.globalValues.SelectAnimeName = "victory";
                    App.globalValues.SetAnime = true;
                    App.globalValues.IsLoop = false;
                    UpdateSpine();
                    // victory 播完由 AnimationState.Complete 事件接续 victoryloop（HandleOnceAnimationFinished），
                    // 不再用按 AnimeDuration 猜测时长的定时器（旧 AnimeDuration 常不准导致接续缺失/静止）
                }
                else
                {
                    App.globalValues.SelectAnimeName = "victory";
                    App.globalValues.SetAnime = true;
                    App.globalValues.IsLoop = true;
                    UpdateSpine();
                }
            }
            else
            {
                eventSimulation_wait();
            }
        }

        private void eventSimulation_lie()
        {
            if (App.globalValues.AnimeList.Contains("lying"))
            {
                if (App.globalValues.SelectAnimeName == "lying")
                    return;   // 与当前动画相同，不重建避免抽搐
                App.globalValues.SelectAnimeName = "lying";
                App.globalValues.SetAnime = true;
                App.globalValues.IsLoop = true;
                UpdateSpine();
            }
        }

        private void timerSimulationMoveX_Tick(object sender, EventArgs e)
        {
            if (App.globalValues.Simulation_Moving == true)
            {
                if (movedDistanceX < moveDistanceX)
                {
                    if (moveXDirection == true)
                    {
                        if ((SystemParameters.PrimaryScreenWidth - Left - Width) > 100)
                        {
                            Left += 1;
                            movedDistanceX += 1;
                        }
                        else
                        {
                            StopMove();
                        }
                    }
                    else
                    {
                        if (Left > 100)
                        {
                            Left -= 1;
                            movedDistanceX += 1;
                        }
                        else
                        {
                            StopMove();
                        }
                    }
                }
                else
                {
                    StopMove();
                }
            }
            else
            {
                StopMove();
            }
        }

        public void StopMove()
        {
            timerSimulationMoveX.Stop();
            App.globalValues.Simulation_Moving = false;
            eventSimulation_wait();
        }
    }
}
