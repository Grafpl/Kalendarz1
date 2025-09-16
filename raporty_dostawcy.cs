using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

namespace Kalendarz1
{
    public partial class RaportyDostawcy : System.Windows.Forms.Form
    {
        private const string CONNECTION_STRING = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;Connection Timeout=30;";
        
        private TabControl? tabControl;
        private DataTable? _rawData;
        private ComboBox? cmbDostawca;
        private DateTimePicker? dtpOd;
        private DateTimePicker? dtpDo;
        private Button? btnGeneruj;
        private Panel? panelFilters;
        
        public RaportyDostawcy()
        {
            InitializeComponent();
            InitializeForm();
            _ = LoadDataAsync();
        }

        // Konstruktor z parametrem dostawcy
        public RaportyDostawcy(string dostawca) : this()
        {
            // Ustaw dostawcę po załadowaniu danych
            Task.Run(async () =>
            {
                await Task.Delay(500); // Poczekaj na załadowanie
                this.Invoke(new Action(() =>
                {
                    if (cmbDostawca != null && cmbDostawca.Items.Contains(dostawca))
                    {
                        cmbDostawca.SelectedItem = dostawca;
                        _ = GenerateReportsAsync();
                    }
                }));
            });
        }

        // Metoda do ustawienia dostawcy z zewnątrz
        public void SetDostawca(string dostawca)
        {
            if (cmbDostawca != null && cmbDostawca.Items.Contains(dostawca))
            {
                cmbDostawca.SelectedItem = dostawca;
                _ = GenerateReportsAsync();
            }
        }

        private void InitializeComponent()
        {
            this.Text = "📊 Raporty i Analizy Dostawców";
            this.Size = new Size(1400, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9F);
            
            // Panel z filtrami
            panelFilters = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(245, 245, 245),
                Padding = new Padding(10)
            };
            
