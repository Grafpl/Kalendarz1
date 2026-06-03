using Kalendarz1.Customer360.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kalendarz1.Customer360.Services
{
    /// <summary>Renderuje wykres obrotu miesięcznego do PNG (do osadzenia w PDF) przez SkiaSharp.</summary>
    public static class ChartImageRenderer
    {
        private static readonly string[] MiesSkrot = { "sty", "lut", "mar", "kwi", "maj", "cze", "lip", "sie", "wrz", "paź", "lis", "gru" };

        public static byte[] RenderObrotMiesieczny(List<MonthlyStats> data, int width, int height)
        {
            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            var c = surface.Canvas;
            c.Clear(SKColors.White);

            using var fontMaly = new SKPaint { Color = SKColor.Parse("#64748B"), TextSize = 12, IsAntialias = true, TextAlign = SKTextAlign.Center };
            using var fontWart = new SKPaint { Color = SKColor.Parse("#1E40AF"), TextSize = 11, IsAntialias = true, TextAlign = SKTextAlign.Center, FakeBoldText = true };
            using var paintSlupek = new SKPaint { Color = Customer360Palette.Niebieski, IsAntialias = true };
            using var paintSr = new SKPaint { Color = Customer360Palette.Zielony, IsAntialias = true, StrokeWidth = 2, IsStroke = true };
            using var paintOs = new SKPaint { Color = SKColor.Parse("#E2E8F0"), StrokeWidth = 1 };

            if (data == null || data.Count == 0)
            {
                using var pEmpty = new SKPaint { Color = SKColors.Gray, TextSize = 14, IsAntialias = true, TextAlign = SKTextAlign.Center };
                c.DrawText("Brak danych", width / 2f, height / 2f, pEmpty);
                return Encode(surface);
            }

            float marginL = 8, marginR = 8, marginTop = 24, marginBottom = 28;
            float plotW = width - marginL - marginR;
            float plotH = height - marginTop - marginBottom;
            float baseY = marginTop + plotH;

            double max = (double)data.Max(d => d.Wartosc);
            if (max <= 0) max = 1;
            double avg = (double)data.Average(d => d.Wartosc);

            int n = data.Count;
            float slot = plotW / n;
            float barW = Math.Min(46, slot * 0.6f);

            // linia bazowa
            c.DrawLine(marginL, baseY, marginL + plotW, baseY, paintOs);

            for (int i = 0; i < n; i++)
            {
                var d = data[i];
                float cx = marginL + slot * i + slot / 2f;
                float h = (float)((double)d.Wartosc / max * plotH);
                if (h < 2) h = 2;
                var rect = new SKRect(cx - barW / 2f, baseY - h, cx + barW / 2f, baseY);
                using (var path = new SKRoundRect(rect, 4, 4))
                    c.DrawRoundRect(path, paintSlupek);

                // wartość nad słupkiem
                c.DrawText(Fmt(d.Wartosc), cx, baseY - h - 4, fontWart);
                // etykieta miesiąca
                string lbl = (d.Month >= 1 && d.Month <= 12 ? MiesSkrot[d.Month - 1] : d.Month.ToString()) + $" {d.Year % 100:00}";
                c.DrawText(lbl, cx, baseY + 16, fontMaly);
            }

            // linia średniej
            float avgY = baseY - (float)(avg / max * plotH);
            c.DrawLine(marginL, avgY, marginL + plotW, avgY, paintSr);
            using (var pAvg = new SKPaint { Color = Customer360Palette.Zielony, TextSize = 11, IsAntialias = true, TextAlign = SKTextAlign.Right, FakeBoldText = true })
                c.DrawText($"śr {Fmt((decimal)avg)}", marginL + plotW, avgY - 4, pAvg);

            return Encode(surface);
        }

        private static byte[] Encode(SKSurface surface)
        {
            using var img = surface.Snapshot();
            using var data = img.Encode(SKEncodedImageFormat.Png, 90);
            return data.ToArray();
        }

        private static string Fmt(decimal v) => Customer360Format.FmtZl(v);
    }
}
