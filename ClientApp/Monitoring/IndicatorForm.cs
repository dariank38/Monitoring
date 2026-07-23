using System.Drawing;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace Monitoring
{
    public class IndicatorForm : Form
    {
        private readonly Screen _screen;
        private readonly string _configKeyPrefix;
        private bool _dragging;
        private Point _dragOffset;

        private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "Logs", "config.json");

        public IndicatorForm(Screen screen)
        {
            _screen = screen;
            _configKeyPrefix = "indicator_" + SanitizeDeviceName(screen.DeviceName);

            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(10, 10);
            ControlBox = false;
            FormBorderStyle = FormBorderStyle.None;
            MaximumSize = new Size(10, 10);
            MinimumSize = new Size(10, 10);
            Opacity = 1D;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "Screen Monitor";
            TopMost = true;
            BackColor = Color.LimeGreen;

            Size = new Size(10, 10);

            MouseDown += IndicatorForm_MouseDown;
            MouseMove += IndicatorForm_MouseMove;
            MouseUp += IndicatorForm_MouseUp;
            FormClosed += IndicatorForm_FormClosed;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            var savedPos = LoadIndicatorPosition();
            if (savedPos.HasValue)
                SetBounds(savedPos.Value.X, savedPos.Value.Y, 10, 10);
            else
                SetBounds(_screen.Bounds.Left, _screen.Bounds.Bottom - 10, 10, 10);
        }

        private void IndicatorForm_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _dragOffset = e.Location;
            }
        }

        private void IndicatorForm_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_dragging)
            {
                var newLoc = PointToScreen(e.Location);
                newLoc.Offset(-_dragOffset.X, -_dragOffset.Y);
                Location = newLoc;
            }
        }

        private void IndicatorForm_MouseUp(object? sender, MouseEventArgs e)
        {
            if (_dragging)
            {
                _dragging = false;
                SaveIndicatorPosition(Location);
            }
        }

        private void IndicatorForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            SaveIndicatorPosition(Location);
        }

        private static string SanitizeDeviceName(string name)
        {
            var sb = new StringBuilder();
            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
            }
            return sb.ToString();
        }

        private Point? LoadIndicatorPosition()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    using var doc = JsonDocument.Parse(json);
                    var xKey = _configKeyPrefix + "_x";
                    var yKey = _configKeyPrefix + "_y";
                    if (doc.RootElement.TryGetProperty(xKey, out var xEl) &&
                        doc.RootElement.TryGetProperty(yKey, out var yEl))
                    {
                        return new Point(xEl.GetInt32(), yEl.GetInt32());
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("IndicatorForm.LoadIndicatorPosition", ex);
            }
            return null;
        }

        private void SaveIndicatorPosition(Point pos)
        {
            try
            {
                var dir = Path.GetDirectoryName(ConfigPath)!;
                Directory.CreateDirectory(dir);
                var xKey = _configKeyPrefix + "_x";
                var yKey = _configKeyPrefix + "_y";
                string json;
                if (File.Exists(ConfigPath))
                {
                    var existing = File.ReadAllText(ConfigPath);
                    using var doc = JsonDocument.Parse(existing);
                    var writer = new StringBuilder("{");
                    var first = true;
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (prop.Name == xKey || prop.Name == yKey) continue;
                        if (!first) writer.Append(',');
                        first = false;
                        writer.Append($"\"{prop.Name}\":{prop.Value.GetRawText()}");
                    }
                    if (!first) writer.Append(',');
                    writer.Append($"\"{xKey}\":{pos.X},\"{yKey}\":{pos.Y}}}");
                    json = writer.ToString();
                }
                else
                {
                    json = $"{{\"{xKey}\":{pos.X},\"{yKey}\":{pos.Y}}}";
                }
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                Logger.Log("IndicatorForm.SaveIndicatorPosition", ex);
            }
        }

        private void InitializeComponent()
        {

        }
    }
}
