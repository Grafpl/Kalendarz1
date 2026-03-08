using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ClosedXML.Excel;
using Kalendarz1.DashboardPrzychodu.Models;
using Kalendarz1.DashboardPrzychodu.Services;

namespace Kalendarz1.DashboardPrzychodu.Views
{
    /// <summary>
    /// Dashboard Przychodu Żywca - okno pokazujące w czasie rzeczywistym
    /// plan vs rzeczywiste przyjęcia żywca z prognozą produkcji
    /// Styl: Warm Industrial Ultra Kompaktowy
    /// </summary>
    public partial class DashboardPrzychoduWindow : Window
    {
        private readonly PrzychodService _przychodService;
        private readonly ObservableCollection<DostawaItem> _dostawy;
        private readonly ObservableCollection<PostepHarmonogramu> _postepyHarmonogramow;
        private readonly DispatcherTimer _autoRefreshTimer;
        private readonly DispatcherTimer _countdownTimer;
        private PodsumowanieDnia _podsumowanie;
        private PrognozaDnia _prognoza;
        private ICollectionView _dostawyView;
        private int _secondsToRefresh = 30;
        private bool _isLoading = false;

        // Trend tracking
        private decimal _poprzednieZwazone = 0;
        private DateTime? _trendStartTime = null;
        private DateTime _lastRefreshTime = DateTime.MinValue;
        private Storyboard _pulseStoryboard;

        // Tryb obliczania planu
        private bool _useNowyPlan = false;
        private decimal _origKgPlanDoZwazonych;
        private decimal _origOdchylenieKgSuma;

        // KPI icon customization
        private string _currentKpiTarget;

        // Śledzenie zmian hodowców (do pulsowania kart)
        private Dictionary<string, int> _poprzednieAutaZwazone = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private const int AUTO_REFRESH_SECONDS = 30;

        // Paleta kolorów hodowców - maksymalnie różnorodne, żadne podobne
        private static readonly Color[] HodowcaColorPalette = new[]
        {
            Color.FromRgb(239, 68, 68),    // CZERWONY
            Color.FromRgb(250, 204, 21),   // ŻÓŁTY
            Color.FromRgb(34, 197, 94),    // ZIELONY
            Color.FromRgb(59, 130, 246),   // NIEBIESKI
            Color.FromRgb(249, 115, 22),   // POMARAŃCZOWY
            Color.FromRgb(168, 85, 247),   // FIOLETOWY
            Color.FromRgb(6, 182, 212),    // CYAN
            Color.FromRgb(236, 72, 153),   // RÓŻOWY
            Color.FromRgb(132, 204, 22),   // LIMONKOWY
            Color.FromRgb(244, 63, 94),    // MALINOWY
            Color.FromRgb(20, 184, 166),   // MORSKI
            Color.FromRgb(245, 158, 11),   // BURSZTYNOWY
            Color.FromRgb(99, 102, 241),   // INDYGO
            Color.FromRgb(16, 185, 129),   // SZMARAGD
            Color.FromRgb(217, 70, 239),   // MAGENTA
            Color.FromRgb(251, 191, 36),   // ZŁOTY
        };
        private readonly Dictionary<string, Brush> _hodowcaColorMap = new Dictionary<string, Brush>(StringComparer.OrdinalIgnoreCase);

        public DashboardPrzychoduWindow()
        {
            InitializeComponent();

            _przychodService = new PrzychodService();
            _dostawy = new ObservableCollection<DostawaItem>();
            _postepyHarmonogramow = new ObservableCollection<PostepHarmonogramu>();
            _podsumowanie = new PodsumowanieDnia();
            _prognoza = new PrognozaDnia();

            // Konfiguracja DataGrid
            dgDostawy.ItemsSource = _dostawy;
            _dostawyView = CollectionViewSource.GetDefaultView(_dostawy);

            // Konfiguracja listy harmonogramow
            icHarmonogramy.ItemsSource = _postepyHarmonogramow;

            // Timer auto-odświeżania (co 30 sekund)
            _autoRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(AUTO_REFRESH_SECONDS)
            };
            _autoRefreshTimer.Tick += async (s, e) => await LoadDataAsync();

            // Timer odliczania do następnego odświeżenia
            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _countdownTimer.Tick += CountdownTimer_Tick;

            // Inicjalizacja animacji pulsowania
            InitializePulseAnimation();

            // Eventy
            dpData.SelectedDateChanged += async (s, e) =>
            {
                // Reset śledzenia przy zmianie daty
                _poprzednieZwazone = 0;
                _trendStartTime = null;
                _poprzednieAutaZwazone.Clear();
                UpdateDateDisplay();
                await LoadDataAsync();
            };
            txtSearch.TextChanged += TxtSearch_TextChanged;
            Loaded += async (s, e) => await InitializeAsync();
            Closing += DashboardPrzychoduWindow_Closing;
        }

        /// <summary>
        /// Inicjalizacja animacji pulsowania dla kafelka odchylenia (styl Warm Industrial)
        /// </summary>
        private void InitializePulseAnimation()
        {
            _pulseStoryboard = new Storyboard();
            _pulseStoryboard.RepeatBehavior = RepeatBehavior.Forever;
            _pulseStoryboard.AutoReverse = true;

            var colorAnimation = new ColorAnimation
            {
                From = Color.FromRgb(239, 68, 68),    // #ef4444 (red)
                To = Color.FromRgb(252, 165, 165),    // #fca5a5 (light red)
                Duration = TimeSpan.FromMilliseconds(700)
            };

            Storyboard.SetTarget(colorAnimation, borderOdchylenie);
            Storyboard.SetTargetProperty(colorAnimation, new PropertyPath("BorderBrush.Color"));

            _pulseStoryboard.Children.Add(colorAnimation);
        }

        /// <summary>
        /// Inicjalizacja okna
        /// </summary>
        private async System.Threading.Tasks.Task InitializeAsync()
        {
            Debug.WriteLine("[DashboardPrzychodu] Inicjalizacja okna...");

            // Ustaw dzisiejszą datę
            dpData.SelectedDate = DateTime.Today;
            UpdateDateDisplay();

            // Pierwsze ładowanie
            await LoadDataAsync();

            // Uruchom timery
            _autoRefreshTimer.Start();
            _countdownTimer.Start();
            _secondsToRefresh = AUTO_REFRESH_SECONDS;

            Debug.WriteLine("[DashboardPrzychodu] Inicjalizacja zakończona. Auto-odświeżanie co 30s.");
        }

        /// <summary>
        /// Ładowanie danych z bazy
        /// </summary>
        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            if (_isLoading)
            {
                Debug.WriteLine("[DashboardPrzychodu] Już trwa ładowanie, pomijam...");
                return;
            }

            if (!_przychodService.CanRefresh)
            {
                Debug.WriteLine("[DashboardPrzychodu] Za szybko - minimalny interwał 5s");
                return;
            }

            var selectedDate = dpData.SelectedDate ?? DateTime.Today;

