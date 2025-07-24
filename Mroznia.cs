using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms.DataVisualization.Charting;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Globalization;

namespace Kalendarz1
{
    public partial class Mroznia : Form
    {
        private string connectionString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        public Mroznia()
        {
            InitializeComponent();
        }
        private void DisplayDataInDataGridView(DateTime poczatek, DateTime koniec)
        {
            // Zapytanie SQL z warunkami dla początkowej i końcowej daty
            string query = @"
                SELECT
                    CONCAT(CONVERT(varchar, MG.[Data], 23), ' ', DATENAME(dw, MG.[Data])) AS DataPrzesuniecia,
                    ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) AS Suma
                FROM [HANDEL].[HM].[MG]
                JOIN [HANDEL].[HM].[MZ] ON MG.ID = MZ.super
                WHERE MG.magazyn = 65552
                AND (mg.seria = 'sMM+' OR mg.seria = 'sMM-' OR mg.seria = 'sMK-' OR mg.seria = 'sMK+')
                AND MG.[Data] BETWEEN @Poczatek AND @Koniec
                GROUP BY MG.[Data]
                ORDER BY MG.[Data] DESC";

            // Utworzenie połączenia z bazą danych
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Utworzenie adaptera danych
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);

                // Dodanie parametrów dla początkowej i końcowej daty
                adapter.SelectCommand.Parameters.AddWithValue("@Poczatek", poczatek);
                adapter.SelectCommand.Parameters.AddWithValue("@Koniec", koniec);

                // Utworzenie tabeli danych
                DataTable table = new DataTable();

                // Wypełnienie tabeli danymi z adaptera
                adapter.Fill(table);

                // Ustawienie źródła danych dla DataGridView
                dataGridView1.DataSource = table;

                // Automatyczne dopasowanie szerokości kolumn do zawartości
                dataGridView1.AutoResizeColumns();

