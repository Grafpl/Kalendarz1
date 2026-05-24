using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Kalendarz1.Zywiec.WidokSpecyfikacji
{
    /// <summary>
    /// Mapowanie LibraNet.Dostawcy → Symfonia.STContractors.
    /// V2: jeden DataGrid z inline autocomplete per wiersz. Brak osobnej listy kontrahentów.
    /// </summary>
    public partial class MapowanieDostawcowWindow : Window
    {
        private const string ConnLibra   = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private const string ConnHandel  = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        public ObservableCollection<DostawcaMapowanie> Dostawcy { get; } = new();

        private List<DostawcaMapowanie> _allDostawcy = new();
        private List<KontrahentSymfonia> _allKontrahenci = new();
        private Dictionary<int, KontrahentSymfonia> _kontrahenciById = new();

        public MapowanieDostawcowWindow()
        {
            // Rejestracja konwerterów PRZED InitializeComponent (bo XAML ich używa)
            if (!Application.Current.Resources.Contains("BoolToVisConv"))
                Application.Current.Resources.Add("BoolToVisConv", new BoolToVisibilityConv());
            if (!Application.Current.Resources.Contains("StrToVisConv"))
                Application.Current.Resources.Add("StrToVisConv", new NonEmptyStringToVisibilityConv());

            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            DataContext = this;
            Loaded += async (_, __) => await LoadAsync();
        }

        // ============ LOADING ============
        private async Task LoadAsync()
        {
            ShowLoading("Ładowanie dostawców i kontrahentów Symfonii...");
            try
            {
                await Task.Run(() =>
                {
                    LoadDostawcy();
                    LoadKontrahenci();
                });

                _kontrahenciById = _allKontrahenci.ToDictionary(k => k.Id);

                Dispatcher.Invoke(() =>
                {
                    foreach (var d in _allDostawcy) d.Owner = this;
                    UpdateMappedDisplay();
                    ApplyFilter();
                    UpdateStats();
                    txtStatus.Text = $"Załadowano {_allDostawcy.Count} dostawców, {_allKontrahenci.Count} kontrahentów Symfonii";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show("Błąd ładowania: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error));
            }
            finally
            {
                HideLoading();
            }
        }

        private void LoadDostawcy()
        {
            _allDostawcy.Clear();
            using var conn = new SqlConnection(ConnLibra);
            conn.Open();
            using var cmd = new SqlCommand(@"
                SELECT ID,
                       ISNULL(ShortName,'') AS ShortName,
                       ISNULL(NIP,'')       AS NIP,
                       ISNULL(IdSymf, 0)    AS IdSymf
                FROM dbo.Dostawcy ORDER BY ShortName", conn);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                _allDostawcy.Add(new DostawcaMapowanie
                {
                    ID = rdr["ID"]?.ToString()?.Trim() ?? "",
                    ShortName = rdr["ShortName"]?.ToString()?.Trim() ?? "",
                    Nip = NormalizeNip(rdr["NIP"]?.ToString() ?? ""),
                    IdSymf = Convert.ToInt32(rdr["IdSymf"])
                });
            }
        }

        private static string NormalizeNip(string nip) =>
            new string((nip ?? "").Where(char.IsDigit).ToArray());

        private void LoadKontrahenci()
        {
            _allKontrahenci.Clear();
            using var conn = new SqlConnection(ConnHandel);
            conn.Open();
            using (var cmd = new SqlCommand(@"
                SELECT Id, ISNULL(Shortcut,'') AS Code, ISNULL(NIP,'') AS NIP, ISNULL(Name,'') AS Name
                FROM SSCommon.STContractors
                ORDER BY Name", conn))
            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    _allKontrahenci.Add(new KontrahentSymfonia
                    {
                        Id = Convert.ToInt32(rdr["Id"]),
                        Code = rdr["Code"]?.ToString()?.Trim() ?? "",
                        NIP = rdr["NIP"]?.ToString()?.Trim() ?? "",
                        NipDigits = NormalizeNip(rdr["NIP"]?.ToString() ?? ""),
                        Name = rdr["Name"]?.ToString()?.Trim() ?? ""
                    });
                }
            }

            // Batch query - liczba faktur FVR/FVZ/FKZ w ostatnich 12mc per khid
            var fvCounts = new Dictionary<int, int>();
            using (var cmd = new SqlCommand(@"
                SELECT khid, COUNT(*) AS cnt
                FROM HM.DK
                WHERE khid IS NOT NULL
                  AND typ_dk IN ('FVR','FVZ','FKZ')
                  AND ISNULL(anulowany,0)=0 AND aktywny=1
                  AND data >= DATEADD(MONTH, -12, GETDATE())
                GROUP BY khid", conn) { CommandTimeout = 60 })
            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                    fvCounts[rdr.GetInt32(0)] = rdr.GetInt32(1);
            }
            foreach (var k in _allKontrahenci)
                k.FvCount12m = fvCounts.TryGetValue(k.Id, out var c) ? c : 0;
        }

        private void UpdateMappedDisplay()
        {
            foreach (var d in _allDostawcy)
            {
                if (d.IdSymf > 0 && _kontrahenciById.TryGetValue(d.IdSymf, out var k))
                    d.ApplyMapping(k);
                else
                    d.ClearMapping();
            }
        }

        private void ApplyFilter()
        {
            string q = (txtFilter?.Text ?? "").Trim().ToLowerInvariant();
            bool onlyBrak = chkOnlyBrak?.IsChecked == true;
            var filtered = _allDostawcy
                .Where(d => string.IsNullOrEmpty(q) ||
                            d.ID.ToLowerInvariant().Contains(q) ||
                            d.ShortName.ToLowerInvariant().Contains(q))
                .Where(d => !onlyBrak || d.IsUnmapped)
                .ToList();

            Dostawcy.Clear();
            foreach (var d in filtered) Dostawcy.Add(d);
            txtStatus.Text = $"Widoczne: {filtered.Count} z {_allDostawcy.Count}";
        }

        private void UpdateStats()
        {
            int total = _allDostawcy.Count;
            int mapped = _allDostawcy.Count(d => d.IdSymf > 0);
            int brak = total - mapped;
            int percent = total > 0 ? (mapped * 100 / total) : 0;
            txtStatZmapowani.Text = mapped.ToString();
            txtStatBrak.Text = brak.ToString();
            txtStatProc.Text = percent + "%";
        }

        // ============ FUZZY MATCH ============
        // Zwraca top 7 najlepszych dopasowań posortowane wg score
        internal List<KontrahentSymfonia> Match(string query, int max = 7)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<KontrahentSymfonia>();
            string q = query.Trim().ToLowerInvariant();

            return _allKontrahenci
                .Select(k => (k, score: Score(k, q)))
                .Where(x => x.score > 0)
                .OrderByDescending(x => x.score)
                .ThenBy(x => x.k.Name)
                .Take(max)
                .Select(x => x.k)
                .ToList();
        }

        private static int Score(KontrahentSymfonia k, string q)
        {
            // Higher = better match
            int score = 0;
            string name = (k.Name ?? "").ToLowerInvariant();
            string code = (k.Code ?? "").ToLowerInvariant();
            string nip  = (k.NIP ?? "").Trim();

            if (name == q || code == q || nip == q) return 1000;            // exact
            if (name.StartsWith(q)) score = 700;
            else if (code.StartsWith(q)) score = 650;
            else if (name.Contains(q)) score = 400;
            else if (code.Contains(q)) score = 350;
            else if (nip.Contains(q)) score = 300;
            // jeśli wpisany NIP (10+ cyfr) i pasuje częściowo
            if (q.Length >= 3 && q.All(char.IsDigit) && nip.Contains(q)) score = Math.Max(score, 500);
            return score;
        }

        // ============ ZAPIS MAPOWANIA ============
        internal async Task<bool> ZapiszMapowanieAsync(DostawcaMapowanie dostawca, KontrahentSymfonia kontrahent)
        {
            try
            {
                using var conn = new SqlConnection(ConnLibra);
                await conn.OpenAsync();
                using var cmd = new SqlCommand("UPDATE dbo.Dostawcy SET IdSymf = @IdSymf WHERE ID = @ID", conn);
                cmd.Parameters.AddWithValue("@IdSymf", kontrahent.Id);
                cmd.Parameters.AddWithValue("@ID", dostawca.ID);
                await cmd.ExecuteNonQueryAsync();

                dostawca.IdSymf = kontrahent.Id;
                dostawca.ApplyMapping(kontrahent);
                UpdateStats();
                txtStatus.Text = $"✓ Zapisano: {dostawca.ShortName} → {kontrahent.Name} (IdSymf={kontrahent.Id})";
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zapisu:\n" + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        internal async Task UsunMapowanieAsync(DostawcaMapowanie dostawca)
        {
            try
            {
                using var conn = new SqlConnection(ConnLibra);
                await conn.OpenAsync();
                using var cmd = new SqlCommand("UPDATE dbo.Dostawcy SET IdSymf = NULL WHERE ID = @ID", conn);
                cmd.Parameters.AddWithValue("@ID", dostawca.ID);
                await cmd.ExecuteNonQueryAsync();

                dostawca.IdSymf = 0;
                dostawca.ClearMapping();
                UpdateStats();
                txtStatus.Text = $"🗑 Usunięto mapowanie: {dostawca.ShortName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============ UI EVENT HANDLERS ============
        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
        private void ChkOnlyBrak_Changed(object sender, RoutedEventArgs e) => ApplyFilter();
        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ============ BULK AUTO-MAP po NIP ============
        private async void BtnBulkAutoMap_Click(object sender, RoutedEventArgs e)
        {
            // Indeks po NIP dla kontrahentow Symfonii (tylko 10-cyfrowe NIP)
            var byNip = _allKontrahenci
                .Where(k => k.NipDigits.Length == 10)
                .GroupBy(k => k.NipDigits)
                .Where(g => g.Count() == 1)  // tylko unikalne NIPy (jak NIP duplikat - rezygnujemy z auto-mapy)
                .ToDictionary(g => g.Key, g => g.First());

            var pary = new List<(DostawcaMapowanie d, KontrahentSymfonia k)>();
            foreach (var d in _allDostawcy.Where(x => x.IsUnmapped && x.Nip.Length == 10))
            {
                if (byNip.TryGetValue(d.Nip, out var k))
                    pary.Add((d, k));
            }

            if (pary.Count == 0)
            {
                MessageBox.Show(
                    "Nie znaleziono par o identycznym NIP.\n\n" +
                    "Sprawdzono dostawcow z 10-cyfrowym NIP-em i kontrahentow Symfonii z unikalnymi 10-cyfrowymi NIP-ami.",
                    "Auto-map po NIP", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var preview = string.Join("\n", pary.Take(15).Select(p => $"  • {p.d.ShortName}  →  {p.k.Name}  ({p.d.Nip})"));
            if (pary.Count > 15) preview += $"\n  ... i {pary.Count - 15} więcej";

            var result = MessageBox.Show(
                $"Znaleziono {pary.Count} par o identycznym NIP:\n\n{preview}\n\n" +
                $"Zmapowac wszystkie automatycznie?",
                "Auto-map po NIP", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            ShowLoading($"Zapisywanie {pary.Count} mapowań...");
            int ok = 0, err = 0;
            try
            {
                using var conn = new SqlConnection(ConnLibra);
                await conn.OpenAsync();
                foreach (var (d, k) in pary)
                {
                    try
                    {
                        using var cmd = new SqlCommand("UPDATE dbo.Dostawcy SET IdSymf = @IdSymf WHERE ID = @ID", conn);
                        cmd.Parameters.AddWithValue("@IdSymf", k.Id);
                        cmd.Parameters.AddWithValue("@ID", d.ID);
                        await cmd.ExecuteNonQueryAsync();
                        d.IdSymf = k.Id;
                        d.ApplyMapping(k);
                        ok++;
                    }
                    catch { err++; }
                }
                UpdateStats();
                txtStatus.Text = $"✓ Auto-mapowano: {ok} sukcesow, {err} bledow";
            }
            finally
            {
                HideLoading();
            }

            MessageBox.Show($"Zapisano {ok} z {pary.Count} mapowań.", "Auto-map po NIP",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
            if (e.Key == Key.F5) _ = LoadAsync();
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.Tag is DostawcaMapowanie d)
            {
                // Niezmapowany + pusty SearchText -> wypelnij od pierwszego slowa z nazwy dostawcy
                // Zmapowany -> NIE otwieraj popupu automatycznie (uzytkownik moze chciec tylko zobaczyc nazwe)
                if (d.IsUnmapped)
                {
                    if (string.IsNullOrWhiteSpace(d.SearchText))
                        d.SearchText = ExtractFirstWord(d.ShortName);
                    else
                        d.RefreshSuggestions(this);
                    tb.SelectAll();
                }
                else
                {
                    // Juz zmapowany - tylko zaznacz tekst, popup zostanie zamkniety
                    tb.SelectAll();
                }
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Daj 200ms zeby klik w popup się zarejestrował
            if (sender is TextBox tb && tb.Tag is DostawcaMapowanie d)
            {
                _ = Task.Delay(200).ContinueWith(_ => Dispatcher.Invoke(() => { d.IsPopupOpen = false; }));
            }
        }

        private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox tb || tb.Tag is not DostawcaMapowanie d) return;

            if (e.Key == Key.Escape)
            {
                d.IsPopupOpen = false;
                Keyboard.ClearFocus();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                // wybierz pierwsza sugestie
                var first = d.Suggestions.FirstOrDefault();
                if (first != null) await ZapiszMapowanieAsync(d, first);
                d.IsPopupOpen = false;
                e.Handled = true;
            }
        }

        private async void Suggestion_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // ListBox PreviewMouseLeftButtonDown - znajdz ListBoxItem clicked
            if (sender is not ListBox lb) return;
            DependencyObject? src = e.OriginalSource as DependencyObject;
            while (src != null && src is not ListBoxItem) src = VisualTreeHelper.GetParent(src);
            if (src is not ListBoxItem lbi || lbi.DataContext is not KontrahentSymfonia k) return;
            if (lb.DataContext is not DostawcaMapowanie d) return;

            await ZapiszMapowanieAsync(d, k);
            // SearchText jest ustawiany przez ApplyMapping na k.Name - NIE czyscic.
            // IsPopupOpen jest ustawiany na false przez ApplyMapping.
            Keyboard.ClearFocus(); // odejdz focusem zeby popup nie wyskoczyl ponownie
            e.Handled = true;
        }

        private async void BtnClearMapping_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DostawcaMapowanie d)
            {
                await UsunMapowanieAsync(d);
            }
        }

        private static string ExtractFirstWord(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var parts = s.Split(new[] { ' ', '.', ',', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : s;
        }

        // ============ LOADING OVERLAY ============
        private void ShowLoading(string msg)
        {
            loadingText.Text = msg;
            loadingOverlay.Visibility = Visibility.Visible;
        }
        private void HideLoading() => loadingOverlay.Visibility = Visibility.Collapsed;
    }

    // ============ MODELS ============
    public class DostawcaMapowanie : INotifyPropertyChanged
    {
        public string ID { get; set; } = "";
        public string ShortName { get; set; } = "";
        public string Nip { get; set; } = "";  // tylko cyfry
        public string NipDisplay => string.IsNullOrEmpty(Nip) ? "—" : Nip;

        private int _idSymf;
        public int IdSymf { get => _idSymf; set { _idSymf = value; OnChanged(); OnChanged(nameof(IsUnmapped)); OnChanged(nameof(HasMapping)); OnChanged(nameof(StatusText)); OnChanged(nameof(StatusBackground)); OnChanged(nameof(StatusForeground)); } }

        public bool IsUnmapped => IdSymf == 0;
        public bool HasMapping => IdSymf > 0;

        // === Per-row autocomplete state ===
        internal MapowanieDostawcowWindow? Owner { get; set; }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnChanged(); RefreshSuggestions(Owner); }
        }

        public ObservableCollection<KontrahentSymfonia> Suggestions { get; } = new();

        private bool _isPopupOpen;
        public bool IsPopupOpen { get => _isPopupOpen; set { _isPopupOpen = value; OnChanged(); } }

        internal void RefreshSuggestions(MapowanieDostawcowWindow? owner)
        {
            Suggestions.Clear();
            if (owner == null) return;
            var matches = owner.Match(SearchText);
            foreach (var k in matches) Suggestions.Add(k);
            IsPopupOpen = matches.Count > 0;
        }

        // === Status / wyświetlanie ===
        public string StatusText => IsUnmapped ? "⚠ BRAK MAPOWANIA" : $"✓ ID Symf. {IdSymf}";
        public Brush StatusBackground => IsUnmapped ? new SolidColorBrush(Color.FromRgb(254, 226, 226)) : new SolidColorBrush(Color.FromRgb(220, 252, 231));
        public Brush StatusForeground => IsUnmapped ? new SolidColorBrush(Color.FromRgb(185, 28, 28)) : new SolidColorBrush(Color.FromRgb(22, 101, 52));

        internal void ApplyMapping(KontrahentSymfonia k)
        {
            // Po sukcesie zapisz nazwę kontrahenta w SearchText
            _searchText = k.Name;
            OnChanged(nameof(SearchText));
            IsPopupOpen = false;
        }

        internal void ClearMapping()
        {
            _searchText = "";
            OnChanged(nameof(SearchText));
            IsPopupOpen = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class KontrahentSymfonia
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string NIP { get; set; } = "";          // raw (z myslnikami itp.)
        public string NipDigits { get; set; } = "";    // tylko cyfry
        public string Name { get; set; } = "";
        public int FvCount12m { get; set; }

        public string FvCountText => FvCount12m > 0 ? FvCount12m + " FV / 12mc" : "brak FV";
        public bool HasFv => FvCount12m > 0;

        public string Hint
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(Code)) parts.Add("Kod: " + Code);
                parts.Add("IdSymf: " + Id);
                parts.Add(FvCountText);
                return string.Join("  ·  ", parts);
            }
        }
    }

    // ============ CONVERTERS ============
    internal class BoolToVisibilityConv : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            throw new NotImplementedException();
    }

    internal class NonEmptyStringToVisibilityConv : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            (value is string s && !string.IsNullOrWhiteSpace(s)) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
