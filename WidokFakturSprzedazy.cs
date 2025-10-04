using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Linq;

namespace Kalendarz1
{
    public partial class WidokFakturSprzedazy : Form
    {
        private string connectionString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private bool isDataLoading = true;

        private TabControl tabControl;
        private Chart chartSprzedaz;
        private Chart chartTop10;
        private Chart chartHandlowcy;

        public string UserID { get; set; }

        private readonly Dictionary<string, string> mapaHandlowcow = new Dictionary<string, string>
        {
            { "9991", "Dawid" },
            { "9998", "Daniel, Jola" },
            { "871231", "Radek" },
            { "432143", "Ania" }
        };

        private string? _docelowyHandlowiec;

        public WidokFakturSprzedazy()
        {
            InitializeComponent();
            UserID = "11111";
            ApplyModernTheme();
            this.Resize += WidokFakturSprzedazy_Resize;
        }

        private void ApplyModernTheme()
        {
            this.BackColor = ColorTranslator.FromHtml("#f5f7fa");
            this.Font = new Font("Segoe UI", 9F);
        }

        private void WidokFakturSprzedazy_Resize(object? sender, EventArgs e)
        {
            AktualizujRozmiaryCzcionek();
            AktualizujRozmiaryKontrolek();
        }

        private void AktualizujRozmiaryCzcionek()
        {
            float skala = Math.Min(this.Width / 1477f, this.Height / 657f);
            float bazowyCzcionki = 9f * skala;

            this.Font = new Font("Segoe UI", Math.Max(8f, bazowyCzcionki));

            if (dataGridViewOdbiorcy != null)
            {
                dataGridViewOdbiorcy.DefaultCellStyle.Font = new Font("Segoe UI", Math.Max(8f, bazowyCzcionki));
                dataGridViewOdbiorcy.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", Math.Max(8f, bazowyCzcionki), FontStyle.Bold);
            }

            if (dataGridViewNotatki != null)
            {
                dataGridViewNotatki.DefaultCellStyle.Font = new Font("Segoe UI", Math.Max(8f, bazowyCzcionki));
                dataGridViewNotatki.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", Math.Max(8f, bazowyCzcionki), FontStyle.Bold);
            }

            if (dataGridViewPlatnosci != null)
            {
                dataGridViewPlatnosci.DefaultCellStyle.Font = new Font("Segoe UI", Math.Max(8f, bazowyCzcionki));
                dataGridViewPlatnosci.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", Math.Max(8f, bazowyCzcionki), FontStyle.Bold);
            }

            if (btnShowAnalysis != null)
                btnShowAnalysis.Font = new Font("Segoe UI", Math.Max(8f, bazowyCzcionki), FontStyle.Bold);

            if (btnRefresh != null)
                btnRefresh.Font = new Font("Segoe UI", Math.Max(8f, bazowyCzcionki), FontStyle.Bold);
        }

        private void AktualizujRozmiaryKontrolek()
        {
            if (this.Width < 100 || this.Height < 100) return;

            int margines = 12;
            int gorniePasek = 45;

            int szerokoscLewej = (int)(this.ClientSize.Width * 0.48);
            int szerokoscPrawej = this.ClientSize.Width - szerokoscLewej - margines * 3;
            int wysokosc = this.ClientSize.Height - gorniePasek - margines;

            if (dataGridViewOdbiorcy != null)
            {
                dataGridViewOdbiorcy.Location = new Point(margines, gorniePasek);
                dataGridViewOdbiorcy.Size = new Size(szerokoscLewej, wysokosc);
            }

            if (tabControl != null)
            {
                tabControl.Location = new Point(szerokoscLewej + margines * 2, gorniePasek);
                tabControl.Size = new Size(szerokoscPrawej, wysokosc);

                if (dataGridViewNotatki != null && dataGridViewNotatki.Parent != null)
                    dataGridViewNotatki.Size = new Size(szerokoscPrawej - 20, wysokosc - 40);

                if (dataGridViewPlatnosci != null && dataGridViewPlatnosci.Parent != null)
                    dataGridViewPlatnosci.Size = new Size(szerokoscPrawej - 20, wysokosc - 40);

                if (chartSprzedaz != null && chartSprzedaz.Parent != null)
                    chartSprzedaz.Size = new Size(szerokoscPrawej - 20, wysokosc - 40);

                if (chartTop10 != null && chartTop10.Parent != null)
                    chartTop10.Size = new Size(szerokoscPrawej - 20, wysokosc - 40);

                if (chartHandlowcy != null && chartHandlowcy.Parent != null)
                    chartHandlowcy.Size = new Size(szerokoscPrawej - 20, wysokosc - 40);
            }
        }

