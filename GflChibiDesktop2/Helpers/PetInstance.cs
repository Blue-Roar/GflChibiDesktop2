using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;

namespace GflChibiDesktop2
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
        public ChibiModelData? Model { get; set; }
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
        public static PetInstance Create(int id, ChibiModelData model)
        {
            string appDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app");
            string workDir = Path.Combine(appDir, "instances", id.ToString());

            // 备份该实例已保存的 settings.json（重启/重建实例时保留用户配置）
            string savedSettings = null;
            string settingsFile = Path.Combine(workDir, "settings.json");
            try
            {
                if (File.Exists(settingsFile))
                {
                    savedSettings = File.ReadAllText(settingsFile);
                }
            }
            catch
            {
            }

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
            string name = model.DisplayName;
            File.WriteAllText(Path.Combine(assetsDir, "name.txt"), model.DisplayName);
            File.WriteAllText(Path.Combine(assetsDir, "model.conf.json"),
                "{\"skeleton\":\"" + model.SkeletonFile + "\",\"type\":\"skel\",\"atlas\":\"" + model.AtlasFile + "\",\"h\":448,\"w\":448,\"x\":224,\"y\":224}");

            // 音频等共享配置
            foreach (string f in new[] { "audio.conf.json", "pet.conf.json" })
            {
                string src = Path.Combine(appDir, "assets", f);
                if (File.Exists(src)) File.Copy(src, Path.Combine(assetsDir, f), true);
            }

            // 共享素材（spine 骨骼数据；luajit 不使用 pic，立绘由主程序 CG 窗口负责）
            LinkDir(Path.Combine(assetsDir, "spine"), Path.Combine(appDir, "assets", "spine"));

            // 窗口位置等设置：优先恢复该实例已保存的配置，否则从默认拷贝一份
            if (!string.IsNullOrEmpty(savedSettings))
            {
                try
                {
                    File.WriteAllText(Path.Combine(workDir, "settings.json"), savedSettings);
                }
                catch
                {
                }
            }
            else
            {
                string srcSettings = Path.Combine(appDir, "settings.json");
                if (File.Exists(srcSettings)) File.Copy(srcSettings, Path.Combine(workDir, "settings.json"), true);
            }

            return new PetInstance(id, name, workDir) { Model = model };
        }

        /// <summary>
        /// 用新模型重写当前实例的配置。
        /// </summary>
        public void UpdateModel(ChibiModelData model)
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
