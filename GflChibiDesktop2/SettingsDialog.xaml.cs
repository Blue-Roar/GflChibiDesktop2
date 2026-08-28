using System.Windows;
using System.Windows.Controls;

namespace GflChibiDesktop2
{
    /// <summary>
    /// 设置对话框：消息提示 / 运行模块 / 界面选项。
    /// </summary>
    public partial class SettingsDialog : HandyControl.Controls.Window
    {
        private readonly MainWindow _main;

        public SettingsDialog(MainWindow main)
        {
            InitializeComponent();
            if (Properties.Settings.Default.DisableCustomWindowChrome)
            {
                // 移除 HandyControl 构造中强制设置的 WindowChrome（System.Windows.Shell），恢复系统非客户区
                System.Windows.Shell.WindowChrome.SetWindowChrome(this, null);
                Style = null;
                WindowStyle = WindowStyle.SingleBorderWindow;
            }
            _main = main;
            DataContext = main.SettingsDataContext;
            SyncModuleSelection();
        }

        /// <summary>按当前设置勾选运行模块单选。</summary>
        private void SyncModuleSelection()
        {
            var context = (MainWindowDataContext)DataContext;
            bool legacy = context.UseLegacyModule;
            bool useOpenGL = context.UseOpenGL;
            bool ulw = context.UseUlwTransparency;

            if (legacy)
            {
                rModuleMonogame.IsChecked = true;
                if (useOpenGL)
                {
                    rGraphicsGL.IsChecked = true;
                }
                else
                {
                    rGraphicsDX.IsChecked = true;
                }
            }
            else
            {
                rGraphicsGL.IsChecked = true;
                if (ulw)
                {
                    rModuleRaylibULW.IsChecked = true;
                }
                else
                {
                    rModuleRaylibDWM.IsChecked = true;
                }
            }
        }

        private void Module_Checked(object sender, RoutedEventArgs e)
        {
            bool legacy, useOpenGL, ulw;

            useOpenGL = rGraphicsGL.IsChecked == true;
            legacy = rModuleMonogame.IsChecked == true;
            ulw = rModuleRaylibULW.IsChecked == true;

            _main.ApplyModule(legacy, useOpenGL, ulw);
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            _main.ShowAbout();
        }
    }
}
