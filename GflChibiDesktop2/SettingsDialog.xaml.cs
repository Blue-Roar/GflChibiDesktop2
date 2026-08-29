using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace GflChibiDesktop2
{
    /// <summary>bool → Visibility（true=Visible）。</summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
    /// <summary>
    /// 设置对话框：消息提示 / 运行模块 / 界面选项。
    /// </summary>
    public partial class SettingsDialog : HandyControl.Controls.Window
    {
        private readonly MainWindow _main;

        public SettingsDialog(MainWindow main)
        {
            InitializeComponent();
            _main = main;
            DataContext = main.SettingsDataContext;
            UpdateLegacyOptionsVisibility();
            SyncModuleSelection();
        }

        /// <summary>旧版渲染模块 exe 缺失时隐藏对应渲染后端选项。</summary>
        private void UpdateLegacyOptionsVisibility()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            bool glExists = File.Exists(Path.Combine(baseDir, "app", "LegacyGL", "GflChibiDesktopLegacyGL.exe"));
            bool dxExists = File.Exists(Path.Combine(baseDir, "app", "LegacyDX", "GflChibiDesktopLegacyDX.exe"));

            if (!glExists)
            {
                rGraphicsGL.IsChecked = false;
                rGraphicsGL.Visibility = Visibility.Collapsed;
            }
            if (!dxExists)
            {
                rGraphicsDX.IsChecked = false;
                rGraphicsDX.Visibility = Visibility.Collapsed;
            }
            if (!glExists && !dxExists)
            {
                _main.EnableLegacy = false;
                lblLegacy404.Visibility = Visibility.Visible;
                chbEnableLegacy.IsChecked = false;
                chbEnableLegacy.IsEnabled = false;
                gridLegacy.Visibility = Visibility.Collapsed;
            }
            else
            {
                lblLegacy404.Visibility = Visibility.Collapsed;
                gridLegacy.Visibility = Visibility.Visible;
                chbEnableLegacy.IsEnabled = true;
            }
        }

        /// <summary>按全局后端设置勾选选项（DWM/ULW、OpenGL/DirectX 均为全局配置）。</summary>
        private void SyncModuleSelection()
        {
            var context = (MainWindowDataContext)DataContext;
            if (context.UseUlwTransparency)
            {
                rModuleRaylibULW.IsChecked = true;
            }
            else
            {
                rModuleRaylibDWM.IsChecked = true;
            }
            if (context.UseOpenGL)
            {
                rGraphicsGL.IsChecked = true;
            }
            else
            {
                rGraphicsDX.IsChecked = true;
            }
        }

        private void Module_Checked(object sender, RoutedEventArgs e)
        {
            var context = (MainWindowDataContext)DataContext;
            if (sender == rModuleRaylibDWM || sender == rModuleRaylibULW)
            {
                // 新版（raylib）透明模式（全局）：DWM / ULW
                context.UseUlwTransparency = rModuleRaylibULW.IsChecked == true;
            }
            else if (sender == rGraphicsGL || sender == rGraphicsDX)
            {
                // 旧版（monogame）渲染后端（全局）：OpenGL / DirectX
                context.UseOpenGL = rGraphicsGL.IsChecked == true;
            }
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>全局“启用旧版”切换：同步 MainWindow 各实例的版本切换面板可见性。</summary>
        private void chbEnableLegacy_Click(object sender, RoutedEventArgs e)
        {
            _main.ApplyLegacyEnableState();
            // 禁用旧版时，取消“设为默认”（绑定会同步写回全局 UseLegacyModule）
            var context = (MainWindowDataContext)DataContext;
            if (!context.EnableLegacy && context.UseLegacyModule)
            {
                chkDefaultLegacy.IsChecked = false;
            }
        }

        /// <summary>“禁用新版”切换：同步 MainWindow 各实例的版本切换面板可见性。</summary>
        private void chbDisableRaylib_Click(object sender, RoutedEventArgs e)
        {
            _main.ApplyLegacyEnableState();
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            _main.ShowAbout();
        }
    }
}
