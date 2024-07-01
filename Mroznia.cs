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
    }
}
