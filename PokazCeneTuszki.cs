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
    public partial class PokazCeneTuszki : Form
    {
        private string connectionString2 = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        static string connectionPermission = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        public PokazCeneTuszki()
        {
            InitializeComponent();
            // Dodanie zdarzeń TextChanged dla TextBox1 i TextBox2

            dateTimePicker1.Value = DateTime.Now; // Ustaw datę na dzisiejszą
            Load += PokazCeneTuszki_Load;

        }







        private void PokazCeneTuszki_Load(object sender, EventArgs e)
        {
            // Pobierz datę z dateTimePicker1
            DateTime selectedDate = dateTimePicker1.Value.Date;

            // Formatuj datę jako string
            string formattedDate = selectedDate.ToString("yyyy-MM-dd");

            dataGridView1.ColumnHeadersVisible = false;
            dataGridView1.RowHeadersVisible = false;
            dataGridView2.RowHeadersVisible = false;
            dataGridView3.RowHeadersVisible = false;

            // Zapytanie SQL z dynamiczną datą
            string query = $@"
    SELECT 
        'Suma' AS KontrahentNazwa,
        SUM(DP.Ilosc) AS SumaIlosci,
        ROUND(SUM(DP.[wartNetto]) / NULLIF(SUM(DP.[ilosc]), 0), 2) AS Cena
    FROM 
        [HANDEL].[HM].[DP] DP 
    INNER JOIN 
        [HANDEL].[HM].[TW] TW ON DP.[idtw] = TW.[id] 
    INNER JOIN 
        [HANDEL].[HM].[DK] DK ON DP.[super] = DK.[id] 
    INNER JOIN 
        [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
    WHERE 
        DP.[data] >= '{formattedDate}'
        AND DP.[data] < DATEADD(DAY, 1, '{formattedDate}') 
        AND DP.[kod] = 'Kurczak A' 
        AND TW.[katalog] = 67095

    UNION ALL

    SELECT
        C.Shortcut AS KontrahentNazwa,
        SUM(DP.Ilosc) AS SumaIlosci,
        ROUND(SUM(DP.[wartNetto]) / NULLIF(SUM(DP.[ilosc]), 0), 2) AS Cena
    FROM 
        [HANDEL].[HM].[DP] DP 
    INNER JOIN 
        [HANDEL].[HM].[TW] TW ON DP.[idtw] = TW.[id] 
    INNER JOIN 
        [HANDEL].[HM].[DK] DK ON DP.[super] = DK.[id] 
    INNER JOIN 
        [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
    WHERE 
        DP.[data] >= '{formattedDate}'
        AND DP.[data] < DATEADD(DAY, 1, '{formattedDate}') 
        AND DP.[kod] = 'Kurczak A' 
        AND TW.[katalog] = 67095
    GROUP BY 
        C.Shortcut, CONVERT(date, DP.[data])
    ORDER BY 
        SumaIlosci DESC, KontrahentNazwa";

            string query2 = $@"
    SELECT 
          MZ.[kod],
          ABS(SUM(CASE WHEN MG.[seria] = 'sPWU' THEN MZ.[ilosc] ELSE 0 END)) AS Przychod,
          SUM(CASE WHEN MG.[seria] = 'RWP' THEN ABS(MZ.[ilosc]) ELSE 0 END) AS Krojenie,
          ABS(SUM(CASE WHEN MG.[seria] = 'sPWU' THEN MZ.[ilosc] ELSE 0 END)) - 
          SUM(CASE WHEN MG.[seria] = 'RWP' THEN ABS(MZ.[ilosc]) ELSE 0 END) AS NaSprzedaz
      FROM [HANDEL].[HM].[MZ] MZ
      INNER JOIN [HANDEL].[HM].[MG] MG ON MZ.[super] = MG.[id] 
      WHERE MZ.[kod] IN ('Kurczak B', 'Kurczak A') 
        AND MZ.[magazyn] = '65554' 
        AND MZ.[data] = '{formattedDate}'
      GROUP BY MZ.[kod]
      ORDER BY MZ.[kod]";

            // Utwórz połączenie z bazą danych
            using (SqlConnection connection = new SqlConnection(connectionString2))
            {
                // Utwórz adapter danych i uzupełnij DataGridView
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable dataTable = new DataTable();
                adapter.Fill(dataTable);
                dataGridView1.DataSource = dataTable;

                // Dopasowanie szerokości kolumn
                dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

                // Ustawienie szerokości kolumn
                if (dataGridView1.Columns.Count > 0)
                {
                    dataGridView1.Columns[0].Width = 85; // Pierwsza kolumna
                    dataGridView1.Columns[1].Width = 50;  // Druga kolumna
                    dataGridView1.Columns[2].Width = 40;  // Trzecia kolumna

                    // Formatowanie kolumny KG z separatorem tysięcy
                    dataGridView1.Columns[1].DefaultCellStyle.Format = "N0";
                    dataGridView1.Columns[2].DefaultCellStyle.Format = "N2";
                }

                // Pogrubienie pierwszego wiersza
                if (dataGridView1.Rows.Count > 0)
                {
                    DataGridViewCellStyle boldStyle = new DataGridViewCellStyle();
                    boldStyle.Font = new Font(dataGridView1.Font, FontStyle.Bold);
                    dataGridView1.Rows[0].DefaultCellStyle = boldStyle;
                }

                // Oblicz sumę ilości i średnią cenę
                decimal totalIlosc = 0;
                decimal totalCena = 0;
                int rowCount = dataTable.Rows.Count;
                foreach (DataRow row in dataTable.Rows.Cast<DataRow>().Skip(1))
                {
                    // Sprawdź, czy wartość w kolumnie "SumaIlosci" jest pusta lub null, jeśli tak, ustaw na 0
                    decimal sumaIlosci = row["SumaIlosci"] == DBNull.Value ? 0 : Convert.ToDecimal(row["SumaIlosci"]);
                    // Sprawdź, czy wartość w kolumnie "Cena" jest pusta lub null, jeśli tak, ustaw na 0
                    decimal cena = row["Cena"] == DBNull.Value ? 0 : Convert.ToDecimal(row["Cena"]);

                    totalIlosc += sumaIlosci;
                    totalCena += cena * sumaIlosci;
                }

                decimal SumaIloscTuszkiSprzedanej = totalIlosc != 0 ? totalIlosc : 0;
                decimal averageCena = totalIlosc != 0 ? totalCena / totalIlosc : 0;

                // Wyświetl sumy i średnią cenę w odpowiednich TextBoxach
                textBox1.Text = averageCena.ToString("N2");
                textBoxSprzedanych.Text = SumaIloscTuszkiSprzedanej.ToString("N0");
            }

            // Utwórz połączenie z bazą danych
            using (SqlConnection connection = new SqlConnection(connectionString2))
            {
                // Utwórz adapter danych i uzupełnij DataGridView
                SqlDataAdapter adapter = new SqlDataAdapter(query2, connection);
                DataTable dataTable = new DataTable();
                adapter.Fill(dataTable);
                dataGridView2.DataSource = dataTable;

                // Dopasowanie szerokości kolumn
                dataGridView2.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

                // Ustawienie szerokości kolumn
                if (dataGridView2.Columns.Count > 0)
                {
                    dataGridView2.Columns[0].Width = 70; // Pierwsza kolumna
                    dataGridView2.Columns[1].Width = 55;  // Druga kolumna
                    dataGridView2.Columns[2].Width = 55;  // Trzecia kolumna
                    dataGridView2.Columns[3].Width = 70;  // Trzecia kolumna

                    // Formatowanie kolumny KG z separatorem tysięcy
                    dataGridView2.Columns[1].DefaultCellStyle.Format = "N0";
                    dataGridView2.Columns[2].DefaultCellStyle.Format = "N0";
                    dataGridView2.Columns[3].DefaultCellStyle.Format = "N0";
                }

                // Oblicz sumę ilości i średnią cenę
                decimal totalIlosc = 0;
                decimal totalKrojenia = 0;
                int rowCount = dataTable.Rows.Count;

                foreach (DataRow row in dataTable.Rows)
                {
                    // Sprawdź, czy wartość w kolumnie "NaSprzedaz" jest pusta lub null, jeśli tak, ustaw na 0
                    decimal sumaIlosci = row["NaSprzedaz"] == DBNull.Value ? 0 : Convert.ToDecimal(row["NaSprzedaz"]);
                    decimal sumakrojenia = row["Krojenie"] == DBNull.Value ? 0 : Convert.ToDecimal(row["Krojenie"]);

                    totalIlosc += sumaIlosci;
                    totalKrojenia += sumakrojenia;
                }

                decimal SumaIloscTuszkiSprzedanej = totalIlosc != 0 ? totalIlosc : 0;
                decimal SumaIloscTuszkiKrojonej = totalKrojenia != 0 ? totalKrojenia : 0;

                // Wyświetl sumy i średnią cenę w odpowiednich TextBoxach
                textBoxDoSprzedania.Text = SumaIloscTuszkiSprzedanej.ToString("N0");
                textBoxKrojenie.Text = SumaIloscTuszkiKrojonej.ToString("N0");
            }

            CalculateDifferenceAndDisplay(textBoxDoSprzedania, textBoxSprzedanych, textBoxZostalo);
            PokazCeneHarmonogramDostaw();
            CalculateAndPopulateDataGridView(textBoxKrojenie, dataGridView3, connectionString2, formattedDate);
            SetRowHeights(18, dataGridView1);
            SetRowHeights(18, dataGridView2);
            SetRowHeights(18, dataGridView3);

            if (dataGridView3.Columns.Count > 0)
            {
                dataGridView3.Columns[0].Width = 70; // Pierwsza kolumna
                dataGridView3.Columns[1].Width = 50;  // Druga kolumna
                dataGridView3.Columns[2].Width = 40;  // Druga kolumna
                dataGridView3.Columns[1].DefaultCellStyle.Format = "N0";
            }
        }

        private void PokazCeneHarmonogramDostaw()
        {
            // Pobierz datę z dateTimePicker1
            DateTime selectedDate = dateTimePicker1.Value.Date;

            // Formatuj datę jako string
            string formattedDate = selectedDate.ToString("yyyy-MM-dd");

            // Zapytanie SQL z dynamiczną datą
            string query = $@"
        SELECT
            Cena,
            SztukiDek
        FROM 
            [LibraNet].[dbo].[HarmonogramDostaw]
        WHERE 
            DataOdbioru >= '{formattedDate}'
            AND DataOdbioru < DATEADD(DAY, 1, '{formattedDate}')
            AND Bufor = 'Potwierdzony'";

            // Utwórz połączenie z bazą danych
            using (SqlConnection connection = new SqlConnection(connectionPermission))
            {
                // Utwórz adapter danych i pobierz dane do DataTable
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable dataTable = new DataTable();
                adapter.Fill(dataTable);

                // Oblicz sumę ilości i średnią cenę
                decimal totalIlosc = 0;
                decimal totalCena = 0;
                int rowCount = dataTable.Rows.Count;

                foreach (DataRow row in dataTable.Rows)
                {
                    decimal ilosc = Convert.ToDecimal(row["SztukiDek"]);
                    decimal cena = Convert.ToDecimal(row["Cena"]);

                    totalIlosc += ilosc;
                    totalCena += cena * ilosc;
                }

                decimal averageCena = totalIlosc != 0 ? totalCena / totalIlosc : 0;

                // Wyświetl średnią cenę w TextBoxie
                textBox2.Text = averageCena.ToString("N2");
            }
        }

        public class CalculationResult
        {
            public decimal TotalIlosc { get; set; }
            public double AverageCena { get; set; }
            public double TotalNetto { get; set; }
        }

        private CalculationResult CalculateAndPopulateDataGridView(TextBox inputTextBox, DataGridView dataGridView, string connectionString, string Data)
        {
            CalculationResult result = new CalculationResult();
            decimal totalValue = 0;

            try
            {
                // Pobierz wartość z TextBoxa i konwertuj ją na liczbę
                decimal inputValue = decimal.Parse(inputTextBox.Text);

                // Stwórz DataTable do przechowywania wyników
                DataTable dataTable = new DataTable();
                dataTable.Columns.Add("Nazwa", typeof(string));
                dataTable.Columns.Add("Ilosc", typeof(decimal));
                dataTable.Columns.Add("Cena", typeof(double));
                dataTable.Columns.Add("Netto", typeof(double));

                // Dodaj wiersze z obliczonymi wartościami procentowymi
                dataTable.Rows.Add("Filet A", inputValue * 0.28m);
                dataTable.Rows.Add("Filet II", inputValue * 0.02m);
                dataTable.Rows.Add("Korpus", inputValue * 0.24m);
                dataTable.Rows.Add("Ćwiartka", inputValue * 0.31m);
                dataTable.Rows.Add("Ćwiartka II", inputValue * 0.02m);
                dataTable.Rows.Add("Skrzydło I", inputValue * 0.09m);
                dataTable.Rows.Add("Skrzydło II", inputValue * 0.01m);
                dataTable.Rows.Add("Noga", inputValue * 0.01m);
                dataTable.Rows.Add("Pałka", inputValue * 0.01m);
                dataTable.Rows.Add("Odpady kat. 3", inputValue * 0.01m);

                // Oblicz całkowitą sumę wartości
                totalValue = inputValue * (0.30m + 0.24m + 0.33m + 0.10m + 0.01m + 0.01m + 0.01m);

                // Pobierz dane z bazy danych
                string query = $@"
        SELECT DP.[kod], 
               SUM(DP.[wartnetto]) / NULLIF(SUM(DP.[ilosc]), 0) AS SredniaCena
        FROM [HANDEL].[HM].[DP] DP 
        INNER JOIN [HANDEL].[HM].[TW] TW ON DP.[idtw] = TW.[id]
        INNER JOIN [HANDEL].[HM].[DK] DK ON DP.[super] = DK.[id]
        WHERE DP.[data] = '{Data}' 
          AND TW.[katalog] = 67095 
          AND DP.[kod] != 'Kurczak A'
        GROUP BY DP.[kod]
        ORDER BY SredniaCena DESC;";

                Dictionary<string, double> prices = new Dictionary<string, double>();

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    SqlCommand command = new SqlCommand(query, connection);
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            prices.Add(reader.GetString(0), reader.GetDouble(1));
                        }
                    }
                }

                double totalNetto = 0;
                decimal totalIlosc = 0;

                // Przypisz ceny i netto do odpowiednich wierszy
                foreach (DataRow row in dataTable.Rows)
                {
                    if (prices.ContainsKey(row["Nazwa"].ToString()))
                    {
                        double cena = prices[row["Nazwa"].ToString()];
                        row["Cena"] = cena;
                        double ilosc = Convert.ToDouble(row["Ilosc"]);
                        double netto = ilosc * cena;
                        row["Netto"] = netto;
                        totalNetto += netto;
                        totalIlosc += (decimal)ilosc;
                    }
                    else
                    {
                        row["Cena"] = DBNull.Value; // Puste jeśli nie znaleziono ceny
                        row["Netto"] = DBNull.Value;
                    }
                }

                // Oblicz średnią cenę
                double averageCena2 = totalNetto / (double)totalIlosc;

                // Dodaj końcowy wiersz jako sumę i średnią cenę
                dataTable.Rows.Add("Suma", totalIlosc, averageCena2, totalNetto);

                // Ustaw DataSource dla DataGridView
                dataGridView.DataSource = dataTable;

                // Formatowanie kolumny z wartościami z separatorem tysięcy
                dataGridView.Columns[1].DefaultCellStyle.Format = "N0";
                dataGridView.Columns[2].DefaultCellStyle.Format = "N2"; // Formatowanie kolumny z cenami
                dataGridView.Columns[3].DefaultCellStyle.Format = "N0"; // Formatowanie kolumny z netto

                // Pogrubienie ostatniego wiersza
                DataGridViewCellStyle boldStyle = new DataGridViewCellStyle();
                boldStyle.Font = new Font(dataGridView.Font, FontStyle.Bold);

                // Upewnij się, że DataGridView zostało uaktualnione, a potem przypisz styl
                dataGridView.Rows[dataGridView.Rows.Count - 2].DefaultCellStyle = boldStyle; // Styl dla wiersza z sumą i średnią ceną

                // Ustaw wartości zwracane
                result.TotalIlosc = totalIlosc;
                result.AverageCena = averageCena2;
                result.TotalNetto = totalNetto;



                decimal myValue;
                decimal.TryParse(textBox1.Text, out myValue);
                myValue = totalIlosc * myValue;

                decimal kosztyPracownicze = totalIlosc / (decimal)2.11;
                decimal sumaElementow = (decimal)totalNetto - (decimal)kosztyPracownicze;


                textBoxIloscElementow.Text = totalNetto.ToString("N0") + " zł";
                textBoxPracownicze.Text = kosztyPracownicze.ToString("N0") + " zł";
                textBoxSumaEle.Text = sumaElementow.ToString("N0") + " zł";
                textBoxWartTuszki.Text = myValue.ToString("N0") + " zł";
                

            }


            catch (FormatException)
            {
                MessageBox.Show("Wartość w polu tekstowym nie jest prawidłową liczbą.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wystąpił nieoczekiwany błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            CalculateDifferenceAndDisplay(textBoxSumaEle, textBoxWartTuszki, textBox4);
            return result;
        }





        private void SetRowHeights(int height, DataGridView Datagrid)
        {
            // Ustawienie wysokości wszystkich wierszy na określoną wartość
            foreach (DataGridViewRow row in Datagrid.Rows)
            {
                row.Height = height;
            }
        }

        private void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
            PokazCeneTuszki_Load(sender, null);
            PokazCeneHarmonogramDostaw();
        }
        private void CalculateDifference()
        {
            // Sprawdzenie czy obie wartości są poprawnymi liczbami całkowitymi
            if (double.TryParse(textBox1.Text, out double value1) && double.TryParse(textBox2.Text, out double value2))
            {
                // Obliczenie różnicy i wyświetlenie w TextBox3
                textBox3.Text = (value1 - value2).ToString("N2"); ;
            }
            else
            {
                // Wyświetlenie komunikatu o błędzie w przypadku nieprawidłowych danych
                textBox3.Text = "Invalid input";
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            CalculateDifference();
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            CalculateDifference();
        }
        private void CalculateDifferenceAndDisplay(TextBox textBox1, TextBox textBox2, TextBox resultTextBox)
        {
            try
            {
                // Funkcja pomocnicza do parsowania wartości z TextBox
                decimal ParseValue(string text)
                {
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return 0;
                    }

                    text = text.Replace("zł", "").Trim();
                    if (decimal.TryParse(text, out decimal value))
                    {
                        return value;
                    }

                    throw new FormatException("Nieprawidłowy format liczby.");
                }

                // Pobierz wartości z TextBoxów i konwertuj je na liczby
                decimal value1 = ParseValue(textBox1.Text);
                decimal value2 = ParseValue(textBox2.Text);

                // Oblicz różnicę
                decimal difference = value1 - value2;

                // Sformatuj wynik z separatorem tysięcy
                string formattedDifference = difference.ToString("N0");

                // Wyświetl wynik w trzecim TextBoxie
                resultTextBox.Text = formattedDifference;
            }
            catch (FormatException)
            {
               
            }
            catch (Exception ex)
            {
               
            }
        }


        private void textBoxDoSprzedania_TextChanged(object sender, EventArgs e)
        {
            CalculateDifferenceAndDisplay(textBoxDoSprzedania, textBoxSprzedanych, textBoxZostalo);
        }

        private void textBoxSprzedanych_TextChanged(object sender, EventArgs e)
        {
            CalculateDifferenceAndDisplay(textBoxDoSprzedania, textBoxSprzedanych, textBoxZostalo);
        }

        private void textBox5_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox4_TextChanged(object sender, EventArgs e)
        {

        }
    }
}