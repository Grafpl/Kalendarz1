using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualBasic.ApplicationServices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using System.Windows.Controls;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Kalendarz1
{
    public partial class WidokSprzedazPlan : Form

    {
        private bool dragging = false;
        private int rowIndexFromMouseDown;
        private DataGridViewRow draggedRow;
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        ZapytaniaSQL zapytaniasql = new ZapytaniaSQL();

        public WidokSprzedazPlan()
        {
            InitializeComponent();
            DisplayData();
        }
        private void DisplayData()
        {
            DataTable finalTable = new DataTable();

            // Tworzenie połączenia z bazą danych i pobieranie danych
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Tworzenie komendy SQL
                string query = "SELECT LP, Auta, Dostawca, WagaDek, SztukiDek FROM dbo.HarmonogramDostaw WHERE DataOdbioru = @StartDate AND Bufor = 'Potwierdzony' ORDER BY WagaDek DESC";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@StartDate", dateTimePicker1.Value.Date);

                // Tworzenie adaptera danych i wypełnianie DataTable
                SqlDataAdapter adapter = new SqlDataAdapter(command);
                DataTable table = new DataTable();
                adapter.Fill(table);

                // Dodanie pozostałych kolumn do tabeli finalTable
                finalTable.Columns.Add("Dostawca", typeof(string));
                finalTable.Columns.Add("Auta", typeof(string)); // Typ string, aby można było ukrywać duplikaty
                finalTable.Columns.Add("WagaDek", typeof(string)); // Typ string, aby można było dodać przedrostki
                finalTable.Columns.Add("SztukiDek", typeof(string)); // Typ string, aby można było dodać przedrostki
                finalTable.Columns.Add("WagaSredniaTuszka", typeof(string)); // Typ string, aby można było dodać przedrostki
                finalTable.Columns.Add("TonazTuszki", typeof(string)); // Typ string, aby można było dodać przedrostki
                finalTable.Columns.Add("Poj", typeof(string)); // Typ string, aby można było dodać przedrostki
                finalTable.Columns.Add("TonazTuszkiA", typeof(string)); // Typ string, aby można było dodać przedrostki
                finalTable.Columns.Add("Cwiartka", typeof(string)); // Typ string, aby można było dodać przedrostki
                finalTable.Columns.Add("Skrzydlo", typeof(string)); // Typ string, aby można było dodać przedrostki
                finalTable.Columns.Add("Filet", typeof(string)); // Typ string, aby można było dodać przedrostki
                finalTable.Columns.Add("Korpus", typeof(string)); // Typ string, aby można było dodać przedrostki

                // Inicjalizacja zmiennych sum
                int sumSztukiDek = 0;
                double sumTonazTuszki = 0;
                double sumTonazTuszkiA = 0;
                double sumCwiartka = 0;
                double sumFilet = 0;
                double sumSkrzydlo = 0;
                double sumKorpus = 0;
                double sumauta = 0;

                // Iteracja przez wiersze tabeli źródłowej
                foreach (DataRow row in table.Rows)
                {
                    int autaValue = (int)row["Auta"];
                    double wagaDekValue = Convert.ToDouble(row["WagaDek"]);
                    int sztukiDekValue = Convert.ToInt32(row["SztukiDek"]);


                    double sredniaTuszkaValue = wagaDekValue * 0.78;
                    double tonazTuszkaValue = sredniaTuszkaValue * sztukiDekValue;
                    double tonazTuszkaAValue = tonazTuszkaValue * 0.80;
                    double tonazTuszkaBValue = tonazTuszkaValue * 0.20;
                    double tonazCwiartkaValue = tonazTuszkaBValue * 0.38;
                    double tonazSkrzydloValue = tonazTuszkaBValue * 0.09;
                    double tonazFiletValue = tonazTuszkaBValue * 0.28;
                    double tonazKorpusValue = tonazTuszkaBValue * 0.19;

                    // Sumowanie wartości
                    sumSztukiDek += sztukiDekValue;
                    sumTonazTuszki += tonazTuszkaValue;
                    sumTonazTuszkiA += tonazTuszkaAValue;
                    sumCwiartka += tonazCwiartkaValue;
                    sumFilet += tonazFiletValue;
                    sumSkrzydlo += tonazSkrzydloValue;
                    sumKorpus += tonazKorpusValue;
                    sumauta += autaValue;

                    double IlePoj = 15 / sredniaTuszkaValue;
                    string formattedIlePoj;

                    // Warunkowe formatowanie
                    if (IlePoj % 1 >= 0.65 && IlePoj % 1 <= 0.99)
                    {
                        formattedIlePoj = $"{Math.Floor(IlePoj)}/{Math.Ceiling(IlePoj)}/{Math.Ceiling(IlePoj) + 1} szt"; // Zaokrąglij w górę i dodaj dwa kolejne
                    }
                    else if (IlePoj % 1 >= 0.36 && IlePoj % 1 <= 0.64)
                    {
                        formattedIlePoj = $"{Math.Floor(IlePoj)}/{Math.Ceiling(IlePoj)} szt"; // Dodaj dwa kolejne
                    }
                    else
                    {
                        formattedIlePoj = $"{Math.Floor(IlePoj)}/{Math.Floor(IlePoj) + 1}/{Math.Floor(IlePoj) + 2} szt"; // Dodaj dwa kolejne
                    }

                    DataRow newRow = finalTable.NewRow();
                    newRow["Dostawca"] = row["Dostawca"].ToString();
                    newRow["Auta"] = autaValue.ToString();
                    newRow["WagaDek"] = $"{wagaDekValue:F2} kg"; // Dodanie przedrostka "kg" i formatowanie
                    newRow["SztukiDek"] = $"{sztukiDekValue} szt"; // Dodanie przedrostka "szt"
                    newRow["WagaSredniaTuszka"] = $"{sredniaTuszkaValue:F2} kg"; // Dodanie przedrostka "kg" i formatowanie
                    newRow["TonazTuszki"] = $"{tonazTuszkaValue:N0} kg"; // Separator tysięcy, bez miejsc po przecinku
                    newRow["Poj"] = formattedIlePoj; // Użycie sformatowanej wartości
                    newRow["TonazTuszkiA"] = $"{tonazTuszkaAValue:N0} kg"; // Dodanie przedrostka "kg" i formatowanie
                    newRow["Cwiartka"] = $"{tonazCwiartkaValue:N0} kg"; // Separator tysięcy, bez miejsc po przecinku, z przedrostkiem "kg"
                    newRow["Skrzydlo"] = $"{tonazSkrzydloValue:N0} kg"; // Separator tysięcy, bez miejsc po przecinku, z przedrostkiem "kg"
                    newRow["Filet"] = $"{tonazFiletValue:N0} kg"; // Separator tysięcy, bez miejsc po przecinku, z przedrostkiem "kg"
                    newRow["Korpus"] = $"{tonazKorpusValue:N0} kg"; // Separator tysięcy, bez miejsc po przecinku, z przedrostkiem "kg"

                    finalTable.Rows.Add(newRow);
                }

                // Dodanie wiersza z datą na początku tabeli
                DataRow dateRow = finalTable.NewRow();
                dateRow["Dostawca"] = dateTimePicker1.Value.ToString("yyyy-MM-dd ddd");
                dateRow["Auta"] = $"{sumauta:N0}";
                dateRow["SztukiDek"] = $"{sumSztukiDek:N0} szt";
                dateRow["TonazTuszki"] = $"{sumTonazTuszki:N0} kg";
                dateRow["TonazTuszkiA"] = $"{sumTonazTuszkiA:N0} kg";
                dateRow["Cwiartka"] = $"{sumCwiartka:N0} kg";
                dateRow["Filet"] = $"{sumFilet:N0} kg";
                dateRow["Skrzydlo"] = $"{sumSkrzydlo:N0} kg";
                dateRow["Korpus"] = $"{sumKorpus:N0} kg";

                finalTable.Rows.InsertAt(dateRow, 0);
            }

            // Ustawienie źródła danych dla DataGridView
            dataGridView1.DataSource = finalTable;

            // Zmiana nazw kolumn
            dataGridView1.Columns["Auta"].HeaderText = "A";
            dataGridView1.Columns["Dostawca"].HeaderText = "Dostawca";
            dataGridView1.Columns["WagaDek"].HeaderText = "Zywiec";
            dataGridView1.Columns["SztukiDek"].HeaderText = "Szt";
            dataGridView1.Columns["WagaSredniaTuszka"].HeaderText = "Tuszka";
            dataGridView1.Columns["TonazTuszki"].HeaderText = "TuszkaA/B";
            dataGridView1.Columns["Poj"].HeaderText = "Poj";
            dataGridView1.Columns["TonazTuszkiA"].HeaderText = "Tusz A";
            dataGridView1.Columns["Cwiartka"].HeaderText = "Ćwiartka";
            dataGridView1.Columns["Skrzydlo"].HeaderText = "Skrzydło";
            dataGridView1.Columns["Filet"].HeaderText = "Filet";
            dataGridView1.Columns["Korpus"].HeaderText = "Korpus";

            dataGridView1.Columns["SztukiDek"].Visible = false;

            // Ustawienie stałej szerokości dla określonych kolumn
            dataGridView1.Columns["Auta"].Width = 35;
            dataGridView1.Columns["Dostawca"].Width = 115;
            dataGridView1.Columns["WagaDek"].Width = 50;
            dataGridView1.Columns["SztukiDek"].Width = 60;
            dataGridView1.Columns["WagaSredniaTuszka"].Width = 50;
            dataGridView1.Columns["TonazTuszki"].Width = 70;
            dataGridView1.Columns["Poj"].Width = 90;
            dataGridView1.Columns["TonazTuszkiA"].Width = 70;
            dataGridView1.Columns["Cwiartka"].Width = 65;
            dataGridView1.Columns["Skrzydlo"].Width = 65;
            dataGridView1.Columns["Filet"].Width = 65;
            dataGridView1.Columns["Korpus"].Width = 65;

            // Ustawienie szarego tła co drugiego wiersza
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
            {
                if (i % 2 == 1) // Sprawdzanie, czy wiersz jest nieparzysty (co drugi wiersz)
                {
                    dataGridView1.Rows[i].DefaultCellStyle.BackColor = Color.LightGray;
                }
            }

            // Pogrubienie kolumn "TonazTuszki" i "Dostawca"
            dataGridView1.Columns["TonazTuszkiA"].DefaultCellStyle.Font = new Font(dataGridView1.Font, FontStyle.Bold);
            dataGridView1.Columns["Filet"].DefaultCellStyle.Font = new Font(dataGridView1.Font, FontStyle.Bold);
            dataGridView1.Columns["Auta"].DefaultCellStyle.Font = new Font(dataGridView1.Font, FontStyle.Bold);
            dataGridView1.Columns["Poj"].DefaultCellStyle.Font = new Font(dataGridView1.Font, FontStyle.Bold);
            dataGridView1.Columns["Dostawca"].DefaultCellStyle.Font = new Font(dataGridView1.Font, FontStyle.Bold);

            // Formatowanie pierwszego wiersza
            DataGridViewRow sumRow = dataGridView1.Rows[0];
            sumRow.DefaultCellStyle.BackColor = SystemColors.Highlight;
            sumRow.DefaultCellStyle.ForeColor = Color.White;
            sumRow.DefaultCellStyle.Font = new Font(dataGridView1.Font, FontStyle.Bold);

            // Ustawienie wysokości wierszy
            SetRowHeights(18, dataGridView1);

            // Ukrycie nagłówków wierszy
            dataGridView1.RowHeadersVisible = false;
        }




        private void SetRowHeights(int height, DataGridView dataGridView)
        {
            // Ustawienie wysokości wszystkich wierszy na określoną wartość
            foreach (DataGridViewRow row in dataGridView.Rows)
            {
                row.Height = height;
            }
        }


        private int selectedRowIndex = -1; // Dodaj zmienną do przechowywania indeksu zaznaczonego wiersza

        // Metoda do przesuwania wiersza w górę
        // Metoda do przesuwania wiersza w górę
       
        private void RefreshNumeration()
        {
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
            {
                dataGridView1.Rows[i].Cells["Nr"].Value = i + 1;
            }
        }
        private void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
            DisplayData();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                foreach (DataGridViewRow row in dataGridView1.Rows)
                {
                    if (!row.IsNewRow) // Pomijamy wiersz tworzący nowe rekordy
                    {

                        string sql = "INSERT INTO dbo.FarmerCalc (ID, CalcDate, CustomerGID, CustomerRealGID, DriverGID, CarLp, SztPoj, WagaDek, CarID, TrailerID, NotkaWozek, LpDostawy, Wyjazd, Zaladunek, Przyjazd, Price, Loss, PriceTypeID) " +
                            "VALUES (@ID, @Date, @Dostawca, @Dostawca, @Kierowca, @Nr, @SztPoj, @WagaDek, @Ciagnik, @Naczepa, @Wozek, @LpDostawy, @Wyjazd, @Zaladunek, @Przyjazd, @Cena, @Ubytek, @TypCeny)";
                        // Pobierz dane z wiersza DataGridView
                        string Dostawca = row.Cells["Dostawca"].Value.ToString();
                        string Kierowca = row.Cells["Kierowca"].Value.ToString();
                        string LpDostawy = row.Cells["Lp"].Value.ToString();
                        string Nr = row.Cells["Nr"].Value.ToString();
                        string SztPoj = row.Cells["SztSzuflada"].Value.ToString();
                        string WagaDek = row.Cells["WagaDek"].Value.ToString();
                        string Ciagnik = row.Cells["Pojazd"].Value.ToString();
                        string Naczepa = row.Cells["Naczepa"].Value.ToString();
                        string Wozek = row.Cells["Wozek"].Value.ToString();

                        string StringPrzyjazd = row.Cells["Przyjazd"].Value.ToString();
                        string StringZaladunek = row.Cells["Zaladunek"].Value.ToString();
                        string StringWyjazd = row.Cells["Wyjazd"].Value.ToString();

                        double Ubytek;
                        if (!double.TryParse(zapytaniasql.PobierzInformacjeZBazyDanychKonkretne(LpDostawy, "Ubytek"), out Ubytek)) Ubytek = 0.0;
                        double Cena;
                        if (!double.TryParse(zapytaniasql.PobierzInformacjeZBazyDanychKonkretne(LpDostawy, "Cena"), out Cena)) Cena = 0.0;
                        string typCeny = zapytaniasql.PobierzInformacjeZBazyDanychKonkretne(LpDostawy, "TypCeny");
                        int intTypCeny = zapytaniasql.ZnajdzIdCeny(typCeny);




                        // Znajdź ID kierowcy i dostawcy
                        int userId = zapytaniasql.ZnajdzIdKierowcy(Kierowca);
                        int userId2 = zapytaniasql.ZnajdzIdHodowcy(Dostawca);

                        StringWyjazd = zapytaniasql.DodajDwukropek(StringWyjazd);
                        StringZaladunek = zapytaniasql.DodajDwukropek(StringZaladunek);
                        StringPrzyjazd = zapytaniasql.DodajDwukropek(StringPrzyjazd);

                        DateTime data = dateTimePicker1.Value; // Przyjmijmy, że to wartość z DateTimePicker
                        DateTime combinedDateTimeWyjazd = ZapytaniaSQL.CombineDateAndTime(StringWyjazd, data);
                        DateTime combinedDateTimeZaladunek = ZapytaniaSQL.CombineDateAndTime(StringZaladunek, data);
                        DateTime combinedDateTimePrzyjazd = ZapytaniaSQL.CombineDateAndTime(StringPrzyjazd, data);

                        //int Cena = zapytaniasql.ZnajdzIdCeny(Dostawca);

                        // Znajdź największe ID w tabeli FarmerCalc
                        long maxLP;
                        string maxLPSql = "SELECT MAX(ID) AS MaxLP FROM dbo.[FarmerCalc];";
                        using (SqlCommand command = new SqlCommand(maxLPSql, conn))
                        {
                            object result = command.ExecuteScalar();
                            maxLP = result == DBNull.Value ? 1 : Convert.ToInt64(result) + 1;
                        }


                        // Wstaw dane do tabeli FarmerCalc
                        using (SqlCommand cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@ID", maxLP);
                            cmd.Parameters.AddWithValue("@Dostawca", userId2);
                            cmd.Parameters.AddWithValue("@Kierowca", userId);
                            cmd.Parameters.AddWithValue("@LpDostawy", string.IsNullOrEmpty(LpDostawy) ? (object)DBNull.Value : LpDostawy);
                            cmd.Parameters.AddWithValue("@Nr", string.IsNullOrEmpty(Nr) ? (object)DBNull.Value : Nr);
                            cmd.Parameters.AddWithValue("@SztPoj", string.IsNullOrEmpty(SztPoj) ? (object)DBNull.Value : decimal.Parse(SztPoj));
                            cmd.Parameters.AddWithValue("@WagaDek", string.IsNullOrEmpty(WagaDek) ? (object)DBNull.Value : decimal.Parse(WagaDek));
                            cmd.Parameters.AddWithValue("@Date", dateTimePicker1.Value.Date);

                            cmd.Parameters.AddWithValue("@Wyjazd", combinedDateTimeWyjazd);
                            cmd.Parameters.AddWithValue("@Zaladunek", combinedDateTimeZaladunek);
                            cmd.Parameters.AddWithValue("@Przyjazd", combinedDateTimePrzyjazd);

                            cmd.Parameters.AddWithValue("@Cena", Cena);
                            cmd.Parameters.AddWithValue("@Ubytek", Ubytek);
                            cmd.Parameters.AddWithValue("@TypCeny", intTypCeny);

                            //cmd.Parameters.AddWithValue("@Cena", CenaInt);

                            cmd.Parameters.AddWithValue("@Ciagnik", string.IsNullOrEmpty(Ciagnik) ? (object)DBNull.Value : Ciagnik);
                            cmd.Parameters.AddWithValue("@Naczepa", string.IsNullOrEmpty(Naczepa) ? (object)DBNull.Value : Naczepa);
                            cmd.Parameters.AddWithValue("@Wozek", string.IsNullOrEmpty(Wozek) ? (object)DBNull.Value : Wozek);

                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        private void button3_Click_1(object sender, EventArgs e)
        {

                // Tworzenie zrzutu ekranu tylko dla formularza
                Bitmap screenshot = new Bitmap(this.Width, this.Height);
                this.DrawToBitmap(screenshot, new Rectangle(0, 0, this.Width, this.Height));

                // Umieszczanie zrzutu ekranu w schowku
                Clipboard.SetImage(screenshot);

                // Informacja dla użytkownika
                MessageBox.Show("Zrzut ekranu widoku formularza został umieszczony w schowku systemowym. Możesz teraz wkleić go do whatsupa na grupie AVILOG.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
           
        }
    }
}
