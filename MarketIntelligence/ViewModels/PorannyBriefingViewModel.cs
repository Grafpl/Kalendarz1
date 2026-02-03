using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Kalendarz1.MarketIntelligence.Models;

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
            RetailPrices = new ObservableCollection<RetailPrice>();
            Farmers = new ObservableCollection<BriefingFarmer>();
            Clients = new ObservableCollection<BriefingClient>();
            CalendarEvents = new ObservableCollection<CalendarEvent>();
            Tasks = new ObservableCollection<BriefingTask>();
            Indicators = new ObservableCollection<PriceIndicator>();
            EuBenchmarks = new ObservableCollection<EuBenchmarkPrice>();
            ElementPrices = new ObservableCollection<ElementPrice>();
            FeedPrices = new ObservableCollection<FeedPrice>();
            SummarySegments = new ObservableCollection<SummarySegment>();

            // Initialize commands
            ToggleArticleCommand = new RelayCommand<BriefingArticle>(ToggleArticle);
            ChangeRoleCommand = new RelayCommand<string>(ChangeRole);
            OpenUrlCommand = new RelayCommand<string>(OpenUrl);
            FilterByCategoryCommand = new RelayCommand<string>(FilterByCategory);
            ToggleTasksPanelCommand = new RelayCommand(_ => ToggleTasksPanel());
            RefreshCommand = new RelayCommand(_ => Refresh());

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
        public ObservableCollection<RetailPrice> RetailPrices { get; }
        public ObservableCollection<BriefingFarmer> Farmers { get; }
        public ObservableCollection<BriefingClient> Clients { get; }
        public ObservableCollection<CalendarEvent> CalendarEvents { get; }
        public ObservableCollection<BriefingTask> Tasks { get; }
        public ObservableCollection<PriceIndicator> Indicators { get; }
        public ObservableCollection<EuBenchmarkPrice> EuBenchmarks { get; }
        public ObservableCollection<ElementPrice> ElementPrices { get; }
        public ObservableCollection<FeedPrice> FeedPrices { get; }
        public ObservableCollection<SummarySegment> SummarySegments { get; }

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
            // In future: reload data from services
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
            LoadRetailPrices();
            LoadFarmers();
            LoadClients();
            LoadCalendarEvents();
            LoadTasks();
            LoadEuBenchmarks();
            LoadElementPrices();
            LoadFeedPrices();
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
