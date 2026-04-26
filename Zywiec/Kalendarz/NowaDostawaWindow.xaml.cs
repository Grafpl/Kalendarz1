using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.Zywiec.Kalendarz.Services;

namespace Kalendarz1.Zywiec.Kalendarz
{
    /// <summary>
    /// Okno tworzenia nowej dostawy - rozbudowana wersja:
    /// - bidirectional kalkulator (wpisz 2 z 3 pól)
    /// - status jako kolorowe toggle buttons
    /// - mini-historia 5 ostatnich dostaw hodowcy
    /// - konflikt-detection (hodowca już ma dostawę dnia)
    /// - anomaly badge dla wagi/ceny
    /// - "Zapisz i kolejna" dla seryjnego wprowadzania
    /// - Enter-to-advance między polami
    /// </summary>
    public partial class NowaDostawaWindow : Window
    {
        private const int SZUFLAD_PER_AUTO = 264;

        private readonly string _connectionString;
        private readonly string _userId;
        private readonly string _userName;
        private readonly AuditLogService _auditService;

        // Statystyki hodowcy (avg waga, avg cena) dla anomaly detection
        private decimal? _avgWaga;
        private decimal? _avgCena;

        // Tablica buttonów statusu
        private readonly List<ToggleButton> _statusButtons = new List<ToggleButton>();
        private string _selectedStatus = "Potwierdzony";

        // Anti-loop guard dla bidirectional calculator
        private bool _calcUpdating = false;

        /// <summary>Wynik: true gdy utworzono dostawę.</summary>
        public bool DeliveryCreated { get; private set; } = false;

        /// <summary>LP utworzonej dostawy (po zapisie).</summary>
        public int? CreatedLP { get; private set; }

        /// <summary>true jeśli użytkownik kliknął "Zapisz i kolejna" - parent ma otworzyć nowe okno.</summary>
        public bool OpenAnother { get; private set; } = false;

        public NowaDostawaWindow(DateTime data, string connectionString, string userId, string userName, AuditLogService auditService = null)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            _connectionString = connectionString;
            _userId = userId;
            _userName = userName;
            _auditService = auditService;

            dpData.SelectedDate = data;
            txtHeaderDate.Text = data.ToString("dddd, d MMMM yyyy", new CultureInfo("pl-PL"));

            SetupComboBoxes();
            BuildStatusButtons();

            _ = LoadHodowcyAsync();

            Loaded += (s, e) => cmbDostawca.Focus();
        }

        #region Setup

        private void SetupComboBoxes()
        {
            cmbTypCeny.Items.Add("wolnyrynek");
            cmbTypCeny.Items.Add("rolnicza");
            cmbTypCeny.Items.Add("łączona");
            cmbTypCeny.Items.Add("ministerialna");

            cmbTypUmowy.Items.Add("Wolnyrynek");
            cmbTypUmowy.Items.Add("Kontrakt");
            cmbTypUmowy.Items.Add("W.Wolnyrynek");
        }

