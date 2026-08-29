using Microsoft.Xna.Framework.Graphics;
using System;
using System.Windows;

namespace GflChibiDesktop
{
    /// <summary>
    /// 备用渲染模块入口（类似 luajit 子进程）。启动后从命令行参数加载模型，
    /// 显示桌宠本体窗口，控制面板由 V2 主程序通过 IPC 提供。
    /// </summary>
    public partial class App : Application
    {
        public static GlobalValue globalValues = new GlobalValue();
        public static string rootDir = Environment.CurrentDirectory;
        public static MonoGameControl appXC;
        public static Texture2D textureBG;

        public static bool isPress = false;
        public static bool isNew = true;
        public static Point mouseLocation;
        public static SpriteBatch spriteBatch;
        public static GraphicsDevice graphicsDevice;
        public static double canvasWidth = SystemParameters.WorkArea.Width;
        public static double canvasHeight = SystemParameters.WorkArea.Height;
        public static double mainWidth;
        public static double mainHeight;

        /// <summary>渲染/加载错误提示回调。</summary>
        public static Action<Exception> NotifyError;
        /// <summary>模型加载完成回调。</summary>
        public static Action NotifyModelLoaded;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                LegacyArgs.Parse(e.Args);
                // 备用渲染模块必须由 V2 主程序启动（带 IPC 名），不允许独立运行
                if (string.IsNullOrEmpty(LegacyArgs.ReadIpcName) || string.IsNullOrEmpty(LegacyArgs.WriteIpcName))
                {
                    System.Windows.MessageBox.Show("GflChibiDesktopLegacy 为 V2 的备用渲染模块，必须通过 V2 主程序（运行模块→旧版）启动。",
                        "GflChibiDesktop", MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown(1);
                    return;
                }
                ApplyDefaults();

                var win = new PetWindow();
                MainWindow = win;
                App.NotifyError = ex => System.Windows.MessageBox.Show("渲染错误：\n" + ex.Message, "GflChibiDesktop", MessageBoxButton.OK, MessageBoxImage.Warning);
                App.NotifyModelLoaded = win.OnModelLoaded;

                win.LoadModel();
                win.Show();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("启动失败：\n" + ex.Message, "GflChibiDesktop", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private static void ApplyDefaults()
        {
            App.globalValues.FrameWidth = LegacyArgs.Width;
            App.globalValues.FrameHeight = LegacyArgs.Height;
            App.canvasWidth = LegacyArgs.Width;
            App.canvasHeight = LegacyArgs.Height;
            App.globalValues.Alpha = true;
            App.globalValues.PreMultiplyAlpha = true;
            App.globalValues.IsLoop = true;
            App.globalValues.Scale = 1;
            App.globalValues.Speed = 0;
            App.globalValues.Opacity = 1;
        }
    }
}
