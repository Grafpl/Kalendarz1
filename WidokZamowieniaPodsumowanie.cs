// Plik: WidokZamowieniaPodsumowanie.cs
// WERSJA 6.0 – funkcjonalności: wydania po pozycjach, różnice, prognozy per produkt,
// ukrycie "Kurczak B", bezpieczne konwersje typów i SUM.
// Uwaga: dla przypisania WYDANO do zamówienia zakładamy, że 1 klient składa max 1 zamówienie/dzień
// albo przynajmniej nie zamawia tego samego towaru w wielu zamówieniach jednego dnia.
// Jeśli to się zdarza – wydania będą „wspólne”.

#nullable enable
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;

namespace Kalendarz1
{
    public partial class WidokZamowieniaPodsumowanie : Form
    {
        // ====== Połączenia ======
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        // ====== Stan UI ======
        public string UserID { get; set; } = string.Empty;
        private DateTime _selectedDate;
        private int? _aktualneIdZamowienia;
        private readonly List<Button> _dayButtons = new();

        // ====== Dane i Cache ======
        private readonly DataTable _dtZamowienia = new();
        private readonly BindingSource _bsZamowienia = new();
        private readonly Dictionary<int, string> _twKodCache = new();      // idtw -> kod
        private readonly Dictionary<int, string> _twKatalogCache = new();  // idtw -> kod (katalog 67095)
        private readonly Dictionary<string, string> _userCache = new();    // operators.ID -> Name
        private readonly List<string> _handlowcyCache = new();

        // Wydajności elementów z puli „B/elementy”: kod->udział (0..1).
        // Uzupełnij wg własnych danych. „Kurczak A/B” traktowane osobno (poniżej).
        private readonly Dictionary<string, decimal> YieldByKod = new(StringComparer.OrdinalIgnoreCase)
        {
            // przykłady – dopasowywane po kodzie (dokładny match)
            // {"Filet", 0.32m}, {"Ćwiartka", 0.22m}, {"Skrzydło", 0.09m}, ...
        };

        private NazwaZiD nazwaZiD = new NazwaZiD();

        public WidokZamowieniaPodsumowanie()
        {
            InitializeComponent();
            Load += WidokZamowieniaPodsumowanie_Load;
            btnUsun.Visible = false;

        }

        private async void WidokZamowieniaPodsumowanie_Load(object? sender, EventArgs e)
        {
            _selectedDate = DateTime.Today;
            UstawPrzyciskiDniTygodnia();
            SzybkiGrid(dgvZamowienia);
            SzybkiGrid(dgvSzczegoly);
            SzybkiGrid(dgvAgregacja);
            SzybkiGrid(dgvPrzychody);
            SzybkiGrid(dgvPojTuszki); // mini grid

            btnUsun.Visible = (UserID == "11111");
            nazwaZiD.PokazPojTuszki(dgvPojTuszki); // wywołanie metody

            await ZaladujDanePoczatkoweAsync();
            await OdswiezWszystkieDaneAsync();
        }

        #region Helpers
        // ===== Null-safe helpers =====
        private static string SafeString(IDataRecord r, int i)
            => r.IsDBNull(i) ? string.Empty : Convert.ToString(r.GetValue(i)) ?? string.Empty;

        private static int? SafeInt32N(IDataRecord r, int i)
            => r.IsDBNull(i) ? (int?)null : Convert.ToInt32(r.GetValue(i));

        private static DateTime? SafeDateTimeN(IDataRecord r, int i)
            => r.IsDBNull(i) ? (DateTime?)null : Convert.ToDateTime(r.GetValue(i));

        private static decimal SafeDecimal(IDataRecord r, int i)
        {
            if (r.IsDBNull(i)) return 0m;
            return Convert.ToDecimal(r.GetValue(i)); // obsłuży double/float:int/decimal:string
        }

        private static object DbOrNull(DateTime? dt) => dt.HasValue ? dt.Value : DBNull.Value;
        private static object DbOrNull(object? v) => v ?? DBNull.Value;

        private static decimal ReadDecimal(IDataRecord r, int i)
        {
            if (r.IsDBNull(i)) return 0m;
            return Convert.ToDecimal(r.GetValue(i));
        }
        private static string AsString(IDataRecord r, int i)
        {
            if (r.IsDBNull(i)) return "";
            return Convert.ToString(r.GetValue(i)) ?? "";
        }

