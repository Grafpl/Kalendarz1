// WidokPorownanieOfert.cs
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace Kalendarz1
{
    public partial class WidokPorownanieOfert : Form
    {
        private readonly string _connHandel;

        private ComboBox cboKontrahent;
        private ComboBox cboTowar;
        private DateTimePicker dtpOd;
        private DateTimePicker dtpDo;
        private DataGridView dgvOferty;
        private Chart chartCeny;
        private Label lblSredniaCena;
        private Label lblMinCena;
        private Label lblMaxCena;
        private Label lblTrend;
        private Button btnOdswiez;
        private Button btnEksportuj;
        private Panel panelFilters;
        private Panel panelStats;
        private Panel panelChart;
        private Panel panelGrid;

        public WidokPorownanieOfert()
        {
            InitializeComponent();
            _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        }

        private void InitializeComponent()
        {
            this.Text = "📊 Porównanie Ofert Historycznych";
            this.Size = new Size(1300, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(249, 250, 251);

            CreateFilterPanel();
            CreateStatsPanel();
            CreateChartPanel();
            CreateGridPanel();

            this.Load += async (s, e) => await LoadInitialData();
        }

        private void CreateFilterPanel()
        {
            panelFilters = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = Color.White,
                Padding = new Padding(15)
            };

            var lblTitle = new Label
            {
                Text = "🔍 FILTRY PORÓWNANIA",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(31, 41, 55),
                Location = new Point(15, 10),
                AutoSize = true
            };

            var lblKontrahent = CreateLabel("Kontrahent:", 15, 45);
            cboKontrahent = new ComboBox
            {
                Location = new Point(90, 43),
                Width = 250,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            var lblTowar = CreateLabel("Towar:", 360, 45);
            cboTowar = new ComboBox
            {
                Location = new Point(410, 43),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            var lblOd = CreateLabel("Od:", 630, 45);
            dtpOd = new DateTimePicker
            {
                Location = new Point(660, 43),
                Width = 120,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today.AddMonths(-3)
            };

            var lblDo = CreateLabel("Do:", 800, 45);
            dtpDo = new DateTimePicker
            {
                Location = new Point(830, 43),
                Width = 120,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today
            };

            btnOdswiez = CreateButton("🔄 Odśwież", 970, 40, Color.FromArgb(59, 130, 246));
            btnOdswiez.Click += async (s, e) => await LoadOfferComparison();

            btnEksportuj = CreateButton("📥 Eksportuj", 1100, 40, Color.FromArgb(34, 197, 94));
            btnEksportuj.Click += (s, e) => ExportToExcel();

            panelFilters.Controls.AddRange(new Control[] {
                lblTitle, lblKontrahent, cboKontrahent, lblTowar, cboTowar,
                lblOd, dtpOd, lblDo, dtpDo, btnOdswiez, btnEksportuj
            });

            this.Controls.Add(panelFilters);
        }

        private void CreateStatsPanel()
        {
            panelStats = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(239, 246, 255),
                Padding = new Padding(15, 10, 15, 10)
            };

            var statsFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            lblSredniaCena = CreateStatLabel("📊 Średnia cena", "0.00 zł/kg", Color.FromArgb(99, 102, 241));
            lblMinCena = CreateStatLabel("📉 Min. cena", "0.00 zł/kg", Color.FromArgb(34, 197, 94));
            lblMaxCena = CreateStatLabel("📈 Max. cena", "0.00 zł/kg", Color.FromArgb(239, 68, 68));
            lblTrend = CreateStatLabel("📈 Trend", "→ Stabilny", Color.FromArgb(107, 114, 128));

            statsFlow.Controls.AddRange(new Control[] {
                lblSredniaCena, CreateSeparator(),
                lblMinCena, CreateSeparator(),
                lblMaxCena, CreateSeparator(),
                lblTrend
            });

            panelStats.Controls.Add(statsFlow);
            this.Controls.Add(panelStats);
        }

        private void CreateChartPanel()
        {
            panelChart = new Panel
            {
                Dock = DockStyle.Top,
                Height = 250,
                BackColor = Color.White,
                Padding = new Padding(15)
            };

            var lblChartTitle = new Label
            {
                Text = "📈 WYKRES CENY W CZASIE",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(31, 41, 55),
                Location = new Point(15, 5),
                AutoSize = true
            };

            chartCeny = new Chart
            {
                Location = new Point(15, 30),
                Size = new Size(1250, 200),
                BackColor = Color.White
            };

            // Konfiguracja wykresu
            var chartArea = new ChartArea("MainArea");
            chartArea.BackColor = Color.White;
            chartArea.BorderColor = Color.FromArgb(229, 231, 235);
            chartArea.BorderDashStyle = ChartDashStyle.Solid;
            chartArea.BorderWidth = 1;

            // Osie
            chartArea.AxisX.LabelStyle.Font = new Font("Segoe UI", 8f);
            chartArea.AxisX.LabelStyle.Format = "dd.MM";
            chartArea.AxisX.MajorGrid.LineColor = Color.FromArgb(243, 244, 246);
            chartArea.AxisX.LineColor = Color.FromArgb(209, 213, 219);

            chartArea.AxisY.LabelStyle.Font = new Font("Segoe UI", 8f);
            chartArea.AxisY.LabelStyle.Format = "N2";
            chartArea.AxisY.MajorGrid.LineColor = Color.FromArgb(243, 244, 246);
            chartArea.AxisY.LineColor = Color.FromArgb(209, 213, 219);
            chartArea.AxisY.Title = "Cena [zł/kg]";
            chartArea.AxisY.TitleFont = new Font("Segoe UI", 9f);

            chartCeny.ChartAreas.Add(chartArea);

            // Legenda
            var legend = new Legend("MainLegend");
            legend.BackColor = Color.Transparent;
            legend.Font = new Font("Segoe UI", 8f);
            legend.Docking = Docking.Top;
            legend.Alignment = System.Drawing.StringAlignment.Center;
            chartCeny.Legends.Add(legend);

            panelChart.Controls.AddRange(new Control[] { lblChartTitle, chartCeny });
            this.Controls.Add(panelChart);
        }

        private void CreateGridPanel()
        {
            panelGrid = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(15)
            };

            var lblGridTitle = new Label
            {
                Text = "📋 HISTORIA OFERT",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(31, 41, 55),
                Location = new Point(15, 5),
                AutoSize = true
            };

            dgvOferty = new DataGridView
            {
                Location = new Point(15, 30),
                Size = new Size(1250, 320),
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersWidth = 25,
                Font = new Font("Segoe UI", 9f)
            };

            // Styl grida
            dgvOferty.EnableHeadersVisualStyles = false;
            dgvOferty.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
            dgvOferty.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(75, 85, 99);
            dgvOferty.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9f);
            dgvOferty.ColumnHeadersHeight = 35;
            dgvOferty.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
            dgvOferty.RowTemplate.Height = 28;
            dgvOferty.DefaultCellStyle.SelectionBackColor = Color.FromArgb(238, 242, 255);
            dgvOferty.DefaultCellStyle.SelectionForeColor = Color.FromArgb(55, 65, 81);

            panelGrid.Controls.AddRange(new Control[] { lblGridTitle, dgvOferty });
            this.Controls.Add(panelGrid);
        }

        private async Task LoadInitialData()
        {
            try
            {
                await LoadKontrahenci();
                await LoadTowary();
                await LoadOfferComparison();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadKontrahenci()
        {
            const string sql = @"
                SELECT DISTINCT oh.KontrahentID, c.Shortcut AS Nazwa
                FROM OfertyHandlowe oh
                INNER JOIN [HANDEL].[SSCommon].[STContractors] c ON c.Id = oh.KontrahentID
                ORDER BY c.Shortcut";

            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                await using var rd = await cmd.ExecuteReaderAsync();

                var kontrahenci = new List<KeyValuePair<int, string>> { new(0, "-- Wszyscy --") };

                while (await rd.ReadAsync())
                {
                    kontrahenci.Add(new KeyValuePair<int, string>(rd.GetInt32(0), rd.GetString(1)));
                }

                cboKontrahent.DataSource = kontrahenci;
                cboKontrahent.DisplayMember = "Value";
                cboKontrahent.ValueMember = "Key";
                cboKontrahent.SelectedIndex = 0;
            }
            catch { }
        }

        private async Task LoadTowary()
        {
            const string sql = @"
                SELECT DISTINCT oh.TowarID, t.Kod
                FROM OfertyHandlowe oh
                INNER JOIN [HANDEL].[HM].[TW] t ON t.Id = oh.TowarID
                ORDER BY t.Kod";

            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                await using var rd = await cmd.ExecuteReaderAsync();

                var towary = new List<KeyValuePair<int, string>> { new(0, "-- Wszystkie --") };

                while (await rd.ReadAsync())
                {
                    towary.Add(new KeyValuePair<int, string>(rd.GetInt32(0), rd.GetString(1)));
                }

                cboTowar.DataSource = towary;
                cboTowar.DisplayMember = "Value";
                cboTowar.ValueMember = "Key";
                cboTowar.SelectedIndex = 0;
            }
            catch { }
        }

        private async Task LoadOfferComparison()
        {
            string sql = @"
                SELECT 
                    oh.Data,
                    oh.NumerOferty,
                    c.Shortcut AS Kontrahent,
                    t.Kod AS Towar,
                    oh.CenaNetto,
                    oh.IloscMin,
                    oh.MarzaPLN,
                    oh.MarzaProc,
                    oh.WarunkiPlatnosci,
                    oh.Handlowiec
                FROM OfertyHandlowe oh
                INNER JOIN [HANDEL].[SSCommon].[STContractors] c ON c.Id = oh.KontrahentID
                INNER JOIN [HANDEL].[HM].[TW] t ON t.Id = oh.TowarID
                WHERE oh.Data BETWEEN @Od AND @Do";

            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@Od", dtpOd.Value.Date),
                new SqlParameter("@Do", dtpDo.Value.Date.AddDays(1))
            };

            if (cboKontrahent.SelectedIndex > 0)
            {
                sql += " AND oh.KontrahentID = @KontrahentID";
                parameters.Add(new SqlParameter("@KontrahentID", cboKontrahent.SelectedValue));
            }

            if (cboTowar.SelectedIndex > 0)
            {
                sql += " AND oh.TowarID = @TowarID";
                parameters.Add(new SqlParameter("@TowarID", cboTowar.SelectedValue));
            }

            sql += " ORDER BY oh.Data DESC, oh.NumerOferty DESC";

            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddRange(parameters.ToArray());

                var dt = new DataTable();
                using var adapter = new SqlDataAdapter(cmd);
                await Task.Run(() => adapter.Fill(dt));

                dgvOferty.DataSource = dt;

                // Formatowanie kolumn
                if (dgvOferty.Columns["Data"] != null)
                {
                    dgvOferty.Columns["Data"].HeaderText = "📅 Data";
                    dgvOferty.Columns["Data"].DefaultCellStyle.Format = "dd.MM.yyyy";
                    dgvOferty.Columns["Data"].Width = 100;
                }
                if (dgvOferty.Columns["NumerOferty"] != null)
                {
                    dgvOferty.Columns["NumerOferty"].HeaderText = "🔢 Numer";
                    dgvOferty.Columns["NumerOferty"].Width = 150;
                }
                if (dgvOferty.Columns["Kontrahent"] != null)
                {
                    dgvOferty.Columns["Kontrahent"].HeaderText = "👤 Kontrahent";
                    dgvOferty.Columns["Kontrahent"].Width = 200;
                }
                if (dgvOferty.Columns["Towar"] != null)
                {
                    dgvOferty.Columns["Towar"].HeaderText = "🥩 Towar";
                    dgvOferty.Columns["Towar"].Width = 150;
                }
                if (dgvOferty.Columns["CenaNetto"] != null)
                {
                    dgvOferty.Columns["CenaNetto"].HeaderText = "💰 Cena [zł/kg]";
                    dgvOferty.Columns["CenaNetto"].DefaultCellStyle.Format = "N2";
                    dgvOferty.Columns["CenaNetto"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    dgvOferty.Columns["CenaNetto"].DefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                }
                if (dgvOferty.Columns["IloscMin"] != null)
                {
                    dgvOferty.Columns["IloscMin"].HeaderText = "📦 Min. ilość";
                    dgvOferty.Columns["IloscMin"].DefaultCellStyle.Format = "N0";
                    dgvOferty.Columns["IloscMin"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
                if (dgvOferty.Columns["MarzaProc"] != null)
                {
                    dgvOferty.Columns["MarzaProc"].HeaderText = "📈 Marża %";
                    dgvOferty.Columns["MarzaProc"].DefaultCellStyle.Format = "N1";
                    dgvOferty.Columns["MarzaProc"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }
                if (dgvOferty.Columns["Handlowiec"] != null)
                {
                    dgvOferty.Columns["Handlowiec"].HeaderText = "👔 Handlowiec";
                    dgvOferty.Columns["Handlowiec"].Width = 100;
                }

                // Kolorowanie wierszy według marży
                dgvOferty.RowPostPaint += (s, e) =>
                {
                    if (e.RowIndex < 0) return;
                    var row = dgvOferty.Rows[e.RowIndex];
                    if (row.Cells["MarzaProc"].Value != null && row.Cells["MarzaProc"].Value != DBNull.Value)
                    {
                        decimal marza = Convert.ToDecimal(row.Cells["MarzaProc"].Value);
                        if (marza < 10)
                            row.Cells["MarzaProc"].Style.ForeColor = Color.FromArgb(239, 68, 68);
                        else if (marza < 20)
                            row.Cells["MarzaProc"].Style.ForeColor = Color.FromArgb(251, 191, 36);
                        else
                            row.Cells["MarzaProc"].Style.ForeColor = Color.FromArgb(34, 197, 94);
                    }
                };

                // Statystyki
                CalculateStatistics(dt);

                // Wykres
                UpdateChart(dt);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania ofert: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CalculateStatistics(DataTable dt)
        {
            if (dt.Rows.Count == 0)
            {
                lblSredniaCena.Text = "📊 Średnia: --";
                lblMinCena.Text = "📉 Min: --";
                lblMaxCena.Text = "📈 Max: --";
                lblTrend.Text = "📈 Trend: --";
                return;
            }

            var ceny = dt.AsEnumerable()
                .Where(r => r["CenaNetto"] != DBNull.Value)
                .Select(r => Convert.ToDecimal(r["CenaNetto"]))
                .ToList();

            if (!ceny.Any()) return;

            decimal srednia = ceny.Average();
            decimal min = ceny.Min();
            decimal max = ceny.Max();

            lblSredniaCena.Text = $"📊 Średnia: {srednia:N2} zł/kg";
            lblMinCena.Text = $"📉 Min: {min:N2} zł/kg";
            lblMaxCena.Text = $"📈 Max: {max:N2} zł/kg";

            // Oblicz trend
            if (ceny.Count >= 2)
            {
                decimal pierwsza = ceny.Last();
                decimal ostatnia = ceny.First();
                decimal roznica = ostatnia - pierwsza;
                decimal procentZmiany = pierwsza != 0 ? (roznica / pierwsza * 100) : 0;

                if (Math.Abs(procentZmiany) < 2)
                {
                    lblTrend.Text = "📈 Trend: → Stabilny";
                    lblTrend.ForeColor = Color.FromArgb(107, 114, 128);
                }
                else if (procentZmiany > 0)
                {
                    lblTrend.Text = $"📈 Trend: ↑ +{procentZmiany:N1}%";
                    lblTrend.ForeColor = Color.FromArgb(239, 68, 68);
                }
                else
                {
                    lblTrend.Text = $"📈 Trend: ↓ {procentZmiany:N1}%";
                    lblTrend.ForeColor = Color.FromArgb(34, 197, 94);
                }
            }
        }

        private void UpdateChart(DataTable dt)
        {
            chartCeny.Series.Clear();

            if (dt.Rows.Count == 0) return;

            // Grupuj według towaru
            var towary = dt.AsEnumerable()
                .Select(r => r.Field<string>("Towar"))
                .Distinct()
                .ToList();

            foreach (var towar in towary)
            {
                var series = new Series(towar)
                {
                    ChartType = SeriesChartType.Line,
                    BorderWidth = 2,
                    MarkerStyle = MarkerStyle.Circle,
                    MarkerSize = 6
                };

                var punkty = dt.AsEnumerable()
                    .Where(r => r.Field<string>("Towar") == towar)
                    .OrderBy(r => r.Field<DateTime>("Data"))
                    .Select(r => new
                    {
                        Data = r.Field<DateTime>("Data"),
                        Cena = r.Field<decimal>("CenaNetto")
                    });

                foreach (var punkt in punkty)
                {
                    series.Points.AddXY(punkt.Data, punkt.Cena);
                }

                chartCeny.Series.Add(series);
            }

            // Dodaj linię średniej
            if (dt.Rows.Count > 0)
            {
                decimal srednia = dt.AsEnumerable()
                    .Where(r => r["CenaNetto"] != DBNull.Value)
                    .Average(r => Convert.ToDecimal(r["CenaNetto"]));

                var avgSeries = new Series("Średnia")
                {
                    ChartType = SeriesChartType.Line,
                    BorderWidth = 1,
                    BorderDashStyle = ChartDashStyle.Dash,
                    Color = Color.FromArgb(156, 163, 175)
                };

                var minDate = dt.AsEnumerable().Min(r => r.Field<DateTime>("Data"));
                var maxDate = dt.AsEnumerable().Max(r => r.Field<DateTime>("Data"));

                avgSeries.Points.AddXY(minDate, srednia);
                avgSeries.Points.AddXY(maxDate, srednia);

                chartCeny.Series.Add(avgSeries);
            }
        }

        private void ExportToExcel()
        {
            if (dgvOferty.DataSource == null)
            {
                MessageBox.Show("Brak danych do eksportu!", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                FileName = $"Porownanie_Ofert_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                // Tu implementacja eksportu do Excel (np. przez EPPlus)
                MessageBox.Show("📥 Eksport do Excel będzie dostępny wkrótce!", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private Label CreateLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(75, 85, 99)
            };
        }

        private Button CreateButton(string text, int x, int y, Color bgColor)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(110, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = bgColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9f),
                Cursor = Cursors.Hand
            };

            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => btn.BackColor = ControlPaint.Dark(bgColor, 0.1f);
            btn.MouseLeave += (s, e) => btn.BackColor = bgColor;

            return btn;
        }

        private Label CreateStatLabel(string title, string value, Color color)
        {
            var panel = new Panel
            {
                Width = 200,
                Height = 60,
                Margin = new Padding(10, 0, 10, 0)
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(107, 114, 128),
                Location = new Point(0, 10),
                AutoSize = true
            };

            var lblValue = new Label
            {
                Text = value,
                Font = new Font("Segoe UI Semibold", 14f),
                ForeColor = color,
                Location = new Point(0, 28),
                AutoSize = true
            };

            panel.Controls.Add(lblTitle);
            panel.Controls.Add(lblValue);

            return lblValue;
        }

        private Panel CreateSeparator()
        {
            return new Panel
            {
                Width = 1,
                Height = 50,
                BackColor = Color.FromArgb(229, 231, 235),
                Margin = new Padding(0, 5, 0, 5)
            };
        }
    }
}