        private void BuildStatusButtons()
        {
            var statuses = new[]
            {
                ("Potwierdzony", "✓", "#10B981", "#D1FAE5"),
                ("Do wykupienia", "💵", "#F59E0B", "#FEF3C7"),
                ("Anulowany", "✕", "#EF4444", "#FEE2E2"),
                ("Sprzedany", "📦", "#3B82F6", "#DBEAFE"),
                ("B.Wolny.", "🟡", "#EAB308", "#FEF9C3"),
                ("B.Kontr.", "🟣", "#A855F7", "#F3E8FF")
            };

            foreach (var (name, icon, activeColor, hoverColor) in statuses)
            {
                var btn = new ToggleButton
                {
                    Style = (Style)FindResource("StatusToggleStyle"),
                    Margin = new Thickness(2),
                    Tag = name,
                    ToolTip = name
                };

                var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                sp.Children.Add(new TextBlock { Text = icon, FontSize = 12, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });
                sp.Children.Add(new TextBlock { Text = name, FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
                btn.Content = sp;

                // Color when active
                btn.Tag = new StatusButtonInfo { Name = name, ActiveColor = activeColor, HoverColor = hoverColor };

                btn.Checked += StatusButton_Checked;

                statusButtonsPanel.Children.Add(btn);
                _statusButtons.Add(btn);
            }

            // Domyślnie zaznacz Potwierdzony
            _statusButtons[0].IsChecked = true;
        }

        private void StatusButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton clicked)
            {
                // Odznacz inne (radio-like behavior)
                foreach (var btn in _statusButtons)
                {
                    if (btn != clicked && btn.IsChecked == true)
                        btn.IsChecked = false;
                }

                // Ustaw kolor aktywnego
                foreach (var btn in _statusButtons)
                {
                    var info = btn.Tag as StatusButtonInfo;
                    if (info == null) continue;
                    if (btn.IsChecked == true)
                    {
                        btn.Background = (SolidColorBrush)new BrushConverter().ConvertFrom(info.ActiveColor);
                        btn.Foreground = Brushes.White;
                        _selectedStatus = info.Name;
                    }
                    else
                    {
                        btn.Background = new SolidColorBrush(Color.FromRgb(243, 244, 246));
                        btn.Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128));
                    }
                }
            }
        }

        private async Task LoadHodowcyAsync()
        {
            try
            {
                var list = await HodowcyCacheManager.GetAsync(_connectionString);
                Dispatcher.Invoke(() =>
                {
                    cmbDostawca.Items.Clear();
                    foreach (var h in list)
                        cmbDostawca.Items.Add(h);
                });
            }
            catch { }
        }

        #endregion

        #region Hodowca - statystyki w tle (anomaly) + konflikt

        private async void CmbDostawca_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string dostawca = cmbDostawca.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(dostawca))
            {
                borderConflict.Visibility = Visibility.Collapsed;
                _avgWaga = null;
                _avgCena = null;
                UpdateAnomalyBadges();
                return;
            }

            // W tle: pobranie średnich do anomaly + sprawdzenie konfliktu
            await Task.WhenAll(
                LoadAvgStatsAsync(dostawca),
                CheckConflictAsync(dostawca, dpData.SelectedDate));
        }

        // Pobiera średnią wagę i cenę z 5 ostatnich dostaw hodowcy (dla anomaly badges)
        private async Task LoadAvgStatsAsync(string dostawca)
        {
            try
            {
                decimal sumWaga = 0, sumCena = 0;
                int countWaga = 0, countCena = 0;

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string sql = @"SELECT TOP 5 WagaDek, Cena
                                   FROM HarmonogramDostaw
                                   WHERE Dostawca = @d AND DataOdbioru < GETDATE()
                                   ORDER BY DataOdbioru DESC";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@d", dostawca);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                if (reader["WagaDek"] != DBNull.Value)
                                {
                                    decimal w = Convert.ToDecimal(reader["WagaDek"]);
                                    if (w > 0) { sumWaga += w; countWaga++; }
                                }
                                if (reader["Cena"] != DBNull.Value)
                                {
                                    decimal c = Convert.ToDecimal(reader["Cena"]);
                                    if (c > 0) { sumCena += c; countCena++; }
                                }
                            }
                        }
                    }
                }

                _avgWaga = countWaga > 0 ? sumWaga / countWaga : (decimal?)null;
                _avgCena = countCena > 0 ? sumCena / countCena : (decimal?)null;

                Dispatcher.Invoke(() => UpdateAnomalyBadges());
            }
            catch { }
        }

        private async Task CheckConflictAsync(string dostawca, DateTime? data)
        {
            if (string.IsNullOrEmpty(dostawca) || data == null)
            {
                Dispatcher.Invoke(() => borderConflict.Visibility = Visibility.Collapsed);
                return;
            }

            try
            {
                int conflictCount = 0;
                int existingLp = 0;
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string sql = @"SELECT TOP 1 Lp, COUNT(*) OVER() AS Cnt
                                   FROM HarmonogramDostaw
                                   WHERE Dostawca = @d AND CAST(DataOdbioru AS DATE) = @dt";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@d", dostawca);
                        cmd.Parameters.AddWithValue("@dt", data.Value.Date);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                existingLp = Convert.ToInt32(reader["Lp"]);
                                conflictCount = Convert.ToInt32(reader["Cnt"]);
                            }
                        }
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    if (conflictCount > 0)
                    {
                        txtConflictMessage.Text = $"Hodowca {dostawca} ma już {conflictCount} dostaw{(conflictCount == 1 ? "ę" : "y")} dnia {data.Value:dd.MM.yyyy} (LP {existingLp}). Czy na pewno tworzyć kolejną?";
                        borderConflict.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        borderConflict.Visibility = Visibility.Collapsed;
                    }
                });
            }
            catch { }
        }

        private async void BtnRefreshHodowcy_Click(object sender, RoutedEventArgs e)
        {
            HodowcyCacheManager.Invalidate();
            string previouslySelected = cmbDostawca.SelectedItem?.ToString();
            await LoadHodowcyAsync();
            if (!string.IsNullOrEmpty(previouslySelected) && cmbDostawca.Items.Contains(previouslySelected))
                cmbDostawca.SelectedItem = previouslySelected;
        }

        #endregion

        #region Bidirectional kalkulator (3 pola, wpisz 2 z 3)

        private void CalcField_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_calcUpdating) return;

            int? sztSzuf = ParseInt(txtCalcSztSzuflada.Text);
            int? auta = ParseInt(txtCalcAuta.Text);
            int? suma = ParseIntFromFormatted(txtCalcSumaSztuki.Text);

            _calcUpdating = true;
            try
            {
                // Strategia: które pole jest puste? Wylicz je z pozostałych dwóch.
                bool tbIsCalcSuma = sender is TextBox tbS && tbS.Name == "txtCalcSumaSztuki";
                bool tbIsCalcAuta = sender is TextBox tbA && tbA.Name == "txtCalcAuta";
                bool tbIsCalcSztSzuf = sender is TextBox tbZ && tbZ.Name == "txtCalcSztSzuflada";

                // Jeśli mamy Szt/szuf i Auta → policz Sumę (wpisana zostaje gdzie indziej zachowana logika)
                if (sztSzuf.HasValue && sztSzuf.Value > 0 && auta.HasValue && auta.Value > 0 && !tbIsCalcSuma)
                {
                    int newSuma = sztSzuf.Value * SZUFLAD_PER_AUTO * auta.Value;
                    txtCalcSumaSztuki.Text = newSuma.ToString("#,0", CultureInfo.GetCultureInfo("pl-PL"));
                    txtCalcHint.Text = $"💡 {sztSzuf} × 264 × {auta} = {newSuma:#,0}";
                }
                // Jeśli mamy Szt/szuf i Sumę → policz Auta
                else if (sztSzuf.HasValue && sztSzuf.Value > 0 && suma.HasValue && suma.Value > 0 && !tbIsCalcAuta)
                {
                    double sztPerAuto = sztSzuf.Value * (double)SZUFLAD_PER_AUTO;
                    double newAuta = suma.Value / sztPerAuto;
                    txtCalcAuta.Text = ((int)Math.Ceiling(newAuta)).ToString();
                    txtCalcHint.Text = $"💡 {suma:#,0} ÷ ({sztSzuf} × 264) = {newAuta:0.00} auta";
                }
                // Jeśli mamy Auta i Sumę → policz Szt/szuf
                else if (auta.HasValue && auta.Value > 0 && suma.HasValue && suma.Value > 0 && !tbIsCalcSztSzuf)
                {
                    double newSztSzuf = suma.Value / (double)(SZUFLAD_PER_AUTO * auta.Value);
                    txtCalcSztSzuflada.Text = ((int)Math.Round(newSztSzuf)).ToString();
                    txtCalcHint.Text = $"💡 {suma:#,0} ÷ (264 × {auta}) = {newSztSzuf:0.00} szt/szuf";
                }
                else
                {
                    txtCalcHint.Text = "Wpisz dowolne 2 z 3 pól, trzecie się wyliczy";
                }
            }
            finally
            {
                _calcUpdating = false;
            }
        }

        private void BtnUseCalc_Click(object sender, RoutedEventArgs e)
        {
            int? sztSzuf = ParseInt(txtCalcSztSzuflada.Text);
            int? auta = ParseInt(txtCalcAuta.Text);
            int? suma = ParseInt(txtCalcSumaSztuki.Text);

            int filled = 0;
            if (sztSzuf.HasValue && sztSzuf.Value > 0)
            {
                txtSztNaSzuflade.Text = sztSzuf.Value.ToString();
                filled++;
            }
            if (auta.HasValue && auta.Value > 0)
            {
                txtAuta.Text = auta.Value.ToString();
                filled++;
            }
            if (suma.HasValue && suma.Value > 0)
            {
                txtSztuki.Text = suma.Value.ToString();
                filled++;
            }

            UpdateObliczoneAuta();

            // Krótki feedback w hint pod kalkulatorem
            if (filled > 0)
            {
                txtCalcHint.Text = $"✅ Wstawiono {filled} {(filled == 1 ? "wartość" : "wartości")} do pól dostawy";
            }
        }

        private int? ParseInt(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            // Usuń wszystkie białe znaki (włącznie z non-breaking space \u00A0 z pl-PL)
            // oraz separatory tysięcy (przecinek, kropka)
            var clean = new string(text.Where(c => !char.IsWhiteSpace(c) && c != ',' && c != '.').ToArray());
            if (int.TryParse(clean, out int v)) return v;
            return null;
        }

        private int? ParseIntFromFormatted(string text)
        {
            return ParseInt(text);
        }

        #endregion

        #region obl. Auta + walidacja inline + anomaly badges

        private void UpdateObliczoneAuta()
        {
            if (txtOblA == null || txtSztuki == null || txtSztNaSzuflade == null) return;

            int? sztuki = ParseInt(txtSztuki.Text);
            int? sztSzuf = ParseInt(txtSztNaSzuflade.Text);

            if (sztuki.HasValue && sztSzuf.HasValue && sztSzuf.Value > 0)
            {
                double pojemnosc = sztSzuf.Value * (double)SZUFLAD_PER_AUTO;
                double oblA = sztuki.Value / pojemnosc;
                txtOblA.Text = oblA.ToString("F2");
            }
            else
            {
                txtOblA.Text = "—";
            }
        }

        private void TxtOblA_Click(object sender, MouseButtonEventArgs e)
        {
            if (double.TryParse(txtOblA.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double oblA))
            {
                txtAuta.Text = ((int)Math.Ceiling(oblA)).ToString();
            }
        }

        private void ValidationField_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateObliczoneAuta();
            UpdateAnomalyBadges();

            if (sender is TextBox tb)
            {
                string fieldName = tb.Name switch
                {
                    "txtAuta" => "Auta",
                    "txtSztuki" => "SztukiDek",
                    "txtWagaDek" => "WagaDek",
                    "txtCena" => "Cena",
                    _ => ""
                };

                if (!string.IsNullOrEmpty(fieldName) && !string.IsNullOrWhiteSpace(tb.Text))
                {
                    var result = InlineEditValidator.Validate(fieldName, tb.Text);
                    if (result.Level == ValidationLevel.Error)
                        tb.ToolTip = "❌ " + result.Message;
                    else if (result.Level == ValidationLevel.Warning)
                        tb.ToolTip = "⚠ " + result.Message;
                    else
                        tb.ToolTip = null;
                }
                else
                {
                    tb.ToolTip = null;
                }
            }
        }

        // Anomaly: waga/cena znacząco różna od średniej hodowcy
        private void UpdateAnomalyBadges()
        {
            // Waga: >15% różnicy od średniej
            if (_avgWaga.HasValue && decimal.TryParse(txtWagaDek.Text?.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal waga) && waga > 0)
            {
                decimal diff = Math.Abs(waga - _avgWaga.Value);
                decimal pct = (_avgWaga.Value > 0) ? diff / _avgWaga.Value * 100m : 0m;
                if (pct > 15m)
                {
                    string sign = waga > _avgWaga.Value ? "+" : "−";
                    badgeWaga.Text = $"⚠ {sign}{pct:0}% od śr. {_avgWaga:0.00}";
                    badgeWaga.Visibility = Visibility.Visible;
                    borderWaga.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                }
                else
                {
                    badgeWaga.Visibility = Visibility.Collapsed;
                    borderWaga.BorderBrush = new SolidColorBrush(Color.FromRgb(187, 247, 208));
                }
            }
            else
            {
                badgeWaga.Visibility = Visibility.Collapsed;
            }

            // Cena: >20% różnicy od średniej
            if (_avgCena.HasValue && decimal.TryParse(txtCena.Text?.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal cena) && cena > 0)
            {
                decimal diff = Math.Abs(cena - _avgCena.Value);
                decimal pct = (_avgCena.Value > 0) ? diff / _avgCena.Value * 100m : 0m;
                if (pct > 20m)
                {
                    string sign = cena > _avgCena.Value ? "+" : "−";
                    badgeCena.Text = $"⚠ {sign}{pct:0}% od śr. {_avgCena:0.00}";
                    badgeCena.Visibility = Visibility.Visible;
                    borderCena.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                }
                else
                {
                    badgeCena.Visibility = Visibility.Collapsed;
                    borderCena.BorderBrush = new SolidColorBrush(Color.FromRgb(251, 207, 232));
                }
            }
            else
            {
                badgeCena.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region Daty + Enter-to-advance + skróty

        private async void DpData_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpData.SelectedDate.HasValue)
            {
                txtHeaderDate.Text = dpData.SelectedDate.Value.ToString("dddd, d MMMM yyyy", new CultureInfo("pl-PL"));
            }

            // Re-check konflikt po zmianie daty
            string dostawca = cmbDostawca.SelectedItem?.ToString();
            if (!string.IsNullOrWhiteSpace(dostawca))
                await CheckConflictAsync(dostawca, dpData.SelectedDate);
        }

        private void CmbDostawca_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                txtWagaDek.Focus();
                txtWagaDek.SelectAll();
                e.Handled = true;
            }
        }

        // Enter w polu = przejdź do następnego pola
        private void EnterAdvances_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                BtnZapiszIKolejna_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            // Tab order: Waga → Szt/szuf → Sztuki → Auta → Cena → Dodatek → Notatka → Zapisz
            if (sender is TextBox tb)
            {
                TextBox next = tb.Name switch
                {
                    "txtWagaDek" => txtSztNaSzuflade,
                    "txtSztNaSzuflade" => txtSztuki,
                    "txtSztuki" => txtAuta,
                    "txtAuta" => txtCena,
                    "txtCena" => txtDodatek,
                    "txtDodatek" => txtNotatka,
                    _ => null
                };

                if (next != null)
                {
                    next.Focus();
                    next.SelectAll();
                    e.Handled = true;
                }
            }
        }

        #endregion

        #region Zapis (INSERT do bazy)

        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            await SaveAndCloseAsync(openAnother: false);
        }

        private async void BtnZapiszIKolejna_Click(object sender, RoutedEventArgs e)
        {
            await SaveAndCloseAsync(openAnother: true);
        }

        private async Task SaveAndCloseAsync(bool openAnother)
        {
            string error = ValidateForm();
            if (error != null)
            {
                ShowValidationMessage(error);
                return;
            }

            HideValidationMessage();
            btnZapisz.IsEnabled = false;
            btnZapiszKolejna.IsEnabled = false;

            try
            {
                int newLp = await SaveDostawaAsync();
                CreatedLP = newLp;
                DeliveryCreated = true;
                OpenAnother = openAnother;
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                ShowValidationMessage($"Błąd zapisu: {ex.Message}");
                btnZapisz.IsEnabled = true;
                btnZapiszKolejna.IsEnabled = true;
            }
        }

        private string ValidateForm()
        {
            string dostawca = cmbDostawca.SelectedItem?.ToString() ?? cmbDostawca.Text?.Trim();
            if (string.IsNullOrWhiteSpace(dostawca))
                return "Wybierz hodowcę.";

            if (dpData.SelectedDate == null)
                return "Wybierz datę dostawy.";

            if (!string.IsNullOrWhiteSpace(txtAuta.Text))
            {
                var v = InlineEditValidator.Validate("Auta", txtAuta.Text);
                if (v.Level == ValidationLevel.Error) return $"Pole Auta: {v.Message}";
            }
            if (!string.IsNullOrWhiteSpace(txtSztuki.Text))
            {
                var v = InlineEditValidator.Validate("SztukiDek", txtSztuki.Text);
                if (v.Level == ValidationLevel.Error) return $"Pole Sztuki: {v.Message}";
            }
            if (!string.IsNullOrWhiteSpace(txtWagaDek.Text))
            {
                var v = InlineEditValidator.Validate("WagaDek", txtWagaDek.Text);
                if (v.Level == ValidationLevel.Error) return $"Pole Waga: {v.Message}";
            }
            if (!string.IsNullOrWhiteSpace(txtCena.Text))
            {
                var v = InlineEditValidator.Validate("Cena", txtCena.Text);
                if (v.Level == ValidationLevel.Error) return $"Pole Cena: {v.Message}";
            }

            return null;
        }

        private async Task<int> SaveDostawaAsync()
        {
            int newLp;
            string dostawca = cmbDostawca.SelectedItem?.ToString() ?? cmbDostawca.Text?.Trim();
            DateTime data = dpData.SelectedDate.Value;
            string notatka = txtNotatka.Text?.Trim();

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                using (var cmd = new SqlCommand("SELECT ISNULL(MAX(Lp), 0) + 1 FROM HarmonogramDostaw", conn))
                {
                    var result = await cmd.ExecuteScalarAsync();
                    newLp = Convert.ToInt32(result);
                }

                string sql = @"INSERT INTO HarmonogramDostaw
                    (Lp, DataOdbioru, Dostawca, Auta, SztukiDek, WagaDek, SztSzuflada,
                     TypUmowy, TypCeny, Cena, Dodatek, Bufor, DataUtw, ktoStwo, DataMod, KtoMod)
                    VALUES
                    (@Lp, @DataOdbioru, @Dostawca, @Auta, @Sztuki, @Waga, @SztSzuflada,
                     @TypUmowy, @TypCeny, @Cena, @Dodatek, @Bufor, GETDATE(), @KtoStwo, GETDATE(), @KtoMod)";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Lp", newLp);
                    cmd.Parameters.AddWithValue("@DataOdbioru", data);
                    cmd.Parameters.AddWithValue("@Dostawca", dostawca);
                    cmd.Parameters.AddWithValue("@Auta", ParseIntOrDbNull(txtAuta.Text));
                    cmd.Parameters.AddWithValue("@Sztuki", ParseIntOrDbNull(txtSztuki.Text));
                    cmd.Parameters.AddWithValue("@Waga", ParseDecimalOrDbNull(txtWagaDek.Text));
                    cmd.Parameters.AddWithValue("@SztSzuflada", ParseIntOrDbNull(txtSztNaSzuflade.Text));
                    cmd.Parameters.AddWithValue("@TypUmowy", (object)cmbTypUmowy.SelectedItem ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@TypCeny", (object)cmbTypCeny.SelectedItem ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Cena", ParseDecimalOrDbNull(txtCena.Text));
                    cmd.Parameters.AddWithValue("@Dodatek", ParseDecimalOrDbNull(txtDodatek.Text));
                    cmd.Parameters.AddWithValue("@Bufor", _selectedStatus);
                    cmd.Parameters.AddWithValue("@KtoStwo", _userId ?? "0");
                    cmd.Parameters.AddWithValue("@KtoMod", _userId ?? "0");
                    await cmd.ExecuteNonQueryAsync();
                }

                if (!string.IsNullOrWhiteSpace(notatka))
                {
                    string noteSql = @"INSERT INTO Notatki (IndeksID, Tresc, KtoStworzyl, DataUtworzenia)
                                       VALUES (@IndeksID, @Tresc, @Kto, GETDATE())";
                    using (var cmd = new SqlCommand(noteSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@IndeksID", newLp);
                        cmd.Parameters.AddWithValue("@Tresc", notatka);
                        cmd.Parameters.AddWithValue("@Kto", _userId ?? "0");
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }

            if (_auditService != null)
            {
                await _auditService.LogInsertAsync(
                    "HarmonogramDostaw",
                    newLp.ToString(),
                    AuditChangeSource.Button_Nowa,
                    new Dictionary<string, object>
                    {
                        ["Dostawca"] = dostawca,
                        ["DataOdbioru"] = data.ToString("yyyy-MM-dd"),
                        ["Auta"] = txtAuta.Text,
                        ["SztukiDek"] = txtSztuki.Text,
                        ["WagaDek"] = txtWagaDek.Text,
                        ["Cena"] = txtCena.Text,
                        ["TypCeny"] = cmbTypCeny.SelectedItem?.ToString(),
                        ["Bufor"] = _selectedStatus
                    },
                    new AuditContextInfo { Dostawca = dostawca, DataOdbioru = data });
            }

            return newLp;
        }

        private object ParseIntOrDbNull(string text)
        {
            if (int.TryParse(text?.Trim(), out int v)) return v;
            return DBNull.Value;
        }

        private object ParseDecimalOrDbNull(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return DBNull.Value;
            string normalized = text.Trim().Replace(",", ".");
            if (decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal v)) return v;
            return DBNull.Value;
        }

        #endregion

        #region UI helpers

        private void ShowValidationMessage(string msg)
        {
            txtValidationMessage.Text = "❌ " + msg;
            borderValidation.Visibility = Visibility.Visible;
        }

        private void HideValidationMessage()
        {
            borderValidation.Visibility = Visibility.Collapsed;
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.DialogResult = false;
                this.Close();
                e.Handled = true;
            }
            else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                BtnZapisz_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                BtnZapiszIKolejna_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        #endregion

        private class StatusButtonInfo
        {
            public string Name { get; set; }
            public string ActiveColor { get; set; }
            public string HoverColor { get; set; }
        }
    }
}
