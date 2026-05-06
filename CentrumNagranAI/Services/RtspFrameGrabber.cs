using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kalendarz1.CentrumNagranAI.Services
{
    /// <summary>
    /// Wyciąga pojedyncze klatki JPEG z kamery przez RTSP używając ffmpeg.exe.
    /// Używamy sub-streamu (Hikvision: Channels/CH02), bo:
    ///   1) main-stream zostaje na NVR do zapisu nagrań,
    ///   2) sub jest 4-8x lżejszy w transmisji (kluczowe gdy idzie przez VPN Ubojnia),
    ///   3) dla embeddingów CLIP rozdzielczość 640x360 wystarczy.
    /// </summary>
    public static class RtspFrameGrabber
    {
        /// <summary>
        /// Pobiera 1 klatkę z kamery i zapisuje jako JPEG.
        /// Zwraca pełną ścieżkę do pliku albo rzuca wyjątek z komunikatem ffmpega.
        /// </summary>
        public static async Task<string> ZapiszKlatkeAsync(
            CnaCameraEndpoint kamera,
            string outputPath,
            int timeoutSekund = 15,
            CancellationToken ct = default)
        {
            CnaConfig.ZaladujJesliTrzeba();

            if (string.IsNullOrEmpty(CnaConfig.FfmpegExePath) || !File.Exists(CnaConfig.FfmpegExePath))
                throw new FileNotFoundException(
                    $"Nie znaleziono ffmpeg.exe. Oczekiwana lokalizacja: tools/ffmpeg/.../bin/ffmpeg.exe");

            string rtspUrl = ZbudujRtspUrl(kamera);

            // Tworzymy katalog na klatkę jeśli nie istnieje.
            var outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);

            var psi = new ProcessStartInfo
            {
                FileName = CnaConfig.FfmpegExePath,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            // Argumenty osobno — Process.Start nie pójdzie przez shell, więc znaki specjalne
            // w haśle (np. '$') są bezpieczne.
            psi.ArgumentList.Add("-rtsp_transport"); psi.ArgumentList.Add("tcp");
            psi.ArgumentList.Add("-i");              psi.ArgumentList.Add(rtspUrl);
            psi.ArgumentList.Add("-frames:v");       psi.ArgumentList.Add("1");
            psi.ArgumentList.Add("-q:v");            psi.ArgumentList.Add("5");        // jakość ~70%
            psi.ArgumentList.Add("-vf");             psi.ArgumentList.Add("scale=640:-2"); // szer. 640, wys. proporcjonalnie
            psi.ArgumentList.Add("-y");                                                  // nadpisz
            psi.ArgumentList.Add(outputPath);

            using var proc = new Process { StartInfo = psi };
            var stderrBuilder = new StringBuilder();
            proc.ErrorDataReceived += (s, e) => { if (e.Data != null) stderrBuilder.AppendLine(e.Data); };

            proc.Start();
            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSekund));

            try
            {
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                throw new TimeoutException(
                    $"FFmpeg nie odpowiedział w {timeoutSekund}s dla {kamera.Id} ({kamera.Host}). " +
                    $"Sprawdź sieć/VPN/credentials. Stderr:\n{stderrBuilder}");
            }

            if (proc.ExitCode != 0)
                throw new InvalidOperationException(
                    $"FFmpeg padł (exit {proc.ExitCode}) dla {kamera.Id}. Stderr:\n{stderrBuilder}");

            if (!File.Exists(outputPath) || new FileInfo(outputPath).Length < 100)
                throw new InvalidOperationException(
                    $"FFmpeg zakończył sukcesem, ale plik wyjściowy pusty/za mały: {outputPath}");

            return outputPath;
        }

        /// <summary>
        /// Format URL specyficzny dla NVR Internec i6-N25232UHV (firmware NVR-B3601).
        /// Wzorzec znaleziony przez ONVIF GetStreamUri:
        ///   rtsp://host:port/unicast/c{channel}/s{stream}/live
        /// gdzie stream: 0=main (zawsze dostępny), 1=sub (nie wszystkie kanały).
        /// UWAGA: Hikvision-standardowe ścieżki (/Streaming/Channels/...) na tym
        /// firmware zwracają 401 niezależnie od ścieżki (NVR rzuca 401 bez walidacji).
        /// </summary>
        private static string ZbudujRtspUrl(CnaCameraEndpoint k)
        {
            string user = Uri.EscapeDataString(k.User);
            string pass = Uri.EscapeDataString(k.Password);
            return $"rtsp://{user}:{pass}@{k.Host}:{k.RtspPort}/unicast/c{k.Channel}/s{k.StreamType}/live";
        }
    }
}
