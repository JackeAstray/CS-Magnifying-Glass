using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Magnifying_Glass
{
    public partial class MagnifierWindow : Window
    {
        private DispatcherTimer _timer;
        private double _magnification = 2.0;
        private int _magnifierSize = 200;

        // Zero-allocation buffer fields
        private Bitmap _captureBmp;
        private Graphics _captureGraphics;
        private WriteableBitmap _writeableBitmap;
        private int _lastCaptureWidth;
        private int _lastCaptureHeight;

        public MagnifierWindow()
        {
            InitializeComponent();
            SetupWindow();
            StartCapture();
            this.Visibility = Visibility.Hidden;
        }

        private void SetupWindow()
        {
            this.Width = _magnifierSize;
            this.Height = _magnifierSize;
            MagnifierBorder.CornerRadius = new CornerRadius(_magnifierSize / 2.0);
            CenterWindow();
        }

        public void CenterWindow()
        {
            this.Left = (SystemParameters.PrimaryScreenWidth - this.Width) / 2;
            this.Top = (SystemParameters.PrimaryScreenHeight - this.Height) / 2;
        }

        public void EnableMagnifier(bool enabled)
        {
            if (enabled)
            {
                this.Visibility = Visibility.Visible;
                _timer.Start();
            }
            else
            {
                this.Visibility = Visibility.Hidden;
                _timer.Stop();
            }
        }

        public void UpdateSettings(double zoom, int size)
        {
            _magnification = zoom;
            _magnifierSize = size;
            this.Width = size;
            this.Height = size;
            MagnifierBorder.CornerRadius = new CornerRadius(size / 2.0);
            CenterWindow();
        }

        public void UpdateFps(int fps)
        {
            if (_timer != null)
            {
                _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps);
            }
        }

        private void StartCapture()
        {
            _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(5) }; // fast refresh
            _timer.Tick += (s, e) => CaptureScreen();
        }

        private void CaptureScreen()
        {
            if (this.Visibility != Visibility.Visible) return;

            // Get standard DPI scaling from WPF properly
            double dpiX = 1.0;
            double dpiY = 1.0;
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            // Convert logical size to physical pixels
            int physicalMagnifierWidth = (int)(_magnifierSize * dpiX);
            int physicalMagnifierHeight = (int)(_magnifierSize * dpiY);

            // Compute capture size based on the physical size, not the logical size
            int captureWidth = Math.Max(1, (int)(physicalMagnifierWidth / _magnification));
            int captureHeight = Math.Max(1, (int)(physicalMagnifierHeight / _magnification));

            // Compute center of primary screen in physical pixels
            // This needs to correctly factor into Screen Bounds, not just PrimaryScreenWidth (which is logical).
            int screenWidthPhysical = (int)(SystemParameters.PrimaryScreenWidth * dpiX);
            int screenHeightPhysical = (int)(SystemParameters.PrimaryScreenHeight * dpiY);

            int centerX = screenWidthPhysical / 2;
            int centerY = screenHeightPhysical / 2;

            int startX = centerX - (captureWidth / 2);
            int startY = centerY - (captureHeight / 2);

            if (_captureBmp == null || _lastCaptureWidth != captureWidth || _lastCaptureHeight != captureHeight)
            {
                _captureGraphics?.Dispose();
                _captureBmp?.Dispose();
                _captureBmp = new Bitmap(captureWidth, captureHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                _captureGraphics = Graphics.FromImage(_captureBmp);

                // Initialize a high-performance WriteableBitmap
                _writeableBitmap = new WriteableBitmap(captureWidth, captureHeight, 96, 96, PixelFormats.Bgra32, null);
                MagnifierBrush.ImageSource = _writeableBitmap;

                _lastCaptureWidth = captureWidth;
                _lastCaptureHeight = captureHeight;
            }

            // 1. Copy screen to reusable RAM Bitmap
            _captureGraphics.CopyFromScreen(startX, startY, 0, 0, new System.Drawing.Size(captureWidth, captureHeight), CopyPixelOperation.SourceCopy);

            // 2. Perform direct memory transplant to WPF rendering buffer (0 GC Allocations, Lightning Fast)
            BitmapData data = _captureBmp.LockBits(new Rectangle(0, 0, captureWidth, captureHeight), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            _writeableBitmap.Lock();
            CopyMemory(_writeableBitmap.BackBuffer, data.Scan0, (uint)(data.Stride * captureHeight));
            _writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, captureWidth, captureHeight));
            _writeableBitmap.Unlock();
            _captureBmp.UnlockBits(data);

            this.Visibility = Visibility.Visible;
        }

        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        private static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            
            // Allow mouse events to pass through, and hide from Alt+Tab
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
            
            // On Windows 10/11, setting WDA_EXCLUDEFROMCAPTURE (0x00000011) hides the window from our own CaptureScreen
            SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);
        
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        private static extern uint SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);
    }
}