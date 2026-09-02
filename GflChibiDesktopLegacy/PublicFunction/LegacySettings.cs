using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GflChibiDesktop
{
    /// <summary>
    /// legacy 渲染模块的持久化设置（实例工作目录 settings1.json，正常 JSON，V2 重建实例时备份恢复）。
    /// 字段：left/top = 窗口位置、canvasMode = 画布模式，以及全部 IPC 面板项
    /// （anime/scale/opacity/speed/rotation/loop/moveFlip/simulation/simulateInterval/topmost/clickThrough）。
    /// 只读写 settings1.json（不做旧文件兼容）。
    /// </summary>
    internal static class LegacySettings
    {
        public static string SettingsFile => Path.Combine(Environment.CurrentDirectory, "settings1.json");

        /// <summary>画布模式默认值：小(448×448)。</summary>
        public const int DefaultCanvasMode = 0;

        // ---------- 兼容旧调用（窗口位置 / 画布模式） ----------

        /// <summary>读取画布模式；文件缺失/损坏时返回默认小(448×448)。</summary>
        public static int LoadCanvasMode() => GetInt("canvasMode", DefaultCanvasMode);

        /// <summary>保存画布模式（0=小 448、1=大 768、2=动态），保留其余字段。</summary>
        public static void SaveCanvasMode(int mode) => SetInt("canvasMode", mode < 0 || mode > 2 ? DefaultCanvasMode : mode);

        /// <summary>读取窗口位置；成功返回 true。</summary>
        public static bool TryLoadWindowPosition(out double left, out double top)
        {
            left = GetDouble("left", double.NaN);
            top = GetDouble("top", double.NaN);
            return !double.IsNaN(left) && !double.IsNaN(top);
        }

        /// <summary>保存窗口位置，保留其余字段。</summary>
        public static void SaveWindowPosition(double left, double top)
        {
            Update(root => { root["left"] = left; root["top"] = top; });
        }

        // ---------- 通用读写 ----------

        public static double GetDouble(string key, double def)
        {
            try
            {
                return GetNode(key) is JsonValue v ? v.GetValue<double>() : def;
            }
            catch
            {
                return def;
            }
        }

        public static int GetInt(string key, int def)
        {
            try
            {
                return GetNode(key) is JsonValue v ? v.GetValue<int>() : def;
            }
            catch
            {
                return def;
            }
        }

        public static bool GetBool(string key, bool def)
        {
            try
            {
                return GetNode(key) is JsonValue v ? v.GetValue<bool>() : def;
            }
            catch
            {
                return def;
            }
        }

        public static string GetString(string key, string def)
        {
            try
            {
                return GetNode(key) is JsonValue v ? v.GetValue<string>() : def;
            }
            catch
            {
                return def;
            }
        }

        public static void SetDouble(string key, double value) => Update(root => root[key] = value);

        public static void SetInt(string key, int value) => Update(root => root[key] = value);

        public static void SetBool(string key, bool value) => Update(root => root[key] = value);

        public static void SetString(string key, string value) => Update(root => root[key] = value);

        private static JsonNode GetNode(string key)
        {
            if (!File.Exists(SettingsFile)) return null;
            JsonNode root = JsonNode.Parse(File.ReadAllText(SettingsFile));
            if (root is JsonObject obj && obj[key] != null)
            {
                return obj[key];
            }
            return null;
        }

        /// <summary>读取现有设置、应用变更后整体写回 settings1.json。</summary>
        private static void Update(Action<JsonObject> mutate)
        {
            try
            {
                JsonObject root;
                try
                {
                    if (File.Exists(SettingsFile))
                    {
                        root = JsonNode.Parse(File.ReadAllText(SettingsFile)) as JsonObject ?? new JsonObject();
                    }
                    else
                    {
                        root = new JsonObject();
                    }
                }
                catch
                {
                    root = new JsonObject();
                }
                mutate(root);
                Directory.CreateDirectory(Environment.CurrentDirectory);
                File.WriteAllText(SettingsFile, root.ToJsonString());
            }
            catch
            {
            }
        }
    }
}
