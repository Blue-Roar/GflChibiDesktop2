#nullable disable
using System;
using System.IO;

namespace GflChibiDesktop2.Properties
{
    internal sealed class Settings
    {
        private static readonly Settings defaultInstance = new Settings();
        public static Settings Default => defaultInstance;
        public string DownloadSource { get; set; } = "https://gfl-data.brightsu.cn/res/";
        public bool EasterEgg { get; set; } = false;
        public bool SuppressMultiInstanceWarning { get; set; } = false;
        public bool SuppressGlobalGrowl { get; set; } = false;
        public bool SuppressLoadPrompts { get; set; } = false;
        public bool SuppressUpdatePrompts { get; set; } = false;
        /// <summary>用户忽略的更新版本（隐藏字段，仅用于抑制该版本及更早版本的更新提示）。</summary>
        public string SuppressedVersion { get; set; } = string.Empty;
        public bool SuppressMinimizePrompts { get; set; } = false;
        public bool SuppressConnectionErrorPrompts { get; set; } = false;
        public bool ForceOfflineMode { get; set; } = false;
        public bool DisableCustomWindowChrome { get; set; } = false;
        /// <summary>运行模块：true=旧版(legacy)渲染模块；false=新版(luajit)渲染模块。</summary>
        public bool UseLegacyModule { get; set; } = false;
        /// <summary>渲染后端：true=OpenGL(DesktopGL)；false=DirectX(WindowsDX)。</summary>
        public bool UseOpenGL { get; set; } = false;
        /// <summary>全局启用旧版（MonoGame）功能：false 时禁用旧版，实例版本切换面板隐藏且强制使用新版。</summary>
        public bool EnableLegacy { get; set; } = true;
        /// <summary>全局禁用新版（Raylib）功能：true 时禁用新版，实例强制使用旧版。</summary>
        public bool DisableNew { get; set; } = false;
        private static string FilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app", "gfl_settings.json");

        private class SettingsData
        {
            public string DownloadSource { get; set; }
            public bool EasterEgg { get; set; }
            public bool SuppressMultiInstanceWarning { get; set; }
            public bool SuppressGlobalGrowl { get; set; }
            public bool SuppressLoadPrompts { get; set; }
            public bool SuppressUpdatePrompts { get; set; }
            public string SuppressedVersion { get; set; }
            public bool SuppressMinimizePrompts { get; set; }
            public bool SuppressConnectionErrorPrompts { get; set; }
            public bool ForceOfflineMode { get; set; }
            public bool DisableCustomWindowChrome { get; set; }
            public bool UseLegacyModule { get; set; }
            public bool UseOpenGL { get; set; }
            public bool EnableLegacy { get; set; }
            public bool DisableNew { get; set; }
        }

        private Settings()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = Newtonsoft.Json.JsonConvert.DeserializeObject<SettingsData>(File.ReadAllText(FilePath));
                    if (json != null)
                    {
                        if (!string.IsNullOrEmpty(json.DownloadSource)) DownloadSource = json.DownloadSource;
                        if (json.EasterEgg)
                        {
                            EasterEgg = json.EasterEgg;
                            if (json.SuppressMultiInstanceWarning) SuppressMultiInstanceWarning = json.SuppressMultiInstanceWarning;
                        }
                        if (json.SuppressGlobalGrowl) SuppressGlobalGrowl = json.SuppressGlobalGrowl;
                        if (json.SuppressLoadPrompts) SuppressLoadPrompts = json.SuppressLoadPrompts;
                        if (json.SuppressUpdatePrompts) SuppressUpdatePrompts = json.SuppressUpdatePrompts;
                        if (!string.IsNullOrEmpty(json.SuppressedVersion)) SuppressedVersion = json.SuppressedVersion;
                        if (json.SuppressMinimizePrompts) SuppressMinimizePrompts = json.SuppressMinimizePrompts;
                        if (json.SuppressConnectionErrorPrompts) SuppressConnectionErrorPrompts = json.SuppressConnectionErrorPrompts;
                        if (json.ForceOfflineMode) ForceOfflineMode = json.ForceOfflineMode;
                        if (json.DisableCustomWindowChrome) DisableCustomWindowChrome = json.DisableCustomWindowChrome;
                        if (json.UseLegacyModule) UseLegacyModule = json.UseLegacyModule;
                        if (json.UseOpenGL) UseOpenGL = json.UseOpenGL;
                        if (json.EnableLegacy) EnableLegacy = json.EnableLegacy;
                        if (json.DisableNew) DisableNew = json.DisableNew;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"File path: {FilePath}");
                System.Diagnostics.Debug.WriteLine($"Exception details: {ex}");
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var data = new SettingsData
                {
                    DownloadSource = DownloadSource,
                    EasterEgg = EasterEgg,
                    SuppressMultiInstanceWarning = SuppressMultiInstanceWarning,
                    SuppressGlobalGrowl = SuppressGlobalGrowl,
                    SuppressLoadPrompts = SuppressLoadPrompts,
                    SuppressUpdatePrompts = SuppressUpdatePrompts,
                    SuppressedVersion = SuppressedVersion,
                    SuppressMinimizePrompts = SuppressMinimizePrompts,
                    SuppressConnectionErrorPrompts = SuppressConnectionErrorPrompts,
                    ForceOfflineMode = ForceOfflineMode,
                    DisableCustomWindowChrome = DisableCustomWindowChrome,
                    UseLegacyModule = UseLegacyModule,
                    UseOpenGL = UseOpenGL,
                    EnableLegacy = EnableLegacy,
                    DisableNew = DisableNew
                };
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(FilePath, json);
                System.Diagnostics.Debug.WriteLine($"Settings saved successfully to: {FilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"File path: {FilePath}");
                System.Diagnostics.Debug.WriteLine($"Exception details: {ex}");
            }
        }
    }
}
