using GflChibiDesktop;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
        readonly List<PetInstance> pets = new();
        int nextPetId = 1;
        bool isExiting = false;
        GflChibiDesktop.Windows.DataManagerWindow? dataManagerWindow;

        string AppDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app") + Path.DirectorySeparatorChar;

        private PetInstance? SelectedPet => (PetTabs.SelectedItem as TabItem)?.Tag as PetInstance;

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
            AutoStartInstance();
            notifyIcon.Init();
            WindowState = WindowState.Minimized;
            Window_StateChanged(null, new());
        }

        /// <summary>
        /// 启动时恢复上次会话的桌宠实例（多开也一并恢复）。
        /// </summary>
        private void AutoStartInstance()
        {
            List<GflChibiDesktop.Windows.ChibiModelData>? saved = LoadSavedInstances();
            if (saved != null && saved.Count > 0)
            {
                foreach (var m in saved)
                {
                    StartInstance(m);
                }
                return;
            }

            string nameFile = Path.Combine(AppDir, "assets", "name.txt");
            string modelFile = Path.Combine(AppDir, "assets", "model.conf.json");
            if (!File.Exists(nameFile) || !File.Exists(modelFile))
            {
                return;
            }
            StartInstance(null);
        }

        private static string InstancesFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "instances.json");

        private List<GflChibiDesktop.Windows.ChibiModelData> LoadSavedInstances()
        {
            try
            {
                if (File.Exists(InstancesFilePath))
                {
                    return JsonConvert.DeserializeObject<List<GflChibiDesktop.Windows.ChibiModelData>>(File.ReadAllText(InstancesFilePath)) ?? new List<GflChibiDesktop.Windows.ChibiModelData>();
                }
            }
            catch
            {
            }
            return new List<GflChibiDesktop.Windows.ChibiModelData>();
        }

        private void SaveInstances()
        {
            try
            {
                var list = pets.Select(p => p.Model).Where(m => m != null).Cast<GflChibiDesktop.Windows.ChibiModelData>().ToList();
                File.WriteAllText(InstancesFilePath, JsonConvert.SerializeObject(list, Newtonsoft.Json.Formatting.Indented));
            }
            catch
            {
            }
        }

        /// <summary>
        /// 开启一个新的桌宠实例。
        /// </summary>
        private void StartNewInstance(object sender, RoutedEventArgs e)
        {
            StartInstance(null);
        }

        private void StartInstance(GflChibiDesktop.Windows.ChibiModelData? model)
        {
            if (isExiting)
            {
                return;
            }
            try
            {
                PetInstance pet = PetInstance.Create(nextPetId++, model);
                pet.Manager = new ProcessManager(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app/luajit.exe"),
                    pet.WorkDir,
                    "main.lua",
                    () => Dispatcher.Invoke(() => ReadIpc(pet)));
                pet.Manager.Exited += (_, _) => Dispatcher.Invoke(() => OnPetExited(pet));
                pets.Add(pet);
                AddTab(pet);
                PetTabs.SelectedItem = pet.Tab;
                UpdateStatus();
            }
            catch (Exception ex)
            {
                HandyControl.Controls.Growl.ErrorGlobal($"开启桌宠实例失败。\n{ex.Message}");
            }
        }

        /// <summary>
        /// 用新模型重启当前选中的桌宠。
        /// </summary>
        private async Task RestartSelected(GflChibiDesktop.Windows.ChibiModelData data)
        {
            PetInstance? pet = SelectedPet;
            if (pet is null)
            {
                StartInstance(data);
                return;
            }
            pet.UpdateModel(data);
            if (pet.TabTitle is not null)
            {
                pet.TabTitle.Text = pet.Name;
            }
            await StopManager(pet);
            pet.Panel.Children.Clear();
            pet.IsChanged = false;
            pet.Manager = new ProcessManager(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app/luajit.exe"),
                pet.WorkDir,
                "main.lua",
                () => Dispatcher.Invoke(() => ReadIpc(pet)));
            pet.Manager.Exited += (_, _) => Dispatcher.Invoke(() => OnPetExited(pet));
            UpdateStatus();
        }

        private async Task StopManager(PetInstance pet)
        {
            if (pet.Manager is null)
            {
                return;
            }
            context.IsBusyClosing = true;
            pet.Manager.TryCloseWindow();
            try
            {
                await Task.Delay(1000);
            }
            catch (OperationCanceledException)
            {
            }
            if (pet.Manager != null && !pet.Manager.process.HasExited)
            {
                pet.Manager.ForceCloseWindow();
            }
            pet.Manager?.Dispose();
            pet.Manager = null;
            context.IsBusyClosing = false;
        }

        private async void StopSelectedInstance(object sender, RoutedEventArgs e)
        {
            PetInstance? pet = SelectedPet;
            if (pet is null)
            {
                return;
            }
            await StopInstance(pet);
        }

        private async Task StopInstance(PetInstance pet)
        {
            if (!pets.Contains(pet))
            {
                return;
            }
            await StopManager(pet);
            RemovePet(pet);
        }

        private void OnPetExited(PetInstance pet)
        {
            if (!pets.Contains(pet))
            {
                return;
            }
            try
            {
                pet.Manager?.Dispose();
            }
            catch
            {
            }
            pet.Manager = null;
            RemovePet(pet);
        }

        private void RemovePet(PetInstance pet)
        {
            pets.Remove(pet);
            if (pet.Tab is not null)
            {
                PetTabs.Items.Remove(pet.Tab);
            }
            try
            {
                pet.Dispose();
            }
            catch
            {
            }
            UpdateStatus();
        }

        private void AddTab(PetInstance pet)
        {
            var tab = new TabItem();
            pet.Tab = tab;
            tab.Tag = pet;

            var header = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            pet.TabTitle = new TextBlock { Text = pet.Name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            var close = new Button
            {
                Content = "✕",
                Width = 20,
                Height = 20,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                ToolTip = "结束该桌宠"
            };
            close.Click += (_, _) => _ = StopInstance(pet);
            header.Children.Add(pet.TabTitle);
            header.Children.Add(close);
            tab.Header = header;

            var scroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = pet.Panel
            };
            tab.Content = scroller;
            PetTabs.Items.Add(tab);
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
            SaveInstances();
            notifyIcon.Visibility = Visibility.Hidden;
            notifyIcon.Dispose();
            foreach (PetInstance pet in pets.ToList())
            {
                try
                {
                    pet.Manager?.TryCloseWindow();
                }
                catch
                {
                }
            }
            Thread.Sleep(2000);
            foreach (PetInstance pet in pets.ToList())
            {
                try
                {
                    pet.Manager?.ForceCloseWindow();
                }
                catch
                {
                }
                try
                {
                    pet.Manager?.Dispose();
                }
                catch
                {
                }
            }
            pets.Clear();
        }

        private void SaveConfig(object sender, RoutedEventArgs e)
        {
            PetInstance? pet = SelectedPet;
            if (pet?.Manager is null)
            {
                throw new NullReferenceException();
            }
            using var w = pet.Manager.txIpc.BeginWrite();
            w.Write(2);
            foreach (var i in pet.Panel.Children)
            {
                (i as ISaveableControl)?.Save(w);
            }
            w.Write(0);
            pet.IsChanged = false;
            context.IsChanged = false;
        }

        private void DiscardConfigChange(object sender, RoutedEventArgs e)
        {
            PetInstance? pet = SelectedPet;
            if (pet?.Manager is null)
            {
                throw new NullReferenceException();
            }
            using var w = pet.Manager.txIpc.BeginWrite();
            w.Write(3);
        }

        private void ReadIpc(PetInstance pet)
        {
            var reader = pet.Manager?.rxIpc.GetReader();
            if (reader is not null)
            {
                while (reader.Next())
                {
                    switch (reader.ReadInt())
                    {
                        case 0:
                            pet.Panel.Children.Clear();
                            pet.IsChanged = false;
                            if (SelectedPet == pet)
                            {
                                context.IsChanged = false;
                            }
                            break;
                        case 1:
                            {
                                SingleLineTextControl c = new(pet.Panel.Children.Count + 1);
                                c.PromptText = reader.ReadString();
                                c.HintText = reader.ReadString();
                                if (reader.ReadInt() == 1)
                                {
                                    c.Type = SingleLineTextControl.SingleLineTextType.Integer;
                                    c.InputContent = reader.ReadInt().ToString();
                                }
                                c.PropertyChanged += (_, _) => OnPetControlChanged(pet);
                                c.changed = false;
                                pet.Panel.Children.Add(c);
                            }
                            break;
                        case 2:
                            {
                                BoolControl c = new(pet.Panel.Children.Count + 1);
                                c.PromptText = reader.ReadString();
                                c.HintText = reader.ReadString();
                                c.Choice = reader.ReadInt() != 0;
                                c.PropertyChanged += (_, _) => OnPetControlChanged(pet);
                                c.changed = false;
                                pet.Panel.Children.Add(c);
                            }
                            break;
                        case 3:
                            {
                                ReadonlyTextControl c = new(reader.ReadString());
                                pet.Panel.Children.Add(c);
                            }
                            break;
                        case 4:
                            {
                                ButtonControl c = new(pet.Panel.Children.Count + 1, pet.Manager!.txIpc);
                                c.PromptText = reader.ReadString();
                                c.HintText = reader.ReadString();
                                pet.Panel.Children.Add(c);
                            }
                            break;
                    }
                }
            }
        }

        private void OnPetControlChanged(PetInstance pet)
        {
            pet.IsChanged = true;
            if (SelectedPet == pet)
            {
                context.IsChanged = true;
            }
        }

        private void PetTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            PetInstance? pet = SelectedPet;
            context.IsRunning = pet?.Manager != null;
            context.IsChanged = pet?.IsChanged ?? false;
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                ShowInTaskbar = false;
                Hide();
                HandyControl.Controls.Growl.InfoGlobal("少前桌宠已最小化到系统托盘。双击托盘图标显示主窗口。");
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
                if (data.NewInstance)
                {
                    StartInstance(data);
                    HandyControl.Controls.Growl.InfoGlobal($"已多开 {data.DisplayName}。");
                }
                else
                {
                    await RestartSelected(data);
                    HandyControl.Controls.Growl.InfoGlobal($"已加载 {data.DisplayName}，战术人形已应用。");
                }
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