        private void WidokFakturSprzedazy_Load(object? sender, EventArgs e)
        {
            if (UserID == "11111")
            {
                _docelowyHandlowiec = null;
                this.Text = "System Zarządzania Fakturami Sprzedaży - [ADMINISTRATOR]";
            }
            else if (mapaHandlowcow.ContainsKey(UserID))
            {
                _docelowyHandlowiec = mapaHandlowcow[UserID];
                this.Text = $"System Zarządzania Fakturami Sprzedaży - [{_docelowyHandlowiec}]";
            }
            else
            {
                MessageBox.Show("Nieznany lub nieprawidłowy identyfikator użytkownika.", "Błąd logowania", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _docelowyHandlowiec = "____BRAK_UPRAWNIEN____";
            }

            dateTimePickerDo.Value = DateTime.Today;
            dateTimePickerOd.Value = DateTime.Today.AddMonths(-3);

            KonfigurujDataGridViewDokumenty();
            KonfigurujDataGridViewPozycje();
            KonfigurujDataGridViewPlatnosci();

            StworzZakladkiAnalityczne();

            WczytajPlatnosciPerKontrahent(_docelowyHandlowiec);
            ZaladujTowary();
            ZaladujKontrahentow();

            dataGridViewNotatki.RowHeadersVisible = false;
            dataGridViewOdbiorcy.RowHeadersVisible = false;
            dataGridViewPlatnosci.RowHeadersVisible = false;

            StylizujPrzyciski();
            StylizujKomboBoxes();
            StylizujDataGridViews();

            isDataLoading = false;
            OdswiezDaneGlownejSiatki();

            AktualizujRozmiaryCzcionek();
            AktualizujRozmiaryKontrolek();
        }

        private void StworzZakladkiAnalityczne()
        {
            tabControl = new TabControl();
            tabControl.Location = new Point(733, 45);
            tabControl.Size = new Size(732, 600);
            tabControl.Font = new Font("Segoe UI", 9F);

            TabPage tabSzczegoly = new TabPage("Szczegóły dokumentu");
            dataGridViewNotatki.Parent = tabSzczegoly;
            dataGridViewNotatki.Location = new Point(10, 10);
            dataGridViewNotatki.Size = new Size(710, 560);

            TabPage tabPlatnosci = new TabPage("Płatności");
            dataGridViewPlatnosci.Parent = tabPlatnosci;
            dataGridViewPlatnosci.Location = new Point(10, 10);
            dataGridViewPlatnosci.Size = new Size(710, 560);

            TabPage tabWykres = new TabPage("Sprzedaż miesięczna");
            chartSprzedaz = new Chart();
            chartSprzedaz.Parent = tabWykres;
            chartSprzedaz.Location = new Point(10, 10);
            chartSprzedaz.Size = new Size(710, 560);
            chartSprzedaz.BackColor = Color.White;
            KonfigurujWykresSprzedazy();

            TabPage tabTop10 = new TabPage("Top 10 - ilości");
            chartTop10 = new Chart();
            chartTop10.Parent = tabTop10;
            chartTop10.Location = new Point(10, 10);
            chartTop10.Size = new Size(710, 560);
            chartTop10.BackColor = Color.White;
            KonfigurujWykresTop10();

            TabPage tabHandlowcy = new TabPage("Udział handlowców");
            chartHandlowcy = new Chart();
            chartHandlowcy.Parent = tabHandlowcy;
            chartHandlowcy.Location = new Point(10, 10);
            chartHandlowcy.Size = new Size(710, 560);
            chartHandlowcy.BackColor = Color.White;
            KonfigurujWykresHandlowcow();

            tabControl.TabPages.Add(tabSzczegoly);
            tabControl.TabPages.Add(tabPlatnosci);
            tabControl.TabPages.Add(tabWykres);
            tabControl.TabPages.Add(tabTop10);
            tabControl.TabPages.Add(tabHandlowcy);

            this.Controls.Add(tabControl);

            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
        }

        private void TabControl_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (tabControl.SelectedIndex == 2)
            {
                OdswiezWykresSprzedazy();
            }
            else if (tabControl.SelectedIndex == 3)
            {
                OdswiezWykresTop10();
            }
            else if (tabControl.SelectedIndex == 4)
            {
                OdswiezWykresHandlowcow();
            }
        }

        private void KonfigurujWykresSprzedazy()
        {
            chartSprzedaz.ChartAreas.Clear();
            ChartArea area = new ChartArea("MainArea");
            area.AxisX.Title = "Miesiąc";
            area.AxisX.TitleFont = new Font("Segoe UI", 10F, FontStyle.Bold);
            area.AxisY.Title = "Wartość sprzedaży (zł)";
            area.AxisY.TitleFont = new Font("Segoe UI", 10F, FontStyle.Bold);
            area.BackColor = Color.WhiteSmoke;
            chartSprzedaz.ChartAreas.Add(area);

            chartSprzedaz.Titles.Clear();
            Title title = new Title("Miesięczna wartość sprzedaży");
            title.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            chartSprzedaz.Titles.Add(title);

            chartSprzedaz.Legends.Clear();
            Legend legend = new Legend("Legend");
            legend.Docking = Docking.Bottom;
            chartSprzedaz.Legends.Add(legend);
        }

        private void KonfigurujWykresTop10()
        {
            chartTop10.ChartAreas.Clear();
            ChartArea area = new ChartArea("MainArea");
            area.AxisX.Title = "Kontrahent";
            area.AxisX.TitleFont = new Font("Segoe UI", 10F, FontStyle.Bold);
            area.AxisY.Title = "Ilość (kg)";
            area.AxisY.TitleFont = new Font("Segoe UI", 10F, FontStyle.Bold);
            area.BackColor = Color.WhiteSmoke;
            chartTop10.ChartAreas.Add(area);

            chartTop10.Titles.Clear();
            Title title = new Title("Top 10 kontrahentów według ilości (kg)");
            title.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            chartTop10.Titles.Add(title);
        }

