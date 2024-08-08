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
using static Azure.Core.HttpHeader;

namespace Kalendarz1
{
    public partial class WidokWszystkichDostaw : Form
    {
        static string connectionPermission = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        public string TextBoxValue { get; set; }
        public WidokWszystkichDostaw()
        {
            InitializeComponent();
            DisplayDataInDataGridView();
        }
        public void SetTextBoxValue()
        {
            textBox1.Text = TextBoxValue;
        }
        private void DisplayDataInDataGridView()
        {
            // Zapytanie SQL zależne od stanu checkboxa
            string query = @"SELECT * FROM [LibraNet].[dbo].[HarmonogramDostaw] order by LP Desc";

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
                dataGridView1.DataSource = table;

                // Ustawienie wysokości wierszy na minimalną wartość
                SetRowHeights(18);

                // Ustawienie nazw i szerokości kolumn
                dataGridView1.Columns["Lp"].HeaderText = "Lp";
                dataGridView1.Columns["Lp"].Width = 50;

                dataGridView1.Columns["DostawcaID"].HeaderText = "LpK";
                dataGridView1.Columns["DostawcaID"].Width = 50;

                dataGridView1.Columns["DataOdbioru"].HeaderText = "DOdb";
                dataGridView1.Columns["DataOdbioru"].Width = 80;

                dataGridView1.Columns["Dostawca"].HeaderText = "Dost";
                dataGridView1.Columns["Dostawca"].Width = 150;

                dataGridView1.Columns["Auta"].HeaderText = "Auta";
                dataGridView1.Columns["Auta"].Width = 40;

                dataGridView1.Columns["SztukiDek"].HeaderText = "SztDek";
                dataGridView1.Columns["SztukiDek"].Width = 60;

                dataGridView1.Columns["WagaDek"].HeaderText = "Waga";
                dataGridView1.Columns["WagaDek"].Width = 50;

                dataGridView1.Columns["SztSzuflada"].HeaderText = "SztSz";
                dataGridView1.Columns["SztSzuflada"].Width = 30;

                dataGridView1.Columns["TypUmowy"].HeaderText = "TUm";
                dataGridView1.Columns["TypUmowy"].Width = 50;

                dataGridView1.Columns["TypCeny"].HeaderText = "TCen";
                dataGridView1.Columns["TypCeny"].Width = 50;

                dataGridView1.Columns["Cena"].HeaderText = "Cena";
                dataGridView1.Columns["Cena"].Width = 50;

                dataGridView1.Columns["PaszaPisklak"].Visible = false;
                dataGridView1.Columns["PaszaPisklak"].HeaderText = "PaszPis";
                dataGridView1.Columns["PaszaPisklak"].Width = 70;

                dataGridView1.Columns["Bufor"].HeaderText = "Bufor";
                dataGridView1.Columns["Bufor"].Width = 50;

                dataGridView1.Columns["UWAGI"].HeaderText = "Uwagi";
                dataGridView1.Columns["UWAGI"].Width = 100;

                dataGridView1.Columns["LpW"].HeaderText = "LpW";
                dataGridView1.Columns["LpW"].Width = 50;

                dataGridView1.Columns["LpP1"].Visible = false;
                dataGridView1.Columns["LpP1"].HeaderText = "LpP1";
                dataGridView1.Columns["LpP1"].Width = 50;

                dataGridView1.Columns["LpP2"].Visible = false;
                dataGridView1.Columns["LpP2"].HeaderText = "LpP2";
                dataGridView1.Columns["LpP2"].Width = 50;

                dataGridView1.Columns["Utworzone"].HeaderText = "Utw";
                dataGridView1.Columns["Utworzone"].Width = 25;

                dataGridView1.Columns["Wysłane"].HeaderText = "Wys";
                dataGridView1.Columns["Wysłane"].Width = 25;

                dataGridView1.Columns["Otrzymane"].HeaderText = "Otrz";
                dataGridView1.Columns["Otrzymane"].Width = 25;

                dataGridView1.Columns["PotwWaga"].HeaderText = "PWaga";
                dataGridView1.Columns["PotwWaga"].Width = 25;

                dataGridView1.Columns["KtoWaga"].HeaderText = "KWaga";
                dataGridView1.Columns["KtoWaga"].Width = 70;

                dataGridView1.Columns["KiedyWaga"].HeaderText = "KiedyW";
                dataGridView1.Columns["KiedyWaga"].Width = 70;

                dataGridView1.Columns["PotwSztuki"].HeaderText = "PSzt";
                dataGridView1.Columns["PotwSztuki"].Width = 25;

                dataGridView1.Columns["KtoSztuki"].HeaderText = "KSzt";
                dataGridView1.Columns["KtoSztuki"].Width = 70;

                dataGridView1.Columns["KiedySztuki"].HeaderText = "KiedyS";
                dataGridView1.Columns["KiedySztuki"].Width = 70;

                dataGridView1.Columns["PotwCena"].HeaderText = "PCena";
                dataGridView1.Columns["PotwCena"].Width = 70;

                dataGridView1.Columns["Dodatek"].HeaderText = "Dod";
                dataGridView1.Columns["Dodatek"].Width = 50;

                dataGridView1.Columns["Kurnik"].HeaderText = "Kurn";
                dataGridView1.Columns["Kurnik"].Width = 70;

                dataGridView1.Columns["KmK"].HeaderText = "KmK";
                dataGridView1.Columns["KmK"].Width = 50;

                dataGridView1.Columns["KmH"].HeaderText = "KmH";
                dataGridView1.Columns["KmH"].Width = 50;

                dataGridView1.Columns["Ubiorka"].Visible = false;
                dataGridView1.Columns["Ubiorka"].HeaderText = "Ubiorka";
                dataGridView1.Columns["Ubiorka"].Width = 70;

                dataGridView1.Columns["Ubytek"].HeaderText = "Ubyt";
                dataGridView1.Columns["Ubytek"].Width = 70;

                dataGridView1.Columns["DataUtw"].HeaderText = "DUtw";
                dataGridView1.Columns["DataUtw"].Width = 70;

                dataGridView1.Columns["KtoStwo"].HeaderText = "KtoSt";
                dataGridView1.Columns["KtoStwo"].Width = 70;

                dataGridView1.Columns["DataMod"].HeaderText = "DMod";
                dataGridView1.Columns["DataMod"].Width = 70;

                dataGridView1.Columns["KtoMod"].HeaderText = "KtoMod";
                dataGridView1.Columns["KtoMod"].Width = 70;

                dataGridView1.Columns["CzyOdznaczoneWstawienie"].HeaderText = "CzyOds";
                dataGridView1.Columns["CzyOdznaczoneWstawienie"].Width = 25;
            }

