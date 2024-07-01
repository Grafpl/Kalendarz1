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

namespace Kalendarz1
{
    public partial class WidokSprzeZakup : Form
    {
        private int LastRow = 0; // Dodaj deklarację zmiennej LastRow na poziomie klasy
        public WidokSprzeZakup()
        {
            InitializeComponent();
            // Oblicz pierwszy dzień poprzedniego miesiąca
            DateTime today = DateTime.Today;
            DateTime firstDayOfPreviousMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-1);

            // Oblicz ostatni dzień poprzedniego miesiąca
            DateTime lastDayOfPreviousMonth = firstDayOfPreviousMonth.AddMonths(1).AddDays(-1);

            // Ustaw wartości w DateTimePicker dataPoczatek i dataKoniec
            dataPoczatek.Value = firstDayOfPreviousMonth;
            dataKoniec.Value = lastDayOfPreviousMonth;
            // Oblicz pierwszy dzień bieżącego miesiąca
            DateTime firstDayOfCurrentMonth = new DateTime(today.Year, today.Month, 1);

            // Oblicz ostatni dzień bieżącego miesiąca
            DateTime lastDayOfCurrentMonth = firstDayOfCurrentMonth.AddMonths(1).AddDays(-1);

            // Ustaw wartości w DateTimePicker dataPoczatek2 i dataKoniec2
            dataPoczatek2.Value = firstDayOfCurrentMonth;
            dataKoniec2.Value = lastDayOfCurrentMonth;


        }

        private void WykonajZapytanieSQL(DataGridView dataGridView, DateTime startDate, DateTime endDate)
        {
            double sumaWartNettoSprzedaz = 0;
            double sumaIloscSprzedaz = 0;
            double sumaWartNettoSprzedazMroz = 0;
            double sumaIloscSprzedazMroz = 0;
            double sumaWartNettoSprzedazZywiec = 0;
            double sumaIloscSprzedazZywiec = 0;
            double sumaWartNettoSprzedazZywiecSprzedaz = 0;
            double sumaIloscSprzedazZywiecSprzedaz = 0;

            double sumaWartNettoSprzedazKoszt = 0;
            double sumaIloscSprzedazKoszt = 0;
            double sumaWartNettoSprzedazPasza = 0;
            double sumaIloscSprzedazPasza = 0;
            double sumaWartNettoSprzedazPisklak = 0;
            double sumaIloscSprzedazPisklak = 0;

            double sumaWartNettoSprzedazMroznia = 0;
            double sumaIloscSprzedazMroznia = 0;


            string connectionString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
            string query = "SELECT DP.kod, SUM(DP.wartNetto) AS SumaWartNetto, SUM(DP.ilosc) AS SumaIlosc " +
                           "FROM HANDEL.HM.DP DP " +
                           "INNER JOIN HANDEL.HM.TW TW ON DP.idtw = TW.id " +
                           "INNER JOIN HANDEL.HM.DK DK ON DP.super = DK.id " +
                           "WHERE DP.data BETWEEN @StartDate AND @EndDate AND (TW.katalog = 67095 OR TW.[katalog] = 67094) " +
                           "GROUP BY DP.kod " +
                           "ORDER BY SumaWartNetto DESC, SumaIlosc DESC;";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                //Świeże
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);

                    connection.Open();

