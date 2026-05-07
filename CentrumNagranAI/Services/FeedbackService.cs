using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace Kalendarz1.CentrumNagranAI.Services
{
    /// <summary>
    /// Ground truth feedback dla wyników search (F1). Użytkownik klika 👍/👎,
    /// zbieramy dataset do mierzenia precision@K i kalibracji promptów.
    /// </summary>
    public static class FeedbackService
    {
        private static string ConnString =>
            $"Data Source={CnaConfig.DbPath};Cache=Shared;Foreign Keys=True";

        public static void Submit(string query, long frameId, int score, int rank, bool correct, string? userId = null, long? auditId = null)
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO search_feedback (audit_id, query, frame_id, score, rank, feedback, user_id, ts)
                VALUES ($a, $q, $f, $s, $r, $fb, $u, $t)";
            cmd.Parameters.AddWithValue("$a", (object?)auditId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$q", query);
            cmd.Parameters.AddWithValue("$f", frameId);
            cmd.Parameters.AddWithValue("$s", score);
            cmd.Parameters.AddWithValue("$r", rank);
            cmd.Parameters.AddWithValue("$fb", correct ? 1 : 0);
            cmd.Parameters.AddWithValue("$u", (object?)userId ?? "ser");
            cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Statystyki precision@K. Bierze wyniki feedbacku, agreguje per query.
        /// </summary>
        public class PrecisionStats
        {
            public int TotalFeedbacks { get; set; }
            public int Correct { get; set; }
            public int Incorrect { get; set; }
            public double PrecisionPercent => TotalFeedbacks == 0 ? 0 : 100.0 * Correct / TotalFeedbacks;
            public List<(string Query, int Total, int Correct, double Precision)> ByQuery { get; set; } = new();
        }

        public static PrecisionStats GetStats(int daysBack = 30)
        {
            var since = DateTime.UtcNow.AddDays(-daysBack);
            using var conn = new SqliteConnection(ConnString);
            conn.Open();

            var stats = new PrecisionStats();
            var byQuery = new Dictionary<string, (int total, int correct)>();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT query, feedback FROM search_feedback WHERE ts >= $since";
            cmd.Parameters.AddWithValue("$since", since.ToString("o"));
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                string q = rdr.GetString(0);
                bool correct = rdr.GetInt32(1) == 1;
                stats.TotalFeedbacks++;
                if (correct) stats.Correct++; else stats.Incorrect++;

                if (!byQuery.ContainsKey(q)) byQuery[q] = (0, 0);
                var e = byQuery[q]; e.total++; if (correct) e.correct++; byQuery[q] = e;
            }

            foreach (var kv in byQuery)
            {
                double prec = kv.Value.total == 0 ? 0 : 100.0 * kv.Value.correct / kv.Value.total;
                stats.ByQuery.Add((kv.Key, kv.Value.total, kv.Value.correct, prec));
            }
            stats.ByQuery.Sort((a, b) => b.Total.CompareTo(a.Total));

            return stats;
        }
    }
}
