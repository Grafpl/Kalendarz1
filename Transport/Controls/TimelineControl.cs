using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Kalendarz1.Transport.Theme;

namespace Kalendarz1.Transport.Controls
{
    /// <summary>
    /// Gantt chart oś czasu kursu — segmenty: załadunek, jazda, rozładunek, powrót.
    /// Marker "TERAZ" jako czerwona pionowa linia.
    /// </summary>
    public class TimelineControl : Control
    {
        public class TimelineStop
        {
            public string ClientName { get; set; } = "";
            public TimeSpan PlannedArrival { get; set; }
            public int DistanceKm { get; set; }
        }

        private TimeSpan _godzinaWyjazdu = new TimeSpan(6, 0, 0);
        private TimeSpan _godzinaPowrotu = new TimeSpan(18, 0, 0);
        private List<TimelineStop> _stops = new List<TimelineStop>();
        private List<Segment> _segments = new List<Segment>();

        private const int LoadUnloadMinutes = 30;
        private const int AvgSpeedKmh = 60;

        private enum SegmentType { Loading, Driving, Unloading, Return }

        private class Segment
        {
            public SegmentType Type { get; set; }
            public TimeSpan Start { get; set; }
            public TimeSpan End { get; set; }
            public string Label { get; set; } = "";
        }

        public TimelineControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Height = 80;
            BackColor = ZpspColors.PanelDarkAlt;
        }

        public void SetCourse(TimeSpan godzinaWyjazdu, TimeSpan godzinaPowrotu, IEnumerable<TimelineStop> stops)
        {
            _godzinaWyjazdu = godzinaWyjazdu;
            _godzinaPowrotu = godzinaPowrotu;
            _stops = stops.OrderBy(s => s.PlannedArrival).ToList();
            BuildSegments();
            Invalidate();
        }

