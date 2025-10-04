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
        private ComboBox comboBoxDostawca;
        private ComboBox comboBoxStatus;

        public string TextBoxValue { get; set; }

        public WidokWszystkichDostaw()
        {
            InitializeComponent();
            InitializeUI();
            LoadDataInitial();
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
            textBox1.Width = 200;
            textBox1.TextChanged += TextBox1_TextChanged;
            filterGroup.Controls.Add(textBox1);

            // Dostawca
            Label lblDostawca = new Label();
            lblDostawca.Text = "Dostawca:";
            lblDostawca.Location = new Point(280, 25);
            lblDostawca.Width = 65;
            filterGroup.Controls.Add(lblDostawca);

            comboBoxDostawca = new ComboBox();
            comboBoxDostawca.Location = new Point(350, 22);
            comboBoxDostawca.Width = 250;
            comboBoxDostawca.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxDostawca.SelectedIndexChanged += (s, e) => { if (!isLoading) ApplyFilters(); };
            filterGroup.Controls.Add(comboBoxDostawca);

            // Status
            Label lblStatus = new Label();
            lblStatus.Text = "Status:";
            lblStatus.Location = new Point(620, 25);
            lblStatus.Width = 50;
            filterGroup.Controls.Add(lblStatus);

            comboBoxStatus = new ComboBox();
            comboBoxStatus.Location = new Point(675, 22);
            comboBoxStatus.Width = 150;
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

            Button btnAnalysis = new Button();
            btnAnalysis.Text = "Analiza K/W";
            btnAnalysis.Location = new Point(280, 55);
            btnAnalysis.Width = 90;
            btnAnalysis.FlatStyle = FlatStyle.Flat;
            btnAnalysis.BackColor = Color.FromArgb(255, 230, 150);
            btnAnalysis.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnAnalysis.Click += (s, e) => ShowContractAnalysis();
            filterGroup.Controls.Add(btnAnalysis);

            // CheckBox grupowanie
            checkBoxGroupBySupplier.Location = new Point(380, 55);
            checkBoxGroupBySupplier.Width = 160;
            checkBoxGroupBySupplier.Text = "Grupuj po dostawcy";
            checkBoxGroupBySupplier.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            checkBoxGroupBySupplier.CheckedChanged += CheckBoxGroupBySupplier_CheckedChanged;
            filterGroup.Controls.Add(checkBoxGroupBySupplier);

            // Label ze statystykami
            Label lblStats = new Label();
            lblStats.Name = "lblStats";
            lblStats.Location = new Point(550, 55);
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
                            HD.LP, 
                            HD.DostawcaID, 
                            HD.DataOdbioru, 
                            HD.Dostawca, 
                            HD.Auta, 
                            HD.SztukiDek, 
                            HD.WagaDek, 
                            HD.SztSzuflada, 
                            HD.TypUmowy, 
                            HD.TypCeny, 
                            HD.Cena, 
                            HD.PaszaPisklak, 
                            HD.Bufor, 
                            HD.UWAGI, 
                            HD.LpW, 
                            HD.LpP1, 
                            HD.LpP2,
                            HD.Utworzone, 
                            HD.Wysłane, 
                            HD.Otrzymane, 
                            HD.PotwWaga, 
                            HD.KtoWaga, 
                            HD.KiedyWaga, 
                            HD.PotwSztuki, 
                            HD.KtoSztuki, 
                            HD.KiedySztuki, 
                            HD.PotwCena, 
                            HD.Dodatek, 
                            HD.Kurnik, 
                            HD.KmK, 
                            HD.KmH, 
                            HD.Ubiorka, 
                            HD.Ubytek, 
                            HD.DataUtw, 
                            HD.KtoStwo, 
                            HD.DataMod, 
                            HD.KtoMod, 
                            HD.CzyOdznaczoneWstawienie,
                            WK.DataWstawienia,
                            CASE 
                                WHEN WK.DataWstawienia IS NOT NULL 
                                THEN DATEDIFF(day, WK.DataWstawienia, HD.DataOdbioru)
                                ELSE NULL
                            END AS RoznicaDni
                        FROM [LibraNet].[dbo].[HarmonogramDostaw] HD
                        LEFT JOIN WstawieniaKurczakow WK ON HD.LpW = WK.Lp
                        ORDER BY HD.LP DESC";

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
                            HD.LP, 
                            HD.DostawcaID, 
                            HD.DataOdbioru, 
                            HD.Dostawca, 
                            HD.Auta, 
                            HD.SztukiDek, 
                            HD.WagaDek, 
                            HD.SztSzuflada, 
                            HD.TypUmowy, 
                            HD.TypCeny, 
                            HD.Cena, 
                            HD.PaszaPisklak, 
                            HD.Bufor, 
                            HD.UWAGI, 
                            HD.LpW, 
                            HD.LpP1, 
                            HD.LpP2,
                            HD.Utworzone, 
                            HD.Wysłane, 
                            HD.Otrzymane, 
                            HD.PotwWaga, 
                            HD.KtoWaga, 
                            HD.KiedyWaga, 
                            HD.PotwSztuki, 
                            HD.KtoSztuki, 
                            HD.KiedySztuki, 
                            HD.PotwCena, 
                            HD.Dodatek, 
                            HD.Kurnik, 
                            HD.KmK, 
                            HD.KmH, 
                            HD.Ubiorka, 
                            HD.Ubytek, 
                            HD.DataUtw, 
                            HD.KtoStwo, 
                            HD.DataMod, 
                            HD.KtoMod, 
                            HD.CzyOdznaczoneWstawienie,
                            WK.DataWstawienia,
                            CASE 
                                WHEN WK.DataWstawienia IS NOT NULL 
                                THEN DATEDIFF(day, WK.DataWstawienia, HD.DataOdbioru)
                                ELSE NULL
                            END AS RoznicaDni
                        FROM [LibraNet].[dbo].[HarmonogramDostaw] HD
                        LEFT JOIN WstawieniaKurczakow WK ON HD.LpW = WK.Lp
                        ORDER BY HD.LP DESC";

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
                {"RoznicaDni", ("Dni hodowli", 80)},
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
                {"Ubytek", ("Ubytek %", 70)},
                {"DataWstawienia", ("Data Wstaw.", 85)}
            };

            foreach (var config in configs)
            {
                if (dataGridView1.Columns.Contains(config.Key))
                {
                    dataGridView1.Columns[config.Key].HeaderText = config.Value.header;
                    dataGridView1.Columns[config.Key].Width = config.Value.width;
                }
            }

            // Ustawienie kolejności kolumn - RoznicaDni zaraz po DataOdbioru
            if (dataGridView1.Columns.Contains("RoznicaDni") && dataGridView1.Columns.Contains("DataOdbioru"))
            {
                int dataOdbioruIndex = dataGridView1.Columns["DataOdbioru"].DisplayIndex;
                dataGridView1.Columns["RoznicaDni"].DisplayIndex = dataOdbioruIndex + 1;

                // Przesuń pozostałe kolumny
                if (dataGridView1.Columns.Contains("Dostawca"))
                    dataGridView1.Columns["Dostawca"].DisplayIndex = dataOdbioruIndex + 2;
                if (dataGridView1.Columns.Contains("Auta"))
                    dataGridView1.Columns["Auta"].DisplayIndex = dataOdbioruIndex + 3;
                if (dataGridView1.Columns.Contains("SztukiDek"))
                    dataGridView1.Columns["SztukiDek"].DisplayIndex = dataOdbioruIndex + 4;
                if (dataGridView1.Columns.Contains("WagaDek"))
                    dataGridView1.Columns["WagaDek"].DisplayIndex = dataOdbioruIndex + 5;
            }

            // Formatowanie kolumny RoznicaDni
            if (dataGridView1.Columns.Contains("RoznicaDni"))
            {
                dataGridView1.Columns["RoznicaDni"].DefaultCellStyle.Format = "0 dni";
                dataGridView1.Columns["RoznicaDni"].DefaultCellStyle.NullValue = "-";
                dataGridView1.Columns["RoznicaDni"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }

            // Ukryj niektóre kolumny
            string[] hiddenColumns = { "PaszaPisklak", "LpW", "LpP1", "LpP2", "KtoWaga",
                "KiedyWaga", "KtoSztuki", "KiedySztuki", "Ubiorka", "DataUtw",
                "KtoStwo", "DataMod", "KtoMod", "CzyOdznaczoneWstawienie", "DataWstawienia" };

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

            try
            {
                IEnumerable<DataRow> filteredRows = originalData.AsEnumerable();

                // Filtr tekstowy
                if (!string.IsNullOrWhiteSpace(textBox1.Text))
                {
                    string searchText = textBox1.Text.Trim().ToLower();
                    filteredRows = filteredRows.Where(r =>
                    {
                        // Kolumny tekstowe
                        bool matchText =
                            (r["Dostawca"] != DBNull.Value && r["Dostawca"].ToString().ToLower().Contains(searchText)) ||
                            (r["UWAGI"] != DBNull.Value && r["UWAGI"].ToString().ToLower().Contains(searchText)) ||
                            (r["Kurnik"] != DBNull.Value && r["Kurnik"].ToString().ToLower().Contains(searchText)) ||
                            (r["TypUmowy"] != DBNull.Value && r["TypUmowy"].ToString().ToLower().Contains(searchText)) ||
                            (r["TypCeny"] != DBNull.Value && r["TypCeny"].ToString().ToLower().Contains(searchText)) ||
                            (r["Bufor"] != DBNull.Value && r["Bufor"].ToString().ToLower().Contains(searchText));

                        // Kolumny numeryczne
                        bool matchNumeric =
                            (r["DostawcaID"] != DBNull.Value && r["DostawcaID"].ToString().Contains(searchText)) ||
                            (r["Auta"] != DBNull.Value && r["Auta"].ToString().Contains(searchText)) ||
                            (r["SztukiDek"] != DBNull.Value && r["SztukiDek"].ToString().Contains(searchText)) ||
                            (r["LP"] != DBNull.Value && r["LP"].ToString().Contains(searchText));

                        return matchText || matchNumeric;
                    });
                }

                // Filtr dostawcy
                if (comboBoxDostawca.SelectedIndex > 0 && comboBoxDostawca.SelectedItem != null)
                {
                    string supplier = comboBoxDostawca.SelectedItem.ToString();
                    filteredRows = filteredRows.Where(r =>
                        r["Dostawca"] != DBNull.Value && r["Dostawca"].ToString() == supplier);
                }

                // Filtr statusu
                if (comboBoxStatus.SelectedIndex > 0 && comboBoxStatus.SelectedItem != null)
                {
                    string status = comboBoxStatus.SelectedItem.ToString();
                    filteredRows = filteredRows.Where(r =>
                        r["Bufor"] != DBNull.Value && r["Bufor"].ToString() == status);
                }

                // Tworzenie nowej tabeli z przefiltrowanymi danymi
                DataTable filteredTable = originalData.Clone();
                foreach (var row in filteredRows)
                {
                    filteredTable.ImportRow(row);
                }

                dataGridView1.DataSource = filteredTable;
                ColorRows();
                UpdateStatistics(filteredTable);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd filtrowania: {ex.Message}\n\n{ex.StackTrace}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void ClearFilters()
        {
            isLoading = true;

            textBox1.Clear();
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
                        SrednieDniHodowli = g.Average(r => r.Field<int?>("RoznicaDni") ?? 0),
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
                groupedTable.Columns.Add("Śr. Dni Hodowli", typeof(decimal));
                groupedTable.Columns.Add("Potwierdzone", typeof(int));
                groupedTable.Columns.Add("Anulowane", typeof(int));

                foreach (var item in grouped)
                {
                    groupedTable.Rows.Add(item.Dostawca, item.IloscDostaw,
                        item.SumaWagi, item.SumaSztuk, Math.Round(item.SredniaCena, 2),
                        Math.Round(item.SrednieDniHodowli, 1),
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

        private void ShowSuppliersBreakdown()
        {
            if (originalData == null || originalData.Rows.Count == 0)
            {
                MessageBox.Show("Brak danych do analizy!", "Informacja",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Utworzenie okna z listą hodowców
            Form suppliersForm = new Form();
            suppliersForm.Text = "Analiza Hodowców - Kontrakty vs Wolny Rynek";
            suppliersForm.Size = new Size(1200, 700);
            suppliersForm.StartPosition = FormStartPosition.CenterParent;

            // TabControl dla różnych widoków
            TabControl tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;

            // Tab 1: Szczegółowa lista hodowców
            TabPage tabDetails = new TabPage("Szczegóły Hodowców");
            DataGridView dgvSuppliers = new DataGridView();
            dgvSuppliers.Dock = DockStyle.Fill;
            dgvSuppliers.AllowUserToAddRows = false;
            dgvSuppliers.AllowUserToDeleteRows = false;
            dgvSuppliers.ReadOnly = true;
            dgvSuppliers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dgvSuppliers.BackgroundColor = Color.White;
            dgvSuppliers.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 152, 219);
            dgvSuppliers.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvSuppliers.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgvSuppliers.EnableHeadersVisualStyles = false;
            dgvSuppliers.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);

            // Tab 2: Podsumowanie grup
            TabPage tabSummary = new TabPage("Podsumowanie Grup");

            // Panel z podsumowaniem kontraktów
            Panel panelContracts = new Panel();
            panelContracts.Dock = DockStyle.Left;
            panelContracts.Width = 580;
            panelContracts.BackColor = Color.FromArgb(236, 240, 241);

            Label lblContractsTitle = new Label();
            lblContractsTitle.Text = "🤝 HODOWCY KONTRAKTOWI";
            lblContractsTitle.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            lblContractsTitle.ForeColor = Color.FromArgb(41, 128, 185);
            lblContractsTitle.Dock = DockStyle.Top;
            lblContractsTitle.Height = 40;
            lblContractsTitle.TextAlign = ContentAlignment.MiddleCenter;

            ListBox listContracts = new ListBox();
            listContracts.Dock = DockStyle.Fill;
            listContracts.Font = new Font("Segoe UI", 10F);
            listContracts.BorderStyle = BorderStyle.None;

            panelContracts.Controls.Add(listContracts);
            panelContracts.Controls.Add(lblContractsTitle);

            // Panel z podsumowaniem wolnego rynku
            Panel panelFreeMarket = new Panel();
            panelFreeMarket.Dock = DockStyle.Right;
            panelFreeMarket.Width = 580;
            panelFreeMarket.BackColor = Color.FromArgb(253, 235, 208);

            Label lblFreeMarketTitle = new Label();
            lblFreeMarketTitle.Text = "💰 HODOWCY WOLNEGO RYNKU";
            lblFreeMarketTitle.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            lblFreeMarketTitle.ForeColor = Color.FromArgb(211, 84, 0);
            lblFreeMarketTitle.Dock = DockStyle.Top;
            lblFreeMarketTitle.Height = 40;
            lblFreeMarketTitle.TextAlign = ContentAlignment.MiddleCenter;

            ListBox listFreeMarket = new ListBox();
            listFreeMarket.Dock = DockStyle.Fill;
            listFreeMarket.Font = new Font("Segoe UI", 10F);
            listFreeMarket.BorderStyle = BorderStyle.None;

            panelFreeMarket.Controls.Add(listFreeMarket);
            panelFreeMarket.Controls.Add(lblFreeMarketTitle);

            // Separator
            Panel separator = new Panel();
            separator.Dock = DockStyle.Fill;
            separator.BackColor = Color.Gray;

            tabSummary.Controls.Add(separator);
            tabSummary.Controls.Add(panelFreeMarket);
            tabSummary.Controls.Add(panelContracts);

            // Analiza danych
            var confirmedData = originalData.AsEnumerable()
                .Where(r => r.Field<string>("Bufor") == "Potwierdzony");

            var supplierAnalysis = confirmedData
                .GroupBy(r => r.Field<string>("Dostawca"))
                .Select(g =>
                {
                    var kontrakty = g.Where(r =>
                    {
                        string typCeny = r.Field<string>("TypCeny")?.ToLower() ?? "";
                        return !typCeny.Contains("wolnyrynek") && !typCeny.Contains("wolnorynkowa");
                    });

                    var wolnyRynek = g.Where(r =>
                    {
                        string typCeny = r.Field<string>("TypCeny")?.ToLower() ?? "";
                        return typCeny.Contains("wolnyrynek") || typCeny.Contains("wolnorynkowa");
                    });

                    int dostawK = kontrakty.Count();
                    int dostawWR = wolnyRynek.Count();
                    int sztukiK = kontrakty.Sum(r => r.Field<int?>("SztukiDek") ?? 0);
                    int sztukiWR = wolnyRynek.Sum(r => r.Field<int?>("SztukiDek") ?? 0);
                    decimal wagaK = kontrakty.Sum(r => r.Field<decimal?>("WagaDek") ?? 0);
                    decimal wagaWR = wolnyRynek.Sum(r => r.Field<decimal?>("WagaDek") ?? 0);

                    return new
                    {
                        Hodowca = g.Key,
                        DostawK = dostawK,
                        DostawWR = dostawWR,
                        DostawRazem = dostawK + dostawWR,
                        SztukiK = sztukiK,
                        SztukiWR = sztukiWR,
                        SztukiRazem = sztukiK + sztukiWR,
                        ProcentSztukiK = (sztukiK + sztukiWR) > 0 ? (decimal)sztukiK / (sztukiK + sztukiWR) * 100 : 0,
                        ProcentSztukiWR = (sztukiK + sztukiWR) > 0 ? (decimal)sztukiWR / (sztukiK + sztukiWR) * 100 : 0,
                        WagaK = wagaK,
                        WagaWR = wagaWR,
                        WagaRazem = wagaK + wagaWR,
                        Typ = dostawK > dostawWR ? "Kontrakt" : dostawWR > dostawK ? "Wolny Rynek" : "Mieszany",
                        DominacjaKontrakt = dostawK > 0 ? (decimal)dostawK / (dostawK + dostawWR) * 100 : 0
                    };
                })
                .OrderByDescending(x => x.SztukiRazem)
                .ToList();

            // Utworzenie DataTable dla szczegółów
            DataTable dtSuppliers = new DataTable();
            dtSuppliers.Columns.Add("Hodowca", typeof(string));
            dtSuppliers.Columns.Add("Typ dominujący", typeof(string));
            dtSuppliers.Columns.Add("Sztuki K", typeof(int));
            dtSuppliers.Columns.Add("Sztuki WR", typeof(int));
            dtSuppliers.Columns.Add("Sztuki Razem", typeof(int));
            dtSuppliers.Columns.Add("Kontrakty %", typeof(decimal));
            dtSuppliers.Columns.Add("Wolny Rynek %", typeof(decimal));

            foreach (var item in supplierAnalysis)
            {
                dtSuppliers.Rows.Add(
                    item.Hodowca,
                    item.Typ,
                    item.SztukiK,
                    item.SztukiWR,
                    item.SztukiRazem,
                    item.ProcentSztukiK,
                    item.ProcentSztukiWR
                );
            }

            dgvSuppliers.DataSource = dtSuppliers;

            // Formatowanie kolumn
            foreach (DataGridViewColumn col in dgvSuppliers.Columns)
            {
                if (col.Name.Contains("%"))
                    col.DefaultCellStyle.Format = "0.0'%'";
                else if (col.Name.Contains("Waga"))
                    col.DefaultCellStyle.Format = "N0";
                else if (col.Name.Contains("Sztuki"))
                    col.DefaultCellStyle.Format = "N0";
            }

            // Kolorowanie wierszy według typu
            dgvSuppliers.CellFormatting += (s, e) =>
            {
                if (e.RowIndex >= 0 && dgvSuppliers.Columns[e.ColumnIndex].Name == "Typ dominujący")
                {
                    string typ = dgvSuppliers.Rows[e.RowIndex].Cells["Typ dominujący"].Value?.ToString();
                    if (typ == "Kontrakt")
                    {
                        dgvSuppliers.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(214, 234, 248);
                    }
                    else if (typ == "Wolny Rynek")
                    {
                        dgvSuppliers.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(253, 237, 197);
                    }
                }
            };

            // Wypełnienie list w zakładce podsumowania
            var contractSuppliers = supplierAnalysis
                .Where(x => x.DominacjaKontrakt >= 70)
                .OrderByDescending(x => x.SztukiK);

            var freeMarketSuppliers = supplierAnalysis
                .Where(x => x.DominacjaKontrakt <= 30)
                .OrderByDescending(x => x.SztukiWR);

            foreach (var supplier in contractSuppliers)
            {
                listContracts.Items.Add($"{supplier.Hodowca} - {supplier.SztukiK:N0} szt ({supplier.ProcentSztukiK:F0}%)");
            }

            foreach (var supplier in freeMarketSuppliers)
            {
                listFreeMarket.Items.Add($"{supplier.Hodowca} - {supplier.SztukiWR:N0} szt ({supplier.ProcentSztukiWR:F0}%)");
            }

            // Panel statystyk na dole
            Panel statsPanel = new Panel();
            statsPanel.Height = 80;
            statsPanel.Dock = DockStyle.Bottom;
            statsPanel.BackColor = Color.FromArgb(44, 62, 80);

            Label lblStats = new Label();
            lblStats.Dock = DockStyle.Fill;
            lblStats.ForeColor = Color.White;
            lblStats.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblStats.Padding = new Padding(10);

            int totalSuppliersK = supplierAnalysis.Count(x => x.DominacjaKontrakt >= 70);
            int totalSuppliersWR = supplierAnalysis.Count(x => x.DominacjaKontrakt <= 30);
            int totalSuppliersMixed = supplierAnalysis.Count(x => x.DominacjaKontrakt > 30 && x.DominacjaKontrakt < 70);

            int totalSztukiK = supplierAnalysis.Sum(x => x.SztukiK);
            int totalSztukiWR = supplierAnalysis.Sum(x => x.SztukiWR);

            lblStats.Text = $"📊 PODSUMOWANIE:   " +
                $"Hodowcy kontraktowi (≥70% K): {totalSuppliersK}   |   " +
                $"Hodowcy mieszani: {totalSuppliersMixed}   |   " +
                $"Hodowcy wolnego rynku (≥70% WR): {totalSuppliersWR}\n" +
                $"🐔 SZTUKI OGÓŁEM:   Kontrakty: {totalSztukiK:N0} ({(totalSztukiK + totalSztukiWR > 0 ? (decimal)totalSztukiK / (totalSztukiK + totalSztukiWR) * 100 : 0):F1}%)   |   " +
                $"Wolny Rynek: {totalSztukiWR:N0} ({(totalSztukiK + totalSztukiWR > 0 ? (decimal)totalSztukiWR / (totalSztukiK + totalSztukiWR) * 100 : 0):F1}%)";

            statsPanel.Controls.Add(lblStats);

            // Dodanie kontrolek do zakładek
            tabDetails.Controls.Add(dgvSuppliers);

            tabControl.TabPages.Add(tabDetails);
            tabControl.TabPages.Add(tabSummary);

            suppliersForm.Controls.Add(tabControl);
            suppliersForm.Controls.Add(statsPanel);

            suppliersForm.ShowDialog();
        }

        public void SetTextBoxValue()
        {
            textBox1.Text = TextBoxValue;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            // Pusta - obsługa w TextBox1_TextChanged
        }

        private void ShowContractAnalysis()
        {
            if (originalData == null || originalData.Rows.Count == 0)
            {
                MessageBox.Show("Brak danych do analizy!", "Informacja",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Utworzenie okna analizy
            Form analysisForm = new Form();
            analysisForm.Text = "Analiza Kontrakty vs Wolny Rynek";
            analysisForm.Size = new Size(900, 600);
            analysisForm.StartPosition = FormStartPosition.CenterParent;

            // Panel kontrolek
            Panel controlPanel = new Panel();
            controlPanel.Height = 80;
            controlPanel.Dock = DockStyle.Top;
            controlPanel.BackColor = Color.FromArgb(240, 240, 240);

            // RadioButtons dla wyboru okresu
            GroupBox periodGroup = new GroupBox();
            periodGroup.Text = "Wybór okresu";
            periodGroup.Location = new Point(10, 5);
            periodGroup.Size = new Size(400, 65);

            RadioButton rbYear = new RadioButton();
            rbYear.Text = "Rok";
            rbYear.Location = new Point(10, 20);
            rbYear.Width = 60;
            rbYear.Checked = true;

            RadioButton rbQuarter = new RadioButton();
            rbQuarter.Text = "Kwartał";
            rbQuarter.Location = new Point(80, 20);
            rbQuarter.Width = 70;

            RadioButton rbMonth = new RadioButton();
            rbMonth.Text = "Miesiąc";
            rbMonth.Location = new Point(160, 20);
            rbMonth.Width = 70;

            RadioButton rbAll = new RadioButton();
            rbAll.Text = "Wszystko";
            rbAll.Location = new Point(240, 20);
            rbAll.Width = 80;

            RadioButton rbCustom = new RadioButton();
            rbCustom.Text = "Zakres";
            rbCustom.Location = new Point(330, 20);
            rbCustom.Width = 65;

            periodGroup.Controls.AddRange(new Control[] { rbYear, rbQuarter, rbMonth, rbAll, rbCustom });

            // Data pickers dla zakresu custom
            DateTimePicker dtpFrom = new DateTimePicker();
            dtpFrom.Format = DateTimePickerFormat.Short;
            dtpFrom.Location = new Point(10, 40);
            dtpFrom.Width = 100;
            dtpFrom.Enabled = false;

            DateTimePicker dtpTo = new DateTimePicker();
            dtpTo.Format = DateTimePickerFormat.Short;
            dtpTo.Location = new Point(120, 40);
            dtpTo.Width = 100;
            dtpTo.Enabled = false;

            periodGroup.Controls.Add(dtpFrom);
            periodGroup.Controls.Add(dtpTo);

            // Przycisk analizy
            Button btnAnalyze = new Button();
            btnAnalyze.Text = "Analizuj";
            btnAnalyze.Location = new Point(420, 25);
            btnAnalyze.Size = new Size(100, 40);
            btnAnalyze.BackColor = Color.FromArgb(52, 152, 219);
            btnAnalyze.ForeColor = Color.White;
            btnAnalyze.FlatStyle = FlatStyle.Flat;
            btnAnalyze.Font = new Font("Segoe UI", 10F, FontStyle.Bold);

            // Przycisk listy hodowców
            Button btnShowSuppliers = new Button();
            btnShowSuppliers.Text = "Hodowcy";
            btnShowSuppliers.Location = new Point(530, 25);
            btnShowSuppliers.Size = new Size(100, 40);
            btnShowSuppliers.BackColor = Color.FromArgb(155, 89, 182);
            btnShowSuppliers.ForeColor = Color.White;
            btnShowSuppliers.FlatStyle = FlatStyle.Flat;
            btnShowSuppliers.Font = new Font("Segoe UI", 10F, FontStyle.Bold);

            // Przycisk eksportu
            Button btnExportAnalysis = new Button();
            btnExportAnalysis.Text = "Eksport CSV";
            btnExportAnalysis.Location = new Point(640, 25);
            btnExportAnalysis.Size = new Size(100, 40);
            btnExportAnalysis.BackColor = Color.FromArgb(46, 204, 113);
            btnExportAnalysis.ForeColor = Color.White;
            btnExportAnalysis.FlatStyle = FlatStyle.Flat;
            btnExportAnalysis.Font = new Font("Segoe UI", 10F, FontStyle.Bold);

            controlPanel.Controls.Add(periodGroup);
            controlPanel.Controls.Add(btnAnalyze);
            controlPanel.Controls.Add(btnShowSuppliers);
            controlPanel.Controls.Add(btnExportAnalysis);

            // DataGridView dla wyników
            DataGridView dgvResults = new DataGridView();
            dgvResults.Dock = DockStyle.Fill;
            dgvResults.AllowUserToAddRows = false;
            dgvResults.AllowUserToDeleteRows = false;
            dgvResults.ReadOnly = true;
            dgvResults.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvResults.BackgroundColor = Color.White;
            dgvResults.BorderStyle = BorderStyle.None;
            dgvResults.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 152, 219);
            dgvResults.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvResults.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dgvResults.EnableHeadersVisualStyles = false;
            dgvResults.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);

            // Panel podsumowania
            Panel summaryPanel = new Panel();
            summaryPanel.Height = 120;
            summaryPanel.Dock = DockStyle.Bottom;
            summaryPanel.BackColor = Color.FromArgb(245, 245, 245);
            summaryPanel.BorderStyle = BorderStyle.FixedSingle;

            Label lblSummary = new Label();
            lblSummary.Name = "lblSummary";
            lblSummary.Dock = DockStyle.Fill;
            lblSummary.Font = new Font("Segoe UI", 10F);
            lblSummary.Padding = new Padding(10);
            summaryPanel.Controls.Add(lblSummary);

            // Obsługa zmiany typu okresu
            rbCustom.CheckedChanged += (s, e) =>
            {
                dtpFrom.Enabled = rbCustom.Checked;
                dtpTo.Enabled = rbCustom.Checked;
            };

            // Obsługa przycisku Analizuj
            btnAnalyze.Click += (s, e) =>
            {
                DataTable analysisData = PerformContractAnalysis(
                    rbYear.Checked ? "Year" :
                    rbQuarter.Checked ? "Quarter" :
                    rbMonth.Checked ? "Month" :
                    rbAll.Checked ? "All" : "Custom",
                    dtpFrom.Value, dtpTo.Value);

                dgvResults.DataSource = analysisData;

                // Formatowanie kolumn
                foreach (DataGridViewColumn col in dgvResults.Columns)
                {
                    if (col.Name.Contains("%"))
                        col.DefaultCellStyle.Format = "0.0'%'";
                    else if (col.Name.Contains("Waga"))
                        col.DefaultCellStyle.Format = "N0";
                    else if (col.Name.Contains("Sztuki"))
                        col.DefaultCellStyle.Format = "N0";
                }

                // Aktualizacja podsumowania
                UpdateAnalysisSummary(lblSummary, analysisData);
            };

            // Obsługa przycisku Hodowcy
            btnShowSuppliers.Click += (s, e) =>
            {
                ShowSuppliersBreakdown();
            };

            // Obsługa eksportu
            btnExportAnalysis.Click += (s, e) =>
            {
                if (dgvResults.DataSource == null)
                {
                    MessageBox.Show("Najpierw wykonaj analizę!", "Informacja",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                ExportAnalysisToCSV(dgvResults);
            };

            analysisForm.Controls.Add(dgvResults);
            analysisForm.Controls.Add(summaryPanel);
            analysisForm.Controls.Add(controlPanel);

            // Wykonaj początkową analizę
            btnAnalyze.PerformClick();

            analysisForm.ShowDialog();
        }

        private DataTable PerformContractAnalysis(string periodType, DateTime customFrom, DateTime customTo)
        {
            DataTable result = new DataTable();
            result.Columns.Add("Okres", typeof(string));
            result.Columns.Add("SztukiKontrakty", typeof(int));
            result.Columns.Add("SztukiWolnyRynek", typeof(int));
            result.Columns.Add("SztukiRazem", typeof(int));
            result.Columns.Add("ProcentSztukiK", typeof(decimal));
            result.Columns.Add("ProcentSztukiWR", typeof(decimal));

            // Filtruj tylko potwierdzone
            var confirmedData = originalData.AsEnumerable()
                .Where(r => r.Field<string>("Bufor") == "Potwierdzony" &&
                           r.Field<DateTime?>("DataOdbioru") != null);

            if (periodType == "Custom")
            {
                confirmedData = confirmedData.Where(r =>
                    r.Field<DateTime>("DataOdbioru") >= customFrom.Date &&
                    r.Field<DateTime>("DataOdbioru") <= customTo.Date);
            }

            // Grupowanie według okresu
            IEnumerable<IGrouping<string, DataRow>> grouped = null;

            switch (periodType)
            {
                case "Year":
                    grouped = confirmedData.GroupBy(r => r.Field<DateTime>("DataOdbioru").Year.ToString());
                    break;
                case "Quarter":
                    grouped = confirmedData.GroupBy(r =>
                    {
                        var date = r.Field<DateTime>("DataOdbioru");
                        int quarter = (date.Month - 1) / 3 + 1;
                        return $"{date.Year} Q{quarter}";
                    });
                    break;
                case "Month":
                    grouped = confirmedData.GroupBy(r =>
                    {
                        var date = r.Field<DateTime>("DataOdbioru");
                        return $"{date.Year}-{date.Month:00}";
                    });
                    break;
                case "All":
                    grouped = confirmedData.GroupBy(r => "Wszystkie dane");
                    break;
                case "Custom":
                    grouped = confirmedData.GroupBy(r => $"{customFrom:yyyy-MM-dd} - {customTo:yyyy-MM-dd}");
                    break;
            }

            // Analiza dla każdego okresu
            foreach (var group in grouped.OrderBy(g => g.Key))
            {
                var kontrakty = group.Where(r =>
                {
                    string typCeny = r.Field<string>("TypCeny")?.ToLower() ?? "";
                    return !typCeny.Contains("wolnyrynek") && !typCeny.Contains("wolnorynkowa");
                });

                var wolnyRynek = group.Where(r =>
                {
                    string typCeny = r.Field<string>("TypCeny")?.ToLower() ?? "";
                    return typCeny.Contains("wolnyrynek") || typCeny.Contains("wolnorynkowa");
                });

                int sztukiKontrakty = kontrakty.Sum(r => r.Field<int?>("SztukiDek") ?? 0);
                int sztukiWolnyRynek = wolnyRynek.Sum(r => r.Field<int?>("SztukiDek") ?? 0);
                int sztukiRazem = sztukiKontrakty + sztukiWolnyRynek;

                decimal procentSztukiK = sztukiRazem > 0 ? (decimal)sztukiKontrakty / sztukiRazem * 100 : 0;
                decimal procentSztukiWR = sztukiRazem > 0 ? (decimal)sztukiWolnyRynek / sztukiRazem * 100 : 0;

                result.Rows.Add(
                    group.Key,
                    sztukiKontrakty,
                    sztukiWolnyRynek,
                    sztukiRazem,
                    procentSztukiK,
                    procentSztukiWR
                );
            }

            // Zmiana nazw kolumn na polskie
            result.Columns["SztukiKontrakty"].ColumnName = "Sztuki K";
            result.Columns["SztukiWolnyRynek"].ColumnName = "Sztuki WR";
            result.Columns["SztukiRazem"].ColumnName = "Sztuki Razem";
            result.Columns["ProcentSztukiK"].ColumnName = "Kontrakty %";
            result.Columns["ProcentSztukiWR"].ColumnName = "Wolny Rynek %";

            return result;
        }

        private void UpdateAnalysisSummary(Label lblSummary, DataTable data)
        {
            if (data == null || data.Rows.Count == 0)
            {
                lblSummary.Text = "Brak danych do analizy";
                return;
            }

            int sztukiKontrakty = data.AsEnumerable().Sum(r => Convert.ToInt32(r["Sztuki K"]));
            int sztukiWolnyRynek = data.AsEnumerable().Sum(r => Convert.ToInt32(r["Sztuki WR"]));
            int sztukiTotal = sztukiKontrakty + sztukiWolnyRynek;

            decimal percentSztukiK = sztukiTotal > 0 ? (decimal)sztukiKontrakty / sztukiTotal * 100 : 0;
            decimal percentSztukiWR = sztukiTotal > 0 ? (decimal)sztukiWolnyRynek / sztukiTotal * 100 : 0;

            lblSummary.Text = $"PODSUMOWANIE ANALIZY:\n\n" +
                $"🐔 SZTUKI DROBIU OGÓŁEM: {sztukiTotal:N0}\n\n" +
                $"📊 KONTRAKTY: {sztukiKontrakty:N0} sztuk ({percentSztukiK:F1}%)\n" +
                $"💰 WOLNY RYNEK: {sztukiWolnyRynek:N0} sztuk ({percentSztukiWR:F1}%)\n\n" +
                $"📈 ŚREDNIA WAGA/SZTUKĘ: {(sztukiTotal > 0 ? "Oblicz na podstawie danych szczegółowych" : "Brak danych")}";
        }

        private void ExportAnalysisToCSV(DataGridView dgv)
        {
            try
            {
                SaveFileDialog saveDialog = new SaveFileDialog();
                saveDialog.Filter = "CSV Files (*.csv)|*.csv";
                saveDialog.FileName = $"Analiza_KW_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    StringBuilder csv = new StringBuilder();

                    // Nagłówki
                    for (int i = 0; i < dgv.Columns.Count; i++)
                    {
                        csv.Append(dgv.Columns[i].HeaderText);
                        if (i < dgv.Columns.Count - 1)
                            csv.Append(";");
                    }
                    csv.AppendLine();

                    // Dane
                    foreach (DataGridViewRow row in dgv.Rows)
                    {
                        for (int i = 0; i < dgv.Columns.Count; i++)
                        {
                            csv.Append(row.Cells[i].Value?.ToString() ?? "");
                            if (i < dgv.Columns.Count - 1)
                                csv.Append(";");
                        }
                        csv.AppendLine();
                    }

                    File.WriteAllText(saveDialog.FileName, csv.ToString(), Encoding.UTF8);
                    MessageBox.Show("Eksport zakończony pomyślnie!", "Sukces",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd eksportu: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}