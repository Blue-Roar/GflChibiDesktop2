using System;

namespace GflChibiDesktop2.Helpers
{
    class UrlHelper
    {
        /// <summary>
        /// 用系统默认程序打开链接（.NET Core 需 UseShellExecute=true 才会走 Shell）。
        /// </summary>
        public static void OpenUrl(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                GrowlHelper.ErrorGlobal($"无法打开链接。\n{ex.Message}");
            }
        }
    }
}
