#nullable enable
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace GflChibiDesktop2
{
    /// <summary>
    /// ImportExternalDialog.xaml 的交互逻辑
    /// </summary>
    public partial class ImportExternalDialog : UserControl
    {
        public event Action<string, string[]>? ConfirmRequested;

        private string[] selectedFiles = Array.Empty<string>();

        public ImportExternalDialog()
        {
            InitializeComponent();
        }

        private void btn_browse_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择骨骼数据文件",
                Filter = "Spine 骨骼数据 (*.atlas;*.skel;*.png)|*.atlas;*.skel;*.png|所有文件 (*.*)|*.*",
                Multiselect = true
            };
            if (ofd.ShowDialog() == true)
            {
                selectedFiles = ofd.FileNames;
                tb_files.Text = $"{System.IO.Path.GetFileName(selectedFiles[0])} 等 {selectedFiles.Length} 个文件";
                // 打开文件时皮肤名称为空则填入基础基名（排除 r{基名} 宿舍文件）
                if (string.IsNullOrEmpty(tb_skinName.Text?.Trim()))
                {
                    string? baseName = DetermineBaseName(selectedFiles);
                    if (baseName is not null)
                    {
                        tb_skinName.Text = baseName;
                    }
                }
            }
        }

        /// <summary>
        /// 从所选文件中确定基础基名（r{基名} 形式的宿舍文件除外）。
        /// </summary>
        private static string? DetermineBaseName(string[] files)
        {
            var skelNames = files
                .Where(f => System.IO.Path.GetExtension(f).Equals(".skel", StringComparison.OrdinalIgnoreCase))
                .Select(f => System.IO.Path.GetFileNameWithoutExtension(f))
                .ToList();
            foreach (string bn in skelNames)
            {
                bool isRVersion = skelNames.Any(o => o != bn && string.Equals(bn, "r" + o, StringComparison.OrdinalIgnoreCase));
                if (!isRVersion)
                {
                    return bn;
                }
            }
            return null;
        }

        private void btn_ok_Click(object sender, RoutedEventArgs e)
        {
            string name = tb_skinName.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(name))
            {
                HandyControl.Controls.Growl.WarningGlobal("请输入皮肤名称。");
                return;
            }
            if (selectedFiles.Length == 0)
            {
                HandyControl.Controls.Growl.WarningGlobal("请选择骨骼数据文件。");
                return;
            }
            ConfirmRequested?.Invoke(name, selectedFiles);
            btn_Cancel.Command.Execute(null);
        }
    }
}
