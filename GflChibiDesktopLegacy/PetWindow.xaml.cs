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
        System.Windows.Threading.DispatcherTimer timerSimulationVictory = new System.Windows.Threading.DispatcherTimer();
        System.Windows.Threading.DispatcherTimer timerSimulationReload = new System.Windows.Threading.DispatcherTimer();

        private int moveDistanceX;
        private int movedDistanceX;
        private bool moveXDirection;
        private bool DummyReverse = false;

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
            timerSimulationVictory.Tick += timerSimulationVictory_Tick;
            timerSimulationReload.Tick += timerSimulationReload_Tick;
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

        /// <summary>模型加载完成回调（App.NotifyModelLoaded 目标）：初始化 V2 控制面板。</summary>
        public void OnModelLoaded()
        {
            InitIpc();
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
                        }
                    });

                Ipc.AddNumeric("缩放(%)", "100 为一倍缩放",
                    () => (int)(App.globalValues.Scale * 100),
                    v => App.globalValues.Scale = v / 100f, 1, 200);

                Ipc.AddNumeric("透明度", "0（透明）~255（不透明）",
                    () => (int)(App.globalValues.Opacity * 255),
                    v => App.globalValues.Opacity = v / 255.0, 0, 255);

                Ipc.AddNumeric("FPS", "帧率",
                    () => App.globalValues.Speed,
                    v => App.globalValues.Speed = v, 1, 60);

                Ipc.AddNumeric("水平位置", "",
                    () => (int)App.globalValues.PosX,
                    v => App.globalValues.PosX = v, 0, (int)App.globalValues.FrameWidth);

                Ipc.AddNumeric("垂直位置", "",
                    () => (int)App.globalValues.PosY,
                    v => App.globalValues.PosY = v, 0, (int)App.globalValues.FrameHeight);

                Ipc.AddNumeric("旋转角度", "",
                    () => (int)App.globalValues.Rotation,
                    v => App.globalValues.Rotation = v, 0, 359);

                Ipc.AddBool("循环播放", "",
                    () => App.globalValues.IsLoop,
                    v => { App.globalValues.IsLoop = v; App.globalValues.SetAnime = true; });

                Ipc.AddBool("水平翻转", "",
                    () => App.globalValues.FilpX,
                    v => App.globalValues.FilpX = v);

                Ipc.AddBool("垂直翻转", "",
                    () => App.globalValues.FilpY,
                    v => App.globalValues.FilpY = v);

                Ipc.AddBool("动态模拟", "定时随机播放动作/走动",
                    () => App.globalValues.Simulation,
                    v => toggleSimulation(v));

                Ipc.AddNumeric("模拟间隔(秒)", "",
                    () => (int)timerEventsSimulation.Interval.TotalSeconds,
                    v => setSimulationInterval(v), 1, 120);

                Ipc.AddBool("窗口置顶", "",
                    () => Topmost,
                    v => Topmost = v);

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

        /// <summary>重置桌宠窗口到画布中心。</summary>
        public void ResetDummy()
        {
            WindowState = WindowState.Normal;
            Left = (int)((SystemParameters.WorkArea.Width - Width) / 2);
            Top = (int)((SystemParameters.WorkArea.Height - Height) / 2);
            App.globalValues.PosX = 224;
            App.globalValues.PosY = 224;
            App.globalValues.Scale = 1;
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private bool _dragging;

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
            if (player != null) player.ChangeSet();
        }

        public void toggleSimulation(bool toggleSwitch)
        {
            if (toggleSwitch)
            {
                App.globalValues.Simulation = true;
                timerEventsSimulation.Start();
                timerEventsSimulation_Tick(this, EventArgs.Empty);
                App.globalValues.Speed = 30;
            }
            else
            {
                App.globalValues.Simulation = false;
                timerEventsSimulation.Stop();
                StopMove();
            }
        }

        public void setSimulationInterval(double intervalsec)
        {
            timerEventsSimulation.Interval = new TimeSpan(0, 0, 0, (int)intervalsec);
        }

        private void timerEventsSimulation_Tick(object sender, EventArgs e)
        {
            StopMove();
            timerSimulationMoveX.Stop();
            timerSimulationReload.Stop();
            timerSimulationS.Stop();
            timerSimulationVictory.Stop();
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
                if (DummyReverse) App.globalValues.FilpX = true;
                else App.globalValues.FilpX = false;
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
                if (DummyReverse) App.globalValues.FilpX = false;
                else App.globalValues.FilpX = true;
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
                App.globalValues.SelectAnimeName = "s";
                App.globalValues.SetAnime = true;
                App.globalValues.IsLoop = false;
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
                App.globalValues.SelectAnimeName = "reload";
                App.globalValues.SetAnime = true;
                App.globalValues.IsLoop = false;
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
                App.globalValues.SelectAnimeName = "die";
                App.globalValues.SetAnime = true;
                App.globalValues.IsLoop = false;
                UpdateSpine();
            }
        }

        private void eventSimulation_victory()
        {
            if (App.globalValues.AnimeList.Contains("victory"))
            {
                if (App.globalValues.AnimeList.Contains("victoryloop"))
                {
                    App.globalValues.SelectAnimeName = "victory";
                    App.globalValues.SetAnime = true;
                    App.globalValues.IsLoop = false;
                    UpdateSpine();
                    timerSimulationVictory.Interval = new TimeSpan(0, 0, 0, (int)App.globalValues.AnimeDuration);
                    timerSimulationVictory.Start();
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

        private void timerSimulationVictory_Tick(object sender, EventArgs e)
        {
            timerSimulationVictory.Stop();
            if (App.globalValues.AnimeList.Contains("victoryloop"))
            {
                App.globalValues.SelectAnimeName = "victoryloop";
                App.globalValues.SetAnime = true;
                App.globalValues.IsLoop = true;
                UpdateSpine();
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
