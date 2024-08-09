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

        private string lpDostawa;
        private int selectedRowIndex = -1; // Zmienna do przechowywania indeksu zaznaczonego wiersza
        public WidokWstawienia()
        {
            InitializeComponent();
            DisplayDataInDataGridView();
            this.StartPosition = FormStartPosition.CenterScreen;

        }



        private bool isUserInitiatedChange = false;

        private void DisplayDataInDataGridView()
        {
            // Zapytanie SQL
            string query = "SELECT LP, Dostawca, CONVERT(varchar, DataWstawienia, 23) AS Data, IloscWstawienia, TypUmowy, Uwagi, [isCheck], [CheckCom] " +
                           "FROM [LibraNet].[dbo].[WstawieniaKurczakow] " +
                           "ORDER BY LP desc, DataWstawienia DESC";

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

                // Ustawienie szerokości kolumn dla dataGridView1
                dataGridView1.Columns["LP"].Width = 40;
                dataGridView1.Columns["Dostawca"].Width = 130;
                dataGridView1.Columns["IloscWstawienia"].HeaderText = "Ilosc";
                dataGridView1.Columns["Data"].Width = 100;
                dataGridView1.Columns["IloscWstawienia"].Width = 50;
                dataGridView1.Columns["TypUmowy"].Width = 80;
                dataGridView1.Columns["Uwagi"].Width = 80;
                dataGridView1.Columns["IloscWstawienia"].HeaderText = "Ilosc";
                dataGridView1.Columns["isCheck"].Width = 45;
                dataGridView1.Columns["isCheck"].HeaderText = "V";
                dataGridView1.Columns["CheckCom"].Width = 100;
                dataGridView1.RowHeadersVisible = false;
                dataGridView2.RowHeadersVisible = false;
                dataGridView3.RowHeadersVisible = false;
                dataGridView4.RowHeadersVisible = false;

                // Ustawienie formatu kolumny "IloscWstawienia" z odstępami tysięcznymi
                dataGridView1.Columns["IloscWstawienia"].DefaultCellStyle.Format = "#,##0";

                // Ustawienie wysokości wierszy
                SetRowHeights(dataGridView1, 11);

                // Tworzenie drugiego DataGridView (dataGridView3)
                dataGridView3.Columns.Clear(); // Usunięcie poprzednich kolumn
                dataGridView3.Columns.Add("Lp", "LP");
                dataGridView3.Columns.Add("Data", "Data");
                dataGridView3.Columns.Add("Dostawca", "Dostawca");
                dataGridView3.Columns.Add("IloscWstawienia", "Ilosc");

                DataGridViewCheckBoxColumn confirmColumn = new DataGridViewCheckBoxColumn();
                confirmColumn.HeaderText = "V";
                confirmColumn.Name = "ConfirmColumn";
                confirmColumn.Width = 80;
                dataGridView3.Columns.Add(confirmColumn);
                dataGridView3.Columns["ConfirmColumn"].Width = 35;

                // Ustawienie szerokości kolumn dla dataGridView3
                dataGridView3.Columns["Lp"].Width = 45;
                dataGridView3.Columns["Data"].Width = 90;
                dataGridView3.Columns["Dostawca"].Width = 120;
                dataGridView3.Columns["IloscWstawienia"].Width = 70;

                for (int i = 0; i < dataGridView1.Rows.Count; i++)
                {
                    // Formatowanie wierszy zgodnie z statusami
                    FormatujWierszeZgodnieZStatus(i);

                    // Dodawanie skopiowanych wierszy do dataGridView3
                    if (dataGridView1.Rows[i].DefaultCellStyle.BackColor == Color.Red)
                    {
                        dataGridView3.Rows.Add(
                            dataGridView1.Rows[i].Cells["LP"].Value,
                            dataGridView1.Rows[i].Cells["Data"].Value,
                            dataGridView1.Rows[i].Cells["Dostawca"].Value,
                            dataGridView1.Rows[i].Cells["IloscWstawienia"].Value
                        );
                    }
                }


            }

            DisplayDataInDataGridView4();

            // Dodanie obsługi zdarzenia CellValueChanged
            dataGridView1.CellValueChanged -= dataGridView1_CellValueChanged;
            dataGridView1.CellValueChanged += dataGridView1_CellValueChanged;
            dataGridView1.CurrentCellDirtyStateChanged -= dataGridView1_CurrentCellDirtyStateChanged;
            dataGridView1.CurrentCellDirtyStateChanged += dataGridView1_CurrentCellDirtyStateChanged;

            // Sortowanie dataGridView3 po kolumnie "Data" w kolejności malejącej
            dataGridView3.Sort(dataGridView3.Columns["Data"], System.ComponentModel.ListSortDirection.Descending);
        }


        private void DisplayDataInDataGridView4()
        {
            // Nowe zapytanie SQL
            string query = @"
        WITH FilteredData AS (
            SELECT *
            FROM [LibraNet].[dbo].[HarmonogramDostaw]
            WHERE [LpW] IS NULL
        ),
        SuppliersWithOnlyNullLpW AS (
            SELECT [Dostawca]
            FROM [LibraNet].[dbo].[HarmonogramDostaw]
            GROUP BY [Dostawca]
            HAVING COUNT(*) = SUM(CASE WHEN [LpW] IS NULL THEN 1 ELSE 0 END)
        ),
        RankedData AS (
            SELECT 
                fd.[Lp],
                fd.[DataOdbioru],
                fd.[Dostawca],
                fd.[SztukiDek],
                fd.[WagaDek],
                fd.[TypCeny],
                fd.[Bufor],
                ROW_NUMBER() OVER (PARTITION BY fd.[Dostawca] ORDER BY fd.[DataOdbioru]) AS RowNum
            FROM FilteredData fd
            INNER JOIN SuppliersWithOnlyNullLpW swn ON fd.[Dostawca] = swn.[Dostawca]
        )
        SELECT 
            [Lp],
            [DataOdbioru],
            [Dostawca],
            [SztukiDek],
            [WagaDek],
            [TypCeny],
            [Bufor]
        FROM RankedData
        WHERE RowNum = 1
        ORDER BY [DataOdbioru] DESC;";

            // Utworzenie połączenia z bazą danych
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Utworzenie adaptera danych
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);

                // Utworzenie tabeli danych
                DataTable table = new DataTable();

                // Wypełnienie tabeli danymi z adaptera
                adapter.Fill(table);

                // Ustawienie źródła danych dla DataGridView
                dataGridView4.DataSource = table;

                // Ustawienie szerokości kolumn dla dataGridView4
                dataGridView4.Columns["Lp"].Width = 45;
                dataGridView4.Columns["DataOdbioru"].Width = 75;
                dataGridView4.Columns["Dostawca"].Width = 120;
                dataGridView4.Columns["SztukiDek"].Width = 40;
                dataGridView4.Columns["SztukiDek"].HeaderText = "Sztuki";
                dataGridView4.Columns["WagaDek"].Width = 40;
                dataGridView4.Columns["WagaDek"].HeaderText = "Waga";
                dataGridView4.Columns["TypCeny"].Width = 80;
                dataGridView4.Columns["Bufor"].Width = 80;

                // Ustawienie wysokości wierszy

            }

            // Dodanie obsługi zdarzenia CellValueChanged
        }

        private void SetRowHeights(DataGridView dataGridView, int height)
        {
            // Ustawienie wysokości wierszy
            foreach (DataGridViewRow row in dataGridView.Rows)
            {
                row.Height = height;
            }
            // Ustawienie wysokości nagłówka
            dataGridView.ColumnHeadersHeight = height * 2; // Można dostosować według potrzeb
        }


        private void dataGridView1_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentCell is DataGridViewCheckBoxCell)
            {
                dataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }

        }

        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {


            if (e.ColumnIndex == dataGridView1.Columns["isCheck"].Index && e.RowIndex >= 0)
            {
                if (isUserInitiatedChange)
                {
                    bool isChecked = (bool)dataGridView1.Rows[e.RowIndex].Cells["isCheck"].Value;
                    int lp = (int)dataGridView1.Rows[e.RowIndex].Cells["LP"].Value;

                    string userComment = ShowInputDialog("Enter your comment:");

                    UpdateIsCheckInDatabase(lp, isChecked, userComment);
                    DisplayDataInDataGridView(); // Przeładowanie danych po zmianie
                }
            }


        }

        private string ShowInputDialog(string text)
        {
            Form prompt = new Form()
            {
                Width = 500,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "Input",
                StartPosition = FormStartPosition.CenterScreen
            };
            Label textLabel = new Label() { Left = 50, Top = 20, Text = text };
            TextBox inputBox = new TextBox() { Left = 50, Top = 50, Width = 400 };
            Button confirmation = new Button() { Text = "Ok", Left = 350, Width = 100, Top = 70, DialogResult = DialogResult.OK };
            confirmation.Click += (sender, e) => { prompt.Close(); };
            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(inputBox);
            prompt.Controls.Add(confirmation);
            prompt.AcceptButton = confirmation;

            return prompt.ShowDialog() == DialogResult.OK ? inputBox.Text : "";
        }

        private void UpdateIsCheckInDatabase(int lp, bool isChecked, string comment)
        {
            string query = "UPDATE [LibraNet].[dbo].[WstawieniaKurczakow] SET [isCheck] = @isCheck, [CheckCom] = @comment WHERE LP = @lp";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@isCheck", isChecked ? 1 : 0);
                    command.Parameters.AddWithValue("@comment", comment);
                    command.Parameters.AddWithValue("@lp", lp);

                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }

        }

        // Aby ustawić flagę, gdy zmiana jest inicjowana przez użytkownika
        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {


            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                selectedRowIndex = e.RowIndex;
                object wartoscKomorki = dataGridView1.Rows[e.RowIndex].Cells["LP"].Value;
                lpDostawa = wartoscKomorki != null ? wartoscKomorki.ToString() : "0";


            }

            if (e.ColumnIndex == dataGridView1.Columns["isCheck"].Index && e.RowIndex >= 0)
            {
                isUserInitiatedChange = true;
            }
            // Utwórz połączenie z bazą danych SQL Server
            using (SqlConnection cnn = new SqlConnection(connectionString))
            {
                cnn.Open();

                if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                {
                    // Pobierz wartość z kolumny "Lp" z zaznaczonego wiersza
                    object selectedCellValue = dataGridView1.Rows[e.RowIndex].Cells["Lp"].Value;

                    String strSQL;

                    strSQL = "SELECT LP, DataOdbioru, SztukiDek, bufor FROM [LibraNet].[dbo].[HarmonogramDostaw] WHERE LpW = @NumerWstawienia order by DataOdbioru ASC";

                    // Przygotowanie drugiego zapytania
                    double sumaSztukWstawienia = 0;

                    // Wyczyszczenie danych w DataGridView2
                    dataGridView2.Rows.Clear();
                    dataGridView2.Columns.Clear(); // Usunięcie istniejących kolumn

                    // Dodanie kolumn do DataGridView2
                    dataGridView2.Columns.Add("DataOdbioru", "Data Odbioru");
                    dataGridView2.Columns.Add("SztukiDek", "Sztuki Dek");
                    dataGridView2.Columns.Add("Bufor", "Bufor");

                    using (SqlCommand command2 = new SqlCommand(strSQL, cnn))
                    {
                        command2.Parameters.AddWithValue("@NumerWstawienia", selectedCellValue);

                        using (SqlDataReader rs = command2.ExecuteReader())
                        {
                            while (rs.Read())
                            {
                                // Sprawdź, czy wartość DataOdbioru nie jest DBNull przed konwersją na DateTime
                                string dataOdbioru = string.Empty;
                                if (rs["DataOdbioru"] != DBNull.Value)
                                {
                                    dataOdbioru = Convert.ToDateTime(rs["DataOdbioru"]).ToString("yyyy-MM-dd dddd");
                                }

                                // Sprawdź, czy wartość SztukiDek nie jest DBNull przed dodaniem do sumy
                                object sztukiDekValue = rs["SztukiDek"];
                                if (sztukiDekValue != DBNull.Value)
                                {
                                    sumaSztukWstawienia += Convert.ToDouble(sztukiDekValue);
                                }

                                // Formatuj wartość SztukiDek z odstępami tysięcznymi
                                string formattedSztukiDek = sztukiDekValue != DBNull.Value ? string.Format("{0:#,0}", sztukiDekValue) : "0";

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

        private void dataGridView1_MouseUp(object sender, MouseEventArgs e)
        {
            isUserInitiatedChange = false;
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
                var dataWstawieniaCell = dataGridView1.Rows[rowIndex].Cells["Data"];
                var dostawcaCell = dataGridView1.Rows[rowIndex].Cells["Dostawca"];
                var isCheckCell = dataGridView1.Rows[rowIndex].Cells["isCheck"];

                if (dataWstawieniaCell != null && dataWstawieniaCell.Value != null && dostawcaCell != null && dostawcaCell.Value != null)
                {
                    DateTime dataWstawienia;
                    bool isChecked = false;

                    if (isCheckCell != null && isCheckCell.Value != DBNull.Value)
                    {
                        isChecked = (bool)isCheckCell.Value;
                    }

                    if (DateTime.TryParse(dataWstawieniaCell.Value.ToString(), out dataWstawienia) && !isChecked)
                    {
                        // Oblicz różnicę w dniach między datą wstawienia a dniem obecnym
                        TimeSpan roznicaDni = DateTime.Now.Date - dataWstawienia.Date;

                        // Sprawdź, czy różnica dni wynosi 35
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
                if (row.Cells["Dostawca"].Value != null && row.Cells["Data"].Value != null &&
                    row.Cells["Dostawca"].Value.ToString() == dostawca)
                {
                    DateTime dataWstawienia;
                    if (DateTime.TryParse(row.Cells["Data"].Value.ToString(), out dataWstawienia))
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

        private void button3_Click(object sender, EventArgs e)
        {
            string deleteQuery1 = "DELETE FROM dbo.HarmonogramDostaw WHERE LpW = @LpW";
            string deleteQuery2 = "DELETE FROM dbo.WstawieniaKurczakow WHERE Lp = @LpW";

            using (SqlConnection cnn = new SqlConnection(connectionString))
            {
                try
                {
                    cnn.Open();

                    using (SqlCommand cmd1 = new SqlCommand(deleteQuery1, cnn))
                    {
                        cmd1.Parameters.AddWithValue("@LpW", lpDostawa);
                        cmd1.ExecuteNonQuery();
                    }

                    using (SqlCommand cmd2 = new SqlCommand(deleteQuery2, cnn))
                    {
                        cmd2.Parameters.AddWithValue("@LpW", lpDostawa);
                        cmd2.ExecuteNonQuery();
                    }

                    MessageBox.Show("Wiersz oraz powiązane z nim dane zostały usunięte z bazy danych.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // Odśwież wszystkie połączenia w skoroszycie
                    DisplayDataInDataGridView();
                    this.StartPosition = FormStartPosition.CenterScreen;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Wystąpił błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Wstawienie wstawienie = new Wstawienie();
            wstawienie.UserID = App.UserID;

            // Initialize fields and execute methods
            wstawienie.WypelnijStartowo();

            // Wyświetlanie Form1
            wstawienie.Show();

            DisplayDataInDataGridView();
        }
    }
}