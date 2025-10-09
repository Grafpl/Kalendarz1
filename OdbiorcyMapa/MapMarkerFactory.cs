// =============== MapMarkerFactory.cs - WYSOKA JAKOŚĆ + ETYKIETY ===============
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using GMap.NET.WindowsForms.ToolTips;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;

namespace Kalendarz1
{
    public class MapMarkerFactory
    {
        private readonly HandlowiecColorProvider colorProvider;
        private readonly Dictionary<string, Bitmap> iconCache = new Dictionary<string, Bitmap>();
        private readonly object cacheLock = new object();

        public MapMarkerFactory(HandlowiecColorProvider provider)
        {
            colorProvider = provider;
        }

        public CustomMapMarker CreateMarker(List<OdbiorcaDto> odbiorcy)
        {
            if (odbiorcy == null || odbiorcy.Count == 0)
                return null;

            var first = odbiorcy.First();
            if (!first.Latitude.HasValue || !first.Longitude.HasValue)
                return null;

            var position = new PointLatLng(first.Latitude.Value, first.Longitude.Value);

            var handlowcy = odbiorcy.Select(o => o.HandlowiecNazwa).Distinct().ToList();
            Color markerColor = handlowcy.Count == 1
                ? colorProvider.GetColor(handlowcy.First())
                : Color.Gray;

            return new CustomMapMarker(position, markerColor, odbiorcy);
        }

        public void ClearCache()
        {
            lock (cacheLock)
            {
                foreach (var bitmap in iconCache.Values)
                {
                    bitmap?.Dispose();
                }
                iconCache.Clear();
            }
        }
    }

    public class CustomMapMarker : GMapMarker
    {
        public List<OdbiorcaDto> Odbiorcy { get; }
        private readonly Color markerColor;
        private readonly Bitmap markerBitmap;
        private readonly bool showLabel;

        public CustomMapMarker(PointLatLng pos, Color color, List<OdbiorcaDto> odbiorcy)
            : base(pos)
        {
            Odbiorcy = odbiorcy;
            markerColor = color;
            showLabel = odbiorcy.Count > 1;

            // Większy rozmiar dla lepszej jakości
            Size = new Size(36, 48);
            Offset = new Point(-18, -48);

            // Utwórz bitmapę markera
            markerBitmap = CreateMarkerBitmap();

            // Tooltip z nazwami
            if (odbiorcy.Count == 1)
            {
                ToolTipText = $"{odbiorcy[0].Nazwa}\n{odbiorcy[0].AdresPelny}\n{odbiorcy[0].HandlowiecNazwa}";
                ToolTipMode = MarkerTooltipMode.OnMouseOver;
            }
            else
            {
                // Dla grup - stała etykieta z nazwami
                var names = string.Join("\n", odbiorcy.Take(3).Select(o => o.Nazwa));
                if (odbiorcy.Count > 3)
                    names += $"\n... +{odbiorcy.Count - 3} więcej";

                ToolTipText = names;
                ToolTipMode = MarkerTooltipMode.Always; // Zawsze widoczne dla grup
            }
        }

        private Bitmap CreateMarkerBitmap()
        {
            var bitmap = new Bitmap(36, 48);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                // Cień
                using (var shadowBrush = new SolidBrush(Color.FromArgb(60, Color.Black)))
                {
                    g.FillEllipse(shadowBrush, 4, 4, 30, 30);
                }

                // Główne koło markera
                using (var brush = new SolidBrush(markerColor))
                using (var pen = new Pen(Color.White, 2))
                {
                    g.FillEllipse(brush, 2, 2, 32, 32);
                    g.DrawEllipse(pen, 2, 2, 32, 32);

                    // Strzałka w dół
                    Point[] points = {
                        new Point(18, 30),
                        new Point(10, 40),
                        new Point(26, 40)
                    };
                    g.FillPolygon(brush, points);
                }

                // Wewnętrzne koło
                using (var innerBrush = new SolidBrush(Color.FromArgb(50, Color.White)))
                {
                    g.FillEllipse(innerBrush, 6, 6, 24, 24);
                }

                // Liczba dla grup
                if (Odbiorcy.Count > 1)
                {
                    using (var font = new Font("Arial", 11, FontStyle.Bold))
                    using (var textBrush = new SolidBrush(Color.White))
                    using (var outlinePen = new Pen(Color.Black, 1))
                    {
                        var text = Odbiorcy.Count > 99 ? "99+" : Odbiorcy.Count.ToString();
                        var textSize = g.MeasureString(text, font);
                        var x = (36 - textSize.Width) / 2;
                        var y = (32 - textSize.Height) / 2;

                        // Kontur tekstu
                        using (var path = new GraphicsPath())
                        {
                            path.AddString(text, font.FontFamily, (int)font.Style, font.Size,
                                new RectangleF(x, y, textSize.Width, textSize.Height),
                                StringFormat.GenericDefault);
                            g.DrawPath(outlinePen, path);
                            g.FillPath(textBrush, path);
                        }
                    }
                }
            }
            return bitmap;
        }

        public override void OnRender(Graphics g)
        {
            if (markerBitmap != null)
            {
                g.DrawImage(markerBitmap, LocalPosition.X, LocalPosition.Y, Size.Width, Size.Height);
            }
        }

        public override void Dispose()
        {
            markerBitmap?.Dispose();
            base.Dispose();
        }
    }

    // Opcjonalna klasa dla etykiet tekstowych
    public class GMapMarkerLabel : GMapMarker
    {
        private readonly string text;
        private readonly Color textColor;
        private readonly Font font;

        public GMapMarkerLabel(PointLatLng pos, string text, Color color)
            : base(pos)
        {
            this.text = text;
            this.textColor = color;
            this.font = new Font("Arial", 9, FontStyle.Bold);

            using (var g = Graphics.FromImage(new Bitmap(1, 1)))
            {
                var size = g.MeasureString(text, font);
                Size = new Size((int)size.Width + 10, (int)size.Height + 4);
                Offset = new Point(-Size.Width / 2, -Size.Height - 50);
            }
        }

        public override void OnRender(Graphics g)
        {
            var rect = new Rectangle(LocalPosition.X, LocalPosition.Y, Size.Width, Size.Height);

            // Tło etykiety
            using (var bgBrush = new SolidBrush(Color.FromArgb(230, Color.White)))
            using (var borderPen = new Pen(textColor, 1))
            {
                g.FillRectangle(bgBrush, rect);
                g.DrawRectangle(borderPen, rect);
            }

            // Tekst
            using (var textBrush = new SolidBrush(Color.Black))
            {
                g.DrawString(text, font, textBrush, rect,
                    new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    });
            }
        }

        public override void Dispose()
        {
            font?.Dispose();
            base.Dispose();
        }
    }
}