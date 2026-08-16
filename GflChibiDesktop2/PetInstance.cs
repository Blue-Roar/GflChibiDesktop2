using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;

namespace HDTLPanel
{
    /// <summary>
    /// 一个桌宠实例（一个 luajit 子进程 + 一个标签页）。
    /// </summary>
    internal class PetInstance
    {
        public int Id { get; }
        public string Name { get; set; }
        public string WorkDir { get; }
        public ProcessManager? Manager { get; set; }
        public TabItem? Tab { get; set; }
        public TextBlock? TabTitle { get; set; }
        public StackPanel Panel { get; } = new();
        public bool IsChanged { get; set; }
        /// <summary>
        /// 该实例当前使用的模型（用于多开持久化恢复）。
        /// </summary>
        public GflChibiDesktop.Windows.ChibiModelData? Model { get; set; }
        /// <summary>
        /// 是否正在重启中（进程退出事件在重启期间被忽略，避免被当作正常结束而移除）。
        /// </summary>
        public bool IsRestarting { get; set; }

        public PetInstance(int id, string name, string workDir)
        {
            Id = id;
            Name = name;
            WorkDir = workDir;
        }

        public void Dispose()
        {
            try
            {
                Manager?.Dispose();
            }
            catch
            {
            }
            SafeDelete(WorkDir);
        }

        /// <summary>
        /// 创建实例工作目录，复用共享的 lua 模块与素材。
        /// </summary>
        public static PetInstance Create(int id, GflChibiDesktop.Windows.ChibiModelData? model)
        {
            string appDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app");
            string workDir = Path.Combine(appDir, "instances", id.ToString());
            // 清理残留：普通目录直接删除，悬空 junction 也强制移除
            try
            {
                if (Directory.Exists(workDir))
                {
                    SafeDelete(workDir);
                }
                else
                {
                    // 悬空 junction：Exists 为 false 但路径仍被占用，尝试按链接删除
                    Directory.Delete(workDir, false);
                }
            }
            catch
            {
            }
            Directory.CreateDirectory(workDir);
            string assetsDir = Path.Combine(workDir, "assets");
            Directory.CreateDirectory(assetsDir);

            // 入口 lua 文件
            foreach (string f in new[] { "main.lua", "hdtbase.lua", "hdtmodule.lua" })
            {
                string src = Path.Combine(appDir, f);
                if (File.Exists(src)) File.Copy(src, Path.Combine(workDir, f), true);
            }

            // 复制 lua 模块（含 raylib DLL）。不用 junction 共享，避免多实例进程加载同一 DLL 时
            // 出现渲染上下文冲突导致骨骼抽风/约束失效。
            CopyDirectory(Path.Combine(appDir, "lua"), Path.Combine(workDir, "lua"));

            // 实例配置
            string name;
            if (model != null)
            {
                name = model.DisplayName;
                File.WriteAllText(Path.Combine(assetsDir, "name.txt"), model.DisplayName);
                File.WriteAllText(Path.Combine(assetsDir, "model.conf.json"),
                    "{\"skeleton\":\"" + model.SkeletonFile + "\",\"type\":\"skel\",\"atlas\":\"" + model.AtlasFile + "\",\"h\":448,\"w\":448,\"x\":224,\"y\":224}");
            }
            else
            {
                string srcName = Path.Combine(appDir, "assets", "name.txt");
                string srcModel = Path.Combine(appDir, "assets", "model.conf.json");
                name = File.Exists(srcName) ? File.ReadAllText(srcName) : $"桌宠 #{id}";
                if (File.Exists(srcName)) File.Copy(srcName, Path.Combine(assetsDir, "name.txt"), true);
                if (File.Exists(srcModel)) File.Copy(srcModel, Path.Combine(assetsDir, "model.conf.json"), true);
                model = ParseModel(name, srcModel);
            }

            // 音频等共享配置
            foreach (string f in new[] { "audio.conf.json", "pet.conf.json" })
            {
                string src = Path.Combine(appDir, "assets", f);
                if (File.Exists(src)) File.Copy(src, Path.Combine(assetsDir, f), true);
            }

            // 共享素材（spine 骨骼数据；luajit 不使用 pic，立绘由主程序 CG 窗口负责）
            LinkDir(Path.Combine(assetsDir, "spine"), Path.Combine(appDir, "assets", "spine"));

            // 窗口位置等设置，从默认拷贝一份
            string srcSettings = Path.Combine(appDir, "settings.json");
            if (File.Exists(srcSettings)) File.Copy(srcSettings, Path.Combine(workDir, "settings.json"), true);

            return new PetInstance(id, name, workDir) { Model = model };
        }