        private static bool IsKurczakB(string kod)
            => kod.IndexOf("Kurczak B", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsKurczakA(string kod)
            => kod.IndexOf("Kurczak A", StringComparison.OrdinalIgnoreCase) >= 0;

        #endregion

        #region Inicjalizacja i UI

        private void UstawPrzyciskiDniTygodnia()
        {
            _dayButtons.AddRange(new[] { btnPon, btnWt, btnSr, btnCzw, btnPt, btnSo, btnNd });
            foreach (var btn in _dayButtons)
                btn.Click += DzienButton_Click;
            AktualizujDatyPrzyciskow();
        }

        private void SzybkiGrid(DataGridView dgv)
        {
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.AllowUserToResizeRows = false;
            dgv.AllowUserToResizeColumns = true;
            dgv.RowHeadersVisible = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.BackgroundColor = Color.White;
            dgv.BorderStyle = BorderStyle.None;
            dgv.RowTemplate.Height = 30;
            dgv.Font = new Font("Segoe UI", 9f);
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            TryEnableDoubleBuffer(dgv);
        }

        private static void TryEnableDoubleBuffer(Control c)
        {
            try
            {
                var pi = c.GetType().GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                pi?.SetValue(c, true, null);
            }
            catch { /* ignoruj */ }
        }

        #endregion

        #region Nawigacja i zdarzenia

        private async void DzienButton_Click(object? sender, EventArgs e)
        {
            if (sender is Button clickedButton && clickedButton.Tag is DateTime date)
            {
                _selectedDate = date;
                AktualizujDatyPrzyciskow();
                await OdswiezWszystkieDaneAsync();
            }
        }

        private async void btnTydzienPrev_Click(object? sender, EventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(-7);
            AktualizujDatyPrzyciskow();
            await OdswiezWszystkieDaneAsync();
        }

        private async void btnTydzienNext_Click(object? sender, EventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(7);
            AktualizujDatyPrzyciskow();
            await OdswiezWszystkieDaneAsync();
        }

        private void AktualizujDatyPrzyciskow()
        {
            int delta = ((int)_selectedDate.DayOfWeek + 6) % 7;
            DateTime startOfWeek = _selectedDate.AddDays(-delta);
            lblZakresDat.Text = $"{startOfWeek:dd.MM.yyyy} - {startOfWeek.AddDays(6):dd.MM.yyyy}";

            for (int i = 0; i < 7; i++)
            {
                var dt = startOfWeek.AddDays(i);
                _dayButtons[i].Tag = dt;
                _dayButtons[i].Text = $"{_dayButtons[i].Name.Substring(3)}\n{dt:dd.MM}";
                _dayButtons[i].BackColor = dt.Date == _selectedDate.Date ? SystemColors.Highlight : SystemColors.Control;
                _dayButtons[i].ForeColor = dt.Date == _selectedDate.Date ? Color.White : SystemColors.ControlText;
            }
        }

        private async void btnOdswiez_Click(object? sender, EventArgs e)
        {
            await OdswiezWszystkieDaneAsync();
        }

        private void btnNoweZamowienie_Click(object? sender, EventArgs e)
        {
            using var widokZamowienia = new WidokZamowienia(UserID, null);
            if (widokZamowienia.ShowDialog(this) == DialogResult.OK)
            {
                Task.Run(async () => await OdswiezWszystkieDaneAsync());
            }
        }

        private void btnModyfikuj_Click(object? sender, EventArgs e)
        {
            if (!TrySetAktualneIdZamowieniaFromGrid(out var id) || id <= 0)
            {
                MessageBox.Show("Najpierw kliknij wiersz z zamówieniem, aby je wybrać.", "Brak wyboru",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var widokZamowienia = new WidokZamowienia(UserID, id);
            if (widokZamowienia.ShowDialog(this) == DialogResult.OK)
            {
                Task.Run(async () => await OdswiezWszystkieDaneAsync());
            }
        }
        private async void btnAnuluj_Click(object? sender, EventArgs e)
        {
            if (!TrySetAktualneIdZamowieniaFromGrid(out var id) || id <= 0)
            {
                MessageBox.Show("Najpierw kliknij wiersz z zamówieniem, które chcesz anulować.", "Brak wyboru",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show("Czy na pewno chcesz anulować wybrane zamówienie? Tej operacji nie można cofnąć.", "Potwierdź anulowanie", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result == DialogResult.Yes)
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    await using var cmd = new SqlCommand("UPDATE dbo.ZamowieniaMieso SET Status = 'Anulowane' WHERE Id = @Id", cn);
                    cmd.Parameters.AddWithValue("@Id", _aktualneIdZamowienia.Value);
                    await cmd.ExecuteNonQueryAsync();

                    MessageBox.Show("Zamówienie zostało anulowane.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await OdswiezWszystkieDaneAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Wystąpił błąd podczas anulowania zamówienia: {ex.Message}", "Błąd krytyczny", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void Filtry_Changed(object? sender, EventArgs e)
        {
            ZastosujFiltry();
            AktualizujPodsumowanieDnia();
        }

        private void btnDuplikuj_Click(object? sender, EventArgs e)
        {
            if (!TrySetAktualneIdZamowieniaFromGrid(out var id) || id <= 0)
            {
                MessageBox.Show("Najpierw kliknij wiersz z zamówieniem do duplikacji.", "Brak wyboru", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var widokZamowienia = new WidokZamowienia(UserID, id);
            widokZamowienia.Text = "Duplikuj zamówienie";
            widokZamowienia.Load += (s, ev) =>
            {
                try
                {
                    var dtp = widokZamowienia.Controls.Find("dateTimePickerSprzedaz", true).FirstOrDefault() as DateTimePicker;
                    if (dtp != null)
                    {
                        var now = DateTime.Today;
                        var next = now.AddDays(1);
                        if (next.DayOfWeek == DayOfWeek.Saturday)
                            next = next.AddDays(2); // na poniedziałek
                        dtp.Value = next;
                    }
                }
                catch { }
            };
            if (widokZamowienia.ShowDialog(this) == DialogResult.OK)
            {
                Task.Run(async () => await OdswiezWszystkieDaneAsync());
            }
        }

        #endregion

        #region Wczytywanie i przetwarzanie

        private async Task OdswiezWszystkieDaneAsync()
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                await WczytajZamowieniaDlaDniaAsync(_selectedDate);
                await WczytajDanePrzychodowAsync(_selectedDate);
                await WyswietlAgregacjeProduktowAsync(_selectedDate);
                AktualizujPodsumowanieDnia();
                WyczyscSzczegoly();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas odświeżania danych: {ex.Message}\n\nSTACKTRACE:\n{ex.StackTrace}\n\nINNER: {ex.InnerException}", "Błąd Krytyczny", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private async Task ZaladujDanePoczatkoweAsync()
        {
            _twKodCache.Clear();
            _twKatalogCache.Clear();
            await using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                await using var cmd = new SqlCommand("SELECT ID, kod, katalog FROM [HANDEL].[HM].[TW]", cn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int idtw = reader.GetInt32(0);
                    string kod = AsString(reader, 1);

                    object katObj = reader.GetValue(2);
                    bool w67095 = false;
                    if (!(katObj is DBNull))
                    {
                        if (katObj is int ki) w67095 = (ki == 67095);
                        else w67095 = string.Equals(Convert.ToString(katObj), "67095", StringComparison.Ordinal);
                    }

                    _twKodCache[idtw] = kod;
                    if (w67095)
                        _twKatalogCache[idtw] = kod;
                }
            }

            // combobox towar
            var listaTowarow = _twKatalogCache
                .OrderBy(x => x.Value)
                .Select(k => new KeyValuePair<int, string>(k.Key, k.Value))
                .ToList();
            listaTowarow.Insert(0, new KeyValuePair<int, string>(0, "— Wszystkie towary —"));
            cbFiltrujTowar.DataSource = new BindingSource(listaTowarow, null);
            cbFiltrujTowar.DisplayMember = "Value";
            cbFiltrujTowar.ValueMember = "Key";
            cbFiltrujTowar.SelectedIndexChanged += Filtry_Changed;
            cbFiltrujTowar.SelectedIndex = 0;

            // users
            _userCache.Clear();
            await using (var cn2 = new SqlConnection(_connLibra))
            {
                await cn2.OpenAsync();
                await using var cmd = new SqlCommand("SELECT ID, Name FROM dbo.operators", cn2);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var idStr = AsString(reader, 0);
                    var name = AsString(reader, 1);
                    if (!string.IsNullOrEmpty(idStr))
                        _userCache[idStr] = name;
                }
            }

            // handlowcy (string/INT-safe)
            _handlowcyCache.Clear();
            await using (var cn3 = new SqlConnection(_connHandel))
            {
                await cn3.OpenAsync();
                await using var cmd = new SqlCommand(@"
                    SELECT DISTINCT CDim_Handlowiec_Val 
                    FROM [HANDEL].[SSCommon].[ContractorClassification] 
                    WHERE CDim_Handlowiec_Val IS NOT NULL
                    ORDER BY 1", cn3);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var val = AsString(reader, 0);
                    if (!string.IsNullOrWhiteSpace(val))
                        _handlowcyCache.Add(val);
                }
            }

            cbFiltrujHandlowca.Items.Clear();
            cbFiltrujHandlowca.Items.Add("— Wszyscy —");
            cbFiltrujHandlowca.Items.AddRange(_handlowcyCache.ToArray());
            cbFiltrujHandlowca.SelectedIndexChanged += Filtry_Changed;
            cbFiltrujHandlowca.SelectedIndex = 0;

            txtFiltrujOdbiorce.TextChanged += Filtry_Changed;
        }

        private async Task WczytajZamowieniaDlaDniaAsync(DateTime dzien)
        {
            // Definicja kolumn (raz)
            if (_dtZamowienia.Columns.Count == 0)
            {
                _dtZamowienia.Columns.Add("Id", typeof(int));
                _dtZamowienia.Columns.Add("Odbiorca", typeof(string));
                _dtZamowienia.Columns.Add("Handlowiec", typeof(string));
                _dtZamowienia.Columns.Add("IloscZamowiona", typeof(decimal));
                _dtZamowienia.Columns.Add("IloscFaktyczna", typeof(decimal));
                var colDataUtw = new DataColumn("DataUtworzenia", typeof(DateTime));
                colDataUtw.AllowDBNull = true;
                _dtZamowienia.Columns.Add(colDataUtw);
                _dtZamowienia.Columns.Add("Utworzyl", typeof(string));
                _dtZamowienia.Columns.Add("Status", typeof(string));
            }
            else
            {
                _dtZamowienia.Clear();
            }

            // map kontrahentów: id -> (Shortcut?, Handlowiec?)
            var kontrahenci = new Dictionary<int, (string Nazwa, string Handlowiec)>();
            await using (var cnHandel = new SqlConnection(_connHandel))
            {
                await cnHandel.OpenAsync();
                const string sqlKontr = @"
            SELECT c.Id, c.Shortcut, wym.CDim_Handlowiec_Val 
            FROM [HANDEL].[SSCommon].[STContractors] c
            LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym ON c.Id = wym.ElementId";
                await using var cmdKontr = new SqlCommand(sqlKontr, cnHandel);
                await using var rd = await cmdKontr.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    string shortcut = SafeString(rd, 1);
                    string handl = SafeString(rd, 2);
                    kontrahenci[id] = (string.IsNullOrWhiteSpace(shortcut) ? $"KH {id}" : shortcut, handl);
                }
            }

            // filtr – wybrany towar
            int? selectedProductId = null;
            if (cbFiltrujTowar.SelectedIndex > 0 && cbFiltrujTowar.SelectedValue is int selectedTowarId)
                selectedProductId = selectedTowarId;

            // wczytaj zamówienia (sumy)
            var temp = new DataTable();
            if (_twKatalogCache.Keys.Any())
            {
                await using (var cnLibra = new SqlConnection(_connLibra))
                {
                    await cnLibra.OpenAsync();
                    var idwList = string.Join(",", _twKatalogCache.Keys);
                    string sql = $@"
                SELECT zm.Id, zm.KlientId, SUM(ISNULL(zmt.Ilosc,0)) AS Ilosc, zm.DataUtworzenia, zm.IdUser, zm.Status
                FROM [dbo].[ZamowieniaMieso] zm
                JOIN [dbo].[ZamowieniaMiesoTowar] zmt ON zm.Id = zmt.ZamowienieId
                WHERE zm.DataZamowienia = @Dzien AND zmt.KodTowaru IN ({idwList}) " +
                        (selectedProductId.HasValue ? "AND zmt.KodTowaru = @TowarId " : "") +
                        @"GROUP BY zm.Id, zm.KlientId, zm.DataUtworzenia, zm.IdUser, zm.Status
                  ORDER BY zm.Id";

                    await using var cmd = new SqlCommand(sql, cnLibra);
                    cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
                    if (selectedProductId.HasValue)
                        cmd.Parameters.AddWithValue("@TowarId", selectedProductId.Value);
                    using var da = new SqlDataAdapter(cmd);
                    da.Fill(temp);
                }
            }

            // Faktyczne wydania (WZ) per klient i produkt
            var wydaniaPerKhidIdtw = await PobierzWydaniaPerKhidIdtwAsync(dzien);

            // Zbiór klientów z zamówień
            var klienciZamowien = new HashSet<int>(temp.Rows.Cast<DataRow>().Select(r => r["KlientId"] == DBNull.Value ? 0 : Convert.ToInt32(r["KlientId"])));

            foreach (DataRow r in temp.Rows)
            {
                // NULL-safe konwersje z DataTable (może zwrócić DBNull)
                int id = r["Id"] == DBNull.Value ? 0 : Convert.ToInt32(r["Id"]);
                int klientId = r["KlientId"] == DBNull.Value ? 0 : Convert.ToInt32(r["KlientId"]);
                decimal ilosc = r["Ilosc"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Ilosc"]);
                DateTime? dataUtw = (r["DataUtworzenia"] is DBNull or null) ? (DateTime?)null : Convert.ToDateTime(r["DataUtworzenia"]);
                string idUser = r["IdUser"]?.ToString() ?? "";
                string status = r["Status"]?.ToString() ?? "Nowe";

                var (nazwa, handlowiec) = kontrahenci.TryGetValue(klientId, out var kh)
                    ? kh
                    : ($"Nieznany ({klientId})", "");

                decimal wydane = 0m;
                if (wydaniaPerKhidIdtw.TryGetValue(klientId, out var perIdtw))
                {
                    wydane = perIdtw.Values.Sum();
                }

                _dtZamowienia.Rows.Add(
                    id,
                    nazwa,
                    handlowiec,
                    ilosc,
                    wydane,
                    dataUtw.HasValue ? (object)dataUtw.Value : DBNull.Value,
                    _userCache.TryGetValue(idUser, out var user) ? user : "Brak",
                    status
                );
            }

            // Dodaj wydania bez zamówień (Symfonia)
            var wydaniaBezZamowien = new List<DataRow>();
            foreach (var kv in wydaniaPerKhidIdtw)
            {
                int khid = kv.Key;
                if (klienciZamowien.Contains(khid)) continue; // już jest zamówienie
                decimal wydane = kv.Value.Values.Sum();
                var (nazwa, handlowiec) = kontrahenci.TryGetValue(khid, out var kh)
                    ? kh
                    : ($"Nieznany ({khid})", "");
                var row = _dtZamowienia.NewRow();
                row["Id"] = 0;
                row["Odbiorca"] = nazwa;
                row["Handlowiec"] = handlowiec;
                row["IloscZamowiona"] = 0m;
                row["IloscFaktyczna"] = wydane;
                row["DataUtworzenia"] = DBNull.Value;
                row["Utworzyl"] = "";
                row["Status"] = "Wydanie bez zamówienia";
                wydaniaBezZamowien.Add(row);
            }
            // Sortuj wydania bez zamówień malejąco po IloscFaktyczna
            foreach (var row in wydaniaBezZamowien.OrderByDescending(r => (decimal)r["IloscFaktyczna"]))
                _dtZamowienia.Rows.Add(row.ItemArray);

            // Sortowanie: najpierw zamówienia, potem wydania bez zamówień
            _bsZamowienia.DataSource = _dtZamowienia;
            dgvZamowienia.DataSource = _bsZamowienia;
            _bsZamowienia.Sort = "Status ASC, IloscZamowiona DESC";
            dgvZamowienia.ClearSelection();

            // Ustawienia kolumn: szerokości, kolejność, nagłówki
            if (dgvZamowienia.Columns["Id"] != null) dgvZamowienia.Columns["Id"].Visible = false;
            if (dgvZamowienia.Columns["Odbiorca"] != null)
                dgvZamowienia.Columns["Odbiorca"].Width = 160;
            if (dgvZamowienia.Columns["Handlowiec"] != null)
                dgvZamowienia.Columns["Handlowiec"].Width = 120;
            if (dgvZamowienia.Columns["IloscZamowiona"] != null)
            {
                dgvZamowienia.Columns["IloscZamowiona"].DefaultCellStyle.Format = "N0";
                dgvZamowienia.Columns["IloscZamowiona"].HeaderText = "Zamówiono (kg)";
                dgvZamowienia.Columns["IloscZamowiona"].DisplayIndex = 3;
            }
            if (dgvZamowienia.Columns["IloscFaktyczna"] != null)
            {
                dgvZamowienia.Columns["IloscFaktyczna"].DefaultCellStyle.Format = "N0";
                dgvZamowienia.Columns["IloscFaktyczna"].HeaderText = "Wydano (kg)";
                dgvZamowienia.Columns["IloscFaktyczna"].DisplayIndex = 4;
            }
            if (dgvZamowienia.Columns["DataUtworzenia"] != null)
            {
                dgvZamowienia.Columns["DataUtworzenia"].HeaderText = "Utworzono";
                dgvZamowienia.Columns["DataUtworzenia"].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
            }
            if (dgvZamowienia.Columns["Status"] != null)
                dgvZamowienia.Columns["Status"].DisplayIndex = dgvZamowienia.Columns.Count - 1;

            ZastosujFiltry();
        }
        private async Task<Dictionary<int, decimal>> PobierzFaktyczneWydaniaAsync(DateTime dzien, int? towarId = null)
        {
            var wynik = new Dictionary<int, decimal>();
            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            string sqlWz = @"
        SELECT DK.khid, SUM(ABS(MZ.ilosc))
        FROM [HANDEL].[HM].[MZ] MZ
        JOIN [HANDEL].[HM].[DK] ON MZ.super = DK.id
        WHERE DK.seria IN ('sWZ', 'sWZ-W') AND DK.data = @Dzien " +
                (towarId.HasValue ? "AND MZ.idtw = @TowarId " : "") +
                "GROUP BY DK.khid";

            await using var cmd = new SqlCommand(sqlWz, cn);
            cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
            if (towarId.HasValue) cmd.Parameters.AddWithValue("@TowarId", towarId.Value);

            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                int khid = rd.GetInt32(0);
                decimal ilosc = SafeDecimal(rd, 1); // obsługa double/NULL
                wynik[khid] = ilosc;
            }
            return wynik;
        }


        /// <summary>
        /// Sumy wydań per (khid, idtw) dla MG.seria in ('sWZ','sWZ-W'), MG.aktywny=1, MG.data=@Dzien.
        /// </summary>
        private async Task<Dictionary<int, Dictionary<int, decimal>>> PobierzWydaniaPerKhidIdtwAsync(DateTime dzien)
        {
            var dict = new Dictionary<int, Dictionary<int, decimal>>();
            if (!_twKatalogCache.Keys.Any()) return dict;

            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            var idwList = string.Join(",", _twKatalogCache.Keys);
            string sql = $@"
                SELECT MG.khid, MZ.idtw, SUM(ABS(MZ.ilosc)) AS qty
                FROM [HANDEL].[HM].[MZ] MZ
                JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny = 1 AND MG.data = @Dzien AND MG.khid IS NOT NULL
                AND MZ.idtw IN ({idwList})
                GROUP BY MG.khid, MZ.idtw";
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                int khid = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0);
                int idtw = rdr.GetInt32(1);
                decimal qty = ReadDecimal(rdr, 2);
                if (!dict.TryGetValue(khid, out var perIdtw))
                {
                    perIdtw = new Dictionary<int, decimal>();
                    dict[khid] = perIdtw;
                }
                if (perIdtw.ContainsKey(idtw)) perIdtw[idtw] += qty;
                else perIdtw[idtw] = qty;
            }
            return dict;
        }

        private async void dgvZamowienia_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgvZamowienia.CurrentRow != null && dgvZamowienia.CurrentRow.Index >= 0)
            {
                await HandleGridSelection(dgvZamowienia.CurrentRow.Index);
            }
            else
            {
                WyczyscSzczegoly();
            }
        }
        private async void dgvZamowienia_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                await HandleGridSelection(e.RowIndex);
            }
        }

