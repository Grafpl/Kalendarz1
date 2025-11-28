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

        private bool dragging = false;
        private int rowIndexFromMouseDown;
        private DataGridViewRow draggedRow;

        public WidokMatrycaNowy()
        {
            InitializeComponent();
            InitializeCustomUI();
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
                BackColor = Color.FromArgb(92, 138, 58) // #5C8A3A - zielony z Menu1
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

            // DataGridView
            dataGridView1 = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(230, 230, 230),
                RowHeadersVisible = true,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AllowUserToOrderColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
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
                EnableHeadersVisualStyles = false
            };

            // Events dla drag & drop
            dataGridView1.MouseDown += DataGridView1_MouseDown;
            dataGridView1.MouseMove += DataGridView1_MouseMove;
            dataGridView1.DragOver += DataGridView1_DragOver;
            dataGridView1.DragDrop += DataGridView1_DragDrop;
            dataGridView1.AllowDrop = true;
            dataGridView1.SelectionChanged += (s, e) => UpdateStatistics();

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

        private void DisplayData()
        {
            try
            {
                // Sprawdzenie czy kontrolki są zainicjalizowane
                if (dataGridView1 == null)
                {
                    MessageBox.Show(
                        "Błąd krytyczny: DataGridView nie został zainicjalizowany.\n" +
                        "Spróbuj ponownie otworzyć formularz.",
                        "Błąd inicjalizacji",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                if (dateTimePicker1 == null)
                {
                    MessageBox.Show(
                        "Błąd krytyczny: DateTimePicker nie został zainicjalizowany.\n" +
                        "Spróbuj ponownie otworzyć formularz.",
                        "Błąd inicjalizacji",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Pobierz listę kierowców
                    string driverQuery = @"
                        SELECT GID, [Name]
                        FROM [LibraNet].[dbo].[Driver]
                        WHERE Deleted = 0
                        ORDER BY name ASC";

                    SqlDataAdapter driverAdapter = new SqlDataAdapter(driverQuery, connection);
                    DataTable driverTable = new DataTable();
                    driverAdapter.Fill(driverTable);

                    // Tabela CarID
                    string carQuery = @"
                        SELECT DISTINCT ID
                        FROM dbo.CarTrailer
                        WHERE kind = '1'
                        ORDER BY ID DESC";

                    SqlDataAdapter carAdapter = new SqlDataAdapter(carQuery, connection);
                    DataTable carTable = new DataTable();
                    carAdapter.Fill(carTable);

                    // Tabela TrailerID
                    string trailerQuery = @"
                        SELECT DISTINCT ID
                        FROM dbo.CarTrailer
                        WHERE kind = '2'
                        ORDER BY ID DESC";

                    SqlDataAdapter trailerAdapter = new SqlDataAdapter(trailerQuery, connection);
                    DataTable trailerTable = new DataTable();
                    trailerAdapter.Fill(trailerTable);

                    // Tabela Wózek
                    DataTable wozekTable = new DataTable();
                    wozekTable.Columns.Add("WozekValue", typeof(string));
                    wozekTable.Rows.Add("");
                    wozekTable.Rows.Add("Wieziesz wozek");
                    wozekTable.Rows.Add("Przywozisz wozek");
                    wozekTable.Rows.Add("Wozek w obie strony");

                    // Sprawdź czy są dane w FarmerCalc
                    string checkQuery = @"
                        SELECT COUNT(*) 
                        FROM [LibraNet].[dbo].[FarmerCalc] 
                        WHERE CalcDate = @SelectedDate";

                    SqlCommand checkCommand = new SqlCommand(checkQuery, connection);
                    checkCommand.Parameters.AddWithValue("@SelectedDate", dateTimePicker1.Value.Date);
                    int count = (int)checkCommand.ExecuteScalar();

                    DataTable table = new DataTable();
                    bool isFarmerCalc = false;

                    if (count > 0)
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
                        isFarmerCalc = true;
                    }
                    else
                    {
                        // Dane z HarmonogramDostaw - dodajemy wszystkie wymagane kolumny
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
                            "Brak danych do wyświetlenia na wybrany dzień.\n\n" +
                            "Możliwe przyczyny:\n" +
                            "• Brak potwierdzonych dostaw w Harmonogramie\n" +
                            "• Nieprawidłowa data\n" +
                            "• Dane jeszcze nie zostały wprowadzone",
                            "Informacja",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

                        // Utwórz pustą tabelę z odpowiednią strukturą
                        DataTable emptyTable = new DataTable();
                        emptyTable.Columns.Add("ID", typeof(long));
                        emptyTable.Columns.Add("LpDostawy", typeof(string));
                        emptyTable.Columns.Add("CustomerGID", typeof(string));
                        emptyTable.Columns.Add("WagaDek", typeof(decimal));
                        emptyTable.Columns.Add("SztPoj", typeof(int));
                        emptyTable.Columns.Add("DriverGID", typeof(string));
                        emptyTable.Columns.Add("CarID", typeof(string));
                        emptyTable.Columns.Add("TrailerID", typeof(string));
                        emptyTable.Columns.Add("Wyjazd", typeof(DateTime));
                        emptyTable.Columns.Add("Zaladunek", typeof(DateTime));
                        emptyTable.Columns.Add("Przyjazd", typeof(DateTime));
                        emptyTable.Columns.Add("NotkaWozek", typeof(string));

                        dataGridView1.DataSource = emptyTable;
                        UpdateStatistics();
                        return;
                    }

                    // Konfiguracja DataGridView
                    dataGridView1.DataSource = table;

                    // Sprawdzenie czy kolumny zostały utworzone
                    if (dataGridView1.Columns == null || dataGridView1.Columns.Count == 0)
                    {
                        MessageBox.Show(
                            "Błąd: Nie udało się utworzyć kolumn w tabeli.\n" +
                            "Sprawdź strukturę danych w bazie.",
                            "Błąd konfiguracji",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }

                    // Ukryj kolumnę ID jeśli istnieje
                    if (dataGridView1.Columns.Contains("ID"))
                    {
                        dataGridView1.Columns["ID"].Visible = false;
                    }

                    ConfigureColumn("LpDostawy", "LP Dostawy", 100, true);
                    ConfigureColumn("CustomerGID", "Hodowca", 200, false);
                    ConfigureColumn("WagaDek", "Waga (kg)", 100, false);
                    ConfigureColumn("SztPoj", "Sztuk", 100, false);

                    // Konfiguracja kolumn z ComboBox
                    ConfigureComboBoxColumn("DriverGID", "Kierowca", 180, driverTable, "Name", "GID", isFarmerCalc);
                    ConfigureComboBoxColumn("CarID", "Ciągnik", 120, carTable, "ID", "ID", isFarmerCalc);
                    ConfigureComboBoxColumn("TrailerID", "Naczepa", 120, trailerTable, "ID", "ID", isFarmerCalc);

                    // Kolumny czasowe
                    ConfigureTimeColumn("Wyjazd", "Wyjazd", 100, isFarmerCalc);
                    ConfigureTimeColumn("Zaladunek", "Załadunek", 100, isFarmerCalc);
                    ConfigureTimeColumn("Przyjazd", "Przyjazd", 100, isFarmerCalc);

                    ConfigureComboBoxColumn("NotkaWozek", "Wózek", 150, wozekTable, "WozekValue", "WozekValue", isFarmerCalc);

                    dataGridView1.Refresh();
                    UpdateStatistics();
                }
            }
            catch (SqlException sqlEx)
            {
                MessageBox.Show(
                    $"Błąd połączenia z bazą danych:\n\n" +
                    $"Komunikat: {sqlEx.Message}\n" +
                    $"Numer błędu: {sqlEx.Number}\n\n" +
                    $"Sprawdź:\n" +
                    $"• Czy serwer SQL jest dostępny (192.168.0.109)\n" +
                    $"• Czy masz uprawnienia do bazy LibraNet\n" +
                    $"• Czy tabele FarmerCalc i HarmonogramDostaw istnieją",
                    "Błąd SQL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                System.Diagnostics.Debug.WriteLine($"SQL Error in DisplayData: {sqlEx.ToString()}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Wystąpił błąd podczas ładowania danych:\n\n" +
                    $"Typ: {ex.GetType().Name}\n" +
                    $"Komunikat: {ex.Message}\n" +
                    $"Źródło: {ex.Source}\n\n" +
                    $"Stack Trace (dla debugowania):\n{ex.StackTrace}",
                    "Błąd",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                System.Diagnostics.Debug.WriteLine($"Error in DisplayData: {ex.ToString()}");

                // Próba ustawienia pustej tabeli aby aplikacja nie crashowała
                try
                {
                    DataTable emptyTable = new DataTable();
                    emptyTable.Columns.Add("ID", typeof(long));
                    emptyTable.Columns.Add("LpDostawy", typeof(string));
                    emptyTable.Columns.Add("CustomerGID", typeof(string));
                    emptyTable.Columns.Add("WagaDek", typeof(decimal));
                    emptyTable.Columns.Add("SztPoj", typeof(int));
                    emptyTable.Columns.Add("DriverGID", typeof(string));
                    emptyTable.Columns.Add("CarID", typeof(string));
                    emptyTable.Columns.Add("TrailerID", typeof(string));
                    emptyTable.Columns.Add("Wyjazd", typeof(DateTime));
                    emptyTable.Columns.Add("Zaladunek", typeof(DateTime));
                    emptyTable.Columns.Add("Przyjazd", typeof(DateTime));
                    emptyTable.Columns.Add("NotkaWozek", typeof(string));

                    dataGridView1.DataSource = emptyTable;
                    UpdateStatistics();
                }
                catch
                {
                    // Jeśli nawet to się nie uda, po prostu ignoruj
                }
            }
        }

        private void ConfigureColumn(string columnName, string headerText, int width, bool readOnly)
        {
            try
            {
                if (dataGridView1 == null || dataGridView1.Columns == null)
                {
                    System.Diagnostics.Debug.WriteLine($"ConfigureColumn: DataGridView lub Columns jest null dla {columnName}");
                    return;
                }

                if (!dataGridView1.Columns.Contains(columnName))
                {
                    System.Diagnostics.Debug.WriteLine($"ConfigureColumn: Kolumna {columnName} nie istnieje");
                    return;
                }

                DataGridViewColumn column = dataGridView1.Columns[columnName];
                if (column == null)
                {
                    System.Diagnostics.Debug.WriteLine($"ConfigureColumn: Kolumna {columnName} jest null mimo że Contains zwraca true");
                    return;
                }

                // Sprawdź czy kolumna jest dołączona do DataGridView i czy DataGridView jest zainicjalizowany
                if (column.DataGridView == null || !dataGridView1.IsHandleCreated)
                {
                    System.Diagnostics.Debug.WriteLine($"ConfigureColumn: Kolumna {columnName} nie jest dołączona do DataGridView lub Handle nie jest utworzony");
                    return;
                }

                // Użyj BeginInvoke aby uniknąć problemów z ustawianiem szerokości podczas bindingu
                if (dataGridView1.InvokeRequired)
                {
                    dataGridView1.BeginInvoke(new Action(() =>
                    {
                        SafeSetColumnProperties(column, headerText, width, readOnly);
                    }));
                }
                else
                {
                    SafeSetColumnProperties(column, headerText, width, readOnly);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd w ConfigureColumn dla {columnName}: {ex.Message}\n{ex.StackTrace}");
                // Nie pokazuj MessageBox - to może być wywołane wiele razy
            }
        }

        private void SafeSetColumnProperties(DataGridViewColumn column, string headerText, int width, bool readOnly)
        {
            try
            {
                if (column == null || column.DataGridView == null)
                    return;

                column.HeaderText = headerText;

                // Ustaw MinimumWidth przed Width aby uniknąć błędów
                if (width > 0)
                {
                    column.MinimumWidth = Math.Min(width, 50);
                    column.Width = width;
                }

                column.ReadOnly = readOnly;
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"SafeSetColumnProperties InvalidOperation: {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SafeSetColumnProperties Error: {ex.Message}");
            }
        }

        private void ConfigureComboBoxColumn(string columnName, string headerText, int width,
            DataTable dataSource, string displayMember, string valueMember, bool hasData)
        {
            try
            {
                if (dataGridView1 == null || dataGridView1.Columns == null)
                {
                    System.Diagnostics.Debug.WriteLine($"ConfigureComboBoxColumn: DataGridView jest null dla {columnName}");
                    return;
                }

                if (!dataGridView1.Columns.Contains(columnName))
                {
                    System.Diagnostics.Debug.WriteLine($"ConfigureComboBoxColumn: Kolumna {columnName} nie istnieje");
                    return;
                }

                int index = dataGridView1.Columns[columnName].Index;
                string dataPropertyName = dataGridView1.Columns[columnName].DataPropertyName;

                // Usuń starą kolumnę
                dataGridView1.Columns.Remove(columnName);

                // Utwórz nową kolumnę ComboBox
                DataGridViewComboBoxColumn comboColumn = new DataGridViewComboBoxColumn
                {
                    Name = columnName,
                    HeaderText = headerText,
                    DataSource = dataSource,
                    DisplayMember = displayMember,
                    ValueMember = valueMember,
                    DataPropertyName = string.IsNullOrEmpty(dataPropertyName) ? columnName : dataPropertyName,
                    Width = width,
                    DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
                    FlatStyle = FlatStyle.Flat
                };

                // Wstaw kolumnę w odpowiednim miejscu
                dataGridView1.Columns.Insert(index, comboColumn);

                // Jeśli nie ma danych, wyczyść wartości
                if (!hasData)
                {
                    foreach (DataGridViewRow row in dataGridView1.Rows)
                    {
                        if (!row.IsNewRow && row.Cells[columnName] != null)
                        {
                            row.Cells[columnName].Value = DBNull.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd w ConfigureComboBoxColumn dla {columnName}: {ex.Message}\n{ex.StackTrace}");

                // Komunikat tylko jeśli to poważny błąd
                if (ex is ArgumentOutOfRangeException || ex is InvalidOperationException)
                {
                    MessageBox.Show(
                        $"Uwaga: Nie udało się skonfigurować kolumny '{headerText}'.\n" +
                        $"Kolumna może nie działać poprawnie.\n\n" +
                        $"Szczegóły: {ex.Message}",
                        "Ostrzeżenie konfiguracji",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
        }

        private void ConfigureTimeColumn(string columnName, string headerText, int width, bool hasData)
        {
            try
            {
                if (dataGridView1 == null || dataGridView1.Columns == null)
                {
                    System.Diagnostics.Debug.WriteLine($"ConfigureTimeColumn: DataGridView jest null dla {columnName}");
                    return;
                }

                if (!dataGridView1.Columns.Contains(columnName))
                {
                    System.Diagnostics.Debug.WriteLine($"ConfigureTimeColumn: Kolumna {columnName} nie istnieje");
                    return;
                }

                DataGridViewColumn column = dataGridView1.Columns[columnName];
                if (column == null || column.DataGridView == null)
                {
                    System.Diagnostics.Debug.WriteLine($"ConfigureTimeColumn: Kolumna {columnName} jest null lub nie dołączona");
                    return;
                }

                // Bezpieczne ustawienie właściwości kolumny
                SafeSetColumnProperties(column, headerText, width, false);

                // Ustaw format daty
                if (column.DefaultCellStyle != null)
                {
                    column.DefaultCellStyle.Format = "HH:mm";
                }

                if (!hasData)
                {
                    foreach (DataGridViewRow row in dataGridView1.Rows)
                    {
                        if (!row.IsNewRow && row.Cells[columnName] != null)
                        {
                            row.Cells[columnName].Value = DBNull.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd w ConfigureTimeColumn dla {columnName}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void UpdateStatistics()
        {
            try
            {
                if (dataGridView1.DataSource == null || dataGridView1.Rows.Count == 0)
                {
                    lblRecordCount.Text = "Rekordów: 0";
                    lblTotalWeight.Text = "Waga: 0 kg";
                    lblTotalPieces.Text = "Sztuk: 0";
                    return;
                }

                int recordCount = 0;
                decimal totalWeight = 0;
                int totalPieces = 0;

                foreach (DataGridViewRow row in dataGridView1.Rows)
                {
                    if (!row.IsNewRow)
                    {
                        recordCount++;

                        if (row.Cells["WagaDek"].Value != null && row.Cells["WagaDek"].Value != DBNull.Value)
                        {
                            if (decimal.TryParse(row.Cells["WagaDek"].Value.ToString(), out decimal weight))
                            {
                                totalWeight += weight;
                            }
                        }

                        if (row.Cells["SztPoj"].Value != null && row.Cells["SztPoj"].Value != DBNull.Value)
                        {
                            if (int.TryParse(row.Cells["SztPoj"].Value.ToString(), out int pieces))
                            {
                                totalPieces += pieces;
                            }
                        }
                    }
                }

                lblRecordCount.Text = $"Rekordów: {recordCount}";
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
            UpdateStatistics();
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
            MoveRowUp();
        }

        private void BtnMoveDown_Click(object sender, EventArgs e)
        {
            MoveRowDown();
        }

        private void BtnAddRow_Click(object sender, EventArgs e)
        {
            try
            {
                if (dataGridView1.DataSource is DataTable dt)
                {
                    DataRow newRow = dt.NewRow();
                    dt.Rows.Add(newRow);
                    UpdateStatistics();

                    // Zaznacz nowy wiersz
                    if (dataGridView1.Rows.Count > 0)
                    {
                        int lastRowIndex = dataGridView1.Rows.Count - 2; // -2 bo ostatni to NewRow
                        if (lastRowIndex >= 0)
                        {
                            dataGridView1.ClearSelection();
                            dataGridView1.Rows[lastRowIndex].Selected = true;
                            if (dataGridView1.Rows[lastRowIndex].Cells.Count > 0)
                            {
                                dataGridView1.CurrentCell = dataGridView1.Rows[lastRowIndex].Cells[1]; // Kolumna LpDostawy
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show(
                        "Brak danych do edycji. Najpierw wybierz datę z danymi lub utwórz nowy harmonogram.",
                        "Informacja",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Błąd podczas dodawania wiersza:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void BtnDeleteRow_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count > 0 && !dataGridView1.SelectedRows[0].IsNewRow)
            {
                DialogResult result = MessageBox.Show(
                    "Czy na pewno chcesz usunąć zaznaczony wiersz?",
                    "Potwierdzenie usunięcia",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    dataGridView1.Rows.Remove(dataGridView1.SelectedRows[0]);
                    UpdateStatistics();
                }
            }
            else
            {
                MessageBox.Show(
                    "Proszę zaznaczyć wiersz do usunięcia.",
                    "Informacja",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void MoveRowUp()
        {
            try
            {
                if (dataGridView1.SelectedRows.Count > 0)
                {
                    int rowIndex = dataGridView1.SelectedRows[0].Index;
                    if (rowIndex > 0 && !dataGridView1.SelectedRows[0].IsNewRow)
                    {
                        DataTable dt = (DataTable)dataGridView1.DataSource;
                        if (dt == null) return;

                        DataRow row = dt.Rows[rowIndex];
                        dt.Rows.RemoveAt(rowIndex);
                        dt.Rows.InsertAt(row, rowIndex - 1);
                        dataGridView1.ClearSelection();
                        dataGridView1.Rows[rowIndex - 1].Selected = true;

                        if (dataGridView1.Rows[rowIndex - 1].Cells.Count > 0)
                        {
                            dataGridView1.CurrentCell = dataGridView1.Rows[rowIndex - 1].Cells[0];
                        }
                    }
                }
                else
                {
                    MessageBox.Show(
                        "Proszę zaznaczyć wiersz do przesunięcia.",
                        "Informacja",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Błąd podczas przesuwania wiersza w górę:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void MoveRowDown()
        {
            try
            {
                if (dataGridView1.SelectedRows.Count > 0)
                {
                    int rowIndex = dataGridView1.SelectedRows[0].Index;
                    DataTable dt = (DataTable)dataGridView1.DataSource;
                    if (dt == null) return;

                    if (rowIndex < dt.Rows.Count - 1 && !dataGridView1.SelectedRows[0].IsNewRow)
                    {
                        DataRow row = dt.Rows[rowIndex];
                        dt.Rows.RemoveAt(rowIndex);
                        dt.Rows.InsertAt(row, rowIndex + 1);
                        dataGridView1.ClearSelection();
                        dataGridView1.Rows[rowIndex + 1].Selected = true;

                        if (dataGridView1.Rows[rowIndex + 1].Cells.Count > 0)
                        {
                            dataGridView1.CurrentCell = dataGridView1.Rows[rowIndex + 1].Cells[0];
                        }
                    }
                }
                else
                {
                    MessageBox.Show(
                        "Proszę zaznaczyć wiersz do przesunięcia.",
                        "Informacja",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Błąd podczas przesuwania wiersza w dół:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // Drag & Drop functionality
        private void DataGridView1_MouseDown(object sender, MouseEventArgs e)
        {
            rowIndexFromMouseDown = dataGridView1.HitTest(e.X, e.Y).RowIndex;
            if (rowIndexFromMouseDown != -1 && e.Button == MouseButtons.Left)
            {
                draggedRow = dataGridView1.Rows[rowIndexFromMouseDown];
                dragging = true;
            }
        }

        private void DataGridView1_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging && e.Button == MouseButtons.Left)
            {
                dataGridView1.DoDragDrop(draggedRow, DragDropEffects.Move);
            }
        }

        private void DataGridView1_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void DataGridView1_DragDrop(object sender, DragEventArgs e)
        {
            Point clientPoint = dataGridView1.PointToClient(new Point(e.X, e.Y));
            int targetIndex = dataGridView1.HitTest(clientPoint.X, clientPoint.Y).RowIndex;

            if (targetIndex != -1 && targetIndex != rowIndexFromMouseDown && !draggedRow.IsNewRow)
            {
                DataTable dt = (DataTable)dataGridView1.DataSource;
                DataRow sourceRow = dt.Rows[rowIndexFromMouseDown];
                DataRow newRow = dt.NewRow();

                foreach (DataColumn col in dt.Columns)
                {
                    newRow[col] = sourceRow[col];
                }

                dt.Rows.RemoveAt(rowIndexFromMouseDown);
                dt.Rows.InsertAt(newRow, targetIndex);

                dataGridView1.ClearSelection();
                dataGridView1.Rows[targetIndex].Selected = true;
            }

            dragging = false;
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

                            foreach (DataGridViewRow row in dataGridView1.Rows)
                            {
                                if (!row.IsNewRow)
                                {
                                    string Dostawca = row.Cells["CustomerGID"].Value?.ToString() ?? "";
                                    string Kierowca = row.Cells["DriverGID"].Value?.ToString() ?? "";
                                    string LpDostawy = row.Cells["LpDostawy"].Value?.ToString() ?? "";
                                    string SztPoj = row.Cells["SztPoj"].Value?.ToString() ?? "";
                                    string WagaDek = row.Cells["WagaDek"].Value?.ToString() ?? "";
                                    string Ciagnik = row.Cells["CarID"].Value?.ToString() ?? "";
                                    string Naczepa = row.Cells["TrailerID"].Value?.ToString() ?? "";
                                    string NotkaWozek = row.Cells["NotkaWozek"].Value?.ToString() ?? "";

                                    string StringPrzyjazd = row.Cells["Przyjazd"].Value?.ToString() ?? "";
                                    string StringZaladunek = row.Cells["Zaladunek"].Value?.ToString() ?? "";
                                    string StringWyjazd = row.Cells["Wyjazd"].Value?.ToString() ?? "";

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
                                        cmd.Parameters.AddWithValue("@SztPoj", string.IsNullOrEmpty(SztPoj) ? DBNull.Value : decimal.Parse(SztPoj));
                                        cmd.Parameters.AddWithValue("@WagaDek", string.IsNullOrEmpty(WagaDek) ? DBNull.Value : decimal.Parse(WagaDek));
                                        cmd.Parameters.AddWithValue("@Date", data);

                                        cmd.Parameters.AddWithValue("@Wyjazd", combinedDateTimeWyjazd);
                                        cmd.Parameters.AddWithValue("@Zaladunek", combinedDateTimeZaladunek);
                                        cmd.Parameters.AddWithValue("@Przyjazd", combinedDateTimePrzyjazd);

                                        cmd.Parameters.AddWithValue("@Cena", Cena);
                                        cmd.Parameters.AddWithValue("@Ubytek", Ubytek);
                                        cmd.Parameters.AddWithValue("@TypCeny", intTypCeny);

                                        cmd.Parameters.AddWithValue("@Ciagnik", string.IsNullOrEmpty(Ciagnik) ? DBNull.Value : Ciagnik);
                                        cmd.Parameters.AddWithValue("@Naczepa", string.IsNullOrEmpty(Naczepa) ? DBNull.Value : Naczepa);
                                        cmd.Parameters.AddWithValue("@NotkaWozek", string.IsNullOrEmpty(NotkaWozek) ? DBNull.Value : NotkaWozek);

                                        cmd.ExecuteNonQuery();
                                        savedCount++;
                                    }
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