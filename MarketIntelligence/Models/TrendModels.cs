using System;
using System.Collections.Generic;
using System.Linq;

namespace Kalendarz1.MarketIntelligence.Models
{
    /// <summary>Pojedynczy punkt szeregu czasowego (intel_TrendDataPoints).</summary>
    public class IntelTrendPoint
    {
        public int Id { get; set; }
        public string MetricKey { get; set; } = "";
        public decimal Value { get; set; }
        public string? Unit { get; set; }
        public DateTime SnapshotDate { get; set; }
        public int? SourceArticleId { get; set; }
        public string? SourceUrl { get; set; }
        public bool AIExtracted { get; set; }
        public int Confidence { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>Seria do wyświetlenia na sparkline'u (lista punktów + label + ostatnia wartość + delta).</summary>
    public class TrendSeries
    {
        public string MetricKey { get; set; } = "";
        public string Label { get; set; } = "";       // np. "Cena żywca"
        public string Unit { get; set; } = "";        // np. "zł/kg"
        public List<IntelTrendPoint> Points { get; set; } = new();

        public decimal? Latest => Points.Count == 0 ? (decimal?)null : Points.Last().Value;
        public decimal? First => Points.Count == 0 ? (decimal?)null : Points.First().Value;

        public string LatestDisplay => Latest.HasValue ? $"{Latest.Value:0.##}" : "—";

        public string DeltaDisplay
        {
            get
            {
                if (Points.Count < 2 || !First.HasValue || First.Value == 0) return "";
                var delta = (Latest!.Value - First.Value) / First.Value * 100m;
                var sign = delta >= 0 ? "+" : "";
                return $"{sign}{delta:0.#}%";
            }
        }

        /// <summary>Czy trend rośnie (do koloryzacji delta).</summary>
        public bool IsUp => Latest.HasValue && First.HasValue && Latest.Value > First.Value;
        public bool IsDown => Latest.HasValue && First.HasValue && Latest.Value < First.Value;

        /// <summary>Sparkline jako string „x1,y1 x2,y2 …" do bindowania w Polyline.Points (Canvas 100×28).</summary>
        public string PolylinePoints
        {
            get
            {
                if (Points.Count < 2) return "";
                const double W = 100, H = 28, Pad = 2;
                double min = (double)Points.Min(p => p.Value);
                double max = (double)Points.Max(p => p.Value);
                double range = max - min;
                if (range < 0.0001) range = 1; // płaska linia
                var step = (W - 2 * Pad) / (Points.Count - 1);
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < Points.Count; i++)
                {
                    double x = Pad + i * step;
                    double y = H - Pad - ((double)Points[i].Value - min) / range * (H - 2 * Pad);
                    if (i > 0) sb.Append(' ');
                    sb.Append(x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                    sb.Append(',');
                    sb.Append(y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                }
                return sb.ToString();
            }
        }

        public bool HasData => Points.Count >= 2;
    }
}
