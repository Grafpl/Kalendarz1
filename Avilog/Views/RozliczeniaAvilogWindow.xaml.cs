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
        private ObservableCollection<AvilogDayModel> _dniCollection;
        private List<AvilogKursModel> _allKursy;
        private List<AvilogDayModel> _allDni;
        private AvilogSummaryModel _summary;
        private decimal _stawkaZaKg = 0.119m;

        // Słownik przechowujący kursy pogrupowane po dniach
        private Dictionary<DateTime, List<AvilogKursModel>> _kursyWgDni;

        public RozliczeniaAvilogWindow()
        {
            InitializeComponent();

            // Ustaw ikonę okna
            try { WindowIconHelper.SetIcon(this); } catch { }

            _dataService = new AvilogDataService();
            _kursyCollection = new ObservableCollection<AvilogKursModel>();
            _dniCollection = new ObservableCollection<AvilogDayModel>();
            _allKursy = new List<AvilogKursModel>();
            _allDni = new List<AvilogDayModel>();
            _kursyWgDni = new Dictionary<DateTime, List<AvilogKursModel>>();

            // Ustaw domyślny zakres dat (ten tydzień)
            var (od, do_) = AvilogCalculator.GetCurrentWeekRange();
            datePickerOd.SelectedDate = od;
            datePickerDo.SelectedDate = do_;

            // Bindowanie kolekcji
            dataGridKursy.ItemsSource = _kursyCollection;
            dataGridPodsumowanie.ItemsSource = _dniCollection;

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

                // Pobierz dane z bazy - kursy i podsumowania dzienne
                var kursy = await _dataService.GetKursyAsync(dataOd, dataDo);
                var dni = await _dataService.GetPodsumowaniaDzienneAsync(dataOd, dataDo);

                // Ustaw stawkę dla każdego dnia
                foreach (var dzien in dni)
                {
                    dzien.StawkaZaKg = _stawkaZaKg;
                }

                // Zapisz wszystkie dane
                _allKursy = kursy;
                _allDni = dni;

                // Grupuj kursy według dni
                _kursyWgDni = kursy
                    .GroupBy(k => k.CalcDate.Date)
                    .OrderBy(g => g.Key)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Aktualizuj kolekcję podsumowań dziennych + wiersz SUMA
                _dniCollection.Clear();
                foreach (var dzien in dni)
                {
                    _dniCollection.Add(dzien);
                }

                // Dodaj wiersz SUMA na końcu
                if (dni.Any())
                {
                    var suma = new AvilogDayModel
                    {
                        LP = 0,
                        Data = dataDo,
                        DzienTygodnia = "SUMA",
                        LiczbaKursow = dni.Sum(d => d.LiczbaKursow),
                        LiczbaZestawow = dni.Sum(d => d.LiczbaZestawow),
                        SumaSztuk = dni.Sum(d => d.SumaSztuk),
                        SumaBrutto = dni.Sum(d => d.SumaBrutto),
                        SumaTara = dni.Sum(d => d.SumaTara),
                        SumaNetto = dni.Sum(d => d.SumaNetto),
                        SumaUpadkowSzt = dni.Sum(d => d.SumaUpadkowSzt),
                        SumaUpadkowKg = dni.Sum(d => d.SumaUpadkowKg),
                        SumaKM = dni.Sum(d => d.SumaKM),
                        SumaGodzin = dni.Sum(d => d.SumaGodzin),
                        StawkaZaKg = _stawkaZaKg,
                        JestSuma = true
                    };
                    _dniCollection.Add(suma);
                }

                // Oblicz podsumowanie
                _summary = CalculateSummaryFromKursy(kursy);
                UpdateSummaryUI();

                // Aktualizuj nagłówek
                txtOkres.Text = $"Okres: {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}";
                txtPodsumowanieOkres.Text = $"Okres: {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}";

                // Twórz zakładki dla dni
                CreateDayTabs();

                ShowLoading(false, $"Załadowano {kursy.Count} kursów");
            }
            catch (Exception ex)
            {
                ShowLoading(false, "Błąd ładowania");
                MessageBox.Show($"Błąd podczas ładowania danych:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateDayTabs()
        {
            tabControlDni.SelectionChanged -= TabControlDni_SelectionChanged;
            tabControlDni.Items.Clear();

            // Pobierz styl zakładki z zasobów
            var tabItemStyle = (Style)FindResource("SimpleTabItem");

            int dayNumber = 1;
            foreach (var dzienKvp in _kursyWgDni.OrderBy(kvp => kvp.Key))
            {
                var tabItem = new TabItem
                {
                    Header = $"{dayNumber} dzień",
                    Tag = dzienKvp.Key,
                    Style = tabItemStyle
                };
                tabControlDni.Items.Add(tabItem);
                dayNumber++;
            }

            // Dodaj zakładkę "Podsumowanie" na końcu
            var podsumowanieTab = new TabItem
            {
                Header = "Podsumowanie",
                Tag = "PODSUMOWANIE",
                Style = tabItemStyle
            };
            tabControlDni.Items.Add(podsumowanieTab);

            tabControlDni.SelectionChanged += TabControlDni_SelectionChanged;

            // Wybierz pierwszą zakładkę
            if (tabControlDni.Items.Count > 0)
            {
                tabControlDni.SelectedIndex = 0;
                UpdateTabContent();
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

            // Footer - statystyki
            txtSumaSztuk.Text = $"{_summary.SumaSztuk:N0}";
            txtSumaNetto.Text = $"{_summary.SumaNetto:N0}";
            txtSumaUpadkow.Text = $"{_summary.SumaUpadkowKg:N0}";
            txtSumaRoznica.Text = $"{_summary.SumaRoznicaKg:N0}";
            txtLiczbaKursow.Text = $"{_summary.LiczbaKursow}";
            txtSumaKM.Text = $"{_summary.SumaKM:N0}";
            txtSumaGodzin.Text = _summary.SumaGodzinFormatowana;

            // Footer - DO ZAPŁATY
            txtDoZaplaty.Text = $"{_summary.DoZaplaty:N2} zł";
            txtFormula.Text = $"{_summary.SumaRoznicaKg:N0} × {_stawkaZaKg:N3}";

            // Zakładka Podsumowanie - kalkulacja
            txtKalkNetto.Text = $"{_summary.SumaNetto:N0} kg";
            txtKalkUpadki.Text = $"- {_summary.SumaUpadkowKg:N0} kg";
            txtKalkRoznica.Text = $"= {_summary.SumaRoznicaKg:N0} kg";
            txtKalkDoZaplaty.Text = $"{_summary.DoZaplaty:N2} zł";
            txtKalkFormula.Text = $"{_summary.SumaRoznicaKg:N0} kg × {_stawkaZaKg:N3} zł";
        }

        private void ShowLoading(bool isLoading, string message = "")
        {
            progressBar.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            txtStatus.Text = message;
        }

        #endregion

        #region === OBSŁUGA ZAKŁADEK ===

        private void TabControlDni_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (tabControlDni == null || tabControlDni.SelectedItem == null) return;

            UpdateTabContent();
        }

        private void UpdateTabContent()
        {
            if (tabControlDni.SelectedItem is TabItem selectedTab)
            {
                if (selectedTab.Tag is string tagStr && tagStr == "PODSUMOWANIE")
                {
                    // Zakładka Podsumowanie
                    gridKursyDnia.Visibility = Visibility.Collapsed;
                    scrollPodsumowanie.Visibility = Visibility.Visible;
                }
                else if (selectedTab.Tag is DateTime selectedDate)
                {
                    // Zakładka dnia
                    gridKursyDnia.Visibility = Visibility.Visible;
                    scrollPodsumowanie.Visibility = Visibility.Collapsed;

                    // Pokaż kursy dla wybranego dnia
                    _kursyCollection.Clear();
                    if (_kursyWgDni.TryGetValue(selectedDate, out var kursyDnia))
                    {
                        int lp = 1;
                        foreach (var kurs in kursyDnia)
                        {
                            kurs.LP = lp++;
                            _kursyCollection.Add(kurs);
                        }
                    }
                }
            }
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

                    // Zaktualizuj stawki w dniach
                    foreach (var dzien in _dniCollection)
                    {
                        dzien.StawkaZaKg = _stawkaZaKg;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd zapisu stawki:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnZarzadzajStawkami_Click(object sender, RoutedEventArgs e)
        {
            // Pokaż dialog zarządzania stawkami
            try
            {
                var historia = await _dataService.GetHistoriaStawekAsync();

                var sb = new StringBuilder();
                sb.AppendLine("HISTORIA STAWEK ZA KG");
                sb.AppendLine("═════════════════════════════════════════");

                if (!historia.Any())
                {
                    sb.AppendLine("Brak zapisanych stawek w historii.");
                }
                else
                {
                    foreach (var s in historia)
                    {
                        var status = s.JestAktywna ? "[AKTYWNA]" : "";
                        sb.AppendLine($"{s.StawkaZaKg:N4} zł/kg  {status}");
                        sb.AppendLine($"Okres: {s.DataOd:dd.MM.yyyy} - {(s.DataDo.HasValue ? s.DataDo.Value.ToString("dd.MM.yyyy") : "obecnie")}");
                        sb.AppendLine($"Zmienione: {s.ZmienionePrzez} ({s.DataZmiany:dd.MM.yyyy HH:mm})");
                        if (!string.IsNullOrEmpty(s.Uwagi))
                            sb.AppendLine($"Uwagi: {s.Uwagi}");
                        sb.AppendLine("─────────────────────────────────────────");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("Aby zmienić stawkę, wpisz nową wartość w polu STAWKA i kliknij Zapisz.");

                MessageBox.Show(sb.ToString(), "Zarządzanie stawkami", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    _allDni,
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
            else if (e.Key == Key.Left && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                // Poprzednia zakładka
                if (tabControlDni.SelectedIndex > 0)
                    tabControlDni.SelectedIndex--;
                e.Handled = true;
            }
            else if (e.Key == Key.Right && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                // Następna zakładka
                if (tabControlDni.SelectedIndex < tabControlDni.Items.Count - 1)
                    tabControlDni.SelectedIndex++;
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
            sb.AppendLine("DZIENNE ZESTAWIENIE UBOJU - AVILOG");
            sb.AppendLine("═══════════════════════════════════════════════════");
            sb.AppendLine($"Okres: {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}");
            sb.AppendLine("═══════════════════════════════════════════════════");
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
            sb.AppendLine($"  Suma czasu:          {_summary.SumaGodzinFormatowana}");
            sb.AppendLine($"  Liczba kursów:       {_summary.LiczbaKursow}");
            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════");
            sb.AppendLine($"  Stawka:              {_stawkaZaKg:N3} zł/kg");
            sb.AppendLine($"  DO ZAPŁATY:          {_summary.DoZaplaty:N2} zł");
            sb.AppendLine("═══════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"  Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}");

            return sb.ToString();
        }

        #endregion
    }
}
