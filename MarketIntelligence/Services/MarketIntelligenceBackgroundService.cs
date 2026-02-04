using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Kalendarz1.MarketIntelligence.Database;

namespace Kalendarz1.MarketIntelligence.Services
{
    /// <summary>
    /// Background service dla automatycznego pobierania newsów
    /// Uruchamiany przy starcie aplikacji, wykonuje codzienne zadania
    /// </summary>
    public class MarketIntelligenceBackgroundService : IDisposable
    {
        private readonly NewsFetchOrchestrator _orchestrator;
        private readonly DatabaseSetup _databaseSetup;
        private System.Timers.Timer _dailyTimer;
        private System.Timers.Timer _priceTimer;
        private System.Timers.Timer _hpaiTimer;
        private CancellationTokenSource _cts;

        private DateTime _lastDailyFetch = DateTime.MinValue;
        private DateTime _lastPriceFetch = DateTime.MinValue;
        private DateTime _lastHpaiFetch = DateTime.MinValue;

        private bool _isRunning;

        // Configuration
        public TimeSpan DailyFetchTime { get; set; } = new TimeSpan(5, 0, 0); // 5:00 AM
        public TimeSpan PriceFetchInterval { get; set; } = TimeSpan.FromMinutes(15);
        public TimeSpan HpaiFetchInterval { get; set; } = TimeSpan.FromHours(2);

        // Singleton
        private static MarketIntelligenceBackgroundService _instance;
        private static readonly object _lock = new object();

