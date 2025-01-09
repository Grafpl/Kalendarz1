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
            totalIloscZamowiona = 0; // Reset sumy na początku
            dzienneSumaIloscZamowiona.Clear(); // Reset słownika dla dni tygodnia

            using (SqlConnection connection = new SqlConnection(connectionString1)) // Połączenie do serwera .109
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
                    [LibraNet].[dbo].[ZamowieniaMiesoTowar] zmt
                ON 
                    zm.Id = zmt.ZamowienieId
                JOIN 
                    [RemoteServer].[HANDEL].[HM].[TW] tw
                ON 
                    zmt.KodTowaru = tw.ID
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

                DataTable dtPoniedzialek = CreateDataTable(new string[] { "IdZamowienia", "Klient", "Ilosc" });
                DataTable dtWtorek = CreateDataTable(new string[] { "IdZamowienia", "Klient", "Ilosc" });
                DataTable dtSroda = CreateDataTable(new string[] { "IdZamowienia", "Klient", "Ilosc" });
                DataTable dtCzwartek = CreateDataTable(new string[] { "IdZamowienia", "Klient", "Ilosc" });
                DataTable dtPiatek = CreateDataTable(new string[] { "IdZamowienia", "Klient", "Ilosc" });


                foreach (DataRow row in dataTable.Rows)
                {

                    DateTime dataZamowienia = Convert.ToDateTime(row["DataZamowienia"]);
                    string klientId = row["KlientId"].ToString();
                    string idZamowienia = row["Id"].ToString();
                    var daneOdbiorcy = dataService.PobierzDaneOdbiorcy(klientId);
                    string nazwaOdbiorcy = daneOdbiorcy[RozwijanieComboBox.DaneKontrahenta.Kod];
                    decimal iloscZamowiona = Convert.ToDecimal(row["IloscZamowiona"]);

                    totalIloscZamowiona += iloscZamowiona; // Aktualizacja sumy całkowitej

                    if (dataZamowienia.Date == startOfWeek.Date)
                    {
                        dtPoniedzialek.Rows.Add(idZamowienia, nazwaOdbiorcy, iloscZamowiona);
                        AddToDailySum("Poniedziałek", iloscZamowiona);
                    }
                    else if (dataZamowienia.Date == startOfWeek.AddDays(1).Date)
                    {
                        dtWtorek.Rows.Add(idZamowienia, nazwaOdbiorcy, iloscZamowiona);
                        AddToDailySum("Wtorek", iloscZamowiona);
                    }
                    else if (dataZamowienia.Date == startOfWeek.AddDays(2).Date)
                    {
                        dtSroda.Rows.Add(idZamowienia, nazwaOdbiorcy, iloscZamowiona);
                        AddToDailySum("Środa", iloscZamowiona);
                    }
                    else if (dataZamowienia.Date == startOfWeek.AddDays(3).Date)
                    {
                        dtCzwartek.Rows.Add(idZamowienia, nazwaOdbiorcy, iloscZamowiona);
                        AddToDailySum("Czwartek", iloscZamowiona);
                    }
                    else if (dataZamowienia.Date == startOfWeek.AddDays(4).Date)
                    {
                        dtPiatek.Rows.Add(idZamowienia, nazwaOdbiorcy, iloscZamowiona);
                        AddToDailySum("Piątek", iloscZamowiona);
                    }
                }

                ConfigureDataGridView(dataGridViewPoniedzialek, dtPoniedzialek);
                ConfigureDataGridView(dataGridViewWtorek, dtWtorek);
                ConfigureDataGridView(dataGridViewSroda, dtSroda);
                ConfigureDataGridView(dataGridViewCzwartek, dtCzwartek);
                ConfigureDataGridView(dataGridViewPiatek, dtPiatek);
            }

            // Wyświetlenie sumy w kontrolkach lub logu
            Console.WriteLine($"Suma całkowita: {totalIloscZamowiona}");
            foreach (var dzien in dzienneSumaIloscZamowiona)
            {
                Console.WriteLine($"{dzien.Key}: {dzien.Value}");
            }
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
                gridView.Columns["Klient"].Width = 200;
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

            // Formatowanie kolumny "Ilosc" z separatorem tysięcy i jednostką "kg"
            gridView.CellFormatting += (sender, e) =>
            {
                if (gridView.Columns[e.ColumnIndex].HeaderText == "Ilosc" && e.Value != null)
                {
                    if (decimal.TryParse(e.Value.ToString(), out decimal value))
                    {
                        e.Value = $"{value:N0} kg";
                        e.FormattingApplied = true;
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

            // Dodanie odpowiednich kolumn do tabeli
            finalTable.Columns.Add("Kategoria", typeof(string)); // Kolumna dla kategorii
            finalTable.Columns.Add("Przewidywalny", typeof(string)); // Kolumna dla sum
            finalTable.Columns.Add("Faktyczny", typeof(string)); // Kolumna dla procentów

            // Tworzenie połączenia z bazą danych i pobieranie danych
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

                double sumTonazTuszkiA = 0;
                double sumTonazTuszkiB = 0;

                foreach (DataRow row in table.Rows)
                {
                    double wagaDekValue = row["WagaDek"] != DBNull.Value ? Convert.ToDouble(row["WagaDek"]) : 0.0;
                    int sztukiDekValue = row["SztukiDek"] != DBNull.Value ? Convert.ToInt32(row["SztukiDek"]) : 0;

                    double sredniaTuszkaValue = wagaDekValue * 0.78;
                    double tonazTuszkaValue = sredniaTuszkaValue * sztukiDekValue;
                    double tonazTuszkaAValue = tonazTuszkaValue * 0.85;
                    double tonazTuszkaBValue = tonazTuszkaValue * 0.15;

                    sumTonazTuszkiA += tonazTuszkaAValue;
                    sumTonazTuszkiB += tonazTuszkaBValue;
                }

                double wynikPrzychodu = 0;
                int selectedTowarId = Convert.ToInt32(comboBoxTowar.SelectedValue);
                //kurczak a
                if (selectedTowarId == 66443)
                {
                    wynikPrzychodu = sumTonazTuszkiA;
                }
                else
                {
                    wynikPrzychodu = dataService.WydajnoscElement(sumTonazTuszkiB, selectedTowarId);
                }


                double przewidywalny = dzienneSumaIloscZamowiona.ContainsKey(zamowienie)
                    ? (double)dzienneSumaIloscZamowiona[zamowienie]
                    : 0.0;



                // Dodanie wierszy do tabeli
                DataRow przychod = finalTable.NewRow();
                przychod["Kategoria"] = "Przychód";
                przychod["Przewidywalny"] = $"{wynikPrzychodu:N0} kg";
                przychod["Faktyczny"] = "";
                finalTable.Rows.Add(przychod);

                DataRow zamowienia = finalTable.NewRow();
                zamowienia["Kategoria"] = "Zamówione";
                zamowienia["Przewidywalny"] = $"{przewidywalny:N0} kg";
                zamowienia["Faktyczny"] = $"{przewidywalny:N0} kg";
                finalTable.Rows.Add(zamowienia);

                DataRow pozostalo = finalTable.NewRow();
                pozostalo["Kategoria"] = "Pozostalo";
                double pozostaloKg = wynikPrzychodu - przewidywalny;
                pozostalo["Przewidywalny"] = $"{pozostaloKg:N0} ";
                pozostalo["Faktyczny"] = "";
                finalTable.Rows.Add(pozostalo);


            }

            // Ustawienie źródła danych dla DataGridView
            datagrid.DataSource = finalTable;
            datagrid.Columns["Kategoria"].HeaderText = "Kategoria";
            datagrid.Columns["Przewidywalny"].HeaderText = "Przewidywalny";
            datagrid.Columns["Faktyczny"].HeaderText = "Faktyczny";
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
