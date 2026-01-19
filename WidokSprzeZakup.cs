using Microsoft.Data.SqlClient;
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
        double KurczakANetto, KurczakAIlosc;

        public WidokSprzeZakup()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
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
            

            double sumaWartNettoSprzedazKoszt = 0;
            double sumaIloscSprzedazKoszt = 0;

            


            bool isGrupowanieChecked = Grupowanie.Checked;
           

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
            string ZapytanieZywy = "SELECT MZ.[kod], " +
               "SUM(MZ.[wartNetto]) AS SumaWartNetto, " +
               "SUM(MZ.[ilosc]) AS SumaIlosc " +
            "FROM [HANDEL].[HM].[MZ] MZ " +
            "INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.[idtw] = TW.[id] " +
            "INNER JOIN [HANDEL].[HM].[MG] MG ON MZ.[super] = MG.[id] " +
            "WHERE MZ.[data] BETWEEN @StartDate AND @EndDate " + // usunięto niepotrzebny apostrof
            "  AND TW.[katalog] = @katalog AND seria = 'sPZ' " +
            "GROUP BY MZ.[kod] " +
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


                (double sumaWartSprzedaz, double sumaIloscSprzedaz) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "67095", "Sprzedaż Świeże", isGrupowanieChecked);
                (double sumaWartMrozonka, double sumaIloscMrozonka) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "67153", "Sprzedaż Mrozone", isGrupowanieChecked);
                (double sumaWartOdpady, double sumaIloscOdpady) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "67094", "Sprzedaż Odpady", isGrupowanieChecked);
                (double sumaWartKarma, double sumaIloscKarma) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "65910", "Sprzedaż Karma", isGrupowanieChecked);
                (double sumaWartMasarnia, double sumaIloscMasarnia) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "65911", "Sprzedaż Masarnia", isGrupowanieChecked);
                (double sumaWartGarmaz, double sumaIloscGarmaz) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "67198", "Sprzedaż Garmaż", isGrupowanieChecked);
                (double sumaWartZwroty, double sumaIloscZwroty) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "67104", "Zakup zwrotów", isGrupowanieChecked);
                (double sumaWartZywiec, double sumaIloscZywiec) = SumaWartosciIlosci(connection, ZapytanieZywy, startDate, endDate, dataGridView, "65882", "Zakup Żywca:", isGrupowanieChecked);

                double cenaZywcaSrednia = sumaWartZywiec / sumaIloscZywiec;
                double sumaProdukcyjna = sumaWartSprzedaz + sumaWartMrozonka + sumaWartOdpady + sumaWartKarma + sumaWartMasarnia + sumaWartGarmaz + sumaWartZwroty + sumaWartZywiec;
                double sumaProdukcyjnaIloscSprzedaz = sumaIloscSprzedaz + sumaIloscMrozonka + sumaIloscOdpady + sumaIloscKarma + sumaIloscMasarnia + sumaIloscGarmaz + sumaIloscZwroty;
                double absSumaIloscZywiec = Math.Abs(sumaIloscZywiec);
                double IloscSprzedazZakup = absSumaIloscZywiec - sumaProdukcyjnaIloscSprzedaz;

                DodajWierszSumy(dataGridView, sumaProdukcyjna, IloscSprzedazZakup, sumaProdukcyjna / IloscSprzedazZakup, "Suma Produkcja", 12,"#FFFFFF", "#006400");

                // Dodaj pusty wiersz
                int emptyRowIndex = dataGridView.Rows.Add("", "", "", "");
                dataGridView.Rows[emptyRowIndex].DefaultCellStyle.BackColor = Color.White;

                DodajWierszPrzebitki(dataGridView,  KurczakANetto,  KurczakAIlosc, sumaWartZywiec, sumaIloscZywiec);



                //Rozliczenie działu handlowego
                (double sumaWartZywegoSprzedaz, double sumaIloscZywegoSprzedaz) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "65913", "Sprzedaż Żywego", isGrupowanieChecked);
                (double sumaWartPasza, double sumaIloscPasza) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "65883", "Pasza", isGrupowanieChecked);
                (double sumaWartPisklak, double sumaIloscPisklak) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "65884", "Pisklak", isGrupowanieChecked);
                (double sumaWartIndyk, double sumaIloscIndyj) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "67096", "Indyk", isGrupowanieChecked);
                (double sumaWartTowaryHandlowe, double sumaIloscTowaryHandlowe) = SumaWartosciIlosci(connection, ZapytanieHandel, startDate, endDate, dataGridView, "65881", "Towary Handlowe", isGrupowanieChecked);
                //Suma Handlowego działu
                double sumaHandlowa = sumaWartZywegoSprzedaz + sumaWartPasza + sumaWartPisklak + sumaWartIndyk + sumaWartTowaryHandlowe;
                DodajWierszSumy(dataGridView, sumaHandlowa, 0, sumaHandlowa / 0, "Handel", 12, "#FFFFFF", "#006400");

                // Dodaj pusty wiersz
                emptyRowIndex = dataGridView.Rows.Add("", "", "", "");
                dataGridView.Rows[emptyRowIndex].DefaultCellStyle.BackColor = Color.White;

                //Suma Handlowego działu
                double sumaHandlowoProdukcyjna = sumaHandlowa + sumaProdukcyjna;
                DodajWierszSumy(dataGridView, sumaHandlowoProdukcyjna, 0, sumaHandlowoProdukcyjna / 0, "Produkcja - Handel", 12, "#FFFFFF", "#006400");

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

                    DodajWierszSumy(dataGridView, sumaWartNettoSprzedazKoszt, sumaIloscSprzedazKoszt, sumaWartNettoSprzedazKoszt / sumaIloscSprzedazKoszt, "Faktury Kosztowe:", 12, "#FFFFFF", "#006400");
                }

               

                


                // Dodaj pusty wiersz
                emptyRowIndex = dataGridView.Rows.Add("", "", "", "");
                dataGridView.Rows[emptyRowIndex].DefaultCellStyle.BackColor = Color.White;



                //Stan Mroźni
                (double sumaWartNettoSprzedazMrozniaPoczatek, double sumaIloscSprzedazMrozniaPoczatek) = MrozniaMetoda(connection, ZapytanieStan, startDate, dataGridView, isGrupowanieChecked, "65552", -1, "Stan Mroźni Początkowy");
                (double sumaWartNettoSprzedazMrozniaKoniec, double sumaIloscSprzedazMrozniaKoniec) = MrozniaMetoda(connection, ZapytanieStan, endDate, dataGridView, isGrupowanieChecked, "65552", 0, "Stan Mroźni Końcowy");
                double stanNettoMrozni = sumaWartNettoSprzedazMrozniaKoniec - sumaWartNettoSprzedazMrozniaPoczatek;
                double stanIloscMrozni = sumaIloscSprzedazMrozniaKoniec - sumaIloscSprzedazMrozniaPoczatek;
                DodajWierszSumy(dataGridView, stanNettoMrozni, stanIloscMrozni, stanNettoMrozni / stanIloscMrozni, "Saldo Mroźni", 12, "#FFFFFF", "#006400");
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

                        if (kod == "Kurczak A")
                        {
                            KurczakANetto = wartoscNetto;
                            KurczakAIlosc = ilosc;
                        }

                        if (!isGrupowanieChecked)
                        {
                            double cena = wartoscNetto / ilosc;
                            int rowIndex = dataGridView.Rows.Add(
                                kod,
                                string.Format("{0:N0} zł", wartoscNetto),
                                string.Format("{0:N0} kg", ilosc),
                                string.Format("{0:N2} zł/kg", cena)
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
                    DodajWierszSumy(dataGridView, SumaWartosNetto, SumaIlosc, SumaWartosNetto / SumaIlosc, Podpis, 11, "#000000", "#D3D3D3");

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
                            dataGridView.Rows[rowIndex].DefaultCellStyle.Font = new Font("Calibri", 10);
                        }

                        sumaWarto += sumaWartNetto;
                        sumaIlosc += sumaIloscNetto;
                    }

                    DodajWierszSumy(dataGridView, sumaWarto, sumaIlosc, sumaWarto / sumaIlosc, Podpis, 11, "#000000", "#D3D3D3");
                }
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
                MessageBox.Show("An error occurred: " + ex.Message);
            }

            return (sumaWarto, sumaIlosc);
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

        private void DodajWierszSumy(DataGridView dataGridView, double Netto, double Ilosc, double Cena, string Podpis, int Wielkosc, string kolorCzcionki, string kolorTylu)
        {
            int sumRowIndex;

            if (Cena > 0 && !double.IsInfinity(Cena))
            {
                sumRowIndex = dataGridView.Rows.Add(
                    Podpis,
                    string.Format("{0:N0} zł", Netto),
                    string.Format("{0:N0} kg", Ilosc),
                    string.Format("{0:N2} zł", Cena)
                );
            }
            else
            {
                sumRowIndex = dataGridView.Rows.Add(
                    Podpis,
                    string.Format("{0:N0} zł", Netto),
                    string.Format("{0:N0} kg", Ilosc),
                    ""
                );
            }

            dataGridView.Rows[sumRowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            // Convert string colors to Color objects
            Color backColor = ColorTranslator.FromHtml(kolorTylu);
            Color foreColor = ColorTranslator.FromHtml(kolorCzcionki);


            dataGridView.Rows[sumRowIndex].DefaultCellStyle.BackColor = backColor;
            dataGridView.Rows[sumRowIndex].DefaultCellStyle.ForeColor = foreColor;
            dataGridView.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", Wielkosc, FontStyle.Bold);
        }

        private void DodajWierszPrzebitki(DataGridView dataGridView, double Netto, double Ilosc, double Nettozywy, double Ilosczywy)
        {

            // Dodaj wiersz z ceną żywca
            int rowIndex = dataGridView.Rows.Add(
                "Cena Żywca",
                string.Format("{0:N0} zł", Nettozywy),
                string.Format("{0:N0} kg", Ilosczywy),
                string.Format("{0:N2} zł/kg", Nettozywy / Ilosczywy),
                ""
            );
            dataGridView.Rows[rowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            dataGridView.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightBlue;
            dataGridView.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.Black;
            dataGridView.Rows[rowIndex].DefaultCellStyle.Font = new Font("Calibri", 11);

            // Dodaj wiersz z ceną tuszki
            rowIndex = dataGridView.Rows.Add(
                "Cena Tuszki",
                string.Format("{0:N0} zł", Netto),
                string.Format("{0:N0} kg", Ilosc),
                string.Format("{0:N2} zł/kg", Netto / Ilosc),
                ""
            );
            dataGridView.Rows[rowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            dataGridView.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightBlue;
            dataGridView.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.Black;
            dataGridView.Rows[rowIndex].DefaultCellStyle.Font = new Font("Calibri", 11);

            // Dodaj wiersz z przebitką
            rowIndex = dataGridView.Rows.Add(
                "Przebitka",
                string.Format("{0:N0} zł", Nettozywy + Netto),
                string.Format("{0:N0} kg", Ilosczywy + Ilosc),
                string.Format("{0:N2} zł/kg", (Netto / Ilosc) - (Nettozywy / Ilosczywy)),
                ""
            );
            dataGridView.Rows[rowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            dataGridView.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightBlue;
            dataGridView.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.Black;
            dataGridView.Rows[rowIndex].DefaultCellStyle.Font = new Font("Calibri", 11);
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
           
        }
    }
}