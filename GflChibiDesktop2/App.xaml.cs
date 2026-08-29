using System.Windows;

namespace GflChibiDesktop2
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static System.Threading.Mutex? mutex;
        protected override void OnStartup(StartupEventArgs e)
        {
            // 单实例互斥判断通过后才创建 MainWindow，
            // 避免在已运行实例时框架仍通过 StartupUri 加载 MainWindow 造成“闪现”
            mutex = new System.Threading.Mutex(true, "OnlyRun_GflChibiDesktop2_Merged");
            if (mutex.WaitOne(0, false))
            {
                base.OnStartup(e);
                new MainWindow().Show();
            }
            else
            {
                HandyControl.Controls.MessageBox.Show("此程序已有一个运行中的实例。", "注意", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK);
                Shutdown();
            }
        }
    }
}
