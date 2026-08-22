using System.ComponentModel;
using System.Windows.Controls;

namespace GflChibiDesktop2
{
    /// <summary>
    /// NumericControl.xaml 的交互逻辑
    /// </summary>
    public partial class NumericControl : UserControl, ISaveableControl
    {
        private readonly int index;
        private string promptText = "老猫的长度:";
        private string hintText = "老猫有多长谁又知道呢？";
        private double? value = 30;
        private double? minValue = int.MinValue;
        private double? maxValue = int.MaxValue;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string PromptText { get => promptText; set { promptText = value; OnPropertyChanged(); } }
        public string HintText { get => hintText; set { hintText = value; OnPropertyChanged(); } }
        public double? Value
        {
            get => value;
            set
            {
                // 仅在值实际变化时触发，避免绑定目标（NumericUpDown）加载时回写相同值造成“伪更改”
                if (this.value != value)
                {
                    this.value = value;
                    OnPropertyChanged();
                    changed = true;
                }
            }
        }
        public double? MinValue { get => minValue; set { minValue = value; OnPropertyChanged(); } }
        public double? MaxValue { get => maxValue; set { maxValue = value; OnPropertyChanged(); } }
        public bool changed = false;

        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public NumericControl(int index)
        {
            InitializeComponent();
            DataContext = this;
            this.index = index;
            changed = false;
        }

        public NumericControl()
        {
            InitializeComponent();
            DataContext = this;
            this.index = 0;
        }

        public void Save(ManagedIpc.IpcWriter writer)
        {
            if (changed)
            {
                writer.Write(index);
                writer.Write((int)(Value ?? 0));
                changed = false;
            }
        }
    }
}
