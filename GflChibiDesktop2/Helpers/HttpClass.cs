#nullable disable
using System;
using System.Threading.Tasks;

namespace GflChibiDesktop2
{
    class HttpClass
    {
        [System.Runtime.InteropServices.DllImport("wininet")]
        private extern static bool InternetGetConnectedState(out int connectionDescription, int reservedValue);

        //判断网络是连接到互联网
        public static bool IsNetWorkConnect()
        {
            int i = 0;
            return InternetGetConnectedState(out i, 0) ? true : false;
        }


        //转换 BYTE为 MB 格式
        private static string BytesToString(decimal Bytes)
        {
            decimal Kb = System.Math.Round(Bytes / 1024);
            if (Kb > 1000)
                return string.Format("{0:0.0} MB", Kb / 1024);
            else
                return string.Format("{0:0} KB", Kb);
        }

        //下载网络文件
        /// <summary>
        /// 下载网络文件 带进度条
        /// </summary>
        /// <param name="URL"></param>
        /// <param name="fileName"></param>
        /// <param name="progressBar1"></param>
        /// <returns></returns>
        public static bool DownloadFile(string URL, string fileName, System.Windows.Controls.ProgressBar progressBar)
        {
            try
            {
                System.Net.HttpWebRequest httpWebRequest1 = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(URL);
                System.Net.HttpWebResponse httpWebResponse1 = (System.Net.HttpWebResponse)httpWebRequest1.GetResponse();

                long totalLength = httpWebResponse1.ContentLength;
                progressBar.Maximum = (int)totalLength;

                System.IO.Stream stream1 = httpWebResponse1.GetResponseStream();
                System.IO.Stream stream2 = new System.IO.FileStream(fileName, System.IO.FileMode.Create);

                long currentLength = 0;
                byte[] by = new byte[1024];
                int osize = stream1.Read(by, 0, (int)by.Length);
                while (osize > 0)
                {
                    DispatcherHelper.DoEvents();

                    currentLength = osize + currentLength;
                    stream2.Write(by, 0, osize);

                    progressBar.Value = (int)currentLength;
                    osize = stream1.Read(by, 0, (int)by.Length);
                }

                stream2.Close();
                stream1.Close();

                return (currentLength == totalLength);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 下载网络文件 带进度条 显示当前值和 最大值 100KB / 50mb
        /// </summary>
        /// <param name="URL"></param>
        /// <param name="fileName"></param>
        /// <param name="progressBar1"></param>
        /// <param name="label1"></param>
        /// <returns></returns>
        public static bool DownloadFile(string URL, string fileName, System.Windows.Controls.ProgressBar progressBar, System.Windows.Controls.Label label)
        {
            try
            {
                System.Net.HttpWebRequest httpWebRequest1 = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(URL);
                System.Net.HttpWebResponse httpWebResponse1 = (System.Net.HttpWebResponse)httpWebRequest1.GetResponse();

                long totalLength = httpWebResponse1.ContentLength;

                progressBar.Maximum = (int)totalLength;

                System.IO.Stream stream1 = httpWebResponse1.GetResponseStream();
                System.IO.Stream stream2 = new System.IO.FileStream(fileName, System.IO.FileMode.Create);

                long currentLength = 0;
                byte[] by = new byte[1024];
                int osize = stream1.Read(by, 0, (int)by.Length);
                while (osize > 0)
                {
                    DispatcherHelper.DoEvents();

                    currentLength = osize + currentLength;
                    stream2.Write(by, 0, osize);


                    progressBar.Value = (int)currentLength;
                    label.Content = String.Format("{0} / {1}", BytesToString(currentLength), BytesToString(totalLength));

                    osize = stream1.Read(by, 0, (int)by.Length);
                }

                stream2.Close();
                stream1.Close();

                return (currentLength == totalLength);
            }
            catch
            {
                HandyControl.Controls.Growl.WarningGlobal($"无法下载 {URL}，请检查下载源设置。");
                return false;
            }
        }

        /// <summary>
        /// 异步下载网络文件（不阻塞 UI，进度通过回调更新）。
        /// </summary>
        /// <param name="URL">下载地址</param>
        /// <param name="fileName">保存路径</param>
        /// <param name="onProgress">进度回调 (已下载字节, 总字节)，在 UI 线程执行</param>
        /// <returns>是否成功</returns>
        public static async Task<bool> DownloadFileAsync(string URL, string fileName, Action<long, long>? onProgress = null)
        {
            try
            {
                System.Net.HttpWebRequest httpWebRequest = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(URL);
                using (System.Net.HttpWebResponse httpWebResponse = (System.Net.HttpWebResponse)await httpWebRequest.GetResponseAsync())
                {
                    long totalLength = httpWebResponse.ContentLength;
                    using (System.IO.Stream stream1 = httpWebResponse.GetResponseStream())
                    using (System.IO.Stream stream2 = new System.IO.FileStream(fileName, System.IO.FileMode.Create))
                    {
                        long currentLength = 0;
                        byte[] by = new byte[81920];
                        int osize = await stream1.ReadAsync(by, 0, by.Length);
                        while (osize > 0)
                        {
                            currentLength += osize;
                            await stream2.WriteAsync(by, 0, osize);
                            onProgress?.Invoke(currentLength, totalLength);
                            osize = await stream1.ReadAsync(by, 0, by.Length);
                        }
                        return currentLength == totalLength;
                    }
                }
            }
            catch
            {
                HandyControl.Controls.Growl.WarningGlobal($"无法下载 {URL}，请检查下载源设置。");
                return false;
            }
        }

        //URL 是否能连接
        /// <summary>
        /// 判断网络文件是否存在 1.5秒得到出结果 如这样的格式  http://191.168.1.105:8000/CPW/wmgjUpdate.7
        /// </summary>
        /// <param name="URL"></param>
        /// <returns></returns>
        public static bool UrlIsExists(string URL)
        {
            try
            {
                System.Net.WebRequest webRequest1 = System.Net.WebRequest.Create(URL);
                webRequest1.Timeout = 1500;
                System.Net.WebResponse webResponse1 = webRequest1.GetResponse();
                return (webResponse1 == null ? false : true);
            }
            catch
            {
                return false;
            }
        }
    }
}