            try
            {
                _isLoading = true;
                ShowLoading("Aktualizacja danych...");

                Debug.WriteLine($"[DashboardPrzychodu] Pobieranie danych na {selectedDate:yyyy-MM-dd}");

                // Pobierz dane rownolegle
                var dostawyTask = _przychodService.GetDostawyAsync(selectedDate);
                var podsumowanieTask = _przychodService.GetPodsumowanieAsync(selectedDate);
                var prognozaTask = _przychodService.GetPrognozaDniaAsync(selectedDate);
                var harmonogramyTask = _przychodService.GetPostepyHarmonogramowAsync(selectedDate);
                var faktycznyTask = _przychodService.GetFaktycznyPrzychodAsync(selectedDate);

                await System.Threading.Tasks.Task.WhenAll(dostawyTask, podsumowanieTask, prognozaTask, harmonogramyTask, faktycznyTask);

                var noweDostawy = await dostawyTask;
                _podsumowanie = await podsumowanieTask;
                _prognoza = await prognozaTask;
                var noweHarmonogramy = await harmonogramyTask;
                var faktyczny = await faktycznyTask;

                // Uzupełnij podsumowanie o faktyczny przychód z Symfonia
                _podsumowanie.FaktKlasaAKg = faktyczny.KlasaA;
                _podsumowanie.FaktKlasaBKg = faktyczny.KlasaB;

                // Zapamiętaj oryginalne wartości planu (do przełączania Stare/Nowe)
                _origKgPlanDoZwazonych = _podsumowanie.KgPlanDoZwazonych;
                _origOdchylenieKgSuma = _podsumowanie.OdchylenieKgSuma;

                // Aktualizuj UI w watku UI
                await Dispatcher.InvokeAsync(() =>
                {
                    // Zachowaj selekcje i scroll przed odswiezeniem
                    int? selectedId = (dgDostawy.SelectedItem as DostawaItem)?.ID;
                    var scrollViewer = FindScrollViewer(dgDostawy);
                    double scrollOffset = scrollViewer?.VerticalOffset ?? 0;

                    // Aktualizuj kolekcje dostaw
                    _dostawy.Clear();
                    foreach (var dostawa in noweDostawy)
                    {
                        _dostawy.Add(dostawa);
                    }

                    // Przywroc selekcje
                    if (selectedId.HasValue)
                    {
                        var sel = _dostawy.FirstOrDefault(d => d.ID == selectedId.Value);
                        if (sel != null)
                            dgDostawy.SelectedItem = sel;
                    }

                    // Przywroc scroll po renderowaniu
                    if (scrollOffset > 0)
                    {
                        dgDostawy.Dispatcher.BeginInvoke(
                            DispatcherPriority.Loaded,
                            new Action(() => scrollViewer?.ScrollToVerticalOffset(scrollOffset)));
                    }

                    // Aktualizuj kolekcje harmonogramow — merguj duplikaty po nazwie hodowcy
                    _postepyHarmonogramow.Clear();

                    // Grupuj harmonogramy z SQL po nazwie hodowcy (mogą mieć różne LpDostawy)
                    var mergedHarmonogramy = noweHarmonogramy
                        .GroupBy(h => (h.Hodowca ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
                        .Select(g =>
                        {
                            var first = g.First();
                            if (g.Count() == 1) return first;
                            // Merguj wiele wpisów tego samego hodowcy
                            return new PostepHarmonogramu
                            {
                                LpDostawy = first.LpDostawy,
                                Hodowca = first.Hodowca,
                                AutaZwazone = g.Sum(x => x.AutaZwazone),
                                AutaOgolem = g.Sum(x => x.AutaOgolem),
                                AutaPlanowane = g.Sum(x => x.AutaPlanowane),
                                PlanSztukiLacznie = g.Sum(x => x.PlanSztukiLacznie),
                                PlanKgLacznie = g.Sum(x => x.PlanKgLacznie),
                                SztukiZwazoneSuma = g.Sum(x => x.SztukiZwazoneSuma),
                                KgZwazoneSuma = g.Sum(x => x.KgZwazoneSuma),
                                SredniaWagaPlan = first.SredniaWagaPlan,
                                SredniaWagaRzecz = g.Where(x => x.SredniaWagaRzecz.HasValue).Select(x => x.SredniaWagaRzecz).LastOrDefault(),
                            };
                        }).ToList();

                    foreach (var harmonogram in mergedHarmonogramy)
                        _postepyHarmonogramow.Add(harmonogram);

                    // Dodaj brakujących hodowców (z tabeli, ale bez harmonogramu)
                    var istniejaceHodowcy = new HashSet<string>(
                        _postepyHarmonogramow.Select(h => (h.Hodowca ?? "").Trim()),
                        StringComparer.OrdinalIgnoreCase);
                    var brakujacyHodowcy = _dostawy
                        .GroupBy(d => (d.Hodowca ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
                        .Where(g => !string.IsNullOrEmpty(g.Key) && !istniejaceHodowcy.Contains(g.Key));
                    foreach (var grp in brakujacyHodowcy)
                    {
                        var items = grp.ToList();
                        int zwazone = items.Count(x => x.Status == StatusDostawy.Zwazony);
                        _postepyHarmonogramow.Add(new PostepHarmonogramu
                        {
                            LpDostawy = items.First().LpDostawy ?? 0,
                            Hodowca = grp.Key,
                            AutaZwazone = zwazone,
                            AutaOgolem = items.Count,
                            AutaPlanowane = items.Count,
                            PlanSztukiLacznie = items.Sum(x => x.SztukiPlan),
                            PlanKgLacznie = items.Sum(x => x.KgPlan),
                            SztukiZwazoneSuma = items.Where(x => x.Status == StatusDostawy.Zwazony).Sum(x => x.SztukiRzeczywiste),
                            KgZwazoneSuma = items.Where(x => x.Status == StatusDostawy.Zwazony).Sum(x => x.KgRzeczywiste),
                            SredniaWagaPlan = items.First().WagaDeklHarmonogram,
                            SredniaWagaRzecz = items.First().SredniaWagaRzeczywistaCalc,
                        });
                    }

                    // Przypisz kolory hodowcom
                    AssignHodowcaColors();

                    // Zastosuj tryb planu (Nowe/Stare)
                    if (_useNowyPlan)
                        RecalculateNowyPlan();

                    // Aktualizuj podsumowanie
                    UpdateSummaryUI();

                    // Aktualizuj wiersz podsumowania tabeli
                    UpdateTableSummary();

                    // Aktualizuj licznik wynikow
                    txtLiczbaWynikow.Text = $"Wyniki: {_dostawy.Count}";

                    // Aktualizuj czas ostatniej aktualizacji
                    txtLastUpdate.Text = DateTime.Now.ToString("HH:mm:ss");

                    // Reset countdown
                    _secondsToRefresh = AUTO_REFRESH_SECONDS;
                });

                HideLoading();
                HideError();

                Debug.WriteLine($"[DashboardPrzychodu] Załadowano {noweDostawy.Count} dostaw");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DashboardPrzychodu] Błąd ładowania: {ex.Message}");
                HideLoading();

                // Diagnostyka: sprawdź które zapytanie powoduje błąd
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Błąd: {ex.Message}");
                sb.AppendLine();

                var queries = new (string Name, Func<System.Threading.Tasks.Task> Action)[]
                {
                    ("GetDostawyAsync", async () => await _przychodService.GetDostawyAsync(selectedDate)),
                    ("GetPodsumowanieAsync", async () => await _przychodService.GetPodsumowanieAsync(selectedDate)),
                    ("GetPrognozaDniaAsync", async () => await _przychodService.GetPrognozaDniaAsync(selectedDate)),
                    ("GetPostepyHarmonogramowAsync", async () => await _przychodService.GetPostepyHarmonogramowAsync(selectedDate)),
                    ("GetFaktycznyPrzychodAsync", async () => { await _przychodService.GetFaktycznyPrzychodAsync(selectedDate); }),
                };

                foreach (var (name, action) in queries)
                {
                    try
                    {
                        await action();
                        sb.AppendLine($"[OK] {name}");
                    }
                    catch (Exception qex)
                    {
                        sb.AppendLine($"[BŁĄD] {name}: {qex.Message}");
                    }
                }

                var fullError = sb.ToString();
                try { Clipboard.SetText(fullError); } catch { }
                ShowError("Błąd skopiowany do schowka.\n\n" + fullError);
            }
            finally
            {
                _isLoading = false;
            }
        }

        /// <summary>
        /// Aktualizacja UI podsumowania (KPI Strip + Sidebar)
        /// </summary>
        private void UpdateSummaryUI()
        {
            // Planowane - KPI Strip
            txtKgPlan.Text = _podsumowanie.KgPlanSuma.ToString("N0");

            // Zważone z trendem - KPI Strip
            decimal noweZwazone = _podsumowanie.KgZwazoneSuma;
            txtKgZwazone.Text = noweZwazone.ToString("N0");

            // Aktualizuj trend
            UpdateTrend(noweZwazone);

            // Pozostałe - KPI Strip
            txtKgPozostalo.Text = _podsumowanie.KgPozostalo.ToString("N0");

            // Odchylenie - KPI Strip
            if (_podsumowanie.KgZwazoneSuma > 0)
            {
                string znak = _podsumowanie.OdchylenieKgSuma > 0 ? "+" : "";
                txtOdchylenie.Text = $"{znak}{_podsumowanie.OdchylenieKgSuma:N0}";
                txtOdchylenieProc.Text = $"({znak}{_podsumowanie.OdchylenieProc:N1}%)";

                // Kolorowanie odchylenia
                var brush = _podsumowanie.Poziom switch
                {
                    PoziomOdchylenia.OK => new SolidColorBrush(Color.FromRgb(34, 197, 94)),      // #22c55e
                    PoziomOdchylenia.Uwaga => new SolidColorBrush(Color.FromRgb(251, 191, 36)), // #fbbf24
                    PoziomOdchylenia.Problem => new SolidColorBrush(Color.FromRgb(239, 68, 68)), // #ef4444
                    _ => new SolidColorBrush(Color.FromRgb(168, 162, 158)) // #a8a29e
                };
                txtOdchylenie.Foreground = brush;
                txtOdchylenieProc.Foreground = new SolidColorBrush(Color.FromRgb(120, 113, 108)); // #78716c

                // Pulsujące obramowanie przy problemie
                UpdatePulseAnimation(_podsumowanie.Poziom == PoziomOdchylenia.Problem);
            }
            else
            {
                txtOdchylenie.Text = "-";
                txtOdchylenieProc.Text = "";
                txtOdchylenie.Foreground = new SolidColorBrush(Color.FromRgb(168, 162, 158)); // #a8a29e
                UpdatePulseAnimation(false);
            }

            // Realizacja - KPI Strip
            txtRealizacja.Text = $"{_podsumowanie.ProcentRealizacjiKg}%";
            txtDostawyStatus.Text = $"({_podsumowanie.LiczbaZwazonych}/{_podsumowanie.LiczbaDostawOgolem})";

            // Tuszki - KPI Strip
            txtPrognozaTuszek.Text = _podsumowanie.PrognozaTuszekKg.ToString("N0");

            // Średnie wagi - sidebar
            if (_podsumowanie.SrWagaPlanSrednia.HasValue)
            {
                txtSrWagaPlanSidebar.Text = _podsumowanie.SrWagaPlanSrednia.Value.ToString("N2");
            }
            else if (_podsumowanie.SztukiPlanSuma > 0)
            {
                decimal srWagaPlan = _podsumowanie.KgPlanSuma / _podsumowanie.SztukiPlanSuma;
                txtSrWagaPlanSidebar.Text = srWagaPlan.ToString("N2");
            }
            else
            {
                txtSrWagaPlanSidebar.Text = "-";
            }

            if (_podsumowanie.SrWagaRzeczSrednia.HasValue)
            {
                txtSrWagaRzeczSidebar.Text = _podsumowanie.SrWagaRzeczSrednia.Value.ToString("N2");
            }
            else
            {
                txtSrWagaRzeczSidebar.Text = "-";
            }

            // Tuszki - sidebar (duże cyfry)
            decimal tuszkiPlan = _podsumowanie.TuszkiPlanKg;
            decimal tuszkiRzecz = _podsumowanie.PrognozaTuszekKg;
            decimal tuszkiOdchylenie = tuszkiRzecz - tuszkiPlan;

            txtTuszkiPlanSidebar.Text = tuszkiPlan > 0 ? tuszkiPlan.ToString("N0") : "-";
            txtTuszkiRzeczSidebar.Text = tuszkiRzecz > 0 ? tuszkiRzecz.ToString("N0") : "-";

            if (tuszkiPlan > 0 && tuszkiRzecz > 0)
            {
                string znak = tuszkiOdchylenie > 0 ? "+" : "";
                txtTuszkiOdchylenieSidebar.Text = $"{znak}{tuszkiOdchylenie:N0}";

                decimal procent = (tuszkiOdchylenie / tuszkiPlan) * 100;
                txtTuszkiOdchylenieProcSidebar.Text = $"({znak}{procent:N1}%)";

                // Kolorowanie odchylenia
                if (tuszkiOdchylenie >= 0)
                {
                    txtTuszkiOdchylenieSidebar.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // #22c55e - zielony
                    txtTuszkiOdchylenieProcSidebar.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                }
                else if (Math.Abs(procent) <= 5)
                {
                    txtTuszkiOdchylenieSidebar.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36)); // #fbbf24 - żółty
                    txtTuszkiOdchylenieProcSidebar.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
                }
                else
                {
                    txtTuszkiOdchylenieSidebar.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // #ef4444 - czerwony
                    txtTuszkiOdchylenieProcSidebar.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                }
            }
            else
            {
                txtTuszkiOdchylenieSidebar.Text = "-";
                txtTuszkiOdchylenieProcSidebar.Text = "";
                txtTuszkiOdchylenieSidebar.Foreground = new SolidColorBrush(Color.FromRgb(120, 113, 108)); // #78716c
            }

            // Klasy A/B - sidebar (Plan, Rzecz, Fakt z %)
            decimal tuszkiPlanTotal = _podsumowanie.TuszkiPlanKg;
            decimal planKlasaA = Math.Round(tuszkiPlanTotal * 0.80m, 0);
            decimal planKlasaB = Math.Round(tuszkiPlanTotal * 0.20m, 0);
            decimal planSuma = planKlasaA + planKlasaB;

            decimal rzeczKlasaA = _podsumowanie.PrognozaKlasaAKg;
            decimal rzeczKlasaB = _podsumowanie.PrognozaKlasaBKg;
            decimal rzeczSuma = rzeczKlasaA + rzeczKlasaB;

            decimal faktKlasaA = _podsumowanie.FaktKlasaAKg;
            decimal faktKlasaB = _podsumowanie.FaktKlasaBKg;
            decimal faktSuma = faktKlasaA + faktKlasaB;

            // Plan - wartości i %
            txtKlasaAPlanSidebar.Text = planKlasaA > 0 ? planKlasaA.ToString("N0") : "-";
            txtKlasaBPlanSidebar.Text = planKlasaB > 0 ? planKlasaB.ToString("N0") : "-";
            txtKlasaAPlanProcSidebar.Text = planSuma > 0 ? $"({Math.Round(planKlasaA / planSuma * 100, 0)}%)" : "";
            txtKlasaBPlanProcSidebar.Text = planSuma > 0 ? $"({Math.Round(planKlasaB / planSuma * 100, 0)}%)" : "";

            // Rzecz - wartości i %
            txtKlasaASidebar.Text = rzeczKlasaA.ToString("N0");
            txtKlasaBSidebar.Text = rzeczKlasaB.ToString("N0");
            txtKlasaAProcSidebar.Text = rzeczSuma > 0 ? $"({Math.Round(rzeczKlasaA / rzeczSuma * 100, 0)}%)" : "";
            txtKlasaBProcSidebar.Text = rzeczSuma > 0 ? $"({Math.Round(rzeczKlasaB / rzeczSuma * 100, 0)}%)" : "";

            // Fakt - faktyczny przychód z Symfonia (sPWU) - wartości i %
            txtKlasaAFaktSidebar.Text = faktKlasaA > 0 ? faktKlasaA.ToString("N0") : "-";
            txtKlasaBFaktSidebar.Text = faktKlasaB > 0 ? faktKlasaB.ToString("N0") : "-";
            txtKlasaAFaktProcSidebar.Text = faktSuma > 0 ? $"({Math.Round(faktKlasaA / faktSuma * 100, 0)}%)" : "";
            txtKlasaBFaktProcSidebar.Text = faktSuma > 0 ? $"({Math.Round(faktKlasaB / faktSuma * 100, 0)}%)" : "";

            // Auta i trend - sidebar
            txtAutaSidebar.Text = $"{_podsumowanie.LiczbaZwazonych}/{_podsumowanie.LiczbaDostawOgolem}";
            txtTrendSidebar.Text = $"{_podsumowanie.ProcentRealizacjiKg}%";

            // Alert redukcji - sidebar
            if (_prognoza != null && _prognoza.JestAlert)
            {
                borderRedukcjaSidebar.Visibility = Visibility.Visible;
                txtRedukcjaAlertSidebar.Text = _prognoza.PoziomAlertu;
                txtRedukcjaKgSidebar.Text = _prognoza.RedukcjaDisplay;
            }
            else
            {
                borderRedukcjaSidebar.Visibility = Visibility.Collapsed;
            }
        }


