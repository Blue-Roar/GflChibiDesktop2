using System;
using HandyControl.Data;

namespace GflChibiDesktop2
{
    /// <summary>
    /// 更新检查与提示（MainWindow 启动检查 / DataManagerWindow 更新链接检查共用）：
    /// 解析服务器返回的最新版本，高于当前版本且未被用户忽略（SuppressedVersion）时，
    /// 弹出"是否前往更新页面"询问（立刻查看 / 下次再说）；选"下次再说"后再询问是否
    /// "忽略此版本"——确认后把该版本写入 SuppressedVersion（全局设置），之后该版本及更早版本不再提示。
    /// </summary>
    internal static class UpdateCheckHelper
    {
        /// <summary>
        /// 检查并提示更新：版本号无效、不高于当前版本、已开启"不再提示更新"或
        /// 已忽略该版本（SuppressedVersion ≥ 最新版）时不提示。
        /// 确认后调用 openUpdatePage（在 UI 线程）。
        /// </summary>
        public static void CheckAndPrompt(string latestVersionString, Version productVersion, string productTitle, Action openUpdatePage)
        {
            Version latest;
            try
            {
                latest = new Version(latestVersionString);
            }
            catch
            {
                return;   // 服务器未返回/无效版本号：不提示
            }
            if (productVersion is null || latest <= productVersion)
            {
                return;
            }
            if (Properties.Settings.Default.SuppressUpdatePrompts)
            {
                return;
            }

            // 用户已忽略该版本（或更新版本）：忽略版本 ≥ 最新版时不再提示
            string suppressed = Properties.Settings.Default.SuppressedVersion;
            if (!string.IsNullOrEmpty(suppressed))
            {
                try
                {
                    if (new Version(suppressed) >= latest)
                    {
                        return;
                    }
                }
                catch
                {
                }
            }

            GrowlHelper.AskGlobal(new GrowlInfo()
            {
                Type = InfoType.Info,
                Message = $"{productTitle}有版本更新可用。\n当前版本: {productVersion}\n最新版本: {latest}\n\n是否前往更新页面？",
                ShowCloseButton = false,
                ShowDateTime = false,
                ConfirmStr = "立刻查看",
                CancelStr = "下次再说",
                ActionBeforeClose = (b) =>
                {
                    if (b)
                    {
                        openUpdatePage();
                    }
                    else
                    {
                        // 选择"下次再说"后，再询问是否忽略该版本
                        GrowlHelper.AskGlobal(new GrowlInfo()
                        {
                            Type = InfoType.Info,
                            Message = $"是否忽略版本 {latest}？\n忽略后该版本及更早版本将不再提示更新。",
                            ShowCloseButton = false,
                            ShowDateTime = false,
                            ConfirmStr = "忽略此版本",
                            CancelStr = "不用了",
                            ActionBeforeClose = (b2) =>
                            {
                                if (b2)
                                {
                                    // 用户确认忽略：记录到全局设置，之后该版本及更早版本不再提示
                                    Properties.Settings.Default.SuppressedVersion = latest.ToString();
                                    Properties.Settings.Default.Save();
                                }
                                return true;
                            }
                        });
                    }
                    return true;
                }
            });
        }
    }
}
