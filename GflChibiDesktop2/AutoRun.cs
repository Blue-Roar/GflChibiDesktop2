using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HDTLPanel
{
    class AutoRun
    {
        public static void SetAutoRun(string strAppPath, string strAppName, bool bIsAutoRun)
        {
            using var run = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run\");

            if (bIsAutoRun)
            {
                run.SetValue(strAppName, strAppPath);
            }
            else
            {
                if (null != run.GetValue(strAppName))
                {
                    run.DeleteValue(strAppName);
                }
            }
        }

        /// <summary>
        /// 判断是否开机启动
        /// </summary>
        /// <param name="strAppPath">应用程序路径</param>
        /// <param name="strAppName">应用程序名称</param>
        /// <returns></returns>
        public static bool IsAutoRun(string strAppPath, string strAppName)
        {
            using var run = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run\") ?? throw new Exception();
            object? key = run.GetValue(strAppName);
            return strAppPath.Equals(key);
        }
    }
}