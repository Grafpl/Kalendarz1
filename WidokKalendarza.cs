 using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Primitives;
using Microsoft.VisualBasic.ApplicationServices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing; // Dodaj tę dyrektywę
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;


namespace Kalendarz1
{
    public partial class WidokKalendarza : Form
    {
        private string GID;
        private string lpDostawa;
        static string connectionPermission = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private int selectedRowIndex = -1; // Zmienna do przechowywania indeksu zaznaczonego wiersza
        private double sumaSztuk; // pozostale wstawienia
        private Timer timer;
        private Timer timer2;
        public string UserID { get; set; }
        private MojeObliczenia obliczenia = new MojeObliczenia();
        private NazwaZiD nazwaZiD = new NazwaZiD();
        private CenoweMetody CenoweMetody = new CenoweMetody();
        private DataService dataService = new DataService();
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
            checkBox1.Checked = true; //Paleciak


            // Inicjalizacja timera
            timer = new Timer();
            timer.Interval = 600000; // Interwał 10 minut (600 000 ms)
            timer.Tick += Timer_Tick; // Przypisanie zdarzenia
            timer.Start(); // Rozpoczęcie pracy timera

            // Inicjalizacja timera2
            timer2 = new Timer();
            timer2.Interval = 1800000; // Interwał 30 minut (1 800 000 ms)
            timer2.Tick += Timer2_Tick; // Przypisanie zdarzenia
            timer2.Start(); // Rozpoczęcie pracy timera

        }
        // Metoda wywoływana podczas ładowania formularza
        private void Timer_Tick(object sender, EventArgs e)
        {
            // Ta metoda zostanie wywołana co określony interwał czasowy
            MyCalendar_DateChanged_1(this, new DateRangeEventArgs(DateTime.Today, DateTime.Today));


            // Sprawdź, czy aktualna godzina jest między 14:30 a 15:00
            //TimeSpan start = new TimeSpan(14, 30, 0); // 14:30
            //TimeSpan end = new TimeSpan(15, 30, 0);    // 15:00
            //TimeSpan now = DateTime.Now.TimeOfDay;
            // DayOfWeek today = DateTime.Now.DayOfWeek;

            /*
            bool isDateInDatabase = false;
            using (SqlConnection connection = new SqlConnection(connectionPermission))
            {
                connection.Open();
                string query = "SELECT COUNT(*) FROM [LibraNet].[dbo].[CenaRolnicza] WHERE CAST(data AS DATE) = @Today";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Today", today);
                    int count = (int)command.ExecuteScalar();
                    isDateInDatabase = (count > 0);
                }
            }

            // Sprawdź, czy aktualny dzień tygodnia jest poniedziałek, środa lub piątek

            bool isMondayWednesdayOrFriday = (today == DayOfWeek.Monday || today == DayOfWeek.Wednesday || today == DayOfWeek.Friday);

            if (now >= start && now <= end && isMondayWednesdayOrFriday && !isDateInDatabase)
            {
                // Wyświetl komunikat
                MessageBox.Show("Wstaw Cene z cenyrolnicze.pl");
            }
            */
        }
        private void Timer2_Tick(object sender, EventArgs e)
        {
            // Ta metoda zostanie wywołana co określony interwał czasowy
            PokazCeny();
            BiezacePartie();
            nazwaZiD.PokazPojTuszki(dataGridSumaPartie);
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
                            dataGridViewNotatki.Rows.Clear();
                            dataGridViewNotatki.RowHeadersVisible = false;
                            dataGridViewNotatki.ColumnHeadersVisible = false;
                            dataGridViewNotatki.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                            dataGridViewNotatki.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
                            dataGridViewNotatki.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
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
                            //MessageBox.Show("Błąd odczytu danych: " + ex.Message);
                        }
                    }
                }
            }
        }


        private void WidokKalendarza_Load(object sender, EventArgs e)
        {
            BiezacePartie();
            nazwaZiD.PokazPojTuszki(dataGridSumaPartie);
            NazwaZiD databaseManager = new NazwaZiD();
            string name = databaseManager.GetNameById(UserID);
            // Przypisanie wartości UserID do TextBoxa userTextbox
            userTextbox.Text = name;
            //Mozliwosci prawego klikniecia na kalendarz
            dataGridView1.ContextMenuStrip = contextMenuStrip1;
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
            //dataWstawienia.Value = DateTime.Today;
            LpWstawienia.Text = "";
            lpDostawa = "0";
            //Wstawienia
            obecnaDoba.Text = "";
            sztukiWstawienia.Text = "";
            sztukiPoUpadkach.Text = "";
            sztukiPozostale.Text = "";
            obecnaDoba.Text = "";
            //Obliczenia
            wyliczone.Text = "";
            obliczoneAuta.Text = "";
            obliczoneSztuki.Text = "";
            obliczeniaAut.Text = "";

            DataGridWstawienia.Rows.Clear();

            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                selectedRowIndex = e.RowIndex;
                object wartoscKomorki = dataGridView1.Rows[e.RowIndex].Cells["LP"].Value;
                lpDostawa = wartoscKomorki != null ? wartoscKomorki.ToString() : "0";
                PobierzInformacjeZBazyDanych(lpDostawa);
                //zapytaniasql.UzupełnienieDanychHodowcydoTextBoxow(Dostawca, UlicaH, KodPocztowyH, MiejscH, KmH, tel1, tel2, tel3);

            }
            if (int.TryParse(lpDostawa, out int dostawaId))
            {
                WczytajNotatki(dostawaId);
            }

            KolorZielonyCheckbox(potwWaga, srednia);
            KolorZielonyCheckbox(potwSztuki, sztuki);
            UpdateDataGrid();
            obliczenia.ZestawDoObliczaniaTransportu(sztukNaSzuflade, wyliczone, obliczeniaAut, sztuki, srednia, KGwSkrzynce, obliczeniaAut2, sztukNaSzuflade2);
            obliczenia.ProponowanaIloscNaSkrzynke(sztukNaSzuflade2, srednia, KGwSkrzynce2);

        }
        private void WczytajNotatki(int lpDostawa)
        {
            using (SqlConnection conn = new SqlConnection(connectionPermission))
            {
                try
                {
                    conn.Open();
                    string query = @"
                SELECT 
                    N.DataUtworzenia AS [Data], 
                    O.Name AS [Kto dodał], 
                    N.Tresc AS [Treść]
                FROM [LibraNet].[dbo].[Notatki] N
                LEFT JOIN [LibraNet].[dbo].[operators] O ON N.KtoStworzyl = O.ID
                WHERE N.IndeksID = @Lp
                ORDER BY N.DataUtworzenia DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Lp", lpDostawa);

                        SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);

                        dataGridViewNotatki.DataSource = dt;

                        // 🔒 WYŁĄCZ auto-rozszerzanie kolumn
                        dataGridViewNotatki.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

                        dataGridViewNotatki.Columns["Data"].Width = 70;
                        dataGridViewNotatki.Columns["Data"].DefaultCellStyle.Format = "dd.MM HH:mm";
                        dataGridViewNotatki.Columns["Kto dodał"].Width = 50;
                        dataGridViewNotatki.Columns["Kto dodał"].DefaultCellStyle.WrapMode = DataGridViewTriState.False;
                        dataGridViewNotatki.Columns["Treść"].Width = 270;

                        // Zawijanie tekstu i dynamiczna wysokość wierszy
                        dataGridViewNotatki.Columns["Treść"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                        dataGridViewNotatki.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;

                        // (opcjonalnie) Wyłącz możliwość zmiany rozmiaru kolumn przez użytkownika
                        foreach (DataGridViewColumn col in dataGridViewNotatki.Columns)
                        {
                            col.Resizable = DataGridViewTriState.False;
                        }



                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd podczas wczytywania notatek: " + ex.Message);
                }
            }
        }

        private void WczytajNotatkiDoGrida(DataGridView grid)
        {
            using (SqlConnection conn = new SqlConnection(connectionPermission))
            {
                conn.Open();

                string query = @"
        SELECT TOP (20) 
            N.DataUtworzenia,
            FORMAT(H.DataOdbioru, 'MM-dd ddd') AS DataOdbioru,
            H.Dostawca, 
            N.Tresc, 
            O.Name AS KtoStworzyl
        FROM [LibraNet].[dbo].[Notatki] N
        LEFT JOIN [LibraNet].[dbo].[operators] O ON N.KtoStworzyl = O.ID
        LEFT JOIN [LibraNet].[dbo].[HarmonogramDostaw] H ON N.IndeksID = H.LP
        WHERE N.TypID = 1
        ORDER BY N.DataUtworzenia DESC;";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    grid.DataSource = dt;
                }

                // Format daty – tylko wygląd
                if (grid.Columns.Contains("DataUtworzenia"))
                {
                    grid.Columns["DataUtworzenia"].DefaultCellStyle.Format = "MM-dd HH:mm";
                    grid.Columns["DataUtworzenia"].HeaderText = "Data Utworzenia";
                }

                // Przytnij autora do 6 znaków
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.Cells["KtoStworzyl"].Value != null)
                    {
                        string autor = row.Cells["KtoStworzyl"].Value.ToString();
                        row.Cells["KtoStworzyl"].Value = autor.Length > 6 ? autor.Substring(0, 5) : autor;
                    }
                }

                // Styl ogólny
                grid.RowHeadersVisible = false;
                grid.ColumnHeadersVisible = true;
                grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;

                // 📐 Szerokość proporcjonalna
                int totalWidth = grid.Width;

                grid.Columns["Tresc"].Width = (int)(totalWidth * 0.36);
                grid.Columns["Dostawca"].Width = (int)(totalWidth * 0.25);
                grid.Columns["DataOdbioru"].Width = (int)(totalWidth * 0.13);
                grid.Columns["DataUtworzenia"].Width = (int)(totalWidth * 0.14);
                grid.Columns["KtoStworzyl"].Width = (int)(totalWidth * 0.10);

                // Wyłącz zawijanie wszędzie poza Treść
                foreach (DataGridViewColumn col in grid.Columns)
                {
                    col.DefaultCellStyle.WrapMode = col.Name == "Tresc"
                        ? DataGridViewTriState.True
                        : DataGridViewTriState.False;
                }
                // Wyłącz zawijanie wszędzie poza Treść
                foreach (DataGridViewColumn col in grid.Columns)
                {
                    col.DefaultCellStyle.WrapMode = col.Name == "Dostawca"
                        ? DataGridViewTriState.True
                        : DataGridViewTriState.False;
                }
            }
        }


        private void buttonUpDate_Click(object sender, EventArgs e)
        {
            ZmienDate(lpDostawa, 1); // Zwiększenie daty o jeden dzień
            MyCalendar_DateChanged_1(sender, null);
            DodajAktywnosc(1);

        }

        private void buttonDownDate_Click(object sender, EventArgs e)
        {
            ZmienDate(lpDostawa, -1); // Zmniejszenie daty o jeden dzień
            MyCalendar_DateChanged_1(sender, null);
            DodajAktywnosc(1);

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
            DodajAktywnosc(7);
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
    SELECT DISTINCT 
        HD.LP, 
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
        HD.Ubytek,
        (
    SELECT TOP 1 N.Tresc 
    FROM Notatki N 
    WHERE N.IndeksID = HD.Lp 
    ORDER BY N.DataUtworzenia DESC
) AS UWAGI,

        HD.PotwWaga,
        HD.PotwSztuki,
        WK.isConf,
        D.TypOsobowosci,
        D.TypOsobowosci2,
        CASE 
            WHEN HD.bufor = 'Potwierdzony' THEN 1
            WHEN HD.bufor = 'B.Kontr.' THEN 2
            WHEN HD.bufor = 'B.Wolny.' THEN 3
            WHEN HD.bufor = 'Do Wykupienia' THEN 5
            ELSE 4
        END AS buforPriority
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

                    strSQL += @"
    ORDER BY 
        HD.DataOdbioru, 
        buforPriority, 
        HD.WagaDek DESC";


                    using (SqlCommand command = new SqlCommand(strSQL, connection))
                    {
                        command.Parameters.AddWithValue("@startDate", startOfWeek);
                        command.Parameters.AddWithValue("@endDate", endOfWeek);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            dataGridView1.RowHeadersVisible = false;
                            dataGridView1.Rows.Clear();
                            dataGridView1.Columns.Clear();
                            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                            dataGridView1.AllowUserToAddRows = false;
                            dataGridView1.ReadOnly = true;

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
                            dataGridView1.Columns.Add("procentUbytek", "%");
                            dataGridView1.Columns.Add("UwagaKolumna", "Uwagi");
                            dataGridView1.Columns.Add("PotwWagaKolumna", "PotwWaga");
                            dataGridView1.Columns.Add("PotwSztuki", "PotwSztuki");
                            dataGridView1.Columns.Add("Osobowosc", "Osobowosc");

                            if (!checkBoxCena.Checked)
                            {
                                dataGridView1.Columns["CenaKolumna"].Visible = true;

                            }
                            else
                            {
                                dataGridView1.Columns["CenaKolumna"].Visible = false;

                            }
                            dataGridView1.Columns["PotwSztuki"].Visible = false;
                            dataGridView1.Columns["PotwWagaKolumna"].Visible = false;
                            dataGridView1.Columns["PotwSztuki"].Visible = false;
                            dataGridView1.Columns["LP"].Visible = false;
                            dataGridView1.Columns["DataOdbioruKolumna"].Visible = false;
                            dataGridView1.Columns["bufor"].Visible = false;
                            dataGridView1.Columns["procentUbytek"].Visible = false;

                            // Ustawienie szerokości kolumn
                            dataGridView1.Columns["LP"].Width = 50;
                            dataGridView1.Columns["DataOdbioruKolumna"].Width = 85;
                            dataGridView1.Columns["DostawcaKolumna"].Width = 110;
                            dataGridView1.Columns["AutaKolumna"].Width = 25;
                            dataGridView1.Columns["SztukiDekKolumna"].Width = 70;
                            dataGridView1.Columns["WagaDek"].Width = 50;
                            dataGridView1.Columns["bufor"].Width = 85;
                            dataGridView1.Columns["RóżnicaDni"].Width = 43;
                            dataGridView1.Columns["TypCenyKolumna"].Width = 55;
                            dataGridView1.Columns["CenaKolumna"].Width = 50;
                            dataGridView1.Columns["KmKolumna"].Width = 50;
                            dataGridView1.Columns["procentUbytek"].Width = 43;
                            dataGridView1.Columns["Osobowosc"].Width = 130;


                            DataGridViewCheckBoxColumn confirmColumn = new DataGridViewCheckBoxColumn();
                            confirmColumn.HeaderText = "V";
                            confirmColumn.Name = "ConfirmColumn";
                            confirmColumn.Width = 80;
                            dataGridView1.Columns.Add(confirmColumn);
                            dataGridView1.Columns["ConfirmColumn"].Width = 35;

                            DataGridViewCheckBoxColumn isConfColumn = new DataGridViewCheckBoxColumn();
                            isConfColumn.HeaderText = "✓ Wstaw.";
                            isConfColumn.Name = "WstawienieConfirmed";
                            isConfColumn.Width = 80;


                            dataGridView1.Columns.Add(isConfColumn);
                            dataGridView1.Columns["WstawienieConfirmed"].Width = 35;

                            foreach (DataGridViewColumn column in dataGridView1.Columns)
                            {
                                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                                column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
                            }
                            /*
                            if (!checkBoxNotatki.Checked)
                            {
                                dataGridView1.Columns["UwagaKolumna"].Width = 80;
                                //dataGridView1.Columns["UwagaKolumna"].Visible = false;
                                dataGridView1.Width = 612;
                                groupBox1.Location = new Point(695, 180);
                                groupBoxPrzyciski.Location = new Point(612, 221);
                                MyCalendar.Location = new Point(612, 1);

                            }
                            else
                            {
                                dataGridView1.Columns["UwagaKolumna"].Width = 600;
                                //dataGridView1.Columns["UwagaKolumna"].Visible = true;
                                dataGridView1.Width = 1212;
                                groupBox1.Location = new Point(1295, 180);
                                groupBoxPrzyciski.Location = new Point(1212, 168);
                                MyCalendar.Location = new Point(1212, 1);
                            }
                            */
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
                                string formattedDate = date.ToString("yyyy-MM-dd ddd");

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

                                // Wiersz danych
                                DataGridViewRow newRow = new DataGridViewRow();
                                newRow.CreateCells(dataGridView1);

                                for (int i = 0; i < dataGridView1.Columns.Count; i++)

                                {
                                    string columnName = dataGridView1.Columns[i].Name;

                                    if (columnName == "DataOdbioruKolumna")
                                    {
                                        newRow.Cells[i].Value = formattedDate;
                                    }
                                    else if (columnName == "SztukiDekKolumna")
                                    {
                                        if (!Convert.IsDBNull(reader["SztukiDek"]))
                                            newRow.Cells[i].Value = $"{Convert.ToDouble(reader["SztukiDek"]):#,0} szt";
                                        else
                                            newRow.Cells[i].Value = "";
                                    }
                                    else if (columnName == "procentUbytek")
                                    {
                                        newRow.Cells[i].Value = reader["Ubytek"] + "%";
                                    }
                                    else if (columnName == "WagaDek")
                                    {
                                        newRow.Cells[i].Value = reader["WagaDek"] + " kg";
                                    }
                                    else if (columnName == "RóżnicaDni")
                                    {
                                        DateTime dataWstawienia = reader.IsDBNull(reader.GetOrdinal("DataWstawienia")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("DataWstawienia"));
                                        if (dataWstawienia == DateTime.MinValue)
                                            newRow.Cells[i].Value = "-";
                                        else
                                        {
                                            int roznicaDni = (date - dataWstawienia).Days;
                                            newRow.Cells[i].Value = roznicaDni + " dni";
                                        }
                                    }
                                    else if (columnName == "TypCenyKolumna")
                                    {
                                        newRow.Cells[i].Value = reader["TypCeny"];
                                    }
                                    else if (columnName == "CenaKolumna")
                                    {
                                        newRow.Cells[i].Value = reader["Cena"] != DBNull.Value ? reader["Cena"] + " zł" : "-";
                                    }
                                    else if (columnName == "KmKolumna")
                                    {
                                        newRow.Cells[i].Value = reader["Distance"] != DBNull.Value ? reader["Distance"] + " km" : "-";
                                    }
                                    else if (columnName == "Osobowosc")
                                    {
                                        string osobowosc1 = reader["TypOsobowosci"] != DBNull.Value ? reader["TypOsobowosci"].ToString() : "";
                                        string osobowosc2 = reader["TypOsobowosci2"] != DBNull.Value ? reader["TypOsobowosci2"].ToString() : "";

                                        if (!string.IsNullOrWhiteSpace(osobowosc1) && !string.IsNullOrWhiteSpace(osobowosc2))
                                        {
                                            newRow.Cells[i].Value = $"{osobowosc1} - {osobowosc2}";
                                        }
                                        else if (!string.IsNullOrWhiteSpace(osobowosc1))
                                        {
                                            newRow.Cells[i].Value = osobowosc1;
                                        }
                                        else if (!string.IsNullOrWhiteSpace(osobowosc2))
                                        {
                                            newRow.Cells[i].Value = osobowosc2;
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

                                    // Suma do obliczeń
                                    if (columnName == "AutaKolumna" && reader["Auta"] != DBNull.Value)
                                    {
                                        double auta = Convert.ToDouble(reader["Auta"]);
                                        sumaAuta += auta;
                                    }
                                    else if (columnName == "SztukiDekKolumna" && reader["SztukiDek"] != DBNull.Value)
                                    {
                                        sumaSztukiDek += Convert.ToDouble(reader["SztukiDek"]);
                                    }
                                    else if (columnName == "WagaDek" && reader["WagaDek"] != DBNull.Value)
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
                                    else if (columnName == "CenaKolumna" && reader["Cena"] != DBNull.Value)
                                    {
                                        double cena = Convert.ToDouble(reader["Cena"]);
                                        if (reader["Auta"] != DBNull.Value)
                                        {
                                            double auta = Convert.ToDouble(reader["Auta"]);
                                            sumaCenaPomnozona += cena * auta;
                                        }
                                    }
                                    else if (columnName == "KmKolumna" && reader["Distance"] != DBNull.Value)
                                    {
                                        double KM = Convert.ToDouble(reader["Distance"]);
                                        if (reader["Auta"] != DBNull.Value)
                                        {
                                            double auta = Convert.ToDouble(reader["Auta"]);
                                            sumaKMPomnozona += KM * auta;
                                        }
                                    }
                                    else if (columnName == "RóżnicaDni" && reader["WagaDek"] != DBNull.Value)
                                    {
                                        double typCeny = Convert.ToDouble(reader["WagaDek"]);
                                        if (typCeny >= 0.5 && typCeny <= 2.4 && reader["Auta"] != DBNull.Value)
                                        {
                                            double auta = Convert.ToDouble(reader["Auta"]);
                                            sumaTypCenyKolumnaPomnozona += 1 * auta;
                                        }
                                    }
                                }

                                // Ustawienie checkboxa WstawienieConfirmed
                                if (dataGridView1.Columns.Contains("WstawienieConfirmed"))
                                {
                                    bool confirmed = reader["isConf"] != DBNull.Value && Convert.ToBoolean(reader["isConf"]);
                                    newRow.Cells[dataGridView1.Columns["WstawienieConfirmed"].Index].Value = confirmed;
                                }

                                dataGridView1.Rows.Add(newRow);
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
            // Ustawienie wysokości wierszy na minimalną wartość
            // Ustawienie wysokości wierszy na określoną wartość (np. 25 pikseli)
            SetRowHeights(18, dataGridView1);
            WczytajNotatkiDoGrida(dataGridViewOstatnieNotatki);
        }


        private void SetRowHeights(int height, DataGridView dataGridView)
        {
            // Ustawienie wysokości wszystkich wierszy na określoną wartość
            foreach (DataGridViewRow row in dataGridView.Rows)
            {
                row.Height = height;
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

            double cenaTuszkirol = CenoweMetody.PobierzCeneTuszkiDzisiaj();
            if (cenaTuszki > 0)
            {
                textTuszkiRol.Text = cenaTuszkirol.ToString("F2"); // Formatowanie do dwóch miejsc po przecinku
            }

            double cenaWolna = CenoweMetody.PobierzSredniaCeneWolnorynkowa();
            if (cenaWolna > 0)
            {
                textWolny.Text = cenaWolna.ToString("F2"); // Formatowanie do dwóch miejsc po przecinku
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

            if (e.RowIndex >= 0 && e.ColumnIndex == dataGridView1.Columns["WstawienieConfirmed"].Index)
            {
                DataGridViewCheckBoxCell checkboxCell = (DataGridViewCheckBoxCell)dataGridView1.Rows[e.RowIndex].Cells["WstawienieConfirmed"];
                bool currentValue = checkboxCell.Value != null && (bool)checkboxCell.Value;

                bool newValue = !currentValue;
                checkboxCell.Value = newValue;

                // 1. Pobierz LP dostawy (czyli z HarmonogramDostaw)
                object lpValueObj = dataGridView1.Rows[e.RowIndex].Cells["LP"].Value;
                if (lpValueObj == null || lpValueObj == DBNull.Value)
                    return;

                string lpDostawy = lpValueObj.ToString();
                string lpWstawienia = null;

                using (SqlConnection conn = new SqlConnection(connectionPermission))
                {
                    conn.Open();

                    // 2. Najpierw pobierz LpW z tabeli HarmonogramDostaw
                    string selectLpW = "SELECT LpW FROM HarmonogramDostaw WHERE Lp = @lp";
                    using (SqlCommand cmdSelect = new SqlCommand(selectLpW, conn))
                    {
                        cmdSelect.Parameters.AddWithValue("@lp", lpDostawy);
                        object result = cmdSelect.ExecuteScalar();
                        if (result == null || result == DBNull.Value)
                            return;

                        lpWstawienia = result.ToString();
                    }

                    // 3. Teraz zaktualizuj WstawieniaKurczakow
                    string updateQuery = @"
                        UPDATE WstawieniaKurczakow
                        SET isConf = @isConf,
                            KtoConf = @UserID,
                            DataConf = @DateConf
                        WHERE Lp = @lpw";

                    using (SqlCommand cmdUpdate = new SqlCommand(updateQuery, conn))
                    {
                        cmdUpdate.Parameters.AddWithValue("@isConf", newValue ? 1 : 0);
                        cmdUpdate.Parameters.AddWithValue("@UserID", UserID); // lub inna wartość użytkownika
                        cmdUpdate.Parameters.AddWithValue("@DateConf", DateTime.Now);
                        cmdUpdate.Parameters.AddWithValue("@lpw", lpWstawienia);

                        cmdUpdate.ExecuteNonQuery();
                    }

                }

                MyCalendar_DateChanged_1(this, null);
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

                var potwSztukiCell = dataGridView1.Rows[rowIndex].Cells["PotwSztuki"];
                var SztukiCell = dataGridView1.Rows[rowIndex].Cells["SztukiDekKolumna"];

                var potwWagaCell = dataGridView1.Rows[rowIndex].Cells["PotwWagaKolumna"];
                var wagaDekCell = dataGridView1.Rows[rowIndex].Cells["WagaDek"];
                var typCenyCell = dataGridView1.Rows[rowIndex].Cells["TypCenyKolumna"];
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
                    if (parsedDate.Date == DateTime.Today)
                    {
                        dataGridView1.Rows[rowIndex].DefaultCellStyle.Font = new Font(dataGridView1.Font.FontFamily, 9, FontStyle.Bold);
                        dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.Blue;
                        dataGridView1.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.White;
                    }
                    else if (parsedDate.Date < DateTime.Today)
                    {
                        // Kod do wykonania, gdy data jest wcześniejsza niż dzisiejsza
                        dataGridView1.Rows[rowIndex].DefaultCellStyle.Font = new Font(dataGridView1.Font.FontFamily, 9, FontStyle.Bold);
                        dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.Black;
                        dataGridView1.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.White;
                    }
                    else
                    {
                        dataGridView1.Rows[rowIndex].DefaultCellStyle.Font = new Font(dataGridView1.Font.FontFamily, 9, FontStyle.Bold);
                        dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightGray;
                    }


                }



                else
                {
                    dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.White; // Domyślny kolor tła dla pozostałych wierszy
                }

                if (potwWagaCell != null &&
                     (potwWagaCell.Value == null || string.IsNullOrEmpty(potwWagaCell.Value.ToString()) || potwWagaCell.Value.ToString() == "False") &&
                     typCenyCell != null && typCenyCell.Value != null)
                {
                    if (wagaDekCell != null)
                    {
                        wagaDekCell.Style.BackColor = Color.White;
                    }
                }
                if (potwSztukiCell != null &&
                     (potwSztukiCell.Value == null || string.IsNullOrEmpty(potwSztukiCell.Value.ToString()) || potwSztukiCell.Value.ToString() == "False") &&
                     typCenyCell != null && typCenyCell.Value != null)
                {
                    if (SztukiCell != null)
                    {
                        SztukiCell.Style.BackColor = Color.White;
                    }
                }



                // Nie dodajemy żadnego else, aby "nic nie robić" w przypadku, gdy wartość jest 1




            }
        }



        // Wypełnianie Textboxów
        private void LpWstawienia_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateDataGrid();
        }
        private void UpdateDataGrid()
        {
            sumaSztuk = 0;

            using (SqlConnection cnn = new SqlConnection(connectionPermission))
            {
                cnn.Open();

                string lpWstawieniaValue = LpWstawienia.Text;

                string strSQL = $"SELECT * FROM dbo.WstawieniaKurczakow WHERE Lp = @lp";

                using (SqlCommand command = new SqlCommand(strSQL, cnn))
                {
                    command.Parameters.AddWithValue("@lp", lpWstawieniaValue);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string dataWstawieniaFormatted = Convert.ToDateTime(reader["DataWstawienia"]).ToString("yyyy-MM-dd");
                            dataWstawienia.Text = dataWstawieniaFormatted;
                            sztukiWstawienia.Text = reader["IloscWstawienia"].ToString();
                        }
                    }
                }

                strSQL = "SELECT LP, DataOdbioru, Auta, SztukiDek, WagaDek, bufor FROM [LibraNet].[dbo].[HarmonogramDostaw] WHERE LpW = @NumerWstawienia order by DataOdbioru ASC";

                double sumaAut = 0;

                using (SqlCommand command2 = new SqlCommand(strSQL, cnn))
                {
                    command2.Parameters.AddWithValue("@NumerWstawienia", lpWstawieniaValue);

                    using (SqlDataReader reader = command2.ExecuteReader())
                    {
                        try
                        {
                            DataGridWstawienia.Rows.Clear();
                            DataGridWstawienia.Columns.Clear();
                            DataGridWstawienia.RowHeadersVisible = false;

                            DataGridWstawienia.Columns.Add("DataOdbioruKolumnaWstawienia", "Data Odbioru");
                            DataGridWstawienia.Columns.Add("AutaKolumnaWstawienia", "A");
                            DataGridWstawienia.Columns.Add("SztukiDekKolumnaWstawienia", "Sztuki");
                            DataGridWstawienia.Columns.Add("WagaDekKolumnaWstawienia", "Waga");
                            DataGridWstawienia.Columns.Add("buforkKolumnaWstawienia", "Status");

                            DataGridWstawienia.Columns["DataOdbioruKolumnaWstawienia"].Width = 120;
                            DataGridWstawienia.Columns["AutaKolumnaWstawienia"].Width = 25;
                            DataGridWstawienia.Columns["SztukiDekKolumnaWstawienia"].Width = 50;
                            DataGridWstawienia.Columns["WagaDekKolumnaWstawienia"].Width = 30;
                            DataGridWstawienia.Columns["buforkKolumnaWstawienia"].Width = 80;

                            while (reader.Read())
                            {
                                string dataOdbioru = reader["DataOdbioru"] != DBNull.Value ? Convert.ToDateTime(reader["DataOdbioru"]).ToString("yyyy-MM-dd ddd") : string.Empty;

                                DataGridViewRow newRow = new DataGridViewRow();
                                newRow.CreateCells(DataGridWstawienia);
                                newRow.Cells[0].Value = dataOdbioru;
                                newRow.Cells[1].Value = reader["Auta"] != DBNull.Value ? reader["Auta"].ToString() : string.Empty;
                                newRow.Cells[2].Value = reader["SztukiDek"] != DBNull.Value ? string.Format("{0:#,0}", Convert.ToDouble(reader["SztukiDek"])) : string.Empty;
                                newRow.Cells[3].Value = reader["WagaDek"] != DBNull.Value ? reader["WagaDek"].ToString() : string.Empty;
                                newRow.Cells[4].Value = reader["bufor"] != DBNull.Value ? reader["bufor"].ToString() : string.Empty;

                                DataGridWstawienia.Rows.Add(newRow);

                                if (reader["Auta"] != DBNull.Value)
                                {
                                    sumaAut += Convert.ToDouble(reader["Auta"]);
                                }
                                if (reader["SztukiDek"] != DBNull.Value)
                                {
                                    sumaSztuk += Convert.ToDouble(reader["SztukiDek"]);
                                }
                            }

                            DataGridViewRow sumRow = new DataGridViewRow();
                            sumRow.CreateCells(DataGridWstawienia);
                            sumRow.Cells[0].Value = "Suma";
                            sumRow.Cells[2].Value = string.Format("{0:#,0}", sumaSztuk);
                            sumRow.Cells[1].Value = sumaAut.ToString();
                            DataGridWstawienia.Rows.Add(sumRow);

                            ObliczanieProcentuUbytku(sztukiWstawienia, sztukiPoUpadkach);
                            OObliczaniePozosalychSztuk(sztukiPoUpadkach, sumaSztuk, sztukiPozostale);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Błąd odczytu danych: " + ex.Message);
                        }
                    }
                }
            }
            SetRowHeights(18, DataGridWstawienia);
        }

        private void PobierzInformacjeZBazyDanych(string lp)
        {
            nazwaZiD.publicPobierzInformacjeZBazyDanych(lp, LpWstawienia, Status, Data, Dostawca, KmH, KmK, liczbaAut, srednia, sztukNaSzuflade, sztuki, TypUmowy, TypCeny, Cena, Uwagi, Dodatek, dataStwo, dataMod, Ubytek, ktoMod, ktoStwo, KtoWaga, KiedyWaga, KtoSztuki, KiedySztuki);
            nazwaZiD.PobierzCheckBoxyWagSztuk(lp, potwWaga, potwSztuki);
            string hodowca = Dostawca.Text;
            string hodowcaid = zapytaniasql.ZnajdzIdHodowcyString(hodowca);
            nazwaZiD.PobierzTypOsobowosci(hodowcaid, comboBoxOsobowosc, comboBoxOsobowosc2);
            // Nadpisanie pola Uwagi pustym tekstem:
            Uwagi.Text = "";
            UstawPoradyDlaOsobowosci();


        }
        private void Dostawca_SelectedIndexChanged(object sender, EventArgs e)
        {
            nazwaZiD.ZmianaDostawcy(Dostawca, Kurnik, UlicaK, KodPocztowyK, MiejscK, KmK, UlicaH, KodPocztowyH, MiejscH, KmH, Dodatek, Ubytek, tel1, tel2, tel3, info1, info2, info3, Email);
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

            // Dodaj opcje do comboBox2
            comboBoxOsobowosc.Items.AddRange(new string[] { "Analityk", "Na Cel", "Wpływowy", "Relacyjny" });
            comboBoxOsobowosc2.Items.AddRange(new string[] { "Analityk", "Na Cel", "Wpływowy", "Relacyjny" });



        }
        private void srednia_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ileSztukOblcizenie(sztukNaSzuflade, wyliczone);
            obliczenia.ProponowanaIloscNaSkrzynke(sztukNaSzuflade, srednia, KGwSkrzynce);

            nazwaZiD.ReplaceCommaWithDot(srednia);
        }
        private void Data_ValueChanged(object sender, EventArgs e)
        {
            obliczenia.ObliczRozniceDni(Data, dataWstawienia);
            obliczenia.ObliczWageDni(WagaDni, RoznicaDni);
        }
        private void sztukNaSzuflade_TextChanged(object sender, EventArgs e)
        {
            //obliczenia.ProponowanaIloscNaSkrzynke(sztukNaSzuflade, sztuki, obliczeniaAut, srednia, KGwSkrzynce, wyliczone);
            //obliczenia.ileSztukOblcizenie(sztukNaSzuflade, wyliczone);
            //obliczenia.ObliczenieAutaCzySieMiesci(sztukNaSzuflade, obliczeniaAut);
            //obliczenia.ObliczenieSztuki(sztuki, sztukNaSzuflade, obliczeniaAut); 
            obliczenia.ZestawDoObliczaniaTransportu(sztukNaSzuflade, wyliczone, obliczeniaAut, sztuki, srednia, KGwSkrzynce, obliczeniaAut2, sztukNaSzuflade2);
            sztukNaSzuflade1.Text = sztukNaSzuflade.Text;

            if (int.TryParse(sztukNaSzuflade1.Text, out int value))
            {
                // Odejmij 1 od wartości textbox2 i ustaw w textbox1
                sztukNaSzuflade2.Text = (value + 1).ToString();
            }
            else
            {
                // Jeśli wartość w textbox2 nie jest liczbą, wyczyść textbox1
                sztukNaSzuflade2.Text = string.Empty;
            }

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

            int roznicaDniObecnie = obliczenia.ObliczRozniceDniWstawieniaObecnie(DateTime.Now, dataWstawienia);
            obecnaDoba.Text = roznicaDniObecnie.ToString();
        }
        private void Cena_TextChanged(object sender, EventArgs e)
        {
            nazwaZiD.ReplaceCommaWithDot(Cena);
            DodajAktywnosc(8);
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
                        //zapytaniasql.UpdateDaneAdresoweDostawcy(Dostawca, UlicaH, KodPocztowyH, MiejscH, KmH);
                        //zapytaniasql.UpdateDaneKontaktowe(Dostawca, tel1, tel2, tel3, info1, info2, info3, Email);

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
            int intValue = string.IsNullOrEmpty(lpDostawa) ? 0 : int.Parse(lpDostawa);
            DateTime dzienUbojowy = zapytaniasql.PobierzInformacjeZBazyDanychHarmonogram<DateTime>(intValue, "[LibraNet].[dbo].[HarmonogramDostaw]", "DataOdbioru");

            Dostawa dostawa = new Dostawa("", dzienUbojowy);
            dostawa.UserID = App.UserID;

            // Subscribe to the FormClosed event
            dostawa.FormClosed += (s, args) => MyCalendar_DateChanged_1(sender, null);

            // Wyświetlanie formy Dostawa
            dostawa.Show();
        }
        private void Ubytek_TextChanged(object sender, EventArgs e)
        {
            nazwaZiD.ReplaceCommaWithDot(Ubytek);
        }
        private void buttonCena_Click(object sender, EventArgs e)
        {
            WidokCena widokcena = new WidokCena();
            widokcena.Show();
            DodajAktywnosc(6);
        }
        private void WidokKalendarza_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Zatrzymaj timer przy zamykaniu formularza
            timer.Stop();
            // Zatrzymaj timer przy zamykaniu formularza
            timer2.Stop();
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

            // Initialize fields and execute methods
            wstawienie.WypelnijStartowo();

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
            KGZestaw.Text = "24000";
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
                        value += 3150;
                        // Ustaw nową wartość z separatorem tysięcy
                        KGwPaleciak.Text = value.ToString("N0");
                    }
                    else
                    {
                        // Jeśli zawartość KGwPaleciak nie jest liczbą, ustaw 3000
                        KGwPaleciak.Text = "3150";
                    }
                }
                else
                {
                    // Jeśli KGwPaleciak jest puste, ustaw 3000
                    KGwPaleciak.Text = "3150";
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
            DodajAktywnosc(7);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            ChangeDateByWeeks(-1);
            DodajAktywnosc(7);
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
            sztuki.Text = obliczoneSztuki.Text;
            liczbaAut.Text = obliczoneAuta.Text;
        }

        private void sztukiWstawienia_TextChanged(object sender, EventArgs e)
        {
            //ObliczanieProcentuUbytku(sztukiWstawienia, sztukiPoUpadkach);
        }
        private void ObliczanieProcentuUbytku(TextBox Wstawienie, TextBox Wynik)
        {
            try
            {
                double wstawienie = double.Parse(Wstawienie.Text);
                double wynik = wstawienie * 0.97;
                Wynik.Text = wynik.ToString();
            }
            catch (FormatException)
            {

            }
        }
        private void OObliczaniePozosalychSztuk(TextBox SztukiPoUpadkach, double sumaSztuk, TextBox textboxWynik)
        {
            try
            {
                double sztUpadki = double.Parse(SztukiPoUpadkach.Text);
                double sztSuma = sumaSztuk;
                double wynik = sztUpadki - sztSuma;
                textboxWynik.Text = wynik.ToString();
            }
            catch (FormatException)
            {

            }
        }

        private void sztukiPoUpadkach_TextChanged(object sender, EventArgs e)
        {
            //ObliczanieProcentuUbytku(sztukiWstawienia, sztukiPoUpadkach);

        }

        private void KmH_TextChanged_1(object sender, EventArgs e)
        {
            zapytaniasql.ObliczanieUbytkuTransportowegoNaPodstawieKM(KmH, ubytekProcentowyObliczenie);
        }

        private void button12_Click(object sender, EventArgs e)
        {

            // Tworzenie nowej instancji WidokKalendarza
            WidokWaga Widokwaga = new WidokWaga();

            // Wyświetlanie formularza
            Widokwaga.Show();

        }

        private void button11_Click(object sender, EventArgs e)
        {
            // Sprawdź, czy wybrano wartość w ComboBoxie
            if (Dostawca.SelectedItem != null)
            {
                // Pobierz wybraną wartość z ComboBoxa
                string selectedValue = Dostawca.SelectedItem.ToString();

                // Tworzenie nowej instancji WidokWaga
                WidokWaga widokWaga = new WidokWaga();

                // Ustawienie wartości TextBoxa w WidokWaga
                widokWaga.TextBoxValue = selectedValue;

                // Ustaw wartość TextBoxa przed wyświetleniem formularza
                widokWaga.SetTextBoxValue();

                // Wyświetlanie formularza
                widokWaga.Show();
            }
            else
            {
                MessageBox.Show("Proszę wybrać wartość z listy.");
            }
        }

        private void button10_Click(object sender, EventArgs e)
        {
            // Sprawdź, czy wybrano wartość w ComboBoxie
            if (Dostawca.SelectedItem != null)
            {
                // Pobierz wybraną wartość z ComboBoxa
                string selectedValue = Dostawca.SelectedItem.ToString();

                // Tworzenie nowej instancji WidokWaga
                WidokPaszaPisklak Widokpaszapisklak = new WidokPaszaPisklak();

                // Ustawienie wartości TextBoxa w WidokWaga
                Widokpaszapisklak.TextBoxValue = selectedValue;

                // Ustaw wartość TextBoxa przed wyświetleniem formularza
                Widokpaszapisklak.SetTextBoxValue();

                // Wyświetlanie formularza
                Widokpaszapisklak.Show();
            }
            else
            {
                MessageBox.Show("Proszę wybrać wartość z listy.");
            }
        }

        private void button13_Click(object sender, EventArgs e)
        {

            // Tworzenie nowej instancji WidokWaga
            WidokPaszaPisklak Widokpaszapisklak = new WidokPaszaPisklak();

            // Wyświetlanie formularza
            Widokpaszapisklak.Show();
        }

        private void button9_Click(object sender, EventArgs e)
        {
            // Tworzenie nowej instancji WidokWaga
            WidokWszystkichDostaw widokWszystkichDostaw = new WidokWszystkichDostaw();

            // Wyświetlanie formularza
            widokWszystkichDostaw.Show();
        }

        private void button14_Click(object sender, EventArgs e)
        {
            // Tworzenie nowej instancji WidokWaga
            WidokCenWszystkich widokCenWszystkich = new WidokCenWszystkich();

            // Wyświetlanie formularza
            widokCenWszystkich.Show();
        }

        private void pictureBox8_Click(object sender, EventArgs e)
        {

        }

        private void button15_Click(object sender, EventArgs e)
        {
            WidokAvilogPlan widokAvilogPlan = new WidokAvilogPlan();
            widokAvilogPlan.Show();
        }

        private void SMSupomnienie_Click(object sender, EventArgs e)
        {
            string destinationPhoneNumber = "+48506262541";
            string DataTXT = Data.Text;
            string sztukiTXT = sztuki.Text;
            string sredniaTXT = srednia.Text;
            string messageBody = $"Witam. Prosimy o zaaakceptowanie dostawy na dzień ubojowy {DataTXT}. Sztuki dek.:{sztukiTXT} szt, waga dek.: {sredniaTXT} kg. Prosimy o kontakt telefoniczny lub SMS z jednym z naszych przedstawicieli w celu potwierdzenia. Ubojnia Drobiu Piórkowscy.";
            SmsSender.SendSms(destinationPhoneNumber, messageBody);
        }
        private void KolorZielonyCheckbox(CheckBox checkBox, TextBox textBox)
        {
            if (checkBox.Checked)
            {
                textBox.BackColor = Color.LightGreen;
            }
            else
            {
                textBox.BackColor = SystemColors.Window; // Ustawia kolor na domyślny kolor TextBoxa
            }
        }
        private void potwWaga_CheckedChanged(object sender, EventArgs e)
        {
            // Sprawdź, czy zmiana stanu CheckBox została wywołana przez użytkownika
            if (potwWaga.Focused)
            {
                nazwaZiD.AktualizacjaPotwZDostaw(lpDostawa, potwWaga, "PotwWaga", UserID, "KtoWaga", "KiedyWaga");
                KolorZielonyCheckbox(potwWaga, srednia);
                MyCalendar_DateChanged_1(sender, null);
            }
        }
        private void potwSztuki_CheckedChanged_1(object sender, EventArgs e)
        {
            if (potwSztuki.Focused)
            {
                nazwaZiD.AktualizacjaPotwZDostaw(lpDostawa, potwSztuki, "PotwSztuki", UserID, "KtoSztuki", "KiedySztuki");
                KolorZielonyCheckbox(potwSztuki, sztuki);
                MyCalendar_DateChanged_1(sender, null);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            // Poproś użytkownika o potwierdzenie usunięcia
            var response = MessageBox.Show("Czy na pewno chcesz usunąć ten wiersz? Nie lepiej anulować?", "Potwierdź usunięcie", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
            if (response != DialogResult.Yes)
                return;

            // Utwórz połączenie z bazą danych
            using (SqlConnection cnn = new SqlConnection(connectionPermission))
            {
                try
                {
                    cnn.Open();

                    // Utwórz zapytanie SQL do usunięcia wiersza
                    string strSQL = "DELETE FROM dbo.HarmonogramDostaw WHERE Lp = @selectedLP;";

                    // Wykonaj zapytanie SQL
                    using (SqlCommand cmd = new SqlCommand(strSQL, cnn))
                    {
                        cmd.Parameters.AddWithValue("@selectedLP", lpDostawa);
                        cmd.ExecuteNonQuery();
                    }

                    // Komunikat potwierdzający
                    MessageBox.Show("Wiersz został usunięty z bazy danych.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);


                }
                catch (Exception ex)
                {
                    MessageBox.Show("Wystąpił błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                MyCalendar_DateChanged_1(sender, null);
                DodajAktywnosc(4);
            }
        }

        private void KGwSkrzynce2_TextChanged(object sender, EventArgs e)
        {

            {
                KGZestaw2.Text = "24000";
                // Sprawdź, czy KGwSkrzynce nie jest puste
                if (!string.IsNullOrEmpty(KGwSkrzynce2.Text))
                {
                    // Spróbuj przekonwertować zawartość KGwSkrzynce na liczbę
                    if (double.TryParse(KGwSkrzynce2.Text, out double value))
                    {
                        // Pomnóż wartość przez 264 i wyświetl wynik w KGwSkrzynekWAucie
                        double result = value * 264;
                        KGwSkrzynekWAucie2.Text = result.ToString("N0"); // "N0" formatuje do liczby całkowitej z separatorem tysięcy
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
                    KGwSkrzynekWAucie2.Clear();
                }
                DodajTextBoxy(KGwSkrzynekWAucie2, KGwPaleciak2, KGZestaw2, KGSuma2);

            }
        }


        private void KGwSkrzynekWAucie2_TextChanged(object sender, EventArgs e)
        {
            DodajTextBoxy(KGwSkrzynekWAucie2, KGwPaleciak2, KGZestaw2, KGSuma2);
        }

        private void KGwPaleciak2_TextChanged(object sender, EventArgs e)
        {
            DodajTextBoxy(KGwSkrzynekWAucie2, KGwPaleciak2, KGZestaw2, KGSuma2);
        }

        private void KGZestaw2_TextChanged(object sender, EventArgs e)
        {
            DodajTextBoxy(KGwSkrzynekWAucie2, KGwPaleciak2, KGZestaw2, KGSuma2);
        }

        private void sztukNaSzuflade1_TextChanged(object sender, EventArgs e)
        {

        }

        private void sztukNaSzuflade2_TextChanged(object sender, EventArgs e)
        {
            //obliczenia.ObliczenieAutaCzySieMiesci(sztukNaSzuflade2, obliczeniaAut2);
            obliczenia.ZestawDoObliczaniaTransportu(sztukNaSzuflade, wyliczone, obliczeniaAut, sztuki, srednia, KGwSkrzynce, obliczeniaAut2, sztukNaSzuflade2);
        }

        private void pokazDlaSprzedazy_Click(object sender, EventArgs e)
        {
            WidokSprzedazPlan widokSprzedazPlan = new WidokSprzedazPlan();
            widokSprzedazPlan.Show();
        }

        private void button16_Click(object sender, EventArgs e)
        {
            try
            {
                using (SqlConnection cnn = new SqlConnection(connectionPermission))
                {
                    cnn.Open();

                    // Pobranie istniejącego wiersza do zduplikowania
                    string getRowSql = "SELECT * FROM dbo.HarmonogramDostaw WHERE Lp = @selectedLP;";
                    SqlCommand getRowCmd = new SqlCommand(getRowSql, cnn);
                    getRowCmd.Parameters.AddWithValue("@selectedLP", lpDostawa);
                    DataTable dt = new DataTable();
                    using (SqlDataAdapter da = new SqlDataAdapter(getRowCmd))
                    {
                        da.Fill(dt);
                    }

                    if (dt.Rows.Count == 0)
                    {
                        MessageBox.Show("Nie znaleziono wiersza do duplikacji.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    DataRow row = dt.Rows[0];

                    // Pobranie maksymalnego LP
                    string getMaxLpSql = "SELECT MAX(Lp) AS MaxLP FROM dbo.HarmonogramDostaw;";
                    SqlCommand getMaxLpCmd = new SqlCommand(getMaxLpSql, cnn);
                    int maxLP = Convert.ToInt32(getMaxLpCmd.ExecuteScalar()) + 1;

                    // Utworzenie zapytania SQL do wstawienia danych
                    string insertSql = @"
                    INSERT INTO dbo.HarmonogramDostaw 
                    (Lp, DataOdbioru, Dostawca, KmH, Kurnik, KmK, Auta, SztukiDek, WagaDek, 
                    SztSzuflada, TypUmowy, TypCeny, Cena, Bufor, UWAGI, Dodatek, DataUtw, LpW, Ubytek, ktoStwo) 
                    VALUES 
                    (@Lp, @DataOdbioru, @Dostawca, @KmH, @Kurnik, @KmK, @Auta, @SztukiDek, @WagaDek, 
                    @SztSzuflada, @TypUmowy, @TypCeny, @Cena, @Bufor, @UWAGI, @Dodatek, @DataUtw, @LpW, @Ubytek, @)";

                    SqlCommand insertCmd = new SqlCommand(insertSql, cnn);
                    insertCmd.Parameters.AddWithValue("@Lp", maxLP);
                    insertCmd.Parameters.AddWithValue("@DataOdbioru", row["DataOdbioru"] != DBNull.Value ? row["DataOdbioru"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@Dostawca", row["Dostawca"] != DBNull.Value ? row["Dostawca"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@KmH", row["KmH"] != DBNull.Value ? row["KmH"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@Kurnik", row["Kurnik"] != DBNull.Value ? row["Kurnik"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@KmK", row["KmK"] != DBNull.Value ? row["KmK"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@Auta", row["Auta"] != DBNull.Value ? row["Auta"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@SztukiDek", row["SztukiDek"] != DBNull.Value ? row["SztukiDek"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@WagaDek", row["WagaDek"] != DBNull.Value ? row["WagaDek"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@SztSzuflada", row["SztSzuflada"] != DBNull.Value ? row["SztSzuflada"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@TypUmowy", row["TypUmowy"] != DBNull.Value ? row["TypUmowy"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@TypCeny", row["TypCeny"] != DBNull.Value ? row["TypCeny"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@Cena", row["Cena"] != DBNull.Value ? row["Cena"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@Bufor", row["Bufor"] != DBNull.Value ? row["Bufor"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@UWAGI", row["UWAGI"] != DBNull.Value ? row["UWAGI"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@Dodatek", row["Dodatek"] != DBNull.Value ? row["Dodatek"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@DataUtw", DateTime.Now);
                    insertCmd.Parameters.AddWithValue("@LpW", row["LpW"] != DBNull.Value ? row["LpW"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@Ubytek", row["Ubytek"] != DBNull.Value ? row["Ubytek"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@ktoStwo", UserID);

                    insertCmd.ExecuteNonQuery();

                    // Komunikat potwierdzający
                    MessageBox.Show("Wiersz został zduplikowany w bazie danych.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            DodajAktywnosc(3);
            MyCalendar_DateChanged_1(sender, null);
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            MyCalendar_DateChanged_1(sender, null);
        }

        private void button17_Click(object sender, EventArgs e)
        {
            PokazCeny();
            MyCalendar_DateChanged_1(this, new DateRangeEventArgs(DateTime.Today, DateTime.Today));
            BiezacePartie();
            nazwaZiD.PokazPojTuszki(dataGridSumaPartie);
        }

        private void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
            // Pobierz wybraną datę z DateTimePicker
            DateTime selectedDate = dateTimePicker1.Value;

            // Wywołaj metodę z przekazaną datą i kontrolką DataGridView
            zapytaniasql.PokazwTabeliRozliczeniaAvilogazDanegoDnia(selectedDate, dataGridAvilog);
        }
        private void dataGridAvilog_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return; // Pomijamy nagłówki

            var grid = (DataGridView)sender;

            // Sprawdź, czy kolumna istnieje przed kontynuowaniem
            if (!grid.Columns.Contains("Dostawca") || !grid.Columns.Contains("Km-") || !grid.Columns.Contains("P"))
            {
                return; // Pomijamy jeśli brakuje kluczowych kolumn
            }

            string columnName = grid.Columns[e.ColumnIndex].Name;

            // Podświetlanie dla kolumny "Km-"
            if (columnName == "Km-")
            {
                if (e.Value != null && int.TryParse(e.Value.ToString(), out int value) && value > 20)
                {
                    e.CellStyle.BackColor = Color.Red; // Tło na czerwono
                    e.CellStyle.ForeColor = Color.White; // Tekst na biało
                }

                // Dodaj " km" do wartości
                if (e.Value != null && int.TryParse(e.Value.ToString(), out value))
                {
                    e.Value = $"{value} km";
                    e.FormattingApplied = true;
                }
            }

            // Podświetlanie dla kolumny "P"
            if (columnName == "P")
            {
                if (e.Value != null && int.TryParse(e.Value.ToString(), out int value) && value > 25)
                {
                    e.CellStyle.BackColor = Color.Red; // Tło na czerwono
                    e.CellStyle.ForeColor = Color.White; // Tekst na biało
                }

                // Dodaj " szt" do wartości
                if (e.Value != null && int.TryParse(e.Value.ToString(), out value))
                {
                    e.Value = $"{value} szt";
                    e.FormattingApplied = true;
                }
            }

            // Dodawanie " km" do kolumn "KmAvi" i "KmHod"
            if (columnName == "KmAvi" || columnName == "KmHod")
            {
                if (e.Value != null && int.TryParse(e.Value.ToString(), out int value))
                {
                    e.Value = $"{value} km";
                    e.FormattingApplied = true;
                }
            }

            // Dodaj podświetlanie dla kolumn "Dojazd", "Zaladunek", "Przyjazd"
            if (columnName == "Dojazd" || columnName == "Zaladunek" || columnName == "Przyjazd")
            {
                // Pobierz nazwę dostawcy dla bieżącego wiersza
                string dostawca = grid.Rows[e.RowIndex].Cells["Dostawca"].Value?.ToString();

                // Pobierz wartości wszystkich wierszy dla tego dostawcy
                var valuesForDostawca = grid.Rows.Cast<DataGridViewRow>()
                    .Where(row => row.Cells["Dostawca"].Value?.ToString() == dostawca)
                    .Select(row => new
                    {
                        ColumnName = columnName,
                        Value = int.TryParse(row.Cells[columnName].Value?.ToString(), out int result) ? result : (int?)null
                    })
                    .Where(v => v.Value.HasValue && v.Value.Value > 0) // Ignorujemy wartości 0
                    .ToList();

                // Sprawdź, ile wierszy ma ten dostawca
                int rowCount = valuesForDostawca.Count;

                // Znajdź największą wartość dla tej kolumny i tego dostawcy
                int? maxValue = valuesForDostawca.Max(v => v.Value);

                // Sprawdź, ile wartości jest równych maksymalnej
                int maxCount = valuesForDostawca.Count(v => v.Value == maxValue);

                // Jeśli więcej niż 70% wartości to maksymalne, nie podświetlaj
                if (rowCount > 0 && maxValue.HasValue && maxCount <= (0.7 * rowCount))
                {
                    // Jeśli bieżąca wartość to największa wartość, zmień kolor
                    if (e.Value != null && int.TryParse(e.Value.ToString(), out int currentValue) && currentValue == maxValue)
                    {
                        e.CellStyle.BackColor = Color.Red; // Tło na czerwono
                        e.CellStyle.ForeColor = Color.White; // Tekst na biało
                    }
                }

                // Dodaj " min" do wartości
                if (e.Value != null && int.TryParse(e.Value.ToString(), out int value))
                {
                    e.Value = $"{value} min";
                    e.FormattingApplied = true;
                }
            }

            // Dostosowanie stylu siatki
            zapytaniasql.stylGridaPodstawowy(dataGridAvilog);
        }

        private async void pictureBox14_Click(object sender, EventArgs e)
        {
            var dataService = new DataService(); // Tworzenie instancji klasy
            await dataService.CalculateAverageSpeed(UlicaH, KodPocztowyH); // Przekazanie TextBoxów
        }

        private void updateInfoBotton_Click(object sender, EventArgs e)
        {


            // Wyciągnięcie tekstu z ComboBoxa/TextBoxa dostawcy
            string dostawcaNazwa = Dostawca.Text;
            string idHodowca = zapytaniasql.ZnajdzIdHodowcyString(dostawcaNazwa);

            // Wykonanie aktualizacji danych kontaktowych i adresowych
            zapytaniasql.UpdateDaneAdresoweDostawcy(idHodowca, UlicaH, KodPocztowyH, MiejscH, KmH);
            zapytaniasql.UpdateDaneKontaktowe(idHodowca, tel1, tel2, tel3, info1, info2, info3, Email, comboBoxOsobowosc, comboBoxOsobowosc2);



            MyCalendar_DateChanged_1(this, new DateRangeEventArgs(DateTime.Today, DateTime.Today));
            DodajAktywnosc(5);
        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {

        }
        private void UstawPoradyDlaOsobowosci()
        {
            string o1 = comboBoxOsobowosc.Text.Trim();
            string o2 = comboBoxOsobowosc2.Text.Trim();
            string typ = (string.IsNullOrWhiteSpace(o2) ? o1 : $"{o1}-{o2}");
            string porada = "";

            switch (typ)
            {
                case "Na Cel":
                    porada =
                        "Styl: Szybki, konkretny, nastawiony na wynik\n" +
                        "Jak rozmawiać:\n" +
                        "- Mów konkretnie: ile, za ile, na kiedy\n" +
                        "- Daj wybór: '4,80 dzisiaj, 4,85 jutro – decyduj'\n" +
                        "- Nie tłumacz się – liczby mówią same za siebie\n" +
                        "- Pokazuj zysk: 'tu zarobisz więcej'\n" +
                        "Osoba: Tereska – zdecydowana i konkretna.";
                    break;

                case "Wpływowy":
                    porada =
                        "Styl: Rozmowny, emocjonalny, otwarty\n" +
                        "Jak rozmawiać:\n" +
                        "- Zacznij rozmowę od small talku, nie od ceny\n" +
                        "- Ustal cel z humorem: 'Jak zwykle coś ugrasz, co?'\n" +
                        "- Pochwal towar, wspomnij o innych klientach\n" +
                        "- Cena? Powiedz: 'Tyle daję, bo Ci ufam'\n" +
                        "Osoba: Tereska – dynamiczna i kontaktowa.";
                    break;

                case "Relacyjny":
                    porada =
                        "Styl: Spokojny, lojalny, niekonfliktowy\n" +
                        "Jak rozmawiać:\n" +
                        "- Nie wywieraj presji – on potrzebuje czasu\n" +
                        "- Mów: 'Zawsze się dogadujemy, nie?' \n" +
                        "- Uprzedź każdą zmianę – nie lubi niespodzianek\n" +
                        "- Zapytaj co u niego – to ważniejsze niż cena\n" +
                        "Osoba: Paulina – spokojna i empatyczna.";
                    break;

                case "Analityk":
                    porada =
                        "Styl: Precyzyjny, logiczny, ostrożny\n" +
                        "Jak rozmawiać:\n" +
                        "- Przygotuj tabelę cen, wydajności, historii\n" +
                        "- Nie gadaj – pokazuj dane\n" +
                        "- Zostaw mu czas na przemyślenie\n" +
                        "- Pokaż różnicę: 'przy tej cenie zyskujesz X zł'\n" +
                        "Osoba: Paulina – cierpliwa i przygotowana.";
                    break;

                case "Analityk-Na Cel":
                case "Na Cel-Analityk":
                    porada =
                        "Styl: Twardy, konkretny, analityczny\n" +
                        "Jak rozmawiać:\n" +
                        "- Pokaż mu tabelę + decyzję: 'Tu masz liczby, decyduj'\n" +
                        "- Nie lej wody – zero historii, sama esencja\n" +
                        "- Cena: pokaż mu co zyskuje i czemu tak\n" +
                        "- Wysyłaj oferty na maila z wyliczeniami\n" +
                        "Osoba: Tereska – konkretna i szybka.";
                    break;

                case "Analityk-Relacyjny":
                case "Relacyjny-Analityk":
                    porada =
                        "Styl: Dokładny, lojalny, przewidywalny\n" +
                        "Jak rozmawiać:\n" +
                        "- Rozpisz dokładnie każdą opcję\n" +
                        "- Powiedz mu spokojnie: 'Masz czas, przemyśl'\n" +
                        "- Nie dzwoń po godzinie – poczekaj dzień\n" +
                        "- Cena? Podkreśl uczciwość i stałość\n" +
                        "Osoba: Paulina – cierpliwa i rzeczowa.";
                    break;

                case "Analityk-Wpływowy":
                case "Wpływowy-Analityk":
                    porada =
                        "Styl: Rozdarty – potrzebuje danych, ale lubi pogadać\n" +
                        "Jak rozmawiać:\n" +
                        "- Najpierw relacja – potem prezentacja danych\n" +
                        "- Ustal cenę żartem, ale pokaż tabelkę\n" +
                        "- Przygotuj PDF z porównaniem\n" +
                        "- Powiedz: 'To uczciwa cena – sprawdź sam'\n" +
                        "Osoba: Paulina – łączy dane i relację.";
                    break;

                case "Dominujący-Relacyjny":
                case "Relacyjny-Dominujący":
                    porada =
                        "Styl: Rządzi, ale szanuje relacje\n" +
                        "Jak rozmawiać:\n" +
                        "- Mów krótko: 'Daję 4,85, jak zawsze, ok?'\n" +
                        "- Nie spieraj się – pokaż szacunek\n" +
                        "- Cena? Stała – bo ufa Ci\n" +
                        "- Doceniaj lojalność: 'Tacy jak Ty to podstawa'\n" +
                        "Osoba: Paulina – łagodna i opanowana.";
                    break;

                case "Dominujący-Wpływowy":
                case "Wpływowy-Dominujący":
                    porada =
                        "Styl: Szybki, emocjonalny, chce wygrać\n" +
                        "Jak rozmawiać:\n" +
                        "- Bądź odważny, ale trzymaj ramy\n" +
                        "- Cena? Daj jedno zdanie: 'To moja najlepsza oferta'\n" +
                        "- Pozwól mu mówić – i wracaj do celu\n" +
                        "- Zakończ mocno: 'Dogadane. Działamy?'\n" +
                        "Osoba: Tereska – zdecydowana i przebojowa.";
                    break;

                case "Relacyjny-Wpływowy":
                case "Wpływowy-Relacyjny":
                    porada =
                        "Styl: Ciepły, ale negocjacyjny między wierszami\n" +
                        "Jak rozmawiać:\n" +
                        "- Zacznij rozmowę od relacji, potem płynnie do ceny\n" +
                        "- Cena? Powiedz: 'Dla Ciebie, jak zawsze – uczciwie'\n" +
                        "- Nie wchodź w konflikt – żartuj, ale ustal granice\n" +
                        "- Powiedz: 'Zawsze się dogadamy, jak zwykle'\n" +
                        "Osoba: Paulina – spokojna i elastyczna.";
                    break;

                default:
                    porada = "Brak wybranej lub rozpoznanej kombinacji osobowości.";
                    break;
            }

            //textBox3.Text = porada;
        }

        private void buttonNotatka_Click(object sender, EventArgs e)
        {
            int indeksId = string.IsNullOrEmpty(lpDostawa) ? 0 : int.Parse(lpDostawa);
            string tresc = Uwagi.Text?.Trim();

            if (indeksId == 0 || string.IsNullOrWhiteSpace(tresc))
            {
                MessageBox.Show("Brak numeru LP lub treść notatki jest pusta.");
                return;
            }

            using (SqlConnection conn = new SqlConnection(connectionPermission))
            {
                try
                {
                    conn.Open();

                    string query = @"
                INSERT INTO [LibraNet].[dbo].[Notatki] (IndeksID, TypID, Tresc, KtoStworzyl, DataUtworzenia)
                VALUES (@IndeksID, @TypID, @Tresc, @KtoStworzyl, @DataUtworzenia);

                SELECT SCOPE_IDENTITY();";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@IndeksID", indeksId);
                        cmd.Parameters.AddWithValue("@TypID", 1); // Stała wartość
                        cmd.Parameters.AddWithValue("@Tresc", tresc);
                        cmd.Parameters.AddWithValue("@KtoStworzyl", UserID);
                        cmd.Parameters.AddWithValue("@DataUtworzenia", DateTime.Now);

                        // Pobierz nowy NotatkaID
                        int newNoteId = Convert.ToInt32(cmd.ExecuteScalar());

                        MessageBox.Show($"Dodano notatkę o treści : {newNoteId}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd przy dodawaniu notatki: " + ex.Message);
                }
                MyCalendar_DateChanged_1(sender, null);
                if (int.TryParse(lpDostawa, out int dostawaId))
                {
                    WczytajNotatki(dostawaId);
                }
            }
        }

        private void dataGridView1_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var hitTest = dataGridView1.HitTest(e.X, e.Y);
                if (hitTest.RowIndex >= 0)
                {
                    dataGridView1.ClearSelection();
                    dataGridView1.Rows[hitTest.RowIndex].Selected = true;
                    selectedRowIndex = hitTest.RowIndex;
                }
                else
                {
                    selectedRowIndex = -1;
                }
            }
        }

        private void Dubluj_Click(object sender, EventArgs e)
        {
            try
            {
                using (SqlConnection cnn = new SqlConnection(connectionPermission))
                {
                    cnn.Open();

                    // Pobranie istniejącego wiersza do zduplikowania
                    string getRowSql = "SELECT * FROM dbo.HarmonogramDostaw WHERE Lp = @selectedLP;";
                    SqlCommand getRowCmd = new SqlCommand(getRowSql, cnn);
                    getRowCmd.Parameters.AddWithValue("@selectedLP", lpDostawa);
                    DataTable dt = new DataTable();
                    using (SqlDataAdapter da = new SqlDataAdapter(getRowCmd))
                    {
                        da.Fill(dt);
                    }

                    if (dt.Rows.Count == 0)
                    {
                        MessageBox.Show("Nie znaleziono wiersza do duplikacji.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    DataRow row = dt.Rows[0];

                    // Pobranie maksymalnego LP
                    string getMaxLpSql = "SELECT MAX(Lp) AS MaxLP FROM dbo.HarmonogramDostaw;";
                    SqlCommand getMaxLpCmd = new SqlCommand(getMaxLpSql, cnn);
                    int maxLP = Convert.ToInt32(getMaxLpCmd.ExecuteScalar()) + 1;

                    // Utworzenie zapytania SQL do wstawienia danych
                    string insertSql = @"
                    INSERT INTO dbo.HarmonogramDostaw 
                    (Lp, DataOdbioru, Dostawca, KmH, Kurnik, KmK, Auta, SztukiDek, WagaDek, 
                    SztSzuflada, TypUmowy, TypCeny, Cena, Bufor, UWAGI, Dodatek, DataUtw, LpW, Ubytek, ktoStwo) 
                    VALUES 
                    (@Lp, @DataOdbioru, @Dostawca, @KmH, @Kurnik, @KmK, @Auta, @SztukiDek, @WagaDek, 
                    @SztSzuflada, @TypUmowy, @TypCeny, @Cena, @Bufor, @UWAGI, @Dodatek, @DataUtw, @LpW, @Ubytek, @ktoStwo)";

                    SqlCommand insertCmd = new SqlCommand(insertSql, cnn);
                    insertCmd.Parameters.AddWithValue("@Lp", maxLP);
                    insertCmd.Parameters.AddWithValue("@DataOdbioru", row["DataOdbioru"] != DBNull.Value ? row["DataOdbioru"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@Dostawca", row["Dostawca"] != DBNull.Value ? row["Dostawca"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@KmH", row["KmH"] != DBNull.Value ? row["KmH"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@Kurnik", row["Kurnik"] != DBNull.Value ? row["Kurnik"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@KmK", row["KmK"] != DBNull.Value ? row["KmK"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@Auta", row["Auta"] != DBNull.Value ? row["Auta"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@SztukiDek", row["SztukiDek"] != DBNull.Value ? row["SztukiDek"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@WagaDek", row["WagaDek"] != DBNull.Value ? row["WagaDek"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@SztSzuflada", row["SztSzuflada"] != DBNull.Value ? row["SztSzuflada"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@TypUmowy", row["TypUmowy"] != DBNull.Value ? row["TypUmowy"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@TypCeny", row["TypCeny"] != DBNull.Value ? row["TypCeny"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@Cena", row["Cena"] != DBNull.Value ? row["Cena"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@Bufor", row["Bufor"] != DBNull.Value ? row["Bufor"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@UWAGI", row["UWAGI"] != DBNull.Value ? row["UWAGI"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@Dodatek", row["Dodatek"] != DBNull.Value ? row["Dodatek"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@DataUtw", DateTime.Now);
                    insertCmd.Parameters.AddWithValue("@LpW", row["LpW"] != DBNull.Value ? row["LpW"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@Ubytek", row["Ubytek"] != DBNull.Value ? row["Ubytek"] : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@ktoStwo", UserID);

                    insertCmd.ExecuteNonQuery();

                    // Komunikat potwierdzający
                    MessageBox.Show("Wiersz został zduplikowany w bazie danych.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            MyCalendar_DateChanged_1(sender, null);
        }

        private void Anuluj_Click(object sender, EventArgs e)
        {
            int intValue = string.IsNullOrEmpty(lpDostawa) ? 0 : int.Parse(lpDostawa);
            DateTime dzienUbojowy = zapytaniasql.PobierzInformacjeZBazyDanychHarmonogram<DateTime>(intValue, "[LibraNet].[dbo].[HarmonogramDostaw]", "DataOdbioru");

            Dostawa dostawa = new Dostawa("", dzienUbojowy);
            dostawa.UserID = App.UserID;

            // Subscribe to the FormClosed event
            dostawa.FormClosed += (s, args) => MyCalendar_DateChanged_1(sender, null);

            // Wyświetlanie formy Dostawa
            dostawa.Show();
        }

        private void Usuń_Click(object sender, EventArgs e)
        {
            // Poproś użytkownika o potwierdzenie usunięcia
            var response = MessageBox.Show("Czy na pewno chcesz usunąć ten wiersz? Nie lepiej anulować?", "Potwierdź usunięcie", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
            if (response != DialogResult.Yes)
                return;

            // Utwórz połączenie z bazą danych
            using (SqlConnection cnn = new SqlConnection(connectionPermission))
            {
                try
                {
                    cnn.Open();

                    // Utwórz zapytanie SQL do usunięcia wiersza
                    string strSQL = "DELETE FROM dbo.HarmonogramDostaw WHERE Lp = @selectedLP;";

                    // Wykonaj zapytanie SQL
                    using (SqlCommand cmd = new SqlCommand(strSQL, cnn))
                    {
                        cmd.Parameters.AddWithValue("@selectedLP", lpDostawa);
                        cmd.ExecuteNonQuery();
                    }

                    // Komunikat potwierdzający
                    MessageBox.Show("Wiersz został usunięty z bazy danych.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);


                }
                catch (Exception ex)
                {
                    MessageBox.Show("Wystąpił błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                MyCalendar_DateChanged_1(sender, null);
                DodajAktywnosc(4);
            }
        }

        private void buttonModWstawienie_Click(object sender, EventArgs e)
        {
            try
            {
                using (SqlConnection cnn = new SqlConnection(connectionPermission))
                {
                    cnn.Open();

                    string strSQL = @"
                UPDATE dbo.WstawieniaKurczakow
                SET DataWstawienia = @dataWstawienia,
                    DataMod = @DataMod,
                    KtoMod = @KtoMod
                WHERE Lp = @LpWstawienia;";

                    using (SqlCommand command = new SqlCommand(strSQL, cnn))
                    {
                        // Zakładamy, że dataWstawienia.Text to textbox z datą
                        command.Parameters.AddWithValue("@dataWstawienia", string.IsNullOrEmpty(dataWstawienia.Text)
                            ? (object)DBNull.Value
                            : DateTime.Parse(dataWstawienia.Text).Date);

                        // Ustawiamy bieżącą datę i godzinę
                        command.Parameters.AddWithValue("@DataMod", DateTime.Now);

                        // Ustawiamy identyfikator użytkownika
                        command.Parameters.AddWithValue("@KtoMod", UserID);

                        // Lp z tabeli WstawieniaKurczakow – przekazywane np. z textboxa lub wybranego wiersza
                        command.Parameters.AddWithValue("@LpWstawienia", int.Parse(LpWstawienia.Text));

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            MessageBox.Show("Wstawienie zostało zaktualizowane.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show("Nie znaleziono takiego wstawienia.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas aktualizacji: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            MyCalendar_DateChanged_1(sender, null);
        }
        private void DodajAktywnosc(int typLicznika)
        {
            using (SqlConnection conn = new SqlConnection(connectionPermission))
            {
                conn.Open();

                int nextLp;
                using (SqlCommand getMaxCmd = new SqlCommand("SELECT ISNULL(MAX(Lp), 0) + 1 FROM Aktywnosc", conn))
                {
                    nextLp = (int)getMaxCmd.ExecuteScalar();
                }

                string insertQuery = @"
            INSERT INTO Aktywnosc (Lp, Licznik, TypLicznika, KtoStworzyl, Data)
            VALUES (@Lp, @Licznik, @TypLicznika, @KtoStworzyl, @Data)";

                using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Lp", nextLp);
                    cmd.Parameters.AddWithValue("@Licznik", 1);
                    cmd.Parameters.AddWithValue("@TypLicznika", typLicznika);
                    cmd.Parameters.AddWithValue("@KtoStworzyl", App.UserID); // ⬅️ to działa!
                    cmd.Parameters.AddWithValue("@Data", DateTime.Now);

                    cmd.ExecuteNonQuery();
                }
            }
        }


        private void Status_SelectedIndexChanged(object sender, EventArgs e)
        {
            //DodajAktywnosc(2);
        }

        private void buttonModHodowca_Click(object sender, EventArgs e)
        {
            // 1) Pobierz nazwę z ComboBoxa/TextBoxa
            string nazwa = Dostawca.Text?.Trim();
            if (string.IsNullOrWhiteSpace(nazwa))
            {
                MessageBox.Show("Wybierz hodowcę z listy lub wpisz jego nazwę.");
                return;
            }

            // 2) Znajdź ID (ID w bazie to VARCHAR → traktujemy jako string)
            string idHodowca = zapytaniasql.ZnajdzIdHodowcyString(nazwa);
            if (string.IsNullOrWhiteSpace(idHodowca))
            {
                MessageBox.Show("Nie znaleziono hodowcy o tej nazwie.");
                return;
            }

            // 3) Ustal kto wykonuje operację: preferuj UserID, a jak puste – weź nazwę z systemu
            string appUser = string.IsNullOrWhiteSpace(this.UserID) ? Environment.UserName : this.UserID;

            // 4) Otwórz formularz kartoteki z ID hodowcy i UserID
            using (var f = new HodowcaForm(idHodowca, appUser))
                f.ShowDialog(this);
        }

        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {

        }
    }
}


     