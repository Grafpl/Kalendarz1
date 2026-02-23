using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1.WPF
{
    public partial class HistoriaSnapshotowWindow : Window
    {
        private readonly int _zamowienieId;
        private readonly string _klientNazwa;
        private readonly string _connLibra;
        private readonly string _connHandel;
        private List<TimelineEntry> _timelineEntries = new();
        private bool _suppressCompareChange = false;
        private DateTime? _currentSnapshotDate;

        // Avatar color palette (same as UserAvatarManager.GetColorFromId)
        private static readonly Color[] AvatarColors =
        {
            Color.FromRgb(46, 125, 50),   // Zielony
            Color.FromRgb(25, 118, 210),  // Niebieski
            Color.FromRgb(156, 39, 176),  // Fioletowy
            Color.FromRgb(230, 81, 0),    // Pomarańczowy
            Color.FromRgb(0, 137, 123),   // Teal
            Color.FromRgb(194, 24, 91),   // Różowy
            Color.FromRgb(69, 90, 100),   // Szary
            Color.FromRgb(121, 85, 72)    // Brązowy
        };

        public HistoriaSnapshotowWindow(int zamowienieId, string klientNazwa, string connLibra, string connHandel)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _zamowienieId = zamowienieId;
            _klientNazwa = klientNazwa;
            _connLibra = connLibra;
            _connHandel = connHandel;

            txtTitle.Text = $"HISTORIA ZAMÓWIENIA — {klientNazwa}";
            txtSubtitle.Text = $"Zamówienie #{zamowienieId}";

            _ = LoadTimelineAsync();
        }

        private async Task LoadTimelineAsync()
        {
            _timelineEntries.Clear();

            try
            {
                using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // === SOURCE A: HistoriaZmianZamowien ===
                bool historiaTableExists = false;
                var checkCmd = new SqlCommand("SELECT COUNT(*) FROM sys.objects WHERE name='HistoriaZmianZamowien' AND type='U'", cn);
                historiaTableExists = (int)await checkCmd.ExecuteScalarAsync() > 0;

                if (historiaTableExists)
                {
                    var cmdHist = new SqlCommand(@"
                        SELECT Id, TypZmiany, PoleZmienione, WartoscPoprzednia, WartoscNowa,
                               Uzytkownik, UzytkownikNazwa, DataZmiany, OpisZmiany
                        FROM dbo.HistoriaZmianZamowien
                        WHERE ZamowienieId = @Id
                        ORDER BY DataZmiany DESC", cn);
                    cmdHist.Parameters.AddWithValue("@Id", _zamowienieId);

                    using var rd = await cmdHist.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        var typ = rd.IsDBNull(1) ? "" : rd.GetString(1);
                        var opis = rd.IsDBNull(8) ? "" : rd.GetString(8);
                        var pole = rd.IsDBNull(2) ? "" : rd.GetString(2);
                        var valPrev = rd.IsDBNull(3) ? "" : rd.GetString(3);
                        var valNew = rd.IsDBNull(4) ? "" : rd.GetString(4);
                        var userId = rd.IsDBNull(5) ? "" : rd.GetString(5);
                        var userName = rd.IsDBNull(6) ? "" : rd.GetString(6);
                        var kto = !string.IsNullOrEmpty(userName) ? userName : userId;
                        var data = rd.GetDateTime(7);

                        bool isNote = pole.Equals("Notatka", StringComparison.OrdinalIgnoreCase) ||
                                      pole.Equals("Uwagi", StringComparison.OrdinalIgnoreCase);

                        string mappedTyp = isNote ? "NOTATKA" : MapTypZmiany(typ);

                        if (string.IsNullOrEmpty(opis) && !string.IsNullOrEmpty(pole))
                            opis = $"{pole}: {valPrev} → {valNew}";

                        var entry = CreateEntry(
                            data: data,
                            typ: mappedTyp,
                            kto: kto,
                            ktoId: userId,
                            opis: string.IsNullOrEmpty(opis) ? typ : opis,
                            wartoscPoprzednia: valPrev,
                            wartoscNowa: valNew,
                            snapshotDate: null
                        );
                        _timelineEntries.Add(entry);
                    }
                }

                // === SOURCE B: ZamowieniaMiesoSnapshot ===
                var checkSnap = new SqlCommand("SELECT COUNT(*) FROM sys.objects WHERE name='ZamowieniaMiesoSnapshot' AND type='U'", cn);
                bool snapshotTableExists = (int)await checkSnap.ExecuteScalarAsync() > 0;

                if (snapshotTableExists)
                {
                    var cmdSnap = new SqlCommand(@"
                        SELECT DISTINCT DataSnapshotu, TypSnapshotu
                        FROM dbo.ZamowieniaMiesoSnapshot
                        WHERE ZamowienieId = @Id
                        ORDER BY DataSnapshotu DESC", cn);
                    cmdSnap.Parameters.AddWithValue("@Id", _zamowienieId);

                    using var rdSnap = await cmdSnap.ExecuteReaderAsync();
                    while (await rdSnap.ReadAsync())
                    {
                        var dataSnap = rdSnap.GetDateTime(0);
                        var typSnap = rdSnap.GetString(1);

                        var entry = CreateEntry(
                            data: dataSnap,
                            typ: $"SNAPSHOT ({typSnap})",
                            kto: "System",
                            ktoId: "SYSTEM",
                            opis: $"Stan zamówienia zapisany",
                            snapshotDate: dataSnap
                        );
                        _timelineEntries.Add(entry);
                    }
                }

                // === SOURCE C: ZamowieniaMieso (status dates) ===
                await LoadStatusEntriesAsync(cn);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad ladowania osi czasu: {ex.Message}");
            }

            _timelineEntries = _timelineEntries.OrderByDescending(e => e.Data).ToList();

            lstTimeline.ItemsSource = null;
            lstTimeline.ItemsSource = _timelineEntries;

            if (!_timelineEntries.Any())
            {
                txtEmptyState.Text = "Brak historii dla tego zamówienia";
            }
        }

        private async Task LoadStatusEntriesAsync(SqlConnection cn)
        {
            try
            {
                var existingCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var cmdCols = new SqlCommand(
                    "SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.ZamowieniaMieso') AND name IN " +
                    "('DataRealizacji','KtoRealizowal','KtoRealizowaNazwa','DataWydania','KtoWydal','KtoWydalNazwa','DataAnulowania','DataUtworzenia')", cn);
                using (var rdCols = await cmdCols.ExecuteReaderAsync())
                {
                    while (await rdCols.ReadAsync())
                        existingCols.Add(rdCols.GetString(0));
                }

                var cols = new List<string>();
                if (existingCols.Contains("DataUtworzenia")) cols.Add("DataUtworzenia"); else cols.Add("NULL AS DataUtworzenia");
                if (existingCols.Contains("DataRealizacji")) cols.Add("DataRealizacji"); else cols.Add("NULL AS DataRealizacji");
                if (existingCols.Contains("KtoRealizowal")) cols.Add("KtoRealizowal"); else cols.Add("NULL AS KtoRealizowal");
                if (existingCols.Contains("KtoRealizowaNazwa")) cols.Add("KtoRealizowaNazwa"); else cols.Add("NULL AS KtoRealizowaNazwa");
                if (existingCols.Contains("DataWydania")) cols.Add("DataWydania"); else cols.Add("NULL AS DataWydania");
                if (existingCols.Contains("KtoWydal")) cols.Add("KtoWydal"); else cols.Add("NULL AS KtoWydal");
                if (existingCols.Contains("KtoWydalNazwa")) cols.Add("KtoWydalNazwa"); else cols.Add("NULL AS KtoWydalNazwa");
                if (existingCols.Contains("DataAnulowania")) cols.Add("DataAnulowania"); else cols.Add("NULL AS DataAnulowania");

                var sql = $"SELECT {string.Join(", ", cols)} FROM dbo.ZamowieniaMieso WHERE Id = @Id";
                var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Id", _zamowienieId);

                using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    if (!rd.IsDBNull(1))
                    {
                        var dataReal = rd.GetDateTime(1);
                        var ktoId = rd.IsDBNull(2) ? "" : rd.GetString(2);
                        var ktoNazwa = rd.IsDBNull(3) ? "" : rd.GetString(3);
                        var kto = !string.IsNullOrEmpty(ktoNazwa) ? ktoNazwa : ktoId;

                        if (!_timelineEntries.Any(e => e.Typ == "REALIZACJA" && Math.Abs((e.Data - dataReal).TotalMinutes) < 2))
                        {
                            _timelineEntries.Add(CreateEntry(
                                data: dataReal,
                                typ: "REALIZACJA",
                                kto: string.IsNullOrEmpty(kto) ? "System" : kto,
                                ktoId: string.IsNullOrEmpty(ktoId) ? "SYSTEM" : ktoId,
                                opis: "Zamówienie zrealizowane"
                            ));
                        }
                    }

                    if (!rd.IsDBNull(4))
                    {
                        var dataWyd = rd.GetDateTime(4);
                        var ktoId = rd.IsDBNull(5) ? "" : rd.GetString(5);
                        var ktoNazwa = rd.IsDBNull(6) ? "" : rd.GetString(6);
                        var kto = !string.IsNullOrEmpty(ktoNazwa) ? ktoNazwa : ktoId;

                        if (!_timelineEntries.Any(e => e.Typ == "WYDANIE" && Math.Abs((e.Data - dataWyd).TotalMinutes) < 2))
                        {
                            _timelineEntries.Add(CreateEntry(
                                data: dataWyd,
                                typ: "WYDANIE",
                                kto: string.IsNullOrEmpty(kto) ? "System" : kto,
                                ktoId: string.IsNullOrEmpty(ktoId) ? "SYSTEM" : ktoId,
                                opis: "Wydano towar"
                            ));
                        }
                    }

                    if (!rd.IsDBNull(7))
                    {
                        var dataAnul = rd.GetDateTime(7);
                        if (!_timelineEntries.Any(e => e.Typ == "ANULOWANIE" && Math.Abs((e.Data - dataAnul).TotalMinutes) < 2))
                        {
                            _timelineEntries.Add(CreateEntry(
                                data: dataAnul,
                                typ: "ANULOWANIE",
                                kto: "System",
                                ktoId: "SYSTEM",
                                opis: "Zamówienie anulowane"
                            ));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad ladowania statusow: {ex.Message}");
            }
        }

        private async void lstTimeline_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstTimeline.SelectedItem is not TimelineEntry entry) return;

            HideAllPanels();

            if (entry.SnapshotDate.HasValue)
            {
                _currentSnapshotDate = entry.SnapshotDate.Value;
                dgvSnapshotDetails.Visibility = Visibility.Visible;
                txtDetailsHeader.Text = $"Snapshot: {entry.Typ} — {entry.Data:dd.MM.yyyy HH:mm}";
                await LoadSnapshotDetailsAsync(entry.SnapshotDate.Value);
                PopulateCompareCombo(entry.SnapshotDate.Value);
            }
            else if (entry.Typ == "NOTATKA")
            {
                _currentSnapshotDate = null;
                pnlCompare.Visibility = Visibility.Collapsed;
                svNoteDetails.Visibility = Visibility.Visible;
                txtDetailsHeader.Text = $"NOTATKA — {entry.Kto} — {entry.Data:dd.MM.yyyy HH:mm}";
                txtNoteBefore.Text = string.IsNullOrEmpty(entry.WartoscPoprzednia) ? "(brak)" : entry.WartoscPoprzednia;
                txtNoteAfter.Text = string.IsNullOrEmpty(entry.WartoscNowa) ? "(brak)" : entry.WartoscNowa;
            }
            else
            {
                _currentSnapshotDate = null;
                pnlCompare.Visibility = Visibility.Collapsed;
                svChangeDetails.Visibility = Visibility.Visible;
                txtDetailsHeader.Text = $"{entry.Typ} — {entry.Data:dd.MM.yyyy HH:mm}";

                var details = $"Typ: {entry.Typ}\n" +
                              $"Data: {entry.Data:dd.MM.yyyy HH:mm:ss}\n" +
                              $"Uzytkownik: {entry.Kto}\n\n" +
                              $"{entry.Opis}";

                if (!string.IsNullOrEmpty(entry.OpisSzczegol))
                    details += $"\n\nSzczegoly:\n{entry.OpisSzczegol}";

                txtChangeDetails.Text = details;
            }
        }

        private void PopulateCompareCombo(DateTime currentSnapshotDate)
        {
            var otherSnapshots = _timelineEntries
                .Where(e => e.SnapshotDate.HasValue && e.SnapshotDate.Value != currentSnapshotDate)
                .Select(e => new SnapshotComboItem { Data = e.SnapshotDate!.Value, Display = $"{e.Typ} — {e.Data:dd.MM.yyyy HH:mm}" })
                .ToList();

            if (otherSnapshots.Any())
            {
                _suppressCompareChange = true;
                cmbCompareSnapshot.ItemsSource = otherSnapshots;
                cmbCompareSnapshot.DisplayMemberPath = "Display";
                cmbCompareSnapshot.SelectedIndex = -1;
                _suppressCompareChange = false;
                pnlCompare.Visibility = Visibility.Visible;
                btnClearCompare.Visibility = Visibility.Collapsed;
            }
            else
            {
                pnlCompare.Visibility = Visibility.Collapsed;
            }
        }

        private async void cmbCompareSnapshot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressCompareChange) return;
            if (cmbCompareSnapshot.SelectedItem is not SnapshotComboItem selected) return;
            if (!_currentSnapshotDate.HasValue) return;

            btnClearCompare.Visibility = Visibility.Visible;
            txtDetailsHeader.Text = $"POROWNANIE SNAPSHOTOW";
            dgvSnapshotDetails.Visibility = Visibility.Collapsed;
            svDiffView.Visibility = Visibility.Visible;

            await LoadDiffAsync(_currentSnapshotDate.Value, selected.Data);
        }

        private void btnClearCompare_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentSnapshotDate.HasValue) return;

            _suppressCompareChange = true;
            cmbCompareSnapshot.SelectedIndex = -1;
            _suppressCompareChange = false;
            btnClearCompare.Visibility = Visibility.Collapsed;

            svDiffView.Visibility = Visibility.Collapsed;
            dgvSnapshotDetails.Visibility = Visibility.Visible;

            var entry = _timelineEntries.FirstOrDefault(x => x.SnapshotDate == _currentSnapshotDate.Value);
            if (entry != null)
                txtDetailsHeader.Text = $"Snapshot: {entry.Typ} — {entry.Data:dd.MM.yyyy HH:mm}";
        }

        private async Task LoadDiffAsync(DateTime dateA, DateTime dateB)
        {
            var itemsA = await LoadSnapshotRawAsync(dateA);
            var itemsB = await LoadSnapshotRawAsync(dateB);

            // Determine which is older/newer
            bool aIsOlder = dateA < dateB;
            var older = aIsOlder ? itemsA : itemsB;
            var newer = aIsOlder ? itemsB : itemsA;
            var olderDate = aIsOlder ? dateA : dateB;
            var newerDate = aIsOlder ? dateB : dateA;

            txtDetailsHeader.Text = $"POROWNANIE: {olderDate:dd.MM HH:mm} → {newerDate:dd.MM HH:mm}";

            var allKody = older.Keys.Union(newer.Keys).OrderBy(k => k).ToList();
            var productNames = await LoadProductNamesAsync(allKody);

            var diffItems = new List<DiffItem>();

            foreach (var kod in allKody)
            {
                bool inOld = older.ContainsKey(kod);
                bool inNew = newer.ContainsKey(kod);
                var name = productNames.TryGetValue(kod, out var n) ? n : $"Towar #{kod}";

                if (inOld && inNew)
                {
                    var oldItem = older[kod];
                    var newItem = newer[kod];
                    var delta = newItem.Ilosc - oldItem.Ilosc;
                    var flags = BuildFlagChanges(oldItem, newItem);
                    bool hasAnyChange = delta != 0 || !string.IsNullOrEmpty(flags);

                    diffItems.Add(new DiffItem
                    {
                        Produkt = name,
                        StatusIcon = !hasAnyChange ? "=" : (delta > 0 ? "▲" : (delta < 0 ? "▼" : "~")),
                        StatusColor = !hasAnyChange
                            ? new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66))
                            : (delta > 0 ? new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71))
                                         : (delta < 0 ? new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C))
                                                       : new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12)))),
                        IloscA = oldItem.Ilosc,
                        IloscB = newItem.Ilosc,
                        Delta = delta,
                        FlagChanges = flags,
                        HasChanged = hasAnyChange,
                        RowBackground = !hasAnyChange
                            ? new SolidColorBrush(Color.FromRgb(0x2D, 0x2F, 0x3E))
                            : new SolidColorBrush(Color.FromArgb(25,
                                delta != 0 ? (delta > 0 ? (byte)0x2E : (byte)0xE7) : (byte)0xF3,
                                delta != 0 ? (delta > 0 ? (byte)0xCC : (byte)0x4C) : (byte)0x9C,
                                delta != 0 ? (delta > 0 ? (byte)0x71 : (byte)0x3C) : (byte)0x12))
                    });
                }
                else if (inNew && !inOld)
                {
                    var item = newer[kod];
                    diffItems.Add(new DiffItem
                    {
                        Produkt = name,
                        StatusIcon = "+",
                        StatusColor = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60)),
                        IloscA = 0,
                        IloscB = item.Ilosc,
                        Delta = item.Ilosc,
                        FlagChanges = BuildFlagSummary(item),
                        HasChanged = true,
                        RowBackground = new SolidColorBrush(Color.FromArgb(25, 0x27, 0xAE, 0x60))
                    });
                }
                else if (inOld && !inNew)
                {
                    var item = older[kod];
                    diffItems.Add(new DiffItem
                    {
                        Produkt = name,
                        StatusIcon = "✗",
                        StatusColor = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)),
                        IloscA = item.Ilosc,
                        IloscB = 0,
                        Delta = -item.Ilosc,
                        FlagChanges = BuildFlagSummary(item),
                        HasChanged = true,
                        RowBackground = new SolidColorBrush(Color.FromArgb(25, 0xE7, 0x4C, 0x3C))
                    });
                }
            }

            // Sort: changes first, then unchanged
            diffItems = diffItems
                .OrderByDescending(d => d.HasChanged)
                .ThenBy(d => d.Produkt)
                .ToList();

            icDiffItems.ItemsSource = diffItems;
        }

        private async Task<Dictionary<int, SnapshotRawItem>> LoadSnapshotRawAsync(DateTime dataSnapshotu)
        {
            var result = new Dictionary<int, SnapshotRawItem>();
            try
            {
                using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                bool hasE2 = false, hasStrefa = false;
                var cmdCols = new SqlCommand(@"SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.ZamowieniaMiesoSnapshot') AND name IN ('E2','Strefa')", cn);
                using (var rdCols = await cmdCols.ExecuteReaderAsync())
                {
                    while (await rdCols.ReadAsync())
                    {
                        if (rdCols.GetString(0) == "E2") hasE2 = true;
                        if (rdCols.GetString(0) == "Strefa") hasStrefa = true;
                    }
                }

                string e2Col = hasE2 ? ", ISNULL(E2, 0)" : ", CAST(0 AS BIT)";
                string strefaCol = hasStrefa ? ", ISNULL(Strefa, 0)" : ", CAST(0 AS BIT)";

                var cmd = new SqlCommand($@"
                    SELECT KodTowaru, Ilosc, ISNULL(Folia, 0), ISNULL(Hallal, 0){e2Col}{strefaCol}
                    FROM dbo.ZamowieniaMiesoSnapshot
                    WHERE ZamowienieId = @Id AND DataSnapshotu = @Data AND Ilosc > 0", cn);
                cmd.Parameters.AddWithValue("@Id", _zamowienieId);
                cmd.Parameters.AddWithValue("@Data", dataSnapshotu);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var kod = rd.GetInt32(0);
                    result[kod] = new SnapshotRawItem
                    {
                        KodTowaru = kod,
                        Ilosc = rd.GetDecimal(1),
                        Folia = rd.GetBoolean(2),
                        Halal = rd.GetBoolean(3),
                        E2 = rd.GetBoolean(4),
                        Strefa = rd.GetBoolean(5)
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad ladowania snapshotu raw: {ex.Message}");
            }
            return result;
        }

        private async Task<Dictionary<int, string>> LoadProductNamesAsync(List<int> kodTowary)
        {
            var names = new Dictionary<int, string>();
            if (!kodTowary.Any()) return names;
            try
            {
                using var cnHandel = new SqlConnection(_connHandel);
                await cnHandel.OpenAsync();
                var cmd = new SqlCommand($"SELECT ID, kod FROM HM.TW WHERE ID IN ({string.Join(',', kodTowary)})", cnHandel);
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                    names[rd.GetInt32(0)] = rd.GetString(1);
            }
            catch { /* Handel unavailable */ }
            return names;
        }

        private void HideAllPanels()
        {
            txtEmptyState.Visibility = Visibility.Collapsed;
            dgvSnapshotDetails.Visibility = Visibility.Collapsed;
            svChangeDetails.Visibility = Visibility.Collapsed;
            svNoteDetails.Visibility = Visibility.Collapsed;
            svDiffView.Visibility = Visibility.Collapsed;
        }

        private async Task LoadSnapshotDetailsAsync(DateTime dataSnapshotu)
        {
            var items = new List<SnapshotProductItem>();

            try
            {
                using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                bool hasE2 = false, hasStrefa = false;
                var cmdCols = new SqlCommand(@"SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.ZamowieniaMiesoSnapshot') AND name IN ('E2','Strefa')", cn);
                using (var rdCols = await cmdCols.ExecuteReaderAsync())
                {
                    while (await rdCols.ReadAsync())
                    {
                        if (rdCols.GetString(0) == "E2") hasE2 = true;
                        if (rdCols.GetString(0) == "Strefa") hasStrefa = true;
                    }
                }

                string e2Col = hasE2 ? ", ISNULL(s.E2, 0)" : ", CAST(0 AS BIT)";
                string strefaCol = hasStrefa ? ", ISNULL(s.Strefa, 0)" : ", CAST(0 AS BIT)";

                var cmd = new SqlCommand($@"
                    SELECT s.KodTowaru, s.Ilosc, ISNULL(s.Folia, 0), ISNULL(s.Hallal, 0){e2Col}{strefaCol}
                    FROM dbo.ZamowieniaMiesoSnapshot s
                    WHERE s.ZamowienieId = @Id AND s.DataSnapshotu = @Data AND s.Ilosc > 0
                    ORDER BY s.KodTowaru", cn);
                cmd.Parameters.AddWithValue("@Id", _zamowienieId);
                cmd.Parameters.AddWithValue("@Data", dataSnapshotu);

                var kodTowary = new List<int>();
                var rawItems = new List<(int KodTowaru, decimal Ilosc, bool Folia, bool Halal, bool E2, bool Strefa)>();

                using (var rd = await cmd.ExecuteReaderAsync())
                {
                    while (await rd.ReadAsync())
                    {
                        var kodTowaru = rd.GetInt32(0);
                        kodTowary.Add(kodTowaru);
                        rawItems.Add((kodTowaru, rd.GetDecimal(1), rd.GetBoolean(2), rd.GetBoolean(3), rd.GetBoolean(4), rd.GetBoolean(5)));
                    }
                }

                var productNames = new Dictionary<int, string>();
                if (kodTowary.Any())
                {
                    try
                    {
                        using var cnHandel = new SqlConnection(_connHandel);
                        await cnHandel.OpenAsync();
                        var cmdNames = new SqlCommand($"SELECT ID, kod FROM HM.TW WHERE ID IN ({string.Join(',', kodTowary)})", cnHandel);
                        using var rdNames = await cmdNames.ExecuteReaderAsync();
                        while (await rdNames.ReadAsync())
                            productNames[rdNames.GetInt32(0)] = rdNames.GetString(1);
                    }
                    catch { /* Handel unavailable */ }
                }

                foreach (var raw in rawItems)
                {
                    items.Add(new SnapshotProductItem
                    {
                        KodTowaru = raw.KodTowaru,
                        Produkt = productNames.TryGetValue(raw.KodTowaru, out var name) ? name : $"Towar #{raw.KodTowaru}",
                        Ilosc = raw.Ilosc,
                        Folia = raw.Folia,
                        Halal = raw.Halal,
                        E2 = raw.E2,
                        Strefa = raw.Strefa
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad ladowania snapshotu: {ex.Message}");
            }

            dgvSnapshotDetails.ItemsSource = items;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();

        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            lstTimeline.SelectedItem = null;
            _currentSnapshotDate = null;
            HideAllPanels();
            pnlCompare.Visibility = Visibility.Collapsed;
            txtEmptyState.Visibility = Visibility.Visible;
            txtEmptyState.Text = "Kliknij element na osi czasu, aby zobaczyc szczegoly";
            txtDetailsHeader.Text = "Wybierz element z osi czasu";
            await LoadTimelineAsync();
        }

        #region Helpers

        private TimelineEntry CreateEntry(DateTime data, string typ, string kto, string ktoId = "",
            string opis = "", string opisSzczegol = "",
            string wartoscPoprzednia = "", string wartoscNowa = "",
            DateTime? snapshotDate = null)
        {
            return new TimelineEntry
            {
                Data = data,
                Typ = typ,
                Kto = string.IsNullOrEmpty(kto) ? "System" : kto,
                KtoId = ktoId,
                Opis = opis,
                OpisSzczegol = opisSzczegol,
                Kolor = GetBadgeBrush(typ),
                KolorTekst = new SolidColorBrush(Colors.White),
                Ikona = GetIconForTyp(typ),
                Inicjaly = GetInitials(string.IsNullOrEmpty(kto) ? "System" : kto),
                AvatarKolor = GetAvatarBrush(ktoId),
                SnapshotDate = snapshotDate,
                WartoscPoprzednia = wartoscPoprzednia,
                WartoscNowa = wartoscNowa
            };
        }

        private static string MapTypZmiany(string typ)
        {
            if (string.IsNullOrEmpty(typ)) return "ZMIANA";
            var upper = typ.ToUpperInvariant();
            if (upper.Contains("UTWORZ") || upper.Contains("NOWE")) return "UTWORZENIE";
            if (upper.Contains("EDYCJA") || upper.Contains("ZMIANA") || upper.Contains("MODYFIKACJA")) return "EDYCJA";
            if (upper.Contains("ANULO")) return "ANULOWANIE";
            if (upper.Contains("PRZYWRO")) return "PRZYWROCENIE";
            if (upper.Contains("AKCEPTACJA") || upper.Contains("REALIZ")) return "REALIZACJA";
            if (upper.Contains("WYDANIE") || upper.Contains("WYDANO")) return "WYDANIE";
            return typ.ToUpperInvariant();
        }

        private static Brush GetBadgeBrush(string typ)
        {
            if (string.IsNullOrEmpty(typ)) return new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12));
            var upper = typ.ToUpperInvariant();
            if (upper.Contains("UTWORZ")) return new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
            if (upper.Contains("EDYCJA")) return new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12));
            if (upper.Contains("NOTATKA")) return new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB));
            if (upper.Contains("ANULO")) return new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));
            if (upper.Contains("PRZYWRO")) return new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
            if (upper.Contains("SNAPSHOT")) return new SolidColorBrush(Color.FromRgb(0x9B, 0x59, 0xB6));
            if (upper.Contains("REALIZACJA")) return new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71));
            if (upper.Contains("WYDANIE")) return new SolidColorBrush(Color.FromRgb(0x1A, 0xBC, 0x9C));
            return new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB));
        }

        private static string GetIconForTyp(string typ)
        {
            if (string.IsNullOrEmpty(typ)) return "?";
            var upper = typ.ToUpperInvariant();
            if (upper.Contains("UTWORZ")) return "+";
            if (upper.Contains("EDYCJA")) return "E";
            if (upper.Contains("NOTATKA")) return "N";
            if (upper.Contains("ANULO")) return "X";
            if (upper.Contains("PRZYWRO")) return "R";
            if (upper.Contains("SNAPSHOT")) return "S";
            if (upper.Contains("REALIZACJA")) return "A";
            if (upper.Contains("WYDANIE")) return "W";
            return "?";
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpper();
            return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
        }

        private static string BuildFlagChanges(SnapshotRawItem oldItem, SnapshotRawItem newItem)
        {
            var changes = new List<string>();
            if (oldItem.Folia != newItem.Folia)
                changes.Add($"Folia: {(oldItem.Folia ? "V" : "✗")} → {(newItem.Folia ? "V" : "✗")}");
            if (oldItem.Halal != newItem.Halal)
                changes.Add($"Halal: {(oldItem.Halal ? "V" : "✗")} → {(newItem.Halal ? "V" : "✗")}");
            if (oldItem.E2 != newItem.E2)
                changes.Add($"E2: {(oldItem.E2 ? "V" : "✗")} → {(newItem.E2 ? "V" : "✗")}");
            if (oldItem.Strefa != newItem.Strefa)
                changes.Add($"Strefa: {(oldItem.Strefa ? "V" : "✗")} → {(newItem.Strefa ? "V" : "✗")}");
            return string.Join("  |  ", changes);
        }

        private static string BuildFlagSummary(SnapshotRawItem item)
        {
            var flags = new List<string>();
            if (item.Folia) flags.Add("Folia");
            if (item.Halal) flags.Add("Halal");
            if (item.E2) flags.Add("E2");
            if (item.Strefa) flags.Add("Strefa");
            return flags.Any() ? string.Join(", ", flags) : "";
        }

        private static Brush GetAvatarBrush(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return new SolidColorBrush(Color.FromRgb(69, 90, 100));
            int hash = userId.GetHashCode();
            var color = AvatarColors[Math.Abs(hash) % AvatarColors.Length];
            return new SolidColorBrush(color);
        }

        #endregion

        #region Data Classes

        public class TimelineEntry
        {
            public DateTime Data { get; set; }
            public string DataDisplay => Data.ToString("dd.MM HH:mm");
            public string Typ { get; set; } = "";
            public string Kto { get; set; } = "";
            public string KtoId { get; set; } = "";
            public string Opis { get; set; } = "";
            public string OpisSzczegol { get; set; } = "";
            public Brush Kolor { get; set; } = Brushes.Gray;
            public Brush KolorTekst { get; set; } = Brushes.White;
            public string Ikona { get; set; } = "?";
            public string Inicjaly { get; set; } = "?";
            public Brush AvatarKolor { get; set; } = Brushes.Gray;
            public DateTime? SnapshotDate { get; set; }
            public string WartoscPoprzednia { get; set; } = "";
            public string WartoscNowa { get; set; } = "";
        }

        public class SnapshotProductItem
        {
            public int KodTowaru { get; set; }
            public string Produkt { get; set; } = "";
            public decimal Ilosc { get; set; }
            public bool Folia { get; set; }
            public bool Halal { get; set; }
            public bool E2 { get; set; }
            public bool Strefa { get; set; }

            public string FoliaDisplay => Folia ? "V" : "";
            public string HalalDisplay => Halal ? "V" : "";
            public string E2Display => E2 ? "V" : "";
            public string StrefaDisplay => Strefa ? "V" : "";
        }

        private class SnapshotRawItem
        {
            public int KodTowaru { get; set; }
            public decimal Ilosc { get; set; }
            public bool Folia { get; set; }
            public bool Halal { get; set; }
            public bool E2 { get; set; }
            public bool Strefa { get; set; }
        }

        private class SnapshotComboItem
        {
            public DateTime Data { get; set; }
            public string Display { get; set; } = "";
        }

        public class DiffItem
        {
            public string Produkt { get; set; } = "";
            public string StatusIcon { get; set; } = "";
            public Brush StatusColor { get; set; } = Brushes.Gray;
            public decimal IloscA { get; set; }
            public decimal IloscB { get; set; }
            public decimal Delta { get; set; }
            public string FlagChanges { get; set; } = "";
            public bool HasChanged { get; set; }
            public Brush RowBackground { get; set; } = Brushes.Transparent;

            public bool HasFlagChanges => !string.IsNullOrEmpty(FlagChanges);
            public string IloscADisplay => IloscA > 0 ? $"{IloscA:N0} kg" : "—";
            public string IloscBDisplay => IloscB > 0 ? $"{IloscB:N0} kg" : "—";
            public string DeltaDisplay
            {
                get
                {
                    if (Delta == 0) return "";
                    return Delta > 0 ? $"+{Delta:N0} kg" : $"{Delta:N0} kg";
                }
            }
            public Brush DeltaColor => Delta >= 0
                ? new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71))
                : new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));
        }

        #endregion
    }
}
