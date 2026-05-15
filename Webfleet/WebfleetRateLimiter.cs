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
        private static readonly Dictionary<string, int> MinIntervalMs = new(StringComparer.OrdinalIgnoreCase)
        {
            // showStandStills ma limit 6 req/min = ~1 co 10s (dokumentacja Webfleet).
            // Wcześniej rozwiązywane batch'em 3 pojazdy + Task.Delay 10s.
            ["showStandStillsExtern"]      = 10_000,

            // showTripSummary jest mniej restrictive; trzymamy 200ms dla bezpieczeństwa
            // (poprzednio 300ms delay między batchami po 3).
            ["showTripSummaryReportExtern"] = 200,
            ["showTripReportExtern"]        = 200,

            // insertDestinationOrder (push do TomTom) — chronimy przed flood'em.
            ["insertDestinationOrderExtern"] = 250,
            ["updateOrderExtern"]            = 250,
            ["deleteOrderExtern"]            = 250,

            // showObjectReport (pozycje GPS) — bez limitu (cache 30s w Fazie 3.3 załatwi).
            // showTrackExtern — bez limitu.
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
