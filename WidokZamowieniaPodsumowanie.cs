using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Kalendarz1.CenoweMetody;
using System.Windows.Controls;

namespace Kalendarz1
{
    public partial class WidokZamowieniaPodsumowanie : Form
    {
        private string connectionString1 = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string connectionString2 = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private RozwijanieComboBox RozwijanieComboBox = new RozwijanieComboBox();
        private NazwaZiD nazwaZiD = new NazwaZiD();
        private DataService dataService = new DataService();
        private DateTime startOfWeek;
        private DateTime poniedzialek;
        private DateTime wtorek;
        private DateTime sroda;
        private DateTime czwartek;
        private DateTime piatek;
        private decimal totalIloscZamowiona; // Suma dla wszystkich dni tygodnia
        private Dictionary<string, decimal> dzienneSumaIloscZamowiona = new Dictionary<string, decimal>();
        public string UserID { get; set; }
        private int? aktualneIdZamowienia; // Zmienna klasy do przechowywania Id Zamówieniavar idZamowieniaValue = null;

        public WidokZamowieniaPodsumowanie()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Maximized;
            ZaladujTowary();
            nazwaZiD.PokazPojTuszki(dataGridSumaPartie);

            // Podłączanie wspólnej metody obsługi do wszystkich DataGridView
            dataGridViewPoniedzialek.CellClick += UniwersalnyCellClick;
            dataGridViewWtorek.CellClick += UniwersalnyCellClick;
            dataGridViewSroda.CellClick += UniwersalnyCellClick;
            dataGridViewCzwartek.CellClick += UniwersalnyCellClick;
            dataGridViewPiatek.CellClick += UniwersalnyCellClick;

        }

        private void comboBoxTowar_SelectedIndexChanged(object sender, EventArgs e)
        {
            OdswiezPodsumowanie();
        }

        private DataTable CreateDataTable(string[] columnNames)
        {
            DataTable table = new DataTable();
            foreach (var columnName in columnNames)
            {
                table.Columns.Add(columnName);
            }
            return table;
        }

