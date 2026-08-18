using System.Windows;

namespace GflChibiDesktop2
{
    /// <summary>
    /// PosCGDialog.xaml 的交互逻辑
    /// </summary>
    public partial class PosCGDialog
    {
        private CGWindow _cg;
        public PosCGDialog(CGWindow windowCG)
        {
            InitializeComponent();
            _cg = windowCG;

            nud_width.Minimum = 1;
            nud_width.Maximum = SystemParameters.PrimaryScreenWidth;
            nud_height.Minimum = 1;
            nud_height.Maximum = SystemParameters.PrimaryScreenHeight;
            nud_width.Value = _cg.vb_img.Width;
            nud_height.Value = _cg.vb_img.Height;
            //_cg.Cursor = System.Windows.Input.Cursors.Arrow;
            nud_posx.Minimum = -SystemParameters.PrimaryScreenWidth;
            nud_posx.Maximum = SystemParameters.PrimaryScreenWidth;
            nud_posy.Minimum = -SystemParameters.PrimaryScreenHeight;
            nud_posy.Maximum = SystemParameters.PrimaryScreenHeight;
            nud_posx.Value = _cg.Left;
            nud_posy.Value = _cg.Top;
            chb_fixPos.IsChecked = _cg.menuItem_fixed.IsChecked;
        }

        private void btn_OK_Click(object sender, RoutedEventArgs e)
        {
            double dpi = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11;
            _cg.vb_img.Width = nud_width.Value;
            _cg.vb_img.Height = nud_height.Value;
            _cg.Width = nud_width.Value / dpi;
            _cg.Height = nud_height.Value / dpi;
            _cg.Left = nud_posx.Value;
            _cg.Top = nud_posy.Value;
            if (chb_fixPos.IsChecked == true)
            {
                _cg.menuItem_fixed.IsChecked = true;
            }
            else
            {
                _cg.menuItem_fixed.IsChecked = false;
            }
        }

        private void UserControl_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            e.Handled = true;
        }

        //btn_Close.Command.Execute(null);
    }
}
