using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kalendarz1.CentrumNagranAI.Services;
using Microsoft.Data.Sqlite;

namespace Kalendarz1.CentrumNagranAI.Test
{
    /// <summary>
    /// Lokalne narzędzie deweloperskie. Uruchamia Indexer na N sekund i wypisuje wynik.
    /// Wywoływane z App.OnStartup gdy command-line zawiera "--cna-test [seconds]".
    /// Po zakończeniu Application.Shutdown — proces wychodzi.
    /// </summary>
    public static class CnaSelfTest
    {
        public static async Task RunAsync(int seconds = 30)
        {
            string outFile = Path.Combine(CnaConfig.AuditDir, "cna_selftest.log");
            Directory.CreateDirectory(CnaConfig.AuditDir);

            void W(string s)
            {
                string line = $"{DateTime.Now:HH:mm:ss.fff} {s}";
                Console.WriteLine(line);
                System.Diagnostics.Debug.WriteLine(line);
                File.AppendAllText(outFile, line + Environment.NewLine, System.Text.Encoding.UTF8);
            }

            W("===== CNA Self-Test START =====");
            CnaConfig.ZaladujJesliTrzeba();
            W($"Konfiguracja:");
            W($"  BaseDir         = {CnaConfig.BaseDir}");
            W($"  DbPath          = {CnaConfig.DbPath}");
            W($"  FramesDir       = {CnaConfig.FramesDir}");
            W($"  FfmpegExePath   = {CnaConfig.FfmpegExePath}");
            W($"  AnthropicApiKey = {(string.IsNullOrEmpty(CnaConfig.AnthropicApiKey) ? "BRAK" : "ustawiony (" + CnaConfig.AnthropicApiKey.Length + " znaków)")}");
            W($"  Kamer           = {CnaConfig.Kamery.Count}");
            foreach (var k in CnaConfig.Kamery)
                W($"    - {k.Id} {k.Host}:{k.RtspPort} ch={k.Channel} stream={k.StreamType}");

            FrameIndex.Init();
            long countBefore = FrameIndex.CountFrames();
            W($"  COUNT(frame) PRZED = {countBefore}");

            await IndexerBackgroundService.Instance.StartAsync();
            W($"Indexer uruchomiony. Test trwa {seconds}s...");

            await Task.Delay(TimeSpan.FromSeconds(seconds));

            IndexerBackgroundService.Instance.Stop();

            long countAfter = FrameIndex.CountFrames();
            W($"  COUNT(frame) PO    = {countAfter}");
            W($"  RÓŻNICA            = {countAfter - countBefore}");

            W("Klatek per kamera:");
            foreach (var (cam, n) in FrameIndex.CountPerCamera())
                W($"  {cam}: {n}");

            W("===== CNA Self-Test KONIEC =====");
        }

