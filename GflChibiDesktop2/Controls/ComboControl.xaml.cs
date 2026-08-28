using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Controls;

namespace GflChibiDesktop2
{
    /// <summary>
    /// 下拉选择面板控件（动画/皮肤等枚举项）。
    /// </summary>
    public partial class ComboControl : UserControl, ISaveableControl
    {
        private readonly int index;
        private string promptText = "选择";
        private string hintText = "";
        private List<string> items = new();
        private int selectedIndex = -1;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string PromptText { get => promptText; set { promptText = value; OnPropertyChanged(); } }
        public string HintText { get => hintText; set { hintText = value; OnPropertyChanged(); } }
        public List<string> Items { get => items; set { items = value ?? new(); OnPropertyChanged(); } }
        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                // 仅在值实际变化时触发，避免 ComboBox 加载时回写相同值造成“伪更改”
                if (selectedIndex != value)
                {
                    selectedIndex = value;
                    OnPropertyChanged();
                    changed = true;
                }
            }
        }
        public bool changed = false;

        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public ComboControl(int index)
        {
            InitializeComponent();
            DataContext = this;
            this.index = index;
            changed = false;
        }

        public ComboControl()
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
                writer.Write(selectedIndex);
                changed = false;
            }
        }
    }
}