        public static MarketIntelligenceBackgroundService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new MarketIntelligenceBackgroundService();
                    }
                }
                return _instance;
            }
        }

        public MarketIntelligenceBackgroundService(string connectionString = null, string claudeApiKey = null)
        {
            _orchestrator = new NewsFetchOrchestrator(connectionString, claudeApiKey);
            _databaseSetup = new DatabaseSetup(connectionString);
            _cts = new CancellationTokenSource();
        }

        #region Service Lifecycle

        /// <summary>
        /// Uruchom background service
        /// </summary>
        public async Task StartAsync()
        {
            if (_isRunning) return;

            Debug.WriteLine("[BackgroundService] Starting Market Intelligence background service...");

            // Ensure database tables exist
            await _databaseSetup.EnsureTablesExistAsync();

            // Setup timers
            SetupDailyTimer();
            SetupPriceTimer();
            SetupHpaiTimer();

            _isRunning = true;

            // If it's morning and we haven't fetched today, do initial fetch
            var now = DateTime.Now;
            if (now.TimeOfDay > DailyFetchTime &&
                _lastDailyFetch.Date < now.Date)
            {
                Debug.WriteLine("[BackgroundService] Triggering initial daily fetch...");
                _ = Task.Run(() => ExecuteDailyFetchAsync());
            }

            Debug.WriteLine("[BackgroundService] Background service started");
        }

        /// <summary>
        /// Zatrzymaj background service
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;

            Debug.WriteLine("[BackgroundService] Stopping background service...");

            _cts.Cancel();
            _dailyTimer?.Stop();
            _priceTimer?.Stop();
            _hpaiTimer?.Stop();

            _isRunning = false;

            Debug.WriteLine("[BackgroundService] Background service stopped");
        }

        #endregion

        #region Timer Setup

        private void SetupDailyTimer()
        {
            _dailyTimer = new System.Timers.Timer();

            // Calculate time until next daily fetch
            var now = DateTime.Now;
            var nextFetch = now.Date.Add(DailyFetchTime);
            if (nextFetch <= now)
            {
                nextFetch = nextFetch.AddDays(1);
            }

            var timeUntilFetch = nextFetch - now;
            _dailyTimer.Interval = timeUntilFetch.TotalMilliseconds;
            _dailyTimer.Elapsed += OnDailyTimerElapsed;
            _dailyTimer.AutoReset = false; // We'll reset it manually for next day
            _dailyTimer.Start();

            Debug.WriteLine($"[BackgroundService] Daily timer set for {nextFetch:HH:mm} ({timeUntilFetch.TotalHours:F1}h from now)");
        }

        private void SetupPriceTimer()
        {
            _priceTimer = new System.Timers.Timer(PriceFetchInterval.TotalMilliseconds);
            _priceTimer.Elapsed += OnPriceTimerElapsed;
            _priceTimer.AutoReset = true;
            _priceTimer.Start();

            Debug.WriteLine($"[BackgroundService] Price timer set for every {PriceFetchInterval.TotalMinutes} minutes");
        }

        private void SetupHpaiTimer()
        {
            _hpaiTimer = new System.Timers.Timer(HpaiFetchInterval.TotalMilliseconds);
            _hpaiTimer.Elapsed += OnHpaiTimerElapsed;
            _hpaiTimer.AutoReset = true;
            _hpaiTimer.Start();

            Debug.WriteLine($"[BackgroundService] HPAI timer set for every {HpaiFetchInterval.TotalHours} hours");
        }

        #endregion

        #region Timer Handlers

        private async void OnDailyTimerElapsed(object sender, ElapsedEventArgs e)
        {
            await ExecuteDailyFetchAsync();

            // Reset timer for next day
            _dailyTimer.Interval = TimeSpan.FromDays(1).TotalMilliseconds;
            _dailyTimer.Start();
        }

        private async void OnPriceTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // Only fetch prices during business hours (8:00 - 18:00)
            var now = DateTime.Now;
            if (now.Hour >= 8 && now.Hour <= 18 && now.DayOfWeek != DayOfWeek.Sunday)
            {
                await ExecutePriceFetchAsync();
            }
        }

        private async void OnHpaiTimerElapsed(object sender, ElapsedEventArgs e)
        {
            await ExecuteHpaiFetchAsync();
        }

        #endregion

        #region Fetch Operations

        /// <summary>
        /// Pełne codzienne pobieranie (RSS, scraping, AI)
        /// </summary>
        private async Task ExecuteDailyFetchAsync()
        {
            if (_cts.IsCancellationRequested) return;

            Debug.WriteLine("[BackgroundService] Starting daily fetch...");

            try
            {
                var result = await _orchestrator.FullFetchAsync(_cts.Token);

                if (result.Success)
                {
                    _lastDailyFetch = DateTime.Now;
                    Debug.WriteLine($"[BackgroundService] Daily fetch completed: {result.Statistics?.RelevantArticles} articles");

                    // Fire event for UI update
                    OnDailyFetchCompleted?.Invoke(this, result);
                }
                else
                {
                    Debug.WriteLine($"[BackgroundService] Daily fetch failed: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BackgroundService] Daily fetch error: {ex.Message}");
            }
        }

        /// <summary>
        /// Szybkie pobieranie cen (NBP, MATIF)
        /// </summary>
        private async Task ExecutePriceFetchAsync()
        {
            if (_cts.IsCancellationRequested) return;

            Debug.WriteLine("[BackgroundService] Fetching prices...");

            try
            {
                using var scraper = new DataSources.WebScraperService();

                // Fetch commodity prices (includes NBP via ContextBuilder)
                var commodityPrices = await scraper.FetchCommodityPricesAsync(_cts.Token);

                _lastPriceFetch = DateTime.Now;
                Debug.WriteLine($"[BackgroundService] Price fetch completed: {commodityPrices.Count} prices");

                // Fire event
                OnPricesFetched?.Invoke(this, commodityPrices);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BackgroundService] Price fetch error: {ex.Message}");
            }
        }

        /// <summary>
        /// Sprawdzenie alertów HPAI
        /// </summary>
        private async Task ExecuteHpaiFetchAsync()
        {
            if (_cts.IsCancellationRequested) return;

            Debug.WriteLine("[BackgroundService] Checking HPAI alerts...");

            try
            {
                using var scraper = new DataSources.WebScraperService();
                var alerts = await scraper.FetchHpaiAlertsAsync(_cts.Token);

                _lastHpaiFetch = DateTime.Now;

                // Check for new critical alerts
                var criticalAlerts = alerts.FindAll(a => a.Severity == "Critical");

                if (criticalAlerts.Count > 0)
                {
                    Debug.WriteLine($"[BackgroundService] HPAI: Found {criticalAlerts.Count} critical alerts!");

                    // Check if any are in łódzkie (our region)
                    var localAlerts = criticalAlerts.FindAll(a =>
                        a.Voivodeship?.ToLower() == "łódzkie" ||
                        a.Location?.ToLower().Contains("brzezin") == true);

                    if (localAlerts.Count > 0)
                    {
                        Debug.WriteLine("[BackgroundService] WARNING: HPAI alerts in łódzkie region!");
                        OnCriticalHpaiAlert?.Invoke(this, localAlerts);
                    }
                }

                OnHpaiAlertsFetched?.Invoke(this, alerts);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BackgroundService] HPAI fetch error: {ex.Message}");
            }
        }

        #endregion

        #region Manual Triggers

        /// <summary>
        /// Wymuszony natychmiastowy daily fetch
        /// </summary>
        public async Task<FetchResult> TriggerDailyFetchAsync()
        {
            Debug.WriteLine("[BackgroundService] Manual daily fetch triggered");
            return await _orchestrator.FullFetchAsync(_cts.Token);
        }

        /// <summary>
        /// Wymuszony natychmiastowy price fetch
        /// </summary>
        public async Task TriggerPriceFetchAsync()
        {
            Debug.WriteLine("[BackgroundService] Manual price fetch triggered");
            await ExecutePriceFetchAsync();
        }

        /// <summary>
        /// Wymuszony natychmiastowy HPAI check
        /// </summary>
        public async Task TriggerHpaiFetchAsync()
        {
            Debug.WriteLine("[BackgroundService] Manual HPAI fetch triggered");
            await ExecuteHpaiFetchAsync();
        }

        #endregion

        #region Events

        /// <summary>
        /// Event wywoływany po zakończeniu daily fetch
        /// </summary>
        public event EventHandler<FetchResult> OnDailyFetchCompleted;

        /// <summary>
        /// Event wywoływany po pobraniu cen
        /// </summary>
        public event EventHandler<System.Collections.Generic.List<DataSources.CommodityPrice>> OnPricesFetched;

        /// <summary>
        /// Event wywoływany po sprawdzeniu alertów HPAI
        /// </summary>
        public event EventHandler<System.Collections.Generic.List<DataSources.HpaiAlert>> OnHpaiAlertsFetched;

        /// <summary>
        /// Event wywoływany gdy wykryto krytyczny alert HPAI w regionie
        /// </summary>
        public event EventHandler<System.Collections.Generic.List<DataSources.HpaiAlert>> OnCriticalHpaiAlert;

        #endregion

        #region Status

        public bool IsRunning => _isRunning;
        public DateTime LastDailyFetch => _lastDailyFetch;
        public DateTime LastPriceFetch => _lastPriceFetch;
        public DateTime LastHpaiFetch => _lastHpaiFetch;

        public string GetStatusReport()
        {
            return $@"Market Intelligence Background Service
=====================================
Status: {(IsRunning ? "Running" : "Stopped")}
Last Daily Fetch: {(_lastDailyFetch == DateTime.MinValue ? "Never" : _lastDailyFetch.ToString("yyyy-MM-dd HH:mm"))}
Last Price Fetch: {(_lastPriceFetch == DateTime.MinValue ? "Never" : _lastPriceFetch.ToString("yyyy-MM-dd HH:mm"))}
Last HPAI Check: {(_lastHpaiFetch == DateTime.MinValue ? "Never" : _lastHpaiFetch.ToString("yyyy-MM-dd HH:mm"))}

Schedule:
- Daily fetch: {DailyFetchTime:hh\\:mm} every day
- Price updates: every {PriceFetchInterval.TotalMinutes} minutes (8:00-18:00)
- HPAI checks: every {HpaiFetchInterval.TotalHours} hours";
        }

        #endregion

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
            _dailyTimer?.Dispose();
            _priceTimer?.Dispose();
            _hpaiTimer?.Dispose();
            _orchestrator?.Dispose();
        }
    }
}
