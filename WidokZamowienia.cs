// Plik: WidokZamowienia.cs
// WERSJA 18.0 - POPRAWIONE: panele, kolory, ikonki, dynamiczne dane
// Główne zmiany:
// 1. Kompaktowe dolne panele (130px zamiast 180px)
// 2. Info Transport pobiera faktyczne preferowane godziny z bazy
// 3. Ostatnie zamówienia z paletami i dniem tygodnia
// 4. Działające statystyki
// 5. Zielone wiersze dla towarów z ilością > 0
// 6. Zmieniony kolor zaznaczenia (jasno-zielony)
// 7. Specjalny kolor dla kolumny Ilosc
// 8. Zamienione emoji na proste znaki [T], [P], [#], [kg], etc.

#nullable enable
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class WidokZamowienia : Form
    {
        // ===== Publiczne Właściwości =====
        public string UserID { get; set; } = string.Empty;
        private int? _idZamowieniaDoEdycji;

        // ===== Połączenia z Bazą =====
        private readonly string _connLibra;
        private readonly string _connHandel;

        // ===== Stałe przeliczeniowe =====
        private const decimal POJEMNIKOW_NA_PALECIE = 36m;
        private const decimal POJEMNIKOW_NA_PALECIE_E2 = 40m;
        private const decimal KG_NA_POJEMNIKU = 15m;
        private const decimal KG_NA_POJEMNIKU_SPECJALNY = 10m;
        private const decimal KG_NA_PALECIE = POJEMNIKOW_NA_PALECIE * KG_NA_POJEMNIKU;
        private const decimal KG_NA_PALECIE_E2 = POJEMNIKOW_NA_PALECIE_E2 * KG_NA_POJEMNIKU;

        // ===== Kolory motywu =====
        private readonly Color PRIMARY_COLOR = Color.FromArgb(99, 102, 241);
        private readonly Color PRIMARY_DARK = Color.FromArgb(67, 56, 202);
        private readonly Color SUCCESS_COLOR = Color.FromArgb(34, 197, 94);
        private readonly Color WARNING_COLOR = Color.FromArgb(251, 146, 60);
        private readonly Color DANGER_COLOR = Color.FromArgb(239, 68, 68);
        private readonly Color INFO_COLOR = Color.FromArgb(59, 130, 246);
        private readonly Color PURPLE_COLOR = Color.FromArgb(168, 85, 247);

        // ===== Zmienne Stanu Formularza =====
        private string? _selectedKlientId;
        private bool _blokujObslugeZmian;
        private readonly CultureInfo _pl = new("pl-PL");
        private readonly Dictionary<string, Image> _headerIcons = new();

        // ===== Dane i Cache =====
        private sealed class KontrahentInfo
        {
            public string Id { get; set; } = "";
            public string Nazwa { get; set; } = "";
            public string KodPocztowy { get; set; } = "";
            public string Miejscowosc { get; set; } = "";
            public string NIP { get; set; } = "";
            public string Handlowiec { get; set; } = "";
            public DateTime? OstatnieZamowienie { get; set; }
            public string PreferredDeliveryTime { get; set; } = "";
        }

        private sealed class OrderInfo
        {
            public DateTime DataZamowienia { get; set; }
            public decimal TotalKg { get; set; }
            public int TotalPojemniki { get; set; }
            public decimal TotalPalety { get; set; }
        }

        private readonly DataTable _dt = new();
        private DataView _view = default!;
        private readonly List<KontrahentInfo> _kontrahenci = new();
        private readonly Dictionary<string, DateTime> _ostatnieZamowienia = new();

        // ===== Panele UI =====
        private Panel? panelSummary;
        private Label? lblSumaPalet;
        private Label? lblSumaPojemnikow;
        private Label? lblSumaKg;
        private Panel? panelTransport;
        private ProgressBar? progressSolowka;
        private ProgressBar? progressTir;
        private Label? lblSolowkaInfo;
        private Label? lblTirInfo;

        // Panele dolne
        private Panel? panelStatystyki;
        private Panel? panelOstatnichZamowien;
        private Panel? panelInfoTransport;
        private Label? lblAvgOrder;
        private Label? lblTopProduct;
        private Label? lblTrend;
        private ListBox? listRecentOrders;
        private Label? lblPreferredTime;
        private Label? lblDeliveryNotes;

        // ===== Konstruktory =====
        public WidokZamowienia() : this(App.UserID ?? string.Empty, null) { }
        public WidokZamowienia(int? idZamowienia) : this(App.UserID ?? string.Empty, idZamowienia) { }

        public WidokZamowienia(string userId, int? idZamowienia = null)
        {
            InitializeComponent();
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;

            UserID = userId;
            _idZamowieniaDoEdycji = idZamowienia;

            _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
            _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

            this.Load += WidokZamowienia_Load;
        }

        // ===== GŁÓWNA METODA ŁADUJĄCA =====
        private async void WidokZamowienia_Load(object? sender, EventArgs e)
        {
            ApplyColorfulUIStyles();
            CreateHeaderIcons();
            SzybkiGrid();
            WireShortcuts();
            BuildDataTableSchema();
            InitDefaults();
            CreateSummaryPanel();
            CreateResponsiveTransportPanel();
            CreateBottomPanels();
            SetupOstatniOdbiorcyGrid();
            ConfigureResponsiveLayout();

            dateTimePickerSprzedaz.Format = DateTimePickerFormat.Custom;
            dateTimePickerSprzedaz.CustomFormat = "yyyy-MM-dd (dddd)";

            try
            {
                await LoadInitialDataInBackground();
                WireUpUIEvents();
                await LoadOstatnieZamowienia();

                if (_idZamowieniaDoEdycji.HasValue)
                {
                    await LoadZamowienieAsync(_idZamowieniaDoEdycji.Value);
                    lblTytul.Text = "[E] Edycja zamowienia";
                    btnZapisz.Text = "[S] Zapisz zmiany";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wystąpił błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
                btnZapisz.Enabled = true;
            }
        }

        #region Kolorowy UI

        private void ApplyColorfulUIStyles()
        {
            this.BackColor = Color.FromArgb(245, 247, 250);

            if (lblTytul != null)
            {
                lblTytul.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
                lblTytul.ForeColor = PRIMARY_COLOR;
                lblTytul.Text = "[+] Nowe zamowienie";
            }

            StyleGradientButton(btnZapisz, PRIMARY_COLOR, PRIMARY_DARK, Color.White);
            btnZapisz.Text = "[S] Zapisz";

            if (panelOdbiorca != null)
            {
                panelOdbiorca.BackColor = Color.White;
                panelOdbiorca.Padding = new Padding(20, 15, 20, 10);
                panelOdbiorca.Paint += (s, e) => DrawPanelWithShadow(e, panelOdbiorca);
            }

            if (panelDaneOdbiorcy != null)
            {
                panelDaneOdbiorcy.BackColor = Color.White;
                panelDaneOdbiorcy.BorderStyle = BorderStyle.None;
                panelDaneOdbiorcy.Paint += (s, e) => DrawGradientHeader(e, panelDaneOdbiorcy);
            }

            if (panelDetaleZamowienia != null)
            {
                panelDetaleZamowienia.BackColor = Color.White;
                panelDetaleZamowienia.AutoScroll = true;
            }

            StyleColorfulDateTimePicker(dateTimePickerSprzedaz, INFO_COLOR);
            StyleColorfulDateTimePicker(dateTimePickerGodzinaPrzyjazdu, PURPLE_COLOR);
            StyleColorfulTextBox(txtSzukajOdbiorcy, PRIMARY_COLOR);
            StyleColorfulTextBox(textBoxUwagi, SUCCESS_COLOR);
            StyleColorfulComboBox(cbHandlowiecFilter, WARNING_COLOR);

            if (summaryLabelPalety != null) summaryLabelPalety.Visible = false;
            if (summaryLabelPojemniki != null) summaryLabelPojemniki.Visible = false;

            foreach (Control c in panelDetaleZamowienia.Controls)
            {
                if (c is Label lbl && lbl.Font.Bold)
                {
                    lbl.Font = new Font("Segoe UI Semibold", 9.5f);
                    lbl.ForeColor = Color.FromArgb(75, 85, 99);
                }
            }
        }

        private void DrawPanelWithShadow(PaintEventArgs e, Panel panel)
        {
            using var shadowBrush = new SolidBrush(Color.FromArgb(20, 0, 0, 0));
            e.Graphics.FillRectangle(shadowBrush, new Rectangle(2, 2, panel.Width - 4, panel.Height - 4));

            using var path = GetRoundedRectPath(new Rectangle(0, 0, panel.Width - 3, panel.Height - 3), 12);
            using var brush = new SolidBrush(Color.White);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, path);
        }

        private void DrawGradientHeader(PaintEventArgs e, Panel panel)
        {
            var rect = new Rectangle(0, 0, panel.Width, 60);
            using var brush = new LinearGradientBrush(rect, PRIMARY_COLOR, PURPLE_COLOR, 45f);
            e.Graphics.FillRectangle(brush, rect);
        }

        private void StyleGradientButton(Button? btn, Color color1, Color color2, Color textColor)
        {
            if (btn == null) return;

            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.ForeColor = textColor;
            btn.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            btn.Cursor = Cursors.Hand;
            btn.Height = 42;
            btn.Width = 160;

            btn.Paint += (s, e) =>
            {
                var rect = btn.ClientRectangle;
                using var path = GetRoundedRectPath(rect, 8);
                using var brush = new LinearGradientBrush(rect, color1, color2, 45f);

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);

                btn.Region = new Region(path);

                var textRect = new Rectangle(0, 0, rect.Width, rect.Height);
                TextRenderer.DrawText(e.Graphics, btn.Text, btn.Font, textRect, textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };

            btn.MouseEnter += (s, e) => btn.BackColor = ControlPaint.Light(color1, 0.1f);
            btn.MouseLeave += (s, e) => btn.BackColor = color1;
        }

        private void StyleColorfulDateTimePicker(DateTimePicker? dtp, Color accentColor)
        {
            if (dtp == null) return;
            dtp.Font = new Font("Segoe UI", 10f);
            dtp.CalendarTitleBackColor = accentColor;
            dtp.CalendarTitleForeColor = Color.White;
            dtp.CalendarMonthBackground = Color.FromArgb(250, 250, 252);
        }

        private void StyleColorfulTextBox(TextBox? tb, Color borderColor)
        {
            if (tb == null) return;
            tb.Font = new Font("Segoe UI", 10f);
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.BackColor = Color.FromArgb(250, 250, 252);

            tb.Enter += (s, e) => tb.BackColor = Color.FromArgb(245, 245, 255);
            tb.Leave += (s, e) => tb.BackColor = Color.FromArgb(250, 250, 252);
        }

        private void StyleColorfulComboBox(ComboBox? cb, Color accentColor)
        {
            if (cb == null) return;
            cb.Font = new Font("Segoe UI", 10f);
            cb.BackColor = Color.FromArgb(250, 250, 252);
            cb.FlatStyle = FlatStyle.Flat;
        }

        #endregion

        #region Panele Dolne - KOMPAKTOWE

        private void CreateBottomPanels()
        {
            var bottomContainer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 130,
                BackColor = Color.FromArgb(245, 247, 250),
                Name = "bottomPanelsContainer"
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(10, 5, 10, 10),
                BackColor = Color.Transparent
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));

            panelStatystyki = CreateStatisticsPanel();
            layout.Controls.Add(panelStatystyki, 0, 0);

            panelOstatnichZamowien = CreateRecentOrdersPanel();
            layout.Controls.Add(panelOstatnichZamowien, 1, 0);

            panelInfoTransport = CreateTransportInfoPanel();
            layout.Controls.Add(panelInfoTransport, 2, 0);

            bottomContainer.Controls.Add(layout);

            if (panelDetails != null)
            {
                panelDetails.Controls.Add(bottomContainer);
                bottomContainer.BringToFront();
            }
        }

        private Panel CreateStatisticsPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Margin = new Padding(5),
                Padding = new Padding(12, 8, 12, 8)
            };

            panel.Paint += (s, e) => DrawColorfulCard(e, panel, SUCCESS_COLOR);

            var lblTitle = new Label
            {
                Text = "STATYSTYKI",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = SUCCESS_COLOR,
                Location = new Point(12, 8),
                AutoSize = true
            };

            lblAvgOrder = new Label
            {
                Text = "Srednie: 0 kg",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(75, 85, 99),
                Location = new Point(12, 30),
                Size = new Size(180, 20)
            };

            lblTopProduct = new Label
            {
                Text = "Top produkt: -",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(75, 85, 99),
                Location = new Point(12, 52),
                Size = new Size(180, 20)
            };

            lblTrend = new Label
            {
                Text = "Trend: wybierz klienta",
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(107, 114, 128),
                Location = new Point(12, 74),
                AutoSize = true
            };

            panel.Controls.AddRange(new Control[] { lblTitle, lblAvgOrder, lblTopProduct, lblTrend });
            return panel;
        }

        private Panel CreateRecentOrdersPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Margin = new Padding(5),
                Padding = new Padding(12, 8, 12, 8)
            };

            panel.Paint += (s, e) => DrawColorfulCard(e, panel, INFO_COLOR);

            var lblTitle = new Label
            {
                Text = "OSTATNIE ZAMOWIENIA",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = INFO_COLOR,
                Location = new Point(12, 8),
                AutoSize = true
            };

            listRecentOrders = new ListBox
            {
                Location = new Point(12, 30),
                Size = new Size(panel.Width - 24, 80),
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(250, 252, 255),
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = Color.FromArgb(55, 65, 81),
                SelectionMode = SelectionMode.None
            };

            listRecentOrders.Items.Add("Wybierz kontrahenta");

            panel.Controls.AddRange(new Control[] { lblTitle, listRecentOrders });
            return panel;
        }

        private Panel CreateTransportInfoPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Margin = new Padding(5),
                Padding = new Padding(12, 8, 12, 8)
            };

            panel.Paint += (s, e) => DrawColorfulCard(e, panel, WARNING_COLOR);

            var lblTitle = new Label
            {
                Text = "INFO TRANSPORT",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = WARNING_COLOR,
                Location = new Point(12, 8),
                AutoSize = true
            };

            lblPreferredTime = new Label
            {
                Text = "Preferowane godziny:\nWybierz kontrahenta",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(75, 85, 99),
                Location = new Point(12, 30),
                Size = new Size(200, 40),
                TextAlign = ContentAlignment.TopLeft
            };

            lblDeliveryNotes = new Label
            {
                Text = "Sprawdz dostepnosc kierowcy\nPotwierdz godzine z klientem\nZarezerwuj transport",
                Font = new Font("Segoe UI", 7f),
                ForeColor = Color.FromArgb(107, 114, 128),
                Location = new Point(12, 72),
                Size = new Size(200, 42),
                TextAlign = ContentAlignment.TopLeft
            };

            panel.Controls.AddRange(new Control[] { lblTitle, lblPreferredTime, lblDeliveryNotes });
            return panel;
        }

        private void DrawColorfulCard(PaintEventArgs e, Panel panel, Color accentColor)
        {
            var rect = panel.ClientRectangle;
            rect.Inflate(-1, -1);

            using var path = GetRoundedRectPath(rect, 12);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using var bgBrush = new SolidBrush(Color.White);
            e.Graphics.FillPath(bgBrush, path);

            var topRect = new Rectangle(rect.X, rect.Y, rect.Width, 28);
            using var topPath = GetRoundedRectPath(topRect, 12);
            using var gradBrush = new LinearGradientBrush(topRect,
                accentColor,
                ControlPaint.Light(accentColor, 0.3f),
                90f);
            e.Graphics.FillPath(gradBrush, topPath);

            using var pen = new Pen(Color.FromArgb(229, 231, 235), 1.5f);
            e.Graphics.DrawPath(pen, path);
        }

        #endregion

        #region Aktualizacja Dynamicznych Paneli

        private async void UpdateClientInfoPanels(string clientId)
        {
            if (string.IsNullOrEmpty(clientId)) return;

            var client = _kontrahenci.FirstOrDefault(k => k.Id == clientId);
            if (client == null) return;

            await UpdateTransportInfoPanel(client);
            await UpdateRecentOrdersPanel(clientId);
            await UpdateStatisticsPanel(clientId);
        }

        private async Task UpdateTransportInfoPanel(KontrahentInfo client)
        {
            if (lblPreferredTime == null) return;

            try
            {
                var preferredTimes = await GetPreferredDeliveryTimes(client.Id);

                if (preferredTimes.Count == 0)
                {
                    lblPreferredTime.Text = "Preferowane godziny:\n8:00-16:00 (standard)";
                    lblPreferredTime.ForeColor = Color.FromArgb(107, 114, 128);
                }
                else
                {
                    var topTimes = preferredTimes
                        .GroupBy(t => t.ToString("HH:mm"))
                        .OrderByDescending(g => g.Count())
                        .Take(4)
                        .Select(g => g.Key)
                        .ToList();

                    lblPreferredTime.Text = "Preferowane godziny:\n" + string.Join(" | ", topTimes);
                    lblPreferredTime.ForeColor = WARNING_COLOR;
                }

                if (lblDeliveryNotes != null)
                {
                    lblDeliveryNotes.Text = $"Kontakt: {client.Handlowiec}\n" +
                                           $"{client.Miejscowosc}\n" +
                                           $"Potwierdz dostawa";
                }
            }
            catch
            {
                lblPreferredTime.Text = "Preferowane godziny:\nBrak danych";
            }
        }

        private async Task<List<DateTime>> GetPreferredDeliveryTimes(string clientId)
        {
            var times = new List<DateTime>();

            const string sql = @"
                SELECT TOP 10 DataPrzyjazdu
                FROM [dbo].[ZamowieniaMieso]
                WHERE KlientId = @KlientId
                AND DataPrzyjazdu IS NOT NULL
                ORDER BY DataZamowienia DESC";

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@KlientId", clientId);

                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    times.Add(rd.GetDateTime(0));
                }
            }
            catch
            {
                // W przypadku błędu zwróć pustą listę
            }

            return times;
        }

        private async Task UpdateRecentOrdersPanel(string clientId)
        {
            if (listRecentOrders == null) return;

            listRecentOrders.Items.Clear();

            try
            {
                var orders = await GetLast4OrdersForClient(clientId);

                if (orders.Count == 0)
                {
                    listRecentOrders.Items.Add("Brak zamowien");
                    return;
                }

                var polishDays = new Dictionary<DayOfWeek, string>
                {
                    { DayOfWeek.Monday, "Pon" },
                    { DayOfWeek.Tuesday, "Wt" },
                    { DayOfWeek.Wednesday, "Sr" },
                    { DayOfWeek.Thursday, "Czw" },
                    { DayOfWeek.Friday, "Pt" },
                    { DayOfWeek.Saturday, "Sob" },
                    { DayOfWeek.Sunday, "Nie" }
                };

                foreach (var order in orders)
                {
                    string dayName = polishDays[order.DataZamowienia.DayOfWeek];
                    string dateStr = order.DataZamowienia.ToString("yyyy-MM-dd");
                    string orderStr = $"{dateStr} ({dayName}) | {order.TotalPalety:N1}pal | {order.TotalKg:N0}kg";
                    listRecentOrders.Items.Add(orderStr);
                }
            }
            catch (Exception ex)
            {
                listRecentOrders.Items.Add($"Blad: {ex.Message}");
            }
        }

        private async Task<List<OrderInfo>> GetLast4OrdersForClient(string clientId)
        {
            var orders = new List<OrderInfo>();

            const string sql = @"
                SELECT TOP 4 
                    z.DataZamowienia,
                    ISNULL(SUM(t.Ilosc), 0) as TotalKg,
                    ISNULL(SUM(t.Pojemniki), 0) as TotalPojemniki,
                    ISNULL(SUM(t.Palety), 0) as TotalPalety
                FROM [dbo].[ZamowieniaMieso] z
                LEFT JOIN [dbo].[ZamowieniaMiesoTowar] t ON z.Id = t.ZamowienieId
                WHERE z.KlientId = @KlientId
                GROUP BY z.DataZamowienia, z.Id
                ORDER BY z.DataZamowienia DESC";

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@KlientId", clientId);

                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    orders.Add(new OrderInfo
                    {
                        DataZamowienia = rd.GetDateTime(0),
                        TotalKg = rd.GetDecimal(1),
                        TotalPojemniki = rd.GetInt32(2),
                        TotalPalety = rd.GetDecimal(3)
                    });
                }
            }
            catch
            {
                // W przypadku błędu zwróć pustą listę
            }

            return orders;
        }

        private async Task UpdateStatisticsPanel(string clientId)
        {
            if (lblAvgOrder == null || lblTopProduct == null || lblTrend == null) return;

            try
            {
                const string sql = @"
                    SELECT 
                        ISNULL(AVG(t.Ilosc), 0) as AvgKg,
                        COUNT(DISTINCT z.Id) as OrderCount
                    FROM [dbo].[ZamowieniaMieso] z
                    LEFT JOIN [dbo].[ZamowieniaMiesoTowar] t ON z.Id = t.ZamowienieId
                    WHERE z.KlientId = @KlientId 
                    AND z.DataZamowienia >= DATEADD(MONTH, -6, GETDATE())";

                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@KlientId", clientId);

                decimal avgKg = 0;
                int orderCount = 0;

                await using (var rd = await cmd.ExecuteReaderAsync())
                {
                    if (await rd.ReadAsync())
                    {
                        avgKg = rd.GetDecimal(0);
                        orderCount = rd.GetInt32(1);
                    }
                }

                lblAvgOrder.Text = $"Srednie: {avgKg:N0} kg";

                if (orderCount > 0)
                {
                    lblTrend.Text = $"Zamowien: {orderCount} (6 mies.)";
                    lblTrend.ForeColor = SUCCESS_COLOR;
                }
                else
                {
                    lblTrend.Text = "Trend: brak danych";
                    lblTrend.ForeColor = Color.FromArgb(107, 114, 128);
                }

                // Pobierz najczęściej zamawiany produkt
                const string sqlTop = @"
                    SELECT TOP 1 tw.Kod, SUM(t.Ilosc) as TotalIlosc
                    FROM [dbo].[ZamowieniaMieso] z
                    INNER JOIN [dbo].[ZamowieniaMiesoTowar] t ON z.Id = t.ZamowienieId
                    INNER JOIN [HANDEL].[HM].[TW] tw ON t.KodTowaru = tw.Id
                    WHERE z.KlientId = @KlientId 
                    AND z.DataZamowienia >= DATEADD(MONTH, -6, GETDATE())
                    GROUP BY tw.Kod
                    ORDER BY SUM(t.Ilosc) DESC";

                await using var cmd2 = new SqlCommand(sqlTop, cn);
                cmd2.Parameters.AddWithValue("@KlientId", clientId);

                await using var rd2 = await cmd2.ExecuteReaderAsync();
                if (await rd2.ReadAsync())
                {
                    string topProduct = rd2.GetString(0);
                    decimal totalKg = rd2.GetDecimal(1);
                    lblTopProduct.Text = $"Top: {topProduct.Substring(0, Math.Min(15, topProduct.Length))}";
                }
                else
                {
                    lblTopProduct.Text = "Top produkt: -";
                }
            }
            catch (Exception ex)
            {
                lblAvgOrder.Text = "Srednie: Brak";
                lblTopProduct.Text = "Top produkt: Blad";
                lblTrend.Text = $"Blad: {ex.Message.Substring(0, Math.Min(30, ex.Message.Length))}";
            }
        }

        #endregion

        #region Responsywny Layout

        private void ConfigureResponsiveLayout()
        {
            this.MinimumSize = new Size(1024, 768);

            if (panelDetaleZamowienia != null)
            {
                panelDetaleZamowienia.AutoScroll = true;
                panelDetaleZamowienia.Padding = new Padding(10, 5, 10, 5);
                ReorganizeDetailsPanel();
            }

            ReorganizeHeaderPanel();

            if (txtSzukajOdbiorcy != null)
            {
                txtSzukajOdbiorcy.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            }

            if (listaWynikowOdbiorcy != null)
            {
                listaWynikowOdbiorcy.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                listaWynikowOdbiorcy.MaximumSize = new Size(0, 155);
                listaWynikowOdbiorcy.BringToFront();
            }
        }

        private void ReorganizeHeaderPanel()
        {
            if (panelOdbiorca == null) return;

            panelOdbiorca.Padding = new Padding(10, 10, 10, 5);
            panelOdbiorca.Height = 120;

            var oldHandlowiecLabel = panelOdbiorca.Controls["label4"];
            if (oldHandlowiecLabel != null)
            {
                panelOdbiorca.Controls.Remove(oldHandlowiecLabel);
            }

            if (lblTytul != null && cbHandlowiecFilter != null)
            {
                lblTytul.Location = new Point(10, 10);
                lblTytul.Size = new Size(200, 35);

                cbHandlowiecFilter.Location = new Point(215, 12);
                cbHandlowiecFilter.Size = new Size(140, 30);
                cbHandlowiecFilter.Font = new Font("Segoe UI", 10f);
            }

            var lblOdbiorca = panelOdbiorca.Controls["label1"];
            if (lblOdbiorca != null)
            {
                lblOdbiorca.Location = new Point(10, 50);
                lblOdbiorca.Text = "[O] Odbiorca";
            }

            if (txtSzukajOdbiorcy != null)
            {
                txtSzukajOdbiorcy.Location = new Point(10, 70);
                txtSzukajOdbiorcy.Size = new Size(400, 28);
                txtSzukajOdbiorcy.Font = new Font("Segoe UI", 10f);
            }
        }

        private void ReorganizeDetailsPanel()
        {
            if (panelDetaleZamowienia == null) return;

            panelDetaleZamowienia.SuspendLayout();

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoScroll = true,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(5, 0, 5, 5)
            };

            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            if (panelOstatniOdbiorcy != null)
            {
                panelOstatniOdbiorcy.Dock = DockStyle.Top;
                panelOstatniOdbiorcy.MinimumSize = new Size(0, 260);
                panelOstatniOdbiorcy.MaximumSize = new Size(0, 380);
                panelOstatniOdbiorcy.AutoSize = false;
                panelOstatniOdbiorcy.Margin = new Padding(0, 0, 0, 5);
                mainLayout.Controls.Add(panelOstatniOdbiorcy, 0, 0);
            }

            var datesPanel = CreateDatesPanel();
            datesPanel.Height = 70;
            mainLayout.Controls.Add(datesPanel, 0, 1);

            var notesPanel = CreateNotesPanel();
            notesPanel.Height = 100;
            mainLayout.Controls.Add(notesPanel, 0, 2);

            if (panelTransport != null)
            {
                panelTransport.Dock = DockStyle.Top;
                panelTransport.Height = 100;
                mainLayout.Controls.Add(panelTransport, 0, 3);
            }

            panelDetaleZamowienia.Controls.Clear();
            panelDetaleZamowienia.Controls.Add(mainLayout);
            panelDetaleZamowienia.Controls.Add(listaWynikowOdbiorcy);

            panelDetaleZamowienia.ResumeLayout(true);
        }

        private Panel CreateDatesPanel()
        {
            var panel = new Panel
            {
                Height = 80,
                Dock = DockStyle.Top,
                Padding = new Padding(5),
                BackColor = Color.FromArgb(250, 251, 255)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                AutoSize = false
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblDate = new Label
            {
                Text = "[D] Data odbioru",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = INFO_COLOR,
                AutoSize = true,
                Padding = new Padding(0, 0, 0, 5)
            };

            var lblTime = new Label
            {
                Text = "[T] Godzina odbioru",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = PURPLE_COLOR,
                AutoSize = true,
                Padding = new Padding(5, 0, 0, 5)
            };

            if (dateTimePickerSprzedaz != null)
            {
                dateTimePickerSprzedaz.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            }

            if (dateTimePickerGodzinaPrzyjazdu != null)
            {
                dateTimePickerGodzinaPrzyjazdu.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            }

            layout.Controls.Add(lblDate, 0, 0);
            layout.Controls.Add(lblTime, 1, 0);
            layout.Controls.Add(dateTimePickerSprzedaz, 0, 1);
            layout.Controls.Add(dateTimePickerGodzinaPrzyjazdu, 1, 1);

            panel.Controls.Add(layout);
            return panel;
        }

        private Panel CreateNotesPanel()
        {
            var panel = new Panel
            {
                Height = 120,
                Dock = DockStyle.Top,
                Padding = new Padding(5),
                BackColor = Color.FromArgb(250, 255, 250)
            };

            var lblNotes = new Label
            {
                Text = "[N] Notatka",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = SUCCESS_COLOR,
                Location = new Point(5, 5),
                AutoSize = true
            };

            if (textBoxUwagi != null)
            {
                textBoxUwagi.Location = new Point(5, 25);
                textBoxUwagi.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
                textBoxUwagi.Size = new Size(panel.Width - 10, 85);
                textBoxUwagi.BackColor = Color.FromArgb(250, 255, 250);
            }

            panel.Controls.Add(lblNotes);
            panel.Controls.Add(textBoxUwagi);

            return panel;
        }

        private void CreateResponsiveTransportPanel()
        {
            panelTransport = new Panel
            {
                Height = 100,
                Dock = DockStyle.Top,
                BackColor = Color.White,
                Padding = new Padding(5)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                AutoSize = false
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var lblTransportHeader = new Label
            {
                Text = "[!] LIMITY TRANSPORTOWE",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = WARNING_COLOR,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            layout.Controls.Add(lblTransportHeader, 0, 0);
            layout.SetColumnSpan(lblTransportHeader, 2);

            var solowkaPanel = CreateCompactProgressPanel("Solowka (18 pal.)", 18, out progressSolowka, out lblSolowkaInfo);
            layout.Controls.Add(solowkaPanel, 0, 1);

            var tirPanel = CreateCompactProgressPanel("TIR (33 pal.)", 33, out progressTir, out lblTirInfo);
            layout.Controls.Add(tirPanel, 1, 1);

            panelTransport.Controls.Add(layout);

            panelTransport.Paint += (s, e) =>
            {
                using var path = GetRoundedRectPath(panelTransport.ClientRectangle, 8);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(Color.FromArgb(255, 251, 245));
                e.Graphics.FillPath(brush, path);
                using var pen = new Pen(WARNING_COLOR.WithAlpha(50), 1);
                e.Graphics.DrawPath(pen, path);
            };
        }

        private Panel CreateCompactProgressPanel(string labelText, int maxValue, out ProgressBar progressBar, out Label infoLabel)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5, 0, 5, 0)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                AutoSize = false
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));

            var label = new Label
            {
                Text = labelText,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(107, 114, 128),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            progressBar = new ProgressBar
            {
                Maximum = maxValue,
                Style = ProgressBarStyle.Continuous,
                Dock = DockStyle.Fill,
                Height = 22,
                Margin = new Padding(0, 2, 2, 2)
            };

            infoLabel = new Label
            {
                Text = $"0 / {maxValue}",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(31, 41, 55),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };

            layout.Controls.Add(label, 0, 0);
            layout.SetColumnSpan(label, 2);
            layout.Controls.Add(progressBar, 0, 1);
            layout.Controls.Add(infoLabel, 1, 1);

            panel.Controls.Add(layout);
            return panel;
        }

        #endregion

        #region Grid Ostatnich Odbiorców

        private void SetupOstatniOdbiorcyGrid()
        {
            if (panelOstatniOdbiorcy == null || gridOstatniOdbiorcy == null) return;

            panelOstatniOdbiorcy.AutoSize = false;
            panelOstatniOdbiorcy.Dock = DockStyle.Top;
            panelOstatniOdbiorcy.BackColor = Color.FromArgb(252, 252, 255);

            gridOstatniOdbiorcy.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            gridOstatniOdbiorcy.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            panelOstatniOdbiorcy.Paint += (s, e) =>
            {
                using var path = GetRoundedRectPath(panelOstatniOdbiorcy.ClientRectangle, 8);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                using var gradBrush = new LinearGradientBrush(
                    panelOstatniOdbiorcy.ClientRectangle,
                    Color.FromArgb(250, 250, 255),
                    Color.FromArgb(245, 245, 255),
                    90f);
                e.Graphics.FillPath(gradBrush, path);

                using var pen = new Pen(PRIMARY_COLOR.WithAlpha(50), 1);
                e.Graphics.DrawPath(pen, path);
            };

            gridOstatniOdbiorcy.DefaultCellStyle.SelectionBackColor = PRIMARY_COLOR;
            gridOstatniOdbiorcy.DefaultCellStyle.SelectionForeColor = Color.White;
            gridOstatniOdbiorcy.DefaultCellStyle.Padding = new Padding(8, 3, 8, 3);
            gridOstatniOdbiorcy.RowTemplate.Height = 28;
            gridOstatniOdbiorcy.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 255);

            gridOstatniOdbiorcy.CellClick -= GridOstatniOdbiorcy_CellClick;
            gridOstatniOdbiorcy.CellMouseEnter -= GridOstatniOdbiorcy_CellMouseEnter;
            gridOstatniOdbiorcy.CellMouseLeave -= GridOstatniOdbiorcy_CellMouseLeave;

            gridOstatniOdbiorcy.CellClick += GridOstatniOdbiorcy_CellClick;
            gridOstatniOdbiorcy.CellMouseEnter += GridOstatniOdbiorcy_CellMouseEnter;
            gridOstatniOdbiorcy.CellMouseLeave += GridOstatniOdbiorcy_CellMouseLeave;
        }

        private async void GridOstatniOdbiorcy_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                var value = gridOstatniOdbiorcy.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    await SelectOdbiorcaFromCell(value);
                }
            }
        }

        private void GridOstatniOdbiorcy_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                gridOstatniOdbiorcy.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = PRIMARY_COLOR.WithAlpha(30);
                gridOstatniOdbiorcy.Cursor = Cursors.Hand;
            }
        }

        private void GridOstatniOdbiorcy_CellMouseLeave(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                var row = gridOstatniOdbiorcy.Rows[e.RowIndex];
                row.Cells[e.ColumnIndex].Style.BackColor =
                    e.RowIndex % 2 == 0 ? Color.White : Color.FromArgb(250, 250, 255);
                gridOstatniOdbiorcy.Cursor = Cursors.Default;
            }
        }

        private async Task SelectOdbiorcaFromCell(string nazwaOdbiorcy)
        {
            var odbiorca = _kontrahenci.FirstOrDefault(k => k.Nazwa == nazwaOdbiorcy);
            if (odbiorca != null)
            {
                if (_selectedKlientId != null && _selectedKlientId != odbiorca.Id)
                {
                    _blokujObslugeZmian = true;
                    foreach (DataRow r in _dt.Rows)
                    {
                        r["E2"] = false;
                        r["Ilosc"] = 0m;
                        r["Pojemniki"] = 0m;
                        r["Palety"] = 0m;
                    }
                    _blokujObslugeZmian = false;
                    textBoxUwagi.Text = "";
                }

                UstawOdbiorce(odbiorca.Id);
                //await UpdateClientInfoPanels(odbiorca.Id);
                RecalcSum();
            }
        }

        private GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseAllFigures();
            return path;
        }

        private void UpdateOstatniOdbiorcyGrid(string? handlowiec)
        {
            if (panelOstatniOdbiorcy == null || gridOstatniOdbiorcy == null) return;

            panelOstatniOdbiorcy.Visible = true;

            if (string.IsNullOrEmpty(handlowiec) || handlowiec == "— Wszyscy —")
            {
                lblOstatniOdbiorcy.Text = "Wybierz handlowca aby zobaczyc odbiorców";
                lblOstatniOdbiorcy.ForeColor = WARNING_COLOR;
                gridOstatniOdbiorcy.DataSource = null;
                return;
            }

            var odbiorcy = _kontrahenci
                .Where(k => k.Handlowiec == handlowiec)
                .OrderByDescending(k => k.OstatnieZamowienie.HasValue && k.OstatnieZamowienie >= DateTime.Now.AddMonths(-1))
                .ThenBy(k => k.Nazwa)
                .Select(k => k.Nazwa)
                .ToList();

            if (!odbiorcy.Any())
            {
                lblOstatniOdbiorcy.Text = $"Brak odbiorcow dla: {handlowiec}";
                lblOstatniOdbiorcy.ForeColor = DANGER_COLOR;
                gridOstatniOdbiorcy.DataSource = null;
                return;
            }

            var dt = new DataTable();
            dt.Columns.Add("Kolumna1", typeof(string));
            dt.Columns.Add("Kolumna2", typeof(string));

            for (int i = 0; i < odbiorcy.Count; i += 2)
            {
                var row = dt.NewRow();
                row["Kolumna1"] = odbiorcy[i];
                row["Kolumna2"] = (i + 1 < odbiorcy.Count) ? odbiorcy[i + 1] : "";
                dt.Rows.Add(row);
            }

            gridOstatniOdbiorcy.DataSource = dt;

            if (gridOstatniOdbiorcy.Parent != null)
            {
                gridOstatniOdbiorcy.Location = new Point(10, 30);
                gridOstatniOdbiorcy.Size = new Size(390, panelOstatniOdbiorcy.Height - 40);
            }

            if (gridOstatniOdbiorcy.Columns.Count > 0)
            {
                gridOstatniOdbiorcy.Columns["Kolumna1"].FillWeight = 50;
                gridOstatniOdbiorcy.Columns["Kolumna2"].FillWeight = 50;
            }

            foreach (DataGridViewRow row in gridOstatniOdbiorcy.Rows)
            {
                for (int col = 0; col < 2; col++)
                {
                    var nazwa = row.Cells[col].Value?.ToString();
                    if (!string.IsNullOrEmpty(nazwa))
                    {
                        var kontrahent = _kontrahenci.FirstOrDefault(k => k.Nazwa == nazwa);
                        if (kontrahent?.OstatnieZamowienie != null &&
                            kontrahent.OstatnieZamowienie >= DateTime.Now.AddMonths(-1))
                        {
                            row.Cells[col].Style.Font = new Font(gridOstatniOdbiorcy.Font, FontStyle.Bold);
                            row.Cells[col].Style.ForeColor = SUCCESS_COLOR;
                        }
                    }
                }
            }

            lblOstatniOdbiorcy.Text = $"[U] Odbiorcy ({odbiorcy.Count}):";
            lblOstatniOdbiorcy.ForeColor = PRIMARY_COLOR;
        }

        private async Task LoadOstatnieZamowienia()
        {
            const string sql = @"
                SELECT KlientId, MAX(DataZamowienia) as OstatnieZamowienie
                FROM [dbo].[ZamowieniaMieso]
                WHERE DataZamowienia >= DATEADD(MONTH, -4, GETDATE())
                GROUP BY KlientId";

            _ostatnieZamowienia.Clear();

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                await using var rd = await cmd.ExecuteReaderAsync();

                while (await rd.ReadAsync())
                {
                    string klientId = rd.GetInt32(0).ToString();
                    DateTime data = rd.GetDateTime(1);
                    _ostatnieZamowienia[klientId] = data;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd pobierania ostatnich zamówień: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            foreach (var k in _kontrahenci)
            {
                if (_ostatnieZamowienia.TryGetValue(k.Id, out var data))
                {
                    k.OstatnieZamowienie = data;
                }
                else
                {
                    k.OstatnieZamowienie = null;
                }
            }
        }

        #endregion

        #region Panel Transportu

        private void UpdateTransportBars(decimal palety)
        {
            if (progressSolowka == null || progressTir == null) return;

            int paletyInt = (int)Math.Ceiling(palety);

            progressSolowka.Value = Math.Min(paletyInt, 18);
            lblSolowkaInfo!.Text = $"{paletyInt:N0} / 18";

            if (paletyInt <= 18)
            {
                SetProgressBarColor(progressSolowka, SUCCESS_COLOR);
                lblSolowkaInfo.ForeColor = SUCCESS_COLOR;
            }
            else
            {
                SetProgressBarColor(progressSolowka, DANGER_COLOR);
                lblSolowkaInfo.ForeColor = DANGER_COLOR;
                lblSolowkaInfo.Text = $"{paletyInt:N0} / 18 [!]";
            }

            progressTir.Value = Math.Min(paletyInt, 33);
            lblTirInfo!.Text = $"{paletyInt:N0} / 33";

            if (paletyInt <= 33)
            {
                SetProgressBarColor(progressTir, SUCCESS_COLOR);
                lblTirInfo.ForeColor = SUCCESS_COLOR;
            }
            else
            {
                SetProgressBarColor(progressTir, DANGER_COLOR);
                lblTirInfo.ForeColor = DANGER_COLOR;
                lblTirInfo.Text = $"{paletyInt:N0} / 33 [!]";
            }
        }

        private void SetProgressBarColor(ProgressBar bar, Color color)
        {
            bar.ForeColor = color;
            if (bar.Value == bar.Maximum)
            {
                bar.Style = ProgressBarStyle.Continuous;
            }
        }

        #endregion

        #region Panel Podsumowania

        private void CreateSummaryPanel()
        {
            panelSummary = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                BackColor = Color.White,
                Parent = panelDetails
            };

            panelSummary.Paint += (s, e) =>
            {
                var rect = panelSummary.ClientRectangle;
                using var brush = new LinearGradientBrush(rect, PRIMARY_COLOR, PURPLE_COLOR, 45f);
                e.Graphics.FillRectangle(brush, rect);
            };

            var flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(30, 15, 30, 15),
                BackColor = Color.Transparent
            };

            lblSumaPalet = CreateSummaryLabel("[P] PALETY", "0", DANGER_COLOR);
            lblSumaPojemnikow = CreateSummaryLabel("[#] POJEMNIKI", "0", INFO_COLOR);
            lblSumaKg = CreateSummaryLabel("[kg] KILOGRAMY", "0", SUCCESS_COLOR);

            flowPanel.Controls.Add(lblSumaPalet);
            flowPanel.Controls.Add(CreateSeparator());
            flowPanel.Controls.Add(lblSumaPojemnikow);
            flowPanel.Controls.Add(CreateSeparator());
            flowPanel.Controls.Add(lblSumaKg);

            panelSummary.Controls.Add(flowPanel);
        }

        private Label CreateSummaryLabel(string title, string value, Color accentColor)
        {
            var panel = new Panel
            {
                Width = 220,
                Height = 35,
                Margin = new Padding(15, 0, 15, 0),
                BackColor = Color.Transparent
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(200, 255, 255, 255),
                Location = new Point(0, 0),
                AutoSize = true
            };

            var lblValue = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(0, 12),
                AutoSize = true,
                Name = $"lbl{title}"
            };

            panel.Controls.Add(lblTitle);
            panel.Controls.Add(lblValue);

            return panel.Controls[1] as Label ?? new Label();
        }

        private Panel CreateSeparator()
        {
            return new Panel
            {
                Width = 1,
                Height = 40,
                BackColor = Color.FromArgb(100, 255, 255, 255),
                Margin = new Padding(0, 0, 0, 0)
            };
        }

        #endregion

        #region Inicjalizacja i Ustawienia UI

        private void SzybkiGrid()
        {
            dataGridViewZamowienie.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewZamowienie.AllowUserToAddRows = false;
            dataGridViewZamowienie.AllowUserToDeleteRows = false;
            dataGridViewZamowienie.RowHeadersVisible = false;
            dataGridViewZamowienie.SelectionMode = DataGridViewSelectionMode.CellSelect;
            dataGridViewZamowienie.MultiSelect = true;
            dataGridViewZamowienie.EditMode = DataGridViewEditMode.EditOnKeystroke;
            dataGridViewZamowienie.BackgroundColor = Color.White;
            dataGridViewZamowienie.BorderStyle = BorderStyle.None;
            dataGridViewZamowienie.GridColor = Color.FromArgb(230, 235, 245);
            dataGridViewZamowienie.Font = new Font("Segoe UI", 10f);

            dataGridViewZamowienie.EnableHeadersVisualStyles = false;
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.BackColor = PRIMARY_COLOR;
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.SelectionBackColor = PRIMARY_COLOR;
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            dataGridViewZamowienie.ColumnHeadersHeight = 45;
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewZamowienie.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;

            dataGridViewZamowienie.RowTemplate.Height = 32;
            // Zmieniony kolor zaznaczenia
            dataGridViewZamowienie.DefaultCellStyle.SelectionBackColor = Color.FromArgb(187, 247, 208);
            dataGridViewZamowienie.DefaultCellStyle.SelectionForeColor = Color.FromArgb(21, 128, 61);
            dataGridViewZamowienie.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 255);
            dataGridViewZamowienie.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;

            dataGridViewZamowienie.ScrollBars = ScrollBars.Vertical;
            dataGridViewZamowienie.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;

            TryEnableDoubleBuffer(dataGridViewZamowienie);
        }

        private static void TryEnableDoubleBuffer(Control c)
        {
            try
            {
                var pi = c.GetType().GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                pi?.SetValue(c, true, null);
            }
            catch { }
        }

        private void WireShortcuts()
        {
            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.S) { e.SuppressKeyPress = true; btnZapisz.PerformClick(); }
                else if (e.KeyCode == Keys.Delete) { e.SuppressKeyPress = true; ZeroSelectedCells(); }
            };
        }

        private void InitDefaults()
        {
            this.Cursor = Cursors.WaitCursor;
            btnZapisz.Enabled = false;
            var dzis = DateTime.Now.Date;
            dateTimePickerSprzedaz.Value = (dzis.DayOfWeek == DayOfWeek.Friday) ? dzis.AddDays(3) : dzis.AddDays(1);
            dateTimePickerGodzinaPrzyjazdu.Value = DateTime.Today.AddHours(8);
            RecalcSum();
        }

        private void BuildDataTableSchema()
        {
            _dt.Columns.Add("Id", typeof(int));
            _dt.Columns.Add("Kod", typeof(string));
            _dt.Columns.Add("E2", typeof(bool));
            _dt.Columns.Add("Palety", typeof(decimal));
            _dt.Columns.Add("Pojemniki", typeof(decimal));
            _dt.Columns.Add("Ilosc", typeof(decimal));
            _dt.Columns.Add("KodTowaru", typeof(string));
            _dt.Columns.Add("KodKopia", typeof(string));

            _view = new DataView(_dt);
            dataGridViewZamowienie.DataSource = _view;

            dataGridViewZamowienie.Columns["Id"]!.Visible = false;
            dataGridViewZamowienie.Columns["KodTowaru"]!.Visible = false;

            var cKod = dataGridViewZamowienie.Columns["Kod"]!;
            cKod.ReadOnly = true;
            cKod.FillWeight = 180;
            cKod.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            cKod.DefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            cKod.DefaultCellStyle.ForeColor = Color.FromArgb(31, 41, 55);
            cKod.HeaderText = "[T] Towar";

            var cE2 = dataGridViewZamowienie.Columns["E2"] as DataGridViewCheckBoxColumn;
            if (cE2 != null)
            {
                cE2.HeaderText = "[E2] 40poj";
                cE2.FillWeight = 50;
                cE2.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                cE2.ToolTipText = "40 pojemników/paletę";
            }

            var cPalety = dataGridViewZamowienie.Columns["Palety"]!;
            cPalety.FillWeight = 80;
            cPalety.DefaultCellStyle.Format = "N0";
            cPalety.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            cPalety.DefaultCellStyle.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            cPalety.DefaultCellStyle.ForeColor = DANGER_COLOR;
            cPalety.HeaderText = "[P] Palety";

            var cPojemniki = dataGridViewZamowienie.Columns["Pojemniki"]!;
            cPojemniki.FillWeight = 100;
            cPojemniki.DefaultCellStyle.Format = "N0";
            cPojemniki.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            cPojemniki.DefaultCellStyle.ForeColor = INFO_COLOR;
            cPojemniki.HeaderText = "[#] Pojemniki";

            var cIlosc = dataGridViewZamowienie.Columns["Ilosc"]!;
            cIlosc.FillWeight = 110;
            cIlosc.DefaultCellStyle.Format = "N0";
            cIlosc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            cIlosc.HeaderText = "[kg] Ilosc";
            cIlosc.DefaultCellStyle.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            cIlosc.DefaultCellStyle.ForeColor = Color.FromArgb(5, 150, 105);
            cIlosc.DefaultCellStyle.BackColor = Color.FromArgb(240, 253, 244);

            var cKodKopia = dataGridViewZamowienie.Columns["KodKopia"]!;
            cKodKopia.ReadOnly = true;
            cKodKopia.FillWeight = 180;
            cKodKopia.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            cKodKopia.DefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            cKodKopia.DefaultCellStyle.ForeColor = Color.FromArgb(31, 41, 55);
            cKodKopia.DefaultCellStyle.BackColor = Color.FromArgb(250, 250, 252);
            cKodKopia.HeaderText = "[T] Towar";
        }

        private void WireUpUIEvents()
        {
            dataGridViewZamowienie.CellValueChanged += DataGridViewZamowienie_CellValueChanged;
            dataGridViewZamowienie.EditingControlShowing += DataGridViewZamowienie_EditingControlShowing;
            dataGridViewZamowienie.CellPainting += DataGridViewZamowienie_CellPainting;
            dataGridViewZamowienie.CellFormatting += DataGridViewZamowienie_CellFormatting;
            dataGridViewZamowienie.ColumnWidthChanged += (s, e) => dataGridViewZamowienie.Invalidate();
            dataGridViewZamowienie.CurrentCellDirtyStateChanged += DataGridViewZamowienie_CurrentCellDirtyStateChanged;

            txtSzukajOdbiorcy.TextChanged += TxtSzukajOdbiorcy_TextChanged;
            txtSzukajOdbiorcy.KeyDown += TxtSzukajOdbiorcy_KeyDown;

            listaWynikowOdbiorcy.Click += ListaWynikowOdbiorcy_Click;
            listaWynikowOdbiorcy.KeyDown += ListaWynikowOdbiorcy_KeyDown;

            var hands = _kontrahenci.Select(k => k.Handlowiec).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList();
            hands.Insert(0, "— Wszyscy —");
            cbHandlowiecFilter.Items.Clear();
            cbHandlowiecFilter.Items.AddRange(hands.ToArray());
            cbHandlowiecFilter.SelectedIndex = 0;
            cbHandlowiecFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cbHandlowiecFilter.SelectedIndexChanged += CbHandlowiecFilter_SelectedIndexChanged;
        }

        private void DataGridViewZamowienie_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = (dataGridViewZamowienie.Rows[e.RowIndex].DataBoundItem as DataRowView)?.Row;
            if (row == null) return;

            decimal ilosc = row.Field<decimal?>("Ilosc") ?? 0m;

            if (ilosc > 0)
            {
                e.CellStyle.BackColor = Color.FromArgb(220, 252, 231);
                e.CellStyle.ForeColor = Color.FromArgb(21, 128, 61);
            }
            else
            {
                if (e.RowIndex % 2 == 0)
                {
                    e.CellStyle.BackColor = Color.White;
                }
                else
                {
                    e.CellStyle.BackColor = Color.FromArgb(250, 251, 255);
                }
                e.CellStyle.ForeColor = Color.FromArgb(31, 41, 55);
            }
        }

        private void ListaWynikowOdbiorcy_Click(object? sender, EventArgs e) => WybierzOdbiorceZListy();

        private async void CbHandlowiecFilter_SelectedIndexChanged(object? sender, EventArgs e)
        {
            string? handlowiec = cbHandlowiecFilter.SelectedItem?.ToString();
            await LoadOstatnieZamowienia();
            UpdateOstatniOdbiorcyGrid(handlowiec);
            TxtSzukajOdbiorcy_TextChanged(null, EventArgs.Empty);
        }

        private void DataGridViewZamowienie_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            if (dataGridViewZamowienie.CurrentCell is DataGridViewCheckBoxCell)
            {
                dataGridViewZamowienie.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        #endregion

        #region Asynchroniczne Ładowanie Danych

        private async Task LoadInitialDataInBackground()
        {
            var towaryTask = LoadTowaryAsRowsAsync();
            var kontrahenciTask = LoadKontrahenciAsync();
            await Task.WhenAll(towaryTask, kontrahenciTask);
        }

        private async Task LoadTowaryAsRowsAsync()
        {
            _dt.Clear();

            var excludedProducts = new HashSet<string> { "KURCZAK B", "FILET C" };

            var priorityOrder = new Dictionary<string, int>
            {
                { "KURCZAK A", 1 },
                { "FILET A", 2 },
                { "ĆWIARTKA", 3 },
                { "SKRZYDŁO I", 4 },
                { "NOGA", 5 },
                { "PAŁKA", 6 },
                { "KORPUS", 7 },
                { "POLĘDWICZKI", 8 },
                { "SERCE", 9 },
                { "WĄTROBA", 10 },
                { "ŻOŁĄDKI", 11 },
                { "ĆWIARTKA II", 12 },
                { "FILET II", 13 },
                { "FILET II PP", 14 },
                { "SKRZYDŁO II", 15 }
            };

            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT Id, Kod FROM [HANDEL].[HM].[TW] WHERE katalog = '67095' ORDER BY Kod ASC", cn);
            await using var rd = await cmd.ExecuteReaderAsync();

            var tempList = new List<(int Id, string Kod, int Priority)>();

            while (await rd.ReadAsync())
            {
                var kod = rd.GetString(1);

                if (excludedProducts.Any(excluded => kod.ToUpper().Contains(excluded)))
                    continue;

                int priority = int.MaxValue;
                foreach (var kvp in priorityOrder)
                {
                    if (kod.ToUpper().Contains(kvp.Key))
                    {
                        priority = kvp.Value;
                        break;
                    }
                }

                tempList.Add((rd.GetInt32(0), kod, priority));
            }

            var sortedList = tempList
                .OrderBy(x => x.Priority)
                .ThenBy(x => x.Kod)
                .ToList();

            foreach (var item in sortedList)
            {
                _dt.Rows.Add(item.Id, item.Kod, false, 0m, 0m, 0m, item.Kod, item.Kod);
            }
        }

        private async Task LoadKontrahenciAsync()
        {
            const string sql = @"
                SELECT
                    c.Id,
                    c.Shortcut AS Nazwa,
                    c.NIP,
                    poa.Postcode AS KodPocztowy,
                    poa.Street AS Miejscowosc, 
                    wym.CDim_Handlowiec_Val AS Handlowiec
                FROM
                    [HANDEL].[SSCommon].[STContractors] c
                LEFT JOIN
                    [HANDEL].[SSCommon].[STPostOfficeAddresses] poa ON poa.ContactGuid = c.ContactGuid AND poa.AddressName = N'adres domyślny'
                LEFT JOIN
                    [HANDEL].[SSCommon].[ContractorClassification] wym ON c.Id = wym.ElementId
                ORDER BY
                    c.Shortcut;";

            _kontrahenci.Clear();
            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            await using var rd = await cmd.ExecuteReaderAsync();

            while (await rd.ReadAsync())
            {
                _kontrahenci.Add(new KontrahentInfo
                {
                    Id = rd["Id"]?.ToString() ?? "",
                    Nazwa = rd["Nazwa"]?.ToString() ?? "",
                    NIP = rd["NIP"]?.ToString() ?? "",
                    KodPocztowy = rd["KodPocztowy"]?.ToString() ?? "",
                    Miejscowosc = rd["Miejscowosc"]?.ToString() ?? "",
                    Handlowiec = rd["Handlowiec"]?.ToString() ?? ""
                });
            }
        }

        #endregion

        #region Logika Biznesowa i Zdarzenia UI

        private void TxtSzukajOdbiorcy_TextChanged(object? sender, EventArgs e)
        {
            var query = txtSzukajOdbiorcy.Text.Trim().ToLower();
            var handlowiec = cbHandlowiecFilter.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(query))
            {
                listaWynikowOdbiorcy.Visible = false;
                return;
            }

            IEnumerable<KontrahentInfo> zrodlo = _kontrahenci;

            if (handlowiec != null && handlowiec != "— Wszyscy —")
            {
                zrodlo = zrodlo.Where(k => k.Handlowiec == handlowiec);
            }

            var wyniki = zrodlo
                .Where(k => k.Nazwa.ToLower().Contains(query) || k.Miejscowosc.ToLower().Contains(query) || k.NIP.Contains(query))
                .Take(10)
                .ToList();

            listaWynikowOdbiorcy.DataSource = wyniki;
            listaWynikowOdbiorcy.DisplayMember = "Nazwa";
            listaWynikowOdbiorcy.ValueMember = "Id";

            if (wyniki.Any())
            {
                var screenPoint = txtSzukajOdbiorcy.Parent.PointToScreen(txtSzukajOdbiorcy.Location);
                var clientPoint = panelDetaleZamowienia.PointToClient(screenPoint);

                listaWynikowOdbiorcy.Location = new Point(
                    clientPoint.X,
                    clientPoint.Y + txtSzukajOdbiorcy.Height + 50
                );
                listaWynikowOdbiorcy.Width = txtSzukajOdbiorcy.Width;
                listaWynikowOdbiorcy.Height = Math.Min(180, wyniki.Count * 22 + 5);

                listaWynikowOdbiorcy.BackColor = Color.White;
                listaWynikowOdbiorcy.ForeColor = Color.FromArgb(31, 41, 55);
                listaWynikowOdbiorcy.BorderStyle = BorderStyle.FixedSingle;
                listaWynikowOdbiorcy.Font = new Font("Segoe UI", 9.5f);

                listaWynikowOdbiorcy.Visible = true;
                listaWynikowOdbiorcy.BringToFront();
            }
            else
            {
                listaWynikowOdbiorcy.Visible = false;
            }
        }

        private void TxtSzukajOdbiorcy_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down && listaWynikowOdbiorcy.Visible && listaWynikowOdbiorcy.Items.Count > 0)
            {
                listaWynikowOdbiorcy.Focus();
                listaWynikowOdbiorcy.SelectedIndex = 0;
            }
            else if (e.KeyCode == Keys.Enter && listaWynikowOdbiorcy.Visible && listaWynikowOdbiorcy.Items.Count > 0)
            {
                WybierzOdbiorceZListy();
                e.SuppressKeyPress = true;
            }
        }

        private void ListaWynikowOdbiorcy_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                WybierzOdbiorceZListy();
                e.SuppressKeyPress = true;
            }
        }

        private async void WybierzOdbiorceZListy()
        {
            if (listaWynikowOdbiorcy.SelectedItem is KontrahentInfo wybrany)
            {
                UstawOdbiorce(wybrany.Id);
                //await UpdateClientInfoPanels(wybrany.Id);
            }
        }

        private void UstawOdbiorce(string id)
        {
            _selectedKlientId = id;
            var info = _kontrahenci.FirstOrDefault(k => k.Id == id);
            if (info != null)
            {
                txtSzukajOdbiorcy.Text = info.Nazwa;
                listaWynikowOdbiorcy.Visible = false;
                panelDaneOdbiorcy.Visible = true;
                lblWybranyOdbiorca.Text = info.Nazwa;
                lblNip.Text = $"NIP: {info.NIP}";
                lblAdres.Text = $"[LOC] {info.KodPocztowy} {info.Miejscowosc}";
                lblHandlowiec.Text = $"[U] Opiekun: {info.Handlowiec}";
                dataGridViewZamowienie.Focus();
            }
        }

        private void DataGridViewZamowienie_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (_blokujObslugeZmian || e.RowIndex < 0) return;

            var row = (dataGridViewZamowienie.Rows[e.RowIndex].DataBoundItem as DataRowView)?.Row;
            if (row == null) return;

            _blokujObslugeZmian = true;

            string changedColumnName = dataGridViewZamowienie.Columns[e.ColumnIndex].Name;
            string kodTowaru = row.Field<string>("Kod") ?? "";

            bool useE2 = row.Field<bool>("E2");
            decimal pojemnikNaPalete = useE2 ? POJEMNIKOW_NA_PALECIE_E2 : POJEMNIKOW_NA_PALECIE;
            decimal kgNaPojemnik = GetKgPerContainer(kodTowaru);
            decimal kgNaPalete = pojemnikNaPalete * kgNaPojemnik;

            try
            {
                switch (changedColumnName)
                {
                    case "E2":
                        decimal currentPalety = ParseDec(row["Palety"]);
                        if (currentPalety > 0)
                        {
                            row["Pojemniki"] = currentPalety * pojemnikNaPalete;
                            row["Ilosc"] = currentPalety * kgNaPalete;
                        }
                        break;

                    case "Ilosc":
                        decimal ilosc = ParseDec(row["Ilosc"]);
                        row["Pojemniki"] = (ilosc > 0 && kgNaPojemnik > 0) ? Math.Round(ilosc / kgNaPojemnik, 0) : 0m;
                        row["Palety"] = (ilosc > 0 && kgNaPalete > 0) ? ilosc / kgNaPalete : 0m;
                        MarkInvalid(dataGridViewZamowienie.Rows[e.RowIndex].Cells["Ilosc"], ilosc < 0);
                        break;

                    case "Pojemniki":
                        decimal pojemniki = ParseDec(row["Pojemniki"]);
                        row["Ilosc"] = pojemniki * kgNaPojemnik;
                        row["Palety"] = (pojemniki > 0 && pojemnikNaPalete > 0) ? pojemniki / pojemnikNaPalete : 0m;
                        break;

                    case "Palety":
                        decimal palety = ParseDec(row["Palety"]);
                        row["Pojemniki"] = palety * pojemnikNaPalete;
                        row["Ilosc"] = palety * kgNaPalete;
                        break;
                }
            }
            finally
            {
                _blokujObslugeZmian = false;
            }
            RecalcSum();
        }

        private void DataGridViewZamowienie_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is TextBox tb)
            {
                tb.KeyPress -= OnlyNumeric_KeyPress;
                tb.KeyPress += OnlyNumeric_KeyPress;
            }
        }

        private void ZeroSelectedCells()
        {
            _blokujObslugeZmian = true;
            foreach (DataGridViewCell c in dataGridViewZamowienie.SelectedCells)
            {
                var row = (c.OwningRow.DataBoundItem as DataRowView)?.Row;
                if (row == null) continue;

                row["Palety"] = 0m;
                row["Pojemniki"] = 0m;
                row["Ilosc"] = 0m;
            }
            _blokujObslugeZmian = false;
            RecalcSum();
        }

        private void CreateHeaderIcons()
        {
            // Ikony są teraz tekstowe [T], [P], etc.
        }

        private void RecalcSum()
        {
            decimal sumaIlosc = 0m;
            decimal sumaPalety = 0m;
            decimal sumaPojemniki = 0m;

            foreach (DataRow row in _dt.Rows)
            {
                decimal ilosc = row.Field<decimal?>("Ilosc") ?? 0m;
                decimal pojemniki = row.Field<decimal?>("Pojemniki") ?? 0m;
                decimal palety = row.Field<decimal?>("Palety") ?? 0m;

                sumaIlosc += ilosc;
                sumaPojemniki += pojemniki;
                sumaPalety += palety;
            }

            if (lblSumaPalet != null) lblSumaPalet.Text = sumaPalety.ToString("N1");
            if (lblSumaPojemnikow != null) lblSumaPojemnikow.Text = sumaPojemniki.ToString("N0");
            if (lblSumaKg != null) lblSumaKg.Text = sumaIlosc.ToString("N0");

            UpdateTransportBars(sumaPalety);
        }

        private decimal ParseDec(object? v)
        {
            var s = v?.ToString()?.Trim();
            if (string.IsNullOrEmpty(s)) return 0m;
            if (decimal.TryParse(s, System.Globalization.NumberStyles.Number, _pl, out var d)) return d;
            if (decimal.TryParse(s, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d2)) return d2;
            return 0m;
        }

        private void MarkInvalid(DataGridViewCell cell, bool invalid) =>
            cell.Style.BackColor = invalid ? Color.FromArgb(254, 226, 226) : dataGridViewZamowienie.DefaultCellStyle.BackColor;

        private void OnlyNumeric_KeyPress(object? sender, KeyPressEventArgs e)
        {
            char dec = _pl.NumberFormat.NumberDecimalSeparator[0];
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != dec) e.Handled = true;
            if (e.KeyChar == dec && sender is TextBox tb && tb.Text.Contains(dec)) e.Handled = true;
        }

        private void DataGridViewZamowienie_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            // Proste znaki zamiast emoji
        }

        private void ClearFormForNewOrder()
        {
            var selectedHandlowiec = cbHandlowiecFilter.SelectedItem;

            _idZamowieniaDoEdycji = null;
            _selectedKlientId = null;
            txtSzukajOdbiorcy.Text = "";
            panelDaneOdbiorcy.Visible = false;
            listaWynikowOdbiorcy.Visible = false;
            _view.RowFilter = string.Empty;

            _blokujObslugeZmian = true;
            foreach (DataRow r in _dt.Rows)
            {
                r["E2"] = false;
                r["Ilosc"] = 0m;
                r["Pojemniki"] = 0m;
                r["Palety"] = 0m;
            }
            _blokujObslugeZmian = false;

            textBoxUwagi.Text = "";

            var dzis = DateTime.Now.Date;
            dateTimePickerSprzedaz.Value = (dzis.DayOfWeek == DayOfWeek.Friday) ? dzis.AddDays(3) : dzis.AddDays(1);
            dateTimePickerGodzinaPrzyjazdu.Value = DateTime.Today.AddHours(8);

            lblTytul.Text = "[+] Nowe zamowienie";
            btnZapisz.Text = "[S] Zapisz";

            cbHandlowiecFilter.SelectedItem = selectedHandlowiec;

            if (listRecentOrders != null)
            {
                listRecentOrders.Items.Clear();
                listRecentOrders.Items.Add("Wybierz kontrahenta");
            }

            if (lblPreferredTime != null)
            {
                lblPreferredTime.Text = "Preferowane godziny:\nWybierz kontrahenta";
            }

            if (lblAvgOrder != null) lblAvgOrder.Text = "Srednie: 0 kg";
            if (lblTopProduct != null) lblTopProduct.Text = "Top produkt: -";
            if (lblTrend != null)
            {
                lblTrend.Text = "Trend: wybierz klienta";
                lblTrend.ForeColor = Color.FromArgb(107, 114, 128);
            }

            RecalcSum();
            txtSzukajOdbiorcy.Focus();
        }

        private bool IsSpecialProduct(string kod)
        {
            if (string.IsNullOrEmpty(kod)) return false;

            var kodUpper = kod.ToUpper();
            return kodUpper.Contains("WĄTROBA") ||
                   kodUpper.Contains("ŻOŁĄDKI") ||
                   kodUpper.Contains("SERCE");
        }

        private decimal GetKgPerContainer(string kod)
        {
            return IsSpecialProduct(kod) ? KG_NA_POJEMNIKU_SPECJALNY : KG_NA_POJEMNIKU;
        }

        #endregion

        #region Zapis i Odczyt Zamówienia

        private async Task LoadZamowienieAsync(int id)
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            string klientId = "";

            await using (var cmdZ = new SqlCommand("SELECT DataZamowienia, KlientId, Uwagi, DataPrzyjazdu FROM [dbo].[ZamowieniaMieso] WHERE Id=@Id", cn))
            {
                cmdZ.Parameters.AddWithValue("@Id", id);
                await using var rd = await cmdZ.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    dateTimePickerSprzedaz.Value = rd.GetDateTime(0);
                    klientId = rd.GetInt32(1).ToString();
                    UstawOdbiorce(klientId);
                    textBoxUwagi.Text = await rd.IsDBNullAsync(2) ? "" : rd.GetString(2);
                    dateTimePickerGodzinaPrzyjazdu.Value = rd.GetDateTime(3);
                }
            }

            _blokujObslugeZmian = true;
            foreach (DataRow r in _dt.Rows)
            {
                r["E2"] = false;
                r["Ilosc"] = 0m;
                r["Pojemniki"] = 0m;
                r["Palety"] = 0m;
            }

            await using (var cmdT = new SqlCommand("SELECT KodTowaru, Ilosc, ISNULL(Pojemniki, 0) as Pojemniki, ISNULL(Palety, 0) as Palety, ISNULL(E2, 0) as E2 FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId=@Id", cn))
            {
                cmdT.Parameters.AddWithValue("@Id", id);
                await using var rd = await cmdT.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int towarId = rd.GetInt32(0);
                    var rows = _dt.Select($"Id = {towarId}");
                    if (rows.Any())
                    {
                        decimal ilosc = await rd.IsDBNullAsync(1) ? 0m : rd.GetDecimal(1);
                        int pojemniki = rd.GetInt32(2);
                        decimal palety = rd.GetDecimal(3);
                        bool e2 = rd.GetBoolean(4);

                        rows[0]["Ilosc"] = ilosc;
                        rows[0]["Pojemniki"] = pojemniki;
                        rows[0]["Palety"] = palety;
                        rows[0]["E2"] = e2;
                    }
                }
            }
            _blokujObslugeZmian = false;

            if (!string.IsNullOrEmpty(klientId))
            {
                //await UpdateClientInfoPanels(klientId);
            }

            RecalcSum();
        }

        private bool ValidateBeforeSave(out string message)
        {
            if (string.IsNullOrWhiteSpace(_selectedKlientId))
            {
                message = "Wybierz odbiorcę.";
                return false;
            }
            if (!_dt.AsEnumerable().Any(r => r.Field<decimal>("Ilosc") > 0m))
            {
                message = "Wpisz ilość dla przynajmniej jednego towaru.";
                return false;
            }
            if (_dt.AsEnumerable().Any(r => r.Field<decimal>("Ilosc") < 0m))
            {
                message = "Ilość nie może być ujemna.";
                return false;
            }
            message = "";
            return true;
        }

        private async void btnZapisz_Click(object? sender, EventArgs e)
        {
            if (!ValidateBeforeSave(out var msg))
            {
                MessageBox.Show(msg, "Błąd danych", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Cursor = Cursors.WaitCursor;
            btnZapisz.Enabled = false;

            try
            {
                await SaveOrderAsync();
                string summary = BuildOrderSummary();
                string title = _idZamowieniaDoEdycji.HasValue ? "[OK] Zamowienie zaktualizowane" : "[OK] Zamowienie zapisane";

                MessageBox.Show(summary, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadOstatnieZamowienia();
                UpdateOstatniOdbiorcyGrid(cbHandlowiecFilter.SelectedItem?.ToString());
                ClearFormForNewOrder();
            }
            catch (Exception ex)
            {
                MessageBox.Show("[ERR] Blad zapisu: " + ex.Message, "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                btnZapisz.Enabled = true;
            }
        }

        private string BuildOrderSummary()
        {
            var sb = new StringBuilder();
            var orderedItems = _dt.AsEnumerable()
                .Where(r => r.Field<decimal?>("Ilosc") > 0m)
                .ToList();

            sb.AppendLine($"[O] Odbiorca: {lblWybranyOdbiorca.Text}");
            sb.AppendLine($"[D] Data: {dateTimePickerSprzedaz.Value:yyyy-MM-dd}");

            var e2Items = orderedItems.Where(r => r.Field<bool>("E2")).ToList();
            if (e2Items.Any())
            {
                sb.AppendLine($"[E2] Towary E2 (40 poj./pal.): {e2Items.Count}");
            }

            sb.AppendLine("\n[T] Zamowione towary:");

            decimal totalPojemniki = 0;
            decimal totalPalety = 0;

            foreach (var item in orderedItems)
            {
                string e2Marker = item.Field<bool>("E2") ? " [E2]" : "";
                decimal pojemniki = item.Field<decimal>("Pojemniki");
                decimal palety = item.Field<decimal>("Palety");

                totalPojemniki += pojemniki;
                totalPalety += palety;

                sb.AppendLine($"  - {item.Field<string>("Kod")}{e2Marker}: {item.Field<decimal>("Ilosc"):N0} kg " +
                            $"({pojemniki:N0} poj., {palety:N1} pal.)");
            }

            decimal totalKg = orderedItems.Sum(r => r.Field<decimal>("Ilosc"));
            sb.AppendLine($"\n[SUM] Podsumowanie:");
            sb.AppendLine($"  [kg] Lacznie: {totalKg:N0} kg");
            sb.AppendLine($"  [#] Pojemnikow: {totalPojemniki:N0}");
            sb.AppendLine($"  [P] Palet: {totalPalety:N1}");

            return sb.ToString();
        }

        private async Task SaveOrderAsync()
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var tr = (SqlTransaction)await cn.BeginTransactionAsync();

            int orderId;

            decimal sumaPojemnikow = 0;
            decimal sumaPalet = 0;
            bool czyJakikolwiekE2 = false;

            foreach (DataRow r in _dt.Rows)
            {
                if (r.Field<decimal>("Ilosc") > 0m)
                {
                    sumaPojemnikow += r.Field<decimal>("Pojemniki");
                    sumaPalet += r.Field<decimal>("Palety");
                    if (r.Field<bool>("E2")) czyJakikolwiekE2 = true;
                }
            }

            if (_idZamowieniaDoEdycji.HasValue)
            {
                orderId = _idZamowieniaDoEdycji.Value;
                var cmdUpdate = new SqlCommand(@"UPDATE [dbo].[ZamowieniaMieso] SET 
                    DataZamowienia = @dz, DataPrzyjazdu = @dp, KlientId = @kid, Uwagi = @uw, 
                    KtoMod = @km, KiedyMod = SYSDATETIME(),
                    LiczbaPojemnikow = @poj, LiczbaPalet = @pal, TrybE2 = @e2
                    WHERE Id=@id", cn, tr);
                cmdUpdate.Parameters.AddWithValue("@dz", dateTimePickerSprzedaz.Value.Date);
                var dataPrzyjazdu = dateTimePickerSprzedaz.Value.Date.Add(dateTimePickerGodzinaPrzyjazdu.Value.TimeOfDay);
                cmdUpdate.Parameters.AddWithValue("@dp", dataPrzyjazdu);
                cmdUpdate.Parameters.AddWithValue("@kid", _selectedKlientId!);
                cmdUpdate.Parameters.AddWithValue("@uw", string.IsNullOrWhiteSpace(textBoxUwagi.Text) ? DBNull.Value : textBoxUwagi.Text);
                cmdUpdate.Parameters.AddWithValue("@km", UserID);
                cmdUpdate.Parameters.AddWithValue("@id", orderId);
                cmdUpdate.Parameters.AddWithValue("@poj", (int)Math.Round(sumaPojemnikow));
                cmdUpdate.Parameters.AddWithValue("@pal", sumaPalet);
                cmdUpdate.Parameters.AddWithValue("@e2", czyJakikolwiekE2);
                await cmdUpdate.ExecuteNonQueryAsync();

                var cmdDelete = new SqlCommand(@"DELETE FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId=@id", cn, tr);
                cmdDelete.Parameters.AddWithValue("@id", orderId);
                await cmdDelete.ExecuteNonQueryAsync();
            }
            else
            {
                var cmdGetId = new SqlCommand(@"SELECT ISNULL(MAX(Id),0)+1 FROM [dbo].[ZamowieniaMieso]", cn, tr);
                orderId = Convert.ToInt32(await cmdGetId.ExecuteScalarAsync());

                var cmdInsert = new SqlCommand(@"INSERT INTO [dbo].[ZamowieniaMieso] 
                    (Id, DataZamowienia, DataPrzyjazdu, KlientId, Uwagi, IdUser, DataUtworzenia, 
                     LiczbaPojemnikow, LiczbaPalet, TrybE2, TransportStatus) 
                    VALUES (@id, @dz, @dp, @kid, @uw, @u, GETDATE(), @poj, @pal, @e2, 'Oczekuje')", cn, tr);
                cmdInsert.Parameters.AddWithValue("@id", orderId);
                cmdInsert.Parameters.AddWithValue("@dz", dateTimePickerSprzedaz.Value.Date);
                var dataPrzyjazdu = dateTimePickerSprzedaz.Value.Date.Add(dateTimePickerGodzinaPrzyjazdu.Value.TimeOfDay);
                cmdInsert.Parameters.AddWithValue("@dp", dataPrzyjazdu);
                cmdInsert.Parameters.AddWithValue("@kid", _selectedKlientId!);
                cmdInsert.Parameters.AddWithValue("@uw", string.IsNullOrWhiteSpace(textBoxUwagi.Text) ? DBNull.Value : textBoxUwagi.Text);
                cmdInsert.Parameters.AddWithValue("@u", UserID);
                cmdInsert.Parameters.AddWithValue("@poj", (int)Math.Round(sumaPojemnikow));
                cmdInsert.Parameters.AddWithValue("@pal", sumaPalet);
                cmdInsert.Parameters.AddWithValue("@e2", czyJakikolwiekE2);
                await cmdInsert.ExecuteNonQueryAsync();
            }

            var cmdInsertItem = new SqlCommand(@"INSERT INTO [dbo].[ZamowieniaMiesoTowar] 
                (ZamowienieId, KodTowaru, Ilosc, Cena, Pojemniki, Palety, E2) 
                VALUES (@zid, @kt, @il, @ce, @poj, @pal, @e2)", cn, tr);
            cmdInsertItem.Parameters.Add("@zid", SqlDbType.Int);
            cmdInsertItem.Parameters.Add("@kt", SqlDbType.Int);
            cmdInsertItem.Parameters.Add("@il", SqlDbType.Decimal);
            cmdInsertItem.Parameters.Add("@ce", SqlDbType.Decimal);
            cmdInsertItem.Parameters.Add("@poj", SqlDbType.Int);
            cmdInsertItem.Parameters.Add("@pal", SqlDbType.Decimal);
            cmdInsertItem.Parameters.Add("@e2", SqlDbType.Bit);

            foreach (DataRow r in _dt.Rows)
            {
                if (r.Field<decimal>("Ilosc") <= 0m) continue;

                decimal palety = r.Field<decimal>("Palety");
                decimal pojemniki = r.Field<decimal>("Pojemniki");
                bool e2 = r.Field<bool>("E2");

                cmdInsertItem.Parameters["@zid"].Value = orderId;
                cmdInsertItem.Parameters["@kt"].Value = r.Field<int>("Id");
                cmdInsertItem.Parameters["@il"].Value = r.Field<decimal>("Ilosc");
                cmdInsertItem.Parameters["@ce"].Value = 0m;
                cmdInsertItem.Parameters["@poj"].Value = (int)Math.Round(pojemniki);
                cmdInsertItem.Parameters["@pal"].Value = palety;
                cmdInsertItem.Parameters["@e2"].Value = e2;
                await cmdInsertItem.ExecuteNonQueryAsync();
            }

            await tr.CommitAsync();
        }

        #endregion
    }

    public static class ColorExtensions
    {
        public static Color WithAlpha(this Color color, int alpha)
        {
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }
    }
}