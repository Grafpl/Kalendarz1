using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Kalendarz1.MarketIntelligence.Models;
using Kalendarz1.MarketIntelligence.Services;
using Kalendarz1.MarketIntelligence.Services.AI;

namespace Kalendarz1.MarketIntelligence.ViewModels
{
    public class PorannyBriefingViewModel : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

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
            // Initialize collections
            AllArticles = new ObservableCollection<BriefingArticle>();
            FilteredArticles = new ObservableCollection<BriefingArticle>();
            Competitors = new ObservableCollection<BriefingCompetitor>();
            ExtendedCompetitors = new ObservableCollection<ExtendedCompetitor>();
            RetailPrices = new ObservableCollection<RetailPrice>();
            RetailChainsExtended = new ObservableCollection<RetailChainExtended>();
            Farmers = new ObservableCollection<BriefingFarmer>();
            Clients = new ObservableCollection<BriefingClient>();
            PotentialClients = new ObservableCollection<PotentialClient>();
            CalendarEvents = new ObservableCollection<CalendarEvent>();
            Tasks = new ObservableCollection<BriefingTask>();
            Indicators = new ObservableCollection<PriceIndicator>();
            EuBenchmarks = new ObservableCollection<EuBenchmarkPrice>();
            ElementPrices = new ObservableCollection<ElementPrice>();
            FeedPrices = new ObservableCollection<FeedPrice>();
            SummarySegments = new ObservableCollection<SummarySegment>();
            ExportData = new ObservableCollection<ExportImportData>();
            ImportData = new ObservableCollection<ExportImportData>();
            Subsidies = new ObservableCollection<SubsidyGrant>();
            InternationalNews = new ObservableCollection<InternationalMarketNews>();
            ChartSeries = new ObservableCollection<ChartDataSeries>();

            // Initialize commands
            ToggleArticleCommand = new RelayCommand<BriefingArticle>(ToggleArticle);
            ChangeRoleCommand = new RelayCommand<string>(ChangeRole);
            OpenUrlCommand = new RelayCommand<string>(OpenUrl);
            FilterByCategoryCommand = new RelayCommand<string>(FilterByCategory);
            ToggleTasksPanelCommand = new RelayCommand(_ => ToggleTasksPanel());
            RefreshCommand = new RelayCommand(_ => Refresh());
            RefreshFromInternetCommand = new RelayCommand(_ => RefreshFromInternetAsync(), _ => !IsLoading);
            LoadFromDatabaseCommand = new RelayCommand(_ => LoadFromDatabaseAsync(), _ => !IsLoading);

            // Set defaults
            CurrentRole = UserRole.CEO;
            SelectedCategory = "Wszystkie";
            CurrentDate = DateTime.Now;

