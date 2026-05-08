using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Kalendarz1.CentrumNagranAI.Services
{
    /// <summary>
    /// Detekcja tablic rejestracyjnych w klatkach przez VLM (Haiku).
    /// Zamiast lokalnego OCR (Tesseract / PaddleOCR) używamy Haiku z konkretnym promptem -
    /// jakość lepsza, multilingual, koszt ~$0.001 per klatka.
    ///
    /// Domyślnie sprawdzamy TYLKO klatki z kamer rampa/brama (filter w secrets/settings).
    /// Inaczej koszt 100k klatek × $0.001 = $100 niepotrzebnie.
    /// </summary>
    public static class PlateDetectionService
    {
        private static string ConnString =>
            $"Data Source={CnaConfig.DbPath};Cache=Shared;Foreign Keys=True";

        // Polskie tablice: [A-Z]{1,3} ?[A-Z0-9]{3,5} (np. WK 12345, WPI 5G23, WX 7H88).
        // Akceptujemy też tablice z dwucyfrowym wojew np. WK1234E.
        private static readonly Regex PlateRegex = new(
            @"\b[A-Z]{1,3}\s?[A-Z0-9]{3,6}\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private const string Prompt =
            "Wypisz wszystkie tablice rejestracyjne pojazdów widoczne na tym zdjęciu z kamery CCTV. " +
            "Dla każdej tablicy podaj DOKŁADNIE odczytany numer w formacie polskim (np. 'WK 12345', 'WPI 5G23'). " +
            "Jeśli żadnej tablicy nie ma, odpowiedz słowem: BRAK. " +
            "NIE komentuj, NIE dodawaj słów typu 'widzę' albo 'na zdjęciu'. " +
            "Tylko lista tablic, każda w nowej linii. Jeśli tablica jest niewyraźna - dodaj na końcu '?'.";

        /// <summary>
        /// Sprawdź klatkę pod kątem tablic. Zwraca listę znalezionych (znormalizowanych) tablic.
        /// </summary>
        public static async Task<List<string>> DetectAsync(string jpegPath, CancellationToken ct = default)
        {
            var result = await VlmClient.AnalyzeImageAsync(
                jpegPath, Prompt,
                model: VlmClient.ModelHaiku,
                maxTokens: 200,
                ct: ct);

            return ExtractPlates(result.Text);
        }

        /// <summary>
        /// D1 polished: Multi-frame voting w JEDNYM VLM call (3× tańsze niż osobne calls).
        /// 3 klatki idą do Haiku w pojedynczym requeście, prompt każe wypisać tablice z każdej osobno.
        /// </summary>
        public static async Task<List<string>> DetectWithVotingAsync(long frameId, CancellationToken ct = default)
        {
            var frames = FrameIndex.GetNeighbours(frameId, countBefore: 1, countAfter: 1);
            if (frames.Count == 0) return new List<string>();

            var paths = frames.Select(f => f.FilePath).Where(File.Exists).ToList();
            if (paths.Count == 0) return new List<string>();
            if (paths.Count == 1) return await DetectAsync(paths[0], ct);

            string votingPrompt =
                $"Otrzymałeś {paths.Count} kolejnych klatek z tej samej kamery (różnica ~10 sekund). " +
                "Dla KAŻDEJ klatki osobno wypisz wszystkie polskie tablice rejestracyjne. " +
                "Format dokładnie taki (jedna linia na klatkę):\n" +
                "K1: WK 12345, WPI 5G23\n" +
                "K2: BRAK\n" +
                "K3: WK 12345\n" +
                "NIC poza tym formatem.";

            try
            {
                var result = await VlmClient.AnalyzeMultiImageAsync(
                    paths, votingPrompt,
                    model: VlmClient.ModelHaiku,
                    maxTokens: 250,
                    ct: ct);

                var perFrame = new List<List<string>>();
                foreach (Match m in Regex.Matches(result.Text, @"K\d+\s*:\s*([^\r\n]*)"))
                    perFrame.Add(ExtractPlates(m.Groups[1].Value));
                if (perFrame.Count == 0) perFrame.Add(ExtractPlates(result.Text));

                int requiredVotes = perFrame.Count >= 2 ? 2 : 1;
                var counts = new Dictionary<string, int>();
                foreach (var list in perFrame)
                    foreach (var p in list)
                    {
                        if (!counts.ContainsKey(p)) counts[p] = 0;
                        counts[p]++;
                    }
                return counts.Where(kv => kv.Value >= requiredVotes).Select(kv => kv.Key).ToList();
            }
            catch (Exception ex)
            {
                Log($"voting fail (frame {frameId}): {ex.Message.Split('\n')[0]} — fallback single-frame");
                return await DetectAsync(paths[paths.Count / 2], ct);
            }
        }

        private static List<string> ExtractPlates(string raw)
        {
            raw = raw.Trim();
            if (raw.IndexOf("BRAK", StringComparison.OrdinalIgnoreCase) >= 0)
                return new List<string>();

            var plates = new List<string>();
            foreach (Match m in PlateRegex.Matches(raw.ToUpperInvariant()))
            {
                var p = NormalizePlate(m.Value);
                if (p.Length >= 4 && !plates.Contains(p)) plates.Add(p);
            }
            return plates;
        }

        public static void SavePlates(long frameId, string cameraId, DateTime tsUtc, List<string> plates, string rawText)
        {
            if (plates.Count == 0) return;
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            foreach (var p in plates)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO plate_detection (frame_id, camera_id, ts, plate, raw_text)
                    VALUES ($f, $c, $t, $p, $raw)";
                cmd.Parameters.AddWithValue("$f", frameId);
                cmd.Parameters.AddWithValue("$c", cameraId);
                cmd.Parameters.AddWithValue("$t", tsUtc.ToString("o"));
                cmd.Parameters.AddWithValue("$p", p);
                cmd.Parameters.AddWithValue("$raw", rawText ?? "");
                cmd.ExecuteNonQuery();
            }
            Log($"frame {frameId} kamera={cameraId}: {plates.Count} tablic [{string.Join(", ", plates)}]");
        }

        public static List<(long FrameId, string Plate, DateTime Ts, string CameraId, string FilePath)> SearchByPlate(string plateQuery, int limit = 50)
        {
            var norm = NormalizePlate(plateQuery.ToUpperInvariant());
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT p.frame_id, p.plate, p.ts, p.camera_id, f.file_path
                FROM plate_detection p
                INNER JOIN frame f ON f.id = p.frame_id
                WHERE p.plate LIKE $q OR REPLACE(p.plate,' ','') LIKE $qNoSpace
                ORDER BY p.ts DESC LIMIT $lim";
            cmd.Parameters.AddWithValue("$q", "%" + norm + "%");
            cmd.Parameters.AddWithValue("$qNoSpace", "%" + norm.Replace(" ", "") + "%");
            cmd.Parameters.AddWithValue("$lim", limit);
            var result = new List<(long, string, DateTime, string, string)>();
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                result.Add((
                    rdr.GetInt64(0),
                    rdr.GetString(1),
                    DateTime.Parse(rdr.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    rdr.GetString(3),
                    rdr.GetString(4)
                ));
            }
            return result;
        }

        private static string NormalizePlate(string p)
        {
            // Usuń wielokrotne spacje, zostaw jedną pomiędzy literami a cyframi.
            var clean = Regex.Replace(p.Trim(), @"\s+", " ").ToUpperInvariant();
            return clean;
        }

        private static void Log(string msg)
        {
            string line = $"{DateTime.Now:HH:mm:ss.fff} [CNA-Plate] {msg}";
            Debug.WriteLine(line);
            try
            {
                Directory.CreateDirectory(CnaConfig.AuditDir);
                File.AppendAllText(Path.Combine(CnaConfig.AuditDir, "cna_plate.log"),
                    line + Environment.NewLine, System.Text.Encoding.UTF8);
            }
            catch { }
        }
    }
}