        private void KonfigurujWykresHandlowcow()
        {
            chartHandlowcy.ChartAreas.Clear();
            ChartArea area = new ChartArea("MainArea");
            area.BackColor = Color.WhiteSmoke;
            chartHandlowcy.ChartAreas.Add(area);

            chartHandlowcy.Titles.Clear();
            Title title = new Title("Procentowy udział handlowców w sprzedaży");
            title.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            chartHandlowcy.Titles.Add(title);

            chartHandlowcy.Legends.Clear();
            Legend legend = new Legend("Legend");
            legend.Docking = Docking.Right;
            legend.Font = new Font("Segoe UI", 10F);
            chartHandlowcy.Legends.Add(legend);
        }

        private void OdswiezWykresSprzedazy()
        {
            string query = @"
                DECLARE @DataOd DATE = @pDataOd;
                DECLARE @DataDo DATE = @pDataDo;
                DECLARE @NazwaHandlowca NVARCHAR(100) = @pNazwaHandlowca;
                
                SELECT 
                    YEAR(DK.data) AS Rok,
                    MONTH(DK.data) AS Miesiac,
                    SUM(DP.wartNetto) AS WartoscSprzedazy
                FROM [HANDEL].[HM].[DK] DK
                INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                WHERE DK.data >= @DataOd 
                  AND DK.data <= @DataDo
                  AND (@NazwaHandlowca IS NULL OR WYM.CDim_Handlowiec_Val = @NazwaHandlowca)
                GROUP BY YEAR(DK.data), MONTH(DK.data)
                ORDER BY Rok, Miesiac;";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@pDataOd", dateTimePickerOd.Value.Date);
                    cmd.Parameters.AddWithValue("@pDataDo", dateTimePickerDo.Value.Date);
                    cmd.Parameters.AddWithValue("@pNazwaHandlowca", (object)_docelowyHandlowiec ?? DBNull.Value);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    chartSprzedaz.Series.Clear();
                    Series series = new Series("Wartość sprzedaży");
                    series.ChartType = SeriesChartType.Column;
                    series.Color = ColorTranslator.FromHtml("#3498db");
                    series.BorderWidth = 2;
                    series.IsValueShownAsLabel = true;
                    series.LabelFormat = "#,##0";

                    var polskieKultura = new CultureInfo("pl-PL");

                    foreach (DataRow row in dt.Rows)
                    {
                        int rok = Convert.ToInt32(row["Rok"]);
                        int miesiac = Convert.ToInt32(row["Miesiac"]);
                        decimal wartosc = Convert.ToDecimal(row["WartoscSprzedazy"]);

                        string etykieta = new DateTime(rok, miesiac, 1).ToString("MMMM yyyy", polskieKultura);
                        var point = series.Points.AddXY(etykieta, wartosc);
                        series.Points[point].AxisLabel = etykieta;
                    }

                    chartSprzedaz.Series.Add(series);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas generowania wykresu: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OdswiezWykresTop10()
        {
            string query = @"
                DECLARE @DataOd DATE = @pDataOd;
                DECLARE @DataDo DATE = @pDataDo;
                DECLARE @NazwaHandlowca NVARCHAR(100) = @pNazwaHandlowca;
                
                SELECT TOP 10
                       C.shortcut AS Kontrahent,
                       SUM(DP.ilosc) AS IloscKG
                FROM [HANDEL].[HM].[DK] DK
                INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                WHERE DK.data >= @DataOd 
                  AND DK.data <= @DataDo
                  AND (@NazwaHandlowca IS NULL OR WYM.CDim_Handlowiec_Val = @NazwaHandlowca)
                GROUP BY C.shortcut
                ORDER BY IloscKG DESC;";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@pDataOd", dateTimePickerOd.Value.Date);
                    cmd.Parameters.AddWithValue("@pDataDo", dateTimePickerDo.Value.Date);
                    cmd.Parameters.AddWithValue("@pNazwaHandlowca", (object)_docelowyHandlowiec ?? DBNull.Value);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    chartTop10.Series.Clear();
                    Series series = new Series("Ilość");
                    series.ChartType = SeriesChartType.Column;
                    series.IsValueShownAsLabel = true;
                    series.LabelFormat = "#,##0";

                    Color[] colors = {
                        ColorTranslator.FromHtml("#e74c3c"),
                        ColorTranslator.FromHtml("#e67e22"),
                        ColorTranslator.FromHtml("#f39c12"),
                        ColorTranslator.FromHtml("#f1c40f"),
                        ColorTranslator.FromHtml("#2ecc71"),
                        ColorTranslator.FromHtml("#1abc9c"),
                        ColorTranslator.FromHtml("#3498db"),
                        ColorTranslator.FromHtml("#9b59b6"),
                        ColorTranslator.FromHtml("#95a5a6"),
                        ColorTranslator.FromHtml("#7f8c8d")
                    };

                    int colorIdx = 0;
                    foreach (DataRow row in dt.Rows)
                    {
                        string kontrahent = row["Kontrahent"].ToString();
                        decimal ilosc = Convert.ToDecimal(row["IloscKG"]);

                        var point = series.Points.AddXY(kontrahent, ilosc);
                        series.Points[point].Color = colors[colorIdx % colors.Length];
                        colorIdx++;
                    }

