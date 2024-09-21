using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
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
        public double sztWstawienia { get; set; }
        public string dostawca { get; set; }
        public bool modyfikacja { get; set; }
        public int LpWstawienia { get; set; }
        public DateTime DataWstawienia { get; set; }



        public Wstawienie()
        {
            InitializeComponent();
            FillComboBox();
            TextBox[] textBoxesSumy = { sztuki1, sztuki2, sztuki3, sztuki4, sztuki5, sztukiSuma, SztukiUpadki, sztukiRoznica };
        }


        private void Form1_Load(object sender, EventArgs e)
        {

        }
        public class HarmonogramDostaw
        {
            public DateTime DataOdbioru { get; set; }
            public int SztukiDek { get; set; }
        }
        public void UzupelnijBraki()
        {
            string str = sztWstawienia.ToString();
            sztukiWstawienia.Text = str;
            Dostawca.Text = dostawca;
            sztukiWstawienia.Visible = true;
            pictureBox29.Visible = true;
            dataWstawienia.Visible = true;
            LiczbaDniWstawienia.Visible = true;
            dataGridWagi.Visible = true;
            dataGridWstawien.Visible = true;
            pictureBox1.Visible = true;
            sztukiWstawienia.Visible = true;
            SztukiUpadki.Visible = true;
            Dostawca_SelectedIndexChanged(this, EventArgs.Empty);
            sztukiWstawienia_TextChanged(this, EventArgs.Empty);
        }
        public void MetodaModyfiacji()
        {
            string str = sztWstawienia.ToString();
            sztukiWstawienia.Text = str;
            Dostawca.Text = dostawca;
            sztukiWstawienia.Visible = true;
            pictureBox29.Visible = true;
            dataWstawienia.Visible = true;
            LiczbaDniWstawienia.Visible = true;
            dataGridWagi.Visible = true;
            dataGridWstawien.Visible = true;
            pictureBox1.Visible = true;
            sztukiWstawienia.Visible = true;
            SztukiUpadki.Visible = true;
            checkBox1.Visible = true;
            checkBox2.Visible = true;
            checkBox3.Visible = true;
            checkBox4.Visible = true;
            checkBox5.Visible = true;
            dataWstawienia.Value = DataWstawienia;
            Dostawca_SelectedIndexChanged(this, EventArgs.Empty);
            sztukiWstawienia_TextChanged(this, EventArgs.Empty);

            var harmonogramDostaw = PobierzHarmonogramDostaw(LpWstawienia);

            // Przy założeniu, że masz TextBoxy o nazwach dataDostawy1, sztuki1 itd.
            for (int i = 0; i < 5; i++)
            {
                DateTimePicker dataDostawyPicker = this.Controls.Find("Data" + (i + 1), true).FirstOrDefault() as DateTimePicker;
                TextBox sztukiTextBox = this.Controls.Find("sztuki" + (i + 1), true).FirstOrDefault() as TextBox;

                if (dataDostawyPicker != null && sztukiTextBox != null)
                {
                    if (i < harmonogramDostaw.Count && harmonogramDostaw[i] != null)
                    {
                        dataDostawyPicker.Value = harmonogramDostaw[i].DataOdbioru;
                        sztukiTextBox.Text = harmonogramDostaw[i].SztukiDek.ToString();
                    }
                    else
                    {
                        dataDostawyPicker.Value = DateTime.Today; // Możesz ustawić domyślną wartość np. dzisiejszą datę
                        sztukiTextBox.Text = "";
                    }
                }
                else
                {
                    MessageBox.Show($"Control '{(i + 1)}' not found.");
                }
            }



        }
        public List<HarmonogramDostaw> PobierzHarmonogramDostaw(int lpw)
        {
            var listaDostaw = new List<HarmonogramDostaw>();

            string query = "SELECT dataodbioru, sztukidek FROM dbo.HarmonogramDostaw WHERE LpW = @LpW ORDER BY dataodbioru";

            using (SqlConnection cnn = new SqlConnection(connectionPermission))
            using (SqlCommand cmd = new SqlCommand(query, cnn))
            {
                cmd.Parameters.AddWithValue("@LpW", lpw);

                cnn.Open();

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var dostawa = new HarmonogramDostaw
                        {
                            DataOdbioru = reader.GetDateTime(0),
                            SztukiDek = reader.IsDBNull(1) ? 0 : reader.GetInt32(1)
                        };
                        listaDostaw.Add(dostawa);
                    }
                }
            }

            return listaDostaw;
        }


        public void WypelnijStartowo()
        {
            textDni1.Text = "35";
            textDni2.Text = "42";

            srednia1.Text = "2,1";
            srednia2.Text = "2,8";

            sztukNaSzuflade1.Text = "20";
            sztukNaSzuflade2.Text = "16";

            DodajDniDoDaty(textDni1, dataWstawienia, Data1);
            DodajDniDoDaty(textDni2, dataWstawienia, Data2);
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

        private void WidocznoscWierszy(CheckBox checkBox, DateTimePicker dateTimePicker, Button Wklej, params TextBox[] textBoxes)
        {
            bool isVisible = checkBox.Checked;

            // Ustaw widoczność dla każdego TextBoxa
            foreach (var textBox in textBoxes)
            {
                textBox.Visible = isVisible;
            }

            // Ustaw widoczność dla DateTimePicker
            dateTimePicker.Visible = isVisible;
            Wklej.Visible = isVisible;
        }
        private void srednia1_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ileSztukOblcizenie(sztukNaSzuflade1, wyliczone1);
            nazwaZiD.ReplaceCommaWithDot(srednia1);

        }

        private void srednia2_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ileSztukOblcizenie(sztukNaSzuflade2, wyliczone1);
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


        private void ObliczSumeSztuk(params TextBox[] textBoxesSumy)
        {
            try
            {
                // Dodawanie wartości z pierwszych pięciu TextBoxów
                double sum = 0;
                for (int i = 0; i < 5; i++)
                {
                    if (string.IsNullOrWhiteSpace(textBoxesSumy[i].Text))
                    {
                        sum += 0; // Traktuj puste komórki jako 0
                    }
                    else if (double.TryParse(textBoxesSumy[i].Text, out double value))
                    {
                        sum += value;
                    }
                    else
                    {
                        textBoxesSumy[5].Text = "";
                        textBoxesSumy[7].Text = "";
                        return; // Przerwij obliczenia, jeśli napotkano niepoprawne dane
                    }
                }

                // Ustawienie wyniku sumy w szóstym TextBoxie
                textBoxesSumy[5].Text = sum.ToString("N0"); // Użycie formatu z separatorem tysięcy

                // Odejmowanie wartości z sumy od wartości w siódmym TextBoxie
                if (string.IsNullOrWhiteSpace(textBoxesSumy[6].Text))
                {
                    textBoxesSumy[7].Text = sum.ToString("N0"); // Jeśli wartość w SztukiUpadki jest pusta, wynikiem jest suma
                }
                else if (double.TryParse(textBoxesSumy[6].Text, out double upadkiValue))
                {
                    double result = upadkiValue - sum; // Zmienione odejmowanie: upadki - sum
                                                       // Ustawienie wyniku odejmowania w ósmym TextBoxie
                    textBoxesSumy[7].Text = result.ToString("N0"); // Użycie formatu z separatorem tysięcy
                }

            }
            catch (FormatException)
            {
                MessageBox.Show("Proszę wprowadzić poprawne liczby we wszystkich TextBoxach.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void sztuki1_TextChanged(object sender, EventArgs e)
        {
            ObliczSumeSztuk(sztuki1, sztuki2, sztuki3, sztuki4, sztuki5, sztukiSuma, SztukiUpadki, sztukiRoznica);
            obliczenia.ZestawDoObliczaniaTransportuWstawien(sztukNaSzuflade1, wyliczone1, obliczeniaAut1, sztuki1, srednia1, KGwSkrzynce1);
        }

        private void sztuki2_TextChanged(object sender, EventArgs e)
        {
            ObliczSumeSztuk(sztuki1, sztuki2, sztuki3, sztuki4, sztuki5, sztukiSuma, SztukiUpadki, sztukiRoznica);
            obliczenia.ZestawDoObliczaniaTransportuWstawien(sztukNaSzuflade2, wyliczone2, obliczeniaAut2, sztuki2, srednia2, KGwSkrzynce2);
        }

        private void sztuki3_TextChanged(object sender, EventArgs e)
        {
            ObliczSumeSztuk(sztuki1, sztuki2, sztuki3, sztuki4, sztuki5, sztukiSuma, SztukiUpadki, sztukiRoznica);
            obliczenia.ZestawDoObliczaniaTransportuWstawien(sztukNaSzuflade3, wyliczone3, obliczeniaAut3, sztuki3, srednia3, KGwSkrzynce3);
        }

        private void sztuki4_TextChanged(object sender, EventArgs e)
        {
            ObliczSumeSztuk(sztuki1, sztuki2, sztuki3, sztuki4, sztuki5, sztukiSuma, SztukiUpadki, sztukiRoznica);
            obliczenia.ZestawDoObliczaniaTransportuWstawien(sztukNaSzuflade4, wyliczone4, obliczeniaAut4, sztuki4, srednia4, KGwSkrzynce4);
        }

        private void sztuki5_TextChanged(object sender, EventArgs e)
        {
            ObliczSumeSztuk(sztuki1, sztuki2, sztuki3, sztuki4, sztuki5, sztukiSuma, SztukiUpadki, sztukiRoznica);
            obliczenia.ZestawDoObliczaniaTransportuWstawien(sztukNaSzuflade5, wyliczone5, obliczeniaAut5, sztuki5, srednia5, KGwSkrzynce5);
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


            //ObliczRozniceDniMiedzyWstawieniami(dataWstawienia, LiczbaDniWstawienia, Dostawca);
            DodajDniDoDaty(textDni1, dataWstawienia, Data1);
            DodajDniDoDaty(textDni2, dataWstawienia, Data2);
            DodajDniDoDaty(textDni3, dataWstawienia, Data3);
            DodajDniDoDaty(textDni4, dataWstawienia, Data4);
            DodajDniDoDaty(textDni5, dataWstawienia, Data5);
            pictureBox1.Visible = true;
            sztukiWstawienia.Visible = true;
            SztukiUpadki.Visible = true;

            checkBox1.Visible = true;
            checkBox2.Visible = true;
            checkBox3.Visible = true;
            checkBox4.Visible = true;
            checkBox5.Visible = true;
        }

        public void ObliczRozniceDniMiedzyWstawieniami(DateTimePicker datePicker, TextBox liczbaDniWstawienia, ComboBox comboBoxDostawca)
        {
            if (comboBoxDostawca.SelectedItem == null)
            {

                return;
            }

            string dostawca = comboBoxDostawca.SelectedItem.ToString();

            string query = @"
        WITH CTE AS (
            SELECT TOP (1)
                LP,
                Dostawca,
                DataWstawienia
            FROM 
                [LibraNet].[dbo].[WstawieniaKurczakow]
            WHERE 
                Dostawca = @Dostawca
            ORDER BY
                DataWstawienia DESC
        )
        SELECT DataWstawienia FROM CTE;";

            using (SqlConnection connection = new SqlConnection(connectionPermission))
            {
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Dostawca", dostawca);

                try
                {
                    connection.Open();
                    object result = command.ExecuteScalar();
                    if (result != null)
                    {
                        DateTime dataWstawienia = Convert.ToDateTime(result);
                        DateTime selectedDate = datePicker.Value;
                        TimeSpan difference = selectedDate - dataWstawienia;
                        int liczbaDni = (int)difference.TotalDays;
                        liczbaDniWstawienia.Text = liczbaDni.ToString();
                    }
                    else
                    {

                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Wystąpił błąd: " + ex.Message);
                }
            }
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
            // Wywołaj metodę zmieniającą dostawcę z odpowiednimi parametrami
            nazwaZiD.ZmianaDostawcy(Dostawca, Kurnik, UlicaK, KodPocztowyK, MiejscK, KmK, UlicaH, KodPocztowyH, MiejscH, KmH, Dodatek, Ubytek, tel1, tel2, tel3, info1, info2, info3, Email);
            DisplayDataInDataGridView();


            // Pobierz wybrany element z ComboBox
            string selectedDostawca = Dostawca.SelectedItem?.ToString().Trim().ToLower() ?? "";

            // Sprawdzenie, czy istnieje źródło danych dla DataGridView
            if (dataGridWagi.DataSource is DataTable dataTable)
            {
                // Ustawienie filtra dla kolumny "Dostawca" na podstawie wybranego elementu w ComboBox
                dataTable.DefaultView.RowFilter = $"Dostawca LIKE '%{selectedDostawca}%'";

                // Przywrócenie pozycji kursora po zastosowaniu filtra
                if (dataGridWagi.RowCount > 0)
                {
                    int currentPosition = dataGridWagi.FirstDisplayedScrollingRowIndex;
                    if (currentPosition >= 0 && currentPosition < dataGridWagi.RowCount)
                    {
                        dataGridWagi.FirstDisplayedScrollingRowIndex = currentPosition;
                    }
                }
            }


            pictureBox29.Visible = true;
            dataWstawienia.Visible = true;
            LiczbaDniWstawienia.Visible = true;
            dataGridWagi.Visible = true;
            dataGridWstawien.Visible = true;
        }
        private void PokazWstawienia()
        {
            // Pobierz wybranego dostawcę z ComboBox
            string selectedDostawca = Dostawca.SelectedItem.ToString();

            // Zapytanie SQL
            string query = @"
            WITH CTE AS (
                SELECT 
                    LP,
                    Dostawca,
                    DataWstawienia,
                    CONVERT(varchar, DataWstawienia, 23) AS Data,
                    IloscWstawienia,
                    LAG(DataWstawienia) OVER (PARTITION BY Dostawca ORDER BY DataWstawienia ASC) AS PreviousDataWstawienia
                FROM 
                    [LibraNet].[dbo].[WstawieniaKurczakow]
                WHERE 
                    Dostawca = @Dostawca
            )
            SELECT
                LP,
                Dostawca,
                Data,
                IloscWstawienia,
                DATEDIFF(day, PreviousDataWstawienia, DataWstawienia) AS Przerwa
            FROM 
                CTE
            ORDER BY 
                DataWstawienia DESC;";

            // Utworzenie połączenia z bazą danych
            using (SqlConnection connection = new SqlConnection(connectionPermission))
            {
                // Utworzenie adaptera danych
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);

                // Dodanie parametru do zapytania SQL
                adapter.SelectCommand.Parameters.AddWithValue("@Dostawca", selectedDostawca);

                // Utworzenie tabeli danych
                DataTable table = new DataTable();

                // Wypełnienie tabeli danymi z adaptera
                adapter.Fill(table);

                // Ustawienie źródła danych dla DataGridView

                dataGridWstawien.DataSource = table;
                // Ustawienia kolumn
                foreach (DataGridViewColumn column in dataGridWstawien.Columns)
                {
                    // Automatyczne zawijanie tekstu w nagłówkach kolumn
                    column.HeaderCell.Style.WrapMode = DataGridViewTriState.True;
                }

                // Ustawienie szerokości kolumn dla DataGridView
                dataGridWstawien.Columns["LP"].Width = 35;
                dataGridWstawien.Columns["Dostawca"].Visible = false;
                dataGridWstawien.Columns["Data"].Width = 80;
                dataGridWstawien.Columns["IloscWstawienia"].Width = 50;
                dataGridWstawien.Columns["Przerwa"].Width = 35;


                // Ukrycie nagłówków wierszy
                dataGridWstawien.RowHeadersVisible = false; // Usunięcie bocznego paska do zaznaczania wielu wierszy
                dataGridWstawien.RowTemplate.Height = 18; // Ustawienie wysokości wierszy na 18
                dataGridWstawien.DefaultCellStyle.Font = new Font("Arial", 8); // Zmniejszenie czcionki

                // Ustawienie wysokości wierszy na minimalną wartość
                dataGridWstawien.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
                dataGridWstawien.AllowUserToResizeRows = false;

                // Ustawienie formatu kolumny "IloscWstawienia" z odstępami tysięcznymi
                dataGridWstawien.Columns["IloscWstawienia"].DefaultCellStyle.Format = "#,##0";
            }
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
        private void Oblicz3ProcentUpadkow(TextBox inputTextBox, TextBox resultTextBox)
        {
            if (decimal.TryParse(inputTextBox.Text, out decimal value))
            {
                decimal result = value * 0.97m;
                resultTextBox.Text = result.ToString("N0"); // Formatowanie z separatorem tysięcy
            }
            else
            {
                // Obsługa przypadku, gdy konwersja nie powiedzie się
                resultTextBox.Text = "Błędna wartość";
            }
        }



        private void sztukiWstawienia_TextChanged(object sender, EventArgs e)
        {
            Oblicz3ProcentUpadkow(sztukiWstawienia, SztukiUpadki);
            ObliczSumeSztuk(sztuki1, sztuki2, sztuki3, sztuki4, sztuki5, sztukiSuma, SztukiUpadki, sztukiRoznica);
            groupBox2.Visible = true;
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
        private void Ubytek_TextChanged(object sender, EventArgs e)
        {

        }
        public void ObliczSztuki(TextBox textBox1, TextBox textBox2, TextBox textBox3, TextBox textBox4)
        {
            // Sprawdź, czy textBox1 i textBox2 zawierają poprawne liczby zmiennoprzecinkowe
            double value1, value2;
            if (double.TryParse(textBox1.Text, out value1) && double.TryParse(textBox2.Text, out value2))
            {
                // Mnożenie wartości z textBox1 przez wartość z textBox2 i ustawienie wyniku w textBox3
                double result = value1 * value2;
                textBox3.Text = result.ToString();

                // Ustawienie wartości z textBox2 w textBox4
                textBox4.Text = value2.ToString();
            }
        }

        private void DisplayDataInDataGridView()
        {
            // Zapytanie SQL
            string query = @"
            SELECT 
                k.CreateData AS Data,
                Partia.CustomerName AS Dostawca,
                DATEDIFF(day, MIN(wk.DataWstawienia), MAX(hd.DataOdbioru)) AS RoznicaDni,
                AVG(hd.WagaDek) AS WagaDek,
                CONVERT(decimal(18, 2), ((15.0 / CAST(AVG(CAST(k.QntInCont AS decimal(18, 2))) AS decimal(18, 2))) * 1.22) - AVG(hd.WagaDek)) AS roznica,
                CONVERT(decimal(18, 2), (15.0 / CAST(AVG(CAST(k.QntInCont AS decimal(18, 2))) AS decimal(18, 2))) * 1.22) AS SredniaZywy
                
            FROM 
                [LibraNet].[dbo].[In0E] k
            JOIN 
                [LibraNet].[dbo].[PartiaDostawca] Partia ON k.P1 = Partia.Partia
            LEFT JOIN 
                [LibraNet].[dbo].[HarmonogramDostaw] hd ON k.CreateData = hd.DataOdbioru AND Partia.CustomerName = hd.Dostawca
            LEFT JOIN 
                [LibraNet].[dbo].[WstawieniaKurczakow] wk ON hd.LpW = wk.Lp
            WHERE 
                k.ArticleID = 40 
                AND k.QntInCont > 4
            GROUP BY 
                k.CreateData, 
                Partia.CustomerName
            ORDER BY 
                k.CreateData DESC";


            // Utworzenie połączenia z bazą danych
            using (SqlConnection connection = new SqlConnection(connectionPermission))
            {
                // Utworzenie adaptera danych
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);

                // Utworzenie tabeli danych
                DataTable table = new DataTable();

                // Wypełnienie tabeli danymi z adaptera
                adapter.Fill(table);

                // Ustawienie DataTable jako DataSource dla DataGridView
                dataGridWagi.DataSource = table;

                // Ustawienia kolumn
                foreach (DataGridViewColumn column in dataGridWagi.Columns)
                {
                    // Automatyczne zawijanie tekstu w nagłówkach kolumn
                    column.HeaderCell.Style.WrapMode = DataGridViewTriState.True;
                }

                dataGridWagi.Columns["Dostawca"].Visible = false;
                // Ręczne dodawanie szerokości kolumn i formatowanie jednostek
                dataGridWagi.Columns["Data"].Width = 80;
                dataGridWagi.Columns["RoznicaDni"].Width = 50;
                dataGridWagi.Columns["RoznicaDni"].DefaultCellStyle.Format = "# 'dni'";

                dataGridWagi.Columns["WagaDek"].Width = 60;
                dataGridWagi.Columns["WagaDek"].DefaultCellStyle.Format = "#,##0.00 'kg'";

                dataGridWagi.Columns["SredniaZywy"].Width = 60;
                dataGridWagi.Columns["SredniaZywy"].DefaultCellStyle.Format = "#,##0.00 'kg'";

                dataGridWagi.Columns["roznica"].Width = 50;
                dataGridWagi.Columns["roznica"].DefaultCellStyle.Format = "#,##0.00 'kg'";
                // Ustawienie wysokości wierszy na minimalną wartość
                // Ustawienia DataGridView
                dataGridWagi.RowHeadersVisible = false; // Usunięcie bocznego paska do zaznaczania wielu wierszy
                dataGridWagi.RowTemplate.Height = 18; // Ustawienie wysokości wierszy na 18
                dataGridWagi.DefaultCellStyle.Font = new Font("Arial", 8); // Zmniejszenie czcionki

                // Ustawienie wysokości wierszy na minimalną wartość
                dataGridWagi.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
                dataGridWagi.AllowUserToResizeRows = false;

            }
        }
        private void ObliczSztuki1_TextChanged(object sender, EventArgs e)
        {
            ObliczSztuki(wyliczone1, ObliczSztuki1, sztuki1, liczbaAut1);
        }

        private void ObliczSztuki2_TextChanged(object sender, EventArgs e)
        {
            ObliczSztuki(wyliczone2, ObliczSztuki2, sztuki2, liczbaAut2);
        }

        private void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            WidocznoscWierszy(checkBox5, Data5, Wklej5, textDni5, RoznicaDni5, srednia5, sztukNaSzuflade5, ObliczSztuki5, liczbaAut5, sztuki5, obliczeniaAut5, KGwSkrzynce5, wyliczone5);
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            WidocznoscWierszy(checkBox1, Data1, Wklej1, textDni1, RoznicaDni1, srednia1, sztukNaSzuflade1, ObliczSztuki1, liczbaAut1, sztuki1, obliczeniaAut1, KGwSkrzynce1, wyliczone1);
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            WidocznoscWierszy(checkBox2, Data2, Wklej2, textDni2, RoznicaDni2, srednia2, sztukNaSzuflade2, ObliczSztuki2, liczbaAut2, sztuki2, obliczeniaAut2, KGwSkrzynce2, wyliczone2);
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            WidocznoscWierszy(checkBox3, Data3, Wklej3, textDni3, RoznicaDni3, srednia3, sztukNaSzuflade3, ObliczSztuki3, liczbaAut3, sztuki3, obliczeniaAut3, KGwSkrzynce3, wyliczone3);
        }

        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            WidocznoscWierszy(checkBox4, Data4, Wklej4, textDni4, RoznicaDni4, srednia4, sztukNaSzuflade4, ObliczSztuki4, liczbaAut4, sztuki4, obliczeniaAut4, KGwSkrzynce4, wyliczone4);
        }

        private void ObliczSztuki3_TextChanged(object sender, EventArgs e)
        {
            ObliczSztuki(wyliczone3, ObliczSztuki3, sztuki3, liczbaAut3);
        }

        private void ObliczSztuki4_TextChanged(object sender, EventArgs e)
        {
            ObliczSztuki(wyliczone4, ObliczSztuki4, sztuki4, liczbaAut4);
        }

        private void ObliczSztuki5_TextChanged(object sender, EventArgs e)
        {
            ObliczSztuki(wyliczone5, ObliczSztuki5, sztuki5, liczbaAut5);
        }

        private void sztukNaSzuflade1_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ZestawDoObliczaniaTransportuWstawien(sztukNaSzuflade1, wyliczone1, obliczeniaAut1, sztuki1, srednia1, KGwSkrzynce1);
        }
        private void sztukNaSzuflade2_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ZestawDoObliczaniaTransportuWstawien(sztukNaSzuflade2, wyliczone2, obliczeniaAut2, sztuki2, srednia2, KGwSkrzynce2);
        }

        private void sztukNaSzuflade3_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ZestawDoObliczaniaTransportuWstawien(sztukNaSzuflade3, wyliczone3, obliczeniaAut3, sztuki3, srednia3, KGwSkrzynce3);
        }

        private void sztukNaSzuflade4_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ZestawDoObliczaniaTransportuWstawien(sztukNaSzuflade4, wyliczone4, obliczeniaAut4, sztuki4, srednia4, KGwSkrzynce4);
        }

        private void sztukNaSzuflade5_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ZestawDoObliczaniaTransportuWstawien(sztukNaSzuflade5, wyliczone5, obliczeniaAut5, sztuki5, srednia5, KGwSkrzynce5);
        }
        private (string TypUmowy, string TypCeny, string Bufor) JakiTypKontraktu()
        {
            string TypUmowy = string.Empty;  // Zainicjalizowane jako pusty string
            string TypCeny = string.Empty;   // Zainicjalizowane jako pusty string
            string Bufor = string.Empty;     // Zainicjalizowane jako pusty string

            var isFreeMarket = MessageBox.Show("Czy hodowca jest WolnymRynkiem?", "Potwierdź", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (isFreeMarket == DialogResult.Yes)
            {
                var isLoyalFreeMarket = MessageBox.Show("Czy jest naszym WIERNYM WolnymRynkiem?", "Potwierdź", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (isLoyalFreeMarket == DialogResult.Yes)
                {
                    TypUmowy = "W.Wolnyrynek";
                    TypCeny = "wolnyrynek";
                    Bufor = "B.Wolny.";
                }
                else
                {
                    TypUmowy = "Wolnyrynek";
                    TypCeny = "wolnyrynek";
                    Bufor = "Do wykupienia";
                }
            }
            else
            {

                var priceOptions = new string[] { "łączona", "rolnicza", "wolnyrynek", "ministerialna" };
                var priceDialog = new Form();
                var layout = new FlowLayoutPanel() { Dock = DockStyle.Fill };
                priceDialog.Controls.Add(layout);

                foreach (var option in priceOptions)
                {
                    var button = new Button() { Text = option, Tag = option };
                    button.Click += (s, ea) => { priceDialog.Tag = option; priceDialog.DialogResult = DialogResult.OK; };
                    layout.Controls.Add(button);
                }

                priceDialog.ShowDialog();

                var selectedPrice = priceDialog.Tag as string;

                if (!string.IsNullOrEmpty(selectedPrice))
                {
                    TypUmowy = "Kontrakt";
                    TypCeny = selectedPrice;
                    Bufor = "B.Kontr.";
                }

            }

            return (TypUmowy, TypCeny, Bufor);
        }

        private long WstawianieWstawienia(
    SqlConnection connection,
    ComboBox dostawca,
    DateTimePicker dataWstawienia,
    TextBox sztukiWstawienia,
    TextBox uwagi,
    String TypUmowy,
    String TypCeny)
        {
            long maxLP = 0;

            try
            {
                connection.Open();

                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string maxLPSql = "SELECT MAX(Lp) AS MaxLP FROM dbo.WstawieniaKurczakow;";
                        using (SqlCommand command = new SqlCommand(maxLPSql, connection, transaction))
                        {
                            object result = command.ExecuteScalar();
                            maxLP = result == DBNull.Value ? 1 : Convert.ToInt64(result) + 1;
                        }

                        if (dostawca.SelectedItem != null)
                        {
                            string insertDostawaSql = @"INSERT INTO dbo.WstawieniaKurczakow (Lp, Dostawca, DataWstawienia, IloscWstawienia, DataUtw, KtoStwo, Uwagi, TypUmowy, TypCeny) 
                                            VALUES (@MaxLP, @Dostawca, @DataWstawienia, @IloscWstawienia, @DataUtw, @KtoStwo, @Uwagi, @TypUmowy, @TypCeny)";
                            using (SqlCommand command = new SqlCommand(insertDostawaSql, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@MaxLP", maxLP);
                                command.Parameters.AddWithValue("@Dostawca", dostawca.SelectedItem.ToString());
                                command.Parameters.AddWithValue("@DataWstawienia", dataWstawienia.Value);
                                command.Parameters.AddWithValue("@IloscWstawienia", string.IsNullOrEmpty(sztukiWstawienia.Text) ? (object)DBNull.Value : Convert.ToInt32(sztukiWstawienia.Text));
                                command.Parameters.AddWithValue("@DataUtw", DateTime.Now);
                                command.Parameters.AddWithValue("@KtoStwo", UserID);  // Upewnij się, że UserID jest zdefiniowane
                                command.Parameters.AddWithValue("@Uwagi", string.IsNullOrEmpty(uwagi.Text) ? (object)DBNull.Value : uwagi.Text);
                                command.Parameters.AddWithValue("@TypUmowy", TypUmowy);
                                command.Parameters.AddWithValue("@TypCeny", TypCeny);
                                command.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException("Dostawca nie został wybrany.");
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show("Wystąpił błąd: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd: " + ex.Message);
            }
            finally
            {
                connection.Close();
            }

            return maxLP;
        }



        private Tuple<string, string, string, string> WstawianieDanych(
    SqlConnection connection,
    CheckBox checkBox,
    ComboBox dostawca,
    DateTimePicker dataOdbioru,
    TextBox kmK,
    TextBox kmH,
    TextBox ubytek,
    TextBox srednia,
    TextBox sztuki,
    TextBox sztukNaSzuflade,
    TextBox liczbaAut,
    TextBox uwagi,
    String TypUmowy,
    String TypCeny,
    String Bufor,
    long LpW)
        {
            string maxLP2Str = string.Empty;
            string dataOdbioruStr = string.Empty;
            string sztukiStr = string.Empty;
            string liczbaAutStr = string.Empty;

            try
            {
                connection.Open();

                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        long maxLP2;
                        string maxLP2Sql = "SELECT MAX(Lp) AS MaxLP2 FROM dbo.HarmonogramDostaw;";
                        using (SqlCommand command = new SqlCommand(maxLP2Sql, connection, transaction))
                        {
                            object maxLP2Result = command.ExecuteScalar();
                            maxLP2 = maxLP2Result == DBNull.Value ? 1 : Convert.ToInt64(maxLP2Result) + 1;
                        }

                        if (dostawca.SelectedItem != null)
                        {
                            string insertDostawaSql = @"INSERT INTO dbo.HarmonogramDostaw (Lp, LpW, Dostawca, DataOdbioru, Kmk, KmH, Ubytek, WagaDek, SztukiDek, TypUmowy, bufor, SztSzuflada, Auta, typCeny, UWAGI, DataUtw, KtoStwo) 
                                    VALUES (@MaxLP2, @MaxLP, @Dostawca, @DataOdbioru, @KmK, @KmH, @Ubytek, @Srednia, @Sztuki, @TypUmowy, @Status, @SztukNaSzuflade, @LiczbaAut, @TypCeny, @Uwagi, @DataUtw, @KtoStwo)";
                            using (SqlCommand command = new SqlCommand(insertDostawaSql, connection, transaction))
                            {
                                // Capture the values as strings before executing the command
                                maxLP2Str = maxLP2.ToString();
                                dataOdbioruStr = dataOdbioru.Value.ToString("yyyy-MM-dd");
                                sztukiStr = string.IsNullOrEmpty(sztuki.Text) ? "NULL" : Convert.ToInt32(sztuki.Text).ToString();
                                liczbaAutStr = string.IsNullOrEmpty(liczbaAut.Text) ? "NULL" : Convert.ToInt32(liczbaAut.Text).ToString();

                                command.Parameters.AddWithValue("@MaxLP2", maxLP2);
                                command.Parameters.AddWithValue("@MaxLP", LpW);
                                command.Parameters.AddWithValue("@Dostawca", dostawca.SelectedItem.ToString());
                                command.Parameters.AddWithValue("@DataOdbioru", dataOdbioru.Value);
                                command.Parameters.AddWithValue("@KmK", string.IsNullOrEmpty(kmK.Text) ? (object)DBNull.Value : kmK.Text);
                                command.Parameters.AddWithValue("@KmH", string.IsNullOrEmpty(kmH.Text) ? (object)DBNull.Value : kmH.Text);
                                command.Parameters.AddWithValue("@Ubytek", string.IsNullOrEmpty(ubytek.Text) ? (object)DBNull.Value : Convert.ToDecimal(ubytek.Text));
                                command.Parameters.AddWithValue("@Srednia", string.IsNullOrEmpty(srednia.Text) ? (object)DBNull.Value : Convert.ToDecimal(srednia.Text));
                                command.Parameters.AddWithValue("@Sztuki", string.IsNullOrEmpty(sztuki.Text) ? (object)DBNull.Value : Convert.ToInt32(sztuki.Text));
                                command.Parameters.AddWithValue("@TypUmowy", TypUmowy);
                                command.Parameters.AddWithValue("@Status", Bufor);
                                command.Parameters.AddWithValue("@SztukNaSzuflade", string.IsNullOrEmpty(sztukNaSzuflade.Text) ? (object)DBNull.Value : Convert.ToInt32(sztukNaSzuflade.Text));
                                command.Parameters.AddWithValue("@LiczbaAut", string.IsNullOrEmpty(liczbaAut.Text) ? (object)DBNull.Value : Convert.ToInt32(liczbaAut.Text));
                                command.Parameters.AddWithValue("@TypCeny", TypCeny);
                                command.Parameters.AddWithValue("@Uwagi", string.IsNullOrEmpty(uwagi.Text) ? (object)DBNull.Value : uwagi.Text);
                                command.Parameters.AddWithValue("@DataUtw", DateTime.Now);
                                command.Parameters.AddWithValue("@KtoStwo", UserID);
                                command.ExecuteNonQuery();

                                // Commit the transaction
                                transaction.Commit();
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException("Dostawca nie został wybrany.");
                        }
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show("Wystąpił błąd: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd: " + ex.Message);
            }
            finally
            {
                connection.Close();
            }

            return Tuple.Create(maxLP2Str, dataOdbioruStr, sztukiStr, liczbaAutStr);
        }

        private void buttonWstawianie_Click(object sender, EventArgs e)
        {
            var contractDetails = JakiTypKontraktu();

            string TypUmowy = contractDetails.TypUmowy;
            string TypCeny = contractDetails.TypCeny;
            string Bufor = contractDetails.Bufor;
            string selectedDostawca = Dostawca.SelectedItem?.ToString().Trim() ?? "";

            using (SqlConnection connection = new SqlConnection(connectionPermission))
            {
                long LpW = WstawianieWstawienia(
                    connection,
                    Dostawca,
                    dataWstawienia,
                    sztukiWstawienia,
                    uwagi,
                    TypUmowy,
                    TypCeny
                );

                StringBuilder successMessages = new StringBuilder();

                successMessages.AppendLine($"Dostawca : {selectedDostawca}, Typ Umowy: {TypUmowy}, TypCeny: {TypCeny} ");

                if (checkBox1.Checked)
                {
                    var result1 = WstawianieDanych(
                        connection,
                        checkBox1,
                        Dostawca,
                        Data1,
                        KmK,
                        KmH,
                        Ubytek,
                        srednia1,
                        sztuki1,
                        sztukNaSzuflade1,
                        liczbaAut1,
                        uwagi,
                        TypUmowy,
                        TypCeny,
                        Bufor,
                        LpW
                    );
                    successMessages.AppendLine($"1.Lp: {result1.Item1}, Data: {result1.Item2}, Sztuki: {result1.Item3}, Auta: {result1.Item4}");
                }

                if (checkBox2.Checked)
                {
                    var result2 = WstawianieDanych(
                        connection,
                        checkBox2,
                        Dostawca,
                        Data2,
                        KmK,
                        KmH,
                        Ubytek,
                        srednia2,
                        sztuki2,
                        sztukNaSzuflade2,
                        liczbaAut2,
                        uwagi,
                        TypUmowy,
                        TypCeny,
                        Bufor,
                        LpW
                    );
                    successMessages.AppendLine($"2.Lp: {result2.Item1}, Data: {result2.Item2}, Sztuki: {result2.Item3}, Auta: {result2.Item4}");
                }

                if (checkBox3.Checked)
                {
                    var result3 = WstawianieDanych(
                        connection,
                        checkBox3,
                        Dostawca,
                        Data3,
                        KmK,
                        KmH,
                        Ubytek,
                        srednia3,
                        sztuki3,
                        sztukNaSzuflade3,
                        liczbaAut3,
                        uwagi,
                        TypUmowy,
                        TypCeny,
                        Bufor,
                        LpW
                    );
                    successMessages.AppendLine($"3.Lp: {result3.Item1}, Data: {result3.Item2}, Sztuki: {result3.Item3}, Auta: {result3.Item4}");
                }

                if (checkBox4.Checked)
                {
                    var result4 = WstawianieDanych(
                        connection,
                        checkBox4,
                        Dostawca,
                        Data4,
                        KmK,
                        KmH,
                        Ubytek,
                        srednia4,
                        sztuki4,
                        sztukNaSzuflade4,
                        liczbaAut4,
                        uwagi,
                        TypUmowy,
                        TypCeny,
                        Bufor,
                        LpW
                    );
                    successMessages.AppendLine($"4.Lp: {result4.Item1}, Data: {result4.Item2}, Sztuki: {result4.Item3}, Auta: {result4.Item4}");
                }

                if (checkBox5.Checked)
                {
                    var result5 = WstawianieDanych(
                        connection,
                        checkBox5,
                        Dostawca,
                        Data5,
                        KmK,
                        KmH,
                        Ubytek,
                        srednia5,
                        sztuki5,
                        sztukNaSzuflade5,
                        liczbaAut5,
                        uwagi,
                        TypUmowy,
                        TypCeny,
                        Bufor,
                        LpW
                    );
                    successMessages.AppendLine($"5.Lp: {result5.Item1}, Data: {result5.Item2}, Sztuki: {result5.Item3}, Auta: {result5.Item4}");
                }

                // Display all success messages at the end
                if (successMessages.Length > 0)
                {
                    MessageBox.Show(successMessages.ToString(), "Successful Entries", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("No entries were processed.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                this.Close();
            }
        }

        private void buttonWklej(TextBox Sztuki, TextBox sztukiRoznica)
        {
            if (double.TryParse(sztukiRoznica.Text, System.Globalization.NumberStyles.AllowThousands, System.Globalization.CultureInfo.CurrentCulture, out double value))
            {
                Sztuki.Text = value.ToString();
            }
            else
            {
                Sztuki.Text = sztukiRoznica.Text; // Preserve the original text if it can't be parsed
            }
        }

        private void Wklej1_Click(object sender, EventArgs e)
        {
            buttonWklej(sztuki1, sztukiRoznica);
        }

        private void Wklej2_Click(object sender, EventArgs e)
        {
            buttonWklej(sztuki2, sztukiRoznica);
        }

        private void Wklej3_Click(object sender, EventArgs e)
        {
            buttonWklej(sztuki3, sztukiRoznica);
        }

        private void Wklej4_Click(object sender, EventArgs e)
        {
            buttonWklej(sztuki4, sztukiRoznica);
        }

        private void Wklej5_Click(object sender, EventArgs e)
        {
            buttonWklej(sztuki5, sztukiRoznica);
        }

        private void buttonAnulowanie_Click_1(object sender, EventArgs e)
        {
            this.Close();
        }

        private void textDni5_TextChanged(object sender, EventArgs e)
        {
            DodajDniDoDaty(textDni4, dataWstawienia, Data4);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            PokazWstawienia();
        }
    }
}
