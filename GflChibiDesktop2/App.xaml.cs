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
            mutex = new System.Threading.Mutex(true, "OnlyRun_GflChibiDesktop2_Merged");
            if (mutex.WaitOne(0, false))
            {
                base.OnStartup(e);
            }
            else
            {
                HandyControl.Controls.MessageBox.Show("此程序已有一个运行中的实例。", "注意", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
            }
        }
    }
}
