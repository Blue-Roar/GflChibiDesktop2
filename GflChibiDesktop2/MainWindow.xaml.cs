using GflChibiDesktop;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using static GflChibiDesktop.WebAPI;

namespace HDTLPanel
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const string settingsPath = "settings.json";

        public readonly Version productVersion = new Version(((AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyFileVersionAttribute))).Version);
        public readonly Version productBuild = Assembly.GetExecutingAssembly().GetName().Version;
        public readonly string currentBuild = ((AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyInformationalVersionAttribute))).InformationalVersion;

        readonly MainWindowDataContext context = new();
        ProcessManager? manager;
        bool isExiting = false;
        GflChibiDesktop.Windows.DataManagerWindow? dataManagerWindow;

        public MainWindow()
        {
            InitializeComponent();
            window.Title += $" {productVersion}";
            lblVersion.Content = $"程序版本 {productBuild}";

            bool StartupPost = false;
            string StartupStr = HttpRequestHelper.PostWebRequest("https://api.brightsu.cn/GflChibiDesktop2/startup", $"version={productVersion}&build={productBuild}/{currentBuild}", Encoding.UTF8, ref StartupPost);
            if (StartupPost)
            {
                StartupRoot rt = JsonConvert.DeserializeObject<StartupRoot>(StartupStr);
                if (rt.ret != 200)
                {
                    HandyControl.Controls.Growl.WarningGlobal($"API接口调用失败。部分功能可能会受到影响。\n错误：API 接口返回了状态码 {rt.ret}");
                }
            }
            else
            {
                HandyControl.Controls.Growl.WarningGlobal($"API接口调用失败。部分功能可能会受到影响。\n{StartupStr}");
            }

            DataContext = context;
            SwitchSubprogramRunningStatus(null, new());
            notifyIcon.Init();
            WindowState = WindowState.Minimized;
            Window_StateChanged(null, new());
        }

        async private void SwitchSubprogramRunningStatus(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (context.IsRunning)
                {
                    await StopSubprogram();
                }
                else
                {
                    StartSubprogram();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private async Task StopSubprogram()
        {
            if (manager is not null)
            {
                context.IsBusyClosing = true;
                context.IsChanged = false;
                MainStackPanel.Children.Clear();
                manager.TryCloseWindow();
                try
                {
                    await Task.Delay(1000);
                }
                catch (OperationCanceledException)
                {
                }
                if (isExiting)
                {
                    context.IsBusyClosing = false;
                    return;
                }
                if (context.IsRunning)
                {
                    manager.ForceCloseWindow();
                }
                manager.Dispose();
                manager = null;
                context.IsBusyClosing = false;
            }
            context.IsRunning = false;
        }

        private void StartSubprogram()
        {
            if (isExiting)
            {
                return;
            }
            try
            {
                context.IsRunning = true;
                manager = new ProcessManager(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app/luajit.exe"), System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app"), "main.lua", () => Dispatcher.Invoke(ReadIpc));
                manager.Exited += (_, _) =>
                {
                    context.IsRunning = false;
                    context.IsChanged = false;
                    Dispatcher.Invoke(() => {
                        MainStackPanel.Children.Clear();
                        if (WindowState == WindowState.Minimized)
                        {
                            WindowState = WindowState.Normal;
                        }
                    });
                };
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private async Task RestartSubprogram()
        {
            await StopSubprogram();
            if (!isExiting)
            {
                StartSubprogram();
            }
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (isExiting)
            {
                return;
            }

            foreach (Window window in Application.Current.Windows)
            {
                if (window is GflChibiDesktop.Windows.DataManagerWindow dm && dm.IsVisible)
                {
                    e.Cancel = true;
                    dm.Activate();
                    HandyControl.Controls.Growl.InfoGlobal("请先关闭数据管理窗口，再关闭本程序。");
                    return;
                }
            }
        }

        private void ExitApplication(object sender, RoutedEventArgs e)
        {
            isExiting = true;
            Application.Current.Shutdown();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            isExiting = true;
            notifyIcon.Visibility = Visibility.Hidden;
            notifyIcon.Dispose();
            if (manager is not null)
            {
                manager.TryCloseWindow();
                Thread.Sleep(2000);
                if (context.IsRunning)
                {
                    manager.ForceCloseWindow();
                }
                manager.Dispose();
            }
        }

        private void SaveConfig(object sender, RoutedEventArgs e)
        {
            if (manager is null) throw new NullReferenceException();
            using var w = manager.txIpc.BeginWrite();
            w.Write(2);
            foreach (var i in MainStackPanel.Children)
            {
                (i as ISaveableControl)?.Save(w);
            }
            w.Write(0);
            context.IsChanged = false;
        }

        private void DiscardConfigChange(object sender, RoutedEventArgs e)
        {
            if (manager is null) throw new NullReferenceException();
            using var w = manager.txIpc.BeginWrite();
            w.Write(3);
        }

        private void FlipModel(object sender, RoutedEventArgs e)
        {
            if (manager is not null)
            {
                using var writer = manager.txIpc.BeginWrite();
                writer.Write(1);
            }
        }

        private void ReadIpc()
        {
            var reader = manager?.rxIpc.GetReader();
            if (reader is not null)
            {
                while (reader.Next())
                {
                    switch (reader.ReadInt())
                    {
                        case 0:
                            MainStackPanel.Children.Clear();
                            context.IsChanged = false;
                            break;
                        case 1:
                            {
                                SingleLineTextControl c = new(MainStackPanel.Children.Count + 1);
                                c.PromptText = reader.ReadString();
                                c.HintText = reader.ReadString();
                                if (reader.ReadInt() == 1)
                                {
                                    c.Type = SingleLineTextControl.SingleLineTextType.Integer;
                                    c.InputContent = reader.ReadInt().ToString();
                                }
                                c.PropertyChanged += (_, _) => context.IsChanged = true;
                                c.changed = false;
                                MainStackPanel.Children.Add(c);
                            }
                            break;
                        case 2:
                            {
                                BoolControl c = new(MainStackPanel.Children.Count + 1);
                                c.PromptText = reader.ReadString();
                                c.HintText = reader.ReadString();
                                c.Choice = reader.ReadInt() != 0;
                                c.PropertyChanged += (_, _) => context.IsChanged = true;
                                c.changed = false;
                                MainStackPanel.Children.Add(c);
                            }
                            break;
                        case 3:
                            {
                                ReadonlyTextControl c = new(reader.ReadString());
                                MainStackPanel.Children.Add(c);
                            }
                            break;
                        case 4:
                            {
                                ButtonControl c = new(MainStackPanel.Children.Count + 1, manager!.txIpc);
                                c.PromptText = reader.ReadString();
                                c.HintText = reader.ReadString();
                                MainStackPanel.Children.Add(c);
                            }
                            break;
                    }
                }
            }
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                ShowInTaskbar = false;
                Hide();
                HandyControl.Controls.Growl.InfoGlobal("少前桌宠已最小化到系统托盘。双击托盘图标显示主窗口。");
                //notifyIcon.ShowBalloonTip("少女前线桌面Q宠", "双击托盘图标显示主窗口", HandyControl.Data.NotifyIconInfoType.Info);
            }
        }

        private void ChangeAutoRun(object sender, RoutedEventArgs e)
        {
            context.IsAutoRun = !context.IsAutoRun;
        }

        private void notifyIcon_MouseDoubleClick(object sender, RoutedEventArgs e)
        {
            Visibility = Visibility.Visible;
            ShowInTaskbar = true;
            WindowState = WindowState.Normal;
            Activate();
        }

        private void GflChibiDesktop(object sender, RoutedEventArgs e)
        {
            if (dataManagerWindow is null)
            {
                dataManagerWindow = new GflChibiDesktop.Windows.DataManagerWindow();
                dataManagerWindow.ModelLoadRequested += DataManagerWindow_ModelLoadRequested;
                dataManagerWindow.Closed += (_, _) => dataManagerWindow = null;
            }
            dataManagerWindow.Show();
            dataManagerWindow.Activate();
        }

        private async void DataManagerWindow_ModelLoadRequested(GflChibiDesktop.Windows.ChibiModelData data)
        {
            try
            {
                await RestartSubprogram();
                HandyControl.Controls.Growl.InfoGlobal($"已加载 {data.DisplayName}，战术人形已应用。");
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    class MainWindowDataContext : INotifyPropertyChanged
    {
        private bool isRunning = false;
        private bool isBusyClosing = false;
        private bool isChanged = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsRunning
        {
            get => isRunning;
            set
            {
                isRunning = value;
                OnPropertyChanged();
            }
        }

        public bool IsBusyClosing
        {
            get => isBusyClosing;
            set
            {
                isBusyClosing = value;
                OnPropertyChanged();
            }
        }

        public bool IsChanged
        {
            get => isChanged;
            set
            {
                isChanged = value;
                OnPropertyChanged();
            }
        }

        public bool IsAutoRun
        {
            get => AutoRun.IsAutoRun(AppDomain.CurrentDomain.BaseDirectory + "HDTLPanel.exe", "HuiDesktop启动器与控制面板");
            set
            {
                AutoRun.SetAutoRun(AppDomain.CurrentDomain.BaseDirectory + "HDTLPanel.exe", "HuiDesktop启动器与控制面板", value);
                OnPropertyChanged();
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class BoolToStringValueConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool v && parameter is string s) return s.Split('/')[v ? 1 : 0];
            return null;
        }

        public object? ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    public class ReverseBooleanValueConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool v) return !v;
            return null;
        }

        public object? ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
