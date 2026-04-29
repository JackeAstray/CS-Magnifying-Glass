using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Magnifying_Glass
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MagnifierWindow _magnifierWindow;
        private bool _isMagnifierEnabled = false;

        public MainWindow()
        {
            InitializeComponent();
            _magnifierWindow = new MagnifierWindow();
            _magnifierWindow.Show();
            
            // Register hotkeys
            ComponentDispatcher.ThreadPreprocessMessage += ComponentDispatcher_ThreadPreprocessMessage;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            RegisterHotKey(helper.Handle, F8_HOTKEY_ID, 0, (uint)KeyInterop.VirtualKeyFromKey(Key.F8));
            RegisterHotKey(helper.Handle, F9_HOTKEY_ID, 0, (uint)KeyInterop.VirtualKeyFromKey(Key.F9));
        }

        protected override void OnClosed(EventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            UnregisterHotKey(helper.Handle, F8_HOTKEY_ID);
            UnregisterHotKey(helper.Handle, F9_HOTKEY_ID);
            _magnifierWindow.Close();
            base.OnClosed(e);
        }

        private void ComponentDispatcher_ThreadPreprocessMessage(ref MSG msg, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg.message == WM_HOTKEY)
            {
                int id = msg.wParam.ToInt32();
                if (id == F8_HOTKEY_ID)
                {
                    this.Visibility = this.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
                    handled = true;
                }
                else if (id == F9_HOTKEY_ID)
                {
                    _isMagnifierEnabled = !_isMagnifierEnabled;
                    _magnifierWindow.EnableMagnifier(_isMagnifierEnabled);
                    handled = true;
                }
            }
        }

        private void SldZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtZoom != null && _magnifierWindow != null)
            {
                TxtZoom.Text = $"Zoom Level: x{e.NewValue:F1}";
                _magnifierWindow.UpdateSettings(e.NewValue, (int)SldSize.Value);
            }
        }

        private void SldSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtSize != null && _magnifierWindow != null)
            {
                TxtSize.Text = $"Size: {(int)e.NewValue}px";
                _magnifierWindow.UpdateSettings(SldZoom.Value, (int)e.NewValue);
            }
        }

        private const int F8_HOTKEY_ID = 9000;
        private const int F9_HOTKEY_ID = 9001;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vlc);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}