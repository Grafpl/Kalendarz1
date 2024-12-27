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
    public partial class WidokMatryca : Form

    {
        private bool dragging = false;
        private int rowIndexFromMouseDown;
        private DataGridViewRow draggedRow;
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        ZapytaniaSQL zapytaniasql = new ZapytaniaSQL();

        public WidokMatryca()
        {
            InitializeComponent();
            DisplayData();
        }
        private void DisplayData()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                

                connection.Open();

                // 1. Pobierz listę kierowców (GID, Name) do driverTable
                string driverQuery = @"
                    SELECT 
                        GID, 
                        [Name]
                    FROM [LibraNet].[dbo].[Driver]
                    WHERE Deleted = 0
                    Order by name ASC
                ";
                SqlCommand driverCommand = new SqlCommand(driverQuery, connection);
                SqlDataAdapter driverAdapter = new SqlDataAdapter(driverCommand);
                DataTable driverTable = new DataTable();
                driverAdapter.Fill(driverTable);

                // Tabela CarID (Kind = '1')
                string carQuery = @"
                SELECT DISTINCT ID
                FROM dbo.CarTrailer
                WHERE kind = '1'
                order by ID DESC
            ";
                SqlCommand carCommand = new SqlCommand(carQuery, connection);
                SqlDataAdapter carAdapter = new SqlDataAdapter(carCommand);
                DataTable carTable = new DataTable();
                carAdapter.Fill(carTable);

                // Tabela TrailerID (Kind = '2')
                string trailerQuery = @"
                SELECT DISTINCT ID
                FROM dbo.CarTrailer
                WHERE kind = '2'
                order by ID DESC
            ";
                SqlCommand trailerCommand = new SqlCommand(trailerQuery, connection);
                SqlDataAdapter trailerAdapter = new SqlDataAdapter(trailerCommand);
                DataTable trailerTable = new DataTable();
                trailerAdapter.Fill(trailerTable);

                // 3) Przygotuj wozekTable
                DataTable wozekTable = new DataTable();
                wozekTable.Columns.Add("WozekValue", typeof(string));
                wozekTable.Rows.Add("");
                wozekTable.Rows.Add("Wieziesz wozek");
                wozekTable.Rows.Add("Przywozisz wozek");
                wozekTable.Rows.Add("Wozek w obie strony");


                // 2. Sprawdź, czy istnieją rekordy w FarmerCalc dla wybranej daty
                string checkQuery = @"
                    SELECT COUNT(*) 
                    FROM [LibraNet].[dbo].[FarmerCalc] 
                    WHERE CalcDate = @SelectedDate
                ";
                SqlCommand checkCommand = new SqlCommand(checkQuery, connection);
                checkCommand.Parameters.AddWithValue("@SelectedDate", dateTimePicker1.Value.Date);
                int count = (int)checkCommand.ExecuteScalar();

                DataTable table = new DataTable();
                bool isFarmerCalc = false;

                if (count > 0)
                {
                    // Jeśli są dane w FarmerCalc
                    string query = @"
                        SELECT 
                            ID, 
                            CarLp, 
                            CustomerGID, 
                            WagaDek, 
                            SztPoj, 
                            DriverGID, 
                            CarID, 
                            TrailerID, 
                            Wyjazd, 
                            Zaladunek, 
                            Przyjazd,
                            NotkaWozek
                        FROM [LibraNet].[dbo].[FarmerCalc] 
                        WHERE CalcDate = @SelectedDate
                    ";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@SelectedDate", dateTimePicker1.Value.Date);
                    SqlDataAdapter adapter = new SqlDataAdapter(command);
                    adapter.Fill(table);
                    isFarmerCalc = true;
                }
                else
                {
                    // Jeśli nie ma danych w FarmerCalc – pobierz z HarmonogramDostaw
                    string query = @"
                        SELECT
                            Lp AS CarLp,
                            Auta,
                            Dostawca AS CustomerGID,
                            WagaDek,
                            SztSzuflada AS SztPoj
                        FROM dbo.HarmonogramDostaw 
                        WHERE DataOdbioru = @StartDate 
                        AND Bufor = 'Potwierdzony'
                    ";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@StartDate", dateTimePicker1.Value.Date);
                    SqlDataAdapter adapter = new SqlDataAdapter(command);
                    adapter.Fill(table);
                }

                if (table.Rows.Count == 0)
                {
                    MessageBox.Show(
                        "Brak danych do wyświetlenia na wybrany dzień.",
                        "Informacja",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    connection.Close();
                    return;
                }
                dataGridView1.EditMode = DataGridViewEditMode.EditOnEnter;
                // 3. Tworzymy finalTable z odpowiednimi kolumnami
                DataTable finalTable = new DataTable();
                finalTable.Columns.Add("NrAuta", typeof(int));
                finalTable.Columns.Add("CarLp", typeof(int));
                finalTable.Columns.Add("ID", typeof(int));
                finalTable.Columns.Add("Auta", typeof(int));
                finalTable.Columns.Add("CustomerGID", typeof(string));
                finalTable.Columns.Add("WagaDek", typeof(double));
                finalTable.Columns.Add("SztPoj", typeof(int));
                finalTable.Columns.Add("DriverGID", typeof(int));
                finalTable.Columns.Add("CarID", typeof(string));
                finalTable.Columns.Add("TrailerID", typeof(string));
                finalTable.Columns.Add("Wyjazd", typeof(string));
                finalTable.Columns.Add("Zaladunek", typeof(string));
                finalTable.Columns.Add("Przyjazd", typeof(string));
                finalTable.Columns.Add("NotkaWozek", typeof(string));

                int numer = 1; // Licznik 'NrAuta'

                // 4. Przeniesienie danych z table do finalTable
                foreach (DataRow row in table.Rows)
                {
                    int autaValue = 1;
                    if (row.Table.Columns.Contains("Auta") && row["Auta"] != DBNull.Value)
                        autaValue = Convert.ToInt32(row["Auta"]);

                    // Duplikowanie wierszy według liczby Auta
                    for (int i = 0; i < autaValue; i++)
                    {
                        DataRow newRow = finalTable.NewRow();
                        newRow["NrAuta"] = numer++;
                        newRow["CarLp"] = row.Table.Columns.Contains("CarLp") ? row["CarLp"] : DBNull.Value;
                        newRow["ID"] = isFarmerCalc && row.Table.Columns.Contains("ID") ? row["ID"] : DBNull.Value;
                        newRow["Auta"] = row.Table.Columns.Contains("Auta") ? row["Auta"] : DBNull.Value;
                        newRow["CustomerGID"] = row["CustomerGID"];
                        newRow["WagaDek"] = row["WagaDek"];
                        newRow["SztPoj"] = row["SztPoj"];

                        // Dla FarmerCalc – kolumny DriverGID, CarID, TrailerID, Wyjazd, Zaladunek, Przyjazd, NotkaWozek
                        newRow["DriverGID"] = isFarmerCalc && row.Table.Columns.Contains("DriverGID")
                            ? row["DriverGID"]
                            : DBNull.Value;
                        newRow["CarID"] = isFarmerCalc && row.Table.Columns.Contains("CarID")
                            ? row["CarID"]
                            : DBNull.Value;
                        newRow["TrailerID"] = isFarmerCalc && row.Table.Columns.Contains("TrailerID")
                            ? row["TrailerID"]
                            : DBNull.Value;

                        // Formatowanie pól czasowych
                        newRow["Wyjazd"] = FormatToHHMM(
                            isFarmerCalc && row.Table.Columns.Contains("Wyjazd")
                                ? row["Wyjazd"].ToString()
                                : ""
                        );
                        newRow["Zaladunek"] = FormatToHHMM(
                            isFarmerCalc && row.Table.Columns.Contains("Zaladunek")
                                ? row["Zaladunek"].ToString()
                                : ""
                        );
                        newRow["Przyjazd"] = FormatToHHMM(
                            isFarmerCalc && row.Table.Columns.Contains("Przyjazd")
                                ? row["Przyjazd"].ToString()
                                : ""
                        );
                        newRow["NotkaWozek"] = FormatToHHMM(
                            isFarmerCalc && row.Table.Columns.Contains("NotkaWozek")
                                ? row["NotkaWozek"].ToString()
                                : ""
                        );

                        finalTable.Rows.Add(newRow);
                    }
                }

                // 5. Ustawianie DataGridView
                dataGridView1.AutoGenerateColumns = false; // wyłącz automatyczne generowanie kolumn
                dataGridView1.Columns.Clear();             // wyczyść, gdyby były jakieś kolumny

                // Przykładowe kolumny tekstowe:

                var colNrAuta = new DataGridViewTextBoxColumn();
                colNrAuta.Name = "NrAuta";
                colNrAuta.HeaderText = "Nr Auta";
                colNrAuta.DataPropertyName = "NrAuta";
                dataGridView1.Columns.Add(colNrAuta);

                var colCarLp = new DataGridViewTextBoxColumn();
                colCarLp.Name = "CarLp";
                colCarLp.HeaderText = "Numer";
                colCarLp.DataPropertyName = "CarLp";
                dataGridView1.Columns.Add(colCarLp);

                var colID = new DataGridViewTextBoxColumn();
                colID.Name = "ID";
                colID.HeaderText = "Identyfikator";
                colID.DataPropertyName = "ID";
                dataGridView1.Columns.Add(colID);

                var colAuta = new DataGridViewTextBoxColumn();
                colAuta.Name = "Auta";
                colAuta.HeaderText = "Liczba Aut";
                colAuta.DataPropertyName = "Auta";
                dataGridView1.Columns.Add(colAuta);

                var colCustomerGID = new DataGridViewTextBoxColumn();
                colCustomerGID.Name = "CustomerGID";
                colCustomerGID.HeaderText = "Nazwa Dostawcy";
                colCustomerGID.DataPropertyName = "CustomerGID";
                dataGridView1.Columns.Add(colCustomerGID);

                var colWagaDek = new DataGridViewTextBoxColumn();
                colWagaDek.Name = "WagaDek";
                colWagaDek.HeaderText = "Deklarowana Waga";
                colWagaDek.DataPropertyName = "WagaDek";
                dataGridView1.Columns.Add(colWagaDek);

                var colSztPoj = new DataGridViewTextBoxColumn();
                colSztPoj.Name = "SztPoj";
                colSztPoj.HeaderText = "Sztuki w Szufladzie";
                colSztPoj.DataPropertyName = "SztPoj";
                dataGridView1.Columns.Add(colSztPoj);

                // Kolumna ComboBox dla DriverGID:
                var colDriver = new DataGridViewComboBoxColumn();
                colDriver.Name = "DriverGID";
                colDriver.HeaderText = "Imię Kierowcy";
                colDriver.DataPropertyName = "DriverGID";
                colDriver.DataSource = driverTable;
                colDriver.DisplayMember = "Name";  // to, co widać na liście
                colDriver.ValueMember = "GID";      // wartość, która będzie trafiać do finalTable["DriverGID"]
                dataGridView1.Columns.Add(colDriver);

                // DGV - ComboBox CarID
                var colCarID = new DataGridViewComboBoxColumn();
                colCarID.Name = "CarID";
                colCarID.HeaderText = "Numer Pojazdu";
                colCarID.DataPropertyName = "CarID";
                colCarID.DataSource = carTable;
                colCarID.DisplayMember = "ID";
                colCarID.ValueMember = "ID";
                dataGridView1.Columns.Add(colCarID);
                // DGV - ComboBox TrailerID
                var colTrailerID = new DataGridViewComboBoxColumn();
                colTrailerID.Name = "TrailerID";
                colTrailerID.HeaderText = "Numer Naczepy";
                colTrailerID.DataPropertyName = "TrailerID";
                colTrailerID.DataSource = trailerTable;
                colTrailerID.DisplayMember = "ID";
                colTrailerID.ValueMember = "ID";
                dataGridView1.Columns.Add(colTrailerID);

                var colWyjazd = new DataGridViewTextBoxColumn();
                colWyjazd.Name = "Wyjazd";
                colWyjazd.HeaderText = "Godzina Wyjazdu";
                colWyjazd.DataPropertyName = "Wyjazd";
                dataGridView1.Columns.Add(colWyjazd);

                var colZaladunek = new DataGridViewTextBoxColumn();
                colZaladunek.Name = "Zaladunek";
                colZaladunek.HeaderText = "Godzina Załadunku";
                colZaladunek.DataPropertyName = "Zaladunek";
                dataGridView1.Columns.Add(colZaladunek);

                var colPrzyjazd = new DataGridViewTextBoxColumn();
                colPrzyjazd.Name = "Przyjazd";
                colPrzyjazd.HeaderText = "Godzina Przyjazdu";
                colPrzyjazd.DataPropertyName = "Przyjazd";
                dataGridView1.Columns.Add(colPrzyjazd);
                // DGV - ComboBox NotkaWozek
                var colWozek = new DataGridViewComboBoxColumn();
                colWozek.Name = "NotkaWozek";
                colWozek.HeaderText = "Numer Wózka";
                colWozek.DataPropertyName = "NotkaWozek";
                colWozek.DataSource = wozekTable;
                colWozek.DisplayMember = "WozekValue";
                colWozek.ValueMember = "WozekValue";
                dataGridView1.Columns.Add(colWozek);

                // 6. Przypisz finalTable jako DataSource
                dataGridView1.DataSource = finalTable;

                // 7. Ewentualnie formatowanie komórek
                dataGridView1.CellFormatting += (sender, e) =>
                {
                    // Jeżeli chcesz w locie formatować godziny, możesz użyć takiego kodu:
                    if ((dataGridView1.Columns.Contains("Wyjazd") && e.ColumnIndex == dataGridView1.Columns["Wyjazd"].Index) ||
                        (dataGridView1.Columns.Contains("Zaladunek") && e.ColumnIndex == dataGridView1.Columns["Zaladunek"].Index) ||
                        (dataGridView1.Columns.Contains("Przyjazd") && e.ColumnIndex == dataGridView1.Columns["Przyjazd"].Index) ||
                        (dataGridView1.Columns.Contains("NotkaWozek") && e.ColumnIndex == dataGridView1.Columns["NotkaWozek"].Index))
                    {
                        if (e.Value != null && e.Value is string value)
                        {
                            if (value.Length == 3)
                            {
                                e.Value = "0" + value.Substring(0, 1) + ":" + value.Substring(1);
                            }
                            else if (value.Length == 4)
                            {
                                e.Value = value.Substring(0, 2) + ":" + value.Substring(2);
                            }
                        }
                    }
                };
                
                connection.Close();
            }
        }

        private string FormatToHHMM(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.Length == 3)
            {
                // Np. "800" → "08:00"
                return "0" + value.Substring(0, 1) + ":" + value.Substring(1);
            }
            else if (value.Length == 4)
            {
                // Np. "1230" → "12:30"
                return value.Substring(0, 2) + ":" + value.Substring(2);
            }
            return value;
        }



        private int selectedRowIndex = -1; // Dodaj zmienną do przechowywania indeksu zaznaczonego wiersza

        // Metoda do przesuwania wiersza w górę
        // Metoda do przesuwania wiersza w górę
        private void MoveRowUp()
        {
            int rowIndex = dataGridView1.CurrentCell.RowIndex;
            if (rowIndex > 0)
            {
                DataTable table = (DataTable)dataGridView1.DataSource;
                DataRow row = table.NewRow();
                row.ItemArray = table.Rows[rowIndex].ItemArray;
                table.Rows.RemoveAt(rowIndex);
                table.Rows.InsertAt(row, rowIndex - 1);
                RefreshNumeration();
                dataGridView1.Rows[rowIndex - 1].Selected = true; // Zaznacz przesunięty wiersz
                dataGridView1.Rows[rowIndex].Selected = false; // Odznacz poprzedni zaznaczony wiersz
                dataGridView1.CurrentCell = dataGridView1.Rows[rowIndex - 1].Cells[0]; // Ustawienie aktywnej komórki na pierwszą kolumnę przesuniętego wiersza
                selectedRowIndex = rowIndex - 1; // Zaktualizuj indeks zaznaczonego wiersza
            }
        }

        // Metoda do przesuwania wiersza w dół
        private void MoveRowDown()
        {
            int rowIndex = dataGridView1.CurrentCell.RowIndex;
            if (rowIndex < dataGridView1.Rows.Count - 1)
            {
                DataTable table = (DataTable)dataGridView1.DataSource;
                DataRow row = table.NewRow();
                row.ItemArray = table.Rows[rowIndex].ItemArray;
                table.Rows.RemoveAt(rowIndex);
                table.Rows.InsertAt(row, rowIndex + 1);
                RefreshNumeration();
                dataGridView1.Rows[rowIndex + 1].Selected = true; // Zaznacz przesunięty wiersz
                dataGridView1.Rows[rowIndex].Selected = false; // Odznacz poprzedni zaznaczony wiersz
                dataGridView1.CurrentCell = dataGridView1.Rows[rowIndex + 1].Cells[0]; // Ustawienie aktywnej komórki na pierwszą kolumnę przesuniętego wiersza
                selectedRowIndex = rowIndex + 1; // Zaktualizuj indeks zaznaczonego wiersza
            }
        }
        // Metoda odświeżająca numerację wierszy
        private void RefreshNumeration()
        {
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
            {
                dataGridView1.Rows[i].Cells["NrAuta"].Value = i + 1;
            }
        }
        private void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
            DisplayData();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            MoveRowUp();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            MoveRowDown();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Rozpoczęcie transakcji
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            foreach (DataGridViewRow row in dataGridView1.Rows)
                            {
                                if (!row.IsNewRow)
                                {
                                    // Tworzymy polecenie SQL w transakcji
                                    string sql = @"INSERT INTO dbo.FarmerCalc 
                                        (ID, CalcDate, CustomerGID, CustomerRealGID, DriverGID, CarLp, SztPoj, WagaDek, 
                                         CarID, TrailerID, NotkaWozek, LpDostawy, Wyjazd, Zaladunek, Przyjazd, Price, 
                                         Loss, PriceTypeID) 
                                        VALUES 
                                        (@ID, @Date, @Dostawca, @Dostawca, @Kierowca, @Nr, @SztPoj, @WagaDek, 
                                         @Ciagnik, @Naczepa, @NotkaWozek, @LpDostawy, @Wyjazd, @Zaladunek, 
                                         @Przyjazd, @Cena, @Ubytek, @TypCeny)";

                                    // Pobierz dane z wiersza DataGridView
                                    string Dostawca = row.Cells["CustomerGID"].Value.ToString();
                                    string Kierowca = row.Cells["DriverGID"].Value.ToString();
                                    string LpDostawy = row.Cells["CarLp"].Value.ToString();
                                    string Nr = row.Cells["CarLp"].Value.ToString();
                                    string SztPoj = row.Cells["SztPoj"].Value.ToString();
                                    string WagaDek = row.Cells["WagaDek"].Value.ToString();
                                    string Ciagnik = row.Cells["CarID"].Value.ToString();
                                    string Naczepa = row.Cells["TrailerID"].Value.ToString();
                                    string NotkaWozek = row.Cells["NotkaWozek"].Value.ToString();

                                    string StringPrzyjazd = row.Cells["Przyjazd"].Value.ToString();
                                    string StringZaladunek = row.Cells["Zaladunek"].Value.ToString();
                                    string StringWyjazd = row.Cells["Wyjazd"].Value.ToString();

                                    double Ubytek;
                                    if (!double.TryParse(zapytaniasql.PobierzInformacjeZBazyDanychKonkretne(LpDostawy, "Ubytek"), out Ubytek))
                                        Ubytek = 0.0;
                                    double Cena;
                                    if (!double.TryParse(zapytaniasql.PobierzInformacjeZBazyDanychKonkretne(LpDostawy, "Cena"), out Cena))
                                        Cena = 0.0;
                                    string typCeny = zapytaniasql.PobierzInformacjeZBazyDanychKonkretne(LpDostawy, "TypCeny");
                                    int intTypCeny = zapytaniasql.ZnajdzIdCeny(typCeny);

                                    // Znajdź ID kierowcy i dostawcy
                                    int userId = zapytaniasql.ZnajdzIdKierowcy(Kierowca);
                                    int userId2 = zapytaniasql.ZnajdzIdHodowcy(Dostawca);

                                    // Dodaj dwukropek do formy czasu
                                    StringWyjazd = zapytaniasql.DodajDwukropek(StringWyjazd);
                                    StringZaladunek = zapytaniasql.DodajDwukropek(StringZaladunek);
                                    StringPrzyjazd = zapytaniasql.DodajDwukropek(StringPrzyjazd);

                                    DateTime data = dateTimePicker1.Value;
                                    DateTime combinedDateTimeWyjazd = ZapytaniaSQL.CombineDateAndTime(StringWyjazd, data);
                                    DateTime combinedDateTimeZaladunek = ZapytaniaSQL.CombineDateAndTime(StringZaladunek, data);
                                    DateTime combinedDateTimePrzyjazd = ZapytaniaSQL.CombineDateAndTime(StringPrzyjazd, data);

                                    // Znajdź największe ID w tabeli FarmerCalc
                                    long maxLP;
                                    string maxLPSql = "SELECT MAX(ID) AS MaxLP FROM dbo.[FarmerCalc];";
                                    using (SqlCommand command = new SqlCommand(maxLPSql, conn, transaction))
                                    {
                                        object result = command.ExecuteScalar();
                                        maxLP = result == DBNull.Value ? 1 : Convert.ToInt64(result) + 1;
                                    }

                                    // Wstaw dane do tabeli FarmerCalc
                                    using (SqlCommand cmd = new SqlCommand(sql, conn, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@ID", maxLP);
                                        cmd.Parameters.AddWithValue("@Dostawca", userId2);
                                        cmd.Parameters.AddWithValue("@Kierowca", userId);
                                        cmd.Parameters.AddWithValue("@LpDostawy", string.IsNullOrEmpty(LpDostawy)
                                            ? (object)DBNull.Value
                                            : LpDostawy);
                                        cmd.Parameters.AddWithValue("@Nr", string.IsNullOrEmpty(Nr)
                                            ? (object)DBNull.Value
                                            : Nr);
                                        cmd.Parameters.AddWithValue("@SztPoj", string.IsNullOrEmpty(SztPoj)
                                            ? (object)DBNull.Value
                                            : decimal.Parse(SztPoj));
                                        cmd.Parameters.AddWithValue("@WagaDek", string.IsNullOrEmpty(WagaDek)
                                            ? (object)DBNull.Value
                                            : decimal.Parse(WagaDek));
                                        cmd.Parameters.AddWithValue("@Date", dateTimePicker1.Value.Date);

                                        cmd.Parameters.AddWithValue("@Wyjazd", combinedDateTimeWyjazd);
                                        cmd.Parameters.AddWithValue("@Zaladunek", combinedDateTimeZaladunek);
                                        cmd.Parameters.AddWithValue("@Przyjazd", combinedDateTimePrzyjazd);

                                        cmd.Parameters.AddWithValue("@Cena", Cena);
                                        cmd.Parameters.AddWithValue("@Ubytek", Ubytek);
                                        cmd.Parameters.AddWithValue("@TypCeny", intTypCeny);

                                        cmd.Parameters.AddWithValue("@Ciagnik", string.IsNullOrEmpty(Ciagnik)
                                            ? (object)DBNull.Value
                                            : Ciagnik);
                                        cmd.Parameters.AddWithValue("@Naczepa", string.IsNullOrEmpty(Naczepa)
                                            ? (object)DBNull.Value
                                            : Naczepa);
                                        cmd.Parameters.AddWithValue("@NotkaWozek", string.IsNullOrEmpty(NotkaWozek)
                                            ? (object)DBNull.Value
                                            : NotkaWozek);

                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            // Jeśli wszystko OK – zatwierdzamy zmiany
                            transaction.Commit();
                            MessageBox.Show("Pomyślnie dodano dane do bazy.",
                                            "Sukces",
                                            MessageBoxButtons.OK,
                                            MessageBoxIcon.Information);
                        }
                        catch (Exception ex)
                        {
                            // Jeśli coś pójdzie nie tak – wycofujemy zmiany
                            transaction.Rollback();
                            MessageBox.Show($"Wystąpił błąd podczas dodawania danych:\n{ex.Message}",
                                            "Błąd",
                                            MessageBoxButtons.OK,
                                            MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Ten catch złapie błędy z poziomu np. otwarcia połączenia
                MessageBox.Show($"Wystąpił błąd aplikacji lub połączenia z bazą:\n{ex.Message}",
                                "Błąd",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }
        }
    }
}
