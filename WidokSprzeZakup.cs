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

        }

        private void WykonajZapytanieSQL()
        {
            DateTime startDate = dataPoczatek.Value.Date;
            DateTime endDate = dataKoniec.Value.Date;

            string connectionString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
            string query = "SELECT DP.kod, SUM(DP.wartNetto) AS SumaWartNetto, SUM(DP.ilosc) AS SumaIlosc " +
                           "FROM HANDEL.HM.DP DP " +
                           "INNER JOIN HANDEL.HM.TW TW ON DP.idtw = TW.id " +
                           "INNER JOIN HANDEL.HM.DK DK ON DP.super = DK.id " +
                           "WHERE DP.data BETWEEN @StartDate AND @EndDate AND TW.katalog = 67095 " +
                           "GROUP BY DP.kod " +
                           "ORDER BY SumaWartNetto DESC, SumaIlosc DESC;";

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

                    dataGridView1.Rows.Clear();
                    dataGridView1.Columns.Clear();

                    dataGridView1.Columns.Add("Nazwa", "Nazwa");
                    dataGridView1.Columns[0].Width = 200; // Szerokość pierwszej kolumny (Nazwa)
                    dataGridView1.Columns.Add("Netto", "Netto (zł)");
                    dataGridView1.Columns[1].Width = 150; // Szerokość drugiej kolumny (Netto)
                    dataGridView1.Columns.Add("Ilość", "Ilość (kg)");
                    dataGridView1.Columns[2].Width = 150; // Szerokość trzeciej kolumny (Ilość)
                    dataGridView1.Columns.Add("Cena", "Cena (zł/kg)");
                    dataGridView1.Columns[3].Width = 150; // Szerokość czwartej kolumny (Cena)

                    dataGridView1.AllowUserToResizeRows = true; // Umożliwienie zmiany wysokości wierszy

                    double sumaWartNettoSprzedaz = 0;
                    double sumaIloscSprzedaz = 0;

                    foreach (DataRow dataRow in dataTable.Rows)
                    {
                        string kod = dataRow["kod"].ToString();
                        double sumaWartNetto = Convert.ToDouble(dataRow["SumaWartNetto"]);
                        double sumaIlosc = Convert.ToDouble(dataRow["SumaIlosc"]);
                        double cena = sumaIlosc != 0 ? sumaWartNetto / sumaIlosc : 0;

                        int rowIndex = dataGridView1.Rows.Add(
                            kod,
                            string.Format("{0:N0} zł", sumaWartNetto),
                            string.Format("{0:N0} kg", sumaIlosc),
                            string.Format("{0:N2} zł/kg", cena)
                        );

                        dataGridView1.Rows[rowIndex].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                        // Ustaw ciemniejszy kolor tła i kolor czcionki na biały dla każdego wiersza
                        dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(0, 80, 0); // Ciemniejszy odcień zielonego
                        dataGridView1.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.White;

                        // Ustaw czcionkę dla każdego wiersza
                        dataGridView1.Rows[rowIndex].DefaultCellStyle.Font = new Font("Calibri", 11);

                        sumaWartNettoSprzedaz += sumaWartNetto;
                        sumaIloscSprzedaz += sumaIlosc;
                    }

                    // Dodaj wiersz sumy na końcu
                    int sumRowIndex = dataGridView1.Rows.Add(
                        "SUMA",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedaz),
                        string.Format("{0:N0} kg", sumaIloscSprzedaz),
                        string.Format("{0:N2} zł/kg", sumaIloscSprzedaz != 0 ? sumaWartNettoSprzedaz / sumaIloscSprzedaz : 0)
                    );

                    // Ustaw białe tło dla wiersza sumy
                    dataGridView1.Rows[sumRowIndex].DefaultCellStyle.BackColor = Color.White;

                    // Pogrubienie wiersza sumy
                    dataGridView1.Rows[sumRowIndex].DefaultCellStyle.Font = new Font("Calibri", 11, FontStyle.Bold);

                    // Dodaj kolejne wartości pod spodem
                    double sumaWartNettoSprzedazMroz = 0;
                    double sumaIloscSprzedazMroz = 0;

                    LastRow = dataGridView1.Rows.Count - 1;

                    foreach (DataRow dataRow in dataTable.Rows)
                    {
                        string kod = dataRow["kod"].ToString();
                        double sumaWartNettoMroz = Convert.ToDouble(dataRow["SumaWartNetto"]);
                        double sumaIloscMroz = Convert.ToDouble(dataRow["SumaIlosc"]);

                        dataGridView1.Rows.Add(
                            kod,
                            string.Format("{0:N0} zł", sumaWartNettoMroz),
                            string.Format("{0:N0} kg", sumaIloscMroz),
                            string.Format("{0:N2} zł/kg", sumaIloscMroz != 0 ? sumaWartNettoMroz / sumaIloscMroz : 0)
                        );

                        sumaWartNettoSprzedazMroz += sumaWartNettoMroz;
                        sumaIloscSprzedazMroz += sumaIloscMroz;
                    }

                    // Wstaw sumy na samym dole, ale w osobnym wierszu
                    dataGridView1.Rows.Add(
                        "SUMA Sprzedaż Mroź:",
                        string.Format("{0:N0} zł", sumaWartNettoSprzedazMroz),
                        string.Format("{0:N0} kg", sumaIloscSprzedazMroz),
                        string.Format("{0:N2} zł/kg", sumaIloscSprzedazMroz != 0 ? sumaWartNettoSprzedazMroz / sumaIloscSprzedazMroz : 0)
                    );
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
            WykonajZapytanieSQL();
            WykonajZapytanieSQL2();
        }


        private void dataKoniec_ValueChanged(object sender, EventArgs e)
        {
            WykonajZapytanieSQL();
            WykonajZapytanieSQL2();
        }
    }
}
