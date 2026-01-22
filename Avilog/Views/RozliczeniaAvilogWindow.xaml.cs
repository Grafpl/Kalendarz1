using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kalendarz1.Avilog.Helpers;
using Kalendarz1.Avilog.Models;
using Kalendarz1.Avilog.Services;

namespace Kalendarz1.Avilog.Views
{
    /// <summary>
    /// Okno rozliczeń transportowych Avilog - Dzienne zestawienie uboju
    /// </summary>
    public partial class RozliczeniaAvilogWindow : Window
    {
        private readonly AvilogDataService _dataService;
        private ObservableCollection<AvilogKursModel> _kursyCollection;
        private List<AvilogKursModel> _allKursy;
        private AvilogSummaryModel _summary;
        private decimal _stawkaZaKg = 0.119m;

        public RozliczeniaAvilogWindow()
        {
            InitializeComponent();

            // Ustaw ikonę okna
            try { WindowIconHelper.SetIcon(this); } catch { }

            _dataService = new AvilogDataService();
            _kursyCollection = new ObservableCollection<AvilogKursModel>();
            _allKursy = new List<AvilogKursModel>();

            // Ustaw domyślny zakres dat (ten tydzień)
            var (od, do_) = AvilogCalculator.GetCurrentWeekRange();
            datePickerOd.SelectedDate = od;
            datePickerDo.SelectedDate = do_;

            // Bindowanie kolekcji
            dataGridKursy.ItemsSource = _kursyCollection;

            // Załaduj stawkę z bazy
            LoadStawkaAsync();

            // Skróty klawiszowe
            this.PreviewKeyDown += Window_PreviewKeyDown;

            // Załaduj dane przy starcie
            Loaded += async (s, e) => await LoadDataAsync();
        }

        #region === ŁADOWANIE DANYCH ===

