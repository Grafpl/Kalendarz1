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
    public partial class WidokAvilogPlan : Form

    {
        private bool dragging = false;
        private int rowIndexFromMouseDown;
        private DataGridViewRow draggedRow;
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        ZapytaniaSQL zapytaniasql = new ZapytaniaSQL();

        public WidokAvilogPlan()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            DisplayData();
        }
        private void DisplayData()
        {
            // Tworzenie połączenia z bazą danych i pobieranie danych
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Tworzenie komendy SQL
                string query = "SELECT LP, Auta, Dostawca, SztukiDek, WagaDek, SztSzuflada FROM dbo.HarmonogramDostaw WHERE DataOdbioru = @StartDate AND Bufor = 'Potwierdzony'";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@StartDate", dateTimePicker1.Value.Date);

                // Tworzenie adaptera danych i wypełnianie DataTable
                SqlDataAdapter adapter = new SqlDataAdapter(command);
                DataTable table = new DataTable();
                adapter.Fill(table);

                // Dodanie pozostałych kolumn do tabeli finalTable
                DataTable finalTable = new DataTable();
                finalTable.Columns.Add("Nr", typeof(int)); // Dodanie kolumny "Numer" na początku
                finalTable.Columns.Add("Auta", typeof(string)); // Typ string, aby można było ukrywać duplikaty
                finalTable.Columns.Add("Dostawca", typeof(string));
                finalTable.Columns.Add("SztukiDek", typeof(string));
                finalTable.Columns.Add("WagaDek", typeof(string)); // Typ string, aby można było dodać przedrostki
                finalTable.Columns.Add("SztSzuflada", typeof(string)); // Typ string, aby można było dodać przedrostki
                finalTable.Columns.Add("TotalWeight", typeof(string)); // Kolumna na wynik mnożenia z przedrostkiem
                finalTable.Columns.Add("TotalWeightMultiplied", typeof(string)); // Kolumna na wynik TotalWeight * 264
                finalTable.Columns.Add("TaraAuta", typeof(string)); // Nowa kolumna dla Tary Auta
                finalTable.Columns.Add("Paleciak", typeof(string)); // Nowa kolumna dla Paleciaka
                finalTable.Columns.Add("Suma", typeof(string)); // Nowa kolumna dla Sumy
                finalTable.Columns.Add("Ulica", typeof(string)); // Nowa kolumna dla Ulicy
                finalTable.Columns.Add("KodPocztowy", typeof(string)); // Nowa kolumna dla Kodu Pocztowego
                finalTable.Columns.Add("Miejscowosc", typeof(string)); // Nowa kolumna dla Miejscowości
                finalTable.Columns.Add("KM", typeof(string)); // Nowa kolumna dla Miejscowości

                int numer = 1; // Początkowa wartość numeru
                string previousDostawca = null;
                bool paleciakAdded = false; // Flaga do dodawania wartości Paleciak tylko raz dla dostawcy

                // Iteracja przez wiersze tabeli źródłowej
                foreach (DataRow row in table.Rows)
                {
                    int autaValue = (int)row["Auta"];
                    double wagaDekValue = Convert.ToDouble(row["WagaDek"]);
                    int sztSzufladaValue = Convert.ToInt32(row["SztSzuflada"]);
                    int iloscSztukValue = Convert.ToInt32(row["SztukiDek"]);
                    double totalWeight = wagaDekValue * sztSzufladaValue;
                    double totalWeightMultiplied = totalWeight * 264;
                    double taraAuta = 23500;
                    double paleciak = 3000;
                    double suma;

                    // Pobieranie dodatkowych informacji z bazy danych
                    string dostawca = row["Dostawca"].ToString();
                    string idDostawcy = zapytaniasql.ZnajdzIdHodowcyString(dostawca);
                    string ulica = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "address");
                    string kodPocztowy = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "postalcode");
                    string miejscowosc = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "city");
                    string km = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "distance");

                    // Duplikowanie wiersza tyle razy, ile wynosi wartość w kolumnie Auta
                    for (int i = 0; i < autaValue; i++)
                    {
                        DataRow newRow = finalTable.NewRow();
                        newRow["Nr"] = numer++; // Ustaw numer i zwiększ wartość

                        // Ustawianie wartości kolumn, aby unikać duplikatów
                        if (previousDostawca != row["Dostawca"].ToString())
                        {
                            newRow["Auta"] = row["Auta"];
                            newRow["Dostawca"] = row["Dostawca"];
                            previousDostawca = row["Dostawca"].ToString(); // Zaktualizuj poprzedniego dostawcę
                            newRow["SztukiDek"] = string.Format("{0:N0} szt", row["SztukiDek"]);
                            paleciakAdded = false; // Reset flagi dla nowego dostawcy
                        }
                        else
                        {
                            newRow["Auta"] = string.Empty;
                            newRow["Dostawca"] = string.Empty;
                        }

                        newRow["WagaDek"] = $"{wagaDekValue:F2} kg"; // Dodanie przedrostka "kg" i formatowanie
                        newRow["SztSzuflada"] = $"{sztSzufladaValue} szt"; // Dodanie przedrostka "szt"
                        newRow["TotalWeight"] = $"{totalWeight:F2} kg"; // Dodanie przedrostka "kg" i formatowanie
                        newRow["TotalWeightMultiplied"] = $"{totalWeightMultiplied:N0} kg"; // Separator tysięcy, bez miejsc po przecinku, z przedrostkiem "kg"
                        newRow["TaraAuta"] = $"{taraAuta:N0} kg"; // Dodanie wartości Tary Auta

                        // Dodawanie wartości Paleciak tylko raz dla dostawcy
                        if (!paleciakAdded)
                        {
                            newRow["Paleciak"] = $"{paleciak:N0} kg";
                            paleciakAdded = true;
                            suma = totalWeightMultiplied + taraAuta + paleciak;
                        }
                        else
                        {
                            newRow["Paleciak"] = string.Empty;
                            suma = totalWeightMultiplied + taraAuta;
                        }

                        newRow["Suma"] = $"{suma:N0} kg"; // Dodanie wartości Suma

                        // Dodanie dodatkowych informacji do nowego wiersza
                        newRow["Ulica"] = ulica;
                        newRow["KodPocztowy"] = kodPocztowy;
                        newRow["Miejscowosc"] = miejscowosc;
                        newRow["KM"] = km + " km";

                        finalTable.Rows.Add(newRow);
                    }
                }

                // Ustawienie źródła danych dla DataGridView
                dataGridView1.DataSource = finalTable;

                // Zmiana nazw kolumn
                dataGridView1.Columns["Nr"].HeaderText = "Nr";
                dataGridView1.Columns["Auta"].HeaderText = "Auta";
                dataGridView1.Columns["Dostawca"].HeaderText = "Dostawca";
                dataGridView1.Columns["SztukiDek"].HeaderText = "Sztuki";
                dataGridView1.Columns["WagaDek"].HeaderText = "Waga";
                dataGridView1.Columns["SztSzuflada"].HeaderText = "Szt";
                dataGridView1.Columns["TotalWeight"].HeaderText = "Waga x Szt";
                dataGridView1.Columns["TotalWeightMultiplied"].HeaderText = "Klatka x 264";
                dataGridView1.Columns["TaraAuta"].HeaderText = "Tara Auta";
                dataGridView1.Columns["Paleciak"].HeaderText = "Paleciak";
                dataGridView1.Columns["Suma"].HeaderText = "Suma";
                dataGridView1.Columns["Ulica"].HeaderText = "Ulica";
                dataGridView1.Columns["KodPocztowy"].HeaderText = "Kod";
                dataGridView1.Columns["Miejscowosc"].HeaderText = "Miejs";
                dataGridView1.Columns["KM"].HeaderText = "KM";

                // Automatyczne dopasowanie szerokości kolumn
                foreach (DataGridViewColumn column in dataGridView1.Columns)
                {
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                }

                // Ustawienie szarego tła co drugiego wiersza
                for (int i = 0; i < dataGridView1.Rows.Count; i++)
                {
                    if (i % 2 == 1) // Sprawdzanie, czy wiersz jest nieparzysty (co drugi wiersz)
                    {
                        dataGridView1.Rows[i].DefaultCellStyle.BackColor = Color.LightGray;
                    }
                }

                // Pogrubienie kolumn "Suma" i "Dostawca"
                dataGridView1.Columns["Suma"].DefaultCellStyle.Font = new Font(dataGridView1.Font, FontStyle.Bold);
                dataGridView1.Columns["Dostawca"].DefaultCellStyle.Font = new Font(dataGridView1.Font, FontStyle.Bold);
            }

            SetRowHeights(18, dataGridView1);
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
