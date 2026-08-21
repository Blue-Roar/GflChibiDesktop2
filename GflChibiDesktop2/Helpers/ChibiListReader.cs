#nullable disable
using System.Collections.Generic;

namespace GflChibiDesktop2
{
    class ChibiListReader
    {
        public class Meta
        {
            public string uuid { get; set; }
            public string version { get; set; }
            public string content { get; set; }
        }

        public class Content
        {
            public string name { get; set; }
            public string parent { get; set; }
            public string category { get; set; }
            public string type { get; set; }
            public string display { get; set; }
            public string display_full { get; set; }
            public string path { get; set; }
            public string filename { get; set; }
            public string filename_r { get; set; }
            public string files { get; set; }
            public string cg { get; set; }
            public string cg_d { get; set; }
        }

        public class RootObject
        {
            public Meta meta { get; set; }
            public List<Content> content { get; set; }
        }

        public class ExternalMeta
        {
            public string uuid { get; set; }
            public string version { get; set; }
            public string content { get; set; }
        }

        public class ExternalContent
        {
            public string name { get; set; }
            public string display { get; set; }
            public string path { get; set; }
            public string filename { get; set; }
            public string filename_r { get; set; }
            public string files { get; set; }
        }

        public class ExternalRoot
        {
            public ExternalMeta meta { get; set; }
            public List<ExternalContent> content { get; set; }
        }
    }
}
