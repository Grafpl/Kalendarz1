using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace Kalendarz1.CentrumNagranAI.Services
{
    /// <summary>
    /// Anomaly detection bez uczenia — bazuje na embedingach z OpenAI.
    /// Idea: dla każdej kamery × godziny dnia obliczamy "centroid" (średnią
    /// embedingów ostatnich N dni z tej godziny). Nowa klatka odległa od
    /// centroida (cosine sim niska) = anomalia.
    ///
    /// To proste podejście dobrze łapie:
    ///  - osoby na hali w nocy (gdy normalnie pusto)
    ///  - światła w magazynie po godzinach
    ///  - obce pojazdy na rampie
    /// Kiepsko łapie subtelne rzeczy ("brak czepka") - od tego mamy GuardService.
    /// </summary>
    public static class AnomalyService
    {
        private static string ConnString =>
            $"Data Source={CnaConfig.DbPath};Cache=Shared;Foreign Keys=True";

        // Domyślny próg cosine. < 0.7 = "daleko od normy"
        public const double DefaultDistanceThreshold = 0.30;

        /// <summary>
        /// Przelicza baseline dla wszystkich (kamera × godzina) na podstawie
        /// klatek z ostatnich daysBack dni.
        /// </summary>
        public static void RebuildBaseline(int daysBack = 7)
        {
            CnaConfig.ZaladujJesliTrzeba();
            FrameIndex.Init();

            DateTime since = DateTime.UtcNow.AddDays(-daysBack);
            using var conn = new SqliteConnection(ConnString);
            conn.Open();

            // Zbierz wszystkie embedingi w jednym SELECT
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT f.camera_id, f.ts, fe.vector
                FROM frame f
                INNER JOIN frame_embedding fe ON fe.frame_id = f.id
                WHERE f.ts >= $since";
            cmd.Parameters.AddWithValue("$since", since.ToString("o"));

            // Akumulator: (cameraId, hour) → (sum vector, count)
            var acc = new Dictionary<(string, int), (float[] sum, int count)>();
            int dim = EmbeddingService.Dim;

            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    string cam = rdr.GetString(0);
                    var ts = DateTime.Parse(rdr.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind);
                    int hour = ts.ToLocalTime().Hour; // baseline po lokalnej godzinie (rytm dobowy zakładu)
                    byte[] blob = (byte[])rdr["vector"];
                    var v = EmbeddingService.BlobToFloatArray(blob);

                    var key = (cam, hour);
                    if (!acc.TryGetValue(key, out var entry))
                    {
                        entry = (new float[dim], 0);
                    }
                    for (int i = 0; i < dim; i++) entry.sum[i] += v[i];
                    entry.count++;
                    acc[key] = entry;
                }
            }

            // Zapisz centroidy
            foreach (var kv in acc)
            {
                if (kv.Value.count < 3) continue; // za mało próbek żeby ufać średniej
                var centroid = new float[dim];
                for (int i = 0; i < dim; i++) centroid[i] = kv.Value.sum[i] / kv.Value.count;
                // Normalizuj centroid (cosine wymaga unit vectors)
                float norm = 0;
                for (int i = 0; i < dim; i++) norm += centroid[i] * centroid[i];
                norm = (float)Math.Sqrt(norm);
                if (norm > 0) for (int i = 0; i < dim; i++) centroid[i] /= norm;

                using var up = conn.CreateCommand();
                up.CommandText = @"
                    INSERT INTO camera_baseline (camera_id, hour, centroid, sample_count, updated)
                    VALUES ($c, $h, $v, $n, $now)
                    ON CONFLICT(camera_id, hour) DO UPDATE SET
                        centroid = excluded.centroid,
                        sample_count = excluded.sample_count,
                        updated = excluded.updated;";
                up.Parameters.AddWithValue("$c", kv.Key.Item1);
                up.Parameters.AddWithValue("$h", kv.Key.Item2);
                up.Parameters.AddWithValue("$v", EmbeddingService.FloatArrayToBlob(centroid));
                up.Parameters.AddWithValue("$n", kv.Value.count);
                up.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
                up.ExecuteNonQuery();
            }

            Log($"Baseline przebudowany: {acc.Count} (kamera×godzina) klastrów z ostatnich {daysBack} dni.");
        }

        /// <summary>
        /// Sprawdź klatkę przeciwko baseline jej kamery×godziny. Jeśli daleko - zapisz alert.
        /// Zwraca distance albo null jeśli baseline brak.
        /// </summary>
        public static double? CheckFrame(long frameId, string cameraId, DateTime tsUtc, float[] embedding,
            double? threshold = null)
        {
            double effThreshold = threshold ?? CnaConfig.AnomalyThreshold;

            int hour = tsUtc.ToLocalTime().Hour;
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT centroid, sample_count FROM camera_baseline WHERE camera_id=$c AND hour=$h";
            cmd.Parameters.AddWithValue("$c", cameraId);
            cmd.Parameters.AddWithValue("$h", hour);
            using var rdr = cmd.ExecuteReader();
            if (!rdr.Read()) return null;

            byte[] blob = (byte[])rdr["centroid"];
            int sampleCount = rdr.GetInt32(1);
            if (sampleCount < 5) return null; // baseline jest niepewny

            var centroid = EmbeddingService.BlobToFloatArray(blob);
            float sim = EmbeddingService.Cosine(embedding, centroid);
            double distance = 1.0 - sim;

            if (distance >= effThreshold)
            {
                rdr.Close();
                using var ins = conn.CreateCommand();
                ins.CommandText = @"
                    INSERT INTO anomaly_alert (frame_id, camera_id, ts, distance, threshold)
                    VALUES ($f, $c, $t, $d, $th)";
                ins.Parameters.AddWithValue("$f", frameId);
                ins.Parameters.AddWithValue("$c", cameraId);
                ins.Parameters.AddWithValue("$t", tsUtc.ToString("o"));
                ins.Parameters.AddWithValue("$d", distance);
                ins.Parameters.AddWithValue("$th", effThreshold);
                ins.ExecuteNonQuery();
                Log($"ANOMALIA! kamera={cameraId} hour={hour} dist={distance:F3} (próg {effThreshold:F3})");
            }

            return distance;
        }

        public static List<(long FrameId, string CameraId, DateTime Ts, double Distance, string FilePath)> GetRecentAnomalies(int limit = 50)
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT a.frame_id, a.camera_id, a.ts, a.distance, f.file_path
                FROM anomaly_alert a
                INNER JOIN frame f ON f.id = a.frame_id
                ORDER BY a.ts DESC LIMIT $lim";
            cmd.Parameters.AddWithValue("$lim", limit);
            var result = new List<(long, string, DateTime, double, string)>();
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                result.Add((
                    rdr.GetInt64(0),
                    rdr.GetString(1),
                    DateTime.Parse(rdr.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    rdr.GetDouble(3),
                    rdr.GetString(4)
                ));
            }
            return result;
        }

        private static void Log(string msg)
        {
            string line = $"{DateTime.Now:HH:mm:ss.fff} [CNA-Anomaly] {msg}";
            Debug.WriteLine(line);
            try
            {
                Directory.CreateDirectory(CnaConfig.AuditDir);
                File.AppendAllText(Path.Combine(CnaConfig.AuditDir, "cna_anomaly.log"),
                    line + Environment.NewLine, System.Text.Encoding.UTF8);
            }
            catch { }
        }
    }
}
