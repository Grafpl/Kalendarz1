using Microsoft.Data.SqlClient;
using OfficeOpenXml.Style;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
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
            double sumaWartNettoSprzedazTowaryHandlowe = 0;
            double sumaIloscSprzedazTowaryHandlowe = 0;

            double sumaWartNettoSprzedazGarmaz = 0;
            double sumaIloscSprzedazGarmaz = 0;
            double sumaWartNettoSprzedazMasarnia = 0;
            double sumaIloscSprzedazMasarnia = 0;
            double sumaWartNettoSprzedazOdpady = 0;
            double sumaIloscSprzedazodpady = 0;
            double sumaWartNettoSprzedazZakup = 0;
            double sumaIloscSprzedazZakup = 0;
            double sumaWartNettoSprzedazIndyk = 0;
            double sumaIloscSprzedazIndyk = 0;
            double sumaWartNettoSprzedazKarma = 0;
            double sumaIloscSprzedazKarma = 0;


            double sumaWartNettoSprzedazMroznia1 = 0;
            double sumaIloscSprzedazMroznia1 = 0;

            double sumaWartNettoSprzedazMroznia = 0;
            double sumaIloscSprzedazMroznia = 0;

            double sumaWartNettoSprzedazDystrybucja1 = 0;
            double sumaIloscSprzedazDystrybucja1 = 0;

            double sumaWartNettoSprzedazDystrybucja = 0;
            double sumaIloscSprzedazDystrybucja = 0;


            double cenaZywca;
            double cenatuszki;

            string query5 = "SELECT DP.[kod], SUM(DP.[wartNetto]) AS SumaWartNetto, SUM(DP.[ilosc]) AS SumaIlosc " +
               "FROM [HANDEL].[HM].[DP] DP " +
               "INNER JOIN [HANDEL].[HM].[TW] TW ON DP.[idtw] = TW.[id] " +
               "INNER JOIN [HANDEL].[HM].[DK] DK ON DP.[super] = DK.[id] " +
               "WHERE DP.[data] BETWEEN @StartDate AND @EndDate AND TW.[katalog] = @katalog " +
               "GROUP BY DP.[kod] " +
               "ORDER BY SumaWartNetto DESC, SumaIlosc DESC;";

            // Odejmowanie jednego dnia od daty wybranej w DatePicker
            DateTime adjustedDate = startDate.AddDays(-1);


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
                connection.Open();

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
                dataGridView.RowHeadersVisible = false;

                dataGridView.AllowUserToResizeRows = true; // Umożliwienie zmiany wysokości wierszy


                

                //Świeże
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);



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
                        "Świeży",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedaz),
                        string.Format("{0:N0} kg", sumaIloscSprzedaz),
                        string.Format("{0:N2} zł/kg", sumaIloscSprzedaz != 0 ? sumaWartNettoSprzedaz / sumaIloscSprzedaz : 0)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightGreen;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                    cenatuszki = sumaIloscSprzedaz;

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
                        "Mrożony",
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

                //Odpady
                using (SqlCommand command = new SqlCommand(query5, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);
                    command.Parameters.AddWithValue("@katalog", "67094");
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


                        sumaWartNettoSprzedazOdpady += sumaWartNetto;
                        sumaIloscSprzedazodpady += sumaWartNetto;
                    }

                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        "Odpady",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazOdpady)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                }
                //Karma
                using (SqlCommand command = new SqlCommand(query5, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);
                    command.Parameters.AddWithValue("@katalog", "65910");
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


                        sumaWartNettoSprzedazKarma += sumaWartNetto;
                        sumaIloscSprzedazKarma += sumaIlosc;
                    }

                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        "Karma",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazKarma)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                }
                //Masarnia
                using (SqlCommand command = new SqlCommand(query5, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);
                    command.Parameters.AddWithValue("@katalog", "65911");
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


                        sumaWartNettoSprzedazMasarnia += sumaWartNetto;
                        sumaIloscSprzedazMasarnia += sumaIlosc;
                    }

                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        "Masarnia",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazMasarnia)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                }
                //Garmaż Zakup
                using (SqlCommand command = new SqlCommand(query5, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);
                    command.Parameters.AddWithValue("@katalog", "67198");
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


                        sumaWartNettoSprzedazGarmaz += sumaWartNetto;
                        sumaIloscSprzedazGarmaz += sumaIlosc;
                    }

                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        "Garmaz",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazGarmaz)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                }
                //Zakup zwrotów Zakup
                using (SqlCommand command = new SqlCommand(query5, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);
                    command.Parameters.AddWithValue("@katalog", "67104");
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


                        sumaWartNettoSprzedazZakup += sumaWartNetto;
                        sumaIloscSprzedazZakup += sumaIlosc;
                    }

                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        "Zwroty",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazZakup)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
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
                        "Żywy",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazZywiec),
                        string.Format("{0:N0} kg", sumaIloscSprzedazZywiec),
                        string.Format("{0:N2} zł/kg", sumaIloscSprzedazZywiec != 0 ? sumaWartNettoSprzedazZywiec / sumaIloscSprzedazZywiec : 0)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.Red;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                    cenaZywca = sumaIloscSprzedazZywiec;
                }

                double suma = sumaWartNettoSprzedaz + sumaWartNettoSprzedazMroz + sumaWartNettoSprzedazOdpady + sumaWartNettoSprzedazKarma + sumaWartNettoSprzedazMasarnia + sumaWartNettoSprzedazGarmaz + sumaWartNettoSprzedazZakup + sumaWartNettoSprzedazZywiec;
                // Dodaj wiersz sumy na końcu
                int sumRowIndex2 = dataGridView.Rows.Add(
                    "Suma Produkcyjna",
                    string.Format("{0:N0} zł", suma)
                    //string.Format("{0:N0} zł", cenatuszki - cenaZywca)

                ); ;
                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.BackColor = Color.DarkGray;
                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.ForeColor = Color.White;
                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.Font = new Font("Calibri", 13, FontStyle.Bold);

                // Dodaj pusty wiersz
                int emptyRowIndex = dataGridView.Rows.Add("", "", "", "");
                dataGridView.Rows[emptyRowIndex].DefaultCellStyle.BackColor = Color.White;


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

                            dataGridView.Rows[sumRowIndex2].DefaultCellStyle.BackColor = Color.LightYellow;
                            dataGridView.Rows[sumRowIndex2].DefaultCellStyle.ForeColor = Color.Black;
                            dataGridView.Rows[sumRowIndex2].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                        }



                        sumaWartNettoSprzedazZywiecSprzedaz += sumaWartNetto;
                        sumaIloscSprzedazZywiecSprzedaz += sumaIlosc;
                    }
                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        "Sprzedaż żywego",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazZywiecSprzedaz),
                        string.Format("{0:N0} kg", sumaIloscSprzedazZywiecSprzedaz),
                        string.Format("{0:N2} zł/kg", sumaIloscSprzedazZywiecSprzedaz != 0 ? sumaWartNettoSprzedazZywiecSprzedaz / sumaIloscSprzedazZywiecSprzedaz : 0)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                }
                //Pasza Zakup
                using (SqlCommand command = new SqlCommand(query5, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);
                    command.Parameters.AddWithValue("@katalog", "65883");

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
                        "Pasza",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazPasza)
                    ); ;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                }


                //Pisklak Zakup
                using (SqlCommand command = new SqlCommand(query5, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);
                    command.Parameters.AddWithValue("@katalog", "65884");

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
                        "Pisklak",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazPisklak)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                }
                //Zakup Indyk
                using (SqlCommand command = new SqlCommand(query5, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);
                    command.Parameters.AddWithValue("@katalog", "67096");
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


                        sumaWartNettoSprzedazIndyk += sumaWartNetto;
                        sumaIloscSprzedazIndyk += sumaIlosc;
                    }

                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        "Indyk",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazIndyk)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                }


                //Towary handlowe Zakup
                using (SqlCommand command = new SqlCommand(query5, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);
                    command.Parameters.AddWithValue("@katalog", "65881");
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


                        sumaWartNettoSprzedazTowaryHandlowe += sumaWartNetto;
                        sumaIloscSprzedazTowaryHandlowe += sumaIlosc;
                    }

                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        "Towary Handlowe",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazTowaryHandlowe)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                }




                suma = sumaWartNettoSprzedazZywiecSprzedaz + sumaWartNettoSprzedazPisklak + sumaWartNettoSprzedazIndyk + sumaWartNettoSprzedazPasza + sumaWartNettoSprzedazTowaryHandlowe;
                // Dodaj wiersz sumy na końcu
                sumRowIndex2 = dataGridView.Rows.Add(
                    "Towary handlowe",
                    string.Format("{0:N0} zł", suma)

                ); ;
                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.BackColor = Color.DarkGray;
                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.ForeColor = Color.White;
                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.Font = new Font("Calibri", 13, FontStyle.Bold);

                // Dodaj pusty wiersz
                emptyRowIndex = dataGridView.Rows.Add("", "", "", "");
                dataGridView.Rows[emptyRowIndex].DefaultCellStyle.BackColor = Color.White;


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

               

                

                
                

                

                double suma2 = suma + sumaWartNettoSprzedazKoszt + sumaWartNettoSprzedazPasza + sumaWartNettoSprzedazPisklak + sumaWartNettoSprzedazTowaryHandlowe;
                /* Dodaj wiersz sumy na końcu
                int sumRowIndex3 = dataGridView.Rows.Add(
                    "Po kosztach",
                    string.Format("{0:N0} zł", suma2)

                ); ;
                dataGridView.Rows[sumRowIndex3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                dataGridView.Rows[sumRowIndex3].DefaultCellStyle.BackColor = Color.DarkGray;
                dataGridView.Rows[sumRowIndex3].DefaultCellStyle.ForeColor = Color.White;
                dataGridView.Rows[sumRowIndex3].DefaultCellStyle.Font = new Font("Calibri", 13, FontStyle.Bold);
                */
                // Dodaj pusty wiersz
                emptyRowIndex = dataGridView.Rows.Add("", "", "", "");
                dataGridView.Rows[emptyRowIndex].DefaultCellStyle.BackColor = Color.White;

                string query8 =
                                                "SELECT kod, " +
                "ROUND(ABS(SUM([iloscwp])), 3) AS SumaIlosc, " +
                "ROUND(ABS(SUM([wartNetto])), 3) AS SumaWartosc " +
                "FROM [HANDEL].[HM].[MZ] " +
                "WHERE [data] >= '2020-01-07' " +
                "AND [data] <= DATEADD(DAY, -1, @EndDate) " + // Subtract one day from @EndDate
                "AND [magazyn] = 65552 " +
                "AND typ = '0' " +
                "GROUP BY kod " +
                "HAVING ROUND(SUM([iloscwp]), 3) <> 0 " +
                "ORDER BY SumaIlosc ASC;";


                //Stan Mroźni poczatek
                using (SqlCommand command = new SqlCommand(query8, connection))
                {
                    command.Parameters.AddWithValue("@EndDate", startDate);

                    //connection.Open();

                    DataTable dataTable = new DataTable();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(dataTable);
                    }

                    foreach (DataRow dataRow in dataTable.Rows)
                    {
                        string kod = dataRow["kod"].ToString();
                        double sumaWartNetto = Convert.ToDouble(dataRow["SumaWartosc"]);
                        double sumaIloscNetto = Convert.ToDouble(dataRow["SumaIlosc"]);
                        double cena = sumaIloscNetto != 0 ? sumaWartNetto / sumaIloscNetto : 0;

                        if (!Grupowanie.Checked)
                        {
                            int rowIndex = dataGridView.Rows.Add(
                            kod,
                            string.Format("{0:N0} zł", sumaWartNetto),
                            string.Format("{0:N0} kg", sumaIloscNetto),
                            string.Format("{0:N2} zł/kg", cena)
                        );
                            dataGridView.Rows[rowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                            // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                            dataGridView.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightBlue;
                            dataGridView.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.Black;

                            // Ustaw czcionkę dla każdego wiersza
                            dataGridView.Rows[rowIndex].DefaultCellStyle.Font = new Font("Calibri", 11);
                        }


                        sumaWartNettoSprzedazMroznia1 += sumaWartNetto;
                        sumaIloscSprzedazMroznia1 += sumaIloscNetto;


                    }


                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        "Stan Mroźni początkowy",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazMroznia1),
                        string.Format("{0:N0} kg", sumaIloscSprzedazMroznia1)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                }

                string query7 = "SELECT kod, " +
                "ROUND(ABS(SUM([iloscwp])), 3) AS SumaIlosc, " +
                "ROUND(ABS(SUM([wartNetto])), 3) AS SumaWartosc " +
                "FROM [HANDEL].[HM].[MZ] " +
                "WHERE [data] >= '2020-01-07' " +
                "AND [data] <= DATEADD(DAY, 0, @EndDate) " + // Add one day to @EndDate
                "AND [magazyn] = 65552 " +
                "AND typ = '0' " +
                "GROUP BY kod " +
                "HAVING ROUND(SUM([iloscwp]), 3) <> 0 " +
                "ORDER BY SumaIlosc ASC;";


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
                        double sumaWartNetto = Convert.ToDouble(dataRow["SumaWartosc"]);
                        double sumaIloscNetto = Convert.ToDouble(dataRow["SumaIlosc"]);
                        double cena = sumaIloscNetto != 0 ? sumaWartNetto / sumaIloscNetto : 0;


                        if (!Grupowanie.Checked)
                        {
                            int rowIndex = dataGridView.Rows.Add(
                            kod,
                            string.Format("{0:N0} zł", sumaWartNetto),
                            string.Format("{0:N0} kg", sumaIloscNetto),
                            string.Format("{0:N2} zł/kg", cena)
                        );
                            dataGridView.Rows[rowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                            // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                            dataGridView.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightBlue;
                            dataGridView.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.Black;

                            // Ustaw czcionkę dla każdego wiersza
                            dataGridView.Rows[rowIndex].DefaultCellStyle.Font = new Font("Calibri", 11);
                        }


                        sumaWartNettoSprzedazMroznia += sumaWartNetto;
                        sumaIloscSprzedazMroznia += sumaIloscNetto;


                    }
                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        "Stan Mroźni końcowy",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazMroznia),
                        string.Format("{0:N0} kg", sumaIloscSprzedazMroznia)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                }

                suma = sumaIloscSprzedazMroznia - sumaIloscSprzedazMroznia1;
                suma2 = sumaWartNettoSprzedazMroznia - sumaWartNettoSprzedazMroznia1;
                sumRowIndex2 = dataGridView.Rows.Add(
                    "Różnica:",
                    string.Format("{0:N0} zł", suma2),
                    string.Format("{0:N0} kg", suma)


                ) ; ;
                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.BackColor = Color.DarkGray;
                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.ForeColor = Color.White;
                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.Font = new Font("Calibri", 13, FontStyle.Bold);
                /*
                ///Dystrybucja
                string query9 =
                                "SELECT kod, " +
                "ROUND(ABS(SUM([iloscwp])), 3) AS SumaIlosc, " +
                "ROUND(ABS(SUM([wartNetto])), 3) AS SumaWartosc " +
                "FROM [HANDEL].[HM].[MZ] " +
                "WHERE [data] >= '2020-01-07' " +
                "AND [data] <= DATEADD(DAY, -1, @EndDate) " + // Subtract one day from @EndDate
                "AND [magazyn] = 65556 " +
                "AND typ = '0' " +
                "GROUP BY kod " +
                "HAVING ROUND(SUM([iloscwp]), 3) <> 0 " +
                "ORDER BY SumaIlosc ASC;";

                //Stan Dystrybucji poczatek
                using (SqlCommand command = new SqlCommand(query9, connection))
                {
                    command.Parameters.AddWithValue("@EndDate", startDate);

                    //connection.Open();

                    DataTable dataTable = new DataTable();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(dataTable);
                    }

                    foreach (DataRow dataRow in dataTable.Rows)
                    {
                        string kod = dataRow["kod"].ToString();
                        double sumaWartNetto = Convert.ToDouble(dataRow["SumaWartosc"]);
                        double sumaIloscNetto = Convert.ToDouble(dataRow["SumaIlosc"]);
                        double cena = sumaIloscNetto != 0 ? sumaWartNetto / sumaIloscNetto : 0;

                        if (!Grupowanie.Checked)
                        {
                            int rowIndex = dataGridView.Rows.Add(
                            kod,
                            string.Format("{0:N0} zł", sumaWartNetto),
                            string.Format("{0:N0} kg", sumaIloscNetto),
                            string.Format("{0:N2} zł/kg", cena)
                        );
                            dataGridView.Rows[rowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                            // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                            dataGridView.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightBlue;
                            dataGridView.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.Black;

                            // Ustaw czcionkę dla każdego wiersza
                            dataGridView.Rows[rowIndex].DefaultCellStyle.Font = new Font("Calibri", 11);
                        }

                        sumaWartNettoSprzedazDystrybucja1 += sumaWartNetto;
                        sumaIloscSprzedazDystrybucja1 += sumaIloscNetto;


                    }


                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        "Stan Dyst. początkowy",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazDystrybucja1),
                        string.Format("{0:N0} kg", sumaIloscSprzedazDystrybucja1)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                }

                string query10 = "SELECT kod, " +
                "ROUND(ABS(SUM([iloscwp])), 3) AS SumaIlosc, " +
                "ROUND(ABS(SUM([wartNetto])), 3) AS SumaWartosc " +
                "FROM [HANDEL].[HM].[MZ] " +
                "WHERE [data] >= '2020-01-07' " +
                "AND [data] < DATEADD(DAY, 0, @EndDate) " + // Add one day to @EndDate
                "AND [magazyn] = 65556 " +
                "AND typ = '0' " +
                "GROUP BY kod " +
                "HAVING ROUND(SUM([iloscwp]), 3) <> 0 " +
                "ORDER BY SumaIlosc ASC;";


                //Stan Mroźni
                using (SqlCommand command = new SqlCommand(query10, connection))
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
                        double sumaWartNetto = Convert.ToDouble(dataRow["SumaWartosc"]);
                        double sumaIloscNetto = Convert.ToDouble(dataRow["SumaIlosc"]);
                        double cena = sumaIloscNetto != 0 ? sumaWartNetto / sumaIloscNetto : 0;


                        if (!Grupowanie.Checked)
                        {
                            int rowIndex = dataGridView.Rows.Add(
                            kod,
                            string.Format("{0:N0} zł", sumaWartNetto),
                            string.Format("{0:N0} kg", sumaIloscNetto),
                            string.Format("{0:N2} zł/kg", cena)
                        );
                            dataGridView.Rows[rowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                            // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                            dataGridView.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightBlue;
                            dataGridView.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.Black;

                            // Ustaw czcionkę dla każdego wiersza
                            dataGridView.Rows[rowIndex].DefaultCellStyle.Font = new Font("Calibri", 11);
                        }


                        sumaWartNettoSprzedazDystrybucja += sumaWartNetto;
                        sumaIloscSprzedazDystrybucja += sumaIloscNetto;


                    }
                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        "Stan Dyst. końcowy",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazMroznia),
                        string.Format("{0:N0} kg", sumaIloscSprzedazDystrybucja)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                }

                suma = sumaWartNettoSprzedazMroznia - sumaWartNettoSprzedazMroznia1;
                suma2 = sumaIloscSprzedazDystrybucja - sumaIloscSprzedazDystrybucja1;
                sumRowIndex2 = dataGridView.Rows.Add(
                    "Różnica:",
                    string.Format("{0:N0} zł", suma2),
                    string.Format("{0:N0} kg", suma)


                ); ;
                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.BackColor = Color.DarkGray;
                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.ForeColor = Color.White;
                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.Font = new Font("Calibri", 13, FontStyle.Bold);
                */



                // Zdefiniuj zmienną na sumę netto
            }
            SetRowHeights(18, dataGridView);
        }
        private void SetRowHeights(int height, DataGridView dataGridView)
        {
            // Ustawienie wysokości wszystkich wierszy na określoną wartość
            foreach (DataGridViewRow row in dataGridView.Rows)
            {
                row.Height = height;
            }
        }
        private void ExportToExcel(DataGridView dataGridView)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Excel files (*.xlsx)|*.xlsx";
                saveFileDialog.FilterIndex = 2;
                saveFileDialog.RestoreDirectory = true;

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = saveFileDialog.FileName;

                    // Set the license context to NonCommercial
                    OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

                    using (ExcelPackage excelPackage = new ExcelPackage())
                    {
                        ExcelWorksheet worksheet = excelPackage.Workbook.Worksheets.Add("Data");

                        // Adding headers
                        for (int i = 0; i < dataGridView.Columns.Count; i++)
                        {
                            worksheet.Cells[1, i + 1].Value = dataGridView.Columns[i].HeaderText;
                            worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                            worksheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                            worksheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.Gray);
                            worksheet.Cells[1, i + 1].Style.Font.Color.SetColor(Color.White);
                            worksheet.Cells[1, i + 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        }

                        // Adding data
                        for (int i = 0; i < dataGridView.Rows.Count; i++)
                        {
                            for (int j = 0; j < dataGridView.Columns.Count; j++)
                            {
                                worksheet.Cells[i + 2, j + 1].Value = dataGridView.Rows[i].Cells[j].Value;
                                worksheet.Cells[i + 2, j + 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                            }
                        }

                        // Auto fit columns
                        worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                        // Save to file
                        FileInfo fi = new FileInfo(filePath);
                        excelPackage.SaveAs(fi);
                    }

                    MessageBox.Show("Export successful!", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
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

        private void button1_Click(object sender, EventArgs e)
        {
            ExportToExcel(dataGridView1);
        }
    }
}