        private void BuildSegments()
        {
            _segments.Clear();
            if (_stops.Count == 0) return;

            var currentTime = _godzinaWyjazdu;

            // Załadunek w bazie
            var loadEnd = currentTime.Add(TimeSpan.FromMinutes(LoadUnloadMinutes));
            _segments.Add(new Segment
            {
                Type = SegmentType.Loading,
                Start = currentTime,
                End = loadEnd,
                Label = "Załadunek"
            });
            currentTime = loadEnd;

            for (int i = 0; i < _stops.Count; i++)
            {
                var stop = _stops[i];

                // Jazda do klienta
                var driveMinutes = stop.DistanceKm > 0 ? (double)stop.DistanceKm / AvgSpeedKmh * 60 : 0;
                if (driveMinutes < 5 && stop.PlannedArrival > currentTime)
                    driveMinutes = (stop.PlannedArrival - currentTime).TotalMinutes;
                if (driveMinutes < 5) driveMinutes = 30;

                var driveEnd = currentTime.Add(TimeSpan.FromMinutes(driveMinutes));
                if (driveEnd > stop.PlannedArrival && stop.PlannedArrival > currentTime)
                    driveEnd = stop.PlannedArrival;

                _segments.Add(new Segment
                {
                    Type = SegmentType.Driving,
                    Start = currentTime,
                    End = driveEnd,
                    Label = $"Jazda \u2192 {stop.ClientName}"
                });
                currentTime = driveEnd;

                // Rozładunek
                var unloadEnd = currentTime.Add(TimeSpan.FromMinutes(LoadUnloadMinutes));
                _segments.Add(new Segment
                {
                    Type = SegmentType.Unloading,
                    Start = currentTime,
                    End = unloadEnd,
                    Label = stop.ClientName
                });
                currentTime = unloadEnd;
            }

            // Powrót
            if (_godzinaPowrotu > currentTime)
            {
                _segments.Add(new Segment
                {
                    Type = SegmentType.Return,
                    Start = currentTime,
                    End = _godzinaPowrotu,
                    Label = "\u2190 Powrót"
                });
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var bounds = ClientRectangle;
            int radius = 6;

            // Tło + border
            using (var path = RoundedRect(bounds, radius))
            using (var bg = new SolidBrush(ZpspColors.PanelDarkAlt))
                g.FillPath(bg, path);
            using (var path = RoundedRect(new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1), radius))
            using (var pen = new Pen(ZpspColors.PanelDarkBorder, 1))
                g.DrawPath(pen, path);

            // Nagłówek
            using (var hBrush = new SolidBrush(ZpspColors.TextMuted))
                g.DrawString("\u23f1\ufe0f OŚ CZASU KURSU", ZpspFonts.Label8Bold, hBrush, 10, 6);

            if (_segments.Count == 0)
            {
                using var emptyBrush = new SolidBrush(ZpspColors.TextMuted);
                g.DrawString("Dodaj ładunki aby zobaczyć oś czasu", ZpspFonts.Text9, emptyBrush, 10, 32);
                return;
            }

            // Zakres czasu
            var timeStart = _godzinaWyjazdu;
            var timeEnd = _godzinaPowrotu.Add(TimeSpan.FromHours(1));
            if (_segments.Count > 0 && _segments.Last().End > timeEnd)
                timeEnd = _segments.Last().End.Add(TimeSpan.FromMinutes(30));
            double totalMinutes = (timeEnd - timeStart).TotalMinutes;
            if (totalMinutes <= 0) totalMinutes = 1;

            int leftMargin = 10;
            int rightMargin = 10;
            int barY = 26;
            int barHeight = 16;
            int availWidth = bounds.Width - leftMargin - rightMargin;

            // Linie godzinowe
            var hour = new TimeSpan((int)Math.Ceiling(timeStart.TotalHours), 0, 0);
            while (hour < timeEnd)
            {
                float x = leftMargin + (float)((hour - timeStart).TotalMinutes / totalMinutes * availWidth);
                using var linePen = new Pen(ZpspColors.PanelDarkBorder, 1);
                g.DrawLine(linePen, x, barY, x, barY + barHeight + 2);

                using var timeBrush = new SolidBrush(ZpspColors.TextMuted);
                g.DrawString($"{hour.Hours:D2}:00", ZpspFonts.Text7, timeBrush, x - 12, barY + barHeight + 4);

                hour = hour.Add(TimeSpan.FromHours(1));
            }

            // Segmenty
            foreach (var seg in _segments)
            {
                float x1 = leftMargin + (float)((seg.Start - timeStart).TotalMinutes / totalMinutes * availWidth);
                float x2 = leftMargin + (float)((seg.End - timeStart).TotalMinutes / totalMinutes * availWidth);
                float w = Math.Max(x2 - x1, 4);

                Color segColor;
                switch (seg.Type)
                {
                    case SegmentType.Loading:   segColor = ZpspColors.GreenDark; break;
                    case SegmentType.Driving:   segColor = ZpspColors.Blue; break;
                    case SegmentType.Unloading: segColor = ZpspColors.Purple; break;
                    case SegmentType.Return:    segColor = ZpspColors.Red; break;
                    default:                    segColor = ZpspColors.TextMuted; break;
                }

                var segRect = new RectangleF(x1, barY, w, barHeight);
                using (var path = RoundedRect(Rectangle.Round(segRect), 3))
                using (var segBrush = new SolidBrush(segColor))
                    g.FillPath(segBrush, path);

                // Tekst w segmencie (jeśli jest miejsce)
                if (w > 30)
                {
                    string label = seg.Label.Length > (int)(w / 6) ? seg.Label.Substring(0, Math.Max(1, (int)(w / 6))) + ".." : seg.Label;
                    using var textBrush = new SolidBrush(Color.White);
                    g.DrawString(label, ZpspFonts.Text7Bold, textBrush, x1 + 3, barY + 2);
                }
            }

            // Marker "TERAZ"
            var now = DateTime.Now.TimeOfDay;
            if (now >= timeStart && now <= timeEnd)
            {
                float nowX = leftMargin + (float)((now - timeStart).TotalMinutes / totalMinutes * availWidth);
                using var nowPen = new Pen(ZpspColors.Red, 2);
                g.DrawLine(nowPen, nowX, barY - 4, nowX, barY + barHeight + 2);

                using var nowBrush = new SolidBrush(ZpspColors.Red);
                g.DrawString("TERAZ", ZpspFonts.Text7Bold, nowBrush, nowX - 14, barY - 14);
            }

            // Legenda
            int legendY = barY + barHeight + 18;
            int lx = leftMargin;
            lx = DrawLegendItem(g, lx, legendY, "Załadunek", ZpspColors.GreenDark);
            lx = DrawLegendItem(g, lx, legendY, "Jazda", ZpspColors.Blue);
            lx = DrawLegendItem(g, lx, legendY, "Rozładunek", ZpspColors.Purple);
            DrawLegendItem(g, lx, legendY, "Powrót", ZpspColors.Red);
        }

        private int DrawLegendItem(Graphics g, int x, int y, string label, Color color)
        {
            using (var brush = new SolidBrush(color))
                g.FillRectangle(brush, x, y + 2, 8, 8);
            using (var textBrush = new SolidBrush(ZpspColors.TextLight))
                g.DrawString(label, ZpspFonts.Text8, textBrush, x + 12, y);
            return x + (int)g.MeasureString(label, ZpspFonts.Text8).Width + 22;
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d > bounds.Width) d = bounds.Width;
            if (d > bounds.Height) d = bounds.Height;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
