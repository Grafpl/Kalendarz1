using System;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    public partial class AnalizaTygodniowaForm : Form
    {
        private string connectionString;
        private int kontrahentId;
        private int? towarId;
        private string nazwaKontrahenta;
        private string nazwaTowaru;

        private DataGridView dataGridViewAnaliza;
        private Label lblTitle;
        private Label lblSubtitle;
        private Button btnExport;
        private Button btnClose;
        private Panel panelHeader;
        private Panel panelFooter;

        // Kolory motywu
        private readonly Color primaryColor = Color.FromArgb(155, 89, 182);
        private readonly Color accentColor = Color.FromArgb(46, 204, 113);

        public AnalizaTygodniowaForm(string connString, int khId, int? twId, string khNazwa, string twNazwa = null)
        {
            connectionString = connString;
            kontrahentId = khId;
            towarId = twId;
            nazwaKontrahenta = khNazwa;
            nazwaTowaru = twNazwa ?? "Wszystkie towary";

            InitializeComponent();
            ConfigureForm();
            LoadData();
        }

        private void InitializeComponent()
        {
            this.dataGridViewAnaliza = new DataGridView();
            this.lblTitle = new Label();
            this.lblSubtitle = new Label();
            this.btnExport = new Button();
            this.btnClose = new Button();
            this.panelHeader = new Panel();
            this.panelFooter = new Panel();

            this.SuspendLayout();

            // Form
            this.Text = "📊 Analiza Tygodniowa Sprzedaży";
            this.Size = new Size(1200, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(1000, 400);
            this.Icon = SystemIcons.Application;
            this.BackColor = Color.FromArgb(248, 249, 250);

            // Panel Header
            this.panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.White
            };

            // Gradient dla nagłówka
            this.panelHeader.Paint += (s, e) =>
            {
                using (var brush = new LinearGradientBrush(
                    panelHeader.ClientRectangle,
                    primaryColor,
                    Color.FromArgb(142, 68, 173),
                    90f))
                {
                    e.Graphics.FillRectangle(brush, panelHeader.ClientRectangle);
                }
            };

            // Tytuł
            this.lblTitle = new Label
            {
                Text = "📊 Analiza Tygodniowa Sprzedaży",
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 10),
                AutoSize = true
            };

            // Podtytuł
            this.lblSubtitle = new Label
            {
                Text = $"Kontrahent: {nazwaKontrahenta} | Towar: {nazwaTowaru}",
                Font = new Font("Segoe UI", 11f, FontStyle.Regular),
                ForeColor = Color.FromArgb(230, 230, 230),
                Location = new Point(20, 45),
                AutoSize = true
            };

            this.panelHeader.Controls.Add(this.lblTitle);
            this.panelHeader.Controls.Add(this.lblSubtitle);

            // DataGridView
            this.dataGridViewAnaliza = new DataGridView
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackgroundColor = Color.White,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                CellBorderStyle = DataGridViewCellBorderStyle.Single,
                GridColor = Color.FromArgb(230, 230, 230)
            };

            // Panel Footer
            this.panelFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.White,
                Padding = new Padding(20, 10, 20, 10)
            };

            // Linia separatora
            this.panelFooter.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(230, 230, 230), 1))
                {
                    e.Graphics.DrawLine(pen, 0, 0, panelFooter.Width, 0);
                }
            };

            // Przycisk Eksport
            this.btnExport = new Button
            {
                Text = "📥 Eksportuj do Excel",
                Size = new Size(160, 40),
                Location = new Point(20, 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = accentColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };

            this.btnExport.FlatAppearance.BorderSize = 0;
            this.btnExport.Click += BtnExport_Click;

            // Hover effect dla Export
            this.btnExport.MouseEnter += (s, e) =>
            {
                btnExport.BackColor = Color.FromArgb(39, 174, 96);
            };
            this.btnExport.MouseLeave += (s, e) =>
            {
                btnExport.BackColor = accentColor;
            };

            // Przycisk Zamknij
            this.btnClose = new Button
            {
                Text = "❌ Zamknij",
                Size = new Size(120, 40),
                Anchor = AnchorStyles.Right,
                Location = new Point(panelFooter.Width - 140, 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(231, 76, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };

            this.btnClose.FlatAppearance.BorderSize = 0;

            // Hover effect dla Close
            this.btnClose.MouseEnter += (s, e) =>
            {
                btnClose.BackColor = Color.FromArgb(192, 57, 43);
            };
            this.btnClose.MouseLeave += (s, e) =>
            {
                btnClose.BackColor = Color.FromArgb(231, 76, 60);
            };

            this.panelFooter.Controls.Add(this.btnExport);
            this.panelFooter.Controls.Add(this.btnClose);

            // Dodanie kontrolek do formularza
            this.Controls.Add(this.dataGridViewAnaliza);
            this.Controls.Add(this.panelFooter);
            this.Controls.Add(this.panelHeader);

            this.ResumeLayout(false);
        }

        private void ConfigureForm()
        {
            // Stylizacja nagłówków DataGridView
            dataGridViewAnaliza.ColumnHeadersDefaultCellStyle.BackColor = primaryColor;
            dataGridViewAnaliza.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dataGridViewAnaliza.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            dataGridViewAnaliza.ColumnHeadersDefaultCellStyle.Padding = new Padding(5);
            dataGridViewAnaliza.ColumnHeadersHeight = 50;
            dataGridViewAnaliza.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridViewAnaliza.EnableHeadersVisualStyles = false;

            // Stylizacja komórek
            dataGridViewAnaliza.DefaultCellStyle.SelectionBackColor = Color.FromArgb(142, 68, 173);
            dataGridViewAnaliza.DefaultCellStyle.SelectionForeColor = Color.White;
            dataGridViewAnaliza.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
            dataGridViewAnaliza.DefaultCellStyle.Padding = new Padding(5, 2, 5, 2);
            dataGridViewAnaliza.RowTemplate.Height = 35;

            // Zdarzenia
            dataGridViewAnaliza.CellFormatting += DataGridViewAnaliza_CellFormatting;
            dataGridViewAnaliza.DataBindingComplete += DataGridViewAnaliza_DataBindingComplete;
        }

        private void LoadData()
        {
            string query = @"
            DECLARE @TowarID INT = @pTowarID;
            WITH DaneZrodlowe AS (
                SELECT DP.kod AS NazwaTowaru, DP.ilosc, DATEDIFF(week, DK.data, GETDATE()) AS TydzienWstecz
                FROM [HANDEL].[HM].[DP] DP 
                INNER JOIN [HANDEL].[HM].[DK] DK ON DP.super = DK.id
                WHERE DK.khid = @KontrahentID 
                AND DATEDIFF(week, DK.data, GETDATE()) < 10 
                AND (@TowarID IS NULL OR DP.idtw = @TowarID)
            )
            SELECT NazwaTowaru,
                SUM(CASE WHEN TydzienWstecz = 9 THEN ilosc ELSE 0 END) AS Tydzien_9,
                SUM(CASE WHEN TydzienWstecz = 8 THEN ilosc ELSE 0 END) AS Tydzien_8,
                SUM(CASE WHEN TydzienWstecz = 7 THEN ilosc ELSE 0 END) AS Tydzien_7,
                SUM(CASE WHEN TydzienWstecz = 6 THEN ilosc ELSE 0 END) AS Tydzien_6,
                SUM(CASE WHEN TydzienWstecz = 5 THEN ilosc ELSE 0 END) AS Tydzien_5,
                SUM(CASE WHEN TydzienWstecz = 4 THEN ilosc ELSE 0 END) AS Tydzien_4,
                SUM(CASE WHEN TydzienWstecz = 3 THEN ilosc ELSE 0 END) AS Tydzien_3,
                SUM(CASE WHEN TydzienWstecz = 2 THEN ilosc ELSE 0 END) AS Tydzien_2,
                SUM(CASE WHEN TydzienWstecz = 1 THEN ilosc ELSE 0 END) AS Tydzien_1,
                SUM(CASE WHEN TydzienWstecz = 0 THEN ilosc ELSE 0 END) AS Tydzien_0
            FROM DaneZrodlowe 
            GROUP BY NazwaTowaru 
            HAVING SUM(ilosc) > 0 
            ORDER BY SUM(ilosc) DESC;";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@KontrahentID", kontrahentId);
                    cmd.Parameters.AddWithValue("@pTowarID", (object)towarId ?? DBNull.Value);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    // Konfiguracja kolumn
                    ConfigureColumns();

                    dataGridViewAnaliza.DataSource = dt;

                    // Podsumowanie
                    AddSummaryRow(dt);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wczytywania analizy: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ConfigureColumns()
        {
            dataGridViewAnaliza.AutoGenerateColumns = false;
            dataGridViewAnaliza.Columns.Clear();

            // Nazwa towaru
            var colNazwa = new DataGridViewTextBoxColumn
            {
                Name = "NazwaTowaru",
                DataPropertyName = "NazwaTowaru",
                HeaderText = "📦 Nazwa Towaru",
                Width = 200,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(245, 245, 245),
                    Font = new Font("Segoe UI Semibold", 9f),
                    Padding = new Padding(10, 2, 5, 2)
                }
            };
            dataGridViewAnaliza.Columns.Add(colNazwa);

            // Kolumny tygodni
            for (int i = 9; i >= 0; i--)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    Name = $"Tydzien_{i}",
                    DataPropertyName = $"Tydzien_{i}",
                    HeaderText = $"Tydzień {-i}\n{GetWeekDates(i)}",
                    Width = 110,
                    DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Format = "N2",
                        Alignment = DataGridViewContentAlignment.MiddleCenter
                    }
                };

                // Kolorowanie tygodni
                if (i == 0)
                {
                    col.DefaultCellStyle.BackColor = Color.FromArgb(200, 230, 201);
                    col.DefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                }
                else if (i == 1)
                {
                    col.DefaultCellStyle.BackColor = Color.FromArgb(225, 245, 254);
                }
                else if (i <= 3)
                {
                    col.DefaultCellStyle.BackColor = Color.FromArgb(250, 250, 250);
                }

                dataGridViewAnaliza.Columns.Add(col);
            }

            // Kolumna SUMA
            var colSuma = new DataGridViewTextBoxColumn
            {
                Name = "Suma",
                HeaderText = "📊 SUMA",
                Width = 120,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "N2",
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    BackColor = Color.FromArgb(255, 243, 224),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(50, 50, 50)
                }
            };
            dataGridViewAnaliza.Columns.Add(colSuma);

            // Zamrożenie pierwszej kolumny
            if (dataGridViewAnaliza.Columns.Count > 0)
            {
                dataGridViewAnaliza.Columns[0].Frozen = true;
            }
        }

        private string GetWeekDates(int weeksBack)
        {
            DateTime today = DateTime.Today;
            int daysOffset = (int)today.DayOfWeek - (int)DayOfWeek.Monday;
            if (daysOffset < 0) daysOffset += 7;

            DateTime weekStart = today.AddDays(-daysOffset - (weeksBack * 7));
            DateTime weekEnd = weekStart.AddDays(6);

            return $"{weekStart:dd.MM}-{weekEnd:dd.MM}";
        }

        private void DataGridViewAnaliza_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            // Obliczanie sumy dla każdego wiersza
            foreach (DataGridViewRow row in dataGridViewAnaliza.Rows)
            {
                if (!row.IsNewRow && row.Cells["NazwaTowaru"].Value?.ToString() != "📊 PODSUMOWANIE")
                {
                    decimal suma = 0;
                    for (int i = 0; i <= 9; i++)
                    {
                        var cellValue = row.Cells[$"Tydzien_{i}"].Value;
                        if (cellValue != null && cellValue != DBNull.Value)
                        {
                            if (decimal.TryParse(cellValue.ToString(), out decimal val))
                            {
                                suma += val;
                            }
                        }
                    }
                    row.Cells["Suma"].Value = suma;
                }
            }
        }

        private void AddSummaryRow(DataTable dt)
        {
            if (dt.Rows.Count > 0)
            {
                var summaryRow = dt.NewRow();
                summaryRow["NazwaTowaru"] = "📊 PODSUMOWANIE";

                // Oblicz sumy dla każdego tygodnia
                for (int i = 0; i <= 9; i++)
                {
                    decimal suma = 0;
                    foreach (DataRow row in dt.Rows)
                    {
                        if (row[$"Tydzien_{i}"] != DBNull.Value)
                        {
                            suma += Convert.ToDecimal(row[$"Tydzien_{i}"]);
                        }
                    }
                    summaryRow[$"Tydzien_{i}"] = suma;
                }

                dt.Rows.Add(summaryRow);
            }
        }

        private void DataGridViewAnaliza_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                var row = dataGridViewAnaliza.Rows[e.RowIndex];

                // Formatowanie wiersza podsumowania
                if (row.Cells["NazwaTowaru"].Value?.ToString() == "📊 PODSUMOWANIE")
                {
                    e.CellStyle.BackColor = Color.FromArgb(255, 243, 224);
                    e.CellStyle.Font = new Font(dataGridViewAnaliza.Font, FontStyle.Bold);
                    e.CellStyle.ForeColor = Color.FromArgb(50, 50, 50);
                }
                // Kolorowanie wartości
                else if (e.ColumnIndex > 0 && e.Value != null)
                {
                    if (decimal.TryParse(e.Value.ToString(), out decimal value))
                    {
                        if (value == 0)
                        {
                            e.CellStyle.ForeColor = Color.LightGray;
                        }
                        else if (value > 100)
                        {
                            e.CellStyle.ForeColor = accentColor;
                            e.CellStyle.Font = new Font(dataGridViewAnaliza.Font, FontStyle.Bold);
                        }
                        else if (value > 50)
                        {
                            e.CellStyle.ForeColor = Color.FromArgb(52, 152, 219);
                        }
                    }
                }
            }
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            try
            {
                using (var saveDialog = new SaveFileDialog())
                {
                    saveDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                    saveDialog.FileName = $"Analiza_{nazwaKontrahenta}_{DateTime.Now:yyyyMMdd}.csv";

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        using (var writer = new System.IO.StreamWriter(saveDialog.FileName, false, System.Text.Encoding.UTF8))
                        {
                            // Nagłówki
                            var headers = new System.Text.StringBuilder();
                            for (int i = 0; i < dataGridViewAnaliza.Columns.Count; i++)
                            {
                                headers.Append(dataGridViewAnaliza.Columns[i].HeaderText.Replace("\n", " "));
                                if (i < dataGridViewAnaliza.Columns.Count - 1)
                                    headers.Append(";");
                            }
                            writer.WriteLine(headers.ToString());

                            // Dane
                            foreach (DataGridViewRow row in dataGridViewAnaliza.Rows)
                            {
                                if (!row.IsNewRow)
                                {
                                    var line = new System.Text.StringBuilder();
                                    for (int i = 0; i < dataGridViewAnaliza.Columns.Count; i++)
                                    {
                                        var value = row.Cells[i].Value?.ToString() ?? "";
                                        line.Append(value.Replace(";", ","));
                                        if (i < dataGridViewAnaliza.Columns.Count - 1)
                                            line.Append(";");
                                    }
                                    writer.WriteLine(line.ToString());
                                }
                            }
                        }

                        MessageBox.Show("Dane zostały wyeksportowane pomyślnie!", "Sukces",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas eksportu: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}