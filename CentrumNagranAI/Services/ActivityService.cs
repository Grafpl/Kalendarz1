using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Kalendarz1.CentrumNagranAI.Services
{
    /// <summary>
    /// Mierzy "aktywność" w każdej klatce jako delta embedingu vs poprzedniej klatki
    /// tej samej kamery. Aktywność = 1 - cosine(emb_now, emb_prev). 0 = identyczne, 1 = bardzo różne.
    ///
    /// Używane przez Heatmap (wizualizacja kamera × godzina), oraz potencjalnie
    /// przez detektor "linia stoi" (gdy delta = 0 przez kilka klatek).
    /// </summary>
    public static class ActivityService
    {
        private static string ConnString =>
            $"Data Source={CnaConfig.DbPath};Cache=Shared;Foreign Keys=True";

        public static void RecordActivity(long frameId, string cameraId, DateTime tsUtc, float[] currentVec)
        {
            // Pobierz poprzednią klatkę tej samej kamery (najnowszą przed tsUtc)
            using var conn = new SqliteConnection(ConnString);
            conn.Open();

            float[]? prevVec = null;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT fe.vector FROM frame f
                    INNER JOIN frame_embedding fe ON fe.frame_id = f.id
                    WHERE f.camera_id = $c AND f.ts < $t
                    ORDER BY f.ts DESC LIMIT 1";
                cmd.Parameters.AddWithValue("$c", cameraId);
                cmd.Parameters.AddWithValue("$t", tsUtc.ToString("o"));
                var blob = cmd.ExecuteScalar();
                if (blob != null && blob is not DBNull)
                    prevVec = EmbeddingService.BlobToFloatArray((byte[])blob);
            }

            float activity = prevVec == null ? 0.5f : (1.0f - EmbeddingService.Cosine(currentVec, prevVec));
            if (activity < 0) activity = 0;
            if (activity > 1) activity = 1;

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO frame_activity (frame_id, camera_id, ts, activity)
                    VALUES ($f, $c, $t, $a)
                    ON CONFLICT(frame_id) DO UPDATE SET activity = excluded.activity";
                cmd.Parameters.AddWithValue("$f", frameId);
                cmd.Parameters.AddWithValue("$c", cameraId);
                cmd.Parameters.AddWithValue("$t", tsUtc.ToString("o"));
                cmd.Parameters.AddWithValue("$a", activity);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Zwraca średnią aktywność per (kamera, godzina dnia 0-23) z zakresu daysBack.
        /// Klucz: (cameraId, hour).
        /// </summary>
        public static Dictionary<(string Camera, int Hour), float> GetHeatmap(int daysBack = 7)
        {
            var since = DateTime.UtcNow.AddDays(-daysBack);
            var sums = new Dictionary<(string, int), (double sum, int count)>();

            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT camera_id, ts, activity
                FROM frame_activity
                WHERE ts >= $since";
            cmd.Parameters.AddWithValue("$since", since.ToString("o"));
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                var cam = rdr.GetString(0);
                var ts = DateTime.Parse(rdr.GetString(1), null, DateTimeStyles.RoundtripKind);
                int hour = ts.ToLocalTime().Hour;
                double a = rdr.GetDouble(2);
                var key = (cam, hour);
                if (!sums.TryGetValue(key, out var entry))
                    entry = (0, 0);
                entry.sum += a;
                entry.count++;
                sums[key] = entry;
            }

            var result = new Dictionary<(string, int), float>();
            foreach (var kv in sums)
                result[kv.Key] = (float)(kv.Value.sum / Math.Max(1, kv.Value.count));
            return result;
        }
    }
}
