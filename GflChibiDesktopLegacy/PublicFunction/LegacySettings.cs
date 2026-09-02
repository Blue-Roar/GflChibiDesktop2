using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GflChibiDesktop
{
    /// <summary>
    /// legacy 渲染模块的持久化设置（实例工作目录 settings1.json，正常 JSON，V2 重建实例时备份恢复）。
    /// 字段：left/top = 窗口位置、canvasMode = 画布模式。只读写 settings1.json（不做旧文件兼容）。
    /// </summary>
    internal static class LegacySettings
    {
        public static string SettingsFile => Path.Combine(Environment.CurrentDirectory, "settings1.json");

        /// <summary>画布模式默认值：动态(2)。</summary>
        public const int DefaultCanvasMode = 2;

        /// <summary>读取画布模式；文件缺失/损坏时返回默认动态(2)。</summary>
        public static int LoadCanvasMode()
        {
            if (TryGetInt(SettingsFile, "canvasMode", out int mode))
            {
                return mode;
            }
            return DefaultCanvasMode;
        }

        /// <summary>读取窗口位置；成功返回 true。</summary>
        public static bool TryLoadWindowPosition(out double left, out double top)
        {
            left = 0;
            top = 0;
            try
            {
                if (File.Exists(SettingsFile))
                {
                    using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(SettingsFile)))
                    {
                        JsonElement root = doc.RootElement;
                        if (root.ValueKind == JsonValueKind.Object &&
                            root.TryGetProperty("left", out JsonElement jl) &&
                            root.TryGetProperty("top", out JsonElement jt) &&
                            jl.TryGetDouble(out left) && jt.TryGetDouble(out top))
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        /// <summary>保存画布模式（0=小 448、1=大 768、2=动态），保留其余字段。</summary>
        public static void SaveCanvasMode(int mode)
        {
            if (mode < 0 || mode > 2) mode = DefaultCanvasMode;
            Update(root => root["canvasMode"] = mode);
        }

        /// <summary>保存窗口位置，保留其余字段。</summary>
        public static void SaveWindowPosition(double left, double top)
        {
            Update(root => { root["left"] = left; root["top"] = top; });
        }

        /// <summary>读取 JSON 数值字段。</summary>
        private static bool TryGetInt(string file, string name, out int value)
        {
            value = 0;
            try
            {
                if (!File.Exists(file)) return false;
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file)))
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                        doc.RootElement.TryGetProperty(name, out JsonElement je) &&
                        je.TryGetInt32(out value))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }
            return false;
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
