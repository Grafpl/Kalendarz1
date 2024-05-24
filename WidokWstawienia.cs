using System;
using System.Data;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using System.Drawing;

namespace Kalendarz1
{
    public partial class WidokWstawienia : Form
    {
        // Connection string do bazy danych
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public WidokWstawienia()
        {
            InitializeComponent();
            DisplayDataInDataGridView();
        }

        private void DisplayDataInDataGridView()
        {
            // Zapytanie SQL
            string query = "SELECT LP, Dostawca, CONVERT(varchar, DataWstawienia, 23) AS DataWstawienia, IloscWstawienia, TypUmowy, Uwagi " +
                           "FROM [LibraNet].[dbo].[WstawieniaKurczakow] " +
                           "ORDER BY Dostawca ASC, DataWstawienia DESC";

            // Utworzenie połączenia z bazą danych
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Utworzenie adaptera danych
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);

                // Utworzenie tabeli danych
                DataTable table = new DataTable();

                // Wypełnienie tabeli danymi z adaptera
                adapter.Fill(table);

                // Dodanie pustych wierszy między różnymi dostawcami
                AddEmptyRows(table);

                // Ustawienie źródła danych dla DataGridView
                dataGridView1.DataSource = table;

                // Automatyczne dopasowanie szerokości kolumn do zawartości
                dataGridView1.AutoResizeColumns();

                // Ustawienie formatu kolumny "IloscWstawienia" z odstępami tysięcznymi
                dataGridView1.Columns["IloscWstawienia"].DefaultCellStyle.Format = "#,##0";

                for (int i = 0; i < dataGridView1.Rows.Count; i++)
                {
                    FormatujWierszeZgodnieZStatus(i);
                }

                // Tworzenie drugiego DataGridView (dataGridView2)
                dataGridView3.Columns.Add("DataWstawienia", "Data Wstawienia");
                dataGridView3.Columns.Add("Dostawca", "Dostawca");
                dataGridView3.Columns.Add("IloscWstawienia", "Ilosc Wstawienia");



