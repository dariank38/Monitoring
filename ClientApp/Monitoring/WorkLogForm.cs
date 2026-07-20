using System.Drawing;
using System.Windows.Forms;

namespace Monitoring
{
    public class WorkLogForm : Form
    {
        private HeatmapControl _heatmap;
        private DateTimePicker _datePicker;
        private Label _lblTotal;

        public WorkLogForm(ActivityTracker tracker)
        {
            Text = "Work Time Logs";
            ClientSize = new Size(620, 500);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            BackColor = Color.FromArgb(30, 30, 30);

            BuildUi();
            LoadLogData(DateTime.Today);
        }

        private void BuildUi()
        {
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(38, 38, 38),
                Padding = new Padding(12, 8, 12, 8)
            };

            var lblDateTitle = new Label
            {
                Text = "Date:",
                Location = new Point(0, 4),
                AutoSize = true,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 10F)
            };

            _datePicker = new DateTimePicker
            {
                Location = new Point(40, 2),
                Size = new Size(150, 24),
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today,
                Checked = true
            };
            _datePicker.ValueChanged += (_, _) => LoadLogData(_datePicker.Checked ? _datePicker.Value : null);

            _lblTotal = new Label
            {
                Location = new Point(210, 4),
                AutoSize = true,
                ForeColor = Color.LimeGreen,
                Font = new Font("Segoe UI", 10F)
            };

            topPanel.Controls.AddRange(lblDateTitle, _datePicker, _lblTotal);

            _heatmap = new HeatmapControl
            {
                Dock = DockStyle.Fill
            };

            Controls.Add(_heatmap);
            Controls.Add(topPanel);
        }

        private void LoadLogData(DateTime? filterDate)
        {
            var entries = ActivityTracker.LoadLogEntries();

            if (filterDate.HasValue)
            {
                var dateStr = filterDate.Value.ToString("yyyy-MM-dd");
                entries = entries.Where(e => e.Date == dateStr).ToList();
            }

            _heatmap.SetData(entries);

            if (filterDate.HasValue)
            {
                var totalSec = entries.Where(e => e.Status == "Active").Sum(e => e.DurationSec);
                _lblTotal.Text = $"Total: {FormatDuration(totalSec)}";
            }
            else
            {
                _lblTotal.Text = "";
            }
        }

        private static string FormatDuration(int seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";
        }
    }
}
