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

            // Wczytaj wszystkie embedingi raz do RAM (dla dim=1536 i 100k klatek = ~600MB,
            // ale baseline robimy max raz na 24h więc OK; alternatywnie 2 osobne SELECTy).
            var allFrames = new List<(string Cam, int Hour, float[] Vec)>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT f.camera_id, f.ts, fe.vector
                    FROM frame f
                    INNER JOIN frame_embedding fe ON fe.frame_id = f.id
                    WHERE f.ts >= $since";
                cmd.Parameters.AddWithValue("$since", since.ToString("o"));
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    string cam = rdr.GetString(0);
                    var ts = DateTime.Parse(rdr.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind);
                    int hour = ts.ToLocalTime().Hour;
                    byte[] blob = (byte[])rdr["vector"];
                    allFrames.Add((cam, hour, EmbeddingService.BlobToFloatArray(blob)));
                }
            }

            int dim = EmbeddingService.Dim;
            // Pierwsze przejście: średnia (centroid) per (cameraId, hour)
            var sums = new Dictionary<(string, int), (float[] sum, int count)>();
            foreach (var (cam, hour, v) in allFrames)
            {
                var key = (cam, hour);
                if (!sums.TryGetValue(key, out var entry)) entry = (new float[dim], 0);
                for (int i = 0; i < dim; i++) entry.sum[i] += v[i];
                entry.count++;
                sums[key] = entry;
            }

            // Oblicz centroidy znormalizowane (cosine wymaga unit vectors)
            var centroids = new Dictionary<(string, int), float[]>();
            var distances = new Dictionary<(string, int), List<float>>();
            foreach (var kv in sums)
            {
                if (kv.Value.count < 3) continue;
                var centroid = new float[dim];
                for (int i = 0; i < dim; i++) centroid[i] = kv.Value.sum[i] / kv.Value.count;
                float norm = 0;
                for (int i = 0; i < dim; i++) norm += centroid[i] * centroid[i];
                norm = (float)Math.Sqrt(norm);
                if (norm > 0) for (int i = 0; i < dim; i++) centroid[i] /= norm;
                centroids[kv.Key] = centroid;
                distances[kv.Key] = new List<float>(kv.Value.count);
            }

            // Drugie przejście — distance dla stddev
            foreach (var (cam, hour, v) in allFrames)
            {
                var key = (cam, hour);
                if (!centroids.TryGetValue(key, out var c)) continue;
                float sim = EmbeddingService.Cosine(v, c);
                distances[key].Add(1f - sim);
            }

            // Zapisz centroid + sample_count + stddev distance
            foreach (var kv in centroids)
            {
                var dists = distances[kv.Key];
                if (dists.Count < 3) continue;

                double mean = dists.Average();
                double variance = dists.Sum(d => (d - mean) * (d - mean)) / dists.Count;
                double stddev = Math.Sqrt(variance);

                using var up = conn.CreateCommand();
                up.CommandText = @"
                    INSERT INTO camera_baseline (camera_id, hour, centroid, sample_count, stddev, updated)
                    VALUES ($c, $h, $v, $n, $sd, $now)
                    ON CONFLICT(camera_id, hour) DO UPDATE SET
                        centroid = excluded.centroid,
                        sample_count = excluded.sample_count,
                        stddev = excluded.stddev,
                        updated = excluded.updated;";
                up.Parameters.AddWithValue("$c", kv.Key.Item1);
                up.Parameters.AddWithValue("$h", kv.Key.Item2);
                up.Parameters.AddWithValue("$v", EmbeddingService.FloatArrayToBlob(kv.Value));
                up.Parameters.AddWithValue("$n", dists.Count);
                up.Parameters.AddWithValue("$sd", stddev);
                up.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
                up.ExecuteNonQuery();
            }

            Log($"Baseline przebudowany: {centroids.Count} cells z ostatnich {daysBack} dni (z stddev dla Gaussian threshold).");
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
            cmd.CommandText = "SELECT centroid, sample_count, COALESCE(stddev, 0.0) FROM camera_baseline WHERE camera_id=$c AND hour=$h";
            cmd.Parameters.AddWithValue("$c", cameraId);
            cmd.Parameters.AddWithValue("$h", hour);
            using var rdr = cmd.ExecuteReader();
            if (!rdr.Read()) return null;

            byte[] blob = (byte[])rdr["centroid"];
            int sampleCount = rdr.GetInt32(1);
            double stddev = rdr.GetDouble(2);
            if (sampleCount < 5) return null; // baseline jest niepewny

            var centroid = EmbeddingService.BlobToFloatArray(blob);
            float sim = EmbeddingService.Cosine(embedding, centroid);
            double distance = 1.0 - sim;

            // C1: Gaussian-style threshold. Jeśli mamy stddev z baseline,
            // próg = max(effThreshold, mean + 2σ). Inaczej fallback do flat threshold.
            // mean dla cosine distance (1-cos) jest "around" stddev: oczekujemy distance ~ 0,
            // więc realnie alarm gdy distance > 2σ.
            double dynamicThreshold = stddev > 0
                ? Math.Max(effThreshold, 2.0 * stddev)
                : effThreshold;

            if (distance >= dynamicThreshold)
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
                ins.Parameters.AddWithValue("$th", dynamicThreshold);
                ins.ExecuteNonQuery();
                Log($"ANOMALIA! kamera={cameraId} hour={hour} dist={distance:F3} (dynThr={dynamicThreshold:F3}, σ={stddev:F3})");
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
