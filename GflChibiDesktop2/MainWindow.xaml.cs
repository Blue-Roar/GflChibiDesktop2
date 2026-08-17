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
        public readonly Version productVersion = GetProductVersion();
        public readonly string currentBuild = ((AssemblyInformationalVersionAttribute?)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyInformationalVersionAttribute)))?.InformationalVersion ?? "";

        readonly MainWindowDataContext context = new();
        readonly List<PetInstance> pets = new();
        int nextPetId = 1;
        bool isExiting = false;
        bool hasCentered = false;
        bool skipConfirmOnClose = false;
        GflChibiDesktop.Windows.DataManagerWindow? dataManagerWindow;
        public string announcementMsg = "";

        string AppDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app") + Path.DirectorySeparatorChar;

        private static Version GetProductVersion()
        {
            var attr = (AssemblyFileVersionAttribute?)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyFileVersionAttribute));
            if (attr != null && !string.IsNullOrEmpty(attr.Version) && Version.TryParse(attr.Version, out var v))
            {
                return v;
            }
            return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(2, 0, 0, 0);
        }

        private PetInstance? SelectedPet => (PetTabs.SelectedItem as TabItem)?.Tag as PetInstance;

        public MainWindow()
        {
            InitializeComponent();
            window.Title += $" {productVersion.Major}.{productVersion.Minor}";
            //lblVersion.Content = $"程序版本 {productVersion}";

            // 启动统计请求放后台线程，避免阻塞窗口初始化
            string fallbackTitle = window.Title; // 在 UI 线程缓存，供后台线程使用
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    bool StartupPost = false;
                    string StartupStr = HttpRequestHelper.PostWebRequest("https://api.brightsu.cn/GflChibiDesktop2/startup", $"version={productVersion}&build={currentBuild}", Encoding.UTF8, ref StartupPost);
                    if (StartupPost)
                    {
                        StartupRoot? rt = JsonConvert.DeserializeObject<StartupRoot>(StartupStr);
                        if (rt != null && rt.ret == 200)
                        {
                            announcementMsg = rt.data?.msg;
                            // 公告为空时回退为程序名+版本
                            if (string.IsNullOrEmpty(announcementMsg))
                            {
                                announcementMsg = fallbackTitle;
                            }
                            Dispatcher.BeginInvoke(() =>
                            {
                                if (!isExiting && dataManagerWindow is not null)
                                {
                                    dataManagerWindow.AnnouncementMsg = announcementMsg;
                                }
                            });
                        }
                        else
                        {
                            Dispatcher.BeginInvoke(() =>
                            {
                                if (!isExiting)
                                    HandyControl.Controls.Growl.WarningGlobal($"API接口调用失败。部分功能可能会受到影响。\n错误：API 接口返回了状态码 {rt?.ret}");
                            });
                        }
                    }
                    else
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            if (!isExiting)
                                HandyControl.Controls.Growl.WarningGlobal($"API接口调用失败。部分功能可能会受到影响。\n{StartupStr}");
                        });
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (!isExiting)
                            HandyControl.Controls.Growl.WarningGlobal($"API接口调用失败。部分功能可能会受到影响。\n错误：{ex.Message}");
                    });
                }
            });

            // 后台任务未完成前的兜底
            if (announcementMsg == "")
            {
                announcementMsg = window.Title;
            }
            // 清理上次崩溃残留的 luajit 进程（主程序已无法管理它们），再重新启动
            KillStaleLuajitProcesses();
            DataContext = context;
            bool started = AutoStartInstance();
            notifyIcon.Init();
            if (started)
            {
                WindowState = WindowState.Minimized;
                Window_StateChanged(null, new());
            }
        }

        /// <summary>
        /// 结束本程序目录下残留的 luajit 进程（程序崩溃后遗留的桌宠渲染进程）。
        /// 仅清理可执行文件位于本程序 app\ 目录下的进程，避免误伤其他程序的 luajit。
        /// </summary>
        private static void KillStaleLuajitProcesses()
        {
            try
            {
                string expectedDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app");
                foreach (var p in System.Diagnostics.Process.GetProcessesByName("luajit"))
                {
                    try
                    {
                        string file = p.MainModule?.FileName ?? "";
                        if (string.IsNullOrEmpty(file))
                        {
                            continue;
                        }
                        // 校验进程可执行文件是否在本程序的 app 目录下
                        if (!string.Equals(Path.GetDirectoryName(file), expectedDir, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        p.Kill();
                        p.WaitForExit(1000);
                    }
                    catch
                    {
                    }
                    finally
                    {
                        p.Dispose();
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 启动时恢复上次会话的桌宠实例（多开也一并恢复）。
        /// </summary>
        /// <returns>是否成功启动了至少一个桌宠实例（false 表示无任何配置，此时主窗口保持可见）。</returns>
        private bool AutoStartInstance()
        {
            List<GflChibiDesktop.Windows.ChibiModelData>? saved = LoadSavedInstances();
            if (saved != null && saved.Count > 0)
            {
                foreach (var m in saved)
                {
                    StartInstance(m);
                }
                return true;
            }

            string nameFile = Path.Combine(AppDir, "assets", "name.txt");
            string modelFile = Path.Combine(AppDir, "assets", "model.conf.json");
            if (!File.Exists(nameFile) || !File.Exists(modelFile))
            {
                return false;
            }
            StartInstance(null);
            return true;
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
        /// 开启一个新的桌宠实例（基于当前选中的桌宠配置）。
        /// </summary>
        private void StartNewInstance(object sender, RoutedEventArgs e)
        {
            StartInstance(SelectedPet?.Model);
        }

        private void StartInstance(GflChibiDesktop.Windows.ChibiModelData? model)
        {
            if (isExiting)
            {
                return;
            }
            // 限制多开数量为 8 个
            if (pets.Count >= 8)
            {
                HandyControl.Controls.Growl.WarningGlobal("最多同时开启 8 个桌宠实例。");
                return;
            }
            try
            {
                PetInstance pet = PetInstance.Create(nextPetId++, model);
                ProcessManager? pm = null;
                pm = new ProcessManager(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app/luajit.exe"),
                    pet.WorkDir,
                    "main.lua",
                    () => Dispatcher.BeginInvoke(() => { if (pm is not null) ReadIpc(pm, pet); }));
                pm.Exited += (s, _) => Dispatcher.BeginInvoke(() => OnPetExited(pet, s as ProcessManager));
                pet.Manager = pm;
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
                pet.TabTitle.Text = GetTabTitle(pet);
            }
            pet.IsRestarting = true;
            await StopManager(pet);
            pet.Panel.Children.Clear();
            pet.IsChanged = false;
            ProcessManager? pm = null;
            pm = new ProcessManager(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app/luajit.exe"),
                pet.WorkDir,
                "main.lua",
                () => Dispatcher.BeginInvoke(() => { if (pm is not null) ReadIpc(pm, pet); }));
            pm.Exited += (s, _) => Dispatcher.BeginInvoke(() => OnPetExited(pet, s as ProcessManager));
            pet.Manager = pm;
            pet.IsRestarting = false;
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

        private void OnPetExited(PetInstance pet, ProcessManager? exitedManager)
        {
            if (!pets.Contains(pet))
            {
                return;
            }
            // 重启中（RestartSelected 主动结束旧进程）：忽略退出事件，等待新进程就绪
            if (pet.IsRestarting)
            {
                return;
            }
            // 只处理当前 manager 的退出；旧 manager 延迟触发的退出事件忽略
            if (pet.Manager != exitedManager)
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

        /// <summary>
        /// 生成标签页标题：实例编号 + 人形名称。
        /// </summary>
        private static string GetTabTitle(PetInstance pet) => $"{pet.Id}. {pet.Name}";

        private void AddTab(PetInstance pet)
        {
            var tab = new TabItem();
            pet.Tab = tab;
            tab.Tag = pet;

            var header = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            pet.TabTitle = new TextBlock { Text = GetTabTitle(pet), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
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

            // 通过托盘“退出”菜单退出：不弹确认框，直接关闭
            if (skipConfirmOnClose)
            {
                return;
            }

            // 后台静默运行时窗口不可见：不弹模态确认框，直接关闭（避免窗口状态异常）
            if (!IsVisible)
            {
                return;
            }

            MessageBoxResult result = HandyControl.Controls.MessageBox.Show("确定要退出少女前线桌面Q宠吗？", "确认退出", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
            }
        }

        private void ExitApplication(object sender, RoutedEventArgs e)
        {
            // 托盘“退出”：跳过确认框，直接关闭（确认后由 OnMainWindowClose 退出程序）
            skipConfirmOnClose = true;
            Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            isExiting = true;
            SaveInstances();
            // 退出前保存所有实例的面板配置（FPS/透明度等），让 luajit 写盘到各自 settings.json
            SaveAllConfigs();
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
            // 主窗口关闭后应用即退出（OnMainWindowClose），此处需同步等待子进程关闭，避免残留
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
            SavePetConfig(pet);
            SaveInstances();
        }

        /// <summary>
        /// 保存单个实例的配置（通过 IPC 让 luajit 写盘 settings.json）。
        /// </summary>
        private void SavePetConfig(PetInstance pet)
        {
            if (pet?.Manager is null)
            {
                return;
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

        /// <summary>
        /// 退出时保存所有实例的配置，确保 luajit 把面板设置写盘到各自的 settings.json。
        /// </summary>
        private void SaveAllConfigs()
        {
            foreach (PetInstance pet in pets.ToList())
            {
                try
                {
                    SavePetConfig(pet);
                }
                catch
                {
                }
            }
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

        private void ReadIpc(ProcessManager pm, PetInstance pet)
        {
            if (pet.Manager != pm) return;
            var reader = pm.rxIpc.GetReader();
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
            context.HasActiveTab = pet != null;
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                ShowInTaskbar = false;
                Hide();
                HandyControl.Controls.Growl.InfoGlobal("少女前线桌面Q宠已在后台静默启动。\n双击托盘图标显示主窗口。");
            }
        }

        private void notifyIcon_MouseDoubleClick(object sender, RoutedEventArgs e)
        {
            if (isExiting)
            {
                return;
            }
            Show();
            ShowInTaskbar = true;
            WindowState = WindowState.Normal;
            if (!hasCentered)
            {
                CenterWindow();
                hasCentered = true;
            }
            Activate();
        }

        /// <summary>
        /// 将窗口居中于当前工作区（托盘唤起后 CenterScreen 不重新生效，需手动居中）。
        /// </summary>
        private void CenterWindow()
        {
            double waW = SystemParameters.WorkArea.Width;
            double waH = SystemParameters.WorkArea.Height;
            double waX = SystemParameters.WorkArea.Left;
            double waY = SystemParameters.WorkArea.Top;
            Left = waX + (waW - ActualWidth) / 2;
            Top = waY + (waH - ActualHeight) / 2;
        }

        private void GflChibiDesktop(object sender, RoutedEventArgs e)
        {
            if (isExiting)
            {
                return;
            }
            if (dataManagerWindow is null)
            {
                dataManagerWindow = new GflChibiDesktop.Windows.DataManagerWindow();
                dataManagerWindow.ModelLoadRequested += DataManagerWindow_ModelLoadRequested;
                dataManagerWindow.Closed += (_, _) => dataManagerWindow = null;
            }
            dataManagerWindow.AnnouncementMsg = announcementMsg;
            dataManagerWindow.LoadedPaths = GetLoadedPaths();
            dataManagerWindow.Show();
            dataManagerWindow.Activate();
        }

        /// <summary>
        /// 收集所有运行中桌宠实例正在使用的模型数据目录（相对 assets/spine/ 的 path）。
        /// </summary>
        private System.Collections.Generic.HashSet<string> GetLoadedPaths()
        {
            var paths = new System.Collections.Generic.HashSet<string>();
            foreach (var pet in pets)
            {
                if (pet.Model?.SkeletonFile is string s)
                {
                    // 形如 assets/spine/{path}/{file}.skel
                    string[] parts = s.Split('/');
                    if (parts.Length >= 3 && parts[0] == "assets" && parts[1] == "spine")
                    {
                        paths.Add(parts[2]);
                    }
                }
            }
            return paths;
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
                    HandyControl.Controls.Growl.InfoGlobal($"已加载 {data.DisplayName}，人形已应用。");
                }
                SaveInstances();
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
        private bool hasActiveTab = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool HasActiveTab
        {
            get => hasActiveTab;
            set
            {
                hasActiveTab = value;
                OnPropertyChanged();
            }
        }

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
            get => AutoRun.IsAutoRun(CurrentExePath, "少女前线桌面Q宠");
            set
            {
                AutoRun.SetAutoRun(CurrentExePath, "少女前线桌面Q宠", value);
                OnPropertyChanged();
            }
        }

        private static string CurrentExePath => System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";

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
}
