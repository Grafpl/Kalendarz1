#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using static Kalendarz1.CenoweMetody;

namespace Kalendarz1
{
    public partial class WidokZamowieniaPodsumowanie : Form
    {
        // ====== Połączenia ======
        private readonly string connectionString1 = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string connectionString2 = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        // ====== Serwisy/proxy z Twojego projektu ======
        private readonly RozwijanieComboBox RozwijanieComboBox = new RozwijanieComboBox();
        private readonly NazwaZiD nazwaZiD = new NazwaZiD();
        private readonly DataService dataService = new DataService();

        // ====== Daty tygodnia ======
        private DateTime startOfWeek;
        private DateTime poniedzialek;
        private DateTime wtorek;
        private DateTime sroda;
        private DateTime czwartek;
        private DateTime piatek;

        // ====== Agregacje ======
        private decimal totalIloscZamowiona = 0m;
        private readonly Dictionary<string, decimal> dzienneSumaIloscZamowiona = new();

        // ====== Stan UI/otwarcia ======
        public string UserID { get; set; } = string.Empty;
        private int? aktualneIdZamowienia; // Id ostatnio klikniętego zamówienia

        // ====== Cache kodów towarów (ID -> Kod) ======
        private readonly Dictionary<int, string> _twKodCache = new();

        public WidokZamowieniaPodsumowanie()
        {
            InitializeComponent();
            WindowState = FormWindowState.Maximized;

            // Towary (combo + cache)
            ZaladujTowary();

            // Twoja metoda – pokazuje pojemności tuszek itp.
            nazwaZiD.PokazPojTuszki(dataGridSumaPartie);

            // Uniwersalny click do wszystkich dziennych gridów
            dataGridViewPoniedzialek.CellClick += UniwersalnyCellClick;
            dataGridViewWtorek.CellClick += UniwersalnyCellClick;
            dataGridViewSroda.CellClick += UniwersalnyCellClick;
            dataGridViewCzwartek.CellClick += UniwersalnyCellClick;
            dataGridViewPiatek.CellClick += UniwersalnyCellClick;

            // Reaguj na zmianę towaru
            comboBoxTowar.SelectedIndexChanged += comboBoxTowar_SelectedIndexChanged;
        }

        // ====== Zdarzenia wysokiego poziomu ======
        private void comboBoxTowar_SelectedIndexChanged(object? sender, EventArgs e)
        {
            OdswiezPodsumowanie();
        }

        private void myCalendar_DateChanged(object? sender, DateRangeEventArgs e)
        {
            OdswiezPodsumowanie();
        }

        private void buttonOdswiez_Click(object? sender, EventArgs e)
        {
            OdswiezPodsumowanie();
            nazwaZiD.PokazPojTuszki(dataGridSumaPartie);
        }

        private void CommandButton_Update_Click(object? sender, EventArgs e)
        {
            // NOWE zamówienie
            var widokZamowienia = new WidokZamowienia(App.UserID, null);
            widokZamowienia.Show();
        }

