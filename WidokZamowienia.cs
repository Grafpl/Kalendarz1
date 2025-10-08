// Plik: WidokZamowienia.cs
// WERSJA 18.1 - Poprawki DataProdukcji
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
        public string UserID { get; set; } = string.Empty;
        private int? _idZamowieniaDoEdycji;
        private readonly string _connLibra;
        private readonly string _connHandel;

        private const decimal POJEMNIKOW_NA_PALECIE = 36m;
        private const decimal POJEMNIKOW_NA_PALECIE_E2 = 40m;
        private const decimal KG_NA_POJEMNIKU = 15m;
        private const decimal KG_NA_POJEMNIKU_SPECJALNY = 10m;
        private const decimal KG_NA_PALECIE = POJEMNIKOW_NA_PALECIE * KG_NA_POJEMNIKU;
        private const decimal KG_NA_PALECIE_E2 = POJEMNIKOW_NA_PALECIE_E2 * KG_NA_POJEMNIKU;
        private const decimal LIMIT_PALET_OSTRZEZENIE = 33m;

        private string? _selectedKlientId;
        private bool _blokujObslugeZmian;
        private readonly CultureInfo _pl = new("pl-PL");
        private readonly Dictionary<string, Image> _headerIcons = new();
        private RadioButton? rbSwiezy;
        private RadioButton? rbMrozony;
        private string _aktywnyKatalog = "67095";

        private sealed class KontrahentInfo
        {
            public string Id { get; set; } = "";
            public string Nazwa { get; set; } = "";
            public string KodPocztowy { get; set; } = "";
            public string Miejscowosc { get; set; } = "";
            public string NIP { get; set; } = "";
            public string Handlowiec { get; set; } = "";
            public DateTime? OstatnieZamowienie { get; set; }
        }

        private readonly DataTable _dt = new();
        private DataView _view = default!;
        private readonly List<KontrahentInfo> _kontrahenci = new();
        private readonly Dictionary<string, DateTime> _ostatnieZamowienia = new();

        private Panel? panelSummary;
        private Label? lblSumaPalet;
        private Label? lblSumaPojemnikow;
        private Label? lblSumaKg;

        private Panel? panelTransport;
        private ProgressBar? progressSolowka;
        private ProgressBar? progressTir;
        private Label? lblSolowkaInfo;
        private Label? lblTirInfo;

        private FlowLayoutPanel? panelSugestieGodzin;
        private Label? lblHistoriaZamowien;

        private Panel? panelPlatnosci;
        private Label? lblLimitInfo;
        private ProgressBar? progressLimit;
        private decimal _limitKredytowy = 0;
        private decimal _doZaplacenia = 0;

        private DateTimePicker? dateTimePickerProdukcji;

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

        private async void WidokZamowienia_Load(object? sender, EventArgs e)
        {
            ApplyModernUIStyles();
            CreateHeaderIcons();
            SzybkiGrid();
            WireShortcuts();
            BuildDataTableSchema();
            InitDefaults();
            CreateSummaryPanel();
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
                    lblTytul.Text = "✏️ Edycja zamówienia";
                    btnZapisz.Text = "💾 Zapisz zmiany";
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

        #region Responsywny Layout

        private void ConfigureResponsiveLayout()
        {
            this.MinimumSize = new Size(1024, 600);

            ReorganizeHeaderPanel();

            if (panelDetaleZamowienia != null)
            {
                panelDetaleZamowienia.AutoScroll = true;
                panelDetaleZamowienia.Padding = new Padding(10, 10, 15, 10);
                ReorganizeDetailsPanel();
            }

            if (panelOstatniOdbiorcy != null)
            {
                panelOstatniOdbiorcy.Height = 190;
                panelOstatniOdbiorcy.Padding = new Padding(15, 10, 15, 10);
            }
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
            panelOdbiorca.Height = 95;

            var oldHandlowiecLabel = panelOdbiorca.Controls["label4"];
            if (oldHandlowiecLabel != null)
            {
                panelOdbiorca.Controls.Remove(oldHandlowiecLabel);
            }

            if (lblTytul != null)
            {
                lblTytul.Location = new Point(10, 10);
                lblTytul.Size = new Size(300, 30);
            }

            var lblOdbiorca = panelOdbiorca.Controls["label1"];
            if (lblOdbiorca != null)
            {
                lblOdbiorca.Text = "👤 Odbiorca:";
                lblOdbiorca.Location = new Point(10, 45);
                lblOdbiorca.Size = new Size(80, 16);
            }

            var existingHandlowiecLabel = panelOdbiorca.Controls.OfType<Label>()
                .FirstOrDefault(l => l.Text.Contains("Handlowiec"));

            if (existingHandlowiecLabel == null)
            {
                var lblHandlowiec = new Label
                {
                    Text = "👔 Handlowiec:",
                    Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                    ForeColor = Color.FromArgb(107, 114, 128),
                    Location = new Point(120, 45),
                    Size = new Size(90, 16)
                };
                panelOdbiorca.Controls.Add(lblHandlowiec);
            }
            else
            {
                existingHandlowiecLabel.Text = "👔 Handlowiec:";
                existingHandlowiecLabel.Location = new Point(120, 45);
                existingHandlowiecLabel.Size = new Size(90, 16);
            }

            if (txtSzukajOdbiorcy != null)
            {
                txtSzukajOdbiorcy.Location = new Point(10, 63);
                txtSzukajOdbiorcy.Size = new Size(100, 28);
                txtSzukajOdbiorcy.Font = new Font("Segoe UI", 10f);
            }

            if (cbHandlowiecFilter != null)
            {
                cbHandlowiecFilter.Location = new Point(120, 63);
                cbHandlowiecFilter.Size = new Size(100, 28);
                cbHandlowiecFilter.Font = new Font("Segoe UI", 10f);
            }

            CreateCompactRadioPanel();
        }

        private void CreateCompactRadioPanel()
        {
            if (panelOdbiorca == null) return;

            var oldRadioContainer = panelOdbiorca.Controls.OfType<Panel>()
                .FirstOrDefault(p => p.Controls.OfType<RadioButton>().Any());
            if (oldRadioContainer != null)
            {
                panelOdbiorca.Controls.Remove(oldRadioContainer);
            }

            var rbContainer = new Panel
            {
                Location = new Point(230, 58),
                Size = new Size(130, 32),
                BackColor = Color.FromArgb(249, 250, 251),
                BorderStyle = BorderStyle.None
            };

            rbSwiezy = new RadioButton
            {
                Text = "🥩 Świeże",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(31, 41, 55),
                Location = new Point(5, 7),
                Size = new Size(60, 18),
                Checked = true,
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat
            };

            rbMrozony = new RadioButton
            {
                Text = "❄️ Mrożone",
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = Color.FromArgb(31, 41, 55),
                Location = new Point(65, 7),
                Size = new Size(62, 18),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat
            };

            rbSwiezy.CheckedChanged += RbTypProduktu_CheckedChanged;
            rbMrozony.CheckedChanged += RbTypProduktu_CheckedChanged;

            rbContainer.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(209, 213, 219), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, rbContainer.Width - 1, rbContainer.Height - 1);
            };

            rbContainer.Controls.Add(rbSwiezy);
            rbContainer.Controls.Add(rbMrozony);
            panelOdbiorca.Controls.Add(rbContainer);
        }

        private void RbTypProduktu_CheckedChanged(object? sender, EventArgs e)
        {
            if (sender is RadioButton rb && !rb.Checked) return;

            string nowyKatalog = rbSwiezy?.Checked == true ? "67095" : "67153";

            if (nowyKatalog == _aktywnyKatalog) return;

            _aktywnyKatalog = nowyKatalog;
            _view.RowFilter = $"Katalog = '{_aktywnyKatalog}'";

            RecalcSum();
        }

        private void ReorganizeDetailsPanel()
        {
            if (panelDetaleZamowienia == null) return;

            panelDetaleZamowienia.SuspendLayout();
            panelDetaleZamowienia.Padding = new Padding(10, 10, 15, 10);

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoScroll = true,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(0)
            };

            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 280));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            if (panelOstatniOdbiorcy != null)
            {
                panelOstatniOdbiorcy.Dock = DockStyle.Fill;
                panelOstatniOdbiorcy.Margin = new Padding(0, 0, 0, 12);
                mainLayout.Controls.Add(panelOstatniOdbiorcy, 0, 0);
            }

            var transportPanel = CreateTransportPanel();
            transportPanel.Dock = DockStyle.Fill;
            transportPanel.Margin = new Padding(0, 0, 0, 12);
            mainLayout.Controls.Add(transportPanel, 0, 1);

            var platnosciPanel = CreatePlatnosciPanel();
            platnosciPanel.Dock = DockStyle.Fill;
            platnosciPanel.Margin = new Padding(0, 0, 0, 12);
            mainLayout.Controls.Add(platnosciPanel, 0, 2);

            var notesPanel = CreateNotesPanel();
            notesPanel.Dock = DockStyle.Top;
            mainLayout.Controls.Add(notesPanel, 0, 3);

            panelDetaleZamowienia.Controls.Clear();
            panelDetaleZamowienia.Controls.Add(mainLayout);
            panelDetaleZamowienia.Controls.Add(listaWynikowOdbiorcy);

            panelDetaleZamowienia.ResumeLayout(true);
        }

        private Panel CreateNotesPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                Padding = new Padding(12),
                BackColor = Color.White,
                Height = 80,
                AutoSize = false
            };

            var lblNotes = new Label
            {
                Text = "📝 NOTATKA",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(217, 119, 6),
                Location = new Point(12, 8),
                AutoSize = true
            };

            if (textBoxUwagi != null)
            {
                if (textBoxUwagi.Parent != null)
                {
                    textBoxUwagi.Parent.Controls.Remove(textBoxUwagi);
                }

                textBoxUwagi.Location = new Point(10, 30);
                textBoxUwagi.Size = new Size(panel.Width - 22, 42);
                textBoxUwagi.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                textBoxUwagi.Multiline = true;
                textBoxUwagi.ScrollBars = ScrollBars.Vertical;
                textBoxUwagi.Font = new Font("Segoe UI", 9f);

                panel.Controls.Add(textBoxUwagi);
            }

            panel.Controls.Add(lblNotes);

            panel.Paint += (s, e) =>
            {
                using var path = GetRoundedRectPath(panel.ClientRectangle, 8);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(Color.FromArgb(255, 251, 235));
                e.Graphics.FillPath(brush, path);
                using var pen = new Pen(Color.FromArgb(253, 230, 138), 1);
                e.Graphics.DrawPath(pen, path);
            };

            return panel;
        }

        private Panel CreateTransportPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                BackColor = Color.White,
                Padding = new Padding(12),
                Height = 280
            };

            var lblHeader = new Label
            {
                Text = "🚚 TRANSPORT",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(59, 130, 246),
                Location = new Point(12, 10),
                AutoSize = true
            };

            var datesLayout = new TableLayoutPanel
            {
                Location = new Point(10, 35),
                Size = new Size(390, 155),
                ColumnCount = 2,
                RowCount = 4
            };

            datesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            datesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            datesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            datesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            datesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            datesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            // Data Produkcji
            var lblProdukcja = new Label
            {
                Text = "🏭 Data produkcji",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(31, 41, 55),
                AutoSize = true,
                Padding = new Padding(0, 0, 0, 3)
            };

            dateTimePickerProdukcji = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd (dddd)",
                Font = new Font("Segoe UI", 10.5f),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 0, 5, 5),
                Value = DateTime.Today
            };
            StyleDateTimePicker(dateTimePickerProdukcji);



            var lblDate = new Label
            {
                Text = "📅 Data odbioru",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(31, 41, 55),
                AutoSize = true,
                Padding = new Padding(0, 0, 0, 3)
            };

            var lblTime = new Label
            {
                Text = "🕐 Godzina przyjazdu",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(31, 41, 55),
                AutoSize = true,
                Padding = new Padding(5, 0, 0, 3)
            };

            if (dateTimePickerSprzedaz != null)
            {
                dateTimePickerSprzedaz.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                dateTimePickerSprzedaz.Margin = new Padding(0, 0, 5, 5);
            }

            if (dateTimePickerGodzinaPrzyjazdu != null)
            {
                dateTimePickerGodzinaPrzyjazdu.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                dateTimePickerGodzinaPrzyjazdu.Margin = new Padding(5, 0, 0, 5);
            }

            panelSugestieGodzin = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = false,
                Height = 45,
                Padding = new Padding(0, 2, 0, 0)
            };

            datesLayout.Controls.Add(lblProdukcja, 0, 0);
            datesLayout.Controls.Add(dateTimePickerProdukcji, 0, 1);
            datesLayout.SetColumnSpan(dateTimePickerProdukcji, 2);

            datesLayout.Controls.Add(lblDate, 0, 2);
            datesLayout.Controls.Add(lblTime, 1, 2);

            datesLayout.Controls.Add(dateTimePickerSprzedaz, 0, 3);
            datesLayout.Controls.Add(dateTimePickerGodzinaPrzyjazdu, 1, 3);

            panelTransport = new Panel
            {
                Location = new Point(10, 200),
                Size = new Size(390, 50),
                BackColor = Color.Transparent
            };

            var transportLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0)
            };

            transportLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            transportLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            var solowkaPanel = CreateBetterProgressPanel("🚐 Solówka 18 palet", 18, out progressSolowka, out lblSolowkaInfo);
            var tirPanel = CreateBetterProgressPanel("🚛 TIR 33 palety", 33, out progressTir, out lblTirInfo);

            transportLayout.Controls.Add(solowkaPanel, 0, 0);
            transportLayout.Controls.Add(tirPanel, 1, 0);
            panelTransport.Controls.Add(transportLayout);

            panel.Controls.Add(lblHeader);
            panel.Controls.Add(datesLayout);
            panel.Controls.Add(panelTransport);

            panel.Paint += (s, e) =>
            {
                using var path = GetRoundedRectPath(panel.ClientRectangle, 8);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(Color.FromArgb(239, 246, 255));
                e.Graphics.FillPath(brush, path);
                using var pen = new Pen(Color.FromArgb(191, 219, 254), 1);
                e.Graphics.DrawPath(pen, path);
            };

            return panel;
        }
        private Panel CreateBetterProgressPanel(string labelText, int maxValue, out ProgressBar progressBar, out Label infoLabel)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5, 0, 5, 0)
            };

            var label = new Label
            {
                Text = labelText,
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = Color.FromArgb(107, 114, 128),
                Location = new Point(8, 3),
                AutoSize = true
            };

            progressBar = new ProgressBar
            {
                Maximum = maxValue,
                Style = ProgressBarStyle.Continuous,
                Location = new Point(8, 22),
                Size = new Size(panel.Width - 65, 16),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            infoLabel = new Label
            {
                Text = $"0/{maxValue}",
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = Color.FromArgb(31, 41, 55),
                Location = new Point(panel.Width - 52, 22),
                Size = new Size(48, 16),
                TextAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            panel.Controls.Add(label);
            panel.Controls.Add(progressBar);
            panel.Controls.Add(infoLabel);

            return panel;
        }

        private Panel CreatePlatnosciPanel()
        {
            panelPlatnosci = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(12),
                Height = 100
            };

            var lblHeader = new Label
            {
                Text = "💰 PŁATNOŚCI",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(16, 185, 129),
                Location = new Point(12, 10),
                AutoSize = true
            };

            lblLimitInfo = new Label
            {
                Text = "Wybierz odbiorcę aby zobaczyć informacje o płatnościach",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(107, 114, 128),
                Location = new Point(12, 35),
                Size = new Size(380, 18)
            };

            progressLimit = new ProgressBar
            {
                Location = new Point(12, 58),
                Size = new Size(250, 24),
                Maximum = 100,
                Style = ProgressBarStyle.Continuous
            };

            panelPlatnosci.Controls.Add(lblHeader);
            panelPlatnosci.Controls.Add(lblLimitInfo);
            panelPlatnosci.Controls.Add(progressLimit);

            panelPlatnosci.Paint += (s, e) =>
            {
                using var path = GetRoundedRectPath(panelPlatnosci.ClientRectangle, 8);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(Color.FromArgb(240, 253, 244));
                e.Graphics.FillPath(brush, path);
                using var pen = new Pen(Color.FromArgb(167, 243, 208), 1);
                e.Graphics.DrawPath(pen, path);
            };

            return panelPlatnosci;
        }

        private async Task LoadPlatnosciOdbiorcy(string klientId)
        {
            if (panelPlatnosci == null || lblLimitInfo == null || progressLimit == null) return;

            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                var cmdLimit = new SqlCommand(@"
            SELECT LimitAmount 
            FROM [HANDEL].[SSCommon].[STContractors] 
            WHERE id = @KlientId", cn);
                cmdLimit.Parameters.AddWithValue("@KlientId", int.Parse(klientId));
                var limitObj = await cmdLimit.ExecuteScalarAsync();
                _limitKredytowy = limitObj != DBNull.Value ? Convert.ToDecimal(limitObj) : 0;

                var cmdZadluzenie = new SqlCommand(@"
            WITH PNAgg AS (
                SELECT PN.dkid, SUM(ISNULL(PN.kwotarozl,0)) AS KwotaRozliczona
                FROM [HANDEL].[HM].[PN] PN
                GROUP BY PN.dkid
            )
            SELECT SUM(DK.walbrutto - ISNULL(PA.KwotaRozliczona, 0)) AS DoZaplacenia
            FROM [HANDEL].[HM].[DK] DK
            LEFT JOIN PNAgg PA ON PA.dkid = DK.id
            WHERE DK.khid = @KlientId 
              AND DK.anulowany = 0
              AND (DK.walbrutto - ISNULL(PA.KwotaRozliczona, 0)) > 0", cn);
                cmdZadluzenie.Parameters.AddWithValue("@KlientId", int.Parse(klientId));
                var zadluzenieObj = await cmdZadluzenie.ExecuteScalarAsync();
                _doZaplacenia = zadluzenieObj != DBNull.Value ? Convert.ToDecimal(zadluzenieObj) : 0;

                lblLimitInfo.Location = new Point(270, 58);
                lblLimitInfo.Size = new Size(120, 24);
                lblLimitInfo.TextAlign = ContentAlignment.MiddleLeft;

                if (_limitKredytowy > 0)
                {
                    decimal procentWykorzystania = (_doZaplacenia / _limitKredytowy) * 100;
                    progressLimit.Value = Math.Min((int)procentWykorzystania, 100);

                    lblLimitInfo.Text = $"{_doZaplacenia:N0} / {_limitKredytowy:N0} zł";

                    decimal dostepny = _limitKredytowy - _doZaplacenia;

                    if (dostepny < 0)
                    {
                        lblLimitInfo.ForeColor = Color.FromArgb(220, 38, 38);
                        lblLimitInfo.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                        progressLimit.ForeColor = Color.FromArgb(220, 38, 38);
                        panelPlatnosci.BackColor = Color.FromArgb(254, 242, 242);
                    }
                    else if (procentWykorzystania > 80)
                    {
                        lblLimitInfo.ForeColor = Color.FromArgb(217, 119, 6);
                        lblLimitInfo.Font = new Font("Segoe UI", 8.5f);
                        progressLimit.ForeColor = Color.FromArgb(251, 191, 36);
                        panelPlatnosci.BackColor = Color.FromArgb(255, 251, 235);
                    }
                    else
                    {
                        lblLimitInfo.ForeColor = Color.FromArgb(22, 163, 74);
                        lblLimitInfo.Font = new Font("Segoe UI", 8.5f);
                        progressLimit.ForeColor = Color.FromArgb(34, 197, 94);
                        panelPlatnosci.BackColor = Color.White;
                    }
                }
                else
                {
                    lblLimitInfo.Text = $"{_doZaplacenia:N0} / brak";
                    lblLimitInfo.ForeColor = Color.FromArgb(107, 114, 128);
                    progressLimit.Value = 0;
                    panelPlatnosci.BackColor = Color.White;
                }
            }
            catch
            {
                lblLimitInfo.Text = "Błąd danych";
                lblLimitInfo.ForeColor = Color.FromArgb(107, 114, 128);
                progressLimit.Value = 0;
            }
        }

        #endregion

        #region Grid Ostatnich Odbiorców

        private void SetupOstatniOdbiorcyGrid()
        {
            if (panelOstatniOdbiorcy == null || gridOstatniOdbiorcy == null) return;

            panelOstatniOdbiorcy.AutoSize = false;
            panelOstatniOdbiorcy.Height = 280;

            if (lblOstatniOdbiorcy != null)
            {
                lblOstatniOdbiorcy.Location = new Point(15, 10);
                lblOstatniOdbiorcy.Size = new Size(800, 20);
            }

            gridOstatniOdbiorcy.Location = new Point(15, 35);
            gridOstatniOdbiorcy.Size = new Size(panelOstatniOdbiorcy.Width - 30, 235);
            gridOstatniOdbiorcy.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            gridOstatniOdbiorcy.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridOstatniOdbiorcy.ReadOnly = true;
            gridOstatniOdbiorcy.AllowUserToResizeRows = false;
            gridOstatniOdbiorcy.AllowUserToResizeColumns = false;

            panelOstatniOdbiorcy.Paint += (s, e) =>
            {
                using var path = GetRoundedRectPath(panelOstatniOdbiorcy.ClientRectangle, 8);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(Color.FromArgb(249, 250, 251));
                e.Graphics.FillPath(brush, path);
                using var pen = new Pen(Color.FromArgb(229, 231, 235), 1);
                e.Graphics.DrawPath(pen, path);
            };

            gridOstatniOdbiorcy.DefaultCellStyle.SelectionBackColor = Color.FromArgb(99, 102, 241);
            gridOstatniOdbiorcy.DefaultCellStyle.SelectionForeColor = Color.White;
            gridOstatniOdbiorcy.DefaultCellStyle.Padding = new Padding(8, 3, 8, 3);
            gridOstatniOdbiorcy.RowTemplate.Height = 28;
            gridOstatniOdbiorcy.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);

            gridOstatniOdbiorcy.CellClick -= GridOstatniOdbiorcy_CellClick;
            gridOstatniOdbiorcy.CellMouseEnter -= GridOstatniOdbiorcy_CellMouseEnter;
            gridOstatniOdbiorcy.CellMouseLeave -= GridOstatniOdbiorcy_CellMouseLeave;

            gridOstatniOdbiorcy.CellClick += GridOstatniOdbiorcy_CellClick;
            gridOstatniOdbiorcy.CellMouseEnter += GridOstatniOdbiorcy_CellMouseEnter;
            gridOstatniOdbiorcy.CellMouseLeave += GridOstatniOdbiorcy_CellMouseLeave;
        }

        private void GridOstatniOdbiorcy_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                var value = gridOstatniOdbiorcy.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    SelectOdbiorcaFromCell(value);
                }
            }
        }

        private void GridOstatniOdbiorcy_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                gridOstatniOdbiorcy.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.FromArgb(238, 242, 255);
            }
        }

        private void GridOstatniOdbiorcy_CellMouseLeave(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                var row = gridOstatniOdbiorcy.Rows[e.RowIndex];
                row.Cells[e.ColumnIndex].Style.BackColor =
                    e.RowIndex % 2 == 0 ? Color.White : Color.FromArgb(249, 250, 251);
            }
        }

        private void SelectOdbiorcaFromCell(string nazwaOdbiorcy)
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
                        r["Folia"] = false;
                        r["Ilosc"] = 0m;
                        r["Pojemniki"] = 0m;
                        r["Palety"] = 0m;
                    }
                    _blokujObslugeZmian = false;
                    textBoxUwagi.Text = "";
                }

                UstawOdbiorce(odbiorca.Id);
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
                lblOstatniOdbiorcy.Text = "👉 Wybierz handlowca aby zobaczyć odbiorców";
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
                lblOstatniOdbiorcy.Text = $"❌ Brak odbiorców dla: {handlowiec}";
                gridOstatniOdbiorcy.DataSource = null;
                return;
            }

            var dt = new DataTable();
            dt.Columns.Add("Kolumna1", typeof(string));
            dt.Columns.Add("Kolumna2", typeof(string));
            dt.Columns.Add("Kolumna3", typeof(string));
            dt.Columns.Add("Kolumna4", typeof(string));

            for (int i = 0; i < odbiorcy.Count; i += 4)
            {
                var row = dt.NewRow();
                row["Kolumna1"] = odbiorcy[i];
                row["Kolumna2"] = (i + 1 < odbiorcy.Count) ? odbiorcy[i + 1] : "";
                row["Kolumna3"] = (i + 2 < odbiorcy.Count) ? odbiorcy[i + 2] : "";
                row["Kolumna4"] = (i + 3 < odbiorcy.Count) ? odbiorcy[i + 3] : "";
                dt.Rows.Add(row);
            }

            gridOstatniOdbiorcy.DataSource = dt;

            if (gridOstatniOdbiorcy.Columns.Count > 0)
            {
                gridOstatniOdbiorcy.Columns["Kolumna1"].FillWeight = 25;
                gridOstatniOdbiorcy.Columns["Kolumna2"].FillWeight = 25;
                gridOstatniOdbiorcy.Columns["Kolumna3"].FillWeight = 25;
                gridOstatniOdbiorcy.Columns["Kolumna4"].FillWeight = 25;
            }

            foreach (DataGridViewRow row in gridOstatniOdbiorcy.Rows)
            {
                for (int col = 0; col < 4; col++)
                {
                    var nazwa = row.Cells[col].Value?.ToString();
                    if (!string.IsNullOrEmpty(nazwa))
                    {
                        var kontrahent = _kontrahenci.FirstOrDefault(k => k.Nazwa == nazwa);
                        if (kontrahent?.OstatnieZamowienie != null &&
                            kontrahent.OstatnieZamowienie >= DateTime.Now.AddMonths(-1))
                        {
                            row.Cells[col].Style.Font = new Font(gridOstatniOdbiorcy.Font, FontStyle.Bold);
                            row.Cells[col].Style.ForeColor = Color.FromArgb(16, 185, 129);
                        }
                    }
                }
            }

            lblOstatniOdbiorcy.Text = $"👥 Odbiorcy ({odbiorcy.Count}) - kliknij aby wybrać:";
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

        #region Informacje o kliencie

        private async Task LoadPreferredHours(string klientId)
        {
            if (panelSugestieGodzin == null) return;

            panelSugestieGodzin.Controls.Clear();

            const string sql = @"
                SELECT TOP 5 CONVERT(VARCHAR(5), DataPrzyjazdu, 108) as Godzina, COUNT(*) as Ilosc
                FROM [dbo].[ZamowieniaMieso]
                WHERE KlientId = @KlientId 
                  AND DataZamowienia >= DATEADD(MONTH, -6, GETDATE())
                GROUP BY CONVERT(VARCHAR(5), DataPrzyjazdu, 108)
                ORDER BY COUNT(*) DESC";

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@KlientId", int.Parse(klientId));
                await using var rd = await cmd.ExecuteReaderAsync();

                var godziny = new List<string>();
                while (await rd.ReadAsync())
                {
                    godziny.Add(rd.GetString(0));
                }

                if (godziny.Any())
                {
                    var lblInfo = new Label
                    {
                        Text = "⭐ Preferowane:",
                        Font = new Font("Segoe UI", 7.5f),
                        ForeColor = Color.FromArgb(107, 114, 128),
                        AutoSize = true,
                        Padding = new Padding(0, 3, 5, 0)
                    };
                    panelSugestieGodzin.Controls.Add(lblInfo);

                    foreach (var godz in godziny)
                    {
                        var btn = new Button
                        {
                            Text = godz,
                            Size = new Size(42, 20),
                            Font = new Font("Segoe UI", 7.5f),
                            FlatStyle = FlatStyle.Flat,
                            BackColor = Color.FromArgb(219, 234, 254),
                            ForeColor = Color.FromArgb(30, 64, 175),
                            Cursor = Cursors.Hand,
                            Margin = new Padding(2, 0, 2, 0)
                        };
                        btn.FlatAppearance.BorderSize = 0;
                        btn.Click += (s, e) =>
                        {
                            if (TimeSpan.TryParse(godz, out var time))
                            {
                                dateTimePickerGodzinaPrzyjazdu.Value = DateTime.Today.Add(time);
                            }
                        };
                        panelSugestieGodzin.Controls.Add(btn);
                    }
                }
            }
            catch { }
        }

        private async Task LoadHistoriaZamowien(string klientId)
        {
            if (panelDaneOdbiorcy == null) return;

            const string sql = @"
                SELECT TOP 6 DataZamowienia, LiczbaPojemnikow, LiczbaPalet
                FROM [dbo].[ZamowieniaMieso]
                WHERE KlientId = @KlientId
                ORDER BY DataZamowienia DESC";

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@KlientId", int.Parse(klientId));
                await using var rd = await cmd.ExecuteReaderAsync();

                var oldPanel = panelDaneOdbiorcy.Controls["panelHistoria"];
                if (oldPanel != null)
                {
                    panelDaneOdbiorcy.Controls.Remove(oldPanel);
                }

                var historiaPanel = new Panel
                {
                    Name = "panelHistoria",
                    Location = new Point(10, 70),
                    Size = new Size(panelDaneOdbiorcy.Width - 20, 120),
                    BackColor = Color.FromArgb(249, 250, 251),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };

                historiaPanel.Paint += (s, e) =>
                {
                    using var path = GetRoundedRectPath(historiaPanel.ClientRectangle, 6);
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var pen = new Pen(Color.FromArgb(229, 231, 235), 1);
                    e.Graphics.DrawPath(pen, path);
                };

                var lblTitle = new Label
                {
                    Text = "📋 OSTATNIE ZAMÓWIENIA",
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(75, 85, 99),
                    Location = new Point(10, 8),
                    AutoSize = true
                };
                historiaPanel.Controls.Add(lblTitle);

                int yPos = 30;
                int count = 0;
                while (await rd.ReadAsync() && count < 6)
                {
                    var data = rd.GetDateTime(0);
                    var poj = rd.GetInt32(1);
                    var pal = rd.GetDecimal(2);

                    string dzienTyg = data.ToString("ddd", _pl).Substring(0, 2).ToUpper();

                    var lblHistoria = new Label
                    {
                        Text = $"📦 {data:dd.MM} ({dzienTyg})  •  {poj} poj.  •  {pal:N1} pal.",
                        Font = new Font("Segoe UI", 7.5f),
                        ForeColor = Color.FromArgb(107, 114, 128),
                        Location = new Point(15, yPos),
                        AutoSize = true
                    };
                    historiaPanel.Controls.Add(lblHistoria);
                    yPos += 14;
                    count++;
                }

                if (count == 0)
                {
                    var lblBrak = new Label
                    {
                        Text = "ℹ️ Brak ostatnich zamówień",
                        Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
                        ForeColor = Color.FromArgb(156, 163, 175),
                        Location = new Point(15, 30),
                        AutoSize = true
                    };
                    historiaPanel.Controls.Add(lblBrak);
                }

                panelDaneOdbiorcy.Controls.Add(historiaPanel);
            }
            catch { }
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
                SetProgressBarColor(progressSolowka, Color.FromArgb(34, 197, 94));
                lblSolowkaInfo.ForeColor = Color.FromArgb(34, 197, 94);
            }
            else
            {
                SetProgressBarColor(progressSolowka, Color.FromArgb(239, 68, 68));
                lblSolowkaInfo.ForeColor = Color.FromArgb(239, 68, 68);
                lblSolowkaInfo.Text = $"{paletyInt:N0} / 18 ⚠️";
            }

            progressTir.Value = Math.Min(paletyInt, 33);
            lblTirInfo!.Text = $"{paletyInt:N0} / 33";

            if (paletyInt <= 33)
            {
                SetProgressBarColor(progressTir, Color.FromArgb(34, 197, 94));
                lblTirInfo.ForeColor = Color.FromArgb(34, 197, 94);
            }
            else
            {
                SetProgressBarColor(progressTir, Color.FromArgb(239, 68, 68));
                lblTirInfo.ForeColor = Color.FromArgb(239, 68, 68);
                lblTirInfo.Text = $"{paletyInt:N0} / 33 ⚠️";
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
                Height = 60,
                BackColor = Color.FromArgb(17, 24, 39),
                Parent = dataGridViewZamowienie.Parent
            };

            var flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(30, 15, 30, 15)
            };

            lblSumaPalet = CreateSummaryLabel("📦 PALETY", "0");
            lblSumaPojemnikow = CreateSummaryLabel("📥 POJEMNIKI", "0");
            lblSumaKg = CreateSummaryLabel("⚖️ KILOGRAMY", "0");

            flowPanel.Controls.Add(lblSumaPalet);
            flowPanel.Controls.Add(CreateSeparator());
            flowPanel.Controls.Add(lblSumaPojemnikow);
            flowPanel.Controls.Add(CreateSeparator());
            flowPanel.Controls.Add(lblSumaKg);

            panelSummary.Controls.Add(flowPanel);

            if (dataGridViewZamowienie != null)
            {
                dataGridViewZamowienie.Height -= 60;
            }
        }

        private Label CreateSummaryLabel(string title, string value)
        {
            var panel = new Panel
            {
                Width = 200,
                Height = 30,
                Margin = new Padding(15, 0, 15, 0)
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 8f, FontStyle.Regular),
                ForeColor = Color.FromArgb(156, 163, 175),
                Location = new Point(0, 0),
                AutoSize = true
            };

            var lblValue = new Label
            {
                Text = value,
                Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(0, 10),
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
                Height = 35,
                BackColor = Color.FromArgb(55, 65, 81),
                Margin = new Padding(0, 0, 0, 0)
            };
        }

        #endregion

        #region Inicjalizacja i Ustawienia UI

        private void ApplyModernUIStyles()
        {
            this.BackColor = Color.FromArgb(249, 250, 251);

            if (lblTytul != null)
            {
                lblTytul.Text = "📋 Nowe zamówienie";
                lblTytul.Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold);
                lblTytul.ForeColor = Color.FromArgb(17, 24, 39);
            }

            if (btnZapisz != null)
            {
                btnZapisz.Text = "💾 Zapisz";
                StyleButton(btnZapisz, Color.FromArgb(99, 102, 241), Color.White);
            }

            if (btnAnuluj != null)
            {
                btnAnuluj.Text = "❌ Anuluj";
            }

            if (panelDaneOdbiorcy != null)
            {
                panelDaneOdbiorcy.BackColor = Color.White;
                panelDaneOdbiorcy.BorderStyle = BorderStyle.None;
                panelDaneOdbiorcy.Paint += ModernPanel_Paint;
            }

            if (panelMaster != null)
            {
                panelMaster.BackColor = Color.White;
                panelMaster.Padding = new Padding(0);
            }

            if (panelOdbiorca != null)
            {
                panelOdbiorca.BackColor = Color.White;
                panelOdbiorca.Padding = new Padding(20, 15, 20, 10);
            }

            if (panelDetaleZamowienia != null)
            {
                panelDetaleZamowienia.BackColor = Color.White;
                panelDetaleZamowienia.AutoScroll = true;
            }

            StyleDateTimePicker(dateTimePickerSprzedaz);
            StyleDateTimePicker(dateTimePickerGodzinaPrzyjazdu);
            StyleTextBox(txtSzukajOdbiorcy);
            StyleTextBox(textBoxUwagi);
            StyleComboBox(cbHandlowiecFilter);

            if (summaryLabelPalety != null) summaryLabelPalety.Visible = false;
            if (summaryLabelPojemniki != null) summaryLabelPojemniki.Visible = false;

            foreach (Control c in panelDetaleZamowienia.Controls)
            {
                if (c is Label lbl && lbl.Font.Bold)
                {
                    lbl.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                    lbl.ForeColor = Color.FromArgb(107, 114, 128);
                }
            }

            foreach (Control c in panelOdbiorca.Controls)
            {
                if (c is Label lbl && lbl != lblTytul && lbl.Font.Bold)
                {
                    lbl.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                    lbl.ForeColor = Color.FromArgb(107, 114, 128);
                }
            }

            if (lblWybranyOdbiorca != null && !lblWybranyOdbiorca.Text.Contains("👤"))
            {
                lblWybranyOdbiorca.Text = "👤 " + lblWybranyOdbiorca.Text;
            }

            if (lblNip != null && !lblNip.Text.Contains("💳"))
            {
                lblNip.Text = "💳 " + lblNip.Text;
            }

            if (lblAdres != null && !lblAdres.Text.Contains("📍"))
            {
                lblAdres.Text = "📍 " + lblAdres.Text;
            }

            if (lblHandlowiec != null && !lblHandlowiec.Text.Contains("👔"))
            {
                lblHandlowiec.Text = "👔 " + lblHandlowiec.Text;
            }
        }

        private void StyleButton(Button? btn, Color bgColor, Color fgColor)
        {
            if (btn == null) return;

            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = bgColor;
            btn.ForeColor = fgColor;
            btn.Font = new Font("Segoe UI Semibold", 10f);
            btn.Cursor = Cursors.Hand;
            btn.Height = 38;
            btn.Width = 140;

            btn.Paint += (s, e) =>
            {
                var rect = btn.ClientRectangle;
                using var path = GetRoundedRectPath(rect, 6);
                btn.Region = new Region(path);
            };

            btn.MouseEnter += (s, e) => btn.BackColor = ControlPaint.Dark(bgColor, 0.1f);
            btn.MouseLeave += (s, e) => btn.BackColor = bgColor;
        }

        private void StyleDateTimePicker(DateTimePicker? dtp)
        {
            if (dtp == null) return;
            dtp.Font = new Font("Segoe UI", 9.5f);
            dtp.CalendarTitleBackColor = Color.FromArgb(99, 102, 241);
            dtp.CalendarTitleForeColor = Color.White;
        }

        private void StyleTextBox(TextBox? tb)
        {
            if (tb == null) return;
            tb.Font = new Font("Segoe UI", 9.5f);
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.BackColor = Color.FromArgb(249, 250, 251);
        }

        private void StyleComboBox(ComboBox? cb)
        {
            if (cb == null) return;
            cb.Font = new Font("Segoe UI", 9.5f);
            cb.BackColor = Color.FromArgb(249, 250, 251);
            cb.FlatStyle = FlatStyle.Flat;
        }

        private void ModernPanel_Paint(object? sender, PaintEventArgs e)
        {
            var panel = sender as Panel;
            if (panel == null) return;

            using var path = GetRoundedRectPath(panel.ClientRectangle, 8);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.FromArgb(229, 231, 235), 1);
            e.Graphics.DrawPath(pen, path);
        }

        private void SzybkiGrid()
        {
            dataGridViewZamowienie.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewZamowienie.AllowUserToAddRows = false;
            dataGridViewZamowienie.AllowUserToDeleteRows = false;
            dataGridViewZamowienie.AllowUserToResizeRows = false;
            dataGridViewZamowienie.RowHeadersVisible = false;
            dataGridViewZamowienie.SelectionMode = DataGridViewSelectionMode.CellSelect;
            dataGridViewZamowienie.MultiSelect = true;
            dataGridViewZamowienie.EditMode = DataGridViewEditMode.EditOnKeystroke;
            dataGridViewZamowienie.BackgroundColor = Color.White;
            dataGridViewZamowienie.BorderStyle = BorderStyle.None;
            dataGridViewZamowienie.GridColor = Color.FromArgb(243, 244, 246);
            dataGridViewZamowienie.Font = new Font("Segoe UI", 9.5f);

            dataGridViewZamowienie.EnableHeadersVisualStyles = false;
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f);
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(75, 85, 99);
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(249, 250, 251);
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(75, 85, 99);
            dataGridViewZamowienie.ColumnHeadersHeight = 38;
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewZamowienie.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;

            dataGridViewZamowienie.RowTemplate.Height = 28;
            dataGridViewZamowienie.DefaultCellStyle.SelectionBackColor = Color.FromArgb(238, 242, 255);
            dataGridViewZamowienie.DefaultCellStyle.SelectionForeColor = Color.FromArgb(55, 65, 81);
            dataGridViewZamowienie.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
            dataGridViewZamowienie.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;

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

            if (dateTimePickerProdukcji != null)
            {
                dateTimePickerProdukcji.Value = DateTime.Today;
            }

            dateTimePickerSprzedaz.Value = (dzis.DayOfWeek == DayOfWeek.Friday) ? dzis.AddDays(3) : dzis.AddDays(1);
            dateTimePickerGodzinaPrzyjazdu.Value = DateTime.Today.AddHours(8);
            RecalcSum();
        }

        private void BuildDataTableSchema()
        {
            _dt.Columns.Add("Id", typeof(int));
            _dt.Columns.Add("Kod", typeof(string));
            _dt.Columns.Add("Katalog", typeof(string));
            _dt.Columns.Add("E2", typeof(bool));
            _dt.Columns.Add("Folia", typeof(bool));
            _dt.Columns.Add("Palety", typeof(decimal));
            _dt.Columns.Add("Pojemniki", typeof(decimal));
            _dt.Columns.Add("Ilosc", typeof(decimal));
            _dt.Columns.Add("KodTowaru", typeof(string));
            _dt.Columns.Add("KodKopia", typeof(string));

            _view = new DataView(_dt);
            _view.RowFilter = $"Katalog = '{_aktywnyKatalog}'";
            dataGridViewZamowienie.DataSource = _view;

            dataGridViewZamowienie.Columns["Id"]!.Visible = false;
            dataGridViewZamowienie.Columns["KodTowaru"]!.Visible = false;
            dataGridViewZamowienie.Columns["Katalog"]!.Visible = false;

            var cKod = dataGridViewZamowienie.Columns["Kod"]!;
            cKod.ReadOnly = true;
            cKod.FillWeight = 165;
            cKod.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            cKod.DefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f);
            cKod.DefaultCellStyle.ForeColor = Color.FromArgb(31, 41, 55);
            cKod.HeaderText = "🥩 Towar";

            var cE2 = dataGridViewZamowienie.Columns["E2"] as DataGridViewCheckBoxColumn;
            if (cE2 != null)
            {
                cE2.HeaderText = "✅ 40 E2";
                cE2.FillWeight = 25;
                cE2.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                cE2.DefaultCellStyle.Padding = new Padding(3);
                cE2.DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242);
                cE2.ToolTipText = "40 pojemników/paletę";
            }

            var cFolia = dataGridViewZamowienie.Columns["Folia"] as DataGridViewCheckBoxColumn;
            if (cFolia != null)
            {
                cFolia.HeaderText = "📦 Folia";
                cFolia.FillWeight = 25;
                cFolia.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                cFolia.DefaultCellStyle.Padding = new Padding(3);
                cFolia.DefaultCellStyle.BackColor = Color.FromArgb(239, 246, 255);
                cFolia.ToolTipText = "Zapakowane w folię";
            }

            var cPalety = dataGridViewZamowienie.Columns["Palety"]!;
            cPalety.HeaderText = "📦 Palety";
            cPalety.FillWeight = 75;
            cPalety.DefaultCellStyle.Format = "N0";
            cPalety.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            cPalety.DefaultCellStyle.Font = new Font("Segoe UI Semibold", 10.5f);
            cPalety.DefaultCellStyle.ForeColor = Color.FromArgb(239, 68, 68);

            var cPojemniki = dataGridViewZamowienie.Columns["Pojemniki"]!;
            cPojemniki.HeaderText = "📥 Pojemniki";
            cPojemniki.FillWeight = 95;
            cPojemniki.DefaultCellStyle.Format = "N0";
            cPojemniki.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            cPojemniki.DefaultCellStyle.ForeColor = Color.FromArgb(59, 130, 246);

            var cIlosc = dataGridViewZamowienie.Columns["Ilosc"]!;
            cIlosc.FillWeight = 105;
            cIlosc.DefaultCellStyle.Format = "N0";
            cIlosc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            cIlosc.HeaderText = "⚖️ Ilość (kg)";
            cIlosc.DefaultCellStyle.Font = new Font("Segoe UI Semibold", 10.5f);
            cIlosc.DefaultCellStyle.ForeColor = Color.FromArgb(16, 185, 129);

            var cKodKopia = dataGridViewZamowienie.Columns["KodKopia"]!;
            cKodKopia.ReadOnly = true;
            cKodKopia.FillWeight = 165;
            cKodKopia.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            cKodKopia.DefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f);
            cKodKopia.DefaultCellStyle.ForeColor = Color.FromArgb(31, 41, 55);
            cKodKopia.DefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
            cKodKopia.HeaderText = "🥩 Towar";
        }

        private void WireUpUIEvents()
        {
            dataGridViewZamowienie.CellValueChanged += DataGridViewZamowienie_CellValueChanged;
            dataGridViewZamowienie.EditingControlShowing += DataGridViewZamowienie_EditingControlShowing;
            dataGridViewZamowienie.CellPainting += DataGridViewZamowienie_CellPainting;
            dataGridViewZamowienie.RowPostPaint += DataGridViewZamowienie_RowPostPaint;
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

        private void DataGridViewZamowienie_RowPostPaint(object? sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = dataGridViewZamowienie.Rows[e.RowIndex];
            var dataRow = (row.DataBoundItem as DataRowView)?.Row;
            if (dataRow == null) return;

            string kod = dataRow.Field<string>("Kod") ?? "";
            decimal ilosc = dataRow.Field<decimal?>("Ilosc") ?? 0m;
            decimal pojemniki = dataRow.Field<decimal?>("Pojemniki") ?? 0m;
            decimal palety = dataRow.Field<decimal?>("Palety") ?? 0m;
            bool e2 = dataRow.Field<bool>("E2");
            bool folia = dataRow.Field<bool>("Folia");

            bool maWartosci = (ilosc > 0 || pojemniki > 0 || palety > 0);

            if (maWartosci)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(240, 253, 244);
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(167, 243, 208);
            }
            else
            {
                row.DefaultCellStyle.BackColor = (e.RowIndex % 2 == 0) ? Color.White : Color.FromArgb(249, 250, 251);
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(238, 242, 255);
            }

            var e2Cell = row.Cells["E2"];
            var foliaCell = row.Cells["Folia"];

            if (e2)
            {
                e2Cell.Style.BackColor = Color.FromArgb(254, 202, 202);
                e2Cell.Style.SelectionBackColor = Color.FromArgb(252, 165, 165);
            }
            else if (!maWartosci)
            {
                e2Cell.Style.BackColor = Color.FromArgb(254, 242, 242);
                e2Cell.Style.SelectionBackColor = Color.FromArgb(254, 226, 226);
            }
            else
            {
                e2Cell.Style.BackColor = Color.FromArgb(254, 240, 240);
                e2Cell.Style.SelectionBackColor = Color.FromArgb(220, 252, 231);
            }

            if (folia)
            {
                foliaCell.Style.BackColor = Color.FromArgb(191, 219, 254);
                foliaCell.Style.SelectionBackColor = Color.FromArgb(147, 197, 253);
            }
            else if (!maWartosci)
            {
                foliaCell.Style.BackColor = Color.FromArgb(239, 246, 255);
                foliaCell.Style.SelectionBackColor = Color.FromArgb(219, 234, 254);
            }
            else
            {
                foliaCell.Style.BackColor = Color.FromArgb(245, 250, 255);
                foliaCell.Style.SelectionBackColor = Color.FromArgb(220, 252, 231);
            }

            if (CzyRysowacSeparatorPo(kod))
            {
                using var pen = new Pen(Color.FromArgb(209, 213, 219), 2);
                var bounds = e.RowBounds;
                e.Graphics.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
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
                { "KURCZAK A", 1 }, { "FILET A", 2 }, { "ĆWIARTKA", 3 }, { "SKRZYDŁO I", 4 },
                { "NOGA", 5 }, { "PAŁKA", 6 }, { "KORPUS", 7 }, { "POLĘDWICZKI", 8 },
                { "SERCE", 9 }, { "WĄTROBA", 10 }, { "ŻOŁĄDKI", 11 }, { "ĆWIARTKA II", 12 },
                { "FILET II", 13 }, { "FILET II PP", 14 }, { "SKRZYDŁO II", 15 }
            };

            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();

            var katalogi = new[] { "67095", "67153" };

            foreach (var katalog in katalogi)
            {
                await using var cmd = new SqlCommand(
                    "SELECT Id, Kod FROM [HANDEL].[HM].[TW] WHERE katalog = @katalog ORDER BY Kod ASC", cn);
                cmd.Parameters.AddWithValue("@katalog", katalog);

                await using var rd = await cmd.ExecuteReaderAsync();

                var tempList = new List<(int Id, string Kod, int Priority, string Katalog)>();

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

                    tempList.Add((rd.GetInt32(0), kod, priority, katalog));
                }

                var sortedList = tempList.OrderBy(x => x.Priority).ThenBy(x => x.Kod).ToList();

                foreach (var item in sortedList)
                {
                    _dt.Rows.Add(item.Id, item.Kod, item.Katalog, false, false, 0m, 0m, 0m, item.Kod, item.Kod);
                }
            }
        }

        private bool CzyRysowacSeparatorPo(string kod)
        {
            if (string.IsNullOrEmpty(kod)) return false;
            var kodUpper = kod.ToUpper();
            return kodUpper.Contains("KURCZAK A") || kodUpper.Contains("POLĘDWICZKI") || kodUpper.Contains("ŻOŁĄDKI");
        }

        private async Task LoadKontrahenciAsync()
        {
            const string sql = @"
                SELECT c.Id, c.Shortcut AS Nazwa, c.NIP,
                    poa.Postcode AS KodPocztowy, poa.Street AS Miejscowosc, 
                    wym.CDim_Handlowiec_Val AS Handlowiec
                FROM [HANDEL].[SSCommon].[STContractors] c
                LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] poa 
                    ON poa.ContactGuid = c.ContactGuid AND poa.AddressName = N'adres domyślny'
                LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym ON c.Id = wym.ElementId
                ORDER BY c.Shortcut;";

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
                .Take(10).ToList();

            listaWynikowOdbiorcy.DataSource = wyniki;
            listaWynikowOdbiorcy.DisplayMember = "Nazwa";
            listaWynikowOdbiorcy.ValueMember = "Id";

            if (wyniki.Any())
            {
                var screenPoint = txtSzukajOdbiorcy.Parent.PointToScreen(txtSzukajOdbiorcy.Location);
                var clientPoint = panelDetaleZamowienia.PointToClient(screenPoint);

                listaWynikowOdbiorcy.Location = new Point(clientPoint.X, clientPoint.Y + txtSzukajOdbiorcy.Height + 50);
                listaWynikowOdbiorcy.Width = 270;
                listaWynikowOdbiorcy.Height = Math.Min(180, wyniki.Count * 22 + 5);
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

        private void WybierzOdbiorceZListy()
        {
            if (listaWynikowOdbiorcy.SelectedItem is KontrahentInfo wybrany)
            {
                UstawOdbiorce(wybrany.Id);
            }
        }

        private async void UstawOdbiorce(string id)
        {
            _selectedKlientId = id;
            var info = _kontrahenci.FirstOrDefault(k => k.Id == id);
            if (info != null)
            {
                txtSzukajOdbiorcy.Text = info.Nazwa;
                listaWynikowOdbiorcy.Visible = false;
                panelDaneOdbiorcy.Visible = true;
                lblWybranyOdbiorca.Text = $"👤 {info.Nazwa}";
                lblNip.Text = $"💳 NIP: {info.NIP}";
                lblAdres.Text = $"📍 {info.KodPocztowy} {info.Miejscowosc}";
                lblHandlowiec.Text = $"👔 Opiekun: {info.Handlowiec}";

                await LoadPreferredHours(id);
                await LoadHistoriaZamowien(id);
                await LoadPlatnosciOdbiorcy(id);

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
            _headerIcons["Kod"] = CreateIconForText("PROD");
            _headerIcons["Palety"] = CreatePalletIcon();
            _headerIcons["Pojemniki"] = CreateContainerIcon();
            _headerIcons["Ilosc"] = CreateScaleIcon();
            _headerIcons["KodTowaru"] = CreateIconForText("PROD");
            _headerIcons["KodKopia"] = CreateIconForText("PROD");
        }

        private Image CreatePalletIcon()
        {
            var bmp = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var brownPen = new Pen(Color.FromArgb(146, 64, 14), 1.5f);
            g.DrawRectangle(brownPen, 2, 8, 12, 6);
            g.DrawLine(brownPen, 2, 11, 14, 11);
            g.FillRectangle(new SolidBrush(Color.FromArgb(217, 119, 6)), 4, 3, 3, 5);
            g.FillRectangle(new SolidBrush(Color.FromArgb(217, 119, 6)), 9, 3, 3, 5);
            return bmp;
        }

        private Image CreateContainerIcon()
        {
            var bmp = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var grayBrush = new SolidBrush(Color.FromArgb(156, 163, 175));
            g.FillRectangle(grayBrush, 2, 4, 12, 9);
            using var darkPen = new Pen(Color.FromArgb(107, 114, 128), 1);
            g.DrawRectangle(darkPen, 2, 4, 12, 9);
            return bmp;
        }

        private Image CreateScaleIcon()
        {
            var bmp = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var darkGrayPen = new Pen(Color.FromArgb(75, 85, 99), 1.5f);
            g.DrawLine(darkGrayPen, 2, 14, 14, 14);
            g.DrawLine(darkGrayPen, 8, 14, 8, 4);
            g.DrawLine(darkGrayPen, 2, 5, 14, 5);
            return bmp;
        }

        private Image CreateIconForText(string text)
        {
            var bmp = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            using var font = new Font("Segoe UI", 6.5f, FontStyle.Bold);
            TextRenderer.DrawText(g, text, font, new Point(0, 2), Color.FromArgb(107, 114, 128));
            return bmp;
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
            if (e.RowIndex == -1 && e.ColumnIndex >= 0)
            {
                string colName = dataGridViewZamowienie.Columns[e.ColumnIndex].Name;
                if (!_headerIcons.ContainsKey(colName)) return;

                e.PaintBackground(e.CellBounds, true);

                var g = e.Graphics;
                var icon = _headerIcons[colName];

                int y = e.CellBounds.Y + (e.CellBounds.Height - icon.Height) / 2;
                g.DrawImage(icon, e.CellBounds.X + 6, y);

                var textBounds = new Rectangle(
                    e.CellBounds.X + icon.Width + 12,
                    e.CellBounds.Y,
                    e.CellBounds.Width - icon.Width - 18,
                    e.CellBounds.Height);

                TextRenderer.DrawText(g, e.Value?.ToString(), e.CellStyle.Font, textBounds,
                    e.CellStyle.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

                e.Handled = true;
            }
        }

        private void ClearFormForNewOrder()
        {
            var selectedHandlowiec = cbHandlowiecFilter.SelectedItem;

            _idZamowieniaDoEdycji = null;
            _selectedKlientId = null;
            txtSzukajOdbiorcy.Text = "";
            panelDaneOdbiorcy.Visible = false;
            listaWynikowOdbiorcy.Visible = false;

            _blokujObslugeZmian = true;
            foreach (DataRow r in _dt.Rows)
            {
                r["E2"] = false;
                r["Folia"] = false;
                r["Ilosc"] = 0m;
                r["Pojemniki"] = 0m;
                r["Palety"] = 0m;
            }
            _blokujObslugeZmian = false;

            if (rbSwiezy != null) rbSwiezy.Checked = true;
            _aktywnyKatalog = "67095";
            _view.RowFilter = $"Katalog = '{_aktywnyKatalog}'";

            textBoxUwagi.Text = "";

            var dzis = DateTime.Now.Date;

            if (dateTimePickerProdukcji != null)
            {
                dateTimePickerProdukcji.Value = DateTime.Today;
            }

            dateTimePickerSprzedaz.Value = (dzis.DayOfWeek == DayOfWeek.Friday) ? dzis.AddDays(3) : dzis.AddDays(1);
            dateTimePickerGodzinaPrzyjazdu.Value = DateTime.Today.AddHours(8);

            lblTytul.Text = "📋 Nowe zamówienie";
            btnZapisz.Text = "💾 Zapisz";

            cbHandlowiecFilter.SelectedItem = selectedHandlowiec;

            if (panelSugestieGodzin != null) panelSugestieGodzin.Controls.Clear();
            if (lblHistoriaZamowien != null) lblHistoriaZamowien.Text = "";

            _limitKredytowy = 0;
            _doZaplacenia = 0;
            if (lblLimitInfo != null)
            {
                lblLimitInfo.Text = "Wybierz odbiorcę aby zobaczyć informacje o płatnościach";
                lblLimitInfo.ForeColor = Color.FromArgb(107, 114, 128);
                lblLimitInfo.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            }
            if (progressLimit != null) progressLimit.Value = 0;
            if (panelPlatnosci != null) panelPlatnosci.BackColor = Color.White;

            RecalcSum();
            txtSzukajOdbiorcy.Focus();
        }

        #endregion

        #region Zapis i Odczyt Zamówienia

        private async Task LoadZamowienieAsync(int id)
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            // BEZPIECZNE SPRAWDZENIE CZY KOLUMNA ISTNIEJE
            bool dataProdukcjiExists = false;
            try
            {
                await using var cmdCheck = new SqlCommand(@"
            SELECT COUNT(*) 
            FROM sys.columns 
            WHERE object_id = OBJECT_ID(N'[dbo].[ZamowieniaMieso]') 
            AND name = 'DataProdukcji'", cn);
                int count = (int)await cmdCheck.ExecuteScalarAsync();
                dataProdukcjiExists = count > 0;
            }
            catch { }

            // BUDUJ ZAPYTANIE DYNAMICZNIE
            string sqlQuery = dataProdukcjiExists
                ? "SELECT DataZamowienia, KlientId, Uwagi, DataPrzyjazdu, DataProdukcji FROM [dbo].[ZamowieniaMieso] WHERE Id=@Id"
                : "SELECT DataZamowienia, KlientId, Uwagi, DataPrzyjazdu FROM [dbo].[ZamowieniaMieso] WHERE Id=@Id";

            await using (var cmdZ = new SqlCommand(sqlQuery, cn))
            {
                cmdZ.Parameters.AddWithValue("@Id", id);
                await using var rd = await cmdZ.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    dateTimePickerSprzedaz.Value = rd.GetDateTime(0);
                    UstawOdbiorce(rd.GetInt32(1).ToString());
                    textBoxUwagi.Text = await rd.IsDBNullAsync(2) ? "" : rd.GetString(2);
                    dateTimePickerGodzinaPrzyjazdu.Value = rd.GetDateTime(3);

                    // POPRAWIONA OBSŁUGA DATY PRODUKCJI
                    if (dateTimePickerProdukcji != null)
                    {
                        if (dataProdukcjiExists && rd.FieldCount > 4 && !await rd.IsDBNullAsync(4))
                        {
                            dateTimePickerProdukcji.Value = rd.GetDateTime(4);
                        }
                        else
                        {
                            // Jeśli brak kolumny lub NULL, ustaw dzisiejszą datę
                            dateTimePickerProdukcji.Value = DateTime.Today;
                        }
                    }
                }
            }

            _blokujObslugeZmian = true;
            foreach (DataRow r in _dt.Rows)
            {
                r["E2"] = false;
                r["Folia"] = false;
                r["Ilosc"] = 0m;
                r["Pojemniki"] = 0m;
                r["Palety"] = 0m;
            }

            var zamowienieTowary = new List<(int TowarId, decimal Ilosc, int Pojemniki, decimal Palety, bool E2, bool Folia)>();

            await using (var cmdT = new SqlCommand(@"
        SELECT KodTowaru, Ilosc, ISNULL(Pojemniki, 0) as Pojemniki, 
               ISNULL(Palety, 0) as Palety, ISNULL(E2, 0) as E2, 
               ISNULL(Folia, 0) as Folia
        FROM [dbo].[ZamowieniaMiesoTowar]
        WHERE ZamowienieId=@Id", cn))
            {
                cmdT.Parameters.AddWithValue("@Id", id);
                await using var rd = await cmdT.ExecuteReaderAsync();

                while (await rd.ReadAsync())
                {
                    zamowienieTowary.Add((
                        rd.GetInt32(0),
                        await rd.IsDBNullAsync(1) ? 0m : rd.GetDecimal(1),
                        rd.GetInt32(2),
                        rd.GetDecimal(3),
                        rd.GetBoolean(4),
                        rd.GetBoolean(5)
                    ));
                }
            }

            bool czySaMrozonki = false;

            foreach (var towar in zamowienieTowary)
            {
                var rows = _dt.Select($"Id = {towar.TowarId}");
                if (rows.Any())
                {
                    var row = rows[0];
                    string katalog = row.Field<string>("Katalog") ?? "";

                    if (katalog == "67153") czySaMrozonki = true;

                    row["Ilosc"] = towar.Ilosc;
                    row["Pojemniki"] = towar.Pojemniki;
                    row["Palety"] = towar.Palety;
                    row["E2"] = towar.E2;
                    row["Folia"] = towar.Folia;
                }
            }

            if (czySaMrozonki && rbMrozony != null)
            {
                rbMrozony.Checked = true;
                _aktywnyKatalog = "67153";
            }
            else if (rbSwiezy != null)
            {
                rbSwiezy.Checked = true;
                _aktywnyKatalog = "67095";
            }

            _view.RowFilter = $"Katalog = '{_aktywnyKatalog}'";

            _blokujObslugeZmian = false;
            RecalcSum();
        }
        private bool ValidateBeforeSave(out string message)
        {
            if (string.IsNullOrWhiteSpace(_selectedKlientId))
            {
                message = "❌ Wybierz odbiorcę.";
                return false;
            }

            bool czyMaJakiekolwiekIlosci = false;
            foreach (DataRow r in _dt.Rows)
            {
                if (r.Field<decimal>("Ilosc") > 0m)
                {
                    czyMaJakiekolwiekIlosci = true;
                    break;
                }
            }

            if (!czyMaJakiekolwiekIlosci)
            {
                message = "❌ Wpisz ilość dla przynajmniej jednego towaru.";
                return false;
            }

            if (_dt.AsEnumerable().Any(r => r.Field<decimal>("Ilosc") < 0m))
            {
                message = "❌ Ilość nie może być ujemna.";
                return false;
            }
            message = "";
            return true;
        }

        private bool IsSpecialProduct(string kod)
        {
            if (string.IsNullOrEmpty(kod)) return false;

            var kodUpper = kod.ToUpper();
            return kodUpper.Contains("WĄTROBA") || kodUpper.Contains("ŻOŁĄDKI") || kodUpper.Contains("SERCE");
        }

        private decimal GetKgPerContainer(string kod)
        {
            return IsSpecialProduct(kod) ? KG_NA_POJEMNIKU_SPECJALNY : KG_NA_POJEMNIKU;
        }

        private async void btnZapisz_Click(object? sender, EventArgs e)
        {
            if (!ValidateBeforeSave(out var msg))
            {
                MessageBox.Show(msg, "Błąd danych", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal sumaPaletCalkowita = 0m;
            foreach (DataRow r in _dt.Rows)
            {
                sumaPaletCalkowita += r.Field<decimal?>("Palety") ?? 0m;
            }

            if (sumaPaletCalkowita > LIMIT_PALET_OSTRZEZENIE)
            {
                var result = MessageBox.Show(
                    $"⚠️ Łączna liczba palet ({sumaPaletCalkowita:N1}) przekracza limit TIR ({LIMIT_PALET_OSTRZEZENIE}).\n\n" +
                    "Czy na pewno chcesz zapisać to zamówienie?",
                    "Uwaga - przekroczenie limitu",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.No) return;
            }

            Cursor = Cursors.WaitCursor;
            btnZapisz.Enabled = false;

            try
            {
                await SaveOrderAsync();
                string summary = BuildOrderSummary();
                string title = _idZamowieniaDoEdycji.HasValue ? "✅ Zamówienie zaktualizowane" : "✅ Zamówienie zapisane";

                MessageBox.Show(summary, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadOstatnieZamowienia();
                UpdateOstatniOdbiorcyGrid(cbHandlowiecFilter.SelectedItem?.ToString());
                ClearFormForNewOrder();
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Błąd zapisu: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            var orderedItems = _dt.AsEnumerable().Where(r => r.Field<decimal?>("Ilosc") > 0m).ToList();

            sb.AppendLine($"👤 Odbiorca: {lblWybranyOdbiorca.Text.Replace("👤 ", "")}");
            sb.AppendLine($"🏭 Data produkcji: {dateTimePickerProdukcji?.Value:yyyy-MM-dd (dddd)}");
            sb.AppendLine($"📅 Data sprzedaży: {dateTimePickerSprzedaz.Value:yyyy-MM-dd (dddd)}");

            var swiezeItems = orderedItems.Where(r => r.Field<string>("Katalog") == "67095").ToList();
            var mrozoneItems = orderedItems.Where(r => r.Field<string>("Katalog") == "67153").ToList();

            if (swiezeItems.Any() && mrozoneItems.Any())
            {
                sb.AppendLine($"\n⚠️ ZAMÓWIENIE MIESZANE: {swiezeItems.Count} świeżych + {mrozoneItems.Count} mrożonych");
            }
            else if (mrozoneItems.Any())
            {
                sb.AppendLine($"\n❄️ Produkty mrożone: {mrozoneItems.Count}");
            }

            var e2Items = orderedItems.Where(r => r.Field<bool>("E2")).ToList();
            if (e2Items.Any()) sb.AppendLine($"✅ Towary E2 (40 poj./pal.): {e2Items.Count}");

            var foliaItems = orderedItems.Where(r => r.Field<bool>("Folia")).ToList();
            if (foliaItems.Any()) sb.AppendLine($"📦 Towary w folii: {foliaItems.Count}");

            sb.AppendLine("\n📋 Zamówione towary:");

            decimal totalPojemniki = 0;
            decimal totalPalety = 0;

            foreach (var item in orderedItems)
            {
                string katalog = item.Field<string>("Katalog") == "67153" ? " [❄️MROŻONY]" : "";
                string e2Marker = item.Field<bool>("E2") ? " [✅E2]" : "";
                string foliaMarker = item.Field<bool>("Folia") ? " [📦FOLIA]" : "";
                decimal pojemniki = item.Field<decimal>("Pojemniki");
                decimal palety = item.Field<decimal>("Palety");

                totalPojemniki += pojemniki;
                totalPalety += palety;

                sb.AppendLine($"  🥩 {item.Field<string>("Kod")}{katalog}{e2Marker}{foliaMarker}: {item.Field<decimal>("Ilosc"):N0} kg " +
                            $"({pojemniki:N0} poj., {palety:N1} pal.)");
            }

            decimal totalKg = orderedItems.Sum(r => r.Field<decimal>("Ilosc"));
            sb.AppendLine($"\n📊 Podsumowanie:");
            sb.AppendLine($"  ⚖️ Łącznie: {totalKg:N0} kg");
            sb.AppendLine($"  📥 Pojemników: {totalPojemniki:N0}");
            sb.AppendLine($"  📦 Palet: {totalPalety:N1}");

            return sb.ToString();
        }

        private async Task SaveOrderAsync()
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            // **SPRAWDŹ CZY KOLUMNY ISTNIEJĄ PRZED TRANSAKCJĄ**
            bool dataProdukcjiExists = await CheckIfColumnExists(cn, "DataProdukcji");
            bool dataUbojuExists = await CheckIfColumnExists(cn, "DataUboju");  // NOWE

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

            // **JEDNA DATA DLA OBU KOLUMN**
            DateTime dataProdukcji = dateTimePickerProdukcji?.Value.Date ?? DateTime.Today;

            if (_idZamowieniaDoEdycji.HasValue)
            {
                orderId = _idZamowieniaDoEdycji.Value;

                string updateSql = @"UPDATE [dbo].[ZamowieniaMieso] SET 
            DataZamowienia = @dz, 
            DataPrzyjazdu = @dp, 
            KlientId = @kid, 
            Uwagi = @uw, 
            KtoMod = @km, 
            KiedyMod = SYSDATETIME(),
            LiczbaPojemnikow = @poj, 
            LiczbaPalet = @pal, 
            TrybE2 = @e2";

                if (dataProdukcjiExists)
                {
                    updateSql += ", DataProdukcji = @dprod";
                }

                // **DODAJ DataUboju**
                if (dataUbojuExists)
                {
                    updateSql += ", DataUboju = @duboj";
                }

                updateSql += " WHERE Id = @id";

                var cmdUpdate = new SqlCommand(updateSql, cn, tr);
                cmdUpdate.Parameters.AddWithValue("@dz", dateTimePickerSprzedaz.Value.Date);

                var dataPrzyjazdu = dateTimePickerSprzedaz.Value.Date.Add(dateTimePickerGodzinaPrzyjazdu.Value.TimeOfDay);
                cmdUpdate.Parameters.AddWithValue("@dp", dataPrzyjazdu);

                if (dataProdukcjiExists)
                {
                    cmdUpdate.Parameters.AddWithValue("@dprod", dataProdukcji);
                }

                // **TA SAMA DATA DO DataUboju**
                if (dataUbojuExists)
                {
                    cmdUpdate.Parameters.AddWithValue("@duboj", dataProdukcji);
                }

                cmdUpdate.Parameters.AddWithValue("@kid", int.Parse(_selectedKlientId!));
                cmdUpdate.Parameters.AddWithValue("@uw", string.IsNullOrWhiteSpace(textBoxUwagi.Text) ? (object)DBNull.Value : textBoxUwagi.Text);
                cmdUpdate.Parameters.AddWithValue("@km", UserID);
                cmdUpdate.Parameters.AddWithValue("@id", orderId);
                cmdUpdate.Parameters.AddWithValue("@poj", (int)Math.Round(sumaPojemnikow));
                cmdUpdate.Parameters.AddWithValue("@pal", sumaPalet);
                cmdUpdate.Parameters.AddWithValue("@e2", czyJakikolwiekE2);

                await cmdUpdate.ExecuteNonQueryAsync();

                var cmdDelete = new SqlCommand(@"DELETE FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId = @id", cn, tr);
                cmdDelete.Parameters.AddWithValue("@id", orderId);
                await cmdDelete.ExecuteNonQueryAsync();
            }
            else
            {
                // **NOWE ZAMÓWIENIE**
                var cmdGetId = new SqlCommand(@"SELECT ISNULL(MAX(Id), 0) + 1 FROM [dbo].[ZamowieniaMieso]", cn, tr);
                orderId = Convert.ToInt32(await cmdGetId.ExecuteScalarAsync());

                // **BUDUJ SQL DYNAMICZNIE Z OBIEMA KOLUMNAMI**
                string insertColumns = "Id, DataZamowienia, DataPrzyjazdu, KlientId, Uwagi, IdUser, DataUtworzenia, LiczbaPojemnikow, LiczbaPalet, TrybE2, TransportStatus";
                string insertValues = "@id, @dz, @dp, @kid, @uw, @u, GETDATE(), @poj, @pal, @e2, 'Oczekuje'";

                if (dataProdukcjiExists)
                {
                    insertColumns += ", DataProdukcji";
                    insertValues += ", @dprod";
                }

                if (dataUbojuExists)
                {
                    insertColumns += ", DataUboju";
                    insertValues += ", @duboj";
                }

                string insertSql = $@"INSERT INTO [dbo].[ZamowieniaMieso] 
            ({insertColumns}) 
            VALUES ({insertValues})";

                var cmdInsert = new SqlCommand(insertSql, cn, tr);
                cmdInsert.Parameters.AddWithValue("@id", orderId);
                cmdInsert.Parameters.AddWithValue("@dz", dateTimePickerSprzedaz.Value.Date);

                var dataPrzyjazdu = dateTimePickerSprzedaz.Value.Date.Add(dateTimePickerGodzinaPrzyjazdu.Value.TimeOfDay);
                cmdInsert.Parameters.AddWithValue("@dp", dataPrzyjazdu);

                if (dataProdukcjiExists)
                {
                    cmdInsert.Parameters.AddWithValue("@dprod", dataProdukcji);
                }

                // **TA SAMA DATA DO DataUboju**
                if (dataUbojuExists)
                {
                    cmdInsert.Parameters.AddWithValue("@duboj", dataProdukcji);
                }

                cmdInsert.Parameters.AddWithValue("@kid", int.Parse(_selectedKlientId!));
                cmdInsert.Parameters.AddWithValue("@uw", string.IsNullOrWhiteSpace(textBoxUwagi.Text) ? (object)DBNull.Value : textBoxUwagi.Text);
                cmdInsert.Parameters.AddWithValue("@u", UserID);
                cmdInsert.Parameters.AddWithValue("@poj", (int)Math.Round(sumaPojemnikow));
                cmdInsert.Parameters.AddWithValue("@pal", sumaPalet);
                cmdInsert.Parameters.AddWithValue("@e2", czyJakikolwiekE2);

                await cmdInsert.ExecuteNonQueryAsync();
            }

            // **ZAPISZ POZYCJE ZAMÓWIENIA**
            var cmdInsertItem = new SqlCommand(@"INSERT INTO [dbo].[ZamowieniaMiesoTowar] 
        (ZamowienieId, KodTowaru, Ilosc, Cena, Pojemniki, Palety, E2, Folia) 
        VALUES (@zid, @kt, @il, @ce, @poj, @pal, @e2, @folia)", cn, tr);

            cmdInsertItem.Parameters.Add("@zid", SqlDbType.Int);
            cmdInsertItem.Parameters.Add("@kt", SqlDbType.Int);
            cmdInsertItem.Parameters.Add("@il", SqlDbType.Decimal);
            cmdInsertItem.Parameters.Add("@ce", SqlDbType.Decimal);
            cmdInsertItem.Parameters.Add("@poj", SqlDbType.Int);
            cmdInsertItem.Parameters.Add("@pal", SqlDbType.Decimal);
            cmdInsertItem.Parameters.Add("@e2", SqlDbType.Bit);
            cmdInsertItem.Parameters.Add("@folia", SqlDbType.Bit);

            foreach (DataRow r in _dt.Rows)
            {
                if (r.Field<decimal>("Ilosc") <= 0m) continue;

                cmdInsertItem.Parameters["@zid"].Value = orderId;
                cmdInsertItem.Parameters["@kt"].Value = r.Field<int>("Id");
                cmdInsertItem.Parameters["@il"].Value = r.Field<decimal>("Ilosc");
                cmdInsertItem.Parameters["@ce"].Value = 0m;
                cmdInsertItem.Parameters["@poj"].Value = (int)Math.Round(r.Field<decimal>("Pojemniki"));
                cmdInsertItem.Parameters["@pal"].Value = r.Field<decimal>("Palety");
                cmdInsertItem.Parameters["@e2"].Value = r.Field<bool>("E2");
                cmdInsertItem.Parameters["@folia"].Value = r.Field<bool>("Folia");

                await cmdInsertItem.ExecuteNonQueryAsync();
            }

            await tr.CommitAsync();
        }
        private async Task<bool> CheckIfColumnExists(SqlConnection cn, string columnName)
        {
            try
            {
                await using var cmd = new SqlCommand(@"
            SELECT COUNT(*) 
            FROM sys.columns 
            WHERE object_id = OBJECT_ID(N'[dbo].[ZamowieniaMieso]') 
            AND name = @ColumnName", cn);
                cmd.Parameters.AddWithValue("@ColumnName", columnName);
                int count = (int)await cmd.ExecuteScalarAsync();
                return count > 0;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}