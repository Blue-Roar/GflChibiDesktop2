using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GflChibiDesktop
{
    /// <summary>
    /// 承载 MonoGame 游戏循环的宿主。使用隐藏的 SDL 窗口创建 GraphicsDevice，
    /// 离屏渲染到 RenderTarget2D 后读回像素，由 MonoGameControl 显示到 WPF。
    /// </summary>
    public class MonoGameHost : Game
    {
        private readonly MonoGameControl _control;
        private readonly GraphicsDeviceManager _graphics;

        private RenderTarget2D _target;
        private int _targetWidth;
        private int _targetHeight;
        private Color[] _pixelData;
        private byte[] _pixelBuffer;

        /// <summary>Initialize/LoadContent 完成后置为 true，随后每帧调用 Update/Draw。</summary>
        public bool Ready;

        public int TargetWidth => _targetWidth;
        public int TargetHeight => _targetHeight;

        /// <summary>BGRA 预乘像素缓冲（与 Pbgra32 对应），每帧由 Draw 填充。</summary>
        public byte[] PixelBuffer => _pixelBuffer;

        public MonoGameHost(MonoGameControl control, GraphicsProfile profile)
        {
            _control = control;
            _graphics = new GraphicsDeviceManager(this)
            {
                GraphicsProfile = profile,
                SynchronizeWithVerticalRetrace = false
            };
            IsFixedTimeStep = false;
            InactiveSleepTime = TimeSpan.Zero;
            IsMouseVisible = false;
        }

        /// <summary>驱动一帧游戏循环，须在创建宿主同一线程（WPF UI 线程）调用。</summary>
        public void RenderFrame()
        {
            if (!Ready)
                return;
            Tick();
        }

        protected override void Update(GameTime gameTime)
        {
            if (!Ready)
                return;

            // 部分 Player 在 Update 中渲染（如 2.1.25），部分在 Draw 中渲染（如 3.8.95）。
            // 因此在 Update 开始前绑定渲染目标，直到 Draw 结束统一读回。
            EnsureTarget();
            GraphicsDevice.SetRenderTarget(_target);
            GraphicsDevice.Clear(Color.Transparent);

            _control.Update?.Invoke(gameTime);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            if (!Ready)
                return;

            EnsureTarget();

            _control.Draw?.Invoke();

            GraphicsDevice.SetRenderTarget(null);
            _target.GetData(_pixelData);
            ConvertPixels();

            base.Draw(gameTime);
        }

        private void EnsureTarget()
        {
            double width = App.globalValues.FrameWidth;
            double height = App.globalValues.FrameHeight;
            if (width <= 0) width = 448;
            if (height <= 0) height = 448;

            int w = (int)Math.Round(width);
            int h = (int)Math.Round(height);
            if (_target != null && _targetWidth == w && _targetHeight == h)
                return;

            _target?.Dispose();
            _targetWidth = w;
            _targetHeight = h;
            _target = new RenderTarget2D(GraphicsDevice, w, h, false, SurfaceFormat.Color,
                DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            _pixelData = new Color[w * h];
            _pixelBuffer = new byte[w * h * 4];
        }

        /// <summary>Color[]（RGBA 预乘）转 BGRA 字节，用于 Pbgra32 WriteableBitmap。</summary>
        private void ConvertPixels()
        {
            Color[] src = _pixelData;
            byte[] dst = _pixelBuffer;
            for (int i = 0, j = 0; i < src.Length; i++, j += 4)
            {
                dst[j] = src[i].B;
                dst[j + 1] = src[i].G;
                dst[j + 2] = src[i].R;
                dst[j + 3] = src[i].A;
            }
        }
    }
}