        private async void LoadStawkaAsync()
        {
            try
            {
                _stawkaZaKg = await _dataService.GetAktualnaStawkaAsync();
                txtStawka.Text = _stawkaZaKg.ToString("N3");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania stawki: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            if (!datePickerOd.SelectedDate.HasValue || !datePickerDo.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz zakres dat.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dataOd = datePickerOd.SelectedDate.Value;
            var dataDo = datePickerDo.SelectedDate.Value;

            if (dataOd > dataDo)
            {
                MessageBox.Show("Data 'Od' nie może być późniejsza niż data 'Do'.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                ShowLoading(true, "Ładowanie danych...");

                // Pobierz stawkę z pola tekstowego
                if (decimal.TryParse(txtStawka.Text.Replace(",", "."), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal stawka))
                {
                    _stawkaZaKg = stawka;
                }

                // Pobierz dane z bazy
                var kursy = await _dataService.GetKursyAsync(dataOd, dataDo);

                // Zapisz wszystkie kursy
                _allKursy = kursy;

                // Aktualizuj kolekcję
                _kursyCollection.Clear();
                foreach (var kurs in kursy)
                {
                    _kursyCollection.Add(kurs);
                }

                // Oblicz podsumowanie
                _summary = CalculateSummaryFromKursy(kursy);
                UpdateSummaryUI();

                // Aktualizuj nagłówek
                txtOkres.Text = $"Okres: {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}";

                ShowLoading(false, $"Załadowano {kursy.Count} kursów");
            }
            catch (Exception ex)
            {
                ShowLoading(false, "Błąd ładowania");
                MessageBox.Show($"Błąd podczas ładowania danych:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private AvilogSummaryModel CalculateSummaryFromKursy(List<AvilogKursModel> kursy)
        {
            var summary = new AvilogSummaryModel
            {
                DataOd = datePickerOd.SelectedDate ?? DateTime.Today,
                DataDo = datePickerDo.SelectedDate ?? DateTime.Today,
                LiczbaKursow = kursy.Count,
                LiczbaZestawow = kursy.Select(k => $"{k.CarID}-{k.TrailerID}").Distinct().Count(),
                LiczbaDni = kursy.Select(k => k.CalcDate.Date).Distinct().Count(),
                SumaSztuk = kursy.Sum(k => k.SztukiRazem),
                SumaUpadkowSzt = kursy.Sum(k => k.SztukiPadle),
                SumaBrutto = kursy.Sum(k => k.BruttoUbojni),
                SumaTara = kursy.Sum(k => k.TaraUbojni),
                SumaNetto = kursy.Sum(k => k.NettoUbojni),
                SumaUpadkowKg = kursy.Sum(k => k.UpadkiKg),
                SumaRoznicaKg = kursy.Sum(k => k.RoznicaKg),
                SumaKM = kursy.Sum(k => k.DystansKM),
                SumaGodzin = kursy.Sum(k => k.CzasUslugiGodziny),
                StawkaZaKg = _stawkaZaKg
            };

            return summary;
        }

        private void UpdateSummaryUI()
        {
            if (_summary == null) return;

            // Statystyki
            txtSumaSztuk.Text = $"{_summary.SumaSztuk:N0}";
            txtSumaBrutto.Text = $"{_summary.SumaBrutto:N0}";
            txtSumaTara.Text = $"{_summary.SumaTara:N0}";
            txtSumaNetto.Text = $"{_summary.SumaNetto:N0}";
            txtSumaUpadkow.Text = $"{_summary.SumaUpadkowKg:N0}";
            txtSumaRoznica.Text = $"{_summary.SumaRoznicaKg:N0}";
            txtSumaKM.Text = $"{_summary.SumaKM:N0}";
            txtSumaGodzin.Text = FormatHours(_summary.SumaGodzin);

            // Formuła rozliczenia
            txtFormulaCena.Text = $"{_stawkaZaKg:N3} zł";
            txtFormulaRoznica.Text = $"{_summary.SumaRoznicaKg:N0} kg";
            txtDoZaplaty.Text = $"{_summary.DoZaplaty:N2} zł";
        }

        private string FormatHours(decimal totalHours)
        {
            int hours = (int)totalHours;
            int minutes = (int)((totalHours - hours) * 60);
            return $"{hours}:{minutes:D2}";
        }

        private void ShowLoading(bool isLoading, string message = "")
        {
            progressBar.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            txtStatus.Text = message;
        }

        #endregion

        #region === OBSŁUGA PRZYCISKÓW ===

        private async void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private void BtnThisWeek_Click(object sender, RoutedEventArgs e)
        {
            var (od, do_) = AvilogCalculator.GetCurrentWeekRange();
            datePickerOd.SelectedDate = od;
            datePickerDo.SelectedDate = do_;
        }

        private void BtnPrevWeek_Click(object sender, RoutedEventArgs e)
        {
            if (datePickerOd.SelectedDate.HasValue)
            {
                var currentStart = datePickerOd.SelectedDate.Value;
                var prevStart = currentStart.AddDays(-7);
                var prevEnd = prevStart.AddDays(4); // pon-pt

                datePickerOd.SelectedDate = prevStart;
                datePickerDo.SelectedDate = prevEnd;
            }
            else
            {
                var (od, do_) = AvilogCalculator.GetPreviousWeekRange();
                datePickerOd.SelectedDate = od;
                datePickerDo.SelectedDate = do_;
            }
        }

        private void BtnNextWeek_Click(object sender, RoutedEventArgs e)
        {
            if (datePickerOd.SelectedDate.HasValue)
            {
                var currentStart = datePickerOd.SelectedDate.Value;
                var nextStart = currentStart.AddDays(7);
                var nextEnd = nextStart.AddDays(4); // pon-pt

                datePickerOd.SelectedDate = nextStart;
                datePickerDo.SelectedDate = nextEnd;
            }
        }

        private async void BtnSaveStawka_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(txtStawka.Text.Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal nowaStawka))
            {
                MessageBox.Show("Nieprawidłowa wartość stawki.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (nowaStawka <= 0 || nowaStawka > 10)
            {
                MessageBox.Show("Stawka musi być większa niż 0 i mniejsza niż 10 zł/kg.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var result = MessageBox.Show(
                $"Czy chcesz zapisać nową stawkę?\n\nStara stawka: {_stawkaZaKg:N3} zł/kg\nNowa stawka: {nowaStawka:N3} zł/kg",
                "Potwierdzenie",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _dataService.SaveStawkaAsync(nowaStawka, Environment.UserName);
                    _stawkaZaKg = nowaStawka;
                    MessageBox.Show("Stawka została zapisana.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Przelicz podsumowanie
                    if (_summary != null)
                    {
                        _summary.StawkaZaKg = _stawkaZaKg;
                        UpdateSummaryUI();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd zapisu stawki:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnHistoriaStawek_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var historia = await _dataService.GetHistoriaStawekAsync();

                if (!historia.Any())
                {
                    MessageBox.Show("Brak historii zmian stawki.", "Historia stawek", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine("HISTORIA ZMIAN STAWKI ZA KG\n");
                sb.AppendLine("─────────────────────────────────────────");

                foreach (var s in historia)
                {
                    sb.AppendLine($"Stawka: {s.StawkaZaKg:N4} zł/kg");
                    sb.AppendLine($"Od: {s.DataOd:dd.MM.yyyy}  Do: {(s.DataDo.HasValue ? s.DataDo.Value.ToString("dd.MM.yyyy") : "obecnie")}");
                    sb.AppendLine($"Zmienione przez: {s.ZmienionePrzez} ({s.DataZmiany:dd.MM.yyyy HH:mm})");
                    if (!string.IsNullOrEmpty(s.Uwagi))
                        sb.AppendLine($"Uwagi: {s.Uwagi}");
                    sb.AppendLine("─────────────────────────────────────────");
                }

                MessageBox.Show(sb.ToString(), "Historia stawek", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd pobierania historii:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_allKursy == null || !_allKursy.Any())
            {
                MessageBox.Show("Brak danych do eksportu.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var exportService = new AvilogExportService();
                var dataOd = datePickerOd.SelectedDate.Value;
                var dataDo = datePickerDo.SelectedDate.Value;

                string filePath = exportService.ExportToExcel(
                    _allKursy,
                    null, // brak dni
                    _summary,
                    dataOd,
                    dataDo,
                    _stawkaZaKg);

                if (!string.IsNullOrEmpty(filePath))
                {
                    var result = MessageBox.Show(
                        $"Plik Excel został zapisany:\n{filePath}\n\nCzy chcesz otworzyć plik?",
                        "Eksport zakończony",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd eksportu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Funkcja drukowania raportu PDF będzie dostępna wkrótce.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_summary == null)
            {
                MessageBox.Show("Brak danych do skopiowania.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string text = GenerateSummaryText();
                Clipboard.SetText(text);
                MessageBox.Show("Podsumowanie skopiowane do schowka.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd kopiowania:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region === DATAGRID EVENTS ===

        private void DataGridKursy_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dataGridKursy.SelectedItem is AvilogKursModel kurs)
            {
                // Otwórz WidokAvilog dla tego rekordu
                try
                {
                    var avilogWindow = new WidokAvilog(kurs.ID);
                    avilogWindow.ShowDialog();

                    // Odśwież dane po zamknięciu
                    _ = LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd otwierania widoku Avilog:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region === SKRÓTY KLAWISZOWE ===

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                _ = LoadDataAsync();
                e.Handled = true;
            }
            else if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
            {
                BtnExportExcel_Click(null, null);
                e.Handled = true;
            }
            else if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.Control)
            {
                BtnPrint_Click(null, null);
                e.Handled = true;
            }
            else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && !txtStawka.IsFocused)
            {
                BtnCopy_Click(null, null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                this.Close();
                e.Handled = true;
            }
        }

        #endregion

        #region === GENEROWANIE TEKSTU ===

        private string GenerateSummaryText()
        {
            var dataOd = datePickerOd.SelectedDate.Value;
            var dataDo = datePickerDo.SelectedDate.Value;

            var sb = new StringBuilder();
            sb.AppendLine("╔═══════════════════════════════════════════════════════════╗");
            sb.AppendLine("║     DZIENNE ZESTAWIENIE UBOJU - AVILOG                    ║");
            sb.AppendLine("╠═══════════════════════════════════════════════════════════╣");
            sb.AppendLine($"║  Okres: {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}");
            sb.AppendLine("╠═══════════════════════════════════════════════════════════╣");
            sb.AppendLine();
            sb.AppendLine($"  Suma sztuk:          {_summary.SumaSztuk:N0} szt");
            sb.AppendLine($"  Suma brutto:         {_summary.SumaBrutto:N0} kg");
            sb.AppendLine($"  Suma tara:           {_summary.SumaTara:N0} kg");
            sb.AppendLine($"  Suma netto:          {_summary.SumaNetto:N0} kg");
            sb.AppendLine();
            sb.AppendLine($"  Suma upadków:        {_summary.SumaUpadkowKg:N0} kg");
            sb.AppendLine($"  RÓŻNICA KG:          {_summary.SumaRoznicaKg:N0} kg");
            sb.AppendLine();
            sb.AppendLine($"  Suma KM:             {_summary.SumaKM:N0} km");
            sb.AppendLine($"  Suma godzin:         {FormatHours(_summary.SumaGodzin)}");
            sb.AppendLine($"  Liczba kursów:       {_summary.LiczbaKursow}");
            sb.AppendLine();
            sb.AppendLine("╠═══════════════════════════════════════════════════════════╣");
            sb.AppendLine($"  Stawka:              {_stawkaZaKg:N3} zł/kg");
            sb.AppendLine($"  DO ZAPŁATY:          {_summary.DoZaplaty:N2} zł");
            sb.AppendLine("╚═══════════════════════════════════════════════════════════╝");
            sb.AppendLine();
            sb.AppendLine($"  Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}");

            return sb.ToString();
        }

        #endregion
    }
}
