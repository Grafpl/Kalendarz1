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
using System.Windows.Media;
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
        private readonly DispatcherTimer _autoRefreshTimer;
        private readonly DispatcherTimer _countdownTimer;
        private PodsumowanieDnia _podsumowanie;
        private ICollectionView _dostawyView;
        private int _secondsToRefresh = 30;
        private bool _isLoading = false;

        private const int AUTO_REFRESH_SECONDS = 30;

        public DashboardPrzychoduWindow()
        {
            InitializeComponent();

            _przychodService = new PrzychodService();
            _dostawy = new ObservableCollection<DostawaItem>();
            _podsumowanie = new PodsumowanieDnia();

            // Konfiguracja DataGrid
            dgDostawy.ItemsSource = _dostawy;
            _dostawyView = CollectionViewSource.GetDefaultView(_dostawy);

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

            // Eventy
            dpData.SelectedDateChanged += async (s, e) => await LoadDataAsync();
            txtSearch.TextChanged += TxtSearch_TextChanged;
            Loaded += async (s, e) => await InitializeAsync();
            Closing += DashboardPrzychoduWindow_Closing;
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
                ShowLoading("Ladowanie danych...");

                Debug.WriteLine($"[DashboardPrzychodu] Pobieranie danych na {selectedDate:yyyy-MM-dd}");

                // Pobierz dane równolegle
                var dostawyTask = _przychodService.GetDostawyAsync(selectedDate);
                var podsumowanieTask = _przychodService.GetPodsumowanieAsync(selectedDate);

                await System.Threading.Tasks.Task.WhenAll(dostawyTask, podsumowanieTask);

                var noweDostawy = await dostawyTask;
                _podsumowanie = await podsumowanieTask;

                // Aktualizuj UI w wątku UI
                await Dispatcher.InvokeAsync(() =>
                {
                    // Aktualizuj kolekcję dostaw
                    _dostawy.Clear();
                    foreach (var dostawa in noweDostawy)
                    {
                        _dostawy.Add(dostawa);
                    }

                    // Aktualizuj podsumowanie
                    UpdateSummaryUI();

                    // Aktualizuj licznik wyników
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

            // Zważone
            txtKgZwazone.Text = _podsumowanie.KgZwazoneSuma.ToString("N0");
            txtSztZwazone.Text = $"{_podsumowanie.SztukiZwazoneSuma:N0} szt";

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
            }
            else
            {
                txtOdchylenie.Text = "-";
                txtOdchylenieProc.Text = "";
                txtOdchylenie.Foreground = FindResource("TextSecondaryBrush") as SolidColorBrush;
            }

            // Realizacja
            txtRealizacja.Text = $"{_podsumowanie.ProcentRealizacjiKg}%";
            txtDostawyStatus.Text = $"{_podsumowanie.LiczbaZwazonych}/{_podsumowanie.LiczbaDostawOgolem} dostaw";

            // Prognoza produkcji
            txtPrognozaA.Text = $"{_podsumowanie.PrognozaKlasaAKg:N0} kg";
            txtPrognozaB.Text = $"{_podsumowanie.PrognozaKlasaBKg:N0} kg";

            // Średnia waga planowana (kg plan / sztuki plan)
            if (_podsumowanie.SztukiPlanSuma > 0)
            {
                decimal srWagaPlan = _podsumowanie.KgPlanSuma / _podsumowanie.SztukiPlanSuma;
                txtSrWagaPlan.Text = srWagaPlan.ToString("N2");
            }
            else
            {
                txtSrWagaPlan.Text = "-";
            }

            // Średnia waga rzeczywista (netto kg / lumel sztuki)
            if (_podsumowanie.SztukiZwazoneSuma > 0)
            {
                decimal srWagaRzecz = _podsumowanie.KgZwazoneSuma / _podsumowanie.SztukiZwazoneSuma;
                txtSrWagaRzecz.Text = srWagaRzecz.ToString("N2");
            }
            else
            {
                txtSrWagaRzecz.Text = "-";
            }
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

            // Aktualizuj licznik
            txtLiczbaWynikow.Text = $"Wyniki: {_dostawyView.Cast<object>().Count()}";
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
