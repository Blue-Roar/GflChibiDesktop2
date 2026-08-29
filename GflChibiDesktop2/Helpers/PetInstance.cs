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
        /// <summary>标签页顶部的实例级版本切换面板（全局禁用旧版时隐藏）。</summary>
        public HandyControl.Controls.ButtonGroup? VersionPanel { get; set; }
        /// <summary>
        /// 暂停时显示在控制面板位置上的提示信息（默认隐藏）。
        /// </summary>
        public TextBlock? HintPanel { get; set; }
        public bool IsChanged { get; set; }
        /// <summary>
        /// 该实例当前使用的模型（用于多开持久化恢复）。
        /// </summary>
        public ChibiModelData? Model { get; set; }
        /// <summary>
        /// 是否正在重启中（进程退出事件在重启期间被忽略，避免被当作正常结束而移除）。
        /// </summary>
        public bool IsRestarting { get; set; }
        /// <summary>
        /// 是否由用户主动停止（主动停止后不自动重启）。
        /// </summary>
        public bool StopRequested { get; set; }
        /// <summary>
        /// 是否处于暂停状态（进程已停止、标签页保留，重启后仍保持暂停）。
        /// </summary>
        public bool IsSuspended { get; set; }
        /// <summary>
        /// 异常退出后的自动重启尝试次数。
        /// </summary>
        public int RestartAttempts { get; set; }
        /// <summary>
        /// 本实例使用的渲染模块：true=旧版(MonoGame)；false=新版(Raylib)。
        /// 与全局设置分开，每个实例可独立选择；DWM/ULW 与 OpenGL/DirectX 跟随全局设置。
        /// </summary>
        public bool UseLegacyModule { get; set; }

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
            // 备份已计算的 model.conf.json 画布尺寸（首次启动由 luajit 计算并写回，之后保留，
            // 避免每次启动重算包围盒导致卡顿；模型变更时下方按模型路径判断是否沿用）
            string savedModelConf = null;
            string modelConfFile = Path.Combine(workDir, "assets", "model.conf.json");
            try
            {
                if (File.Exists(modelConfFile))
                {
                    savedModelConf = File.ReadAllText(modelConfFile);
                }
            }
            catch
            {
            }
            // 备份 legacy 渲染模块保存的窗口位置
            string savedLegacyPosition = null;
            string legacyPositionFile = Path.Combine(workDir, "legacy_position.json");
            try
            {
                if (File.Exists(legacyPositionFile))
                {
                    savedLegacyPosition = File.ReadAllText(legacyPositionFile);
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

            // 共享 lua 模块（junction）。原"复制避免共享"基于多实例共享 DLL 导致骨骼抽风/约束失效的假设，
            // 已确认根因是模型 FFD 数据异常（SkinnedMeshAttachment.c 已修复），共享无碍且省磁盘。
            LinkDir(Path.Combine(workDir, "lua"), Path.Combine(appDir, "lua"));

            // 实例配置
            string name = model.DisplayName;
            File.WriteAllText(Path.Combine(assetsDir, "name.txt"), model.DisplayName);
            // 优先沿用已计算的 model.conf.json（含画布尺寸），避免每次启动重算包围盒；
            // 仅当备份缺失、模型已变更（骨架路径不同）、或仍是旧版硬编码 448 占位尺寸时才重新计算。
            // 旧版占位格式为 C# 精确拼接（luajit 写回时字段顺序/格式不同，不会误匹配）
            string stalePlaceholder = "{\"skeleton\":\"" + model.SkeletonFile + "\",\"type\":\"skel\",\"atlas\":\"" + model.AtlasFile + "\",\"h\":448,\"w\":448,\"x\":224,\"y\":224}";
            bool useSaved = !string.IsNullOrEmpty(savedModelConf)
                && savedModelConf != stalePlaceholder
                && savedModelConf.Contains("\"" + model.SkeletonFile + "\"", StringComparison.Ordinal);
            if (useSaved)
            {
                File.WriteAllText(Path.Combine(assetsDir, "model.conf.json"), savedModelConf);
            }
            else
            {
                File.WriteAllText(Path.Combine(assetsDir, "model.conf.json"),
                    "{\"skeleton\":\"" + model.SkeletonFile + "\",\"type\":\"skel\",\"atlas\":\"" + model.AtlasFile + "\"}");
            }

            // 音频等共享配置
            foreach (string f in new[] { "audio.conf.json", "pet.conf.json" })
            {
                string src = Path.Combine(appDir, "assets", f);
                if (File.Exists(src)) File.Copy(src, Path.Combine(assetsDir, f), true);
            }

            // 共享素材（spine 骨骼数据；luajit 不使用 pic，立绘由主程序 CG 窗口负责）
            LinkDir(Path.Combine(assetsDir, "spine"), Path.Combine(appDir, "assets", "spine"));
            // 外部导入的骨骼数据
            LinkDir(Path.Combine(assetsDir, "spine_external"), Path.Combine(appDir, "assets", "spine_external"));

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

            // 恢复 legacy 渲染模块的窗口位置
            if (!string.IsNullOrEmpty(savedLegacyPosition))
            {
                try
                {
                    File.WriteAllText(Path.Combine(workDir, "legacy_position.json"), savedLegacyPosition);
                }
                catch
                {
                }
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
            // 模型变更：不写尺寸字段，由 luajit 首次启动计算画布尺寸并写回
            File.WriteAllText(Path.Combine(assetsDir, "model.conf.json"),
                "{\"skeleton\":\"" + model.SkeletonFile + "\",\"type\":\"skel\",\"atlas\":\"" + model.AtlasFile + "\"}");
        }

        /// <summary>
        /// 创建目录联接（优先 junction，失败则回退为复制）。
        /// 目标目录尚不存在时也建立 junction（指向空目录），之后导入的数据自动可见。
        /// </summary>
        private static void LinkDir(string linkPath, string targetPath)
        {
            if (Directory.Exists(linkPath))
            {
                var info = new DirectoryInfo(linkPath);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0) return;
                // 已存在的普通空目录（首次启动时目标缺失产生的残留）：替换为 junction
                if (Directory.GetFileSystemEntries(linkPath).Length == 0)
                {
                    try
                    {
                        Directory.Delete(linkPath, false);
                    }
                    catch
                    {
                        return;
                    }
                }
                else
                {
                    // 非空普通目录（mklink 失败后的复制回退产物）：保留
                    return;
                }
            }
            // 确保目标存在，junction 可指向空目录
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
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
