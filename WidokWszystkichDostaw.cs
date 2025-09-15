using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class WidokWszystkichDostaw : Form
    {
        private static readonly string connectionPermission = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private DataTable originalData;
        private bool isLoading = false;
        private Timer filterTimer;

        // Kontrolki UI
        private DateTimePicker dateFromPicker;
        private DateTimePicker dateToPicker;
        private ComboBox comboBoxDostawca;
        private ComboBox comboBoxStatus;

        public string TextBoxValue { get; set; }

        public WidokWszystkichDostaw()
        {
            InitializeComponent();
            InitializeUI();
            LoadDataInitial(); // Specjalna metoda dla pierwszego ładowania
        }

        private void InitializeUI()
        {
            this.Text = "System Zarządzania Dostawami Drobiu";
            this.WindowState = FormWindowState.Maximized;

            // Panel górny z filtrami
            Panel topPanel = new Panel();
            topPanel.Height = 100;
            topPanel.Dock = DockStyle.Top;
            topPanel.BackColor = Color.White;

            // Grupa filtrów
            GroupBox filterGroup = new GroupBox();
            filterGroup.Text = "Filtry";
            filterGroup.Dock = DockStyle.Fill;
            filterGroup.Padding = new Padding(10);

            // Szukaj - z natychmiastowym filtrowaniem
            Label lblSearch = new Label();
            lblSearch.Text = "Szukaj:";
            lblSearch.Location = new Point(10, 25);
            lblSearch.Width = 50;
            filterGroup.Controls.Add(lblSearch);

            textBox1.Location = new Point(65, 22);
            textBox1.Width = 150;
            // Filtrowanie na żywo po wpisaniu
            textBox1.TextChanged += TextBox1_TextChanged;
            filterGroup.Controls.Add(textBox1);

            // Data od - USTAWIONA NA 2023
            Label lblDateFrom = new Label();
            lblDateFrom.Text = "Data od:";
            lblDateFrom.Location = new Point(230, 25);
            lblDateFrom.Width = 55;
            filterGroup.Controls.Add(lblDateFrom);

            dateFromPicker = new DateTimePicker();
            dateFromPicker.Location = new Point(290, 22);
            dateFromPicker.Width = 100;
            dateFromPicker.Format = DateTimePickerFormat.Short;
            dateFromPicker.Value = new DateTime(2023, 1, 1);
            dateFromPicker.ValueChanged += (s, e) => { if (!isLoading) ApplyFilters(); };
            filterGroup.Controls.Add(dateFromPicker);

            // Data do
            Label lblDateTo = new Label();
            lblDateTo.Text = "Data do:";
            lblDateTo.Location = new Point(400, 25);
            lblDateTo.Width = 55;
            filterGroup.Controls.Add(lblDateTo);

            dateToPicker = new DateTimePicker();
            dateToPicker.Location = new Point(460, 22);
            dateToPicker.Width = 100;
            dateToPicker.Format = DateTimePickerFormat.Short;
            dateToPicker.Value = DateTime.Now;
            dateToPicker.ValueChanged += (s, e) => { if (!isLoading) ApplyFilters(); };
            filterGroup.Controls.Add(dateToPicker);

            // Dostawca
            Label lblDostawca = new Label();
            lblDostawca.Text = "Dostawca:";
            lblDostawca.Location = new Point(580, 25);
            lblDostawca.Width = 65;
            filterGroup.Controls.Add(lblDostawca);

            comboBoxDostawca = new ComboBox();
            comboBoxDostawca.Location = new Point(650, 22);
            comboBoxDostawca.Width = 200;
            comboBoxDostawca.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxDostawca.SelectedIndexChanged += (s, e) => { if (!isLoading) ApplyFilters(); };
            filterGroup.Controls.Add(comboBoxDostawca);

            // Status
            Label lblStatus = new Label();
            lblStatus.Text = "Status:";
            lblStatus.Location = new Point(870, 25);
            lblStatus.Width = 50;
            filterGroup.Controls.Add(lblStatus);

            comboBoxStatus = new ComboBox();
            comboBoxStatus.Location = new Point(925, 22);
            comboBoxStatus.Width = 120;
            comboBoxStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxStatus.Items.AddRange(new[] { "Wszystkie", "Potwierdzony", "Anulowany", "Sprzedany", "B.Kontr.", "B.Wolny.", "Do wykupienia" });
            comboBoxStatus.SelectedIndex = 0;
            comboBoxStatus.SelectedIndexChanged += (s, e) => { if (!isLoading) ApplyFilters(); };
            filterGroup.Controls.Add(comboBoxStatus);

            // Przyciski
            Button btnClear = new Button();
            btnClear.Text = "Wyczyść";
            btnClear.Location = new Point(10, 55);
            btnClear.Width = 80;
            btnClear.FlatStyle = FlatStyle.Flat;
            btnClear.BackColor = Color.Orange;
            btnClear.Click += (s, e) => ClearFilters();
            filterGroup.Controls.Add(btnClear);

            Button btnRefresh = new Button();
            btnRefresh.Text = "Odśwież";
            btnRefresh.Location = new Point(100, 55);
            btnRefresh.Width = 80;
            btnRefresh.FlatStyle = FlatStyle.Flat;
            btnRefresh.BackColor = Color.LightGreen;
            btnRefresh.Click += (s, e) => LoadData();
            filterGroup.Controls.Add(btnRefresh);

            Button btnExport = new Button();
            btnExport.Text = "Eksport";
            btnExport.Location = new Point(190, 55);
            btnExport.Width = 80;
            btnExport.FlatStyle = FlatStyle.Flat;
            btnExport.BackColor = Color.LightBlue;
            btnExport.Click += (s, e) => ExportToCSV();
            filterGroup.Controls.Add(btnExport);

            // CheckBox grupowanie
            checkBoxGroupBySupplier.Location = new Point(290, 55);
            checkBoxGroupBySupplier.Width = 160;
            checkBoxGroupBySupplier.Text = "Grupuj po dostawcy";
            checkBoxGroupBySupplier.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            checkBoxGroupBySupplier.CheckedChanged += CheckBoxGroupBySupplier_CheckedChanged;
            filterGroup.Controls.Add(checkBoxGroupBySupplier);

            // Label ze statystykami
            Label lblStats = new Label();
            lblStats.Name = "lblStats";
            lblStats.Location = new Point(460, 55);
            lblStats.Width = 600;
            lblStats.Text = "Statystyki: Ładowanie...";
            lblStats.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblStats.ForeColor = Color.DarkBlue;
            filterGroup.Controls.Add(lblStats);

            topPanel.Controls.Add(filterGroup);
            this.Controls.Add(topPanel);

            // Konfiguracja DataGridView
            ConfigureDataGridView();

            // Dodaj DataGridView do formularza
            dataGridView1.Dock = DockStyle.Fill;
            this.Controls.Add(dataGridView1);
            dataGridView1.BringToFront();

            // Status bar
            StatusStrip statusBar = new StatusStrip();
            statusBar.Items.Add(new ToolStripStatusLabel("Gotowy"));
            statusBar.Items.Add(new ToolStripStatusLabel() { Spring = true });
            statusBar.Items.Add(new ToolStripStatusLabel(""));
            this.Controls.Add(statusBar);
        }

        private void ConfigureDataGridView()
        {
            // Podstawowe ustawienia
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.ReadOnly = true;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.MultiSelect = true;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dataGridView1.RowHeadersWidth = 30;
            dataGridView1.BackgroundColor = Color.White;
            dataGridView1.BorderStyle = BorderStyle.None;
            dataGridView1.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);

            // Styl nagłówków
            dataGridView1.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 152, 219);
            dataGridView1.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dataGridView1.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dataGridView1.EnableHeadersVisualStyles = false;

            // Włącz double buffering dla płynności
            EnableDoubleBuffering();

            // Dodaj obsługę sortowania po kliknięciu nagłówka
            dataGridView1.ColumnHeaderMouseClick += DataGridView1_ColumnHeaderMouseClick;
        }

        private void EnableDoubleBuffering()
        {
            Type dgvType = dataGridView1.GetType();
            System.Reflection.PropertyInfo pi = dgvType.GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (pi != null)
                pi.SetValue(dataGridView1, true, null);
        }

        // Specjalna metoda dla pierwszego ładowania - bez żadnych komunikatów
        private void LoadDataInitial()
        {
            try
            {
                isLoading = true;
                this.Cursor = Cursors.WaitCursor;

                using (SqlConnection connection = new SqlConnection(connectionPermission))
                {
                    string query = @"
                        SELECT 
                            LP, DostawcaID, DataOdbioru, Dostawca, Auta, 
                            SztukiDek, WagaDek, SztSzuflada, TypUmowy, TypCeny, 
                            Cena, PaszaPisklak, Bufor, UWAGI, LpW, LpP1, LpP2,
                            Utworzone, Wysłane, Otrzymane, PotwWaga, KtoWaga, 
                            KiedyWaga, PotwSztuki, KtoSztuki, KiedySztuki, 
                            PotwCena, Dodatek, Kurnik, KmK, KmH, Ubiorka, 
                            Ubytek, DataUtw, KtoStwo, DataMod, KtoMod, 
                            CzyOdznaczoneWstawienie
                        FROM [LibraNet].[dbo].[HarmonogramDostaw] 
                        ORDER BY LP DESC";

                    SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                    DataTable table = new DataTable();
                    adapter.Fill(table);

                    originalData = table.Copy();

                    dataGridView1.DataSource = table;
                    ConfigureColumns();
                    LoadSuppliers();

                    // Zastosuj kolorowanie po małym opóźnieniu
                    Timer colorTimer = new Timer();
                    colorTimer.Interval = 100;
                    colorTimer.Tick += (s, e) =>
                    {
                        colorTimer.Stop();
                        ColorRows();
                        UpdateStatistics(table);
                    };
                    colorTimer.Start();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isLoading = false;
                this.Cursor = Cursors.Default;
            }
        }

        private void LoadData()
        {
            try
            {
                isLoading = true;
                this.Cursor = Cursors.WaitCursor;

                using (SqlConnection connection = new SqlConnection(connectionPermission))
                {
                    string query = @"
                        SELECT 
                            LP, DostawcaID, DataOdbioru, Dostawca, Auta, 
                            SztukiDek, WagaDek, SztSzuflada, TypUmowy, TypCeny, 
                            Cena, PaszaPisklak, Bufor, UWAGI, LpW, LpP1, LpP2,
                            Utworzone, Wysłane, Otrzymane, PotwWaga, KtoWaga, 
                            KiedyWaga, PotwSztuki, KtoSztuki, KiedySztuki, 
                            PotwCena, Dodatek, Kurnik, KmK, KmH, Ubiorka, 
                            Ubytek, DataUtw, KtoStwo, DataMod, KtoMod, 
                            CzyOdznaczoneWstawienie
                        FROM [LibraNet].[dbo].[HarmonogramDostaw] 
                        ORDER BY LP DESC";

                    SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                    DataTable table = new DataTable();
                    adapter.Fill(table);

                    originalData = table.Copy();

                    dataGridView1.DataSource = table;
                    ConfigureColumns();
                    ColorRows();
                    LoadSuppliers();
                    UpdateStatistics(table);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isLoading = false;
                this.Cursor = Cursors.Default;
            }
        }

        private void ConfigureColumns()
        {
            if (dataGridView1.Columns.Count == 0) return;

            var configs = new Dictionary<string, (string header, int width)>
            {
                {"Lp", ("Lp", 50)},
                {"DostawcaID", ("ID", 50)},
                {"DataOdbioru", ("Data Odb.", 85)},
                {"Dostawca", ("Dostawca", 200)},
                {"Auta", ("Auta", 50)},
                {"SztukiDek", ("Szt.Dek", 70)},
                {"WagaDek", ("Waga", 70)},
                {"SztSzuflada", ("Szt.Sz", 60)},
                {"TypUmowy", ("Umowa", 70)},
                {"TypCeny", ("Typ Ceny", 70)},
                {"Cena", ("Cena", 70)},
                {"Bufor", ("Status", 100)},
                {"UWAGI", ("Uwagi", 150)},
                {"Utworzone", ("Utw", 40)},
                {"Wysłane", ("Wys", 40)},
                {"Otrzymane", ("Otrz", 40)},
                {"PotwWaga", ("P.Waga", 60)},
                {"PotwSztuki", ("P.Szt", 60)},
                {"PotwCena", ("P.Cena", 60)},
                {"Dodatek", ("Dod.", 60)},
                {"Kurnik", ("Kurnik", 100)},
                {"KmK", ("KmK", 60)},
                {"KmH", ("KmH", 60)},
                {"Ubytek", ("Ubytek %", 70)}
            };

            foreach (var config in configs)
            {
                if (dataGridView1.Columns.Contains(config.Key))
                {
                    dataGridView1.Columns[config.Key].HeaderText = config.Value.header;
                    dataGridView1.Columns[config.Key].Width = config.Value.width;
                }
            }

            // Ukryj niektóre kolumny
            string[] hiddenColumns = { "PaszaPisklak", "LpW", "LpP1", "LpP2", "KtoWaga",
                "KiedyWaga", "KtoSztuki", "KiedySztuki", "Ubiorka", "DataUtw",
                "KtoStwo", "DataMod", "KtoMod", "CzyOdznaczoneWstawienie" };

            foreach (string col in hiddenColumns)
            {
                if (dataGridView1.Columns.Contains(col))
                    dataGridView1.Columns[col].Visible = false;
            }
        }

        private void ColorRows()
        {
            if (!dataGridView1.Columns.Contains("Bufor")) return;

            dataGridView1.SuspendLayout();

            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (row.Cells["Bufor"].Value == null) continue;

                string status = row.Cells["Bufor"].Value.ToString();
                Color bgColor = Color.White;
                Color fgColor = Color.Black;

                switch (status)
                {
                    case "Potwierdzony":
                        bgColor = Color.FromArgb(198, 239, 206);
                        break;
                    case "Anulowany":
                        bgColor = Color.FromArgb(255, 199, 199);
                        break;
                    case "Sprzedany":
                        bgColor = Color.FromArgb(199, 223, 255);
                        break;
                    case "B.Kontr.":
                        bgColor = Color.FromArgb(147, 112, 219);
                        fgColor = Color.White;
                        break;
                    case "B.Wolny.":
                        bgColor = Color.FromArgb(255, 253, 184);
                        break;
                    case "Do wykupienia":
                        bgColor = Color.FromArgb(245, 245, 245);
                        break;
                }

                row.DefaultCellStyle.BackColor = bgColor;
                row.DefaultCellStyle.ForeColor = fgColor;
            }

            dataGridView1.ResumeLayout();
        }

        private void LoadSuppliers()
        {
            if (originalData == null) return;

            isLoading = true;

            var suppliers = originalData.AsEnumerable()
                .Select(r => r.Field<string>("Dostawca"))
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            comboBoxDostawca.Items.Clear();
            comboBoxDostawca.Items.Add("Wszyscy");
            comboBoxDostawca.Items.AddRange(suppliers.ToArray());
            comboBoxDostawca.SelectedIndex = 0;

            isLoading = false;
        }

        private void TextBox1_TextChanged(object sender, EventArgs e)
        {
            if (isLoading) return;

            // Anuluj poprzedni timer
            if (filterTimer != null)
            {
                filterTimer.Stop();
                filterTimer.Dispose();
            }

            // Ustaw nowy timer - czeka 300ms po zakończeniu pisania
            filterTimer = new Timer();
            filterTimer.Interval = 300;
            filterTimer.Tick += (s, args) =>
            {
                filterTimer.Stop();
                ApplyFilters();
            };
            filterTimer.Start();
        }

        private void ApplyFilters()
        {
            if (originalData == null || isLoading) return;

            DataView dv = new DataView(originalData);
            List<string> filters = new List<string>();

            // Filtr tekstowy
            if (!string.IsNullOrWhiteSpace(textBox1.Text))
            {
                string searchText = textBox1.Text.Trim().Replace("'", "''");
                filters.Add($"Dostawca LIKE '%{searchText}%'");
            }

            // Filtr dat
            filters.Add($"DataOdbioru >= #{dateFromPicker.Value.Date:yyyy-MM-dd}#");
            filters.Add($"DataOdbioru <= #{dateToPicker.Value.Date:yyyy-MM-dd}#");

            // Filtr dostawcy
            if (comboBoxDostawca.SelectedIndex > 0 && comboBoxDostawca.SelectedItem != null)
            {
                string supplier = comboBoxDostawca.SelectedItem.ToString().Replace("'", "''");
                filters.Add($"Dostawca = '{supplier}'");
            }

            // Filtr statusu
            if (comboBoxStatus.SelectedIndex > 0 && comboBoxStatus.SelectedItem != null)
            {
                string status = comboBoxStatus.SelectedItem.ToString().Replace("'", "''");
                filters.Add($"Bufor = '{status}'");
            }

            dv.RowFilter = string.Join(" AND ", filters);
            DataTable filteredTable = dv.ToTable();

            dataGridView1.DataSource = filteredTable;
            ColorRows();
            UpdateStatistics(filteredTable);
        }

        private void ClearFilters()
        {
            isLoading = true;

            textBox1.Clear();
            dateFromPicker.Value = new DateTime(2023, 1, 1);
            dateToPicker.Value = DateTime.Now;
            if (comboBoxDostawca.Items.Count > 0)
                comboBoxDostawca.SelectedIndex = 0;
            if (comboBoxStatus.Items.Count > 0)
                comboBoxStatus.SelectedIndex = 0;

            isLoading = false;

            dataGridView1.DataSource = originalData;
            ColorRows();
            UpdateStatistics(originalData);
        }

        private void UpdateStatistics(DataTable data)
        {
            if (data == null) return;

            int totalRows = data.Rows.Count;
            decimal totalWeight = 0;
            int totalPieces = 0;
            int confirmedCount = 0;

            foreach (DataRow row in data.Rows)
            {
                if (row["WagaDek"] != DBNull.Value)
                    totalWeight += Convert.ToDecimal(row["WagaDek"]);

                if (row["SztukiDek"] != DBNull.Value)
                    totalPieces += Convert.ToInt32(row["SztukiDek"]);

                if (row["Bufor"]?.ToString() == "Potwierdzony")
                    confirmedCount++;
            }

            // Znajdź label ze statystykami
            Control[] controls = this.Controls.Find("lblStats", true);
            if (controls.Length > 0 && controls[0] is Label lblStats)
            {
                lblStats.Text = $"Rekordy: {totalRows} | Waga: {totalWeight:N0} kg | " +
                               $"Sztuki: {totalPieces:N0} | Potwierdzone: {confirmedCount}";
            }

            UpdateStatusBar($"Wyświetlono {totalRows} z {originalData?.Rows.Count ?? 0} rekordów");
        }

        private void UpdateStatusBar(string message)
        {
            foreach (Control control in this.Controls)
            {
                if (control is StatusStrip statusBar)
                {
                    if (statusBar.Items.Count > 0)
                    {
                        statusBar.Items[0].Text = message;
                    }
                    break;
                }
            }
        }

        private void CheckBoxGroupBySupplier_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxGroupBySupplier.Checked)
            {
                if (originalData == null) return;

                var grouped = originalData.AsEnumerable()
                    .Where(r => r.Field<string>("Dostawca") != null)
                    .GroupBy(r => r.Field<string>("Dostawca"))
                    .Select(g => new
                    {
                        Dostawca = g.Key,
                        IloscDostaw = g.Count(),
                        SumaWagi = g.Sum(r => r.Field<decimal?>("WagaDek") ?? 0),
                        SumaSztuk = g.Sum(r => r.Field<int?>("SztukiDek") ?? 0),
                        SredniaCena = g.Average(r => r.Field<decimal?>("Cena") ?? 0),
                        Potwierdzone = g.Count(r => r.Field<string>("Bufor") == "Potwierdzony"),
                        Anulowane = g.Count(r => r.Field<string>("Bufor") == "Anulowany")
                    })
                    .OrderByDescending(x => x.SumaWagi);

                DataTable groupedTable = new DataTable();
                groupedTable.Columns.Add("Dostawca", typeof(string));
                groupedTable.Columns.Add("Ilość Dostaw", typeof(int));
                groupedTable.Columns.Add("Suma Wagi (kg)", typeof(decimal));
                groupedTable.Columns.Add("Suma Sztuk", typeof(int));
                groupedTable.Columns.Add("Średnia Cena", typeof(decimal));
                groupedTable.Columns.Add("Potwierdzone", typeof(int));
                groupedTable.Columns.Add("Anulowane", typeof(int));

                foreach (var item in grouped)
                {
                    groupedTable.Rows.Add(item.Dostawca, item.IloscDostaw,
                        item.SumaWagi, item.SumaSztuk, Math.Round(item.SredniaCena, 2),
                        item.Potwierdzone, item.Anulowane);
                }

                dataGridView1.DataSource = groupedTable;
            }
            else
            {
                dataGridView1.DataSource = originalData;
                ConfigureColumns();
                ColorRows();
            }
        }

        private void DataGridView1_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            string columnName = dataGridView1.Columns[e.ColumnIndex].Name;
            DataTable dt = dataGridView1.DataSource as DataTable;
            if (dt != null)
            {
                string currentSort = dt.DefaultView.Sort;
                string newSort = columnName + " ASC";

                if (currentSort.StartsWith(columnName + " ASC"))
                    newSort = columnName + " DESC";

                dt.DefaultView.Sort = newSort;
                dataGridView1.DataSource = dt.DefaultView.ToTable();
                ColorRows();
            }
        }

        private void ExportToCSV()
        {
            try
            {
                SaveFileDialog saveDialog = new SaveFileDialog();
                saveDialog.Filter = "CSV Files (*.csv)|*.csv";
                saveDialog.FileName = $"Dostawy_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    StringBuilder csv = new StringBuilder();

                    // Nagłówki
                    for (int i = 0; i < dataGridView1.Columns.Count; i++)
                    {
                        if (dataGridView1.Columns[i].Visible)
                        {
                            csv.Append(dataGridView1.Columns[i].HeaderText);
                            if (i < dataGridView1.Columns.Count - 1)
                                csv.Append(";");
                        }
                    }
                    csv.AppendLine();

                    // Dane
                    foreach (DataGridViewRow row in dataGridView1.Rows)
                    {
                        for (int i = 0; i < dataGridView1.Columns.Count; i++)
                        {
                            if (dataGridView1.Columns[i].Visible)
                            {
                                csv.Append(row.Cells[i].Value?.ToString() ?? "");
                                if (i < dataGridView1.Columns.Count - 1)
                                    csv.Append(";");
                            }
                        }
                        csv.AppendLine();
                    }

                    File.WriteAllText(saveDialog.FileName, csv.ToString(), Encoding.UTF8);
                    UpdateStatusBar("Eksport zakończony pomyślnie!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd eksportu: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void SetTextBoxValue()
        {
            textBox1.Text = TextBoxValue;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            // Pusta - obsługa w TextBox1_TextChanged
        }
    }
}