        private async Task HandleGridSelection(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgvZamowienia.Rows.Count)
            {
                WyczyscSzczegoly();
                return;
            }

            var row = dgvZamowienia.Rows[rowIndex];
            if (row.DataBoundItem is DataRowView drv)
            {
                var status = drv.Row.Field<string>("Status") ?? "";
                if (status == "Wydanie bez zamówienia")
                {
                    var odbiorca = drv.Row.Field<string>("Odbiorca") ?? "";
                    await WyswietlSzczegolyWydaniaBezZamowieniaAsync(odbiorca, _selectedDate);
                    return;
                }
            }

            if (TrySetAktualneIdZamowieniaFromGrid(out var id))
            {
                await WyswietlSzczegolyZamowieniaAsync(id);
            }
            else
            {
                WyczyscSzczegoly();
            }
        }
        // Dodaj metodę do wyświetlania szczegółów wydania bez zamówienia
        private async Task WyswietlSzczegolyWydaniaBezZamowieniaAsync(string odbiorca, DateTime dzien)
        {
            // Pobierz wydania z Symfonii dla tego odbiorcy i dnia
            var dt = new DataTable();
            dt.Columns.Add("Produkt", typeof(string));
            dt.Columns.Add("Wydano", typeof(decimal));

            var khId = 0;
            await using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                var cmdKh = new SqlCommand("SELECT Id FROM [HANDEL].[SSCommon].[STContractors] WHERE Shortcut = @Odbiorca", cn);
                cmdKh.Parameters.AddWithValue("@Odbiorca", odbiorca);
                var result = await cmdKh.ExecuteScalarAsync();
                if (result != null) khId = Convert.ToInt32(result);
            }

