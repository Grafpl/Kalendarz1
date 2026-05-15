using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Kalendarz1.Webfleet
{
    /// <summary>
    /// Centralny rate limiter dla Webfleet.connect API.
    ///
    /// Wcześniejszy stan: rate-limit handlowany ad-hoc w 2 plikach (batch po 3 + Task.Delay).
    /// Teraz: jedna kolejka per endpoint, pre-configured min intervals.
    ///
    /// Pierwszy klient w aktywnym oknie blokuje kolejnych aż minie minInterval.
    /// Wewnątrz aplikacji to wystarczy — Webfleet jest jedynym konsumentem.
    /// </summary>
    public static class WebfleetRateLimiter
    {
        /// <summary>Minimalny interwał (ms) między wywołaniami danego endpointu.</summary>
        /// <remarks>
        /// Nazwy action ZGODNE z faktycznymi stringami w kodzie konsumentów MapaFloty,
        /// nie z dokumentacją Webfleet. StringComparer.OrdinalIgnoreCase, ale i tak warto
        /// trzymać casing zgodny z kodem.
        /// </remarks>
        private static readonly Dictionary<string, int> MinIntervalMs = new(StringComparer.OrdinalIgnoreCase)
        {
            // showStandStills ma limit 6 req/min = ~1 co 10s (dokumentacja Webfleet).
            // Wywoływane z FleetAlertService, OsCzasuFlotyWindow, MonitorKursowWindow.
            ["showStandStills"]                 = 10_000,

            // Trip reports — mniej restrictive; trzymamy 200ms (poprzednio 300ms delay między batchami).
            ["showTripSummaryReportExtern"]     = 200,
            ["showTripReportExtern"]            = 200,

            // Order operations (push do TomTom) — chronimy przed flood'em.
            ["insertDestinationOrderExtern"]    = 250,
            ["updateDestinationOrderExtern"]    = 250,
            ["cancelOrderExtern"]               = 250,

            // showObjectReport (pozycje GPS) — bez limitu, ale cache 30s w Fazie 3.4.
            // showTracks — bez limitu (per-pojazd, rzadko).
            // showOrderReport / showDriverReport — rzadkie, bez limitu.
        };

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();
        private static readonly ConcurrentDictionary<string, DateTime> _lastCallUtc = new();

        /// <summary>
        /// Czeka jeśli trzeba, żeby kolejny call mieścił się w limicie dla danego endpointu.
        /// Działa per-action; różne action'y nie blokują się nawzajem.
        ///
        /// Jeśli action nie ma zdefiniowanego limitu — return natychmiast (brak rate limit).
        /// </summary>
        public static async Task AcquireAsync(string action, CancellationToken ct = default)
        {
            if (!MinIntervalMs.TryGetValue(action, out var minMs) || minMs <= 0)
                return;

            var sem = _semaphores.GetOrAdd(action, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_lastCallUtc.TryGetValue(action, out var last))
                {
                    var elapsed = (DateTime.UtcNow - last).TotalMilliseconds;
                    var waitMs = minMs - (int)elapsed;
                    if (waitMs > 0)
                        await Task.Delay(waitMs, ct).ConfigureAwait(false);
                }
                _lastCallUtc[action] = DateTime.UtcNow;
            }
            finally
            {
                sem.Release();
            }
        }
    }
}
