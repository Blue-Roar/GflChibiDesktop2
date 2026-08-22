using HandyControl.Data;

namespace GflChibiDesktop2
{
    /// <summary>
    /// 全局提示封装：开启“禁用全部全局提示”后抑制所有全局提示。
    /// </summary>
    public static class GrowlHelper
    {
        private static bool Suppressed => Properties.Settings.Default.SuppressGlobalGrowl;

        public static void InfoGlobal(string message)
        {
            if (!Suppressed) HandyControl.Controls.Growl.InfoGlobal(message);
        }

        public static void InfoGlobal(GrowlInfo info)
        {
            if (!Suppressed) HandyControl.Controls.Growl.InfoGlobal(info);
        }

        public static void WarningGlobal(string message)
        {
            if (!Suppressed) HandyControl.Controls.Growl.WarningGlobal(message);
        }

        public static void WarningGlobal(GrowlInfo info)
        {
            if (!Suppressed) HandyControl.Controls.Growl.WarningGlobal(info);
        }

        public static void ErrorGlobal(string message)
        {
            if (!Suppressed) HandyControl.Controls.Growl.ErrorGlobal(message);
        }

        public static void SuccessGlobal(string message)
        {
            if (!Suppressed) HandyControl.Controls.Growl.SuccessGlobal(message);
        }

        public static void AskGlobal(GrowlInfo info)
        {
            if (!Suppressed) HandyControl.Controls.Growl.AskGlobal(info);
        }
    }
}