            if (khId > 0)
            {
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    const string sql = @"
                        SELECT MZ.idtw, SUM(ABS(MZ.ilosc)) 
                        FROM [HANDEL].[HM].[MZ] MZ 
                        JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id 
                        JOIN [HANDEL].[HM].[TW] ON MZ.idtw = TW.id
                        WHERE MG.seria IN ('sWZ','sWZ-W') 
                          AND MG.aktywny = 1 
                          AND MG.data = @Dzien 
                          AND MG.khid = @Khid
                          AND TW.katalog = 67095
                        GROUP BY MZ.idtw";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
                    cmd.Parameters.AddWithValue("@Khid", khId);
                    using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        int idtw = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                        decimal ilosc = SafeDecimal(rd, 1);
                        string produkt = _twKatalogCache.TryGetValue(idtw, out var kod) ? kod : $"Nieznany ({idtw})";
                        dt.Rows.Add(produkt, ilosc);
                    }
                }
            }
            txtNotatki.Text = "Wydanie bez zamówienia (tylko towary z katalogu 67095)";
            dgvSzczegoly.DataSource = dt;
            if (dgvSzczegoly.Columns["Wydano"] != null) dgvSzczegoly.Columns["Wydano"].DefaultCellStyle.Format = "N0";
        }


        private async Task WyswietlSzczegolyZamowieniaAsync(int zamowienieId)
        {
            // Pobierz dane zamówienia
            var dtZam = new DataTable();
            int klientId = 0;
            await using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                const string sql = @"
            SELECT zmt.KodTowaru, zmt.Ilosc, zm.Uwagi, zm.KlientId
            FROM [dbo].[ZamowieniaMiesoTowar] zmt
            INNER JOIN [dbo].[ZamowieniaMieso] zm ON zm.Id = zmt.ZamowienieId
            WHERE zmt.ZamowienieId = @Id";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Id", zamowienieId);
                using var da = new SqlDataAdapter(cmd);
                da.Fill(dtZam);
                if (dtZam.Rows.Count > 0 && dtZam.Columns.Contains("KlientId"))
                    klientId = dtZam.Rows[0]["KlientId"] is DBNull ? 0 : Convert.ToInt32(dtZam.Rows[0]["KlientId"]);
            }

            // Pobierz wydania z Symfonii dla tego klienta i dnia
            var wydania = new Dictionary<int, decimal>();
            if (klientId > 0)
            {
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    const string sql = @"
                SELECT MZ.idtw, SUM(ABS(MZ.ilosc))
                FROM [HANDEL].[HM].[MZ] MZ
                JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny = 1 AND MG.data = @Dzien AND MG.khid = @Khid
                GROUP BY MZ.idtw";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Dzien", _selectedDate.Date);
                    cmd.Parameters.AddWithValue("@Khid", klientId);
                    using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        int idtw = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                        decimal ilosc = SafeDecimal(rd, 1);
                        wydania[idtw] = ilosc;
                    }
                }
            }

            // Przygotuj wynikową tabelę
            var dt = new DataTable();
            dt.Columns.Add("Produkt", typeof(string));
            dt.Columns.Add("Zamówiono", typeof(decimal));
            dt.Columns.Add("Wydano", typeof(decimal));

            // Dodaj wszystkie towary z zamówienia
            foreach (DataRow r in dtZam.Rows)
            {
                int idTowaru = r["KodTowaru"] == DBNull.Value ? 0 : Convert.ToInt32(r["KodTowaru"]);
                if (!_twKatalogCache.ContainsKey(idTowaru)) continue; // Pomiń towary spoza katalogu

                string produkt = _twKatalogCache.TryGetValue(idTowaru, out var kod) ? kod : $"Nieznany ({idTowaru})";
                decimal zamowiono = r["Ilosc"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Ilosc"]);
                decimal wydano = wydania.TryGetValue(idTowaru, out var w) ? w : 0m;
                dt.Rows.Add(produkt, zamowiono, wydano);
                wydania.Remove(idTowaru); // usuwamy, by potem dodać tylko te wydane bez zamówienia
            }

            // Dodaj towary wydane bez zamówienia (ale z katalogu 67095)
            foreach (var kv in wydania)
            {
                if (!_twKatalogCache.ContainsKey(kv.Key)) continue; // Pomiń towary spoza katalogu
                string produkt = _twKatalogCache.TryGetValue(kv.Key, out var kod) ? kod : $"Nieznany ({kv.Key})";
                dt.Rows.Add(produkt, 0m, kv.Value);
            }

            // Notatki
            string notatki = dtZam.Rows.Count > 0 ? (dtZam.Rows[0]["Uwagi"]?.ToString() ?? "") : "";
            txtNotatki.Text = notatki;
            dgvSzczegoly.DataSource = dt;

            // Formatowanie
            if (dgvSzczegoly.Columns["Zamówiono"] != null) {
                dgvSzczegoly.Columns["Zamówiono"].DefaultCellStyle.Format = "N0";
                dgvSzczegoly.Columns["Zamówiono"].HeaderText = "Zamówiono (kg)";
            }
            if (dgvSzczegoly.Columns["Wydano"] != null) {
                dgvSzczegoly.Columns["Wydano"].DefaultCellStyle.Format = "N0";
                dgvSzczegoly.Columns["Wydano"].HeaderText = "Wydano (kg)";
            }
            if (dgvSzczegoly.Columns["Produkt"] != null) dgvSzczegoly.Columns["Produkt"].DisplayIndex = 0;
        }


        private async Task WyswietlAgregacjeProduktowAsync(DateTime dzien)
        {
            var dtAg = new DataTable();
            dtAg.Columns.Add("Produkt", typeof(string));
            dtAg.Columns.Add("Zamówiono", typeof(decimal));
            dtAg.Columns.Add("Wydano", typeof(decimal));
            dtAg.Columns.Add("Różnica", typeof(decimal));
            dtAg.Columns.Add("PlanowanyPrzychód", typeof(decimal)); // prognoza per produkt
            dtAg.Columns.Add("FaktycznyPrzychód", typeof(decimal)); // PWU per produkt

            // SUMY WYDAN per produkt
            var sumaWydan = await PobierzSumeWydanPoProdukcieAsync(dzien);

            // PROGNOZA / FAKT przychodu per produkt (PWU)
            var (planPrzychodu, faktPrzychodu) = await PrognozaIFaktPrzychoduPerProduktAsync(dzien);

            // SUMY ZAMÓWIEŃ per produkt z bieżącej listy zamówień (bez anulowanych)
            var sumaZamowien = new Dictionary<int, decimal>();
            var zamowieniaIds = _dtZamowienia.AsEnumerable()
                .Where(r => !string.Equals(r.Field<string>("Status"), "Anulowane", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Field<int>("Id")).ToList();
            if (zamowieniaIds.Any())
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                var sql = $"SELECT KodTowaru, SUM(Ilosc) FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId IN ({string.Join(",", zamowieniaIds)}) GROUP BY KodTowaru";
                using var cmd = new SqlCommand(sql, cn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    sumaZamowien[reader.GetInt32(0)] = ReadDecimal(reader, 1);
            }

            foreach (var towar in _twKatalogCache.OrderBy(kvp => kvp.Value))
            {
                var kod = towar.Value;
                if (IsKurczakB(kod)) continue; // #1 ukryj „Kurczak B”

                var zam = sumaZamowien.TryGetValue(towar.Key, out var z) ? z : 0m;
                var wyd = sumaWydan.TryGetValue(towar.Key, out var w) ? w : 0m;
                var diff = zam - wyd;
                var plan = planPrzychodu.TryGetValue(towar.Key, out var p) ? p : 0m;
                var fakt = faktPrzychodu.TryGetValue(towar.Key, out var f) ? f : 0m;

                dtAg.Rows.Add(kod, zam, wyd, diff, plan, fakt);
            }

            dgvAgregacja.DataSource = dtAg;
            foreach (DataGridViewColumn col in dgvAgregacja.Columns)
            {
                if (col.Name != "Produkt") col.DefaultCellStyle.Format = "N0";
            }
        }

        private void AktualizujPodsumowanieDnia()
        {
            int liczbaZamowien = 0;
            int liczbaWydanBezZamowien = 0;
            decimal sumaKgZamowiono = 0;
            decimal sumaKgWydano = 0;
            var handlowiecStat = new Dictionary<string, (int zZam, int bezZam, decimal kgZam, decimal kgWyd)>();

            if (_bsZamowienia.List is System.Collections.IEnumerable list)
            {
                foreach (var item in list)
                {
                    if (item is DataRowView drv)
                    {
                        var status = drv.Row.Field<string>("Status") ?? "";
                        var handlowiec = drv.Row.Field<string>("Handlowiec") ?? "BRAK";
                        var iloscZam = drv.Row.Field<decimal?>("IloscZamowiona") ?? 0m;
                        var iloscWyd = drv.Row.Field<decimal?>("IloscFaktyczna") ?? 0m;

                        if (!handlowiecStat.ContainsKey(handlowiec))
                            handlowiecStat[handlowiec] = (0, 0, 0, 0);

                        if (status == "Wydanie bez zamówienia")
                        {
                            liczbaWydanBezZamowien++;
                            sumaKgWydano += iloscWyd;
                            handlowiecStat[handlowiec] = (handlowiecStat[handlowiec].zZam, handlowiecStat[handlowiec].bezZam + 1, handlowiecStat[handlowiec].kgZam, handlowiecStat[handlowiec].kgWyd + iloscWyd);
                        }
                        else if (status != "Anulowane")
                        {
                            liczbaZamowien++;
                            sumaKgZamowiono += iloscZam;
                            sumaKgWydano += iloscWyd;
                            handlowiecStat[handlowiec] = (handlowiecStat[handlowiec].zZam + 1, handlowiecStat[handlowiec].bezZam, handlowiecStat[handlowiec].kgZam + iloscZam, handlowiecStat[handlowiec].kgWyd + iloscWyd);
                        }
                    }
                }
            }
            int suma = liczbaZamowien + liczbaWydanBezZamowien;
            string perHandlowiec = string.Join(" | ", handlowiecStat.OrderBy(h => h.Key).Select(h => $"{h.Key}: {h.Value.zZam}/{h.Value.bezZam} ({h.Value.kgZam:N0}/{h.Value.kgWyd:N0}kg)"));
            lblPodsumowanie.Text = $"Suma: {suma} ({liczbaZamowien} zam. / {liczbaWydanBezZamowien} wyd.) | Zamówiono: {sumaKgZamowiono:N0} kg | Wydano: {sumaKgWydano:N0} kg | {perHandlowiec}";
        }

        private void WyczyscSzczegoly()
        {
            dgvSzczegoly.DataSource = null;
            txtNotatki.Clear();
            _aktualneIdZamowienia = null;
        }

        /// <summary>
        /// PRZYCHODY: prognoza (na podstawie harmonogramu) i fakt (PWU) per produkt.
        /// </summary>
        private async Task WczytajDanePrzychodowAsync(DateTime dzien)
        {
            var dtP = new DataTable();
            dtP.Columns.Add("Produkt", typeof(string));
            dtP.Columns.Add("Plan (kg)", typeof(decimal));
            dtP.Columns.Add("Fakt (kg)", typeof(decimal));
            dtP.Columns.Add("Różnica", typeof(decimal));

            var (plan, fakt) = await PrognozaIFaktPrzychoduPerProduktAsync(dzien);

            foreach (var p in _twKatalogCache.OrderBy(x => x.Value))
            {
                var kod = p.Value;
                var planKg = plan.TryGetValue(p.Key, out var v1) ? v1 : 0m;
                var faktKg = fakt.TryGetValue(p.Key, out var v2) ? v2 : 0m;
                var roznica = planKg - faktKg;

                dtP.Rows.Add(kod, planKg, faktKg, roznica);
            }

            dgvPrzychody.DataSource = dtP;
            if (dgvPrzychody.Columns["Plan (kg)"] != null) dgvPrzychody.Columns["Plan (kg)"].DefaultCellStyle.Format = "N0";
            if (dgvPrzychody.Columns["Fakt (kg)"] != null) dgvPrzychody.Columns["Fakt (kg)"].DefaultCellStyle.Format = "N0";
            if (dgvPrzychody.Columns["Różnica"] != null) dgvPrzychody.Columns["Różnica"].DefaultCellStyle.Format = "N0";
        }

        /// <summary>
        /// Suma WZ/WZ-W per produkt.
        /// </summary>
        private async Task<Dictionary<int, decimal>> PobierzSumeWydanPoProdukcieAsync(DateTime dzien)
        {
            var sumaWydan = new Dictionary<int, decimal>();
            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            const string sql = @"
                SELECT MZ.idtw, SUM(ABS(MZ.ilosc))
                FROM [HANDEL].[HM].[MZ] MZ 
                JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                WHERE MG.seria IN ('sWZ', 'sWZ-W') AND MG.aktywny=1 AND MG.data = @Dzien 
                GROUP BY MZ.idtw";
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                sumaWydan[reader.GetInt32(0)] = ReadDecimal(reader, 1);
            return sumaWydan;
        }

        /// <summary>
        /// Wylicza prognozę (z Harmonogramu) per produkt i faktyczny przychód z PWU per produkt.
        /// Reguły:
        ///  - obliczamy całkowitą masę tuszek: masaDek * 0.78,
        ///  - Tuszka A = 85% puli, Tuszka B (i „elementy”) = 15% puli,
        ///  - „Kurczak A” dostaje pulę A; „Kurczak B” – pulę B; pozostałe z puli B wg YieldByKod.
        /// </summary>
        private async Task<(Dictionary<int, decimal> plan, Dictionary<int, decimal> fakt)> PrognozaIFaktPrzychoduPerProduktAsync(DateTime dzien)
        {
            // 1) POLICZ surowiec z harmonogramu
            decimal sumaMasyDek = 0m; // WagaDek * SztukiDek
            await using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                const string sql = @"
                    SELECT WagaDek, SztukiDek 
                    FROM dbo.HarmonogramDostaw 
                    WHERE DataOdbioru = @Dzien AND Bufor = 'Potwierdzony'";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var waga = ReadDecimal(rdr, 0);
                    var szt = ReadDecimal(rdr, 1);
                    sumaMasyDek += (waga * szt);
                }
            }

            // współczynniki (jak w Twoim starym kodzie)
            decimal p_tuszka = 0.78m;
            decimal udzA = 0.85m;
            decimal udzB = 0.15m;

            decimal pula = sumaMasyDek * p_tuszka;
            decimal pulaA = pula * udzA;
            decimal pulaB = pula * udzB;

            // 2) Zmapuj puli na produkty
            var plan = new Dictionary<int, decimal>();
            foreach (var kv in _twKatalogCache) // idtw->kod
            {
                var idtw = kv.Key;
                var kod = kv.Value;

                if (IsKurczakA(kod))
                {
                    plan[idtw] = pulaA;
                }
                else if (IsKurczakB(kod))
                {
                    plan[idtw] = pulaB;
                }
                else
                {
                    if (YieldByKod.TryGetValue(kod, out var share) && share > 0m)
                        plan[idtw] = Math.Max(0m, pulaB * share);
                    else
                        plan[idtw] = 0m; // brak skonfigurowanej wydajności
                }
            }

            // 3) Faktyczny przychód PWU per produkt
            var fakt = new Dictionary<int, decimal>();
            await using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                const string sql = @"
                    SELECT MZ.idtw, SUM(ABS(MZ.ilosc)) 
                    FROM [HANDEL].[HM].[MZ] MZ 
                    JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id 
                    WHERE MG.seria = 'sPWU' AND MG.aktywny=1 AND MG.data = @Dzien 
                    GROUP BY MZ.idtw";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                    fakt[rdr.GetInt32(0)] = ReadDecimal(rdr, 1);
            }
            return (plan, fakt);
        }

        private void dgvZamowienia_RowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var rowObj = dgvZamowienia.Rows[e.RowIndex].DataBoundItem as DataRowView;
            if (rowObj == null) return;

            var status = rowObj.Row.Table.Columns.Contains("Status")
                ? rowObj.Row["Status"]?.ToString()
                : null;

            // Bezpieczne pobranie daty utworzenia (może być null)
            DateTime? dataUtw = null;
            if (rowObj.Row.Table.Columns.Contains("DataUtworzenia"))
            {
                var val = rowObj.Row["DataUtworzenia"];
                if (val != DBNull.Value && val != null)
                    dataUtw = (DateTime)val;
            }

            var row = dgvZamowienia.Rows[e.RowIndex];
            // Kolorowanie: zamówienia z wydaniami (standard), wydania bez zamówień (specjalny kolor)
            if (status == "Wydanie bez zamówienia")
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 240, 200); // jasny pomarańczowy
                row.DefaultCellStyle.ForeColor = Color.Black;
                row.DefaultCellStyle.Font = new Font(dgvZamowienia.Font, FontStyle.Italic);
            }
            else if (status == "Anulowane")
            {
                row.DefaultCellStyle.ForeColor = Color.Gray;
                row.DefaultCellStyle.Font = new Font(dgvZamowienia.Font, FontStyle.Strikeout);
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 230, 230); // czerwona poświata
            }
            else if (status == "Zrealizowane")
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(220, 255, 220);
                row.DefaultCellStyle.ForeColor = SystemColors.ControlText;
                row.DefaultCellStyle.Font = new Font(dgvZamowienia.Font, FontStyle.Regular);
            }
            else
            {
                row.DefaultCellStyle.ForeColor = SystemColors.ControlText;
                row.DefaultCellStyle.BackColor = (e.RowIndex % 2 == 0) ? Color.White : Color.FromArgb(248, 248, 248);
                row.DefaultCellStyle.Font = new Font(dgvZamowienia.Font, FontStyle.Regular);
            }
        }
        // ===== Pomocnicza: odczyt ID z aktualnego wiersza dgvZamowienia =====
        private bool TrySetAktualneIdZamowieniaFromGrid(out int id)
        {
            id = 0;
            if (dgvZamowienia.CurrentRow == null) return false;

            // 1) Najpierw spróbuj przez DataRowView
            if (dgvZamowienia.CurrentRow.DataBoundItem is DataRowView rv &&
                rv.Row.Table.Columns.Contains("Id") &&
                rv.Row["Id"] != DBNull.Value)
            {
                id = Convert.ToInt32(rv.Row["Id"]);
                if (id > 0) { _aktualneIdZamowienia = id; return true; }
            }

            // 2) Fallback – bezpośrednio z komórki „Id”
            if (dgvZamowienia.Columns.Contains("Id"))
            {
                var cellVal = dgvZamowienia.CurrentRow.Cells["Id"]?.Value;
                if (cellVal != null && cellVal != DBNull.Value && int.TryParse(cellVal.ToString(), out id) && id > 0)
                {
                    _aktualneIdZamowienia = id;
                    return true;
                }
            }

            _aktualneIdZamowienia = null;
            return false;
        }


        #endregion

        #region Filtrowanie
        private void ZastosujFiltry()
        {
            if (_dtZamowienia.DefaultView == null) return;

            var warunki = new List<string>();

            var txt = txtFiltrujOdbiorce.Text?.Trim().Replace("'", "''");
            if (!string.IsNullOrEmpty(txt))
                warunki.Add($"Odbiorca LIKE '%{txt}%'");

            if (cbFiltrujHandlowca.SelectedIndex > 0)
            {
                var hand = cbFiltrujHandlowca.SelectedItem?.ToString()?.Replace("'", "''");
                if (!string.IsNullOrEmpty(hand))
                    warunki.Add($"Handlowiec = '{hand}'");
            }

            _dtZamowienia.DefaultView.RowFilter = string.Join(" AND ", warunki);
        }
        #endregion

        // Metoda do obsługi usuwania zamówienia (wiersza)
        private async void btnUsun_Click(object? sender, EventArgs e)
        {
            if (!TrySetAktualneIdZamowieniaFromGrid(out var id) || id <= 0)
            {
                MessageBox.Show("Najpierw wybierz zamówienie do usunięcia.", "Brak wyboru", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show("Czy na pewno chcesz TRWALE usunąć wybrane zamówienie? Tej operacji nie można cofnąć.", "Potwierdź usunięcie", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result == DialogResult.Yes)
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    using (var cmd = new SqlCommand("DELETE FROM dbo.ZamowieniaMiesoTowar WHERE ZamowienieId = @Id", cn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                    using (var cmd = new SqlCommand("DELETE FROM dbo.ZamowieniaMieso WHERE Id = @Id", cn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                    MessageBox.Show("Zamówienie zostało trwale usunięte.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await OdswiezWszystkieDaneAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas usuwania zamówienia: {ex.Message}", "Błąd krytyczny", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}