            // Load data
            LoadSeedData();
            ApplyFilters();
        }

        #endregion

        #region Collections

        public ObservableCollection<BriefingArticle> AllArticles { get; }
        public ObservableCollection<BriefingArticle> FilteredArticles { get; }
        public ObservableCollection<BriefingCompetitor> Competitors { get; }
        public ObservableCollection<ExtendedCompetitor> ExtendedCompetitors { get; }
        public ObservableCollection<RetailPrice> RetailPrices { get; }
        public ObservableCollection<RetailChainExtended> RetailChainsExtended { get; }
        public ObservableCollection<BriefingFarmer> Farmers { get; }
        public ObservableCollection<BriefingClient> Clients { get; }
        public ObservableCollection<PotentialClient> PotentialClients { get; }
        public ObservableCollection<CalendarEvent> CalendarEvents { get; }
        public ObservableCollection<BriefingTask> Tasks { get; }
        public ObservableCollection<PriceIndicator> Indicators { get; }
        public ObservableCollection<EuBenchmarkPrice> EuBenchmarks { get; }
        public ObservableCollection<ElementPrice> ElementPrices { get; }
        public ObservableCollection<FeedPrice> FeedPrices { get; }
        public ObservableCollection<SummarySegment> SummarySegments { get; }
        public ObservableCollection<ExportImportData> ExportData { get; }
        public ObservableCollection<ExportImportData> ImportData { get; }
        public ObservableCollection<SubsidyGrant> Subsidies { get; }
        public ObservableCollection<InternationalMarketNews> InternationalNews { get; }
        public ObservableCollection<ChartDataSeries> ChartSeries { get; }

        #endregion

        #region Properties

        private UserRole _currentRole;
        public UserRole CurrentRole
        {
            get => _currentRole;
            set
            {
                if (SetProperty(ref _currentRole, value))
                {
                    OnPropertyChanged(nameof(IsCeoRole));
                    OnPropertyChanged(nameof(IsSalesRole));
                    OnPropertyChanged(nameof(IsBuyerRole));
                    OnPropertyChanged(nameof(RoleDisplayName));
                }
            }
        }

        public bool IsCeoRole => CurrentRole == UserRole.CEO;
        public bool IsSalesRole => CurrentRole == UserRole.Sales;
        public bool IsBuyerRole => CurrentRole == UserRole.Buyer;

        public string RoleDisplayName => CurrentRole switch
        {
            UserRole.CEO => "CEO / Strategia",
            UserRole.Sales => "Handlowiec",
            UserRole.Buyer => "Zakupowiec",
            _ => "CEO"
        };

        private string _selectedCategory;
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    ApplyFilters();
                }
            }
        }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplyFilters();
                }
            }
        }

        private bool _isTasksPanelVisible;
        public bool IsTasksPanelVisible
        {
            get => _isTasksPanelVisible;
            set => SetProperty(ref _isTasksPanelVisible, value);
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
                    System.Globalization.CalendarWeekRule.FirstFourDayWeek,
                    DayOfWeek.Monday);
                return $"Tydzien {week} | {CurrentDate:HH:mm}";
            }
        }

        public int ArticleCount => FilteredArticles.Count;

        public int UrgentTasksCount => Tasks.Count(t => !t.IsCompleted && t.DaysUntil <= 3);

        #endregion

        #region Commands

        public ICommand ToggleArticleCommand { get; }
        public ICommand ChangeRoleCommand { get; }
        public ICommand OpenUrlCommand { get; }
        public ICommand FilterByCategoryCommand { get; }
        public ICommand ToggleTasksPanelCommand { get; }
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

        private void ChangeRole(string role)
        {
            CurrentRole = role switch
            {
                "CEO" => UserRole.CEO,
                "Sales" => UserRole.Sales,
                "Buyer" => UserRole.Buyer,
                _ => UserRole.CEO
            };
        }

        private void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening URL: {ex.Message}");
            }
        }

        private void FilterByCategory(string category)
        {
            SelectedCategory = category;
        }

        private void ToggleTasksPanel()
        {
            IsTasksPanelVisible = !IsTasksPanelVisible;
        }

        private void Refresh()
        {
            CurrentDate = DateTime.Now;
            OnPropertyChanged(nameof(DateDisplay));
            OnPropertyChanged(nameof(WeekDisplay));
        }

        /// <summary>
        /// Pobierz nowe dane z internetu (RSS, scraping, AI)
        /// </summary>
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
                    // Add fetched articles to collection
                    foreach (var article in result.Articles)
                    {
                        // Check if not already exists
                        if (!AllArticles.Any(a => a.Title == article.Title))
                        {
                            AllArticles.Add(article);
                        }
                    }

                    // Update indicators if available
                    if (result.Indicators?.Any() == true)
                    {
                        foreach (var indicator in result.Indicators)
                        {
                            var existing = Indicators.FirstOrDefault(i => i.Name == indicator.Name);
                            if (existing != null)
                            {
                                var idx = Indicators.IndexOf(existing);
                                Indicators[idx] = indicator;
                            }
                            else
                            {
                                Indicators.Add(indicator);
                            }
                        }
                    }

                    // Add HPAI alerts as articles
                    foreach (var alert in result.HpaiAlerts ?? Enumerable.Empty<BriefingArticle>())
                    {
                        if (!AllArticles.Any(a => a.Title == alert.Title))
                        {
                            AllArticles.Add(alert);
                        }
                    }

                    // Update summary segments if available
                    if (result.Summary != null)
                    {
                        UpdateSummaryFromDailySummary(result.Summary);
                    }

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

        /// <summary>
        /// Załaduj dane tylko z bazy danych (bez internetu)
        /// </summary>
        private async void LoadFromDatabaseAsync()
        {
            if (IsLoading) return;

            IsLoading = true;
            LoadingStatus = "Ładowanie z bazy...";

            try
            {
                var loader = BriefingDataLoaderService.Instance;
                var result = await loader.LoadFromDatabaseAsync(days: 7);

                if (result.Success && result.Articles.Any())
                {
                    foreach (var article in result.Articles)
                    {
                        if (!AllArticles.Any(a => a.Title == article.Title))
                        {
                            AllArticles.Add(article);
                        }
                    }

                    FetchedArticlesCount = result.Articles.Count;
                    LoadingStatus = $"Załadowano {result.Articles.Count} artykułów z bazy";
                }
                else
                {
                    LoadingStatus = result.Success ? "Brak artykułów w bazie" : $"Błąd: {result.Error}";
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

        private void UpdateSummaryFromDailySummary(DailySummary summary)
        {
            if (summary == null) return;

            SummarySegments.Clear();

            // Build summary from AI-generated content
            if (!string.IsNullOrEmpty(summary.Headline))
            {
                SummarySegments.Add(new SummarySegment(summary.Headline + " ", "#C9A96E", true));
            }

            // Add market mood
            var moodColor = summary.MarketMood switch
            {
                "positive" => "#6DAF6D",
                "negative" => "#C05050",
                _ => "#C9A96E"
            };

            if (!string.IsNullOrEmpty(summary.MarketMoodReason))
            {
                SummarySegments.Add(new SummarySegment(summary.MarketMoodReason));
            }

            // Add top alerts if any
            foreach (var alert in summary.TopAlerts?.Take(3) ?? Enumerable.Empty<Alert>())
            {
                var alertColor = alert.Severity == "critical" ? "#C05050" : "#D4A035";
                SummarySegments.Add(new SummarySegment($" {alert.Message}", alertColor, true));
            }
        }

        #endregion

        #region Filtering

        private void ApplyFilters()
        {
            FilteredArticles.Clear();

            var filtered = AllArticles.AsEnumerable();

            // Category filter
            if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != "Wszystkie")
            {
                filtered = filtered.Where(a => a.Category == SelectedCategory);
            }

            // Search filter
            if (!string.IsNullOrEmpty(SearchText))
            {
                var search = SearchText.ToLower();
                filtered = filtered.Where(a =>
                    a.Title.ToLower().Contains(search) ||
                    a.FullContent?.ToLower().Contains(search) == true ||
                    a.Tags?.Any(t => t.ToLower().Contains(search)) == true);
            }

            // Order by date descending, featured first
            filtered = filtered
                .OrderByDescending(a => a.IsFeatured)
                .ThenByDescending(a => a.Severity == SeverityLevel.Critical)
                .ThenByDescending(a => a.PublishDate);

            foreach (var article in filtered)
            {
                FilteredArticles.Add(article);
            }

            OnPropertyChanged(nameof(ArticleCount));
        }

        #endregion

        #region Seed Data

        private void LoadSeedData()
        {
            LoadSummarySegments();
            LoadIndicators();
            LoadArticles();
            LoadCompetitors();
            LoadExtendedCompetitors();
            LoadRetailPrices();
            LoadRetailChainsExtended();
            LoadFarmers();
            LoadClients();
            LoadPotentialClients();
            LoadCalendarEvents();
            LoadTasks();
            LoadEuBenchmarks();
            LoadElementPrices();
            LoadFeedPrices();
            LoadExportData();
            LoadImportData();
            LoadSubsidies();
            LoadInternationalNews();
            LoadChartSeries();
        }

        private void LoadSummarySegments()
        {
            SummarySegments.Clear();
            SummarySegments.Add(new SummarySegment("Rynek drobiu pod potrojna presja: "));
            SummarySegments.Add(new SummarySegment("19 ognisk HPAI", "#C05050", true));
            SummarySegments.Add(new SummarySegment(" w styczniu (w tym 2 w lodzkim), "));
            SummarySegments.Add(new SummarySegment("fala mrozow -30°C", "#C05050", true));
            SummarySegments.Add(new SummarySegment(" paralizuje transport, a "));
            SummarySegments.Add(new SummarySegment("przejecie Cedrobu przez ADQ za 8 mld PLN", "#C9A96E", true));
            SummarySegments.Add(new SummarySegment(" zmieni architekture rynku. "));
            SummarySegments.Add(new SummarySegment("KSeF obowiazkowy od 01.04", "#D4A035", true));
            SummarySegments.Add(new SummarySegment(" — 58 dni do deadline'u. Jasne punkty: "));
            SummarySegments.Add(new SummarySegment("relacja zywiec/pasza 4.24", "#6DAF6D", true));
            SummarySegments.Add(new SummarySegment(" (najlepsza od 2 lat) i "));
            SummarySegments.Add(new SummarySegment("Dino planuje 300 nowych sklepow", "#6DAF6D", true));
            SummarySegments.Add(new SummarySegment(". Filet rosnie +2.1%, tuszka spada -0.23/tydz."));
        }

        private void LoadIndicators()
        {
            Indicators.Clear();
            Indicators.Add(new PriceIndicator
            {
                Name = "SKUP",
                Value = "4,72",
                Unit = "zl/kg",
                Change = "-0,02",
                Direction = PriceDirection.Down,
                SubLabel = "wolny rynek",
                SparkData = new double[] { 4.55, 4.58, 4.60, 4.62, 4.65, 4.68, 4.70, 4.72, 4.74, 4.72 }
            });
            Indicators.Add(new PriceIndicator
            {
                Name = "TUSZKA",
                Value = "7,33",
                Unit = "zl/kg",
                Change = "-0,23",
                Direction = PriceDirection.Down,
                SubLabel = "hurt"
            });
            Indicators.Add(new PriceIndicator
            {
                Name = "FILET",
                Value = "24,50",
                Unit = "zl/kg",
                Change = "+2,1%",
                Direction = PriceDirection.Up,
                SubLabel = "piers z koscia"
            });
            Indicators.Add(new PriceIndicator
            {
                Name = "UDKO",
                Value = "8,90",
                Unit = "zl/kg",
                Change = "+2,7%",
                Direction = PriceDirection.Up,
                SubLabel = ""
            });
            Indicators.Add(new PriceIndicator
            {
                Name = "KUKURYDZA",
                Value = "192,50",
                Unit = "EUR/t",
                Change = "-0,39%",
                Direction = PriceDirection.Down,
                SubLabel = "MATIF MAR26"
            });
            Indicators.Add(new PriceIndicator
            {
                Name = "HPAI PL",
                Value = "19",
                Unit = "ognisk",
                Change = "+2",
                Direction = PriceDirection.Up,
                SubLabel = "styczen 2026"
            });
            Indicators.Add(new PriceIndicator
            {
                Name = "ZYWIEC/PASZA",
                Value = "4,24",
                Unit = "relacja",
                Change = "+25% r/r",
                Direction = PriceDirection.Up,
                SubLabel = "vs 3.39 rok temu"
            });
        }

        private void LoadArticles()
        {
            AllArticles.Clear();

            // ARTICLE 1: HPAI
            AllArticles.Add(new BriefingArticle
            {
                Id = 1,
                Title = "HPAI Polska 2026: 19 ognisk w styczniu, 1.5M ptakow do likwidacji — 2 ogniska w lodzkim",
                ShortPreview = "Glowny Lekarz Weterynarii potwierdził 19 ognisk wysoce zjadliwej grypy ptakow...",
                FullContent = @"Glowny Lekarz Weterynarii potwierdził 19 ognisk wysoce zjadliwej grypy ptakow (HPAI) podtypu H5N1 na terenie Polski w styczniu 2026 roku. Laczna liczba ptakow przeznaczonych do likwidacji siega 1.5 miliona osobnikow. Budzet Ministerstwa Rolnictwa na odszkodowania dla hodowcow w 2026 roku wynosi 1.1 mld PLN.

Rozklad ognisk na wojewodztwa: wielkopolskie (5 ognisk — najwieksze zageszczenie ferm), podlaskie (4), mazowieckie (3), lodzkie (2), lubuskie (2), lubelskie (2), pomorskie (1). W kazdym ognisku ustanowione sa strefy: ochronna (3 km) i nadzoru (10 km), w ktorych obowiazuje calkowity zakaz przemieszczania drobiu.

W wojewodztwie lodzkim odnotowano 2 ogniska obejmujace lacznie 80 000 ptakow. Lokalizacja ognisk nie została publicznie ujawniona na poziomie gminy, ale strefy restriction moga obejmowac tereny, z ktorych Ubojnia Piorkowscy pozyskuje zywiec.

Sytuacja w Europie: wg EFSA w sezonie 2025/2026 odnotowano ponad 300 ognisk HPAI w UE. Najwieksze nasilenie we Francji (68 ognisk), Holandii (34) i Polsce (25 od pazdziernika 2025). Wirus H5N1 wykazuje zwiekszona zdolnosc do przenoszenia przez ptaki migrujace.",
                EducationalSection = @"GLW — Glowny Lekarz Weterynarii. Naczelny organ inspekcji weterynaryjnej w Polsce, podlegly Ministerstwu Rolnictwa. Odpowiedzialny za nadzor nad zdrowiem zwierzat, bezpieczenstwem zywnosci pochodzenia zwierzecego, zwalczaniem chorob zakaznych. Aktualnie: dr Miroslaw Welz (od 2020 r.). Siedziba: Warszawa, ul. Wspolna 30.

PIW — Powiatowy Inspektorat Weterynarii. Organ terenowy weterynaryjny na poziomie powiatu. Dla Ubojni Piorkowscy wlasciwy PIW: PIW Brzeziny, ul. Sienkiewicza 16, 95-060 Brzeziny, tel. 46 874 26 53.

HPAI H5N1 — Highly Pathogenic Avian Influenza, wysoce zjadliwa grypa ptakow. Podtyp H5N1 jest najgrozniejszy — smiertelnosc w stadach siega 100%. Przenoszony glownie przez ptaki migrujace. Strefy restriction: 3 km (ochronna) i 10 km (nadzoru).",
                AiAnalysisCeo = "2 ogniska w lodzkim to nasz region! Natychmiastowe dzialania: (1) Sprawdzic u PIW Brzeziny dokladna lokalizacje ognisk i zasieg stref. (2) Zweryfikowac czy nasi hodowcy kat. A (Sukiennikowa 20km, Kaczmarek 20km, Wojciechowski 7km) nie znalezli sie w strefie 10km. (3) Bioasekuracja ramp — kontrola dezynfekcji kol pojazdow Avilog. (4) Strategicznie: HPAI = mniej zywca na rynku w Q2 = potencjalny wzrost cen skupu. (5) Ryzyko: jesli ognisko pojawi sie u nas — lockdown zakladu.",
                AiAnalysisSales = "HPAI oznacza mniej zywca na rynku, co w Q2 przelozy sie na potencjalny wzrost cen miesa. To SZANSA na renegocjacje cen z sieciami w gore. Argument dla klientow: \"Polskie mieso z kontrolowanego regionu, certyfikowana bioasekuracja, pelna sledzialnosc od hodowcy do polki.\" Informowac Jole (Biedronka, Dino) i Terese (Carrefour) o sytuacji.",
                AiAnalysisBuyer = "PILNE! Zweryfikowac status HPAI WSZYSTKICH dostawcow z woj. lodzkiego. Hodowcy w strefach restriction (3 km i 10 km) nie moga sprzedawac zywca — jesli Sukiennikowa lub Kaczmarek sa w strefie, tracimy 50 000 szt./tydzien. Sprawdzic: dzwonic do hodowcow i do PIW Brzeziny. Plan B: przygotowac liste alternatywnych dostawcow z woj. swietokrzyskiego/slaskiego.",
                RecommendedActionsCeo = "• Sprawdzic status HPAI u PIW Brzeziny — tel. 46 874 26 53\n• Zadzwonic do hodowcow kat. A — potwierdzic czysta strefe\n• Wzmocnic bioasekuracje ramp\n• Sprawdzic polise ubezpieczeniowa\n• Monitorowac komunikaty GLW codziennie",
                RecommendedActionsSales = "• Przygotowac argumenty dla klientow o bezpieczenstwie polskiego miesa\n• Informowac handlowcow o sytuacji HPAI\n• Przygotowac sie na mozliwe podwyzki cen",
                RecommendedActionsBuyer = "• Natychmiast zadzwonic do PIW Brzeziny\n• Zweryfikowac status wszystkich hodowcow z lodzkiego\n• Przygotowac liste alternatywnych dostawcow\n• Sprawdzic dostepnosc transportu z dalszych regionow",
                Category = "HPAI",
                Source = "GLW / PIW",
                SourceUrl = "https://www.wetgiw.gov.pl/nadzor-weterynaryjny/hpai-raport",
                PublishDate = new DateTime(2026, 1, 31),
                Severity = SeverityLevel.Critical,
                IsFeatured = true,
                Tags = new List<string> { "HPAI", "lodzkie", "bioasekuracja", "GLW" }
            });

            // ARTICLE 2: Mrozy
            AllArticles.Add(new BriefingArticle
            {
                Id = 2,
                Title = "Bestia ze wschodu: Fala mrozow -30°C paralizuje transport zywca",
                ShortPreview = "IMGW ostrzega przed ekstremalnymi mrozami do -30°C w centralnej Polsce...",
                FullContent = @"Instytut Meteorologii i Gospodarki Wodnej wydal ostrzezenie trzeciego stopnia przed ekstremalnymi mrozami. Prognozowana temperatura w woj. lodzkim i mazowieckim: od -25°C do -30°C w nocy, -15°C do -20°C w dzien. Okres trwania: 3-7 lutego 2026.

Skutki dla brazy drobiarskiej:
• Transport zywca: Koniecznosc skrocenia tras do max. 2h (ryzyko przemrozenia ptakow)
• Kurniki: Dodatkowe koszty ogrzewania +40%
• Woda: Ryzyko zamarzniecia instalacji pojenia
• Smiertelnosc: Prognozowany wzrost o 2-3% w stadach

Avilog (nasz przewoznik) informuje o mozliwych opoznieniach do 4h oraz koniecznosci weryfikacji warunkow transportu przed kazda trasa. Koszt dodatkowego ogrzewania w naczepach: +15 zl/km.",
                EducationalSection = @"IMGW — Instytut Meteorologii i Gospodarki Wodnej. Panstwowy instytut badawczy odpowiedzialny za prognozy pogody i ostrzezenia meteorologiczne. Ostrzezenie 3. stopnia = najwyzszy poziom alertu.

Avilog — Firma transportowa specjalizujaca sie w przewozie zywca drobiowego. Glowny przewoznik Ubojni Piorkowscy. Stawka bazowa: 116-145 zl/km. Flota: 40+ pojazdow z kontrolowana temperatura.",
                AiAnalysisCeo = "Kryzysowa sytuacja logistyczna na najblizszy tydzien. Priorytet: utrzymac ciaglosc dostaw od najblizszych hodowcow (Wojciechowski 7km, Sukiennikowa 20km). Rozwazyc czasowe wstrzymanie odbioru od hodowcow >50km. Koszty: dodatkowe ogrzewanie + opoznienia = szacunkowo +50-80 tys. PLN/tydzien.",
                AiAnalysisSales = "Mozliwe opoznienia dostaw do klientow w tym tygodniu. Proaktywnie poinformowac Biedronke i Makro o potencjalnych problemach. Argument: \"Sytuacja nadzwyczajna, priorytet = jakosc i dobrostan zwierzat.\"",
                AiAnalysisBuyer = "Skrocic trasy odbioru zywca do max. 50km. Priorytet: hodowcy kat. A w promieniu 25km. Zweryfikowac z Avilogiem dostepnosc pojazdow z ogrzewaniem. Rozwazyc czasowe zwiekszenie cen skupu dla najblizszych hodowcow.",
                RecommendedActionsCeo = "• Zwolac spotkanie kryzysowe z logistyka\n• Zweryfikowac ubezpieczenie od strat transportowych\n• Przygotowac komunikat dla klientow",
                RecommendedActionsSales = "• Poinformowac kluczowych klientow o mozliwych opoznieniach\n• Przygotowac plan awaryjny dostaw",
                RecommendedActionsBuyer = "• Skontaktowac sie z Avilogiem ws. dostepnosci pojazdow\n• Priorytetyzowac odbiory od najblizszych hodowcow\n• Monitorowac prognozy IMGW",
                Category = "Pogoda",
                Source = "IMGW",
                SourceUrl = "https://www.imgw.pl/ostrzezenia",
                PublishDate = new DateTime(2026, 2, 2),
                Severity = SeverityLevel.Critical,
                Tags = new List<string> { "mrozy", "transport", "Avilog", "logistyka" }
            });

            // ARTICLE 3: ADQ / Cedrob
            AllArticles.Add(new BriefingArticle
            {
                Id = 3,
                Title = "ADQ negocjuje przejecie Cedrobu za 8 mld PLN — co to oznacza dla rynku?",
                ShortPreview = "Fundusz ADQ z Abu Dhabi prowadzi zaawansowane negocjacje przejecia Cedrob S.A....",
                FullContent = @"Emiracki fundusz inwestycyjny ADQ (Abu Dhabi Developmental Holding) prowadzi zaawansowane negocjacje przejecia Cedrob S.A., najwiekszego producenta drobiu w Polsce. Wycena transakcji: 8 mld PLN (ok. 2 mld USD). Termin finalizacji: Q2 2026.

Cedrob S.A. to:
• Przychody: ~5 mld PLN rocznie
• Uboj: 800 000 kurczakow dziennie
• Udzial w rynku: ~25%
• Hodowla: 2000+ kurnikow pod kontraktem
• Pracownicy: 8000+ osob

ADQ juz kontroluje LDC Group (Francja), wlasciciela Drosedu i Indykpolu w Polsce. Jezeli transakcja dojdzie do skutku, ADQ bedzie kontrolowac ~45% polskiego rynku drobiu (Cedrob + Drosed + Indykpol).

UOKiK zapowiedzial szczegolowa analize transakcji pod katem koncentracji. Decyzja oczekiwana w Q3 2026.",
                EducationalSection = @"ADQ — Abu Dhabi Developmental Holding. Emiracki panstwowy fundusz inwestycyjny zarzadzajacy aktywami o wartosci ponad 150 mld USD. Inwestuje w strategiczne sektory: zywnosc, energia, transport, zdrowie. W Europie kontroluje m.in. LDC Group.

LDC Group — Francuski koncern spozywczy, jeden z najwiekszych producentow drobiu w Europie. Przychody: ~5 mld EUR. W Polsce: Drosed (Siedlce) i Indykpol (Olsztyn). Od 2024 kontrolowany przez ADQ.

Cedrob S.A. — Najwiekszy producent drobiu w Polsce. Zalozony w 1993 w Ujazdówku (woj. kujawsko-pomorskie). Wlasciciel: rodzina Gowin. Marka detaliczna: Cedrob, Kurczak Zagrodowy.

UOKiK — Urzad Ochrony Konkurencji i Konsumentow. Organ antymonopolowy. Prezes: Tomasz Chrostny.",
                AiAnalysisCeo = "STRATEGICZNE! Jesli ADQ przejmie Cedrob, powstanie podmiot kontrolujacy 45% rynku. Dla nas: (1) Konkurencja cenowa bedzie jeszcze trudniejsza (efekt skali). (2) Mniejsze ubojnie moga stac sie celami przejec — czy jestesmy gotowi na oferte? (3) Alternatywa: konsolidacja z innymi sredniakami (Wipasz, Roldrob?). Obserwowac decyzje UOKiK.",
                AiAnalysisSales = "Klienci moga miec obawy o dominacje rynkowa — wykorzystac to jako argument za dywersyfikacja dostawcow. \"Wspolpraca z niezalezna, polska ubojnia to gwarancja stabilnosci dostaw.\"",
                AiAnalysisBuyer = "Jesli ADQ przejmie Cedrob, ceny kontraktowe moga wzrosnac (mniej konkurencji o hodowcow). Rozwazyc renegocjacje dlugterminowych kontraktow z hodowcami TERAZ, zanim rynek sie skonsoliduje.",
                RecommendedActionsCeo = "• Sledzic komunikaty UOKiK\n• Przygotowac analize strategiczna: co jesli ADQ kupi Cedrob?\n• Rozmowy z doradca M&A o opcjach",
                RecommendedActionsSales = "• Przygotowac argumenty o niezaleznosci dla klientow\n• Monitorowac reakcje rynku",
                RecommendedActionsBuyer = "• Rozwazyc przedluzenie kontraktow z hodowcami\n• Analizowac pozycje negocjacyjna na Q2",
                Category = "Konkurencja",
                Source = "Bloomberg / Reuters",
                SourceUrl = "https://www.bloomberg.com/news",
                PublishDate = new DateTime(2026, 1, 28),
                Severity = SeverityLevel.Warning,
                Tags = new List<string> { "Cedrob", "ADQ", "M&A", "konkurencja" }
            });

            // ARTICLE 4: KSeF
            AllArticles.Add(new BriefingArticle
            {
                Id = 4,
                Title = "KSeF obowiazkowy od 1 kwietnia 2026 — zostalo 58 dni!",
                ShortPreview = "Krajowy System e-Faktur stanie sie obowiazkowy za 58 dni...",
                FullContent = @"Ministerstwo Finansow potwierdza: Krajowy System e-Faktur (KSeF) bedzie obowiazkowy od 1 kwietnia 2026 dla wszystkich podatnikow VAT. Nie bedzie kolejnych przesuniec terminu.

Co trzeba zrobic:
• Zintegrowac system ERP/fakturowania z KSeF (API lub reczne przesylanie)
• Przeszkolić pracownikow
• Przetestowac w srodowisku testowym (dostepne juz teraz)
• Zaktualizowac wzory faktur

Kary za brak zgodnosci:
• Brak wystawienia e-faktury: 100% kwoty VAT
• Opoznienie: do 30% kwoty VAT
• Powtorne naruszenia: blokada konta w KSeF

Dla Ubojni Piorkowscy:
• ~400 faktur miesiecznie do wystawienia
• Integracja z Sage Symfonia (192.168.0.112)
• Wymagane: certyfikat kwalifikowany lub pieczec elektroniczna",
                EducationalSection = @"KSeF — Krajowy System e-Faktur. Centralny system Ministerstwa Finansow do wystawiania, przesylania i przechowywania faktur ustrukturyzowanych. Kazda faktura otrzymuje unikalny numer KSeF. Cel: uszczelnienie VAT, automatyzacja kontroli skarbowych.

Sage Symfonia — System ERP uzywany przez Ubojnie Piorkowscy (serwer 192.168.0.112). Obsluguje: ksiegowosc, fakturowanie, magazyn, kadry. Wymaga aktualizacji modulu fakturowania do KSeF.",
                AiAnalysisCeo = "58 dni to bardzo malo czasu! Priorytet: weryfikacja gotowosci Sage Symfonia i IT. Czy mamy zaplanowany budzet na integracje? Szacunkowy koszt: 10-30 tys. PLN. Ryzyko: jesli nie zdazymy, kary moga siegnac setek tysiecy PLN miesiecznie.",
                AiAnalysisSales = "Upewnic sie, ze klienci maja poprawne dane do fakturowania (NIP, adres). KSeF nie toleruje bledow — kazda faktura musi byc precyzyjna. Sprawdzic z dzialem ksiegowosci.",
                AiAnalysisBuyer = "Upewnic sie, ze hodowcy maja mozliwosc wystawiania e-faktur (lub faktur uproszczonych). Mniejsze gospodarstwa moga miec problem — rozwazyc wsparcie.",
                RecommendedActionsCeo = "• Spotkanie z IT i ksiegowoscia ws. KSeF — do konca tygodnia\n• Weryfikacja gotowosci Sage Symfonia\n• Testy w srodowisku KSeF",
                RecommendedActionsSales = "• Zweryfikowac poprawnosc danych klientow\n• Poinformowac klientow o zmianach",
                RecommendedActionsBuyer = "• Sprawdzic gotowosc hodowcow do KSeF\n• Rozwazyc pomoc dla mniejszych dostawcow",
                Category = "Regulacje",
                Source = "MF / ARiMR",
                SourceUrl = "https://www.podatki.gov.pl/ksef/",
                PublishDate = new DateTime(2026, 2, 1),
                Severity = SeverityLevel.Warning,
                Tags = new List<string> { "KSeF", "regulacje", "faktury", "VAT" }
            });

            // ARTICLE 5: Dino expansion
            AllArticles.Add(new BriefingArticle
            {
                Id = 5,
                Title = "Dino planuje 300 nowych sklepow w 2026 — szansa dla dostawcow",
                ShortPreview = "Dino Polska zapowiada otwarcie 300 nowych sklepow w 2026 roku...",
                FullContent = @"Dino Polska S.A. zapowiada kontynuacje agresywnej ekspansji: 300 nowych sklepow w 2026 roku. Laczna liczba placowek przekroczy 2800. Budzet inwestycyjny: 3.5 mld PLN.

Kluczowe informacje:
• Fokus: miasta powyzej 15 tys. mieszkancow w centralnej i polnocnej Polsce
• Format: sklepy 400-500 m², z ladami miesno-wedliniarskimi
• Strategia: swiezość, polskie produkty, konkurencyjne ceny

Dino to juz nasz klient (handlowiec: Jola). Aktualne wolumeny: 250 palet E2/miesiac z trendem +40.

Mozliwosci:
• Negocjacje zwiekszenia wolumenu do 400-500 palet/mies
• Wejscie do nowych regionow wraz z ekspansja Dino
• Premium pricing za swiezosc i lokalne pochodzenie",
                EducationalSection = @"Dino Polska S.A. — Polska siec supermarketow, zalożona w 1999 w Krotoszynie przez Tomasza Biernackiego. Notowana na GPW od 2017. Przychody 2025: ~25 mld PLN. Jedna z najszybciej rosnacych sieci w Europie. Model: wlasne nieruchomosci, silny fokus na swieze mieso.",
                AiAnalysisCeo = "Swietna wiadomosc! Dino to strategiczny partner — polska siec z polskim kapitalem, ceniaca lokalnośc. Cel: podwoic wolumeny do 500 palet/mies do konca 2026. Ustawic spotkanie z category managerem Dino.",
                AiAnalysisSales = "PRIORYTET! Jola musi natychmiast umowic spotkanie z Dino. Argument: \"Jestesmy blisko nowych lokalizacji w centralnej Polsce, swiezosc 24h od uboju.\" Przygotowac oferte na zwiekszenie wolumenow.",
                AiAnalysisBuyer = "Wieksza sprzedaz = wiecej zywca. Zaczac rozmowy z hodowcami o zwiekszeniu produkcji. Priorytet: hodowcy kat. A w promieniu 30km.",
                RecommendedActionsCeo = "• Zlecic Joli priorytetowe spotkanie z Dino\n• Przygotowac analize mocy produkcyjnych\n• Rozwazyc inwestycje w rozbudowe linii",
                RecommendedActionsSales = "• Umowic spotkanie z Dino na najblizszy tydzien\n• Przygotowac prezentacje oferty\n• Propozycja wolumenu 400-500 palet/mies",
                RecommendedActionsBuyer = "• Przeanalizowac zdolnosc zwiekszenia zakupu zywca\n• Rozmowy z hodowcami o potencjale wzrostu",
                Category = "Klienci",
                Source = "Dino Polska / PAP",
                SourceUrl = "https://grupadino.pl/relacje-inwestorskie/",
                PublishDate = new DateTime(2026, 1, 29),
                Severity = SeverityLevel.Positive,
                Tags = new List<string> { "Dino", "ekspansja", "sprzedaz", "szansa" }
            });

            // ARTICLE 6: Relacja zywiec/pasza
            AllArticles.Add(new BriefingArticle
            {
                Id = 6,
                Title = "Relacja zywiec/pasza osiagnela 4.24 — najlepsza od 2 lat",
                ShortPreview = "Wskaznik oplacalnosci produkcji drobiu osiagnal najwyzszy poziom od Q1 2024...",
                FullContent = @"Relacja ceny zywca drobiowego do ceny paszy (kluczowy wskaznik oplacalnosci hodowli) osiagnela wartosc 4.24 — najwyzsza od Q1 2024. Rok temu wskaznik wynosil 3.39 (+25% r/r).

Czynniki:
• Cena skupu zywca: 4.72 zl/kg (stabilna)
• Cena paszy: spadek o 15% r/r dzieki dobrym zbiorem zboz 2025
• Kukurydza MATIF: 192.50 EUR/t (-12% r/r)
• Pszenica MATIF: 210 EUR/t (-8% r/r)

Co to oznacza:
• Hodowcy maja lepsza rentownosc — wieksza chec produkcji
• Potencjalnie wieksza podaz zywca w Q2-Q3 2026
• Stabilizacja lub lekki spadek cen skupu mozliwy",
                EducationalSection = @"Relacja zywiec/pasza — Wskaznik oplacalnosci hodowli drobiu. Obliczany jako: cena 1 kg zywca / cena 1 kg paszy. Wartosc >4.0 oznacza dobra rentownosc, <3.5 sygnalizuje problemy. Historycznie: srednia 3.8-4.0.

MATIF — Marche a Terme International de France. Gielda terminowa w Paryzu (obecnie czesc Euronext), gdzie notowane sa kontrakty futures na zboża (kukurydza, pszenica, rzepak).",
                AiAnalysisCeo = "Dobra wiadomosc dla stabilnosci lancucha dostaw. Hodowcy sa zadowoleni = mniejsze ryzyko rezygnacji z kontraktow. Ale uwaga: wysoka rentownosc moze przyciagnac nowych graczy = wieksza konkurencja o hodowcow w przyszlosci.",
                AiAnalysisSales = "Stabilne ceny skupu = mozliwosc utrzymania marż bez podwyzek dla klientow. Argument: \"Dzieki stabilnym relacjom z hodowcami gwarantujemy przewidywalnosc cenowa.\"",
                AiAnalysisBuyer = "Dobry moment na negocjacje dlugterminowych kontraktow z hodowcami. Przy relacji 4.24 hodowcy sa otwarci na wspolprace. Rozwazyc kontrakty 12-miesięczne z fiksowana cena.",
                RecommendedActionsCeo = "• Monitorowac wskaznik co tydzien\n• Przygotowac strategie na potencjalny spadek w Q3",
                RecommendedActionsSales = "• Komunikowac klientom stabilnosc cenowa\n• Przygotowac argumenty na negocjacje",
                RecommendedActionsBuyer = "• Negocjowac dlugoterminowe kontrakty z hodowcami\n• Wykorzystac dobry moment rynkowy",
                Category = "Ceny",
                Source = "farmer.pl / MATIF",
                SourceUrl = "https://www.farmer.pl/ceny/drob",
                PublishDate = new DateTime(2026, 2, 3),
                Severity = SeverityLevel.Positive,
                Tags = new List<string> { "ceny", "oplacalnosc", "pasze", "MATIF" }
            });

            // ARTICLE 7: Mercosur
            AllArticles.Add(new BriefingArticle
            {
                Id = 7,
                Title = "Umowa Mercosur wchodzi w zycie 1 lipca — 180 000 ton drobiu z Brazylii duty-free",
                ShortPreview = "UE i Mercosur sfinalizowaly umowe handlowa — kontyngent drobiu bez cla...",
                FullContent = @"Komisja Europejska potwierdzila wejscie w zycie umowy handlowej UE-Mercosur od 1 lipca 2026 roku. Dla sektora drobiarskiego kluczowy jest kontyngent 180 000 ton miesa drobiowego z krajow Mercosur (glownie Brazylia) bez cla.

Szczegoly kontyngentu:
• Wolumen: 180 000 ton/rok (wzrost z obecnych ~90 000 ton)
• Clo: 0% (obecnie: 10.9% + opłaty dodatkowe)
• Kraje: Brazylia (90%), Argentyna (8%), pozostale (2%)
• Produkty: glownie filet mrozony, ale tez tuszki

Skutki dla polskiego rynku:
• Brazylijski filet mrozony: ~13 zl/kg vs polski swiezy ~17 zl/kg
• Presja cenowa na segment mrozony
• Szansa dla swiezego, lokalnego miesa (argument jakosciowy)

Reakcje branzowe:
• KRD-IG: ostrzezenie przed 'zalewem taniego miesa'
• Copa-Cogeca: apel o monitoring importu
• Producenci PL: fokus na swiezosc i śledzialnosc",
                EducationalSection = @"Mercosur — Wspolny Rynek Poludnia. Blok gospodarczy: Brazylia, Argentyna, Paragwaj, Urugwaj. Brazylia to 2. najwiekszy eksporter drobiu na swiecie (za USA). Glowne firmy: BRF, JBS, Seara.

KRD-IG — Krajowa Rada Drobiarstwa - Izba Gospodarcza. Glowna organizacja branzowa polskiego drobiarstwa. Prezes: Robert Krygier.",
                AiAnalysisCeo = "STRATEGICZNE! Mercosur to sredniookresowe zagrozenie. Musimy ustawic pozycjonowanie: 'swieze, polskie, 24h od uboju'. Brazylijski mrozony filet to inna kategoria — nie konkurujemy bezposrednio. Ale uwaga: sieci moga uzywac brazyljiskiego importu do presji cenowej.",
                AiAnalysisSales = "Przygotuj argumenty dla klientow: (1) Swiezosc 24h vs import mrozony 3-4 tygodnie, (2) Sledzialnosc od hodowcy do polki, (3) 'Kupujesz polskie, wspierasz lokalna gospodarke'. Klienci premium (Dino, Makro) docenia jakość.",
                AiAnalysisBuyer = "Wplyw na ceny skupu bedzie ograniczony — import to glownie filet mrozony, nie zywiec. Ale monitorowac: jesli sieci zaczna kupowac wiecej importu, popyt na nasz zywiec moze spaść.",
                RecommendedActionsCeo = "• Przygotowac strategie pozycjonowania 'swieze PL'\n• Monitorowac reakcje sieci na import\n• Rozwazyc certyfikacje dodatkowe (np. Zero Food Waste)",
                RecommendedActionsSales = "• Przygotowac materiayl sprzedazowe o przewagach swiezego miesa\n• Rozmowy z klientami o lojalnosci wobec polskich dostawcow",
                RecommendedActionsBuyer = "• Bez bezposredniego wplywu na zakup zywca\n• Monitorowac trendy popytu",
                Category = "Regulacje",
                Source = "KE / KRD-IG",
                SourceUrl = "https://ec.europa.eu/trade/policy/countries-and-regions/regions/mercosur/",
                PublishDate = new DateTime(2026, 1, 25),
                Severity = SeverityLevel.Warning,
                Tags = new List<string> { "Mercosur", "import", "Brazylia", "handel" }
            });

            // ARTICLE 8: Audyt IFS
            AllArticles.Add(new BriefingArticle
            {
                Id = 8,
                Title = "Audyt IFS Food 8 zaplanowany na 15 marca — nowe wymagania dotyczace food fraud",
                ShortPreview = "Coroczny audyt certyfikacyjny IFS z nowymi wymaganiami wersji 8...",
                FullContent = @"Jednostka certyfikujaca (TÜV Rheinland) potwierdzila termin corocznego audytu IFS Food dla Ubojni Piorkowscy: 15-16 marca 2026. Bedzie to pierwszy audyt wg nowej wersji standardu IFS Food 8 (obowiazuje od stycznia 2026).

Kluczowe zmiany IFS Food 8:
• Food Fraud Prevention: rozszerzone wymagania dot. oceny podatnosci na oszustwa zywnosciowe
• Food Defense: nowe wymagania dot. ochrony przed celowym zanieczyszczeniem
• Sustainability: elementy zrownowazonego rozwoju w ocenie
• Digitalizacja: wymagania dot. systemow informatycznych i cyberbezpieczenstwa

Obszary do przygotowania:
• Aktualizacja procedury Food Fraud Vulnerability Assessment
• Szkolenie pracownikow z food defense
• Przeglad dokumentacji HACCP
• Weryfikacja kalibracji urzadzen pomiarowych

Poprzedni audyt (IFS Food 7): wynik Higher Level (97%), 2 niezgodnosci mniejsze.",
                EducationalSection = @"IFS Food — International Featured Standards. Miedzynarodowy standard bezpieczenstwa zywnosci uznawany przez GFSI. Wymagany przez sieci detaliczne w Europie (Carrefour, Auchan, REWE, Lidl). Poziomy: Foundation (<75%), Higher Level (>=75%).

TÜV Rheinland — Niemiecka jednostka certyfikujaca. Jedna z najwiekszych na swiecie. Certyfikuje m.in. IFS, BRC, ISO.",
                AiAnalysisCeo = "Audyt IFS to kluczowy moment — utrata certyfikatu = utrata klientow sieciowych. 6 tygodni do audytu! Priorytet: spotkanie z dzialem jakości, przeglad gotowosci. Food Fraud to nowy obszar — upewnic sie, ze mamy procedury.",
                AiAnalysisSales = "Certyfikat IFS Higher Level to argument sprzedazowy. Przed audytem NIE obiecuj klientom nowych wolumenow — poczekaj na wynik. Po pozytywnym audycie: komunikat do klientow o utrzymaniu najwyzszego poziomu.",
                AiAnalysisBuyer = "Audyt IFS obejmuje tez weryfikacje dostawcow. Upewnic sie, ze dokumentacja od hodowcow jest kompletna (oswiadczenia, atesty, certyfikaty). Szczegolnie: Kowalski i Nowak (kontrakty).",
                RecommendedActionsCeo = "• Spotkanie z dzialem jakosci — ten tydzien\n• Przeglad wynikow poprzedniego audytu\n• Budzet na ewentualne poprawki",
                RecommendedActionsSales = "• Wstrzymac sie z nowymi zobowiazaniami do audytu\n• Przygotowac komunikat po audycie",
                RecommendedActionsBuyer = "• Zweryfikowac dokumentacje dostawcow\n• Upewnic sie o komplecie certyfikatow hodowcow",
                Category = "Regulacje",
                Source = "IFS / TÜV Rheinland",
                SourceUrl = "https://www.ifs-certification.com/index.php/en/",
                PublishDate = new DateTime(2026, 2, 1),
                Severity = SeverityLevel.Warning,
                Tags = new List<string> { "IFS", "audyt", "certyfikacja", "jakosc" }
            });

            // ARTICLE 9: Biedronka przetarg
            AllArticles.Add(new BriefingArticle
            {
                Id = 9,
                Title = "Biedronka oglasza przetarg na dostawcow drobiu na 2026/2027 — termin 28 lutego",
                ShortPreview = "Jeronimo Martins Polska zaprasza do składania ofert w przetargu...",
                FullContent = @"Jeronimo Martins Polska (operator sieci Biedronka) oglosil przetarg na dostawcow miesa drobiowego na okres kwiecien 2026 - marzec 2027. Termin skladania ofert: 28 lutego 2026.

Parametry przetargu:
• Wolumen: ~50 000 ton/rok (filet, tuszka, udko, skrzydla)
• Regiony dostaw: cala Polska (16 centrow dystrybucyjnych)
• Wymagania: IFS Food Higher Level, zdolnosc dostaw 5x/tydzien
• Ceny: formula oparta na indeksie + marza stala

Aktualni dostawcy Biedronki:
• Cedrob — glowny dostawca (~40%)
• SuperDrob — drugi dostawca (~25%)
• Drosed — trzeci (~15%)
• Pozostali — 20%

Dla Ubojni Piorkowscy:
• Nie jestesmy aktualnym dostawcem Biedronki
• Szansa na wejscie do portfolia
• Wymagana skala moze byc wyzwaniem",
                EducationalSection = @"Jeronimo Martins — Portugalski koncern handlowy, wlasciciel Biedronki (PL), Pingo Doce (Portugalia), Ara (Kolumbia). Biedronka: ~3500 sklepow, ~70 mld PLN obrotu/rok, najwiekszy detalista w Polsce.

Centra dystrybucyjne Biedronki: Mszczonow, Sosnowiec, Gdansk, Poznan, Wroclaw, Krakow, i inne. Dla nas najblizsze: Mszczonow (~80 km).",
                AiAnalysisCeo = "STRATEGICZNE! Biedronka to najwiekszy detalista w Polsce. Wejscie do ich portfolia = stabilne wolumeny, ale tez presja cenowa. Pytanie: czy mamy zdolnosci produkcyjne na 5000+ ton/rok dla jednego klienta? Rozwazyc ostrożnie.",
                AiAnalysisSales = "Przetarg Biedronki to duza szansa, ale tez ryzyko. Przygotowac oferte, ale NIE za wszelka cene. Lepiej byc dostawca mniejszych, ale rentownych klientow (Dino, Makro) niz duzym, ale nierentownym dostawca Biedronki.",
                AiAnalysisBuyer = "Jesli wejdziemy do Biedronki, potrzebujemy +30% zywca. Czy nasi hodowcy maja rezerwy? Sprawdzic: Sukiennikowa, Kaczmarek, Wojciechowski — ile moga zwiekszyc produkcje?",
                RecommendedActionsCeo = "• Analiza zdolnosci produkcyjnych — do konca tygodnia\n• Decyzja strategiczna: skladamy oferte czy nie?\n• Jesli tak: kalkulacja cen i marży",
                RecommendedActionsSales = "• Przygotowac oferte przetargowa\n• Skoordynowac z dzialem produkcji\n• Deadline: 28 lutego",
                RecommendedActionsBuyer = "• Ocenic mozliwosci zwiekszenia zakupu zywca\n• Rozmowy z hodowcami o potencjale",
                Category = "Klienci",
                Source = "Biedronka / JMP",
                SourceUrl = "https://www.biedronka.pl/pl/dla-dostawcow",
                PublishDate = new DateTime(2026, 2, 2),
                Severity = SeverityLevel.Positive,
                Tags = new List<string> { "Biedronka", "przetarg", "sieci", "sprzedaz" }
            });

            // ARTICLE 10: Wzrost cen energii
            AllArticles.Add(new BriefingArticle
            {
                Id = 10,
                Title = "Ceny energii elektrycznej wzrosna o 23% od kwietnia — URE zatwierdza taryfy",
                ShortPreview = "Urzad Regulacji Energetyki zatwierdził nowe taryfy dla odbiorcow przemyslowych...",
                FullContent = @"Urzad Regulacji Energetyki (URE) zatwierdził nowe taryfy energii elektrycznej dla odbiorcow przemyslowych, obowiazujace od 1 kwietnia 2026. Sredni wzrost cen: +23% w stosunku do Q1 2026.

Szczegoly dla przemyslu:
• Energia czynna: 0.85 zl/kWh → 1.05 zl/kWh (+23%)
• Oplaty dystrybucyjne: +12%
• Oplaty mocowe: +8%

Dla Ubojni Piorkowscy:
• Aktualne zuzycie: ~120 000 kWh/mies
• Obecny koszt: ~120 000 zl/mies
• Po podwyzce: ~148 000 zl/mies (+28 000 zl/mies!)
• Roczny wplyw: +336 000 zl

Opcje:
• Negocjacje z dostawca (TAURON) — deadline 15 marca
• Instalacja PV (zwrot: 4-5 lat)
• Optymalizacja produkcji (szczyty vs off-peak)",
                EducationalSection = @"URE — Urzad Regulacji Energetyki. Regulator rynku energii w Polsce. Zatwierdza taryfy, wydaje koncesje, chroni konsumentow. Prezes: Rafał Gawin.

TAURON — Jedna z 4 glownych grup energetycznych w Polsce (obok PGE, Enea, Energa). Dostawca energii dla Ubojni Piorkowscy. Obsluguje glownie poludniowa i centralna Polske.",
                AiAnalysisCeo = "Poważny wzrost kosztow stalych! +336 000 zl/rok to znaczna kwota. Opcje: (1) renegocjowac umowe z TAURON, (2) rozwazyc instalacje PV, (3) przerzucic czesc kosztow na ceny sprzedazy. Pilne: analiza finansowa.",
                AiAnalysisSales = "Wzrost kosztow energii MUSI byc czesciowo przerzucony na klientow. Przygotuj argumentacje: 'obiektywny wzrost kosztow produkcji, niezalezny od nas'. Docelowa podwyzka: +2-3% na cenach hurtowych.",
                AiAnalysisBuyer = "Bezposredniego wplywu na zakup zywca brak. Ale: wzrost kosztow energii u hodowcow moze przelozyc sie na wyzsze ceny skupu w Q3-Q4.",
                RecommendedActionsCeo = "• Spotkanie z TAURON ws. negocjacji — do 15 marca\n• Analiza oplacalnosci instalacji PV\n• Przeglad budzetu na Q2",
                RecommendedActionsSales = "• Przygotowac komunikat o podwyzce cen dla klientow\n• Negocjacje cen z klientami — przed 1 kwietnia",
                RecommendedActionsBuyer = "• Monitorowac sygnaly o podwyzkach od hodowcow\n• Bez natychmiastowych dzialan",
                Category = "Koszty",
                Source = "URE",
                SourceUrl = "https://www.ure.gov.pl/",
                PublishDate = new DateTime(2026, 1, 30),
                Severity = SeverityLevel.Warning,
                Tags = new List<string> { "energia", "koszty", "URE", "TAURON" }
            });

            // ARTICLE 11: Nowy klient — Carrefour rozbudowa
            AllArticles.Add(new BriefingArticle
            {
                Id = 11,
                Title = "Carrefour Polska inwestuje 500 mln PLN w rozbudowe sieci — szansa na wieksze wolumeny",
                ShortPreview = "Carrefour zapowiada otwarcie 50 nowych sklepow i modernizacje istniejacych...",
                FullContent = @"Carrefour Polska oglasza plan inwestycyjny na lata 2026-2027: 500 mln PLN na rozbudowe sieci i modernizacje istniejacych sklepow.

Szczegoly planu:
• 50 nowych sklepow (glownie format Express i Market)
• Modernizacja 120 istniejacych placowek
• Rozbudowa 3 centrow dystrybucyjnych
• Fokus: centralna i wschodnia Polska

Dla Ubojni Piorkowscy:
• Carrefour to aktualny klient (handlowiec: Teresa)
• Wolumeny: 340 palet/mies (trend: -30)
• Szansa na odwrocenie trendu i wzrost

Carrefour preferuje:
• Lokanych dostawcow
• Produkty premium/bio
• Elastycznosc dostaw",
                EducationalSection = @"Carrefour Polska — Czesc francuskiego koncernu Carrefour SA. W Polsce: ~900 sklepow (hipermarkety, supermarkety, Express). Obroty: ~15 mld PLN/rok. Silna pozycja w segmencie premium.",
                AiAnalysisCeo = "Carrefour to wazny klient, ale wolumeny spadaja (-30). Inwestycja 500 mln to szansa na odwrocenie trendu. Teresa musi ustawic spotkanie z category managerem. Cel: wrócic do poziomu 400 palet/mies.",
                AiAnalysisSales = "PRIORYTET dla Teresy! Carrefour inwestuje = szansa na wieksze zamowienia. Argumenty: lokalna produkcja z Brzezin, swiezosc, elastycznosc. Ustawic spotkanie z kupcem do konca lutego.",
                AiAnalysisBuyer = "Jesli Carrefour zwiększy wolumeny, bedziemy potrzebowac wiecej zywca. Na razie: monitorowac rozmowy Teresy, przygotowac sie na potencjalny wzrost.",
                RecommendedActionsCeo = "• Zlecic Teresie priorytetowe spotkanie z Carrefour\n• Przeanalizowac przyczyny spadku wolumenow",
                RecommendedActionsSales = "• Teresa: umowic spotkanie z Carrefour — do konca lutego\n• Przygotowac prezentacje oferty",
                RecommendedActionsBuyer = "• Monitorowac rozwoj sytuacji\n• Byc gotowym na zwiekszenie zakupow",
                Category = "Klienci",
                Source = "Carrefour Polska",
                SourceUrl = "https://www.carrefour.pl/",
                PublishDate = new DateTime(2026, 1, 27),
                Severity = SeverityLevel.Positive,
                Tags = new List<string> { "Carrefour", "inwestycje", "sprzedaz", "sieci" }
            });

            // ARTICLE 12: Strajk kierowcow Avilog
            AllArticles.Add(new BriefingArticle
            {
                Id = 12,
                Title = "Avilog ostrzega przed mozliwym strajkiem kierowcow — zagrozenie dla dostaw zywca",
                ShortPreview = "Zwiazki zawodowe kierowcow Avilog zapowiadaja strajk ostrzegawczy...",
                FullContent = @"Zwiazki zawodowe kierowcow firmy Avilog (glowny przewoznik zywca dla Ubojni Piorkowscy) zapowiedzialy strajk ostrzegawczy na 10-11 lutego 2026. Przyczyna: spor o podwyzki plac.

Szczegoly sporu:
• Zadania zwiazkow: +15% do plac zasadniczych
• Oferta Avilog: +6%
• Ostatnie negocjacje: 5 lutego (bez porozumienia)

Potencjalny wplyw:
• 2 dni bez transportu zywca = ~40 000 sztuk kurczakow niezodebranych
• Koniecznosc przedluzenia pobytu ptakow u hodowcow
• Ryzyko przekroczenia optymalnej wagi ubojowej
• Straty szacunkowe: 80-120 tys. PLN

Plan awaryjny:
• Alternatywni przewoznicy: TRANS-KUR, DROBEX Trans
• Wlasny transport? (2 samochody)",
                EducationalSection = @"Avilog — Specjalistyczna firma transportowa, 40+ pojazdow do przewozu zywca drobiowego. Glowny przewoznik dla Ubojni Piorkowscy. Stawka: 116-145 zl/km. Siedziba: Lodz.",
                AiAnalysisCeo = "RYZYKO OPERACYJNE! 2 dni bez transportu to powazny problem. Pilne: (1) kontakt z zarzadem Avilog — jaka szansa na porozumienie? (2) przygotowac plan B z alternatywnymi przewoznikami. (3) rozwazyc przedluzenie umow o dzieln wczesniej przed strajkiem.",
                AiAnalysisSales = "Strajk Avilog moze opoznic dostawy do klientow. Przygotowac komunikat awaryjny. NIE obiecuj dostaw na 10-11 lutego poki sytuacja nie jest jasna.",
                AiAnalysisBuyer = "PILNE! Skontaktowac sie z hodowcami — poinformowac o mozliwym opoznieniu odbioru. Sprawdzic alternatywnych przewoznikow: TRANS-KUR, DROBEX Trans. Czy moga przejac nasze trasy?",
                RecommendedActionsCeo = "• Kontakt z zarzadem Avilog — dzis\n• Przygotowac plan B (alternatywni przewoznicy)\n• Ocenic mozliwosc wlasnego transportu",
                RecommendedActionsSales = "• Przygotowac komunikat dla klientow o mozliwych opoznieniach\n• Nie skladac zobowiazan na 10-11 lutego",
                RecommendedActionsBuyer = "• Skontaktowac sie z TRANS-KUR i DROBEX Trans\n• Poinformowac hodowcow o sytuacji\n• Przygotowac harmonogram awaryjny",
                Category = "Logistyka",
                Source = "Avilog / OZZL",
                SourceUrl = "",
                PublishDate = new DateTime(2026, 2, 3),
                Severity = SeverityLevel.Critical,
                Tags = new List<string> { "Avilog", "strajk", "transport", "logistyka" }
            });
        }

        private void LoadCompetitors()
        {
            Competitors.Clear();

            // Tier 1 - Giants
            Competitors.Add(new BriefingCompetitor
            {
                Id = 1,
                Name = "Cedrob S.A.",
                Owner = "Rodzina Gowin → ADQ (negocjacje)",
                CountryFlag = "🇵🇱→🇦🇪",
                CountryOrigin = "Polski → Emiraty?",
                Headquarters = "Ujazdowek, woj. kujawsko-pom.",
                Revenue = "~5 mld PLN",
                Capacity = "800k kurcz./dzien",
                ThreatLevel = 95,
                LatestNews = "ADQ negocjuje przejecie za 8 mld PLN",
                Description = "Najwiekszy producent drobiu w Polsce. 25% udzial w rynku.",
                Tier = 1
            });

            Competitors.Add(new BriefingCompetitor
            {
                Id = 2,
                Name = "SuperDrob / LipCo Foods",
                Owner = "Lipka + CPF (Tajlandia) + Jagiello",
                CountryFlag = "🇵🇱🇹🇭",
                CountryOrigin = "Polski + tajski kapital",
                Headquarters = "Karczew k. Otwocka",
                Revenue = "$1 mld → plan $2 mld",
                Capacity = "350k/dzien",
                ThreatLevel = 88,
                LatestNews = "Jagiello (ex-PKO BP) w RN, 180 mln PLN inwestycji",
                Description = "Agresywna ekspansja, silne zaplecze finansowe.",
                Tier = 1
            });

            Competitors.Add(new BriefingCompetitor
            {
                Id = 3,
                Name = "Drosed S.A.",
                Owner = "LDC Group (Francja) → ADQ",
                CountryFlag = "🇫🇷🇦🇪",
                CountryOrigin = "Francuski → emiracki",
                Headquarters = "Siedlce",
                Revenue = "~2 mld PLN",
                Capacity = "400k/dzien",
                ThreatLevel = 82,
                LatestNews = "ADQ kontroluje LDC. Jesli kupi Cedrob = dominacja",
                Description = "Czesc grupy LDC, ktora kontroluje ADQ.",
                Tier = 1
            });

            Competitors.Add(new BriefingCompetitor
            {
                Id = 4,
                Name = "Animex Foods",
                Owner = "WH Group (Chiny/USA)",
                CountryFlag = "🇨🇳🇺🇸",
                CountryOrigin = "Chinski kapital",
                Headquarters = "Warszawa",
                Revenue = "~1.5 mld PLN",
                Capacity = "200k/dzien",
                ThreatLevel = 70,
                LatestNews = "Stabilnie, glownie wieprzowina",
                Description = "Czesc globalnego giganta miesnego WH Group.",
                Tier = 1
            });

            Competitors.Add(new BriefingCompetitor
            {
                Id = 5,
                Name = "Plukon Food Group",
                Owner = "Plukon (Holandia)",
                CountryFlag = "🇳🇱",
                CountryOrigin = "Holenderski",
                Headquarters = "Goor, Holandia",
                Revenue = "€3+ mld",
                Capacity = "38 zakladow w UE",
                ThreatLevel = 65,
                LatestNews = "Europejski gigant, rosnaca obecnosc w PL",
                Description = "Jeden z najwiekszych producentow drobiu w Europie.",
                Tier = 1
            });

            Competitors.Add(new BriefingCompetitor
            {
                Id = 6,
                Name = "Drobimex / PHW-Gruppe",
                Owner = "Wiesenhof (Niemcy)",
                CountryFlag = "🇩🇪",
                CountryOrigin = "Niemiecki",
                Headquarters = "Szczecin",
                Revenue = "~500 mln PLN",
                Capacity = "150k/dzien",
                ThreatLevel = 60,
                LatestNews = "Silna pozycja na polnocy PL",
                Description = "Czesc niemieckiej grupy PHW/Wiesenhof.",
                Tier = 1
            });

            Competitors.Add(new BriefingCompetitor
            {
                Id = 7,
                Name = "Wipasz S.A.",
                Owner = "Polski prywatny",
                CountryFlag = "🇵🇱",
                CountryOrigin = "Polski",
                Headquarters = "Olsztyn",
                Revenue = "~800 mln PLN",
                Capacity = "Zintegrowany (pasza→uboj)",
                ThreatLevel = 55,
                LatestNews = "Rozbudowa mocy",
                Description = "Zintegrowany pionowo — od pasz do uboju.",
                Tier = 2
            });

            // Tier 2 - Regional
            Competitors.Add(new BriefingCompetitor
            {
                Id = 8,
                Name = "RADDROB Chlebowski",
                Owner = "Polski prywatny",
                CountryFlag = "🇵🇱",
                CountryOrigin = "Polski",
                Headquarters = "Lodzkie",
                Revenue = "~100 mln PLN",
                Capacity = "Srednia",
                ThreatLevel = 30,
                LatestNews = "Nasz odbiorca ALE TEZ konkurent (ma ubojnie)",
                Description = "Hurtownia z wlasna ubojnia w naszym regionie.",
                Tier = 2
            });
        }

        private void LoadRetailPrices()
        {
            RetailPrices.Clear();
            RetailPrices.Add(new RetailPrice { ChainName = "Biedronka", FiletPrice = 22.99m, TuszkaPrice = 9.99m, UdkoPrice = 12.99m, Source = "Gazetka", SourceUrl = "https://www.biedronka.pl/pl/gazetka", CheckDate = new DateTime(2026, 1, 28) });
            RetailPrices.Add(new RetailPrice { ChainName = "Lidl", FiletPrice = 23.49m, TuszkaPrice = 10.49m, UdkoPrice = 13.49m, Source = "Gazetka", SourceUrl = "https://www.lidl.pl/gazetki", CheckDate = new DateTime(2026, 1, 28) });
            RetailPrices.Add(new RetailPrice { ChainName = "Kaufland", FiletPrice = 24.99m, TuszkaPrice = 10.99m, UdkoPrice = 13.99m, Source = "Cennik online", SourceUrl = "https://www.kaufland.pl", CheckDate = new DateTime(2026, 1, 30) });
            RetailPrices.Add(new RetailPrice { ChainName = "Dino", FiletPrice = 24.49m, TuszkaPrice = 10.49m, UdkoPrice = 14.49m, Source = "Sklep", SourceUrl = "https://grupadino.pl", CheckDate = new DateTime(2026, 2, 1) });
            RetailPrices.Add(new RetailPrice { ChainName = "Makro", FiletPrice = 19.90m, TuszkaPrice = 8.50m, UdkoPrice = 10.90m, Source = "Cennik hurt", SourceUrl = "https://www.makro.pl", CheckDate = new DateTime(2026, 1, 30), Notes = "Ceny hurtowe" });
            RetailPrices.Add(new RetailPrice { ChainName = "Selgros", FiletPrice = 20.50m, TuszkaPrice = 8.90m, UdkoPrice = 11.20m, Source = "Cennik hurt", SourceUrl = "https://www.selgros.pl", CheckDate = new DateTime(2026, 1, 30), Notes = "Ceny hurtowe" });
        }

        private void LoadFarmers()
        {
            Farmers.Clear();
            // Category A
            Farmers.Add(new BriefingFarmer { Id = 1, Name = "Sukiennikowa", DistanceKm = 20, Barns = 3, Category = FarmerCategory.A, HpaiStatus = "Czysta strefa", IsAtRisk = false, Notes = "Wieloletni, niezawodny", Phone = "600 111 222" });
            Farmers.Add(new BriefingFarmer { Id = 2, Name = "Kaczmarek", DistanceKm = 20, Barns = 2, Category = FarmerCategory.A, HpaiStatus = "Czysta strefa", IsAtRisk = false, Notes = "Wieloletni", Phone = "600 222 333" });
            Farmers.Add(new BriefingFarmer { Id = 3, Name = "Wojciechowski Eryk", DistanceKm = 7, Barns = 2, Category = FarmerCategory.A, HpaiStatus = "Czysta strefa", IsAtRisk = false, Notes = "Najblizszy hodowca", Phone = "600 333 444" });
            // Category B
            Farmers.Add(new BriefingFarmer { Id = 4, Name = "Kowalski (kontrakt)", DistanceKm = 45, Barns = 4, Category = FarmerCategory.B, HpaiStatus = "Blisko strefy!", IsAtRisk = true, Notes = "Sprawdzic status u PIW", Phone = "600 444 555" });
            Farmers.Add(new BriefingFarmer { Id = 5, Name = "Nowak (kontrakt)", DistanceKm = 60, Barns = 5, Category = FarmerCategory.B, HpaiStatus = "Czysta strefa", IsAtRisk = false, Notes = "Transport drozszy", Phone = "600 555 666" });
            // Category C
            Farmers.Add(new BriefingFarmer { Id = 6, Name = "Wisniewski", DistanceKm = 95, Barns = 3, Category = FarmerCategory.C, HpaiStatus = "Czysta strefa", IsAtRisk = false, Notes = "Drogi transport (Avilog)", Phone = "600 666 777" });
        }

        private void LoadClients()
        {
            Clients.Clear();
            Clients.Add(new BriefingClient { Id = 1, Name = "RADDROB Chlebowski", PaletsPerMonth = 540, Salesperson = "Jola", ChangeAmount = 80, ClientType = "Hurtownia/ubojnia" });
            Clients.Add(new BriefingClient { Id = 2, Name = "Sklepy ABC", PaletsPerMonth = 540, Salesperson = "Jola", ChangeAmount = 80, ClientType = "Siec" });
            Clients.Add(new BriefingClient { Id = 3, Name = "Makro Cash & Carry", PaletsPerMonth = 480, Salesperson = "Ania", ChangeAmount = 30, ClientType = "Cash & Carry" });
            Clients.Add(new BriefingClient { Id = 4, Name = "Selgros", PaletsPerMonth = 420, Salesperson = "Ania", ChangeAmount = -80, ClientType = "Cash & Carry" });
            Clients.Add(new BriefingClient { Id = 5, Name = "Biedronka DC", PaletsPerMonth = 380, Salesperson = "Nieprzypisany", ChangeAmount = 55, ClientType = "Dyskont" });
            Clients.Add(new BriefingClient { Id = 6, Name = "Carrefour Logistics", PaletsPerMonth = 340, Salesperson = "Teresa", ChangeAmount = -30, ClientType = "Hipermarket" });
            Clients.Add(new BriefingClient { Id = 7, Name = "Stokrotka", PaletsPerMonth = 280, Salesperson = "Maja", ChangeAmount = 25, ClientType = "Supermarket" });
            Clients.Add(new BriefingClient { Id = 8, Name = "Dino Polska", PaletsPerMonth = 250, Salesperson = "Jola", ChangeAmount = 40, ClientType = "Supermarket" });
        }

        private void LoadCalendarEvents()
        {
            CalendarEvents.Clear();
            CalendarEvents.Add(new CalendarEvent { Id = 1, Title = "KSeF obowiazkowy", EventDate = new DateTime(2026, 4, 1), Severity = SeverityLevel.Critical, Description = "Deadline wdrozenia e-faktur" });
            CalendarEvents.Add(new CalendarEvent { Id = 2, Title = "Spotkanie Dino", EventDate = new DateTime(2026, 2, 10), Severity = SeverityLevel.Positive, Description = "Negocjacje zwiekszenia wolumenow" });
            CalendarEvents.Add(new CalendarEvent { Id = 3, Title = "Audyt IFS", EventDate = new DateTime(2026, 3, 15), Severity = SeverityLevel.Warning, Description = "Coroczny audyt certyfikacyjny" });
            CalendarEvents.Add(new CalendarEvent { Id = 4, Title = "Mercosur — start", EventDate = new DateTime(2026, 7, 1), Severity = SeverityLevel.Warning, Description = "180k ton duty-free z Brazylii" });
        }

        private void LoadTasks()
        {
            Tasks.Clear();
            Tasks.Add(new BriefingTask { Id = 1, Title = "Sprawdzic status HPAI u PIW Brzeziny", AssignedTo = "Zakupowiec", Deadline = DateTime.Today.AddDays(1), Severity = SeverityLevel.Critical, RelatedArticleTitle = "HPAI Polska 2026" });
            Tasks.Add(new BriefingTask { Id = 2, Title = "Spotkanie ws. gotowosci KSeF", AssignedTo = "IT + Ksiegowosc", Deadline = DateTime.Today.AddDays(3), Severity = SeverityLevel.Warning, RelatedArticleTitle = "KSeF obowiazkowy" });
            Tasks.Add(new BriefingTask { Id = 3, Title = "Umowic spotkanie z Dino", AssignedTo = "Jola", Deadline = DateTime.Today.AddDays(5), Severity = SeverityLevel.Positive, RelatedArticleTitle = "Dino 300 sklepow" });
        }

        private void LoadEuBenchmarks()
        {
            EuBenchmarks.Clear();
            EuBenchmarks.Add(new EuBenchmarkPrice { Country = "Polska", CountryFlag = "🇵🇱", PricePer100kg = 185.5m, ChangePercent = -1.2m, IsPoland = true });
            EuBenchmarks.Add(new EuBenchmarkPrice { Country = "Niemcy", CountryFlag = "🇩🇪", PricePer100kg = 245.0m, ChangePercent = 0.5m });
            EuBenchmarks.Add(new EuBenchmarkPrice { Country = "Francja", CountryFlag = "🇫🇷", PricePer100kg = 238.0m, ChangePercent = -2.1m });
            EuBenchmarks.Add(new EuBenchmarkPrice { Country = "Holandia", CountryFlag = "🇳🇱", PricePer100kg = 225.0m, ChangePercent = 1.0m });
            EuBenchmarks.Add(new EuBenchmarkPrice { Country = "Belgia", CountryFlag = "🇧🇪", PricePer100kg = 218.0m, ChangePercent = -0.3m });
            EuBenchmarks.Add(new EuBenchmarkPrice { Country = "Wlochy", CountryFlag = "🇮🇹", PricePer100kg = 210.0m, ChangePercent = 0.8m });
            EuBenchmarks.Add(new EuBenchmarkPrice { Country = "Hiszpania", CountryFlag = "🇪🇸", PricePer100kg = 195.0m, ChangePercent = -0.5m });
            EuBenchmarks.Add(new EuBenchmarkPrice { Country = "Brazylia", CountryFlag = "🇧🇷", PricePer100kg = 125.0m, ChangePercent = -3.0m, IsImporter = true });
            EuBenchmarks.Add(new EuBenchmarkPrice { Country = "Ukraina", CountryFlag = "🇺🇦", PricePer100kg = 140.0m, ChangePercent = -1.5m, IsImporter = true });
        }

        private void LoadElementPrices()
        {
            ElementPrices.Clear();
            ElementPrices.Add(new ElementPrice { Name = "Filet z piersi", Price = 24.50m, Unit = "zl/kg", ChangePercent = 2.1m });
            ElementPrices.Add(new ElementPrice { Name = "Udko", Price = 8.90m, Unit = "zl/kg", ChangePercent = 2.7m });
            ElementPrices.Add(new ElementPrice { Name = "Podudzie", Price = 6.50m, Unit = "zl/kg", ChangePercent = -0.5m });
            ElementPrices.Add(new ElementPrice { Name = "Tuszka", Price = 7.33m, Unit = "zl/kg", ChangePercent = -3.0m });
            ElementPrices.Add(new ElementPrice { Name = "Skrzydlo", Price = 7.80m, Unit = "zl/kg", ChangePercent = -1.2m });
            ElementPrices.Add(new ElementPrice { Name = "Korpus", Price = 3.20m, Unit = "zl/kg", ChangePercent = 0.0m });
        }

        private void LoadFeedPrices()
        {
            FeedPrices.Clear();
            FeedPrices.Add(new FeedPrice { Commodity = "Kukurydza", Contract = "MAR26", Price = 192.50m, Unit = "EUR/t", ChangePercent = -0.39m });
            FeedPrices.Add(new FeedPrice { Commodity = "Pszenica", Contract = "MAR26", Price = 210.00m, Unit = "EUR/t", ChangePercent = -0.25m });
            FeedPrices.Add(new FeedPrice { Commodity = "Soja", Contract = "MAR26", Price = 385.00m, Unit = "EUR/t", ChangePercent = 0.15m });
            FeedPrices.Add(new FeedPrice { Commodity = "Rzepak", Contract = "MAY26", Price = 445.00m, Unit = "EUR/t", ChangePercent = -0.52m });
        }

        private void LoadExtendedCompetitors()
        {
            ExtendedCompetitors.Clear();

            // MY LOCATION: Brzeziny, lodzkie - 51.7944° N, 19.7569° E
            // Tier 1 - GIGANCI
            ExtendedCompetitors.Add(new ExtendedCompetitor
            {
                Id = 1, Name = "Cedrob S.A.", ShortName = "Cedrob",
                Owner = "Rodzina Gowin (negocjacje z ADQ)", OwnerCountry = "Polska/Emiraty", CountryFlag = "🇵🇱🇦🇪",
                Latitude = 52.8833, Longitude = 19.4167, City = "Ujazdówek", Voivodeship = "kujawsko-pomorskie", DistanceFromUsKm = 180,
                RevenueMillionPln = 5000, CapacityPerDay = 800000, Employees = 8000, NumberOfPlants = 5, MarketSharePercent = 25,
                Tier = 1, ThreatLevel = 95,
                MainProducts = new List<string> { "Tuszka", "Filet", "Elementy", "Produkty przetworzone" },
                Certifications = new List<string> { "IFS Higher Level", "BRC A", "QAFP", "Halal" },
                MainClients = new List<string> { "Biedronka", "Lidl", "Kaufland", "Tesco", "Auchan" },
                CompanyHistory = "Zalozony 1993 przez rodzine Gowin. Od lat 90. dynamiczny rozwoj. 2024-2026: negocjacje sprzedazy do ADQ za 8 mld PLN.",
                LatestNews = "ADQ (Abu Dhabi) negocjuje przejecie za 8 mld PLN. Jesli transakcja dojdzie do skutku, ADQ bedzie kontrolowac 45% rynku PL.",
                NewsSource = "Bloomberg", NewsSourceUrl = "https://www.bloomberg.com", NewsDate = new DateTime(2026, 1, 28),
                Strengths = "Najwiekszy gracz, efekt skali, pelna integracja pionowa, silna marka",
                Weaknesses = "Biurokracja duzej firmy, negocjacje sprzedazy moga destabilizowac",
                OpportunitiesForUs = "Chaos organizacyjny przy przejęciu, niezadowoleni hodowcy moga szukac alternatyw",
                ThreatsFromThem = "Dominacja cenowa, moze przejmowac naszych hodowcow kontraktami",
                Website = "https://www.cedrob.pl", Address = "Ujazdówek 20, 87-326 Ujazdówek"
            });

            ExtendedCompetitors.Add(new ExtendedCompetitor
            {
                Id = 2, Name = "SuperDrob / LipCo Foods", ShortName = "SuperDrob",
                Owner = "Lipka + CPF (Tajlandia) + Jagiello", OwnerCountry = "Polska/Tajlandia", CountryFlag = "🇵🇱🇹🇭",
                Latitude = 52.0833, Longitude = 21.2333, City = "Karczew", Voivodeship = "mazowieckie", DistanceFromUsKm = 95,
                RevenueMillionPln = 4000, CapacityPerDay = 350000, Employees = 3500, NumberOfPlants = 3, MarketSharePercent = 15,
                Tier = 1, ThreatLevel = 88,
                MainProducts = new List<string> { "Filet", "Tuszka", "Produkty convenience" },
                Certifications = new List<string> { "IFS Higher Level", "BRC", "ISO 22000" },
                MainClients = new List<string> { "Biedronka", "Lidl", "Zabka", "Makro" },
                CompanyHistory = "2015: Lipka zakłada SuperDrob. 2022: wejscie CPF (Charoen Pokphand Foods). 2025: Zbigniew Jagiello w RN.",
                LatestNews = "180 mln PLN inwestycji w nowa linie. Cel: podwojenie przychodow do $2 mld do 2028.",
                NewsSource = "Puls Biznesu", NewsSourceUrl = "https://www.pb.pl", NewsDate = new DateTime(2026, 1, 20),
                Strengths = "Agresywna ekspansja, silne zaplecze finansowe (CPF), nowoczesne zaklady",
                Weaknesses = "Relatywnie nowy gracz, mniejsza baza hodowcow",
                OpportunitiesForUs = "Moga miec problemy z pozyskaniem zywca przy szybkim wzroscie",
                ThreatsFromThem = "Agresywna polityka cenowa, przejmowanie hodowcow",
                Website = "https://www.superdrob.pl", Address = "ul. Otwocka 1, 05-480 Karczew"
            });

            ExtendedCompetitors.Add(new ExtendedCompetitor
            {
                Id = 3, Name = "Drosed S.A.", ShortName = "Drosed",
                Owner = "LDC Group (Francja) → ADQ", OwnerCountry = "Francja/Emiraty", CountryFlag = "🇫🇷🇦🇪",
                Latitude = 52.1667, Longitude = 22.2833, City = "Siedlce", Voivodeship = "mazowieckie", DistanceFromUsKm = 200,
                RevenueMillionPln = 2000, CapacityPerDay = 400000, Employees = 2500, NumberOfPlants = 2, MarketSharePercent = 12,
                Tier = 1, ThreatLevel = 82,
                MainProducts = new List<string> { "Tuszka", "Filet", "Elementy" },
                Certifications = new List<string> { "IFS Higher Level", "BRC AA" },
                MainClients = new List<string> { "Carrefour", "Auchan", "Intermarche", "Eksport UE" },
                CompanyHistory = "Historia siega lat 60. Od 2006 w grupie LDC. 2024: ADQ przejmuje LDC Group.",
                LatestNews = "Jesli ADQ kupi tez Cedrob, Drosed+Cedrob = 37% rynku pod jedna kontrola.",
                NewsSource = "Reuters", NewsSourceUrl = "https://www.reuters.com", NewsDate = new DateTime(2026, 1, 25),
                Strengths = "Silna pozycja eksportowa, doswiadczenie, know-how LDC",
                Weaknesses = "Zmiany wlascicielskie moga destabilizowac",
                OpportunitiesForUs = "Koncentracja na eksporcie = mniejszy fokus na rynek krajowy",
                ThreatsFromThem = "Synergie z Cedrobem po przejęciu ADQ",
                Website = "https://www.drosed.pl", Address = "ul. Brzeska 90, 08-110 Siedlce"
            });

            ExtendedCompetitors.Add(new ExtendedCompetitor
            {
                Id = 4, Name = "Animex Foods Sp. z o.o.", ShortName = "Animex",
                Owner = "WH Group (Chiny/USA)", OwnerCountry = "Chiny", CountryFlag = "🇨🇳",
                Latitude = 52.2297, Longitude = 21.0122, City = "Warszawa", Voivodeship = "mazowieckie", DistanceFromUsKm = 120,
                RevenueMillionPln = 1500, CapacityPerDay = 200000, Employees = 2000, NumberOfPlants = 4, MarketSharePercent = 8,
                Tier = 1, ThreatLevel = 70,
                MainProducts = new List<string> { "Wieprzowina (glownie)", "Drob", "Wedliny" },
                Certifications = new List<string> { "IFS", "BRC", "ISO" },
                MainClients = new List<string> { "Sieci handlowe", "HoReCa", "Eksport" },
                CompanyHistory = "Historia od 1951. Prywatyzacja 1995. 2014: przejecie przez WH Group (Shuanghui).",
                LatestNews = "Stabilna pozycja, glowny fokus na wieprzowine. Drob to ok. 20% biznesu.",
                NewsSource = "PAP", NewsSourceUrl = "https://www.pap.pl", NewsDate = new DateTime(2026, 1, 15),
                Strengths = "Globalny koncern, dywersyfikacja produktowa",
                Weaknesses = "Drob nie jest core business",
                OpportunitiesForUs = "Mniejszy fokus na drob = szansa na klientow",
                ThreatsFromThem = "Mogą zintensyfikować działania w drobiu",
                Website = "https://www.animex.pl", Address = "ul. Chałubińskiego 8, 00-613 Warszawa"
            });

            ExtendedCompetitors.Add(new ExtendedCompetitor
            {
                Id = 5, Name = "Indykpol S.A.", ShortName = "Indykpol",
                Owner = "LDC Group → ADQ", OwnerCountry = "Francja/Emiraty", CountryFlag = "🇫🇷🇦🇪",
                Latitude = 53.7756, Longitude = 20.4833, City = "Olsztyn", Voivodeship = "warminsko-mazurskie", DistanceFromUsKm = 280,
                RevenueMillionPln = 800, CapacityPerDay = 150000, Employees = 1500, NumberOfPlants = 2, MarketSharePercent = 5,
                Tier = 1, ThreatLevel = 55,
                MainProducts = new List<string> { "Indyk", "Kurczak", "Produkty przetworzone" },
                Certifications = new List<string> { "IFS", "BRC" },
                MainClients = new List<string> { "Sieci handlowe", "Eksport" },
                CompanyHistory = "Zalozony 1972. Specjalizacja: indyk. Od 2010 w grupie LDC/Drosed.",
                LatestNews = "Czesc grupy LDC/ADQ. Fokus na segment premium indyka.",
                NewsSource = "Farmer.pl", NewsSourceUrl = "https://www.farmer.pl", NewsDate = new DateTime(2026, 1, 10),
                Strengths = "Silna marka w segmencie indyka",
                Weaknesses = "Ograniczona obecnosc w kurczaku",
                OpportunitiesForUs = "Nie konkurujemy bezposrednio (my = kurczak)",
                ThreatsFromThem = "Synergie grupowe z Drosed",
                Website = "https://www.indykpol.pl", Address = "ul. Jesienna 3, 10-370 Olsztyn"
            });

            ExtendedCompetitors.Add(new ExtendedCompetitor
            {
                Id = 6, Name = "Drobimex Sp. z o.o. / PHW-Gruppe", ShortName = "Drobimex",
                Owner = "PHW-Gruppe / Wiesenhof (Niemcy)", OwnerCountry = "Niemcy", CountryFlag = "🇩🇪",
                Latitude = 53.4289, Longitude = 14.5530, City = "Szczecin", Voivodeship = "zachodniopomorskie", DistanceFromUsKm = 420,
                RevenueMillionPln = 500, CapacityPerDay = 150000, Employees = 800, NumberOfPlants = 1, MarketSharePercent = 4,
                Tier = 1, ThreatLevel = 60,
                MainProducts = new List<string> { "Tuszka", "Filet", "Eksport do Niemiec" },
                Certifications = new List<string> { "IFS Higher Level", "QS", "Tierschutz" },
                MainClients = new List<string> { "Sieci niemieckie", "REWE", "EDEKA" },
                CompanyHistory = "Czesc niemieckiej grupy PHW (Wiesenhof) - najwiekszego producenta drobiu w Niemczech.",
                LatestNews = "Silna pozycja eksportowa do Niemiec. Fokus na polnocno-zachodnia Polske.",
                NewsSource = "Fleischwirtschaft", NewsSourceUrl = "https://www.fleischwirtschaft.de", NewsDate = new DateTime(2026, 1, 18),
                Strengths = "Know-how niemieckie, dostep do rynku DE",
                Weaknesses = "Ograniczona obecnosc w centralnej/wschodniej PL",
                OpportunitiesForUs = "Nie konkurujemy geograficznie",
                ThreatsFromThem = "Moga ekspandowac na wschod",
                Website = "https://www.drobimex.pl", Address = "ul. Welecka 20, 72-006 Mierzyn"
            });

            // Tier 2 - REGIONALNI
            ExtendedCompetitors.Add(new ExtendedCompetitor
            {
                Id = 7, Name = "Wipasz S.A.", ShortName = "Wipasz",
                Owner = "Polski prywatny", OwnerCountry = "Polska", CountryFlag = "🇵🇱",
                Latitude = 53.7756, Longitude = 20.4833, City = "Wadąg k/Olsztyna", Voivodeship = "warminsko-mazurskie", DistanceFromUsKm = 290,
                RevenueMillionPln = 800, CapacityPerDay = 100000, Employees = 600, NumberOfPlants = 1, MarketSharePercent = 3,
                Tier = 2, ThreatLevel = 55,
                MainProducts = new List<string> { "Drob", "Pasze", "Zintegrowany pionowo" },
                Certifications = new List<string> { "IFS", "ISO 22000" },
                MainClients = new List<string> { "Sieci regionalne", "Hurt" },
                CompanyHistory = "Zintegrowany pionowo: od produkcji pasz, przez hodowle, do uboju.",
                LatestNews = "Inwestycje w rozbudowe mocy produkcyjnych.",
                NewsSource = "Farmer.pl", NewsSourceUrl = "https://www.farmer.pl", NewsDate = new DateTime(2026, 1, 12),
                Strengths = "Integracja pionowa, kontrola kosztow",
                Weaknesses = "Ograniczony zasieg geograficzny",
                OpportunitiesForUs = "Daleko od naszego regionu",
                ThreatsFromThem = "Model integracji pionowej moze byc inspiracja",
                Website = "https://www.wipasz.pl", Address = "Wadąg 9, 11-034 Stawiguda"
            });

            ExtendedCompetitors.Add(new ExtendedCompetitor
            {
                Id = 8, Name = "Konspol Holding Sp. z o.o.", ShortName = "Konspol",
                Owner = "Rodzina Krupa", OwnerCountry = "Polska", CountryFlag = "🇵🇱",
                Latitude = 50.0833, Longitude = 19.9167, City = "Nowy Sącz", Voivodeship = "malopolskie", DistanceFromUsKm = 280,
                RevenueMillionPln = 600, CapacityPerDay = 80000, Employees = 500, NumberOfPlants = 1, MarketSharePercent = 2,
                Tier = 2, ThreatLevel = 40,
                MainProducts = new List<string> { "Kurczak", "Indyk", "Produkty przetworzone" },
                Certifications = new List<string> { "IFS", "BRC" },
                MainClients = new List<string> { "Sieci handlowe", "Gastronomia" },
                CompanyHistory = "Firma rodzinna z Malopolski. Od lat 90. dynamiczny rozwoj.",
                LatestNews = "Stabilna pozycja w poludniowej Polsce.",
                NewsSource = "Portal Spozywczy", NewsSourceUrl = "https://www.portalspozywczy.pl", NewsDate = new DateTime(2026, 1, 8),
                Strengths = "Silna pozycja regionalna, lojalnosc klientow",
                Weaknesses = "Ograniczony zasieg poza Malopolska",
                OpportunitiesForUs = "Nie konkurujemy geograficznie",
                ThreatsFromThem = "Minimalne - daleko",
                Website = "https://www.konspol.pl", Address = "ul. Lwowska 164, 33-300 Nowy Sącz"
            });

            ExtendedCompetitors.Add(new ExtendedCompetitor
            {
                Id = 9, Name = "RADDROB Chlebowski", ShortName = "RADDROB",
                Owner = "Rodzina Chlebowski", OwnerCountry = "Polska", CountryFlag = "🇵🇱",
                Latitude = 51.75, Longitude = 19.45, City = "Lodz region", Voivodeship = "lodzkie", DistanceFromUsKm = 35,
                RevenueMillionPln = 100, CapacityPerDay = 30000, Employees = 150, NumberOfPlants = 1, MarketSharePercent = 0.5m,
                Tier = 2, ThreatLevel = 45,
                MainProducts = new List<string> { "Hurt drobiu", "Uboj" },
                Certifications = new List<string> { "Weterynaryjny" },
                MainClients = new List<string> { "Lokalne sklepy", "Targowiska" },
                CompanyHistory = "Lokalna hurtownia z wlasna mala ubojnia. Nasz klient ALE TEZ konkurent.",
                LatestNews = "Rozbudowuje wlasna ubojnie - moze stac sie wiekszym konkurentem.",
                NewsSource = "Lokalne info", NewsSourceUrl = "", NewsDate = new DateTime(2026, 1, 5),
                Strengths = "Zna lokalny rynek, blisko nas",
                Weaknesses = "Mala skala, ograniczone certyfikaty",
                OpportunitiesForUs = "Jest naszym klientem - mozemy kontrolowac relacje",
                ThreatsFromThem = "Moze przejmowac lokalnych hodowcow, rozbudowuje ubojnie",
                Website = "", Address = "Lodzkie"
            });

            ExtendedCompetitors.Add(new ExtendedCompetitor
            {
                Id = 10, Name = "Zakłady Drobiarskie Koziegłowy", ShortName = "ZD Koziegłowy",
                Owner = "Polski prywatny", OwnerCountry = "Polska", CountryFlag = "🇵🇱",
                Latitude = 50.4667, Longitude = 19.1833, City = "Koziegłowy", Voivodeship = "slaskie", DistanceFromUsKm = 130,
                RevenueMillionPln = 150, CapacityPerDay = 40000, Employees = 200, NumberOfPlants = 1, MarketSharePercent = 0.8m,
                Tier = 2, ThreatLevel = 35,
                MainProducts = new List<string> { "Tuszka", "Elementy", "Hurt regionalny" },
                Certifications = new List<string> { "IFS", "Weterynaryjny" },
                MainClients = new List<string> { "Hurt slaski", "Lokalne sieci" },
                CompanyHistory = "Tradycyjna ubojnia ze Slaska.",
                LatestNews = "Stabilna pozycja na Slasku.",
                NewsSource = "Farmer.pl", NewsSourceUrl = "https://www.farmer.pl", NewsDate = new DateTime(2026, 1, 3),
                Strengths = "Silna pozycja na Slasku",
                Weaknesses = "Ograniczona skala",
                OpportunitiesForUs = "Mozemy konkurowac o klientow ze Slaska",
                ThreatsFromThem = "Konkuruja o tych samych klientow hurtowych",
                Website = "", Address = "Koziegłowy, woj. slaskie"
            });

            ExtendedCompetitors.Add(new ExtendedCompetitor
            {
                Id = 11, Name = "Res-Drob Sp. z o.o.", ShortName = "Res-Drob",
                Owner = "Polski prywatny", OwnerCountry = "Polska", CountryFlag = "🇵🇱",
                Latitude = 51.4, Longitude = 21.15, City = "Radom region", Voivodeship = "mazowieckie", DistanceFromUsKm = 110,
                RevenueMillionPln = 200, CapacityPerDay = 50000, Employees = 250, NumberOfPlants = 1, MarketSharePercent = 1.0m,
                Tier = 2, ThreatLevel = 50,
                MainProducts = new List<string> { "Filet", "Tuszka", "Elementy" },
                Certifications = new List<string> { "IFS", "BRC" },
                MainClients = new List<string> { "Hurt", "Sieci regionalne" },
                CompanyHistory = "Sredniej wielkosci ubojnia z regionu radomskiego.",
                LatestNews = "Rozbudowa mocy produkcyjnych planowana na 2026.",
                NewsSource = "Portal Spozywczy", NewsSourceUrl = "https://www.portalspozywczy.pl", NewsDate = new DateTime(2026, 1, 22),
                Strengths = "Dobra lokalizacja (centralna PL)",
                Weaknesses = "Srednia skala",
                OpportunitiesForUs = "Mozemy byc alternatywa dla ich klientow",
                ThreatsFromThem = "Blisko geograficznie, podobna skala",
                Website = "https://www.res-drob.pl", Address = "Region Radom"
            });

            ExtendedCompetitors.Add(new ExtendedCompetitor
            {
                Id = 12, Name = "Zakład Drobiarski Gzella", ShortName = "Gzella",
                Owner = "Rodzina Gzella", OwnerCountry = "Polska", CountryFlag = "🇵🇱",
                Latitude = 52.4, Longitude = 16.9, City = "Poznan region", Voivodeship = "wielkopolskie", DistanceFromUsKm = 210,
                RevenueMillionPln = 300, CapacityPerDay = 60000, Employees = 300, NumberOfPlants = 1, MarketSharePercent = 1.2m,
                Tier = 2, ThreatLevel = 40,
                MainProducts = new List<string> { "Tuszka", "Filet", "Wedliny drobiowe" },
                Certifications = new List<string> { "IFS", "ISO" },
                MainClients = new List<string> { "Sieci Wielkopolska", "Hurt" },
                CompanyHistory = "Tradycyjna firma rodzinna z Wielkopolski.",
                LatestNews = "Inwestycje w linie przetworcze.",
                NewsSource = "Glos Wielkopolski", NewsSourceUrl = "https://www.gloswielkopolski.pl", NewsDate = new DateTime(2026, 1, 15),
                Strengths = "Silna marka lokalna",
                Weaknesses = "Ograniczony zasieg geograficzny",
                OpportunitiesForUs = "Nie konkurujemy bezposrednio",
                ThreatsFromThem = "Mogą ekspandowac na wschod",
                Website = "", Address = "Region Poznania"
            });

            // Lokalizacja NASZA dla porownania
            ExtendedCompetitors.Add(new ExtendedCompetitor
            {
                Id = 99, Name = "UBOJNIA PIORKOWSCY (MY)", ShortName = "MY",
                Owner = "Rodzina Piorkowscy", OwnerCountry = "Polska", CountryFlag = "🇵🇱",
                Latitude = 51.7944, Longitude = 19.7569, City = "Brzeziny", Voivodeship = "lodzkie", DistanceFromUsKm = 0,
                RevenueMillionPln = 50, CapacityPerDay = 25000, Employees = 80, NumberOfPlants = 1, MarketSharePercent = 0.3m,
                Tier = 3, ThreatLevel = 0,
                MainProducts = new List<string> { "Tuszka", "Filet", "Elementy" },
                Certifications = new List<string> { "IFS Higher Level", "Weterynaryjny" },
                MainClients = new List<string> { "Dino", "Makro", "Selgros", "Carrefour", "Hurt" },
                CompanyHistory = "Rodzinna ubojnia z Brzezin. Fokus na jakosc i swiezosc.",
                LatestNews = "Nasz zaklad - centrum naszej dzialalnosci",
                NewsSource = "", NewsSourceUrl = "", NewsDate = DateTime.Today,
                Strengths = "Swiezosc, elastycznosc, lokalna marka",
                Weaknesses = "Mala skala vs giganci",
                OpportunitiesForUs = "Wzrost z Dino, nowi klienci",
                ThreatsFromThem = "N/A",
                Website = "", Address = "Brzeziny, woj. lodzkie"
            });
        }

        private void LoadRetailChainsExtended()
        {
            RetailChainsExtended.Clear();

            RetailChainsExtended.Add(new RetailChainExtended
            {
                Id = 1, ChainName = "Biedronka", OwnerCompany = "Jeronimo Martins", OwnerCountry = "Portugalia", CountryFlag = "🇵🇹",
                FiletPrice = 22.99m, FiletPricePromo = 18.99m, TuszkaPrice = 9.99m, TuszkaPricePromo = 7.99m,
                UdkoPrice = 12.99m, UdkoPricePromo = 9.99m, SkrzydloPrice = 9.99m, PodrudziePrice = 7.99m,
                Source = "Gazetka promocyjna", SourceUrl = "https://www.biedronka.pl/pl/gazetki-promocyjne",
                CheckDate = new DateTime(2026, 2, 1), PromoValidUntil = "05.02.2026",
                StoreCount = 3500, Regions = "Cala Polska", IsOurClient = false, OurHandlowiec = "-",
                Notes = "Najwieksza siec, bardzo konkurencyjne ceny. Przetarg do 28.02!"
            });

            RetailChainsExtended.Add(new RetailChainExtended
            {
                Id = 2, ChainName = "Lidl", OwnerCompany = "Schwarz Gruppe", OwnerCountry = "Niemcy", CountryFlag = "🇩🇪",
                FiletPrice = 23.49m, FiletPricePromo = 19.99m, TuszkaPrice = 10.49m, TuszkaPricePromo = 8.49m,
                UdkoPrice = 13.49m, UdkoPricePromo = 10.99m, SkrzydloPrice = 10.49m, PodrudziePrice = 8.49m,
                Source = "Gazetka promocyjna", SourceUrl = "https://www.lidl.pl/c/nasze-gazetki/s10005918",
                CheckDate = new DateTime(2026, 2, 1), PromoValidUntil = "04.02.2026",
                StoreCount = 850, Regions = "Cala Polska", IsOurClient = false, OurHandlowiec = "-",
                Notes = "Silny gracz, premium quality image"
            });

            RetailChainsExtended.Add(new RetailChainExtended
            {
                Id = 3, ChainName = "Kaufland", OwnerCompany = "Schwarz Gruppe", OwnerCountry = "Niemcy", CountryFlag = "🇩🇪",
                FiletPrice = 24.99m, FiletPricePromo = 0, TuszkaPrice = 10.99m, TuszkaPricePromo = 0,
                UdkoPrice = 13.99m, UdkoPricePromo = 0, SkrzydloPrice = 10.99m, PodrudziePrice = 8.99m,
                Source = "Cennik online", SourceUrl = "https://www.kaufland.pl",
                CheckDate = new DateTime(2026, 1, 30), PromoValidUntil = "",
                StoreCount = 230, Regions = "Duze miasta", IsOurClient = false, OurHandlowiec = "-",
                Notes = "Format hipermarket, wieksze opakowania"
            });

            RetailChainsExtended.Add(new RetailChainExtended
            {
                Id = 4, ChainName = "Dino", OwnerCompany = "Dino Polska S.A.", OwnerCountry = "Polska", CountryFlag = "🇵🇱",
                FiletPrice = 24.49m, FiletPricePromo = 21.99m, TuszkaPrice = 10.49m, TuszkaPricePromo = 0,
                UdkoPrice = 14.49m, UdkoPricePromo = 0, SkrzydloPrice = 11.49m, PodrudziePrice = 9.49m,
                Source = "Wizyta w sklepie", SourceUrl = "https://www.marketdino.pl",
                CheckDate = new DateTime(2026, 2, 1), PromoValidUntil = "07.02.2026",
                StoreCount = 2500, Regions = "Cala Polska (300 nowych w 2026!)", IsOurClient = true, OurHandlowiec = "Jola",
                Notes = "NASZ KLIENT! 250 palet/mies. Cel: 500 palet. Polski kapital!"
            });

            RetailChainsExtended.Add(new RetailChainExtended
            {
                Id = 5, ChainName = "Carrefour", OwnerCompany = "Carrefour SA", OwnerCountry = "Francja", CountryFlag = "🇫🇷",
                FiletPrice = 25.99m, FiletPricePromo = 22.99m, TuszkaPrice = 11.99m, TuszkaPricePromo = 0,
                UdkoPrice = 14.99m, UdkoPricePromo = 0, SkrzydloPrice = 11.99m, PodrudziePrice = 9.99m,
                Source = "Carrefour online", SourceUrl = "https://www.carrefour.pl",
                CheckDate = new DateTime(2026, 1, 30), PromoValidUntil = "",
                StoreCount = 900, Regions = "Duze miasta", IsOurClient = true, OurHandlowiec = "Teresa",
                Notes = "NASZ KLIENT! 340 palet/mies. Trend spadkowy - dzialac!"
            });

            RetailChainsExtended.Add(new RetailChainExtended
            {
                Id = 6, ChainName = "Auchan", OwnerCompany = "Groupe Auchan", OwnerCountry = "Francja", CountryFlag = "🇫🇷",
                FiletPrice = 24.49m, FiletPricePromo = 0, TuszkaPrice = 10.99m, TuszkaPricePromo = 0,
                UdkoPrice = 13.99m, UdkoPricePromo = 0, SkrzydloPrice = 10.49m, PodrudziePrice = 8.99m,
                Source = "Auchan online", SourceUrl = "https://www.auchan.pl",
                CheckDate = new DateTime(2026, 1, 28), PromoValidUntil = "",
                StoreCount = 75, Regions = "Duze miasta", IsOurClient = false, OurHandlowiec = "-",
                Notes = "Potencjalny klient - duze hipermarkety"
            });

            RetailChainsExtended.Add(new RetailChainExtended
            {
                Id = 7, ChainName = "Makro", OwnerCompany = "Metro AG", OwnerCountry = "Niemcy", CountryFlag = "🇩🇪",
                FiletPrice = 19.90m, FiletPricePromo = 0, TuszkaPrice = 8.50m, TuszkaPricePromo = 0,
                UdkoPrice = 10.90m, UdkoPricePromo = 0, SkrzydloPrice = 8.90m, PodrudziePrice = 6.90m,
                Source = "Cennik hurtowy", SourceUrl = "https://www.makro.pl",
                CheckDate = new DateTime(2026, 1, 30), PromoValidUntil = "",
                StoreCount = 30, Regions = "Duze miasta (C&C)", IsOurClient = true, OurHandlowiec = "Ania",
                Notes = "NASZ KLIENT! 480 palet/mies. Ceny hurtowe. B2B."
            });

            RetailChainsExtended.Add(new RetailChainExtended
            {
                Id = 8, ChainName = "Selgros", OwnerCompany = "Transgourmet/Coop", OwnerCountry = "Szwajcaria", CountryFlag = "🇨🇭",
                FiletPrice = 20.50m, FiletPricePromo = 0, TuszkaPrice = 8.90m, TuszkaPricePromo = 0,
                UdkoPrice = 11.20m, UdkoPricePromo = 0, SkrzydloPrice = 9.20m, PodrudziePrice = 7.20m,
                Source = "Cennik hurtowy", SourceUrl = "https://www.selgros.pl",
                CheckDate = new DateTime(2026, 1, 30), PromoValidUntil = "",
                StoreCount = 20, Regions = "Duze miasta (C&C)", IsOurClient = true, OurHandlowiec = "Ania",
                Notes = "NASZ KLIENT! 420 palet/mies. Trend spadkowy (-80)."
            });

            RetailChainsExtended.Add(new RetailChainExtended
            {
                Id = 9, ChainName = "Stokrotka", OwnerCompany = "Maxima Gruppe", OwnerCountry = "Litwa", CountryFlag = "🇱🇹",
                FiletPrice = 23.99m, FiletPricePromo = 0, TuszkaPrice = 10.99m, TuszkaPricePromo = 0,
                UdkoPrice = 13.49m, UdkoPricePromo = 0, SkrzydloPrice = 10.99m, PodrudziePrice = 8.99m,
                Source = "Stokrotka online", SourceUrl = "https://www.stokrotka.pl",
                CheckDate = new DateTime(2026, 1, 28), PromoValidUntil = "",
                StoreCount = 700, Regions = "Wschodnia i centralna PL", IsOurClient = true, OurHandlowiec = "Maja",
                Notes = "NASZ KLIENT! 280 palet/mies. Trend wzrostowy."
            });

            RetailChainsExtended.Add(new RetailChainExtended
            {
                Id = 10, ChainName = "Netto", OwnerCompany = "Salling Group", OwnerCountry = "Dania", CountryFlag = "🇩🇰",
                FiletPrice = 21.99m, FiletPricePromo = 17.99m, TuszkaPrice = 9.49m, TuszkaPricePromo = 0,
                UdkoPrice = 11.99m, UdkoPricePromo = 0, SkrzydloPrice = 9.49m, PodrudziePrice = 7.49m,
                Source = "Gazetka", SourceUrl = "https://www.netto.pl",
                CheckDate = new DateTime(2026, 1, 30), PromoValidUntil = "",
                StoreCount = 400, Regions = "Cala Polska", IsOurClient = false, OurHandlowiec = "-",
                Notes = "Potencjalny klient - dyskont, dobre ceny"
            });

            RetailChainsExtended.Add(new RetailChainExtended
            {
                Id = 11, ChainName = "Zabka", OwnerCompany = "CVC Capital Partners", OwnerCountry = "UK", CountryFlag = "🇬🇧",
                FiletPrice = 27.99m, FiletPricePromo = 0, TuszkaPrice = 0, TuszkaPricePromo = 0,
                UdkoPrice = 0, UdkoPricePromo = 0, SkrzydloPrice = 0, PodrudziePrice = 0,
                Source = "Zabka app", SourceUrl = "https://www.zabka.pl",
                CheckDate = new DateTime(2026, 1, 30), PromoValidUntil = "",
                StoreCount = 10000, Regions = "Cala Polska", IsOurClient = false, OurHandlowiec = "-",
                Notes = "Convenience, tylko produkty przetworzone (kanapki)"
            });

            RetailChainsExtended.Add(new RetailChainExtended
            {
                Id = 12, ChainName = "Intermarche", OwnerCompany = "Muszkieterowie", OwnerCountry = "Francja", CountryFlag = "🇫🇷",
                FiletPrice = 23.99m, FiletPricePromo = 0, TuszkaPrice = 10.49m, TuszkaPricePromo = 0,
                UdkoPrice = 13.49m, UdkoPricePromo = 0, SkrzydloPrice = 10.49m, PodrudziePrice = 8.49m,
                Source = "Intermarche online", SourceUrl = "https://www.intermarche.pl",
                CheckDate = new DateTime(2026, 1, 28), PromoValidUntil = "",
                StoreCount = 230, Regions = "Cala Polska", IsOurClient = false, OurHandlowiec = "-",
                Notes = "Potencjalny klient - supermarkety"
            });
        }

        private void LoadPotentialClients()
        {
            PotentialClients.Clear();

            PotentialClients.Add(new PotentialClient
            {
                Id = 1, CompanyName = "Siec Netto Polska", Industry = "Siec detaliczna (dyskont)",
                Description = "Dunski dyskont z 400+ sklepami. Poszukuja lokalnych dostawcow drobiu.",
                Location = "Cala Polska", Region = "Centrala: Motaniec",
                EstimatedVolumePerMonth = 300, OpportunityScore = 75,
                ContactPerson = "Dzial zakupow", ContactEmail = "zakupy@netto.pl", ContactPhone = "61 XXX XX XX",
                Website = "https://www.netto.pl",
                LatestNews = "Netto oglasza program 'Lokalny Dostawca 2026' - poszukuja producentow z centralnej Polski",
                NewsSource = "Portal Spozywczy", NewsSourceUrl = "https://www.portalspozywczy.pl/handel/wiadomosci/netto-lokalny-dostawca",
                NewsDate = new DateTime(2026, 1, 28),
                RecommendedAction = "Zgloszenie do programu 'Lokalny Dostawca'. Kontakt z dzialem zakupow.",
                AssignedTo = "Jola", Priority = SeverityLevel.Positive
            });

            PotentialClients.Add(new PotentialClient
            {
                Id = 2, CompanyName = "Auchan Polska", Industry = "Siec detaliczna (hipermarket)",
                Description = "Francuska siec hipermarketow. 75 sklepow w duzych miastach.",
                Location = "Duze miasta", Region = "Centrala: Warszawa",
                EstimatedVolumePerMonth = 400, OpportunityScore = 60,
                ContactPerson = "Category Manager Mieso", ContactEmail = "", ContactPhone = "",
                Website = "https://www.auchan.pl",
                LatestNews = "Auchan poszukuje dostawcow swiezego drobiu z certyfikatem IFS Higher Level",
                NewsSource = "Detal Dzisiaj", NewsSourceUrl = "https://www.dlahandlu.pl",
                NewsDate = new DateTime(2026, 1, 22),
                RecommendedAction = "Przygotowac prezentacje firmy. Podkreslic IFS Higher Level i swiezosc.",
                AssignedTo = "Teresa", Priority = SeverityLevel.Positive
            });

            PotentialClients.Add(new PotentialClient
            {
                Id = 3, CompanyName = "Siec Restauracji Sfinks (Sphinx, Chlopskie Jadlo)", Industry = "HoReCa",
                Description = "Najwieksza polska siec gastronomiczna. 200+ restauracji.",
                Location = "Cala Polska", Region = "Centrala: Piaseczno",
                EstimatedVolumePerMonth = 150, OpportunityScore = 55,
                ContactPerson = "Dzial zaopatrzenia", ContactEmail = "", ContactPhone = "",
                Website = "https://www.sfinks.pl",
                LatestNews = "Sfinks Polska szuka nowych dostawcow drobiu po problemach z dotychczasowym",
                NewsSource = "HoReCa Biznes", NewsSourceUrl = "https://www.horecabiznes.pl",
                NewsDate = new DateTime(2026, 1, 20),
                RecommendedAction = "Kontakt telefoniczny. Oferta na filet i udka.",
                AssignedTo = "Nieprzypisany", Priority = SeverityLevel.Warning
            });

            PotentialClients.Add(new PotentialClient
            {
                Id = 4, CompanyName = "Zaklady Miesne Madej & Wrobel", Industry = "Przetworstwo",
                Description = "Producent wedlin i produktow miesnych. Szuka dostawcy surowca drobiowego.",
                Location = "Wodzislaw Slaski", Region = "Slaskie",
                EstimatedVolumePerMonth = 200, OpportunityScore = 70,
                ContactPerson = "Dyrektor ds. zakupow", ContactEmail = "", ContactPhone = "",
                Website = "https://www.madejwrobel.pl",
                LatestNews = "Firma rozbudowuje linie produkcyjna i poszukuje stalych dostawcow kurczaka",
                NewsSource = "Farmer.pl", NewsSourceUrl = "https://www.farmer.pl",
                NewsDate = new DateTime(2026, 1, 25),
                RecommendedAction = "Spotkanie handlowe. Oferta na korpusy i elementy do przetwórstwa.",
                AssignedTo = "Nieprzypisany", Priority = SeverityLevel.Positive
            });

            PotentialClients.Add(new PotentialClient
            {
                Id = 5, CompanyName = "Siec Polomarket", Industry = "Siec detaliczna (supermarket)",
                Description = "Polska siec supermarketow, 300+ sklepow w centralnej Polsce.",
                Location = "Centralna Polska", Region = "Centrala: Giebultow",
                EstimatedVolumePerMonth = 250, OpportunityScore = 80,
                ContactPerson = "Kupiec kategoria mieso", ContactEmail = "", ContactPhone = "",
                Website = "https://www.polomarket.pl",
                LatestNews = "Polomarket otwiera 30 nowych sklepow w 2026 i szuka dostawcow swiezego miesa",
                NewsSource = "Portal Spozywczy", NewsSourceUrl = "https://www.portalspozywczy.pl",
                NewsDate = new DateTime(2026, 1, 30),
                RecommendedAction = "PRIORYTET! Blisko nas geograficznie. Umowic spotkanie.",
                AssignedTo = "Jola", Priority = SeverityLevel.Critical
            });

            PotentialClients.Add(new PotentialClient
            {
                Id = 6, CompanyName = "Eurocash (Delikatesy Centrum, Lewiatan)", Industry = "Hurt i sieci franczyzowe",
                Description = "Najwiekszy dystrybutor FMCG w Polsce. Obsluguje 15000+ sklepow.",
                Location = "Cala Polska", Region = "Centrala: Komorniki",
                EstimatedVolumePerMonth = 500, OpportunityScore = 50,
                ContactPerson = "Dzial zakupow fresh", ContactEmail = "", ContactPhone = "",
                Website = "https://www.eurocash.pl",
                LatestNews = "Eurocash rozwija kategorie fresh i szuka lokalnych dostawcow",
                NewsSource = "Wiadomosci Handlowe", NewsSourceUrl = "https://www.wiadomoscihandlowe.pl",
                NewsDate = new DateTime(2026, 1, 18),
                RecommendedAction = "Duzy gracz, trudne negocjacje. Przygotowac solidna oferte.",
                AssignedTo = "Nieprzypisany", Priority = SeverityLevel.Warning
            });
        }

        private void LoadExportData()
        {
            ExportData.Clear();

            // Polski eksport drobiu 2025 (dane szacunkowe)
            ExportData.Add(new ExportImportData { Country = "Niemcy", CountryFlag = "🇩🇪", ProductType = "Drob ogolem", VolumeThousandTons = 320, ChangePercent = 2.5m, ValueMillionEur = 640, IsExport = true, Year = 2025, Source = "GUS/KRD-IG", SourceUrl = "https://www.krd-ig.pl" });
            ExportData.Add(new ExportImportData { Country = "UK", CountryFlag = "🇬🇧", ProductType = "Drob ogolem", VolumeThousandTons = 180, ChangePercent = -5.2m, ValueMillionEur = 360, IsExport = true, Year = 2025, Source = "GUS/KRD-IG", SourceUrl = "https://www.krd-ig.pl" });
            ExportData.Add(new ExportImportData { Country = "Francja", CountryFlag = "🇫🇷", ProductType = "Drob ogolem", VolumeThousandTons = 120, ChangePercent = 8.1m, ValueMillionEur = 250, IsExport = true, Year = 2025, Source = "GUS/KRD-IG", SourceUrl = "https://www.krd-ig.pl" });
            ExportData.Add(new ExportImportData { Country = "Holandia", CountryFlag = "🇳🇱", ProductType = "Drob ogolem", VolumeThousandTons = 95, ChangePercent = 3.2m, ValueMillionEur = 190, IsExport = true, Year = 2025, Source = "GUS/KRD-IG", SourceUrl = "https://www.krd-ig.pl" });
            ExportData.Add(new ExportImportData { Country = "Czechy", CountryFlag = "🇨🇿", ProductType = "Drob ogolem", VolumeThousandTons = 85, ChangePercent = 1.8m, ValueMillionEur = 145, IsExport = true, Year = 2025, Source = "GUS/KRD-IG", SourceUrl = "https://www.krd-ig.pl" });
            ExportData.Add(new ExportImportData { Country = "Belgia", CountryFlag = "🇧🇪", ProductType = "Drob ogolem", VolumeThousandTons = 70, ChangePercent = 12.5m, ValueMillionEur = 140, IsExport = true, Year = 2025, Source = "GUS/KRD-IG", SourceUrl = "https://www.krd-ig.pl" });
            ExportData.Add(new ExportImportData { Country = "Slowacja", CountryFlag = "🇸🇰", ProductType = "Drob ogolem", VolumeThousandTons = 55, ChangePercent = 4.5m, ValueMillionEur = 95, IsExport = true, Year = 2025, Source = "GUS/KRD-IG", SourceUrl = "https://www.krd-ig.pl" });
            ExportData.Add(new ExportImportData { Country = "Hiszpania", CountryFlag = "🇪🇸", ProductType = "Drob ogolem", VolumeThousandTons = 45, ChangePercent = 15.2m, ValueMillionEur = 90, IsExport = true, Year = 2025, Source = "GUS/KRD-IG", SourceUrl = "https://www.krd-ig.pl" });
            ExportData.Add(new ExportImportData { Country = "Wlochy", CountryFlag = "🇮🇹", ProductType = "Drob ogolem", VolumeThousandTons = 40, ChangePercent = 6.8m, ValueMillionEur = 85, IsExport = true, Year = 2025, Source = "GUS/KRD-IG", SourceUrl = "https://www.krd-ig.pl" });
            ExportData.Add(new ExportImportData { Country = "Inne UE", CountryFlag = "🇪🇺", ProductType = "Drob ogolem", VolumeThousandTons = 150, ChangePercent = 3.5m, ValueMillionEur = 280, IsExport = true, Year = 2025, Source = "GUS/KRD-IG", SourceUrl = "https://www.krd-ig.pl" });
            ExportData.Add(new ExportImportData { Country = "Kraje trzecie", CountryFlag = "🌍", ProductType = "Drob ogolem", VolumeThousandTons = 90, ChangePercent = -8.5m, ValueMillionEur = 150, IsExport = true, Year = 2025, Source = "GUS/KRD-IG", SourceUrl = "https://www.krd-ig.pl" });
        }

        private void LoadImportData()
        {
            ImportData.Clear();

            // Import do Polski 2025
            ImportData.Add(new ExportImportData { Country = "Brazylia", CountryFlag = "🇧🇷", ProductType = "Filet mrozony", VolumeThousandTons = 45, ChangePercent = 18.5m, ValueMillionEur = 85, IsExport = false, Year = 2025, Source = "GUS/Eurostat", SourceUrl = "https://ec.europa.eu/eurostat" });
            ImportData.Add(new ExportImportData { Country = "Ukraina", CountryFlag = "🇺🇦", ProductType = "Drob ogolem", VolumeThousandTons = 35, ChangePercent = 25.2m, ValueMillionEur = 55, IsExport = false, Year = 2025, Source = "GUS/Eurostat", SourceUrl = "https://ec.europa.eu/eurostat" });
            ImportData.Add(new ExportImportData { Country = "Niemcy", CountryFlag = "🇩🇪", ProductType = "Produkty przetw.", VolumeThousandTons = 25, ChangePercent = 3.5m, ValueMillionEur = 65, IsExport = false, Year = 2025, Source = "GUS/Eurostat", SourceUrl = "https://ec.europa.eu/eurostat" });
            ImportData.Add(new ExportImportData { Country = "Holandia", CountryFlag = "🇳🇱", ProductType = "Drob ogolem", VolumeThousandTons = 18, ChangePercent = 5.2m, ValueMillionEur = 42, IsExport = false, Year = 2025, Source = "GUS/Eurostat", SourceUrl = "https://ec.europa.eu/eurostat" });
            ImportData.Add(new ExportImportData { Country = "Tajlandia", CountryFlag = "🇹🇭", ProductType = "Filet mrozony", VolumeThousandTons = 12, ChangePercent = -2.5m, ValueMillionEur = 28, IsExport = false, Year = 2025, Source = "GUS/Eurostat", SourceUrl = "https://ec.europa.eu/eurostat" });
            ImportData.Add(new ExportImportData { Country = "Argentyna", CountryFlag = "🇦🇷", ProductType = "Filet mrozony", VolumeThousandTons = 8, ChangePercent = 35.0m, ValueMillionEur = 15, IsExport = false, Year = 2025, Source = "GUS/Eurostat", SourceUrl = "https://ec.europa.eu/eurostat" });
        }

        private void LoadSubsidies()
        {
            Subsidies.Clear();

            Subsidies.Add(new SubsidyGrant
            {
                Id = 1,
                Name = "Modernizacja gospodarstw rolnych - obszar D (zielone inwestycje)",
                Provider = "ARiMR",
                Description = "Dofinansowanie na inwestycje zwiazane z ochrona srodowiska, OZE, efektywnoscia energetyczna w zakladach przetworstwa rolno-spozywczego.",
                MaxAmountPln = 500000,
                CoFinancingPercent = 50,
                DeadlineDate = new DateTime(2026, 3, 31),
                EligibleFor = "MSP w sektorze przetworstwa, w tym ubojnie",
                RequiredDocuments = "Biznesplan, kosztorys inwestycji, dokumenty rejestrowe, zaswiadczenia",
                ApplicationUrl = "https://www.arimr.gov.pl/pomoc-unijna/prow-2021-2027",
                Priority = SeverityLevel.Critical,
                IsActive = true,
                Tags = new List<string> { "OZE", "modernizacja", "energia" }
            });

            Subsidies.Add(new SubsidyGrant
            {
                Id = 2,
                Name = "Kredyt technologiczny (BGK)",
                Provider = "BGK / NCBiR",
                Description = "Kredyt na wdrozenie nowych technologii z premia technologiczna (umorzenie czesci kredytu).",
                MaxAmountPln = 6000000,
                CoFinancingPercent = 70,
                DeadlineDate = new DateTime(2026, 6, 30),
                EligibleFor = "MSP wdrazajace innowacje technologiczne",
                RequiredDocuments = "Opinia o innowacyjnosci, biznesplan, dokumenty kredytowe",
                ApplicationUrl = "https://www.bgk.pl/przedsiebiorstwa/kredyt-technologiczny",
                Priority = SeverityLevel.Warning,
                IsActive = true,
                Tags = new List<string> { "technologia", "innowacja", "kredyt" }
            });

            Subsidies.Add(new SubsidyGrant
            {
                Id = 3,
                Name = "Gospodarka o obiegu zamknietym (NFOSiGW)",
                Provider = "NFOSiGW",
                Description = "Dofinansowanie na projekty dot. gospodarki odpadami, GOZ, zmniejszenia emisji.",
                MaxAmountPln = 2000000,
                CoFinancingPercent = 60,
                DeadlineDate = new DateTime(2026, 4, 15),
                EligibleFor = "Przedsiebiorstwa realizujace projekty proekologiczne",
                RequiredDocuments = "Audyt energetyczny/srodowiskowy, kosztorys, pozwolenia",
                ApplicationUrl = "https://www.gov.pl/nfosigw",
                Priority = SeverityLevel.Warning,
                IsActive = true,
                Tags = new List<string> { "ekologia", "odpady", "GOZ" }
            });

            Subsidies.Add(new SubsidyGrant
            {
                Id = 4,
                Name = "Wsparcie MSP w promocji marek produktowych - Go to Brand",
                Provider = "PARP",
                Description = "Dofinansowanie udzialu w targach miedzynarodowych i misjach gospodarczych.",
                MaxAmountPln = 500000,
                CoFinancingPercent = 50,
                DeadlineDate = new DateTime(2026, 5, 31),
                EligibleFor = "MSP z branzy spozywczej chcace eksportowac",
                RequiredDocuments = "Strategia eksportowa, dokumenty rejestrowe",
                ApplicationUrl = "https://www.parp.gov.pl/go-to-brand",
                Priority = SeverityLevel.Positive,
                IsActive = true,
                Tags = new List<string> { "eksport", "targi", "promocja" }
            });

            Subsidies.Add(new SubsidyGrant
            {
                Id = 5,
                Name = "Dofinansowanie fotowoltaiki dla przedsiebiorstw (Moj Prad dla Firm)",
                Provider = "NFOSiGW",
                Description = "Dotacja na instalacje PV i magazyny energii dla firm.",
                MaxAmountPln = 300000,
                CoFinancingPercent = 50,
                DeadlineDate = new DateTime(2026, 12, 31),
                EligibleFor = "Przedsiebiorstwa, w tym MSP",
                RequiredDocuments = "Projekt instalacji, kosztorys, umowa z wykonawca",
                ApplicationUrl = "https://www.gov.pl/mojprad-dla-firm",
                Priority = SeverityLevel.Positive,
                IsActive = true,
                Tags = new List<string> { "PV", "fotowoltaika", "energia" }
            });
        }

        private void LoadInternationalNews()
        {
            InternationalNews.Clear();

            InternationalNews.Add(new InternationalMarketNews
            {
                Id = 1, Country = "Brazylia", CountryFlag = "🇧🇷", Region = "Mercosur",
                Title = "Brazylia zwieksza eksport drobiu do UE o 25% — rekordowy styczen 2026",
                Summary = "Brazylijscy eksporterzy (BRF, JBS, Seara) notuja rekordowe dostawy do UE. Niskie koszty produkcji (kukurydza $180/t) pozwalaja na ceny 30% nizsze niz europejskie.",
                FullContent = "Brazylijski eksport drobiu do UE w styczniu 2026 osiagnal rekordowy poziom 42 tys. ton (+25% r/r). Glowni beneficjenci: BRF, JBS, Seara. Ceny brazylijskiego fileta mrozone: $3.20/kg FOB (ok. 13 zl/kg po doliczeniu transportu i cla). Polski filet swiezy: 17-19 zl/kg. Roznica: 30%.",
                ImpactOnPoland = "Presja cenowa na segment mrozony. Sieci moga wykorzystywac tani import jako leverage w negocjacjach. Nasze przewagi: swiezosc, lokalnosc, sledzialnosc.",
                ThreatLevel = SeverityLevel.Critical,
                Source = "ABPA / Reuters", SourceUrl = "https://www.abpa-br.org",
                PublishDate = new DateTime(2026, 2, 1),
                Tags = new List<string> { "Brazylia", "import", "ceny", "konkurencja" }
            });

            InternationalNews.Add(new InternationalMarketNews
            {
                Id = 2, Country = "Ukraina", CountryFlag = "🇺🇦", Region = "Europa Wschodnia",
                Title = "MHP (Ukraina) uruchamia nowa linie — zdolnosc eksportowa +100k ton/rok",
                Summary = "MHP, najwiekszy ukrainski producent drobiu, uruchomil nowa linie produkcyjna w Winnicy. Zdolnosc eksportowa do UE wzrosla o 100k ton rocznie.",
                FullContent = "MHP (Mironivsky Hliboproduct) zainwestowalo $150 mln w rozbudowe zakladu w Winnicy. Nowa linia: zdolnosc 300k sztuk/dzien. Eksport do UE (glownie Holandia, Niemcy, Polska) wzrosnie o 100k ton/rok. Ceny ukrainskiego drobiu: 15-20% nizsze niz polskie.",
                ImpactOnPoland = "Ukraina to rosnacie zagrozenie. Nizsze koszty pracy, tansza pasza. Bezcłowy dostep do UE. Moga przejmowac udzialy w rynku.",
                ThreatLevel = SeverityLevel.Warning,
                Source = "MHP / Interfax-Ukraine", SourceUrl = "https://www.mhp.com.ua",
                PublishDate = new DateTime(2026, 1, 28),
                Tags = new List<string> { "Ukraina", "MHP", "konkurencja", "eksport" }
            });

            InternationalNews.Add(new InternationalMarketNews
            {
                Id = 3, Country = "Francja", CountryFlag = "🇫🇷", Region = "UE",
                Title = "Francja: 68 ognisk HPAI — deficyt drobiu, ceny +15%",
                Summary = "Francja boryka sie z najgorsza epidemia HPAI od lat. 68 ognisk, 10 mln ptakow zlikwidowanych. Deficyt produkcji, ceny wzrosly o 15%.",
                FullContent = "Francuskie HPAI: 68 ognisk (stan na 01.02.2026), glownie pd-zach. Francja (Landes, Gers). 10 mln ptakow zlikwidowanych. Produkcja spadla o 12%. Ceny francuskiego drobiu +15% m/m. Import z Polski i Hiszpanii wzrasta.",
                ImpactOnPoland = "SZANSA! Francja potrzebuje importu. Polscy eksporterzy moga zwiekszyc dostawy. Ale uwaga: HPAI moze sie rozprzestrzeniac.",
                ThreatLevel = SeverityLevel.Positive,
                Source = "ITAVI / FAO", SourceUrl = "https://www.itavi.asso.fr",
                PublishDate = new DateTime(2026, 2, 2),
                Tags = new List<string> { "Francja", "HPAI", "eksport", "szansa" }
            });

            InternationalNews.Add(new InternationalMarketNews
            {
                Id = 4, Country = "Rumunia", CountryFlag = "🇷🇴", Region = "UE",
                Title = "Rumunia: Transavia inwestuje €80 mln — nowa ubojnia 400k/dzien",
                Summary = "Rumuński producent Transavia buduje nowa ubojnie o zdolnosci 400k sztuk/dzien. Cel: eksport do UE zachodniej.",
                FullContent = "Transavia (lider rynku rumunskiego) zainwestuje €80 mln w nowy zaklad. Lokalizacja: Alba Iulia. Zdolnosc: 400k szt./dzien (porownywalna z Cedrobem). Start: Q4 2026. Cel: eksport do Niemiec, Wloch, Hiszpanii.",
                ImpactOnPoland = "Rumunia staje sie konkurentem eksportowym. Nizsze koszty pracy niz w Polsce. Moga przejmowac kontrakty eksportowe.",
                ThreatLevel = SeverityLevel.Warning,
                Source = "Transavia / Ziarul Financiar", SourceUrl = "https://www.transavia.ro",
                PublishDate = new DateTime(2026, 1, 25),
                Tags = new List<string> { "Rumunia", "konkurencja", "eksport", "inwestycja" }
            });

            InternationalNews.Add(new InternationalMarketNews
            {
                Id = 5, Country = "Portugalia", CountryFlag = "🇵🇹", Region = "UE",
                Title = "Portugalia: JM (Biedronka) testuje dostawcow drobiu z Portugalii",
                Summary = "Jeronimo Martins testuje dostawy drobiu portugalskiego do polskich Biedronek. Pilotaz w 50 sklepach.",
                FullContent = "JM (wlasciciel Biedronki) testuje import drobiu z portugalskich zakladow Avicola do polskich sklepow. Pilotaz: 50 sklepow w Warszawie. Argument: 'integracja pionowa w grupie'. Ceny porownywalne z polskimi.",
                ImpactOnPoland = "Potencjalne zagrozenie dla polskich dostawcow Biedronki. Jesli pilotaz sie sprawdzi, JM moze zwiekszyc import z Portugalii.",
                ThreatLevel = SeverityLevel.Warning,
                Source = "Dinheiro Vivo / PAP", SourceUrl = "https://www.dinheirovivo.pt",
                PublishDate = new DateTime(2026, 1, 20),
                Tags = new List<string> { "Portugalia", "Biedronka", "import", "JM" }
            });

            InternationalNews.Add(new InternationalMarketNews
            {
                Id = 6, Country = "Niemcy", CountryFlag = "🇩🇪", Region = "UE",
                Title = "Niemcy: popyt na drob +8% — Polska glownym importerem",
                Summary = "Niemiecki rynek drobiu rosnie. Popyt +8% r/r. Polska pozostaje glownym dostawca (32% importu).",
                FullContent = "Niemiecki rynek drobiu w 2025: +8% r/r. Glowni importerzy: Polska (32%), Holandia (22%), Belgia (15%). Trend: rosnie popyt na drob ekologiczny i wellness. Polska moze skorzystac na trendzie.",
                ImpactOnPoland = "SZANSA! Niemcy to nasz glowny rynek eksportowy. Rosnacy popyt = wieksza sprzedaz. Ale rosnie konkurencja z Ukrainy i Rumunii.",
                ThreatLevel = SeverityLevel.Positive,
                Source = "Destatis / BLE", SourceUrl = "https://www.ble.de",
                PublishDate = new DateTime(2026, 1, 30),
                Tags = new List<string> { "Niemcy", "eksport", "szansa", "popyt" }
            });
        }

        private void LoadChartSeries()
        {
            ChartSeries.Clear();
            var today = DateTime.Today;

            // Cena skupu zywca - 30 dni
            var skupData = new ChartDataSeries
            {
                Name = "Cena skupu zywca",
                Color = "#C9A96E",
                Unit = "zl/kg",
                CurrentValue = 4.72m,
                ChangePercent = -0.4m,
                MinValue = 4.55m,
                MaxValue = 4.85m,
                AvgValue = 4.68m,
                Source = "farmer.pl",
                SourceUrl = "https://www.farmer.pl/ceny/zywiec-drobiowy"
            };
            for (int i = 30; i >= 0; i--)
            {
                skupData.DataPoints.Add(new PricePoint(today.AddDays(-i), 4.55m + (decimal)(Math.Sin(i * 0.3) * 0.15 + 0.15)));
            }
            ChartSeries.Add(skupData);

            // Kukurydza MATIF
            var kukurydzaData = new ChartDataSeries
            {
                Name = "Kukurydza MATIF",
                Color = "#6DAF6D",
                Unit = "EUR/t",
                CurrentValue = 192.50m,
                ChangePercent = -0.39m,
                MinValue = 185m,
                MaxValue = 210m,
                AvgValue = 195m,
                Source = "MATIF/Euronext",
                SourceUrl = "https://www.euronext.com/en/products/commodities/corn"
            };
            for (int i = 30; i >= 0; i--)
            {
                kukurydzaData.DataPoints.Add(new PricePoint(today.AddDays(-i), 185m + (decimal)(Math.Cos(i * 0.2) * 10 + 10)));
            }
            ChartSeries.Add(kukurydzaData);

            // Filet hurt
            var filetData = new ChartDataSeries
            {
                Name = "Filet z piersi (hurt)",
                Color = "#5AB8B8",
                Unit = "zl/kg",
                CurrentValue = 24.50m,
                ChangePercent = 2.1m,
                MinValue = 22.00m,
                MaxValue = 26.00m,
                AvgValue = 24.00m,
                Source = "farmer.pl",
                SourceUrl = "https://www.farmer.pl/ceny/drob"
            };
            for (int i = 30; i >= 0; i--)
            {
                filetData.DataPoints.Add(new PricePoint(today.AddDays(-i), 22m + (decimal)(Math.Sin(i * 0.15) * 2 + 2)));
            }
            ChartSeries.Add(filetData);

            // Tuszka hurt
            var tuszkaData = new ChartDataSeries
            {
                Name = "Tuszka (hurt)",
                Color = "#C05050",
                Unit = "zl/kg",
                CurrentValue = 7.33m,
                ChangePercent = -3.0m,
                MinValue = 7.00m,
                MaxValue = 8.50m,
                AvgValue = 7.60m,
                Source = "farmer.pl",
                SourceUrl = "https://www.farmer.pl/ceny/drob"
            };
            for (int i = 30; i >= 0; i--)
            {
                tuszkaData.DataPoints.Add(new PricePoint(today.AddDays(-i), 7.5m + (decimal)(Math.Sin(i * 0.25) * 0.5)));
            }
            ChartSeries.Add(tuszkaData);

            // Pszenica MATIF
            var pszericaData = new ChartDataSeries
            {
                Name = "Pszenica MATIF",
                Color = "#D4A035",
                Unit = "EUR/t",
                CurrentValue = 210m,
                ChangePercent = -0.25m,
                MinValue = 200m,
                MaxValue = 225m,
                AvgValue = 212m,
                Source = "MATIF/Euronext",
                SourceUrl = "https://www.euronext.com/en/products/commodities/wheat"
            };
            for (int i = 30; i >= 0; i--)
            {
                pszericaData.DataPoints.Add(new PricePoint(today.AddDays(-i), 200m + (decimal)(Math.Sin(i * 0.18) * 12 + 12)));
            }
            ChartSeries.Add(pszericaData);

            // Relacja zywiec/pasza
            var relacjaData = new ChartDataSeries
            {
                Name = "Relacja zywiec/pasza",
                Color = "#9B59B6",
                Unit = "",
                CurrentValue = 4.24m,
                ChangePercent = 25.0m,
                MinValue = 3.20m,
                MaxValue = 4.30m,
                AvgValue = 3.80m,
                Source = "Obliczenia wlasne",
                SourceUrl = ""
            };
            for (int i = 30; i >= 0; i--)
            {
                relacjaData.DataPoints.Add(new PricePoint(today.AddDays(-i), 3.8m + (decimal)(i * 0.015)));
            }
            ChartSeries.Add(relacjaData);
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