        /// <summary>
        /// Hello-world VLM: bierze najnowszą klatkę z bazy, pyta Claude Haiku po polsku,
        /// wypisuje odpowiedź + koszt. Wymaga klucza Anthropic w secrets.json.
        /// </summary>
        public static async Task RunVlmHelloAsync()
        {
            string outFile = Path.Combine(CnaConfig.AuditDir, "cna_selftest.log");
            Directory.CreateDirectory(CnaConfig.AuditDir);

            void W(string s)
            {
                string line = $"{DateTime.Now:HH:mm:ss.fff} {s}";
                Console.WriteLine(line);
                System.Diagnostics.Debug.WriteLine(line);
                File.AppendAllText(outFile, line + Environment.NewLine, System.Text.Encoding.UTF8);
            }

            W("===== CNA VLM Hello-World START =====");
            CnaConfig.ZaladujJesliTrzeba();
            FrameIndex.Init();

            // Wybierz najnowszą klatkę z bazy.
            string? jpegPath = null;
            string? cameraId = null;
            string? ts = null;
            using (var conn = new SqliteConnection($"Data Source={CnaConfig.DbPath}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT camera_id, ts, file_path FROM frame ORDER BY id DESC LIMIT 1";
                using var rdr = cmd.ExecuteReader();
                if (rdr.Read())
                {
                    cameraId = rdr.GetString(0);
                    ts = rdr.GetString(1);
                    jpegPath = rdr.GetString(2);
                }
            }

            if (jpegPath == null || !File.Exists(jpegPath))
            {
                W($"BRAK klatki w bazie albo plik nie istnieje. Najpierw uruchom --cna-test żeby zaindeksować.");
                W($"  jpegPath = {jpegPath ?? "(null)"}");
                return;
            }

            W($"Klatka: {cameraId} {ts} -> {jpegPath}");
            W($"Rozmiar: {new FileInfo(jpegPath).Length / 1024} KB");

            string prompt =
                "Opisz po polsku co widzisz na tym zdjęciu z kamery przemysłowej zakładu drobiarskiego (ubojnia kurczaków). " +
                "Wymień widoczne obiekty, ludzi (jeśli są), aktywność, ewentualne anomalie. " +
                "Odpowiedz w 2-3 zdaniach, konkretnie i bez ozdobników.";

            W($"Prompt: {prompt}");
            W("Wysyłam do Claude Haiku 4.5...");

            try
            {
                var result = await VlmClient.AnalyzeImageAsync(jpegPath, prompt);
                W("--- ODPOWIEDŹ ---");
                W(result.Text);
                W("------------------");
                W($"Tokeny: in={result.InputTokens} out={result.OutputTokens}");
                W($"Czas:   {result.DurationMs}ms");
                W($"Koszt:  ${result.CostUsd:F4} USD");
            }
            catch (Exception ex)
            {
                W($"BŁĄD: {ex.GetType().Name}: {ex.Message}");
            }

            W("===== CNA VLM Hello-World KONIEC =====");
        }

        /// <summary>
        /// End-to-end search: polskie zapytanie → top-N klatek z opisami i score.
        /// Wymaga klatek w bazie (uruchom najpierw --cna-test).
        /// </summary>
        public static async Task RunSearchAsync(string polishQuery, int topN = 5)
        {
            string outFile = Path.Combine(CnaConfig.AuditDir, "cna_selftest.log");
            void W(string s)
            {
                string line = $"{DateTime.Now:HH:mm:ss.fff} {s}";
                Console.WriteLine(line);
                System.Diagnostics.Debug.WriteLine(line);
                File.AppendAllText(outFile, line + Environment.NewLine, System.Text.Encoding.UTF8);
            }

            W("===== CNA Search START =====");
            W($"Zapytanie: \"{polishQuery}\"");
            W($"Top N:     {topN}");
            CnaConfig.ZaladujJesliTrzeba();
            FrameIndex.Init();
            long count = FrameIndex.CountFrames();
            W($"Klatek w bazie: {count}");
            if (count == 0) { W("BRAK klatek. Uruchom najpierw --cna-test"); return; }

            var summary = await SearchService.SearchAsync(polishQuery, topN: topN);

            W($"Kandydatów oceniono: {summary.Candidates}");
            W($"VLM calls:           {summary.VlmCalls}");
            W($"Koszt całkowity:     ${summary.TotalCostUsd:F4} USD");
            W($"Czas:                {summary.DurationMs}ms");
            W($"Audit ID:            {summary.AuditId}");
            W("");
            W($"--- TOP {summary.Hits.Count} ---");
            int rank = 1;
            foreach (var h in summary.Hits)
            {
                W($"#{rank}  score={h.Score,3}  {h.CameraId}  {h.TsUtc:yyyy-MM-dd HH:mm:ss}");
                W($"     {h.Reason}");
                W($"     {h.FilePath}");
                rank++;
            }
            W("===== CNA Search KONIEC =====");
        }
    }
}
