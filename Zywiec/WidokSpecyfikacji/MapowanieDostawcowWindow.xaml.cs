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
        // TAB 2: kontrahenci Symfonii z FVR/FVZ bez mapowania na hodowcę z LibraNet
        public ObservableCollection<OrfanFakturowy> Orfani { get; } = new();

        private List<DostawcaMapowanie> _allDostawcy = new();
        private List<KontrahentSymfonia> _allKontrahenci = new();
        private Dictionary<int, KontrahentSymfonia> _kontrahenciById = new();
        private List<OrfanFakturowy> _allOrfani = new(); // pełna lista (do filtrowania w UI)

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

                // Tab 2 — kontrahenci Symfonii z FVR/FVZ bez przypisania (domyślnie 12 mc)
                await LoadOrfaniAsync(GetSelectedOrfaniMonths());
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

        // ============ TAB 2: ORFANI (kontrahenci Symfonii z FVR/FVZ bez mapowania na LibraNet) ============

        private int GetSelectedOrfaniMonths()
        {
            if (cmbOrfaniOkres?.SelectedItem is ComboBoxItem item
                && int.TryParse(item.Tag?.ToString(), out int m) && m > 0) return m;
            return 12;
        }

        private async Task LoadOrfaniAsync(int monthsBack)
        {
            ShowLoading($"Wyszukiwanie kontrahentów z FVR/FVZ za ostatnie {monthsBack} mc...");
            try
            {
                // Set zmapowanych IdSymf — robimy w UI watku, raz, zanim pojdziemy w tle
                var mappedKhIds = _allDostawcy.Where(d => d.IdSymf > 0)
                                              .Select(d => d.IdSymf)
                                              .ToHashSet();

                var loaded = await Task.Run(() =>
                {
                    var lista = new List<OrfanFakturowy>();
                    using var conn = new SqlConnection(ConnHandel);
                    conn.Open();
                    // Tylko FVR/FVZ (NIE FKZ) z pozycją "Kurczak żywy" — zgodnie z prośbą Sergiusza.
                    // Dodatkowo: SUM(kg) i SUM(wartość netto) z pozycji "Kurczak żywy" → priorytetyzacja
                    // (duzi kontrahenci ze sporym wolumenem = najpilniejsi do zmapowania).
                    // ORDER BY MAX(data) ASC — najstarsza "ostatnia faktura" na górze.
                    using var cmd = new SqlCommand(@"
                        SELECT k.Id,
                               ISNULL(k.Name,'')   AS Name,
                               ISNULL(k.NIP,'')    AS NIP,
                               COUNT(DISTINCT d.id) AS FvCnt,
                               MAX(d.data)         AS Ostatnia,
                               ISNULL(SUM(ABS(p.ilosc)), 0) AS Kg,
                               ISNULL(SUM(ABS(p.ilosc) * p.cena), 0) AS Wartosc
                        FROM HM.DK d
                        JOIN SSCommon.STContractors k ON d.khid = k.Id
                        JOIN HM.DP p ON p.super = d.id
                        JOIN HM.TW t ON t.id = p.idtw
                        WHERE d.typ_dk IN ('FVR','FVZ')
                          AND ISNULL(d.anulowany,0) = 0
                          AND d.aktywny = 1
                          AND d.data >= DATEADD(MONTH, -@m, GETDATE())
                          AND (t.nazwa LIKE N'Kurczak żywy%' OR t.nazwa LIKE N'Kurczak zywy%')
                        GROUP BY k.Id, k.Name, k.NIP
                        ORDER BY MAX(d.data) ASC, COUNT(DISTINCT d.id) DESC", conn) { CommandTimeout = 60 };
                    cmd.Parameters.AddWithValue("@m", monthsBack);
                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        int id = rdr.GetInt32(0);
                        if (mappedKhIds.Contains(id)) continue; // już zmapowany — pomiń
                        lista.Add(new OrfanFakturowy
                        {
                            KontrahentId    = id,
                            KontrahentName  = rdr["Name"]?.ToString()?.Trim() ?? "",
                            KontrahentNip   = rdr["NIP"]?.ToString()?.Trim() ?? "",
                            FvCount         = rdr.GetInt32(3),
                            OstatniaFaktura = rdr.IsDBNull(4) ? (DateTime?)null : rdr.GetDateTime(4),
                            Kg              = Convert.ToDecimal(rdr["Kg"]),
                            Wartosc         = Convert.ToDecimal(rdr["Wartosc"])
                        });
                    }
                    return lista;
                });

                Dispatcher.Invoke(() =>
                {
                    foreach (var o in loaded) o.Owner = this;
                    _allOrfani = loaded;
                    ComputeNipSuggestions();
                    ApplyOrfaniFilter();
                    int withSug = _allOrfani.Count(o => o.MaSugestieNip);
                    string sug = withSug > 0 ? $"  ·  💡 {withSug} z gotową sugestią po NIP" : "";
                    txtStatus.Text = $"📄 Kurczak żywy: {_allOrfani.Count} kontrahentów Symfonii ({monthsBack} mc) bez hodowcy w LibraNet{sug}";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show("Błąd ładowania orfanów: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error));
            }
            finally
            {
                HideLoading();
            }
        }

        // Szukanie hodowcy w LibraNet (tylko niezmapowanych — żeby nie podpinać już zajętego pod 2 kontrahentów)
        internal List<DostawcaMapowanie> MatchDostawcyLibra(string query, int max = 7)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<DostawcaMapowanie>();
            string q = query.Trim().ToLowerInvariant();

            return _allDostawcy
                .Where(d => d.IsUnmapped) // tylko wolni hodowcy (mapowanie 1:1)
                .Select(d => (d, score: ScoreDostawca(d, q)))
                .Where(x => x.score > 0)
                .OrderByDescending(x => x.score)
                .ThenBy(x => x.d.ShortName)
                .Take(max)
                .Select(x => x.d)
                .ToList();
        }

        private static int ScoreDostawca(DostawcaMapowanie d, string q)
        {
            int score = 0;
            string name = (d.ShortName ?? "").ToLowerInvariant();
            string id   = (d.ID ?? "").ToLowerInvariant();
            string nip  = d.Nip ?? "";

            if (name == q || id == q || nip == q) return 1000;
            if (name.StartsWith(q)) score = 700;
            else if (id.StartsWith(q)) score = 650;
            else if (name.Contains(q)) score = 400;
            else if (id.Contains(q)) score = 350;
            else if (nip.Contains(q)) score = 300;
            if (q.Length >= 3 && q.All(char.IsDigit) && nip.Contains(q)) score = Math.Max(score, 500);
            return score;
        }

        // Przypisanie: ustaw IdSymf u WYBRANEGO hodowcy LibraNet na Id orfana
        internal async Task<bool> PrzypiszOrfanaAsync(OrfanFakturowy orfan, DostawcaMapowanie dostawca)
        {
            try
            {
                using var conn = new SqlConnection(ConnLibra);
                await conn.OpenAsync();
                using var cmd = new SqlCommand("UPDATE dbo.Dostawcy SET IdSymf = @IdSymf WHERE ID = @ID", conn);
                cmd.Parameters.AddWithValue("@IdSymf", orfan.KontrahentId);
                cmd.Parameters.AddWithValue("@ID", dostawca.ID);
                await cmd.ExecuteNonQueryAsync();

                // Zsynchronizuj w pamięci: hodowca dostaje IdSymf orfana
                dostawca.IdSymf = orfan.KontrahentId;
                if (_kontrahenciById.TryGetValue(orfan.KontrahentId, out var k))
                    dostawca.ApplyMapping(k);

                // Usuń orfana z listy (już ma swojego hodowcę)
                _allOrfani.Remove(orfan);
                ApplyOrfaniFilter();

                UpdateStats();
                txtStatus.Text = $"✓ Przypisano: {orfan.KontrahentName} → hodowca {dostawca.ShortName} (LibraNet ID {dostawca.ID})";
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zapisu:\n" + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // Sugestia NIP per orfan: jeśli istnieje DOKŁADNIE JEDEN niezmapowany hodowca LibraNet z tym samym 10-cyfr NIP-em.
        private void ComputeNipSuggestions()
        {
            var byNip = _allDostawcy
                .Where(d => d.IsUnmapped && d.Nip.Length == 10)
                .GroupBy(d => d.Nip)
                .Where(g => g.Count() == 1)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var o in _allOrfani)
            {
                var nipDigits = NormalizeNip(o.KontrahentNip);
                o.SugestiaNip = (nipDigits.Length == 10 && byNip.TryGetValue(nipDigits, out var d)) ? d : null;
            }
        }

        private void ApplyOrfaniFilter()
        {
            string q = (txtOrfaniFilter?.Text ?? "").Trim().ToLowerInvariant();
            var filtered = string.IsNullOrEmpty(q)
                ? _allOrfani
                : _allOrfani.Where(o =>
                      o.KontrahentName.ToLowerInvariant().Contains(q) ||
                      o.KontrahentNip.Contains(q) ||
                      NormalizeNip(o.KontrahentNip).Contains(q))
                    .ToList();

            Orfani.Clear();
            foreach (var o in filtered) Orfani.Add(o);
            txtOrfaniCount.Text = filtered.Count.ToString();

            // Empty state — jeśli wszystko zmapowane (lub filtr nie pasuje) pokaż przyjazny komunikat
            if (orfaniEmptyState != null)
                orfaniEmptyState.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (orfaniEmptyText != null)
                orfaniEmptyText.Text = _allOrfani.Count == 0
                    ? "✅  Brawo! Wszyscy kontrahenci z fakturami za 'Kurczak żywy' są zmapowani do hodowców w LibraNet."
                    : $"🔍  Filtr '{q}' nie pasuje do żadnego z {_allOrfani.Count} kontrahentów. Wyczyść pole, by zobaczyć wszystkich.";
        }

        private void TxtOrfaniFilter_TextChanged(object sender, TextChangedEventArgs e) => ApplyOrfaniFilter();

        // Bulk auto-przypisz po NIP (analogicznie do Tab 1, w przeciwnym kierunku)
        private async void BtnOrfaniBulkAutoMap_Click(object sender, RoutedEventArgs e)
        {
            var pary = _allOrfani.Where(o => o.SugestiaNip != null).ToList();
            if (pary.Count == 0)
            {
                MessageBox.Show(
                    "Brak orfanów z gotową sugestią po NIP.\n\n" +
                    "Sugestia pojawia się tylko gdy w LibraNet istnieje DOKŁADNIE JEDEN niezmapowany hodowca o tym samym 10-cyfrowym NIP-ie.",
                    "Auto-przypisz po NIP", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var preview = string.Join("\n", pary.Take(15).Select(p =>
                $"  • {p.KontrahentName}  →  {p.SugestiaNip!.ShortName}  ({NormalizeNip(p.KontrahentNip)})"));
            if (pary.Count > 15) preview += $"\n  ... i {pary.Count - 15} więcej";

            var result = MessageBox.Show(
                $"Znaleziono {pary.Count} orfanów z gotową sugestią po NIP:\n\n{preview}\n\n" +
                "Przypisać wszystkich automatycznie?",
                "Auto-przypisz po NIP (Tab 2)", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            ShowLoading($"Przypisywanie {pary.Count} hodowców...");
            int ok = 0, err = 0;
            try
            {
                using var conn = new SqlConnection(ConnLibra);
                await conn.OpenAsync();
                foreach (var (o, d) in pary.Select(p => (o: p, d: p.SugestiaNip!)))
                {
                    try
                    {
                        using var cmd = new SqlCommand("UPDATE dbo.Dostawcy SET IdSymf = @IdSymf WHERE ID = @ID", conn);
                        cmd.Parameters.AddWithValue("@IdSymf", o.KontrahentId);
                        cmd.Parameters.AddWithValue("@ID", d.ID);
                        await cmd.ExecuteNonQueryAsync();

                        d.IdSymf = o.KontrahentId;
                        if (_kontrahenciById.TryGetValue(o.KontrahentId, out var k)) d.ApplyMapping(k);
                        _allOrfani.Remove(o);
                        ok++;
                    }
                    catch { err++; }
                }
                UpdateStats();
                ApplyOrfaniFilter();
                txtStatus.Text = $"✓ Auto-przypisano: {ok} sukcesów, {err} błędów";
            }
            finally
            {
                HideLoading();
            }

            MessageBox.Show($"Przypisano {ok} z {pary.Count} hodowców.", "Auto-przypisz po NIP",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Klik w zielony chip „💡 Sugestia NIP" — jeden klik = przypisanie
        private async void BtnSugestiaNip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is OrfanFakturowy o && o.SugestiaNip != null)
            {
                await PrzypiszOrfanaAsync(o, o.SugestiaNip);
            }
        }

        // ============ Handlery dla autocomplete orfanów ============

        private void CmbOrfaniOkres_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Nie odpalaj przy pierwszym renderowaniu (gdy _allDostawcy jeszcze pusty)
            if (_allDostawcy.Count == 0) return;
            _ = LoadOrfaniAsync(GetSelectedOrfaniMonths());
        }

        private async void BtnRefreshOrfani_Click(object sender, RoutedEventArgs e)
        {
            await LoadOrfaniAsync(GetSelectedOrfaniMonths());
        }

        private void OrfanSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.Tag is OrfanFakturowy o)
            {
                if (string.IsNullOrWhiteSpace(o.SearchText))
                    o.SearchText = ExtractFirstWord(o.KontrahentName);
                else
                    o.RefreshSuggestions(this);
                tb.SelectAll();
            }
        }

        private void OrfanSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.Tag is OrfanFakturowy o)
            {
                _ = Task.Delay(200).ContinueWith(_ => Dispatcher.Invoke(() => { o.IsPopupOpen = false; }));
            }
        }

        private async void OrfanSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox tb || tb.Tag is not OrfanFakturowy o) return;

            if (e.Key == Key.Escape)
            {
                o.IsPopupOpen = false;
                Keyboard.ClearFocus();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                var first = o.Suggestions.FirstOrDefault();
                if (first != null) await PrzypiszOrfanaAsync(o, first);
                o.IsPopupOpen = false;
                e.Handled = true;
            }
        }

        private async void OrfanSuggestion_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ListBox lb) return;
            DependencyObject? src = e.OriginalSource as DependencyObject;
            while (src != null && src is not ListBoxItem) src = VisualTreeHelper.GetParent(src);
            if (src is not ListBoxItem lbi || lbi.DataContext is not DostawcaMapowanie d) return;
            if (lb.DataContext is not OrfanFakturowy o) return;

            await PrzypiszOrfanaAsync(o, d);
            Keyboard.ClearFocus();
            e.Handled = true;
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

    // Kontrahent Symfonii (z fakturami FVR/FVZ za "Kurczak żywy") bez przypisania do hodowcy LibraNet — TAB 2
    public class OrfanFakturowy : INotifyPropertyChanged
    {
        public int KontrahentId { get; set; }
        public string KontrahentName { get; set; } = "";
        public string KontrahentNip { get; set; } = "";
        public string KontrahentNipDisplay => string.IsNullOrWhiteSpace(KontrahentNip) ? "—" : KontrahentNip;
        public int FvCount { get; set; }
        public DateTime? OstatniaFaktura { get; set; }
        public string OstatniaFakturaText => OstatniaFaktura?.ToString("dd.MM.yyyy") ?? "—";

        // === Wolumen Kurczaka żywego za okres ===
        public decimal Kg { get; set; }
        public decimal Wartosc { get; set; }
        public string KgText => Kg >= 1000 ? $"{Kg / 1000m:N1} t" : $"{Kg:N0} kg";
        public string WartoscText => Wartosc >= 1_000_000 ? $"{Wartosc / 1_000_000m:N2} mln zł"
                                   : Wartosc >= 1000      ? $"{Wartosc / 1000m:N0} tys. zł"
                                                          : $"{Wartosc:N0} zł";

        // Skala "wagi" kontrahenta — do wizualnej priorytetyzacji
        public int WolumenLevel => Wartosc switch
        {
            >= 500_000 => 3,   // duży (>500 tys.)
            >= 100_000 => 2,   // średni (100–500 tys.)
            >= 10_000  => 1,   // mały (10–100 tys.)
            _          => 0    // pomijalny
        };
        public Brush WolumenBadgeBg => WolumenLevel switch
        {
            3 => new SolidColorBrush(Color.FromRgb(0x4C, 0x1D, 0x95)), // głęboki fiolet
            2 => new SolidColorBrush(Color.FromRgb(0x6D, 0x28, 0xD9)),
            1 => new SolidColorBrush(Color.FromRgb(0xA7, 0x8B, 0xFA)),
            _ => new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB))
        };
        public Brush WolumenBadgeFg => WolumenLevel >= 1
            ? new SolidColorBrush(Colors.White)
            : new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));

        // === Inteligentna sugestia po NIP — jest dopasowany hodowca o tym samym NIP, niezmapowany? ===
        private DostawcaMapowanie? _sugestiaNip;
        public DostawcaMapowanie? SugestiaNip
        {
            get => _sugestiaNip;
            set { _sugestiaNip = value; OnChanged(); OnChanged(nameof(MaSugestieNip)); OnChanged(nameof(SugestiaNipText)); }
        }
        public bool MaSugestieNip => SugestiaNip != null;
        public string SugestiaNipText => SugestiaNip != null
            ? $"💡 NIP zgodny: {SugestiaNip.ShortName} — kliknij aby przypisać"
            : "";

        // === Staleness — ile dni od ostatniej faktury (im więcej, tym pilniej zmapować) ===
        public int DniOdOstatniej
        {
            get
            {
                if (!OstatniaFaktura.HasValue) return 9999;
                return Math.Max(0, (DateTime.Today - OstatniaFaktura.Value.Date).Days);
            }
        }
        public string DniText => OstatniaFaktura.HasValue ? $"{DniOdOstatniej} dni temu" : "—";

        // Kategoria staleness: 3 = bardzo stare (>180d), 2 = średnie (90-180d), 1 = świeże (30-90d), 0 = nowe (<30d)
        public int StalenessLevel => DniOdOstatniej switch
        {
            > 180 => 3,
            > 90  => 2,
            > 30  => 1,
            _     => 0
        };

        // Delikatne tinty wiersza wg wieku — ciepło dla starych, neutralnie dla świeżych.
        // Wartości w kolejności: bardzo stare → świeże.
        public Brush WierszTlo => StalenessLevel switch
        {
            3 => new SolidColorBrush(Color.FromRgb(0xFE, 0xF2, 0xF2)), // czerwonawy pastel
            2 => new SolidColorBrush(Color.FromRgb(0xFF, 0xF7, 0xED)), // brzoskwiniowy pastel
            1 => new SolidColorBrush(Color.FromRgb(0xFE, 0xFC, 0xE8)), // jasny żółtawy
            _ => new SolidColorBrush(Color.FromRgb(0xF0, 0xFD, 0xF4))  // jasnozielony pastel = świeże
        };

        // Badge dla dni — kontrastujący, ale spokojny.
        public Brush DniBadgeBg => StalenessLevel switch
        {
            3 => new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2)),
            2 => new SolidColorBrush(Color.FromRgb(0xFF, 0xED, 0xD5)),
            1 => new SolidColorBrush(Color.FromRgb(0xFE, 0xF3, 0xC7)),
            _ => new SolidColorBrush(Color.FromRgb(0xD1, 0xFA, 0xE5))
        };
        public Brush DniBadgeFg => StalenessLevel switch
        {
            3 => new SolidColorBrush(Color.FromRgb(0x99, 0x1B, 0x1B)),
            2 => new SolidColorBrush(Color.FromRgb(0x9A, 0x34, 0x12)),
            1 => new SolidColorBrush(Color.FromRgb(0x92, 0x40, 0x0E)),
            _ => new SolidColorBrush(Color.FromRgb(0x06, 0x5F, 0x46))
        };

        // Badge dla liczby faktur — im więcej tym mocniej.
        public Brush FvBadgeBg => FvCount switch
        {
            >= 10 => new SolidColorBrush(Color.FromRgb(0xDD, 0xD6, 0xFE)),
            >= 3  => new SolidColorBrush(Color.FromRgb(0xE0, 0xE7, 0xFF)),
            _     => new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9))
        };
        public Brush FvBadgeFg => FvCount switch
        {
            >= 10 => new SolidColorBrush(Color.FromRgb(0x5B, 0x21, 0xB6)),
            >= 3  => new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0xBE)),
            _     => new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69))
        };

        internal MapowanieDostawcowWindow? Owner { get; set; }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnChanged(); RefreshSuggestions(Owner); }
        }

        public ObservableCollection<DostawcaMapowanie> Suggestions { get; } = new();

        private bool _isPopupOpen;
        public bool IsPopupOpen { get => _isPopupOpen; set { _isPopupOpen = value; OnChanged(); } }

        internal void RefreshSuggestions(MapowanieDostawcowWindow? owner)
        {
            Suggestions.Clear();
            if (owner == null) return;
            var matches = owner.MatchDostawcyLibra(SearchText);
            foreach (var d in matches) Suggestions.Add(d);
            IsPopupOpen = matches.Count > 0;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