                    chartTop10.Series.Add(series);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas generowania wykresu Top 10: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OdswiezWykresHandlowcow()
        {
            string query = @"
                DECLARE @DataOd DATE = @pDataOd;
                DECLARE @DataDo DATE = @pDataDo;
                
                SELECT 
                    ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
                    SUM(DP.wartNetto) AS WartoscSprzedazy
                FROM [HANDEL].[HM].[DK] DK
                INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                WHERE DK.data >= @DataOd 
                  AND DK.data <= @DataDo
                GROUP BY WYM.CDim_Handlowiec_Val
                ORDER BY WartoscSprzedazy DESC;";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@pDataOd", dateTimePickerOd.Value.Date);
                    cmd.Parameters.AddWithValue("@pDataDo", dateTimePickerDo.Value.Date);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    chartHandlowcy.Series.Clear();
                    Series series = new Series("Udział");
                    series.ChartType = SeriesChartType.Pie;
                    series.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                    series["PieLabelStyle"] = "Outside";
                    series["PieLineColor"] = "Black";

                    decimal suma = 0;
                    foreach (DataRow row in dt.Rows)
                    {
                        suma += Convert.ToDecimal(row["WartoscSprzedazy"]);
                    }

                    Color[] colors = {
                        ColorTranslator.FromHtml("#3498db"),
                        ColorTranslator.FromHtml("#2ecc71"),
                        ColorTranslator.FromHtml("#e74c3c"),
                        ColorTranslator.FromHtml("#f39c12"),
                        ColorTranslator.FromHtml("#9b59b6"),
                        ColorTranslator.FromHtml("#1abc9c"),
                        ColorTranslator.FromHtml("#e67e22"),
                        ColorTranslator.FromHtml("#95a5a6")
                    };

                    int colorIdx = 0;
                    foreach (DataRow row in dt.Rows)
                    {
                        string handlowiec = row["Handlowiec"].ToString();
                        decimal wartosc = Convert.ToDecimal(row["WartoscSprzedazy"]);
                        decimal procent = (wartosc / suma) * 100;

                        var point = series.Points.AddXY(handlowiec, wartosc);
                        series.Points[point].Color = colors[colorIdx % colors.Length];
                        series.Points[point].Label = $"{procent:F1}%";
                        series.Points[point].LegendText = $"{handlowiec} ({wartosc:N0} zł)";
                        colorIdx++;
                    }

                    chartHandlowcy.Series.Add(series);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas generowania wykresu handlowców: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StylizujKomboBoxes()
        {
            comboBoxTowar.FlatStyle = FlatStyle.Flat;
            comboBoxTowar.BackColor = Color.White;

            comboBoxKontrahent.FlatStyle = FlatStyle.Flat;
            comboBoxKontrahent.BackColor = Color.White;
        }

        private void StylizujDataGridViews()
        {
            dataGridViewOdbiorcy.EnableHeadersVisualStyles = false;
            dataGridViewOdbiorcy.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#34495e");
            dataGridViewOdbiorcy.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dataGridViewOdbiorcy.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dataGridViewOdbiorcy.ColumnHeadersHeight = 35;
            dataGridViewOdbiorcy.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#ecf0f1");
            dataGridViewOdbiorcy.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#3498db");
            dataGridViewOdbiorcy.DefaultCellStyle.SelectionForeColor = Color.White;
            dataGridViewOdbiorcy.GridColor = ColorTranslator.FromHtml("#bdc3c7");
            dataGridViewOdbiorcy.BorderStyle = BorderStyle.None;
            dataGridViewOdbiorcy.AllowUserToResizeRows = false;
            dataGridViewOdbiorcy.RowTemplate.Resizable = DataGridViewTriState.False;

            dataGridViewNotatki.EnableHeadersVisualStyles = false;
            dataGridViewNotatki.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#16a085");
            dataGridViewNotatki.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dataGridViewNotatki.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dataGridViewNotatki.ColumnHeadersHeight = 35;
            dataGridViewNotatki.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#e8f8f5");
            dataGridViewNotatki.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#1abc9c");
            dataGridViewNotatki.GridColor = ColorTranslator.FromHtml("#bdc3c7");
            dataGridViewNotatki.BorderStyle = BorderStyle.None;
            dataGridViewNotatki.AllowUserToResizeRows = false;
            dataGridViewNotatki.RowTemplate.Resizable = DataGridViewTriState.False;

            dataGridViewPlatnosci.EnableHeadersVisualStyles = false;
            dataGridViewPlatnosci.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#c0392b");
            dataGridViewPlatnosci.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dataGridViewPlatnosci.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dataGridViewPlatnosci.ColumnHeadersHeight = 35;
            dataGridViewPlatnosci.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#fadbd8");
            dataGridViewPlatnosci.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#e74c3c");
            dataGridViewPlatnosci.GridColor = ColorTranslator.FromHtml("#bdc3c7");
            dataGridViewPlatnosci.BorderStyle = BorderStyle.None;
            dataGridViewPlatnosci.AllowUserToResizeRows = false;
            dataGridViewPlatnosci.RowTemplate.Resizable = DataGridViewTriState.False;
        }

