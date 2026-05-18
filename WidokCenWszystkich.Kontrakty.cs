using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1
{
    // Partial class — wydzielona logika zakładki "Kontrakty vs Wolny rynek".
    // Źródło danych: LibraNet.dbo.HarmonogramDostaw (Bufor='Potwierdzony', Cena>0, SztukiDek>0).
    // Pełna dokumentacja: BAZA_WIEDZY/27_WidokCenWszystkich_modul.md.
    public partial class WidokCenWszystkich
    {
        #region Pola Kontrakty

        private Panel kontraktyChartPanel;
        private AggregationMode _kontraktyAggMode = AggregationMode.Week;
        private readonly List<Button> _kontraktyAggButtons = new();
        private FlowLayoutPanel _kontraktyKpiBar;
        private DataGridView _kontraktyDetailGrid;
        private Label _kontraktyStatusLabel;
        private DataTable _kontraktyData;

        #endregion

        #region UI — CreateKontraktyTab

        private void CreateKontraktyTab(TabPage tab)
        {
            // Layout: kompaktowa głowa (46px) + reszta jako 2-kolumnowy split (charts | table),
            // gdzie każda kolumna ma własny KPI/mini-stats pas nad właściwą treścią
            var container = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = backgroundColor };
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));    // compact controls
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // main 2-col area

            // ── Row 0: Compact controls — all inline in one row
            var controls = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(20, 4, 20, 4) };
            controls.Paint += (s, e) => { using var p = new Pen(subtleBorderColor, 1); e.Graphics.DrawLine(p, 0, controls.Height - 1, controls.Width, controls.Height - 1); };

            var lblTitle = new Label
            {
                Text = "📋  Kontrakty vs Wolny rynek",
                Font = new Font("Segoe UI Emoji", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = true, Location = new Point(0, 11)
            };

            var lblLegend = new Label
            {
                Text = "🟢 Kontrakt   🔵 Wolny",
                Font = new Font("Segoe UI Emoji", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
                AutoSize = true, Location = new Point(260, 13)
            };

            (string Label, AggregationMode Mode)[] aggOptions =
            {
                ("Dzień",   AggregationMode.Day),
                ("Tydzień", AggregationMode.Week),
                ("Miesiąc", AggregationMode.Month),
                ("Kwartał", AggregationMode.Quarter)
            };
            int xAgg = 410;
            foreach (var (lbl, mode) in aggOptions)
            {
                var btn = new Button
                {
                    Text = lbl, Tag = mode,
                    Width = 74, Height = 26,
                    Location = new Point(xAgg, 8),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                    Cursor = Cursors.Hand,
                    BackColor = Color.White,
                    ForeColor = Color.FromArgb(71, 85, 105)
                };
                btn.FlatAppearance.BorderColor = subtleBorderColor;
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
                if (mode == _kontraktyAggMode)
                {
                    btn.BackColor = primaryColor;
                    btn.ForeColor = Color.White;
                    btn.FlatAppearance.BorderColor = primaryColor;
                }
                btn.Click += (s, e) =>
                {
                    _kontraktyAggMode = mode;
                    foreach (var b in _kontraktyAggButtons)
                    {
                        bool active = b.Tag is AggregationMode m && m == mode;
                        b.BackColor = active ? primaryColor : Color.White;
                        b.ForeColor = active ? Color.White : Color.FromArgb(71, 85, 105);
                        b.FlatAppearance.BorderColor = active ? primaryColor : subtleBorderColor;
                    }
                    UpdateKontraktyUI();
                };
                _kontraktyAggButtons.Add(btn);
                controls.Controls.Add(btn);
                xAgg += 78;
            }

            _kontraktyStatusLabel = new Label
            {
                Text = "(ładowanie danych…)",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true, Location = new Point(xAgg + 14, 13)
            };

            var helpBtn = BuildSmallHelpButton(controls,
                "Kontrakty vs Wolny rynek",
                "Wykres porównuje dwa typy dostaw żywca:\n\n" +
                "🤝 KONTRAKT — hodowcy z umową (TypCeny ∈ {rolnicza, ministerialna, łączona}). Sergiusz dostarcza paszę, hodowca rośnie kurczaki, odbierane po umówionej cenie z umowy.\n" +
                "🏪 WOLNY RYNEK — hodowcy bez umowy (TypCeny ∈ {wolnyrynek, wolnorynkowa}). Cena bieżąca dnia.\n\n" +
                "📊 ELEMENTY:\n" +
                "• KPI: udział wolumenowy + średnia ważona cena każdej kategorii\n" +
                "• Górny wykres — średnie ceny w czasie (zielona kontrakt, niebieska wolny)\n" +
                "• Dolny wykres — 100% stacked bar wolumenu (kontrakt + wolny = 100%)\n" +
                "• Tabela (prawy górny róg) — per oryginalny TypCeny (kg, % udziału, średnia cena)\n\n" +
                "Źródło: HarmonogramDostaw.TypCeny + SztukiDek × WagaDek.");

            controls.Controls.AddRange(new Control[] { lblTitle, lblLegend, _kontraktyStatusLabel, helpBtn });
            container.Controls.Add(controls, 0, 0);

            // ── Row 1: Main 2-column area
            //   Col 0 (fill): same wykresy
            //   Col 1 (360 px): 4 KPI cards stacked vertically + tabela mini-fit
            var mainArea = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                BackColor = backgroundColor,
                Padding = new Padding(0)
            };
            mainArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainArea.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
            mainArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // ── COL 0: Charts panel (cała wysokość)
            kontraktyChartPanel = new BufferedPanel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(16, 6, 8, 16) };
            kontraktyChartPanel.Paint += KontraktyChart_Paint;
            kontraktyChartPanel.Resize += (s, e) => kontraktyChartPanel.Invalidate();
            mainArea.Controls.Add(kontraktyChartPanel, 0, 0);

            // ── COL 1: KPI stacked vertically (top) + table (fill, mini-fit)
            var tableHost = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = backgroundColor, Margin = new Padding(0, 6, 16, 16) };
            tableHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tableHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // 4 KPI cards vertical stack
            tableHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // tabela — autosize do liczby wierszy
            tableHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // pusta przestrzeń pod tabelą

            _kontraktyKpiBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                BackColor = backgroundColor,
                Padding = new Padding(0, 0, 0, 8),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            tableHost.Controls.Add(_kontraktyKpiBar, 0, 0);

            var tableCard = new Panel { Dock = DockStyle.Top, BackColor = Color.White, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10, 8, 10, 10) };
            tableCard.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, tableCard.Width - 1, tableCard.Height - 1);
                using var path = GetRoundedRectangle(rect, 10);
                using (var bg = new SolidBrush(Color.White)) g.FillPath(bg, path);
                using (var border = new Pen(subtleBorderColor, 1)) g.DrawPath(border, path);
            };

            var lblGrid = new Label
            {
                Text = "📋  Szczegóły per TypCeny",
                Font = new Font("Segoe UI Emoji", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(2, 0, 0, 0)
            };
            _kontraktyDetailGrid = new DataGridView
            {
                Dock = DockStyle.Top,
                Height = 26 + 26 * 6, // header + max ~6 wierszy widocznych; AutoSize host dopasuje wokół
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ScrollBars = ScrollBars.Vertical,
                EnableHeadersVisualStyles = false,
                Font = new Font("Segoe UI", 8.5F),
                GridColor = Color.FromArgb(226, 232, 240)
            };
            _kontraktyDetailGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(15, 23, 42),
                Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                Padding = new Padding(2)
            };
            _kontraktyDetailGrid.ColumnHeadersHeight = 26;
            _kontraktyDetailGrid.RowTemplate.Height = 22;
            _kontraktyDetailGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            _kontraktyDetailGrid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(15, 23, 42);
            _kontraktyDetailGrid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(248, 250, 252) };
            tableCard.Controls.Add(_kontraktyDetailGrid);
            tableCard.Controls.Add(lblGrid);
            tableHost.Controls.Add(tableCard, 0, 1);
            // RowStyle Row 2 (% 100) zostawia pustą przestrzeń pod tabelą

            // Double-click → szczegóły dostaw dla wybranego TypCeny
            _kontraktyDetailGrid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _kontraktyDetailGrid.Rows.Count) return;
                var typCeny = _kontraktyDetailGrid.Rows[e.RowIndex].Cells["TypCeny"].Value?.ToString();
                if (!string.IsNullOrEmpty(typCeny)) ShowKontraktyDeliveriesDialog(typCeny);
            };

            mainArea.Controls.Add(tableHost, 1, 0);
            container.Controls.Add(mainArea, 0, 1);

            tab.Controls.Add(container);
        }

        // Odśwież KPI + grid + chart po zmianie agregacji / zakresu dat
        private void UpdateKontraktyUI()
        {
            try
            {
                BuildKontraktyKpiCards();
                BuildKontraktyDetailGrid();
                kontraktyChartPanel?.Invalidate();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Kontrakty.UpdateKontraktyUI] {ex.GetType().Name}: {ex.Message}");
            }
        }

        #endregion

        #region Dialog ze szczegółami dostaw

        private void ShowKontraktyDeliveriesDialog(string typCeny)
        {
            if (_kontraktyData == null || _kontraktyData.Rows.Count == 0) return;

            var deliveries = _kontraktyData.AsEnumerable()
                .Where(r => string.Equals(r["TypCeny"].ToString(), typCeny, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r["Data"] != DBNull.Value ? Convert.ToDateTime(r["Data"]) : DateTime.MinValue)
                .ToList();
            if (deliveries.Count == 0) return;

            // Pre-compute statystyki (raz, dla niefiltrowanego zbioru)
            string kat = deliveries[0]["Kategoria"].ToString();
            decimal totalKgAll = deliveries.Sum(r => r["WolumenKg"] != DBNull.Value ? Convert.ToDecimal(r["WolumenKg"]) : 0m);
            decimal totalSztAll = deliveries.Sum(r => r["Sztuki"] != DBNull.Value ? Convert.ToDecimal(r["Sztuki"]) : 0m);
            decimal totalRevAll = deliveries.Sum(r =>
            {
                decimal c = r["Cena"] != DBNull.Value ? Convert.ToDecimal(r["Cena"]) : 0m;
                decimal s2 = r["Sztuki"] != DBNull.Value ? Convert.ToDecimal(r["Sztuki"]) : 0m;
                return c * s2;
            });
            decimal avgCenaAll = totalSztAll > 0 ? totalRevAll / totalSztAll : 0m;
            decimal minCenaAll = deliveries.Where(r => r["Cena"] != DBNull.Value).Min(r => Convert.ToDecimal(r["Cena"]));
            decimal maxCenaAll = deliveries.Where(r => r["Cena"] != DBNull.Value).Max(r => Convert.ToDecimal(r["Cena"]));
            int dostCountAll = deliveries.Where(r => r["DostawcaID"] != DBNull.Value).Select(r => Convert.ToInt32(r["DostawcaID"])).Distinct().Count();
            DateTime minDataAll = deliveries.Where(r => r["Data"] != DBNull.Value).Min(r => Convert.ToDateTime(r["Data"]));
            DateTime maxDataAll = deliveries.Where(r => r["Data"] != DBNull.Value).Max(r => Convert.ToDateTime(r["Data"]));

            string katEmoji = kat == "Kontrakt" ? "🤝" : "🏪";
            Color katColor = kat == "Kontrakt" ? successColor : primaryColor;

            using var dlg = new Form
            {
                Text = $"Dostawy — TypCeny: {typCeny}",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(1280, 720),
                BackColor = backgroundColor,
                Font = new Font("Segoe UI", 9.5F),
                ShowInTaskbar = false,
                MinimizeBox = false,
                MaximizeBox = true,
                FormBorderStyle = FormBorderStyle.Sizable
            };
            try { WindowIconHelper.SetIcon(dlg); }
            catch (Exception ex) { Debug.WriteLine($"[Kontrakty.Dialog.SetIcon] {ex.Message}"); }

            // Layout: TableLayoutPanel 2 col × 2 row
            //   Col 0 = fill: [Row 0 search bar 44px] + [Row 1 grid fill]
            //   Col 1 = 320 px: info panel (RowSpan = 2, full height)
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2, RowCount = 2,
                BackColor = backgroundColor,
                Padding = new Padding(0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // ── Search bar (col 0, row 0)
            var searchHost = new Panel { Dock = DockStyle.Fill, BackColor = backgroundColor, Padding = new Padding(16, 8, 8, 4) };
            var lblSearch = new Label
            {
                Text = "🔍",
                Font = new Font("Segoe UI Emoji", 12F),
                AutoSize = true,
                Location = new Point(0, 6)
            };
            var txtSearch = new TextBox
            {
                Font = new Font("Segoe UI", 10F),
                Width = 600, Height = 28,
                Location = new Point(28, 4),
                PlaceholderText = "Filtruj: nazwa dostawcy, ID, typ umowy, uwagi…",
                BorderStyle = BorderStyle.FixedSingle
            };
            var lblFilterStatus = new Label
            {
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                Location = new Point(640, 8),
                Text = $"({deliveries.Count} z {deliveries.Count})"
            };
            searchHost.Controls.AddRange(new Control[] { lblSearch, txtSearch, lblFilterStatus });
            root.Controls.Add(searchHost, 0, 0);

            // ── Grid (col 0, row 1)
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false,
                Font = new Font("Segoe UI", 9F),
                GridColor = Color.FromArgb(226, 232, 240),
                Margin = new Padding(0)
            };
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(15, 23, 42),
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                Padding = new Padding(3)
            };
            grid.ColumnHeadersHeight = 32;
            grid.RowTemplate.Height = 24;
            grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(248, 250, 252) };

            grid.Columns.Add("Lp", "Lp");
            grid.Columns.Add("Data", "Data");
            grid.Columns.Add("Dostawca", "Dostawca");
            grid.Columns.Add("DostawcaID", "ID");
            grid.Columns.Add("Auta", "Auta");
            grid.Columns.Add("Sztuki", "Sztuki");
            grid.Columns.Add("Waga", "Waga śr. (kg/szt)");
            grid.Columns.Add("Kg", "Wolumen (kg)");
            grid.Columns.Add("Cena", "Cena (zł/kg)");
            grid.Columns.Add("Wartosc", "Wartość (zł)");
            grid.Columns.Add("TypUmowy", "Typ umowy");
            grid.Columns.Add("Uwagi", "Uwagi");

            grid.Columns["Lp"].FillWeight = 5;
            grid.Columns["Data"].FillWeight = 9;
            grid.Columns["Dostawca"].FillWeight = 22;
            grid.Columns["DostawcaID"].FillWeight = 5;
            grid.Columns["Auta"].FillWeight = 5;
            grid.Columns["Sztuki"].FillWeight = 7;
            grid.Columns["Waga"].FillWeight = 9;
            grid.Columns["Kg"].FillWeight = 8;
            grid.Columns["Cena"].FillWeight = 8;
            grid.Columns["Wartosc"].FillWeight = 9;
            grid.Columns["TypUmowy"].FillWeight = 8;
            grid.Columns["Uwagi"].FillWeight = 13;

            foreach (var col in new[] { "Lp", "DostawcaID", "Auta", "Sztuki" })
                grid.Columns[col].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            foreach (var col in new[] { "Waga", "Kg", "Cena", "Wartosc" })
                grid.Columns[col].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            grid.Columns["Data"].DefaultCellStyle.Format = "yyyy-MM-dd";
            grid.Columns["Sztuki"].DefaultCellStyle.Format = "N0";
            grid.Columns["Waga"].DefaultCellStyle.Format = "N3";
            grid.Columns["Kg"].DefaultCellStyle.Format = "N0";
            grid.Columns["Cena"].DefaultCellStyle.Format = "N2";
            grid.Columns["Wartosc"].DefaultCellStyle.Format = "N2";

            var gridHost = new Panel { Dock = DockStyle.Fill, BackColor = backgroundColor, Padding = new Padding(16, 4, 8, 16) };
            gridHost.Controls.Add(grid);
            root.Controls.Add(gridHost, 0, 1);

            // ── Info panel (col 1, RowSpan = 2)
            var infoCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(16),
                Margin = new Padding(0, 8, 16, 16)
            };
            infoCard.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, infoCard.Width - 1, infoCard.Height - 1);
                using var path = GetRoundedRectangle(rect, 10);
                using (var bg = new SolidBrush(Color.White)) g.FillPath(bg, path);
                using (var border = new Pen(subtleBorderColor, 1)) g.DrawPath(border, path);
            };

            // ── Tytuł (TypCeny + Kategoria)
            var lblTitleDlg = new Label
            {
                Text = $"{katEmoji}  {typCeny}",
                Font = new Font("Segoe UI Emoji", 13F, FontStyle.Bold),
                ForeColor = katColor,
                AutoSize = false, Width = 280, Height = 28,
                Location = new Point(0, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var lblKategoria = new Label
            {
                Text = kat == "Kontrakt" ? "🤝 Kontrakt (umowa)" : "🏪 Wolny rynek",
                Font = new Font("Segoe UI Emoji", 9.5F, FontStyle.Bold),
                ForeColor = katColor,
                AutoSize = true, Location = new Point(0, 32)
            };

            // ── Statystyki łącznie (cały zbiór)
            var lblHeader1 = new Label
            {
                Text = "📊  Statystyki łącznie",
                Font = new Font("Segoe UI Emoji", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = true, Location = new Point(0, 68)
            };
            string statsAllText =
                $"Dostawy: {deliveries.Count}\n" +
                $"Dostawców: {dostCountAll}\n" +
                $"Wolumen: {totalKgAll:N0} kg\n" +
                $"Sztuki: {totalSztAll:N0}\n" +
                $"Wartość: {totalRevAll:N0} zł\n" +
                $"Średnia ważona: {avgCenaAll:N2} zł/kg\n" +
                $"Min cena: {minCenaAll:N2} zł/kg\n" +
                $"Max cena: {maxCenaAll:N2} zł/kg\n" +
                $"Zakres dat:\n  {minDataAll:dd.MM.yyyy} – {maxDataAll:dd.MM.yyyy}";
            var lblStatsAll = new Label
            {
                Text = statsAllText,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(71, 85, 105),
                AutoSize = true, Location = new Point(0, 90)
            };

            // ── Statystyki filtra (aktualizowane on-the-fly)
            var lblHeader2 = new Label
            {
                Text = "🔎  Filtr",
                Font = new Font("Segoe UI Emoji", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = true, Location = new Point(0, 290)
            };
            var lblStatsFiltered = new Label
            {
                Text = "(brak filtra)",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(71, 85, 105),
                AutoSize = true, Location = new Point(0, 312)
            };

            // ── Eksport XLSX
            var btnExport = new Button
            {
                Text = "💾  Eksport XLSX (przefiltrowane)",
                Font = new Font("Segoe UI Emoji", 9F, FontStyle.Bold),
                Width = 280, Height = 36,
                Location = new Point(0, infoCard.ClientSize.Height - 56),
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = primaryColor, ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnExport.FlatAppearance.BorderSize = 0;

            infoCard.Controls.AddRange(new Control[] {
                lblTitleDlg, lblKategoria,
                lblHeader1, lblStatsAll,
                lblHeader2, lblStatsFiltered,
                btnExport
            });
            infoCard.Resize += (s, e) =>
            {
                btnExport.Location = new Point(0, infoCard.ClientSize.Height - 56);
            };

            var infoHost = new Panel { Dock = DockStyle.Fill, BackColor = backgroundColor };
            infoHost.Controls.Add(infoCard);
            root.Controls.Add(infoHost, 1, 0);
            root.SetRowSpan(infoHost, 2);

            dlg.Controls.Add(root);

            // ── Filtering logic + populate
            List<DataRow> visible = new List<DataRow>(deliveries);

            void ApplyFilter()
            {
                string q = (txtSearch.Text ?? "").Trim().ToLowerInvariant();
                visible = string.IsNullOrEmpty(q)
                    ? new List<DataRow>(deliveries)
                    : deliveries.Where(r =>
                        (r["Dostawca"]?.ToString() ?? "").ToLowerInvariant().Contains(q) ||
                        (r["DostawcaID"]?.ToString() ?? "").Contains(q) ||
                        (r["TypUmowy"]?.ToString() ?? "").ToLowerInvariant().Contains(q) ||
                        (r["Uwagi"]?.ToString() ?? "").ToLowerInvariant().Contains(q) ||
                        (r["Lp"]?.ToString() ?? "").Contains(q)).ToList();

                grid.SuspendLayout();
                grid.Rows.Clear();

                decimal fSumKg = 0, fSumSzt = 0, fSumRev = 0;
                foreach (var r in visible)
                {
                    decimal cena = r["Cena"] != DBNull.Value ? Convert.ToDecimal(r["Cena"]) : 0m;
                    decimal sz = r["Sztuki"] != DBNull.Value ? Convert.ToDecimal(r["Sztuki"]) : 0m;
                    decimal kg = r["WolumenKg"] != DBNull.Value ? Convert.ToDecimal(r["WolumenKg"]) : 0m;
                    decimal wartosc = cena * sz;
                    fSumKg += kg; fSumSzt += sz; fSumRev += wartosc;
                    grid.Rows.Add(
                        r["Lp"] != DBNull.Value ? r["Lp"] : "",
                        r["Data"] != DBNull.Value ? Convert.ToDateTime(r["Data"]) : (object)"",
                        r["Dostawca"] != DBNull.Value ? r["Dostawca"] : "",
                        r["DostawcaID"] != DBNull.Value ? r["DostawcaID"] : "",
                        r["Auta"] != DBNull.Value ? r["Auta"] : "",
                        sz,
                        r["WagaSrednia"] != DBNull.Value ? Convert.ToDecimal(r["WagaSrednia"]) : 0m,
                        kg, cena, wartosc,
                        r["TypUmowy"] != DBNull.Value ? r["TypUmowy"] : "",
                        r["Uwagi"] != DBNull.Value ? r["Uwagi"] : ""
                    );
                }

                if (visible.Count > 0)
                {
                    decimal fAvg = fSumSzt > 0 ? fSumRev / fSumSzt : 0m;
                    int fIdx = grid.Rows.Add("", "SUMA", $"({visible.Count})", "", "",
                        fSumSzt, "", fSumKg, fAvg, fSumRev, "", "śr. ważona");
                    var sumRow = grid.Rows[fIdx];
                    sumRow.DefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
                    sumRow.DefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
                    sumRow.DefaultCellStyle.ForeColor = Color.FromArgb(15, 23, 42);
                }

                grid.ResumeLayout();

                lblFilterStatus.Text = $"({visible.Count} z {deliveries.Count})";
                if (string.IsNullOrEmpty(q))
                {
                    lblStatsFiltered.Text = "(brak filtra — pokazane wszystkie)";
                }
                else
                {
                    decimal fAvg = fSumSzt > 0 ? fSumRev / fSumSzt : 0m;
                    int fDost = visible.Where(r => r["DostawcaID"] != DBNull.Value)
                        .Select(r => Convert.ToInt32(r["DostawcaID"])).Distinct().Count();
                    lblStatsFiltered.Text =
                        $"Pasujących: {visible.Count}\n" +
                        $"Dostawców: {fDost}\n" +
                        $"Wolumen: {fSumKg:N0} kg\n" +
                        $"Sztuki: {fSumSzt:N0}\n" +
                        $"Wartość: {fSumRev:N0} zł\n" +
                        $"Średnia ważona: {fAvg:N2} zł/kg";
                }
            }

            txtSearch.TextChanged += (s, e) => ApplyFilter();
            btnExport.Click += (s, e) => ExportKontraktyDeliveriesXlsx(typCeny, kat, visible);

            ApplyFilter(); // initial load

            dlg.ShowDialog(this);
        }

        #endregion

        #region Eksport XLSX (ClosedXML)

        // Eksport przefiltrowanych dostaw do XLSX z formatowaniem (header pogrubiony, kolory, auto-fit kolumn, wiersz SUMA).
        private void ExportKontraktyDeliveriesXlsx(string typCeny, string kategoria, List<DataRow> deliveries)
        {
            try
            {
                using var sfd = new SaveFileDialog
                {
                    Filter = "Excel (XLSX)|*.xlsx",
                    FileName = $"Dostawy_{SanitizeFileName(typCeny)}_{DateTime.Today:yyyyMMdd}.xlsx"
                };
                if (sfd.ShowDialog(this) != DialogResult.OK) return;

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Dostawy");

                // Header z metadanymi (4 wiersze)
                ws.Cell(1, 1).Value = "TypCeny:";
                ws.Cell(1, 2).Value = typCeny;
                ws.Cell(1, 2).Style.Font.Bold = true;
                ws.Cell(2, 1).Value = "Kategoria:";
                ws.Cell(2, 2).Value = kategoria;
                ws.Cell(2, 2).Style.Font.Bold = true;
                ws.Cell(2, 2).Style.Font.FontColor = kategoria == "Kontrakt"
                    ? XLColor.FromArgb(22, 101, 52)
                    : XLColor.FromArgb(30, 64, 175);
                ws.Cell(3, 1).Value = "Wyeksportowano:";
                ws.Cell(3, 2).Value = DateTime.Now;
                ws.Cell(3, 2).Style.NumberFormat.Format = "yyyy-MM-dd HH:mm";
                ws.Cell(4, 1).Value = "Liczba dostaw:";
                ws.Cell(4, 2).Value = deliveries.Count;
                ws.Range(1, 1, 4, 1).Style.Font.Bold = true;
                ws.Range(1, 1, 4, 1).Style.Font.FontColor = XLColor.FromArgb(100, 116, 139);

                // Header tabeli (wiersz 6)
                int hdrRow = 6;
                string[] headers = { "Lp", "Data", "Dostawca", "ID", "Auta", "Sztuki",
                                     "Waga śr. (kg/szt)", "Wolumen (kg)", "Cena (zł/kg)",
                                     "Wartość (zł)", "Typ umowy", "Uwagi" };
                for (int i = 0; i < headers.Length; i++)
                    ws.Cell(hdrRow, i + 1).Value = headers[i];

                var headerRange = ws.Range(hdrRow, 1, hdrRow, headers.Length);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(241, 245, 249);
                headerRange.Style.Font.FontColor = XLColor.FromArgb(15, 23, 42);
                headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                headerRange.Style.Border.BottomBorderColor = XLColor.FromArgb(200, 210, 224);
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Dane (od wiersza 7)
                decimal totalKg = 0, totalSzt = 0, totalRev = 0;
                int row = hdrRow + 1;
                foreach (var r in deliveries)
                {
                    decimal cena = r["Cena"] != DBNull.Value ? Convert.ToDecimal(r["Cena"]) : 0m;
                    decimal sz = r["Sztuki"] != DBNull.Value ? Convert.ToDecimal(r["Sztuki"]) : 0m;
                    decimal kg = r["WolumenKg"] != DBNull.Value ? Convert.ToDecimal(r["WolumenKg"]) : 0m;
                    decimal wartosc = cena * sz;
                    totalKg += kg; totalSzt += sz; totalRev += wartosc;

                    ws.Cell(row, 1).Value = r["Lp"] != DBNull.Value ? Convert.ToInt32(r["Lp"]) : (int?)null;
                    if (r["Data"] != DBNull.Value)
                    {
                        ws.Cell(row, 2).Value = Convert.ToDateTime(r["Data"]);
                        ws.Cell(row, 2).Style.NumberFormat.Format = "yyyy-MM-dd";
                    }
                    ws.Cell(row, 3).Value = r["Dostawca"]?.ToString() ?? "";
                    ws.Cell(row, 4).Value = r["DostawcaID"] != DBNull.Value ? Convert.ToInt32(r["DostawcaID"]) : (int?)null;
                    ws.Cell(row, 5).Value = r["Auta"] != DBNull.Value ? Convert.ToInt32(r["Auta"]) : (int?)null;
                    ws.Cell(row, 6).Value = sz;
                    ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
                    ws.Cell(row, 7).Value = r["WagaSrednia"] != DBNull.Value ? Convert.ToDecimal(r["WagaSrednia"]) : 0m;
                    ws.Cell(row, 7).Style.NumberFormat.Format = "0.000";
                    ws.Cell(row, 8).Value = kg;
                    ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
                    ws.Cell(row, 9).Value = cena;
                    ws.Cell(row, 9).Style.NumberFormat.Format = "0.00";
                    ws.Cell(row, 10).Value = wartosc;
                    ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 11).Value = r["TypUmowy"]?.ToString() ?? "";
                    ws.Cell(row, 12).Value = r["Uwagi"]?.ToString() ?? "";
                    row++;
                }

                // Wiersz SUMA
                if (deliveries.Count > 0)
                {
                    decimal avgCena = totalSzt > 0 ? totalRev / totalSzt : 0m;
                    ws.Cell(row, 1).Value = "";
                    ws.Cell(row, 2).Value = "SUMA";
                    ws.Cell(row, 3).Value = $"({deliveries.Count} dostaw)";
                    ws.Cell(row, 6).Value = totalSzt;
                    ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
                    ws.Cell(row, 8).Value = totalKg;
                    ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
                    ws.Cell(row, 9).Value = avgCena;
                    ws.Cell(row, 9).Style.NumberFormat.Format = "0.00";
                    ws.Cell(row, 10).Value = totalRev;
                    ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 12).Value = "śr. ważona";

                    var sumRange = ws.Range(row, 1, row, headers.Length);
                    sumRange.Style.Font.Bold = true;
                    sumRange.Style.Fill.BackgroundColor = XLColor.FromArgb(241, 245, 249);
                    sumRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                    sumRange.Style.Border.TopBorderColor = XLColor.FromArgb(200, 210, 224);
                }

                // Freeze header + auto-fit + filter
                ws.SheetView.FreezeRows(hdrRow);
                ws.Range(hdrRow, 1, hdrRow, headers.Length).SetAutoFilter();
                ws.Columns().AdjustToContents();
                // Ograniczenie maksymalnej szerokości żeby Uwagi/Dostawca nie rozpychały arkusza
                ws.Column(3).Width = Math.Min(ws.Column(3).Width, 40);
                ws.Column(12).Width = Math.Min(ws.Column(12).Width, 40);

                wb.SaveAs(sfd.FileName);

                var result = MessageBox.Show(
                    $"Wyeksportowano {deliveries.Count} dostaw do:\n{sfd.FileName}\n\nOtworzyć teraz?",
                    "Eksport XLSX",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (result == DialogResult.Yes)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Kontrakty.OpenXlsx] {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Kontrakty.ExportXlsx] {ex.GetType().Name}: {ex}");
                MessageBox.Show($"Błąd eksportu: {ex.Message}", "Eksport XLSX",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string SanitizeFileName(string s)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        #endregion

        #region Ładowanie danych SQL

        private async Task LoadKontraktyDataAsync()
        {
            try
            {
                // Pobieramy oryginalny TypCeny + dane fiskalne + dostawcę. Agregację robimy po stronie .NET (Day/Week/Month/Quarter).
                var sql = @"
                    SELECT
                        Lp AS Lp,
                        DataOdbioru AS Data,
                        ISNULL(TypCeny, '(brak)') AS TypCeny,
                        CASE WHEN LOWER(TypCeny) IN ('wolnyrynek','wolnorynkowa') THEN 'Wolny' ELSE 'Kontrakt' END AS Kategoria,
                        ISNULL(Dostawca, '') AS Dostawca,
                        ISNULL(DostawcaID, 0) AS DostawcaID,
                        ISNULL(TypUmowy, '') AS TypUmowy,
                        ISNULL(Auta, 0) AS Auta,
                        CAST(Cena AS DECIMAL(10,4)) AS Cena,
                        CAST(ISNULL(SztukiDek, 0) AS DECIMAL(18,2)) AS Sztuki,
                        CAST(ISNULL(WagaDek, 0) AS DECIMAL(18,4)) AS WagaSrednia,
                        CAST(ISNULL(SztukiDek, 0) * ISNULL(WagaDek, 0) AS DECIMAL(18,2)) AS WolumenKg,
                        ISNULL(UWAGI, '') AS Uwagi
                    FROM [LibraNet].[dbo].[HarmonogramDostaw]
                    WHERE Bufor = 'Potwierdzony'
                        AND DataOdbioru >= @from AND DataOdbioru <= @to
                        AND Cena IS NOT NULL AND Cena > 0
                        AND SztukiDek IS NOT NULL AND SztukiDek > 0
                        AND TypCeny IS NOT NULL
                    ORDER BY DataOdbioru";

                var dt = new DataTable();
                await Task.Run(() =>
                {
                    using var conn = new SqlConnection(connectionString);
                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@from", mainDateFrom.Value.Date);
                    cmd.Parameters.AddWithValue("@to", mainDateTo.Value.Date);
                    cmd.CommandTimeout = 60;
                    using var adapter = new SqlDataAdapter(cmd);
                    adapter.Fill(dt);
                });
                _kontraktyData = dt;

                // Po załadowaniu odpal UI update na wątku UI
                if (kontraktyChartPanel != null && !kontraktyChartPanel.IsDisposed)
                {
                    kontraktyChartPanel.BeginInvoke(new Action(UpdateKontraktyUI));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Kontrakty.LoadDataAsync] {ex.GetType().Name}: {ex.Message}");
                _kontraktyData = null;
            }
        }

        #endregion

        #region Helper — klucz okresu (Day/Week/Month/Quarter)

        // Helper: klucz okresu (start daty okresu) wg trybu agregacji.
        // SortKey = początek okresu (poniedziałek ISO / 1. dzień miesiąca/kwartału).
        // Label = etykieta osi X (dd.MM / T##/yy / MMM yy / Q# yy).
        private (DateTime SortKey, string Label) GetPeriodKey(DateTime d, AggregationMode mode)
        {
            switch (mode)
            {
                case AggregationMode.Day:
                    return (d.Date, d.ToString("dd.MM"));
                case AggregationMode.Week:
                    {
                        // poniedziałek tygodnia ISO
                        int delta = ((int)d.DayOfWeek - 1 + 7) % 7;
                        DateTime mon = d.Date.AddDays(-delta);
                        int wk = System.Globalization.ISOWeek.GetWeekOfYear(d);
                        return (mon, $"T{wk:00}/{d:yy}");
                    }
                case AggregationMode.Month:
                    return (new DateTime(d.Year, d.Month, 1), d.ToString("MMM yy", new System.Globalization.CultureInfo("pl-PL")));
                case AggregationMode.Quarter:
                    {
                        int q = (d.Month - 1) / 3 + 1;
                        DateTime start = new DateTime(d.Year, (q - 1) * 3 + 1, 1);
                        return (start, $"Q{q} {d:yy}");
                    }
                default:
                    return (d.Date, d.ToString("dd.MM"));
            }
        }

        #endregion

        #region KPI cards (4, stacked vertically)

        private void BuildKontraktyKpiCards()
        {
            if (_kontraktyKpiBar == null) return;
            _kontraktyKpiBar.Controls.Clear();

            if (_kontraktyData == null || _kontraktyData.Rows.Count == 0)
            {
                if (_kontraktyStatusLabel != null) _kontraktyStatusLabel.Text = "(brak danych – sprawdź zakres dat lub odśwież)";
                return;
            }

            decimal contractKg = 0, freeKg = 0;
            decimal contractRev = 0, freeRev = 0;
            decimal contractSzt = 0, freeSzt = 0;
            int contractDel = 0, freeDel = 0;
            foreach (DataRow r in _kontraktyData.Rows)
            {
                decimal kg = r["WolumenKg"] != DBNull.Value ? Convert.ToDecimal(r["WolumenKg"]) : 0m;
                decimal szt = r["Sztuki"] != DBNull.Value ? Convert.ToDecimal(r["Sztuki"]) : 0m;
                decimal cena = r["Cena"] != DBNull.Value ? Convert.ToDecimal(r["Cena"]) : 0m;
                string kat = r["Kategoria"].ToString();
                if (kat == "Kontrakt")
                {
                    contractKg += kg; contractSzt += szt; contractRev += cena * szt; contractDel++;
                }
                else
                {
                    freeKg += kg; freeSzt += szt; freeRev += cena * szt; freeDel++;
                }
            }
            decimal totalKg = contractKg + freeKg;
            decimal contractAvg = contractSzt > 0 ? contractRev / contractSzt : 0;
            decimal freeAvg = freeSzt > 0 ? freeRev / freeSzt : 0;
            decimal pctContract = totalKg > 0 ? (contractKg / totalKg) * 100m : 0;
            decimal pctFree = 100m - pctContract;
            decimal spread = freeAvg - contractAvg; // dodatni = wolny droższy (typowo)

            _kontraktyKpiBar.Controls.Add(BuildKontraktyKpiCard("🤝", "Kontrakt", $"{contractKg:N0} kg", $"{pctContract:N1}% wolumenu  •  {contractDel} dostaw", successColor));
            _kontraktyKpiBar.Controls.Add(BuildKontraktyKpiCard("🏪", "Wolny rynek", $"{freeKg:N0} kg", $"{pctFree:N1}% wolumenu  •  {freeDel} dostaw", primaryColor));
            _kontraktyKpiBar.Controls.Add(BuildKontraktyKpiCard("💰", "Średnia cena", $"K {contractAvg:N2} / W {freeAvg:N2} zł/kg", $"różnica: {spread:+0.00;-0.00;0.00} zł/kg", accentColor));
            string ratioSubtext = $"łącznie {totalKg:N0} kg • {(contractDel + freeDel)} dostaw";
            _kontraktyKpiBar.Controls.Add(BuildKontraktyKpiCard("📊", "Stosunek K / W", $"{pctContract:N0}% / {pctFree:N0}%", ratioSubtext, Color.FromArgb(124, 58, 237)));

            if (_kontraktyStatusLabel != null)
            {
                _kontraktyStatusLabel.Text = $"Łącznie {totalKg:N0} kg • {contractDel + freeDel} dostaw • {_kontraktyData.Rows.Count} rekordów";
            }
        }

        private Panel BuildKontraktyKpiCard(string icon, string title, string bigValue, string subtext, Color accent)
        {
            // Karty układane pionowo w kolumnie ~360 px → karta szerokości 344 (margines wewnątrz host'a)
            var card = new Panel
            {
                Width = 344, Height = 68,
                Margin = new Padding(0, 0, 0, 6),
                BackColor = Color.White
            };
            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                using var path = GetRoundedRectangle(rect, 10);
                using (var bg = new SolidBrush(Color.White)) g.FillPath(bg, path);
                using (var border = new Pen(subtleBorderColor, 1)) g.DrawPath(border, path);
                using (var accentBar = new SolidBrush(accent)) g.FillRectangle(accentBar, 0, 0, 4, card.Height);

                using var iconFont = new Font("Segoe UI Emoji", 16F);
                using var titleFont = new Font("Segoe UI", 9F);
                using var bigFont = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
                using var subFont = new Font("Segoe UI", 8F);
                using var titleBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
                using var bigBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
                using var subBrush = new SolidBrush(Color.FromArgb(71, 85, 105));

                g.DrawString(icon, iconFont, new SolidBrush(accent), 12, 8);
                g.DrawString(title, titleFont, titleBrush, 48, 8);
                // bigValue — truncate jeśli nie mieści się
                string disp = bigValue;
                var sz = g.MeasureString(disp, bigFont);
                int maxW = card.Width - 56;
                while (sz.Width > maxW && disp.Length > 4)
                {
                    disp = disp.Substring(0, disp.Length - 2) + "…";
                    sz = g.MeasureString(disp, bigFont);
                }
                g.DrawString(disp, bigFont, bigBrush, 48, 24);
                g.DrawString(subtext, subFont, subBrush, 48, 46);
            };
            return card;
        }

        #endregion

        #region Detail grid per oryginalny TypCeny

        private void BuildKontraktyDetailGrid()
        {
            if (_kontraktyDetailGrid == null) return;
            _kontraktyDetailGrid.Columns.Clear();
            _kontraktyDetailGrid.Rows.Clear();

            if (_kontraktyData == null || _kontraktyData.Rows.Count == 0) return;

            // Agreguj per oryginalny TypCeny
            var byType = _kontraktyData.AsEnumerable()
                .GroupBy(r => new { TypCeny = r["TypCeny"].ToString(), Kategoria = r["Kategoria"].ToString() })
                .Select(g => new
                {
                    g.Key.TypCeny,
                    g.Key.Kategoria,
                    Kg = g.Sum(r => r["WolumenKg"] != DBNull.Value ? Convert.ToDecimal(r["WolumenKg"]) : 0m),
                    Sztuki = g.Sum(r => r["Sztuki"] != DBNull.Value ? Convert.ToDecimal(r["Sztuki"]) : 0m),
                    Revenue = g.Sum(r =>
                    {
                        decimal c = r["Cena"] != DBNull.Value ? Convert.ToDecimal(r["Cena"]) : 0m;
                        decimal s = r["Sztuki"] != DBNull.Value ? Convert.ToDecimal(r["Sztuki"]) : 0m;
                        return c * s;
                    }),
                    MinCena = g.Where(r => r["Cena"] != DBNull.Value).Min(r => (decimal?)Convert.ToDecimal(r["Cena"])) ?? 0m,
                    MaxCena = g.Where(r => r["Cena"] != DBNull.Value).Max(r => (decimal?)Convert.ToDecimal(r["Cena"])) ?? 0m,
                    Dostaw = g.Count()
                })
                .OrderByDescending(x => x.Kg)
                .ToList();

            decimal totalKg = byType.Sum(x => x.Kg);

            // Wąska tabela (kolumna ~360 px) — kolumny zwięzłe (Min/Max cena dostępne w tooltipie wiersza)
            _kontraktyDetailGrid.Columns.Add("TypCeny", "TypCeny");
            _kontraktyDetailGrid.Columns.Add("Kat", "Kat.");
            _kontraktyDetailGrid.Columns.Add("Kg", "Kg");
            _kontraktyDetailGrid.Columns.Add("Pct", "%");
            _kontraktyDetailGrid.Columns.Add("AvgCena", "Śr. cena");
            _kontraktyDetailGrid.Columns.Add("Dostaw", "Dost.");

            // Proporcje szerokości kolumn (AutoSizeMode = Fill respektuje FillWeight)
            _kontraktyDetailGrid.Columns["TypCeny"].FillWeight = 28;
            _kontraktyDetailGrid.Columns["Kat"].FillWeight = 16;
            _kontraktyDetailGrid.Columns["Kg"].FillWeight = 18;
            _kontraktyDetailGrid.Columns["Pct"].FillWeight = 10;
            _kontraktyDetailGrid.Columns["AvgCena"].FillWeight = 16;
            _kontraktyDetailGrid.Columns["Dostaw"].FillWeight = 12;

            _kontraktyDetailGrid.Columns["Kg"].DefaultCellStyle.Format = "N0";
            _kontraktyDetailGrid.Columns["Kg"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _kontraktyDetailGrid.Columns["Pct"].DefaultCellStyle.Format = "N1";
            _kontraktyDetailGrid.Columns["Pct"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _kontraktyDetailGrid.Columns["AvgCena"].DefaultCellStyle.Format = "N2";
            _kontraktyDetailGrid.Columns["AvgCena"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _kontraktyDetailGrid.Columns["Dostaw"].DefaultCellStyle.Format = "N0";
            _kontraktyDetailGrid.Columns["Dostaw"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _kontraktyDetailGrid.Columns["Kat"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            foreach (var row in byType)
            {
                decimal avgCena = row.Sztuki > 0 ? row.Revenue / row.Sztuki : 0m;
                decimal pct = totalKg > 0 ? (row.Kg / totalKg) * 100m : 0m;
                string katShort = row.Kategoria == "Kontrakt" ? "🤝 K" : "🏪 W";
                int idx = _kontraktyDetailGrid.Rows.Add(
                    row.TypCeny, katShort, row.Kg, pct, avgCena, row.Dostaw);

                // Tooltip z pełnym min/max
                _kontraktyDetailGrid.Rows[idx].Cells["TypCeny"].ToolTipText =
                    $"Kategoria: {row.Kategoria}\nMin cena: {row.MinCena:N2} zł/kg\nMax cena: {row.MaxCena:N2} zł/kg";

                // Kolor komórki Kategoria wg typu
                if (row.Kategoria == "Kontrakt")
                {
                    _kontraktyDetailGrid.Rows[idx].Cells["Kat"].Style.BackColor = Color.FromArgb(220, 252, 231);
                    _kontraktyDetailGrid.Rows[idx].Cells["Kat"].Style.ForeColor = Color.FromArgb(22, 101, 52);
                }
                else
                {
                    _kontraktyDetailGrid.Rows[idx].Cells["Kat"].Style.BackColor = Color.FromArgb(219, 234, 254);
                    _kontraktyDetailGrid.Rows[idx].Cells["Kat"].Style.ForeColor = Color.FromArgb(30, 64, 175);
                }
            }

            // Mini-fit: wysokość = nagłówek + (liczba wierszy × wysokość wiersza) + cienka rezerwa
            int wanted = _kontraktyDetailGrid.ColumnHeadersHeight
                       + _kontraktyDetailGrid.RowTemplate.Height * Math.Max(1, byType.Count)
                       + 2;
            _kontraktyDetailGrid.Height = Math.Min(wanted, 26 + 22 * 12);
            _kontraktyDetailGrid.ScrollBars = wanted > _kontraktyDetailGrid.Height ? ScrollBars.Vertical : ScrollBars.None;
        }

        #endregion

        #region KontraktyChart_Paint (dual chart: cena line + 100% stacked bar)

        private void KontraktyChart_Paint(object sender, PaintEventArgs e)
        {
            try
            {
                if (!(sender is Panel panel)) return;
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var cardRect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
                using var cardPath = GetRoundedRectangle(cardRect, 10);
                using (var bg = new SolidBrush(Color.White)) g.FillPath(bg, cardPath);
                using (var border = new Pen(subtleBorderColor, 1)) g.DrawPath(border, cardPath);

                if (_kontraktyData == null || _kontraktyData.Rows.Count == 0)
                {
                    using var emptyFont = new Font("Segoe UI Emoji", 13F, FontStyle.Italic);
                    using var grayBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
                    g.DrawString("📋  Brak danych kontrakt/wolny rynek — sprawdź zakres dat",
                        emptyFont, grayBrush, panel.ClientRectangle,
                        new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    return;
                }

                int padding = 20;
                int chartLeft = padding + 60;
                int chartRight = panel.Width - padding - 16;
                int chartW = chartRight - chartLeft;
                int totalH = panel.Height - padding * 2 - 56; // miejsce na 2 nagłówki + 1 etykiety osi X
                int chartH1 = (int)(totalH * 0.52);
                int chartH2 = (int)(totalH * 0.42);
                int chart1Top = padding + 32;
                int chart1Bottom = chart1Top + chartH1;
                int chart2Top = chart1Bottom + 38;
                int chart2Bottom = chart2Top + chartH2;

                // ── 1) Agreguj per okres (kontrakt vs wolny): średnia ważona cena + suma kg
                var perPeriod = new Dictionary<DateTime, (string Label, decimal CKg, decimal FKg, decimal CRev, decimal FRev, decimal CSzt, decimal FSzt)>();
                foreach (DataRow r in _kontraktyData.Rows)
                {
                    if (r["Data"] == DBNull.Value) continue;
                    var d = Convert.ToDateTime(r["Data"]).Date;
                    var (key, lbl) = GetPeriodKey(d, _kontraktyAggMode);
                    string kat = r["Kategoria"].ToString();
                    decimal cena = r["Cena"] != DBNull.Value ? Convert.ToDecimal(r["Cena"]) : 0m;
                    decimal kg = r["WolumenKg"] != DBNull.Value ? Convert.ToDecimal(r["WolumenKg"]) : 0m;
                    decimal szt = r["Sztuki"] != DBNull.Value ? Convert.ToDecimal(r["Sztuki"]) : 0m;
                    if (!perPeriod.TryGetValue(key, out var t)) t = (lbl, 0m, 0m, 0m, 0m, 0m, 0m);
                    if (kat == "Kontrakt") t = (lbl, t.CKg + kg, t.FKg, t.CRev + cena * szt, t.FRev, t.CSzt + szt, t.FSzt);
                    else t = (lbl, t.CKg, t.FKg + kg, t.CRev, t.FRev + cena * szt, t.CSzt, t.FSzt + szt);
                    perPeriod[key] = t;
                }
                var ordered = perPeriod.OrderBy(kv => kv.Key).ToList();
                if (ordered.Count == 0) return;

                int n = ordered.Count;
                int barSlotW = chartW / Math.Max(1, n);
                int barW = Math.Max(8, (int)(barSlotW * 0.7));

                int CenterXForIdx(int i) => chartLeft + barSlotW * i + barSlotW / 2;

                // ── 2) Skala Y dla cen
                decimal yMin = decimal.MaxValue, yMax = decimal.MinValue;
                foreach (var (_, v) in ordered)
                {
                    if (v.CSzt > 0) { var p = v.CRev / v.CSzt; if (p < yMin) yMin = p; if (p > yMax) yMax = p; }
                    if (v.FSzt > 0) { var p = v.FRev / v.FSzt; if (p < yMin) yMin = p; if (p > yMax) yMax = p; }
                }
                if (yMin == decimal.MaxValue) { yMin = 0; yMax = 1; }
                decimal margin = (yMax - yMin) * 0.15m;
                if (margin == 0) margin = 0.5m;
                yMin -= margin; yMax += margin;
                if (yMin < 0) yMin = 0;
                decimal yRange = yMax - yMin;
                if (yRange == 0) yRange = 1;
                int Y1(decimal v) => chart1Top + (int)((double)((yMax - v) / yRange) * chartH1);

                using var titleFont = new Font("Segoe UI Emoji", 12F, FontStyle.Bold);
                using var titleBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
                using var subFont = new Font("Segoe UI", 9F, FontStyle.Italic);
                using var subBrush = new SolidBrush(Color.FromArgb(100, 116, 139));

                string okresLbl = _kontraktyAggMode switch
                {
                    AggregationMode.Day => "dziennie",
                    AggregationMode.Week => "tygodniowo",
                    AggregationMode.Month => "miesięcznie",
                    AggregationMode.Quarter => "kwartalnie",
                    _ => "tygodniowo"
                };

                g.DrawString($"💰  Średnia ważona cena żywca ({okresLbl})", titleFont, titleBrush, chartLeft, padding - 6);

                // Markery + linia: kontrakt zielony krążek; wolny niebieski krążek; linie łączące tylko gdy są ≥2 pkt
                using var gridPen = new Pen(Color.FromArgb(241, 245, 249), 1);
                using var axisFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
                using var axisBrush = new SolidBrush(Color.FromArgb(71, 85, 105));
                using var axisLine = new Pen(Color.FromArgb(200, 210, 224), 1.4f);
                for (int i = 0; i <= 4; i++)
                {
                    decimal v = yMin + yRange * i / 4m;
                    int yL = Y1(v);
                    g.DrawLine(gridPen, chartLeft, yL, chartRight, yL);
                    g.DrawString($"{v:N2}", axisFont, axisBrush, padding - 6, yL - 8);
                }
                g.DrawLine(axisLine, chartLeft, chart1Top, chartLeft, chart1Bottom);
                g.DrawLine(axisLine, chartLeft, chart1Bottom, chartRight, chart1Bottom);

                var cPts = new List<PointF>();
                var fPts = new List<PointF>();
                for (int i = 0; i < n; i++)
                {
                    var v = ordered[i].Value;
                    int cx = CenterXForIdx(i);
                    if (v.CSzt > 0) cPts.Add(new PointF(cx, Y1(v.CRev / v.CSzt)));
                    if (v.FSzt > 0) fPts.Add(new PointF(cx, Y1(v.FRev / v.FSzt)));
                }

                using (var cPen = new Pen(successColor, 2.5f))
                {
                    if (cPts.Count >= 2) g.DrawLines(cPen, cPts.ToArray());
                }
                using (var fPen = new Pen(primaryColor, 2.5f))
                {
                    if (fPts.Count >= 2) g.DrawLines(fPen, fPts.ToArray());
                }
                using var markerEdge = new Pen(Color.White, 1.5f);
                foreach (var p in cPts)
                {
                    using var b = new SolidBrush(successColor);
                    g.FillEllipse(b, p.X - 5, p.Y - 5, 10, 10);
                    g.DrawEllipse(markerEdge, p.X - 5, p.Y - 5, 10, 10);
                }
                foreach (var p in fPts)
                {
                    using var b = new SolidBrush(primaryColor);
                    g.FillEllipse(b, p.X - 5, p.Y - 5, 10, 10);
                    g.DrawEllipse(markerEdge, p.X - 5, p.Y - 5, 10, 10);
                }

                // Etykiety wartości nad markerami (gdy okres rzadki — Q/M/W)
                if (_kontraktyAggMode != AggregationMode.Day)
                {
                    using var lblFont = new Font("Segoe UI", 8F, FontStyle.Bold);
                    using var labelBg = new SolidBrush(Color.FromArgb(230, 255, 255, 255));
                    using var contractLblBrush = new SolidBrush(successColor);
                    using var freeLblBrush = new SolidBrush(primaryColor);
                    for (int i = 0; i < n; i++)
                    {
                        var v = ordered[i].Value;
                        int cx = CenterXForIdx(i);
                        if (v.CSzt > 0)
                        {
                            string t = (v.CRev / v.CSzt).ToString("0.00");
                            var sz = g.MeasureString(t, lblFont);
                            int y = Y1(v.CRev / v.CSzt) - (int)sz.Height - 7;
                            g.FillRectangle(labelBg, cx - sz.Width / 2 - 2, y, sz.Width + 4, sz.Height);
                            g.DrawString(t, lblFont, contractLblBrush, cx - sz.Width / 2, y);
                        }
                        if (v.FSzt > 0)
                        {
                            string t = (v.FRev / v.FSzt).ToString("0.00");
                            var sz = g.MeasureString(t, lblFont);
                            int y = Y1(v.FRev / v.FSzt) + 7;
                            g.FillRectangle(labelBg, cx - sz.Width / 2 - 2, y, sz.Width + 4, sz.Height);
                            g.DrawString(t, lblFont, freeLblBrush, cx - sz.Width / 2, y);
                        }
                    }
                }

                // Legenda nad chart 1
                using var legendFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
                int legendX = chartRight - 280;
                int legendY = padding - 4;
                using (var b = new SolidBrush(successColor)) g.FillEllipse(b, legendX, legendY + 4, 12, 12);
                g.DrawString("Kontrakt (umowa)", legendFont, new SolidBrush(Color.FromArgb(15, 23, 42)), legendX + 18, legendY + 1);
                legendX += 140;
                using (var b = new SolidBrush(primaryColor)) g.FillEllipse(b, legendX, legendY + 4, 12, 12);
                g.DrawString("Wolny rynek", legendFont, new SolidBrush(Color.FromArgb(15, 23, 42)), legendX + 18, legendY + 1);

                // ── 3) DOLNY: Stacked 100% bar dla udziału wolumenowego per okres
                g.DrawString($"⚖  Udział wolumenowy 100% ({okresLbl})",
                    titleFont, titleBrush, chartLeft, chart1Bottom + 12);

                int Y2(double pct) => chart2Top + (int)((100 - pct) / 100.0 * chartH2);

                // Grid 0/25/50/75/100
                for (int pct = 0; pct <= 100; pct += 25)
                {
                    int yL = Y2(pct);
                    g.DrawLine(gridPen, chartLeft, yL, chartRight, yL);
                    g.DrawString($"{pct}%", axisFont, axisBrush, padding - 6, yL - 8);
                }

                g.DrawLine(axisLine, chartLeft, chart2Top, chartLeft, chart2Bottom);
                g.DrawLine(axisLine, chartLeft, chart2Bottom, chartRight, chart2Bottom);

                // Słupki: 100% stacked (kontrakt + wolny = 100)
                using var contractBrush = new SolidBrush(successColor);
                using var freeBrush = new SolidBrush(primaryColor);
                using var pctLblFont = new Font("Segoe UI", 8F, FontStyle.Bold);
                using var whiteLblBrush = new SolidBrush(Color.White);
                for (int i = 0; i < n; i++)
                {
                    var v = ordered[i].Value;
                    decimal total = v.CKg + v.FKg;
                    if (total <= 0) continue;
                    double pctC = (double)(v.CKg / total) * 100.0;
                    int cx = CenterXForIdx(i);
                    int xLeft = cx - barW / 2;
                    int yTop = chart2Top;
                    int yMid = Y2(pctC);
                    int yBot = chart2Bottom;
                    // wolny (góra) — od top do yMid
                    g.FillRectangle(freeBrush, xLeft, yTop, barW, yMid - yTop);
                    // kontrakt (dół) — od yMid do bot
                    g.FillRectangle(contractBrush, xLeft, yMid, barW, yBot - yMid);

                    // Etykiety % wewnątrz słupka (gdy słupek odpowiednio wysoki)
                    int contractH = yBot - yMid;
                    int freeH = yMid - yTop;
                    if (barW >= 28)
                    {
                        if (contractH >= 16)
                        {
                            string txt = $"{pctC:N0}%";
                            var sz = g.MeasureString(txt, pctLblFont);
                            g.DrawString(txt, pctLblFont, whiteLblBrush,
                                cx - sz.Width / 2, yMid + (contractH - sz.Height) / 2);
                        }
                        if (freeH >= 16)
                        {
                            string txt = $"{100 - pctC:N0}%";
                            var sz = g.MeasureString(txt, pctLblFont);
                            g.DrawString(txt, pctLblFont, whiteLblBrush,
                                cx - sz.Width / 2, yTop + (freeH - sz.Height) / 2);
                        }
                    }
                }

                // ── 4) Etykiety osi X (per okres, pod dolnym wykresem)
                // Jeśli okresów dużo (>20) — pokaż co N-ty.
                int xStep = n switch
                {
                    > 36 => Math.Max(1, n / 12),
                    > 20 => Math.Max(1, n / 10),
                    _ => 1
                };
                using var xFont = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
                for (int i = 0; i < n; i += xStep)
                {
                    int cx = CenterXForIdx(i);
                    string lbl = ordered[i].Value.Label;
                    var sz = g.MeasureString(lbl, xFont);
                    g.DrawString(lbl, xFont, axisBrush, cx - sz.Width / 2, chart2Bottom + 4);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Kontrakty.ChartPaint] {ex.GetType().Name}: {ex.Message}");
            }
        }

        #endregion
    }
}
