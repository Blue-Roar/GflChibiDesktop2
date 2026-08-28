using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GflChibiDesktop
{
    /// <summary>
    /// 命令行启动参数（类似 luajit 子进程的传参方式）。
    /// 末尾两个位置参数为 V2 主程序传入的 IPC 名（读/写），存在时启用控制面板通信。
    /// </summary>
    internal static class LegacyArgs
    {
        /// <summary>要加载的模型 .atlas（绝对路径）。</summary>
        public static string ModelFile;
        /// <summary>模型显示名。</summary>
        public static string DisplayName;
        public static double Width = 448;
        public static double Height = 448;
        public static int PosX = 224;
        public static int PosY = 224;
        public static bool Topmost = true;
        /// <summary>从 V2 读取命令的 IPC 名（空表示未启用面板通信）。</summary>
        public static string ReadIpcName = string.Empty;
        /// <summary>向 V2 写入面板结构的 IPC 名。</summary>
        public static string WriteIpcName = string.Empty;

        public static void Parse(string[] args)
        {
            if (args != null)
            {
                var positional = new List<string>();
                for (int i = 0; i < args.Length; i++)
                {
                    string raw = args[i];
                    string a = raw.ToLowerInvariant();
                    if (a.StartsWith("--") || a.StartsWith("-"))
                    {
                        if (a == "--model" || a == "-m")
                        {
                            if (i + 1 < args.Length)
                            {
                                ModelFile = args[i + 1];
                                positional.Add(args[i + 1]);
                            }
                            i++;
                        }
                        else if (a == "--name" || a == "-n")
                        {
                            if (i + 1 < args.Length)
                            {
                                DisplayName = args[i + 1];
                                positional.Add(args[i + 1]);
                            }
                            i++;
                        }
                        else if (a == "--width" || a == "-w")
                        {
                            if (i + 1 < args.Length && double.TryParse(args[i + 1], out double v)) Width = v;
                            i++;
                        }
                        else if (a == "--height" || a == "-h")
                        {
                            if (i + 1 < args.Length && double.TryParse(args[i + 1], out double v)) Height = v;
                            i++;
                        }
                        else if (a == "--posx" || a == "-x")
                        {
                            if (i + 1 < args.Length && int.TryParse(args[i + 1], out int v)) PosX = v;
                            i++;
                        }
                        else if (a == "--posy" || a == "-y")
                        {
                            if (i + 1 < args.Length && int.TryParse(args[i + 1], out int v)) PosY = v;
                            i++;
                        }
                        else if (a == "--notopmost")
                        {
                            Topmost = false;
                        }
                        continue;
                    }
                    positional.Add(raw);
                }
                // 末尾两个位置参数 = V2 传入的 IPC 名（txIpc.GetName() 在前 = 读端，rxIpc.GetName() 在后 = 写端）
                if (positional.Count >= 2)
                {
                    ReadIpcName = positional[positional.Count - 2];
                    WriteIpcName = positional[positional.Count - 1];
                }
            }

            if (string.IsNullOrEmpty(ModelFile))
            {
                ModelFile = FindDefaultModel();
            }
        }

        /// <summary>未指定模型时，从 exe 所在目录的 Resources\spine 下找第一个 .atlas。</summary>
        private static string FindDefaultModel()
        {
            try
            {
                string spineDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "spine");
                if (Directory.Exists(spineDir))
                {
                    string atlas = Directory.EnumerateFiles(spineDir, "*.atlas", SearchOption.AllDirectories).FirstOrDefault();
                    if (atlas != null) return atlas;
                }
            }
            catch
            {
            }
            return null;
        }
    }
}
