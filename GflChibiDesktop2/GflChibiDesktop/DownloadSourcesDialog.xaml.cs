#nullable disable
using Newtonsoft.Json;
using System.Text;
using System.Windows.Controls;
using static GflChibiDesktop.WebAPI;

namespace GflChibiDesktop
{
    /// <summary>
    /// DownloadSourcesDialog.xaml 的交互逻辑
    /// </summary>
    public partial class DownloadSourcesDialog
    {
        public DownloadSourcesDialog()
        {
            InitializeComponent();
            tb_downloadSource.Text = Properties.Settings.Default.DownloadSource;
            btn_UpdateSources_Click(null, null);
        }

        private void canvasBackgroundColorPicker_Canceled(object sender, System.EventArgs e)
        {
            btn_Cancel.Command.Execute(null);
        }

        private void btn_OK_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Properties.Settings.Default.DownloadSource = tb_downloadSource.Text;
            Properties.Settings.Default.Save();
            btn_Cancel.Command.Execute(null);
        }

        private void btn_UpdateSources_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            bool sourcesPost = false;
            string sourcesStr = HttpRequestHelper.PostWebRequest("https://api.brightsu.cn/GflChibiDesktop2/sources", string.Empty, Encoding.UTF8, ref sourcesPost);
            if (!sourcesPost)
            {
                HandyControl.Controls.Growl.ErrorGlobal($"获取下载源列表失败。API 接口调用失败。\n错误：{sourcesStr}");
                return;
            }
            SourcesRoot rt = JsonConvert.DeserializeObject<SourcesRoot>(sourcesStr);
            if (rt.ret != 200)
            {
                HandyControl.Controls.Growl.ErrorGlobal($"获取下载源列表失败。API 接口调用失败。\n错误：API 接口返回了状态码 {rt.ret}");
                return;
            }

            lb_sources.Items.Clear();
            foreach (SourcesItem item in rt.data.sources)
            {
                ListBoxItem listBoxItem = new ListBoxItem();
                listBoxItem.Name = $"lbiSource{item.id}";
                listBoxItem.Content = $"[{item.id}] {item.name}";
                listBoxItem.IsEnabled = System.Convert.ToBoolean(item.enabled);
                listBoxItem.ToolTip = item.desc;

                string[] tagString = new string[5];
                tagString[0] = item.id;
                tagString[1] = item.name;
                tagString[2] = item.desc;
                tagString[3] = item.url;
                tagString[4] = item.enabled;
                listBoxItem.Tag = tagString;

                lb_sources.Items.Add(listBoxItem);
            }
        }

        private void lb_sources_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lb_sources.SelectedItem != null)
            {
                ListBoxItem item = (ListBoxItem)lb_sources.SelectedItem;
                string[] tagString = (string[])item.Tag;
                tb_downloadSource.Text = tagString[3];
                tb_sourceInfo.Text = $"#{tagString[0]} - {tagString[1]}\n{tagString[2]}\n{tagString[3]}";
            }
        }
    }
}
