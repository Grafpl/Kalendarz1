using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    public partial class WidokWstawienia : Form
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private string lpDostawa;
        private static ZapytaniaSQL zapytaniasql = new ZapytaniaSQL();
        private bool isUserInitiatedChange = false;

        public WidokWstawienia()
        {
            InitializeComponent();

            DisplayDataInDataGridView();   // grid1
            DisplayDataInDataGridView4();  // grid4
            LoadRemindersToGrid3();        // grid3 – tylko ostatnie na hodowcę

            // jeśli nie podpiąłeś w Designerze: odkomentuj poniższe
            // this.btnSnooze.Click += btnSnooze_Click;

            dataGridView1.RowHeadersVisible = false;
            dataGridView2.RowHeadersVisible = false;
            dataGridView3.RowHeadersVisible = false;
            dataGridView4.RowHeadersVisible = false;

            this.StartPosition = FormStartPosition.CenterScreen;

            dataGridView1.CellValueChanged -= dataGridView1_CellValueChanged;
            dataGridView1.CellValueChanged += dataGridView1_CellValueChanged;
            dataGridView1.CurrentCellDirtyStateChanged -= dataGridView1_CurrentCellDirtyStateChanged;
            dataGridView1.CurrentCellDirtyStateChanged += dataGridView1_CurrentCellDirtyStateChanged;
            datapickerOdlozenie.MinDate = DateTime.Today;
            datapickerOdlozenie.Value = DateTime.Today;

            // wybór wiersza w grid3 -> ustaw lp i pokaż historię
            dataGridView3.SelectionChanged += (s, e) =>
            {
                if (dataGridView3.CurrentRow != null)
                    lpDostawa = Convert.ToString(dataGridView3.CurrentRow.Cells["LP"].Value);
                LoadHistoryAll();
            };
            dataGridView3.CellClick += dataGridView3_CellClick;
        }

        // ====== GRID 1 (Twoja lista wstawień) ======
        private void DisplayDataInDataGridView()
        {
            string query =
                "SELECT W.LP, W.Dostawca, CONVERT(varchar, W.DataWstawienia, 23) AS Data, " +
                "W.IloscWstawienia, W.TypUmowy, ISNULL(O.Name, '-') AS KtoStwo, W.DataUtw, W.[isCheck] " +
                "FROM dbo.WstawieniaKurczakow W " +
                "LEFT JOIN dbo.operators O ON W.KtoStwo = O.ID " +
                "ORDER BY W.LP DESC, W.DataWstawienia DESC";

            using (var connection = new SqlConnection(connectionString))
            using (var adapter = new SqlDataAdapter(query, connection))
            {
                var table = new DataTable();
                adapter.Fill(table);
                AddEmptyRows(table);
                dataGridView1.DataSource = table;

                dataGridView1.Columns["LP"].Width = 35;
                dataGridView1.Columns["Dostawca"].Width = 110;
                dataGridView1.Columns["Data"].Width = 70;
                dataGridView1.Columns["IloscWstawienia"].Width = 50;
                dataGridView1.Columns["TypUmowy"].Width = 75;
                dataGridView1.Columns["IloscWstawienia"].HeaderText = "Ilosc";
                dataGridView1.Columns["isCheck"].Width = 45;
                dataGridView1.Columns["isCheck"].HeaderText = "V";
                dataGridView1.Columns["isCheck"].Visible = false;
                dataGridView1.Columns["KtoStwo"].Width = 100;
                dataGridView1.Columns["KtoStwo"].HeaderText = "Kto Stworzył";
                dataGridView1.Columns["DataUtw"].Width = 100;
                dataGridView1.Columns["DataUtw"].HeaderText = "Kiedy Stworzone";

                dataGridView1.Columns["IloscWstawienia"].DefaultCellStyle.Format = "#,##0";
            }
        }

        // ====== GRID 3 (przypomnienia – tylko ostatnie wstawienie na hodowcę) ======
        private void LoadRemindersToGrid3()
        {
            using (var cnn = new SqlConnection(connectionString))
            using (var da = new SqlDataAdapter(
                @"SELECT 
              v.LP,
              CAST(v.DataWstawienia AS date) AS Data,
              v.Dostawca,
              v.IloscWstawienia AS Ilosc,
              d.Phone1 AS Telefon
          FROM dbo.v_WstawieniaDoKontaktu AS v
          LEFT JOIN [LibraNet].[dbo].[Dostawcy] AS d
                 ON d.ShortName = v.Dostawca
          ORDER BY Data DESC, Dostawca", cnn))
            {
                var dt = new DataTable();
                da.Fill(dt);
                dataGridView3.DataSource = dt;

                if (dataGridView3.Columns.Contains("LP")) dataGridView3.Columns["LP"].Width = 45;
                if (dataGridView3.Columns.Contains("Data")) dataGridView3.Columns["Data"].Width = 90;
                if (dataGridView3.Columns.Contains("Dostawca")) dataGridView3.Columns["Dostawca"].Width = 160;
                if (dataGridView3.Columns.Contains("Ilosc")) dataGridView3.Columns["Ilosc"].Width = 70;
                if (dataGridView3.Columns.Contains("Telefon")) dataGridView3.Columns["Telefon"].Width = 110; // np.
            }
        }


        // ====== HISTORIA (Twoja istniejąca siatka: datagridWpisy) ======
        private void LoadHistoryAll()
        {
            using (var cnn = new SqlConnection(connectionString))
            using (var da = new SqlDataAdapter(@"
        SELECT 
            Dostawca,
            ContactDate,
            UserID,
            SnoozedUntil,
            Reason,
            CreatedAt
        FROM dbo.ContactHistory
        ORDER BY 
            CASE WHEN ContactDate IS NOT NULL THEN 0 ELSE 1 END,
            ContactDate DESC,
            CreatedAt  DESC,
            ContactID  DESC
    ", cnn))
            {
                var dt = new DataTable();
                da.Fill(dt);

                datagridWpisy.DataSource = dt;
                if (datagridWpisy.Columns.Contains("Dostawca"))
                {
                    datagridWpisy.Columns["Dostawca"].HeaderText = "Hodowca";
                    datagridWpisy.Columns["Dostawca"].Width = 160;
                }
                if (datagridWpisy.Columns.Contains("ContactDate"))
                {
                    datagridWpisy.Columns["ContactDate"].HeaderText = "Data kontaktu";
                    datagridWpisy.Columns["ContactDate"].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
                    datagridWpisy.Columns["ContactDate"].Width = 130;
                }
                if (datagridWpisy.Columns.Contains("UserID"))
                {
                    datagridWpisy.Columns["UserID"].HeaderText = "UserID";
                    datagridWpisy.Columns["UserID"].Width = 70;
                }
                if (datagridWpisy.Columns.Contains("SnoozedUntil"))
                {
                    datagridWpisy.Columns["SnoozedUntil"].HeaderText = "Następny kontakt";
                    datagridWpisy.Columns["SnoozedUntil"].DefaultCellStyle.Format = "yyyy-MM-dd";
                    datagridWpisy.Columns["SnoozedUntil"].Width = 120;
                }
                if (datagridWpisy.Columns.Contains("Reason"))
                {
                    datagridWpisy.Columns["Reason"].HeaderText = "Notatka";
                    datagridWpisy.Columns["Reason"].Width = 300;
                }
                if (datagridWpisy.Columns.Contains("CreatedAt"))
                {
                    datagridWpisy.Columns["CreatedAt"].HeaderText = "Wprowadzono";
                    datagridWpisy.Columns["CreatedAt"].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
                    datagridWpisy.Columns["CreatedAt"].Width = 130;
                }

                datagridWpisy.RowHeadersVisible = false;
                datagridWpisy.AllowUserToAddRows = false;
                datagridWpisy.AllowUserToDeleteRows = false;
                datagridWpisy.ReadOnly = true;
                datagridWpisy.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            }
        }



        // ====== DRZEMKA: klik przycisku (przypnij w Designerze) ======
        private void btnSnooze_Click(object sender, EventArgs e)
        {
            ApplySnoozeCore();
        }

        // Właściwa logika drzemki – JEDYNA definicja
        private void ApplySnoozeCore()
        {
            if (dataGridView3.CurrentRow == null)
            {
                MessageBox.Show("Wybierz wiersz w liście przypomnień.", "Uwaga",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (dataGridView3.CurrentRow.Cells["LP"].Value == null)
            {
                MessageBox.Show("Brak LP dla zaznaczonego wiersza.", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int lp = Convert.ToInt32(dataGridView3.CurrentRow.Cells["LP"].Value);
            string dostawca = Convert.ToString(dataGridView3.CurrentRow.Cells["Dostawca"].Value);

            // >>> NOWE: ostateczna data z datapickerOdlozenie
            DateTime until = datapickerOdlozenie.Value.Date;

            string note = txtNote.Text?.Trim();

            try
            {
                AddContactHistory(lp, dostawca, until, note);
                LoadRemindersToGrid3();   // wiersz znika do odłożonej daty
                LoadHistoryAll();         // odśwież historię
                                          // txtNote.Text = string.Empty; // opcjonalnie
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zapisu drzemki: " + ex.Message, "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            datapickerOdlozenie.Value = DateTime.Today;
            txtMonths.Text = "";
            txtNote.Text = "";
        }
        private void ApplyNoContactSnooze()
        {
            if (dataGridView3.CurrentRow == null)
            {
                MessageBox.Show("Wybierz wiersz w liście przypomnień.", "Uwaga",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (dataGridView3.CurrentRow.Cells["LP"].Value == null)
            {
                MessageBox.Show("Brak LP dla zaznaczonego wiersza.", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int lp = Convert.ToInt32(dataGridView3.CurrentRow.Cells["LP"].Value);
            string dostawca = Convert.ToString(dataGridView3.CurrentRow.Cells["Dostawca"].Value);

            // Ustawiamy termin na dziś + 3 dni
            DateTime until = DateTime.Today.AddDays(3);

            // Automatyczna notatka
            string note = "Brak kontaktu";

            try
            {
                AddContactHistory(lp, dostawca, until, note);
                LoadRemindersToGrid3();   // odświeżenie listy
                LoadHistoryAll();         // odświeżenie historii
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zapisu drzemki: " + ex.Message, "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // Reset UI
            datapickerOdlozenie.Value = DateTime.Today;
            txtMonths.Text = "";
            txtNote.Text = "";
        }



        // ====== GRID 4 – jak u Ciebie ======
        private void DisplayDataInDataGridView4()
        {
            string query = @"
        WITH FilteredData AS (
            SELECT *
            FROM dbo.HarmonogramDostaw
            WHERE LpW IS NULL
        ),
        SuppliersWithOnlyNullLpW AS (
            SELECT Dostawca
            FROM dbo.HarmonogramDostaw
            GROUP BY Dostawca
            HAVING COUNT(*) = SUM(CASE WHEN LpW IS NULL THEN 1 ELSE 0 END)
        ),
        RankedData AS (
            SELECT 
                fd.Lp,
                fd.DataOdbioru,
                fd.Dostawca,
                fd.SztukiDek,
                fd.WagaDek,
                fd.TypCeny,
                fd.Bufor,
                ROW_NUMBER() OVER (PARTITION BY fd.Dostawca ORDER BY fd.DataOdbioru) AS RowNum
            FROM FilteredData fd
            INNER JOIN SuppliersWithOnlyNullLpW swn ON fd.Dostawca = swn.Dostawca
        )
        SELECT Lp, DataOdbioru, Dostawca, SztukiDek, WagaDek, TypCeny, Bufor
        FROM RankedData
        WHERE RowNum = 1
        ORDER BY DataOdbioru DESC;";

            using (var connection = new SqlConnection(connectionString))
            using (var adapter = new SqlDataAdapter(query, connection))
            {
                var table = new DataTable();
                adapter.Fill(table);
                dataGridView4.DataSource = table;

                dataGridView4.Columns["Lp"].Width = 40;
                dataGridView4.Columns["DataOdbioru"].Width = 65;
                dataGridView4.Columns["Dostawca"].Width = 110;
                dataGridView4.Columns["SztukiDek"].Width = 40;
                dataGridView4.Columns["SztukiDek"].HeaderText = "Sztuki";
                dataGridView4.Columns["WagaDek"].Width = 40;
                dataGridView4.Columns["WagaDek"].HeaderText = "Waga";
                dataGridView4.Columns["TypCeny"].Width = 80;
                dataGridView4.Columns["Bufor"].Width = 75;
            }
        }

        // ====== reszta – jak u Ciebie (checkboxy, usuwanie, itd.) ======
        private void dataGridView1_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentCell is DataGridViewCheckBoxCell)
                dataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == dataGridView1.Columns["isCheck"].Index)
            {
                if (isUserInitiatedChange)
                {
                    bool isChecked = (bool)dataGridView1.Rows[e.RowIndex].Cells["isCheck"].Value;
                    int lp = Convert.ToInt32(dataGridView1.Rows[e.RowIndex].Cells["LP"].Value);
                    string userComment = ShowInputDialog("Podaj komentarz:");

                    UpdateIsCheckInDatabase(lp, isChecked, userComment);

                    DisplayDataInDataGridView();
                    LoadRemindersToGrid3();
                    LoadHistoryAll();
                }
            }
        }

        private string ShowInputDialog(string text)
        {
            var prompt = new Form()
            {
                Width = 500,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "Notatka",
                StartPosition = FormStartPosition.CenterScreen
            };
            var textLabel = new Label() { Left = 50, Top = 20, Text = text, Width = 380 };
            var inputBox = new TextBox() { Left = 50, Top = 50, Width = 400 };
            var confirmation = new Button() { Text = "OK", Left = 350, Width = 100, Top = 70, DialogResult = DialogResult.OK };
            confirmation.Click += (sender, e) => { prompt.Close(); };
            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(inputBox);
            prompt.AcceptButton = confirmation;

            return prompt.ShowDialog() == DialogResult.OK ? inputBox.Text : "";
        }

        private void UpdateIsCheckInDatabase(int lp, bool isChecked, string comment)
        {
            string query = "UPDATE dbo.WstawieniaKurczakow SET isCheck = @isCheck, CheckCom = @comment WHERE LP = @lp";

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@isCheck", isChecked ? 1 : 0);
                command.Parameters.AddWithValue("@comment", (object)comment ?? DBNull.Value);
                command.Parameters.AddWithValue("@lp", lp);

                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            ZaznaczLpPokazDostawy(dataGridView1, dataGridView2, sumaSztuk, connectionString);
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
            if (rowIndex < 0) return;

            var dataWstawieniaCell = dataGridView1.Rows[rowIndex].Cells["Data"];
            var dostawcaCell = dataGridView1.Rows[rowIndex].Cells["Dostawca"];
            var isCheckCell = dataGridView1.Rows[rowIndex].Cells["isCheck"];

            if (dataWstawieniaCell?.Value == null || dostawcaCell?.Value == null) return;

            if (DateTime.TryParse(Convert.ToString(dataWstawieniaCell.Value), out DateTime dataWstawienia)
                && !(isCheckCell?.Value is bool b && b))
            {
                TimeSpan roznicaDni = DateTime.Now.Date - dataWstawienia.Date;
                if (roznicaDni.Days >= 35)
                {
                    DateTime maxDataWstawienia = ZnajdzMaxDateDlaDostawcy(Convert.ToString(dostawcaCell.Value));
                    if (dataWstawienia == maxDataWstawienia)
                        dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.Red;
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
                    if (DateTime.TryParse(Convert.ToString(row.Cells["Data"].Value), out DateTime dataWstawienia))
                        if (dataWstawienia > maxDataWstawienia) maxDataWstawienia = dataWstawienia;
                }
            }
            return maxDataWstawienia;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            string filterText = textBox1.Text.Trim().ToLower();
            if (dataGridView1.DataSource is DataTable dataTable)
                dataTable.DefaultView.RowFilter = $"Dostawca LIKE '%{filterText}%'";
        }

        private void button3_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("Czy na pewno chcesz usunąć wybrany wiersz oraz powiązane z nim dane?",
                                                  "Potwierdzenie usunięcia",
                                                  MessageBoxButtons.YesNo,
                                                  MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                string deleteQuery1 = "DELETE FROM dbo.HarmonogramDostaw WHERE LpW = @LpW";
                string deleteQuery2 = "DELETE FROM dbo.WstawieniaKurczakow WHERE Lp = @LpW";

                using (var cnn = new SqlConnection(connectionString))
                {
                    try
                    {
                        cnn.Open();

                        using (var cmd1 = new SqlCommand(deleteQuery1, cnn))
                        {
                            cmd1.Parameters.AddWithValue("@LpW", lpDostawa);
                            cmd1.ExecuteNonQuery();
                        }

                        using (var cmd2 = new SqlCommand(deleteQuery2, cnn))
                        {
                            cmd2.Parameters.AddWithValue("@LpW", lpDostawa);
                            cmd2.ExecuteNonQuery();
                        }

                        MessageBox.Show("Wiersz oraz powiązane z nim dane zostały usunięte.", "OK",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);

                        DisplayDataInDataGridView();
                        LoadRemindersToGrid3();
                        LoadHistoryAll();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Wystąpił błąd: " + ex.Message, "Błąd",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Operacja usunięcia została anulowana.",
                                "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Wstawienie wstawienie = new Wstawienie();
            wstawienie.UserID = App.UserID;
            wstawienie.WypelnijStartowo();
            wstawienie.Show();

            DisplayDataInDataGridView();
            LoadRemindersToGrid3();
            LoadHistoryAll();
        }

        private void dataGridView3_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            ZaznaczLpPokazDostawy(dataGridView3, dataGridView2, sumaSztuk, connectionString);
        }

        private void dataGridView3_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            Wstawienie wstawienie = new Wstawienie();
            wstawienie.UserID = App.UserID;

            int intValue = string.IsNullOrEmpty(lpDostawa) ? 0 : int.Parse(lpDostawa);
            string Dostawca = zapytaniasql.PobierzInformacjeZBazyDanychHarmonogram<string>(intValue, "dbo.WstawieniaKurczakow", "Dostawca");
            int sztWstawione = zapytaniasql.PobierzInformacjeZBazyDanychHarmonogram<int>(intValue, "dbo.WstawieniaKurczakow", "IloscWstawienia");

            wstawienie.sztWstawienia = sztWstawione;
            wstawienie.dostawca = Dostawca;
            wstawienie.UzupelnijBraki();
            wstawienie.WypelnijStartowo();
            wstawienie.Show();

            DisplayDataInDataGridView();
            LoadRemindersToGrid3();
            LoadHistoryAll();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Wstawienie wstawienie = new Wstawienie();
            wstawienie.UserID = App.UserID;

            int intValue = string.IsNullOrEmpty(lpDostawa) ? 0 : int.Parse(lpDostawa);
            int lpWstawienia = zapytaniasql.PobierzInformacjeZBazyDanychHarmonogram<int>(intValue, "dbo.WstawieniaKurczakow", "Lp");
            string Dostawca = zapytaniasql.PobierzInformacjeZBazyDanychHarmonogram<string>(intValue, "dbo.WstawieniaKurczakow", "Dostawca");
            DateTime DataWstawienia = zapytaniasql.PobierzInformacjeZBazyDanychHarmonogram<DateTime>(intValue, "dbo.WstawieniaKurczakow", "DataWstawienia");
            int sztWstawione = zapytaniasql.PobierzInformacjeZBazyDanychHarmonogram<int>(intValue, "dbo.WstawieniaKurczakow", "IloscWstawienia");

            wstawienie.sztWstawienia = sztWstawione;
            wstawienie.dostawca = Dostawca;
            wstawienie.LpWstawienia = lpWstawienia;
            wstawienie.DataWstawienia = DataWstawienia;

            wstawienie.MetodaModyfiacji();
            wstawienie.Show();

            DisplayDataInDataGridView();
            LoadRemindersToGrid3();
            LoadHistoryAll();
        }

        private void ZaznaczLpPokazDostawy(DataGridView sourceGrid, DataGridView targetGrid, TextBox sumaSztuk, string connectionString)
        {
            if (sourceGrid.SelectedCells.Count == 0) return;

            int selectedRowIndex = sourceGrid.SelectedCells[0].RowIndex;
            int selectedColumnIndex = sourceGrid.SelectedCells[0].ColumnIndex;

            if (selectedRowIndex >= 0 && selectedColumnIndex >= 0)
            {
                object selectedCellValue = sourceGrid.Rows[selectedRowIndex].Cells["Lp"].Value;
                lpDostawa = selectedCellValue != null ? selectedCellValue.ToString() : "0";
                if (selectedCellValue == null || selectedCellValue == DBNull.Value) return;

                string query = "SELECT LP, Dostawca, DataOdbioru, SztukiDek, bufor " +
                               "FROM dbo.HarmonogramDostaw " +
                               "WHERE LpW = @NumerWstawienia ORDER BY DataOdbioru ASC";

                double sumaSztukWstawienia = 0;

                targetGrid.Rows.Clear();
                targetGrid.Columns.Clear();

                targetGrid.Columns.Add("DataOdbioru", "Data Odbioru");
                targetGrid.Columns.Add("SztukiDek", "Sztuki Dek");
                targetGrid.Columns.Add("Bufor", "Bufor");
                targetGrid.Columns.Add("Dostawca", "Dostawca");

                using (var cnn = new SqlConnection(connectionString))
                {
                    cnn.Open();

                    using (var command = new SqlCommand(query, cnn))
                    {
                        command.Parameters.AddWithValue("@NumerWstawienia", selectedCellValue);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string dataOdbioru = reader["DataOdbioru"] != DBNull.Value
                                    ? Convert.ToDateTime(reader["DataOdbioru"]).ToString("yyyy-MM-dd dddd")
                                    : string.Empty;

                                object sztukiDekValue = reader["SztukiDek"];
                                object buforValue = reader["bufor"];
                                string dostawca = reader["Dostawca"] != DBNull.Value ? reader["Dostawca"].ToString() : string.Empty;

                                if (sztukiDekValue != DBNull.Value)
                                    sumaSztukWstawienia += Convert.ToDouble(sztukiDekValue);

                                string formattedSztukiDek = sztukiDekValue != DBNull.Value
                                    ? string.Format("{0:#,0}", sztukiDekValue)
                                    : "0";

                                string formattedBufor = buforValue != DBNull.Value
                                    ? buforValue.ToString()
                                    : string.Empty;

                                targetGrid.Rows.Add(dataOdbioru, formattedSztukiDek, formattedBufor, dostawca);
                            }
                        }
                    }
                }

                sumaSztuk.Text = string.Format("{0:#,0}", sumaSztukWstawienia);
                targetGrid.Rows.Add("Suma:", string.Format("{0:#,0}", sumaSztukWstawienia), string.Empty, string.Empty);

                targetGrid.Rows[targetGrid.Rows.Count - 1].Cells[0].Style.Alignment = DataGridViewContentAlignment.MiddleRight;
                targetGrid.Rows[targetGrid.Rows.Count - 1].Cells[1].Style.Alignment = DataGridViewContentAlignment.MiddleRight;

                targetGrid.Rows[targetGrid.Rows.Count - 1].Cells[0].Style.Font = new Font(targetGrid.DefaultCellStyle.Font, FontStyle.Bold);
                targetGrid.Rows[targetGrid.Rows.Count - 1].Cells[1].Style.Font = new Font(targetGrid.DefaultCellStyle.Font, FontStyle.Bold);

                targetGrid.Columns["DataOdbioru"].Width = 120;
                targetGrid.Columns["SztukiDek"].Width = 80;
                targetGrid.Columns["Bufor"].Width = 100;
                targetGrid.Columns["Dostawca"].Width = 140;
            }
        }

        private int? GetSelectedLp(DataGridView grid)
        {
            if (grid.CurrentRow == null) return null;
            var val = grid.CurrentRow.Cells["LP"]?.Value;
            if (val == null || val == DBNull.Value) return null;
            return Convert.ToInt32(val);
        }

        private void AddContactHistory(int lpWstawienia, string dostawca, DateTime? snoozedUntil, string reason)
        {
            using (var cnn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand("dbo.AddContactHistory", cnn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@LpWstawienia", lpWstawienia);
                cmd.Parameters.AddWithValue("@Dostawca", (object)dostawca ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@UserID", App.UserID);
                cmd.Parameters.AddWithValue("@SnoozedUntil", (object?)snoozedUntil ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Reason", (object?)reason ?? DBNull.Value);

                cnn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // jeżeli masz w Designerze podpięty event:
        private void txtMonths_TextChanged(object sender, EventArgs e) { }

        private void txtMonths_TextChanged_1(object sender, EventArgs e)
        {
            // Gdy użytkownik wpisze liczbę miesięcy, ustawiamy datepicker: dziś + X miesięcy.
            if (int.TryParse((txtMonths.Text ?? "").Trim(), out int months))
            {
                if (months < 0) months = 0;
                if (months > 60) months = 60; // zabezpieczenie

                DateTime target = DateTime.Today.AddMonths(months);

                // DateTimePicker może mieć ograniczenia – w razie czego łapiemy wyjątek
                try
                {
                    datapickerOdlozenie.Value = target;
                }
                catch
                {
                    datapickerOdlozenie.Value = DateTime.Today;
                }
            }
        }

        private void btnNoContact_Click(object sender, EventArgs e)
        {
            ApplyNoContactSnooze();
        }
    }
}