        private void StylizujPrzyciski()
        {
            if (btnShowAnalysis != null)
            {
                btnShowAnalysis.FlatStyle = FlatStyle.Flat;
                btnShowAnalysis.FlatAppearance.BorderSize = 0;
                btnShowAnalysis.Cursor = Cursors.Hand;
                btnShowAnalysis.BackColor = ColorTranslator.FromHtml("#9b59b6");
                btnShowAnalysis.ForeColor = Color.White;
                btnShowAnalysis.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

                var tooltip = new ToolTip();
                tooltip.SetToolTip(btnShowAnalysis, "Wybierz kontrahenta z listy dokumentów, aby wyświetlić analizę tygodniową");
            }

            if (btnRefresh != null)
            {
                btnRefresh.FlatStyle = FlatStyle.Flat;
                btnRefresh.FlatAppearance.BorderSize = 0;
                btnRefresh.Cursor = Cursors.Hand;
                btnRefresh.BackColor = ColorTranslator.FromHtml("#3498db");
                btnRefresh.ForeColor = Color.White;
                btnRefresh.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            }
        }

        private void btnRefresh_Click(object? sender, EventArgs e)
        {
            if (dateTimePickerOd.Value > dateTimePickerDo.Value)
            {
                MessageBox.Show("Data początkowa nie może być późniejsza niż data końcowa!",
                    "Błąd zakresu dat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            OdswiezDaneGlownejSiatki();

            if (tabControl.SelectedIndex == 2)
                OdswiezWykresSprzedazy();
            else if (tabControl.SelectedIndex == 3)
                OdswiezWykresTop10();
            else if (tabControl.SelectedIndex == 4)
                OdswiezWykresHandlowcow();
        }

        private void btnShowAnalysis_Click(object? sender, EventArgs e)
        {
            if (dataGridViewOdbiorcy.SelectedRows.Count > 0)
            {
                var selectedRow = dataGridViewOdbiorcy.SelectedRows[0];
                if (!Convert.ToBoolean(selectedRow.Cells["IsGroupRow"].Value) &&
                    selectedRow.Cells["khid"].Value != DBNull.Value)
                {
                    int idKontrahenta = Convert.ToInt32(selectedRow.Cells["khid"].Value);
                    string nazwaKontrahenta = selectedRow.Cells["NazwaFirmy"].Value?.ToString() ?? "Nieznany";
                    int? towarId = (comboBoxTowar.SelectedValue != null && (int)comboBoxTowar.SelectedValue != 0)
                        ? (int?)comboBoxTowar.SelectedValue : null;
                    string nazwaTowaru = towarId.HasValue ? comboBoxTowar.Text : "Wszystkie towary";

                    using (var analizaForm = new AnalizaTygodniowaForm(
                        connectionString,
                        idKontrahenta,
                        towarId,
                        nazwaKontrahenta,
                        nazwaTowaru))
                    {
                        analizaForm.ShowDialog(this);
                    }
                }
            }
            else
            {
                MessageBox.Show("Proszę wybrać kontrahenta z listy dokumentów.",
                    "Brak wyboru", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void KonfigurujDataGridViewDokumenty()
        {
            dataGridViewOdbiorcy.AutoGenerateColumns = false;
            dataGridViewOdbiorcy.Columns.Clear();
            dataGridViewOdbiorcy.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewOdbiorcy.MultiSelect = false;
            dataGridViewOdbiorcy.ReadOnly = true;
            dataGridViewOdbiorcy.AllowUserToAddRows = false;

            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "khid", DataPropertyName = "khid", Visible = false });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "ID", DataPropertyName = "ID", Visible = false });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsGroupRow", DataPropertyName = "IsGroupRow", Visible = false });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "SortDate", DataPropertyName = "SortDate", Visible = false });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "NumerDokumentu", DataPropertyName = "NumerDokumentu", HeaderText = "Numer Dokumentu", Width = 150 });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "NazwaFirmy", DataPropertyName = "NazwaFirmy", HeaderText = "Nazwa Firmy", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "IloscKG", DataPropertyName = "IloscKG", HeaderText = "Ilość KG", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "SredniaCena", DataPropertyName = "SredniaCena", HeaderText = "Śr. Cena KG", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Handlowiec", DataPropertyName = "Handlowiec", HeaderText = "Handlowiec", Width = 120 });

            dataGridViewOdbiorcy.SelectionChanged += new EventHandler(this.dataGridViewDokumenty_SelectionChanged);
            dataGridViewOdbiorcy.RowPrePaint += new DataGridViewRowPrePaintEventHandler(this.dataGridViewOdbiorcy_RowPrePaint);
            dataGridViewOdbiorcy.CellFormatting += new DataGridViewCellFormattingEventHandler(this.dataGridViewOdbiorcy_CellFormatting);
        }

