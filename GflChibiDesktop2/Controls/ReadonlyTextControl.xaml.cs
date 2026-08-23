using System.Windows.Controls;

namespace GflChibiDesktop2
{
    /// <summary>
    /// SingleLineTextControl.xaml 的交互逻辑
    /// </summary>
    public partial class ReadonlyTextControl : UserControl
    {
        public string Text { get; private set; }

        public ReadonlyTextControl(string text)
        {
            InitializeComponent();
            DataContext = this;
            // Label 会把 '_' 当作助记键前缀（吞字符/触发导航键），双写转义
            this.Text = text?.Replace("_", "__");
        }

        public ReadonlyTextControl() : this("哼、哼、哼，啊啊啊啊啊啊啊啊啊啊啊啊啊啊啊啊啊啊啊啊啊！")
        {
        }
    }
}