        /// <summary>
        /// Aktualizacja trendu dla kafelka ZWAŻONE w KPI Strip
        /// </summary>
        private void UpdateTrend(decimal noweZwazone)
        {
            if (_poprzednieZwazone == 0)
            {
                // Pierwsze ładowanie
                txtTrendZwazone.Text = "";
                txtTempoZwazone.Text = "";
                _trendStartTime = DateTime.Now;
                _lastRefreshTime = DateTime.Now;
            }
            else
            {
                decimal roznica = noweZwazone - _poprzednieZwazone;
                if (roznica > 0)
                {
                    txtTrendZwazone.Text = "↑";
                    txtTrendZwazone.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                }
                else if (roznica < 0)
                {
                    txtTrendZwazone.Text = "↓";
                    txtTrendZwazone.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                }
                else
                {
                    txtTrendZwazone.Text = "→";
                    txtTrendZwazone.Foreground = new SolidColorBrush(Color.FromRgb(120, 113, 108));
                }

                // Tempo - ile kg przybyło od startu śledzenia
                if (_trendStartTime.HasValue)
                {
                    var elapsed = DateTime.Now - _trendStartTime.Value;
                    decimal totalDiff = noweZwazone - 0; // od początku sesji
                    if (elapsed.TotalMinutes >= 1 && roznica != 0)
                    {
                        string tempoText;
                        if (Math.Abs(roznica) >= 1000)
                            tempoText = $"+{roznica / 1000:N1}k";
                        else
                            tempoText = $"+{roznica:N0}";
                        txtTempoZwazone.Text = $"{tempoText} / {AUTO_REFRESH_SECONDS}s";
                        txtTempoZwazone.Foreground = roznica > 0
                            ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
                            : new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    }
                    else if (roznica == 0)
                    {
                        txtTempoZwazone.Text = "bez zmian";
                        txtTempoZwazone.Foreground = new SolidColorBrush(Color.FromRgb(120, 113, 108));
                    }
                }

                _lastRefreshTime = DateTime.Now;
            }

            _poprzednieZwazone = noweZwazone;
        }

        /// <summary>
        /// Włącza/wyłącza animację pulsowania obramowania kafelka ODCHYLENIE
        /// </summary>
        private void UpdatePulseAnimation(bool enable)
        {
            try
            {
                if (enable)
                {
                    borderOdchylenie.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // #ef4444
                    _pulseStoryboard.Begin();
                }
                else
                {
                    _pulseStoryboard.Stop();
                    borderOdchylenie.BorderBrush = new SolidColorBrush(Color.FromRgb(120, 113, 108)); // #78716c
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DashboardPrzychodu] Błąd animacji: {ex.Message}");
            }
        }


        /// <summary>
        /// Przypisuje unikalne kolory hodowcom na podstawie nazwy.
        /// Ten sam hodowca = ten sam kolor w tabeli i kafelkach.
        /// </summary>
        private void AssignHodowcaColors()
        {
            _hodowcaColorMap.Clear();
            int colorIndex = 0;

            // Zbierz unikalne nazwy hodowców z dostaw
            var uniqueNames = _dostawy
                .Select(d => (d.Hodowca ?? "").Trim())
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var name in uniqueNames)
            {
                var color = HodowcaColorPalette[colorIndex % HodowcaColorPalette.Length];
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                _hodowcaColorMap[name] = brush;
                colorIndex++;
            }

