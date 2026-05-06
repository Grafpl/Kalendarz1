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