                    DataTable dataTable = new DataTable();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(dataTable);
                    }

                    dataGridView.Rows.Clear();
                    dataGridView.Columns.Clear();

                    dataGridView.Columns.Add("Nazwa", "Nazwa");
                    dataGridView.Columns[0].Width = 140; // Szerokość pierwszej kolumny (Nazwa)
                    dataGridView.Columns.Add("Netto", "Netto");
                    dataGridView.Columns[1].Width = 100; // Szerokość drugiej kolumny (Netto)
                    dataGridView.Columns.Add("Ilość", "Ilość");
                    dataGridView.Columns[2].Width = 90; // Szerokość trzeciej kolumny (Ilość)
                    dataGridView.Columns.Add("Cena", "Cena");
                    dataGridView.Columns[3].Width = 80; // Szerokość czwartej kolumny (Cena)

                    dataGridView.AllowUserToResizeRows = true; // Umożliwienie zmiany wysokości wierszy



                    foreach (DataRow dataRow in dataTable.Rows)
                    {
                        string kod = dataRow["kod"].ToString();
                        double sumaWartNetto = Convert.ToDouble(dataRow["SumaWartNetto"]);
                        double sumaIlosc = Convert.ToDouble(dataRow["SumaIlosc"]);
                        double cena = sumaIlosc != 0 ? sumaWartNetto / sumaIlosc : 0;

                        if (!Grupowanie.Checked)
                        {
                            int rowIndex = dataGridView.Rows.Add(
                            kod,
                            string.Format("{0:N0} zł", sumaWartNetto),
                            string.Format("{0:N0} kg", sumaIlosc),
                            string.Format("{0:N2} zł/kg", cena)
                        );

                            dataGridView.Rows[rowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                            // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                            dataGridView.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightGreen;
                            dataGridView.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.Black;

                            // Ustaw czcionkę dla każdego wiersza
                            dataGridView.Rows[rowIndex].DefaultCellStyle.Font = new Font("Calibri", 11);
                        }

                        sumaWartNettoSprzedaz += sumaWartNetto;
                        sumaIloscSprzedaz += sumaIlosc;
                    }

                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        "SUMA Świeżego",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedaz),
                        string.Format("{0:N0} kg", sumaIloscSprzedaz),
                        string.Format("{0:N2} zł/kg", sumaIloscSprzedaz != 0 ? sumaWartNettoSprzedaz / sumaIloscSprzedaz : 0)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightGreen;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);


                }
                string query2 = "SELECT DP.kod, SUM(DP.wartNetto) AS SumaWartNetto, SUM(DP.ilosc) AS SumaIlosc " +
                           "FROM HANDEL.HM.DP DP " +
                           "INNER JOIN HANDEL.HM.TW TW ON DP.idtw = TW.id " +
                           "INNER JOIN HANDEL.HM.DK DK ON DP.super = DK.id " +
                           "WHERE DP.data BETWEEN @StartDate AND @EndDate AND TW.katalog = 67153 " + // Zmiana katalogu na 67153
                           "GROUP BY DP.kod " +
                           "ORDER BY SumaWartNetto DESC, SumaIlosc DESC;";

                //Mrożonka
                using (SqlCommand command = new SqlCommand(query2, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);

                    //connection.Open();

                    DataTable dataTable = new DataTable();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(dataTable);
                    }



                    foreach (DataRow dataRow in dataTable.Rows)
                    {
                        string kod = dataRow["kod"].ToString();
                        double sumaWartNetto = Convert.ToDouble(dataRow["SumaWartNetto"]);
                        double sumaIlosc = Convert.ToDouble(dataRow["SumaIlosc"]);
                        double cena = sumaIlosc != 0 ? sumaWartNetto / sumaIlosc : 0;

                        if (!Grupowanie.Checked)
                        {
                            int rowIndex = dataGridView.Rows.Add(
                            kod,
                            string.Format("{0:N0} zł", sumaWartNetto),
                            string.Format("{0:N0} kg", sumaIlosc),
                            string.Format("{0:N2} zł/kg", cena)
                        );
                            dataGridView.Rows[rowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                            // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                            dataGridView.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightBlue;
                            dataGridView.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.Black;

                            // Ustaw czcionkę dla każdego wiersza
                            dataGridView.Rows[rowIndex].DefaultCellStyle.Font = new Font("Calibri", 11);
                        }


                        sumaWartNettoSprzedazMroz += sumaWartNetto;
                        sumaIloscSprzedazMroz += sumaIlosc;
                    }

                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        "Suma Mroźni",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazMroz),
                        string.Format("{0:N0} kg", sumaIloscSprzedazMroz),
                        string.Format("{0:N2} zł/kg", sumaIloscSprzedazMroz != 0 ? sumaWartNettoSprzedazMroz / sumaIloscSprzedazMroz : 0)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightBlue;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);


                }

                string query3 = "SELECT DP.[kod], SUM(DP.[wartNetto]) AS SumaWartNetto, SUM(DP.[ilosc]) AS SumaIlosc " +
                             "FROM [HANDEL].[HM].[DP] DP " +
                             "INNER JOIN [HANDEL].[HM].[TW] TW ON DP.[idtw] = TW.[id] " +
                             "INNER JOIN [HANDEL].[HM].[DK] DK ON DP.[super] = DK.[id] " +
                             "WHERE DP.[data] BETWEEN @StartDate AND @EndDate AND TW.[katalog] = 65882" +
                             "GROUP BY DP.[kod] " +
                             "ORDER BY SumaWartNetto DESC, SumaIlosc DESC;";
                //Żywiec
                using (SqlCommand command = new SqlCommand(query3, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);

                    //connection.Open();

                    DataTable dataTable = new DataTable();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(dataTable);
                    }



                    foreach (DataRow dataRow in dataTable.Rows)
                    {
                        string kod = dataRow["kod"].ToString();
                        double sumaWartNetto = Convert.ToDouble(dataRow["SumaWartNetto"]);
                        double sumaIlosc = Convert.ToDouble(dataRow["SumaIlosc"]);
                        double cena = sumaIlosc != 0 ? sumaWartNetto / sumaIlosc : 0;

                        if (!Grupowanie.Checked)
                        {
                            int rowIndex = dataGridView.Rows.Add(
                            kod,
                            string.Format("{0:N0} zł", sumaWartNetto),
                            string.Format("{0:N0} kg", sumaIlosc),
                            string.Format("{0:N2} zł/kg", cena)
                        );
                            dataGridView.Rows[rowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                            // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                            dataGridView.Rows[rowIndex].DefaultCellStyle.BackColor = Color.Red;
                            dataGridView.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.Black;

                            // Ustaw czcionkę dla każdego wiersza
                            dataGridView.Rows[rowIndex].DefaultCellStyle.Font = new Font("Calibri", 11);
                        }



                        sumaWartNettoSprzedazZywiec += sumaWartNetto;
                        sumaIloscSprzedazZywiec += sumaIlosc;
                    }

                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        "Suma Żywca",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazZywiec),
                        string.Format("{0:N0} kg", sumaIloscSprzedazZywiec),
                        string.Format("{0:N2} zł/kg", sumaIloscSprzedazZywiec != 0 ? sumaWartNettoSprzedazZywiec / sumaIloscSprzedazZywiec : 0)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.Red;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                }

                string query0 = "SELECT DP.[kod], SUM(DP.[wartNetto]) AS SumaWartNetto, SUM(DP.[ilosc]) AS SumaIlosc " +
                             "FROM [HANDEL].[HM].[DP] DP " +
                             "INNER JOIN [HANDEL].[HM].[TW] TW ON DP.[idtw] = TW.[id] " +
                             "INNER JOIN [HANDEL].[HM].[DK] DK ON DP.[super] = DK.[id] " +
                             "WHERE DP.[data] BETWEEN @StartDate AND @EndDate AND TW.[katalog] = 65913 " +
                             "GROUP BY DP.[kod] " +
                             "ORDER BY SumaWartNetto DESC, SumaIlosc DESC;";
                //Żywiec sprzedaz
                using (SqlCommand command = new SqlCommand(query0, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);

                    //connection.Open();

                    DataTable dataTable = new DataTable();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(dataTable);
                    }



                    foreach (DataRow dataRow in dataTable.Rows)
                    {
                        string kod = dataRow["kod"].ToString();
                        double sumaWartNetto = Convert.ToDouble(dataRow["SumaWartNetto"]);
                        double sumaIlosc = Convert.ToDouble(dataRow["SumaIlosc"]);
                        double cena = sumaIlosc != 0 ? sumaWartNetto / sumaIlosc : 0;

                        if (!Grupowanie.Checked)
                        {
                            int rowIndex = dataGridView.Rows.Add(
                            kod,
                            string.Format("{0:N0} zł", sumaWartNetto),
                            string.Format("{0:N0} kg", sumaIlosc),
                            string.Format("{0:N2} zł/kg", cena)
                        );
                            dataGridView.Rows[rowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                            // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                            dataGridView.Rows[rowIndex].DefaultCellStyle.BackColor = Color.Red;
                            dataGridView.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.Black;

                            // Ustaw czcionkę dla każdego wiersza
                            dataGridView.Rows[rowIndex].DefaultCellStyle.Font = new Font("Calibri", 11);
                        }



                        sumaWartNettoSprzedazZywiecSprzedaz += sumaWartNetto;
                        sumaIloscSprzedazZywiecSprzedaz += sumaIlosc;
                    }
                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        "Suma sprzedaży Żywca",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazZywiecSprzedaz),
                        string.Format("{0:N0} kg", sumaIloscSprzedazZywiecSprzedaz),
                        string.Format("{0:N2} zł/kg", sumaIloscSprzedazZywiecSprzedaz != 0 ? sumaWartNettoSprzedazZywiecSprzedaz / sumaIloscSprzedazZywiecSprzedaz : 0)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.Red;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                }





                double suma = sumaWartNettoSprzedaz + sumaWartNettoSprzedazMroz + sumaWartNettoSprzedazZywiec + sumaWartNettoSprzedazZywiecSprzedaz;
                // Dodaj wiersz sumy na końcu
                int sumRowIndex2 = dataGridView.Rows.Add(
                    "Sprzedaz-Zakup",
                    string.Format("{0:N0} zł", suma)

                ); ;
                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.BackColor = Color.DarkGray;
                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.ForeColor = Color.White;
                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.Font = new Font("Calibri", 13, FontStyle.Bold);




                //Kosztówki
                string query4 = "SELECT DK.[Netto], SUM(DK.[Netto]) AS SumaWartNetto " +
                           "FROM [HANDEL].[HM].[DK] DK " +
                           "WHERE DK.[data] BETWEEN @StartDate AND @EndDate " +
                           "AND (DK.[seria] = 'sfzk' OR DK.[seria] = 'sfkk' OR DK.[seria] = 'sRUZ') " +
                           "GROUP BY DK.[Netto] " +
                           "ORDER BY SumaWartNetto DESC;";
                using (SqlCommand command = new SqlCommand(query4, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);

                    //connection.Open();

                    DataTable dataTable = new DataTable();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(dataTable);
                    }



                    foreach (DataRow dataRow in dataTable.Rows)
                    {
                        string kod = "Faktury Kosztowe";
                        double sumaWartNetto = Convert.ToDouble(dataRow["SumaWartNetto"]);
                        double sumaIlosc = 0;
                        double cena = 0;

                        sumaWartNettoSprzedazKoszt += sumaWartNetto;
                        sumaIloscSprzedazKoszt += sumaIlosc;
                    }

                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        "Faktury Kosztowe",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazKoszt)
                    ); ;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                }
                string query5 = "SELECT DP.[kod], SUM(DP.[wartNetto]) AS SumaWartNetto, SUM(DP.[ilosc]) AS SumaIlosc " +
                               "FROM [HANDEL].[HM].[DP] DP " +
                               "INNER JOIN [HANDEL].[HM].[TW] TW ON DP.[idtw] = TW.[id] " +
                               "INNER JOIN [HANDEL].[HM].[DK] DK ON DP.[super] = DK.[id] " +
                               "WHERE DP.[data] BETWEEN @StartDate AND @EndDate AND TW.[katalog] = 65883 " +
                               "GROUP BY DP.[kod] " +
                               "ORDER BY SumaWartNetto DESC, SumaIlosc DESC;";
                //Pasza Zakup
                using (SqlCommand command = new SqlCommand(query5, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);

                    //connection.Open();

                    DataTable dataTable = new DataTable();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(dataTable);
                    }



                    foreach (DataRow dataRow in dataTable.Rows)
                    {
                        string kod = dataRow["kod"].ToString();
                        double sumaWartNetto = Convert.ToDouble(dataRow["SumaWartNetto"]);
                        double sumaIlosc = Convert.ToDouble(dataRow["SumaIlosc"]);
                        double cena = sumaIlosc != 0 ? sumaWartNetto / sumaIlosc : 0;

                        sumaWartNettoSprzedazPasza += sumaWartNetto;
                        sumaIloscSprzedazPasza += sumaIlosc;
                    }

                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        "Suma Paszy",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazPasza)
                    ); ;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                }
                string query6 = "SELECT DP.[kod], SUM(DP.[wartNetto]) AS SumaWartNetto, SUM(DP.[ilosc]) AS SumaIlosc " +
                               "FROM [HANDEL].[HM].[DP] DP " +
                               "INNER JOIN [HANDEL].[HM].[TW] TW ON DP.[idtw] = TW.[id] " +
                               "INNER JOIN [HANDEL].[HM].[DK] DK ON DP.[super] = DK.[id] " +
                               "WHERE DP.[data] BETWEEN @StartDate AND @EndDate AND TW.[katalog] = 65884 " +
                               "GROUP BY DP.[kod] " +
                               "ORDER BY SumaWartNetto DESC, SumaIlosc DESC;";
                //Pisklak Zakup
                using (SqlCommand command = new SqlCommand(query6, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);

                    //connection.Open();

                    DataTable dataTable = new DataTable();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(dataTable);
                    }



                    foreach (DataRow dataRow in dataTable.Rows)
                    {
                        string kod = dataRow["kod"].ToString();
                        double sumaWartNetto = Convert.ToDouble(dataRow["SumaWartNetto"]);
                        double sumaIlosc = Convert.ToDouble(dataRow["SumaIlosc"]);
                        double cena = sumaIlosc != 0 ? sumaWartNetto / sumaIlosc : 0;

                        sumaWartNettoSprzedazPisklak += sumaWartNetto;
                        sumaIloscSprzedazPisklak += sumaIlosc;
                    }

                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        "Suma Pisklak",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazPisklak)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                }

                double suma2 = suma + sumaWartNettoSprzedazKoszt + sumaWartNettoSprzedazPasza + sumaWartNettoSprzedazPisklak;
                // Dodaj wiersz sumy na końcu
                int sumRowIndex3 = dataGridView.Rows.Add(
                    "Po kosztach",
                    string.Format("{0:N0} zł", suma2)

                ); ;
                dataGridView.Rows[sumRowIndex3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                dataGridView.Rows[sumRowIndex3].DefaultCellStyle.BackColor = Color.DarkGray;
                dataGridView.Rows[sumRowIndex3].DefaultCellStyle.ForeColor = Color.White;
                dataGridView.Rows[sumRowIndex3].DefaultCellStyle.Font = new Font("Calibri", 13, FontStyle.Bold);

                string query7 = "SELECT kod, " +
                "ABS(SUM(ilosc)) AS SumaIlosc, " +
                "ABS(SUM(przychod)) AS SumaWartosc " +
                "FROM [HANDEL].[HM].[MZ] " +
                "WHERE [data] BETWEEN '2020-01-01' AND @EndDate AND [magazyn] = 65552 " +
                "GROUP BY kod " +
                "HAVING SUM(ilosc) < 0 " +
                "order by sumailosc desc";

                //Stan Mroźni
                using (SqlCommand command = new SqlCommand(query7, connection))
                {
                    command.Parameters.AddWithValue("@EndDate", endDate);

                    //connection.Open();

                    DataTable dataTable = new DataTable();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(dataTable);
                    }

                    foreach (DataRow dataRow in dataTable.Rows)
                    {
                        string kod = dataRow["kod"].ToString();
                        sumaWartNettoSprzedazMroznia = Convert.ToDouble(dataRow["SumaWartosc"]);
                        sumaIloscSprzedazMroznia = Convert.ToDouble(dataRow["SumaIlosc"]);
                        double cena = sumaIloscSprzedazMroznia != 0 ? sumaWartNettoSprzedazMroznia / sumaIloscSprzedazMroznia : 0;

                        if (!Grupowanie.Checked)
                        {
                            int rowIndex = dataGridView.Rows.Add(
                            kod,
                            string.Format("{0:N0} zł", sumaWartNettoSprzedazMroznia),
                            string.Format("{0:N0} kg", sumaIloscSprzedazMroznia),
                            string.Format("{0:N2} zł/kg", cena)
                        );
                            dataGridView.Rows[rowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                            // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                            dataGridView.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightBlue;
                            dataGridView.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.Black;

                            // Ustaw czcionkę dla każdego wiersza
                            dataGridView.Rows[rowIndex].DefaultCellStyle.Font = new Font("Calibri", 11);
                        }


                        sumaWartNettoSprzedazMroznia += sumaWartNettoSprzedazMroznia;
                        sumaIloscSprzedazMroznia += sumaIloscSprzedazMroznia;

                        
                    }
                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        "Stan Mroźni",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazMroznia),
                        string.Format("{0:N0} kg", sumaIloscSprzedazMroznia)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                }
            

                // Zdefiniuj zmienną na sumę netto
            }
        }

        private void WykonajZapytanieSQL2()
        {
            DateTime startDate = dataPoczatek.Value.Date;
            DateTime endDate = dataKoniec.Value.Date;

            string connectionString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
            string query = @"
    SELECT 
        data, 
        Zywiec,
        Przychod,
        CASE 
            WHEN Zywiec = 0 
                 THEN NULL 
            ELSE 
                 ROUND((Przychod / Zywiec) * 100, 1) 
        END AS Procent,
        Zywiec - Przychod AS Roznica
    FROM (
        SELECT 
            MZ.data, 
            SUM(CASE WHEN seria = 'sPZ' THEN ABS(MZ.ilosc) ELSE 0 END) AS Zywiec,
            SUM(CASE WHEN seria = 'sPWU' THEN ABS(MZ.ilosc) ELSE 0 END) AS Przychod
        FROM 
            [HANDEL].[HM].[MG]
        JOIN 
            [HANDEL].[HM].[MZ] MZ ON MG.id = MZ.super
        JOIN 
            [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id
        WHERE 
            MZ.Data BETWEEN @StartDate AND @EndDate AND (seria = 'sPZ' AND TW.katalog = 65882)
            OR (seria = 'sPWU' AND TW.katalog = 67095)
            AND MG.anulowany = 0
        GROUP BY 
            MZ.data
    ) AS subquery
    ORDER BY 
        data DESC;";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);

                    connection.Open();

                    DataTable dataTable = new DataTable();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(dataTable);
                    }

                    dataGridView2.Rows.Clear();
                    dataGridView2.Columns.Clear();

                    // Add columns to dataGridView2
                    dataGridView2.Columns.Add("Data2", "Data");
                    dataGridView2.Columns[0].Width = 200; // Width of the first column (Data)
                    dataGridView2.Columns.Add("Zywiec", "Zywiec");
                    dataGridView2.Columns[1].Width = 150; // Width of the second column (Zywiec)
                    dataGridView2.Columns.Add("Przychod", "Przychod");
                    dataGridView2.Columns[2].Width = 150; // Width of the third column (Przychod)
                    dataGridView2.Columns.Add("Procent", "Procent");
                    dataGridView2.Columns[3].Width = 150; // Width of the fourth column (Procent)
                    dataGridView2.Columns.Add("Roznica", "Roznica");
                    dataGridView2.Columns[4].Width = 150; // Width of the fifth column (Roznica)

                    // Add rows to dataGridView2 from dataTable
                    foreach (DataRow row in dataTable.Rows)
                    {
                        dataGridView2.Rows.Add(
                            row["Data"],
                            row["Zywiec"],
                            row["Przychod"],
                            row["Procent"],
                            row["Roznica"]);
                    }
                }
            }
        }



        private void dataPoczatek_ValueChanged(object sender, EventArgs e)
        {

        }


        private void dataKoniec_ValueChanged(object sender, EventArgs e)
        {


            //WykonajZapytanieSQL2();
        }

        private void dataPoczatek2_ValueChanged(object sender, EventArgs e)
        {

        }

        private void dataKoniec2_ValueChanged(object sender, EventArgs e)
        {

        }

        private void refresh_Click(object sender, EventArgs e)
        {
            DateTime startDate = dataPoczatek.Value.Date;
            DateTime endDate = dataKoniec.Value.Date;
            WykonajZapytanieSQL(dataGridView1, startDate, endDate);
            startDate = dataPoczatek2.Value.Date;
            endDate = dataKoniec2.Value.Date;
            WykonajZapytanieSQL(dataGridView2, startDate, endDate);
        }
    }
}