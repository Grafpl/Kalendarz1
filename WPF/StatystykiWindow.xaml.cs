using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.WPF
{
    /// <summary>
    /// 🔍 Transparentność klientów — anulowania + redukcje pozycji (kg w dół)
    /// per odbiorca, ze scoringiem wiarygodności i osią ostatnich incydentów.
    /// Anulowania: ZamowieniaMieso (Status='Anulowane').
    /// Redukcje: HistoriaZmianZamowien (TypZmiany='EDYCJA', PoleZmienione 'Pozycja: {towar} - Zam.', nowa &lt; stara).
    /// </summary>
    public partial class StatystykiWindow : Window
    {
        private readonly string _connLibra;
        private readonly string _connHandel;

        private readonly DataTable _dtKlienci = new();
        private readonly DataTable _dtPrzyczyny = new();
        private readonly DataTable _dtIncydenty = new();

        // Wszystkie incydenty okresu — filtr per klient liczony w pamięci
        private List<Incydent> _incydenty = new();
        private bool _isLoading;

        private record Incydent(DateTime Data, int KlientId, string Typ, string Klient, string Opis, string Kto);

        public StatystykiWindow(string connLibra, string connHandel)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _connLibra = connLibra;
            _connHandel = connHandel;

            InitializeDataTables();
            InitializeDates();
            _ = LoadHandlowcyAsync();
        }

        // ════════════════════════════════════ INIT ════════════════════════════════════

        private void InitializeDataTables()
        {
            _dtKlienci.Columns.Add("KlientId", typeof(int));
            _dtKlienci.Columns.Add("Odbiorca", typeof(string));
            _dtKlienci.Columns.Add("Handlowiec", typeof(string));
            _dtKlienci.Columns.Add("Zamowien", typeof(int));
            _dtKlienci.Columns.Add("Anulowane", typeof(int));
            _dtKlienci.Columns.Add("ProcAnul", typeof(double));
            _dtKlienci.Columns.Add("KgAnul", typeof(decimal));
            _dtKlienci.Columns.Add("Redukcje", typeof(int));
            _dtKlienci.Columns.Add("KgRed", typeof(decimal));
            _dtKlienci.Columns.Add("Przesun", typeof(int));
            _dtKlienci.Columns.Add("NiedoborKg", typeof(decimal));
            _dtKlienci.Columns.Add("Rekl", typeof(int));
            _dtKlienci.Columns.Add("OstatniIncydent", typeof(DateTime));
            _dtKlienci.Columns.Add("Score", typeof(int));
            dgKlienci.ItemsSource = _dtKlienci.DefaultView;
            SetupKlienciGrid();

            _dtPrzyczyny.Columns.Add("Przyczyna", typeof(string));
            _dtPrzyczyny.Columns.Add("Liczba", typeof(int));
            _dtPrzyczyny.Columns.Add("Procent", typeof(string));
            dgPrzyczyny.ItemsSource = _dtPrzyczyny.DefaultView;
            SetupPrzyczynyGrid();

            _dtIncydenty.Columns.Add("Data", typeof(DateTime));
            _dtIncydenty.Columns.Add("KlientId", typeof(int));
            _dtIncydenty.Columns.Add("Typ", typeof(string));
            _dtIncydenty.Columns.Add("Klient", typeof(string));
            _dtIncydenty.Columns.Add("Opis", typeof(string));
            _dtIncydenty.Columns.Add("Kto", typeof(string));
            dgIncydenty.ItemsSource = _dtIncydenty.DefaultView;
            SetupIncydentyGrid();
        }

        private static Style CellStyle(HorizontalAlignment ha, bool bold = false, Color? fg = null, double? fs = null)
        {
            var s = new Style(typeof(System.Windows.Controls.TextBlock));
            s.Setters.Add(new Setter(System.Windows.Controls.TextBlock.HorizontalAlignmentProperty, ha));
            s.Setters.Add(new Setter(System.Windows.Controls.TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            if (ha == HorizontalAlignment.Right)
                s.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 6, 0)));
            if (bold) s.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontWeightProperty, FontWeights.Bold));
            if (fg.HasValue) s.Setters.Add(new Setter(System.Windows.Controls.TextBlock.ForegroundProperty, new SolidColorBrush(fg.Value)));
            if (fs.HasValue) s.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontSizeProperty, fs.Value));
            return s;
        }

        private void SetupKlienciGrid()
        {
            dgKlienci.Columns.Clear();

            dgKlienci.Columns.Add(new DataGridTextColumn
            {
                Header = "Odbiorca",
                Binding = new Binding("Odbiorca"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 170
            });
            dgKlienci.Columns.Add(new DataGridTextColumn
            {
                Header = "Handlowiec",
                Binding = new Binding("Handlowiec"),
                Width = new DataGridLength(96)
            });
            dgKlienci.Columns.Add(new DataGridTextColumn
            {
                Header = "Zam.",
                Binding = new Binding("Zamowien"),
                Width = new DataGridLength(54),
                ElementStyle = CellStyle(HorizontalAlignment.Center)
            });
            dgKlienci.Columns.Add(new DataGridTextColumn
            {
                Header = "Anul.",
                Binding = new Binding("Anulowane"),
                Width = new DataGridLength(58),
                ElementStyle = CellStyle(HorizontalAlignment.Center, bold: true, fg: Color.FromRgb(0xC0, 0x39, 0x2B), fs: 12.5)
            });
            dgKlienci.Columns.Add(new DataGridTextColumn
            {
                Header = "% anul.",
                Binding = new Binding("ProcAnul") { StringFormat = "{0:F1}%" },
                Width = new DataGridLength(66),
                ElementStyle = CellStyle(HorizontalAlignment.Center, fg: Color.FromRgb(0xC0, 0x39, 0x2B))
            });
            dgKlienci.Columns.Add(new DataGridTextColumn
            {
                Header = "Kg anul.",
                Binding = new Binding("KgAnul") { StringFormat = "N0" },
                Width = new DataGridLength(82),
                ElementStyle = CellStyle(HorizontalAlignment.Right)
            });
            dgKlienci.Columns.Add(new DataGridTextColumn
            {
                Header = "Reduk.",
                Binding = new Binding("Redukcje"),
                Width = new DataGridLength(60),
                ElementStyle = CellStyle(HorizontalAlignment.Center, bold: true, fg: Color.FromRgb(0xD3, 0x54, 0x00), fs: 12.5)
            });
            dgKlienci.Columns.Add(new DataGridTextColumn
            {
                Header = "Kg −",
                Binding = new Binding("KgRed") { StringFormat = "N0" },
                Width = new DataGridLength(70),
                ElementStyle = CellStyle(HorizontalAlignment.Right, fg: Color.FromRgb(0xD3, 0x54, 0x00))
            });
            dgKlienci.Columns.Add(new DataGridTextColumn
            {
                Header = "🔁 Termin",
                Binding = new Binding("Przesun"),
                Width = new DataGridLength(62),
                ElementStyle = CellStyle(HorizontalAlignment.Center, fg: Color.FromRgb(0x8E, 0x44, 0xAD))
            });
            dgKlienci.Columns.Add(new DataGridTextColumn
            {
                Header = "📦 Niedob.",
                Binding = new Binding("NiedoborKg") { StringFormat = "N0" },
                Width = new DataGridLength(72),
                ElementStyle = CellStyle(HorizontalAlignment.Right, fg: Color.FromRgb(0x2E, 0x86, 0xC1))
            });
            dgKlienci.Columns.Add(new DataGridTextColumn
            {
                Header = "⚠ Rekl.",
                Binding = new Binding("Rekl"),
                Width = new DataGridLength(56),
                ElementStyle = CellStyle(HorizontalAlignment.Center, bold: true, fg: Color.FromRgb(0xB0, 0x3A, 0x2E))
            });
            dgKlienci.Columns.Add(new DataGridTextColumn
            {
                Header = "Ostatni",
                Binding = new Binding("OstatniIncydent") { StringFormat = "yyyy-MM-dd" },
                Width = new DataGridLength(86),
                ElementStyle = CellStyle(HorizontalAlignment.Center)
            });
            dgKlienci.Columns.Add(new DataGridTextColumn
            {
                Header = "Wiarygodność",
                Binding = new Binding("Score") { StringFormat = "{0}/100" },
                Width = new DataGridLength(92),
                ElementStyle = CellStyle(HorizontalAlignment.Center, bold: true, fs: 12.5)
            });

            dgKlienci.LoadingRow += DgKlienci_LoadingRow;
        }

        private void DgKlienci_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is not DataRowView rowView) return;
            int score = rowView.Row.Field<int>("Score");

            if (score < 60)
            {
                e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 221, 221));
                e.Row.FontWeight = FontWeights.SemiBold;
            }
            else if (score < 85)
                e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 246, 214));
            else
                e.Row.Background = Brushes.White;
        }

        private void SetupPrzyczynyGrid()
        {
            dgPrzyczyny.Columns.Clear();
            dgPrzyczyny.Columns.Add(new DataGridTextColumn
            {
                Header = "Przyczyna",
                Binding = new Binding("Przyczyna"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 140
            });
            dgPrzyczyny.Columns.Add(new DataGridTextColumn
            {
                Header = "Liczba",
                Binding = new Binding("Liczba"),
                Width = new DataGridLength(64),
                ElementStyle = CellStyle(HorizontalAlignment.Center, bold: true)
            });
            dgPrzyczyny.Columns.Add(new DataGridTextColumn
            {
                Header = "%",
                Binding = new Binding("Procent"),
                Width = new DataGridLength(58),
                ElementStyle = CellStyle(HorizontalAlignment.Center, fg: Color.FromRgb(0x9B, 0x59, 0xB6))
            });
        }

        private void SetupIncydentyGrid()
        {
            dgIncydenty.Columns.Clear();
            dgIncydenty.Columns.Add(new DataGridTextColumn
            {
                Header = "Data",
                Binding = new Binding("Data") { StringFormat = "dd.MM HH:mm" },
                Width = new DataGridLength(82),
                ElementStyle = CellStyle(HorizontalAlignment.Center),
                SortDirection = System.ComponentModel.ListSortDirection.Descending
            });
            dgIncydenty.Columns.Add(new DataGridTextColumn
            {
                Header = "Typ",
                Binding = new Binding("Typ"),
                Width = new DataGridLength(44),
                ElementStyle = CellStyle(HorizontalAlignment.Center, fs: 14)
            });
            dgIncydenty.Columns.Add(new DataGridTextColumn
            {
                Header = "Klient",
                Binding = new Binding("Klient"),
                Width = new DataGridLength(110)
            });
            dgIncydenty.Columns.Add(new DataGridTextColumn
            {
                Header = "Opis",
                Binding = new Binding("Opis"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
            dgIncydenty.Columns.Add(new DataGridTextColumn
            {
                Header = "Kto",
                Binding = new Binding("Kto"),
                Width = new DataGridLength(82)
            });
        }

        private void InitializeDates()
        {
            var today = DateTime.Today;
            dpOd.SelectedDate = new DateTime(today.Year, today.Month, 1);
            dpDo.SelectedDate = today;
            UpdateDateRangeText();
        }

        private void UpdateDateRangeText()
        {
            if (dpOd.SelectedDate.HasValue && dpDo.SelectedDate.HasValue)
                txtDateRange.Text = $"Zakres dat: {dpOd.SelectedDate:yyyy-MM-dd} — {dpDo.SelectedDate:yyyy-MM-dd}";
        }

        private async System.Threading.Tasks.Task LoadHandlowcyAsync()
        {
            var handlowcy = new List<string> { "(Wszyscy)" };
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                const string sql = @"SELECT DISTINCT wym.CDim_Handlowiec_Val
                                     FROM [HANDEL].[SSCommon].[ContractorClassification] wym
                                     WHERE wym.CDim_Handlowiec_Val IS NOT NULL AND wym.CDim_Handlowiec_Val <> ''
                                     ORDER BY wym.CDim_Handlowiec_Val";
                await using var cmd = new SqlCommand(sql, cn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    handlowcy.Add(reader.GetString(0));
            }
            catch { }

            cmbHandlowiec.SelectionChanged -= CmbHandlowiec_SelectionChanged;
            cmbHandlowiec.ItemsSource = handlowcy;
            cmbHandlowiec.SelectedIndex = 0;
            cmbHandlowiec.SelectionChanged += CmbHandlowiec_SelectionChanged;

            _ = LoadDataAsync();
        }

        // ════════════════════════════════════ DANE ════════════════════════════════════

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            if (_isLoading) return;
            if (!dpOd.SelectedDate.HasValue || !dpDo.SelectedDate.HasValue) return;

            _isLoading = true;
            _dtKlienci.Rows.Clear();
            _dtPrzyczyny.Rows.Clear();
            _dtIncydenty.Rows.Clear();
            _incydenty = new List<Incydent>();

            try
            {
                DateTime dataOd = dpOd.SelectedDate.Value.Date;
                DateTime dataDo1 = dpDo.SelectedDate.Value.Date.AddDays(1); // ekskluzywna górna granica
                string handlowiec = cmbHandlowiec?.SelectedItem?.ToString() ?? "(Wszyscy)";

                // ── 1. Kontrahenci (HANDEL — osobno, brak cross-DB JOIN) ──
                var contractors = new Dictionary<int, (string Name, string Salesman)>();
                await using (var cnHandel = new SqlConnection(_connHandel))
                {
                    await cnHandel.OpenAsync();
                    const string sqlContr = @"SELECT c.Id, c.Shortcut, wym.CDim_Handlowiec_Val
                                    FROM [HANDEL].[SSCommon].[STContractors] c
                                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym
                                    ON c.Id = wym.ElementId";
                    await using var cmdContr = new SqlCommand(sqlContr, cnHandel);
                    await using var rd = await cmdContr.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        int id = rd.GetInt32(0);
                        string shortcut = rd.IsDBNull(1) ? "" : rd.GetString(1);
                        string salesman = rd.IsDBNull(2) ? "" : rd.GetString(2);
                        contractors[id] = (string.IsNullOrWhiteSpace(shortcut) ? $"KH {id}" : shortcut, salesman);
                    }
                }

                var perKlient = new Dictionary<int, KlientAgg>();
                var przyczyny = new Dictionary<string, int>();
                int klienciZamawiajacy = 0;

                var connWithMars = _connLibra.Contains("MultipleActiveResultSets")
                    ? _connLibra
                    : _connLibra + ";MultipleActiveResultSets=True";

                await using (var cnLibra = new SqlConnection(connWithMars))
                {
                    await cnLibra.OpenAsync();

                    bool hasPrzyczyna = await ColumnExistsAsync(cnLibra, "ZamowieniaMieso", "PrzyczynaAnulowania");

                    // ── 2. Per klient: wszystkie zamówienia + anulowane (jedno query) ──
                    const string sqlTotals = @"
                        SELECT zm.KlientId,
                               COUNT(*) AS Zam,
                               SUM(CASE WHEN zm.Status = 'Anulowane' THEN 1 ELSE 0 END) AS Anul,
                               SUM(CASE WHEN zm.Status = 'Anulowane' THEN ISNULL(zmt.IloscSuma, 0) ELSE 0 END) AS KgAnul,
                               MAX(CASE WHEN zm.Status = 'Anulowane'
                                        THEN COALESCE(zm.DataAnulowania, zm.DataPrzyjazdu, zm.DataZamowienia) END) AS LastAnul
                        FROM [dbo].[ZamowieniaMieso] zm
                        LEFT JOIN (
                            SELECT ZamowienieId, SUM(Ilosc) AS IloscSuma
                            FROM [dbo].[ZamowieniaMiesoTowar]
                            GROUP BY ZamowienieId
                        ) zmt ON zm.Id = zmt.ZamowienieId
                        WHERE COALESCE(zm.DataPrzyjazdu, zm.DataZamowienia) >= @Od
                          AND COALESCE(zm.DataPrzyjazdu, zm.DataZamowienia) < @Do1
                          AND zm.KlientId IS NOT NULL
                        GROUP BY zm.KlientId";
                    await using (var cmd = new SqlCommand(sqlTotals, cnLibra) { CommandTimeout = 60 })
                    {
                        cmd.Parameters.AddWithValue("@Od", dataOd);
                        cmd.Parameters.AddWithValue("@Do1", dataDo1);
                        await using var rd = await cmd.ExecuteReaderAsync();
                        while (await rd.ReadAsync())
                        {
                            if (rd.IsDBNull(0)) continue;
                            int kid = rd.GetInt32(0);
                            var agg = GetAgg(perKlient, kid);
                            agg.Zamowien = rd.GetInt32(1);
                            agg.Anulowane = rd.IsDBNull(2) ? 0 : rd.GetInt32(2);
                            agg.KgAnul = rd.IsDBNull(3) ? 0m : Convert.ToDecimal(rd.GetValue(3));
                            if (!rd.IsDBNull(4)) agg.Ostatni = MaxDate(agg.Ostatni, rd.GetDateTime(4));
                            klienciZamawiajacy++;
                        }
                    }

                    // ── 3. Anulacje pojedynczo (oś incydentów) ──
                    string przyczynaCol = hasPrzyczyna ? "ISNULL(zm.PrzyczynaAnulowania, '')" : "''";
                    string sqlAnul = $@"
                        SELECT zm.Id, zm.KlientId,
                               COALESCE(zm.DataAnulowania, zm.DataPrzyjazdu, zm.DataZamowienia) AS Kiedy,
                               {przyczynaCol} AS Przyczyna,
                               ISNULL(zmt.IloscSuma, 0) AS Kg
                        FROM [dbo].[ZamowieniaMieso] zm
                        LEFT JOIN (
                            SELECT ZamowienieId, SUM(Ilosc) AS IloscSuma
                            FROM [dbo].[ZamowieniaMiesoTowar]
                            GROUP BY ZamowienieId
                        ) zmt ON zm.Id = zmt.ZamowienieId
                        WHERE zm.Status = 'Anulowane'
                          AND COALESCE(zm.DataAnulowania, zm.DataPrzyjazdu, zm.DataZamowienia) >= @Od
                          AND COALESCE(zm.DataAnulowania, zm.DataPrzyjazdu, zm.DataZamowienia) < @Do1
                          AND zm.KlientId IS NOT NULL";
                    await using (var cmd = new SqlCommand(sqlAnul, cnLibra) { CommandTimeout = 60 })
                    {
                        cmd.Parameters.AddWithValue("@Od", dataOd);
                        cmd.Parameters.AddWithValue("@Do1", dataDo1);
                        await using var rd = await cmd.ExecuteReaderAsync();
                        while (await rd.ReadAsync())
                        {
                            int zamId = rd.GetInt32(0);
                            int kid = rd.GetInt32(1);
                            DateTime kiedy = rd.IsDBNull(2) ? dataOd : rd.GetDateTime(2);
                            string przyczyna = rd.IsDBNull(3) ? "" : rd.GetString(3);
                            decimal kg = rd.IsDBNull(4) ? 0m : Convert.ToDecimal(rd.GetValue(4));

                            string klient = contractors.TryGetValue(kid, out var c) ? c.Name : $"KH {kid}";
                            string opis = $"Zam. #{zamId} • {kg:N0} kg";
                            if (przyczyna.Length > 0) opis += $" • {przyczyna}";
                            _incydenty.Add(new Incydent(kiedy, kid, "🚫", klient, opis, ""));

                            string klucz = przyczyna.Length > 0 ? przyczyna : "Brak przyczyny";
                            przyczyny[klucz] = przyczyny.TryGetValue(klucz, out int n) ? n + 1 : 1;
                        }
                    }

                    // ── 4. Redukcje pozycji (HistoriaZmianZamowien) ──
                    bool hasHistoria = await TableExistsAsync(cnLibra, "HistoriaZmianZamowien");
                    if (hasHistoria)
                    {
                        const string sqlRed = @"
                            SELECT h.ZamowienieId, zm.KlientId, h.PoleZmienione,
                                   h.WartoscPoprzednia, h.WartoscNowa, h.DataZmiany,
                                   ISNULL(NULLIF(h.UzytkownikNazwa, ''), h.Uzytkownik) AS Kto
                            FROM [dbo].[HistoriaZmianZamowien] h
                            JOIN [dbo].[ZamowieniaMieso] zm ON zm.Id = h.ZamowienieId
                            WHERE h.TypZmiany = 'EDYCJA'
                              AND h.PoleZmienione LIKE N'Pozycja:% - Zam.'
                              AND h.DataZmiany >= @Od AND h.DataZmiany < @Do1
                              AND zm.KlientId IS NOT NULL";
                        await using var cmd = new SqlCommand(sqlRed, cnLibra) { CommandTimeout = 60 };
                        cmd.Parameters.AddWithValue("@Od", dataOd);
                        cmd.Parameters.AddWithValue("@Do1", dataDo1);
                        await using var rd = await cmd.ExecuteReaderAsync();
                        while (await rd.ReadAsync())
                        {
                            int zamId = rd.GetInt32(0);
                            int kid = rd.GetInt32(1);
                            string pole = rd.IsDBNull(2) ? "" : rd.GetString(2);
                            decimal? stara = ParseKg(rd.IsDBNull(3) ? null : rd.GetString(3));
                            decimal? nowa = ParseKg(rd.IsDBNull(4) ? null : rd.GetString(4));
                            DateTime kiedy = rd.GetDateTime(5);
                            string kto = rd.IsDBNull(6) ? "" : rd.GetString(6);

                            // Redukcja = nowa wartość mniejsza od starej (w tym usunięcie pozycji → 0)
                            if (!stara.HasValue || !nowa.HasValue || nowa.Value >= stara.Value) continue;
                            decimal delta = stara.Value - nowa.Value;

                            var agg = GetAgg(perKlient, kid);
                            agg.Redukcje++;
                            agg.KgRed += delta;
                            agg.Ostatni = MaxDate(agg.Ostatni, kiedy);

                            string towar = WyciagnijTowar(pole);
                            string klient = contractors.TryGetValue(kid, out var c) ? c.Name : $"KH {kid}";
                            string opis = nowa.Value == 0
                                ? $"Zam. #{zamId} • {towar}: usunięto pozycję (było {stara.Value:N0} kg)"
                                : $"Zam. #{zamId} • {towar}: {stara.Value:N0} → {nowa.Value:N0} kg (−{delta:N0})";
                            _incydenty.Add(new Incydent(kiedy, kid, "📉", klient, opis, kto));
                        }
                    }

                    // ── 5. Przesunięcia terminu odbioru (zmiany DataPrzyjazdu w historii) ──
                    if (hasHistoria)
                    {
                        const string sqlTerm = @"
                            SELECT h.ZamowienieId, zm.KlientId, h.WartoscPoprzednia, h.WartoscNowa, h.DataZmiany,
                                   ISNULL(NULLIF(h.UzytkownikNazwa, ''), h.Uzytkownik) AS Kto
                            FROM [dbo].[HistoriaZmianZamowien] h
                            JOIN [dbo].[ZamowieniaMieso] zm ON zm.Id = h.ZamowienieId
                            WHERE h.TypZmiany = 'EDYCJA'
                              AND h.PoleZmienione = 'DataPrzyjazdu'
                              AND h.DataZmiany >= @Od AND h.DataZmiany < @Do1
                              AND zm.KlientId IS NOT NULL";
                        await using var cmd = new SqlCommand(sqlTerm, cnLibra) { CommandTimeout = 60 };
                        cmd.Parameters.AddWithValue("@Od", dataOd);
                        cmd.Parameters.AddWithValue("@Do1", dataDo1);
                        await using var rd = await cmd.ExecuteReaderAsync();
                        while (await rd.ReadAsync())
                        {
                            int zamId = rd.GetInt32(0);
                            int kid = rd.GetInt32(1);
                            string stara = rd.IsDBNull(2) ? "" : rd.GetString(2);
                            string nowa = rd.IsDBNull(3) ? "" : rd.GetString(3);
                            DateTime kiedy = rd.GetDateTime(4);
                            string kto = rd.IsDBNull(5) ? "" : rd.GetString(5);
                            if (stara == nowa) continue;

                            var agg = GetAgg(perKlient, kid);
                            agg.Przesuniecia++;
                            agg.Ostatni = MaxDate(agg.Ostatni, kiedy);

                            string klient = contractors.TryGetValue(kid, out var c) ? c.Name : $"KH {kid}";
                            _incydenty.Add(new Incydent(kiedy, kid, "🔁", klient,
                                $"Zam. #{zamId} • termin: {stara} → {nowa}", kto));
                        }
                    }

                    // ── 6. Niedobory przy odbiorze (zamówione vs faktycznie wydane kg) ──
                    if (await TableExistsAsync(cnLibra, "ZamowienieWydanieRoznice"))
                    {
                        const string sqlNied = @"
                            SELECT zm.KlientId, zm.Id,
                                   SUM(zt.Ilosc) AS ZamKg,
                                   SUM(ISNULL(r.IloscWydana, zt.Ilosc)) AS WydKg,
                                   MAX(zm.DataWydania) AS Kiedy
                            FROM [dbo].[ZamowieniaMieso] zm
                            JOIN [dbo].[ZamowieniaMiesoTowar] zt ON zt.ZamowienieId = zm.Id
                            LEFT JOIN [dbo].[ZamowienieWydanieRoznice] r
                                   ON r.ZamowienieId = zm.Id AND r.KodTowaru = zt.KodTowaru
                            WHERE zm.CzyWydane = 1
                              AND zm.DataWydania >= @Od AND zm.DataWydania < @Do1
                              AND zm.KlientId IS NOT NULL
                              AND zt.Ilosc > 0
                            GROUP BY zm.KlientId, zm.Id
                            HAVING SUM(zt.Ilosc) > SUM(ISNULL(r.IloscWydana, zt.Ilosc))";
                        await using var cmd = new SqlCommand(sqlNied, cnLibra) { CommandTimeout = 60 };
                        cmd.Parameters.AddWithValue("@Od", dataOd);
                        cmd.Parameters.AddWithValue("@Do1", dataDo1);
                        await using var rd = await cmd.ExecuteReaderAsync();
                        while (await rd.ReadAsync())
                        {
                            int kid = rd.GetInt32(0);
                            int zamId = rd.GetInt32(1);
                            decimal zamKg = rd.IsDBNull(2) ? 0m : Convert.ToDecimal(rd.GetValue(2));
                            decimal wydKg = rd.IsDBNull(3) ? 0m : Convert.ToDecimal(rd.GetValue(3));
                            DateTime kiedy = rd.IsDBNull(4) ? dataOd : rd.GetDateTime(4);
                            decimal brak = zamKg - wydKg;
                            if (brak <= 0) continue;

                            var agg = GetAgg(perKlient, kid);
                            agg.NiedoborZam++;
                            agg.NiedoborKg += brak;
                            agg.Ostatni = MaxDate(agg.Ostatni, kiedy);

                            string klient = contractors.TryGetValue(kid, out var c) ? c.Name : $"KH {kid}";
                            _incydenty.Add(new Incydent(kiedy, kid, "📦", klient,
                                $"Zam. #{zamId} • odebrano {wydKg:N0} z {zamKg:N0} kg (−{brak:N0})", ""));
                        }
                    }

                    // ── 7. Reklamacje klienta (dbo.Reklamacje) ──
                    if (await TableExistsAsync(cnLibra, "Reklamacje"))
                    {
                        const string sqlRekl = @"
                            SELECT r.IdKontrahenta, r.Id, r.DataZgloszenia,
                                   ISNULL(r.TypReklamacji, '') AS Typ,
                                   ISNULL(r.Status, '') AS Status,
                                   ISNULL(r.SumaKg, 0) AS Kg,
                                   ISNULL(r.SumaWartosc, 0) AS Wartosc
                            FROM [dbo].[Reklamacje] r
                            WHERE r.DataZgloszenia >= @Od AND r.DataZgloszenia < @Do1
                              AND r.IdKontrahenta IS NOT NULL AND r.IdKontrahenta > 0";
                        await using var cmd = new SqlCommand(sqlRekl, cnLibra) { CommandTimeout = 60 };
                        cmd.Parameters.AddWithValue("@Od", dataOd);
                        cmd.Parameters.AddWithValue("@Do1", dataDo1);
                        await using var rd = await cmd.ExecuteReaderAsync();
                        while (await rd.ReadAsync())
                        {
                            int kid = rd.GetInt32(0);
                            int reklId = rd.GetInt32(1);
                            DateTime kiedy = rd.IsDBNull(2) ? dataOd : rd.GetDateTime(2);
                            string typ = rd.IsDBNull(3) ? "" : rd.GetString(3);
                            string status = rd.IsDBNull(4) ? "" : rd.GetString(4);
                            decimal kg = rd.IsDBNull(5) ? 0m : Convert.ToDecimal(rd.GetValue(5));
                            decimal wartosc = rd.IsDBNull(6) ? 0m : Convert.ToDecimal(rd.GetValue(6));

                            var agg = GetAgg(perKlient, kid);
                            agg.Reklamacje++;
                            agg.ReklWartosc += wartosc;
                            agg.Ostatni = MaxDate(agg.Ostatni, kiedy);

                            string klient = contractors.TryGetValue(kid, out var c) ? c.Name : $"KH {kid}";
                            string opis = $"Rekl. #{reklId}";
                            if (typ.Length > 0) opis += $" • {typ}";
                            if (kg > 0) opis += $" • {kg:N0} kg";
                            if (wartosc > 0) opis += $" • {wartosc:N0} zł";
                            if (status.Length > 0) opis += $" • {status}";
                            _incydenty.Add(new Incydent(kiedy, kid, "⚠️", klient, opis, ""));
                        }
                    }
                }

                // ── 8. Tabela klientów + podsumowanie ──
                int totalAnul = 0, totalRed = 0, totalZam = 0, klienciZIncydentem = 0;
                int totalPrzes = 0, totalRekl = 0;
                decimal totalKgAnul = 0m, totalKgRed = 0m, totalNiedoborKg = 0m;

                foreach (var kvp in perKlient.OrderBy(k => k.Key))
                {
                    int kid = kvp.Key;
                    var a = kvp.Value;
                    var (name, salesman) = contractors.TryGetValue(kid, out var c) ? c : ($"Nieznany ({kid})", "");

                    if (handlowiec != "(Wszyscy)" && salesman != handlowiec) continue;

                    totalZam += a.Zamowien;
                    if (!a.MaIncydent) continue; // pokazujemy tylko klientów z incydentami

                    double procAnul = a.Zamowien > 0 ? 100.0 * a.Anulowane / a.Zamowien : (a.Anulowane > 0 ? 100 : 0);
                    int score = ObliczScore(procAnul, a.Redukcje, a.Przesuniecia, a.NiedoborZam, a.Reklamacje);

                    _dtKlienci.Rows.Add(kid, name, salesman, a.Zamowien, a.Anulowane, procAnul,
                        a.KgAnul, a.Redukcje, a.KgRed,
                        a.Przesuniecia, a.NiedoborKg, a.Reklamacje,
                        a.Ostatni == DateTime.MinValue ? (object)DBNull.Value : a.Ostatni, score);

                    totalAnul += a.Anulowane;
                    totalKgAnul += a.KgAnul;
                    totalRed += a.Redukcje;
                    totalKgRed += a.KgRed;
                    totalPrzes += a.Przesuniecia;
                    totalNiedoborKg += a.NiedoborKg;
                    totalRekl += a.Reklamacje;
                    klienciZIncydentem++;
                }

                _dtKlienci.DefaultView.Sort = "Score ASC, Anulowane DESC";
                ApplySearchFilter();

                // ── 10. Przyczyny ──
                int totalPrzyczyn = przyczyny.Values.Sum();
                foreach (var kvp in przyczyny.OrderByDescending(x => x.Value))
                {
                    double proc = totalPrzyczyn > 0 ? 100.0 * kvp.Value / totalPrzyczyn : 0;
                    _dtPrzyczyny.Rows.Add(kvp.Key, kvp.Value, $"{proc:F1}%");
                }

                // ── 11. Incydenty (najświeższe na górze) ──
                _incydenty = _incydenty.OrderByDescending(i => i.Data).ToList();
                WypelnijIncydenty(null);

                // ── 9. Podsumowanie w nagłówku (jedna linia zamiast kart KPI) ──
                double avgProc = totalZam > 0 ? 100.0 * totalAnul / totalZam : 0;
                txtHeaderStats.Text =
                    $"🚫 {totalAnul:N0} ({totalKgAnul:N0} kg)  •  " +
                    $"📉 {totalRed:N0} (−{totalKgRed:N0} kg)  •  " +
                    $"🔁 {totalPrzes:N0}  •  " +
                    $"📦 −{totalNiedoborKg:N0} kg  •  " +
                    $"⚠️ {totalRekl:N0} rekl.  •  " +
                    $"👥 {klienciZIncydentem:N0}/{klienciZamawiajacy:N0}  •  " +
                    $"📊 {avgProc:F1}% anul.";

                txtKlienciInfo.Text = $"{klienciZIncydentem} klientów z incydentami";
                UpdateDateRangeText();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private class KlientAgg
        {
            public int Zamowien;
            public int Anulowane;
            public decimal KgAnul;
            public int Redukcje;
            public decimal KgRed;
            public int Przesuniecia;      // zmiany DataPrzyjazdu (termin odbioru)
            public int NiedoborZam;       // zamówienia odebrane z niedoborem kg
            public decimal NiedoborKg;    // suma niedobranych kg
            public int Reklamacje;
            public decimal ReklWartosc;
            public DateTime Ostatni = DateTime.MinValue;
            public bool MaIncydent => Anulowane > 0 || Redukcje > 0 || Przesuniecia > 0 || NiedoborZam > 0 || Reklamacje > 0;
        }

        private static KlientAgg GetAgg(Dictionary<int, KlientAgg> dict, int kid)
        {
            if (!dict.TryGetValue(kid, out var agg))
            {
                agg = new KlientAgg();
                dict[kid] = agg;
            }
            return agg;
        }

        private static DateTime MaxDate(DateTime a, DateTime b) => a > b ? a : b;

        /// <summary>
        /// Wiarygodność 0–100:
        /// −0,8 pkt za każdy % anulacji, −2/redukcję (max −20), −1/przesunięcie terminu (max −10),
        /// −3/zamówienie z niedoborem kg (max −15), −3/reklamację (max −15).
        /// </summary>
        private static int ObliczScore(double procAnul, int redukcje, int przesuniecia, int niedoborZam, int reklamacje)
        {
            double score = 100
                - procAnul * 0.8
                - Math.Min(20, redukcje * 2)
                - Math.Min(10, przesuniecia * 1)
                - Math.Min(15, niedoborZam * 3)
                - Math.Min(15, reklamacje * 3);
            return Math.Max(0, Math.Min(100, (int)Math.Round(score)));
        }

        /// <summary>Parsuje kg z logu historii — format invariant "0.##", ale defensywnie łykamy też przecinek i " kg".</summary>
        private static decimal? ParseKg(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string s = raw.Replace("kg", "", StringComparison.OrdinalIgnoreCase)
                          .Replace(" ", "").Replace(" ", "").Trim();
            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v)) return v;
            if (decimal.TryParse(s.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out v)) return v;
            return null;
        }

        /// <summary>"Pozycja: Filet z kurczaka - Zam." → "Filet z kurczaka"</summary>
        private static string WyciagnijTowar(string pole)
        {
            const string prefix = "Pozycja: ";
            const string sufix = " - Zam.";
            if (pole.StartsWith(prefix) && pole.EndsWith(sufix) && pole.Length > prefix.Length + sufix.Length)
                return pole.Substring(prefix.Length, pole.Length - prefix.Length - sufix.Length);
            return pole;
        }

        private static async System.Threading.Tasks.Task<bool> ColumnExistsAsync(SqlConnection cn, string table, string column)
        {
            const string sql = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                                 WHERE TABLE_NAME = @T AND COLUMN_NAME = @C";
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@T", table);
            cmd.Parameters.AddWithValue("@C", column);
            return (int)await cmd.ExecuteScalarAsync() > 0;
        }

        private static async System.Threading.Tasks.Task<bool> TableExistsAsync(SqlConnection cn, string table)
        {
            const string sql = @"SELECT COUNT(*) FROM sys.objects
                                 WHERE object_id = OBJECT_ID(N'[dbo].[' + @T + N']') AND type = N'U'";
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@T", table);
            return (int)await cmd.ExecuteScalarAsync() > 0;
        }

        // ════════════════════════════════ INCYDENTY / FILTRY ════════════════════════════════

        private void WypelnijIncydenty(int? klientId)
        {
            _dtIncydenty.Rows.Clear();
            var zrodlo = klientId.HasValue
                ? _incydenty.Where(i => i.KlientId == klientId.Value)
                : _incydenty.AsEnumerable();

            foreach (var i in zrodlo.Take(300))
                _dtIncydenty.Rows.Add(i.Data, i.KlientId, i.Typ, i.Klient, i.Opis, i.Kto);
        }

        private void DgKlienci_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgKlienci.SelectedItem is DataRowView rowView)
            {
                int kid = rowView.Row.Field<int>("KlientId");
                string name = rowView.Row.Field<string>("Odbiorca") ?? "";
                WypelnijIncydenty(kid);
                txtIncydentyHeader.Text = $"INCYDENTY — {name.ToUpperInvariant()}";
                btnIncydentyWszyscy.Visibility = Visibility.Visible;
            }
        }

        /// <summary>2× klik na odbiorcę → pełna karta klienta Customer 360 (wszystkie dane kontrahenta).</summary>
        private void DgKlienci_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgKlienci.SelectedItem is not DataRowView rowView) return;
            int kid = rowView.Row.Field<int>("KlientId");
            try
            {
                var okno = new Kalendarz1.Customer360.Customer360Window(kid) { Owner = this };
                okno.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się otworzyć karty klienta:\n{ex.Message}",
                    "Customer 360", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnIncydentyWszyscy_Click(object sender, RoutedEventArgs e)
        {
            dgKlienci.SelectedItem = null;
            WypelnijIncydenty(null);
            txtIncydentyHeader.Text = "OSTATNIE INCYDENTY";
            btnIncydentyWszyscy.Visibility = Visibility.Collapsed;
        }

        private void ApplySearchFilter()
        {
            string raw = (txtSzukaj?.Text ?? "").Trim();
            if (raw.Length == 0)
            {
                _dtKlienci.DefaultView.RowFilter = "";
                return;
            }
            string esc = raw.Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]");
            _dtKlienci.DefaultView.RowFilter = $"Odbiorca LIKE '%{esc}%'";
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded) ApplySearchFilter();
        }

        private void CmbHandlowiec_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !_isLoading) _ = LoadDataAsync();
        }

        private void CmbOkres_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpOd == null || dpDo == null) return;

            if (cmbOkres.SelectedItem is ComboBoxItem item && item.Tag is string period)
            {
                DateTime today = DateTime.Today;
                switch (period)
                {
                    case "Year":
                        dpOd.SelectedDate = new DateTime(today.Year, 1, 1);
                        dpDo.SelectedDate = today;
                        break;
                    case "Quarter":
                        dpOd.SelectedDate = today.AddMonths(-3);
                        dpDo.SelectedDate = today;
                        break;
                    case "Month":
                        dpOd.SelectedDate = new DateTime(today.Year, today.Month, 1);
                        dpDo.SelectedDate = today;
                        break;
                    case "Week":
                        int delta = ((int)today.DayOfWeek + 6) % 7;
                        dpOd.SelectedDate = today.AddDays(-delta);
                        dpDo.SelectedDate = today;
                        break;
                    case "Day":
                        dpOd.SelectedDate = today;
                        dpDo.SelectedDate = today;
                        break;
                }
                if (IsLoaded && !_isLoading) _ = LoadDataAsync();
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadDataAsync();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