                for (int i = 0; i < dataGridView1.Rows.Count; i++)
                {
                    // Formatowanie wierszy zgodnie z statusami
                    FormatujWierszeZgodnieZStatus(i);

                    // Dodawanie skopiowanych wierszy do dataGridView2
                    if (dataGridView1.Rows[i].DefaultCellStyle.BackColor == Color.Red)
                    {
                        dataGridView3.Rows.Add(
                            dataGridView1.Rows[i].Cells["DataWstawienia"].Value,
                            dataGridView1.Rows[i].Cells["Dostawca"].Value,
                            dataGridView1.Rows[i].Cells["IloscWstawienia"].Value
                        );
                    }
                }
            }
        }

        private void dataGridView1_CellFormatting_1(object sender, DataGridViewCellFormattingEventArgs e)
        {
            FormatujWierszeZgodnieZStatus(e.RowIndex);
        }
        private void AddEmptyRows(DataTable dataTable)
        {
            DataRow previousRow = null;

            // Przechodzenie przez wiersze i dodawanie pustych wierszy między różnymi dostawcami
            for (int i = dataTable.Rows.Count - 1; i >= 0; i--)
            {
                DataRow currentRow = dataTable.Rows[i];
                if (previousRow != null && currentRow["Dostawca"].ToString() != previousRow["Dostawca"].ToString())
                {
                    DataRow emptyRow = dataTable.NewRow();
                    dataTable.Rows.InsertAt(emptyRow, i + 1);
                }
                previousRow = currentRow;
            }
        }
        private void FormatujWierszeZgodnieZStatus(int rowIndex)
        {
            if (rowIndex >= 0)
            {
                var dataWstawieniaCell = dataGridView1.Rows[rowIndex].Cells["DataWstawienia"];
                var dostawcaCell = dataGridView1.Rows[rowIndex].Cells["Dostawca"];

                if (dataWstawieniaCell != null && dataWstawieniaCell.Value != null && dostawcaCell != null && dostawcaCell.Value != null)
                {
                    DateTime dataWstawienia;
                    if (DateTime.TryParse(dataWstawieniaCell.Value.ToString(), out dataWstawienia))
                    {
                        // Oblicz różnicę w dniach między datą wstawienia a dniem obecnym
                        TimeSpan roznicaDni = DateTime.Now.Date - dataWstawienia.Date;

                        // Sprawdź, czy różnica dni wynosi 28
                        if (roznicaDni.Days >= 35)
                        {
                            // Znajdź maksymalną wartość dla dostawcy
                            DateTime maxDataWstawienia = ZnajdzMaxDateDlaDostawcy(dostawcaCell.Value.ToString());

                            // Sprawdź, czy aktualna data wstawienia jest maksymalną datą dla dostawcy
                            if (dataWstawienia == maxDataWstawienia)
                            {
                                dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.Red;
                            }
                        }
                    }
                    else
                    {
                        // Obsłuż przypadki, gdy wartość w komórce "DataWstawienia" nie może być przekonwertowana na DateTime
                        // Tutaj możesz dodać kod obsługi takich przypadków, np. wypisanie komunikatu o błędzie
                        // Możesz także zastosować inne działania w zależności od potrzeb
                    }
                }
            }
        }
        private DateTime ZnajdzMaxDateDlaDostawcy(string dostawca)
        {
            DateTime maxDataWstawienia = DateTime.MinValue;

            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (row.Cells["Dostawca"].Value != null && row.Cells["DataWstawienia"].Value != null &&
                    row.Cells["Dostawca"].Value.ToString() == dostawca)
                {
                    DateTime dataWstawienia;
                    if (DateTime.TryParse(row.Cells["DataWstawienia"].Value.ToString(), out dataWstawienia))
                    {
                        if (dataWstawienia > maxDataWstawienia)
                        {
                            maxDataWstawienia = dataWstawienia;
                        }
                    }
                }
            }

            return maxDataWstawienia;
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
                int currentPosition = dataGridView1.FirstDisplayedScrollingRowIndex;
                if (currentPosition >= 0 && currentPosition < dataGridView1.RowCount)
                {
                    dataGridView1.FirstDisplayedScrollingRowIndex = currentPosition;
                }
            }
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            // Utwórz połączenie z bazą danych SQL Server
            using (SqlConnection cnn = new SqlConnection(connectionString))
            {
                cnn.Open();

                if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                {
                    // Pobierz wartość z kolumny "Lp" z zaznaczonego wiersza
                    object selectedCellValue = dataGridView1.Rows[e.RowIndex].Cells["Lp"].Value;

                    String strSQL;

                    strSQL = "SELECT LP, DataOdbioru, SztukiDek FROM [LibraNet].[dbo].[HarmonogramDostaw] WHERE LpW = @NumerWstawienia order by DataOdbioru ASC";

                    // Przygotowanie drugiego zapytania
                    double sumaSztukWstawienia = 0;

                    // Wyczyszczenie danych w DataGridView2
                    dataGridView2.Rows.Clear();
                    dataGridView2.Columns.Clear(); // Usunięcie istniejących kolumn

                    // Dodanie kolumn do DataGridView2
                    dataGridView2.Columns.Add("DataOdbioru", "Data Odbioru");
                    dataGridView2.Columns.Add("SztukiDek", "Sztuki Dek");

                    using (SqlCommand command2 = new SqlCommand(strSQL, cnn))
                    {
                        command2.Parameters.AddWithValue("@NumerWstawienia", selectedCellValue);

                        using (SqlDataReader rs = command2.ExecuteReader())
                        {
                            while (rs.Read())
                            {
                                // Tutaj wklej kod do formatowania daty
                                string dataOdbioru = Convert.ToDateTime(rs["DataOdbioru"]).ToString("yyyy-MM-dd dddd");

                                // Sprawdź, czy wartość SztukiDek nie jest DBNull przed dodaniem do sumy
                                object sztukiDekValue = rs["SztukiDek"];
                                if (sztukiDekValue != DBNull.Value)
                                {
                                    sumaSztukWstawienia += Convert.ToDouble(sztukiDekValue);
                                }

                                // Formatuj wartość SztukiDek z odstępami tysięcznymi
                                string formattedSztukiDek = string.Format("{0:#,0}", sztukiDekValue);

                                // Dodaj wiersz do DataGridView2
                                dataGridView2.Rows.Add(dataOdbioru, formattedSztukiDek);
                            }
                        }
                    }

                    // Ustawienie sumy sztuk w TextBoxie z odstępami tysięcznymi
                    sumaSztuk.Text = string.Format("{0:#,0}", sumaSztukWstawienia);

                    // Dodanie wiersza z sumą sztuk na końcu
                    dataGridView2.Rows.Add("Suma:", string.Format("{0:#,0}", sumaSztukWstawienia));

                    // Ustawienie wyrównania tekstu w komórkach ostatniego wiersza
                    dataGridView2.Rows[dataGridView2.Rows.Count - 1].Cells[0].Style.Alignment = DataGridViewContentAlignment.MiddleRight;
                    dataGridView2.Rows[dataGridView2.Rows.Count - 1].Cells[1].Style.Alignment = DataGridViewContentAlignment.MiddleRight;

                    // Ustawienie pogrubienia tekstu w komórkach ostatniego wiersza
                    dataGridView2.Rows[dataGridView2.Rows.Count - 1].Cells[0].Style.Font = new Font(dataGridView2.DefaultCellStyle.Font, FontStyle.Bold);
                    dataGridView2.Rows[dataGridView2.Rows.Count - 1].Cells[1].Style.Font = new Font(dataGridView2.DefaultCellStyle.Font, FontStyle.Bold);

                    // Automatyczne dopasowanie szerokości kolumn
                    dataGridView2.AutoResizeColumns();
                }
            }
        }



    }
}