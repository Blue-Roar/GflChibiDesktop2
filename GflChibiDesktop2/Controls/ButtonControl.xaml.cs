using System.Windows;
using System.Windows.Controls;

namespace GflChibiDesktop2
{
    /// <summary>
    /// SingleLineTextControl.xaml 的交互逻辑
    /// </summary>
    public partial class ButtonControl : UserControl
    {
        private readonly int index;
        private readonly ManagedIpc ipc;

        public string PromptText { get; set; } = "是否让老猫干苦力";
        public string HintText { get; set; } = "老猫有多努力谁又知道呢？";
        public bool changed = false;

        public ButtonControl(int index, ManagedIpc ipc)
        {
            InitializeComponent();
            DataContext = this;
            this.index = index;
            this.ipc = ipc;
        }

        public ButtonControl()
        {
            InitializeComponent();
            DataContext = this;
            ipc = null!;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            using var w = ipc.BeginWrite();
            w.Write(2);
            w.Write(index);
        }
    }
}
