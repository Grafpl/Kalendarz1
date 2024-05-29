using System;
using System.Data;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using System.Drawing; // Dodaj tę dyrektywę
using System.Globalization;
using System.Collections.Generic;


namespace Kalendarz1
{
    public partial class WidokKalendarza : Form
    {
        private string GID;
        private string lpDostawa;
        static string connectionPermission = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private int selectedRowIndex = -1; // Zmienna do przechowywania indeksu zaznaczonego wiersza
        private Timer timer;
        public string UserID { get; set; }
        private MojeObliczenia obliczenia = new MojeObliczenia();
        private NazwaZiD nazwaZiD = new NazwaZiD();
        private CenoweMetody CenoweMetody = new CenoweMetody();
        private static ZapytaniaSQL zapytaniasql = new ZapytaniaSQL();
        public WidokKalendarza()
        {
            InitializeComponent();
            this.Load += WidokKalendarza_Load;
            dataGridView1.CellDoubleClick += DataGridView1_CellDoubleClick;
            SetupStatus(); FillComboBox(); PokazCeny();
            checkBoxAnulowane.CheckedChanged += CheckBoxAnulowane_CheckedChanged; // Dodajemy obsługę zdarzenia CheckedChanged
            checkBoxSprzedane.CheckedChanged += CheckBoxSprzedane_CheckedChanged; // Dodajemy obsługę zdarzenia CheckedChanged
            checkBoxDoWykupienia.CheckedChanged += CheckBoxDoWykupienia_CheckedChanged; // Dodajemy obsługę zdarzenia CheckedChanged
            dataGridView1.CellClick += DataGridView1_CellClick; // Dodaj subskrypcję zdarzenia CellClick
            MyCalendar.SelectionStart = DateTime.Today;
            MyCalendar.SelectionEnd = DateTime.Today;
            MyCalendar_DateChanged_1(this, new DateRangeEventArgs(DateTime.Today, DateTime.Today));
            checkBoxDoWykupienia.Checked = true;
            // Inicjalizacja timera
            timer = new Timer();
            timer.Interval = 60000; // Interwał w milisekundach (tu: co 5 sekund)
            timer.Tick += Timer_Tick; // Przypisanie zdarzenia
            timer.Start(); // Rozpoczęcie pracy timera
        }
        // Metoda wywoływana podczas ładowania formularza
        private void Timer_Tick(object sender, EventArgs e)
        {
            // Ta metoda zostanie wywołana co określony interwał czasowy
            PokazCeny();
            MyCalendar_DateChanged_1(this, new DateRangeEventArgs(DateTime.Today, DateTime.Today));
            BiezacePartie();

        }
        private void BiezacePartie()
        {
            // Utwórz połączenie z bazą danych SQL Server
            using (SqlConnection cnn = new SqlConnection(connectionPermission))
            {
                cnn.Open();

                string strSQL = @"
                                SELECT 
                                    k.CreateData AS Data, 
                                    RIGHT(CONVERT(VARCHAR(10), k.P1), 3) AS Partia, 
                                    Partia.CustomerName AS Dostawca, 
                                    AVG(k.QntInCont) AS Srednia, 
                                    CAST(AVG(CAST(k.QntInCont AS decimal(18, 2))) AS decimal(18, 2)) AS SredniaDokładna,
                                    CONVERT(decimal(18, 2), 15.0 / CAST(AVG(CAST(k.QntInCont AS decimal(18, 2))) AS decimal(18, 2))) AS SredniaTuszka,
                                    CONVERT(decimal(18, 2), (15.0 / CAST(AVG(CAST(k.QntInCont AS decimal(18, 2))) AS decimal(18, 2))) * 1.22) AS SredniaZywy,
                                    hd.WagaDek AS WagaDek,
                                    CONVERT(decimal(18, 2), ((15.0 / CAST(AVG(CAST(k.QntInCont AS decimal(18, 2))) AS decimal(18, 2))) * 1.22) - hd.WagaDek) AS roznica
                                FROM [LibraNet].[dbo].[In0E] K
                                JOIN [LibraNet].[dbo].[PartiaDostawca] Partia ON K.P1 = Partia.Partia
                                LEFT JOIN [LibraNet].[dbo].[HarmonogramDostaw] hd ON k.CreateData = hd.DataOdbioru AND Partia.CustomerName = hd.Dostawca
                                WHERE ArticleID = 40 
                                    AND k.QntInCont > 4
                                    AND CONVERT(date, k.CreateData) = CONVERT(date, GETDATE()) -- Dzisiejsza data
                                GROUP BY k.CreateData, k.P1, Partia.CustomerName, hd.WagaDek
                                ORDER BY k.P1 DESC, k.CreateData DESC;
                            ";

                using (SqlCommand command2 = new SqlCommand(strSQL, cnn))
                {
                    using (SqlDataReader reader = command2.ExecuteReader())
                    {
                        try
                        {
                            // Przygotowanie DataGridWstawienia
                            dataGridPartie.Rows.Clear();
                            dataGridPartie.Columns.Clear();
                            dataGridPartie.RowHeadersVisible = false;
                            // Dodaj kolumny do DataGridWstawienia
                            dataGridPartie.Columns.Add("DataKolumna2", "Data");
                            dataGridPartie.Columns.Add("PartiaKolumna", "Partia");
                            dataGridPartie.Columns.Add("DostawcaKolumna2", "Dostawca");
                            dataGridPartie.Columns.Add("WagaDek2Kolumna", "Waga");
                            dataGridPartie.Columns.Add("SredniaDokładnaKolumna", "Srednia");
                            dataGridPartie.Columns.Add("SredniaTuszkaKolumna", "Tuszka");
                            dataGridPartie.Columns.Add("SredniaZywyKolumna", "Waga zywiec");
                            dataGridPartie.Columns.Add("WagaDekKolumna", "WagaDek");
                            dataGridPartie.Columns.Add("roznicaKolumna", "roznica");

                            dataGridPartie.Columns["DataKolumna2"].Visible = false;
                            dataGridPartie.Columns["SredniaTuszkaKolumna"].Visible = false;

                            dataGridPartie.Columns["PartiaKolumna"].Width = 30;
                            dataGridPartie.Columns["DostawcaKolumna2"].Width = 95;
                            dataGridPartie.Columns["WagaDek2Kolumna"].Width = 50;
                            dataGridPartie.Columns["SredniaDokładnaKolumna"].Width = 60;
                            dataGridPartie.Columns["SredniaTuszkaKolumna"].Width = 50;
                            dataGridPartie.Columns["SredniaZywyKolumna"].Width = 50;
                            dataGridPartie.Columns["WagaDekKolumna"].Width = 50;
                            dataGridPartie.Columns["roznicaKolumna"].Width = 50;

                            dataGridPartie.ColumnHeadersVisible = false;


                            while (reader.Read())
                            {
                                // Dodaj nowy wiersz do DataGridWstawienia
                                DataGridViewRow newRow = new DataGridViewRow();
                                newRow.CreateCells(dataGridPartie);
                                newRow.Cells[0].Value = reader["Data"];
                                newRow.Cells[1].Value = reader["Partia"];
                                newRow.Cells[2].Value = reader["Dostawca"];
                                newRow.Cells[3].Value = reader["Srednia"] + " poj";
                                newRow.Cells[4].Value = reader["SredniaDokładna"] + " poj";
                                newRow.Cells[5].Value = reader["SredniaTuszka"] + " kg";
                                newRow.Cells[6].Value = reader["SredniaZywy"] + " kg";
                                newRow.Cells[7].Value = reader["roznica"] + " kg";


                                // Dodaj nowy wiersz do kontrolki DataGridWstawienia
                                dataGridPartie.Rows.Add(newRow);
                                foreach (DataGridViewRow row in dataGridPartie.Rows)
                                {
                                    row.Height = 20; // Ustawienie wysokości każdego wiersza na 50 pikseli
                                }
                                // Ustaw czcionkę dla nowo dodanego wiersza
                                foreach (DataGridViewCell cell in newRow.Cells)
                                {
                                    cell.Style.Font = new Font("Arial", 8); // Ustawienie czcionki Arial o rozmiarze 33 punktów
                                }
                            }


                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Błąd odczytu danych: " + ex.Message);
                        }
                    }
                }
            }
        }
        private void WidokKalendarza_Load(object sender, EventArgs e)
        {
            BiezacePartie();
            NazwaZiD databaseManager = new NazwaZiD();
            string name = databaseManager.GetNameById(UserID);
            // Przypisanie wartości UserID do TextBoxa userTextbox
            userTextbox.Text = name;
        }
        private void CheckBoxAnulowane_CheckedChanged(object sender, EventArgs e)
        {
            // Wywołujemy ponownie metodę do pobrania danych, z uwzględnieniem stanu checkboxa
            MyCalendar_DateChanged_1(sender, null);
        }
        private void CheckBoxSprzedane_CheckedChanged(object sender, EventArgs e)
        {
            // Wywołujemy ponownie metodę do pobrania danych, z uwzględnieniem stanu checkboxa
            MyCalendar_DateChanged_1(sender, null);
        }
        private void CheckBoxDoWykupienia_CheckedChanged(object sender, EventArgs e)
        {
            // Wywołujemy ponownie metodę do pobrania danych, z uwzględnieniem stanu checkboxa
            MyCalendar_DateChanged_1(sender, null);
        }
        private void DataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            /* Sprawdź, czy wiersz i kolumna zostały kliknięte
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                // Pobierz wartość LP z klikniętego wiersza
                lpDostawa = dataGridView1.Rows[e.RowIndex].Cells["LP"].Value.ToString();

                // Wywołaj nowe okno Dostawa, przekazując wartość LP
                Dostawa dostawaForm = new Dostawa(lpDostawa);
                dostawaForm.Show();
            }*/
        }
        private void DataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            dataWstawienia.Value = DateTime.Today;
            LpWstawienia.Text = "0";
            lpDostawa = "0";
            DataGridWstawienia.Rows.Clear();

            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                selectedRowIndex = e.RowIndex;
                object wartoscKomorki = dataGridView1.Rows[e.RowIndex].Cells["LP"].Value;
                lpDostawa = wartoscKomorki != null ? wartoscKomorki.ToString() : "0";
                PobierzInformacjeZBazyDanych(lpDostawa);
            }
        }
        private void buttonUpDate_Click(object sender, EventArgs e)
        {
            ZmienDate(lpDostawa, 1); // Zwiększenie daty o jeden dzień
            MyCalendar_DateChanged_1(sender, null);
        }

        private void buttonDownDate_Click(object sender, EventArgs e)
        {
            ZmienDate(lpDostawa, -1); // Zmniejszenie daty o jeden dzień
            MyCalendar_DateChanged_1(sender, null);
        }
        private void ZmienDate(string lpDostawa, int dni)
        {
            if (lpDostawa == "0")
            {
                MessageBox.Show("Nie wybrano poprawnego wiersza.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }


            using (SqlConnection connection = new SqlConnection(connectionPermission))
            {
                connection.Open();
                string query = "UPDATE [LibraNet].[dbo].[HarmonogramDostaw] SET DataOdbioru = DATEADD(day, @dni, DataOdbioru) WHERE LP = @lpDostawa";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@dni", dni);
                    command.Parameters.AddWithValue("@lpDostawa", lpDostawa);

                    int rowsAffected = command.ExecuteNonQuery();
                    if (rowsAffected > 0)
                    {

                        // Po aktualizacji, odśwież dane w DataGridView
                        PobierzInformacjeZBazyDanych(lpDostawa);

                        // Znajdź nowy indeks wiersza z wartością lpDostawa
                        for (int i = 0; i < dataGridView1.Rows.Count; i++)
                        {
                            if (dataGridView1.Rows[i].Cells["LP"].Value != null &&
                                dataGridView1.Rows[i].Cells["LP"].Value.ToString() == lpDostawa)
                            {
                                selectedRowIndex = i;
                                break;
                            }
                        }

                        // Zaznacz wiersz o nowym indeksie i ustaw go jako pierwszy wyświetlany
                        if (selectedRowIndex >= 0 && selectedRowIndex < dataGridView1.Rows.Count)
                        {
                            dataGridView1.Rows[selectedRowIndex].Selected = true;
                            dataGridView1.FirstDisplayedScrollingRowIndex = selectedRowIndex;
                        }
                    }
                    else
                    {
                        MessageBox.Show("Nie udało się zaktualizować daty.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void MyCalendar_DateChanged_1(object sender, DateRangeEventArgs e)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionPermission))
                {
                    connection.Open();

                    // Oblicz numer tygodnia na podstawie zaznaczonej daty
                    DateTime selectedDateCalendar = MyCalendar.SelectionStart; // Zmiana nazwy zmiennej

                    int weekNumber = GetIso8601WeekOfYear(selectedDateCalendar);

                    // Wyświetl numer tygodnia w polu tekstowym
                    weekNumberTextBox.Text = weekNumber.ToString();

                    DateTime selectedDate = MyCalendar.SelectionStart;
                    DateTime startOfWeek = selectedDate.AddDays(-(int)selectedDate.DayOfWeek);
                    DateTime endOfWeek = startOfWeek.AddDays(7);

                    string strSQL = $@"
                        SELECT DISTINCT HD.LP, 
                                        HD.DataOdbioru, 
                                        HD.Dostawca, 
                                        HD.Auta, 
                                        HD.SztukiDek, 
                                        HD.WagaDek, 
                                        HD.bufor, 
                                        HD.TypCeny, 
                                        HD.Cena, 
                                        WK.DataWstawienia,
                                        D.Distance,
                                        HD.UWAGI
                        FROM HarmonogramDostaw HD
                        LEFT JOIN WstawieniaKurczakow WK ON HD.LpW = WK.Lp
                        LEFT JOIN [LibraNet].[dbo].[Dostawcy] D ON HD.Dostawca = D.Name
                        WHERE HD.DataOdbioru >= @startDate AND HD.DataOdbioru <= @endDate AND D.Halt = '0'";

                    if (!checkBoxAnulowane.Checked)
                    {
                        strSQL += " AND bufor != 'Anulowany'";
                    }
                    if (!checkBoxSprzedane.Checked)
                    {
                        strSQL += " AND bufor != 'Sprzedany'";
                    }
                    if (!checkBoxDoWykupienia.Checked)
                    {
                        strSQL += " AND bufor != 'Do Wykupienia'";
                    }


                    strSQL += " ORDER BY HD.DataOdbioru, HD.bufor, HD.WagaDek Desc";

                    using (SqlCommand command = new SqlCommand(strSQL, connection))
                    {
                        command.Parameters.AddWithValue("@startDate", startOfWeek);
                        command.Parameters.AddWithValue("@endDate", endOfWeek);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            dataGridView1.RowHeadersVisible = false;
                            dataGridView1.Rows.Clear();
                            dataGridView1.Columns.Clear();

                            dataGridView1.Columns.Add("LP", "LP");
                            dataGridView1.Columns.Add("DataOdbioruKolumna", "Data");
                            dataGridView1.Columns.Add("DostawcaKolumna", "Dostawca");
                            dataGridView1.Columns.Add("AutaKolumna", "A");

                            dataGridView1.Columns.Add("SztukiDekKolumna", "Sztuki");
                            dataGridView1.Columns.Add("WagaDek", "Waga");
                            dataGridView1.Columns.Add("bufor", "Status");
                            dataGridView1.Columns.Add("RóżnicaDni", "Doby");
                            dataGridView1.Columns.Add("TypCenyKolumna", "Typ Ceny");
                            dataGridView1.Columns.Add("CenaKolumna", "Cena");
                            dataGridView1.Columns.Add("KmKolumna", "KM");
                            dataGridView1.Columns.Add("UwagaKolumna", "Uwagi");

                            if (!checkBoxCena.Checked)
                            {
                                dataGridView1.Columns["CenaKolumna"].Visible = true;

                            }
                            else
                            {
                                dataGridView1.Columns["CenaKolumna"].Visible = false;

                            }

                            dataGridView1.Columns["LP"].Visible = false;
                            dataGridView1.Columns["DataOdbioruKolumna"].Visible = false;
                            dataGridView1.Columns["bufor"].Visible = false;

                            // Ustawienie szerokości kolumn
                            dataGridView1.Columns["LP"].Width = 50;
                            dataGridView1.Columns["DataOdbioruKolumna"].Width = 100;
                            dataGridView1.Columns["DostawcaKolumna"].Width = 150;
                            dataGridView1.Columns["AutaKolumna"].Width = 25;
                            dataGridView1.Columns["SztukiDekKolumna"].Width = 70;
                            dataGridView1.Columns["WagaDek"].Width = 50;
                            dataGridView1.Columns["bufor"].Width = 85;
                            dataGridView1.Columns["RóżnicaDni"].Width = 43;
                            dataGridView1.Columns["TypCenyKolumna"].Width = 70;
                            dataGridView1.Columns["CenaKolumna"].Width = 50;
                            dataGridView1.Columns["KmKolumna"].Width = 60;
                            dataGridView1.Columns["UwagaKolumna"].Width = 600;

                            DataGridViewCheckBoxColumn confirmColumn = new DataGridViewCheckBoxColumn();
                            confirmColumn.HeaderText = "V";
                            confirmColumn.Name = "ConfirmColumn";
                            confirmColumn.Width = 80;
                            dataGridView1.Columns.Add(confirmColumn);
                            dataGridView1.Columns["ConfirmColumn"].Width = 35;

                            foreach (DataGridViewColumn column in dataGridView1.Columns)
                            {
                                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                                column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
                            }

                            if (!checkBoxNotatki.Checked)
                            {
                                dataGridView1.Columns["UwagaKolumna"].Visible = false;
                                dataGridView1.Width = 612;
                                groupBox1.Location = new Point(695, 180);
                                CommandButton_Update.Location = new Point(618, 258);
                                button1.Location = new Point(612, 221);
                                button5.Location = new Point(653, 221);
                            }
                            else
                            {
                                dataGridView1.Columns["UwagaKolumna"].Visible = true;
                                dataGridView1.Width = 1212;
                                groupBox1.Location = new Point(1355, 180);
                                CommandButton_Update.Location = new Point(1228, 258);
                                button1.Location = new Point(1228, 250);
                                button5.Location = new Point(1228, 221);
                            }

                            DateTime? currentDate = null;
                            DataGridViewRow currentGroupRow = null;
                            double sumaAuta = 0;
                            double sumaSztukiDek = 0;
                            double sumaWagaDek = 0;


                            int count = 0;
                            bool isFirstRow = true;

                            double sumaWagaDekPomnozona = 0;
                            double sumaCenaPomnozona = 0;
                            double sumaKMPomnozona = 0;
                            double sumaTypCenyKolumnaPomnozona = 0;

                            while (reader.Read())
                            {
                                DateTime date = reader.GetDateTime(reader.GetOrdinal("DataOdbioru"));
                                string formattedDate = date.ToString("yyyy-MM-dd dddd");

                                if (currentDate != date)
                                {
                                    if (!isFirstRow)
                                    {
                                        dataGridView1.Rows.Add();
                                    }
                                    else
                                    {
                                        isFirstRow = false;
                                    }

                                    if (currentGroupRow != null)
                                    {
                                        currentGroupRow.Cells["AutaKolumna"].Value = sumaAuta.ToString();
                                        currentGroupRow.Cells["AutaKolumna"].Value = sumaAuta.ToString();
                                        currentGroupRow.Cells["SztukiDekKolumna"].Value = sumaSztukiDek.ToString("N0") + " szt";
                                        if (sumaAuta != 0)
                                        {
                                            double sredniaWagaDek = sumaWagaDekPomnozona / sumaAuta;
                                            currentGroupRow.Cells["WagaDek"].Value = sredniaWagaDek.ToString("0.00") + " kg";

                                            double sredniaCena = sumaCenaPomnozona / sumaAuta;
                                            currentGroupRow.Cells["CenaKolumna"].Value = sredniaCena.ToString("0.00") + " zł";

                                            double sredniaKM = sumaKMPomnozona / sumaAuta;
                                            currentGroupRow.Cells["KmKolumna"].Value = sredniaKM.ToString("0") + " KM";

                                            currentGroupRow.Cells["RóżnicaDni"].Value = sumaTypCenyKolumnaPomnozona.ToString("0") + " ub";
                                        }
                                    }

                                    currentGroupRow = new DataGridViewRow();
                                    currentGroupRow.CreateCells(dataGridView1);
                                    if (!isFirstRow)
                                    {
                                        currentGroupRow.Cells[2].Value = formattedDate;
                                    }
                                    dataGridView1.Rows.Add(currentGroupRow);

                                    currentDate = date;

                                    sumaAuta = 0;
                                    sumaSztukiDek = 0;
                                    sumaWagaDek = 0;
                                    sumaWagaDekPomnozona = 0;
                                    sumaCenaPomnozona = 0;
                                    sumaKMPomnozona = 0;
                                    sumaTypCenyKolumnaPomnozona = 0;
                                    count = 0;
                                }

                                DataGridViewRow row = new DataGridViewRow();
                                row.CreateCells(dataGridView1);
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row.Cells[i].Value = reader.GetValue(i);
                                    if (dataGridView1.Columns[i].Name == "AutaKolumna" && reader["Auta"] != DBNull.Value)
                                    {
                                        double auta = Convert.ToDouble(reader["Auta"]);
                                        sumaAuta += auta;
                                    }
                                    else if (dataGridView1.Columns[i].Name == "SztukiDekKolumna" && reader["SztukiDek"] != DBNull.Value)
                                    {
                                        sumaSztukiDek += Convert.ToDouble(reader["SztukiDek"]);
                                    }
                                    else if (dataGridView1.Columns[i].Name == "WagaDek" && reader["WagaDek"] != DBNull.Value)
                                    {
                                        double wagaDek = Convert.ToDouble(reader["WagaDek"]);
                                        sumaWagaDek += wagaDek;
                                        if (reader["Auta"] != DBNull.Value)
                                        {
                                            double auta = Convert.ToDouble(reader["Auta"]);
                                            sumaWagaDekPomnozona += wagaDek * auta;
                                            count += (int)auta;
                                        }
                                    }
                                    else if (dataGridView1.Columns[i].Name == "CenaKolumna" && reader["Cena"] != DBNull.Value)
                                    {
                                        double cena = Convert.ToDouble(reader["Cena"]);
                                        if (reader["Auta"] != DBNull.Value)
                                        {
                                            double auta = Convert.ToDouble(reader["Auta"]);
                                            sumaCenaPomnozona += cena * auta;
                                        }
                                    }
                                    else if (dataGridView1.Columns[i].Name == "KmKolumna" && reader["Distance"] != DBNull.Value)
                                    {
                                        double KM = Convert.ToDouble(reader["Distance"]);
                                        if (reader["Auta"] != DBNull.Value)
                                        {
                                            double auta = Convert.ToDouble(reader["Auta"]);
                                            sumaKMPomnozona += KM * auta;
                                        }
                                    }
                                    else if (dataGridView1.Columns[i].Name == "RóżnicaDni" && reader["WagaDek"] != DBNull.Value)
                                    {
                                        double typCeny = Convert.ToDouble(reader["WagaDek"]);
                                        if (typCeny >= 0.5 && typCeny <= 2.4)
                                        {
                                            if (reader["Auta"] != DBNull.Value)
                                            {
                                                double auta = Convert.ToDouble(reader["Auta"]);
                                                sumaTypCenyKolumnaPomnozona += 1 * auta; // Liczymy liczbę wystąpień i mnożymy przez auta
                                            }
                                        }
                                    }
                                }

                                if (!isFirstRow)
                                {
                                    DataGridViewRow newRow = new DataGridViewRow();
                                    newRow.CreateCells(dataGridView1);
                                    for (int i = 0; i < reader.FieldCount; i++)
                                    {
                                        if (dataGridView1.Columns[i].Name == "DataOdbioruKolumna")
                                        {
                                            newRow.Cells[i].Value = "";
                                        }
                                        else if (dataGridView1.Columns[i].Name == "SztukiDekKolumna")
                                        {
                                            if (!Convert.IsDBNull(reader["SztukiDek"]))
                                            {
                                                newRow.Cells[i].Value = string.Format("{0:#,0} szt", Convert.ToDouble(reader["SztukiDek"]));
                                            }
                                            else
                                            {
                                                newRow.Cells[i].Value = "";
                                            }
                                        }
                                        else if (dataGridView1.Columns[i].Name == "WagaDek")
                                        {
                                            newRow.Cells[i].Value = reader["WagaDek"] + " kg";
                                        }
                                        else if (dataGridView1.Columns[i].Name == "RóżnicaDni")
                                        {
                                            DateTime dataWstawienia = reader.IsDBNull(reader.GetOrdinal("DataWstawienia")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("DataWstawienia"));
                                            DateTime dataOdbioru = reader.GetDateTime(reader.GetOrdinal("DataOdbioru"));

                                            if (dataWstawienia == DateTime.MinValue)
                                                newRow.Cells[i].Value = "-";
                                            else
                                            {
                                                int roznicaDni = (dataOdbioru - dataWstawienia).Days;
                                                newRow.Cells[i].Value = roznicaDni + " dni";
                                            }
                                        }
                                        else if (dataGridView1.Columns[i].Name == "TypCenyKolumna")
                                        {
                                            newRow.Cells[i].Value = reader["TypCeny"];
                                        }
                                        else if (dataGridView1.Columns[i].Name == "CenaKolumna")
                                        {
                                            if (!Convert.IsDBNull(reader["Cena"]))
                                            {
                                                newRow.Cells[i].Value = reader["Cena"] + " zł";
                                            }
                                            else
                                            {
                                                newRow.Cells[i].Value = "-";
                                            }
                                        }
                                        else if (dataGridView1.Columns[i].Name == "KmKolumna")
                                        {
                                            if (!Convert.IsDBNull(reader["Distance"]))
                                            {
                                                newRow.Cells[i].Value = reader["Distance"] + " km";
                                            }
                                            else
                                            {
                                                newRow.Cells[i].Value = "-";
                                            }
                                        }
                                        else
                                        {
                                            newRow.Cells[i].Value = reader.GetValue(i);
                                        }
                                    }
                                    dataGridView1.Rows.Add(newRow);
                                }
                            }

                            if (currentGroupRow != null)
                            {
                                currentGroupRow.Cells["AutaKolumna"].Value = sumaAuta.ToString();
                                currentGroupRow.Cells["SztukiDekKolumna"].Value = sumaSztukiDek.ToString("N0") + " szt";
                                if (sumaAuta != 0)
                                {
                                    double sredniaWagaDek = sumaWagaDekPomnozona / sumaAuta;
                                    currentGroupRow.Cells["WagaDek"].Value = sredniaWagaDek.ToString("0.00") + " kg";

                                    double sredniaCena = sumaCenaPomnozona / sumaAuta;
                                    currentGroupRow.Cells["CenaKolumna"].Value = sredniaCena.ToString("0.00") + " zł";

                                    double sredniaKM = sumaKMPomnozona / sumaAuta;
                                    currentGroupRow.Cells["KmKolumna"].Value = sredniaKM.ToString("0") + " KM";

                                    currentGroupRow.Cells["RóżnicaDni"].Value = sumaTypCenyKolumnaPomnozona.ToString("0") + " ub";
                                }
                            }




                            UstawStanCheckboxow();
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Błąd połączenia z bazą danych: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
            {
                FormatujWierszeZgodnieZStatus(i);
            }
        }
        private void UstawStanCheckboxow()
        {
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                string statusValue = row.Cells["bufor"].Value?.ToString();

                if (statusValue != null && statusValue.Equals("Potwierdzony"))
                {
                    row.Cells["ConfirmColumn"].Value = true;
                }
                else
                {
                    row.Cells["ConfirmColumn"].Value = false;
                }
            }
        }
        // Obsługa zdarzenia zmiany stanu checkboxa
        private void PokazCeny()
        {

            // Pokazanie ceny Rolniczej
            double cenaRolnicza = CenoweMetody.PobierzCeneRolniczaDzisiaj();
            if (cenaRolnicza > 0)
            {
                textRolnicza.Text = cenaRolnicza.ToString("F2"); // Formatowanie do dwóch miejsc po przecinku
            }

            // Pokazanie ceny Ministerialnej
            double cenaMinisterialna = CenoweMetody.PobierzCeneMinisterialnaDzisiaj();
            if (cenaMinisterialna > 0)
            {
                textMinister.Text = cenaMinisterialna.ToString("F2"); // Formatowanie do dwóch miejsc po przecinku
            }

            double cenaLaczona = (cenaMinisterialna + cenaRolnicza) / 2;
            if (cenaMinisterialna > 0)
            {
                textLaczona.Text = cenaLaczona.ToString("F2"); // Formatowanie do dwóch miejsc po przecinku
            }

            double cenaTuszki = CenoweMetody.PobierzCeneKurczakaA();
            if (cenaTuszki > 0)
            {
                textTuszki.Text = cenaTuszki.ToString("F2"); // Formatowanie do dwóch miejsc po przecinku
            }

            double cenaRolniczaPrzebitka = (cenaTuszki - cenaRolnicza);
            if (cenaRolniczaPrzebitka > 0)
            {
                textRolniczaPrzebitka.Text = cenaRolniczaPrzebitka.ToString("F2"); // Formatowanie do dwóch miejsc po przecinku
            }


            double cenaMinisterPrzebitka = (cenaTuszki - cenaMinisterialna);
            if (cenaMinisterialna > 0)
            {
                textMinisterPrzebitka.Text = cenaMinisterPrzebitka.ToString("F2"); // Formatowanie do dwóch miejsc po przecinku
            }

            double cenaLaczonaPrzebitka = (cenaTuszki - cenaLaczona);
            if (cenaLaczonaPrzebitka > 0)
            {
                textLaczonaPrzebitka.Text = cenaLaczonaPrzebitka.ToString("F2"); // Formatowanie do dwóch miejsc po przecinku
            }
        }
        private void DataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == dataGridView1.Columns["ConfirmColumn"].Index)
            {
                DataGridViewCheckBoxCell checkboxCell = (DataGridViewCheckBoxCell)dataGridView1.Rows[e.RowIndex].Cells["ConfirmColumn"];
                bool isChecked = !(bool)checkboxCell.Value; // Odwróć stan checkboxa

                string lp = dataGridView1.Rows[e.RowIndex].Cells["LP"].Value?.ToString(); // Dodaj obsługę null-ów za pomocą "?"
                if (lp != null)
                {
                    string status = isChecked ? "Potwierdzony" : "Niepotwierdzony"; // Ustaw status w zależności od stanu checkboxa

                    // Zaktualizuj wartość bufora w bazie danych
                    UpdateBufferStatus(lp, status);

                    // Zaktualizuj stan checkboxa w interfejsie użytkownika
                    dataGridView1.Rows[e.RowIndex].Cells["ConfirmColumn"].Value = isChecked;
                    MyCalendar_DateChanged_1(this, new DateRangeEventArgs(DateTime.Today, DateTime.Today));
                }
            }
        }
        // Metoda aktualizacji statusu bufora w bazie danych
        private void UpdateBufferStatus(string lp, string status)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionPermission))
                {
                    connection.Open();
                    string strSQL = "UPDATE HarmonogramDostaw SET bufor = @status WHERE LP = @lp";

                    using (SqlCommand command = new SqlCommand(strSQL, connection))
                    {
                        command.Parameters.AddWithValue("@lp", lp);
                        command.Parameters.AddWithValue("@status", status);

                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Błąd połączenia z bazą danych: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        // Metoda obsługująca formatowanie komórek w DataGridView
        private void dataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            FormatujWierszeZgodnieZStatus(e.RowIndex);
        }
        private int GetIso8601WeekOfYear(DateTime time)
        {
            // Algorytm obliczający numer tygodnia w roku zgodnie z ISO 8601
            // Możesz zaimplementować własny lub skorzystać z dostępnych bibliotek

            // Przykładowa implementacja
            var cal = System.Globalization.CultureInfo.CurrentCulture.Calendar;
            int week = cal.GetWeekOfYear(time, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            return week;
        }
        // Metoda formatująca wiersze zgodnie ze statusem
        public void FormatujWierszeZgodnieZStatus(int rowIndex)
        {
            if (rowIndex >= 0)
            {
                DateTime parsedDate;
                var dostawcaCell = dataGridView1.Rows[rowIndex].Cells["DostawcaKolumna"];
                var statusCell = dataGridView1.Rows[rowIndex].Cells["Bufor"];
                if (statusCell != null && statusCell.Value != null && statusCell.Value.ToString() == "Potwierdzony")
                {
                    dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightGreen;
                    dataGridView1.Rows[rowIndex].DefaultCellStyle.Font = new Font(dataGridView1.Font.FontFamily, 9, FontStyle.Bold);
                }
                else if (statusCell != null && statusCell.Value != null && statusCell.Value.ToString() == "Anulowany")
                {
                    dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.Red;
                }
                else if (statusCell != null && statusCell.Value != null && statusCell.Value.ToString() == "Sprzedany")
                {
                    dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightBlue;
                }
                else if (statusCell != null && statusCell.Value != null && statusCell.Value.ToString() == "B.Kontr.")
                {
                    dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.Indigo;
                    dataGridView1.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.White;

                }
                else if (statusCell != null && statusCell.Value != null && statusCell.Value.ToString() == "B.Wolny.")
                {
                    dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.Yellow;
                }
                else if (statusCell != null && statusCell.Value != null && statusCell.Value.ToString() == "Do wykupienia")
                {
                    dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.WhiteSmoke;
                    dataGridView1.Rows[rowIndex].Height = 18;


                }
                else if (dostawcaCell != null && DateTime.TryParse(dostawcaCell.Value?.ToString(), out parsedDate))
                {
                    dataGridView1.Rows[rowIndex].DefaultCellStyle.Font = new Font(dataGridView1.Font.FontFamily, 9, FontStyle.Bold);
                    dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightGray;
                }
                else
                {
                    dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.White; // Domyślny kolor tła dla pozostałych wierszy
                }
            }
        }
        // Wypełnianie Textboxów
        private void LpWstawienia_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Utwórz połączenie z bazą danych SQL Server
            using (SqlConnection cnn = new SqlConnection(connectionPermission))
            {
                cnn.Open();

                // Utwórz zapytanie SQL do pobrania danych na podstawie wybranej wartości "Lp"
                string lpWstawieniaValue = LpWstawienia.Text;

                string strSQL = $"SELECT * FROM dbo.WstawieniaKurczakow WHERE Lp = @lp";

                // Wykonaj zapytanie SQL
                using (SqlCommand command = new SqlCommand(strSQL, cnn))
                {
                    command.Parameters.AddWithValue("@lp", lpWstawieniaValue);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        // Wyświetl dane w TextBoxach i ComboBoxach
                        if (reader.Read())
                        {
                            string dataWstawieniaFormatted = Convert.ToDateTime(reader["DataWstawienia"]).ToString("yyyy-MM-dd");
                            dataWstawienia.Text = dataWstawieniaFormatted;
                            sztukiWstawienia.Text = reader["IloscWstawienia"].ToString();
                        }
                    }
                }

                // Przygotowanie drugiego zapytania
                double sumaSztukWstawienia = 0;
                double sumaAutWstawienia = 0;

                strSQL = "SELECT LP, DataOdbioru, Auta, SztukiDek, WagaDek, bufor FROM [LibraNet].[dbo].[HarmonogramDostaw] WHERE LpW = @NumerWstawienia order by DataOdbioru ASC";

                double sumaSztuk = 0; // Inicjalizacja zmiennej do sumowania sztuk
                double sumaAut = 0; // Inicjalizacja zmiennej do sumowania aut

                using (SqlCommand command2 = new SqlCommand(strSQL, cnn))
                {
                    command2.Parameters.AddWithValue("@NumerWstawienia", lpWstawieniaValue);

                    using (SqlDataReader reader = command2.ExecuteReader())
                    {
                        try
                        {
                            // Przygotowanie DataGridWstawienia
                            DataGridWstawienia.Rows.Clear();
                            DataGridWstawienia.Columns.Clear();
                            DataGridWstawienia.RowHeadersVisible = false;
                            // Dodaj kolumny do DataGridWstawienia
                            DataGridWstawienia.Columns.Add("DataOdbioruKolumnaWstawienia", "Data Odbioru");
                            //DataGridWstawienia.Columns.Add("DostawcaKolumnaWstawienia", "Dostawca");
                            DataGridWstawienia.Columns.Add("AutaKolumnaWstawienia", "A");
                            DataGridWstawienia.Columns.Add("SztukiDekKolumnaWstawienia", "Sztuki");
                            DataGridWstawienia.Columns.Add("WagaDekKolumnaWstawienia", "Waga");
                            DataGridWstawienia.Columns.Add("buforkKolumnaWstawienia", "Status");

                            DataGridWstawienia.Columns["DataOdbioruKolumnaWstawienia"].Width = 100; // Szerokość kolumny "DataOdbioru" na 100 pikseli
                            //DataGridWstawienia.Columns["DostawcaKolumnaWstawienia"].Width = 150; // Szerokość kolumny "Dostawca" na 150 pikseli
                            DataGridWstawienia.Columns["AutaKolumnaWstawienia"].Width = 25; // Szerokość kolumny "Auta" na 100 pikseli
                            DataGridWstawienia.Columns["SztukiDekKolumnaWstawienia"].Width = 55; // Szerokość kolumny "SztukiDek" na 100 pikseli
                            DataGridWstawienia.Columns["WagaDekKolumnaWstawienia"].Width = 40; // Szerokość kolumny "WagaDek" na 100 pikseli
                            DataGridWstawienia.Columns["buforkKolumnaWstawienia"].Width = 70; // Szerokość kolumny "bufor" na 100 pikseli


                            while (reader.Read())
                            {
                                if (reader["bufor"].ToString() == "Potwierdzony" || reader["bufor"].ToString() == "B.Kontr." || reader["bufor"].ToString() == "B.Wolny.")
                                {
                                    // Tutaj wklej kod do formatowania daty
                                    string dataOdbioru = Convert.ToDateTime(reader["DataOdbioru"]).ToString("yyyy-MM-dd dddd");

                                    // Dodaj nowy wiersz do DataGridWstawienia
                                    DataGridViewRow newRow = new DataGridViewRow();
                                    newRow.CreateCells(DataGridWstawienia);
                                    newRow.Cells[0].Value = dataOdbioru;
                                    //newRow.Cells[1].Value = reader["Dostawca"];
                                    newRow.Cells[1].Value = reader["Auta"];
                                    newRow.Cells[2].Value = string.Format("{0:#,0}", Convert.ToDouble(reader["SztukiDek"])); // Formatowanie liczby z separatorem tysięcy
                                    newRow.Cells[3].Value = reader["WagaDek"];
                                    newRow.Cells[4].Value = reader["bufor"];

                                    // Dodaj nowy wiersz do kontrolki DataGridWstawienia
                                    DataGridWstawienia.Rows.Add(newRow);

                                    sumaAut += Convert.ToDouble(reader["Auta"]); // Dodaj wartość aut do sumy
                                    sumaSztuk += Convert.ToDouble(reader["SztukiDek"]); // Dodaj wartość sztuk do sumy
                                }
                            }
                            // Dodaj wiersz sumujący
                            DataGridViewRow sumRow = new DataGridViewRow();
                            sumRow.CreateCells(DataGridWstawienia);
                            sumRow.Cells[0].Value = "Suma"; // Tekst "Suma" w pierwszej kolumnie
                            sumRow.Cells[2].Value = string.Format("{0:#,0}", sumaSztuk); // Wartość sumy sztuk z separatorem tysięcy
                            sumRow.Cells[1].Value = sumaAut.ToString(); // Wartość sumy aut
                            DataGridWstawienia.Rows.Add(sumRow);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Błąd odczytu danych: " + ex.Message);
                        }
                    }
                }

            }
        }
        private void PobierzInformacjeZBazyDanych(string lp)
        {
            nazwaZiD.publicPobierzInformacjeZBazyDanych(lp, LpWstawienia, Status, Data, Dostawca, KmH, KmK, liczbaAut, srednia, sztukNaSzuflade, sztuki, TypUmowy, TypCeny, Cena, Uwagi, Dodatek, dataStwo, dataMod, Ubytek, ktoMod, ktoStwo);
        }
        private void Dostawca_SelectedIndexChanged(object sender, EventArgs e)
        {
            nazwaZiD.ZmianaDostawcy(Dostawca, Kurnik, UlicaK, KodPocztowyK, MiejscK, KmK, UlicaH, KodPocztowyH, MiejscH, KmH, Dodatek, Ubytek, tel1, tel2, tel3, info1, info2, info3);
            nazwaZiD.WypelnienieLpWstawienia(Dostawca, LpWstawienia);
        }
        private void Kurnik_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Obsługa zmiany ComboBox "Kurnik"
            string selectedDostawca = Kurnik.SelectedItem.ToString();

            // Tworzenie i otwieranie połączenia z bazą danych
            using (SqlConnection conn = new SqlConnection(connectionPermission))
            {
                try
                {
                    conn.Open();

                    // Tworzenie i wykonanie zapytania SQL
                    string query = "SELECT Address, PostalCode, City, Distance FROM [LibraNet].[dbo].[DostawcyAdresy] WHERE Name = @selectedDostawca";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@selectedDostawca", selectedDostawca);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // Przypisanie wartości z bazy danych do TextBox-ów
                                UlicaK.Text = reader["Address"].ToString();
                                KodPocztowyK.Text = reader["PostalCode"].ToString();
                                MiejscK.Text = reader["City"].ToString();
                                KmK.Text = reader["Distance"].ToString();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd połączenia z bazą danych: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        private void FillComboBox()
        {
            zapytaniasql.UzupelnijComboBoxHodowcami(Dostawca);
        }
        private void SetupStatus()
        {
            // Dodaj opcje do comboBox2
            Status.Items.AddRange(new string[] { "Potwierdzony", "Do wykupienia", "Anulowany", "Sprzedany", "B.Wolny.", "B.Kontr." });

            // Opcjonalnie ustaw domyślną opcję wybraną
            Status.SelectedIndex = 0; // Wybierz pierwszą opcję

            // Dodaj opcje do comboBox2
            TypUmowy.Items.AddRange(new string[] { "Wolnyrynek", "Kontrakt", "W.Wolnyrynek" });

            // Opcjonalnie ustaw domyślną opcję wybraną
            TypUmowy.SelectedIndex = 0; // Wybierz pierwszą opcję

            // Dodaj opcje do comboBox2
            TypCeny.Items.AddRange(new string[] { "wolnyrynek", "rolnicza", "łączona", "ministerialna" });

            // Opcjonalnie ustaw domyślną opcję wybraną
            TypCeny.SelectedIndex = 0; // Wybierz pierwszą opcję
        }
        private void srednia_TextChanged(object sender, EventArgs e)
        {

            obliczenia.ObliczWage(srednia, WagaTuszki, iloscPoj, sztukNaSzuflade, wyliczone, KGwSkrzynce, CalcSztukNaSzuflade);
            obliczenia.ileSztukOblcizenie(sztukNaSzuflade, wyliczone);
            nazwaZiD.ReplaceCommaWithDot(srednia);
        }
        private void Data_ValueChanged(object sender, EventArgs e)
        {
            obliczenia.ObliczRozniceDni(Data, dataWstawienia);
            obliczenia.ObliczWageDni(WagaDni, RoznicaDni);
        }
        private void sztukNaSzuflade_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ProponowanaIloscNaSkrzynke(sztukNaSzuflade, sztuki, obliczeniaAut, srednia, KGwSkrzynce, wyliczone);
        }
        private void sztuki_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ObliczenieSztuki(sztuki, sztukNaSzuflade, obliczeniaAut);
        }
        private void ObliczAuta_Click(object sender, EventArgs e)
        {
            // Tworzenie nowej instancji formularza ObliczenieAut z przekazanymi wartościami
            ObliczenieAut obliczenieAut = new ObliczenieAut(sztukNaSzuflade.Text, liczbaAut.Text, sztuki.Text);

            // Wyświetlanie Form1
            obliczenieAut.ShowDialog();

            // Po zamknięciu Form2 odczytujemy wartości z jego właściwości i przypisujemy do kontrolki TextBox w Form1
            sztukNaSzuflade.Text = obliczenieAut.sztukiNaSzuflade;
            liczbaAut.Text = obliczenieAut.iloscAut;
            sztuki.Text = obliczenieAut.iloscSztuk;

            // Opcjonalnie, jeśli chcesz, aby użytkownik mógł interaktywnie korzystać z Form1 i wrócić do Form2, użyj form1.ShowDialog() zamiast form1.Show().
            // form1.ShowDialog();
        }
        private void dataWstawienia_ValueChanged(object sender, EventArgs e)
        {
            int roznicaDni = obliczenia.ObliczRozniceDni(Data, dataWstawienia);
            RoznicaDni.Text = roznicaDni.ToString();
        }
        private void Cena_TextChanged(object sender, EventArgs e)
        {
            nazwaZiD.ReplaceCommaWithDot(Cena);
        }
        private void CommandButton_Update_Click(object sender, EventArgs e)
        {
            try
            {
                // Utworzenie połączenia z bazą danych
                using (SqlConnection cnn = new SqlConnection(connectionPermission))
                {
                    cnn.Open();

                    // Utworzenie zapytania SQL do aktualizacji danych
                    string strSQL = @"
                    UPDATE dbo.HarmonogramDostaw
                    SET DataOdbioru = @DataOdbioru,
                        Dostawca = @Dostawca,
                        Auta = @Auta,
                        KmH = @KmH,
                        KmK = @KmK,
                        Kurnik = @Kurnik,
                        SztukiDek = @SztukiDek,
                        WagaDek = @WagaDek,
                        SztSzuflada = @SztSzuflada,
                        TypUmowy = @TypUmowy,
                        TypCeny = @TypCeny,
                        Cena = @Cena,
                        Ubytek = @Ubytek,
                        Dodatek = @Dodatek,
                        Bufor = @Bufor,
                        DataMod = @DataMod,
                        KtoMod = @KtoMod,
                        LpW = @LpW,
                        Uwagi = @Uwagi
                    WHERE Lp = @LpDostawa;";

                    using (SqlCommand command = new SqlCommand(strSQL, cnn))
                    {
                        // Dodanie parametrów do zapytania SQL, ustawiając wartość NULL dla pustych pól
                        command.Parameters.AddWithValue("@DataOdbioru", string.IsNullOrEmpty(Data.Text) ? (object)DBNull.Value : DateTime.Parse(Data.Text).Date);
                        command.Parameters.AddWithValue("@Dostawca", string.IsNullOrEmpty(Dostawca.Text) ? (object)DBNull.Value : Dostawca.Text);
                        command.Parameters.AddWithValue("@Auta", string.IsNullOrEmpty(liczbaAut.Text) ? (object)DBNull.Value : int.Parse(liczbaAut.Text));
                        command.Parameters.AddWithValue("@KmH", string.IsNullOrEmpty(KmH.Text) ? (object)DBNull.Value : int.Parse(KmH.Text));
                        command.Parameters.AddWithValue("@KmK", string.IsNullOrEmpty(KmK.Text) ? (object)DBNull.Value : int.Parse(KmK.Text));
                        command.Parameters.AddWithValue("@Kurnik", string.IsNullOrEmpty(GID) ? (object)DBNull.Value : int.Parse(GID));
                        command.Parameters.AddWithValue("@SztukiDek", string.IsNullOrEmpty(sztuki.Text) ? (object)DBNull.Value : int.Parse(sztuki.Text));
                        command.Parameters.AddWithValue("@WagaDek", string.IsNullOrEmpty(srednia.Text) ? (object)DBNull.Value : decimal.Parse(srednia.Text));
                        command.Parameters.AddWithValue("@SztSzuflada", string.IsNullOrEmpty(sztukNaSzuflade.Text) ? (object)DBNull.Value : int.Parse(sztukNaSzuflade.Text));
                        command.Parameters.AddWithValue("@TypUmowy", string.IsNullOrEmpty(TypUmowy.Text) ? (object)DBNull.Value : TypUmowy.Text);
                        command.Parameters.AddWithValue("@TypCeny", string.IsNullOrEmpty(TypCeny.Text) ? (object)DBNull.Value : TypCeny.Text);
                        command.Parameters.AddWithValue("@Cena", string.IsNullOrEmpty(Cena.Text) ? (object)DBNull.Value : decimal.Parse(Cena.Text));
                        command.Parameters.AddWithValue("@Ubytek", string.IsNullOrEmpty(Ubytek.Text) ? (object)DBNull.Value : decimal.Parse(Ubytek.Text));
                        command.Parameters.AddWithValue("@Dodatek", string.IsNullOrEmpty(Dodatek.Text) ? (object)DBNull.Value : decimal.Parse(Dodatek.Text));
                        command.Parameters.AddWithValue("@Bufor", string.IsNullOrEmpty(Status.Text) ? (object)DBNull.Value : Status.Text);
                        command.Parameters.AddWithValue("@DataMod", DateTime.Now);
                        command.Parameters.AddWithValue("@KtoMod", UserID);
                        command.Parameters.AddWithValue("@LpW", string.IsNullOrEmpty(LpWstawienia.Text) ? (object)DBNull.Value : int.Parse(LpWstawienia.Text));
                        command.Parameters.AddWithValue("@Uwagi", string.IsNullOrEmpty(Uwagi.Text) ? (object)DBNull.Value : Uwagi.Text);
                        command.Parameters.AddWithValue("@LpDostawa", int.Parse(lpDostawa));

                        // Wykonanie zapytania SQL
                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            MessageBox.Show("Dane zostały zaktualizowane w bazie danych.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show("Nie udało się zaktualizować danych.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        zapytaniasql.UpdateDaneAdresoweDostawcy(Dostawca, UlicaH, KodPocztowyH, MiejscH, KmH);
                    }
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            MyCalendar_DateChanged_1(this, new DateRangeEventArgs(DateTime.Today, DateTime.Today));
        }
        private void CommandButton_Insert_Click(object sender, EventArgs e)
        {
            Dostawa dostawa = new Dostawa();
            dostawa.UserID = App.UserID;
            // Wyświetlanie Form1
            dostawa.Show();
            MyCalendar_DateChanged_1(this, new DateRangeEventArgs(DateTime.Today, DateTime.Today));
        }
        private void Ubytek_TextChanged(object sender, EventArgs e)
        {
            nazwaZiD.ReplaceCommaWithDot(Ubytek);
        }
        private void buttonCena_Click(object sender, EventArgs e)
        {
            WidokCena widokcena = new WidokCena();
            widokcena.Show();
        }
        private void WidokKalendarza_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Zatrzymaj timer przy zamykaniu formularza
            timer.Stop();
        }
        private void buttonPokazTuszke_Click(object sender, EventArgs e)
        {
            PokazCeneTuszki pokazCeneTuszki = new PokazCeneTuszki();
            pokazCeneTuszki.Show();
            MyCalendar_DateChanged_1(this, new DateRangeEventArgs(DateTime.Today, DateTime.Today));
        }
        private void buttonWstawienie_Click(object sender, EventArgs e)
        {
            Wstawienie wstawienie = new Wstawienie();
            wstawienie.UserID = App.UserID;
            // Wyświetlanie Form1
            wstawienie.Show();
            MyCalendar_DateChanged_1(this, new DateRangeEventArgs(DateTime.Today, DateTime.Today));
        }
        private void dataGridPartie_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }
        private void WidokKalendarza_Load_1(object sender, EventArgs e)
        {

        }

        private void KGwSkrzynce_TextChanged(object sender, EventArgs e)
        {
            // Sprawdź, czy KGwSkrzynce nie jest puste
            if (!string.IsNullOrEmpty(KGwSkrzynce.Text))
            {
                // Spróbuj przekonwertować zawartość KGwSkrzynce na liczbę
                if (double.TryParse(KGwSkrzynce.Text, out double value))
                {
                    // Pomnóż wartość przez 264 i wyświetl wynik w KGwSkrzynekWAucie
                    double result = value * 264;
                    KGwSkrzynekWAucie.Text = result.ToString("N0"); // "N0" formatuje do liczby całkowitej z separatorem tysięcy
                }
                else
                {
                    // Jeśli zawartość KGwSkrzynce nie jest liczbą, wyświetl komunikat
                    MessageBox.Show("Wprowadzona wartość nie jest liczbą.");
                }
            }
            else
            {
                // Jeśli KGwSkrzynce jest puste, wyczyść KGwSkrzynekWAucie
                KGwSkrzynekWAucie.Clear();
            }
            DodajTextBoxy(KGwSkrzynekWAucie, KGwPaleciak, KGZestaw, KGSuma);
        }
        private void DodajTextBoxy(TextBox textBox1, TextBox textBox2, TextBox textBox3, TextBox resultTextBox)
        {
            try
            {
                // Inicjalizacja wartości na 0, aby pomijać puste TextBoxy
                double value1 = string.IsNullOrWhiteSpace(textBox1.Text) ? 0 : double.Parse(textBox1.Text);
                double value2 = string.IsNullOrWhiteSpace(textBox2.Text) ? 0 : double.Parse(textBox2.Text);
                double value3 = string.IsNullOrWhiteSpace(textBox3.Text) ? 0 : double.Parse(textBox3.Text);

                // Obliczanie sumy
                double suma = value1 + value2 + value3;

                // Formatowanie sumy z separatorami tysięcy
                resultTextBox.Text = suma.ToString("N0");

                // Formatowanie wejściowych TextBoxów z separatorami tysięcy
                textBox1.Text = value1.ToString("N0");
                textBox2.Text = value2.ToString("N0");
                textBox3.Text = value3.ToString("N0");
            }
            catch (FormatException)
            {
                // Wyświetlanie komunikatu o błędzie w przypadku niepoprawnych danych wejściowych
                MessageBox.Show("Proszę wprowadzić prawidłowe liczby do wszystkich trzech pól.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            // Sprawdź, czy CheckBoxPaleciak jest zaznaczony
            if (checkBox1.Checked)
            {
                // Jeśli zaznaczony, dodaj 3000 do KGwPaleciak.Text
                if (!string.IsNullOrEmpty(KGwPaleciak.Text))
                {
                    // Spróbuj przekonwertować zawartość KGwPaleciak na liczbę
                    if (double.TryParse(KGwPaleciak.Text.Replace(",", ""), out double value))
                    {
                        // Dodaj 3000 do wartości
                        value += 3000;
                        // Ustaw nową wartość z separatorem tysięcy
                        KGwPaleciak.Text = value.ToString("N0");
                    }
                    else
                    {
                        // Jeśli zawartość KGwPaleciak nie jest liczbą, ustaw 3000
                        KGwPaleciak.Text = "3000";
                    }
                }
                else
                {
                    // Jeśli KGwPaleciak jest puste, ustaw 3000
                    KGwPaleciak.Text = "3000";
                }
            }
            else
            {
                // Jeśli niezaznaczony, wyczyść KGwPaleciak
                KGwPaleciak.Clear();
            }
        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void tel2_TextChanged(object sender, EventArgs e)
        {

        }

        private void DostawcaMapa_Click(object sender, EventArgs e)
        {
            zapytaniasql.OtworzGoogleMaps(UlicaH, KodPocztowyH);
        }

        private void KGwPaleciak_TextChanged(object sender, EventArgs e)
        {
            DodajTextBoxy(KGwSkrzynekWAucie, KGwPaleciak, KGZestaw, KGSuma);
        }

        private void KGZestaw_TextChanged(object sender, EventArgs e)
        {
            DodajTextBoxy(KGwSkrzynekWAucie, KGwPaleciak, KGZestaw, KGSuma);
        }

        private void KGwSkrzynekWAucie_TextChanged(object sender, EventArgs e)
        {
            DodajTextBoxy(KGwSkrzynekWAucie, KGwPaleciak, KGZestaw, KGSuma);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            ChangeDateByWeeks(1);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            ChangeDateByWeeks(-1);
        }
        private void ChangeDateByWeeks(int weeks)
        {
            // Zmienienie daty w kalendarzu o określoną liczbę tygodni
            MyCalendar.SelectionStart = MyCalendar.SelectionStart.AddDays(7 * weeks);
            MyCalendar.SelectionEnd = MyCalendar.SelectionStart;

            // Wywołanie metody aktualizującej dane
            MyCalendar_DateChanged_1(this, null);
        }

        private void label40_Click(object sender, EventArgs e)
        {
            zapytaniasql.OtworzCenyRolne();
        }

        private void label42_Click(object sender, EventArgs e)
        {
            zapytaniasql.OtworzCenyTuszki();
        }

        private void label39_Click(object sender, EventArgs e)
        {
            zapytaniasql.OtworzCenyMinistra();
        }

        private void buttonDownDate_ControlRemoved(object sender, ControlEventArgs e)
        {

        }

        private void checkBoxAnulowane_CheckedChanged_1(object sender, EventArgs e)
        {

        }

        private void checkBoxDoWykupienia_CheckedChanged_1(object sender, EventArgs e)
        {

        }

        private void checkBoxCena_CheckedChanged(object sender, EventArgs e)
        {
            MyCalendar_DateChanged_1(sender, null);
        }

        private void checkBoxNotatki_CheckedChanged(object sender, EventArgs e)
        {

            MyCalendar_DateChanged_1(sender, null);
            dataGridView1.BringToFront(); // Ustawia DataGridView na wierzchu

        }

        private void label41_Click(object sender, EventArgs e)
        {

        }

        private void obliczoneAuta_TextChanged(object sender, EventArgs e)
        {
            MnozenieSztukAut(wyliczone, obliczoneAuta, obliczoneSztuki);
        }

        private void MnozenieSztukAut(TextBox sztuki, TextBox auta, TextBox wynik)
        {
            try
            {
                int liczbaSztuk = int.Parse(sztuki.Text);
                int liczbaAut = int.Parse(auta.Text);
                int wartosc = liczbaSztuk * liczbaAut;
                wynik.Text = wartosc.ToString();
            }
            catch (FormatException)
            {
                // Handle the case where the input is not a valid integer
                // For example, you can set the result TextBox to show an error message
                wynik.Text = "Blad";
            }
        }


        private void wyliczone_TextChanged(object sender, EventArgs e)
        {
            MnozenieSztukAut(wyliczone, obliczoneAuta, obliczoneSztuki);
        }

        private void buttonWklej_Click(object sender, EventArgs e)
        {
             sztuki.Text = obliczoneSztuki.Text ;
             liczbaAut.Text = obliczoneAuta.Text;
        }
    }
}
