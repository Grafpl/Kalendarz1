using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.MarketIntelligence.Models;

namespace Kalendarz1.MarketIntelligence.Services
{
    /// <summary>Faza B: odczyt szeregów czasowych do UI (sparkline'y) + zapis (z PriceExtraction/TrendAnalysis).</summary>
    public class TrendsService
    {
        private readonly string _connectionString;

        public TrendsService(string connectionString = null)
        {
            _connectionString = connectionString ?? MarketIntelligenceConfig.LibraNetConnectionString;
        }

        /// <summary>Seria punktów dla danej metryki (ostatnie N dni).</summary>
        public async Task<TrendSeries> GetSeriesAsync(string metricKey, string label, string unit, int daysBack = 30)
        {
            var series = new TrendSeries { MetricKey = metricKey, Label = label, Unit = unit };
            try
            {
                using var cn = new SqlConnection(_connectionString);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(@"
SELECT Id, MetricKey, Value, Unit, SnapshotDate, SourceArticleId, SourceUrl, AIExtracted, Confidence, Notes
FROM intel_TrendDataPoints
WHERE MetricKey = @k
  AND SnapshotDate >= DATEADD(day, -@days, CAST(GETDATE() AS DATE))
ORDER BY SnapshotDate ASC", cn) { CommandTimeout = 15 };
                cmd.Parameters.AddWithValue("@k", metricKey);
                cmd.Parameters.AddWithValue("@days", daysBack);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    series.Points.Add(new IntelTrendPoint
                    {
                        Id = r.GetInt32(0),
                        MetricKey = r.GetString(1),
                        Value = r.GetDecimal(2),
                        Unit = r.IsDBNull(3) ? null : r.GetString(3),
                        SnapshotDate = r.GetDateTime(4),
                        SourceArticleId = r.IsDBNull(5) ? (int?)null : r.GetInt32(5),
                        SourceUrl = r.IsDBNull(6) ? null : r.GetString(6),
                        AIExtracted = r.GetBoolean(7),
                        Confidence = r.GetInt32(8),
                        Notes = r.IsDBNull(9) ? null : r.GetString(9)
                    });
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[Trends] GetSeries({metricKey}): {ex.Message}"); }
            return series;
        }

        /// <summary>UPSERT punktu na konkretną datę: jeśli już istnieje (MetricKey + SnapshotDate) → update, inaczej insert.</summary>
        public async Task UpsertDailyPointAsync(string metricKey, decimal value, string unit, DateTime date,
            bool aiExtracted, int? sourceArticleId = null, string sourceUrl = null, int confidence = 3, string notes = null)
        {
            try
            {
                using var cn = new SqlConnection(_connectionString);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(@"
IF EXISTS (SELECT 1 FROM intel_TrendDataPoints WHERE MetricKey=@k AND SnapshotDate=@d)
    UPDATE intel_TrendDataPoints
       SET Value=@v, Unit=@u, SourceArticleId=@sid, SourceUrl=@surl, AIExtracted=@ai, Confidence=@conf, Notes=@notes
     WHERE MetricKey=@k AND SnapshotDate=@d
ELSE
    INSERT INTO intel_TrendDataPoints (MetricKey, Value, Unit, SnapshotDate, SourceArticleId, SourceUrl, AIExtracted, Confidence, Notes)
    VALUES (@k, @v, @u, @d, @sid, @surl, @ai, @conf, @notes)", cn) { CommandTimeout = 15 };
                cmd.Parameters.AddWithValue("@k", metricKey);
                cmd.Parameters.AddWithValue("@v", value);
                cmd.Parameters.AddWithValue("@u", (object)unit ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@d", date.Date);
                cmd.Parameters.AddWithValue("@sid", (object)sourceArticleId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@surl", (object)sourceUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ai", aiExtracted);
                cmd.Parameters.AddWithValue("@conf", Math.Clamp(confidence, 1, 5));
                cmd.Parameters.AddWithValue("@notes", (object)notes ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { Debug.WriteLine($"[Trends] Upsert({metricKey},{date:yyyy-MM-dd}): {ex.Message}"); }
        }
    }
}
