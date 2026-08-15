using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HDTLPanel
{
    /// <summary>
    /// SingleLineTextControl.xaml 的交互逻辑
    /// </summary>
    public partial class SingleLineTextControl : UserControl, ISaveableControl
    {
        public enum SingleLineTextType
        {
            Text,
            Integer
        }

        private readonly int index;
        private string promptText = "老猫的长度:";
        private string hintText = "老猫有多长谁又知道呢？";
        private string inputContent = "30cm";
        private SingleLineTextType type;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string PromptText { get => promptText; set { promptText=value; OnPropertyChanged(); } }
        public string HintText { get => hintText; set { hintText=value; OnPropertyChanged(); } }
        public string InputContent { get => inputContent; set { inputContent = value; OnPropertyChanged(); changed = true; } }
        public bool changed = false;

        public SingleLineTextType Type
        {
            get => type;
            set
            {
                type = value;
                OnPropertyChanged(nameof(EnableIME));
            }
        }
        public bool EnableIME => type == SingleLineTextType.Text;

        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public SingleLineTextControl(int index)
        {
            InitializeComponent();
            DataContext = this;
            this.index=index;
            changed = false;
        }

        public SingleLineTextControl()
        {
            InitializeComponent();
            DataContext = this;
            this.index=0;
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (type == SingleLineTextType.Integer && !int.TryParse(e.Text, out _))
            {
                e.Handled = true;
            }
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (type == SingleLineTextType.Integer && e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        public void Save(ManagedIpc.IpcWriter writer)
        {
            if (changed)
            {
                writer.Write(index);
                if (type == SingleLineTextType.Integer)
                {
                    writer.Write(int.Parse(InputContent));
                }
                else
                {
                    writer.Write(InputContent);
                }
                changed = false;
            }
        }
    }
}
