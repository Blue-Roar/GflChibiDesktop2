using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GflChibiDesktop
{
    class DummyListReader
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
			public string type { get; set; }
			public string display_name { get; set; }
			public string fullname { get; set; }
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
	}
}
