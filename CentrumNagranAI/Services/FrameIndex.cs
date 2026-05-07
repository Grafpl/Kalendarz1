using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Kalendarz1.CentrumNagranAI.Services
{
    /// <summary>
    /// Repozytorium SQLite dla klatek + (docelowo) embeddingów + audit.
    /// Każda metoda otwiera własne połączenie — SQLite wspiera concurrent readers
    /// i jeden writer (WAL), więc to tańsze niż trzymanie globalnego connection.
    /// </summary>
    public static class FrameIndex
    {
        private static string ConnString =>
            $"Data Source={CnaConfig.DbPath};Cache=Shared;Foreign Keys=True";

        private static bool _initialized;
        private static readonly object _initLock = new();

        /// <summary>
        /// Tworzy bazę (jeśli nie istnieje), wykonuje schema z embedded SQL,
        /// upserta listę kamer z CnaConfig.
        /// </summary>
        public static void Init()
        {
            CnaConfig.ZaladujJesliTrzeba();
            if (_initialized) return;

            lock (_initLock)
            {
                if (_initialized) return;

                Directory.CreateDirectory(Path.GetDirectoryName(CnaConfig.DbPath)!);

                using var conn = new SqliteConnection(ConnString);
                conn.Open();

                // Schema z pliku obok exe (Content w csproj) lub fallback do hardcoded.
                string schemaSql = WczytajSchemaSql();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = schemaSql;
                    cmd.ExecuteNonQuery();
                }

                // Migracje dla starszych baz (nowe kolumny dodane w późniejszych fazach)
                TryAlter(conn, "ALTER TABLE guard_rule ADD COLUMN notify_sms INTEGER NOT NULL DEFAULT 0");

                // Upsert kamer.
                foreach (var k in CnaConfig.Kamery)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        INSERT INTO camera (id, name, host, channel, stream_type, enabled)
                        VALUES ($id, $name, $host, $ch, $stream, 1)
                        ON CONFLICT(id) DO UPDATE SET
                            name = excluded.name,
                            host = excluded.host,
                            channel = excluded.channel,
                            stream_type = excluded.stream_type;";
                    cmd.Parameters.AddWithValue("$id", k.Id);
                    cmd.Parameters.AddWithValue("$name", k.Id);
                    cmd.Parameters.AddWithValue("$host", k.Host);
                    cmd.Parameters.AddWithValue("$ch", k.Channel);
                    cmd.Parameters.AddWithValue("$stream", k.StreamType);
                    cmd.ExecuteNonQuery();
                }

                _initialized = true;
            }
        }

        /// <summary>
        /// Wstawia rekord klatki. Embedding status = 0 (pending) — dorobi się go w innym procesie.
        /// Zwraca id wstawionego rekordu.
        /// </summary>
        public static long InsertFrame(string cameraId, DateTime tsUtc, string filePath, long fileSize)
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO frame (camera_id, ts, file_path, file_size, embedding_status)
                VALUES ($cam, $ts, $path, $size, 0);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$cam", cameraId);
            cmd.Parameters.AddWithValue("$ts", tsUtc.ToString("o")); // ISO 8601
            cmd.Parameters.AddWithValue("$path", filePath);
            cmd.Parameters.AddWithValue("$size", fileSize);
            return (long)cmd.ExecuteScalar()!;
        }

        public static long CountFrames()
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM frame";
            return (long)cmd.ExecuteScalar()!;
        }

        /// <summary>
        /// Rekord klatki dla potrzeb wyszukiwania.
        /// </summary>
        public class FrameRecord
        {
            public long Id { get; set; }
            public string CameraId { get; set; } = string.Empty;
            public DateTime TsUtc { get; set; }
            public string FilePath { get; set; } = string.Empty;
            public long FileSize { get; set; }
        }

        /// <summary>
        /// Zwraca pulę klatek-kandydatów do wyszukiwania.
        /// Optionally filtruje po zakresie dat + liście kamer + maksymalnej liczbie.
        /// Zamawiamy DESC by ts — domyślnie zwracamy najnowsze.
        /// </summary>
        public static List<FrameRecord> GetFrames(
            DateTime? fromUtc = null,
            DateTime? toUtc = null,
            IEnumerable<string>? cameraIds = null,
            int? limit = null)
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();

            var sql = "SELECT id, camera_id, ts, file_path, file_size FROM frame WHERE 1=1";
            if (fromUtc.HasValue) { sql += " AND ts >= $from"; cmd.Parameters.AddWithValue("$from", fromUtc.Value.ToString("o")); }
            if (toUtc.HasValue)   { sql += " AND ts <= $to";   cmd.Parameters.AddWithValue("$to", toUtc.Value.ToString("o")); }
            if (cameraIds != null)
            {
                var list = cameraIds.ToList();
                if (list.Count > 0)
                {
                    var placeholders = string.Join(",", list.Select((_, i) => $"$cam{i}"));
                    sql += $" AND camera_id IN ({placeholders})";
                    for (int i = 0; i < list.Count; i++) cmd.Parameters.AddWithValue($"$cam{i}", list[i]);
                }
            }
            sql += " ORDER BY ts DESC";
            if (limit.HasValue) sql += $" LIMIT {limit.Value}";

            cmd.CommandText = sql;
            var result = new List<FrameRecord>();
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                result.Add(new FrameRecord
                {
                    Id = rdr.GetInt64(0),
                    CameraId = rdr.GetString(1),
                    TsUtc = DateTime.Parse(rdr.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    FilePath = rdr.GetString(3),
                    FileSize = rdr.IsDBNull(4) ? 0 : rdr.GetInt64(4)
                });
            }
            return result;
        }

        /// <summary>
        /// Wpisz audit zapytania użytkownika (RODO + tracking kosztów).
        /// </summary>
        public static long InsertAudit(string queryText, string userId, IEnumerable<long> topKIds,
                                       int vlmCalls, double costUsd, long durationMs)
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO query_audit (ts, user_id, query_text, top_k_ids, vlm_calls, vlm_cost_usd, duration_ms)
                VALUES ($ts, $u, $q, $ids, $calls, $cost, $dur);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$ts", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("$u", userId ?? string.Empty);
            cmd.Parameters.AddWithValue("$q", queryText);
            cmd.Parameters.AddWithValue("$ids", string.Join(",", topKIds));
            cmd.Parameters.AddWithValue("$calls", vlmCalls);
            cmd.Parameters.AddWithValue("$cost", costUsd);
            cmd.Parameters.AddWithValue("$dur", durationMs);
            return (long)cmd.ExecuteScalar()!;
        }

        /// <summary>
        /// Wpis caption + embedding. Wywołane po VLM caption + OpenAI embed.
        /// </summary>
        public static void UpsertCaptionAndEmbedding(long frameId, string caption, float[] vector, string? tagsJson = null)
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO frame_caption (frame_id, caption, tags, created)
                    VALUES ($id, $cap, $tags, $now)
                    ON CONFLICT(frame_id) DO UPDATE SET
                        caption = excluded.caption,
                        tags = excluded.tags;";
                cmd.Parameters.AddWithValue("$id", frameId);
                cmd.Parameters.AddWithValue("$cap", caption);
                cmd.Parameters.AddWithValue("$tags", (object?)tagsJson ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
                cmd.ExecuteNonQuery();
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO frame_embedding (frame_id, dim, vector)
                    VALUES ($id, $dim, $vec)
                    ON CONFLICT(frame_id) DO UPDATE SET
                        dim = excluded.dim,
                        vector = excluded.vector;";
                cmd.Parameters.AddWithValue("$id", frameId);
                cmd.Parameters.AddWithValue("$dim", vector.Length);
                cmd.Parameters.AddWithValue("$vec", EmbeddingService.FloatArrayToBlob(vector));
                cmd.ExecuteNonQuery();
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "UPDATE frame SET embedding_status = 1 WHERE id = $id";
                cmd.Parameters.AddWithValue("$id", frameId);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        public static void MarkEmbeddingFailed(long frameId)
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE frame SET embedding_status = 2 WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", frameId);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Lista klatek bez embedingu (status=0). Używane przez backfill worker.
        /// </summary>
        public static List<FrameRecord> GetFramesWithoutEmbedding(int limit = 50)
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id, camera_id, ts, file_path, file_size FROM frame
                WHERE embedding_status = 0
                ORDER BY ts DESC
                LIMIT $lim";
            cmd.Parameters.AddWithValue("$lim", limit);
            var result = new List<FrameRecord>();
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                result.Add(new FrameRecord
                {
                    Id = rdr.GetInt64(0),
                    CameraId = rdr.GetString(1),
                    TsUtc = DateTime.Parse(rdr.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    FilePath = rdr.GetString(3),
                    FileSize = rdr.IsDBNull(4) ? 0 : rdr.GetInt64(4)
                });
            }
            return result;
        }

        public static (long total, long withEmbedding) GetEmbeddingStats()
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*), SUM(CASE WHEN embedding_status=1 THEN 1 ELSE 0 END) FROM frame";
            using var rdr = cmd.ExecuteReader();
            rdr.Read();
            return (rdr.GetInt64(0), rdr.IsDBNull(1) ? 0 : rdr.GetInt64(1));
        }

        /// <summary>
        /// KNN cosine: top-K klatek najbardziej podobnych do queryVec.
        /// Optionally filtruje po dacie/kamerze. Bez sqlite-vec - prosty in-memory scan.
        /// 100k wektorów × 1536 floats = ~600 MB w RAM. Dla PoC akceptowalne; production
        /// wymagałaby sqlite-vec albo HNSW indeksu.
        /// </summary>
        public class KnnHit
        {
            public long FrameId { get; set; }
            public string CameraId { get; set; } = string.Empty;
            public DateTime TsUtc { get; set; }
            public string FilePath { get; set; } = string.Empty;
            public string? Caption { get; set; }
            public float Similarity { get; set; }
        }

        /// <summary>
        /// KNN streaming - czytamy SQL streamem (DataReader trzyma jeden wiersz na raz),
        /// w pamięci utrzymujemy tylko top-K (PriorityQueue). Skaluje do milionów klatek
        /// bez wybuchu RAM. Dla 100k × 1536 floats: ~30MB constant (top-K=50).
        /// </summary>
        public static List<KnnHit> KnnSearch(
            float[] queryVec, int topK,
            DateTime? fromUtc = null, DateTime? toUtc = null,
            IEnumerable<string>? cameraIds = null)
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();

            var sql = @"
                SELECT f.id, f.camera_id, f.ts, f.file_path, fc.caption, fe.vector
                FROM frame f
                INNER JOIN frame_embedding fe ON fe.frame_id = f.id
                LEFT JOIN frame_caption fc ON fc.frame_id = f.id
                WHERE 1=1";
            if (fromUtc.HasValue) { sql += " AND f.ts >= $from"; cmd.Parameters.AddWithValue("$from", fromUtc.Value.ToString("o")); }
            if (toUtc.HasValue)   { sql += " AND f.ts <= $to";   cmd.Parameters.AddWithValue("$to", toUtc.Value.ToString("o")); }
            if (cameraIds != null)
            {
                var list = cameraIds.ToList();
                if (list.Count > 0)
                {
                    var placeholders = string.Join(",", list.Select((_, i) => $"$cam{i}"));
                    sql += $" AND f.camera_id IN ({placeholders})";
                    for (int i = 0; i < list.Count; i++) cmd.Parameters.AddWithValue($"$cam{i}", list[i]);
                }
            }
            cmd.CommandText = sql;

            // Min-heap: trzymamy K najlepszych. Najmniejszy jest "na wierzchu" - to ten do wymiany.
            // PriorityQueue domyślnie min-heap po priorytecie (similarity).
            var heap = new PriorityQueue<KnnHit, float>(topK + 1);

            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                byte[] blob = (byte[])rdr["vector"];
                // Inline cosine bez alokacji nowego float[] - liczymy bezpośrednio z blob.
                float sim = CosineFromBlob(queryVec, blob);

                if (heap.Count < topK)
                {
                    var hit = MakeHit(rdr, sim);
                    heap.Enqueue(hit, sim);
                }
                else
                {
                    // Sprawdź czy ten kandydat lepszy od najgorszego w heap.
                    if (sim > heap.Peek().Similarity)
                    {
                        heap.Dequeue();
                        var hit = MakeHit(rdr, sim);
                        heap.Enqueue(hit, sim);
                    }
                }
            }

            // Wyciągnij wszystko z heap, posortuj DESC.
            var result = new List<KnnHit>(heap.Count);
            while (heap.Count > 0) result.Add(heap.Dequeue());
            result.Sort((a, b) => b.Similarity.CompareTo(a.Similarity));
            return result;
        }

        private static KnnHit MakeHit(SqliteDataReader rdr, float sim) => new()
        {
            FrameId = rdr.GetInt64(0),
            CameraId = rdr.GetString(1),
            TsUtc = DateTime.Parse(rdr.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
            FilePath = rdr.GetString(3),
            Caption = rdr.IsDBNull(4) ? null : rdr.GetString(4),
            Similarity = sim
        };

        // Inline cosine bezpośrednio z blob — bez alokacji float[].
        // Zakładamy że oba znormalizowane (OpenAI zwraca unit vectors).
        private static float CosineFromBlob(float[] q, byte[] blob)
        {
            int n = Math.Min(q.Length, blob.Length / 4);
            float dot = 0;
            for (int i = 0; i < n; i++)
            {
                int o = i * 4;
                float v = BitConverter.ToSingle(blob, o);
                dot += q[i] * v;
            }
            return dot;
        }

        public static float[]? GetEmbedding(long frameId)
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT vector FROM frame_embedding WHERE frame_id = $id";
            cmd.Parameters.AddWithValue("$id", frameId);
            var result = cmd.ExecuteScalar();
            if (result == null || result is DBNull) return null;
            return EmbeddingService.BlobToFloatArray((byte[])result);
        }

        /// <summary>
        /// Kasuje wpisy frame + frame_embedding starsze niż cutoff. Zwraca ile rekordów.
        /// </summary>
        public static int DeleteFramesOlderThan(DateTime cutoffUtc)
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM frame_embedding WHERE frame_id IN (SELECT id FROM frame WHERE ts < $cut)";
                cmd.Parameters.AddWithValue("$cut", cutoffUtc.ToString("o"));
                cmd.ExecuteNonQuery();
            }

            int rows;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM frame WHERE ts < $cut";
                cmd.Parameters.AddWithValue("$cut", cutoffUtc.ToString("o"));
                rows = cmd.ExecuteNonQuery();
            }

            tx.Commit();
            return rows;
        }

        public static Dictionary<string, long> CountPerCamera()
        {
            var result = new Dictionary<string, long>();
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT camera_id, COUNT(*) FROM frame GROUP BY camera_id ORDER BY camera_id";
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                result[rdr.GetString(0)] = rdr.GetInt64(1);
            }
            return result;
        }

        /// <summary>
        /// Schema SQL — szukamy w katalogu wyjściowym aplikacji obok exe;
        /// fallback do embedded literal jeśli plik niedostępny (np. w testach).
        /// </summary>
        private static void TryAlter(SqliteConnection conn, string sql)
        {
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
            catch (SqliteException) { /* kolumna już istnieje albo inne pole istnieje */ }
        }

        private static string WczytajSchemaSql()
        {
            var kandydaci = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "CentrumNagranAI", "SQL", "InitCnaDb.sql"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "CentrumNagranAI", "SQL", "InitCnaDb.sql")
            };
            foreach (var k in kandydaci)
                if (File.Exists(k)) return File.ReadAllText(k);

            // Fallback: minimalna schema in-line (gdyby plik nie był w deployu).
            return @"
                CREATE TABLE IF NOT EXISTS camera (
                    id TEXT PRIMARY KEY, name TEXT NOT NULL, host TEXT NOT NULL,
                    channel INTEGER NOT NULL, stream_type INTEGER NOT NULL DEFAULT 0,
                    enabled INTEGER NOT NULL DEFAULT 1, last_seen TEXT);
                CREATE TABLE IF NOT EXISTS frame (
                    id INTEGER PRIMARY KEY AUTOINCREMENT, camera_id TEXT NOT NULL,
                    ts TEXT NOT NULL, file_path TEXT NOT NULL, file_size INTEGER,
                    embedding_status INTEGER NOT NULL DEFAULT 0);
                CREATE INDEX IF NOT EXISTS idx_frame_ts ON frame(ts);
                CREATE INDEX IF NOT EXISTS idx_frame_camera_ts ON frame(camera_id, ts);
                CREATE TABLE IF NOT EXISTS frame_embedding (
                    frame_id INTEGER PRIMARY KEY, dim INTEGER NOT NULL, vector BLOB NOT NULL);
                CREATE TABLE IF NOT EXISTS query_audit (
                    id INTEGER PRIMARY KEY AUTOINCREMENT, ts TEXT NOT NULL, user_id TEXT,
                    query_text TEXT NOT NULL, top_k_ids TEXT, vlm_calls INTEGER DEFAULT 0,
                    vlm_cost_usd REAL DEFAULT 0, duration_ms INTEGER);";
        }
    }
}
