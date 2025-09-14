using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
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
        double uzyskanyWynikOplacalnosci;






        private void PokazCeneTuszki_Load(object sender, EventArgs e)
        {
            // Pobierz datę z dateTimePicker1

            decimal averageCena;
            // Formatuj datę jako string

            DateTime selectedDate = dateTimePicker1.Value.Date;
            DateTime selectedDat2 = dateTimePicker2.Value.Date;
            string formattedDate = selectedDate.ToString("yyyy-MM-dd");
            string formattedDate2 = selectedDat2.ToString("yyyy-MM-dd");
            dataGridView1.ColumnHeadersVisible = false;
            dataGridView1.RowHeadersVisible = false;
            dataGridView2.RowHeadersVisible = false;
            dataGridView3.RowHeadersVisible = false;
            dataGridViewPrzewidywalnyElement.RowHeadersVisible = false;
            dataGridViewPrzychodElementow.RowHeadersVisible = false;
            dataGridViewPrzewidywalnyTusz.RowHeadersVisible = false;
            PokazPrzychodElementow();
            PokazPrzewidywalneKilogramy();
            WyswietlDaneZSumami();


            // Zakładam, że zmienne formattedDate i formattedDate2 są już zdefiniowane
            string query = $@"
-- Wiersz sumaryczny na początku
SELECT 
    'Suma' AS KontrahentNazwa,
    SUM(DP.Ilosc) AS SumaIlosci,
    ROUND(SUM(DP.[wartNetto]) / NULLIF(SUM(DP.[ilosc]), 0), 2) AS Cena,
    '' AS Handlowiec
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
    AND DP.[data] < DATEADD(DAY, 1, '{formattedDate2}') 
    AND DP.[kod] = 'Kurczak A' 
    AND TW.[katalog] = 67095

UNION ALL

-- Grupa dla każdego kontrahenta
SELECT
    C.Shortcut AS KontrahentNazwa,
    SUM(DP.Ilosc) AS SumaIlosci,
    ROUND(SUM(DP.[wartNetto]) / NULLIF(SUM(DP.[ilosc]), 0), 2) AS Cena,
    ISNULL(WYM.CDim_Handlowiec_Val,'-') AS Handlowiec
FROM 
    [HANDEL].[HM].[DP] DP 
INNER JOIN 
    [HANDEL].[HM].[TW] TW ON DP.[idtw] = TW.[id] 
INNER JOIN 
    [HANDEL].[HM].[DK] DK ON DP.[super] = DK.[id] 
INNER JOIN 
    [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
LEFT JOIN  [HANDEL].[SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
WHERE 
    DP.[data] >= '{formattedDate}' 
    AND DP.[data] < DATEADD(DAY, 1, '{formattedDate2}') 
    AND DP.[kod] = 'Kurczak A' 
    AND TW.[katalog] = 67095
GROUP BY 
    C.Shortcut, WYM.CDim_Handlowiec_Val
HAVING
    SUM(DP.Ilosc) > 0
ORDER BY 
    SumaIlosci DESC, KontrahentNazwa;
";




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
                    dataGridView1.Columns[0].Width = 90; // Pierwsza kolumna
                    dataGridView1.Columns[1].Width = 65;  // Druga kolumna
                    dataGridView1.Columns[2].Width = 45;  // Trzecia kolumna
                    dataGridView1.Columns[3].Width = 50;  // Trzecia kolumna

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
                    decimal sumaIlosci = row["SumaIlosci"] == DBNull.Value ? 0 : Convert.ToDecimal(row["SumaIlosci"]);
                    decimal cena = row["Cena"] == DBNull.Value ? 0 : Convert.ToDecimal(row["Cena"]);

                    totalIlosc += sumaIlosci;
                    totalCena += cena * sumaIlosci;
                }

                decimal SumaIloscTuszkiSprzedanej = totalIlosc != 0 ? totalIlosc : 0;
                averageCena = totalIlosc != 0 ? totalCena / totalIlosc : 0;

                // Wyświetl sumy i średnią cenę w odpowiednich TextBoxach
                textBox1.Text = averageCena.ToString("N2");
                textBoxSprzedanych.Text = SumaIloscTuszkiSprzedanej.ToString("N0");

                // Dodanie pustego wiersza jako separatora
                DataRow emptyRow = dataTable.NewRow();
                dataTable.Rows.Add(emptyRow);

                // Zdublowanie pierwszego wiersza na końcu tabeli
                if (dataTable.Rows.Count > 0)
                {
                    DataRow firstRow = dataTable.Rows[0];
                    DataRow duplicatedRow = dataTable.NewRow();
                    duplicatedRow.ItemArray = firstRow.ItemArray.Clone() as object[];
                    dataTable.Rows.Add(duplicatedRow);

                    // Wartość do użycia w przypadku Przychodu Tuszki A
                    decimal przychodTuszkiA = przychodKurczakA != 0 ? przychodKurczakA : (decimal)sumTonazTuszkiA;

                    // Dodanie wiersza z wartością przychodTuszkiA
                    DataRow tuszkaARow = dataTable.NewRow();
                    tuszkaARow["KontrahentNazwa"] = "Przychód";
                    tuszkaARow["SumaIlosci"] = przychodTuszkiA;
                    dataTable.Rows.Add(tuszkaARow);

                    // Dodanie wiersza różnicy między SumaIloscTuszkiSprzedanej a przychodTuszkiA
                    DataRow differenceRow = dataTable.NewRow();
                    differenceRow["KontrahentNazwa"] = "Do Sprzedania";
                    differenceRow["SumaIlosci"] = przychodTuszkiA - SumaIloscTuszkiSprzedanej;
                    dataTable.Rows.Add(differenceRow);

                    // Stylizacja wierszy sumujących
                    DataGridViewCellStyle summaryStyle = new DataGridViewCellStyle();
                    summaryStyle.Font = new Font(dataGridView1.Font, FontStyle.Regular);
                    summaryStyle.BackColor = Color.LightGray;  // Kolor tła
                    summaryStyle.ForeColor = Color.Black;   // Kolor tekstu

                    // Stylizacja wierszy sumujących
                    DataGridViewCellStyle summaryStyle2 = new DataGridViewCellStyle();
                    summaryStyle2.Font = new Font(dataGridView1.Font, FontStyle.Bold);
                    summaryStyle2.BackColor = Color.Yellow;  // Kolor tła
                    summaryStyle2.ForeColor = Color.Red;   // Kolor tekstu

                    int lastRowIndex = dataGridView1.Rows.Count - 1;

                    dataGridView1.Rows[lastRowIndex - 3].DefaultCellStyle = summaryStyle; // Przychód Tuszki A
                    dataGridView1.Rows[lastRowIndex - 2].DefaultCellStyle = summaryStyle; // Duplikat pierwszego wiersza
                    dataGridView1.Rows[lastRowIndex - 1].DefaultCellStyle = summaryStyle2; // Duplikat pierwszego wiersza


                }
            }

            CalculateDifferenceAndDisplay(textBoxDoSprzedania, textBoxSprzedanych, textBoxZostalo);

            PokazCeneHarmonogramDostaw();

            CalculateAndPopulateDataGridView(textBoxKrojenie, dataGridView3, connectionString2, averageCena);
            SetRowHeights(16, dataGridView1);
            SetRowHeights(16, dataGridView2);
            SetRowHeights(16, dataGridView3);

            if (dataGridView3.Columns.Count > 0)
            {
                dataGridView3.Columns[0].Width = 70; // Pierwsza kolumna
                dataGridView3.Columns[1].Width = 50;  // Druga kolumna
                dataGridView3.Columns[2].Width = 40;  // Druga kolumna
                dataGridView3.Columns[1].DefaultCellStyle.Format = "N0";
            }
        }




        private decimal przychodKurczakA;
        public void WyswietlDaneZSumami()
        {
            DateTime selectedDate = dateTimePicker1.Value.Date;
            string formattedDate = selectedDate.ToString("yyyy-MM-dd");
            DateTime selectedDat2 = dateTimePicker2.Value.Date;
            // Formatuj datę jako string
            string formattedDate2 = selectedDat2.ToString("yyyy-MM-dd");


            // Odczytanie stanu checkboxa pokaztuszke
            bool isCheckboxChecked = pokaztuszke.Checked;

            // Zakładam, że zmienne formattedDate i formattedDate2 są już zdefiniowane
            string query2;

            query2 = $@"
    SELECT 
        MZ.[kod],
        ABS(SUM(CASE WHEN MG.[seria] = 'sPWU' THEN MZ.[ilosc] ELSE 0 END)) AS Przychod,
        SUM(CASE WHEN MG.[seria] = 'RWP' THEN ABS(MZ.[ilosc]) ELSE 0 END) AS Krojenie
    FROM [HANDEL].[HM].[MZ] MZ
    INNER JOIN [HANDEL].[HM].[MG] MG ON MZ.[super] = MG.[id] 
    WHERE MZ.[kod] IN ('Kurczak B', 'Kurczak A') 
      AND MZ.[magazyn] = '65554' 
      AND MZ.[data] >= '{formattedDate}'
      AND MZ.[data] < DATEADD(DAY, 1, '{formattedDate2}')
    GROUP BY MZ.[kod]
    ORDER BY MZ.[kod]";




            using (SqlConnection connection = new SqlConnection(connectionString2))
            {
                SqlDataAdapter adapter = new SqlDataAdapter(query2, connection);
                DataTable dataTable = new DataTable();
                adapter.Fill(dataTable);

                dataTable.Columns.Add("Procent", typeof(string));

                decimal totalPrzychod = 0;
                decimal totalKrojenie = 0;

                foreach (DataRow row in dataTable.Rows)
                {
                    decimal przychod = row["Przychod"] == DBNull.Value ? 0 : Convert.ToDecimal(row["Przychod"]);
                    decimal krojenie = row["Krojenie"] == DBNull.Value ? 0 : Convert.ToDecimal(row["Krojenie"]);

                    if (row["kod"].ToString() == "Kurczak A")
                    {
                        przychodKurczakA = przychod;  // Przypisanie wartości do zmiennej poza metodą
                    }

                    totalPrzychod += przychod;
                    totalKrojenie += krojenie;
                }

                foreach (DataRow row in dataTable.Rows)
                {
                    decimal przychod = row["Przychod"] == DBNull.Value ? 0 : Convert.ToDecimal(row["Przychod"]);
                    decimal procent = totalPrzychod > 0 ? (przychod / totalPrzychod) * 100 : 0;
                    row["Procent"] = $"{procent:N2} %";
                }

                DataRow sumRow = dataTable.NewRow();
                sumRow["kod"] = "Suma:";
                sumRow["Przychod"] = totalPrzychod;
                sumRow["Krojenie"] = totalKrojenie;
                sumRow["Procent"] = "100 %";
                dataTable.Rows.InsertAt(sumRow, 0);

                dataGridView2.DataSource = dataTable;
                dataGridView2.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

                if (dataGridView2.Columns.Count > 0)
                {
                    dataGridView2.Columns[0].Width = 70; // Pierwsza kolumna
                    dataGridView2.Columns[1].Width = 55; // Druga kolumna
                    dataGridView2.Columns[2].Width = 55; // Trzecia kolumna
                    dataGridView2.Columns[3].Width = 70; // Czwarta kolumna (Procent)

                    dataGridView2.Columns[1].DefaultCellStyle.Format = "N0";
                    dataGridView2.Columns[2].DefaultCellStyle.Format = "N0";
                }

                FormatSumRow(dataGridView2);

                textBoxDoSprzedania.Text = totalPrzychod.ToString("N0");
                textBoxKrojenie.Text = totalKrojenie.ToString("N0");
            }
            SetRowHeights(18, dataGridView2);
        }






        private async void buttonWyslij_Click(object sender, EventArgs e)
        {
            buttonWyslij.Enabled = false;
            textBoxOdpowiedz.Text = "Myślę...";

            try
            {
                // 1) Klucz z ENV (User/Machine)
                var key =
                    Environment.GetEnvironmentVariable("OPENAI_API_KEY") ??
                    Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.User) ??
                    Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.Machine);

                if (string.IsNullOrWhiteSpace(key))
                    throw new InvalidOperationException("Brak klucza API w OPENAI_API_KEY.");

                // 2) Klient
                var client = new OpenAiClient(key);

                // 3) Pytanie → odpowiedź
                var prompt = textBoxPytanie.Text?.Trim();
                if (string.IsNullOrWhiteSpace(prompt))
                    throw new InvalidOperationException("Wpisz pytanie.");

                var answer = await client.AskAsync(prompt, "gpt-5");
                textBoxOdpowiedz.Text = answer;
            }
            catch (Exception ex)
            {
                textBoxOdpowiedz.Text = "Błąd: " + ex.Message;
            }
            finally
            {
                buttonWyslij.Enabled = true;
            }

        }

        private void PokazCeneHarmonogramDostaw()
        {
            DateTime selectedDate = dateTimePicker1.Value.Date;
            string formattedDate = selectedDate.ToString("yyyy-MM-dd");
            DateTime selectedDat2 = dateTimePicker2.Value.Date;
            // Formatuj datę jako string
            string formattedDate2 = selectedDat2.ToString("yyyy-MM-dd");
            // Zapytanie SQL z dynamiczną datą
            string query = $@"
            SELECT
                Cena,
                SztukiDek
            FROM 
                [LibraNet].[dbo].[HarmonogramDostaw]
            WHERE 
                DataOdbioru >= '{formattedDate}'
                AND DataOdbioru < DATEADD(DAY, 1, '{formattedDate2}')
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
                    decimal ilosc = row["SztukiDek"] != DBNull.Value ? Convert.ToDecimal(row["SztukiDek"]) : 0;
                    decimal cena = row["Cena"] != DBNull.Value ? Convert.ToDecimal(row["Cena"]) : 0;

                    totalIlosc += ilosc;
                    totalCena += cena * ilosc;
                }


                decimal averageCena = totalIlosc != 0 ? totalCena / totalIlosc : 0;
                textBox2.Text = averageCena.ToString("N2");
                textBox22.Text = averageCena.ToString("N2");
                uzyskanyWynikOplacalnosci = AktualizacjaOplacalnosci();
            }
        }
        private double AktualizacjaOplacalnosci()
        {
            double mnoznik = 1.25;
            double licznik = 1.00;

            // Wyświetl wartości mnożnika i licznika w odpowiednich TextBoxach
            textBox222.Text = licznik.ToString("N2");
            textBox111.Text = mnoznik.ToString("N2");

            double wynik = 0;

            // Zakładam, że textBox2 zawiera wartość liczbową, którą chcemy pomnożyć przez mnoznik
            if (double.TryParse(textBox2.Text, out double textBox2Value))
            {
                // Obliczenie wartości: textBox2Value * mnoznik + licznik
                wynik = (textBox2Value * mnoznik) + licznik;

                // Wyświetlenie wyniku w textBox11
                textBox11.Text = wynik.ToString("N2");
            }
            else
            {

            }

            // Zwrócenie wyniku z metody
            return wynik;
        }




        public class CalculationResult
        {
            public decimal TotalIlosc { get; set; }
            public double AverageCena { get; set; }
            public double TotalNetto { get; set; }
        }

        private CalculationResult CalculateAndPopulateDataGridView(TextBox inputTextBox, DataGridView dataGridView, string connectionString, decimal averageprice)
        {
            CalculationResult result = new CalculationResult();
            decimal totalValue = 0;
            DateTime selectedDate = dateTimePicker1.Value.Date;
            string formattedDate = selectedDate.ToString("yyyy-MM-dd");
            DateTime selectedDat2 = dateTimePicker2.Value.Date;
            // Formatuj datę jako string
            string formattedDate2 = selectedDat2.ToString("yyyy-MM-dd");
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
                var items = new (string Nazwa, decimal Procent)[]
                {
            ("Filet A", 0.28m),
            ("Filet II", 0.02m),
            ("Korpus", 0.24m),
            ("Ćwiartka", 0.31m),
            ("Ćwiartka II", 0.02m),
            ("Skrzydło I", 0.09m),
            ("Skrzydło II", 0.01m),
            ("Noga", 0.01m),
            ("Pałka", 0.01m),
            ("Odpady kat. 3", 0.01m)
                };

                foreach (var item in items)
                {
                    dataTable.Rows.Add(item.Nazwa, inputValue * item.Procent);
                }

                // Pobierz dane z bazy danych
                string query = $@"
    SELECT DP.[kod], 
           SUM(DP.[wartnetto]) / NULLIF(SUM(DP.[ilosc]), 0) AS SredniaCena
    FROM [HANDEL].[HM].[DP] DP 
    INNER JOIN [HANDEL].[HM].[TW] TW ON DP.[idtw] = TW.[id]
    INNER JOIN [HANDEL].[HM].[DK] DK ON DP.[super] = DK.[id]
    WHERE DP.[data] >= '{formattedDate}' 
      AND DP.[data] < DATEADD(DAY, 1, '{formattedDate2}')
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
                    string nazwa = row["Nazwa"].ToString();
                    decimal ilosc = Convert.ToDecimal(row["Ilosc"]);

                    // Zawsze sumuj ilość, niezależnie od tego, czy cena została znaleziona
                    totalIlosc += ilosc;

                    if (prices.ContainsKey(nazwa))
                    {
                        double cena = prices[nazwa];
                        row["Cena"] = cena;
                        double netto = (double)ilosc * cena;
                        row["Netto"] = netto;
                        totalNetto += netto;
                    }
                    else
                    {
                        double cena = 0;
                        bool cenaZnaleziona = false;

                        // Oblicz cenę na podstawie innych produktów, jeśli to możliwe
                        if (nazwa == "Filet II" && prices.ContainsKey("Filet A"))
                        {
                            cena = prices["Filet A"] * 0.62; // 62% ceny Filet A
                            cenaZnaleziona = true;
                        }
                        else if (nazwa == "Skrzydło II" && prices.ContainsKey("Skrzydło I"))
                        {
                            cena = prices["Skrzydło I"] * 0.58; // 58% ceny Skrzydło I
                            cenaZnaleziona = true;
                        }
                        else if (nazwa == "Ćwiartka II" && prices.ContainsKey("Ćwiartka"))
                        {
                            cena = prices["Ćwiartka"] * 0.83; // 83% ceny Ćwiartka
                            cenaZnaleziona = true;
                        }

                        if (cenaZnaleziona)
                        {
                            row["Cena"] = cena;
                            double netto = (double)ilosc * cena;
                            row["Netto"] = netto;
                            totalNetto += netto;
                        }
                        else
                        {
                            row["Cena"] = DBNull.Value; // Puste jeśli nie znaleziono ceny
                            row["Netto"] = DBNull.Value;
                        }
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
                dataGridView.Rows[dataGridView.Rows.Count - 1].DefaultCellStyle = boldStyle; // Styl dla wiersza z sumą i średnią ceną

                // Ustaw wartości zwracane
                result.TotalIlosc = totalIlosc;
                result.AverageCena = averageCena2;
                result.TotalNetto = totalNetto;
                // Obliczenia dodatkowe
                string inputText = inputTextBox.Text.Replace(" ", ""); // Usuń spacje
                decimal myValue;

                // Sprawdź, czy parsowanie się powiodło
                if (!decimal.TryParse(inputText, out myValue))
                {
                    MessageBox.Show("Wartość w polu tekstowym nie jest prawidłową liczbą. Ustawiono wartość domyślną na 0.", "Ostrzeżenie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    myValue = 0; // Ustaw wartość domyślną, jeśli parsowanie się nie powiedzie
                }

                // Mnożenie myValue przez totalIlosc
                myValue = totalIlosc * averageprice;

                decimal kosztyPracownicze = totalIlosc / 2.11m;
                double sumaElementow = totalNetto - (double)kosztyPracownicze;

                // Kontynuacja operacji, np. wyświetlanie wartości w TextBoxach
                textBoxIloscElementow.Text = totalIlosc.ToString("N0") + " kg";
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

        private void label14_Click(object sender, EventArgs e)
        {

        }

        private void label16_Click(object sender, EventArgs e)
        {

        }
        private double sumTonazTuszkiA;

        private void PokazPrzewidywalneKilogramy()
        {
            // Tworzenie dwóch tabel dla różnych DataGridView
            DataTable finalTableTusz = new DataTable();
            DataTable finalTableElement = new DataTable();

            // Tworzenie połączenia z bazą danych i pobieranie danych
            using (SqlConnection connection = new SqlConnection(connectionPermission))
            {
                // Tworzenie komendy SQL
                string query = @"
                SELECT LP, Auta, Dostawca, WagaDek, SztukiDek 
                FROM dbo.HarmonogramDostaw 
                WHERE DataOdbioru >= @StartDate 
                  AND DataOdbioru <= @EndDate 
                  AND Bufor = 'Potwierdzony' 
                ORDER BY WagaDek DESC";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@StartDate", dateTimePicker1.Value.Date);
                command.Parameters.AddWithValue("@EndDate", dateTimePicker2.Value.Date);

                // Tworzenie adaptera danych i wypełnianie DataTable
                SqlDataAdapter adapter = new SqlDataAdapter(command);
                DataTable table = new DataTable();
                adapter.Fill(table);

                // Dodanie odpowiednich kolumn do obu tabel
                finalTableTusz.Columns.Add("Towar", typeof(string)); // Kolumna dla towarów (Tuszka A, Tuszka B)
                finalTableTusz.Columns.Add("Suma", typeof(string));  // Kolumna dla sum
                finalTableTusz.Columns.Add("Procent", typeof(string)); // Kolumna dla procentów

                finalTableElement.Columns.Add("Towar", typeof(string)); // Kolumna dla towarów
                finalTableElement.Columns.Add("Suma", typeof(string));  // Kolumna dla sum
                finalTableElement.Columns.Add("Procent", typeof(string)); // Kolumna dla procentów

                // Inicjalizacja zmiennych sum
                sumTonazTuszkiA = 0; // Zmienna przeniesiona na poziom klasy
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
                DataRow rowTuszkaA = finalTableTusz.NewRow();
                rowTuszkaA["Towar"] = "Tuszka A";
                rowTuszkaA["Suma"] = $"{sumTonazTuszkiA:N0} kg";
                rowTuszkaA["Procent"] = $"{(sumTonazTuszkiA / totalTuszki) * 100:N2} %";
                finalTableTusz.Rows.Add(rowTuszkaA);

                DataRow rowTuszkaB = finalTableTusz.NewRow();
                rowTuszkaB["Towar"] = "Tuszka B";
                rowTuszkaB["Suma"] = $"{sumTonazTuszkiB:N0} kg";
                rowTuszkaB["Procent"] = $"{(sumTonazTuszkiB / totalTuszki) * 100:N2} %";
                finalTableTusz.Rows.Add(rowTuszkaB);

                // Dodanie wierszy do tabeli finalTableElement dla każdego towaru
                DataRow rowCwiartka = finalTableElement.NewRow();
                rowCwiartka["Towar"] = "Ćwiartka";
                rowCwiartka["Suma"] = $"{sumCwiartka:N0} kg";
                rowCwiartka["Procent"] = $"{(sumCwiartka / totalElementy) * 100:N2} %";
                finalTableElement.Rows.Add(rowCwiartka);

                DataRow rowSkrzydlo = finalTableElement.NewRow();
                rowSkrzydlo["Towar"] = "Skrzydło";
                rowSkrzydlo["Suma"] = $"{sumSkrzydlo:N0} kg";
                rowSkrzydlo["Procent"] = $"{(sumSkrzydlo / totalElementy) * 100:N2} %";
                finalTableElement.Rows.Add(rowSkrzydlo);

                DataRow rowFilet = finalTableElement.NewRow();
                rowFilet["Towar"] = "Filet";
                rowFilet["Suma"] = $"{sumFilet:N0} kg";
                rowFilet["Procent"] = $"{(sumFilet / totalElementy) * 100:N2} %";
                finalTableElement.Rows.Add(rowFilet);

                DataRow rowKorpus = finalTableElement.NewRow();
                rowKorpus["Towar"] = "Korpus";
                rowKorpus["Suma"] = $"{sumKorpus:N0} kg";
                rowKorpus["Procent"] = $"{(sumKorpus / totalElementy) * 100:N2} %";
                finalTableElement.Rows.Add(rowKorpus);

                // Dodanie wiersza sumującego na początku tabeli finalTableTusz
                DataRow sumRowTusz = finalTableTusz.NewRow();
                sumRowTusz["Towar"] = "Suma:";
                sumRowTusz["Suma"] = $"{totalTuszki:N0} kg";
                sumRowTusz["Procent"] = "100 %";
                finalTableTusz.Rows.InsertAt(sumRowTusz, 0);

                // Dodanie wiersza sumującego na początku tabeli finalTableElement
                DataRow sumRowElement = finalTableElement.NewRow();
                sumRowElement["Towar"] = "Suma:";
                sumRowElement["Suma"] = $"{totalElementy:N0} kg";
                sumRowElement["Procent"] = "100 %";
                finalTableElement.Rows.InsertAt(sumRowElement, 0);
                // Dodanie wiersza sumującego na początku tabeli finalTableElement
                DataRow rowOdpad = finalTableElement.NewRow();
                rowOdpad["Towar"] = "Pozostale`";
                rowOdpad["Suma"] = $"{sumPozostale:N0} kg";
                rowOdpad["Procent"] = $"{(sumPozostale / totalElementy) * 100:N2} %";
                finalTableElement.Rows.Add(rowOdpad);
            }

            // Ustawienie źródła danych dla DataGridViewPrzewidywalnyTusz
            dataGridViewPrzewidywalnyTusz.DataSource = finalTableTusz;
            dataGridViewPrzewidywalnyTusz.Columns["Towar"].HeaderText = "Towar";
            dataGridViewPrzewidywalnyTusz.Columns["Suma"].HeaderText = "Suma";
            dataGridViewPrzewidywalnyTusz.Columns["Procent"].HeaderText = "Procent";

            // Ustawienie źródła danych dla DataGridViewPrzewidywalnyElement
            dataGridViewPrzewidywalnyElement.DataSource = finalTableElement;
            dataGridViewPrzewidywalnyElement.Columns["Towar"].HeaderText = "Towar";
            dataGridViewPrzewidywalnyElement.Columns["Suma"].HeaderText = "Suma";
            dataGridViewPrzewidywalnyElement.Columns["Procent"].HeaderText = "Procent";

            // Formatowanie pierwszego wiersza (wiersz sumujący)
            FormatSumRow(dataGridViewPrzewidywalnyTusz);
            FormatSumRow(dataGridViewPrzewidywalnyElement);
            SetRowHeights(18, dataGridViewPrzewidywalnyTusz);
            SetRowHeights(18, dataGridViewPrzewidywalnyElement);
        }

        private void FormatSumRow(DataGridView gridView)
        {
            DataGridViewRow sumRow = gridView.Rows[0];
            sumRow.DefaultCellStyle.BackColor = SystemColors.Highlight;
            sumRow.DefaultCellStyle.ForeColor = Color.White;
            sumRow.DefaultCellStyle.Font = new Font(gridView.Font, FontStyle.Bold);
        }


        public DataTable PobierzPrzychod(bool showDetails)
        {
            // Tworzenie DataTable na przechowywanie wyników
            DataTable resultTable = new DataTable();

            // Łączenie się z bazą danych
            using (SqlConnection connection = new SqlConnection(connectionString2))
            {
                connection.Open();

                string formattedDate = dateTimePicker1.Value.ToString("yyyy-MM-dd");
                string formattedDate2 = dateTimePicker2.Value.ToString("yyyy-MM-dd");

                string query = $@"
    SELECT 
        MZ.[kod],
        ABS(SUM(CASE WHEN MG.[seria] = 'PWP' THEN MZ.[ilosc] ELSE 0 END)) AS Przychod
    FROM [HANDEL].[HM].[MZ] MZ
    INNER JOIN [HANDEL].[HM].[MG] MG ON MZ.[super] = MG.[id] 
    WHERE MZ.[kod] NOT IN ('Kurczak B', 'Kurczak A') 
      AND MZ.[magazyn] = '65554' 
      AND MZ.[data] >= '{formattedDate}' 
      AND MZ.[data] < DATEADD(DAY, 1, '{formattedDate2}')
      AND MG.[opis] LIKE '%zmiana%'
    GROUP BY MZ.[kod]
    HAVING ABS(SUM(CASE WHEN MG.[seria] = 'PWP' THEN MZ.[ilosc] ELSE 0 END)) > 0
    ORDER BY MZ.[kod]";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@FormattedDate", formattedDate);

                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(resultTable);
                    }
                }
            }

            DataTable groupedTable = new DataTable();
            groupedTable.Columns.Add("Towar", typeof(string));
            groupedTable.Columns.Add("Przychod", typeof(double));
            groupedTable.Columns.Add("Procent", typeof(string));

            double totalPrzychod = resultTable.AsEnumerable().Sum(row => row.Field<double>("Przychod"));

            var groups = new Dictionary<string, List<string>>
    {
        { "Cwiartka", NogaGroup },
        { "Filet", FiletGroup },
        { "Skrzydło", SkrzydloGroup },
        { "Korpus", KorpusGroup }
    };

            // Dodanie grupy "Tuba" tylko wtedy, gdy jest obecna w danych
            double tubaPrzychod = resultTable.AsEnumerable()
                .Where(row => TubaGroup.Contains(row.Field<string>("kod")))
                .Sum(row => row.Field<double>("Przychod"));

            if (tubaPrzychod > 0)
            {
                groups.Add("Tuba", TubaGroup);
            }

            foreach (var group in groups)
            {
                double groupPrzychod = resultTable.AsEnumerable()
                    .Where(row => group.Value.Contains(row.Field<string>("kod")))
                    .Sum(row => row.Field<double>("Przychod"));

                if (groupPrzychod > 0)
                {
                    DataRow newRow = groupedTable.NewRow();
                    newRow["Towar"] = group.Key;
                    newRow["Przychod"] = groupPrzychod;
                    newRow["Procent"] = $"{(groupPrzychod / totalPrzychod) * 100:N2} %";
                    groupedTable.Rows.Add(newRow);

                    if (showDetails)
                    {
                        var detailsRows = resultTable.AsEnumerable()
                            .Where(row => group.Value.Contains(row.Field<string>("kod")));

                        foreach (var detailRow in detailsRows)
                        {
                            DataRow detailNewRow = groupedTable.NewRow();
                            detailNewRow["Towar"] = "   " + detailRow.Field<string>("kod"); // Wcięcie, by wskazać szczegóły
                            detailNewRow["Przychod"] = detailRow.Field<double>("Przychod");
                            detailNewRow["Procent"] = $"{(detailRow.Field<double>("Przychod") / totalPrzychod) * 100:N2} %";
                            groupedTable.Rows.Add(detailNewRow);
                        }
                    }
                }
            }

            double otherPrzychod = resultTable.AsEnumerable()
                .Where(row => !groups.SelectMany(g => g.Value).Contains(row.Field<string>("kod")))
                .Sum(row => row.Field<double>("Przychod"));

            if (otherPrzychod > 0)
            {
                DataRow otherRow = groupedTable.NewRow();
                otherRow["Towar"] = "Pozostałe";
                otherRow["Przychod"] = otherPrzychod;
                otherRow["Procent"] = $"{(otherPrzychod / totalPrzychod) * 100:N2} %";
                groupedTable.Rows.Add(otherRow);

                if (showDetails)
                {
                    var detailsRows = resultTable.AsEnumerable()
                        .Where(row => !groups.SelectMany(g => g.Value).Contains(row.Field<string>("kod")));

                    foreach (var detailRow in detailsRows)
                    {
                        DataRow detailNewRow = groupedTable.NewRow();
                        detailNewRow["Towar"] = "   " + detailRow.Field<string>("kod"); // Wcięcie, by wskazać szczegóły
                        detailNewRow["Przychod"] = detailRow.Field<double>("Przychod");
                        detailNewRow["Procent"] = $"{(detailRow.Field<double>("Przychod") / totalPrzychod) * 100:N2} %";
                        groupedTable.Rows.Add(detailNewRow);
                    }
                }
            }

            DataRow sumRow = groupedTable.NewRow();
            sumRow["Towar"] = "Suma:";
            sumRow["Przychod"] = totalPrzychod;
            sumRow["Procent"] = "100 %";
            groupedTable.Rows.InsertAt(sumRow, 0);

            return groupedTable;
        }

        private void dataGridViewPrzychodElementow_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dataGridViewPrzychodElementow.Columns[e.ColumnIndex].Name == "Przychod" && e.Value != null)
            {
                string currentValue = e.Value.ToString();

                // Sprawdzamy, czy "kg" już nie zostało dodane
                if (!currentValue.Contains("kg"))
                {
                    e.Value = string.Format("{0:N0} kg", e.Value);
                    e.FormattingApplied = true;
                }
            }
        }


        private void PokazPrzychodElementow()
        {
            // Ustalanie, czy szczegóły mają być wyświetlane
            bool showDetails = checkBoxShowDetails.Checked;

            // Wywołanie metody PobierzPrzychod z argumentem showDetails
            DataTable przychodTable = PobierzPrzychod(showDetails);

            // Przypisanie wyników do DataGridView
            dataGridViewPrzychodElementow.DataSource = przychodTable;

            // Opcjonalne formatowanie kolumn w DataGridView
            dataGridViewPrzychodElementow.Columns["Towar"].HeaderText = "Grupa";
            dataGridViewPrzychodElementow.Columns["Przychod"].HeaderText = "Przychód";
            dataGridViewPrzychodElementow.Columns["Procent"].HeaderText = "Procent";

            // Dostosowanie szerokości kolumn do zawartości
            dataGridViewPrzychodElementow.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            SetRowHeights(18, dataGridViewPrzychodElementow);
            // Pogrubienie pierwszego wiersza (suma)
            FormatSumRow(dataGridViewPrzychodElementow);
        }


        private readonly List<string> NogaGroup = new List<string>
{
    "Ćwiartka", "Ćwiartka I Mrożona", "Ćwiartka II", "Ćwiartka II Mrożona",
    "Noga", "Noga Mrożona", "Udo", "Udo Mrożone", "Pałka", "Pałka Mrożona"
};

        private readonly List<string> FiletGroup = new List<string>
{
    "Filet A", "Filet I Mrożony", "Filet C", "Filet II", "Filet II Mrożony",
    "Trybowane ze skórą II", "Trybowane ze skórą", "Trybowane bez skóry", "Filet II pp Mrożone", "Filet II pp",
    "Filet II Mrożona", "Filet ze skórą", "Polędwiczki Mrożone", "Polędwiczki"
};

        private readonly List<string> SkrzydloGroup = new List<string>
{
    "Skrzydło I", "Skrzydło II", "Skrzydło I Mrożone", "Skrzydło II Mrożone"
};


        private readonly List<string> KorpusGroup = new List<string>
{
    "Korpus", "Korpus Mrożony"
};
        private readonly List<string> TubaGroup = new List<string>
{
        "Tuba"
};

        private void dataGridView3_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void pictureBox29_Click(object sender, EventArgs e)
        {

        }

        private void OdswiezButton_Click(object sender, EventArgs e)
        {
            PokazCeneTuszki_Load(sender, null);
            PokazCeneHarmonogramDostaw();
        }
    }

}