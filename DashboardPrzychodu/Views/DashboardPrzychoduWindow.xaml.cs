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
using Kalendarz1.DashboardPrzychodu.Theme;

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
        private readonly ObservableCollection<HistoriaZmianItem> _historiaZmian;
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

        // Paleta hodowców → DashboardBrushes.HodowcaPalette (deterministyczny hash nazwy)
        private readonly Dictionary<string, Brush> _hodowcaColorMap = new Dictionary<string, Brush>(StringComparer.OrdinalIgnoreCase);

        // Persystowane ustawienia uzytkownika (#9)
        private DashboardSettings _settings = new DashboardSettings();
        private bool _settingsLoaded = false;

        public DashboardPrzychoduWindow()
        {
            InitializeComponent();

            _przychodService = new PrzychodService();
            _dostawy = new ObservableCollection<DostawaItem>();
            _postepyHarmonogramow = new ObservableCollection<PostepHarmonogramu>();
            _historiaZmian = new ObservableCollection<HistoriaZmianItem>();
            _podsumowanie = new PodsumowanieDnia();
            _prognoza = new PrognozaDnia();

            // Konfiguracja DataGrid
            dgDostawy.ItemsSource = _dostawy;
            _dostawyView = CollectionViewSource.GetDefaultView(_dostawy);

            // Konfiguracja listy harmonogramow
            icHarmonogramy.ItemsSource = _postepyHarmonogramow;
            // Konfiguracja listy historii zmian (sidebar pod AKCJE)
            icHistoriaZmian.ItemsSource = _historiaZmian;

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

            // Wczytaj persystowane ustawienia uzytkownika (#9)
            LoadSettings();

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

                // SKONSOLIDOWANY FETCH: 1 zapytanie z 4 result-setami (zamiast 4 round-tripow)
                // + osobny query do Symfonii dla faktycznego przychodu (inna baza)
                // + osobny query do FarmerCalcChangeLog dla historii zmian deklaracji
                var snapshotTask = _przychodService.GetAllAsync(selectedDate);
                var faktycznyTask = _przychodService.GetFaktycznyPrzychodAsync(selectedDate);
                var historiaTask = _przychodService.GetHistoriaZmianAsync(selectedDate);

                await System.Threading.Tasks.Task.WhenAll(snapshotTask, faktycznyTask, historiaTask);

                var snapshot = await snapshotTask;
                var faktyczny = await faktycznyTask;
                var historia = await historiaTask;

                var noweDostawy = snapshot.Dostawy;
                _podsumowanie = snapshot.Podsumowanie;
                _prognoza = snapshot.Prognoza;
                var noweHarmonogramy = snapshot.Postepy;

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

                    // Aktualizuj historie zmian (sidebar pod AKCJE)
                    _historiaZmian.Clear();
                    foreach (var h in historia) _historiaZmian.Add(h);
                    if (txtHistoriaCount != null) txtHistoriaCount.Text = $"({_historiaZmian.Count})";

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
                    ("GetAllAsync (LibraNet)", async () => await _przychodService.GetAllAsync(selectedDate)),
                    ("GetFaktycznyPrzychodAsync (Handel)", async () => { await _przychodService.GetFaktycznyPrzychodAsync(selectedDate); }),
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
                txtOdchylenie.Foreground = _podsumowanie.Poziom switch
                {
                    PoziomOdchylenia.OK => DashboardBrushes.Green,
                    PoziomOdchylenia.Uwaga => DashboardBrushes.AmberLight,
                    PoziomOdchylenia.Problem => DashboardBrushes.Red,
                    _ => DashboardBrushes.TextSecondary
                };
                txtOdchylenieProc.Foreground = DashboardBrushes.TextMuted;

                // Pulsujące obramowanie przy problemie
                UpdatePulseAnimation(_podsumowanie.Poziom == PoziomOdchylenia.Problem);
            }
            else
            {
                txtOdchylenie.Text = "-";
                txtOdchylenieProc.Text = "";
                txtOdchylenie.Foreground = DashboardBrushes.TextSecondary;
                UpdatePulseAnimation(false);
            }

            // Realizacja - KPI Strip
            txtRealizacja.Text = $"{_podsumowanie.ProcentRealizacjiKg}%";
            txtDostawyStatus.Text = $"({_podsumowanie.LiczbaZwazonych}/{_podsumowanie.LiczbaDostawOgolem})";

            // Tuszki - KPI Strip
            txtPrognozaTuszek.Text = _podsumowanie.PrognozaTuszekKg.ToString("N0");

            // ETA + PACE - KPI Strip (#6 + #7)
            txtEta.Text = _podsumowanie.EtaDisplay;
            txtPaceBadge.Text = _podsumowanie.PaceBadge;
            txtPaceBadge.Foreground = _podsumowanie.PaceBrush;
            borderEta.ToolTip = _podsumowanie.EtaTooltip;

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
                SolidColorBrush odchBrush;
                if (tuszkiOdchylenie >= 0)
                    odchBrush = DashboardBrushes.Green;
                else if (Math.Abs(procent) <= 5)
                    odchBrush = DashboardBrushes.AmberLight;
                else
                    odchBrush = DashboardBrushes.Red;
                txtTuszkiOdchylenieSidebar.Foreground = odchBrush;
                txtTuszkiOdchylenieProcSidebar.Foreground = odchBrush;
            }
            else
            {
                txtTuszkiOdchylenieSidebar.Text = "-";
                txtTuszkiOdchylenieProcSidebar.Text = "";
                txtTuszkiOdchylenieSidebar.Foreground = DashboardBrushes.TextMuted;
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
                    txtTrendZwazone.Foreground = DashboardBrushes.Green;
                }
                else if (roznica < 0)
                {
                    txtTrendZwazone.Text = "↓";
                    txtTrendZwazone.Foreground = DashboardBrushes.Red;
                }
                else
                {
                    txtTrendZwazone.Text = "→";
                    txtTrendZwazone.Foreground = DashboardBrushes.TextMuted;
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
                            ? DashboardBrushes.Green
                            : DashboardBrushes.Red;
                    }
                    else if (roznica == 0)
                    {
                        txtTempoZwazone.Text = "bez zmian";
                        txtTempoZwazone.Foreground = DashboardBrushes.TextMuted;
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
                    // Mutable brush — Storyboard animuje BorderBrush.Color, więc Frozen nie zadziała
                    borderOdchylenie.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    _pulseStoryboard.Begin();
                }
                else
                {
                    _pulseStoryboard.Stop();
                    borderOdchylenie.BorderBrush = DashboardBrushes.TextMuted;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DashboardPrzychodu] Błąd animacji: {ex.Message}");
            }
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
                txtSumaOdchylenie.Foreground = DashboardBrushes.TextMuted;
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
        /// Dwuklik na wierszu - otwiera szczegoly dostawy (#12: refactor do osobnego okna XAML).
        /// </summary>
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgDostawy.SelectedItem is DostawaItem dostawa)
            {
                var win = new Kalendarz1.DashboardPrzychodu.Windows.DeliveryDetailsWindow(dostawa) { Owner = this };
                win.ShowDialog();
            }
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
        /// Zamykanie okna - zatrzymaj timery + zapisz ustawienia (#9)
        /// </summary>
        private void DashboardPrzychoduWindow_Closing(object sender, CancelEventArgs e)
        {
            Debug.WriteLine("[DashboardPrzychodu] Zamykanie okna, zatrzymuję timery...");
            _autoRefreshTimer?.Stop();
            _countdownTimer?.Stop();
            _pulseStoryboard?.Stop();
            SaveSettings();
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

    }
}
