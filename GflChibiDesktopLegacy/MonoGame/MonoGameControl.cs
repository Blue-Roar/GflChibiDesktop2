using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace GflChibiDesktop
{
    /// <summary>
    /// 替代 WpfXnaControl.XnaControl 的 MonoGame 承载控件。
    /// 暴露与 XnaControl 一致的 GraphicsDevice / ContentManager 与 Initialize / Update / LoadContent / Draw 委托。
    /// </summary>
    public class MonoGameControl : UserControl
    {
        public GraphicsDevice GraphicsDevice;
        public ContentManager ContentManager;

        public Action Initialize;
        public Action<GameTime> Update;
        public Action<ContentManager> LoadContent;
        public Action Draw;

        private MonoGameHost _host;
        private Image _image;
        private WriteableBitmap _bitmap;
        private bool _started;
        private readonly Stopwatch _frameClock = new Stopwatch();
        private TimeSpan _lastRenderTime;

        public MonoGameControl()
        {
            _image = new Image
            {
                Stretch = Stretch.Fill,
                SnapsToDevicePixels = true,
                Focusable = false
            };
            Content = _image;
            Background = Brushes.Transparent;
            ClipToBounds = true;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_started)
                return;
            _started = true;
            StartHost();
        }

        private void StartHost()
        {
            try
            {
                StartHostWithProfile(GraphicsProfile.HiDef);
            }
            catch (NoSuitableGraphicsDeviceException ex)
            {
                _host?.Dispose();
                _host = null;
                App.NotifyError?.Invoke(ex);
                throw;
            }
        }

        private void StartHostWithProfile(GraphicsProfile profile)
        {
            _host = new MonoGameHost(this, profile);
            _host.RunOneFrame();

            GraphicsDevice = _host.GraphicsDevice;
            ContentManager = new ContentManager(_host.Services);

            Initialize?.Invoke();
            LoadContent?.Invoke(ContentManager);

            _host.Ready = true;

            _frameClock.Start();
            _lastRenderTime = _frameClock.Elapsed;

            CompositionTarget.Rendering += OnRendering;
            Application.Current.Dispatcher.ShutdownStarted += OnShutdownStarted;
        }

        private void OnShutdownStarted(object sender, EventArgs e)
        {
            try
            {
                CompositionTarget.Rendering -= OnRendering;
                if (Application.Current?.Dispatcher != null)
                {
                    Application.Current.Dispatcher.ShutdownStarted -= OnShutdownStarted;
                }
                // 进程即将退出，不在此手动销毁 MonoGame 宿主：
                // 退出时序中销毁 GL 上下文 / SDL 窗口容易触发原生 AccessViolation，
                // 交由操作系统在进程结束时统一回收即可。
                if (_host != null)
                {
                    GC.SuppressFinalize(_host);
                    _host = null;
                }
            }
            catch
            {
            }
        }

        private void OnRendering(object sender, EventArgs e)
        {
            if (_host == null || !_host.Ready)
                return;

            TimeSpan now = _frameClock.Elapsed;
            // 渲染帧率由“帧率”设置（Speed）控制，默认 60；动画速度不受影响
            double fps = App.globalValues.Speed;
            if (fps <= 0) fps = 60;
            TimeSpan interval = TimeSpan.FromMilliseconds(1000.0 / fps);
            if (now - _lastRenderTime < interval)
                return;
            _lastRenderTime = now;

            _host.RenderFrame();
            UpdateBitmap();
        }

        private void EnsureBitmap(int width, int height)
        {
            if (_bitmap != null && _bitmap.PixelWidth == width && _bitmap.PixelHeight == height)
                return;
            _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
            _image.Source = _bitmap;
        }

        private void UpdateBitmap()
        {
            int w = _host.TargetWidth;
            int h = _host.TargetHeight;
            if (w <= 0 || h <= 0)
                return;

            EnsureBitmap(w, h);
            _bitmap.WritePixels(new Int32Rect(0, 0, w, h), _host.PixelBuffer, w * 4, 0);
        }

        /// <summary>
        /// 重新触发 Initialize / LoadContent。更换 Spine 版本后调用，
        /// 用于让新接线的 Player 重新初始化并加载模型。
        /// </summary>
        public void RequestReload()
        {
            if (!_started)
                return;
            Initialize?.Invoke();
            LoadContent?.Invoke(ContentManager);
        }
    }
}
