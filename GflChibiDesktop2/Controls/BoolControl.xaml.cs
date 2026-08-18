using System.ComponentModel;
using System.Windows.Controls;

namespace GflChibiDesktop2
{
    /// <summary>
    /// SingleLineTextControl.xaml 的交互逻辑
    /// </summary>
    public partial class BoolControl : UserControl, ISaveableControl
    {
        private readonly int index;
        private string promptText = "是否让老猫干苦力";
        private string hintText = "老猫有多努力谁又知道呢？";
        private bool choice = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string PromptText { get => promptText; set { promptText = value; OnPropertyChanged(); } }
        public string HintText { get => hintText; set { hintText = value; OnPropertyChanged(); } }
        public bool Choice { get => choice; set { choice = value; OnPropertyChanged(); changed = true; } }

        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public bool changed = false;

        public BoolControl(int index)
        {
            InitializeComponent();
            DataContext = this;
            this.index = index;
            this.changed = false;
        }

        public BoolControl()
        {
            InitializeComponent();
            DataContext = this;
        }

        public void Save(ManagedIpc.IpcWriter writer)
        {
            if (changed)
            {
                writer.Write(index);
                writer.Write(choice ? 1 : 0);
                changed = false;
            }
        }
    }
}
