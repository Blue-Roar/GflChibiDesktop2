using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GflChibiDesktop
{
    /// <summary>
    /// WindowCG.xaml 的交互逻辑
    /// </summary>
    public partial class WindowCG : Window
    {
        [DllImport("user32", EntryPoint = "SetWindowLong")]
        private static extern uint SetWindowLong(IntPtr hwnd, int nIndex, long dwNewLong);

        [DllImport("user32", EntryPoint = "GetWindowLong")]
        private static extern uint GetWindowLong(IntPtr hwnd, int nIndex);

        public long OldLong;

        public WindowCG()
        {
            InitializeComponent();
        }


        Uri cg_n_uri;
        Uri cg_d_uri;

        private void Window_IsHitTestVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            try
            {
                if (IsHitTestVisible)
                {
                    IntPtr hwnd = new WindowInteropHelper(this).Handle;
                    SetWindowLong(hwnd, (-20), OldLong);
                }
                else
                {
                    IntPtr hwnd = new WindowInteropHelper(this).Handle;
                    SetWindowLong(hwnd, (-20), 0x20);
                }
            }
            catch (Exception)
            {

            }
        }

        public void LoadCG(string dummy_display, string cg_url, string cg_n_filename, string cg_d_filename)
        {
            try
            {
                Title = dummy_display;
                menuItem_dummy.Header = dummy_display;
                cg_n_uri = new Uri($"{cg_url}{cg_n_filename}", UriKind.Absolute);
                ImageCG.Source = new BitmapImage(cg_n_uri);
                menuItem_d.IsEnabled = true;
                menuItem_d.Visibility = Visibility.Visible;
                cg_d_uri = new Uri($"{cg_url}{cg_d_filename}", UriKind.Absolute);
                Show();
            }
            catch (Exception)
            {

            }
        }

        public void LoadCG(string dummy_display, string cg_url, string cg_n_filename)
        {
            try
            {
                Title = dummy_display;
                menuItem_dummy.Header = dummy_display;
                cg_n_uri = new Uri($"{cg_url}{cg_n_filename}", UriKind.Absolute);
                ImageCG.Source = new BitmapImage(cg_n_uri);
                menuItem_d.IsEnabled = false;
                menuItem_d.Visibility = Visibility.Collapsed;
                Show();
            }
            catch (Exception)
            {

            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            if (e.Delta > 0)
            {
                sld_zoomScale.Value++;
            }
            else if (e.Delta < 0)
            {
                sld_zoomScale.Value--;
            }
            sld_zoomScale_ValueChanged(this, null);
        }

        private void menuItem_Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void menuItem_zoom25_Click(object sender, RoutedEventArgs e)
        {
            sld_zoomScale.Value = 25;
        }

        private void menuItem_zoom50_Click(object sender, RoutedEventArgs e)
        {
            sld_zoomScale.Value = 50;
        }

        private void menuItem_zoom75_Click(object sender, RoutedEventArgs e)
        {
            sld_zoomScale.Value = 75;
        }

        private void menuItem_zoom100_Click(object sender, RoutedEventArgs e)
        {
            sld_zoomScale.Value = 100;
        }

        private void menuItem_topmost_Checked(object sender, RoutedEventArgs e)
        {
            Topmost = true;
        }

        private void menuItem_topmost_Unchecked(object sender, RoutedEventArgs e)
        {
            Topmost = false;
        }

        private void sld_zoomScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int scale = (int)sld_zoomScale.Value;

            vb_img.Width = 1024 * scale / 100;
            vb_img.Height = 1024 * scale / 100;
            Width = vb_img.Width / matrixDPI.M11;
            Height = vb_img.Height / matrixDPI.M22;
            menuItem_zoom200.IsChecked = false;
            menuItem_zoom150.IsChecked = false;
            menuItem_zoom100.IsChecked = false;
            menuItem_zoom75.IsChecked = false;
            menuItem_zoom50.IsChecked = false;
            menuItem_zoom25.IsChecked = false;
            switch (scale)
            {
                case 200:
                    menuItem_zoom200.IsChecked = true;
                    break;
                case 150:
                    menuItem_zoom150.IsChecked = true;
                    break;
                case 100:
                    menuItem_zoom100.IsChecked = true;
                    break;
                case 75:
                    menuItem_zoom75.IsChecked = true;
                    break;
                case 50:
                    menuItem_zoom50.IsChecked = true;
                    break;
                case 25:
                    menuItem_zoom25.IsChecked = true;
                    break;
            }
        }

        private void menuItem_d_Unchecked(object sender, RoutedEventArgs e)
        {
            ImageCG.Source = new BitmapImage(cg_n_uri);
        }

        private void menuItem_d_Checked(object sender, RoutedEventArgs e)
        {
            ImageCG.Source = new BitmapImage(cg_d_uri);
        }

        private void sld_alpha_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Opacity = sld_alpha.Value / 100;
        }

        private void menuItem_fixed_Checked(object sender, RoutedEventArgs e)
        {
            Cursor = Cursors.Arrow;
        }

        private void menuItem_fixed_Unchecked(object sender, RoutedEventArgs e)
        {
            Cursor = Cursors.SizeAll;
        }

        private void menuItem_disableInteraction_Checked(object sender, RoutedEventArgs e)
        {
            IsHitTestVisible = false;
        }

        private void menuItem_disableInteraction_Unchecked(object sender, RoutedEventArgs e)
        {
            IsHitTestVisible = true;
        }

        private void tbtn_reset_Click(object sender, EventArgs e)
        {
            menuItem_fixed.IsChecked = false;
            menuItem_disableInteraction.IsChecked = false;

        }

        Matrix matrixDPI;
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            matrixDPI = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;
            ScaleTransform dpiTransform = new ScaleTransform(1 / matrixDPI.M11, 1 / matrixDPI.M22);
            if (dpiTransform.CanFreeze) dpiTransform.Freeze();
            vb_img.LayoutTransform = dpiTransform;
            Width = vb_img.Width / matrixDPI.M11;
            Height = vb_img.Height / matrixDPI.M22;
        }

        private void menuItem_zoom200_Click(object sender, RoutedEventArgs e)
        {
            sld_zoomScale.Value = 200;
        }

        private void menuItem_zoom150_Click(object sender, RoutedEventArgs e)
        {
            sld_zoomScale.Value = 150;
        }
        
        private void Move_MouseMove(object sender, MouseEventArgs e)
        {
            if (!menuItem_fixed.IsChecked)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    DragMove();
                }
            }
        }

        private void mnenuItem_setPos_Click(object sender, RoutedEventArgs e)
        {
            HandyControl.Controls.Dialog.Show(new PosCGDialog(this));
        }
    }
}
