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
    /// Okno rozliczeń transportowych Avilog
    /// </summary>
    public partial class RozliczeniaAvilogWindow : Window
    {
        private readonly AvilogDataService _dataService;
        private ObservableCollection<AvilogDayModel> _dniCollection;
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
            _dniCollection = new ObservableCollection<AvilogDayModel>();
            _kursyCollection = new ObservableCollection<AvilogKursModel>();
            _allKursy = new List<AvilogKursModel>();

            // Ustaw domyślny zakres dat (ten tydzień)
            var (od, do_) = AvilogCalculator.GetCurrentWeekRange();
            datePickerOd.SelectedDate = od;
            datePickerDo.SelectedDate = do_;

            // Bindowanie kolekcji
            dataGridDni.ItemsSource = _dniCollection;
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
                var dni = await _dataService.GetPodsumowaniaDzienneAsync(dataOd, dataDo);

                // Ustaw stawkę dla każdego dnia
                foreach (var dzien in dni)
                {
                    dzien.StawkaZaKg = _stawkaZaKg;
                }

                // Zapisz wszystkie kursy do filtrowania
                _allKursy = kursy;

                // Aktualizuj kolekcje
                _dniCollection.Clear();
                foreach (var dzien in dni)
                {
                    _dniCollection.Add(dzien);
                }

                // Dodaj wiersz sumy
                if (dni.Any())
                {
                    var sumaRow = AvilogCalculator.CreateSumRow(dni, _stawkaZaKg);
                    _dniCollection.Add(sumaRow);
                }

                _kursyCollection.Clear();
                foreach (var kurs in kursy)
                {
                    _kursyCollection.Add(kurs);
                }

                // Oblicz podsumowanie
                _summary = AvilogCalculator.CalculateSummary(kursy, _stawkaZaKg);
                UpdateSummaryUI();

                // Załaduj statystyki
                await LoadStatystykiAsync(dataOd, dataDo);

                ShowLoading(false, $"Załadowano {kursy.Count} kursów z {dni.Count} dni");
            }
            catch (Exception ex)
            {
                ShowLoading(false, "Błąd ładowania");
                MessageBox.Show($"Błąd podczas ładowania danych:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadStatystykiAsync(DateTime dataOd, DateTime dataDo)
        {
            try
            {
                // Top hodowcy
                var topHodowcy = await _dataService.GetTopHodowcyAsync(dataOd, dataDo, 5);
                var hodowcyItems = topHodowcy.Select((h, i) => new { Lp = $"{i + 1}.", Nazwa = h.Hodowca, Wartosc = h.Tonaz }).ToList();
                listTopHodowcy.ItemsSource = hodowcyItems;

                // Top kierowcy
                var topKierowcy = await _dataService.GetTopKierowcyAsync(dataOd, dataDo, 5);
                var kierowcyItems = topKierowcy.Select((k, i) => new { Lp = $"{i + 1}.", Nazwa = k.Kierowca, Wartosc = (decimal)k.KM }).ToList();
                listTopKierowcy.ItemsSource = kierowcyItems;

                // Średnie
                if (_summary != null)
                {
                    txtSredniaWaga.Text = $"{_summary.SredniaWagaKurczaka:N3} kg";
                    txtSredniKM.Text = $"{_summary.SredniKMNaKurs:N1} km";
                    txtSredniCzas.Text = $"{_summary.SredniCzasKursu:N2} h";
                    txtSrednieSztuki.Text = $"{_summary.SrednieSztukiNaKurs:N0} szt";
                }

                // Ostrzeżenia
                var ostrzezenia = new List<string>();
                int brakiKM = _allKursy.Count(k => k.BrakKilometrow);
                int brakiGodzin = _allKursy.Count(k => k.BrakGodzin);
                int duzaRoznica = _allKursy.Count(k => k.DuzaRoznicaWag);

                if (brakiKM > 0)
                    ostrzezenia.Add($"• {brakiKM} kursów bez danych o kilometrach");
                if (brakiGodzin > 0)
                    ostrzezenia.Add($"• {brakiGodzin} kursów bez danych o godzinach");
                if (duzaRoznica > 0)
                    ostrzezenia.Add($"• {duzaRoznica} kursów z dużą różnicą wag (>2%)");

                if (ostrzezenia.Any())
                    txtOstrzezenia.Text = string.Join("\n", ostrzezenia);
                else
                    txtOstrzezenia.Text = "Brak ostrzeżeń - wszystkie dane kompletne.";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania statystyk: {ex.Message}");
            }
        }

        private void UpdateSummaryUI()
        {
            if (_summary == null) return;

            txtSumaSztuk.Text = $"{_summary.SumaSztuk:N0} szt";
            txtSumaNetto.Text = $"{_summary.SumaNetto:N0} kg";
            txtSumaUpadkow.Text = $"{_summary.SumaUpadkowKg:N0} kg";
            txtSumaRoznica.Text = $"{_summary.SumaRoznicaKg:N0} kg";
            txtSumaKM.Text = $"{_summary.SumaKM:N0} km";
            txtSumaGodzin.Text = $"{_summary.SumaGodzin:N2} h";
            txtLiczbaKursow.Text = $"{_summary.LiczbaKursow}";
            txtLiczbaZestawow.Text = $"{_summary.LiczbaZestawow}";

            txtFormulaDoZaplaty.Text = $"{_summary.SumaRoznicaKg:N0} kg × {_stawkaZaKg:N3} zł =";
            txtDoZaplaty.Text = $"{_summary.DoZaplaty:N2} zł";
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
            var (od, do_) = AvilogCalculator.GetPreviousWeekRange();
            datePickerOd.SelectedDate = od;
            datePickerDo.SelectedDate = do_;
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

                    // Przelicz dane
                    await LoadDataAsync();
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
                    _dniCollection.Where(d => !d.JestSuma).ToList(),
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

        private void BtnEmail_Click(object sender, RoutedEventArgs e)
        {
            if (_summary == null)
            {
                MessageBox.Show("Brak danych do wysłania.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var dataOd = datePickerOd.SelectedDate.Value;
                var dataDo = datePickerDo.SelectedDate.Value;

                string subject = $"Rozliczenie Avilog {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}";
                string body = GenerateEmailBody();

                string mailto = $"mailto:?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = mailto,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania klienta poczty:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        #region === FILTROWANIE ===

        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
        {
            txtFilter.Text = "";
        }

        private void ApplyFilter()
        {
            string filter = txtFilter.Text?.Trim().ToLower() ?? "";

            _kursyCollection.Clear();

            var filtered = string.IsNullOrEmpty(filter)
                ? _allKursy
                : _allKursy.Where(k =>
                    (k.HodowcaNazwa?.ToLower().Contains(filter) ?? false) ||
                    (k.KierowcaNazwa?.ToLower().Contains(filter) ?? false) ||
                    (k.CarID?.ToLower().Contains(filter) ?? false) ||
                    (k.TrailerID?.ToLower().Contains(filter) ?? false));

            foreach (var kurs in filtered)
            {
                _kursyCollection.Add(kurs);
            }
        }

        #endregion

        #region === DATAGRID EVENTS ===

        private void DataGridDni_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dataGridDni.SelectedItem is AvilogDayModel dzien && !dzien.JestSuma)
            {
                // Przejdź do zakładki szczegółów i filtruj po dniu
                mainTabControl.SelectedIndex = 1;
                txtFilter.Text = dzien.DataFormatowana;
            }
        }

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
            else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && !txtFilter.IsFocused && !txtStawka.IsFocused)
            {
                BtnCopy_Click(null, null);
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
            sb.AppendLine("═══════════════════════════════════════════════════════");
            sb.AppendLine("       ROZLICZENIE AVILOG - TRANSPORT ŻYWCA");
            sb.AppendLine("═══════════════════════════════════════════════════════");
            sb.AppendLine($"Okres: {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}");
            sb.AppendLine("───────────────────────────────────────────────────────");
            sb.AppendLine();
            sb.AppendLine($"Suma sztuk:        {_summary.SumaSztuk:N0} szt");
            sb.AppendLine($"Suma netto:        {_summary.SumaNetto:N0} kg");
            sb.AppendLine($"Suma upadków:      {_summary.SumaUpadkowKg:N0} kg");
            sb.AppendLine($"Różnica kg:        {_summary.SumaRoznicaKg:N0} kg");
            sb.AppendLine();
            sb.AppendLine($"Suma KM:           {_summary.SumaKM:N0} km");
            sb.AppendLine($"Suma godzin:       {_summary.SumaGodzin:N2} h");
            sb.AppendLine($"Liczba kursów:     {_summary.LiczbaKursow}");
            sb.AppendLine($"Liczba zestawów:   {_summary.LiczbaZestawow}");
            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════════");
            sb.AppendLine($"Stawka za kg:      {_stawkaZaKg:N3} zł/kg");
            sb.AppendLine($"DO ZAPŁATY:        {_summary.DoZaplaty:N2} zł");
            sb.AppendLine("═══════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}");

            return sb.ToString();
        }

        private string GenerateEmailBody()
        {
            var dataOd = datePickerOd.SelectedDate.Value;
            var dataDo = datePickerDo.SelectedDate.Value;

            var sb = new StringBuilder();
            sb.AppendLine("Dzień dobry,");
            sb.AppendLine();
            sb.AppendLine($"Przesyłam rozliczenie transportu za okres {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}.");
            sb.AppendLine();
            sb.AppendLine("PODSUMOWANIE:");
            sb.AppendLine($"- Suma sztuk: {_summary.SumaSztuk:N0}");
            sb.AppendLine($"- Suma netto: {_summary.SumaNetto:N0} kg");
            sb.AppendLine($"- Suma upadków: {_summary.SumaUpadkowKg:N0} kg");
            sb.AppendLine($"- Różnica do rozliczenia: {_summary.SumaRoznicaKg:N0} kg");
            sb.AppendLine($"- Liczba kursów: {_summary.LiczbaKursow}");
            sb.AppendLine($"- Suma KM: {_summary.SumaKM:N0}");
            sb.AppendLine();
            sb.AppendLine($"Stawka: {_stawkaZaKg:N3} zł/kg");
            sb.AppendLine($"DO ZAPŁATY: {_summary.DoZaplaty:N2} zł");
            sb.AppendLine();
            sb.AppendLine("Szczegółowe zestawienie w załączniku.");
            sb.AppendLine();
            sb.AppendLine("Z poważaniem,");
            sb.AppendLine("Ubojnia Drobiu Piórkowscy");

            return sb.ToString();
        }

        #endregion
    }
}
