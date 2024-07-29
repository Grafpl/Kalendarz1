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


//test2

namespace Kalendarz1
{
    public partial class Wstawienie : Form
    {
        private MojeObliczenia obliczenia = new MojeObliczenia();
        private WidokKalendarza kalendarz = new WidokKalendarza();
        private NazwaZiD nazwaZiD = new NazwaZiD();
        private CenoweMetody CenoweMetody = new CenoweMetody();
        static string connectionPermission = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        public string UserID { get; set; }

        public Wstawienie()
        {
            InitializeComponent();
            FillComboBox();
        }


        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void FillComboBox()
        {
            string query = "SELECT DISTINCT Name FROM dbo.DOSTAWCY";

            using (SqlConnection connection = new SqlConnection(connectionPermission))
            {
                SqlCommand command = new SqlCommand(query, connection);
                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string dostawca = reader["Name"].ToString();
                    Dostawca.Items.Add(dostawca);
                }

                reader.Close();
            }
        }


        private void srednia1_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ileSztukOblcizenie(sztukNaSzuflade1, wyliczone);
            nazwaZiD.ReplaceCommaWithDot(srednia1);
        }

        private void srednia2_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ileSztukOblcizenie(sztukNaSzuflade2, wyliczone);
            nazwaZiD.ReplaceCommaWithDot(srednia2);
        }

        private void srednia3_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ileSztukOblcizenie(sztukNaSzuflade3, wyliczone);
            nazwaZiD.ReplaceCommaWithDot(srednia3);
        }

        private void srednia4_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ileSztukOblcizenie(sztukNaSzuflade4, wyliczone);
            nazwaZiD.ReplaceCommaWithDot(srednia4);
        }

        private void srednia5_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ileSztukOblcizenie(sztukNaSzuflade5, wyliczone);
            nazwaZiD.ReplaceCommaWithDot(srednia5);
        }

        private void sztukNaSzuflade1_TextChanged(object sender, EventArgs e)
        {

        }

        private void sztuki1_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ObliczenieSztuki(sztuki1, sztukNaSzuflade1, obliczeniaAut1);
        }

        private void sztuki2_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ObliczenieSztuki(sztuki2, sztukNaSzuflade2, obliczeniaAut2);
        }

        private void sztuki3_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ObliczenieSztuki(sztuki3, sztukNaSzuflade3, obliczeniaAut3);
        }

        private void sztuki4_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ObliczenieSztuki(sztuki4, sztukNaSzuflade4, obliczeniaAut4);
        }

        private void sztuki5_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ObliczenieSztuki(sztuki5, sztukNaSzuflade5, obliczeniaAut5);
        }

        private void Data1_ValueChanged(object sender, EventArgs e)
        {
            int roznicaDni = obliczenia.ObliczRozniceDni(Data1, dataWstawienia);
            RoznicaDni1.Text = roznicaDni.ToString();
        }

        private void Data2_ValueChanged(object sender, EventArgs e)
        {
            ;
            int roznicaDni = obliczenia.ObliczRozniceDni(Data2, dataWstawienia);
            RoznicaDni2.Text = roznicaDni.ToString();
        }

        private void Data3_ValueChanged(object sender, EventArgs e)
        {
            int roznicaDni = obliczenia.ObliczRozniceDni(Data3, dataWstawienia);
            RoznicaDni3.Text = roznicaDni.ToString();
        }

        private void Data4_ValueChanged(object sender, EventArgs e)
        {
            int roznicaDni = obliczenia.ObliczRozniceDni(Data4, dataWstawienia);
            RoznicaDni4.Text = roznicaDni.ToString();
        }

        private void Data5_ValueChanged(object sender, EventArgs e)
        {
            int roznicaDni = obliczenia.ObliczRozniceDni(Data5, dataWstawienia);
            RoznicaDni5.Text = roznicaDni.ToString();
        }

        private void dataWstawienia_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionPermission))
                {
                    connection.Open();

                    DateTime selectedDate = dataWstawienia.Value;
                    DateTime startOfWeek = selectedDate.AddDays(37).AddDays(-(int)selectedDate.DayOfWeek);
                    DateTime endOfWeek = startOfWeek.AddDays(13);

                    string strSQL = $@"
                SELECT HD.LP, HD.DataOdbioru, HD.Dostawca, HD.Auta, HD.SztukiDek, HD.WagaDek, HD.bufor, HD.TypCeny, HD.Cena, WK.DataWstawienia
                FROM HarmonogramDostaw HD
                LEFT JOIN WstawieniaKurczakow WK ON HD.LpW = WK.Lp
                WHERE HD.DataOdbioru >= @startDate AND HD.DataOdbioru <= @endDate
                ORDER BY HD.DataOdbioru, HD.bufor, HD.WagaDek Desc";

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

                            dataGridView1.Columns["LP"].Visible = false;
                            dataGridView1.Columns["DataOdbioruKolumna"].Visible = true;

                            dataGridView1.Columns["LP"].Width = 50;
                            dataGridView1.Columns["DataOdbioruKolumna"].Width = 100;
                            dataGridView1.Columns["DostawcaKolumna"].Width = 150;
                            dataGridView1.Columns["AutaKolumna"].Width = 25;
                            dataGridView1.Columns["SztukiDekKolumna"].Width = 70;
                            dataGridView1.Columns["WagaDek"].Width = 50;
                            dataGridView1.Columns["bufor"].Width = 85;
                            dataGridView1.Columns["RóżnicaDni"].Width = 43;
                            dataGridView1.Columns["TypCenyKolumna"].Width = 85;
                            dataGridView1.Columns["CenaKolumna"].Width = 50;

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

                            DateTime? currentDate = null;
                            DataGridViewRow currentGroupRow = null;
                            double sumaAuta = 0;
                            double sumaSztukiDek = 0;
                            double sumaWagaDek = 0;
                            int count = 0;
                            bool isFirstRow = true;

                            while (reader.Read())
                            {
                                DateTime date = reader.GetDateTime(reader.GetOrdinal("DataOdbioru"));
                                string formattedDate = date.ToString("yyyy-MM-dd dddd");

                                if (currentDate != date)
                                {
                                    if (!isFirstRow)
                                    {
                                        if (currentGroupRow != null)
                                        {
                                            currentGroupRow.Cells["AutaKolumna"].Value = sumaAuta.ToString();
                                            currentGroupRow.Cells["SztukiDekKolumna"].Value = sumaSztukiDek.ToString("N0") + " szt";
                                            if (count != 0)
                                            {
                                                double sredniaWagaDek = sumaWagaDek / count;
                                                currentGroupRow.Cells["WagaDek"].Value = sredniaWagaDek.ToString("0.00") + " kg";
                                            }
                                        }
                                    }
                                    else
                                    {
                                        isFirstRow = false;
                                    }

                                    currentGroupRow = new DataGridViewRow();
                                    currentGroupRow.CreateCells(dataGridView1);
                                    currentGroupRow.Cells[dataGridView1.Columns["DataOdbioruKolumna"].Index].Value = formattedDate;
                                    dataGridView1.Rows.Add(currentGroupRow);

                                    currentDate = date;

                                    sumaAuta = 0;
                                    sumaSztukiDek = 0;
                                    sumaWagaDek = 0;
                                    count = 0;
                                }

                                DataGridViewRow row = new DataGridViewRow();
                                row.CreateCells(dataGridView1);
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    if (dataGridView1.Columns[i].Name == "SztukiDekKolumna")
                                    {
                                        if (!Convert.IsDBNull(reader["SztukiDek"]))
                                        {
                                            row.Cells[i].Value = string.Format("{0:#,0} szt", Convert.ToDouble(reader["SztukiDek"]));
                                        }
                                        else
                                        {
                                            row.Cells[i].Value = "";
                                        }
                                    }
                                    else if (dataGridView1.Columns[i].Name == "WagaDek")
                                    {
                                        row.Cells[i].Value = reader["WagaDek"] + " kg";
                                    }
                                    else if (dataGridView1.Columns[i].Name == "RóżnicaDni")
                                    {
                                        DateTime dataWstawienia = reader.IsDBNull(reader.GetOrdinal("DataWstawienia")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("DataWstawienia"));
                                        DateTime dataOdbioru = reader.GetDateTime(reader.GetOrdinal("DataOdbioru"));

                                        if (dataWstawienia == DateTime.MinValue)
                                            row.Cells[i].Value = "-";
                                        else
                                        {
                                            int roznicaDni = (dataOdbioru - dataWstawienia).Days;
                                            row.Cells[i].Value = roznicaDni + " dni";
                                        }
                                    }
                                    else if (dataGridView1.Columns[i].Name == "TypCenyKolumna")
                                    {
                                        row.Cells[i].Value = reader["TypCeny"];
                                    }
                                    else if (dataGridView1.Columns[i].Name == "CenaKolumna")
                                    {
                                        if (!Convert.IsDBNull(reader["Cena"]))
                                        {
                                            row.Cells[i].Value = reader["Cena"] + " zł";
                                        }
                                        else
                                        {
                                            row.Cells[i].Value = "-";
                                        }
                                    }
                                    else
                                    {
                                        row.Cells[i].Value = reader.GetValue(i);
                                    }
                                }
                                dataGridView1.Rows.Add(row);

                                if (reader["Auta"] != DBNull.Value)
                                {
                                    sumaAuta += Convert.ToDouble(reader["Auta"]);
                                }
                                if (reader["SztukiDek"] != DBNull.Value)
                                {
                                    sumaSztukiDek += Convert.ToDouble(reader["SztukiDek"]);
                                }
                                if (reader["WagaDek"] != DBNull.Value)
                                {
                                    sumaWagaDek += Convert.ToDouble(reader["WagaDek"]);
                                    count++;
                                }
                            }

                            if (currentGroupRow != null)
                            {
                                currentGroupRow.Cells["AutaKolumna"].Value = sumaAuta.ToString();
                                currentGroupRow.Cells["SztukiDekKolumna"].Value = sumaSztukiDek.ToString("N0") + " szt";
                                if (count != 0)
                                {
                                    double sredniaWagaDek = sumaWagaDek / count;
                                    currentGroupRow.Cells["WagaDek"].Value = sredniaWagaDek.ToString("0.00") + " kg";
                                }
                            }

                            // Dodanie nowego wiersza z danymi z kontrolek w odpowiedniej grupie DataOdbioru
                            DataGridViewRow customRow = new DataGridViewRow();
                            customRow.CreateCells(dataGridView1);
                            customRow.Cells[dataGridView1.Columns["DataOdbioruKolumna"].Index].Value = Data1.Value.ToString("yyyy-MM-dd");
                            customRow.Cells[dataGridView1.Columns["AutaKolumna"].Index].Value = liczbaAut1.Text;
                            customRow.Cells[dataGridView1.Columns["SztukiDekKolumna"].Index].Value = sztuki1.Text;
                            customRow.Cells[dataGridView1.Columns["WagaDek"].Index].Value = srednia1.Text;
                            customRow.Cells[dataGridView1.Columns["RóżnicaDni"].Index].Value = RoznicaDni1.Text;



                            bool inserted = false;
                            for (int i = 0; i < dataGridView1.Rows.Count; i++)
                            {
                                DateTime rowDate;
                                if (DateTime.TryParse(dataGridView1.Rows[i].Cells["DataOdbioruKolumna"].Value?.ToString(), out rowDate))
                                {
                                    if (Data1.Value <= rowDate)
                                    {
                                        dataGridView1.Rows.Insert(i, customRow);
                                        inserted = true;
                                        break;
                                    }
                                }
                            }
                            if (!inserted)
                            {
                                dataGridView1.Rows.Add(customRow);
                            }
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
            SetRowHeights(18);
        }

        private void SetRowHeights(int height)
        {
            // Ustawienie wysokości wszystkich wierszy na określoną wartość
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                row.Height = height;
            }
        }
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

        private void Dostawca_SelectedIndexChanged(object sender, EventArgs e)
        {
            
            nazwaZiD.ZmianaDostawcy(Dostawca, Kurnik, UlicaK, KodPocztowyK, MiejscK, KmK, UlicaH, KodPocztowyH, MiejscH, KmH, Dodatek, Ubytek, tel1, tel2, tel3, info1, info2, info3, Email);

        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void buttonWstawienie_Click(object sender, EventArgs e)
        {
            {
                try
                {

                    // Utwórz połączenie z bazą danych
                    using (SqlConnection connection = new SqlConnection(connectionPermission))
                    {
                        connection.Open();

                        // Rozpocznij transakcję
                        using (SqlTransaction transaction = connection.BeginTransaction())
                        {
                            try
                            {
                                // Pobierz maksymalny numer LP z tabeli dbo.WstawieniaKurczakow
                                long maxLP;
                                string maxLPSql = "SELECT MAX(Lp) AS MaxLP FROM dbo.WstawieniaKurczakow;";
                                using (SqlCommand command = new SqlCommand(maxLPSql, connection, transaction))
                                {
                                    object result = command.ExecuteScalar();
                                    maxLP = result == DBNull.Value ? 1 : Convert.ToInt64(result) + 1;
                                }

                                // Jeśli ilość dostaw jest większa lub równa 1, dodaj pierwszą dostawę
                                if (Convert.ToInt32(iloscDostaw.Text) >= 1)
                                {
                                    // Pobierz maksymalny numer LP dla harmonogramu dostaw
                                    long maxLP2;
                                    string maxLP2Sql = "SELECT MAX(Lp) AS MaxLP2 FROM dbo.HarmonogramDostaw;";
                                    using (SqlCommand command = new SqlCommand(maxLP2Sql, connection, transaction))
                                    {
                                        object result = command.ExecuteScalar();
                                        maxLP2 = result == DBNull.Value ? 1 : Convert.ToInt64(result) + 1;
                                    }

                                    // Utwórz zapytanie SQL do wstawienia danych dla pierwszej dostawy
                                    string insertDostawaSql = @"INSERT INTO dbo.HarmonogramDostaw (Lp, LpW, Dostawca, DataOdbioru, Kmk, KmH, Ubytek, WagaDek, SztukiDek, TypUmowy, bufor, SztSzuflada, Auta, typCeny, UWAGI, DataUtw) 
                                                        VALUES (@MaxLP2, @MaxLP, @Dostawca, @DataOdbioru, @KmK, @KmH, @Ubytek, @Srednia, @Sztuki, @TypUmowy, @Status, @SztukNaSzuflade, @LiczbaAut, @TypCeny, @Uwagi, @DataStwo)";
                                    using (SqlCommand command = new SqlCommand(insertDostawaSql, connection, transaction))
                                    {
                                        command.Parameters.AddWithValue("@MaxLP2", maxLP2);
                                        command.Parameters.AddWithValue("@MaxLP", maxLP);
                                        command.Parameters.AddWithValue("@Dostawca", Dostawca.Text);
                                        command.Parameters.AddWithValue("@DataOdbioru", string.IsNullOrEmpty(Data1.Text) ? (object)DBNull.Value : (object)Convert.ToDateTime(Data1.Text));
                                        command.Parameters.AddWithValue("@KmK", string.IsNullOrEmpty(KmK.Text) ? (object)DBNull.Value : KmK.Text);
                                        command.Parameters.AddWithValue("@KmH", string.IsNullOrEmpty(KmH.Text) ? (object)DBNull.Value : KmH.Text);
                                        command.Parameters.AddWithValue("@Ubytek", string.IsNullOrEmpty(Ubytek.Text) ? (object)DBNull.Value : (object)Convert.ToDecimal(Ubytek.Text));
                                        command.Parameters.AddWithValue("@Srednia", string.IsNullOrEmpty(srednia1.Text) ? (object)DBNull.Value : (object)Convert.ToDecimal(srednia1.Text));
                                        command.Parameters.AddWithValue("@Sztuki", string.IsNullOrEmpty(sztuki1.Text) ? (object)DBNull.Value : (object)Convert.ToInt32(sztuki1.Text));
                                       // command.Parameters.AddWithValue("@TypUmowy", string.IsNullOrEmpty(TypUmowy.Text) ? (object)DBNull.Value : TypUmowy.Text);
                                        //command.Parameters.AddWithValue("@Status", string.IsNullOrEmpty(Status.Text) ? (object)DBNull.Value : Status.Text);
                                        command.Parameters.AddWithValue("@SztukNaSzuflade", string.IsNullOrEmpty(sztukNaSzuflade1.Text) ? (object)DBNull.Value : (object)Convert.ToInt32(sztukNaSzuflade1.Text));
                                        command.Parameters.AddWithValue("@LiczbaAut", string.IsNullOrEmpty(liczbaAut1.Text) ? (object)DBNull.Value : (object)Convert.ToInt32(liczbaAut1.Text));
                                        //command.Parameters.AddWithValue("@TypCeny", string.IsNullOrEmpty(TypCeny.Text) ? (object)DBNull.Value : TypCeny.Text);
                                        command.Parameters.AddWithValue("@Uwagi", string.IsNullOrEmpty(uwagi.Text) ? (object)DBNull.Value : uwagi.Text);
                                        command.Parameters.AddWithValue("@DataStwo", string.IsNullOrEmpty(dataStwo.Text) ? (object)DBNull.Value : (object)Convert.ToDateTime(dataStwo.Text));
                                        command.ExecuteNonQuery();

                                    }
                                }
                                // Jeśli ilość dostaw jest większa lub równa 1, dodaj pierwszą dostawę
                                if (Convert.ToInt32(iloscDostaw.Text) >= 2)
                                {
                                    // Pobierz maksymalny numer LP dla harmonogramu dostaw
                                    long maxLP2;
                                    string maxLP2Sql = "SELECT MAX(Lp) AS MaxLP2 FROM dbo.HarmonogramDostaw;";
                                    using (SqlCommand command = new SqlCommand(maxLP2Sql, connection, transaction))
                                    {
                                        object result = command.ExecuteScalar();
                                        maxLP2 = result == DBNull.Value ? 1 : Convert.ToInt64(result) + 1;
                                    }

                                    // Utwórz zapytanie SQL do wstawienia danych dla pierwszej dostawy
                                    string insertDostawaSql = @"INSERT INTO dbo.HarmonogramDostaw (Lp, LpW, Dostawca, DataOdbioru, Kmk, KmH, Ubytek, WagaDek, SztukiDek, TypUmowy, bufor, SztSzuflada, Auta, typCeny, UWAGI, DataUtw) 
                                                        VALUES (@MaxLP2, @MaxLP, @Dostawca, @DataOdbioru, @KmK, @KmH, @Ubytek, @Srednia, @Sztuki, @TypUmowy, @Status, @SztukNaSzuflade, @LiczbaAut, @TypCeny, @Uwagi, @DataStwo)";
                                    using (SqlCommand command = new SqlCommand(insertDostawaSql, connection, transaction))
                                    {
                                        command.Parameters.AddWithValue("@MaxLP2", maxLP2);
                                        command.Parameters.AddWithValue("@MaxLP", maxLP);
                                        command.Parameters.AddWithValue("@Dostawca", Dostawca.Text);
                                        command.Parameters.AddWithValue("@DataOdbioru", string.IsNullOrEmpty(Data2.Text) ? (object)DBNull.Value : (object)Convert.ToDateTime(Data2.Text));
                                        command.Parameters.AddWithValue("@KmK", string.IsNullOrEmpty(KmK.Text) ? (object)DBNull.Value : KmK.Text);
                                        command.Parameters.AddWithValue("@KmH", string.IsNullOrEmpty(KmH.Text) ? (object)DBNull.Value : KmH.Text);
                                        command.Parameters.AddWithValue("@Ubytek", string.IsNullOrEmpty(Ubytek.Text) ? (object)DBNull.Value : (object)Convert.ToDecimal(Ubytek.Text));
                                        command.Parameters.AddWithValue("@Srednia", string.IsNullOrEmpty(srednia2.Text) ? (object)DBNull.Value : (object)Convert.ToDecimal(srednia2.Text));
                                        command.Parameters.AddWithValue("@Sztuki", string.IsNullOrEmpty(sztuki2.Text) ? (object)DBNull.Value : (object)Convert.ToInt32(sztuki2.Text));
                                        //command.Parameters.AddWithValue("@TypUmowy", string.IsNullOrEmpty(TypUmowy.Text) ? (object)DBNull.Value : TypUmowy.Text);
                                        //command.Parameters.AddWithValue("@Status", string.IsNullOrEmpty(Status.Text) ? (object)DBNull.Value : Status.Text);
                                        command.Parameters.AddWithValue("@SztukNaSzuflade", string.IsNullOrEmpty(sztukNaSzuflade2.Text) ? (object)DBNull.Value : (object)Convert.ToInt32(sztukNaSzuflade2.Text));
                                        command.Parameters.AddWithValue("@LiczbaAut", string.IsNullOrEmpty(liczbaAut2.Text) ? (object)DBNull.Value : (object)Convert.ToInt32(liczbaAut2.Text));
                                        //command.Parameters.AddWithValue("@TypCeny", string.IsNullOrEmpty(TypCeny.Text) ? (object)DBNull.Value : TypCeny.Text);
                                        command.Parameters.AddWithValue("@Uwagi", string.IsNullOrEmpty(uwagi.Text) ? (object)DBNull.Value : uwagi.Text);
                                        command.Parameters.AddWithValue("@DataStwo", string.IsNullOrEmpty(dataStwo.Text) ? (object)DBNull.Value : (object)Convert.ToDateTime(dataStwo.Text));
                                        command.ExecuteNonQuery();
                                    }
                                }

                                // Jeśli ilość dostaw jest większa lub równa 1, dodaj pierwszą dostawę
                                if (Convert.ToInt32(iloscDostaw.Text) >= 3)
                                {
                                    // Pobierz maksymalny numer LP dla harmonogramu dostaw
                                    long maxLP2;
                                    string maxLP2Sql = "SELECT MAX(Lp) AS MaxLP2 FROM dbo.HarmonogramDostaw;";
                                    using (SqlCommand command = new SqlCommand(maxLP2Sql, connection, transaction))
                                    {
                                        object result = command.ExecuteScalar();
                                        maxLP2 = result == DBNull.Value ? 1 : Convert.ToInt64(result) + 1;
                                    }

                                    // Utwórz zapytanie SQL do wstawienia danych dla pierwszej dostawy
                                    string insertDostawaSql = @"INSERT INTO dbo.HarmonogramDostaw (Lp, LpW, Dostawca, DataOdbioru, Kmk, KmH, Ubytek, WagaDek, SztukiDek, TypUmowy, bufor, SztSzuflada, Auta, typCeny, UWAGI, DataUtw) 
                                                        VALUES (@MaxLP2, @MaxLP, @Dostawca, @DataOdbioru, @KmK, @KmH, @Ubytek, @Srednia, @Sztuki, @TypUmowy, @Status, @SztukNaSzuflade, @LiczbaAut, @TypCeny, @Uwagi, @DataStwo)";
                                    using (SqlCommand command = new SqlCommand(insertDostawaSql, connection, transaction))
                                    {
                                        command.Parameters.AddWithValue("@MaxLP2", maxLP2);
                                        command.Parameters.AddWithValue("@MaxLP", maxLP);
                                        command.Parameters.AddWithValue("@Dostawca", Dostawca.Text);
                                        command.Parameters.AddWithValue("@DataOdbioru", string.IsNullOrEmpty(Data3.Text) ? (object)DBNull.Value : (object)Convert.ToDateTime(Data3.Text));
                                        command.Parameters.AddWithValue("@KmK", string.IsNullOrEmpty(KmK.Text) ? (object)DBNull.Value : KmK.Text);
                                        command.Parameters.AddWithValue("@KmH", string.IsNullOrEmpty(KmH.Text) ? (object)DBNull.Value : KmH.Text);
                                        command.Parameters.AddWithValue("@Ubytek", string.IsNullOrEmpty(Ubytek.Text) ? (object)DBNull.Value : (object)Convert.ToDecimal(Ubytek.Text));
                                        command.Parameters.AddWithValue("@Srednia", string.IsNullOrEmpty(srednia3.Text) ? (object)DBNull.Value : (object)Convert.ToDecimal(srednia3.Text));
                                        command.Parameters.AddWithValue("@Sztuki", string.IsNullOrEmpty(sztuki3.Text) ? (object)DBNull.Value : (object)Convert.ToInt32(sztuki3.Text));
                                       //command.Parameters.AddWithValue("@TypUmowy", string.IsNullOrEmpty(TypUmowy.Text) ? (object)DBNull.Value : TypUmowy.Text);
                                       //command.Parameters.AddWithValue("@Status", string.IsNullOrEmpty(Status.Text) ? (object)DBNull.Value : Status.Text);
                                        command.Parameters.AddWithValue("@SztukNaSzuflade", string.IsNullOrEmpty(sztukNaSzuflade3.Text) ? (object)DBNull.Value : (object)Convert.ToInt32(sztukNaSzuflade3.Text));
                                        command.Parameters.AddWithValue("@LiczbaAut", string.IsNullOrEmpty(liczbaAut3.Text) ? (object)DBNull.Value : (object)Convert.ToInt32(liczbaAut3.Text));
                                        //command.Parameters.AddWithValue("@TypCeny", string.IsNullOrEmpty(TypCeny.Text) ? (object)DBNull.Value : TypCeny.Text);
                                        command.Parameters.AddWithValue("@Uwagi", string.IsNullOrEmpty(uwagi.Text) ? (object)DBNull.Value : uwagi.Text);
                                        command.Parameters.AddWithValue("@DataStwo", string.IsNullOrEmpty(dataStwo.Text) ? (object)DBNull.Value : (object)Convert.ToDateTime(dataStwo.Text));
                                        command.ExecuteNonQuery();
                                    }
                                }

                                // Utwórz zapytanie SQL do wstawienia danych dla WstawieniaKurczakow
                                string insertWstawieniaSql = @"INSERT INTO dbo.WstawieniaKurczakow (Lp, Dostawca, IloscWstawienia, DataWstawienia, TypUmowy, TypCeny) 
                                                            VALUES (@MaxLP, @Dostawca, @SztukiWstawienia, @DataWstawienia, @TypUmowy, @TypCeny)";
                                using (SqlCommand command = new SqlCommand(insertWstawieniaSql, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("@MaxLP", maxLP);
                                    // Ustaw pozostałe parametry (np. @Dostawca, @SztukiWstawienia itp.) zgodnie z wartościami z formularza
                                    command.Parameters.AddWithValue("@Dostawca", Dostawca.Text);
                                    command.Parameters.AddWithValue("@SztukiWstawienia", sztukiWstawienia.Text);
                                    command.Parameters.AddWithValue("@DataWstawienia", string.IsNullOrEmpty(dataWstawienia.Text) ? (object)DBNull.Value : (object)Convert.ToDateTime(dataWstawienia.Text));
                                    //command.Parameters.AddWithValue("@TypUmowy", string.IsNullOrEmpty(TypUmowy.Text) ? (object)DBNull.Value : TypUmowy.Text);
                                   //command.Parameters.AddWithValue("@TypCeny", string.IsNullOrEmpty(TypCeny.Text) ? (object)DBNull.Value : TypCeny.Text);
                                }

                                // Utwórz zapytanie SQL do aktualizacji danych dostawcy
                                string updateDostawcySql = @"UPDATE dbo.Dostawcy 
                                                         SET Address = @UlicaH, PostalCode = @KodPocztowyH, City = @MiejscH, Distance = @KmH 
                                                         WHERE Shortname = @SelectedDostawca";
                                using (SqlCommand command = new SqlCommand(updateDostawcySql, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("@UlicaH", UlicaH.Text);
                                    command.Parameters.AddWithValue("@KodPocztowyH", KodPocztowyH.Text);
                                    command.Parameters.AddWithValue("@MiejscH", MiejscH.Text);
                                    command.Parameters.AddWithValue("@KmH", KmH.Text);
                                    command.Parameters.AddWithValue("@SelectedDostawca", Dostawca.Text);
                                    command.ExecuteNonQuery();
                                }

                                // Zatwierdź transakcję
                                transaction.Commit();
                            }
                            catch (Exception ex)
                            {
                                // W przypadku wystąpienia błędu, cofnij transakcję
                                transaction.Rollback();
                                throw ex;
                            }
                        }
                    }

                    MessageBox.Show("Dane zostały pomyślnie zapisane do bazy danych LibraNet.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.Close(); // Zamknij formularz
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Wystąpił błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

      

        private void sztukiWstawienia_TextChanged(object sender, EventArgs e)
        {

        }

        private void MiejscK_TextChanged(object sender, EventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void DodajDniDoDaty(TextBox textBox, DateTimePicker dataWstawienia, DateTimePicker data1)
        {
            // Sprawdź, czy w TextBox jest liczba całkowita
            if (int.TryParse(textBox.Text, out int daysToAdd))
            {

                // Pobierz wartość z dataWstawienia i dodaj liczbę dni
                DateTime newDate = dataWstawienia.Value.AddDays(daysToAdd);

                // Ustaw nową datę w data1
                data1.Value = newDate;
            }
            else
            {
                // Jeśli tekst w TextBox nie jest liczbą całkowitą, ustaw domyślną datę
                data1.Value = dataWstawienia.Value;
            }
        }

        private void textDni1_TextChanged(object sender, EventArgs e)
        {
            DodajDniDoDaty(textDni1, dataWstawienia, Data1);
        }

        private void textDni2_TextChanged(object sender, EventArgs e)
        {
            DodajDniDoDaty(textDni2, dataWstawienia, Data2);
        }

        private void textDni3_TextChanged(object sender, EventArgs e)
        {
            DodajDniDoDaty(textDni3, dataWstawienia, Data3);
        }

        private void textDni4_TextChanged(object sender, EventArgs e)
        {
            DodajDniDoDaty(textDni4, dataWstawienia, Data4);
        }
    }
}
