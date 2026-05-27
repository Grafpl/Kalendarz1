using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Kalendarz1.MarketIntelligence.Models;
using Kalendarz1.MarketIntelligence.Services;
using Kalendarz1.MarketIntelligence.Services.AI;

namespace Kalendarz1.MarketIntelligence.ViewModels
{
    /// <summary>
    /// ViewModel dla "Centrum Wiadomości AI" (dawniej Poranny Briefing).
    /// CHAT-FIRST redesign (opcja A): TYLKO realne dane z bazy.
    /// Usunięto 17 hardcoded mock-kolekcji (konkurenci, ceny detal., dotacje, EU benchmark,
    /// kalendarz, eksport/import, mapa konkurencji itd.) — pokazywały dane z lutego 2026 na zawsze.
    /// Zostaje: lista artykułów (intel_Articles) + 3 realne liczniki + chat AI w oknie.
    /// </summary>
    public class PorannyBriefingViewModel : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion

        #region Constructor

        public PorannyBriefingViewModel()
        {
            AllArticles = new ObservableCollection<BriefingArticle>();
            FilteredArticles = new ObservableCollection<BriefingArticle>();
            Stories = new ObservableCollection<Models.IntelStory>();

            ToggleArticleCommand = new RelayCommand<BriefingArticle>(ToggleArticle);
            OpenUrlCommand = new RelayCommand<string>(OpenUrl);
            FilterByCategoryCommand = new RelayCommand<string>(FilterByCategory);
            RefreshCommand = new RelayCommand(_ => Refresh());
            RefreshFromInternetCommand = new RelayCommand(_ => RefreshFromInternetAsync(), _ => !IsLoading);
            LoadFromDatabaseCommand = new RelayCommand(_ => LoadFromDatabaseAsync(), _ => !IsLoading);

            // CurrentRole zostaje CEO — używane przez RoleBasedAnalysisConverter w karcie artykułu
            // (artykuł ma realną analizę AI z perspektywy CEO/Sales/Buyer generowaną przez Claude).
            CurrentRole = UserRole.CEO;
            SelectedCategory = "Wszystkie";
            CurrentDate = DateTime.Now;
            // Brak seed data — dane ładują się z bazy po otwarciu okna (window Loaded → LoadFromDatabaseCommand).
        }

        #endregion

        #region Collections

        public ObservableCollection<BriefingArticle> AllArticles { get; }
        public ObservableCollection<BriefingArticle> FilteredArticles { get; }
        public ObservableCollection<Models.IntelStory> Stories { get; }

        public int StoryCount => Stories.Count;

        #endregion

        #region Stories (Faza A)