                // Ustawienie formatowania kolumny "Suma" po utworzeniu źródła danych dla DataGridView
                dataGridView1.Columns["Suma"].DefaultCellStyle.Format = "#,0";
            }
        }






        private void buttonPokaz_Click(object sender, EventArgs e)
        {
            // Pobierz wartości dat z DataPicker'ów
            DateTime poczatek = dataPoczatek.Value.Date;
            DateTime koniec = dataKoniec.Value.Date;

            // Wyświetl dane w DataGridView z uwzględnieniem warunków dla początkowej i końcowej daty
            DisplayDataInDataGridView(poczatek, koniec);
            DisplayDataInDataGridView2(poczatek, koniec);
        }

        private void DisplayDataInDataGridView2(DateTime dateFrom, DateTime dateTo)
        {
            string connectionString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

            string query = @"
    WITH 
    ScalonaIlosc AS (
        SELECT
            CASE 
                WHEN MZ.kod LIKE 'Kurczak A%' THEN 'Kurczak A'
                WHEN MZ.kod LIKE 'Korpus%' THEN 'Korpus'
                WHEN MZ.kod LIKE 'Ćwiartka%' THEN 'Ćwiartka'
                WHEN MZ.kod LIKE 'Filet II%' THEN 'Filet II'
                WHEN MZ.kod LIKE 'Filet %' THEN 'Filet A'
                WHEN MZ.kod LIKE 'Skrzydło I%' THEN 'Skrzydło I'
                WHEN MZ.kod LIKE 'Trybowane bez skóry%' THEN 'Trybowane bez skóry'
                WHEN MZ.kod LIKE 'Trybowane ze skórą%' THEN 'Trybowane ze skórą'
                ELSE MZ.kod
            END AS ScalonyKod,
            ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) AS Ilosc
        FROM [HANDEL].[HM].[MG]
        JOIN [HANDEL].[HM].[MZ] ON MG.ID = MZ.super
        WHERE MG.magazyn = 65552
          AND MG.seria IN ('sMM+', 'sMM-', 'sMK-', 'sMK+')
          AND MG.[Data] BETWEEN @DateFrom AND @DateTo
        GROUP BY 
            CASE 
                WHEN MZ.kod LIKE 'Kurczak A%' THEN 'Kurczak A'
                WHEN MZ.kod LIKE 'Korpus%' THEN 'Korpus'
                WHEN MZ.kod LIKE 'Ćwiartka%' THEN 'Ćwiartka'
                WHEN MZ.kod LIKE 'Filet II%' THEN 'Filet II'
                WHEN MZ.kod LIKE 'Filet %' THEN 'Filet A'
                WHEN MZ.kod LIKE 'Skrzydło I%' THEN 'Skrzydło I'
                WHEN MZ.kod LIKE 'Trybowane bez skóry%' THEN 'Trybowane bez skóry'
                WHEN MZ.kod LIKE 'Trybowane ze skórą%' THEN 'Trybowane ze skórą'
                ELSE MZ.kod
            END
    ),
    CenyTowarow AS (
        SELECT 
            DP.kod AS KodTowaru,
            ROUND(SUM(DP.wartnetto) / NULLIF(SUM(DP.ilosc), 0), 2) AS SredniaCena
        FROM [HANDEL].[HM].[DP] DP 
        INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.id
        INNER JOIN [HANDEL].[HM].[DK] DK ON DP.super = DK.id
        WHERE DP.data >= @DateFrom 
          AND DP.data < DATEADD(DAY, 1, @DateTo)
          AND TW.katalog = 67095
        GROUP BY DP.kod
    )
    SELECT 
        SI.ScalonyKod,
        SI.Ilosc,
        ROUND(COALESCE(CT.SredniaCena, 0), 2) AS Cena,
        ROUND(SI.Ilosc * COALESCE(CT.SredniaCena, 0), 2) AS Wartosc,
        ROUND(SI.Ilosc * COALESCE(CT.SredniaCena, 0) * 0.82, 2) AS [18%],
        ROUND((SI.Ilosc * COALESCE(CT.SredniaCena, 0) * 0.82) - (SI.Ilosc * COALESCE(CT.SredniaCena, 0)), 2) AS Strata
    FROM ScalonaIlosc SI
    LEFT JOIN CenyTowarow CT ON SI.ScalonyKod = CT.KodTowaru
    ORDER BY Wartosc DESC;
    ";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DateFrom", dateFrom);
                        cmd.Parameters.AddWithValue("@DateTo", dateTo);

                        SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                        DataTable dataTable = new DataTable();
                        adapter.Fill(dataTable);

                        // Usuń wiersze z Wartosc = 0
                        for (int i = dataTable.Rows.Count - 1; i >= 0; i--)
                        {
                            if (decimal.TryParse(dataTable.Rows[i]["Wartosc"].ToString(), out decimal val) && val == 0)
                            {
                                dataTable.Rows.RemoveAt(i);
                            }
                        }

                        // Oblicz sumy
                        decimal sumaIlosc = Convert.ToDecimal(dataTable.Compute("SUM(Ilosc)", string.Empty));
                        decimal sumaWartosc = Convert.ToDecimal(dataTable.Compute("SUM(Wartosc)", string.Empty));
                        decimal suma18 = Convert.ToDecimal(dataTable.Compute("SUM([18%])", string.Empty));
                        decimal sumaStrata = Convert.ToDecimal(dataTable.Compute("SUM(Strata)", string.Empty));
                        decimal sumaCena = sumaIlosc != 0 ? sumaWartosc / sumaIlosc : 0;

                        // Dodaj SUMA jako pierwszy wiersz
                        DataRow summaryRow = dataTable.NewRow();
                        summaryRow["ScalonyKod"] = "SUMA";
                        summaryRow["Ilosc"] = sumaIlosc;
                        summaryRow["Cena"] = sumaCena;
                        summaryRow["Wartosc"] = sumaWartosc;
                        summaryRow["18%"] = suma18;
                        summaryRow["Strata"] = sumaStrata;
                        dataTable.Rows.InsertAt(summaryRow, 0);

                        // Tworzymy tabelę do wyświetlania
                        DataTable formattedTable = new DataTable();
                        formattedTable.Columns.Add("ScalonyKod");
                        formattedTable.Columns.Add("Ilosc");
                        formattedTable.Columns.Add("Cena");
                        formattedTable.Columns.Add("Wartosc");
                        formattedTable.Columns.Add("18%");
                        formattedTable.Columns.Add("Strata");

                        foreach (DataRow row in dataTable.Rows)
                        {
                            string kod = row["ScalonyKod"].ToString();

                            decimal.TryParse(row["Ilosc"]?.ToString(), out decimal ilosc);
                            decimal.TryParse(row["Cena"]?.ToString(), out decimal cena);
                            decimal.TryParse(row["Wartosc"]?.ToString(), out decimal wartosc);
                            decimal.TryParse(row["18%"]?.ToString(), out decimal osiemnascie);
                            decimal.TryParse(row["Strata"]?.ToString(), out decimal strata);

                            formattedTable.Rows.Add(
                                kod,
                                $"{ilosc:N0} kg",
                                $"{cena:N2} zł",
                                $"{wartosc:N2} zł",
                                $"{osiemnascie:N2} zł",
                                $"{strata:N2} zł"
                            );
                        }

                        dataGridView2.DataSource = formattedTable;

                        // Styl siatki
                        dataGridView2.RowHeadersVisible = false;
                        dataGridView2.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                        dataGridView2.DefaultCellStyle.Font = new Font("Arial", 9);
                        dataGridView2.RowTemplate.Height = 16;

                        // Formatowanie wyglądu komórek
                        foreach (DataGridViewRow row in dataGridView2.Rows)
                        {
                            string scalonyKod = row.Cells["ScalonyKod"]?.Value?.ToString();

                            if (scalonyKod == "SUMA")
                            {
                                row.DefaultCellStyle.Font = new Font("Arial", 9, FontStyle.Bold);

                                // Sprawdź wartość Strata w wierszu SUMA i zaznacz na czerwono jeśli ujemna
                                if (row.Cells["Strata"]?.Value != null)
                                {
                                    string raw = row.Cells["Strata"].Value.ToString().Replace(" zł", "").Replace(" ", "").Replace(",", ".");
                                    row.Cells["Strata"].Style.ForeColor = Color.Red;
                                    if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal strataSuma) && strataSuma < 0)
                                    {
                                        row.Cells["Strata"].Style.ForeColor = Color.Red;
                                    }
                                }
                            }
                        }

                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ShowChartButton_Click(object sender, EventArgs e)
        {
            ShowChartFromSelectedRows();
        }
        private void ShowChartFromSelectedRows()
        {
            // Utwórz wykres
            Chart chart = new Chart();
            chart.Size = new System.Drawing.Size(1200, 1000);

            // Utwórz obszar wykresu
            ChartArea chartArea = new ChartArea();
            chart.ChartAreas.Add(chartArea);

            // Utwórz serię danych
            Series series = new Series();
            series.ChartType = SeriesChartType.Column; // Zmiana typu na słupkowy
            series.XValueType = ChartValueType.Date; // Zmiana typu danych na datę

            // Iteruj przez zaznaczone wiersze
            foreach (DataGridViewRow selectedRow in dataGridView1.SelectedRows)
            {
                // Pobierz wartości kolumn
                string dataString = selectedRow.Cells["DataPrzesuniecia"].Value != null
    ? selectedRow.Cells["DataPrzesuniecia"].Value.ToString()
    : "0";
                string sumaString = selectedRow.Cells["Suma"].Value != null
? selectedRow.Cells["Suma"].Value.ToString()
: "0";


                // Sprawdź, czy wartości nie są puste
                if (!string.IsNullOrEmpty(dataString) && !string.IsNullOrEmpty(sumaString))
                {
                    // Konwertuj string do DateTime
                    if (DateTime.TryParse(dataString, out DateTime data))
                    {
                        // Konwertuj string do double
                        if (double.TryParse(sumaString, out double sumaIlosciDodatnichDouble))
                        {
                            decimal sumaIlosciDodatnich = Convert.ToDecimal(sumaIlosciDodatnichDouble);

                            // Dodaj punkt danych do serii wykresu
                            series.Points.AddXY(data, sumaIlosciDodatnich);
                        }
                        else
                        {
                            // Obsłuż błąd konwersji sumy
                            MessageBox.Show("Błąd konwersji sumy.");
                        }
                    }
                    else
                    {
                        // Obsłuż błąd konwersji daty
                        MessageBox.Show("Błąd konwersji daty.");
                    }
                }
                else
                {
                    // Obsłuż puste wartości
                    MessageBox.Show("Brak wartości dla daty lub sumy.");
                }
            }


            // Dodaj serię do wykresu
            chart.Series.Add(series);

            // Ustaw format osi Y
            chartArea.AxisY.LabelStyle.Format = "#,##0"; // Format separatora tysięcy

            // Ustawienie przesunięcia etykiet dat na osi X
            chartArea.AxisX.LabelStyle.IntervalType = DateTimeIntervalType.Days;
            chartArea.AxisX.LabelStyle.Interval = 1;

            // Ustawienia dla osi X
            chartArea.AxisX.IntervalType = DateTimeIntervalType.Days;
            chartArea.AxisX.Interval = 1;
            chartArea.AxisX.LabelStyle.Format = "MM-dd-yyyy"; // Format daty

            // Pokaż wykres
            Form form = new Form();
            form.Controls.Add(chart);
            form.ShowDialog();
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }
    }
}
