using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using Newtonsoft.Json;
using GflChibiDesktop2.Properties;
using static GflChibiDesktop2.WebAPI;
using GflChibiDesktop2.Helpers;

namespace GflChibiDesktop2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : HandyControl.Controls.Window
    {
        public readonly string productTitle = ((AssemblyTitleAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyTitleAttribute))).Title.ToString();
        public readonly Version productVersion = GetProductVersion();
        public readonly string currentBuild = ((AssemblyInformationalVersionAttribute?)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyInformationalVersionAttribute)))?.InformationalVersion ?? "";

        readonly MainWindowDataContext context = new();
        readonly List<PetInstance> pets = new();
        System.Windows.Media.Geometry playGeometry;
        System.Windows.Media.Geometry pauseGeometry;
        System.Windows.Media.Geometry closeGeometry;
        bool isExiting = false;
        bool hasCentered = false;
        bool skipConfirmOnClose = false;
        DataManagerWindow? dataManagerWindow;
        AboutDialog? aboutDialog;
        public string announcementMsg = "";
        public string homepageLink = "https://projects.brightsu.cn/GflChibiDesktop/V2/";
        public string helpLink = "https://projects.brightsu.cn/GflChibiDesktop/V2/FAQ";
        public string repoLink = "https://github.com/Blue-Roar/GflChibiDesktop2";
        public string updateLink = "https://projects.brightsu.cn/GflChibiDesktop/V2/download";
        public string chibiListLink = "https://api.brightsu.cn/GFL/chibi_list";

        string AppDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app") + Path.DirectorySeparatorChar;

        private static Version GetProductVersion()
        {
            var attr = (AssemblyFileVersionAttribute?)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyFileVersionAttribute));
            if (attr is not null && !string.IsNullOrEmpty(attr.Version) && Version.TryParse(attr.Version, out var v))
            {
                return v;
            }
            return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(2, 0, 0, 0);
        }

        /// <summary>
        /// 将 luajit 渲染进程写入系统图形性能首选项（高性能 GPU，2），避免部分人形在核显上卡顿。
        /// </summary>
        private static void EnsureLuajitGpuPreference()
        {
            try
            {
                const string keyPath = @"Software\Microsoft\DirectX\UserGpuPreferences";
                // 系统图形设置的合法格式是 REG_SZ 字符串（如 "GpuPreference=2;"），DWORD 不会被识别
                const string gpuPreferenceHighPerformance = "GpuPreference=2;";
                string luajitPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app", "luajit.exe");
                using RegistryKey? key = Registry.CurrentUser.CreateSubKey(keyPath);
                if (key is null)
                {
                    return;
                }
                object? existing = key.GetValue(luajitPath);
                if (existing is not string s || !s.Contains("GpuPreference=2"))
                {
                    key.SetValue(luajitPath, gpuPreferenceHighPerformance, RegistryValueKind.String);
                }
            }
            catch
            {
            }
        }

        private void CheckLegacyModules()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            bool glExists = File.Exists(Path.Combine(baseDir, "app", "LegacyGL", "GflChibiDesktopLegacyGL.exe"));
            bool dxExists = File.Exists(Path.Combine(baseDir, "app", "LegacyDX", "GflChibiDesktopLegacyDX.exe"));

            if (!glExists && !dxExists)
            {
                Settings.Default.EnableLegacy = false;
                Settings.Default.UseLegacyModule = false;
                Settings.Default.DisableNew = false;
            }
            else
            {
                if (!glExists && dxExists)
                {
                    Settings.Default.UseOpenGL = false;
                }
                if (glExists && !dxExists)
                {
                    Settings.Default.UseOpenGL = true;
                }
            }
            Settings.Default.Save();
            EnableLegacy = Settings.Default.EnableLegacy;
        }

        private PetInstance? SelectedPet => (PetTabs.SelectedItem as TabItem)?.Tag as PetInstance;

        /// <summary>
        /// 取最小的未被占用的实例编号（删除实例后编号可复用）。
        /// </summary>
        private int GetNextPetId()
        {
            var used = pets.Select(p => p.Id).ToHashSet();
            int id = 1;
            while (used.Contains(id))
            {
                id++;
            }
            return id;
        }

        public MainWindow()
        {
            InitializeComponent();
            if (Properties.Settings.Default.DisableCustomWindowChrome)
            {
                // 移除 HandyControl 构造中强制设置的 WindowChrome（System.Windows.Shell），恢复系统非客户区
                System.Windows.Shell.WindowChrome.SetWindowChrome(this, null);
                Style = null;
                WindowStyle = WindowStyle.SingleBorderWindow;
            }
            EnsureLuajitGpuPreference();
            CheckLegacyModules();
            playGeometry = (System.Windows.Media.Geometry)FindResource("PlayGeometry");
            pauseGeometry = (System.Windows.Media.Geometry)FindResource("PauseGeometry");
            closeGeometry = (System.Windows.Media.Geometry)FindResource("CloseGeometry");
            //lblVersion.Content = $"程序版本 {productVersion}";
            // 启动统计请求放后台线程，避免阻塞窗口初始化
            string fallbackTitle = $"{productTitle} {productVersion.Major}.{productVersion.Minor}"; // 在 UI 线程缓存，供后台线程使用
            window.Title = fallbackTitle;
            announcementMsg = fallbackTitle;

            _ = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    bool StartupPost = false;
                    string StartupStr = HttpRequestHelper.PostWebRequest("https://api.brightsu.cn/GflChibiDesktop2/startup", $"version={productVersion}&build={currentBuild}&os={RuntimeInformation.OSDescription}/{RuntimeInformation.RuntimeIdentifier}/{RuntimeInformation.FrameworkDescription}", Encoding.UTF8, ref StartupPost);
                    if (StartupPost)
                    {
                        StartupRoot? rt = JsonConvert.DeserializeObject<StartupRoot>(StartupStr);
                        if (rt is not null && rt.ret == 200)
                        {
                            if (HttpRequestHelper.CheckIsUrlFormat(rt.data.homepage_link)) { homepageLink = rt.data.homepage_link; }
                            if (HttpRequestHelper.CheckIsUrlFormat(rt.data.repo_link)) { repoLink = rt.data.repo_link; }
                            if (HttpRequestHelper.CheckIsUrlFormat(rt.data.update_link)) { updateLink = rt.data.update_link; }
                            if (HttpRequestHelper.CheckIsUrlFormat(rt.data.chibi_list_link)) { chibiListLink = rt.data.chibi_list_link; }
                            if (HttpRequestHelper.CheckIsUrlFormat(rt.data.help_link)) { helpLink = rt.data.help_link; }

                            announcementMsg = rt.data?.msg;
                            UpdateCheckHelper.CheckAndPrompt(rt.data?.latest, productVersion, productTitle, ShowAbout);
                            // 公告为空时回退为程序名+版本
                            if (string.IsNullOrEmpty(announcementMsg)) announcementMsg = fallbackTitle;
                            Dispatcher.BeginInvoke(() =>
                            {
                                if (!isExiting && dataManagerWindow is not null)
                                {
                                    UpdateSharedVariables();
                                }
                            });
                        }
                        else
                        {
                            Dispatcher.BeginInvoke(() =>
                            {
                                if (!isExiting && !Properties.Settings.Default.SuppressConnectionErrorPrompts)
                                    GrowlHelper.WarningGlobal($"API接口调用失败。部分功能可能会受到影响。\n错误：API 接口返回了状态码 {rt?.ret}");
                            });
                        }
                    }
                    else
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            if (!isExiting && !Properties.Settings.Default.SuppressConnectionErrorPrompts)
                                GrowlHelper.WarningGlobal($"API接口调用失败。部分功能可能会受到影响。\n{StartupStr}");
                        });
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (!isExiting && !Properties.Settings.Default.SuppressConnectionErrorPrompts)
                            GrowlHelper.WarningGlobal($"API接口调用失败。部分功能可能会受到影响。\n错误：{ex.Message}");
                    });
                }
            });

            // 后台任务未完成前的兜底
            if (string.IsNullOrEmpty(announcementMsg)) announcementMsg = fallbackTitle;
            // 清理上次崩溃残留的 luajit 进程（主程序已无法管理它们），再重新启动
            KillStalePetProcesses();
            DataContext = context;
            bool started = AutoStartInstance();
            notifyIcon.Init();
            if (started)
            {
                // 静默启动：等窗口完成首次渲染后再隐藏到托盘。
                // 若在首次显示前 Minimize/Hide，窗口从未以 Normal 状态完成初始化，
                // 托盘恢复后标题栏（WindowChrome）拖动会失效。
                ContentRendered += (_, _) =>
                {
                    if (isExiting)
                    {
                        return;
                    }
                    ShowInTaskbar = false;
                    Hide();
                    if (!Properties.Settings.Default.SuppressMinimizePrompts)
                    {
                        GrowlHelper.InfoGlobal($"{productTitle}已在后台静默启动。\n双击托盘图标显示主窗口。");
                    }
                };
            }
            else
            {
                // 无自动恢复实例：窗口默认 Minimized（避免启动闪烁），此处恢复为正常显示
                WindowState = WindowState.Normal;
            }
        }

        /// <summary>
        /// 结束本程序目录下残留的桌宠渲染进程（程序崩溃后遗留的 luajit / legacy 进程）。
        /// 仅清理可执行文件位于本程序 app\ 目录树下的进程，避免误伤其他程序的同名进程。
        /// </summary>
        private static void KillStalePetProcesses()
        {
            try
            {
                string appRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app");
                string[] names = { "luajit", "GflChibiDesktopLegacyDX", "GflChibiDesktopLegacyGL" };
                foreach (string name in names)
                {
                    foreach (var p in System.Diagnostics.Process.GetProcessesByName(name))
                    {
                        try
                        {
                            string file = p.MainModule?.FileName ?? "";
                            if (string.IsNullOrEmpty(file))
                            {
                                continue;
                            }
                            // 校验进程可执行文件是否在本程序的 app 目录下（含 LegacyDX/LegacyGL 子目录）
                            if (!file.StartsWith(appRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
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
            List<SavedInstance>? saved = LoadSavedInstances();
            if (saved is null)
            {
                // 从未保存过实例列表：不自动打开任何桌宠，主窗口保持可见
                return false;
            }
            int started = 0;
            foreach (var s in saved)
            {
                ChibiModelData? m = s.Model;
                if (m is null)
                {
                    continue;
                }
                if (!ValidateModelFiles(m))
                {
                    continue;
                }
                if (s.Suspended)
                {
                    // 上次退出时处于暂停状态：恢复标签页但不启动进程
                    StartSuspendedInstance(m, s.UseLegacyModule);
                }
                else
                {
                    StartInstance(m, true, s.UseLegacyModule);
                }
                started++;
            }
            return started > 0;
        }

        /// <summary>
        /// 恢复暂停的桌宠：创建标签页但不启动进程，控制面板隐藏。
        /// </summary>
        private void StartSuspendedInstance(ChibiModelData model, bool? useLegacyModule = null)
        {
            try
            {
                PetInstance pet = PetInstance.Create(GetNextPetId(), model);
                pet.UseLegacyModule = useLegacyModule ?? Properties.Settings.Default.UseLegacyModule;
                pet.IsSuspended = true;
                pet.StopRequested = true;
                pets.Add(pet);
                AddTab(pet);
                pet.Panel.Visibility = Visibility.Collapsed;
                if (pet.HintPanel is not null) pet.HintPanel.Visibility = Visibility.Visible;
                UpdateStatus();
            }
            catch (Exception ex)
            {
                GrowlHelper.ErrorGlobal($"恢复桌宠失败。\n{ex.Message}");
            }
        }

        /// <summary>
        /// 校验模型数据文件（skeleton/atlas）是否存在，缺失则跳过并提示。
        /// </summary>
        private bool ValidateModelFiles(ChibiModelData model)
        {
            string appDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app");
            foreach (string f in new[] { model.SkeletonFile, model.AtlasFile })
            {
                if (string.IsNullOrEmpty(f))
                {
                    continue;
                }
                if (!File.Exists(Path.Combine(appDir, f)))
                {
                    // if (!Properties.Settings.Default.SuppressLoadPrompts)
                        GrowlHelper.WarningGlobal($"“{model.DisplayName}”的桌宠数据文件缺失，已跳过自动加载：\n{f}");
                    return false;
                }
            }
            return true;
        }

        private static string InstancesFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app", "instances.json");

        private List<SavedInstance>? LoadSavedInstances()
        {
            try
            {
                if (File.Exists(InstancesFilePath))
                {
                    string json = File.ReadAllText(InstancesFilePath);
                    // 新格式：包含暂停状态
                    var saved = JsonConvert.DeserializeObject<List<SavedInstance>>(json);
                    if (saved is not null && (saved.Count == 0 || saved.Any(s => s.Model is not null)))
                    {
                        return saved;
                    }
                    // 旧格式：纯 ChibiModelData 数组（元素无 Model 字段时全部为空）
                    var legacy = JsonConvert.DeserializeObject<List<ChibiModelData>>(json);
                    if (legacy is not null)
                    {
                        return legacy.Select(m => new SavedInstance { Model = m, Suspended = false }).ToList();
                    }
                }
            }
            catch
            {
            }
            // 文件不存在或读取失败：表示从未保存过实例列表，返回 null 交由调用方回退默认配置
            return null;
        }

        private void SaveInstances()
        {
            try
            {
                var list = pets.Select(p => new SavedInstance { Model = p.Model, Suspended = p.IsSuspended, UseLegacyModule = p.UseLegacyModule })
                    .Where(s => s.Model is not null).ToList();
                File.WriteAllText(InstancesFilePath, JsonConvert.SerializeObject(list, Newtonsoft.Json.Formatting.Indented));
            }
            catch
            {
            }
        }

        /// <summary>
        /// 实例列表持久化记录（含暂停状态与实例级渲染模块）。
        /// </summary>
        public class SavedInstance
        {
            public ChibiModelData? Model { get; set; }
            public bool Suspended { get; set; }
            /// <summary>本实例使用的渲染模块（true=旧版 MonoGame；false=新版 Raylib）。null 表示未设置，用全局默认。</summary>
            public bool? UseLegacyModule { get; set; }
        }

        /// <summary>
        /// 开启一个新的桌宠实例（基于当前选中的桌宠配置）。
        /// </summary>
        private void StartNewInstance(object sender, RoutedEventArgs e)
        {
            if (SelectedPet?.Model is ChibiModelData model)
            {
                // 新实例继承当前选中实例的渲染模块选择
                StartInstance(model, false, SelectedPet?.UseLegacyModule);
            }
        }

        private void StartInstance(ChibiModelData model, bool silently = false, bool? useLegacyModule = null, bool applyCategoryDefaults = false)
        {
            if (isExiting)
            {
                return;
            }
            // 限制多开数量为 8 个（EasterEgg 解锁后可取消）
            if (pets.Count >= 8)
            {
                if (Settings.Default.EasterEgg)
                {
                    if (!Settings.Default.SuppressMultiInstanceWarning)
                    {
                        MessageBoxResult result = HandyControl.Controls.MessageBox.Show($"你已解锁桌宠多开数量限制，本程序将不再限制创建8个以上的桌宠实例。\n然而，更多数量的多开实例并未被测试过，请自行承担使用的风险。\n\n是否隐藏此警告？\n「是」：继续多开，以后不再提醒\n「否」：继续多开，下次继续询问\n「取消」：放弃多开", "桌宠无限多开警告", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                        switch (result)
                        {
                            case MessageBoxResult.Yes:
                                Settings.Default.SuppressMultiInstanceWarning = true;
                                Settings.Default.Save();
                                break;
                            case MessageBoxResult.No:
                                break;
                            case MessageBoxResult.Cancel:
                                return;
                        }
                    }
                }
                else
                {
                    HandyControl.Controls.MessageBox.Warning("最多同时开启 8 个桌宠实例。", productTitle);
                    return;
                }
            }
            try
            {
                PetInstance pet = PetInstance.Create(GetNextPetId(), model);
                pet.UseLegacyModule = useLegacyModule ?? Properties.Settings.Default.UseLegacyModule;
                // 加载新数据（从数据管理器选择模型）时按数据分类预设默认开关（ENEMY → 勾选翻转朝向），
                // 需在子进程启动前写入 v1/v2 的 settings 文件；启动恢复/多开复制不预设，保留用户设置
                if (applyCategoryDefaults)
                {
                    pet.ApplyCategoryDefaults(model);
                }
                ProcessManager? pm = null;
                pm = StartPetProcess(pet);
                pm.Exited += (s, _) => Dispatcher.BeginInvoke(() => OnPetExited(pet, s as ProcessManager));
                pet.Manager = pm;
                pets.Add(pet);
                AddTab(pet);
                PetTabs.SelectedItem = pet.Tab;
                UpdateStatus();
                if (!Properties.Settings.Default.SuppressLoadPrompts && !silently)
                    GrowlHelper.InfoGlobal($"已加载 {model.DisplayName}。");
            }
            catch (Exception ex)
            {
                GrowlHelper.ErrorGlobal($"开启桌宠实例失败。\n{ex.Message}");
            }
        }

        /// <summary>
        /// 加载新数据（未勾选"多开"）= 替换当前选中的桌宠实例：
        /// 删除旧实例（停止进程并清除其工作目录/坐标等全部配置），再按新数据启动一个全新实例。
        /// 旧实例的渲染模块选择（legacy/raylib）会继承到新实例。
        /// </summary>
        private async Task RestartSelected(ChibiModelData data)
        {
            PetInstance? old = SelectedPet;
            if (old is null)
            {
                // 无选中实例：直接按新数据启动（预设分类默认，如 ENEMY → 翻转朝向）
                StartInstance(data, applyCategoryDefaults: true);
                return;
            }
            bool? useLegacy = old.UseLegacyModule;
            // 替换 = 删除选中实例（停进程、移除标签页、删除工作目录），坐标/画布等配置随之重建为全新默认
            await StopInstance(old);
            StartInstance(data, applyCategoryDefaults: true, useLegacyModule: useLegacy);
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
            if (pet.Manager is not null && !pet.Manager.process.HasExited)
            {
                pet.Manager.ForceCloseWindow();
            }
            pet.Manager?.Dispose();
            pet.Manager = null;
            context.IsBusyClosing = false;
        }

        private async void ToggleSuspendSelected(object sender, RoutedEventArgs e)
        {
            PetInstance? pet = SelectedPet;
            if (pet is null || context.IsBusyClosing)
            {
                return;
            }
            if (pet.Manager is not null)
            {
                // 运行中 → 暂停（暂时关闭，不删除；标签页保留，之后可恢复）
                pet.StopRequested = true;
                pet.RestartAttempts = 0;
                // 暂停期间忽略进程退出事件，避免 OnPetExited 误删标签页
                pet.IsRestarting = true;
                await StopManager(pet);
                pet.IsRestarting = false;
                pet.IsSuspended = true;
                // 暂停期间隐藏控制面板，避免操作触发 IPC 出错；显示提示信息
                pet.Panel.Visibility = Visibility.Collapsed;
                if (pet.HintPanel is not null) pet.HintPanel.Visibility = Visibility.Visible;
            }
            else
            {
                // 已暂停 → 恢复启动
                ResumeInstance(pet);
            }
            UpdateStatus();
        }

        /// <summary>
        /// 恢复暂停的桌宠实例（复用原标签页与工作目录）。
        /// </summary>
        private void ResumeInstance(PetInstance pet)
        {
            pet.StopRequested = false;
            pet.RestartAttempts = 0;
            pet.IsSuspended = false;
            pet.IsRestarting = true;
            try
            {
                pet.Panel.Children.Clear();
                pet.IsChanged = false;
                ProcessManager? pm = null;
                pm = StartPetProcess(pet);
                pm.Exited += (s, _) => Dispatcher.BeginInvoke(() => OnPetExited(pet, s as ProcessManager));
                pet.Manager = pm;
                pet.Panel.Visibility = Visibility.Visible;
                if (pet.HintPanel is not null) pet.HintPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                GrowlHelper.ErrorGlobal($"恢复桌宠“{pet.Name}”失败。\n{ex.Message}");
            }
            finally
            {
                pet.IsRestarting = false;
            }
        }

        /// <summary>
        /// 顶部“删除”按钮：删除当前选中的桌宠实例（确认后停止并清除配置）。
        /// </summary>
        private void DeleteSelected(object sender, RoutedEventArgs e)
        {
            PetInstance? pet = SelectedPet;
            if (pet is null)
            {
                return;
            }
            MessageBoxResult result = HandyControl.Controls.MessageBox.Show($"是否删除桌宠“{pet.Name}”？\n删除后该实例的配置数据将一并清除，无法恢复。", "删除桌宠确认", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (result == MessageBoxResult.Yes)
            {
                _ = StopInstance(pet);
            }
        }

        private async Task StopInstance(PetInstance pet)
        {
            if (!pets.Contains(pet))
            {
                return;
            }
            // 用户主动停止：标记后不自动重启
            pet.StopRequested = true;
            pet.RestartAttempts = 0;
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

            // 用户主动停止或程序退出：不自动重启
            if (pet.StopRequested || isExiting)
            {
                RemovePet(pet);
                return;
            }

            // 异常退出：3 秒后自动重启，最多重试 3 次
            if (pet.RestartAttempts < 3)
            {
                pet.RestartAttempts++;
                int attempts = pet.RestartAttempts;
                GrowlHelper.WarningGlobal($"桌宠“{pet.Name}”异常退出，3 秒后自动重启（第 {attempts}/3 次）。");
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(3000);
                    Dispatcher.BeginInvoke(() =>
                    {
                        // 期间被主动停止/关闭或计数已变化则放弃
                        if (isExiting || !pets.Contains(pet) || pet.StopRequested || attempts != pet.RestartAttempts)
                        {
                            return;
                        }
                        _ = AutoRestartPet(pet);
                    });
                });
                return;
            }

            GrowlHelper.WarningGlobal($"桌宠“{pet.Name}”异常退出次数过多，已停止自动重启。");
            RemovePet(pet);
        }

        /// <summary>
        /// 自动重启桌宠实例（复用原标签页）。
        /// </summary>
        private async Task AutoRestartPet(PetInstance pet)
        {
            if (isExiting)
            {
                return;
            }
            pet.IsRestarting = true;
            try
            {
                pet.Panel.Children.Clear();
                pet.IsChanged = false;
                ProcessManager? pm = null;
                pm = StartPetProcess(pet);
                pm.Exited += (s, _) => Dispatcher.BeginInvoke(() => OnPetExited(pet, s as ProcessManager));
                pet.Manager = pm;
                GrowlHelper.InfoGlobal($"桌宠“{pet.Name}”已自动重启。");
            }
            catch (Exception ex)
            {
                GrowlHelper.ErrorGlobal($"自动重启桌宠“{pet.Name}”失败。\n{ex.Message}");
            }
            finally
            {
                pet.IsRestarting = false;
                UpdateStatus();
            }
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
            SaveInstances();
        }

        /// <summary>
        /// 生成标签页标题：实例编号 + 人形名称。
        /// </summary>
        private static string GetTabTitle(PetInstance pet) => $"#{pet.Id} {pet.Name}";

        private void AddTab(PetInstance pet)
        {
            var tab = new TabItem();
            pet.Tab = tab;
            tab.Tag = pet;

            var header = new StackPanel {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            pet.TabTitle = new TextBlock {
                Text = GetTabTitle(pet),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 6, 0)
            };
            header.Children.Add(pet.TabTitle);
            tab.Header = header;

            // 控制面板 + 暂停提示（默认隐藏，暂停时显示）
            pet.HintPanel = new TextBlock
            {
                Text = "该桌宠实例已暂停，点击“启动”按钮恢复。",
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(12)
            };
            var container = new Grid();
            container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 实例级渲染模块选择（新版 Raylib / 旧版 MonoGame），切换后重启实例生效
            var versionPanel = new HandyControl.Controls.ButtonGroup
            {
                Margin = new Thickness(10, 8, 10, 4)
            };
            var rbRaylib = new RadioButton {
                Content = "V2新版 (Raylib)",
                IsChecked = !pet.UseLegacyModule
            };
            var rbMonoGame = new RadioButton {
                Content = "V1旧版 (MonoGame)",
                IsChecked = pet.UseLegacyModule
            };
            rbRaylib.Checked += (_, _) => SetInstanceVersion(pet, false);
            rbMonoGame.Checked += (_, _) => SetInstanceVersion(pet, true);
            versionPanel.Items.Add(rbRaylib);
            versionPanel.Items.Add(rbMonoGame);
            pet.VersionPanel = versionPanel;
            // 全局禁用旧版或禁用新版时隐藏版本切换面板
            versionPanel.Visibility = (Properties.Settings.Default.EnableLegacy && !Properties.Settings.Default.DisableNew)
                ? Visibility.Visible : Visibility.Collapsed;
            Grid.SetRow(versionPanel, 0);
            container.Children.Add(versionPanel);

            var panelArea = new Grid();
            panelArea.Children.Add(pet.HintPanel);
            panelArea.Children.Add(pet.Panel);
            Grid.SetRow(panelArea, 1);
            container.Children.Add(panelArea);

            var scroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = container
            };
            tab.Content = scroller;
            PetTabs.Items.Add(tab);
        }

        /// <summary>
        /// 切换实例的渲染模块（新版 Raylib / 旧版 MonoGame），保存并重启实例生效。
        /// </summary>
        private void SetInstanceVersion(PetInstance pet, bool legacy)
        {
            if (pet.UseLegacyModule == legacy)
            {
                return;
            }
            pet.UseLegacyModule = legacy;
            SaveInstances();
            _ = RestartInstanceForVersion(pet);
        }

        /// <summary>
        /// 同步各实例版本切换面板的可见性（全局禁用旧版或禁用新版时隐藏）。
        /// </summary>
        public void ApplyLegacyEnableState()
        {
            CheckLegacyModules();
            bool show = Properties.Settings.Default.EnableLegacy && !Properties.Settings.Default.DisableNew;
            foreach (PetInstance pet in pets)
            {
                if (pet.VersionPanel is not null)
                {
                    pet.VersionPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        /// <summary>停止当前实例进程，并按新版本重新启动（复用标签页）。</summary>
        private async Task RestartInstanceForVersion(PetInstance pet)
        {
            if (!pets.Contains(pet) || isExiting)
            {
                return;
            }
            pet.StopRequested = true;
            pet.RestartAttempts = 0;
            pet.IsRestarting = true;
            await StopManager(pet);
            pet.IsRestarting = false;
            ResumeInstance(pet);
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (isExiting)
            {
                return;
            }

            foreach (Window window in Application.Current.Windows)
            {
                if (window is DataManagerWindow dm && dm.IsVisible)
                {
                    e.Cancel = true;
                    dm.Activate();
                    GrowlHelper.InfoGlobal("在退出程序前，请先关闭数据管理窗口。");
                    skipConfirmOnClose = false;
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

            // 确认框弹出期间禁用托盘菜单全部菜单项，避免在窗口关闭流程中触发嵌套关闭
            SetTrayMenuEnabled(false);
            try
            {
                MessageBoxResult result = HandyControl.Controls.MessageBox.Show($"确定要退出{productTitle}吗？", "确认退出", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                }
            }
            finally
            {
                SetTrayMenuEnabled(true);
            }
        }

        /// <summary>
        /// 启用/禁用托盘菜单的全部菜单项。
        /// </summary>
        private void SetTrayMenuEnabled(bool enabled)
        {
            if (trayMenu is null)
            {
                return;
            }
            foreach (object item in trayMenu.Items)
            {
                if (item is MenuItem menuItem)
                {
                    menuItem.IsEnabled = enabled;
                }
            }
            trayMenu.IsEnabled = enabled;
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
                GrowlHelper.WarningGlobal("当前桌宠实例未运行，无法保存配置。");
                return;
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
                GrowlHelper.WarningGlobal("当前桌宠实例未运行，无法放弃更改。");
                return;
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
                                string prompt = reader.ReadString();
                                string hint = reader.ReadString();
                                if (reader.ReadInt() == 1)
                                {
                                    // 数字输入（main.lua 中 type="single"、valueType="number"）：NumericUpDown 控件
                                    NumericControl c = new(pet.Panel.Children.Count + 1);
                                    c.PromptText = prompt;
                                    c.HintText = hint;
                                    c.Value = reader.ReadInt();
                                    c.MinValue = reader.ReadInt();
                                    c.MaxValue = reader.ReadInt();
                                    c.PropertyChanged += (_, _) => OnPetControlChanged(pet);
                                    c.changed = false;
                                    pet.Panel.Children.Add(c);
                                }
                                else
                                {
                                    // 单行文本输入
                                    SingleLineTextControl c = new(pet.Panel.Children.Count + 1);
                                    c.PromptText = prompt;
                                    c.HintText = hint;
                                    c.InputContent = reader.ReadString();
                                    c.PropertyChanged += (_, _) => OnPetControlChanged(pet);
                                    c.changed = false;
                                    pet.Panel.Children.Add(c);
                                }
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
                        case 5:
                            {
                                // 下拉选择（legacy 渲染模块的动画/皮肤等）
                                ComboControl c = new(pet.Panel.Children.Count + 1);
                                c.PromptText = reader.ReadString();
                                c.HintText = reader.ReadString();
                                int count = reader.ReadInt();
                                var items = new System.Collections.Generic.List<string>();
                                for (int i = 0; i < count; i++)
                                {
                                    items.Add(reader.ReadString());
                                }
                                c.Items = items;
                                c.SelectedIndex = reader.ReadInt();
                                c.PropertyChanged += (_, _) => OnPetControlChanged(pet);
                                c.changed = false;
                                pet.Panel.Children.Add(c);
                            }
                            break;
                    }
                }
            }
            // 面板项联动（如 legacy：动态模拟开启时禁用动画下拉，仍保持显示当前动画）
            BindPanelLinks(pet);
            // 面板重建完成后延迟复位 IsChanged，清除控件加载时回写源属性造成的“伪更改”
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
            {
                if (!pets.Contains(pet))
                {
                    return;
                }
                pet.IsChanged = false;
                if (SelectedPet == pet)
                {
                    context.IsChanged = false;
                }
            });
        }

        /// <summary>
        /// 面板项联动：legacy 渲染模块的“动态模拟”开启时，禁用“动画”下拉（仍显示当前动画），
        /// 并强制启用“循环播放”。
        /// </summary>
        private void BindPanelLinks(PetInstance pet)
        {
            ComboControl? anime = null;
            BoolControl? simulate = null;
            BoolControl? loop = null;
            foreach (object child in pet.Panel.Children)
            {
                if (child is ComboControl cc && cc.PromptText == "动画")
                {
                    anime = cc;
                }
                else if (child is BoolControl bc && bc.PromptText == "动态模拟")
                {
                    simulate = bc;
                }
                else if (child is BoolControl lbc && lbc.PromptText == "循环播放")
                {
                    loop = lbc;
                }
            }
            if (simulate is null)
            {
                return;
            }
            void UpdateState()
            {
                bool sim = simulate.Choice;
                if (anime is not null)
                {
                    // 动态模拟开启时禁用动画下拉；禁用仅阻止操作，不改变选中值
                    anime.IsEnabled = !sim;
                }
                if (loop is not null)
                {
                    // 动态模拟开启时禁用循环开关并强制为开启
                    loop.IsEnabled = !sim;
                    if (sim && !loop.Choice)
                    {
                        loop.Choice = true;
                    }
                }
            }
            UpdateState();
            simulate.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(BoolControl.Choice))
                {
                    UpdateState();
                }
            };
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
            context.IsRunning = pet?.Manager is not null;
            context.IsChanged = pet?.IsChanged ?? false;
            context.HasActiveTab = pet is not null;
            // 未启动（暂停/无实例）时隐藏保存、放弃按钮，避免无意义的操作入口
            Visibility cfgVisible = pet?.Manager is not null ? Visibility.Visible : Visibility.Collapsed;
            btnSaveConfig.Visibility = cfgVisible;
            btnDiscardConfig.Visibility = cfgVisible;
            if (btnSuspend is not null)
            {
                bool running = pet?.Manager is not null;
                btnSuspend.Content = running ? "暂停(_W)" : "启动(_W)";
                btnSuspend.ToolTip = running
                    ? "暂时关闭当前桌宠实例（配置保留）"
                    : "重新启动当前桌宠实例";
                HandyControl.Controls.IconElement.SetGeometry(btnSuspend, running ? pauseGeometry : playGeometry);
            }
        }


        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                ShowInTaskbar = false;
                Hide();
                if (!Properties.Settings.Default.SuppressMinimizePrompts)
                    GrowlHelper.InfoGlobal($"{productTitle}已最小化到托盘。\n双击托盘图标显示主窗口。");
            }
        }

        private void notifyIcon_MouseDoubleClick(object sender, RoutedEventArgs e)
        {
            ShowWindow();
        }

        private void ShowWindow()
        {
            if (trayMenu is not null && trayMenu.IsEnabled)
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

        private void DataManager(object sender, RoutedEventArgs e)
        {
            if (isExiting)
            {
                return;
            }
            if (dataManagerWindow is null)
            {
                dataManagerWindow = new DataManagerWindow();
                dataManagerWindow.OwnerMainWindow = this;
                dataManagerWindow.ModelLoadRequested += DataManagerWindow_ModelLoadRequested;
                dataManagerWindow.Closed += (_, _) => dataManagerWindow = null;
            }
            UpdateSharedVariables();
            dataManagerWindow.LoadedPaths = GetLoadedPaths();
            dataManagerWindow.Show();
            dataManagerWindow.Activate();
        }

        private void UpdateSharedVariables()
        {
            if (dataManagerWindow is not null)
            {
                dataManagerWindow.homepageLink = homepageLink;
                dataManagerWindow.repoLink = repoLink;
                dataManagerWindow.updateLink = updateLink;
                dataManagerWindow.chibiListLink = chibiListLink;
                dataManagerWindow.helpLink = helpLink;
                dataManagerWindow.announcementMsg = announcementMsg;
                dataManagerWindow.UpdateSharedVariables();
            }
        }

        /// <summary>
        /// 收集所有运行中桌宠实例正在使用的模型数据目录（相对 assets/spine/ 的 path）。
        /// </summary>
        public System.Collections.Generic.HashSet<string> GetLoadedPaths()
        {
            var paths = new System.Collections.Generic.HashSet<string>();
            foreach (var pet in pets)
            {
                if (pet.Model?.SkeletonFile is string s)
                {
                    // 形如 assets/spine/{path}/{file}.skel 或 assets/spine_external/{path}/{file}.skel
                    string[] parts = s.Split('/');
                    if (parts.Length >= 3 && parts[0] == "assets" && (parts[1] == "spine" || parts[1] == "spine_external"))
                    {
                        // 带目录前缀区分内置/导入数据，避免同名数据互相误判
                        paths.Add($"{parts[1]}/{parts[2]}");
                    }
                }
            }
            return paths;
        }

        private async void DataManagerWindow_ModelLoadRequested(ChibiModelData data)
        {
            try
            {
                if (data.NewInstance)
                {
                    StartInstance(data, applyCategoryDefaults: true);
                }
                else
                {
                    await RestartSelected(data);
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

        public void ShowAbout()
        {
            if (aboutDialog is null)
            {
                aboutDialog = new AboutDialog();
            }
            aboutDialog.homepageLink = homepageLink;
            aboutDialog.repoLink = repoLink;
            aboutDialog.updateLink = updateLink;
            aboutDialog.Closed += (s, e) => aboutDialog = null;
            aboutDialog.Show();
        }

        private void menuItemAbout_Click(object sender, RoutedEventArgs e)
        {
            ShowAbout();
        }

        private SettingsDialog? settingsDialog;

        /// <summary>设置对话框访问的上下文（消息提示/运行模块/界面选项）。</summary>
        internal MainWindowDataContext SettingsDataContext => context;

        /// <summary>全局启用旧版（转发到 DataContext，触发保存与绑定通知）。</summary>
        public bool EnableLegacy
        {
            get => context.EnableLegacy;
            set => context.EnableLegacy = value;
        }

        private void ShowSettings_Click(object sender, RoutedEventArgs e) => ShowSettings();

        /// <summary>
        /// 打开设置对话框（消息设置 / 运行模块 / 界面选项）。
        /// </summary>
        public void ShowSettings()
        {
            if (settingsDialog is null)
            {
                settingsDialog = new SettingsDialog(this);
                settingsDialog.Closed += (_, _) => settingsDialog = null;
            }
            settingsDialog.Show();
            settingsDialog.Activate();
        }

        /// <summary>
        /// 按实例的渲染模块选择启动进程：新版用 luajit；旧版按全局后端选 GflChibiDesktopLegacyGL/DX.exe。
        /// </summary>
        private ProcessManager StartPetProcess(PetInstance pet)
        {
            // 全局禁用新版 → 强制旧版；禁用旧版 → 强制新版；否则按实例选择
            bool useLegacy = pet.UseLegacyModule;
            if (Properties.Settings.Default.DisableNew)
            {
                useLegacy = true;
            }
            else if (!Properties.Settings.Default.EnableLegacy)
            {
                useLegacy = false;
            }
            if (useLegacy)
            {
                bool useOpenGL = Properties.Settings.Default.UseOpenGL;
                string exe = FindLegacyExe(useOpenGL) ?? throw new FileNotFoundException(
                    useOpenGL ? "未找到备用渲染模块（OpenGL）GflChibiDesktopLegacyGL.exe。" : "未找到备用渲染模块（DirectX）GflChibiDesktopLegacyDX.exe。");
                string? atlas = pet.Model?.AtlasFile is string af ? Path.Combine(AppDir, af.Replace('/', Path.DirectorySeparatorChar)) : null;
                if (atlas is null || !File.Exists(atlas))
                {
                    throw new FileNotFoundException("未找到模型文件：" + atlas);
                }
                // legacy 实例的工作目录用 pet.WorkDir（实例各自独立），避免多实例同时写 app/out.log 冲突
                return new ProcessManager(exe, pet.WorkDir, $"--model \"{atlas}\" --name \"{pet.Name}\"",
                    () => Dispatcher.BeginInvoke(() => { if (pet.Manager is not null) ReadIpc(pet.Manager, pet); }));
            }
            return new ProcessManager(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app/luajit.exe"),
                pet.WorkDir,
                "main.lua",
                () => Dispatcher.BeginInvoke(() => { if (pet.Manager is not null) ReadIpc(pet.Manager, pet); }));
        }

        /// <summary>
        /// 查找旧版渲染模块 exe：按后端找 app 下对应目录，再回退到本解决方案的构建输出。
        /// </summary>
        private static string? FindLegacyExe(bool useOpenGL)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dir = useOpenGL ? "LegacyGL" : "LegacyDX";
            string file = useOpenGL ? "GflChibiDesktopLegacyGL.exe" : "GflChibiDesktopLegacyDX.exe";
            string[] candidates =
            {
                Path.Combine(baseDir, "app", dir, file),
                Path.Combine(baseDir, "..", "..", "..", "GflChibiDesktopLegacy", "bin", dir, "Debug", "net6.0-windows", "win-x64", file),
                Path.Combine(baseDir, "..", "..", "..", "GflChibiDesktopLegacy", "bin", dir, "Release", "net6.0-windows", "win-x64", file),
            };
            foreach (string c in candidates)
            {
                if (File.Exists(c)) return Path.GetFullPath(c);
            }
            return null;
        }

        private void menuItemHelp_Click(object sender, RoutedEventArgs e)
        {
            UrlHelper.OpenUrl(helpLink);
        }
    }

    class MainWindowDataContext : INotifyPropertyChanged
    {
        public readonly string productTitle = ((AssemblyTitleAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyTitleAttribute))).Title.ToString();
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
            get => AutoRun.IsAutoRun(CurrentExePath, productTitle);
            set
            {
                AutoRun.SetAutoRun(CurrentExePath, productTitle, value);
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 禁用全部全局提示（总开关，同时影响以下各项）。
        /// </summary>
        public bool SuppressGlobalGrowl
        {
            get => Settings.Default.SuppressGlobalGrowl;
            set
            {
                Settings.Default.SuppressGlobalGrowl = value;
                Settings.Default.Save();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 高级设置解锁（EasterEgg），用于启用“解除多开限制”等选项。
        /// </summary>
        public bool EasterEgg
        {
            get => Settings.Default.EasterEgg;
            set
            {
                Settings.Default.EasterEgg = value;
                Settings.Default.Save();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 解除 8 个桌宠实例的多开数量限制。
        /// </summary>
        public bool SuppressMultiInstanceWarning
        {
            get => Settings.Default.SuppressMultiInstanceWarning;
            set
            {
                Settings.Default.SuppressMultiInstanceWarning = value;
                Settings.Default.Save();
                OnPropertyChanged();
            }
        }

        public bool SuppressLoadPrompts
        {
            get => Settings.Default.SuppressLoadPrompts;
            set
            {
                Settings.Default.SuppressLoadPrompts = value;
                Settings.Default.Save();
                OnPropertyChanged();
            }
        }

        public bool SuppressUpdatePrompts
        {
            get => Settings.Default.SuppressUpdatePrompts;
            set
            {
                Settings.Default.SuppressUpdatePrompts = value;
                Settings.Default.Save();
                OnPropertyChanged();
            }
        }

        public bool SuppressMinimizePrompts
        {
            get => Settings.Default.SuppressMinimizePrompts;
            set
            {
                Settings.Default.SuppressMinimizePrompts = value;
                Settings.Default.Save();
                OnPropertyChanged();
            }
        }

        public bool SuppressConnectionErrorPrompts
        {
            get => Settings.Default.SuppressConnectionErrorPrompts;
            set
            {
                Settings.Default.SuppressConnectionErrorPrompts = value;
                Settings.Default.Save();
                OnPropertyChanged();
            }
        }

        public bool ForceOfflineMode
        {
            get => Settings.Default.ForceOfflineMode;
            set
            {
                Settings.Default.ForceOfflineMode = value;
                Settings.Default.Save();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 禁用 hc:Window 重绘窗体样式（使用系统原生窗口）。
        /// </summary>
        public bool DisableCustomWindowChrome
        {
            get => Settings.Default.DisableCustomWindowChrome;
            set
            {
                Settings.Default.DisableCustomWindowChrome = value;
                Settings.Default.Save();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 兼容透明模式（UpdateLayeredWindow）：通过 app/transparent_mode.conf 控制，
        /// lua 渲染进程读取该文件决定透明实现；解决部分系统/驱动下透明窗口显示黑底的问题。
        /// </summary>
        public bool UseUlwTransparency
        {
            get
            {
                try
                {
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app", "transparent_mode.conf");
                    return File.Exists(path) && File.ReadAllText(path).Trim() == "ulw";
                }
                catch
                {
                    return false;
                }
            }
            set
            {
                try
                {
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app", "transparent_mode.conf");
                    if (value)
                    {
                        File.WriteAllText(path, "ulw");
                    }
                    else if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch
                {
                }
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 运行模块：true=旧版(legacy)；false=新版(luajit)。切换后需重启实例生效。
        /// </summary>
        public bool UseLegacyModule
        {
            get => Settings.Default.UseLegacyModule;
            set
            {
                Settings.Default.UseLegacyModule = value;
                Settings.Default.Save();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 旧版渲染后端：true=OpenGL；false=DirectX。
        /// </summary>
        public bool UseOpenGL
        {
            get => Settings.Default.UseOpenGL;
            set
            {
                Settings.Default.UseOpenGL = value;
                Settings.Default.Save();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 全局启用旧版（MonoGame）功能。
        /// </summary>
        public bool EnableLegacy
        {
            get => Settings.Default.EnableLegacy;
            set
            {
                Settings.Default.EnableLegacy = value;
                // 禁用旧版时同时取消“禁用新版”，避免两版全被禁用
                if (!value && Settings.Default.DisableNew)
                {
                    Settings.Default.DisableNew = false;
                }
                Settings.Default.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisableNew));
                OnPropertyChanged(nameof(CanSetDefaultLegacy));
            }
        }

        /// <summary>
        /// 全局禁用新版（Raylib）功能；勾选后强制旧版为默认。
        /// </summary>
        public bool DisableNew
        {
            get => Settings.Default.DisableNew;
            set
            {
                Settings.Default.DisableNew = value;
                if (value)
                {
                    // 禁用新版 → 强制旧版为默认（新实例默认使用旧版）
                    Settings.Default.UseLegacyModule = true;
                }
                Settings.Default.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(UseLegacyModule));
                OnPropertyChanged(nameof(CanSetDefaultLegacy));
            }
        }

        /// <summary>是否可设置“旧版为默认”（启用旧版且未禁用新版时才可修改）。</summary>
        public bool CanSetDefaultLegacy => Settings.Default.EnableLegacy && !Settings.Default.DisableNew;

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
