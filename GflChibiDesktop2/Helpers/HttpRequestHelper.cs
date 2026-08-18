#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GflChibiDesktop2
{
    public class HttpRequestHelper
    {
        static string userAgent = $"GflChibiDesktop2 v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}";

        public static string GetWebRequest(string getUrl, Encoding dataEncode)
        {
            string ret = string.Empty;
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(getUrl);
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            Stream stream = resp.GetResponseStream();
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                //获取内容
                using (StreamReader reader = new StreamReader(stream))
                {
                    ret = reader.ReadToEnd();
                }
            }
            finally
            {
                stream.Close();
            }
            return ret;
        }
        public static string PostWebRequest(string postUrl, string paramData, Encoding dataEncode)
        {
            string ret = string.Empty;
            try
            {
                byte[] byteArray = dataEncode.GetBytes(paramData); //转化
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(new Uri(postUrl));
                webReq.Method = "POST";
                webReq.ContentType = "application/x-www-form-urlencoded";
                webReq.UserAgent = userAgent;
                webReq.Timeout = 5000;

                webReq.ContentLength = byteArray.Length;
                Stream newStream = webReq.GetRequestStream();
                newStream.Write(byteArray, 0, byteArray.Length);//写入参数
                newStream.Close();
                HttpWebResponse response = (HttpWebResponse)webReq.GetResponse();

                StreamReader sr = new StreamReader(response.GetResponseStream(), dataEncode);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    int statusCode = GetHttpStatusCode(response.StatusCode);
                    return $"服务器返回了 HTTP 状态码 {statusCode}({response.StatusCode})";
                    //return $"服务器返回了 HTTP 状态码 {response.StatusCode}";
                }
                ret = sr.ReadToEnd();
                sr.Close();
                response.Close();
                newStream.Close();

                return ret;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static string PostWebRequest(string postUrl, string paramData, Encoding dataEncode, ref bool isSuccess)
        {
            isSuccess = true;
            string ret = string.Empty;
            try
            {
                byte[] byteArray = dataEncode.GetBytes(paramData); //转化
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(new Uri(postUrl));
                webReq.Method = "POST";
                webReq.ContentType = "application/x-www-form-urlencoded";
                webReq.UserAgent = userAgent;
                webReq.Timeout = 5000;

                webReq.ContentLength = byteArray.Length;
                Stream newStream = webReq.GetRequestStream();
                newStream.Write(byteArray, 0, byteArray.Length);//写入参数
                newStream.Close();
                HttpWebResponse response = (HttpWebResponse)webReq.GetResponse();

                StreamReader sr = new StreamReader(response.GetResponseStream(), dataEncode);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    int statusCode = GetHttpStatusCode(response.StatusCode);
                    isSuccess = false;
                    return $"服务器返回了 HTTP 状态码 {statusCode}({response.StatusCode})";
                    //return $"服务器返回了 HTTP 状态码 {response.StatusCode}";
                }
                ret = sr.ReadToEnd();
                sr.Close();
                response.Close();
                newStream.Close();

                return ret;
            }
            catch (Exception ex)
            {
                isSuccess = false;
                return ex.Message;
            }
        }

        /// <summary>
        /// 异步 POST 请求。返回 (是否成功, 响应内容或错误信息)。
        /// </summary>
        public static async Task<(bool Success, string Result)> PostWebRequestAsync(string postUrl, string paramData, Encoding dataEncode)
        {
            try
            {
                byte[] byteArray = dataEncode.GetBytes(paramData);
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(new Uri(postUrl));
                webReq.Method = "POST";
                webReq.ContentType = "application/x-www-form-urlencoded";
                webReq.UserAgent = userAgent;
                webReq.Timeout = 5000;
                webReq.ContentLength = byteArray.Length;

                using (Stream newStream = await webReq.GetRequestStreamAsync())
                {
                    await newStream.WriteAsync(byteArray, 0, byteArray.Length);
                }

                using (HttpWebResponse response = (HttpWebResponse)await webReq.GetResponseAsync())
                using (StreamReader sr = new StreamReader(response.GetResponseStream(), dataEncode))
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        int statusCode = GetHttpStatusCode(response.StatusCode);
                        return (false, $"服务器返回了 HTTP 状态码 {statusCode}({response.StatusCode})");
                    }
                    return (true, await sr.ReadToEndAsync());
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// 异步 GET 请求。返回 (是否成功, 响应内容或错误信息)。
        /// </summary>
        public static async Task<(bool Success, string Result)> GetWebRequestAsync(string getUrl, Encoding dataEncode)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(getUrl);
                req.UserAgent = userAgent;
                req.Timeout = 5000;

                using (HttpWebResponse resp = (HttpWebResponse)await req.GetResponseAsync())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream(), dataEncode))
                {
                    return (true, await reader.ReadToEndAsync());
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static int GetHttpStatusCode(HttpStatusCode httpStatusCode)
        {
            switch (httpStatusCode)
            {
                case HttpStatusCode.Continue:
                    return 100;
                case HttpStatusCode.SwitchingProtocols:
                    return 101;
                case HttpStatusCode.OK:
                    return 200;
                case HttpStatusCode.Created:
                    return 201;
                case HttpStatusCode.Accepted:
                    return 202;
                case HttpStatusCode.NonAuthoritativeInformation:
                    return 203;
                case HttpStatusCode.NoContent:
                    return 204;
                case HttpStatusCode.ResetContent:
                    return 205;
                case HttpStatusCode.PartialContent:
                    return 206;
                case HttpStatusCode.MultipleChoices:
                    //case HttpStatusCode.Ambiguous:
                    return 300;
                case HttpStatusCode.MovedPermanently:
                    //case HttpStatusCode.Moved:
                    return 301;
                case HttpStatusCode.Found:
                    //case HttpStatusCode.Redirect:
                    return 302;
                case HttpStatusCode.SeeOther:
                    //case HttpStatusCode.RedirectMethod:
                    return 303;
                case HttpStatusCode.NotModified:
                    return 304;
                case HttpStatusCode.UseProxy:
                    return 305;
                case HttpStatusCode.Unused:
                    return 306;
                case HttpStatusCode.TemporaryRedirect:
                    //case HttpStatusCode.RedirectKeepVerb:
                    return 307;
                case HttpStatusCode.BadRequest:
                    return 400;
                case HttpStatusCode.Unauthorized:
                    return 401;
                case HttpStatusCode.PaymentRequired:
                    return 402;
                case HttpStatusCode.Forbidden:
                    return 403;
                case HttpStatusCode.NotFound:
                    return 404;
                case HttpStatusCode.MethodNotAllowed:
                    return 405;
                case HttpStatusCode.NotAcceptable:
                    return 406;
                case HttpStatusCode.ProxyAuthenticationRequired:
                    return 407;
                case HttpStatusCode.RequestTimeout:
                    return 408;
                case HttpStatusCode.Conflict:
                    return 409;
                case HttpStatusCode.Gone:
                    return 410;
                case HttpStatusCode.LengthRequired:
                    return 411;
                case HttpStatusCode.PreconditionFailed:
                    return 412;
                case HttpStatusCode.RequestEntityTooLarge:
                    return 413;
                case HttpStatusCode.RequestUriTooLong:
                    return 414;
                case HttpStatusCode.UnsupportedMediaType:
                    return 415;
                case HttpStatusCode.RequestedRangeNotSatisfiable:
                    return 416;
                case HttpStatusCode.ExpectationFailed:
                    return 417;
                case HttpStatusCode.UpgradeRequired:
                    return 426;
                case HttpStatusCode.InternalServerError:
                    return 500;
                case HttpStatusCode.NotImplemented:
                    return 501;
                case HttpStatusCode.BadGateway:
                    return 502;
                case HttpStatusCode.ServiceUnavailable:
                    return 503;
                case HttpStatusCode.GatewayTimeout:
                    return 504;
                case HttpStatusCode.HttpVersionNotSupported:
                    return 505;
                default:
                    return 0;
            }
        }
        public static string HttpPost(string Url, string postDataStr, ref bool isSuccess)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.ContentLength = Encoding.UTF8.GetByteCount(postDataStr);
                //request.CookieContainer = cookie;
                Stream myRequestStream = request.GetRequestStream();
                StreamWriter myStreamWriter = new StreamWriter(myRequestStream, Encoding.GetEncoding("utf-8"));
                //StreamWriter myStreamWriter = new StreamWriter(myRequestStream, Encoding.GetEncoding("gb2312"));
                myStreamWriter.Write(postDataStr);
                myStreamWriter.Close();

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                //response.Cookies = cookie.GetCookies(response.ResponseUri);
                Stream myResponseStream = response.GetResponseStream();
                StreamReader myStreamReader = new StreamReader(myResponseStream, Encoding.GetEncoding("utf-8"));
                string retString = myStreamReader.ReadToEnd();
                myStreamReader.Close();
                myResponseStream.Close();

                return retString;
            }
            catch (Exception e)
            {
                isSuccess = false;
                return e.Message;
            }
        }

        public string HttpGet(string Url, string postDataStr)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url + (postDataStr == string.Empty ? string.Empty : "?") + postDataStr);
            request.Method = "GET";
            request.ContentType = "application/json";

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            Stream myResponseStream = response.GetResponseStream();
            StreamReader myStreamReader = new StreamReader(myResponseStream, Encoding.GetEncoding("utf-8"));
            string retString = myStreamReader.ReadToEnd();
            myStreamReader.Close();
            myResponseStream.Close();

            return retString;
        }

        /// <summary> 
        /// 创建GET方式的HTTP请求 
        /// </summary> 
        //public static HttpWebResponse CreateGetHttpResponse(string url, int timeout, string userAgent, CookieCollection cookies)
        public static HttpWebResponse CreateGetHttpResponse(string url)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest request = null;
            if (url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
            {
                //对服务端证书进行有效性校验（非第三方权威机构颁发的证书，如自己生成的，不进行验证，这里返回true）
                ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(CheckValidationResult);
                request = WebRequest.Create(url) as HttpWebRequest;
                request.ProtocolVersion = HttpVersion.Version11;    //http版本，默认是1.1,这里设置为1.0
            }
            else
            {
                request = WebRequest.Create(url) as HttpWebRequest;
            }
            request.Method = "GET";

            //设置代理UserAgent和超时
            request.UserAgent = userAgent;
            //request.Timeout = timeout;
            //if (cookies is not null)
            //{
            //    request.CookieContainer = new CookieContainer();
            //    request.CookieContainer.Add(cookies);
            //}
            return request.GetResponse() as HttpWebResponse;
        }

        /// <summary> 
        /// 创建POST方式的HTTP请求 
        /// </summary> 
        //public static HttpWebResponse CreatePostHttpResponse(string url, IDictionary<string, string> parameters, int timeout, string userAgent, CookieCollection cookies)
        public static HttpWebResponse CreatePostHttpResponse(string url, IDictionary<string, string> parameters)
        {
            HttpWebRequest request = null;
            //如果是发送HTTPS请求 
            if (url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                //ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(CheckValidationResult);
                request = WebRequest.Create(url) as HttpWebRequest;
                //request.ProtocolVersion = HttpVersion.Version10;
            }
            else
            {
                request = WebRequest.Create(url) as HttpWebRequest;
            }
            request.Method = "POST";
            request.ContentType = "application/json";

            //设置代理UserAgent和超时
            request.UserAgent = userAgent;
            //request.Timeout = timeout;

            //if (cookies is not null)
            //{
            //    request.CookieContainer = new CookieContainer();
            //    request.CookieContainer.Add(cookies);
            //}
            //发送POST数据 
            if (!(parameters is null || parameters.Count == 0))
            {
                StringBuilder buffer = new StringBuilder();
                int i = 0;
                foreach (string key in parameters.Keys)
                {
                    if (i > 0)
                    {
                        buffer.AppendFormat("&{0}={1}", key, parameters[key]);
                    }
                    else
                    {
                        buffer.AppendFormat("{0}={1}", key, parameters[key]);
                        i++;
                    }
                }
                byte[] data = Encoding.ASCII.GetBytes(buffer.ToString());
                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
            }
            string[] values = request.Headers.GetValues("Content-Type");
            return request.GetResponse() as HttpWebResponse;
        }

        /// <summary>
        /// 获取请求的数据
        /// </summary>
        public static string GetResponseString(HttpWebResponse webresponse)
        {
            using (Stream s = webresponse.GetResponseStream())
            {
                StreamReader reader = new StreamReader(s, Encoding.UTF8);
                return reader.ReadToEnd();

            }
        }

        /// <summary>
        /// 验证证书
        /// </summary>
        private static bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            if (errors == SslPolicyErrors.None)
                return true;
            return false;
        }

        /// <summary>
        /// 检测串值是否为合法的网址格式
        /// </summary>
        /// <param name="strValue">要检测的String值</param>
        /// <returns>成功返回true 失败返回false</returns>
        public static bool CheckIsUrlFormat(string strValue)
        {
            return CheckIsFormat(@"(http://)?([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?", strValue);
        }

        /// <summary>
        /// 检测串值是否为合法的格式
        /// </summary>
        /// <param name="strRegex">正则表达式</param>
        /// <param name="strValue">要检测的String值</param>
        /// <returns>成功返回true 失败返回false</returns>
        public static bool CheckIsFormat(string strRegex, string strValue)
        {
            if (!string.IsNullOrEmpty(strValue) && strValue.Trim() != string.Empty)
            {
                Regex re = new Regex(strRegex);
                if (re.IsMatch(strValue))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return false;
        }

    }
}