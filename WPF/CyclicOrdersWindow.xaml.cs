using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.WPF
{
    /// <summary>
    /// Dane zamówienia źródłowego — przekazywane z MainWindow do dialogu.
    /// </summary>
    public class SourceOrderInfo
    {
        public int Id { get; set; }
        public int KlientId { get; set; }
        public string Odbiorca { get; set; } = "";
        public DateTime DataZamowienia { get; set; }
        public DateTime DataPrzyjazdu { get; set; }
        public DateTime? DataProdukcji { get; set; }
        public string Produkty { get; set; } = "";
        public bool MaStrefe { get; set; }
        public decimal IloscKg { get; set; }
        public string ConnString { get; set; } = "";
        // Daty, na które klient już ma zamówienia (do wykrywania kolizji)
        public HashSet<DateTime> ExistingOrderDates { get; set; } = new();
    }

    public partial class CyclicOrdersWindow : Window
    {
        private const int MAX_ORDERS = 100;
        private const int CONFIRM_THRESHOLD = 20;

        private readonly SourceOrderInfo _source;
        private readonly int _awizacjaOffsetDays;
        private readonly int? _produkcjaOffsetDays;
        private readonly TimeSpan _godzinaAwizacji;

        // Wynik po zamknięciu dialogu
        public List<DateTime> SelectedDays { get; private set; } = new();
        public Dictionary<DateTime, TimeSpan> GodzinaPerDay { get; private set; } = new();
        public DateTime StartDate { get; private set; }
        public DateTime EndDate { get; private set; }

        // Wartości zapamiętane w momencie kliknięcia OK (kontrolki po Close() mogą być niedostępne)
        public bool CopyNotes { get; private set; }
        public bool CopyKlasyWagowe { get; private set; }
        public bool CopyDataProdukcji { get; private set; }
        public bool CopyStrefa { get; private set; }

        private ToggleButton[] _dayPills;
        private readonly ObservableCollection<PreviewRow> _previewRows = new();
        private bool _suppressUpdate;
        private static readonly CultureInfo _plCulture = new("pl-PL");

        public CyclicOrdersWindow(SourceOrderInfo source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));

            // Oblicz offsety — te reguły są niezmienne dla całego cyklu
            _awizacjaOffsetDays = (_source.DataPrzyjazdu.Date - _source.DataZamowienia.Date).Days;
            _godzinaAwizacji = _source.DataPrzyjazdu.TimeOfDay;
            _produkcjaOffsetDays = _source.DataProdukcji.HasValue
                ? (int?)(_source.DataProdukcji.Value.Date - _source.DataZamowienia.Date).Days
                : null;

            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            SetupSourceInfo();
            SetupOffsetRules();
            SetupDayPills();
            SetupPreviewGrid();

            // Domyślne daty
            _suppressUpdate = true;
            dpStartDate.SelectedDate = DateTime.Today.AddDays(1);
            dpEndDate.SelectedDate = DateTime.Today.AddDays(7);
            _suppressUpdate = false;

            UpdatePreview();
        }

        // Fallback bezparametrowy
        public CyclicOrdersWindow() : this(new SourceOrderInfo
        {
            DataZamowienia = DateTime.Today,
            DataPrzyjazdu = DateTime.Today.AddHours(8),
            Odbiorca = "(brak danych)"
        })
        { }

        #region Setup

        private void SetupSourceInfo()
        {
            lblSourceId.Text = $"#ZAM-{_source.Id}";
            lblSourceOdbiorca.Text = _source.Odbiorca;

            if (string.IsNullOrWhiteSpace(_source.Produkty))
                lblSourceProdukty.Text = $"{_source.IloscKg:N0} kg";
            else
                lblSourceProdukty.Text = _source.Produkty;

            if (_source.MaStrefe)
                chkCopyStrefa.IsChecked = true;
        }

        private void SetupOffsetRules()
        {
            // Awizacja offset
            if (_awizacjaOffsetDays == 0)
                lblOffsetAwizacja.Text = "Ten sam dzień";
            else if (_awizacjaOffsetDays > 0)
                lblOffsetAwizacja.Text = $"+{_awizacjaOffsetDays} {DniSuffix(_awizacjaOffsetDays)}";
            else
                lblOffsetAwizacja.Text = $"{_awizacjaOffsetDays} {DniSuffix(Math.Abs(_awizacjaOffsetDays))}";

            // Godzina
            lblOffsetGodzina.Text = _godzinaAwizacji.ToString(@"hh\:mm");

            // Produkcja offset
            if (!_produkcjaOffsetDays.HasValue)
            {
                lblOffsetProdukcja.Text = "brak";
                lblOffsetProdukcja.Foreground = new SolidColorBrush(Color.FromRgb(0x7F, 0x8C, 0x8D));
            }
            else if (_produkcjaOffsetDays.Value == 0)
                lblOffsetProdukcja.Text = "Ten sam dzień";
            else if (_produkcjaOffsetDays.Value > 0)
                lblOffsetProdukcja.Text = $"+{_produkcjaOffsetDays.Value} {DniSuffix(_produkcjaOffsetDays.Value)}";
            else
                lblOffsetProdukcja.Text = $"{_produkcjaOffsetDays.Value} {DniSuffix(Math.Abs(_produkcjaOffsetDays.Value))}";
        }

        private static string DniSuffix(int n) => n == 1 ? "dzień" : "dni";

        private void SetupDayPills()
        {
            _dayPills = new[] { pillMon, pillTue, pillWed, pillThu, pillFri, pillSat, pillSun };

            // Domyślnie Pn-Pt zaznaczone
            for (int i = 0; i < 5; i++) _dayPills[i].IsChecked = true;
            for (int i = 5; i < 7; i++) _dayPills[i].IsChecked = false;

            UpdateDayPillsState();
        }

        private void SetupPreviewGrid()
        {
            dgPreview.ItemsSource = _previewRows;

            // Kolumny budowane programowo — łatwiejsze zarządzanie stylami
            dgPreview.Columns.Clear();

            // Checkbox — włącz/wyłącz konkretny dzień
            var chkCol = new DataGridCheckBoxColumn
            {
                Header = "",
                Binding = new Binding("IsSelected") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                Width = new DataGridLength(32),
                ElementStyle = new Style(typeof(CheckBox))
                {
                    Setters = { new Setter(HorizontalAlignmentProperty, HorizontalAlignment.Center) }
                }
            };
            dgPreview.Columns.Add(chkCol);

            AddTextColumn("Lp", "Lp", 30);
            AddTextColumn("Data zamówienia", "DataZam", 110);
            AddTextColumn("Dzień", "DzienTygodnia", 36);
            AddTextColumn("Awizacja", "Awizacja", 95);

            // Godzina — edytowalna kolumna
            var godzinaCol = new DataGridTextColumn
            {
                Header = "Godz.",
                Binding = new Binding("Godzina") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                Width = new DataGridLength(52),
                IsReadOnly = false
            };
            dgPreview.Columns.Add(godzinaCol);

            var prodCol = AddTextColumn("Data produkcji", "DataProdukcji", 110);
            prodCol.Visibility = (_produkcjaOffsetDays.HasValue && chkCopyDataProdukcji.IsChecked == true)
                ? Visibility.Visible : Visibility.Collapsed;

            AddTextColumn("", "Info", 0, true);
        }

        private DataGridTextColumn AddTextColumn(string header, string binding, int width, bool star = false)
        {
            var col = new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(binding),
                Width = star ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(width),
                IsReadOnly = true
            };
            dgPreview.Columns.Add(col);
            return col;
        }

        #endregion

        #region Event handlers

        private void DateRange_Changed(object sender, RoutedEventArgs e)
        {
            if (!_suppressUpdate) UpdatePreview();
        }

        private void RbFrequency_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            UpdateDayPillsState();
            UpdatePreview();
        }

        private void DayPill_Changed(object sender, RoutedEventArgs e)
        {
            if (!_suppressUpdate) UpdatePreview();
        }

        private void CopyOption_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;

            // Pokaż/ukryj kolumnę DataProdukcji
            if (dgPreview.Columns.Count > 6)
            {
                dgPreview.Columns[6].Visibility =
                    (_produkcjaOffsetDays.HasValue && chkCopyDataProdukcji.IsChecked == true)
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            UpdatePreview();
        }

        // Presety zakresów
        private void Preset1Week_Click(object sender, RoutedEventArgs e) => SetPresetRange(7);
        private void Preset2Weeks_Click(object sender, RoutedEventArgs e) => SetPresetRange(14);
        private void Preset1Month_Click(object sender, RoutedEventArgs e) => SetPresetRange(30);

        private void PresetEndOfMonth_Click(object sender, RoutedEventArgs e)
        {
            _suppressUpdate = true;
            var start = DateTime.Today.AddDays(1);
            dpStartDate.SelectedDate = start;
            dpEndDate.SelectedDate = new DateTime(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month));
            _suppressUpdate = false;
            UpdatePreview();
        }

        private void SetPresetRange(int days)
        {
            _suppressUpdate = true;
            dpStartDate.SelectedDate = DateTime.Today.AddDays(1);
            dpEndDate.SelectedDate = DateTime.Today.AddDays(days);
            _suppressUpdate = false;
            UpdatePreview();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in _previewRows) row.IsSelected = true;
            UpdateSummary();
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in _previewRows) row.IsSelected = false;
            UpdateSummary();
        }

        #endregion

        #region Logic

        private void UpdateDayPillsState()
        {
            if (_dayPills == null) return;

            bool customDays = rbSelectedDays.IsChecked == true;

            foreach (var pill in _dayPills)
                pill.IsEnabled = customDays;

            if (rbDaily.IsChecked == true)
            {
                _suppressUpdate = true;
                for (int i = 0; i < 5; i++) _dayPills[i].IsChecked = true;
                for (int i = 5; i < 7; i++) _dayPills[i].IsChecked = false;
                _suppressUpdate = false;
            }
            else if (rbWeekly.IsChecked == true)
            {
                // Przy "co tydzień" pills są disabled ale pokazujemy dzień startu
                foreach (var pill in _dayPills)
                    pill.IsEnabled = false;
            }
        }

        private void UpdatePreview()
        {
            CalculateSelectedDays();
            BuildPreviewRows();
            UpdateSummary();
        }

        private void CalculateSelectedDays()
        {
            SelectedDays.Clear();

            if (!dpStartDate.SelectedDate.HasValue || !dpEndDate.SelectedDate.HasValue)
                return;

            StartDate = dpStartDate.SelectedDate.Value.Date;
            EndDate = dpEndDate.SelectedDate.Value.Date;
            if (EndDate < StartDate) return;

            var current = StartDate;
            while (current <= EndDate)
            {
                int dayIndex = ((int)current.DayOfWeek + 6) % 7; // 0=Pn .. 6=Nd

                if (rbDaily.IsChecked == true)
                {
                    if (dayIndex < 5) SelectedDays.Add(current);
                }
                else if (rbSelectedDays.IsChecked == true)
                {
                    if (_dayPills[dayIndex].IsChecked == true) SelectedDays.Add(current);
                }
                else if (rbWeekly.IsChecked == true)
                {
                    if ((current - StartDate).Days % 7 == 0) SelectedDays.Add(current);
                }

                current = current.AddDays(1);
            }
        }

        private void BuildPreviewRows()
        {
            // Zapamiętaj stan zaznaczenia (po dacie)
            var previousSelection = _previewRows.ToDictionary(r => r.TargetDate, r => r.IsSelected);

            _previewRows.Clear();

            bool showProd = chkCopyDataProdukcji?.IsChecked == true && _produkcjaOffsetDays.HasValue;

            int lp = 0;
            foreach (var day in SelectedDays)
            {
                lp++;
                var awizacjaDate = day.AddDays(_awizacjaOffsetDays);
                var prodDate = showProd ? (DateTime?)day.AddDays(_produkcjaOffsetDays!.Value) : null;
                bool isWeekend = day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday;
                bool hasCollision = _source.ExistingOrderDates.Contains(day.Date);

                // Zachowaj poprzedni stan zaznaczenia
                bool selected = previousSelection.TryGetValue(day, out var prev) ? prev : !hasCollision;

                string info = "";
                if (hasCollision) info = "JUZ ISTNIEJE!";
                else if (isWeekend) info = "weekend";

                var row = new PreviewRow
                {
                    Lp = lp,
                    TargetDate = day,
                    DataZam = day.ToString("yyyy-MM-dd (ddd)", _plCulture),
                    DzienTygodnia = GetShortDay(day.DayOfWeek),
                    Awizacja = awizacjaDate.ToString("yyyy-MM-dd", _plCulture),
                    Godzina = _godzinaAwizacji.ToString(@"hh\:mm"),
                    GodzinaTime = _godzinaAwizacji,
                    DataProdukcji = prodDate?.ToString("yyyy-MM-dd", _plCulture) ?? "—",
                    Info = info,
                    IsWeekend = isWeekend,
                    HasCollision = hasCollision,
                    IsSelected = selected
                };
                row.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(PreviewRow.IsSelected)) UpdateSummary();
                };
                _previewRows.Add(row);
            }
        }

        private void UpdateSummary()
        {
            int total = _previewRows.Count;
            int selected = _previewRows.Count(r => r.IsSelected);

            if (total == 0)
            {
                lblPreviewCount.Text = "0";
                lblSummary.Text = "Brak zamówień do utworzenia";
                lblWarning.Text = "";
                lblTotalInfo.Text = "";
                btnOk.IsEnabled = false;
                return;
            }

            lblPreviewCount.Text = selected.ToString();

            // Footer summary
            var first = _previewRows.Where(r => r.IsSelected).FirstOrDefault();
            var last = _previewRows.Where(r => r.IsSelected).LastOrDefault();
            if (first != null && last != null)
            {
                int weeks = (int)Math.Ceiling((last.TargetDate - first.TargetDate).TotalDays / 7.0);
                lblSummary.Text = $"{first.TargetDate:yyyy-MM-dd} — {last.TargetDate:yyyy-MM-dd}  ({weeks} tyg.)";
            }
            else
            {
                lblSummary.Text = "";
            }

            // Warning
            if (selected > MAX_ORDERS)
            {
                lblWarning.Text = $"Max {MAX_ORDERS}!";
                btnOk.IsEnabled = false;
            }
            else if (selected == 0)
            {
                lblWarning.Text = "";
                btnOk.IsEnabled = false;
            }
            else
            {
                lblWarning.Text = selected > CONFIRM_THRESHOLD ? $"Dużo — potwierdzenie" : "";
                btnOk.IsEnabled = true;
            }

            // Total info
            decimal totalKg = selected * _source.IloscKg;
            lblTotalInfo.Text = totalKg > 0
                ? $"{selected} zamówień = {totalKg:N0} kg"
                : $"{selected} zamówień";
        }

        #endregion

        #region OK / Cancel

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            // Zbierz tylko zaznaczone daty
            var selectedDates = _previewRows.Where(r => r.IsSelected).Select(r => r.TargetDate).ToList();

            if (!selectedDates.Any())
            {
                MessageBox.Show("Brak zaznaczonych dni.", "Brak wyboru",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (selectedDates.Count > MAX_ORDERS)
            {
                MessageBox.Show($"Limit to {MAX_ORDERS} zamówień ({selectedDates.Count} zaznaczono).\nOdznacz część dat.",
                    "Zbyt wiele", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Potwierdzenie dla dużej ilości
            if (selectedDates.Count > CONFIRM_THRESHOLD)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Utworzysz {selectedDates.Count} zamówień dla:");
                sb.AppendLine($"   {_source.Odbiorca}");
                sb.AppendLine();
                sb.AppendLine($"Zakres: {selectedDates.First():yyyy-MM-dd} — {selectedDates.Last():yyyy-MM-dd}");
                sb.AppendLine();
                sb.AppendLine("Kopiowane:");
                sb.AppendLine($"   Produkty i ceny: TAK");
                if (CopyNotes) sb.AppendLine($"   Notatki: TAK");
                if (CopyKlasyWagowe) sb.AppendLine($"   Klasy wagowe: TAK");
                if (CopyDataProdukcji) sb.AppendLine($"   Data produkcji: TAK (offset {FormatOffset(_produkcjaOffsetDays)})");
                if (CopyStrefa) sb.AppendLine($"   Strefa: TAK");
                sb.AppendLine();
                sb.AppendLine("Kontynuować?");

                if (MessageBox.Show(sb.ToString(), "Potwierdzenie",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
            }

            // Zapamiętaj wartości PRZED zamknięciem — po Close() kontrolki są niedostępne
            CopyNotes = chkCopyNotes.IsChecked == true;
            CopyKlasyWagowe = chkCopyKlasyWagowe.IsChecked == true;
            CopyDataProdukcji = chkCopyDataProdukcji.IsChecked == true;
            CopyStrefa = chkCopyStrefa.IsChecked == true;

            SelectedDays = selectedDates;
            StartDate = selectedDates.First();
            EndDate = selectedDates.Last();

            // Zbierz godziny per dzień (parsuj z edytowalnego tekstu)
            GodzinaPerDay.Clear();
            foreach (var row in _previewRows.Where(r => r.IsSelected))
            {
                if (TimeSpan.TryParse(row.Godzina, out var ts))
                    GodzinaPerDay[row.TargetDate] = ts;
                else
                    GodzinaPerDay[row.TargetDate] = row.GodzinaTime;
            }

            DialogResult = true;
            Close();
        }

        private static string FormatOffset(int? offset) => offset switch
        {
            null => "brak",
            0 => "0 dni",
            > 0 => $"+{offset} dni",
            _ => $"{offset} dni"
        };

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion

        #region Templates

        private static bool _templateTableChecked;

        private async void EnsureTemplateTable(SqlConnection cn)
        {
            if (_templateTableChecked) return;
            try
            {
                var cmd = new SqlCommand(@"
                    IF OBJECT_ID('dbo.CykliczneSzablony','U') IS NULL
                    CREATE TABLE dbo.CykliczneSzablony (
                        Id INT IDENTITY PRIMARY KEY,
                        KlientId INT NOT NULL,
                        Nazwa NVARCHAR(100),
                        Tryb NVARCHAR(20),
                        DniTygodnia NVARCHAR(20),
                        CopyNotes BIT DEFAULT 0,
                        CopyKlasy BIT DEFAULT 1,
                        CopyProd BIT DEFAULT 1,
                        CopyStrefa BIT DEFAULT 1,
                        Godzina NVARCHAR(5),
                        DataUtworzenia DATETIME DEFAULT GETDATE()
                    )", cn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch { }
            _templateTableChecked = true;
        }

        private async void SaveTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_source.ConnString) || _source.KlientId <= 0) return;

            string tryb = rbDaily.IsChecked == true ? "daily" : rbSelectedDays.IsChecked == true ? "selected" : "weekly";
            string dni = string.Join(",", Enumerable.Range(0, 7).Where(i => _dayPills[i].IsChecked == true));
            string godz = _godzinaAwizacji.ToString(@"hh\:mm");

            try
            {
                await using var cn = new SqlConnection(_source.ConnString);
                await cn.OpenAsync();
                EnsureTemplateTable(cn);

                // Upsert — jeden szablon per klient
                var cmd = new SqlCommand(@"
                    IF EXISTS (SELECT 1 FROM CykliczneSzablony WHERE KlientId = @kid)
                        UPDATE CykliczneSzablony SET Tryb=@tryb, DniTygodnia=@dni, CopyNotes=@cn, CopyKlasy=@ck,
                            CopyProd=@cp, CopyStrefa=@cs, Godzina=@godz, DataUtworzenia=GETDATE()
                        WHERE KlientId = @kid
                    ELSE
                        INSERT INTO CykliczneSzablony (KlientId, Nazwa, Tryb, DniTygodnia, CopyNotes, CopyKlasy, CopyProd, CopyStrefa, Godzina)
                        VALUES (@kid, @nazwa, @tryb, @dni, @cn, @ck, @cp, @cs, @godz)", cn);
                cmd.Parameters.AddWithValue("@kid", _source.KlientId);
                cmd.Parameters.AddWithValue("@nazwa", _source.Odbiorca);
                cmd.Parameters.AddWithValue("@tryb", tryb);
                cmd.Parameters.AddWithValue("@dni", dni);
                cmd.Parameters.AddWithValue("@cn", chkCopyNotes.IsChecked == true);
                cmd.Parameters.AddWithValue("@ck", chkCopyKlasyWagowe.IsChecked == true);
                cmd.Parameters.AddWithValue("@cp", chkCopyDataProdukcji.IsChecked == true);
                cmd.Parameters.AddWithValue("@cs", chkCopyStrefa.IsChecked == true);
                cmd.Parameters.AddWithValue("@godz", godz);
                await cmd.ExecuteNonQueryAsync();

                MessageBox.Show($"Szablon zapisany dla {_source.Odbiorca}.", "Zapisano", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_source.ConnString) || _source.KlientId <= 0) return;

            try
            {
                await using var cn = new SqlConnection(_source.ConnString);
                await cn.OpenAsync();
                EnsureTemplateTable(cn);

                var cmd = new SqlCommand(
                    "SELECT Tryb, DniTygodnia, CopyNotes, CopyKlasy, CopyProd, CopyStrefa, Godzina FROM CykliczneSzablony WHERE KlientId = @kid", cn);
                cmd.Parameters.AddWithValue("@kid", _source.KlientId);
                using var rdr = await cmd.ExecuteReaderAsync();
                if (!await rdr.ReadAsync())
                {
                    MessageBox.Show("Brak zapisanego szablonu dla tego klienta.", "Brak szablonu", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string tryb = rdr.GetString(0);
                string dni = rdr.IsDBNull(1) ? "" : rdr.GetString(1);
                bool cn2 = !rdr.IsDBNull(2) && rdr.GetBoolean(2);
                bool ck = rdr.IsDBNull(3) || rdr.GetBoolean(3);
                bool cp = rdr.IsDBNull(4) || rdr.GetBoolean(4);
                bool cs = rdr.IsDBNull(5) || rdr.GetBoolean(5);

                _suppressUpdate = true;

                // Ustaw tryb
                rbDaily.IsChecked = tryb == "daily";
                rbSelectedDays.IsChecked = tryb == "selected";
                rbWeekly.IsChecked = tryb == "weekly";

                // Ustaw dni
                if (tryb == "selected" && !string.IsNullOrEmpty(dni))
                {
                    var dniSet = new HashSet<int>(dni.Split(',').Where(s => int.TryParse(s, out _)).Select(int.Parse));
                    for (int i = 0; i < 7; i++)
                        _dayPills[i].IsChecked = dniSet.Contains(i);
                }

                chkCopyNotes.IsChecked = cn2;
                chkCopyKlasyWagowe.IsChecked = ck;
                chkCopyDataProdukcji.IsChecked = cp;
                chkCopyStrefa.IsChecked = cs;

                _suppressUpdate = false;
                UpdateDayPillsState();
                UpdatePreview();

                MessageBox.Show("Szablon załadowany.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Helpers

        private static string GetShortDay(DayOfWeek dow) => dow switch
        {
            DayOfWeek.Monday => "Pn",
            DayOfWeek.Tuesday => "Wt",
            DayOfWeek.Wednesday => "Śr",
            DayOfWeek.Thursday => "Cz",
            DayOfWeek.Friday => "Pt",
            DayOfWeek.Saturday => "So",
            DayOfWeek.Sunday => "Nd",
            _ => "?"
        };

        #endregion
    }

    /// <summary>
    /// Wiersz podglądu z obsługą INotifyPropertyChanged (dla checkboxa).
    /// </summary>
    public class PreviewRow : INotifyPropertyChanged
    {
        private bool _isSelected = true;

        public int Lp { get; set; }
        public DateTime TargetDate { get; set; }
        public string DataZam { get; set; } = "";
        public string DzienTygodnia { get; set; } = "";
        public string Awizacja { get; set; } = "";
        private string _godzina = "";
        public string Godzina
        {
            get => _godzina;
            set
            {
                if (_godzina == value) return;
                _godzina = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Godzina)));
            }
        }
        public TimeSpan GodzinaTime { get; set; }
        public string DataProdukcji { get; set; } = "";
        public string Info { get; set; } = "";
        public bool IsWeekend { get; set; }
        public bool HasCollision { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