        private void WyswietlPodsumowanie(int towarId, DateTime startOfWeek, DateTime endOfWeek)
        {
            totalIloscZamowiona = 0;
            dzienneSumaIloscZamowiona.Clear();

            using (SqlConnection connection = new SqlConnection(connectionString1)) // zamówienia
            {
                string query = @"
        SELECT 
            zm.Id,
            zm.DataZamowienia, 
            zm.KlientId, 
            tw.Kod AS KodTowaru, 
            SUM(zmt.Ilosc) AS IloscZamowiona
        FROM 
            [LibraNet].[dbo].[ZamowieniaMieso] zm
        JOIN 
            [LibraNet].[dbo].[ZamowieniaMiesoTowar] zmt ON zm.Id = zmt.ZamowienieId
        JOIN 
            [RemoteServer].[HANDEL].[HM].[TW] tw ON zmt.KodTowaru = tw.ID
        WHERE 
            zm.DataZamowienia BETWEEN @StartOfWeek AND @EndOfWeek
            AND tw.ID = @TowarId
        GROUP BY 
            zm.Id, zm.DataZamowienia, zm.KlientId, tw.Kod
        ORDER BY 
            zm.DataZamowienia;";

                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@StartOfWeek", startOfWeek);
                command.Parameters.AddWithValue("@EndOfWeek", endOfWeek);
                command.Parameters.AddWithValue("@TowarId", towarId);

                SqlDataAdapter adapter = new SqlDataAdapter(command);
                DataTable dataTable = new DataTable();
                adapter.Fill(dataTable);

                // Tworzenie DataTable dla każdego dnia
                DataTable dtPon = CreateDataTable(new[] { "IdZamowienia", "Klient", "Ilosc", "RIlosc" });
                DataTable dtWto = CreateDataTable(new[] { "IdZamowienia", "Klient", "Ilosc", "RIlosc" });
                DataTable dtSro = CreateDataTable(new[] { "IdZamowienia", "Klient", "Ilosc", "RIlosc" });
                DataTable dtCzw = CreateDataTable(new[] { "IdZamowienia", "Klient", "Ilosc", "RIlosc" });
                DataTable dtPia = CreateDataTable(new[] { "IdZamowienia", "Klient", "Ilosc", "RIlosc" });

                foreach (DataRow row in dataTable.Rows)
                {
                    DateTime dataZamowienia = Convert.ToDateTime(row["DataZamowienia"]);
                    string klientId = row["KlientId"].ToString();
                    string idZamowienia = row["Id"].ToString();
                    decimal iloscZamowiona = Convert.ToDecimal(row["IloscZamowiona"]);
                    totalIloscZamowiona += iloscZamowiona;

                    var daneOdbiorcy = dataService.PobierzDaneOdbiorcy(klientId);
                    string nazwaOdbiorcy = daneOdbiorcy[RozwijanieComboBox.DaneKontrahenta.Kod];

                    decimal faktycznaIlosc = 0;
                    using (SqlConnection connReal = new SqlConnection(connectionString2))
                    {
                        string realQuery = @"
                SELECT SUM(ABS(MZ.ilosc)) AS RIlosc
                FROM [HANDEL].[HM].[MZ] MZ
                JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id
                WHERE MG.data = @Data
                  AND MG.aktywny = 1
                  AND MG.khid = @KlientId
                  AND MG.seria IN ('sWZ', 'sWZ-W')
                  AND MZ.idtw = @TowarId";

                        SqlCommand realCmd = new SqlCommand(realQuery, connReal);
                        realCmd.Parameters.AddWithValue("@Data", dataZamowienia);
                        realCmd.Parameters.AddWithValue("@KlientId", klientId);
                        realCmd.Parameters.AddWithValue("@TowarId", towarId);

                        connReal.Open();
                        var realResult = realCmd.ExecuteScalar();
                        if (realResult != null && realResult != DBNull.Value)
                            faktycznaIlosc = Convert.ToDecimal(realResult);
                    }

                    int dayOffset = (dataZamowienia - startOfWeek).Days;
                    switch (dayOffset)
                    {
                        case 0:
                            dtPon.Rows.Add(idZamowienia, nazwaOdbiorcy, iloscZamowiona, faktycznaIlosc);
                            AddToDailySum("Poniedziałek", iloscZamowiona);
                            break;
                        case 1:
                            dtWto.Rows.Add(idZamowienia, nazwaOdbiorcy, iloscZamowiona, faktycznaIlosc);
                            AddToDailySum("Wtorek", iloscZamowiona);
                            break;
                        case 2:
                            dtSro.Rows.Add(idZamowienia, nazwaOdbiorcy, iloscZamowiona, faktycznaIlosc);
                            AddToDailySum("Środa", iloscZamowiona);
                            break;
                        case 3:
                            dtCzw.Rows.Add(idZamowienia, nazwaOdbiorcy, iloscZamowiona, faktycznaIlosc);
                            AddToDailySum("Czwartek", iloscZamowiona);
                            break;
                        case 4:
                            dtPia.Rows.Add(idZamowienia, nazwaOdbiorcy, iloscZamowiona, faktycznaIlosc);
                            AddToDailySum("Piątek", iloscZamowiona);
                            break;
                    }
                }

                // 🔁 DODATKOWO: dodaj klientów z RIlosc > 0, ale bez zamówienia
                using (SqlConnection connExtra = new SqlConnection(connectionString2))
                {
                    string extraQuery = @"
                SELECT 
                    MG.data,
                    MG.khid,
                    SUM(ABS(MZ.ilosc)) AS RIlosc
                FROM [HANDEL].[HM].[MZ] MZ
                JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id
                WHERE 
                    MG.data BETWEEN @StartOfWeek AND @EndOfWeek
                    AND MG.seria IN ('sWZ', 'sWZ-W')
                    AND MG.aktywny = 1
                    AND MZ.idtw = @TowarId
                GROUP BY MG.data, MG.khid";

                    SqlCommand extraCmd = new SqlCommand(extraQuery, connExtra);
                    extraCmd.Parameters.AddWithValue("@StartOfWeek", startOfWeek);
                    extraCmd.Parameters.AddWithValue("@EndOfWeek", endOfWeek);
                    extraCmd.Parameters.AddWithValue("@TowarId", towarId);

                    SqlDataAdapter extraAdapter = new SqlDataAdapter(extraCmd);
                    DataTable odbiorcyBezZamowienia = new DataTable();
                    extraAdapter.Fill(odbiorcyBezZamowienia);

                    foreach (DataRow odb in odbiorcyBezZamowienia.Rows)
                    {
                        DateTime data = Convert.ToDateTime(odb["data"]);
                        string khid = odb["khid"].ToString();
                        decimal rilosc = Convert.ToDecimal(odb["RIlosc"]);

                        bool alreadyExists = dataTable.AsEnumerable().Any(r =>
                            Convert.ToDateTime(r["DataZamowienia"]).Date == data.Date &&
                            r["KlientId"].ToString() == khid);

                        if (!alreadyExists)
                        {
                            var odbiorca = dataService.PobierzDaneOdbiorcy(khid);
                            if (!odbiorca.ContainsKey(RozwijanieComboBox.DaneKontrahenta.Kod))
                            {
                                
                                continue;
                            }
                            string nazwa = odbiorca[RozwijanieComboBox.DaneKontrahenta.Kod];

                            int dzienOffset = (data - startOfWeek).Days;

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
                }

                // Wyświetl w siatkach
                ConfigureDataGridView(dataGridViewPoniedzialek, dtPon);
                ConfigureDataGridView(dataGridViewWtorek, dtWto);
                ConfigureDataGridView(dataGridViewSroda, dtSro);
                ConfigureDataGridView(dataGridViewCzwartek, dtCzw);
                ConfigureDataGridView(dataGridViewPiatek, dtPia);
            }

            Console.WriteLine($"Suma całkowita: {totalIloscZamowiona}");
            foreach (var dzien in dzienneSumaIloscZamowiona)
                Console.WriteLine($"{dzien.Key}: {dzien.Value}");
        }


        private void AddToDailySum(string day, decimal amount)
        {
            if (!dzienneSumaIloscZamowiona.ContainsKey(day))
            {
                dzienneSumaIloscZamowiona[day] = 0;
            }
            dzienneSumaIloscZamowiona[day] += amount;
        }


        private void ConfigureDataGridView(DataGridView gridView, DataTable dataSource)
        {
            gridView.DataSource = dataSource;

            // Ukrycie kolumny "IdZamowienia"
            if (gridView.Columns.Contains("IdZamowienia"))
            {
                gridView.Columns["IdZamowienia"].Visible = false;
            }

            // Szerokość kolumny "Klient"
            if (gridView.Columns.Contains("Klient"))
            {
                gridView.Columns["Klient"].Width = 90;
            }

            // Wyłączanie wiersza nagłówka po lewej stronie
            gridView.RowHeadersVisible = false;

            // Ustawienie wysokości wierszy
            gridView.RowTemplate.Height = 22;

            // Dopasowanie szerokości kolumn do dostępnej przestrzeni
            gridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // Wyłączenie możliwości zmiany rozmiaru wierszy przez użytkownika
            gridView.AllowUserToResizeRows = false;

            // Wyrównanie tekstu w kolumnach
            gridView.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            // Formatowanie wartości i kolorowanie
            gridView.CellFormatting += (sender, e) =>
            {
                if (e.Value != null)
                {
                    string columnName = gridView.Columns[e.ColumnIndex].Name;

                    if (columnName == "Ilosc" || columnName == "RIlosc")
                    {
                        if (decimal.TryParse(e.Value.ToString(), out decimal value))
                        {
                            e.Value = $"{value:N0} kg";
                            e.FormattingApplied = true;
                        }
                    }

                    // Kolorowanie tylko dla RIlosc
                    if (columnName == "RIlosc")
                    {
                        var row = gridView.Rows[e.RowIndex];
                        if (gridView.Columns.Contains("Ilosc") && row.Cells["Ilosc"].Value != null && row.Cells["RIlosc"].Value != null)
                        {
                            if (decimal.TryParse(row.Cells["Ilosc"].Value.ToString(), out decimal ilosc) &&
                                decimal.TryParse(row.Cells["RIlosc"].Value.ToString(), out decimal rilosc))
                            {
                                if (rilosc >= ilosc)
                                {
                                    row.Cells["RIlosc"].Style.ForeColor = Color.Green;
                                }
                                else if (rilosc > 0 && rilosc < ilosc)
                                {
                                    row.Cells["RIlosc"].Style.ForeColor = Color.Red;
                                    row.Cells["RIlosc"].Style.Font = new Font(gridView.Font, FontStyle.Bold);
                                }
                                else
                                {
                                    row.Cells["RIlosc"].Style.ForeColor = Color.Black;
                                }
                            }
                        }
                    }
                }
            };



            // Koloryzowanie wierszy naprzemiennie
            gridView.AlternatingRowsDefaultCellStyle.BackColor = Color.LightGray; // Kolor dla naprzemiennych wierszy
            gridView.DefaultCellStyle.BackColor = Color.White; // Kolor dla pozostałych wierszy

            // Ustawienie domyślnego koloru tekstu
            gridView.DefaultCellStyle.ForeColor = Color.Black;

            // Wyczyść zaznaczenie domyślne
            gridView.ClearSelection();
        }


        private void ZaladujTowary()
        {
            using (SqlConnection connection = new SqlConnection(connectionString2))
            {
                string query = "SELECT [ID], [kod] FROM [HANDEL].[HM].[TW] WHERE katalog = '67095' ORDER BY Kod ASC";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable towary = new DataTable();
                adapter.Fill(towary);

                if (towary.Rows.Count == 0)
                {
                    MessageBox.Show("Brak danych w tabeli TW.", "Błąd");
                    return;
                }

                comboBoxTowar.DisplayMember = "kod"; // Wyświetlany tekst
                comboBoxTowar.ValueMember = "ID";    // Ukryte ID
                comboBoxTowar.DataSource = towary;   // Źródło danych na końcu
            }

        }


        private void PokazPrzewidywalneKilogramy(DataGridView datagrid, DateTime dzien, string zamowienie)
        {
            DataTable finalTable = new DataTable();
            finalTable.Columns.Add("Kategoria", typeof(string));
            finalTable.Columns.Add("Przewidywalny", typeof(string));
            finalTable.Columns.Add("Faktyczny", typeof(string));

            double sumTonazTuszkiA = 0;
            double sumTonazTuszkiB = 0;

            // Przewidywany przychód z Harmonogramu
            using (SqlConnection connection = new SqlConnection(connectionString1))
            {
                string query = @"
            SELECT LP, Auta, Dostawca, WagaDek, SztukiDek 
            FROM dbo.HarmonogramDostaw 
            WHERE DataOdbioru = @StartDate 
              AND Bufor = 'Potwierdzony' 
            ORDER BY WagaDek DESC";

                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@StartDate", dzien);

                SqlDataAdapter adapter = new SqlDataAdapter(command);
                DataTable table = new DataTable();
                adapter.Fill(table);

                foreach (DataRow row in table.Rows)
                {
                    double wagaDek = row["WagaDek"] != DBNull.Value ? Convert.ToDouble(row["WagaDek"]) : 0.0;
                    int sztukiDek = row["SztukiDek"] != DBNull.Value ? Convert.ToInt32(row["SztukiDek"]) : 0;

                    double sredniaTuszka = wagaDek * 0.78;
                    double tonazTuszka = sredniaTuszka * sztukiDek;
                    double tonazA = tonazTuszka * 0.85;
                    double tonazB = tonazTuszka * 0.15;

                    sumTonazTuszkiA += tonazA;
                    sumTonazTuszkiB += tonazB;
                }
            }

            // Wylicz przychód przewidywalny
            double wynikPrzychodu = 0;
            int selectedTowarId = Convert.ToInt32(comboBoxTowar.SelectedValue);

            if (selectedTowarId == 66443)
                wynikPrzychodu = sumTonazTuszkiA;
            else
                wynikPrzychodu = dataService.WydajnoscElement(sumTonazTuszkiB, selectedTowarId);

            // Zamówienie przewidywane
            double przewidywalny = dzienneSumaIloscZamowiona.ContainsKey(zamowienie)
                ? (double)dzienneSumaIloscZamowiona[zamowienie]
                : 0.0;

            // Faktyczny przychód z dokumentów sPWU
            double faktycznyPrzychod = 0.0;

            using (SqlConnection conn = new SqlConnection(connectionString2))
            {
                string sql = @"
            SELECT SUM(ABS(MZ.ilosc)) AS SumaIlosc
            FROM [HANDEL].[HM].[MZ] MZ
            JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id
            WHERE 
                MG.seria = 'sPWU'
                AND MG.aktywny = 1
                AND MG.data = @dzien
                AND MZ.idtw = @idtw";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@dzien", dzien);
                cmd.Parameters.AddWithValue("@idtw", selectedTowarId);

                conn.Open();
                object result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    faktycznyPrzychod = Convert.ToDouble(result);
                }
            }

            // Faktyczne zamówienie z dokumentów sWZ + sWZ-W
            double faktyczneZamowienie = 0.0;

            using (SqlConnection conn = new SqlConnection(connectionString2))
            {
                string sql = @"
            SELECT SUM(ABS(MZ.ilosc)) AS SumaIlosc
            FROM [HANDEL].[HM].[MZ] MZ
            JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id
            WHERE 
                MG.seria IN ('sWZ', 'sWZ-W')
                AND MG.aktywny = 1
                AND MG.data = @dzien
                AND MZ.idtw = @idtw";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@dzien", dzien);
                cmd.Parameters.AddWithValue("@idtw", selectedTowarId);

                conn.Open();
                object result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    faktyczneZamowienie = Convert.ToDouble(result);
                }
            }

            // Wiersz 1: Przychód
            DataRow przychod = finalTable.NewRow();
            przychod["Kategoria"] = "Przychód";
            przychod["Przewidywalny"] = $"{wynikPrzychodu:N0} kg";
            przychod["Faktyczny"] = $"{faktycznyPrzychod:N0} kg";
            finalTable.Rows.Add(przychod);

            // Wiersz 2: Zamówione
            DataRow zamowienia = finalTable.NewRow();
            zamowienia["Kategoria"] = "Zamówione";
            zamowienia["Przewidywalny"] = $"{przewidywalny:N0} kg";
            zamowienia["Faktyczny"] = $"{faktyczneZamowienie:N0} kg";
            finalTable.Rows.Add(zamowienia);

            // Wiersz 3: Pozostało
            DataRow pozostalo = finalTable.NewRow();
            pozostalo["Kategoria"] = "Pozostało";
            double pozostaloKg = wynikPrzychodu - przewidywalny;
            pozostalo["Przewidywalny"] = $"{pozostaloKg:N0} kg";
            double faktycznieKg = faktycznyPrzychod - faktyczneZamowienie;
            pozostalo["Faktyczny"] = $"{faktycznieKg:N0} kg";
            finalTable.Rows.Add(pozostalo);

            // Wyświetlenie
            datagrid.DataSource = finalTable;
            datagrid.Columns["Kategoria"].HeaderText = "Kategoria";
            datagrid.Columns["Przewidywalny"].HeaderText = "Przewidywalny";
            datagrid.Columns["Faktyczny"].HeaderText = "Faktyczny";

            // Formatowanie wiersza końcowego (opcjonalnie)
            FormatSumRow(datagrid, 2);
        }




        private void FormatSumRow(DataGridView gridView, int nrWiersz)
        {
            DataGridViewRow sumRow = gridView.Rows[nrWiersz];
            sumRow.DefaultCellStyle.ForeColor = Color.Black;
            sumRow.DefaultCellStyle.Font = new Font(gridView.Font, FontStyle.Bold);
            // Wyłączanie wiersza nagłówka po lewej stronie
            gridView.RowHeadersVisible = false;
            gridView.ClearSelection();
        }


        private void CommandButton_Update_Click(object sender, EventArgs e)
        {
            WidokZamowienia widokZamowienia = new WidokZamowienia();
            widokZamowienia.UserID = App.UserID;
            widokZamowienia.Show();
        }


        private void OdswiezPodsumowanie()
        {
            try
            {
                if (comboBoxTowar.SelectedValue == null || comboBoxTowar.SelectedValue is DataRowView)
                {
                    MessageBox.Show("SelectedValue jest niewłaściwego typu.", "Błąd");
                    return;
                }

                int selectedTowarId = Convert.ToInt32(comboBoxTowar.SelectedValue);
                Console.WriteLine($"Selected Towar ID: {selectedTowarId}");

                DateTime selectedDate = myCalendar.SelectionStart;
                Console.WriteLine($"Selected Date: {selectedDate}");

                // Oblicz daty tygodnia
                startOfWeek = selectedDate.AddDays(-(int)selectedDate.DayOfWeek + 1); // Poniedziałek
                poniedzialek = startOfWeek;
                wtorek = startOfWeek.AddDays(1);
                sroda = startOfWeek.AddDays(2);
                czwartek = startOfWeek.AddDays(3);
                piatek = startOfWeek.AddDays(4);

                // Wyświetl podsumowanie dla wybranego tygodnia
                WyswietlPodsumowanie(selectedTowarId, startOfWeek, piatek);

                // Wyświetl przewidywalne kilogramy dla każdego dnia tygodnia
                PokazPrzewidywalneKilogramy(dataGridViewPoniedzialekSuma, poniedzialek, "Poniedziałek");
                PokazPrzewidywalneKilogramy(dataGridViewWtorekSuma, wtorek, "Wtorek");
                PokazPrzewidywalneKilogramy(dataGridViewSrodaSuma, sroda, "Środa");
                PokazPrzewidywalneKilogramy(dataGridViewCzwartekSuma, czwartek, "Czwartek");
                PokazPrzewidywalneKilogramy(dataGridViewPiatekSuma, piatek, "Piątek");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd");
            }
        }

        private void UniwersalnyCellClick(object sender, DataGridViewCellEventArgs e)
        {
            // Sprawdź, czy kliknięto na wiersz (pomijamy nagłówki kolumn)
            if (e.RowIndex >= 0)
            {
                // Rzutuj sender na DataGridView
                DataGridView gridView = sender as DataGridView;

                // Sprawdź, czy kolumna "IdZamowienia" istnieje w danym DataGridView
                if (gridView.Columns.Contains("IdZamowienia"))
                {
                    // Pobierz wartość z ukrytej kolumny "IdZamowienia"
                    var idZamowieniaValue = gridView.Rows[e.RowIndex].Cells["IdZamowienia"].Value;

                    if (idZamowieniaValue != null && int.TryParse(idZamowieniaValue.ToString(), out int idZamowienia))
                    {
                        // Zapisz Id Zamówienia w zmiennej klasy
                        aktualneIdZamowienia = idZamowienia;

                        // Wyświetlenie szczegółów zamówienia w innym DataGridView
                        WyswietlSzczegolyZamowienia(idZamowienia, dataGridViewSzczegoly);

                        // Opcjonalnie: Pokaż komunikat, że wartość została zapisana
                       
                    }
                    else
                    {
                       
                    }
                }
                else
                {
                    MessageBox.Show("Kolumna 'IdZamowienia' nie istnieje w tym DataGridView.");
                }
            }
        }


        private void WyswietlSzczegolyZamowienia(int zamowienieId, DataGridView gridView)
        {
            using (SqlConnection connection = new SqlConnection(connectionString1)) // Połączenie do bazy danych
            {
                string query = @"
        SELECT 
            tw.Kod AS NazwaTowaru, -- Wyświetlenie nazwy towaru
            zmt.Ilosc, 
            zmt.Cena
        FROM 
            [LibraNet].[dbo].[ZamowieniaMiesoTowar] zmt
        JOIN 
            [RemoteServer].[HANDEL].[HM].[TW] tw -- Dołączenie tabeli towarów z zdalnego serwera
        ON 
            zmt.KodTowaru = tw.ID
        WHERE 
            zmt.ZamowienieId = @ZamowienieId";

                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ZamowienieId", zamowienieId);

                SqlDataAdapter adapter = new SqlDataAdapter(command);
                DataTable dataTable = new DataTable();
                adapter.Fill(dataTable);

                // Wypełnienie DataGridView
                gridView.DataSource = dataTable;

                // Formatowanie DataGridView
                ConfigureSzczegolyGridView(gridView);
            }
        }
        private void ConfigureSzczegolyGridView(DataGridView gridView)
        {
            gridView.RowHeadersVisible = false;
            gridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridView.AllowUserToResizeRows = false;
            gridView.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            // Formatowanie kolumn
            if (gridView.Columns.Contains("Ilosc"))
            {
                gridView.Columns["Ilosc"].DefaultCellStyle.Format = "N0"; // Separator tysięcy
            }
            if (gridView.Columns.Contains("Cena"))
            {
                gridView.Columns["Cena"].DefaultCellStyle.Format = "C2"; // Format waluty
            }
        }





        // Wywołanie z przycisku
        private void buttonOdswiez_Click(object sender, EventArgs e)
        {
            OdswiezPodsumowanie();
            nazwaZiD.PokazPojTuszki(dataGridSumaPartie);
        }

        private void myCalendar_DateChanged(object sender, DateRangeEventArgs e)
        {
            OdswiezPodsumowanie();
        }

        private void buttonModyfikuj_Click(object sender, EventArgs e)
        {

                WidokZamowienia widokZamowienia = new WidokZamowienia(aktualneIdZamowienia);
                widokZamowienia.UserID = App.UserID;
                widokZamowienia.ShowDialog(); // Otwórz jako dialog

        }
    }
}