        private void KonfigurujDataGridViewPlatnosci()
        {
            dataGridViewPlatnosci.AutoGenerateColumns = false;
            dataGridViewPlatnosci.Columns.Clear();
            dataGridViewPlatnosci.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewPlatnosci.MultiSelect = false;
            dataGridViewPlatnosci.ReadOnly = true;
            dataGridViewPlatnosci.AllowUserToAddRows = false;
            dataGridViewPlatnosci.AllowUserToDeleteRows = false;
            dataGridViewPlatnosci.RowHeadersVisible = false;

            dataGridViewPlatnosci.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Kontrahent",
                DataPropertyName = "Kontrahent",
                HeaderText = "Kontrahent",
                Width = 200,
            });

            dataGridViewPlatnosci.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Limit",
                DataPropertyName = "Limit",
                HeaderText = "Limit",
                Width = 120,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewPlatnosci.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "DoZaplacenia",
                DataPropertyName = "DoZaplacenia",
                HeaderText = "Do zapłacenia",
                Width = 130,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewPlatnosci.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Terminowe",
                DataPropertyName = "Terminowe",
                HeaderText = "Terminowe",
                Width = 120,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewPlatnosci.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Przeterminowane",
                DataPropertyName = "Przeterminowane",
                HeaderText = "Przeterminowane",
                Width = 140,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
        }

        private void KonfigurujDataGridViewPozycje()
        {
            dataGridViewNotatki.AutoGenerateColumns = false;
            dataGridViewNotatki.Columns.Clear();
            dataGridViewNotatki.ReadOnly = true;
            dataGridViewNotatki.Columns.Add(new DataGridViewTextBoxColumn { Name = "KodTowaru", DataPropertyName = "KodTowaru", HeaderText = "Kod Towaru", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dataGridViewNotatki.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ilosc", DataPropertyName = "Ilosc", HeaderText = "Ilość", Width = 60 });
            dataGridViewNotatki.Columns.Add(new DataGridViewTextBoxColumn { Name = "Cena", DataPropertyName = "Cena", HeaderText = "Cena Netto", Width = 60, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            dataGridViewNotatki.Columns.Add(new DataGridViewTextBoxColumn { Name = "Wartosc", DataPropertyName = "Wartosc", HeaderText = "Wartość Netto", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
        }

        private void ZaladujTowary()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT [ID], [kod] FROM [HANDEL].[HM].[TW] WHERE katalog = '67095' ORDER BY Kod ASC";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable towary = new DataTable();
                adapter.Fill(towary);
                DataRow dr = towary.NewRow();
                dr["ID"] = 0;
                dr["kod"] = "--- Wszystkie towary ---";
                towary.Rows.InsertAt(dr, 0);
                comboBoxTowar.DisplayMember = "kod";
                comboBoxTowar.ValueMember = "ID";
                comboBoxTowar.DataSource = towary;
            }
        }

        private void WczytajPlatnosciPerKontrahent(string? handlowiec)
        {
            const string sql = @"
DECLARE @Param NVARCHAR(100) = @pHandlowiec;

WITH PNAgg AS (
    SELECT PN.dkid,
           SUM(ISNULL(PN.kwotarozl,0)) AS KwotaRozliczona,
           MAX(PN.Termin)              AS TerminPrawdziwy
    FROM [HANDEL].[HM].[PN] PN
    GROUP BY PN.dkid
),
Dokumenty AS (
    SELECT DISTINCT DK.id, DK.khid, DK.walbrutto, DK.plattermin
    FROM [HANDEL].[HM].[DK] DK
    WHERE DK.anulowany = 0
      AND (
            NULLIF(LTRIM(RTRIM(@Param)), N'') IS NULL          
            OR EXISTS (
                SELECT 1
                FROM [HANDEL].[SSCommon].[ContractorClassification] W
                WHERE W.ElementId = DK.khid
                  AND (
                        (TRY_CONVERT(INT, @Param) IS NOT NULL 
                         AND TRY_CONVERT(INT, W.CDim_Handlowiec) = TRY_CONVERT(INT, @Param))  
                     OR (TRY_CONVERT(INT, @Param) IS NULL
                         AND LTRIM(RTRIM(W.CDim_Handlowiec_Val)) = LTRIM(RTRIM(@Param)))      
                  )
            )
          )
),
Saldo AS (
    SELECT D.khid,
           (D.walbrutto - ISNULL(PA.KwotaRozliczona,0)) AS DoZaplacenia,
           ISNULL(PA.TerminPrawdziwy, D.plattermin)     AS TerminPlatnosci
    FROM Dokumenty D
    LEFT JOIN PNAgg PA ON PA.dkid = D.id
)
SELECT 
    C.Shortcut AS Kontrahent,
    C.LimitAmount AS Limit,
    CAST(SUM(CASE WHEN S.DoZaplacenia > 0 THEN S.DoZaplacenia ELSE 0 END) AS DECIMAL(18,2)) AS DoZaplacenia,
    CAST(SUM(CASE WHEN S.DoZaplacenia > 0 AND GETDATE() <= S.TerminPlatnosci THEN S.DoZaplacenia ELSE 0 END) AS DECIMAL(18,2)) AS Terminowe,
    CAST(SUM(CASE WHEN S.DoZaplacenia > 0 AND GETDATE() >  S.TerminPlatnosci THEN S.DoZaplacenia ELSE 0 END) AS DECIMAL(18,2)) AS Przeterminowane
FROM Saldo S
JOIN [HANDEL].[SSCommon].[STContractors] C ON C.id = S.khid
GROUP BY C.Shortcut, C.LimitAmount
HAVING SUM(CASE WHEN S.DoZaplacenia > 0 THEN S.DoZaplacenia ELSE 0 END) > 0.01
ORDER BY DoZaplacenia DESC;";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                using (var cmd = new SqlCommand(sql, conn))
                {
                    object param = string.IsNullOrWhiteSpace(handlowiec) ? DBNull.Value : handlowiec;
                    cmd.Parameters.AddWithValue("@pHandlowiec", param);

                    var dt = new DataTable();
                    new SqlDataAdapter(cmd).Fill(dt);

                    if (dt.Rows.Count > 0)
                    {
                        decimal sumaLimit = 0m, sumaDoZap = 0m, sumaTerm = 0m, sumaPrzet = 0m;

                        foreach (DataRow r in dt.Rows)
                        {
                            if (r["Limit"] != DBNull.Value) sumaLimit += Convert.ToDecimal(r["Limit"]);
                            if (r["DoZaplacenia"] != DBNull.Value) sumaDoZap += Convert.ToDecimal(r["DoZaplacenia"]);
                            if (r["Terminowe"] != DBNull.Value) sumaTerm += Convert.ToDecimal(r["Terminowe"]);
                            if (r["Przeterminowane"] != DBNull.Value) sumaPrzet += Convert.ToDecimal(r["Przeterminowane"]);
                        }

                        var sumaRow = dt.NewRow();
                        sumaRow["Kontrahent"] = "SUMA";
                        sumaRow["Limit"] = Math.Round(sumaLimit, 2);
                        sumaRow["DoZaplacenia"] = Math.Round(sumaDoZap, 2);
                        sumaRow["Terminowe"] = Math.Round(sumaTerm, 2);
                        sumaRow["Przeterminowane"] = Math.Round(sumaPrzet, 2);

                        dt.Rows.InsertAt(sumaRow, 0);
                    }

                    dataGridViewPlatnosci.DataSource = dt;

                    foreach (var col in new[] { "Limit", "DoZaplacenia", "Terminowe", "Przeterminowane" })
                    {
                        if (dataGridViewPlatnosci.Columns.Contains(col))
                        {
                            dataGridViewPlatnosci.Columns[col].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        }
                    }

                    dataGridViewPlatnosci.CellFormatting -= DataGridViewPlatnosci_CellFormatting;
                    dataGridViewPlatnosci.CellFormatting += DataGridViewPlatnosci_CellFormatting;

                    if (dataGridViewPlatnosci.Rows.Count > 0)
                    {
                        var row0 = dataGridViewPlatnosci.Rows[0];
                        row0.DefaultCellStyle.BackColor = Color.LightGray;
                        row0.DefaultCellStyle.Font = new Font(dataGridViewPlatnosci.Font, FontStyle.Bold);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas wczytywania płatności: " + ex.Message,
                                "Błąd bazy danych", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DataGridViewPlatnosci_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.Value != null && e.ColumnIndex >= 0)
            {
                var colName = dataGridViewPlatnosci.Columns[e.ColumnIndex].Name;
                if (colName == "Limit" || colName == "DoZaplacenia" || colName == "Terminowe" || colName == "Przeterminowane")
                {
                    if (decimal.TryParse(e.Value.ToString(), out decimal val))
                    {
                        e.Value = val.ToString("N2") + " zł";
                        e.FormattingApplied = true;
                    }
                }
            }
        }

        private void ZaladujKontrahentow()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = @"
                    DECLARE @NazwaHandlowca NVARCHAR(100) = @pNazwaHandlowca;
                    SELECT DISTINCT C.id, C.shortcut AS nazwa
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
                    WHERE @NazwaHandlowca IS NULL OR WYM.CDim_Handlowiec_Val = @NazwaHandlowca
                    ORDER BY C.shortcut ASC;";

                var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@pNazwaHandlowca", (object)_docelowyHandlowiec ?? DBNull.Value);

                SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                DataTable kontrahenci = new DataTable();
                adapter.Fill(kontrahenci);

                DataRow dr = kontrahenci.NewRow();
                dr["id"] = 0;
                dr["nazwa"] = "--- Wszyscy kontrahenci ---";
                kontrahenci.Rows.InsertAt(dr, 0);
                comboBoxKontrahent.DisplayMember = "nazwa";
                comboBoxKontrahent.ValueMember = "id";
                comboBoxKontrahent.DataSource = kontrahenci;
            }
        }

        private void WczytajDokumentySprzedazy(int? towarId, int? kontrahentId)
        {
            string query = @"
DECLARE @TowarID INT = @pTowarID;
DECLARE @KontrahentID INT = @pKontrahentID;
DECLARE @NazwaHandlowca NVARCHAR(100) = @pNazwaHandlowca;
DECLARE @DataOd DATE = @pDataOd;
DECLARE @DataDo DATE = @pDataDo;

WITH AgregatyDokumentu AS (
    SELECT super AS id_dk, SUM(ilosc) AS SumaKG, SUM(wartNetto) / NULLIF(SUM(ilosc), 0) AS SredniaCena
    FROM [HANDEL].[HM].[DP] WHERE @TowarID IS NULL OR idtw = @TowarID GROUP BY super
),
DokumentyFiltrowane AS (
    SELECT DISTINCT DK.*, WYM.CDim_Handlowiec_Val 
    FROM [HANDEL].[HM].[DK] DK
    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
    WHERE 
        (@NazwaHandlowca IS NULL OR WYM.CDim_Handlowiec_Val = @NazwaHandlowca)
        AND (@KontrahentID IS NULL OR DK.khid = @KontrahentID)
        AND (@TowarID IS NULL OR EXISTS (SELECT 1 FROM [HANDEL].[HM].[DP] DP WHERE DP.super = DK.id AND DP.idtw = @TowarID))
        AND DK.data >= @DataOd
        AND DK.data <= @DataDo
)
SELECT 
    CONVERT(date, DF.data) AS SortDate, 1 AS SortOrder, 0 AS IsGroupRow,
    DF.kod AS NumerDokumentu, C.shortcut AS NazwaFirmy,
    ISNULL(AD.SumaKG, 0) AS IloscKG, ISNULL(AD.SredniaCena, 0) AS SredniaCena,
    ISNULL(DF.CDim_Handlowiec_Val, '-') AS Handlowiec, DF.khid, DF.id
FROM DokumentyFiltrowane DF
INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DF.khid = C.id
INNER JOIN AgregatyDokumentu AD ON DF.id = AD.id_dk
UNION ALL
SELECT DISTINCT
    CONVERT(date, data) AS SortDate, 0 AS SortOrder, 1 AS IsGroupRow,
    NULL, NULL, NULL, NULL, NULL, NULL, NULL
FROM DokumentyFiltrowane
ORDER BY SortDate DESC, SortOrder ASC, SredniaCena DESC;
            ";
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@pTowarID", (object)towarId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@pKontrahentID", (object)kontrahentId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@pNazwaHandlowca", (object)_docelowyHandlowiec ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@pDataOd", dateTimePickerOd.Value.Date);
                    cmd.Parameters.AddWithValue("@pDataDo", dateTimePickerDo.Value.Date.AddDays(1).AddSeconds(-1));

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    dataGridViewOdbiorcy.DataSource = dt;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas wczytywania dokumentów: " + ex.Message, "Błąd Bazy Danych", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void WczytajPozycjeDokumentu(int idDokumentu)
        {
            string query = @"SELECT DP.kod AS KodTowaru, DP.ilosc AS Ilosc, DP.cena AS Cena, DP.wartNetto AS Wartosc FROM [HANDEL].[HM].[DP] DP WHERE DP.super = @idDokumentu ORDER BY DP.lp;";
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@idDokumentu", idDokumentu);
                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    dataGridViewNotatki.DataSource = dt;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas wczytywania pozycji dokumentu: " + ex.Message, "Błąd Bazy Danych", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OdswiezDaneGlownejSiatki()
        {
            if (isDataLoading) return;
            int? selectedTowarId = (comboBoxTowar.SelectedValue != null && (int)comboBoxTowar.SelectedValue != 0) ? (int?)comboBoxTowar.SelectedValue : null;
            int? selectedKontrahentId = (comboBoxKontrahent.SelectedValue != null && (int)comboBoxKontrahent.SelectedValue != 0) ? (int?)comboBoxKontrahent.SelectedValue : null;
            WczytajDokumentySprzedazy(selectedTowarId, selectedKontrahentId);
        }

        private void comboBoxTowar_SelectedIndexChanged(object? sender, EventArgs e)
        {
            OdswiezDaneGlownejSiatki();
        }

        private void comboBoxKontrahent_SelectedIndexChanged(object? sender, EventArgs e)
        {
            OdswiezDaneGlownejSiatki();
        }

        private void dataGridViewDokumenty_SelectionChanged(object? sender, EventArgs e)
        {
            if (dataGridViewOdbiorcy.SelectedRows.Count == 0 || Convert.ToBoolean(dataGridViewOdbiorcy.SelectedRows[0].Cells["IsGroupRow"].Value))
            {
                dataGridViewNotatki.DataSource = null;
                btnShowAnalysis.Enabled = false;
                return;
            }

            DataGridViewRow selectedRow = dataGridViewOdbiorcy.SelectedRows[0];

            btnShowAnalysis.Enabled = selectedRow.Cells["khid"].Value != DBNull.Value;

            if (selectedRow.Cells["ID"].Value != DBNull.Value)
            {
                int idDokumentu = Convert.ToInt32(selectedRow.Cells["ID"].Value);
                WczytajPozycjeDokumentu(idDokumentu);
            }
        }

        private void dataGridViewOdbiorcy_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (Convert.ToBoolean(dataGridViewOdbiorcy.Rows[e.RowIndex].Cells["IsGroupRow"].Value))
            {
                var row = dataGridViewOdbiorcy.Rows[e.RowIndex];
                row.DefaultCellStyle.BackColor = Color.FromArgb(220, 220, 220);
                row.DefaultCellStyle.ForeColor = Color.Black;
                row.DefaultCellStyle.Font = new Font(dataGridViewOdbiorcy.Font, FontStyle.Bold);
                row.Height = 30;
                row.DefaultCellStyle.SelectionBackColor = row.DefaultCellStyle.BackColor;
                row.DefaultCellStyle.SelectionForeColor = row.DefaultCellStyle.ForeColor;
            }
        }

        private void dataGridViewOdbiorcy_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (Convert.ToBoolean(dataGridViewOdbiorcy.Rows[e.RowIndex].Cells["IsGroupRow"].Value))
            {
                if (e.ColumnIndex == dataGridViewOdbiorcy.Columns["NumerDokumentu"].Index)
                {
                    if (dataGridViewOdbiorcy.Rows[e.RowIndex].Cells["SortDate"].Value is DateTime dateValue)
                    {
                        e.Value = dateValue.ToString("dddd, dd MMMM yyyy", new CultureInfo("pl-PL"));
                    }
                }
                else
                {
                    e.Value = "";
                    e.FormattingApplied = true;
                }
            }
        }

        private void dataGridViewPlatnosci_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (e.RowIndex == 0 && dataGridViewPlatnosci.Rows.Count > 0)
            {
                var row = dataGridViewPlatnosci.Rows[e.RowIndex];
                if (row.Cells["Kontrahent"].Value?.ToString() == "SUMA")
                {
                    row.DefaultCellStyle.BackColor = Color.LightGray;
                    row.DefaultCellStyle.Font = new Font(dataGridViewPlatnosci.Font, FontStyle.Bold);
                }
            }
        }
    }
}