            // Przypisz kolory, tła i flagę ostatniego wiersza do każdej dostawy
            // Najpierw znajdź ostatni wiersz każdego hodowcy (wg pozycji w kolekcji)
            var ostatniIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _dostawy.Count; i++)
            {
                var key2 = (_dostawy[i].Hodowca ?? "").Trim();
                if (!string.IsNullOrEmpty(key2))
                    ostatniIndex[key2] = i;
            }

            // Znajdź pierwszy wiersz każdego hodowcy (do separatora grup)
            var pierwszyIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _dostawy.Count; i++)
            {
                var key2b = (_dostawy[i].Hodowca ?? "").Trim();
                if (!string.IsNullOrEmpty(key2b) && !pierwszyIndex.ContainsKey(key2b))
                    pierwszyIndex[key2b] = i;
            }

            for (int i = 0; i < _dostawy.Count; i++)
            {
                var d = _dostawy[i];
                var key = (d.Hodowca ?? "").Trim();

                d.OstatniWierszHodowcy = ostatniIndex.TryGetValue(key, out int li) && li == i;

                // Separator grup: pierwszy wiersz nowej grupy (oprócz pierwszego wiersza w ogóle)
                d.PierwszyWierszGrupy = i > 0 && pierwszyIndex.TryGetValue(key, out int fi) && fi == i;

                if (_hodowcaColorMap.TryGetValue(key, out var brush))
                {
                    d.HodowcaKolor = brush;

                    // Tło wiersza = kolor hodowcy (8%) + status tint (5%)
                    var srcColor = ((SolidColorBrush)brush).Color;

                    // Status: zielony=zważony, pomarańczowy=brutto, czerwony=oczekuje
                    byte sR, sG, sB;
                    byte statusAlpha;
                    switch (d.Status)
                    {
                        case StatusDostawy.Zwazony:
                            sR = 34; sG = 197; sB = 94; statusAlpha = 12; break;
                        case StatusDostawy.BruttoWpisane:
                            sR = 249; sG = 115; sB = 22; statusAlpha = 10; break;
                        default: // Oczekuje
                            sR = 239; sG = 68; sB = 68; statusAlpha = 10; break;
                    }

                    // Blend: base dark (28,25,23) + hodowca 8% + status
                    byte bR = (byte)(28 + (srcColor.R - 28) * 0.08 + (sR - 28) * statusAlpha / 255.0);
                    byte bG = (byte)(25 + (srcColor.G - 25) * 0.08 + (sG - 25) * statusAlpha / 255.0);
                    byte bB = (byte)(23 + (srcColor.B - 23) * 0.08 + (sB - 23) * statusAlpha / 255.0);

                    var bgBrush = new SolidColorBrush(Color.FromRgb(bR, bG, bB));
                    bgBrush.Freeze();
                    d.HodowcaKolorTlo = bgBrush;
                }
            }

            // Wykryj aktywne karty (zmiana aut zważonych od ostatniego odświeżenia)
            var noweAuta = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Sortuj: aktywne (w trakcie) na górze, zakończone na dole
            var posortowane = _postepyHarmonogramow.OrderBy(h => h.CzyZakonczone ? 1 : 0).ThenBy(h => h.Hodowca).ToList();
            _postepyHarmonogramow.Clear();
            foreach (var h in posortowane)
            {
                var key = (h.Hodowca ?? "").Trim();

                // Kolor
                if (_hodowcaColorMap.TryGetValue(key, out var brush))
                    h.HodowcaKolor = brush;

                // Pulsowanie - wykryj zmianę
                bool aktywna = false;
                if (_poprzednieAutaZwazone.TryGetValue(key, out int poprzednio))
                {
                    aktywna = h.AutaZwazone > poprzednio; // nowe ważenie od ostatniego odświeżenia
                }
                h.JestAktywna = aktywna;

                noweAuta[key] = h.AutaZwazone;
                _postepyHarmonogramow.Add(h);
            }

            _poprzednieAutaZwazone = noweAuta;

            // Aktualizuj stacked bar realizacji dnia
            UpdateStackedBar();
        }

        /// <summary>
        /// Aktualizuje pasek realizacji łączonej dnia (stacked bar kolorowany per hodowca)
        /// </summary>
        private void UpdateStackedBar()
        {
            var segments = new List<BarSegment>();
            decimal totalPlan = _postepyHarmonogramow.Sum(h => h.PlanKgLacznie);
            if (totalPlan <= 0)
            {
                icStackedBar.ItemsSource = segments;
                txtStackedBarLabel.Text = "";
                return;
            }

            // Szerokość dostępna = szerokość panelu minus padding
            double barTotalWidth = Math.Max(100, icStackedBar.ActualWidth > 0 ? icStackedBar.ActualWidth : 430);

            decimal totalZwazone = 0;
            foreach (var h in _postepyHarmonogramow)
            {
                if (h.KgZwazoneSuma <= 0) continue;
                double proportion = (double)(h.KgZwazoneSuma / totalPlan);
                double width = Math.Max(3, proportion * barTotalWidth);
                totalZwazone += h.KgZwazoneSuma;

                segments.Add(new BarSegment
                {
                    Hodowca = h.Hodowca,
                    BarWidth = width,
                    HodowcaKolor = h.HodowcaKolor,
                    BarTooltip = $"{h.Hodowca}: {h.KgZwazoneSuma:N0} kg ({proportion * 100:N0}%)"
                });
            }

            icStackedBar.ItemsSource = segments;
            decimal procent = totalPlan > 0 ? totalZwazone / totalPlan * 100 : 0;
            txtStackedBarLabel.Text = $"{procent:N0}%";
        }

        /// <summary>
        /// Aktualizacja wiersza podsumowania tabeli
        /// </summary>
        private void UpdateTableSummary()
        {
            int liczbaDostawFiltrowanych = _dostawyView.Cast<object>().Count();
            txtSumaDostawy.Text = $"{liczbaDostawFiltrowanych} dostaw";

            decimal sumaPlan = _dostawy.Sum(d => d.KgPlanNaAuto);
            decimal sumaRzecz = _dostawy.Where(d => d.Status == StatusDostawy.Zwazony).Sum(d => d.KgRzeczywiste);
            decimal? sumaOdchylenie = _dostawy
                .Where(d => d.OdchylenieKgCalc.HasValue)
                .Sum(d => d.OdchylenieKgCalc);

            txtSumaPlan.Text = sumaPlan.ToString("N0");
            txtSumaRzecz.Text = sumaRzecz.ToString("N0");

            if (sumaOdchylenie.HasValue && sumaOdchylenie != 0)
            {
                string znak = sumaOdchylenie > 0 ? "+" : "";
                txtSumaOdchylenie.Text = $"{znak}{sumaOdchylenie:N0} kg";

                // Kolor - więcej niż plan = zawsze zielony (to dobrze!)
                if (sumaOdchylenie > 0)
                {
                    txtSumaOdchylenie.Foreground = FindResource("StatusOKBrush") as SolidColorBrush;
                }
                else if (sumaPlan > 0)
                {
                    // Mniej niż plan - sprawdzamy jak dużo brakuje
                    decimal procent = Math.Abs(sumaOdchylenie.Value / sumaPlan * 100);
                    if (procent <= 2)
                        txtSumaOdchylenie.Foreground = FindResource("StatusOKBrush") as SolidColorBrush;
                    else if (procent <= 5)
                        txtSumaOdchylenie.Foreground = FindResource("StatusWarningBrush") as SolidColorBrush;
                    else
                        txtSumaOdchylenie.Foreground = FindResource("StatusErrorBrush") as SolidColorBrush;
                }
            }
            else
            {
                txtSumaOdchylenie.Text = "-";
                txtSumaOdchylenie.Foreground = new SolidColorBrush(Color.FromRgb(120, 113, 108)); // #78716c
            }

            // Średnia waga planowana
            var zPlanem = _dostawy.Where(d => d.SredniaWagaPlanCalc.HasValue && d.SredniaWagaPlanCalc > 0).ToList();
            if (zPlanem.Any())
            {
                decimal sredniaWagaPlan = zPlanem.Average(d => d.SredniaWagaPlanCalc.Value);
                txtSumaWagaPlan.Text = sredniaWagaPlan.ToString("N2");
            }
            else
            {
                txtSumaWagaPlan.Text = "-";
            }

            // Średnia waga rzeczywista
            var zwazone = _dostawy.Where(d => d.SredniaWagaRzeczywistaCalc.HasValue && d.SredniaWagaRzeczywistaCalc > 0).ToList();
            if (zwazone.Any())
            {
                decimal sredniaSrWaga = zwazone.Average(d => d.SredniaWagaRzeczywistaCalc.Value);
                txtSumaSrWaga.Text = sredniaSrWaga.ToString("N2");
            }
            else
            {
                txtSumaSrWaga.Text = "-";
            }

            // Suma POZOSTAŁO (tylko unikalne harmonogramy - bez duplikatów)
            var unikatoweHarmonogramy = _dostawy
                .GroupBy(d => d.LpDostawy)
                .Select(g => g.First())
                .ToList();

            decimal sumaPozostaloKg = unikatoweHarmonogramy.Sum(d => d.KgPozostalo);
            decimal sumaPozostaloTuszki = Math.Round(sumaPozostaloKg * 0.78m, 0);

            txtSumaPozostaloKg.Text = $"{sumaPozostaloKg:N0} kg";
            txtSumaPozostaloTuszki.Text = $"{sumaPozostaloTuszki:N0} kg";

            // Suma tuszek rzeczywistych (78% z kg rzeczywiste, tylko zważone)
            decimal sumaTuszkiRzecz = _dostawy
                .Where(d => d.TuszkiRzeczywisteKg.HasValue)
                .Sum(d => d.TuszkiRzeczywisteKg.Value);
            txtSumaTuszkiRzecz.Text = sumaTuszkiRzecz > 0 ? sumaTuszkiRzecz.ToString("N0") : "-";
        }

        /// <summary>
        /// Obsługa zmiany trybu planu (Stare/Nowe)
        /// </summary>
        private void RbPlanMode_Checked(object sender, RoutedEventArgs e)
        {
            if (rbPlanNowe == null || rbPlanStare == null) return; // jeszcze nie zainicjalizowane

            _useNowyPlan = rbPlanNowe.IsChecked == true;

            if (_useNowyPlan)
                RecalculateNowyPlan();
            else
                ClearNowyPlan();

            UpdateSummaryUI();
            UpdateTableSummary();
        }

        /// <summary>
        /// Tryb "Nowe": per-auto plan = SztukiExcel * WagaDek(harmonogram),
        /// ostatnie auto w grupie = reszta z harmonogramu
        /// </summary>
        private void RecalculateNowyPlan()
        {
            // Grupy po LpDostawy (harmonogram)
            var groups = _dostawy
                .Where(d => d.LpDostawy.HasValue)
                .GroupBy(d => d.LpDostawy.Value);

            foreach (var group in groups)
            {
                var items = group.OrderBy(d => d.NrKursu).ToList();
                decimal planKgLacznie = items.First().PlanKgLacznie;
                decimal wagaDekl = items.First().WagaDeklHarmonogram ?? 0;

                decimal sumaAssigned = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    if (i == items.Count - 1)
                    {
                        // Ostatnie auto = reszta z harmonogramu
                        items[i].NowyPlanKg = planKgLacznie - sumaAssigned;
                    }
                    else
                    {
                        decimal planNaAuto = items[i].SztukiExcel * wagaDekl;
                        items[i].NowyPlanKg = planNaAuto;
                        sumaAssigned += planNaAuto;
                    }
                }
            }

            // Rekordy bez LpDostawy: SztukiExcel * WagaDek z FarmerCalc
            foreach (var item in _dostawy.Where(d => !d.LpDostawy.HasValue))
            {
                decimal wagaDekl = item.WagaDeklHarmonogram ?? item.SredniaWagaPlan ?? 0;
                item.NowyPlanKg = item.SztukiExcel * wagaDekl;
            }

            // Przelicz odchylenie w podsumowaniu
            RecalculateNowySummary();
        }

        /// <summary>
        /// Tryb "Stare": kasuje override planu
        /// </summary>
        private void ClearNowyPlan()
        {
            foreach (var item in _dostawy)
            {
                item.NowyPlanKg = null;
            }

            // Przywróć oryginalne wartości
            _podsumowanie.KgPlanDoZwazonych = _origKgPlanDoZwazonych;
            _podsumowanie.OdchylenieKgSuma = _origOdchylenieKgSuma;
        }

        /// <summary>
        /// Przelicza odchylenie sumaryczne w trybie "Nowe"
        /// </summary>
        private void RecalculateNowySummary()
        {
            // Suma planów dla zważonych aut w trybie Nowe
            decimal kgPlanDoZwazonych = _dostawy
                .Where(d => d.Status == StatusDostawy.Zwazony && d.NowyPlanKg.HasValue)
                .Sum(d => d.NowyPlanKg.Value);

            _podsumowanie.OdchylenieKgSuma = _podsumowanie.KgZwazoneSuma - kgPlanDoZwazonych;
            _podsumowanie.KgPlanDoZwazonych = kgPlanDoZwazonych;
        }

        /// <summary>
        /// Aktualizacja wyświetlanej daty
        /// </summary>
        private void UpdateDateDisplay()
        {
            var date = dpData.SelectedDate ?? DateTime.Today;
            string dayName = date.ToString("dddd", new System.Globalization.CultureInfo("pl-PL"));
            txtDataDisplay.Text = $"({dayName}, {date:dd.MM.yyyy})";
        }

        /// <summary>
        /// Timer odliczania
        /// </summary>
        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            _secondsToRefresh--;
            if (_secondsToRefresh <= 0)
            {
                _secondsToRefresh = AUTO_REFRESH_SECONDS;
            }
            txtAutoRefresh.Text = $"Auto: {_secondsToRefresh}s";
            txtAutoRefreshFooter.Text = $"{_secondsToRefresh}s";
        }

        /// <summary>
        /// Filtrowanie po nazwie hodowcy
        /// </summary>
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = txtSearch.Text?.Trim().ToLower() ?? "";

            if (string.IsNullOrEmpty(searchText))
            {
                _dostawyView.Filter = null;
            }
            else
            {
                _dostawyView.Filter = obj =>
                {
                    if (obj is DostawaItem item)
                    {
                        return (item.Hodowca?.ToLower().Contains(searchText) ?? false) ||
                               (item.HodowcaSkrot?.ToLower().Contains(searchText) ?? false);
                    }
                    return false;
                };
            }

            // Aktualizuj licznik i podsumowanie
            txtLiczbaWynikow.Text = $"Wyniki: {_dostawyView.Cast<object>().Count()}";
            UpdateTableSummary();
        }

        /// <summary>
        /// Dwuklik na wierszu - otwiera szczegóły dostawy
        /// </summary>
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgDostawy.SelectedItem is DostawaItem dostawa)
            {
                ShowDeliveryDetails(dostawa);
            }
        }

        /// <summary>
        /// Pokazuje szczegóły dostawy w oknie popup (styl Warm Industrial)
        /// Pełne rozbicie wartości i sposób obliczenia każdego pola
        /// </summary>
        private void ShowDeliveryDetails(DostawaItem d)
        {
            // Kolory
            var bgMain = Color.FromRgb(28, 25, 23);       // #1c1917
            var bgPanel = Color.FromRgb(41, 37, 36);      // #292524
            var bgSection = Color.FromRgb(55, 50, 48);    // #373230
            var amber = Color.FromRgb(251, 191, 36);      // #fbbf24
            var amberDark = Color.FromRgb(245, 158, 11);  // #f59e0b
            var green = Color.FromRgb(34, 197, 94);       // #22c55e
            var red = Color.FromRgb(239, 68, 68);         // #ef4444
            var yellow = Color.FromRgb(251, 191, 36);     // #fbbf24
            var cyan = Color.FromRgb(96, 165, 250);       // #60a5fa
            var purple = Color.FromRgb(192, 132, 252);    // #c084fc
            var textPrimary = Color.FromRgb(231, 229, 228);
            var textSecondary = Color.FromRgb(168, 162, 158);
            var textMuted = Color.FromRgb(120, 113, 108);
            var borderColor = Color.FromRgb(68, 64, 60);

            var detailWindow = new Window
            {
                Title = $"Szczegóły dostawy #{d.NrKursu} - {d.Hodowca}",
                Width = 750,
                Height = 820,
                MinWidth = 650,
                MinHeight = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(bgMain),
                ResizeMode = ResizeMode.CanResize
            };

            // === Helpers ===
            Brush Br(Color c) => new SolidColorBrush(c);

            // Wiersz danych: etykieta | wartość | (opcjonalnie) objaśnienie
            UIElement MakeRow(string label, string value, Brush valueBrush = null, string explanation = null)
            {
                var sp = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };
                var row = new Grid();
                row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(180) });
                row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var lbl = new TextBlock
                {
                    Text = label,
                    Foreground = Br(textMuted),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Top
                };
                Grid.SetColumn(lbl, 0);
                row.Children.Add(lbl);

                var val = new TextBlock
                {
                    Text = value,
                    Foreground = valueBrush ?? Br(textPrimary),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Top
                };
                Grid.SetColumn(val, 1);
                row.Children.Add(val);

                sp.Children.Add(row);

                if (!string.IsNullOrEmpty(explanation))
                {
                    var expl = new TextBlock
                    {
                        Text = explanation,
                        Foreground = Br(textSecondary),
                        FontSize = 10.5,
                        FontStyle = FontStyles.Italic,
                        Margin = new Thickness(180, 0, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    };
                    sp.Children.Add(expl);
                }
                return sp;
            }

            // Wiersz z formułą obliczenia (wyróżniony)
            UIElement MakeCalcRow(string formula, string result, Brush resultBrush = null)
            {
                var border = new Border
                {
                    Background = Br(bgSection),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(0, 2, 0, 4)
                };
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new TextBlock
                {
                    Text = formula,
                    Foreground = Br(textSecondary),
                    FontSize = 11,
                    FontFamily = new FontFamily("Consolas"),
                    VerticalAlignment = VerticalAlignment.Center
                });
                sp.Children.Add(new TextBlock
                {
                    Text = $"  =  {result}",
                    Foreground = resultBrush ?? Br(amber),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Consolas"),
                    VerticalAlignment = VerticalAlignment.Center
                });
                border.Child = sp;
                return border;
            }

            // Nagłówek sekcji
            UIElement MakeSectionHeader(string title, string icon)
            {
                var border = new Border
                {
                    BorderBrush = Br(amberDark),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Margin = new Thickness(0, 14, 0, 6),
                    Padding = new Thickness(0, 0, 0, 4)
                };
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new TextBlock
                {
                    Text = icon,
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                sp.Children.Add(new TextBlock
                {
                    Text = title.ToUpper(),
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = Br(amber),
                    VerticalAlignment = VerticalAlignment.Center
                });
                border.Child = sp;
                return border;
            }

            // Separator
            UIElement MakeSeparator() => new Border
            {
                Height = 1,
                Background = Br(borderColor),
                Margin = new Thickness(0, 4, 0, 4),
                Opacity = 0.5
            };

            // ============ BUDOWA CONTENTU ============
            var content = new StackPanel { Margin = new Thickness(24, 16, 24, 16) };

            // === NAGŁÓWEK GŁÓWNY ===
            var headerPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
            headerPanel.Children.Add(new TextBlock
            {
                Text = $"DOSTAWA #{d.NrKursu}",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Br(amber)
            });
            headerPanel.Children.Add(new TextBlock
            {
                Text = $"{d.Hodowca}  |  {d.Data:dd.MM.yyyy (dddd)}  |  Status: {d.StatusText}",
                FontSize = 12,
                Foreground = Br(textSecondary),
                Margin = new Thickness(0, 4, 0, 0)
            });
            content.Children.Add(headerPanel);

            // ─────────────────────────────────────────
            // SEKCJA 1: IDENTYFIKACJA
            // ─────────────────────────────────────────
            content.Children.Add(MakeSectionHeader("Identyfikacja", "\u2139"));
            content.Children.Add(MakeRow("Hodowca:", d.Hodowca ?? "-"));
            content.Children.Add(MakeRow("Hodowca (skrót):", d.HodowcaSkrot ?? "-"));
            content.Children.Add(MakeRow("Nr kursu (CarLp):", d.NrKursu.ToString()));
            content.Children.Add(MakeRow("Data:", d.Data.ToString("dd.MM.yyyy")));
            content.Children.Add(MakeRow("LP dostawy:", d.LpDostawy?.ToString() ?? "-",
                explanation: "Klucz powiązania z HarmonogramDostaw.Lp"));
            content.Children.Add(MakeRow("ID (FarmerCalc):", d.ID.ToString()));

            // ─────────────────────────────────────────
            // SEKCJA 2: PLAN (HARMONOGRAM)
            // ─────────────────────────────────────────
            content.Children.Add(MakeSectionHeader("Plan z harmonogramu", "\U0001F4CB"));
            content.Children.Add(MakeRow("Plan łącznie [szt]:", d.PlanSztukiLacznie.ToString("N0"),
                explanation: "Suma SztukiDek ze wszystkich aut tego hodowcy w HarmonogramDostaw"));
            content.Children.Add(MakeRow("Plan łącznie [kg]:", d.PlanKgLacznie.ToString("N0"),
                explanation: "Suma (SztukiDek × WagaDek) ze wszystkich aut"));
            content.Children.Add(MakeRow("Aut planowanych:", d.AutaPlanowane.ToString(),
                explanation: "Kolumna Auta z HarmonogramDostaw"));
            content.Children.Add(MakeRow("Waga deklar. [kg/szt]:", d.WagaDeklHarmonogram?.ToString("N3") ?? "-",
                explanation: "WagaDek z HarmonogramDostaw - średnia waga 1 sztuki"));
            content.Children.Add(MakeRow("Szt/pojemnik plan:", d.SztPojPlan?.ToString("N1") ?? "-"));
            content.Children.Add(MakeSeparator());

            // Dynamiczny plan na to auto
            content.Children.Add(MakeRow("Tryb planu:", d.NowyPlanKg.HasValue ? "NOWY (SztukiExcel × WagaDek)" : "STARY (harmonogram)", Br(cyan)));
            if (d.NowyPlanKg.HasValue)
            {
                content.Children.Add(MakeRow("SztukiExcel (AVILOG):", d.SztukiExcel.ToString("N0")));
                content.Children.Add(MakeCalcRow(
                    $"NowyPlanKg = SztukiExcel × WagaDek = {d.SztukiExcel} × {d.WagaDeklHarmonogram?.ToString("N3") ?? "?"}",
                    $"{d.NowyPlanKg:N0} kg"));
            }

            bool ostatnie = d.CzyOstatnieAuto;
            content.Children.Add(MakeRow("Ostatnie auto?:", ostatnie ? "TAK (plan = reszta z harmonogramu)" : "NIE (plan = łącznie / auta)",
                ostatnie ? Br(yellow) : null));

            // Obliczenie planu na to auto
            if (d.NowyPlanKg.HasValue)
            {
                content.Children.Add(MakeCalcRow(
                    $"Plan kg na auto = NowyPlanKg",
                    $"{d.KgPlanNaAuto:N0} kg", Br(cyan)));
            }
            else if (ostatnie)
            {
                content.Children.Add(MakeCalcRow(
                    $"Plan kg na auto = KgPozostalo",
                    $"{d.KgPlanNaAuto:N0} kg", Br(cyan)));
                content.Children.Add(MakeCalcRow(
                    $"Plan szt na auto = SztukiPozostalo",
                    $"{d.SztukiPlanNaAuto:N0} szt", Br(cyan)));
            }
            else if (d.AutaPlanowane > 0)
            {
                content.Children.Add(MakeCalcRow(
                    $"Plan kg na auto = {d.PlanKgLacznie:N0} / {d.AutaPlanowane}",
                    $"{d.KgPlanNaAuto:N0} kg", Br(cyan)));
                content.Children.Add(MakeCalcRow(
                    $"Plan szt na auto = {d.PlanSztukiLacznie:N0} / {d.AutaPlanowane}",
                    $"{d.SztukiPlanNaAuto:N0} szt", Br(cyan)));
            }
            else
            {
                content.Children.Add(MakeCalcRow(
                    "Plan kg (fallback z FarmerCalc)",
                    $"{d.KgPlan:N0} kg", Br(cyan)));
                content.Children.Add(MakeCalcRow(
                    "Plan szt (fallback z FarmerCalc)",
                    $"{d.SztukiPlan:N0} szt", Br(cyan)));
            }

            // ─────────────────────────────────────────
            // SEKCJA 3: WAŻENIE (RZECZYWISTE)
            // ─────────────────────────────────────────
            content.Children.Add(MakeSectionHeader("Ważenie - dane rzeczywiste", "\u2696"));

            var statusBrush = d.Status switch
            {
                StatusDostawy.Zwazony => Br(green),
                StatusDostawy.BruttoWpisane => Br(yellow),
                _ => Br(red)
            };
            content.Children.Add(MakeRow("Status:", d.StatusText, statusBrush,
                explanation: d.Status switch
                {
                    StatusDostawy.Zwazony => "Brutto > 0 AND Tara > 0",
                    StatusDostawy.BruttoWpisane => "Brutto > 0, ale Tara = 0",
                    _ => "Brutto = 0 (jeszcze nie ważono)"
                }));

            content.Children.Add(MakeRow("Brutto (pełne):", $"{d.Brutto:N0} kg",
                explanation: "FullWeight z FarmerCalc (auto z żywcem)"));
            content.Children.Add(MakeRow("Tara (puste):", $"{d.Tara:N0} kg",
                explanation: "EmptyWeight z FarmerCalc (puste auto po rozładunku)"));

            if (d.Status == StatusDostawy.Zwazony || d.Brutto > 0)
            {
                content.Children.Add(MakeCalcRow(
                    $"Netto = Brutto - Tara = {d.Brutto:N0} - {d.Tara:N0}",
                    $"{d.KgRzeczywiste:N0} kg", Br(green)));
            }
            else
            {
                content.Children.Add(MakeRow("Netto [kg]:", "-", Br(textMuted)));
            }

            content.Children.Add(MakeRow("Sztuki rzeczywiste:", d.SztukiRzeczywiste > 0 ? d.SztukiRzeczywiste.ToString("N0") : "-",
                explanation: "DeclI1 z FarmerCalc"));
            content.Children.Add(MakeRow("Sztuki Excel (AVILOG):", d.SztukiExcel > 0 ? d.SztukiExcel.ToString("N0") : "-",
                explanation: "SztukiExcel z FarmerCalc"));
            content.Children.Add(MakeRow("Padłe:", d.Padle.ToString("N0"),
                explanation: "DeclI2 z FarmerCalc"));
            content.Children.Add(MakeRow("Konfiskaty:", d.Konfiskaty.ToString("N0"),
                explanation: "DeclI3 + DeclI4 + DeclI5 z FarmerCalc"));
            content.Children.Add(MakeSeparator());
            content.Children.Add(MakeRow("Przyjazd:", d.PrzyjazdDisplay));
            content.Children.Add(MakeRow("Godzina ważenia:", d.GodzinaWazeniaDisplay));
            content.Children.Add(MakeRow("Kto ważył:", d.KtoWazyl ?? "-"));

            // ─────────────────────────────────────────
            // SEKCJA 4: ŚREDNIA WAGA
            // ─────────────────────────────────────────
            content.Children.Add(MakeSectionHeader("Średnia waga sztuki", "\u2696"));

            content.Children.Add(MakeRow("Waga deklarowana:", d.WagaDeklHarmonogram?.ToString("N3") + " kg" ?? "-",
                explanation: "Z HarmonogramDostaw.WagaDek"));

            if (d.SredniaWagaPlanCalc.HasValue && d.SztukiPlan > 0)
            {
                content.Children.Add(MakeCalcRow(
                    $"Śr. waga plan = KgPlan / SztPlan = {d.KgPlan:N0} / {d.SztukiPlan}",
                    $"{d.SredniaWagaPlanCalc:N3} kg"));
            }

            if (d.SztukiRzeczywiste > 0 && d.KgRzeczywiste > 0)
            {
                content.Children.Add(MakeCalcRow(
                    $"Śr. waga rzecz. = Netto / Szt = {d.KgRzeczywiste:N0} / {d.SztukiRzeczywiste}",
                    $"{d.SredniaWagaRzeczywistaCalc:N3} kg", Br(green)));

                if (d.WagaDeklHarmonogram.HasValue)
                {
                    var diffWaga = d.OdchylenieWagiCalc ?? d.OdchylenieWagi;
                    var diffBrush = diffWaga switch
                    {
                        > 0.02m => Br(green),
                        < -0.02m => Br(red),
                        _ => Br(textPrimary)
                    };
                    string znak = diffWaga > 0 ? "+" : "";
                    content.Children.Add(MakeCalcRow(
                        $"Odchylenie wagi = {d.SredniaWagaRzeczywistaCalc:N3} - {d.WagaDeklHarmonogram:N3}",
                        $"{znak}{diffWaga:N3} kg  {d.WagaTrend}", diffBrush));
                }
            }
            else
            {
                content.Children.Add(MakeRow("Śr. waga rzeczywista:", "-", Br(textMuted),
                    explanation: "Brak danych (auto nie zważone)"));
            }

            // ─────────────────────────────────────────
            // SEKCJA 5: ODCHYLENIE OD PLANU
            // ─────────────────────────────────────────
            content.Children.Add(MakeSectionHeader("Odchylenie od planu", "\U0001F4CA"));

            var odchBrush = d.Poziom switch
            {
                PoziomOdchylenia.OK => Br(green),
                PoziomOdchylenia.Uwaga => Br(yellow),
                PoziomOdchylenia.Problem => Br(red),
                _ => Br(textMuted)
            };

            decimal planRef = d.NowyPlanKg ?? d.KgPlan;
            string planLabel = d.NowyPlanKg.HasValue ? "NowyPlanKg" : "KgPlan";

            if (d.Status == StatusDostawy.Zwazony && planRef > 0)
            {
                var odchKg = d.OdchylenieKgCalc ?? d.OdchylenieKg;
                var odchProc = d.OdchylenieProcCalc ?? d.OdchylenieProc;
                string zn = odchKg > 0 ? "+" : "";

                content.Children.Add(MakeCalcRow(
                    $"Odch. kg = Netto - {planLabel} = {d.KgRzeczywiste:N0} - {planRef:N0}",
                    $"{zn}{odchKg:N0} kg", odchBrush));
                content.Children.Add(MakeCalcRow(
                    $"Odch. % = ({zn}{odchKg:N0} / {planRef:N0}) × 100",
                    $"{zn}{odchProc:N1}%", odchBrush));

                content.Children.Add(MakeRow("Ocena:", d.Poziom switch
                {
                    PoziomOdchylenia.OK => "OK (do ±2% lub więcej niż plan)",
                    PoziomOdchylenia.Uwaga => "UWAGA (2-5% poniżej planu)",
                    PoziomOdchylenia.Problem => "PROBLEM (>5% poniżej planu)",
                    _ => "-"
                }, odchBrush));
            }
            else
            {
                content.Children.Add(MakeRow("Odchylenie:", "-", Br(textMuted),
                    explanation: d.Status != StatusDostawy.Zwazony
                        ? "Auto jeszcze nie zważone"
                        : "Brak planu do porównania"));
            }

            // ─────────────────────────────────────────
            // SEKCJA 6: POSTĘP HARMONOGRAMU (HODOWCA)
            // ─────────────────────────────────────────
            content.Children.Add(MakeSectionHeader("Postęp harmonogramu hodowcy", "\U0001F69A"));

            content.Children.Add(MakeRow("Aut ogółem:", d.AutaOgolem.ToString(),
                explanation: "Ile aut przyjechało/jest w planie od tego hodowcy"));
            content.Children.Add(MakeRow("Aut zważonych:", d.AutaZwazone.ToString(), Br(green)));
            content.Children.Add(MakeRow("Aut czekających:", d.AutaCzekajacych.ToString(),
                d.AutaCzekajacych > 0 ? Br(yellow) : Br(green)));
            content.Children.Add(MakeCalcRow(
                $"Postęp = {d.AutaZwazone}/{d.AutaOgolem} aut",
                $"{d.PostepProc:N0}%", d.PostepProc >= 100 ? Br(green) : Br(cyan)));
            content.Children.Add(MakeSeparator());

            content.Children.Add(MakeRow("Zważono łącznie [szt]:", d.SztukiZwazoneSuma.ToString("N0")));
            content.Children.Add(MakeRow("Zważono łącznie [kg]:", d.KgZwazoneSuma.ToString("N0")));

            if (d.PlanSztukiLacznie > 0)
            {
                content.Children.Add(MakeCalcRow(
                    $"Szt pozostało = {d.PlanSztukiLacznie:N0} - {d.SztukiZwazoneSuma:N0}",
                    $"{d.SztukiPozostalo:N0} szt"));
            }
            if (d.PlanKgLacznie > 0)
            {
                content.Children.Add(MakeCalcRow(
                    $"Kg pozostało = {d.PlanKgLacznie:N0} - {d.KgZwazoneSuma:N0}",
                    $"{d.KgPozostalo:N0} kg"));
            }

            if (d.AutaZwazone > 0 && d.AutaPlanowane > 0)
            {
                content.Children.Add(MakeRow("Trend hodowcy:", d.TrendHodowcy,
                    d.TrendProc < 95 ? Br(red) : d.TrendProc > 105 ? Br(green) : Br(textPrimary),
                    explanation: $"Śr. kg na zważone auto vs plan na auto = ({d.KgZwazoneSuma:N0}/{d.AutaZwazone}) / ({d.PlanKgLacznie:N0}/{d.AutaPlanowane}) × 100 = {d.TrendProc:N0}%"));
            }

            // ─────────────────────────────────────────
            // SEKCJA 7: TUSZKI (PROGNOZA PRODUKCJI)
            // ─────────────────────────────────────────
            content.Children.Add(MakeSectionHeader("Tuszki - prognoza produkcji (78%)", "\U0001F357"));

            content.Children.Add(MakeRow("Wydajność:", "78%", Br(textSecondary),
                explanation: "Stały współczynnik: z 1 kg żywca powstaje 0.78 kg tuszki"));

            content.Children.Add(MakeCalcRow(
                $"Tuszki plan = KgPlan × 0.78 = {d.KgPlan:N0} × 0.78",
                $"{d.TuszkiPlanKg:N0} kg"));

            if (d.TuszkiRzeczywisteKg.HasValue)
            {
                content.Children.Add(MakeCalcRow(
                    $"Tuszki rzecz. = Netto × 0.78 = {d.KgRzeczywiste:N0} × 0.78",
                    $"{d.TuszkiRzeczywisteKg:N0} kg", Br(green)));
            }

            if (d.WagaTuszkiKg.HasValue)
            {
                content.Children.Add(MakeCalcRow(
                    $"Waga tuszki = Śr.waga × 0.78 = {d.SredniaWagaRzeczywistaCalc:N3} × 0.78",
                    $"{d.WagaTuszkiKg:N3} kg", Br(purple)));
            }

            if (d.SztukWPojemniku.HasValue)
            {
                content.Children.Add(MakeCalcRow(
                    $"Szt/pojemnik = 15kg / {d.WagaTuszkiKg:N3}",
                    $"{d.SztukWPojemniku:N2} szt", Br(purple)));
                content.Children.Add(MakeRow("Rozmiar:", d.RozmiarDisplay, Br(purple),
                    explanation: "Zaokrąglona liczba sztuk w 15kg pojemniku"));
            }

            content.Children.Add(MakeRow("Szt/poj plan:", d.SztPojPlan?.ToString("N1") ?? "-"));
            content.Children.Add(MakeRow("Szt/poj rzecz.:", d.SztPojRzecz?.ToString("N1") ?? "-"));

            if (d.KgPozostalo > 0)
            {
                content.Children.Add(MakeSeparator());
                content.Children.Add(MakeCalcRow(
                    $"Tuszki pozostałe = KgPozostało × 0.78 = {d.KgPozostalo:N0} × 0.78",
                    $"{d.TuszkiPozostalo:N0} kg", Br(cyan)));
                content.Children.Add(MakeCalcRow(
                    $"Pozostało % = KgPozost / PlanKg × 100 = {d.KgPozostalo:N0} / {d.PlanKgLacznie:N0} × 100",
                    $"{d.PozostaloProc:N1}%", d.PozostaloProc < 5 ? Br(red) : Br(cyan)));
            }

            // ============ SCROLL + FOOTER ============
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = content
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(scrollViewer, 0);
            mainGrid.Children.Add(scrollViewer);

            // Footer z przyciskami
            var footer = new Border
            {
                Background = Br(bgPanel),
                Padding = new Thickness(24, 10, 24, 10),
                BorderBrush = Br(borderColor),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            var footerPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            // Kopiuj do schowka
            var copyButton = new Button
            {
                Content = "Kopiuj do schowka",
                Padding = new Thickness(15, 7, 15, 7),
                Background = Br(Color.FromRgb(68, 64, 60)),
                Foreground = Br(textPrimary),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 12,
                Margin = new Thickness(0, 0, 8, 0)
            };
            copyButton.Click += (s, ev) =>
            {
                try
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"DOSTAWA #{d.NrKursu} - {d.Hodowca}");
                    sb.AppendLine($"Data: {d.Data:dd.MM.yyyy}  |  Status: {d.StatusText}");
                    sb.AppendLine(new string('-', 50));
                    sb.AppendLine($"LP dostawy (harmonogram): {d.LpDostawy?.ToString() ?? "-"}");
                    sb.AppendLine($"Plan łącznie: {d.PlanSztukiLacznie:N0} szt / {d.PlanKgLacznie:N0} kg / {d.AutaPlanowane} aut");
                    sb.AppendLine($"Waga deklar.: {d.WagaDeklHarmonogram?.ToString("N3") ?? "-"} kg/szt");
                    sb.AppendLine($"Plan na auto: {d.SztukiPlanNaAuto:N0} szt / {d.KgPlanNaAuto:N0} kg {(ostatnie ? "(ostatnie auto=reszta)" : "")}");
                    sb.AppendLine(new string('-', 50));
                    sb.AppendLine($"Brutto: {d.Brutto:N0} kg  |  Tara: {d.Tara:N0} kg  |  Netto: {d.KgRzeczywiste:N0} kg");
                    sb.AppendLine($"Sztuki: {d.SztukiRzeczywiste:N0}  |  SztExcel: {d.SztukiExcel:N0}");
                    sb.AppendLine($"Padłe: {d.Padle}  |  Konfiskaty: {d.Konfiskaty}");
                    sb.AppendLine($"Śr. waga: {d.SredniaWagaRzeczywistaCalc?.ToString("N3") ?? "-"} kg  (dekl: {d.WagaDeklHarmonogram?.ToString("N3") ?? "-"})");
                    sb.AppendLine(new string('-', 50));
                    sb.AppendLine($"Odchylenie: {d.OdchylenieDisplay}  |  Ocena: {d.Poziom}");
                    sb.AppendLine($"Postęp: {d.PostepDisplay}  |  Trend: {d.TrendHodowcy}");
                    sb.AppendLine($"Tuszki plan: {d.TuszkiPlanKg:N0} kg  |  rzecz: {d.TuszkiRzeczywisteKg?.ToString("N0") ?? "-"} kg");
                    sb.AppendLine($"Rozmiar: {d.RozmiarDisplay}  |  Szt/poj: {d.SztukWPojemniku?.ToString("N2") ?? "-"}");
                    sb.AppendLine($"Przyjazd: {d.PrzyjazdDisplay}  |  Ważenie: {d.GodzinaWazeniaDisplay}  |  Ważył: {d.KtoWazyl ?? "-"}");
                    Clipboard.SetText(sb.ToString());
                    ((Button)s).Content = "Skopiowano!";
                }
                catch { }
            };
            footerPanel.Children.Add(copyButton);

            // Zamknij
            var closeButton = new Button
            {
                Content = "Zamknij",
                Padding = new Thickness(20, 7, 20, 7),
                Background = Br(amberDark),
                Foreground = Br(bgMain),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12
            };
            closeButton.Click += (s, ev) => detailWindow.Close();
            footerPanel.Children.Add(closeButton);

            footer.Child = footerPanel;
            Grid.SetRow(footer, 1);
            mainGrid.Children.Add(footer);

            detailWindow.Content = mainGrid;
            detailWindow.ShowDialog();
        }

        /// <summary>
        /// Przycisk Odśwież
        /// </summary>
        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[DashboardPrzychodu] Ręczne odświeżenie");
            _secondsToRefresh = AUTO_REFRESH_SECONDS;
            await LoadDataAsync();
        }

        /// <summary>
        /// Otwiera okno pomocy
        /// </summary>
        private void BtnHelp_Click(object sender, RoutedEventArgs e)
        {
            var helpWindow = new HelpWindow { Owner = this };
            helpWindow.ShowDialog();
        }

        /// <summary>
        /// Eksport do Excel
        /// </summary>
        private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedDate = dpData.SelectedDate ?? DateTime.Today;
                string fileName = $"PrzychodZywca_{selectedDate:yyyy-MM-dd}_{DateTime.Now:HHmmss}.xlsx";

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = fileName,
                    DefaultExt = ".xlsx",
                    Filter = "Pliki Excel (*.xlsx)|*.xlsx"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var ws = workbook.Worksheets.Add("Przychod Zywca");

                        // Nagłówek
                        ws.Cell(1, 1).Value = $"PRZYCHÓD ŻYWCA - {selectedDate:dd.MM.yyyy}";
                        ws.Cell(1, 1).Style.Font.Bold = true;
                        ws.Cell(1, 1).Style.Font.FontSize = 16;
                        ws.Range(1, 1, 1, 9).Merge();

                        // Podsumowanie
                        ws.Cell(3, 1).Value = "PODSUMOWANIE:";
                        ws.Cell(3, 1).Style.Font.Bold = true;

                        ws.Cell(4, 1).Value = "Planowane [kg]:";
                        ws.Cell(4, 2).Value = _podsumowanie.KgPlanSuma;
                        ws.Cell(5, 1).Value = "Zważone [kg]:";
                        ws.Cell(5, 2).Value = _podsumowanie.KgZwazoneSuma;
                        ws.Cell(6, 1).Value = "Odchylenie [kg]:";
                        ws.Cell(6, 2).Value = _podsumowanie.OdchylenieKgSuma;
                        ws.Cell(7, 1).Value = "Odchylenie [%]:";
                        ws.Cell(7, 2).Value = _podsumowanie.OdchylenieProc;

                        // Prognoza
                        ws.Cell(9, 1).Value = "PROGNOZA PRODUKCJI:";
                        ws.Cell(9, 1).Style.Font.Bold = true;
                        ws.Cell(10, 1).Value = "Tuszki ogółem [kg]:";
                        ws.Cell(10, 2).Value = _podsumowanie.PrognozaTuszekKg;
                        ws.Cell(11, 1).Value = "Klasa A [kg]:";
                        ws.Cell(11, 2).Value = _podsumowanie.PrognozaKlasaAKg;
                        ws.Cell(12, 1).Value = "Klasa B [kg]:";
                        ws.Cell(12, 2).Value = _podsumowanie.PrognozaKlasaBKg;

                        // Tabela dostaw
                        int startRow = 14;
                        ws.Cell(startRow, 1).Value = "LP";
                        ws.Cell(startRow, 2).Value = "Hodowca";
                        ws.Cell(startRow, 3).Value = "Plan [kg]";
                        ws.Cell(startRow, 4).Value = "Rzecz. [kg]";
                        ws.Cell(startRow, 5).Value = "Odchylenie [kg]";
                        ws.Cell(startRow, 6).Value = "Odchylenie [%]";
                        ws.Cell(startRow, 7).Value = "Śr. waga";
                        ws.Cell(startRow, 8).Value = "Status";
                        ws.Cell(startRow, 9).Value = "Przyjazd";

                        var headerRange = ws.Range(startRow, 1, startRow, 9);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.DarkBlue;
                        headerRange.Style.Font.FontColor = XLColor.White;

                        int row = startRow + 1;
                        foreach (var d in _dostawy)
                        {
                            ws.Cell(row, 1).Value = d.NrKursu;
                            ws.Cell(row, 2).Value = d.Hodowca;
                            ws.Cell(row, 3).Value = d.KgPlan;
                            ws.Cell(row, 4).Value = d.KgRzeczywiste;
                            ws.Cell(row, 5).Value = d.OdchylenieKgCalc ?? 0;
                            ws.Cell(row, 6).Value = d.OdchylenieProcCalc ?? 0;
                            ws.Cell(row, 7).Value = d.SredniaWagaRzeczywistaCalc ?? 0;
                            ws.Cell(row, 8).Value = d.StatusText;
                            ws.Cell(row, 9).Value = d.PrzyjazdDisplay;

                            // Kolorowanie odchylenia
                            if (d.Poziom == PoziomOdchylenia.Problem)
                            {
                                ws.Cell(row, 5).Style.Font.FontColor = XLColor.Red;
                                ws.Cell(row, 6).Style.Font.FontColor = XLColor.Red;
                            }
                            else if (d.Poziom == PoziomOdchylenia.Uwaga)
                            {
                                ws.Cell(row, 5).Style.Font.FontColor = XLColor.Orange;
                                ws.Cell(row, 6).Style.Font.FontColor = XLColor.Orange;
                            }

                            row++;
                        }

                        // Formatowanie
                        ws.Columns().AdjustToContents();
                        ws.Column(2).Width = 30;

                        workbook.SaveAs(saveDialog.FileName);
                    }

                    MessageBox.Show($"Eksport zakończony pomyślnie!\n\n{saveDialog.FileName}",
                        "Eksport Excel", MessageBoxButton.OK, MessageBoxImage.Information);

                    Debug.WriteLine($"[DashboardPrzychodu] Eksport Excel: {saveDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DashboardPrzychodu] Błąd eksportu: {ex.Message}");
                MessageBox.Show($"Błąd podczas eksportu:\n\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Drukowanie raportu
        /// </summary>
        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedDate = dpData.SelectedDate ?? DateTime.Today;

                // Tworzenie dokumentu do wydruku
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    // Tworzenie zawartości do wydruku
                    var document = new System.Windows.Documents.FlowDocument();
                    document.PagePadding = new Thickness(50);
                    document.ColumnWidth = double.MaxValue;

                    // Nagłówek
                    var header = new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"PRZYCHÓD ŻYWCA - {selectedDate:dd.MM.yyyy}"))
                    {
                        FontSize = 18,
                        FontWeight = FontWeights.Bold,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 20)
                    };
                    document.Blocks.Add(header);

                    // Podsumowanie
                    var summary = new System.Windows.Documents.Paragraph();
                    summary.Inlines.Add(new System.Windows.Documents.Run("PODSUMOWANIE\n") { FontWeight = FontWeights.Bold });
                    summary.Inlines.Add(new System.Windows.Documents.Run($"Planowane: {_podsumowanie.KgPlanSuma:N0} kg ({_podsumowanie.SztukiPlanSuma:N0} szt)\n"));
                    summary.Inlines.Add(new System.Windows.Documents.Run($"Zważone: {_podsumowanie.KgZwazoneSuma:N0} kg ({_podsumowanie.SztukiZwazoneSuma:N0} szt)\n"));
                    summary.Inlines.Add(new System.Windows.Documents.Run($"Odchylenie: {_podsumowanie.OdchylenieKgSuma:+#;-#;0} kg ({_podsumowanie.OdchylenieProc:+0.0;-0.0;0}%)\n"));
                    summary.Inlines.Add(new System.Windows.Documents.Run($"Realizacja: {_podsumowanie.ProcentRealizacjiKg}%\n"));
                    summary.Margin = new Thickness(0, 0, 0, 20);
                    document.Blocks.Add(summary);

                    // Prognoza
                    var prognosis = new System.Windows.Documents.Paragraph();
                    prognosis.Inlines.Add(new System.Windows.Documents.Run("PROGNOZA TUSZEK\n") { FontWeight = FontWeights.Bold });
                    prognosis.Inlines.Add(new System.Windows.Documents.Run($"Tuszki ogółem: {_podsumowanie.PrognozaTuszekKg:N0} kg (78%)\n"));
                    prognosis.Inlines.Add(new System.Windows.Documents.Run($"Klasa A: {_podsumowanie.PrognozaKlasaAKg:N0} kg (80%)\n"));
                    prognosis.Inlines.Add(new System.Windows.Documents.Run($"Klasa B: {_podsumowanie.PrognozaKlasaBKg:N0} kg (20%)\n"));
                    prognosis.Margin = new Thickness(0, 0, 0, 20);
                    document.Blocks.Add(prognosis);

                    // Tabela (uproszczona)
                    var table = new System.Windows.Documents.Paragraph();
                    table.Inlines.Add(new System.Windows.Documents.Run("SZCZEGÓŁY DOSTAW\n\n") { FontWeight = FontWeights.Bold });

                    foreach (var d in _dostawy.Take(30)) // Limit dla wydruku
                    {
                        table.Inlines.Add(new System.Windows.Documents.Run(
                            $"{d.NrKursu}. {d.Hodowca,-25} Plan: {d.KgPlan,8:N0} kg  Rzecz: {d.KgRzeczywiste,8:N0} kg  {d.StatusText}\n"));
                    }

                    if (_dostawy.Count > 30)
                    {
                        table.Inlines.Add(new System.Windows.Documents.Run($"\n... i {_dostawy.Count - 30} więcej dostaw"));
                    }

                    document.Blocks.Add(table);

                    // Stopka
                    var footer = new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"\nWygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm:ss}"))
                    {
                        FontSize = 10,
                        Foreground = Brushes.Gray
                    };
                    document.Blocks.Add(footer);

                    // Drukuj
                    var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)document).DocumentPaginator;
                    printDialog.PrintDocument(paginator, $"Przychód Żywca - {selectedDate:dd.MM.yyyy}");

                    Debug.WriteLine("[DashboardPrzychodu] Drukowanie zakończone");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DashboardPrzychodu] Błąd drukowania: {ex.Message}");
                MessageBox.Show($"Błąd podczas drukowania:\n\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Ponów próbę połączenia
        /// </summary>
        private async void BtnRetry_Click(object sender, RoutedEventArgs e)
        {
            HideError();
            await LoadDataAsync();
        }

        /// <summary>
        /// Pokazuje overlay ładowania
        /// </summary>
        private void ShowLoading(string message = "Ładowanie...")
        {
            txtLoadingMessage.Text = message;
            loadingOverlay.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Ukrywa overlay ładowania
        /// </summary>
        private void HideLoading()
        {
            loadingOverlay.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Pokazuje overlay błędu
        /// </summary>
        private void ShowError(string message)
        {
            txtErrorMessage.Text = message;
            errorOverlay.Visibility = Visibility.Visible;
            try { Clipboard.SetText(message); } catch { }
        }

        /// <summary>
        /// Ukrywa overlay błędu
        /// </summary>
        private void HideError()
        {
            errorOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnCopyError_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(txtErrorMessage.Text);
                if (sender is Button btn)
                {
                    btn.Content = "Skopiowano!";
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    timer.Tick += (s, _) => { btn.Content = "Kopiuj błąd"; timer.Stop(); };
                    timer.Start();
                }
            }
            catch { }
        }

        /// <summary>
        /// Znajduje ScrollViewer w drzewie wizualnym kontrolki (do zachowania pozycji scrolla)
        /// </summary>
        private static ScrollViewer FindScrollViewer(DependencyObject obj)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is ScrollViewer sv) return sv;
                var result = FindScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>
        /// Zamykanie okna - zatrzymaj timery
        /// </summary>
        private void DashboardPrzychoduWindow_Closing(object sender, CancelEventArgs e)
        {
            Debug.WriteLine("[DashboardPrzychodu] Zamykanie okna, zatrzymuję timery...");
            _autoRefreshTimer?.Stop();
            _countdownTimer?.Stop();
            _pulseStoryboard?.Stop();
        }

        /// <summary>
        /// Uruchamia diagnostykę zapytania SQL
        /// </summary>
        private async void BtnDiagnose_Click(object sender, RoutedEventArgs e)
        {
            var selectedDate = dpData.SelectedDate ?? DateTime.Today;

            try
            {
                ShowLoading("Uruchamiam diagnostykę...");

                var diagnosticResult = await _przychodService.DiagnoseQueryAsync(selectedDate);

                HideLoading();

                // Pokaż wynik w oknie dialogowym
                var diagWindow = new Window
                {
                    Title = "Diagnostyka zapytania SQL",
                    Width = 900,
                    Height = 700,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Background = new SolidColorBrush(Color.FromRgb(28, 25, 23)) // #1c1917
                };

                var scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Margin = new Thickness(10)
                };

                var textBox = new TextBox
                {
                    Text = diagnosticResult,
                    IsReadOnly = true,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    Background = new SolidColorBrush(Color.FromRgb(41, 37, 36)), // #292524
                    Foreground = new SolidColorBrush(Color.FromRgb(231, 229, 228)), // #e7e5e4
                    BorderThickness = new Thickness(0),
                    TextWrapping = TextWrapping.NoWrap,
                    AcceptsReturn = true
                };

                scrollViewer.Content = textBox;
                diagWindow.Content = scrollViewer;
                diagWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                HideLoading();
                MessageBox.Show($"Błąd diagnostyki:\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Context Menus - Logo i Ikony KPI

        /// <summary>
        /// Prawy przycisk myszy na logo - pokazuje menu wyboru logo
        /// </summary>
        private void LogoBox_RightClick(object sender, MouseButtonEventArgs e)
        {
            var menu = (ContextMenu)FindResource("LogoContextMenu");
            menu.PlacementTarget = sender as UIElement;
            menu.IsOpen = true;
            e.Handled = true;
        }

        /// <summary>
        /// Ustawia emoji jako logo
        /// </summary>
        private void Logo_SetEmoji(object sender, RoutedEventArgs e)
        {
            var emoji = (sender as MenuItem)?.Tag?.ToString();
            if (!string.IsNullOrEmpty(emoji))
            {
                // Jeśli LogoBox zawiera Image, przywróć TextBlock
                if (!(LogoBox.Child is TextBlock))
                {
                    LogoBox.Child = LogoIcon;
                }

                LogoIcon.Text = emoji;
                Debug.WriteLine($"[DashboardPrzychodu] Ustawiono emoji logo: {emoji}");
                // Można zapisać do ustawień: SaveSetting("Logo", emoji);
            }
        }

        /// <summary>
        /// Wybiera logo z dostępnych plików w projekcie
        /// </summary>
        private void Logo_SelectFile(object sender, RoutedEventArgs e)
        {
            // Znajdź dostępne pliki logo
            var logoFiles = FindLogoFiles();

            if (logoFiles.Count == 0)
            {
                MessageBox.Show("Nie znaleziono plików logo w katalogu aplikacji.\n\nDostępne są emoji - użyj menu kontekstowego.",
                    "Brak logo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Stwórz okno wyboru logo
            var selectWindow = new Window
            {
                Title = "Wybierz logo",
                Width = 500,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(28, 25, 23)), // #1c1917
                ResizeMode = ResizeMode.NoResize
            };

            var mainGrid = new Grid { Margin = new Thickness(15) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Nagłówek
            var header = new TextBlock
            {
                Text = "Wybierz logo firmy",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36)), // #fbbf24
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // Lista logo
            var listBox = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(41, 37, 36)), // #292524
                Foreground = new SolidColorBrush(Color.FromRgb(231, 229, 228)), // #e7e5e4
                BorderBrush = new SolidColorBrush(Color.FromRgb(68, 64, 60)), // #44403c
                BorderThickness = new Thickness(1)
            };

            foreach (var logoPath in logoFiles)
            {
                var item = new ListBoxItem
                {
                    Tag = logoPath,
                    Padding = new Thickness(8, 6, 8, 6)
                };

                var panel = new StackPanel { Orientation = Orientation.Horizontal };

                // Podgląd obrazka
                try
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(logoPath);
                    bitmap.DecodePixelHeight = 32;
                    bitmap.EndInit();

                    var img = new Image
                    {
                        Source = bitmap,
                        Width = 32,
                        Height = 32,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    panel.Children.Add(img);
                }
                catch
                {
                    panel.Children.Add(new TextBlock { Text = "📁", FontSize = 20, Margin = new Thickness(0, 0, 10, 0) });
                }

                panel.Children.Add(new TextBlock
                {
                    Text = Path.GetFileName(logoPath),
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(231, 229, 228))
                });

                item.Content = panel;
                listBox.Items.Add(item);
            }

            Grid.SetRow(listBox, 1);
            mainGrid.Children.Add(listBox);

            // Przyciski
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var btnSelect = new Button
            {
                Content = "Wybierz",
                Padding = new Thickness(15, 6, 15, 6),
                Background = new SolidColorBrush(Color.FromRgb(245, 158, 11)), // #f59e0b
                Foreground = new SolidColorBrush(Color.FromRgb(28, 25, 23)),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            btnSelect.Click += (s, args) =>
            {
                if (listBox.SelectedItem is ListBoxItem selected && selected.Tag is string path)
                {
                    SetLogoFromFile(path);
                    selectWindow.Close();
                }
                else
                {
                    MessageBox.Show("Wybierz logo z listy", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            buttonPanel.Children.Add(btnSelect);

            var btnCancel = new Button
            {
                Content = "Anuluj",
                Padding = new Thickness(15, 6, 15, 6),
                Background = new SolidColorBrush(Color.FromRgb(68, 64, 60)), // #44403c
                Foreground = new SolidColorBrush(Color.FromRgb(168, 162, 158)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, args) => selectWindow.Close();
            buttonPanel.Children.Add(btnCancel);

            Grid.SetRow(buttonPanel, 2);
            mainGrid.Children.Add(buttonPanel);

            selectWindow.Content = mainGrid;
            selectWindow.ShowDialog();
        }

        /// <summary>
        /// Znajduje dostępne pliki logo w katalogu aplikacji
        /// </summary>
        private List<string> FindLogoFiles()
        {
            var logoFiles = new List<string>();
            var extensions = new[] { "*.png", "*.jpg", "*.jpeg", "*.ico", "*.bmp" };

            // Przeszukaj katalog aplikacji i nadrzędne
            var searchDirs = new[]
            {
                AppDomain.CurrentDomain.BaseDirectory,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."),
                Environment.CurrentDirectory,
                "/home/user/Kalendarz1"
            };

            foreach (var dir in searchDirs)
            {
                try
                {
                    string fullDir = Path.GetFullPath(dir);
                    if (Directory.Exists(fullDir))
                    {
                        foreach (var ext in extensions)
                        {
                            var files = Directory.GetFiles(fullDir, ext, SearchOption.TopDirectoryOnly)
                                .Where(f => f.ToLower().Contains("logo"))
                                .ToList();
                            logoFiles.AddRange(files);
                        }
                    }
                }
                catch { }
            }

            // Usuń duplikaty
            return logoFiles.Select(f => Path.GetFullPath(f)).Distinct().ToList();
        }

        /// <summary>
        /// Ustawia logo z pliku graficznego
        /// </summary>
        private void SetLogoFromFile(string logoPath)
        {
            try
            {
                // Zamień TextBlock na Image w LogoBox
                LogoBox.Child = null;

                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(logoPath);
                bitmap.DecodePixelHeight = 20;
                bitmap.EndInit();

                var image = new Image
                {
                    Source = bitmap,
                    Width = 20,
                    Height = 20,
                    Stretch = System.Windows.Media.Stretch.Uniform
                };

                LogoBox.Child = image;

                Debug.WriteLine($"[DashboardPrzychodu] Ustawiono logo z pliku: {logoPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DashboardPrzychodu] Błąd ładowania logo: {ex.Message}");
                // Przywróć domyślne emoji
                LogoBox.Child = LogoIcon;
                MessageBox.Show($"Nie można załadować obrazka:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Prawy przycisk myszy na kafelku KPI - pokazuje menu wyboru ikony
        /// </summary>
        private void KpiIcon_RightClick(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            _currentKpiTarget = border?.Tag?.ToString();

            var menu = (ContextMenu)FindResource("IconContextMenu");

            // Aktualizuj tytuł menu
            var names = new Dictionary<string, string>
            {
                {"plan", "PLAN"},
                {"zwazone", "ZWAŻONE"},
                {"pozostalo", "POZOSTAŁO"},
                {"odchylenie", "ODCHYLENIE"},
                {"tuszki", "TUSZKI"},
                {"realizacja", "REALIZACJA"}
            };

            if (names.TryGetValue(_currentKpiTarget ?? "", out var name))
            {
                // Menu title is the first item
                if (menu.Items[0] is MenuItem titleItem)
                {
                    titleItem.Header = $"Wybierz ikonę dla: {name}";
                }
            }

            menu.PlacementTarget = border;
            menu.IsOpen = true;
            e.Handled = true;
        }

        /// <summary>
        /// Ustawia emoji jako ikonę KPI
        /// </summary>
        private void Icon_SetEmoji(object sender, RoutedEventArgs e)
        {
            var emoji = (sender as MenuItem)?.Tag?.ToString();
            if (string.IsNullOrEmpty(emoji) || string.IsNullOrEmpty(_currentKpiTarget)) return;

            switch (_currentKpiTarget)
            {
                case "plan": IcoPlan.Text = emoji; break;
                case "zwazone": IcoZwazone.Text = emoji; break;
                case "pozostalo": IcoPozostalo.Text = emoji; break;
                case "odchylenie": IcoOdchylenie.Text = emoji; break;
                case "tuszki": IcoTuszki.Text = emoji; break;
                case "realizacja": IcoRealizacja.Text = emoji; break;
            }

            // Można zapisać do ustawień: SaveSetting($"Icon_{_currentKpiTarget}", emoji);
        }

        #endregion
    }
}
