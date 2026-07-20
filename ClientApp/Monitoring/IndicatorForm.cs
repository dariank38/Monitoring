using System.Drawing;
using System.Windows.Forms;

namespace Monitoring
{
    public class IndicatorForm : Form
    {
        private readonly Screen _screen;

        public IndicatorForm(Screen screen)
        {
            _screen = screen;

            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(8, 8);
            ControlBox = false;
            FormBorderStyle = FormBorderStyle.None;
            MaximumSize = new Size(8, 8);
            MinimumSize = new Size(8, 8);
            Opacity = 1D;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "Screen Monitor";
            TopMost = true;
            BackColor = Color.LimeGreen;

            Size = new Size(8, 8);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            SetBounds(_screen.Bounds.Left, _screen.Bounds.Bottom - 8, 8, 8);
        }

        private void InitializeComponent()
        {

        }
    }
}
