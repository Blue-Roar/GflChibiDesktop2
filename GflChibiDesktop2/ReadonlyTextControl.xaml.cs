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
    public partial class ReadonlyTextControl : UserControl
    {
        public string Text { get; private set; }

        public ReadonlyTextControl(string text)
        {
            InitializeComponent();
            DataContext = this;
            this.Text = text;
        }

        public ReadonlyTextControl(): this("哼、哼、哼，啊啊啊啊啊啊啊啊啊啊啊啊啊啊啊啊啊啊啊啊啊！")
        {
        }
    }
}
