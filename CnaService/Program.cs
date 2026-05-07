using System;
using System.Threading;
using System.Threading.Tasks;
using Kalendarz1.CentrumNagranAI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;

namespace Kalendarz1.CnaService;

/// <summary>
/// Windows Service uruchamiający Indexer + Backfill 24/7 niezależnie od ZPSP.
///
/// Instalacja jako usługa Windows:
///   sc.exe create "ZPSP-CNA" binPath= "C:\path\to\Kalendarz1.CnaService.exe" start= auto
///   sc.exe start "ZPSP-CNA"
///
/// Albo skrypt install_service.ps1 (w tym samym folderze).
///
/// Uruchamiany manualnie do testów: po prostu `Kalendarz1.CnaService.exe` z konsoli -
/// chodzi w foreground, Ctrl+C zamyka. Jako service - zarządzany przez SCM.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddHostedService<CnaWorker>();
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "ZPSP-CNA";
        });

        var host = builder.Build();
        await host.RunAsync();
    }
}

/// <summary>
/// Hostowany serwis: startuje Indexer + Backfill, czeka na cancel, zamyka.
/// </summary>
public class CnaWorker : BackgroundService
{
    private readonly ILogger<CnaWorker> _logger;

    public CnaWorker(ILogger<CnaWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ZPSP-CNA worker startuje. BaseDir={BaseDir}", CnaConfig.BaseDir);

        try
        {
            CnaConfig.ZaladujJesliTrzeba();
            FrameIndex.Init();
            _logger.LogInformation("Konfiguracja: kamer={Cnt}, klucz Anthropic={Anth}, OpenAI={OAI}",
                CnaConfig.Kamery.Count,
                string.IsNullOrEmpty(CnaConfig.AnthropicApiKey) ? "BRAK" : "OK",
                EmbeddingService.IsConfigured ? "OK" : "BRAK");

            await IndexerBackgroundService.Instance.StartAsync();
            await EmbeddingBackfillService.Instance.StartAsync();

            DateTime lastBriefDay = DateTime.MinValue;
            DateTime lastBaselineRebuild = DateTime.MinValue;

            // Heart-beat co 5 min + auto-brief o 17:00 + auto-rebuild baseline raz na dobę.
            while (!stoppingToken.IsCancellationRequested)
            {
                long count = FrameIndex.CountFrames();
                var (total, withEmb) = FrameIndex.GetEmbeddingStats();
                _logger.LogInformation("Heartbeat: klatek={Count}, embedingów={WithEmb}/{Total}, " +
                    "indexer cykli={Cycles}, backfill processed={Proc} (cost ${Cost:F2})",
                    count, withEmb, total,
                    IndexerBackgroundService.Instance.Cycles,
                    EmbeddingBackfillService.Instance.Processed,
                    EmbeddingBackfillService.Instance.TotalCostUsd);

                var localNow = DateTime.Now;
                var today = localNow.Date;

                // Auto-brief o 17:00 (raz dziennie). Jeśli serwis wystartował po 17 - generujemy od razu.
                if (lastBriefDay < today && localNow.Hour >= 17)
                {
                    try
                    {
                        _logger.LogInformation("Generuję dzisiejszy brief...");
                        var b = await DailyBriefService.GenerateAsync(today, ct: stoppingToken);
                        lastBriefDay = today;
                        _logger.LogInformation("Brief {Day} gotowy (${Cost:F4})", b.Day, b.CostUsd);
                    }
                    catch (Exception ex) { _logger.LogWarning(ex, "Brief generation fail"); }
                }

                // Auto-rebuild baseline raz na 24h (zmienia się rytm, sezonowość).
                if ((localNow - lastBaselineRebuild).TotalHours >= 24)
                {
                    try
                    {
                        AnomalyService.RebuildBaseline(7);
                        lastBaselineRebuild = localNow;
                        _logger.LogInformation("Baseline anomalii przebudowany (z 7 dni).");
                    }
                    catch (Exception ex) { _logger.LogWarning(ex, "Baseline rebuild fail"); }
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
        catch (TaskCanceledException) { /* normalne zamknięcie */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ZPSP-CNA worker padł");
        }
        finally
        {
            _logger.LogInformation("Zatrzymuję IndexerBackgroundService + EmbeddingBackfillService");
            IndexerBackgroundService.Instance.Stop();
            EmbeddingBackfillService.Instance.Stop();
        }
    }
}
