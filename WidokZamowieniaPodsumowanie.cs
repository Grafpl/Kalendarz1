// Plik: WidokZamowieniaPodsumowanie.cs
// WERSJA 5.0 - CENTRUM ANALITYCZNE ZAMÓWIEŃ (FINALNA)
// Zmiany:
// 1. Dodano filtrowanie zamówień po konkretnym produkcie.
// 2. Poprawiono logikę pobierania faktycznych wydań z Symfonii.
// 3. Ulepszono siatkę agregacji, aby pokazywała tylko towary z katalogu '67095'.
// 4. Usunięto zbędny panel przychodów - jego dane są teraz w siatce agregacji.
// 5. Zaimplementowano wszystkie pozostałe prośby: brak zaznaczenia, statusy, anulowanie.

#nullable enable
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

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
        private readonly Dictionary<int, string> _twKodCache = new();
        private readonly Dictionary<int, string> _twKatalogCache = new();
        private readonly Dictionary<string, string> _userCache = new();
        private readonly List<string> _handlowcyCache = new();

        public WidokZamowieniaPodsumowanie()
        {
            InitializeComponent();
            this.Load += WidokZamowieniaPodsumowanie_Load;
        }

        private async void WidokZamowieniaPodsumowanie_Load(object? sender, EventArgs e)
        {
            _selectedDate = DateTime.Today;
            UstawPrzyciskiDniTygodnia();
            SzybkiGrid(dgvZamowienia);
            SzybkiGrid(dgvSzczegoly);
            SzybkiGrid(dgvAgregacja);
            SzybkiGrid(dgvPrzychody);

            await ZaladujDanePoczatkoweAsync();
            await OdswiezWszystkieDaneAsync();
        }

        #region Inicjalizacja i UI

        private void UstawPrzyciskiDniTygodnia()
        {
            _dayButtons.AddRange(new[] { btnPon, btnWt, btnSr, btnCzw, btnPt, btnSo, btnNd });
            foreach (var btn in _dayButtons)
            {
                btn.Click += DzienButton_Click;
            }
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
            catch { /* Ignoruj błąd */ }
        }

        #endregion

        #region Nawigacja i Zdarzenia

        private async void DzienButton_Click(object? sender, EventArgs e)
        {
            if (sender is Button clickedButton && clickedButton.Tag is DateTime date)
            {
                _selectedDate = date;
                AktualizujDatyPrzyciskow();
                await OdswiezWszystkieDaneAsync();
            }
        }

        private async void btnTydzienPrev_Click(object sender, EventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(-7);
            AktualizujDatyPrzyciskow();
            await OdswiezWszystkieDaneAsync();
        }

        private async void btnTydzienNext_Click(object sender, EventArgs e)
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
            if (_aktualneIdZamowienia is null)
            {
                MessageBox.Show("Najpierw kliknij wiersz z zamówieniem, aby je wybrać.", "Brak wyboru", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var widokZamowienia = new WidokZamowienia(UserID, _aktualneIdZamowienia);
            if (widokZamowienia.ShowDialog(this) == DialogResult.OK)
            {
                Task.Run(async () => await OdswiezWszystkieDaneAsync());
            }
        }

        private async void btnAnuluj_Click(object sender, EventArgs e)
        {
            if (_aktualneIdZamowienia is null)
            {
                MessageBox.Show("Najpierw kliknij wiersz z zamówieniem, które chcesz anulować.", "Brak wyboru", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        private async void Filtry_Changed(object sender, EventArgs e)
        {
            await WczytajZamowieniaDlaDniaAsync(_selectedDate);
        }

        #endregion

        #region Wczytywanie i Przetwarzanie Danych

        private async Task OdswiezWszystkieDaneAsync()
        {
            this.Cursor = Cursors.WaitCursor;
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
                MessageBox.Show($"Błąd podczas odświeżania danych: {ex.Message}", "Błąd Krytyczny", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
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
                    int id = reader.GetInt32(0);
                    string kod = reader.GetString(1);
                    string katalog = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    _twKodCache[id] = kod;
                    if (katalog == "67095")
                    {
                        _twKatalogCache[id] = kod;
                    }
                }
            }

            cbFiltrujTowar.DataSource = new BindingSource(_twKatalogCache.OrderBy(x => x.Value).ToList(), null);
            cbFiltrujTowar.DisplayMember = "Value";
            cbFiltrujTowar.ValueMember = "Key";
            cbFiltrujTowar.Items.Insert(0, new KeyValuePair<int, string>(0, "— Wszystkie towary —"));
            cbFiltrujTowar.SelectedIndex = 0;


            _userCache.Clear();
            await using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                await using var cmd = new SqlCommand("SELECT ID, Name FROM dbo.operators", cn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var idStr = reader["ID"]?.ToString();
                    if (!string.IsNullOrEmpty(idStr))
                    {
                        _userCache[idStr] = reader["Name"]?.ToString() ?? "";
                    }
                }
            }

            _handlowcyCache.Clear();
            await using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                await using var cmd = new SqlCommand("SELECT DISTINCT CDim_Handlowiec_Val FROM [HANDEL].[SSCommon].[ContractorClassification] WHERE CDim_Handlowiec_Val IS NOT NULL ORDER BY 1", cn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    _handlowcyCache.Add(reader.GetString(0));
                }
            }

            cbFiltrujHandlowca.Items.Clear();
            cbFiltrujHandlowca.Items.Add("— Wszyscy —");
            cbFiltrujHandlowca.Items.AddRange(_handlowcyCache.ToArray());
            cbFiltrujHandlowca.SelectedIndex = 0;
        }

        private async Task WczytajZamowieniaDlaDniaAsync(DateTime dzien)
        {
            _dtZamowienia.Clear();
            if (_dtZamowienia.Columns.Count == 0)
            {
                _dtZamowienia.Columns.Add("Id", typeof(int));
                _dtZamowienia.Columns.Add("Odbiorca", typeof(string));
                _dtZamowienia.Columns.Add("Handlowiec", typeof(string));
                _dtZamowienia.Columns.Add("IloscZamowiona", typeof(decimal));
                _dtZamowienia.Columns.Add("IloscFaktyczna", typeof(decimal));
                _dtZamowienia.Columns.Add("DataUtworzenia", typeof(DateTime));
                _dtZamowienia.Columns.Add("Utworzyl", typeof(string));
                _dtZamowienia.Columns.Add("Status", typeof(string));
            }

            var kontrahenci = new Dictionary<int, (string Nazwa, string Handlowiec)>();
            await using (var cnHandel = new SqlConnection(_connHandel))
            {
                await cnHandel.OpenAsync();
                const string sqlKontrahenci = @"
                    SELECT c.Id, c.Shortcut, wym.CDim_Handlowiec_Val 
                    FROM [HANDEL].[SSCommon].[STContractors] c
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym ON c.Id = wym.ElementId";
                await using var cmdKontrahenci = new SqlCommand(sqlKontrahenci, cnHandel);
                await using var readerK = await cmdKontrahenci.ExecuteReaderAsync();
                while (await readerK.ReadAsync())
                {
                    kontrahenci[readerK.GetInt32(0)] = (readerK.GetString(1), readerK.IsDBNull(2) ? "" : readerK.GetString(2));
                }
            }

            int? selectedProductId = null;
            if (cbFiltrujTowar.SelectedIndex > 0 && cbFiltrujTowar.SelectedValue is int id)
            {
                selectedProductId = id;
            }

            var tempTable = new DataTable();
            await using (var cnLibra = new SqlConnection(_connLibra))
            {
                await cnLibra.OpenAsync();
                string sql = @"
                    SELECT zm.Id, zm.KlientId, SUM(zmt.Ilosc) AS Ilosc, zm.DataUtworzenia, zm.IdUser, zm.Status
                    FROM [dbo].[ZamowieniaMieso] zm
                    JOIN [dbo].[ZamowieniaMiesoTowar] zmt ON zm.Id = zmt.ZamowienieId
                    WHERE zm.DataZamowienia = @Dzien " +
                    (selectedProductId.HasValue ? "AND zmt.KodTowaru = @TowarId " : "") +
                    @"GROUP BY zm.Id, zm.KlientId, zm.DataUtworzenia, zm.IdUser, zm.Status
                    ORDER BY zm.Id";

                await using var cmd = new SqlCommand(sql, cnLibra);
                cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
                if (selectedProductId.HasValue) cmd.Parameters.AddWithValue("@TowarId", selectedProductId.Value);
                using var adapter = new SqlDataAdapter(cmd);
                adapter.Fill(tempTable);
            }

            var faktyczneWydania = await PobierzFaktyczneWydaniaAsync(dzien, selectedProductId);

            foreach (DataRow row in tempTable.Rows)
            {
                int idm = Convert.ToInt32(row["Id"]);
                int klientId = Convert.ToInt32(row["KlientId"]);
                string idUser = row["IdUser"]?.ToString() ?? "";

                var (nazwa, handlowiec) = kontrahenci.TryGetValue(klientId, out var kh) ? kh : ($"Nieznany ({klientId})", "");

                _dtZamowienia.Rows.Add(
                    idm,
                    nazwa,
                    handlowiec,
                    row["Ilosc"],
                    faktyczneWydania.TryGetValue(idm, out var iloscWz) ? iloscWz : 0m,
                    row["DataUtworzenia"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["DataUtworzenia"]),
                    _userCache.TryGetValue(idUser, out var user) ? user : "Brak",
                    row["Status"]?.ToString() ?? "Nowe"
                );
            }

            _bsZamowienia.DataSource = _dtZamowienia;
            dgvZamowienia.DataSource = _bsZamowienia;
            dgvZamowienia.ClearSelection();
            Filtry_Changed(this, EventArgs.Empty);

            dgvZamowienia.Columns["Id"]!.Visible = false;
            dgvZamowienia.Columns["IloscZamowiona"]!.DefaultCellStyle.Format = "N0";
            dgvZamowienia.Columns["IloscZamowiona"]!.HeaderText = "Zamówiono (kg)";
            dgvZamowienia.Columns["IloscFaktyczna"]!.DefaultCellStyle.Format = "N0";
            dgvZamowienia.Columns["IloscFaktyczna"]!.HeaderText = "Wydano (kg)";
            dgvZamowienia.Columns["DataUtworzenia"]!.HeaderText = "Utworzono";
            dgvZamowienia.Columns["DataUtworzenia"]!.DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
        }

        private async Task<Dictionary<int, decimal>> PobierzFaktyczneWydaniaAsync(DateTime dzien, int? towarId = null)
        {
            var wynik = new Dictionary<int, decimal>();
            var zamowieniaNaDzien = new Dictionary<int, int>();
            await using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                await using var cmd = new SqlCommand("SELECT Id, KlientId FROM dbo.ZamowieniaMieso WHERE DataZamowienia = @Dzien", cn);
                cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) { zamowieniaNaDzien[reader.GetInt32(0)] = reader.GetInt32(1); }
            }

            if (!zamowieniaNaDzien.Any()) return wynik;
            var klientIdsDlaWydan = zamowieniaNaDzien.Values.Distinct().ToList();

            var wydaniaKlienta = new Dictionary<int, decimal>();
            if (klientIdsDlaWydan.Any())
            {
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    string sqlWz = $@"
                        SELECT DK.khid, SUM(ABS(MZ.ilosc))
                        FROM [HANDEL].[HM].[MZ] MZ
                        JOIN [HANDEL].[HM].[DK] DK ON MZ.super = DK.id
                        WHERE DK.seria IN ('sWZ', 'sWZ-W') AND DK.data = @Dzien AND DK.khid IN ({string.Join(",", klientIdsDlaWydan)}) " +
                        (towarId.HasValue ? "AND MZ.idtw = @TowarId " : "") +
                        "GROUP BY DK.khid";
                    await using var cmd = new SqlCommand(sqlWz, cn);
                    cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
                    if (towarId.HasValue) cmd.Parameters.AddWithValue("@TowarId", towarId.Value);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync()) { wydaniaKlienta[reader.GetInt32(0)] = reader.GetDecimal(1); }
                }
            }

            foreach (var zam in zamowieniaNaDzien)
            {
                if (wydaniaKlienta.TryGetValue(zam.Value, out var ilosc))
                {
                    wynik[zam.Key] = ilosc;
                }
            }
            return wynik;
        }

        private async void dgvZamowienia_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgvZamowienia.CurrentRow?.DataBoundItem is DataRowView rowView)
            {
                var row = rowView.Row;
                int id = Convert.ToInt32(row["Id"]);
                _aktualneIdZamowienia = id;
                await WyswietlSzczegolyZamowieniaAsync(id);
            }
            else
            {
                _aktualneIdZamowienia = null;
                WyczyscSzczegoly();
            }
        }

        private async Task WyswietlSzczegolyZamowieniaAsync(int zamowienieId)
        {
            var dtSzczegoly = new DataTable();
            await using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                const string sql = "SELECT KodTowaru, Ilosc, Uwagi FROM [dbo].[ZamowieniaMieso] ZM JOIN [dbo].[ZamowieniaMiesoTowar] ZMT ON ZM.Id = ZMT.ZamowienieId WHERE ZamowienieId = @Id";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Id", zamowienieId);
                using var adapter = new SqlDataAdapter(cmd);
                adapter.Fill(dtSzczegoly);
            }

            dtSzczegoly.Columns.Add("Produkt", typeof(string));
            string notatki = "";
            if (dtSzczegoly.Rows.Count > 0) { notatki = dtSzczegoly.Rows[0]["Uwagi"]?.ToString() ?? ""; }

            foreach (DataRow row in dtSzczegoly.Rows)
            {
                int idTowaru = Convert.ToInt32(row["KodTowaru"]);
                row["Produkt"] = _twKodCache.TryGetValue(idTowaru, out var kod) ? kod : $"Nieznany ({idTowaru})";
            }

            txtNotatki.Text = notatki;
            dgvSzczegoly.DataSource = dtSzczegoly;

            dgvSzczegoly.Columns["KodTowaru"]!.Visible = false;
            dgvSzczegoly.Columns["Uwagi"]!.Visible = false;
            dgvSzczegoly.Columns["Ilosc"]!.DefaultCellStyle.Format = "N0";
            dgvSzczegoly.Columns["Produkt"]!.DisplayIndex = 0;
        }

        private async Task WyswietlAgregacjeProduktowAsync(DateTime dzien)
        {
            var dtAgregacja = new DataTable();
            dtAgregacja.Columns.Add("Produkt", typeof(string));
            dtAgregacja.Columns.Add("Zamówiono", typeof(decimal));
            dtAgregacja.Columns.Add("Wydano", typeof(decimal));
            dtAgregacja.Columns.Add("PlanowanyPrzychód", typeof(decimal));
            dtAgregacja.Columns.Add("FaktycznyPrzychód", typeof(decimal));

            var sumaWydan = await PobierzSumeWydanPoProdukcieAsync(dzien);
            var (planPrzychodu, faktPrzychodu) = await PobierzDanePrzychodowDlaAgregacjiAsync(dzien);

            var sumaZamowien = new Dictionary<int, decimal>();
            var zamowieniaIds = _dtZamowienia.AsEnumerable().Select(r => r.Field<int>("Id")).ToList();
            if (zamowieniaIds.Any())
            {
                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    var sql = $"SELECT KodTowaru, SUM(Ilosc) FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId IN ({string.Join(",", zamowieniaIds)}) GROUP BY KodTowaru";
                    using var cmd = new SqlCommand(sql, cn);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync()) { sumaZamowien[reader.GetInt32(0)] = reader.GetDecimal(1); }
                }
            }

            foreach (var towar in _twKatalogCache.OrderBy(kvp => kvp.Value))
            {
                dtAgregacja.Rows.Add(
                    towar.Value,
                    sumaZamowien.TryGetValue(towar.Key, out var zam) ? zam : 0m,
                    sumaWydan.TryGetValue(towar.Key, out var wyd) ? wyd : 0m,
                    planPrzychodu.TryGetValue(towar.Key, out var plan) ? plan : 0m,
                    faktPrzychodu.TryGetValue(towar.Key, out var fakt) ? fakt : 0m
                );
            }

            dgvAgregacja.DataSource = dtAgregacja;
            foreach (DataGridViewColumn col in dgvAgregacja.Columns)
            {
                if (col.Name != "Produkt") col.DefaultCellStyle.Format = "N0";
            }
        }


        private void AktualizujPodsumowanieDnia()
        {
            int liczbaZamowien = _bsZamowienia.Count;
            decimal sumaKg = 0;
            if (_bsZamowienia.List is ICollection<DataRowView> rows)
            {
                sumaKg = rows.Sum(r => r.Row.Field<decimal?>("IloscZamowiona") ?? 0);
            }

            lblPodsumowanie.Text = $"Liczba zamówień: {liczbaZamowien} | Łączna waga: {sumaKg:N0} kg";
        }

        private void WyczyscSzczegoly()
        {
            dgvSzczegoly.DataSource = null;
            txtNotatki.Clear();
            _aktualneIdZamowienia = null;
        }

        private async Task WczytajDanePrzychodowAsync(DateTime dzien)
        {
            var dtPrzychody = new DataTable();
            dtPrzychody.Columns.Add("Dostawca", typeof(string));
            dtPrzychody.Columns.Add("Plan (kg)", typeof(decimal));
            dtPrzychody.Columns.Add("Realizacja (kg)", typeof(decimal));

            var plan = new Dictionary<string, decimal>();
            await using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                await using var cmd = new SqlCommand("SELECT Dostawca, SUM(SztukiDek * WagaDek) FROM dbo.HarmonogramDostaw WHERE DataOdbioru = @Dzien AND Bufor = 'Potwierdzony' GROUP BY Dostawca", cn);
                cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) { plan[reader.GetString(0)] = Convert.ToDecimal(reader.GetValue(1)); }
            }

            var fakt = new Dictionary<string, decimal>();
            await using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                await using var cmd = new SqlCommand("SELECT C.Shortcut, SUM(ABS(MZ.ilosc)) FROM [HANDEL].[HM].[MZ] MZ JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id JOIN [HANDEL].[SSCommon].[STContractors] C ON MG.khid = C.id WHERE MG.seria = 'sPWU' AND MG.data = @Dzien GROUP BY C.Shortcut", cn);
                cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) { fakt[reader.GetString(0)] = reader.GetDecimal(1); }
            }

            var allKeys = plan.Keys.Union(fakt.Keys).Distinct().OrderBy(k => k);

            foreach (var key in allKeys)
            {
                dtPrzychody.Rows.Add(key, plan.TryGetValue(key, out var p) ? p : 0m, fakt.TryGetValue(key, out var f) ? f : 0m);
            }

            dgvPrzychody.DataSource = dtPrzychody;
            dgvPrzychody.Columns["Plan (kg)"]!.DefaultCellStyle.Format = "N0";
            dgvPrzychody.Columns["Realizacja (kg)"]!.DefaultCellStyle.Format = "N0";
        }

        private async Task<Dictionary<int, decimal>> PobierzSumeWydanPoProdukcieAsync(DateTime dzien)
        {
            var sumaWydan = new Dictionary<int, decimal>();
            await using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                const string sqlWz = @"
                    SELECT MZ.idtw, SUM(ABS(MZ.ilosc))
                    FROM [HANDEL].[HM].[MZ] MZ JOIN [HANDEL].[HM].[DK] DK ON MZ.super = DK.id
                    WHERE DK.seria IN ('sWZ', 'sWZ-W') AND DK.data = @Dzien GROUP BY MZ.idtw";
                await using var cmd = new SqlCommand(sqlWz, cn);
                cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    sumaWydan[reader.GetInt32(0)] = reader.GetDecimal(1);
                }
            }
            return sumaWydan;
        }

        private async Task<(Dictionary<int, decimal> plan, Dictionary<int, decimal> fakt)> PobierzDanePrzychodowDlaAgregacjiAsync(DateTime dzien)
        {
            var plan = new Dictionary<int, decimal>();
            var fakt = new Dictionary<int, decimal>();

            var dostawcyPlan = new Dictionary<string, decimal>();
            await using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                await using var cmd = new SqlCommand("SELECT Dostawca, SUM(SztukiDek * WagaDek) FROM dbo.HarmonogramDostaw WHERE DataOdbioru = @Dzien AND Bufor = 'Potwierdzony' GROUP BY Dostawca", cn);
                cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) { dostawcyPlan[reader.GetString(0)] = Convert.ToDecimal(reader.GetValue(1)); }
            }

            foreach (var kvp in dostawcyPlan)
            {
                var matching_tw = _twKodCache.FirstOrDefault(x => x.Value == kvp.Key);
                if (matching_tw.Key != 0)
                {
                    plan[matching_tw.Key] = kvp.Value;
                }
            }

            await using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                await using var cmd = new SqlCommand("SELECT MZ.idtw, SUM(ABS(MZ.ilosc)) FROM [HANDEL].[HM].[MZ] MZ JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id WHERE MG.seria = 'sPWU' AND MG.data = @Dzien GROUP BY MZ.idtw", cn);
                cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    fakt[reader.GetInt32(0)] = reader.GetDecimal(1);
                }
            }

            return (plan, fakt);
        }

        private void dgvZamowienia_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var rowView = dgvZamowienia.Rows[e.RowIndex].DataBoundItem as DataRowView;
            if (rowView == null) return;

            var status = rowView.Row["Status"]?.ToString();
            var row = dgvZamowienia.Rows[e.RowIndex];

            switch (status)
            {
                case "Anulowane":
                    row.DefaultCellStyle.ForeColor = Color.Gray;
                    row.DefaultCellStyle.Font = new Font(dgvZamowienia.Font, FontStyle.Strikeout);
                    break;
                case "Zrealizowane":
                    row.DefaultCellStyle.BackColor = Color.FromArgb(220, 255, 220); // Jasnozielony
                    break;
                default: // Nowe i inne
                    row.DefaultCellStyle.ForeColor = SystemColors.ControlText;
                    row.DefaultCellStyle.BackColor = (e.RowIndex % 2 == 0) ? Color.White : Color.FromArgb(248, 248, 248);
                    row.DefaultCellStyle.Font = new Font(dgvZamowienia.Font, FontStyle.Regular);
                    break;
            }
        }


        #endregion
    }
}