        /// <summary>
        /// 从默认的 model.conf.json 解析出模型信息。
        /// </summary>
        private static GflChibiDesktop.Windows.ChibiModelData? ParseModel(string name, string modelJsonPath)
        {
            try
            {
                if (File.Exists(modelJsonPath))
                {
                    JObject obj = JObject.Parse(File.ReadAllText(modelJsonPath));
                    return new GflChibiDesktop.Windows.ChibiModelData
                    {
                        DisplayName = name,
                        SkeletonFile = obj["skeleton"]?.ToString() ?? string.Empty,
                        AtlasFile = obj["atlas"]?.ToString() ?? string.Empty
                    };
                }
            }
            catch
            {
            }
            return null;
        }

        /// <summary>
        /// 用新模型重写当前实例的配置。
        /// </summary>
        public void UpdateModel(GflChibiDesktop.Windows.ChibiModelData model)
        {
            Model = model;
            Name = model.DisplayName;
            string assetsDir = Path.Combine(WorkDir, "assets");
            Directory.CreateDirectory(assetsDir);
            File.WriteAllText(Path.Combine(assetsDir, "name.txt"), model.DisplayName);
            File.WriteAllText(Path.Combine(assetsDir, "model.conf.json"),
                "{\"skeleton\":\"" + model.SkeletonFile + "\",\"type\":\"skel\",\"atlas\":\"" + model.AtlasFile + "\",\"h\":448,\"w\":448,\"x\":224,\"y\":224}");
        }

        /// <summary>
        /// 创建目录联接（优先 junction，失败则回退为复制）。
        /// </summary>
        private static void LinkDir(string linkPath, string targetPath)
        {
            if (Directory.Exists(linkPath)) return;
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(linkPath);
                return;
            }
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{linkPath}\" \"{targetPath}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (var p = Process.Start(psi))
                {
                    p?.WaitForExit();
                }
                if (Directory.Exists(linkPath)) return;
            }
            catch
            {
            }
            CopyDirectory(targetPath, linkPath);
        }

        private static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (string f in Directory.GetFiles(src))
            {
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            }
            foreach (string d in Directory.GetDirectories(src))
            {
                CopyDirectory(d, Path.Combine(dst, Path.GetFileName(d)));
            }
        }

        /// <summary>
        /// 安全删除目录：先删联接点（避免跟随 junction 删除共享内容），再递归删除。
        /// 对悬空 junction（Exists 为 true 但枚举抛异常）直接删链接本身。
        /// </summary>
        private static void SafeDelete(string dir)
        {
            if (!Directory.Exists(dir)) return;
            try
            {
                // 悬空 junction 或损坏目录：GetDirectories 会抛异常，直接删链接
                string[] subs = Directory.GetDirectories(dir);
                foreach (string sub in subs)
                {
                    var info = new DirectoryInfo(sub);
                    if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        Directory.Delete(sub, false);
                    }
                    else
                    {
                        SafeDelete(sub);
                    }
                }
                foreach (string f in Directory.GetFiles(dir))
                {
                    File.Delete(f);
                }
                Directory.Delete(dir, false);
            }
            catch (DirectoryNotFoundException)
            {
                // 悬空 junction：按链接删除（不递归，避免删到目标）
                try
                {
                    Directory.Delete(dir, false);
                }
                catch
                {
                }
            }
            catch
            {
            }
        }
    }
}