        private void buttonModyfikuj_Click(object? sender, EventArgs e)
        {
            if (aktualneIdZamowienia is null)
            {
                MessageBox.Show("Najpierw kliknij wiersz z zamówieniem, aby wybrać Id.", "Brak wyboru",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var widokZamowienia = new WidokZamowienia(App.UserID, aktualneIdZamowienia);
            widokZamowienia.ShowDialog();
        }

        // ====== Rdzeń odświeżania ======
        private void OdswiezPodsumowanie()
        {
            try
            {
                if (comboBoxTowar.SelectedValue == null || comboBoxTowar.SelectedValue is DataRowView)
                {
                    MessageBox.Show("Wybierz towar z listy.", "Brak towaru", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                int selectedTowarId = Convert.ToInt32(comboBoxTowar.SelectedValue);

                // Data z kalendarza i tygodnie (pon-pt)
                DateTime selectedDate = myCalendar.SelectionStart.Date;
                // Poniedziałek (ISO): DayOfWeek.Monday = 1, Sunday=0
                int delta = ((int)selectedDate.DayOfWeek + 6) % 7; // ile dni od poniedziałku
                startOfWeek = selectedDate.AddDays(-delta);

                poniedzialek = startOfWeek;
                wtorek = startOfWeek.AddDays(1);
                sroda = startOfWeek.AddDays(2);
                czwartek = startOfWeek.AddDays(3);
                piatek = startOfWeek.AddDays(4);

                // Zamówienia (pon..pt)
                WyswietlPodsumowanie(selectedTowarId, startOfWeek, piatek);

                // Przewidywalne kg (pojemność/przychód vs zamówione) – każdy dzień
                PokazPrzewidywalneKilogramy(dataGridViewPoniedzialekSuma, poniedzialek, "Poniedziałek");
                PokazPrzewidywalneKilogramy(dataGridViewWtorekSuma, wtorek, "Wtorek");
                PokazPrzewidywalneKilogramy(dataGridViewSrodaSuma, sroda, "Środa");
                PokazPrzewidywalneKilogramy(dataGridViewCzwartekSuma, czwartek, "Czwartek");
                PokazPrzewidywalneKilogramy(dataGridViewPiatekSuma, piatek, "Piątek");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ====== Agregacja tygodnia ======
        private static DataTable CreateDataTable(string[] columnNames)
        {
            var table = new DataTable();
            foreach (var name in columnNames) table.Columns.Add(name);
            return table;
        }

        private void WyswietlPodsumowanie(int towarId, DateTime startOfWeek, DateTime endOfWeek)
        {
            totalIloscZamowiona = 0m;
            dzienneSumaIloscZamowiona.Clear();

            // 1) Pobierz zamówienia z LibraNet (bez cross-server JOIN – filtrujemy po KodTowaru=ID towaru)
            DataTable dataTable = new();
            using (var connection = new SqlConnection(connectionString1))
            using (var command = new SqlCommand(@"
                SELECT 
                    zm.Id,
                    zm.DataZamowienia, 
                    zm.KlientId, 
                    SUM(zmt.Ilosc) AS IloscZamowiona
                FROM [LibraNet].[dbo].[ZamowieniaMieso] zm
                JOIN [LibraNet].[dbo].[ZamowieniaMiesoTowar] zmt ON zm.Id = zmt.ZamowienieId
                WHERE 
                    zm.DataZamowienia BETWEEN @StartOfWeek AND @EndOfWeek
                    AND zmt.KodTowaru = @TowarId
                GROUP BY zm.Id, zm.DataZamowienia, zm.KlientId
                ORDER BY zm.DataZamowienia;", connection))
            {
                command.Parameters.AddWithValue("@StartOfWeek", startOfWeek.Date);
                command.Parameters.AddWithValue("@EndOfWeek", endOfWeek.Date);
                command.Parameters.AddWithValue("@TowarId", towarId);

                using var adapter = new SqlDataAdapter(command);
                adapter.Fill(dataTable);
            }

            // 2) Przygotuj per-dzień kontenery
            var dtPon = CreateDataTable(new[] { "IdZamowienia", "Klient", "Ilosc", "RIlosc" });
            var dtWto = CreateDataTable(new[] { "IdZamowienia", "Klient", "Ilosc", "RIlosc" });
            var dtSro = CreateDataTable(new[] { "IdZamowienia", "Klient", "Ilosc", "RIlosc" });
            var dtCzw = CreateDataTable(new[] { "IdZamowienia", "Klient", "Ilosc", "RIlosc" });
            var dtPia = CreateDataTable(new[] { "IdZamowienia", "Klient", "Ilosc", "RIlosc" });

            // 3) Iteruj po zamówieniach i dociągnij "faktyczne" z Handlu (sWZ/sWZ-W)
            foreach (DataRow row in dataTable.Rows)
            {
                var dataZamowienia = Convert.ToDateTime(row["DataZamowienia"]).Date;
                var klientId = row["KlientId"]?.ToString() ?? "";
                var idZamowienia = row["Id"]?.ToString() ?? "";
                var iloscZamowiona = row["IloscZamowiona"] == DBNull.Value ? 0m : Convert.ToDecimal(row["IloscZamowiona"]);

                totalIloscZamowiona += iloscZamowiona;

                // Nazwa klienta (z Twojej usługi)
                var daneOdbiorcy = dataService.PobierzDaneOdbiorcy(klientId);
                daneOdbiorcy.TryGetValue(RozwijanieComboBox.DaneKontrahenta.Kod, out var nazwaOdbiorcy);
                nazwaOdbiorcy ??= klientId;

                // Faktyczna ilość (WZ) dla dnia/klienta/towaru
                decimal faktycznaIlosc = 0m;
                using (var connReal = new SqlConnection(connectionString2))
                using (var realCmd = new SqlCommand(@"
                    SELECT SUM(ABS(MZ.ilosc)) AS RIlosc
                    FROM [HANDEL].[HM].[MZ] MZ
                    JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id
                    WHERE MG.data = @Data
                      AND MG.aktywny = 1
                      AND MG.khid = @KlientId
                      AND MG.seria IN ('sWZ', 'sWZ-W')
                      AND MZ.idtw = @TowarId", connReal))
                {
                    realCmd.Parameters.AddWithValue("@Data", dataZamowienia);
                    realCmd.Parameters.AddWithValue("@KlientId", klientId);
                    realCmd.Parameters.AddWithValue("@TowarId", towarId);

                    connReal.Open();
                    var realResult = realCmd.ExecuteScalar();
                    if (realResult != null && realResult != DBNull.Value)
                        faktycznaIlosc = Convert.ToDecimal(realResult);
                }

                int dayOffset = (int)(dataZamowienia.Subtract(startOfWeek.Date).TotalDays);
                switch (dayOffset)
                {
                    case 0: dtPon.Rows.Add(idZamowienia, nazwaOdbiorcy, iloscZamowiona, faktycznaIlosc); AddToDailySum("Poniedziałek", iloscZamowiona); break;
                    case 1: dtWto.Rows.Add(idZamowienia, nazwaOdbiorcy, iloscZamowiona, faktycznaIlosc); AddToDailySum("Wtorek", iloscZamowiona); break;
                    case 2: dtSro.Rows.Add(idZamowienia, nazwaOdbiorcy, iloscZamowiona, faktycznaIlosc); AddToDailySum("Środa", iloscZamowiona); break;
                    case 3: dtCzw.Rows.Add(idZamowienia, nazwaOdbiorcy, iloscZamowiona, faktycznaIlosc); AddToDailySum("Czwartek", iloscZamowiona); break;
                    case 4: dtPia.Rows.Add(idZamowienia, nazwaOdbiorcy, iloscZamowiona, faktycznaIlosc); AddToDailySum("Piątek", iloscZamowiona); break;
                }
            }

            // 4) Dodaj klientów, którzy mają RIlosc>0 bez zamówienia w danym dniu
            using (var connExtra = new SqlConnection(connectionString2))
            using (var extraCmd = new SqlCommand(@"
                SELECT MG.data, MG.khid, SUM(ABS(MZ.ilosc)) AS RIlosc
                FROM [HANDEL].[HM].[MZ] MZ
                JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id
                WHERE MG.data BETWEEN @StartOfWeek AND @EndOfWeek
                  AND MG.seria IN ('sWZ', 'sWZ-W')
                  AND MG.aktywny = 1
                  AND MZ.idtw = @TowarId
                GROUP BY MG.data, MG.khid;", connExtra))
            {
                extraCmd.Parameters.AddWithValue("@StartOfWeek", startOfWeek.Date);
                extraCmd.Parameters.AddWithValue("@EndOfWeek", endOfWeek.Date);
                extraCmd.Parameters.AddWithValue("@TowarId", towarId);

                var odbiorcyBezZamowienia = new DataTable();
                using var extraAdapter = new SqlDataAdapter(extraCmd);
                extraAdapter.Fill(odbiorcyBezZamowienia);

                foreach (DataRow odb in odbiorcyBezZamowienia.Rows)
                {
                    DateTime data = Convert.ToDateTime(odb["data"]).Date;
                    string khid = odb["khid"]?.ToString() ?? "";
                    decimal rilosc = odb["RIlosc"] == DBNull.Value ? 0m : Convert.ToDecimal(odb["RIlosc"]);

                    bool alreadyExists = dataTable.AsEnumerable().Any(r =>
                        Convert.ToDateTime(r["DataZamowienia"]).Date == data &&
                        string.Equals(r["KlientId"]?.ToString(), khid, StringComparison.Ordinal));

                    if (alreadyExists) continue;

                    var odbiorca = dataService.PobierzDaneOdbiorcy(khid);
                    odbiorca.TryGetValue(RozwijanieComboBox.DaneKontrahenta.Kod, out var nazwa);
                    nazwa ??= khid;

                    int dzienOffset = (int)(data.Subtract(startOfWeek.Date).TotalDays);
                    switch (dzienOffset)
                    {
                        case 0: dtPon.Rows.Add("—", nazwa, 0m, rilosc); break;
                        case 1: dtWto.Rows.Add("—", nazwa, 0m, rilosc); break;
                        case 2: dtSro.Rows.Add("—", nazwa, 0m, rilosc); break;
                        case 3: dtCzw.Rows.Add("—", nazwa, 0m, rilosc); break;
                        case 4: dtPia.Rows.Add("—", nazwa, 0m, rilosc); break;
                    }
                }
            }

            // 5) Podłącz do widoków i sformatuj
            ConfigureDataGridView(dataGridViewPoniedzialek, dtPon);
            ConfigureDataGridView(dataGridViewWtorek, dtWto);
            ConfigureDataGridView(dataGridViewSroda, dtSro);
            ConfigureDataGridView(dataGridViewCzwartek, dtCzw);
            ConfigureDataGridView(dataGridViewPiatek, dtPia);

            // (opcjonalnie) log do konsoli
            Console.WriteLine($"Suma całkowita: {totalIloscZamowiona}");
            foreach (var dzien in dzienneSumaIloscZamowiona)
                Console.WriteLine($"{dzien.Key}: {dzien.Value}");
        }

        private void AddToDailySum(string day, decimal amount)
        {
            if (!dzienneSumaIloscZamowiona.ContainsKey(day))
                dzienneSumaIloscZamowiona[day] = 0m;
            dzienneSumaIloscZamowiona[day] += amount;
        }

        private void ConfigureDataGridView(DataGridView gridView, DataTable dataSource)
        {
            gridView.DataSource = dataSource;

            // Kolumny
            if (gridView.Columns.Contains("IdZamowienia"))
                gridView.Columns["IdZamowienia"].Visible = false;
            if (gridView.Columns.Contains("Klient"))
                gridView.Columns["Klient"].Width = 180;

            // Wygląd
            gridView.RowHeadersVisible = false;
            gridView.RowTemplate.Height = 22;
            gridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridView.AllowUserToResizeRows = false;
            gridView.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            // Usuń stare subskrypcje, żeby nie dublować (przy częstym odświeżaniu)
            gridView.CellFormatting -= GridView_CellFormatting_Amounts;
            gridView.CellFormatting += GridView_CellFormatting_Amounts;

            gridView.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
            gridView.DefaultCellStyle.BackColor = Color.White;
            gridView.DefaultCellStyle.ForeColor = Color.Black;

            gridView.ClearSelection();
        }

        private void GridView_CellFormatting_Amounts(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (sender is not DataGridView gridView || e.Value is null) return;

            string columnName = gridView.Columns[e.ColumnIndex].Name;

            if (columnName is "Ilosc" or "RIlosc")
            {
                if (decimal.TryParse(e.Value.ToString(), out decimal value))
                {
                    e.Value = $"{value:N0} kg";
                    e.FormattingApplied = true;
                }
            }

            if (columnName == "RIlosc")
            {
                var row = gridView.Rows[e.RowIndex];
                if (gridView.Columns.Contains("Ilosc") &&
                    row.Cells["Ilosc"].Value != null &&
                    row.Cells["RIlosc"].Value != null &&
                    decimal.TryParse(row.Cells["Ilosc"].Value.ToString(), out decimal ilosc) &&
                    decimal.TryParse(row.Cells["RIlosc"].Value.ToString(), out decimal rilosc))
                {
                    if (rilosc >= ilosc)
                    {
                        row.Cells["RIlosc"].Style.ForeColor = Color.Green;
                        row.Cells["RIlosc"].Style.Font = gridView.Font;
                    }
                    else if (rilosc > 0 && rilosc < ilosc)
                    {
                        row.Cells["RIlosc"].Style.ForeColor = Color.Red;
                        row.Cells["RIlosc"].Style.Font = new Font(gridView.Font, FontStyle.Bold);
                    }
                    else
                    {
                        row.Cells["RIlosc"].Style.ForeColor = Color.Black;
                        row.Cells["RIlosc"].Style.Font = gridView.Font;
                    }
                }
            }
        }

        private void ZaladujTowary()
        {
            using var connection = new SqlConnection(connectionString2);
            using var adapter = new SqlDataAdapter(
                "SELECT [ID], [kod] FROM [HANDEL].[HM].[TW] WHERE katalog = '67095' ORDER BY kod ASC", connection);
            var towary = new DataTable();
            adapter.Fill(towary);

            if (towary.Rows.Count == 0)
            {
                MessageBox.Show("Brak danych w [HM].[TW].", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            comboBoxTowar.DisplayMember = "kod";
            comboBoxTowar.ValueMember = "ID";
            comboBoxTowar.DataSource = towary;

            // Cache ID->Kod (dla szczegółów zamówienia)
            _twKodCache.Clear();
            foreach (DataRow r in towary.Rows)
            {
                int id = Convert.ToInt32(r["ID"]);
                string kod = r["kod"]?.ToString() ?? id.ToString();
                _twKodCache[id] = kod;
            }
        }

        private void PokazPrzewidywalneKilogramy(DataGridView datagrid, DateTime dzien, string nazwaDnia)
        {
            var finalTable = new DataTable();
            finalTable.Columns.Add("Kategoria", typeof(string));
            finalTable.Columns.Add("Przewidywalny", typeof(string));
            finalTable.Columns.Add("Faktyczny", typeof(string));

            double sumTonazTuszkiA = 0;
            double sumTonazTuszkiB = 0;

            // 1) Harmonogram (LibraNet) – przewidywany przychód
            using (var connection = new SqlConnection(connectionString1))
            using (var command = new SqlCommand(@"
                SELECT LP, Auta, Dostawca, WagaDek, SztukiDek 
                FROM dbo.HarmonogramDostaw 
                WHERE DataOdbioru = @StartDate 
                  AND Bufor = 'Potwierdzony' 
                ORDER BY WagaDek DESC", connection))
            {
                command.Parameters.AddWithValue("@StartDate", dzien.Date);
                using var adapter = new SqlDataAdapter(command);
                var table = new DataTable();
                adapter.Fill(table);

                foreach (DataRow row in table.Rows)
                {
                    double wagaDek = row["WagaDek"] != DBNull.Value ? Convert.ToDouble(row["WagaDek"]) : 0.0;
                    int sztukiDek = row["SztukiDek"] != DBNull.Value ? Convert.ToInt32(row["SztukiDek"]) : 0;

                    double sredniaTuszka = wagaDek * 0.78;
                    double tonazTuszka = sredniaTuszka * sztukiDek;
                    double tonazA = tonazTuszka * 0.85; // przykład podziału
                    double tonazB = tonazTuszka * 0.15;

                    sumTonazTuszkiA += tonazA;
                    sumTonazTuszkiB += tonazB;
                }
            }

            // 2) Przypisanie po towarze
            double wynikPrzychodu;
            int selectedTowarId = Convert.ToInt32(comboBoxTowar.SelectedValue);
            if (selectedTowarId == 66443) // przykład: tuszka A
                wynikPrzychodu = sumTonazTuszkiA;
            else
                wynikPrzychodu = dataService.WydajnoscElement(sumTonazTuszkiB, selectedTowarId);

            // 3) Zamówione przewidywalnie (z agregacji dziennej)
            double przewidywalny = dzienneSumaIloscZamowiona.TryGetValue(nazwaDnia, out var agg)
                ? (double)agg
                : 0.0;

            // 4) Faktyczny przychód (sPWU)
            double faktycznyPrzychod = 0.0;
            using (var conn = new SqlConnection(connectionString2))
            using (var cmd = new SqlCommand(@"
                SELECT SUM(ABS(MZ.ilosc)) AS SumaIlosc
                FROM [HANDEL].[HM].[MZ] MZ
                JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id
                WHERE MG.seria = 'sPWU'
                  AND MG.aktywny = 1
                  AND MG.data = @dzien
                  AND MZ.idtw = @idtw", conn))
            {
                cmd.Parameters.AddWithValue("@dzien", dzien.Date);
                cmd.Parameters.AddWithValue("@idtw", selectedTowarId);
                conn.Open();
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    faktycznyPrzychod = Convert.ToDouble(result);
            }

            // 5) Faktycznie wydane (sWZ + sWZ-W)
            double faktyczneZamowienie = 0.0;
            using (var conn = new SqlConnection(connectionString2))
            using (var cmd = new SqlCommand(@"
                SELECT SUM(ABS(MZ.ilosc)) AS SumaIlosc
                FROM [HANDEL].[HM].[MZ] MZ
                JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id
                WHERE MG.seria IN ('sWZ', 'sWZ-W')
                  AND MG.aktywny = 1
                  AND MG.data = @dzien
                  AND MZ.idtw = @idtw", conn))
            {
                cmd.Parameters.AddWithValue("@dzien", dzien.Date);
                cmd.Parameters.AddWithValue("@idtw", selectedTowarId);
                conn.Open();
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    faktyczneZamowienie = Convert.ToDouble(result);
            }

            // 6) Tabela przeglądowa
            var r1 = finalTable.NewRow();
            r1["Kategoria"] = "Przychód";
            r1["Przewidywalny"] = $"{wynikPrzychodu:N0} kg";
            r1["Faktyczny"] = $"{faktycznyPrzychod:N0} kg";
            finalTable.Rows.Add(r1);

            var r2 = finalTable.NewRow();
            r2["Kategoria"] = "Zamówione";
            r2["Przewidywalny"] = $"{przewidywalny:N0} kg";
            r2["Faktyczny"] = $"{faktyczneZamowienie:N0} kg";
            finalTable.Rows.Add(r2);

            var r3 = finalTable.NewRow();
            r3["Kategoria"] = "Pozostało";
            double pozostaloKg = wynikPrzychodu - przewidywalny;
            double faktycznieKg = faktycznyPrzychod - faktyczneZamowienie;
            r3["Przewidywalny"] = $"{pozostaloKg:N0} kg";
            r3["Faktyczny"] = $"{faktycznieKg:N0} kg";
            finalTable.Rows.Add(r3);

            datagrid.DataSource = finalTable;
            datagrid.RowHeadersVisible = false;
            datagrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            datagrid.AllowUserToResizeRows = false;
            datagrid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            if (datagrid.Columns.Contains("Kategoria")) datagrid.Columns["Kategoria"].HeaderText = "Kategoria";
            if (datagrid.Columns.Contains("Przewidywalny")) datagrid.Columns["Przewidywalny"].HeaderText = "Przewidywalny";
            if (datagrid.Columns.Contains("Faktyczny")) datagrid.Columns["Faktyczny"].HeaderText = "Faktyczny";

            FormatSumRow(datagrid, 2);
        }

        private static void FormatSumRow(DataGridView gridView, int nrWiersz)
        {
            if (nrWiersz < 0 || nrWiersz >= gridView.Rows.Count) return;
            var sumRow = gridView.Rows[nrWiersz];
            sumRow.DefaultCellStyle.ForeColor = Color.Black;
            sumRow.DefaultCellStyle.Font = new Font(gridView.Font, FontStyle.Bold);
            gridView.RowHeadersVisible = false;
            gridView.ClearSelection();
        }

        // ====== Klik w wiersz dnia → szczegóły i Id ======
        private void UniwersalnyCellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (sender is not DataGridView gridView) return;

            if (!gridView.Columns.Contains("IdZamowienia"))
            {
                MessageBox.Show("Kolumna 'IdZamowienia' nie istnieje w tym widoku.", "Brak kolumny",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var val = gridView.Rows[e.RowIndex].Cells["IdZamowienia"].Value;
            if (val != null && int.TryParse(val.ToString(), out int idZamowienia))
            {
                aktualneIdZamowienia = idZamowienia;
                WyswietlSzczegolyZamowienia(idZamowienia, dataGridViewSzczegoly);
            }
        }

        private void WyswietlSzczegolyZamowienia(int zamowienieId, DataGridView gridView)
        {
            // Pobierz pozycje (bez cross-server JOIN)
            var dt = new DataTable();
            using (var connection = new SqlConnection(connectionString1))
            using (var command = new SqlCommand(@"
                SELECT zmt.KodTowaru, zmt.Ilosc, zmt.Cena
                FROM [LibraNet].[dbo].[ZamowieniaMiesoTowar] zmt
                WHERE zmt.ZamowienieId = @ZamowienieId", connection))
            {
                command.Parameters.AddWithValue("@ZamowienieId", zamowienieId);
                using var adapter = new SqlDataAdapter(command);
                adapter.Fill(dt);
            }

            // Dodaj kolumnę NazwaTowaru z cache ID->Kod
            if (!dt.Columns.Contains("NazwaTowaru"))
                dt.Columns.Add("NazwaTowaru", typeof(string));

            foreach (DataRow r in dt.Rows)
            {
                int id = r["KodTowaru"] == DBNull.Value ? 0 : Convert.ToInt32(r["KodTowaru"]);
                r["NazwaTowaru"] = _twKodCache.TryGetValue(id, out var kod) ? kod : id.ToString();
            }

            // Ułóż kolumny: Nazwa, Ilość, Cena
            var outTable = new DataTable();
            outTable.Columns.Add("NazwaTowaru", typeof(string));
            outTable.Columns.Add("Ilosc", typeof(decimal));
            outTable.Columns.Add("Cena", typeof(decimal));

            foreach (DataRow r in dt.Rows)
            {
                var nr = outTable.NewRow();
                nr["NazwaTowaru"] = r["NazwaTowaru"];
                nr["Ilosc"] = r["Ilosc"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Ilosc"]);
                nr["Cena"] = r["Cena"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Cena"]);
                outTable.Rows.Add(nr);
            }

            gridView.DataSource = outTable;
            ConfigureSzczegolyGridView(gridView);
        }

        private static void ConfigureSzczegolyGridView(DataGridView gridView)
        {
            gridView.RowHeadersVisible = false;
            gridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridView.AllowUserToResizeRows = false;
            gridView.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            if (gridView.Columns.Contains("Ilosc"))
                gridView.Columns["Ilosc"].DefaultCellStyle.Format = "N0";
            if (gridView.Columns.Contains("Cena"))
                gridView.Columns["Cena"].DefaultCellStyle.Format = "N2";
            if (gridView.Columns.Contains("NazwaTowaru"))
                gridView.Columns["NazwaTowaru"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        }
    }
}
