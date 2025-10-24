using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class WidokMatrycaNowy : Form
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private ZapytaniaSQL zapytaniasql = new ZapytaniaSQL();

        // UI Components
        private Panel headerPanel;
        private Panel toolbarPanel;
        private DataGridView dataGridView1;
        private DateTimePicker dateTimePicker1;
        private Button btnPreviousDay;
        private Button btnNextDay;
        private Button btnSaveToDatabase;
        private Button btnMoveUp;
        private Button btnMoveDown;
        private Button btnAddRow;
        private Button btnDeleteRow;
        private Button btnRefresh;
        private Label lblTitle;
        private Label lblRecordCount;
        private Label lblTotalWeight;
        private Label lblTotalPieces;

        // Data tables for ComboBoxes
        private DataTable driverTable;
        private DataTable carTable;
        private DataTable trailerTable;
        private DataTable wozekTable;

        // Drag & Drop
        private int dragRowIndex = -1;

        public WidokMatrycaNowy()
        {
            InitializeComponent();
            InitializeCustomUI();
            LoadComboBoxData();
            DisplayData();
            UpdateStatistics();
        }

        private void InitializeCustomUI()
        {
            // Konfiguracja formularza
            this.Text = "Matryca Avilog - Planowanie Transportu";
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.FromArgb(236, 239, 241);
            this.Font = new Font("Segoe UI", 10F);

            // Header Panel
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = Color.FromArgb(92, 138, 58)
            };

            lblTitle = new Label
            {
                Text = "📋 MATRYCA AVILOG - PLANOWANIE TRANSPORTU",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Location = new Point(20, 20),
                Size = new Size(600, 35)
            };

            Label lblSubtitle = new Label
            {
                Text = "System planowania dostaw brojlerów od hodowców",
                Font = new Font("Segoe UI", 11F),
                ForeColor = Color.FromArgb(224, 240, 214),
                AutoSize = false,
                Location = new Point(20, 58),
                Size = new Size(600, 25)
            };

            headerPanel.Controls.AddRange(new Control[] { lblTitle, lblSubtitle });

            // Toolbar Panel
            toolbarPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.White,
                Padding = new Padding(15, 10, 15, 10)
            };

            // Data selection group
            GroupBox dateGroup = CreateStyledGroupBox("📅 Wybór daty", 10, 5, 380, 65);

            dateTimePicker1 = new DateTimePicker
            {
                Location = new Point(15, 25),
                Size = new Size(200, 30),
                Font = new Font("Segoe UI", 10F),
                Format = DateTimePickerFormat.Long
            };
            dateTimePicker1.ValueChanged += DateTimePicker1_ValueChanged;

            btnPreviousDay = CreateIconButton("◀", "Poprzedni dzień", 220, 25, 60, 30);
            btnPreviousDay.Click += BtnPreviousDay_Click;

            btnNextDay = CreateIconButton("▶", "Następny dzień", 285, 25, 60, 30);
            btnNextDay.Click += BtnNextDay_Click;

            dateGroup.Controls.AddRange(new Control[] { dateTimePicker1, btnPreviousDay, btnNextDay });

            // Action buttons group
            GroupBox actionsGroup = CreateStyledGroupBox("⚙️ Akcje", 400, 5, 520, 65);

            btnRefresh = CreateActionButton("🔄", "Odśwież", 10, 25, 90, 35, Color.FromArgb(52, 152, 219));
            btnRefresh.Click += (s, e) => { DisplayData(); UpdateStatistics(); };

            btnMoveUp = CreateActionButton("⬆", "Góra", 110, 25, 80, 35, Color.FromArgb(46, 125, 50));
            btnMoveUp.Click += BtnMoveUp_Click;

            btnMoveDown = CreateActionButton("⬇", "Dół", 200, 25, 80, 35, Color.FromArgb(46, 125, 50));
            btnMoveDown.Click += BtnMoveDown_Click;

            btnAddRow = CreateActionButton("➕", "Dodaj", 290, 25, 80, 35, Color.FromArgb(0, 150, 136));
            btnAddRow.Click += BtnAddRow_Click;

            btnDeleteRow = CreateActionButton("🗑", "Usuń", 380, 25, 80, 35, Color.FromArgb(211, 47, 47));
            btnDeleteRow.Click += BtnDeleteRow_Click;

            actionsGroup.Controls.AddRange(new Control[] { btnRefresh, btnMoveUp, btnMoveDown, btnAddRow, btnDeleteRow });

            // Save button (prominent)
            btnSaveToDatabase = new Button
            {
                Text = "💾 ZAPISZ DO BAZY",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(92, 138, 58),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(200, 65),
                Location = new Point(930, 5),
                Cursor = Cursors.Hand
            };
            btnSaveToDatabase.FlatAppearance.BorderSize = 0;
            btnSaveToDatabase.Click += BtnSaveToDatabase_Click;

            // Statistics panel
            Panel statsPanel = new Panel
            {
                BackColor = Color.FromArgb(240, 245, 250),
                Size = new Size(180, 65),
                Location = new Point(1140, 5)
            };

            lblRecordCount = CreateStatLabel("Rekordów: 0", 5, 5);
            lblTotalWeight = CreateStatLabel("Waga: 0 kg", 5, 25);
            lblTotalPieces = CreateStatLabel("Sztuk: 0", 5, 45);

            statsPanel.Controls.AddRange(new Control[] { lblRecordCount, lblTotalWeight, lblTotalPieces });

            toolbarPanel.Controls.AddRange(new Control[] { dateGroup, actionsGroup, btnSaveToDatabase, statsPanel });

            // DataGridView - inicjalizacja podstawowa
            dataGridView1 = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(230, 230, 230),
                RowHeadersVisible = true,
                RowHeadersWidth = 50,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToOrderColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                AutoGenerateColumns = false,
                ColumnHeadersHeight = 40,
                RowTemplate = { Height = 35 },
                Font = new Font("Segoe UI", 9.5F),
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    SelectionBackColor = Color.FromArgb(92, 138, 58),
                    SelectionForeColor = Color.White,
                    BackColor = Color.White,
                    ForeColor = Color.FromArgb(33, 33, 33),
                    Padding = new Padding(5)
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(45, 57, 69),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Padding = new Padding(5)
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(250, 250, 250)
                },
                EnableHeadersVisualStyles = false,
                AllowDrop = true
            };

            // Events dla drag & drop i edycji
            dataGridView1.MouseDown += DataGridView1_MouseDown;
            dataGridView1.DragOver += DataGridView1_DragOver;
            dataGridView1.DragDrop += DataGridView1_DragDrop;
            dataGridView1.SelectionChanged += (s, e) => UpdateStatistics();
            dataGridView1.CellEndEdit += (s, e) => UpdateStatistics();

            // Dodanie kontrolek do formularza
            this.Controls.Add(dataGridView1);
            this.Controls.Add(toolbarPanel);
            this.Controls.Add(headerPanel);
        }

        private GroupBox CreateStyledGroupBox(string title, int x, int y, int width, int height)
        {
            return new GroupBox
            {
                Text = title,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(45, 57, 69),
                Location = new Point(x, y),
                Size = new Size(width, height),
                FlatStyle = FlatStyle.Flat
            };
        }

        private Button CreateIconButton(string icon, string tooltip, int x, int y, int width, int height)
        {
            var btn = new Button
            {
                Text = icon,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Location = new Point(x, y),
                Size = new Size(width, height),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(236, 239, 241),
                ForeColor = Color.FromArgb(45, 57, 69),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(189, 195, 199);
            btn.FlatAppearance.BorderSize = 1;

            ToolTip toolTip = new ToolTip();
            toolTip.SetToolTip(btn, tooltip);

            return btn;
        }

        private Button CreateActionButton(string icon, string text, int x, int y, int width, int height, Color color)
        {
            var btn = new Button
            {
                Text = $"{icon} {text}",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(x, y),
                Size = new Size(width, height),
                FlatStyle = FlatStyle.Flat,
                BackColor = color,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btn.FlatAppearance.BorderSize = 0;

            // Hover effect
            btn.MouseEnter += (s, e) => btn.BackColor = ControlPaint.Dark(color, 0.1f);
            btn.MouseLeave += (s, e) => btn.BackColor = color;

            return btn;
        }

        private Label CreateStatLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(45, 57, 69),
                AutoSize = true,
                Location = new Point(x, y)
            };
        }

        private void LoadComboBoxData()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Pobierz kierowców
                    string driverQuery = @"
                        SELECT GID, [Name]
                        FROM [LibraNet].[dbo].[Driver]
                        WHERE Deleted = 0
                        ORDER BY Name ASC";
                    SqlDataAdapter driverAdapter = new SqlDataAdapter(driverQuery, connection);
                    driverTable = new DataTable();
                    driverAdapter.Fill(driverTable);

                    // Dodaj pusty wiersz na początku
                    DataRow emptyDriverRow = driverTable.NewRow();
                    emptyDriverRow["GID"] = DBNull.Value;
                    emptyDriverRow["Name"] = "";
                    driverTable.Rows.InsertAt(emptyDriverRow, 0);

                    // Pobierz ciągniki
                    string carQuery = @"
                        SELECT DISTINCT ID
                        FROM dbo.CarTrailer
                        WHERE kind = '1'
                        ORDER BY ID DESC";
                    SqlDataAdapter carAdapter = new SqlDataAdapter(carQuery, connection);
                    carTable = new DataTable();
                    carAdapter.Fill(carTable);

                    // Dodaj pusty wiersz
                    DataRow emptyCarRow = carTable.NewRow();
                    emptyCarRow["ID"] = "";
                    carTable.Rows.InsertAt(emptyCarRow, 0);

                    // Pobierz naczepy
                    string trailerQuery = @"
                        SELECT DISTINCT ID
                        FROM dbo.CarTrailer
                        WHERE kind = '2'
                        ORDER BY ID DESC";
                    SqlDataAdapter trailerAdapter = new SqlDataAdapter(trailerQuery, connection);
                    trailerTable = new DataTable();
                    trailerAdapter.Fill(trailerTable);

                    // Dodaj pusty wiersz
                    DataRow emptyTrailerRow = trailerTable.NewRow();
                    emptyTrailerRow["ID"] = "";
                    trailerTable.Rows.InsertAt(emptyTrailerRow, 0);

                    // Wózek
                    wozekTable = new DataTable();
                    wozekTable.Columns.Add("WozekValue", typeof(string));
                    wozekTable.Rows.Add("");
                    wozekTable.Rows.Add("Wieziesz wozek");
                    wozekTable.Rows.Add("Przywozisz wozek");
                    wozekTable.Rows.Add("Wozek w obie strony");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Błąd podczas ładowania danych słownikowych:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void DisplayData()
        {
            try
            {
                if (dataGridView1 == null || dateTimePicker1 == null)
                {
                    MessageBox.Show(
                        "Błąd krytyczny: Kontrolki nie zostały zainicjalizowane.",
                        "Błąd",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                // Wyczyść obecne dane
                dataGridView1.DataSource = null;
                dataGridView1.Columns.Clear();

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Sprawdź czy są dane w FarmerCalc
                    string checkQuery = "SELECT COUNT(*) FROM [LibraNet].[dbo].[FarmerCalc] WHERE CalcDate = @SelectedDate";
                    SqlCommand checkCommand = new SqlCommand(checkQuery, connection);
                    checkCommand.Parameters.AddWithValue("@SelectedDate", dateTimePicker1.Value.Date);
                    int count = (int)checkCommand.ExecuteScalar();

                    DataTable table = new DataTable();
                    bool isFarmerCalc = count > 0;

                    if (isFarmerCalc)
                    {
                        // Dane z FarmerCalc
                        string query = @"
                            SELECT
                                ID,
                                LpDostawy,
                                CustomerGID,
                                WagaDek,
                                SztPoj,
                                DriverGID,
                                CarID,
                                TrailerID,
                                Wyjazd,
                                Zaladunek,
                                Przyjazd,
                                NotkaWozek
                            FROM [LibraNet].[dbo].[FarmerCalc]
                            WHERE CalcDate = @SelectedDate
                            ORDER BY LpDostawy";

                        SqlCommand command = new SqlCommand(query, connection);
                        command.Parameters.AddWithValue("@SelectedDate", dateTimePicker1.Value.Date);
                        SqlDataAdapter adapter = new SqlDataAdapter(command);
                        adapter.Fill(table);
                    }
                    else
                    {
                        // Dane z HarmonogramDostaw
                        string query = @"
                            SELECT
                                CAST(0 AS BIGINT) AS ID,
                                Lp AS LpDostawy,
                                Dostawca AS CustomerGID,
                                WagaDek,
                                SztSzuflada AS SztPoj,
                                CAST(NULL AS INT) AS DriverGID,
                                CAST(NULL AS VARCHAR(50)) AS CarID,
                                CAST(NULL AS VARCHAR(50)) AS TrailerID,
                                CAST(NULL AS DATETIME) AS Wyjazd,
                                CAST(NULL AS DATETIME) AS Zaladunek,
                                CAST(NULL AS DATETIME) AS Przyjazd,
                                CAST(NULL AS VARCHAR(100)) AS NotkaWozek
                            FROM dbo.HarmonogramDostaw
                            WHERE DataOdbioru = @StartDate
                            AND Bufor = 'Potwierdzony'
                            ORDER BY Lp";

                        SqlCommand command = new SqlCommand(query, connection);
                        command.Parameters.AddWithValue("@StartDate", dateTimePicker1.Value.Date);
                        SqlDataAdapter adapter = new SqlDataAdapter(command);
                        adapter.Fill(table);
                    }

                    if (table.Rows.Count == 0)
                    {
                        MessageBox.Show(
                            "Brak danych do wyświetlenia na wybrany dzień.",
                            "Informacja",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }

                    // Konfiguruj kolumny PRZED ustawieniem DataSource
                    SetupDataGridColumns();

                    // Ustaw DataSource
                    dataGridView1.DataSource = table;

                    // Konfiguruj widoczność i szerokości kolumn
                    ConfigureColumnProperties();

                    UpdateStatistics();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Błąd podczas ładowania danych:\n\n{ex.Message}",
                    "Błąd",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void SetupDataGridColumns()
        {
            dataGridView1.Columns.Clear();
            dataGridView1.AutoGenerateColumns = false;

            // ID (ukryta)
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ID",
                DataPropertyName = "ID",
                HeaderText = "ID",
                Visible = false
            });

            // LP Dostawy
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "LpDostawy",
                DataPropertyName = "LpDostawy",
                HeaderText = "LP Dostawy",
                Width = 100,
                ReadOnly = true
            });

            // Hodowca
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CustomerGID",
                DataPropertyName = "CustomerGID",
                HeaderText = "Hodowca",
                Width = 200
            });

            // Waga (kg)
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "WagaDek",
                DataPropertyName = "WagaDek",
                HeaderText = "Waga (kg)",
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" }
            });

            // Sztuk
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "SztPoj",
                DataPropertyName = "SztPoj",
                HeaderText = "Sztuk",
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N0" }
            });

            // Kierowca (ComboBox)
            var driverColumn = new DataGridViewComboBoxColumn
            {
                Name = "DriverGID",
                DataPropertyName = "DriverGID",
                HeaderText = "Kierowca",
                Width = 180,
                DataSource = driverTable,
                DisplayMember = "Name",
                ValueMember = "GID",
                DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
                FlatStyle = FlatStyle.Flat
            };
            dataGridView1.Columns.Add(driverColumn);

            // Ciągnik (ComboBox)
            var carColumn = new DataGridViewComboBoxColumn
            {
                Name = "CarID",
                DataPropertyName = "CarID",
                HeaderText = "Ciągnik",
                Width = 120,
                DataSource = carTable,
                DisplayMember = "ID",
                ValueMember = "ID",
                DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
                FlatStyle = FlatStyle.Flat
            };
            dataGridView1.Columns.Add(carColumn);

            // Naczepa (ComboBox)
            var trailerColumn = new DataGridViewComboBoxColumn
            {
                Name = "TrailerID",
                DataPropertyName = "TrailerID",
                HeaderText = "Naczepa",
                Width = 120,
                DataSource = trailerTable,
                DisplayMember = "ID",
                ValueMember = "ID",
                DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
                FlatStyle = FlatStyle.Flat
            };
            dataGridView1.Columns.Add(trailerColumn);

            // Wyjazd
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Wyjazd",
                DataPropertyName = "Wyjazd",
                HeaderText = "Wyjazd",
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "HH:mm" }
            });

            // Załadunek
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Zaladunek",
                DataPropertyName = "Zaladunek",
                HeaderText = "Załadunek",
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "HH:mm" }
            });

            // Przyjazd
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Przyjazd",
                DataPropertyName = "Przyjazd",
                HeaderText = "Przyjazd",
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "HH:mm" }
            });

            // Wózek (ComboBox)
            var wozekColumn = new DataGridViewComboBoxColumn
            {
                Name = "NotkaWozek",
                DataPropertyName = "NotkaWozek",
                HeaderText = "Wózek",
                Width = 180,
                DataSource = wozekTable,
                DisplayMember = "WozekValue",
                ValueMember = "WozekValue",
                DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
                FlatStyle = FlatStyle.Flat
            };
            dataGridView1.Columns.Add(wozekColumn);
        }

        private void ConfigureColumnProperties()
        {
            // Dodatkowa konfiguracja kolumn po ustawieniu DataSource
            foreach (DataGridViewColumn column in dataGridView1.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
            }
        }

        private void UpdateStatistics()
        {
            try
            {
                if (dataGridView1.DataSource == null)
                {
                    lblRecordCount.Text = "Rekordów: 0";
                    lblTotalWeight.Text = "Waga: 0 kg";
                    lblTotalPieces.Text = "Sztuk: 0";
                    return;
                }

                DataTable dt = dataGridView1.DataSource as DataTable;
                if (dt == null || dt.Rows.Count == 0)
                {
                    lblRecordCount.Text = "Rekordów: 0";
                    lblTotalWeight.Text = "Waga: 0 kg";
                    lblTotalPieces.Text = "Sztuk: 0";
                    return;
                }

                int recordCount = dt.Rows.Count;
                decimal totalWeight = 0;
                int totalPieces = 0;

                foreach (DataRow row in dt.Rows)
                {
                    if (row["WagaDek"] != null && row["WagaDek"] != DBNull.Value)
                    {
                        if (decimal.TryParse(row["WagaDek"].ToString(), out decimal weight))
                        {
                            totalWeight += weight;
                        }
                    }

                    if (row["SztPoj"] != null && row["SztPoj"] != DBNull.Value)
                    {
                        if (int.TryParse(row["SztPoj"].ToString(), out int pieces))
                        {
                            totalPieces += pieces;
                        }
                    }
                }

                lblRecordCount.Text = $"Rekordów: {recordCount:N0}";
                lblTotalWeight.Text = $"Waga: {totalWeight:N0} kg";
                lblTotalPieces.Text = $"Sztuk: {totalPieces:N0}";
            }
            catch (Exception ex)
            {
                lblRecordCount.Text = "Rekordów: Błąd";
                lblTotalWeight.Text = "Waga: Błąd";
                lblTotalPieces.Text = "Sztuk: Błąd";
                System.Diagnostics.Debug.WriteLine($"Błąd w UpdateStatistics: {ex.Message}");
            }
        }

        // Event handlers
        private void DateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
            DisplayData();
        }

        private void BtnPreviousDay_Click(object sender, EventArgs e)
        {
            dateTimePicker1.Value = dateTimePicker1.Value.AddDays(-1);
        }

        private void BtnNextDay_Click(object sender, EventArgs e)
        {
            dateTimePicker1.Value = dateTimePicker1.Value.AddDays(1);
        }

        private void BtnMoveUp_Click(object sender, EventArgs e)
        {
            MoveRow(-1);
        }

        private void BtnMoveDown_Click(object sender, EventArgs e)
        {
            MoveRow(1);
        }

        private void MoveRow(int direction)
        {
            try
            {
                if (dataGridView1.SelectedRows.Count == 0)
                {
                    MessageBox.Show("Proszę zaznaczyć wiersz do przesunięcia.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                int selectedIndex = dataGridView1.SelectedRows[0].Index;
                int newIndex = selectedIndex + direction;

                DataTable dt = dataGridView1.DataSource as DataTable;
                if (dt == null || newIndex < 0 || newIndex >= dt.Rows.Count)
                {
                    return;
                }

                // Skopiuj dane wiersza
                DataRow row = dt.Rows[selectedIndex];
                DataRow newRow = dt.NewRow();
                newRow.ItemArray = row.ItemArray.Clone() as object[];

                // Usuń stary wiersz i wstaw w nowym miejscu
                dt.Rows.RemoveAt(selectedIndex);
                dt.Rows.InsertAt(newRow, newIndex);

                // Zaznacz przesunięty wiersz
                dataGridView1.ClearSelection();
                dataGridView1.Rows[newIndex].Selected = true;
                dataGridView1.FirstDisplayedScrollingRowIndex = newIndex;

                UpdateStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas przesuwania wiersza:\n{ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnAddRow_Click(object sender, EventArgs e)
        {
            try
            {
                DataTable dt = dataGridView1.DataSource as DataTable;
                if (dt != null)
                {
                    DataRow newRow = dt.NewRow();
                    dt.Rows.Add(newRow);
                    UpdateStatistics();

                    // Zaznacz nowy wiersz
                    if (dataGridView1.Rows.Count > 0)
                    {
                        int lastIndex = dataGridView1.Rows.Count - 1;
                        dataGridView1.ClearSelection();
                        dataGridView1.Rows[lastIndex].Selected = true;
                        dataGridView1.FirstDisplayedScrollingRowIndex = lastIndex;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas dodawania wiersza:\n{ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDeleteRow_Click(object sender, EventArgs e)
        {
            try
            {
                if (dataGridView1.SelectedRows.Count == 0)
                {
                    MessageBox.Show("Proszę zaznaczyć wiersz do usunięcia.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                DialogResult result = MessageBox.Show(
                    "Czy na pewno chcesz usunąć zaznaczony wiersz?",
                    "Potwierdzenie usunięcia",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    DataTable dt = dataGridView1.DataSource as DataTable;
                    if (dt != null)
                    {
                        int selectedIndex = dataGridView1.SelectedRows[0].Index;
                        dt.Rows.RemoveAt(selectedIndex);
                        UpdateStatistics();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas usuwania wiersza:\n{ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Drag & Drop functionality
        private void DataGridView1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var hitTest = dataGridView1.HitTest(e.X, e.Y);
                if (hitTest.RowIndex >= 0 && hitTest.Type == DataGridViewHitTestType.RowHeader)
                {
                    dragRowIndex = hitTest.RowIndex;
                    dataGridView1.DoDragDrop(dataGridView1.Rows[dragRowIndex], DragDropEffects.Move);
                }
            }
        }

        private void DataGridView1_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void DataGridView1_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                Point clientPoint = dataGridView1.PointToClient(new Point(e.X, e.Y));
                int targetIndex = dataGridView1.HitTest(clientPoint.X, clientPoint.Y).RowIndex;

                if (targetIndex >= 0 && targetIndex != dragRowIndex)
                {
                    DataTable dt = dataGridView1.DataSource as DataTable;
                    if (dt != null)
                    {
                        DataRow row = dt.Rows[dragRowIndex];
                        DataRow newRow = dt.NewRow();
                        newRow.ItemArray = row.ItemArray.Clone() as object[];

                        dt.Rows.RemoveAt(dragRowIndex);
                        dt.Rows.InsertAt(newRow, targetIndex);

                        dataGridView1.ClearSelection();
                        dataGridView1.Rows[targetIndex].Selected = true;
                        UpdateStatistics();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas przeciągania wiersza:\n{ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                dragRowIndex = -1;
            }
        }

        private void BtnSaveToDatabase_Click(object sender, EventArgs e)
        {
            DialogResult confirmResult = MessageBox.Show(
                "Czy na pewno chcesz zapisać dane do bazy?\n\nOperacja nadpisze istniejące dane dla tego dnia.",
                "Potwierdzenie zapisu",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirmResult != DialogResult.Yes)
                return;

            try
            {
                DataTable dt = dataGridView1.DataSource as DataTable;
                if (dt == null || dt.Rows.Count == 0)
                {
                    MessageBox.Show("Brak danych do zapisania.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string sql = @"INSERT INTO dbo.FarmerCalc
                        (ID, CalcDate, CustomerGID, CustomerRealGID, DriverGID, LpDostawy, SztPoj, WagaDek,
                         CarID, TrailerID, NotkaWozek, Wyjazd, Zaladunek, Przyjazd, Price,
                         Loss, PriceTypeID)
                        VALUES
                        (@ID, @Date, @Dostawca, @Dostawca, @Kierowca, @LpDostawy, @SztPoj, @WagaDek,
                         @Ciagnik, @Naczepa, @NotkaWozek, @Wyjazd, @Zaladunek,
                         @Przyjazd, @Cena, @Ubytek, @TypCeny)";

                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            int savedCount = 0;

                            foreach (DataRow row in dt.Rows)
                            {
                                string Dostawca = row["CustomerGID"]?.ToString() ?? "";
                                string Kierowca = row["DriverGID"]?.ToString() ?? "";
                                string LpDostawy = row["LpDostawy"]?.ToString() ?? "";
                                string SztPoj = row["SztPoj"]?.ToString() ?? "";
                                string WagaDek = row["WagaDek"]?.ToString() ?? "";
                                string Ciagnik = row["CarID"]?.ToString() ?? "";
                                string Naczepa = row["TrailerID"]?.ToString() ?? "";
                                string NotkaWozek = row["NotkaWozek"]?.ToString() ?? "";

                                string StringPrzyjazd = row["Przyjazd"]?.ToString() ?? "";
                                string StringZaladunek = row["Zaladunek"]?.ToString() ?? "";
                                string StringWyjazd = row["Wyjazd"]?.ToString() ?? "";

                                // Pobierz dodatkowe dane
                                double Ubytek = 0.0;
                                if (!string.IsNullOrWhiteSpace(LpDostawy))
                                {
                                    double.TryParse(zapytaniasql.PobierzInformacjeZBazyDanychKonkretne(LpDostawy, "Ubytek"), out Ubytek);
                                }

                                double Cena = 0.0;
                                if (!string.IsNullOrWhiteSpace(LpDostawy))
                                {
                                    double.TryParse(zapytaniasql.PobierzInformacjeZBazyDanychKonkretne(LpDostawy, "Cena"), out Cena);
                                }

                                string typCeny = zapytaniasql.PobierzInformacjeZBazyDanychKonkretne(LpDostawy, "TypCeny");
                                int intTypCeny = zapytaniasql.ZnajdzIdCeny(typCeny);

                                int userId = zapytaniasql.ZnajdzIdKierowcy(Kierowca);
                                int userId2 = zapytaniasql.ZnajdzIdHodowcy(Dostawca);

                                // Formatowanie godzin
                                StringWyjazd = zapytaniasql.DodajDwukropek(StringWyjazd);
                                StringZaladunek = zapytaniasql.DodajDwukropek(StringZaladunek);
                                StringPrzyjazd = zapytaniasql.DodajDwukropek(StringPrzyjazd);

                                DateTime data = dateTimePicker1.Value;
                                DateTime combinedDateTimeWyjazd = ZapytaniaSQL.CombineDateAndTime(StringWyjazd, data);
                                DateTime combinedDateTimeZaladunek = ZapytaniaSQL.CombineDateAndTime(StringZaladunek, data);
                                DateTime combinedDateTimePrzyjazd = ZapytaniaSQL.CombineDateAndTime(StringPrzyjazd, data);

                                // Znajdź nowe ID
                                long maxLP;
                                string maxLPSql = "SELECT MAX(ID) AS MaxLP FROM dbo.[FarmerCalc];";
                                using (SqlCommand command = new SqlCommand(maxLPSql, conn, transaction))
                                {
                                    object result = command.ExecuteScalar();
                                    maxLP = result == DBNull.Value ? 1 : Convert.ToInt64(result) + 1;
                                }

                                // Wstaw do bazy
                                using (SqlCommand cmd = new SqlCommand(sql, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ID", maxLP);
                                    cmd.Parameters.AddWithValue("@Dostawca", userId2);
                                    cmd.Parameters.AddWithValue("@Kierowca", userId);
                                    cmd.Parameters.AddWithValue("@LpDostawy", string.IsNullOrEmpty(LpDostawy) ? DBNull.Value : LpDostawy);
                                    cmd.Parameters.AddWithValue("@SztPoj", string.IsNullOrEmpty(SztPoj) ? DBNull.Value : (object)decimal.Parse(SztPoj));
                                    cmd.Parameters.AddWithValue("@WagaDek", string.IsNullOrEmpty(WagaDek) ? DBNull.Value : (object)decimal.Parse(WagaDek));
                                    cmd.Parameters.AddWithValue("@Date", data);

                                    cmd.Parameters.AddWithValue("@Wyjazd", combinedDateTimeWyjazd);
                                    cmd.Parameters.AddWithValue("@Zaladunek", combinedDateTimeZaladunek);
                                    cmd.Parameters.AddWithValue("@Przyjazd", combinedDateTimePrzyjazd);

                                    cmd.Parameters.AddWithValue("@Cena", Cena);
                                    cmd.Parameters.AddWithValue("@Ubytek", Ubytek);
                                    cmd.Parameters.AddWithValue("@TypCeny", intTypCeny);

                                    cmd.Parameters.AddWithValue("@Ciagnik", string.IsNullOrEmpty(Ciagnik) ? DBNull.Value : (object)Ciagnik);
                                    cmd.Parameters.AddWithValue("@Naczepa", string.IsNullOrEmpty(Naczepa) ? DBNull.Value : (object)Naczepa);
                                    cmd.Parameters.AddWithValue("@NotkaWozek", string.IsNullOrEmpty(NotkaWozek) ? DBNull.Value : (object)NotkaWozek);

                                    cmd.ExecuteNonQuery();
                                    savedCount++;
                                }
                            }

                            transaction.Commit();

                            MessageBox.Show(
                                $"✓ Pomyślnie zapisano {savedCount} rekordów do bazy danych.",
                                "Sukces",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);

                            DisplayData();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();

                            MessageBox.Show(
                                $"Wystąpił błąd podczas zapisywania danych:\n\n{ex.Message}",
                                "Błąd",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Wystąpił błąd połączenia z bazą danych:\n\n{ex.Message}",
                    "Błąd",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