            // Subskrybuj zdarzenie CellFormatting
            dataGridView1.CellFormatting += DataGridView1_CellFormatting;
        }

        private void SetRowHeights(int height)
        {
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                row.Height = height;
            }
        }

        private void DataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dataGridView1.Columns[e.ColumnIndex].Name == "Bufor")
            {
                DataGridViewRow row = dataGridView1.Rows[e.RowIndex];
                DataGridViewCell statusCell = row.Cells["Bufor"];

                if (statusCell != null && statusCell.Value != null)
                {
                    string status = statusCell.Value.ToString();

                    if (status == "Potwierdzony")
                    {
                        row.DefaultCellStyle.BackColor = Color.LightGreen;
                        row.DefaultCellStyle.Font = new Font(dataGridView1.Font.FontFamily, 9, FontStyle.Bold);
                    }
                    else if (status == "Anulowany")
                    {
                        row.DefaultCellStyle.BackColor = Color.Red;
                    }
                    else if (status == "Sprzedany")
                    {
                        row.DefaultCellStyle.BackColor = Color.LightBlue;
                    }
                    else if (status == "B.Kontr.")
                    {
                        row.DefaultCellStyle.BackColor = Color.Indigo;
                        row.DefaultCellStyle.ForeColor = Color.White;
                    }
                    else if (status == "B.Wolny.")
                    {
                        row.DefaultCellStyle.BackColor = Color.Yellow;
                    }
                    else if (status == "Do wykupienia")
                    {
                        row.DefaultCellStyle.BackColor = Color.WhiteSmoke;
                    }
                }
            }
        }


        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            string filterText = textBox1.Text.Trim().ToLower();

            // Sprawdzenie, czy istnieje źródło danych dla DataGridView
            if (dataGridView1.DataSource is DataTable dataTable)
            {
                // Ustawienie filtra dla kolumny "Dostawca"
                dataTable.DefaultView.RowFilter = $"Dostawca LIKE '%{filterText}%'";

                // Przywrócenie pozycji kursora po zastosowaniu filtra
                if (dataGridView1.RowCount > 0)
                {
                    int currentPosition = dataGridView1.FirstDisplayedScrollingRowIndex;
                    if (currentPosition >= 0 && currentPosition < dataGridView1.RowCount)
                    {
                        dataGridView1.FirstDisplayedScrollingRowIndex = currentPosition;
                    }
                }
            }
        }

    }
}
