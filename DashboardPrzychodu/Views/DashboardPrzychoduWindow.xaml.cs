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
        private Storyboard _pulseStoryboard;

        private const int AUTO_REFRESH_SECONDS = 30;

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
            dpData.SelectedDateChanged += async (s, e) => await LoadDataAsync();
            txtSearch.TextChanged += TxtSearch_TextChanged;
            Loaded += async (s, e) => await InitializeAsync();
            Closing += DashboardPrzychoduWindow_Closing;
        }

        /// <summary>
        /// Inicjalizacja animacji pulsowania dla kafelka odchylenia
        /// </summary>
        private void InitializePulseAnimation()
        {
            _pulseStoryboard = new Storyboard();
            _pulseStoryboard.RepeatBehavior = RepeatBehavior.Forever;
            _pulseStoryboard.AutoReverse = true;

            var colorAnimation = new ColorAnimation
            {
                From = Color.FromRgb(233, 69, 96),    // #E94560
                To = Color.FromRgb(255, 107, 107),    // #FF6B6B
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

                await System.Threading.Tasks.Task.WhenAll(dostawyTask, podsumowanieTask, prognozaTask, harmonogramyTask);

                var noweDostawy = await dostawyTask;
                _podsumowanie = await podsumowanieTask;
                _prognoza = await prognozaTask;
                var noweHarmonogramy = await harmonogramyTask;

                // Aktualizuj UI w watku UI
                await Dispatcher.InvokeAsync(() =>
                {
                    // Aktualizuj kolekcje dostaw
                    _dostawy.Clear();
                    foreach (var dostawa in noweDostawy)
                    {
                        _dostawy.Add(dostawa);
                    }

                    // Aktualizuj kolekcje harmonogramow
                    _postepyHarmonogramow.Clear();
                    foreach (var harmonogram in noweHarmonogramy)
                    {
                        _postepyHarmonogramow.Add(harmonogram);
                    }

                    // Aktualizuj podsumowanie
                    UpdateSummaryUI();

                    // Aktualizuj prognoze redukcji
                    UpdatePrognozaUI();

                    // Aktualizuj wiersz podsumowania tabeli
                    UpdateTableSummary();

                    // Aktualizuj pasek postepu
                    UpdateProgressBar();

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
                ShowError($"Nie udało się połączyć z bazą danych.\n\n{ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        /// <summary>
        /// Aktualizacja UI podsumowania
        /// </summary>
        private void UpdateSummaryUI()
        {
            // Planowane
            txtKgPlan.Text = _podsumowanie.KgPlanSuma.ToString("N0");
            txtSztPlan.Text = $"{_podsumowanie.SztukiPlanSuma:N0} szt";

            // Zważone z trendem
            decimal noweZwazone = _podsumowanie.KgZwazoneSuma;
            txtKgZwazone.Text = noweZwazone.ToString("N0");
            txtSztZwazone.Text = $"{_podsumowanie.SztukiZwazoneSuma:N0} szt";

            // Aktualizuj trend
            UpdateTrend(noweZwazone);

            // Pozostałe
            txtKgPozostalo.Text = _podsumowanie.KgPozostalo.ToString("N0");
            txtSztPozostalo.Text = $"{_podsumowanie.SztukiPozostalo:N0} szt";

            // Odchylenie
            if (_podsumowanie.KgZwazoneSuma > 0)
            {
                string znak = _podsumowanie.OdchylenieKgSuma > 0 ? "+" : "";
                txtOdchylenie.Text = $"{znak}{_podsumowanie.OdchylenieKgSuma:N0}";
                txtOdchylenieProc.Text = $"{znak}{_podsumowanie.OdchylenieProc:N1}%";

                // Kolorowanie odchylenia
                var brush = _podsumowanie.Poziom switch
                {
                    PoziomOdchylenia.OK => FindResource("StatusOKBrush") as SolidColorBrush,
                    PoziomOdchylenia.Uwaga => FindResource("StatusWarningBrush") as SolidColorBrush,
                    PoziomOdchylenia.Problem => FindResource("StatusErrorBrush") as SolidColorBrush,
                    _ => FindResource("TextSecondaryBrush") as SolidColorBrush
                };
                txtOdchylenie.Foreground = brush;
                txtOdchylenieProc.Foreground = brush;

                // Pulsujące obramowanie przy problemie
                UpdatePulseAnimation(_podsumowanie.Poziom == PoziomOdchylenia.Problem);
            }
            else
            {
                txtOdchylenie.Text = "-";
                txtOdchylenieProc.Text = "";
                txtOdchylenie.Foreground = FindResource("TextSecondaryBrush") as SolidColorBrush;
                UpdatePulseAnimation(false);
            }

            // Realizacja
            txtRealizacja.Text = $"{_podsumowanie.ProcentRealizacjiKg}%";
            txtDostawyStatus.Text = $"({_podsumowanie.LiczbaZwazonych}/{_podsumowanie.LiczbaDostawOgolem} dostaw)";

            // Liczniki statusów dostaw (mini panel)
            int zwazoneCount = _dostawy.Count(d => d.Status == StatusDostawy.Zwazony);
            int bruttoCount = _dostawy.Count(d => d.Status == StatusDostawy.BruttoWpisane);
            int oczekujeCount = _dostawy.Count(d => d.Status == StatusDostawy.Oczekuje);
            txtZwazoneCount.Text = zwazoneCount.ToString();
            txtBruttoCount.Text = bruttoCount.ToString();
            txtOczekujeCount.Text = oczekujeCount.ToString();

            // Prognoza produkcji - tuszki planowane vs rzeczywiste
            // Używamy TuszkiPlanKg z modelu (obliczone z KgPlanSuma * 78%)
            decimal tuszkiPlan = _podsumowanie.TuszkiPlanKg;
            decimal tuszkiRzecz = _podsumowanie.PrognozaTuszekKg;

            txtTuszkiPlan.Text = tuszkiPlan.ToString("N0");
            txtPrognozaTuszek.Text = tuszkiRzecz.ToString("N0");

            // Wzór obliczeniowy
            txtWzorTuszki.Text = $"{_podsumowanie.KgZwazoneSuma:N0} kg × 78% = {tuszkiRzecz:N0} kg";

            txtPrognozaA.Text = $"{_podsumowanie.PrognozaKlasaAKg:N0} kg";
            txtPrognozaB.Text = $"{_podsumowanie.PrognozaKlasaBKg:N0} kg";

            // Różnica tuszek (rzeczywiste - planowane)
            decimal roznicaTuszek = tuszkiRzecz - tuszkiPlan;
            if (roznicaTuszek != 0 && _podsumowanie.KgZwazoneSuma > 0)
            {
                string znak = roznicaTuszek > 0 ? "+" : "";
                txtTuszkiRoznica.Text = $"{znak}{roznicaTuszek:N0} kg tuszek";
                txtTuszkiRoznica.Foreground = roznicaTuszek > 0
                    ? new SolidColorBrush(Color.FromRgb(78, 204, 163))   // Zielony - więcej
                    : new SolidColorBrush(Color.FromRgb(252, 129, 129)); // Czerwony - mniej
            }
            else
            {
                txtTuszkiRoznica.Text = "";
            }

            // Średnia waga planowana - z HarmonogramDostaw (WagaDek)
            // Używamy SrWagaPlanSrednia z modelu (obliczone w SQL z HarmonogramDostaw)
            if (_podsumowanie.SrWagaPlanSrednia.HasValue)
            {
                txtSrWagaPlan.Text = _podsumowanie.SrWagaPlanSrednia.Value.ToString("N2");
            }
            else if (_podsumowanie.SztukiPlanSuma > 0)
            {
                // Fallback: oblicz z kg/szt jeśli brak danych z harmonogramu
                decimal srWagaPlan = _podsumowanie.KgPlanSuma / _podsumowanie.SztukiPlanSuma;
                txtSrWagaPlan.Text = srWagaPlan.ToString("N2");
            }
            else
            {
                txtSrWagaPlan.Text = "-";
            }

            // Średnia waga rzeczywista - z FarmerCalc (NettoWeight / LumQnt)
            // Używamy SrWagaRzeczSrednia z modelu (obliczone w SQL)
            if (_podsumowanie.SrWagaRzeczSrednia.HasValue)
            {
                txtSrWagaRzecz.Text = _podsumowanie.SrWagaRzeczSrednia.Value.ToString("N2");
            }
            else
            {
                txtSrWagaRzecz.Text = "-";
            }

            // Porownanie wag - uzywamy wartosci z modelu (z HarmonogramDostaw)
            UpdateWeightComparison(_podsumowanie.SrWagaPlanSrednia, _podsumowanie.SrWagaRzeczSrednia);
        }

        /// <summary>
        /// Aktualizacja panelu prognozy redukcji zamowien
        /// </summary>
        private void UpdatePrognozaUI()
        {
            if (_prognoza == null || _prognoza.AutaZwazone == 0)
            {
                // Ukryj panel jesli brak danych
                borderPrognoza.Visibility = Visibility.Collapsed;
                return;
            }

            // Pokaz panel tylko jesli jest alert (trend < 98%)
            if (_prognoza.JestAlert)
            {
                borderPrognoza.Visibility = Visibility.Visible;

                // Kolor alertu
                var alertColor = _prognoza.AlertKolor as SolidColorBrush;
                borderAlertIkona.Background = alertColor;
                txtAlertTuszkiPrognoza.Foreground = alertColor;
                txtAlertRedukcja.Foreground = alertColor;

                // Teksty
                txtAlertPoziom.Text = _prognoza.PoziomAlertu;
                txtAlertPrognoza.Text = _prognoza.PrognozaDisplay;
                txtAlertTuszkiPlan.Text = _prognoza.TuszkiPlan.ToString("N0");
                txtAlertTuszkiPrognoza.Text = _prognoza.TuszkiPrognoza.ToString("N0");
                txtAlertRedukcja.Text = _prognoza.RedukcjaDisplay;
                txtAlertAutaZwazone.Text = _prognoza.AutaZwazone.ToString();
                txtAlertAutaOgolem.Text = _prognoza.AutaOgolem.ToString();
                txtAlertTrendProc.Text = _prognoza.TrendProc.ToString("N0");
            }
            else
            {
                // Ukryj panel jesli wszystko OK
                borderPrognoza.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Aktualizacja kafelka porownania wag (zintegrowany w kafelku SREDNIE WAGI)
        /// Używa danych z HarmonogramDostaw (WagaDek) vs FarmerCalc (NettoWeight/LumQnt)
        /// </summary>
        private void UpdateWeightComparison(decimal? wagaPlan, decimal? wagaRzecz)
        {
            if (!wagaPlan.HasValue || !wagaRzecz.HasValue)
            {
                txtWagaArrow.Text = "→";
                txtWagaArrow.Foreground = new SolidColorBrush(Color.FromRgb(74, 85, 104));
                txtWagaInterpretacja.Text = "";
                return;
            }

            // Używamy OdchylenieWagiSrednie z modelu (obliczone jako różnica wag)
            decimal? odchylenie = _podsumowanie.OdchylenieWagiSrednie;
            decimal roznica = odchylenie ?? (wagaRzecz.Value - wagaPlan.Value);

            if (roznica > 0.02m)
            {
                // Ptaki cięższe niż deklarowane (>0.02 kg/szt)
                txtWagaArrow.Text = "↑";
                txtWagaArrow.Foreground = new SolidColorBrush(Color.FromRgb(78, 204, 163)); // Zielony
                txtWagaInterpretacja.Text = $"+{roznica:N2}kg";
                txtWagaInterpretacja.Foreground = new SolidColorBrush(Color.FromRgb(78, 204, 163));
            }
            else if (roznica < -0.02m)
            {
                // Ptaki lżejsze niż deklarowane (<-0.02 kg/szt)
                txtWagaArrow.Text = "↓";
                txtWagaArrow.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)); // Czerwony
                txtWagaInterpretacja.Text = $"{roznica:N2}kg";
                txtWagaInterpretacja.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
            }
            else
            {
                // Zgodne (±0.02 kg/szt)
                txtWagaArrow.Text = "≈";
                txtWagaArrow.Foreground = new SolidColorBrush(Color.FromRgb(34, 211, 238)); // Cyan
                txtWagaInterpretacja.Text = "OK";
                txtWagaInterpretacja.Foreground = new SolidColorBrush(Color.FromRgb(34, 211, 238));
            }
        }

        /// <summary>
        /// Aktualizacja trendu dla kafelka ZWAŻONE
        /// </summary>
        private void UpdateTrend(decimal noweZwazone)
        {
            if (_poprzednieZwazone == 0)
            {
                // Pierwsze ładowanie
                txtTrendZwazone.Text = "";
                txtTrendInfo.Text = "";
                _trendStartTime = DateTime.Now;
            }
            else if (noweZwazone > _poprzednieZwazone)
            {
                txtTrendZwazone.Text = "↑";
                txtTrendZwazone.Foreground = new SolidColorBrush(Color.FromRgb(78, 204, 163));
                decimal roznica = noweZwazone - _poprzednieZwazone;
                txtTrendInfo.Text = $"+{roznica:N0}";
            }
            else if (noweZwazone < _poprzednieZwazone)
            {
                txtTrendZwazone.Text = "↓";
                txtTrendZwazone.Foreground = new SolidColorBrush(Color.FromRgb(233, 69, 96));
                decimal roznica = _poprzednieZwazone - noweZwazone;
                txtTrendInfo.Text = $"-{roznica:N0}";
            }
            else
            {
                txtTrendZwazone.Text = "→";
                txtTrendZwazone.Foreground = new SolidColorBrush(Color.FromRgb(113, 128, 150));
                txtTrendInfo.Text = "";
            }

            _poprzednieZwazone = noweZwazone;
        }

        /// <summary>
        /// Włącza/wyłącza animację pulsowania obramowania
        /// </summary>
        private void UpdatePulseAnimation(bool enable)
        {
            try
            {
                if (enable)
                {
                    borderOdchylenie.BorderBrush = new SolidColorBrush(Color.FromRgb(233, 69, 96));
                    _pulseStoryboard.Begin();
                }
                else
                {
                    _pulseStoryboard.Stop();
                    borderOdchylenie.BorderBrush = new SolidColorBrush(Colors.Transparent);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DashboardPrzychodu] Błąd animacji: {ex.Message}");
            }
        }

        /// <summary>
        /// Aktualizacja paska postępu realizacji
        /// </summary>
        private void UpdateProgressBar()
        {
            if (_podsumowanie.KgPlanSuma > 0)
            {
                double procent = (double)_podsumowanie.ProcentRealizacjiKg;
                double maxWidth = progressFill.Parent is Border parentBorder ? parentBorder.ActualWidth : 800;

                if (maxWidth <= 0) maxWidth = 800;

                double targetWidth = Math.Min(100, procent) / 100.0 * maxWidth;

                // Animacja paska
                var animation = new DoubleAnimation
                {
                    To = targetWidth,
                    Duration = TimeSpan.FromMilliseconds(500),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                progressFill.BeginAnimation(WidthProperty, animation);

                // Zmiana koloru w zależności od postępu
                if (procent >= 80)
                    progressFill.Background = new SolidColorBrush(Color.FromRgb(78, 204, 163)); // Zielony
                else if (procent >= 50)
                    progressFill.Background = new SolidColorBrush(Color.FromRgb(255, 179, 71)); // Pomarańczowy
                else
                    progressFill.Background = new SolidColorBrush(Color.FromRgb(233, 69, 96)); // Czerwony

                txtProgressPercent.Text = $"{procent:N0}%";
                txtProgressMax.Text = $"{_podsumowanie.KgPlanSuma:N0} kg";
            }
            else
            {
                progressFill.Width = 0;
                txtProgressPercent.Text = "0%";
                txtProgressMax.Text = "0 kg";
            }
        }

        /// <summary>
        /// Aktualizacja wiersza podsumowania tabeli
        /// </summary>
        private void UpdateTableSummary()
        {
            int liczbaDostawFiltrowanych = _dostawyView.Cast<object>().Count();
            txtSumaDostawy.Text = $"{liczbaDostawFiltrowanych} dostaw";

            decimal sumaPlan = _dostawy.Sum(d => d.KgPlan);
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

                // Kolor
                if (sumaPlan > 0)
                {
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
                txtSumaOdchylenie.Foreground = new SolidColorBrush(Color.FromRgb(160, 174, 192));
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

            // Suma tuszek planowanych (78% z kg plan)
            decimal sumaTuszkiPlan = _dostawy.Sum(d => d.TuszkiPlanKg);
            txtSumaTuszkiPlan.Text = sumaTuszkiPlan.ToString("N0");

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
        /// Pokazuje szczegóły dostawy w oknie popup
        /// </summary>
        private void ShowDeliveryDetails(DostawaItem dostawa)
        {
            var detailWindow = new Window
            {
                Title = $"Szczegóły dostawy - {dostawa.Hodowca}",
                Width = 500,
                Height = 450,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 46)),
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Nagłówek
            var header = new TextBlock
            {
                Text = $"Dostawa #{dostawa.NrKursu}",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 15)
            };
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            // Szczegóły
            var details = new StackPanel();
            Grid.SetRow(details, 1);

            void AddDetailRow(string label, string value, Brush valueBrush = null)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
                row.Children.Add(new TextBlock
                {
                    Text = label,
                    Width = 150,
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    FontSize = 13
                });
                row.Children.Add(new TextBlock
                {
                    Text = value,
                    Foreground = valueBrush ?? Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 13
                });
                details.Children.Add(row);
            }

            AddDetailRow("Hodowca:", dostawa.Hodowca);
            AddDetailRow("Data:", dostawa.Data.ToString("dd.MM.yyyy"));
            AddDetailRow("Nr kursu:", dostawa.NrKursu.ToString());
            details.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(45, 58, 92)), Margin = new Thickness(0, 10, 0, 10) });

            AddDetailRow("Plan [kg]:", dostawa.KgPlan.ToString("N0"));
            AddDetailRow("Plan [szt]:", dostawa.SztukiPlan.ToString("N0"));
            AddDetailRow("Rzeczywiste [kg]:", dostawa.KgRzeczywiste.ToString("N0"),
                dostawa.Status == StatusDostawy.Zwazony ? new SolidColorBrush(Color.FromRgb(78, 204, 163)) : null);
            AddDetailRow("Rzeczywiste [szt]:", dostawa.SztukiRzeczywiste.ToString("N0"));
            details.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(45, 58, 92)), Margin = new Thickness(0, 10, 0, 10) });

            var odchylenieBrush = dostawa.Poziom switch
            {
                PoziomOdchylenia.OK => new SolidColorBrush(Color.FromRgb(78, 204, 163)),
                PoziomOdchylenia.Uwaga => new SolidColorBrush(Color.FromRgb(255, 179, 71)),
                PoziomOdchylenia.Problem => new SolidColorBrush(Color.FromRgb(233, 69, 96)),
                _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
            AddDetailRow("Odchylenie:", dostawa.OdchylenieDisplay, odchylenieBrush);
            AddDetailRow("Średnia waga:", dostawa.SredniaWagaRzeczywistaCalc?.ToString("N3") ?? "-");
            AddDetailRow("Status:", dostawa.StatusText);
            details.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(45, 58, 92)), Margin = new Thickness(0, 10, 0, 10) });

            AddDetailRow("Brutto:", dostawa.Brutto.ToString("N0"));
            AddDetailRow("Tara:", dostawa.Tara.ToString("N0"));
            AddDetailRow("Przyjazd:", dostawa.PrzyjazdDisplay);
            AddDetailRow("Ważył:", dostawa.KtoWazyl ?? "-");

            grid.Children.Add(details);

            // Przycisk zamknij
            var closeButton = new Button
            {
                Content = "Zamknij",
                Padding = new Thickness(20, 8, 20, 8),
                Background = new SolidColorBrush(Color.FromRgb(15, 52, 96)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };
            closeButton.Click += (s, e) => detailWindow.Close();
            Grid.SetRow(closeButton, 2);
            grid.Children.Add(closeButton);

            detailWindow.Content = grid;
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
        }

        /// <summary>
        /// Ukrywa overlay błędu
        /// </summary>
        private void HideError()
        {
            errorOverlay.Visibility = Visibility.Collapsed;
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
                    Background = new SolidColorBrush(Color.FromRgb(26, 26, 46))
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
                    Background = new SolidColorBrush(Color.FromRgb(22, 33, 62)),
                    Foreground = Brushes.White,
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
