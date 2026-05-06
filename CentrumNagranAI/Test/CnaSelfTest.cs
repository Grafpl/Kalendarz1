using System;
using System.IO;
using System.Threading.Tasks;
using Kalendarz1.CentrumNagranAI.Services;

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
    }
}
