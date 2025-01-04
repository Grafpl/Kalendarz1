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
