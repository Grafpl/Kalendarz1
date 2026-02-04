using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kalendarz1.MarketIntelligence.Models
{
    /// <summary>
    /// Informacje diagnostyczne o pipeline'ie pobierania danych
    /// </summary>
    public class DiagnosticInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region API Configuration Status

        private bool _isClaudeConfigured;
        public bool IsClaudeConfigured
        {
            get => _isClaudeConfigured;
            set { _isClaudeConfigured = value; OnPropertyChanged(); OnPropertyChanged(nameof(ClaudeStatusIcon)); }
        }

        private string _claudeApiKeyPreview;
        public string ClaudeApiKeyPreview
        {
            get => _claudeApiKeyPreview;
            set { _claudeApiKeyPreview = value; OnPropertyChanged(); }
        }

        private string _claudeModel;
        public string ClaudeModel
        {
            get => _claudeModel;
            set { _claudeModel = value; OnPropertyChanged(); }
        }

        private bool _isPerplexityConfigured;
        public bool IsPerplexityConfigured
        {
            get => _isPerplexityConfigured;
            set { _isPerplexityConfigured = value; OnPropertyChanged(); OnPropertyChanged(nameof(PerplexityStatusIcon)); }
        }

        private string _perplexityApiKeyPreview;
        public string PerplexityApiKeyPreview
        {
            get => _perplexityApiKeyPreview;
            set { _perplexityApiKeyPreview = value; OnPropertyChanged(); }
        }

        private bool _isDatabaseConnected;
        public bool IsDatabaseConnected
        {
            get => _isDatabaseConnected;
            set { _isDatabaseConnected = value; OnPropertyChanged(); OnPropertyChanged(nameof(DatabaseStatusIcon)); }
        }

        private string _databaseServer;
        public string DatabaseServer
        {
            get => _databaseServer;
            set { _databaseServer = value; OnPropertyChanged(); }
        }

        public string ClaudeStatusIcon => IsClaudeConfigured ? "OK" : "BRAK";
        public string PerplexityStatusIcon => IsPerplexityConfigured ? "OK" : "BRAK";
        public string DatabaseStatusIcon => IsDatabaseConnected ? "OK" : "BRAK";

        #endregion

        #region Pipeline Statistics

        private int _rssArticlesCount;
        public int RssArticlesCount
        {
            get => _rssArticlesCount;
            set { _rssArticlesCount = value; OnPropertyChanged(); }
        }

        private TimeSpan _rssTime;
        public TimeSpan RssTime
        {
            get => _rssTime;
            set { _rssTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(RssTimeDisplay)); }
        }
        public string RssTimeDisplay => $"{RssTime.TotalSeconds:N1}s";

        private int _scrapingArticlesCount;
        public int ScrapingArticlesCount
        {
            get => _scrapingArticlesCount;
            set { _scrapingArticlesCount = value; OnPropertyChanged(); }
        }

        private TimeSpan _scrapingTime;
        public TimeSpan ScrapingTime
        {
            get => _scrapingTime;
            set { _scrapingTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(ScrapingTimeDisplay)); }
        }
        public string ScrapingTimeDisplay => $"{ScrapingTime.TotalSeconds:N1}s";

        private int _perplexityArticlesCount;
        public int PerplexityArticlesCount
        {
            get => _perplexityArticlesCount;
            set { _perplexityArticlesCount = value; OnPropertyChanged(); }
        }

        private int _perplexityQueriesTotal;
        public int PerplexityQueriesTotal
        {
            get => _perplexityQueriesTotal;
            set { _perplexityQueriesTotal = value; OnPropertyChanged(); }
        }

        private int _perplexityQueriesCompleted;
        public int PerplexityQueriesCompleted
        {
            get => _perplexityQueriesCompleted;
            set { _perplexityQueriesCompleted = value; OnPropertyChanged(); OnPropertyChanged(nameof(PerplexityProgressDisplay)); }
        }
        public string PerplexityProgressDisplay => $"{PerplexityQueriesCompleted}/{PerplexityQueriesTotal}";

        private TimeSpan _perplexityTime;
        public TimeSpan PerplexityTime
        {
            get => _perplexityTime;
            set { _perplexityTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(PerplexityTimeDisplay)); }
        }
        public string PerplexityTimeDisplay => $"{PerplexityTime.TotalSeconds:N1}s";

        private int _totalUniqueArticles;
        public int TotalUniqueArticles
        {
            get => _totalUniqueArticles;
            set { _totalUniqueArticles = value; OnPropertyChanged(); }
        }

        private int _translatedCount;
        public int TranslatedCount
        {
            get => _translatedCount;
            set { _translatedCount = value; OnPropertyChanged(); }
        }

        private int _filteredCount;
        public int FilteredCount
        {
            get => _filteredCount;
            set { _filteredCount = value; OnPropertyChanged(); }
        }

        private int _aiFilteredCount;
        public int AiFilteredCount
        {
            get => _aiFilteredCount;
            set { _aiFilteredCount = value; OnPropertyChanged(); }
        }

        private int _enrichedCount;
        public int EnrichedCount
        {
            get => _enrichedCount;
            set { _enrichedCount = value; OnPropertyChanged(); }
        }

        private int _enrichmentFailedCount;
        public int EnrichmentFailedCount
        {
            get => _enrichmentFailedCount;
            set { _enrichmentFailedCount = value; OnPropertyChanged(); }
        }

        private int _analyzedCount;
        public int AnalyzedCount
        {
            get => _analyzedCount;
            set { _analyzedCount = value; OnPropertyChanged(); }
        }

        private int _savedToDbCount;
        public int SavedToDbCount
        {
            get => _savedToDbCount;
            set { _savedToDbCount = value; OnPropertyChanged(); }
        }

        #endregion

        #region Cost Estimation

        private decimal _perplexityCostEstimate;
        public decimal PerplexityCostEstimate
        {
            get => _perplexityCostEstimate;
            set { _perplexityCostEstimate = value; OnPropertyChanged(); OnPropertyChanged(nameof(PerplexityCostDisplay)); }
        }
        public string PerplexityCostDisplay => $"${PerplexityCostEstimate:N2}";

        private decimal _claudeHaikuCostEstimate;
        public decimal ClaudeHaikuCostEstimate
        {
            get => _claudeHaikuCostEstimate;
            set { _claudeHaikuCostEstimate = value; OnPropertyChanged(); OnPropertyChanged(nameof(ClaudeHaikuCostDisplay)); }
        }
        public string ClaudeHaikuCostDisplay => $"${ClaudeHaikuCostEstimate:N2}";

        private decimal _claudeSonnetCostEstimate;
        public decimal ClaudeSonnetCostEstimate
        {
            get => _claudeSonnetCostEstimate;
            set { _claudeSonnetCostEstimate = value; OnPropertyChanged(); OnPropertyChanged(nameof(ClaudeSonnetCostDisplay)); }
        }
        public string ClaudeSonnetCostDisplay => $"${ClaudeSonnetCostEstimate:N2}";

        public decimal TotalCostEstimate => PerplexityCostEstimate + ClaudeHaikuCostEstimate + ClaudeSonnetCostEstimate;
        public string TotalCostDisplay => $"${TotalCostEstimate:N2}";

        #endregion

        #region Errors and Logs

        public ObservableCollection<DiagnosticLogEntry> LogEntries { get; } = new ObservableCollection<DiagnosticLogEntry>();

        public void AddLog(string message, DiagnosticLogLevel level = DiagnosticLogLevel.Info)
        {
            LogEntries.Add(new DiagnosticLogEntry
            {
                Timestamp = DateTime.Now,
                Message = message,
                Level = level
            });
        }

        public void AddError(string message)
        {
            AddLog(message, DiagnosticLogLevel.Error);
        }

        public void AddWarning(string message)
        {
            AddLog(message, DiagnosticLogLevel.Warning);
        }

        public void AddSuccess(string message)
        {
            AddLog(message, DiagnosticLogLevel.Success);
        }

        #endregion

        #region Progress

        private string _currentStage;
        public string CurrentStage
        {
            get => _currentStage;
            set { _currentStage = value; OnPropertyChanged(); }
        }

        private double _overallProgress;
        public double OverallProgress
        {
            get => _overallProgress;
            set { _overallProgress = value; OnPropertyChanged(); OnPropertyChanged(nameof(OverallProgressDisplay)); }
        }
        public string OverallProgressDisplay => $"{OverallProgress:N0}%";

        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            set { _isRunning = value; OnPropertyChanged(); }
        }

        private DateTime? _lastRunTime;
        public DateTime? LastRunTime
        {
            get => _lastRunTime;
            set { _lastRunTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastRunTimeDisplay)); }
        }
        public string LastRunTimeDisplay => LastRunTime?.ToString("HH:mm:ss dd.MM.yyyy") ?? "Nigdy";

        #endregion

        public void Reset()
        {
            RssArticlesCount = 0;
            RssTime = TimeSpan.Zero;
            ScrapingArticlesCount = 0;
            ScrapingTime = TimeSpan.Zero;
            PerplexityArticlesCount = 0;
            PerplexityQueriesTotal = 0;
            PerplexityQueriesCompleted = 0;
            PerplexityTime = TimeSpan.Zero;
            TotalUniqueArticles = 0;
            TranslatedCount = 0;
            FilteredCount = 0;
            AiFilteredCount = 0;
            EnrichedCount = 0;
            EnrichmentFailedCount = 0;
            AnalyzedCount = 0;
            SavedToDbCount = 0;
            PerplexityCostEstimate = 0;
            ClaudeHaikuCostEstimate = 0;
            ClaudeSonnetCostEstimate = 0;
            LogEntries.Clear();
            CurrentStage = "";
            OverallProgress = 0;
            IsRunning = false;
        }
    }

    public class DiagnosticLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Message { get; set; }
        public DiagnosticLogLevel Level { get; set; }

        public string TimestampDisplay => Timestamp.ToString("HH:mm:ss");
        public string LevelIcon => Level switch
        {
            DiagnosticLogLevel.Error => "X",
            DiagnosticLogLevel.Warning => "!",
            DiagnosticLogLevel.Success => "OK",
            DiagnosticLogLevel.Info => "i",
            _ => ""
        };
    }

    public enum DiagnosticLogLevel
    {
        Info,
        Success,
        Warning,
        Error
    }
}
