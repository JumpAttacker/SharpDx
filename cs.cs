using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Windows;
using System.Diagnostics;
using System.Collections.Generic;
using D3D11 = SharpDX.Direct3D11;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using D2DFactory = SharpDX.Direct2D1.Factory;
using DWriteFactory = SharpDX.DirectWrite.Factory;

namespace SharpDx
{
    public partial class NewForm : Form
    {
        #region DllImport
        [DllImport("User32.dll")]
        static extern short GetAsyncKeyState(Int32 vKey);
        [DllImport("USER32.DLL", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string lpClassName,
            string lpWindowName);
        [DllImport("USER32.DLL")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]

        private static extern UInt32 GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("dwmapi.dll")]
        static extern void DwmExtendFrameIntoClientArea(IntPtr hWnd, ref Margins pMargins);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);//

        [DllImport("user32.dll")]
        #endregion
        #region Members
        static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);//

        public const int GWL_EXSTYLE = -20;

        public const int WS_EX_LAYERED = 0x80000;

        public const int WS_EX_TRANSPARENT = 0x20;

        public const int LWA_ALPHA = 0x2;

        public const int LWA_COLORKEY = 0x1;
        internal struct Margins
        {
            public int Left, Right, Top, Bottom;
        }


        private static Margins marg;
        //private static Form _renderForm;

        // General Direct3D 11 stuff
        private static SwapChain _swapChain;
        private static D3D11.Device _d3d11Device;
        private static DeviceContext _d3d11DevCon;
        private static WindowRenderTarget renderTarget;

        // Rainbow background colors
        private static SolidColorBrush backgroundBrush;
        private static SolidColorBrush redBrush;

        private static RectangleF textRegionRect;
        private static RectangleF fullTextBackground;

        private static D2DFactory d2dFactory;
        private static DWriteFactory dwFactory;

        #endregion
        public NewForm()
        {
            InitializeComponent();

            #region Init
            //this = this.FindForm();
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.ClientSize = new System.Drawing.Size(284, 262);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;

            d2dFactory = new D2DFactory();
            dwFactory = new DWriteFactory(SharpDX.DirectWrite.FactoryType.Shared);

            CreateResources();
            SetWindowLong(this.Handle, GWL_EXSTYLE,
                    (IntPtr)(GetWindowLong(this.Handle, GWL_EXSTYLE) ^ WS_EX_LAYERED ^ WS_EX_TRANSPARENT));

            SetLayeredWindowAttributes(this.Handle, 0, 255, LWA_ALPHA);
            InitializeD3D11();
            var c = 100.0f;
            var rectangleGeometry = new RoundedRectangleGeometry(d2dFactory, new RoundedRectangle() { RadiusX = 32, RadiusY = 32, Rect = new RectangleF(200, 500, 300, 300) });
            var solidColorBrush = new SolidColorBrush(renderTarget, Color.Gold);
            #endregion
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            RenderLoop.Run(this, () =>
            {
                renderTarget.BeginDraw();
                //renderTarget.Clear(new Color4(0,0,0,0));
                //renderTarget.FillRectangle(textRegionRect, redBrush);
                //renderTarget.FillRectangle(fullTextBackground, backgroundBrush);
                //solidColorBrush.Color = new Color4(200, 200, 200, (float)Math.Abs(Math.Cos(stopwatch.ElapsedMilliseconds * .001)));
                c += 0.01f;
                rectangleGeometry = new RoundedRectangleGeometry(d2dFactory, new RoundedRectangle() { RadiusX = 32, RadiusY = 32, Rect = new RectangleF(c, c + 200, 300, 300) });
                renderTarget.FillGeometry(rectangleGeometry, redBrush, null);

                try
                {
                    renderTarget.EndDraw();
                    this.Invalidate();
                }
                catch
                {
                    CreateResources();
                }
            });
            #region Dispose
            d2dFactory.Dispose();
            dwFactory.Dispose();
            renderTarget.Dispose();
            solidColorBrush.Dispose();
            rectangleGeometry.Dispose();
            #endregion
        }
        private void InitializeD3D11()
        {
            ModeDescription bufferDescription = new ModeDescription()
            {
                Width = 0,
                Height = 0,
                RefreshRate = new Rational(60, 1),
                Format = Format.R8G8B8A8_UNorm
            };
            SwapChainDescription swapChainDescription = new SwapChainDescription()
            {
                ModeDescription = bufferDescription,
                SampleDescription = new SampleDescription(1, 0),
                Usage = Usage.RenderTargetOutput,
                BufferCount = 1,
                OutputHandle = this.Handle,
                IsWindowed = true
            };
            D3D11.Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.None,
                swapChainDescription, out _d3d11Device, out _swapChain);
            _d3d11DevCon = _d3d11Device.ImmediateContext;
            marg.Left = 0;
            marg.Top = 0;
            marg.Right = this.Width;
            marg.Bottom = this.Height;
            DwmExtendFrameIntoClientArea(this.Handle, ref marg);
        }
        private void CreateResources()
        {
            if (renderTarget != null) { renderTarget.Dispose(); }
            if (redBrush != null) { redBrush.Dispose(); }
            if (backgroundBrush != null) { backgroundBrush.Dispose(); }
            HwndRenderTargetProperties wtp = new HwndRenderTargetProperties();
            wtp.Hwnd = this.Handle;
            wtp.PixelSize = new Size2(this.ClientSize.Width, this.ClientSize.Height);
            wtp.PresentOptions = PresentOptions.Immediately;
            renderTarget = new WindowRenderTarget(d2dFactory, new RenderTargetProperties(), wtp);
            redBrush = new SolidColorBrush(renderTarget, Color.Red);
            textRegionRect = new RectangleF(10, 10, 300, 300);
            backgroundBrush = new SolidColorBrush(renderTarget, new Color4(0.0f, 50.0f, 255, 255.0f));
            fullTextBackground = new RectangleF(300, 300, 300, 300);

        }
    }
}
