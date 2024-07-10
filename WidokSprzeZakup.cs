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
            double sumaWartSprzedaz = 0;
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

            bool isGrupowanieChecked = Grupowanie.Checked;
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

            string connectionString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
            string ZapytanieStan = @"
                SELECT kod, 
                       ROUND(ABS(SUM([iloscwp])), 3) AS SumaIlosc, 
                       ROUND(ABS(SUM([wartNetto])), 3) AS SumaWartosc 
                FROM [HANDEL].[HM].[MZ] 
                WHERE [data] >= '2020-01-07' 
                  AND [data] <= DATEADD(DAY, @IleDni, @EndDate) 
                  AND [magazyn] = @Magazyn 
                  AND typ = '0' 
                GROUP BY kod 
                HAVING ROUND(SUM([iloscwp]), 3) <> 0 
                ORDER BY SumaIlosc ASC;";


            string ZapytanieHandel = "SELECT DP.[kod], SUM(DP.[wartNetto]) AS SumaWartNetto, SUM(DP.[ilosc]) AS SumaIlosc " +
               "FROM [HANDEL].[HM].[DP] DP " +
               "INNER JOIN [HANDEL].[HM].[TW] TW ON DP.[idtw] = TW.[id] " +
               "INNER JOIN [HANDEL].[HM].[DK] DK ON DP.[super] = DK.[id] " +
               "WHERE DP.[data] BETWEEN @StartDate AND @EndDate AND TW.[katalog] = @katalog " +
               "GROUP BY DP.[kod] " +
               "ORDER BY SumaWartNetto DESC, SumaIlosc DESC;";


            
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
                dataGridView.Columns[0].Width = 155; // Szerokość pierwszej kolumny (Nazwa)
                dataGridView.Columns.Add("Netto", "Netto");
                dataGridView.Columns[1].Width = 100; // Szerokość drugiej kolumny (Netto)
                dataGridView.Columns.Add("Ilość", "Ilość");
                dataGridView.Columns[2].Width = 100; // Szerokość trzeciej kolumny (Ilość)
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

                        sumaWartSprzedaz += sumaWartNetto;
                        sumaIloscSprzedaz += sumaIlosc;
                    }

                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        "Świeży",
                        string.Format("{0:N0} zł", sumaWartSprzedaz),
                        string.Format("{0:N0} kg", sumaIloscSprzedaz),
                        string.Format("{0:N2} zł/kg", sumaIloscSprzedaz != 0 ? sumaWartSprzedaz / sumaIloscSprzedaz : 0)
                    );
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightGreen;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                    cenatuszki = sumaIloscSprzedaz;

                }
                (double sumaWartMrozonka, double sumaIloscMrozonka) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "67153", "Sprzedaż Mrozone", isGrupowanieChecked);
                (double sumaWartOdpady, double sumaIloscOdpady) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "67094", "Sprzedaż Odpady", isGrupowanieChecked);
                (double sumaWartKarma, double sumaIloscKarma) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "65910", "Sprzedaż Karma", isGrupowanieChecked);
                (double sumaWartMasarnia, double sumaIloscMasarnia) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "65911", "Sprzedaż Masarnia", isGrupowanieChecked);
                (double sumaWartGarmaz, double sumaIloscGarmaz) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "67198", "Sprzedaż Garmaż", isGrupowanieChecked);
                (double sumaWartZwroty, double sumaIloscZwroty) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "67104", "Zakup zwrotów", isGrupowanieChecked);
                (double sumaWartZywiec, double sumaIloscZywiec) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "65882", "Zakup Żywca:", isGrupowanieChecked);

                double cenaZywcaSrednia = sumaWartZywiec / sumaIloscZywiec;
                double sumaProdukcyjna = sumaWartSprzedaz + sumaWartMrozonka + sumaWartOdpady + sumaWartKarma + sumaWartMasarnia + sumaWartGarmaz + sumaWartZwroty + sumaWartZywiec;
                double sumaProdukcyjnaIloscSprzedaz = sumaIloscSprzedaz + sumaIloscMrozonka + sumaIloscOdpady + sumaIloscKarma + sumaIloscMasarnia + sumaIloscGarmaz + sumaIloscZwroty;
                double absSumaIloscZywiec = Math.Abs(sumaIloscZywiec);
                double ProcentWydajnosci = absSumaIloscZywiec - sumaProdukcyjnaIloscSprzedaz;
 

                // Dodaj wiersz sumy na końcu
                int sumRowIndex2 = dataGridView.Rows.Add(
                    "Suma Produkcyjna",
                    string.Format("{0:N0} zł", sumaProdukcyjna),
                    string.Format("{0:N0} kg", ProcentWydajnosci),
                    string.Format("{0:N3} zł", cenaZywcaSrednia)
                //string.Format("{0:N0} zł", cenatuszki - cenaZywca)

                ); ;
                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.BackColor = Color.DarkGray;
                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.ForeColor = Color.White;
                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.Font = new Font("Calibri", 13, FontStyle.Bold);

                // Dodaj pusty wiersz
                int emptyRowIndex = dataGridView.Rows.Add("", "", "", "");
                dataGridView.Rows[emptyRowIndex].DefaultCellStyle.BackColor = Color.White;


                //Rozliczenie działu handlowego
                (double sumaWartZywegoSprzedaz, double sumaIloscZywegoSprzedaz) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "65913", "Sprzedaż Żywego", isGrupowanieChecked);
                (double sumaWartPasza, double sumaIloscPasza) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "65883", "Pasza", isGrupowanieChecked);
                (double sumaWartPisklak, double sumaIloscPisklak) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "65884", "Pisklak", isGrupowanieChecked);
                (double sumaWartIndyk, double sumaIloscIndyj) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "67096", "Indyk", isGrupowanieChecked);
                (double sumaWartTowaryHandlowe, double sumaIloscTowaryHandlowe) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "65881", "Towary Handlowe", isGrupowanieChecked);
                //Suma Handlowego działu
                double sumaHandlowa = sumaWartZywegoSprzedaz + sumaWartPasza + sumaWartPisklak + sumaWartIndyk + sumaWartTowaryHandlowe;
                sumRowIndex2 = dataGridView.Rows.Add(
                    "Towary handlowe",
                    string.Format("{0:N0} zł", sumaHandlowa)

                ); ;
                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.BackColor = Color.DarkGray;
                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.ForeColor = Color.White;
                dataGridView.Rows[sumRowIndex2].DefaultCellStyle.Font = new Font("Calibri", 13, FontStyle.Bold);

                // Dodaj pusty wiersz
                emptyRowIndex = dataGridView.Rows.Add("", "", "", "");
                dataGridView.Rows[emptyRowIndex].DefaultCellStyle.BackColor = Color.White;

                //Suma Handlowego działu
                double sumaHandlowoProdukcyjna = sumaHandlowa + sumaProdukcyjna;
                sumRowIndex2 = dataGridView.Rows.Add(
                    "Towary handlowe",
                    string.Format("{0:N0} zł", sumaHandlowoProdukcyjna)

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

               

                

                
                

                

               // double suma2 = suma + sumaWartNettoSprzedazKoszt + sumaWartNettoSprzedazPasza + sumaWartNettoSprzedazPisklak + sumaWartNettoSprzedazTowaryHandlowe;
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




                (double sumaWartNettoSprzedazMrozniaPoczatek, double sumaIloscSprzedazMrozniaPoczatek) = MrozniaMetoda(connection, ZapytanieStan, startDate, dataGridView, isGrupowanieChecked, "65552", -1, "Stan Mroźni Początkowy");
                (double sumaWartNettoSprzedazMrozniaKoniec, double sumaIloscSprzedazMrozniaKoniec) = MrozniaMetoda(connection, ZapytanieStan, endDate, dataGridView, isGrupowanieChecked, "65552", 0, "Stan Mroźni Końcowy");

                double stanNettoMrozni = sumaWartNettoSprzedazMrozniaKoniec - sumaWartNettoSprzedazMrozniaPoczatek;
                double stanIloscMrozni = sumaIloscSprzedazMrozniaKoniec - sumaIloscSprzedazMrozniaPoczatek;
                sumRowIndex2 = dataGridView.Rows.Add(
                    "Saldo:",
                    string.Format("{0:N0} zł", stanNettoMrozni),
                    string.Format("{0:N0} kg", stanIloscMrozni)


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
        private (double SumaWartoscNetto, double SumaIlosc) SumaWartosciIlosci(SqlConnection connection, string query, DateTime startDate, DateTime endDate, DataGridView dataGridView, string katalog, string Podpis, bool isGrupowanieChecked)
        {
            double SumaWartosNetto = 0;
            double SumaIlosc = 0;

            try
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);
                    command.Parameters.AddWithValue("@katalog", katalog);

                    DataTable dataTable = new DataTable();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(dataTable);
                    }

                    foreach (DataRow dataRow in dataTable.Rows)
                    {
                        string kod = dataRow["kod"].ToString();
                        double wartoscNetto = Convert.ToDouble(dataRow["SumaWartNetto"]);
                        double ilosc = Convert.ToDouble(dataRow["SumaIlosc"]);



                        if (!isGrupowanieChecked)
                        {
                            int rowIndex = dataGridView.Rows.Add(
                                kod,
                                string.Format("{0:N0} zł", wartoscNetto),
                                string.Format("{0:N0} kg", ilosc)
                            );

                            dataGridView.Rows[rowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                            dataGridView.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightBlue;
                            dataGridView.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.Black;
                            dataGridView.Rows[rowIndex].DefaultCellStyle.Font = new Font("Calibri", 11);
                        }
                        SumaWartosNetto += wartoscNetto;
                        SumaIlosc += ilosc;

                    }
                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView.Rows.Add(
                        Podpis,
                        string.Format("{0:N0} zł", SumaWartosNetto),
                        string.Format("{0:N0} kg", SumaIlosc));

                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);

                }
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
                MessageBox.Show("An error occurred: " + ex.Message);
            }

            return (SumaWartosNetto, SumaIlosc);
        }


        private (double sumaWartNettoSprzedazMroznia, double sumaIloscSprzedazMroznia) MrozniaMetoda(SqlConnection connection, string query, DateTime Date, DataGridView dataGridView, bool isGrupowanieChecked, string Magazyn, int IloscDni, string Podpis)
        {
            double sumaWarto = 0;
            double sumaIlosc = 0;

            try
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@EndDate", Date);
                    command.Parameters.AddWithValue("@Magazyn", Magazyn);
                    command.Parameters.AddWithValue("@IleDni", IloscDni);

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

                        if (!isGrupowanieChecked)
                        {
                            int rowIndex = dataGridView.Rows.Add(
                                kod,
                                string.Format("{0:N0} zł", sumaWartNetto),
                                string.Format("{0:N0} kg", sumaIloscNetto),
                                string.Format("{0:N2} zł/kg", cena)
                            );

                            dataGridView.Rows[rowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                            dataGridView.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightBlue;
                            dataGridView.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.Black;
                            dataGridView.Rows[rowIndex].DefaultCellStyle.Font = new Font("Calibri", 11);
                        }

                        sumaWarto += sumaWartNetto;
                        sumaIlosc += sumaIloscNetto;
                    }

                    int sumRowIndex = dataGridView.Rows.Add(
                        Podpis,
                        string.Format("{0:N0} zł", sumaWarto),
                        string.Format("{0:N0} kg", sumaIlosc)
                    );

                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);
                }
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
                MessageBox.Show("An error occurred: " + ex.Message);
            }

            return (sumaWarto, sumaIlosc);
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