        /// <summary>Ładuje wątki z intel_Stories (ostatnie 7 dni, wg ważności).</summary>
        public async System.Threading.Tasks.Task LoadStoriesAsync()
        {
            try
            {
                var svc = new Services.StoriesService();
                var stories = await svc.GetStoriesAsync(daysBack: 7);
                Stories.Clear();
                foreach (var s in stories) Stories.Add(s);
                OnPropertyChanged(nameof(StoryCount));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ViewModel] LoadStories error: {ex.Message}");
            }
        }

        #endregion

        #region Properties

        // CurrentRole zostaje (default CEO) dla per-article AI analysis converter
        public UserRole CurrentRole { get; set; }

        private string _selectedCategory;
        public string SelectedCategory
        {
            get => _selectedCategory;
            set { if (SetProperty(ref _selectedCategory, value)) ApplyFilters(); }
        }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) ApplyFilters(); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private DateTime _currentDate;
        public DateTime CurrentDate
        {
            get => _currentDate;
            set
            {
                if (SetProperty(ref _currentDate, value))
                {
                    OnPropertyChanged(nameof(DateDisplay));
                    OnPropertyChanged(nameof(WeekDisplay));
                }
            }
        }

        public string DateDisplay => CurrentDate.ToString("dddd, d MMMM yyyy",
            new System.Globalization.CultureInfo("pl-PL"));

        public string WeekDisplay
        {
            get
            {
                var cal = System.Globalization.CultureInfo.CurrentCulture.Calendar;
                var week = cal.GetWeekOfYear(CurrentDate,
                    System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                return $"Tydzien {week} | {CurrentDate:HH:mm}";
            }
        }

        // ── 3 REALNE liczniki (liczone z faktycznie załadowanych artykułów) ──
        public int ArticleCount => FilteredArticles.Count;
        public int TotalArticles => AllArticles.Count;
        public int CriticalCount => AllArticles.Count(a => a.Severity == SeverityLevel.Critical);
        public int HpaiCount => AllArticles.Count(a =>
            (a.Category != null && a.Category.Equals("HPAI", StringComparison.OrdinalIgnoreCase)));

        #endregion

        #region Commands

        public ICommand ToggleArticleCommand { get; }
        public ICommand OpenUrlCommand { get; }
        public ICommand FilterByCategoryCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand RefreshFromInternetCommand { get; }
        public ICommand LoadFromDatabaseCommand { get; }

        #endregion

        #region Progress Properties

        private string _loadingStatus;
        public string LoadingStatus
        {
            get => _loadingStatus;
            set => SetProperty(ref _loadingStatus, value);
        }

        private int _loadingProgress;
        public int LoadingProgress
        {
            get => _loadingProgress;
            set => SetProperty(ref _loadingProgress, value);
        }

        private string _lastFetchTime;
        public string LastFetchTime
        {
            get => _lastFetchTime;
            set => SetProperty(ref _lastFetchTime, value);
        }

        private int _fetchedArticlesCount;
        public int FetchedArticlesCount
        {
            get => _fetchedArticlesCount;
            set => SetProperty(ref _fetchedArticlesCount, value);
        }

        #endregion

        #region Command Handlers

        private void ToggleArticle(BriefingArticle article)
        {
            if (article == null) return;
            article.IsExpanded = !article.IsExpanded;
        }

        private void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening URL: {ex.Message}");
            }
        }

        private void FilterByCategory(string category) => SelectedCategory = category;

        private void Refresh()
        {
            CurrentDate = DateTime.Now;
            OnPropertyChanged(nameof(DateDisplay));
            OnPropertyChanged(nameof(WeekDisplay));
        }

        /// <summary>Pobierz nowe dane z internetu (RSS, scraping, AI).</summary>
        private async void RefreshFromInternetAsync()
        {
            if (IsLoading) return;

            IsLoading = true;
            LoadingStatus = "Inicjalizacja...";
            LoadingProgress = 0;

            var progress = new Progress<FetchProgress>(p =>
            {
                LoadingStatus = p.Message;
                LoadingProgress = p.Percent;
            });

            try
            {
                var loader = BriefingDataLoaderService.Instance;
                var result = await loader.FetchNewDataAsync(fullFetch: false, progress: progress);

                if (result.Success)
                {
                    foreach (var article in result.Articles)
                        if (!AllArticles.Any(a => a.Title == article.Title))
                            AllArticles.Add(article);

                    foreach (var alert in result.HpaiAlerts ?? Enumerable.Empty<BriefingArticle>())
                        if (!AllArticles.Any(a => a.Title == alert.Title))
                            AllArticles.Add(alert);

                    FetchedArticlesCount = result.Statistics?.RelevantArticles ?? result.Articles.Count;
                    LastFetchTime = DateTime.Now.ToString("HH:mm");
                    LoadingStatus = $"Pobrano {FetchedArticlesCount} artykułów";
                }
                else
                {
                    LoadingStatus = $"Błąd: {result.Error}";
                }
            }
            catch (Exception ex)
            {
                LoadingStatus = $"Błąd: {ex.Message}";
                Debug.WriteLine($"[ViewModel] Refresh error: {ex}");
            }
            finally
            {
                IsLoading = false;
                ApplyFilters();
                CurrentDate = DateTime.Now;
            }
        }

        /// <summary>Załaduj artykuły z bazy (intel_Articles, ostatnie 7 dni) — bez internetu.</summary>
        private async void LoadFromDatabaseAsync()
        {
            if (IsLoading) return;

            IsLoading = true;
            LoadingStatus = "Ładowanie z bazy...";

            try
            {
                var loader = BriefingDataLoaderService.Instance;
                var result = await loader.LoadFromDatabaseAsync(days: 3);

                if (result.Success && result.Articles.Any())
                {
                    foreach (var article in result.Articles)
                        if (!AllArticles.Any(a => a.Title == article.Title))
                            AllArticles.Add(article);

                    FetchedArticlesCount = result.Articles.Count;
                    LoadingStatus = $"Załadowano {result.Articles.Count} artykułów z bazy";
                }
                else
                {
                    LoadingStatus = result.Success ? "Brak artykułów w bazie — kliknij 🔄 Pobierz" : $"Błąd: {result.Error}";
                }
            }
            catch (Exception ex)
            {
                LoadingStatus = $"Błąd bazy: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                ApplyFilters();
            }
        }

        #endregion

        #region Filtering

        private void ApplyFilters()
        {
            FilteredArticles.Clear();

            var filtered = AllArticles.AsEnumerable();

            if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != "Wszystkie")
                filtered = filtered.Where(a => a.Category == SelectedCategory);

            if (!string.IsNullOrEmpty(SearchText))
            {
                var search = SearchText.ToLower();
                filtered = filtered.Where(a =>
                    a.Title.ToLower().Contains(search) ||
                    a.FullContent?.ToLower().Contains(search) == true ||
                    a.Tags?.Any(t => t.ToLower().Contains(search)) == true);
            }

            // Sortowanie: najnowsze DODANE do bazy na górze (FetchedAt), potem data publikacji.
            filtered = filtered
                .OrderByDescending(a => a.FetchedAt)
                .ThenByDescending(a => a.PublishDate);

            foreach (var article in filtered)
                FilteredArticles.Add(article);

            OnPropertyChanged(nameof(ArticleCount));
            OnPropertyChanged(nameof(TotalArticles));
            OnPropertyChanged(nameof(CriticalCount));
            OnPropertyChanged(nameof(HpaiCount));
        }

        #endregion
    }

    #region RelayCommand

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute((T)parameter);
        public void Execute(object parameter) => _execute((T)parameter);
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object parameter) => _execute(parameter);
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    #endregion
}
