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

            // Heart-beat co 5 min do logu Windows (visible w Event Viewer)
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