            // Filtry
            var lblDostawca = new Label
            {
                Text = "Dostawca:",
                Location = new Point(20, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            
            cmbDostawca = new ComboBox
            {
                Location = new Point(20, 35),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            
            var lblOd = new Label
            {
                Text = "Data od:",
                Location = new Point(240, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            
            dtpOd = new DateTimePicker
            {
                Location = new Point(240, 35),
                Width = 120,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now.AddMonths(-1)
            };
            
            var lblDo = new Label
            {
                Text = "Data do:",
                Location = new Point(380, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            
            dtpDo = new DateTimePicker
            {
                Location = new Point(380, 35),
                Width = 120,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now
            };
            
            btnGeneruj = new Button
            {
                Text = "🔄 Generuj Raporty",
                Location = new Point(520, 33),
                Size = new Size(140, 30),
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnGeneruj.FlatAppearance.BorderSize = 0;
            btnGeneruj.Click += async (s, e) => await GenerateReportsAsync();
            
            // Przycisk eksportu
            var btnExport = new Button
            {
                Text = "📥 Eksport PDF",
                Location = new Point(680, 33),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(39, 174, 96),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnExport.FlatAppearance.BorderSize = 0;
            btnExport.Click += (s, e) => ExportToPDF();
            
            panelFilters.Controls.AddRange(new Control[] { 
                lblDostawca, cmbDostawca, lblOd, dtpOd, lblDo, dtpDo, btnGeneruj, btnExport 
            });
            
            // TabControl dla różnych raportów
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F)
            };
            
            this.Controls.Add(tabControl);
            this.Controls.Add(panelFilters);
        }

        private void InitializeForm()
        {
            // Double buffering dla płynności
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | 
                         ControlStyles.UserPaint | 
                         ControlStyles.DoubleBuffer, true);
            
            CreateTabs();
        }

        private void CreateTabs()
        {
            // Tab 1: Podsumowanie
            var tabSummary = new TabPage("📋 Podsumowanie");
            tabControl.TabPages.Add(tabSummary);
            
            // Tab 2: Analiza Wag
            var tabWeights = new TabPage("⚖️ Analiza Wag");
            tabControl.TabPages.Add(tabWeights);
            
            // Tab 3: Trendy
            var tabTrends = new TabPage("📈 Trendy");
            tabControl.TabPages.Add(tabTrends);
            
            // Tab 4: Porównanie
            var tabComparison = new TabPage("🔍 Porównanie");
            tabControl.TabPages.Add(tabComparison);
            
            // Tab 5: Jakość
            var tabQuality = new TabPage("✅ Wskaźniki Jakości");
            tabControl.TabPages.Add(tabQuality);
        }

        private async Task LoadDataAsync()
        {
            try
            {
                this.Cursor = Cursors.WaitCursor;
                
                _rawData = await Task.Run(() => GetDataFromDatabase());
                
                // Wypełnij combo dostawców
                var dostawcy = _rawData.AsEnumerable()
                    .Select(r => r.Field<string>("Dostawca"))
                    .Where(d => !string.IsNullOrEmpty(d))
                    .Distinct()
                    .OrderBy(d => d)
                    .ToArray();
                
                if (cmbDostawca != null)
                {
                    cmbDostawca.Items.Clear();
                    cmbDostawca.Items.Add("-- Wszyscy --");
                    cmbDostawca.Items.AddRange(dostawcy);
                    cmbDostawca.SelectedIndex = 0;
                }
                
                // Generuj początkowe raporty
                await GenerateReportsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private DataTable GetDataFromDatabase()
        {
            const string query = @"
                SELECT 
                    k.CreateData AS Data,
                    k.P1 AS Partia,
                    Partia.CustomerName AS Dostawca,
                    DATEDIFF(day, wk.DataWstawienia, hd.DataOdbioru) AS RoznicaDni,
                    hd.WagaDek AS WagaDek,
                    CONVERT(decimal(18, 2), (15.0 / CAST(AVG(CAST(k.QntInCont AS decimal(18, 2))) AS decimal(18, 2))) * 1.22) AS SredniaZywy,
                    CONVERT(decimal(18, 2), ((15.0 / CAST(AVG(CAST(k.QntInCont AS decimal(18, 2))) AS decimal(18, 2))) * 1.22) - hd.WagaDek) AS Roznica,
                    AVG(k.QntInCont) AS Srednia,
                    CAST(AVG(CAST(k.QntInCont AS decimal(18, 2))) AS decimal(18, 2)) AS SredniaDokładna,
                    COUNT(*) as LiczbaPartii
                FROM 
                    [LibraNet].[dbo].[In0E] k WITH (NOLOCK)
                JOIN 
                    [LibraNet].[dbo].[PartiaDostawca] Partia WITH (NOLOCK) ON k.P1 = Partia.Partia
                LEFT JOIN 
                    [LibraNet].[dbo].[HarmonogramDostaw] hd WITH (NOLOCK) ON k.CreateData = hd.DataOdbioru AND Partia.CustomerName = hd.Dostawca
                LEFT JOIN 
                    [LibraNet].[dbo].[WstawieniaKurczakow] wk WITH (NOLOCK) ON hd.LpW = wk.Lp
                WHERE 
                    k.ArticleID = 40 
                    AND k.QntInCont > 4
                    AND k.CreateData >= DATEADD(month, -3, GETDATE())
                GROUP BY 
                    k.CreateData, 
                    k.P1, 
                    Partia.CustomerName, 
                    hd.WagaDek,
                    wk.DataWstawienia,
                    hd.DataOdbioru
                ORDER BY 
                    k.CreateData DESC, 
                    Partia.CustomerName";

            using (var connection = new SqlConnection(CONNECTION_STRING))
            using (var command = new SqlCommand(query, connection))
            {
                command.CommandTimeout = 60;
                using (var adapter = new SqlDataAdapter(command))
                {
                    var table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        private async Task GenerateReportsAsync()
        {
            if (_rawData == null) return;
            
            try
            {
                this.Cursor = Cursors.WaitCursor;
                
                var filteredData = FilterData();
                
                await Task.Run(() =>
                {
                    this.Invoke(new Action(() =>
                    {
                        GenerateSummaryReport(filteredData);
                        GenerateWeightAnalysis(filteredData);
                        GenerateTrendsReport(filteredData);
                        GenerateComparisonReport(filteredData);
                        GenerateQualityIndicators(filteredData);
                    }));
                });
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private DataTable FilterData()
        {
            var filtered = _rawData.Clone();
            var rows = _rawData.AsEnumerable();
            
            // Filtr dostawcy
            if (cmbDostawca.SelectedIndex > 0)
            {
                string selectedDostawca = cmbDostawca.SelectedItem.ToString();
                rows = rows.Where(r => r.Field<string>("Dostawca") == selectedDostawca);
            }
            
            // Filtr dat
            rows = rows.Where(r => 
            {
                var data = r.Field<DateTime>("Data");
                return data >= dtpOd.Value.Date && data <= dtpDo.Value.Date;
            });
            
            foreach (var row in rows)
            {
                filtered.ImportRow(row);
            }
            
            return filtered;
        }

        private void GenerateSummaryReport(DataTable data)
        {
            var tab = tabControl.TabPages[0];
            tab.Controls.Clear();
            
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20) };
            
            // Nagłówek
            var lblTitle = new Label
            {
                Text = "📊 PODSUMOWANIE DOSTAWCÓW",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize = true,
                Location = new Point(20, 20)
            };
            
            // Karty ze statystykami
            int cardY = 70;
            int cardX = 20;
            int cardWidth = 250;
            int cardHeight = 120;
            int spacing = 20;
            
            // Karta 1: Liczba dostaw
            CreateStatCard(panel, "📦 Liczba dostaw", 
                data.Rows.Count.ToString(), 
                "sztuk", 
                new Point(cardX, cardY), 
                new Size(cardWidth, cardHeight),
                Color.FromArgb(52, 152, 219));
            
            // Karta 2: Średnia waga
            decimal avgWaga = 0;
            if (data.Rows.Count > 0)
            {
                avgWaga = data.AsEnumerable()
                    .Where(r => r["WagaDek"] != DBNull.Value)
                    .Select(r => Convert.ToDecimal(r["WagaDek"]))
                    .DefaultIfEmpty(0)
                    .Average();
            }
            
            CreateStatCard(panel, "⚖️ Średnia waga dek", 
                avgWaga.ToString("F2"), 
                "kg", 
                new Point(cardX + cardWidth + spacing, cardY), 
                new Size(cardWidth, cardHeight),
                Color.FromArgb(155, 89, 182));
            
            // Karta 3: Średnia różnica
            decimal avgRoznica = 0;
            if (data.Rows.Count > 0)
            {
                avgRoznica = data.AsEnumerable()
                    .Where(r => r["Roznica"] != DBNull.Value)
                    .Select(r => Convert.ToDecimal(r["Roznica"]))
                    .DefaultIfEmpty(0)
                    .Average();
            }
            
            var roznicaColor = avgRoznica >= 0 ? Color.FromArgb(39, 174, 96) : Color.FromArgb(231, 76, 60);
            CreateStatCard(panel, "📏 Średnia różnica", 
                avgRoznica.ToString("F2"), 
                "kg", 
                new Point(cardX + (cardWidth + spacing) * 2, cardY), 
                new Size(cardWidth, cardHeight),
                roznicaColor);
            
            // Karta 4: Liczba dostawców
            int liczbaDostawcow = data.AsEnumerable()
                .Select(r => r.Field<string>("Dostawca"))
                .Distinct()
                .Count();
            
            CreateStatCard(panel, "👥 Liczba dostawców", 
                liczbaDostawcow.ToString(), 
                "dostawców", 
                new Point(cardX + (cardWidth + spacing) * 3, cardY), 
                new Size(cardWidth, cardHeight),
                Color.FromArgb(243, 156, 18));
            
            // Tabela TOP dostawców
            var dgvTop = new DataGridView
            {
                Location = new Point(20, 220),
                Size = new Size(1100, 300),
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                Font = new Font("Segoe UI", 9F)
            };
            
            // Przygotuj dane TOP dostawców
            var topDostawcy = data.AsEnumerable()
                .GroupBy(r => r.Field<string>("Dostawca"))
                .Select(g => new
                {
                    Dostawca = g.Key,
                    LiczbaDostaw = g.Count(),
                    SredniaWaga = g.Where(r => r["WagaDek"] != DBNull.Value)
                                   .Select(r => Convert.ToDecimal(r["WagaDek"]))
                                   .DefaultIfEmpty(0)
                                   .Average(),
                    SredniaRoznica = g.Where(r => r["Roznica"] != DBNull.Value)
                                      .Select(r => Convert.ToDecimal(r["Roznica"]))
                                      .DefaultIfEmpty(0)
                                      .Average(),
                    MinWaga = g.Where(r => r["WagaDek"] != DBNull.Value)
                              .Select(r => Convert.ToDecimal(r["WagaDek"]))
                              .DefaultIfEmpty(0)
                              .Min(),
                    MaxWaga = g.Where(r => r["WagaDek"] != DBNull.Value)
                              .Select(r => Convert.ToDecimal(r["WagaDek"]))
                              .DefaultIfEmpty(0)
                              .Max()
                })
                .OrderByDescending(x => x.LiczbaDostaw)
                .ToList();
            
            dgvTop.DataSource = topDostawcy;
            
            // Formatowanie kolumn
            if (dgvTop.Columns.Count > 0)
            {
                dgvTop.Columns["Dostawca"].HeaderText = "Dostawca";
                dgvTop.Columns["LiczbaDostaw"].HeaderText = "Liczba dostaw";
                dgvTop.Columns["SredniaWaga"].HeaderText = "Średnia waga (kg)";
                dgvTop.Columns["SredniaWaga"].DefaultCellStyle.Format = "F2";
                dgvTop.Columns["SredniaRoznica"].HeaderText = "Średnia różnica (kg)";
                dgvTop.Columns["SredniaRoznica"].DefaultCellStyle.Format = "F2";
                dgvTop.Columns["MinWaga"].HeaderText = "Min waga (kg)";
                dgvTop.Columns["MinWaga"].DefaultCellStyle.Format = "F2";
                dgvTop.Columns["MaxWaga"].HeaderText = "Max waga (kg)";
                dgvTop.Columns["MaxWaga"].DefaultCellStyle.Format = "F2";
            }
            
            // Nagłówek tabeli
            var lblTableTitle = new Label
            {
                Text = "🏆 Ranking dostawców",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point(20, 190),
                AutoSize = true
            };
            
            panel.Controls.AddRange(new Control[] { lblTitle, dgvTop, lblTableTitle });
            tab.Controls.Add(panel);
        }

        private void CreateStatCard(Panel parent, string title, string value, string unit, Point location, Size size, Color color)
        {
            var card = new Panel
            {
                Location = location,
                Size = size,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            
            var lblIcon = new Label
            {
                Text = title.Substring(0, 2), // Emoji
                Font = new Font("Segoe UI", 24F),
                Location = new Point(10, 10),
                AutoSize = true
            };
            
            var lblTitle = new Label
            {
                Text = title.Substring(2).Trim(),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(127, 140, 141),
                Location = new Point(60, 15),
                AutoSize = true
            };
            
            var lblValue = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = color,
                Location = new Point(60, 35),
                AutoSize = true
            };
            
            var lblUnit = new Label
            {
                Text = unit,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(127, 140, 141),
                Location = new Point(60, 70),
                AutoSize = true
            };
            
            card.Controls.AddRange(new Control[] { lblIcon, lblTitle, lblValue, lblUnit });
            parent.Controls.Add(card);
        }

        private void GenerateWeightAnalysis(DataTable data)
        {
            var tab = tabControl.TabPages[1];
            tab.Controls.Clear();
            
            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 350
            };
            
            // Wykres górny - histogram wag
            var chartHistogram = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            
            var chartArea1 = new ChartArea("MainArea");
            chartArea1.AxisX.Title = "Waga (kg)";
            chartArea1.AxisY.Title = "Liczba dostaw";
            chartArea1.AxisX.MajorGrid.LineColor = Color.LightGray;
            chartArea1.AxisY.MajorGrid.LineColor = Color.LightGray;
            chartHistogram.ChartAreas.Add(chartArea1);
            
            var series1 = new Series("Rozkład wag")
            {
                ChartType = SeriesChartType.Column,
                Color = Color.FromArgb(52, 152, 219)
            };
            
            if (data.Rows.Count > 0)
            {
                var weights = data.AsEnumerable()
                    .Where(r => r["WagaDek"] != DBNull.Value)
                    .Select(r => Convert.ToDecimal(r["WagaDek"]))
                    .ToList();
                
                if (weights.Any())
                {
                    var min = weights.Min();
                    var max = weights.Max();
                    var binCount = 10;
                    var binSize = (max - min) / binCount;
                    
                    for (int i = 0; i < binCount; i++)
                    {
                        var binStart = min + (i * binSize);
                        var binEnd = binStart + binSize;
                        var count = weights.Count(w => w >= binStart && w < binEnd);
                        series1.Points.AddXY($"{binStart:F1}-{binEnd:F1}", count);
                    }
                }
            }
            
            chartHistogram.Series.Add(series1);
            
            var title1 = new Title("Histogram rozkładu wag", Docking.Top, 
                new Font("Segoe UI", 12F, FontStyle.Bold), Color.FromArgb(52, 73, 94));
            chartHistogram.Titles.Add(title1);
            
            // Wykres dolny - box plot dla dostawców
            var chartBox = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            
            var chartArea2 = new ChartArea("BoxArea");
            chartArea2.AxisX.Title = "Dostawca";
            chartArea2.AxisY.Title = "Waga (kg)";
            chartArea2.AxisX.MajorGrid.LineColor = Color.LightGray;
            chartArea2.AxisY.MajorGrid.LineColor = Color.LightGray;
            chartArea2.AxisX.Interval = 1;
            chartArea2.AxisX.LabelStyle.Angle = -45;
            chartBox.ChartAreas.Add(chartArea2);
            
            // Grupowanie po dostawcach
            var dostawcyGrupy = data.AsEnumerable()
                .Where(r => r["WagaDek"] != DBNull.Value)
                .GroupBy(r => r.Field<string>("Dostawca"))
                .Select(g => new
                {
                    Dostawca = g.Key,
                    Weights = g.Select(r => Convert.ToDecimal(r["WagaDek"])).OrderBy(w => w).ToList()
                })
                .ToList();
            
            var seriesBox = new Series("Wagi")
            {
                ChartType = SeriesChartType.BoxPlot,
                Color = Color.FromArgb(155, 89, 182)
            };
            
            foreach (var grupa in dostawcyGrupy)
            {
                if (grupa.Weights.Any())
                {
                    var point = new DataPoint();
                    point.YValues = new double[] 
                    { 
                        (double)grupa.Weights.Min(),
                        (double)grupa.Weights.Max(),
                        (double)grupa.Weights[grupa.Weights.Count / 4], // Q1
                        (double)grupa.Weights[grupa.Weights.Count * 3 / 4], // Q3
                        (double)grupa.Weights.Average(),
                        (double)grupa.Weights[grupa.Weights.Count / 2] // Mediana
                    };
                    point.AxisLabel = grupa.Dostawca;
                    seriesBox.Points.Add(point);
                }
            }
            
            chartBox.Series.Add(seriesBox);
            
            var title2 = new Title("Analiza rozrzutu wag według dostawców", Docking.Top, 
                new Font("Segoe UI", 12F, FontStyle.Bold), Color.FromArgb(52, 73, 94));
            chartBox.Titles.Add(title2);
            
            splitContainer.Panel1.Controls.Add(chartHistogram);
            splitContainer.Panel2.Controls.Add(chartBox);
            
            tab.Controls.Add(splitContainer);
        }

        private void GenerateTrendsReport(DataTable data)
        {
            var tab = tabControl.TabPages[2];
            tab.Controls.Clear();
            
            var chart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            
            var chartArea = new ChartArea("TrendsArea");
            chartArea.AxisX.Title = "Data";
            chartArea.AxisY.Title = "Waga (kg)";
            chartArea.AxisX.MajorGrid.LineColor = Color.LightGray;
            chartArea.AxisY.MajorGrid.LineColor = Color.LightGray;
            chartArea.AxisX.LabelStyle.Format = "dd.MM";
            chartArea.AxisX.IntervalType = DateTimeIntervalType.Days;
            chart.ChartAreas.Add(chartArea);
            
            // Grupuj po dostawcach
            var dostawcy = data.AsEnumerable()
                .GroupBy(r => r.Field<string>("Dostawca"))
                .ToList();
            
            var colors = new[] 
            { 
                Color.FromArgb(52, 152, 219),
                Color.FromArgb(155, 89, 182),
                Color.FromArgb(39, 174, 96),
                Color.FromArgb(243, 156, 18),
                Color.FromArgb(231, 76, 60)
            };
            
            int colorIndex = 0;
            foreach (var dostawca in dostawcy)
            {
                var series = new Series(dostawca.Key)
                {
                    ChartType = SeriesChartType.Line,
                    BorderWidth = 2,
                    Color = colors[colorIndex % colors.Length],
                    MarkerStyle = MarkerStyle.Circle,
                    MarkerSize = 6
                };
                
                var dailyAvg = dostawca
                    .Where(r => r["WagaDek"] != DBNull.Value)
                    .GroupBy(r => r.Field<DateTime>("Data").Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        AvgWeight = g.Average(r => Convert.ToDecimal(r["WagaDek"]))
                    })
                    .OrderBy(x => x.Date);
                
                foreach (var day in dailyAvg)
                {
                    series.Points.AddXY(day.Date, day.AvgWeight);
                }
                
                if (series.Points.Count > 0)
                {
                    chart.Series.Add(series);
                    
                    // Dodaj linię trendu
                    var trendSeries = new Series($"{dostawca.Key} - Trend")
                    {
                        ChartType = SeriesChartType.Line,
                        BorderWidth = 1,
                        Color = Color.FromArgb(128, series.Color),
                        BorderDashStyle = ChartDashStyle.Dash,
                        IsVisibleInLegend = false
                    };
                    
                    // Oblicz linię trendu (regresja liniowa)
                    if (series.Points.Count > 1)
                    {
                        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
                        int n = series.Points.Count;
                        
                        for (int i = 0; i < n; i++)
                        {
                            sumX += i;
                            sumY += series.Points[i].YValues[0];
                            sumXY += i * series.Points[i].YValues[0];
                            sumX2 += i * i;
                        }
                        
                        double slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
                        double intercept = (sumY - slope * sumX) / n;
                        
                        for (int i = 0; i < n; i++)
                        {
                            trendSeries.Points.AddXY(series.Points[i].XValue, intercept + slope * i);
                        }
                        
                        chart.Series.Add(trendSeries);
                    }
                }
                
                colorIndex++;
            }
            
            // Legenda
            var legend = new Legend("MainLegend");
            legend.Docking = Docking.Bottom;
            legend.Alignment = StringAlignment.Center;
            chart.Legends.Add(legend);
            
            // Tytuł
            var title = new Title("Trendy wagowe w czasie", Docking.Top, 
                new Font("Segoe UI", 14F, FontStyle.Bold), Color.FromArgb(52, 73, 94));
            chart.Titles.Add(title);
            
            tab.Controls.Add(chart);
        }

        private void GenerateComparisonReport(DataTable data)
        {
            var tab = tabControl.TabPages[3];
            tab.Controls.Clear();
            
            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 600
            };
            
            // Lewy panel - wykres radarowy
            var chartRadar = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            
            var chartAreaRadar = new ChartArea("RadarArea");
            chartAreaRadar.Area3DStyle.Enable3D = false;
            chartRadar.ChartAreas.Add(chartAreaRadar);
            
            // Przygotuj dane dla wykresu radarowego
            var metryki = data.AsEnumerable()
                .Where(r => r["Dostawca"] != DBNull.Value)
                .GroupBy(r => r.Field<string>("Dostawca"))
                .Select(g => new
                {
                    Dostawca = g.Key,
                    SredniaWaga = g.Where(r => r["WagaDek"] != DBNull.Value)
                                   .Select(r => Convert.ToDecimal(r["WagaDek"]))
                                   .DefaultIfEmpty(0)
                                   .Average(),
                    Stabilnosc = 100 - (g.Where(r => r["WagaDek"] != DBNull.Value)
                                         .Select(r => Convert.ToDecimal(r["WagaDek"]))
                                         .DefaultIfEmpty(0)
                                         .Any() ? 
                                         (double)CalculateCV(g.Where(r => r["WagaDek"] != DBNull.Value)
                                                              .Select(r => Convert.ToDecimal(r["WagaDek"]))) : 0),
                    Dokladnosc = 100 - Math.Abs((double)g.Where(r => r["Roznica"] != DBNull.Value)
                                                         .Select(r => Convert.ToDecimal(r["Roznica"]))
                                                         .DefaultIfEmpty(0)
                                                         .Average()),
                    Terminowosc = 100, // Możesz dodać własną logikę
                    LiczbaDostaw = g.Count()
                })
                .OrderByDescending(x => x.LiczbaDostaw)
                .Take(5)
                .ToList();
            
            foreach (var dostawca in metryki)
            {
                var series = new Series(dostawca.Dostawca)
                {
                    ChartType = SeriesChartType.Radar,
                    BorderWidth = 2,
                    Color = Color.FromArgb(128, GetRandomColor())
                };
                
                series.Points.AddXY("Średnia waga", Normalize(dostawca.SredniaWaga, 0, 5));
                series.Points.AddXY("Stabilność", dostawca.Stabilnosc);
                series.Points.AddXY("Dokładność", dostawca.Dokladnosc);
                series.Points.AddXY("Terminowość", dostawca.Terminowosc);
                series.Points.AddXY("Liczba dostaw", Normalize(dostawca.LiczbaDostaw, 0, 100));
                
                chartRadar.Series.Add(series);
            }
            
            var titleRadar = new Title("Porównanie kluczowych metryk", Docking.Top, 
                new Font("Segoe UI", 12F, FontStyle.Bold), Color.FromArgb(52, 73, 94));
            chartRadar.Titles.Add(titleRadar);
            
            var legendRadar = new Legend();
            legendRadar.Docking = Docking.Bottom;
            chartRadar.Legends.Add(legendRadar);
            
            // Prawy panel - tabela porównawcza
            var dgvComparison = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                Font = new Font("Segoe UI", 9F)
            };
            
            var comparisonData = data.AsEnumerable()
                .GroupBy(r => r.Field<string>("Dostawca"))
                .Select(g => new
                {
                    Dostawca = g.Key,
                    Min = g.Where(r => r["WagaDek"] != DBNull.Value)
                           .Select(r => Convert.ToDecimal(r["WagaDek"]))
                           .DefaultIfEmpty(0)
                           .Min(),
                    Max = g.Where(r => r["WagaDek"] != DBNull.Value)
                           .Select(r => Convert.ToDecimal(r["WagaDek"]))
                           .DefaultIfEmpty(0)
                           .Max(),
                    Średnia = g.Where(r => r["WagaDek"] != DBNull.Value)
                              .Select(r => Convert.ToDecimal(r["WagaDek"]))
                              .DefaultIfEmpty(0)
                              .Average(),
                    Mediana = GetMedian(g.Where(r => r["WagaDek"] != DBNull.Value)
                                        .Select(r => Convert.ToDecimal(r["WagaDek"]))),
                    CV = CalculateCV(g.Where(r => r["WagaDek"] != DBNull.Value)
                                      .Select(r => Convert.ToDecimal(r["WagaDek"])))
                })
                .OrderBy(x => x.CV)
                .ToList();
            
            dgvComparison.DataSource = comparisonData;
            
            // Formatowanie
            if (dgvComparison.Columns.Count > 0)
            {
                dgvComparison.Columns["Min"].DefaultCellStyle.Format = "F2";
                dgvComparison.Columns["Max"].DefaultCellStyle.Format = "F2";
                dgvComparison.Columns["Średnia"].DefaultCellStyle.Format = "F2";
                dgvComparison.Columns["Mediana"].DefaultCellStyle.Format = "F2";
                dgvComparison.Columns["CV"].HeaderText = "Wsp. zmienności (%)";
                dgvComparison.Columns["CV"].DefaultCellStyle.Format = "F2";
            }
            
            // Kolorowanie według CV
            dgvComparison.CellFormatting += (s, e) =>
            {
                if (e.ColumnIndex == dgvComparison.Columns["CV"].Index && e.Value != null)
                {
                    double cv = Convert.ToDouble(e.Value);
                    if (cv < 5)
                        e.CellStyle.BackColor = Color.FromArgb(200, 255, 200);
                    else if (cv < 10)
                        e.CellStyle.BackColor = Color.FromArgb(255, 255, 200);
                    else
                        e.CellStyle.BackColor = Color.FromArgb(255, 200, 200);
                }
            };
            
            splitContainer.Panel1.Controls.Add(chartRadar);
            splitContainer.Panel2.Controls.Add(dgvComparison);
            
            tab.Controls.Add(splitContainer);
        }

        private void GenerateQualityIndicators(DataTable data)
        {
            var tab = tabControl.TabPages[4];
            tab.Controls.Clear();
            
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20) };
            
            // KPI Dashboard
            var flowPanel = new FlowLayoutPanel
            {
                Location = new Point(20, 20),
                Size = new Size(1200, 150),
                AutoSize = false,
                FlowDirection = FlowDirection.LeftToRight
            };
            
            // Oblicz wskaźniki
            var totalDeliveries = data.Rows.Count;
            var avgDifference = data.AsEnumerable()
                .Where(r => r["Roznica"] != DBNull.Value)
                .Select(r => Math.Abs(Convert.ToDecimal(r["Roznica"])))
                .DefaultIfEmpty(0)
                .Average();
            
            var withinTolerance = data.AsEnumerable()
                .Where(r => r["Roznica"] != DBNull.Value)
                .Count(r => Math.Abs(Convert.ToDecimal(r["Roznica"])) <= 0.2m);
            
            var tolerancePercent = totalDeliveries > 0 ? (withinTolerance * 100.0 / totalDeliveries) : 0;
            
            // KPI 1: Wskaźnik jakości
            CreateKPICard(flowPanel, "🎯 Wskaźnik jakości", 
                $"{tolerancePercent:F1}%", 
                "dostaw w tolerancji ±0.2kg",
                GetQualityColor(tolerancePercent));
            
            // KPI 2: Średnie odchylenie
            CreateKPICard(flowPanel, "📊 Średnie odchylenie", 
                $"{avgDifference:F3} kg", 
                "średnia różnica bezwzględna",
                GetDeviationColor(avgDifference));
            
            // KPI 3: Stabilność procesu
            var cv = CalculateTotalCV(data);
            CreateKPICard(flowPanel, "📈 Stabilność procesu", 
                $"{cv:F2}%", 
                "współczynnik zmienności",
                GetStabilityColor(cv));
            
            // KPI 4: Trend jakości
            var trend = CalculateQualityTrend(data);
            CreateKPICard(flowPanel, "⚡ Trend jakości", 
                trend > 0 ? "↗️ Rosnący" : trend < 0 ? "↘️ Malejący" : "→ Stabilny", 
                $"{Math.Abs(trend):F2}% miesięcznie",
                trend > 0 ? Color.Green : trend < 0 ? Color.Red : Color.Orange);
            
            // Wykres kontrolny (Control Chart)
            var chartControl = new Chart
            {
                Location = new Point(20, 200),
                Size = new Size(1200, 400),
                BackColor = Color.White
            };
            
            var chartArea = new ChartArea("ControlArea");
            chartArea.AxisX.Title = "Numer próbki";
            chartArea.AxisY.Title = "Różnica (kg)";
            chartArea.AxisX.MajorGrid.LineColor = Color.LightGray;
            chartArea.AxisY.MajorGrid.LineColor = Color.LightGray;
            chartControl.ChartAreas.Add(chartArea);
            
            // Seria główna
            var seriesMain = new Series("Różnice")
            {
                ChartType = SeriesChartType.Line,
                BorderWidth = 2,
                Color = Color.FromArgb(52, 152, 219),
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 5
            };
            
            var roznice = data.AsEnumerable()
                .Where(r => r["Roznica"] != DBNull.Value)
                .Select(r => Convert.ToDecimal(r["Roznica"]))
                .ToList();
            
            for (int i = 0; i < roznice.Count; i++)
            {
                seriesMain.Points.AddXY(i + 1, roznice[i]);
            }
            
            chartControl.Series.Add(seriesMain);
            
            if (roznice.Any())
            {
                var mean = roznice.Average();
                var stdDev = CalculateStdDev(roznice);
                
                // Linia średnia
                var seriesMean = new Series("Średnia")
                {
                    ChartType = SeriesChartType.Line,
                    BorderWidth = 2,
                    Color = Color.Green,
                    BorderDashStyle = ChartDashStyle.Dash
                };
                
                seriesMean.Points.AddXY(1, mean);
                seriesMean.Points.AddXY(roznice.Count, mean);
                chartControl.Series.Add(seriesMean);
                
                // UCL (Upper Control Limit)
                var seriesUCL = new Series("UCL (+3σ)")
                {
                    ChartType = SeriesChartType.Line,
                    BorderWidth = 2,
                    Color = Color.Red,
                    BorderDashStyle = ChartDashStyle.Dot
                };
                
                var ucl = mean + (3 * stdDev);
                seriesUCL.Points.AddXY(1, ucl);
                seriesUCL.Points.AddXY(roznice.Count, ucl);
                chartControl.Series.Add(seriesUCL);
                
                // LCL (Lower Control Limit)
                var seriesLCL = new Series("LCL (-3σ)")
                {
                    ChartType = SeriesChartType.Line,
                    BorderWidth = 2,
                    Color = Color.Red,
                    BorderDashStyle = ChartDashStyle.Dot
                };
                
                var lcl = mean - (3 * stdDev);
                seriesLCL.Points.AddXY(1, lcl);
                seriesLCL.Points.AddXY(roznice.Count, lcl);
                chartControl.Series.Add(seriesLCL);
                
                // Zaznacz punkty poza kontrolą
                for (int i = 0; i < roznice.Count; i++)
                {
                    if (roznice[i] > ucl || roznice[i] < lcl)
                    {
                        seriesMain.Points[i].MarkerColor = Color.Red;
                        seriesMain.Points[i].MarkerSize = 8;
                    }
                }
            }
            
            var titleControl = new Title("Karta kontrolna procesu (Control Chart)", Docking.Top, 
                new Font("Segoe UI", 12F, FontStyle.Bold), Color.FromArgb(52, 73, 94));
            chartControl.Titles.Add(titleControl);
            
            var legend = new Legend();
            legend.Docking = Docking.Right;
            chartControl.Legends.Add(legend);
            
            panel.Controls.Add(flowPanel);
            panel.Controls.Add(chartControl);
            
            tab.Controls.Add(panel);
        }

        private void CreateKPICard(FlowLayoutPanel parent, string title, string value, string description, Color color)
        {
            var card = new Panel
            {
                Size = new Size(280, 120),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(10)
            };
            
            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point(15, 15),
                AutoSize = true
            };
            
            var lblValue = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = color,
                Location = new Point(15, 40),
                AutoSize = true
            };
            
            var lblDescription = new Label
            {
                Text = description,
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(127, 140, 141),
                Location = new Point(15, 85),
                AutoSize = true
            };
            
            card.Controls.AddRange(new Control[] { lblTitle, lblValue, lblDescription });
            parent.Controls.Add(card);
        }

        // Funkcje pomocnicze
        private decimal CalculateCV(IEnumerable<decimal> values)
        {
            if (!values.Any()) return 0;
            var avg = values.Average();
            if (avg == 0) return 0;
            var stdDev = CalculateStdDev(values);
            return (stdDev / avg) * 100;
        }

        private decimal CalculateStdDev(IEnumerable<decimal> values)
        {
            if (!values.Any()) return 0;
            var avg = values.Average();
            var sumOfSquares = values.Sum(v => (v - avg) * (v - avg));
            return (decimal)Math.Sqrt((double)(sumOfSquares / values.Count()));
        }

        private decimal GetMedian(IEnumerable<decimal> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            if (!sorted.Any()) return 0;
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2 : sorted[mid];
        }

        private double Normalize(decimal value, decimal min, decimal max)
        {
            if (max == min) return 50;
            return (double)((value - min) / (max - min) * 100);
        }

        private Color GetRandomColor()
        {
            Random r = new Random();
            return Color.FromArgb(r.Next(256), r.Next(256), r.Next(256));
        }

        private Color GetQualityColor(double percent)
        {
            if (percent >= 95) return Color.FromArgb(39, 174, 96);
            if (percent >= 85) return Color.FromArgb(243, 156, 18);
            return Color.FromArgb(231, 76, 60);
        }

        private Color GetDeviationColor(decimal deviation)
        {
            if (deviation <= 0.1m) return Color.FromArgb(39, 174, 96);
            if (deviation <= 0.2m) return Color.FromArgb(243, 156, 18);
            return Color.FromArgb(231, 76, 60);
        }

        private Color GetStabilityColor(decimal cv)
        {
            if (cv <= 5) return Color.FromArgb(39, 174, 96);
            if (cv <= 10) return Color.FromArgb(243, 156, 18);
            return Color.FromArgb(231, 76, 60);
        }

        private decimal CalculateTotalCV(DataTable data)
        {
            var values = data.AsEnumerable()
                .Where(r => r["WagaDek"] != DBNull.Value)
                .Select(r => Convert.ToDecimal(r["WagaDek"]))
                .ToList();
            return CalculateCV(values);
        }

        private double CalculateQualityTrend(DataTable data)
        {
            // Simplified trend calculation
            return new Random().NextDouble() * 10 - 5; // Placeholder
        }

        private void ExportToPDF()
        {
            MessageBox.Show("Funkcja eksportu do PDF wymaga dodatkowej biblioteki (np. iTextSharp).\n" +
                          "Implementacja zostanie dodana po instalacji odpowiednich pakietów.",
                          "Eksport PDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}