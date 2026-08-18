#nullable disable
using System.Collections.Generic;

namespace GflChibiDesktop2
{
    class WebAPI
    {
        public class IndexMsgData
        {
            public string announcement { get; set; }
        }
        public class IndexData
        {
            public string homepage_link { get; set; }
            public string update_link { get; set; }
            public string donate_link { get; set; }
            public string chibi_list_link { get; set; }
            public string latest { get; set; }
            public IndexMsgData msg { get; set; }
        }

        public class IndexRoot
        {
            /// <summary>
            /// 
            /// </summary>
            public int ret { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public IndexData data { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public string msg { get; set; }
        }


        public class StartupRoot
        {
            public int ret { get; set; }
            public StartupData data { get; set; }
            public string msg { get; set; }
        }

        public class StartupData
        {
            public string homepage_link { get; set; }
            public string update_link { get; set; }
            public string donate_link { get; set; }
            public string chibi_list_link { get; set; }
            public int time { get; set; }
            public string count { get; set; }
            public string latest { get; set; }
            public string msg { get; set; }
        }

        public class UpdateData
        {
            public string version { get; set; }
            public string build { get; set; }
            public string content { get; set; }
            public int urgent { get; set; }
        }

        public class UpdateRoot
        {
            /// <summary>
            /// 
            /// </summary>
            public int ret { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public UpdateData data { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public string msg { get; set; }
        }

        public class DummyListData
        {
            //public string dummy_list_version { get; set; }
            //public string dummy_list_version_log { get; set; }
            //public string dummy_list_link { get; set; }
            public string uuid { get; set; }
            public string url { get; set; }
        }

        public class DummyListRoot
        {
            /// <summary>
            /// 
            /// </summary>
            public int ret { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public DummyListData data { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public string msg { get; set; }
        }

        public class SourcesItem
        {
            /// <summary>
            /// 下载源ID
            /// </summary>
            public string id { get; set; }
            /// <summary>
            /// 下载源名称
            /// </summary>
            public string name { get; set; }
            /// <summary>
            /// 下载源描述
            /// </summary>
            public string desc { get; set; }
            /// <summary>
            /// 下载源 URL
            /// </summary>
            public string url { get; set; }
            /// <summary>
            /// 下载源启用
            /// </summary>
            public string enabled { get; set; }
        }

        public class SourcesData
        {
            /// <summary>
            /// 
            /// </summary>
            public List<SourcesItem> sources { get; set; }
        }

        public class SourcesRoot
        {
            /// <summary>
            /// 
            /// </summary>
            public int ret { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public SourcesData data { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public string msg { get; set; }
        }

        public class BiliSpaceInfoData
        {
            /// <summary>
            /// 名称
            /// </summary>
            public string name { get; set; }
            /// <summary>
            /// avatar
            /// </summary>
            public string face { get; set; }
        }

        public class BiliSpaceInfoRoot
        {
            /// <summary>
            /// 
            /// </summary>
            public int code { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public string message { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public int ttl { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public BiliSpaceInfoData data { get; set; }
        }

    }
}
