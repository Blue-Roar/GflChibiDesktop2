#nullable disable
using System;
using System.IO;

namespace GflChibiDesktop.Properties
{
    internal sealed class Settings
    {
        private static readonly Settings defaultInstance = new Settings();
        public static Settings Default => defaultInstance;

        public string DownloadSource { get; set; } = "https://gfl-data.brightsu.cn/res/";

        private static string FilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app", "gfl_settings.json");

        private Settings()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = Newtonsoft.Json.JsonConvert.DeserializeObject<Settings>(File.ReadAllText(FilePath));
                    if (json != null && !string.IsNullOrEmpty(json.DownloadSource))
                    {
                        DownloadSource = json.DownloadSource;
                    }
                }
            }
            catch
            {
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, Newtonsoft.Json.JsonConvert.SerializeObject(this));
            }
            catch
            {
            }
        }
    }
}
