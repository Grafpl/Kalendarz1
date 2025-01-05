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

namespace Kalendarz1
{
    public partial class WidokZamowieniaPodsumowanie : Form
    {
        private string connectionString1 = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string connectionString2 = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private RozwijanieComboBox RozwijanieComboBox = new RozwijanieComboBox();
        private DataService dataService = new DataService();

        public WidokZamowieniaPodsumowanie()
        {
            InitializeComponent();
            ZaladujTowary();
        }

        private void comboBoxTowar_SelectedIndexChanged(object sender, EventArgs e)
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

                DateTime startOfWeek = selectedDate.AddDays(-(int)selectedDate.DayOfWeek + 1);
                DateTime endOfWeek = startOfWeek.AddDays(4);

                MessageBox.Show($"Selected Towar ID: {selectedTowarId}, startOfWeek {startOfWeek},endOfWeek {endOfWeek}");

                WyswietlPodsumowanie(selectedTowarId, startOfWeek, endOfWeek);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd");
            }
        }







        private void WyswietlPodsumowanie(int towarId, DateTime startOfWeek, DateTime endOfWeek)
        {
            using (SqlConnection connection = new SqlConnection(connectionString1)) // Połączenie do serwera .109
            {
                string query = @"
        SELECT 
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
            zm.DataZamowienia, zm.KlientId, tw.Kod
        ORDER BY 
            zm.DataZamowienia;";

                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@StartOfWeek", startOfWeek);
                command.Parameters.AddWithValue("@EndOfWeek", endOfWeek);
                command.Parameters.AddWithValue("@TowarId", towarId);

                SqlDataAdapter adapter = new SqlDataAdapter(command);
                DataTable dataTable = new DataTable();
                adapter.Fill(dataTable);

                // Przygotowanie DataTable dla każdego dnia tygodnia
                DataTable dtPoniedzialek = new DataTable();
                dtPoniedzialek.Columns.Add("Klient");
                dtPoniedzialek.Columns.Add("Ilosc");

                DataTable dtWtorek = new DataTable();
                dtWtorek.Columns.Add("Klient");
                dtWtorek.Columns.Add("Ilosc");

                DataTable dtSroda = new DataTable();
                dtSroda.Columns.Add("Klient");
                dtSroda.Columns.Add("Ilosc");

                DataTable dtCzwartek = new DataTable();
                dtCzwartek.Columns.Add("Klient");
                dtCzwartek.Columns.Add("Ilosc");

                DataTable dtPiatek = new DataTable();
                dtPiatek.Columns.Add("Klient");
                dtPiatek.Columns.Add("Ilosc");

                // Przypisanie danych do odpowiednich tabel
                foreach (DataRow row in dataTable.Rows)
                {
                    DateTime dataZamowienia = Convert.ToDateTime(row["DataZamowienia"]);
                    string klientId = row["KlientId"].ToString();
                    var daneOdbiorcy = dataService.PobierzDaneOdbiorcy(klientId);
                    string nazwaOdbiorcy = daneOdbiorcy[RozwijanieComboBox.DaneKontrahenta.Kod];
                    string iloscZamowiona = row["IloscZamowiona"].ToString();

                    if (dataZamowienia.Date == startOfWeek.Date)
                        dtPoniedzialek.Rows.Add(nazwaOdbiorcy, iloscZamowiona);
                    else if (dataZamowienia.Date == startOfWeek.AddDays(1).Date)
                        dtWtorek.Rows.Add(nazwaOdbiorcy, iloscZamowiona);
                    else if (dataZamowienia.Date == startOfWeek.AddDays(2).Date)
                        dtSroda.Rows.Add(nazwaOdbiorcy, iloscZamowiona);
                    else if (dataZamowienia.Date == startOfWeek.AddDays(3).Date)
                        dtCzwartek.Rows.Add(nazwaOdbiorcy, iloscZamowiona);
                    else if (dataZamowienia.Date == startOfWeek.AddDays(4).Date)
                        dtPiatek.Rows.Add(nazwaOdbiorcy, iloscZamowiona);
                }

                // Przypisanie DataTable do DataGridView
                ConfigureDataGridView(dataGridViewPoniedzialek, dtPoniedzialek);
                ConfigureDataGridView(dataGridViewWtorek, dtWtorek);
                ConfigureDataGridView(dataGridViewSroda, dtSroda);
                ConfigureDataGridView(dataGridViewCzwartek, dtCzwartek);
                ConfigureDataGridView(dataGridViewPiatek, dtPiatek);
            }
        }

        private void ConfigureDataGridView(DataGridView gridView, DataTable dataSource)
        {
            gridView.DataSource = dataSource;
            gridView.Columns["Klient"].Width = 200; // Szerokość kolumny Klient
            gridView.RowHeadersVisible = false;
            gridView.RowTemplate.Height = 18;
            gridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridView.AllowUserToResizeRows = false;
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


        private void PokazPrzewidywalneKilogramy(DataGridView datagrid, )
        {
            // Tworzenie dwóch tabel dla różnych DataGridView
            DataTable finalTable = new DataTable();

            // Tworzenie połączenia z bazą danych i pobieranie danych
            using (SqlConnection connection = new SqlConnection(connectionString1))
            {
                // Tworzenie komendy SQL
                string query = @"
                SELECT LP, Auta, Dostawca, WagaDek, SztukiDek 
                FROM dbo.HarmonogramDostaw 
                WHERE DataOdbioru == @StartDate 
                  AND Bufor = 'Potwierdzony' 
                ORDER BY WagaDek DESC";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@StartDate", dateTimePicker1.Value.Date);

                // Tworzenie adaptera danych i wypełnianie DataTable
                SqlDataAdapter adapter = new SqlDataAdapter(command);
                DataTable table = new DataTable();
                adapter.Fill(table);

                // Dodanie odpowiednich kolumn do obu tabel
                finalTable.Columns.Add("", typeof(string)); // Kolumna dla towarów (Tuszka A, Tuszka B)
                finalTable.Columns.Add("Przewidywalny", typeof(string));  // Kolumna dla sum
                finalTable.Columns.Add("Faktyczny", typeof(string)); // Kolumna dla procentów


                // Inicjalizacja zmiennych sum
                double sumTonazTuszkiA = 0; // Zmienna przeniesiona na poziom klasy
                double sumTonazTuszkiB = 0;
                double sumCwiartka = 0;
                double sumFilet = 0;
                double sumSkrzydlo = 0;
                double sumKorpus = 0;
                double sumPozostale = 0;

                // Iteracja przez wiersze tabeli źródłowej
                foreach (DataRow row in table.Rows)
                {
                    // Check if "WagaDek" is DBNull, if so, assign a default value (e.g., 0.0)
                    double wagaDekValue = row["WagaDek"] != DBNull.Value ? Convert.ToDouble(row["WagaDek"]) : 0.0;

                    // Check if "SztukiDek" is DBNull, if so, assign a default value (e.g., 0)
                    int sztukiDekValue = row["SztukiDek"] != DBNull.Value ? Convert.ToInt32(row["SztukiDek"]) : 0;

                    double sredniaTuszkaValue = wagaDekValue * 0.78;
                    double tonazTuszkaValue = sredniaTuszkaValue * sztukiDekValue;
                    double tonazTuszkaAValue = tonazTuszkaValue * 0.85;
                    double tonazTuszkaBValue = tonazTuszkaValue * 0.15;
                    double tonazCwiartkaValue = tonazTuszkaBValue * 0.37;
                    double tonazSkrzydloValue = tonazTuszkaBValue * 0.09;
                    double tonazFiletValue = tonazTuszkaBValue * 0.295;
                    double tonazKorpusValue = tonazTuszkaBValue * 0.235;
                    double PozostaleValue = tonazTuszkaBValue * 0.01;

                    // Sumowanie wartości
                    sumTonazTuszkiA += tonazTuszkaAValue;
                    sumTonazTuszkiB += tonazTuszkaBValue;
                    sumCwiartka += tonazCwiartkaValue;
                    sumFilet += tonazFiletValue;
                    sumSkrzydlo += tonazSkrzydloValue;
                    sumKorpus += tonazKorpusValue;
                    sumPozostale += PozostaleValue;
                }


                double totalTuszki = sumTonazTuszkiA + sumTonazTuszkiB;
                double totalElementy = sumCwiartka + sumSkrzydlo + sumFilet + sumKorpus;

                // Dodanie wierszy do tabeli finalTableTusz dla Tuszka A i Tuszka B
                DataRow rowTuszkaA = finalTable.NewRow();
                rowTuszkaA[""] = "Przychód";
                rowTuszkaA["Przewidywalny"] = $"{sumTonazTuszkiA:N0} kg";
                rowTuszkaA["Faktyczny"] = $"{(sumTonazTuszkiA / totalTuszki) * 100:N2} %";
                finalTable.Rows.Add(rowTuszkaA);

                DataRow rowTuszkaB = finalTable.NewRow();
                rowTuszkaB[""] = "Zamówione";
                rowTuszkaB["Przewidywalny"] = $"{sumTonazTuszkiB:N0} kg";
                rowTuszkaB["Faktyczny"] = $"{(sumTonazTuszkiB / totalTuszki) * 100:N2} %";
                finalTable.Rows.Add(rowTuszkaB);

                // Dodanie wierszy do tabeli finalTableElement dla każdego towaru
                DataRow rowCwiartka = finalTable.NewRow();
                rowCwiartka[""] = "Różnica";
                rowCwiartka["Przewidywalny"] = $"{sumCwiartka:N0} kg";
                rowCwiartka["Faktyczny"] = $"{(sumCwiartka / totalElementy) * 100:N2} %";
                finalTable.Rows.Add(rowCwiartka);

            }

            // Ustawienie źródła danych dla DataGridViewPrzewidywalnyElement
            datagrid.DataSource = finalTable;
            datagrid.Columns[""].HeaderText = "";
            datagrid.Columns["Przewidywalny"].HeaderText = "Przewidywalny";
            datagrid.Columns["Faktyczny"].HeaderText = "Faktyczny";

            // Formatowanie pierwszego wiersza (wiersz sumujący)
            FormatSumRow(datagrid);
            //SetRowHeights(18, datagrid);

        }

        private void FormatSumRow(DataGridView gridView)
        {
            DataGridViewRow sumRow = gridView.Rows[0];
            sumRow.DefaultCellStyle.BackColor = SystemColors.Highlight;
            sumRow.DefaultCellStyle.ForeColor = Color.White;
            sumRow.DefaultCellStyle.Font = new Font(gridView.Font, FontStyle.Bold);
        }


        private void CommandButton_Update_Click(object sender, EventArgs e)
        {
            WidokZamowienia widokZamowienia = new WidokZamowienia();

            // Wyświetlanie Form1
            widokZamowienia.Show();
            int selectedTowarId = Convert.ToInt32(comboBoxTowar.SelectedValue);
            DateTime selectedDate = myCalendar.SelectionStart;
            DateTime startOfWeek = selectedDate.AddDays(-(int)selectedDate.DayOfWeek + 1);
            DateTime endOfWeek = startOfWeek.AddDays(4);
            WyswietlPodsumowanie(selectedTowarId, startOfWeek, endOfWeek);
        }
    }
}
