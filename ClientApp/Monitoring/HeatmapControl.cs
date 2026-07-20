using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Monitoring
{
    public class HeatmapControl : Panel
    {
        private class HeatCell
        {
            public string Date = "";
            public int Hour;
            public int ActiveSec;
            public int KeyCount;
            public int MouseCount;
        }

        private List<string> _dates = new();
        private Dictionary<(string date, int hour), HeatCell> _cells = new();
        private int _maxActiveSec = 1;

        private const int CellSize = 18;
        private const int CellGap = 3;
        private const int LabelLeft = 50;
        private const int LabelTop = 20;
        private const int LegendHeight = 20;

        private string? _hoverDate;
        private int _hoverHour = -1;

        public HeatmapControl()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(30, 30, 30);
            Font = new Font("Segoe UI", 8F);
        }

        public void SetData(IEnumerable<ActivityLogEntry> entries)
        {
            _dates.Clear();
            _cells.Clear();
            _maxActiveSec = 1;

            var hourBuckets = new Dictionary<(string date, int hour), HeatCell>();

            foreach (var e in entries)
            {
                if (e.Status != "Active")
                    continue;

                if (!DateTime.TryParse($"{e.Date} {e.Start}", out var start))
                    continue;
                if (!DateTime.TryParse($"{e.Date} {e.End}", out var end))
                    continue;

                var current = start;
                while (current < end)
                {
                    var hourEnd = new DateTime(current.Year, current.Month, current.Day, current.Hour, 0, 0).AddHours(1);
                    var segEnd = end < hourEnd ? end : hourEnd;
                    var segSec = (int)(segEnd - current).TotalSeconds;
                    var hour = current.Hour;
                    var key = (e.Date, hour);

                    if (!hourBuckets.TryGetValue(key, out var cell))
                    {
                        cell = new HeatCell { Date = e.Date, Hour = hour };
                        hourBuckets[key] = cell;
                    }

                    cell.ActiveSec += segSec;
                    cell.KeyCount += e.KeyCount;
                    cell.MouseCount += e.MouseCount;

                    if (cell.ActiveSec > _maxActiveSec)
                        _maxActiveSec = cell.ActiveSec;

                    current = segEnd;
                }
            }

            _cells = hourBuckets;
            _dates = hourBuckets.Keys.Select(k => k.date).Distinct().OrderBy(d => d).ToList();

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (_dates.Count == 0)
            {
                using var nb = new SolidBrush(Color.FromArgb(60, 60, 60));
                g.DrawString("No activity data yet", Font, nb, 10, 10);
                return;
            }

            var textBrush = new SolidBrush(Color.FromArgb(140, 140, 140));
            var headerFont = new Font("Segoe UI", 7F);

            for (var h = 0; h < 24; h++)
            {
                var x = LabelLeft + h * (CellSize + CellGap);
                if (h % 3 == 0)
                    g.DrawString($"{h:00}", headerFont, textBrush, x + 2, 2);
            }

            for (var di = 0; di < _dates.Count; di++)
            {
                var date = _dates[di];
                var y = LabelTop + di * (CellSize + CellGap);

                if (di % 2 == 0 || di == _dates.Count - 1)
                {
                    var dt = DateTime.TryParse(date, out var parsed) ? parsed.ToString("MM/dd") : date;
                    g.DrawString(dt, headerFont, textBrush, 2, y + 3);
                }

                for (var h = 0; h < 24; h++)
                {
                    var x = LabelLeft + h * (CellSize + CellGap);
                    var rect = new Rectangle(x, y, CellSize, CellSize);

                    Color color;
                    if (_cells.TryGetValue((date, h), out var cell))
                        color = GetHeatColor(cell.ActiveSec);
                    else
                        color = Color.FromArgb(40, 40, 40);

                    using var b = new SolidBrush(color);
                    g.FillRoundedRect(b, rect, 3);

                    if (_hoverDate == date && _hoverHour == h)
                    {
                        using var p = new Pen(Color.White, 1.5f);
                        g.DrawRoundedRect(p, rect, 3);
                    }
                }
            }

            var legendY = LabelTop + _dates.Count * (CellSize + CellGap) + 8;
            g.DrawString("Less", headerFont, textBrush, LabelLeft, legendY + 2);
            for (var i = 0; i < 5; i++)
            {
                var lx = LabelLeft + 35 + i * (CellSize + 2);
                var rect = new Rectangle(lx, legendY, 12, 12);
                using var b = new SolidBrush(GetHeatColorByLevel(i, 4));
                g.FillRoundedRect(b, rect, 2);
            }
            g.DrawString("More", headerFont, textBrush, LabelLeft + 35 + 5 * (CellSize + 2) + 4, legendY + 2);

            if (_hoverDate != null && _hoverHour >= 0 && _cells.TryGetValue((_hoverDate, _hoverHour), out var hc))
            {
                var tip = $"{_hoverDate} {_hoverHour:00}:00  |  {FormatDuration(hc.ActiveSec)}  |  Keys: {hc.KeyCount}  |  Mouse: {hc.MouseCount}";
                using var tipFont = new Font("Segoe UI", 8F);
                var tipSize = g.MeasureString(tip, tipFont);
                var tipX = Width - tipSize.Width - 12;
                var tipY = legendY;
                using var tipBg = new SolidBrush(Color.FromArgb(50, 50, 50));
                g.FillRoundedRect(tipBg, new Rectangle((int)tipX - 6, (int)tipY - 2, (int)tipSize.Width + 12, (int)tipSize.Height + 6), 4);
                using var tipBrush = new SolidBrush(Color.White);
                g.DrawString(tip, tipFont, tipBrush, tipX, tipY);
            }

            textBrush.Dispose();
            headerFont.Dispose();
        }

        private Color GetHeatColor(int activeSec)
        {
            var ratio = (float)activeSec / _maxActiveSec;
            return GetHeatColorByRatio(ratio);
        }

        private Color GetHeatColorByRatio(float ratio)
        {
            if (ratio <= 0) return Color.FromArgb(40, 40, 40);
            if (ratio < 0.25f) return Color.FromArgb(20, 80, 30);
            if (ratio < 0.50f) return Color.FromArgb(30, 120, 50);
            if (ratio < 0.75f) return Color.FromArgb(40, 160, 70);
            return Color.FromArgb(50, 200, 90);
        }

        private Color GetHeatColorByLevel(int level, int maxLevel)
        {
            return GetHeatColorByRatio((float)level / maxLevel);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            var newHour = -1;
            string? newDate = null;

            if (e.X >= LabelLeft)
            {
                newHour = (e.X - LabelLeft) / (CellSize + CellGap);
                if (newHour >= 0 && newHour < 24)
                {
                    var di = (e.Y - LabelTop) / (CellSize + CellGap);
                    if (di >= 0 && di < _dates.Count)
                        newDate = _dates[di];
                    else
                        newHour = -1;
                }
                else
                    newHour = -1;
            }

            if (newDate != _hoverDate || newHour != _hoverHour)
            {
                _hoverDate = newDate;
                _hoverHour = newHour;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverDate = null;
            _hoverHour = -1;
            Invalidate();
        }

        public int GetPreferredHeight()
        {
            return LabelTop + (_dates.Count + 1) * (CellSize + CellGap) + LegendHeight + 10;
        }

        private static string FormatDuration(int seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return $"{ts.Minutes}m {ts.Seconds}s";
        }
    }

    internal static class GraphicsExtensions
    {
        public static void FillRoundedRect(this Graphics g, Brush b, Rectangle rect, int radius)
        {
            var path = RoundedRectPath(rect, radius);
            g.FillPath(b, path);
        }

        public static void DrawRoundedRect(this Graphics g, Pen p, Rectangle rect, int radius)
        {
            var path = RoundedRectPath(rect, radius);
            g.DrawPath(p, path);
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRectPath(Rectangle rect, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            var d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
