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
            this.Text = text;
        }

        public ReadonlyTextControl() : this("哼、哼、哼，啊啊啊啊啊啊啊啊啊啊啊啊啊啊啊啊啊啊啊啊啊！")
        {
        }
    }
}
