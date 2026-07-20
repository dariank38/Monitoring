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
            ClientSize = new Size(16, 16);
            ControlBox = false;
            FormBorderStyle = FormBorderStyle.None;
            MaximumSize = new Size(16, 16);
            MinimumSize = new Size(16, 16);
            Opacity = 1D;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "Screen Monitor";
            TopMost = true;
            BackColor = Color.LimeGreen;

            Size = new Size(16, 16);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            SetBounds(_screen.Bounds.Left, _screen.Bounds.Bottom - 16, 16, 16);
        }

        private void InitializeComponent()
        {

        }
    }
}
