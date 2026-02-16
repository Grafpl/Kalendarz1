// Plik: WidokZamowienia.cs
// WERSJA 23.0 - Rozbudowany pasek klas wagowych z dokładnymi informacjami ile zabrano
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
using Kalendarz1.Services;

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

        // Cache miniaturek produktów: TowarId -> Image (32x32)
        private readonly Dictionary<int, Image?> _productImages = new();

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
        private Label? lblPozycji;

        // Pasek klas wagowych w podsumowaniu
        private Panel? panelKlasyWagowe;
        private Panel? pnlKlasyPasek;
        private Label? lblKlasyInfo;

        private Panel? panelTransport;
        private ProgressBar? progressSolowka;
        private ProgressBar? progressTir;
        private Label? lblSolowkaInfo;
        private Label? lblTirInfo;

        private FlowLayoutPanel? panelSugestieGodzin;

        private Panel? panelPlatnosci;
        private Label? lblLimitInfo;
        private ProgressBar? progressLimit;
        private decimal _limitKredytowy = 0;
        private decimal _doZaplacenia = 0;

        private DateTimePicker? dateTimePickerProdukcji;
        private Label? lblGodzinaLabel;

        // Waluta zamówienia
        private ComboBox? cbWaluta;

        // Klasy wagowe dla tuszki (Kurczak A)
        private RozkladKlasWagowych? _rozkladKlasKurczakA;

        // Śledzenie zmian - oryginalne wartości przy edycji
        private OryginalneWartosciZamowienia? _oryginalneWartosci;

        // Animacja pulsowania wiersza
        private readonly Dictionary<int, int> _pulseRows = new();
        private System.Windows.Forms.Timer? _pulseTimer;

        /// <summary>
        /// Przechowuje oryginalne wartości zamówienia do śledzenia zmian
        /// </summary>
        private class OryginalneWartosciZamowienia
        {
            public DateTime DataZamowienia { get; set; }
            public DateTime DataPrzyjazdu { get; set; }
            public DateTime? DataProdukcji { get; set; }
            public int KlientId { get; set; }
            public string? KlientNazwa { get; set; }
            public string? Uwagi { get; set; }
            public string? TransportStatus { get; set; }
            public Dictionary<int, OryginalnyTowar> Towary { get; set; } = new();
        }

        private class OryginalnyTowar
        {
            public string Nazwa { get; set; } = "";
            public decimal Ilosc { get; set; }
            public string? Cena { get; set; }
            public bool E2 { get; set; }
            public bool Folia { get; set; }
            public bool Hallal { get; set; }
            public int Pojemniki { get; set; }
            public decimal Palety { get; set; }
        }

        public WidokZamowienia() : this(App.UserID ?? string.Empty, null) { }
        public WidokZamowienia(int? idZamowienia) : this(App.UserID ?? string.Empty, idZamowienia) { }

        public WidokZamowienia(string userId, int? idZamowienia = null)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
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

            // Konfiguracja checkboxa własnego odbioru
            chkWlasnyOdbior.CheckedChanged += ChkWlasnyOdbior_CheckedChanged;

            // Przycisk "Kopiuj z ostatniego zamówienia"
            var btnKopiuj = new Button
            {
                Text = "Kopiuj ostatnie",
                Font = new Font("Segoe UI", 8f),
                Size = new Size(110, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(25, 118, 210),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(panelOdbiorca.Width - 120, 65)
            };
            btnKopiuj.FlatAppearance.BorderSize = 0;
            btnKopiuj.Click += BtnKopiujOstatnie_Click;
            panelOdbiorca.Controls.Add(btnKopiuj);
            btnKopiuj.BringToFront();

            // Przyciski szablonów w panelu akcji
            var btnZapiszSzablon = new Button
            {
                Text = "Zapisz szablon",
                Font = new Font("Segoe UI", 8f),
                Size = new Size(100, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(107, 114, 128),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Location = new Point(10, 4)
            };
            btnZapiszSzablon.FlatAppearance.BorderSize = 0;
            btnZapiszSzablon.Click += BtnZapiszSzablon_Click;

            var btnWczytajSzablon = new Button
            {
                Text = "Wczytaj szablon",
                Font = new Font("Segoe UI", 8f),
                Size = new Size(105, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(79, 70, 229),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Location = new Point(115, 4)
            };
            btnWczytajSzablon.FlatAppearance.BorderSize = 0;
            btnWczytajSzablon.Click += BtnWczytajSzablon_Click;

            panelAkcji.Controls.Add(btnZapiszSzablon);
            panelAkcji.Controls.Add(btnWczytajSzablon);

            try
            {
                await LoadInitialDataInBackground();
                WireUpUIEvents();
                await LoadOstatnieZamowienia();

                if (_idZamowieniaDoEdycji.HasValue)
                {
                    await LoadZamowienieAsync(_idZamowieniaDoEdycji.Value);
                    btnZapisz.Text = "Zapisz zmiany";
                }
            }
            catch (Exception ex)
            {
                ShowError($"Wystąpił błąd: {ex.Message}");
            }
            finally
            {
                btnZapisz.Enabled = true;
            }
        }

        private void ChkWlasnyOdbior_CheckedChanged(object? sender, EventArgs e)
        {
            if (lblGodzinaLabel != null)
            {
                lblGodzinaLabel.Text = chkWlasnyOdbior.Checked ? "Godzina odbioru" : "Godzina przyjazdu";
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
                panelOstatniOdbiorcy.Height = 220;
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

            panelOdbiorca.Padding = new Padding(10, 5, 10, 5);
            panelOdbiorca.Height = 70;

            var oldTitle = panelOdbiorca.Controls.OfType<Label>().FirstOrDefault(l => l == lblTytul);
            if (oldTitle != null)
            {
                panelOdbiorca.Controls.Remove(oldTitle);
            }

            var lblOdbiorca = panelOdbiorca.Controls.OfType<Label>().FirstOrDefault(l => l.Name == "label1");
            if (lblOdbiorca != null)
            {
                lblOdbiorca.Text = "Odbiorca:";
                lblOdbiorca.Location = new Point(10, 10);
                lblOdbiorca.Size = new Size(80, 16);
            }
            else
            {
                lblOdbiorca = new Label { Name = "label1", Text = "Odbiorca:", Location = new Point(10, 10), Size = new Size(80, 16) };
                panelOdbiorca.Controls.Add(lblOdbiorca);
            }

            var existingHandlowiecLabel = panelOdbiorca.Controls.OfType<Label>()
                .FirstOrDefault(l => l.Text.Contains("Handlowiec"));

            if (existingHandlowiecLabel == null)
            {
                var lblHandlowiec = new Label
                {
                    Text = "Handlowiec:",
                    Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                    ForeColor = Color.FromArgb(107, 114, 128),
                    Location = new Point(120, 10),
                    Size = new Size(90, 16)
                };
                panelOdbiorca.Controls.Add(lblHandlowiec);
            }
            else
            {
                existingHandlowiecLabel.Text = "Handlowiec:";
                existingHandlowiecLabel.Location = new Point(120, 10);
                existingHandlowiecLabel.Size = new Size(90, 16);
            }

            if (txtSzukajOdbiorcy != null)
            {
                txtSzukajOdbiorcy.Location = new Point(10, 28);
                txtSzukajOdbiorcy.Size = new Size(100, 28);
                txtSzukajOdbiorcy.Font = new Font("Segoe UI", 10f);
            }

            if (cbHandlowiecFilter != null)
            {
                cbHandlowiecFilter.Location = new Point(120, 28);
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
                Location = new Point(230, 23),
                Size = new Size(130, 32),
                BackColor = Color.FromArgb(224, 242, 215),
                BorderStyle = BorderStyle.None
            };

            rbSwiezy = new RadioButton
            {
                Text = "Świeże",
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
                Text = "Mrożone",
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
                using var pen = new Pen(Color.FromArgb(92, 138, 58), 1);
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

            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 270));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 85));
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
                Text = "NOTATKA",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(230, 126, 34),
                Location = new Point(35, 8),
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

                using var iconPen = new Pen(Color.FromArgb(217, 119, 6), 1.5f);
                e.Graphics.DrawRectangle(iconPen, 14, 9, 12, 12);
                e.Graphics.DrawLine(iconPen, 17, 13, 23, 13);
                e.Graphics.DrawLine(iconPen, 17, 17, 23, 17);
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
                Height = 270
            };

            var lblHeader = new Label
            {
                Text = "TRANSPORT",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(92, 138, 58),
                Location = new Point(35, 10),
                AutoSize = true
            };

            var datesLayout = new TableLayoutPanel
            {
                Location = new Point(10, 35),
                Size = new Size(390, 175),
                ColumnCount = 2,
                RowCount = 5
            };

            datesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            datesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            datesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            datesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            datesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            datesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            datesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));

            var lblProdukcja = new Label
            {
                Text = "Data produkcji",
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
                Text = "Data odbioru",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(31, 41, 55),
                AutoSize = true,
                Padding = new Padding(0, 0, 0, 3)
            };

            lblGodzinaLabel = new Label
            {
                Text = "Godzina przyjazdu",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(31, 41, 55),
                AutoSize = true,
                Padding = new Padding(5, 0, 0, 3)
            };

            if (dateTimePickerSprzedaz != null)
            {
                dateTimePickerSprzedaz.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                dateTimePickerSprzedaz.Margin = new Padding(0, 0, 5, 5);
                dateTimePickerSprzedaz.Width = 140;
            }

            if (dateTimePickerGodzinaPrzyjazdu != null)
            {
                dateTimePickerGodzinaPrzyjazdu.Format = DateTimePickerFormat.Custom;
                dateTimePickerGodzinaPrzyjazdu.CustomFormat = "HH:mm";
                dateTimePickerGodzinaPrzyjazdu.ShowUpDown = true;
                dateTimePickerGodzinaPrzyjazdu.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                dateTimePickerGodzinaPrzyjazdu.Margin = new Padding(5, 0, 0, 5);
                dateTimePickerGodzinaPrzyjazdu.Width = 140;
            }

            if (chkWlasnyOdbior != null)
            {
                if (chkWlasnyOdbior.Parent != null)
                {
                    chkWlasnyOdbior.Parent.Controls.Remove(chkWlasnyOdbior);
                }
                chkWlasnyOdbior.Dock = DockStyle.None;
                chkWlasnyOdbior.AutoSize = true;
                chkWlasnyOdbior.Font = new Font("Segoe UI", 8f);
                chkWlasnyOdbior.Margin = new Padding(0);
            }

            panelSugestieGodzin = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = false,
                Padding = new Padding(0, 2, 0, 0)
            };

            datesLayout.Controls.Add(lblProdukcja, 0, 0);
            datesLayout.Controls.Add(dateTimePickerProdukcji, 0, 1);
            datesLayout.SetColumnSpan(dateTimePickerProdukcji, 2);

            datesLayout.Controls.Add(lblDate, 0, 2);
            datesLayout.Controls.Add(lblGodzinaLabel, 1, 2);

            datesLayout.Controls.Add(dateTimePickerSprzedaz, 0, 3);
            datesLayout.Controls.Add(dateTimePickerGodzinaPrzyjazdu, 1, 3);

            if (chkWlasnyOdbior != null)
            {
                datesLayout.Controls.Add(chkWlasnyOdbior, 0, 4);
            }

            datesLayout.Controls.Add(panelSugestieGodzin, 1, 4);

            panelTransport = new Panel
            {
                Location = new Point(10, 220),
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

            var solowkaPanel = CreateBetterProgressPanel("Solówka 18 palet", 18, out progressSolowka, out lblSolowkaInfo);
            var tirPanel = CreateBetterProgressPanel("TIR 33 palety", 33, out progressTir, out lblTirInfo);

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
                using var brush = new SolidBrush(Color.FromArgb(240, 247, 237));
                e.Graphics.FillPath(brush, path);
                using var pen = new Pen(Color.FromArgb(92, 138, 58), 1);
                e.Graphics.DrawPath(pen, path);

                using var iconBrush = new SolidBrush(Color.FromArgb(92, 138, 58));
                e.Graphics.FillRectangle(iconBrush, 14, 16, 13, 8);
                e.Graphics.FillRectangle(iconBrush, 18, 11, 5, 5);
                e.Graphics.FillEllipse(iconBrush, 15, 23, 4, 4);
                e.Graphics.FillEllipse(iconBrush, 24, 23, 4, 4);
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
                Height = 85
            };

            var lblHeader = new Label
            {
                Text = "PŁATNOŚCI",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(92, 138, 58),
                Location = new Point(35, 8),
                AutoSize = true
            };

            lblLimitInfo = new Label
            {
                Text = "Wybierz odbiorcę",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(107, 114, 128),
                Location = new Point(12, 32),
                Size = new Size(370, 18)
            };

            progressLimit = new ProgressBar
            {
                Location = new Point(12, 53),
                Size = new Size(370, 18),
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
                using var pen = new Pen(Color.FromArgb(92, 138, 58), 1);
                e.Graphics.DrawPath(pen, path);

                using var iconPen = new Pen(Color.FromArgb(92, 138, 58), 1.5f);
                e.Graphics.DrawEllipse(iconPen, 14, 9, 12, 12);
                e.Graphics.DrawString("$", new Font("Arial", 8f, FontStyle.Bold), iconPen.Brush, 17, 11);
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

                lblLimitInfo.Location = new Point(12, 32);
                lblLimitInfo.Size = new Size(370, 18);
                lblLimitInfo.TextAlign = ContentAlignment.MiddleLeft;

                if (_limitKredytowy > 0)
                {
                    decimal procentWykorzystania = (_doZaplacenia / _limitKredytowy) * 100;
                    progressLimit.Value = Math.Min((int)procentWykorzystania, 100);

                    lblLimitInfo.Text = $"Wykorzystano: {_doZaplacenia:N0} zł / {_limitKredytowy:N0} zł (dostępne: {(_limitKredytowy - _doZaplacenia):N0} zł)";

                    decimal dostepny = _limitKredytowy - _doZaplacenia;

                    if (dostepny < 0)
                    {
                        lblLimitInfo.ForeColor = Color.FromArgb(220, 38, 38);
                        lblLimitInfo.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
                        progressLimit.ForeColor = Color.FromArgb(220, 38, 38);
                        panelPlatnosci.BackColor = Color.FromArgb(254, 242, 242);
                    }
                    else if (procentWykorzystania > 80)
                    {
                        lblLimitInfo.ForeColor = Color.FromArgb(217, 119, 6);
                        lblLimitInfo.Font = new Font("Segoe UI", 8f);
                        progressLimit.ForeColor = Color.FromArgb(251, 191, 36);
                        panelPlatnosci.BackColor = Color.FromArgb(255, 251, 235);
                    }
                    else
                    {
                        lblLimitInfo.ForeColor = Color.FromArgb(92, 138, 58);
                        lblLimitInfo.Font = new Font("Segoe UI", 8f);
                        progressLimit.ForeColor = Color.FromArgb(92, 138, 58);
                        panelPlatnosci.BackColor = Color.White;
                    }
                }
                else
                {
                    lblLimitInfo.Text = $"Zadłużenie: {_doZaplacenia:N0} zł (brak limitu kredytowego)";
                    lblLimitInfo.ForeColor = Color.FromArgb(107, 114, 128);
                    progressLimit.Value = 0;
                    panelPlatnosci.BackColor = Color.White;
                }
            }
            catch
            {
                lblLimitInfo.Text = "Błąd pobierania danych płatności";
                lblLimitInfo.ForeColor = Color.FromArgb(107, 114, 128);
                progressLimit.Value = 0;
            }
        }

        #endregion

        #region Inicjalizacja i Ustawienia UI

        private void BuildDataTableSchema()
        {
            _dt.Columns.Add("Id", typeof(int));
            _dt.Columns.Add("Kod", typeof(string));
            _dt.Columns.Add("Katalog", typeof(string));
            _dt.Columns.Add("E2", typeof(bool));
            _dt.Columns.Add("Folia", typeof(bool));
            _dt.Columns.Add("Hallal", typeof(bool));
            _dt.Columns.Add("Strefa", typeof(bool));
            _dt.Columns.Add("Palety", typeof(decimal));
            _dt.Columns.Add("Pojemniki", typeof(decimal));
            _dt.Columns.Add("Ilosc", typeof(decimal));

            // STRING, nie DECIMAL - bo w bazie to VARCHAR(20)
            var cenaColumn = _dt.Columns.Add("Cena", typeof(string));
            cenaColumn.AllowDBNull = true;

            _dt.Columns.Add("KodTowaru", typeof(string));
            _dt.Columns.Add("KodKopia", typeof(string));
            _dt.Columns.Add("MaWartosci", typeof(int));

            _view = new DataView(_dt);
            _view.RowFilter = $"Katalog = '{_aktywnyKatalog}'";
            _view.Sort = "MaWartosci ASC, Kod ASC";
            dataGridViewZamowienie.DataSource = _view;

            dataGridViewZamowienie.Columns["Id"]!.Visible = false;
            dataGridViewZamowienie.Columns["KodTowaru"]!.Visible = false;
            dataGridViewZamowienie.Columns["Katalog"]!.Visible = false;
            dataGridViewZamowienie.Columns["MaWartosci"]!.Visible = false;

            var cKod = dataGridViewZamowienie.Columns["Kod"]!;
            cKod.ReadOnly = true;
            cKod.FillWeight = 180;
            cKod.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            cKod.DefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f);
            cKod.DefaultCellStyle.ForeColor = Color.FromArgb(31, 41, 55);
            cKod.HeaderText = "Towar";

            var cE2 = dataGridViewZamowienie.Columns["E2"] as DataGridViewCheckBoxColumn;
            if (cE2 != null)
            {
                cE2.HeaderText = "40 E2";
                cE2.Width = 55;
                cE2.MinimumWidth = 55;
                cE2.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                cE2.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                cE2.DefaultCellStyle.Padding = new Padding(3);
                cE2.DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242);
                cE2.ToolTipText = "40 pojemników/paletę";
                cE2.Resizable = DataGridViewTriState.False;
            }

            var cFolia = dataGridViewZamowienie.Columns["Folia"] as DataGridViewCheckBoxColumn;
            if (cFolia != null)
            {
                cFolia.HeaderText = "Folia";
                cFolia.Width = 55;
                cFolia.MinimumWidth = 55;
                cFolia.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                cFolia.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                cFolia.DefaultCellStyle.Padding = new Padding(3);
                cFolia.DefaultCellStyle.BackColor = Color.FromArgb(240, 247, 237);
                cFolia.ToolTipText = "Zapakowane w folię";
                cFolia.Resizable = DataGridViewTriState.False;
            }

            var cHallal = dataGridViewZamowienie.Columns["Hallal"] as DataGridViewCheckBoxColumn;
            if (cHallal != null)
            {
                cHallal.HeaderText = "🔪 Hallal";
                cHallal.Width = 65;
                cHallal.MinimumWidth = 65;
                cHallal.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                cHallal.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                cHallal.DefaultCellStyle.Padding = new Padding(3);
                cHallal.DefaultCellStyle.BackColor = Color.FromArgb(240, 255, 240);
                cHallal.ToolTipText = "Cięty zgodnie z Hallal";
                cHallal.Resizable = DataGridViewTriState.False;
            }

            var cStrefa = dataGridViewZamowienie.Columns["Strefa"] as DataGridViewCheckBoxColumn;
            if (cStrefa != null)
            {
                cStrefa.HeaderText = "⚠️ Strefa";
                cStrefa.Width = 65;
                cStrefa.MinimumWidth = 65;
                cStrefa.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                cStrefa.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                cStrefa.DefaultCellStyle.Padding = new Padding(3);
                cStrefa.DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 238);
                cStrefa.ToolTipText = "Strefa ptasiej grypy / pomoru";
                cStrefa.Resizable = DataGridViewTriState.False;
            }

            var cPalety = dataGridViewZamowienie.Columns["Palety"]!;
            cPalety.HeaderText = "Palety";
            cPalety.FillWeight = 75;
            cPalety.DefaultCellStyle.Format = "N0";
            cPalety.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            cPalety.DefaultCellStyle.Font = new Font("Segoe UI Semibold", 10.5f);
            cPalety.DefaultCellStyle.ForeColor = Color.FromArgb(239, 68, 68);

            var cPojemniki = dataGridViewZamowienie.Columns["Pojemniki"]!;
            cPojemniki.HeaderText = "Pojemniki";
            cPojemniki.FillWeight = 95;
            cPojemniki.DefaultCellStyle.Format = "N0";
            cPojemniki.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            cPojemniki.DefaultCellStyle.ForeColor = Color.FromArgb(92, 138, 58);

            var cIlosc = dataGridViewZamowienie.Columns["Ilosc"]!;
            cIlosc.FillWeight = 105;
            cIlosc.DefaultCellStyle.Format = "N0";
            cIlosc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            cIlosc.HeaderText = "Ilość (kg)";
            cIlosc.DefaultCellStyle.Font = new Font("Segoe UI Semibold", 10.5f);
            cIlosc.DefaultCellStyle.ForeColor = Color.FromArgb(92, 138, 58);

            var cCena = dataGridViewZamowienie.Columns["Cena"]!;
            cCena.HeaderText = "Cena";
            cCena.FillWeight = 80;
            cCena.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            cCena.DefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            cCena.DefaultCellStyle.ForeColor = Color.FromArgb(41, 128, 185);
            cCena.DefaultCellStyle.NullValue = "";

            var cKodKopia = dataGridViewZamowienie.Columns["KodKopia"]!;
            cKodKopia.ReadOnly = true;
            cKodKopia.FillWeight = 165;
            cKodKopia.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            cKodKopia.DefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f);
            cKodKopia.DefaultCellStyle.ForeColor = Color.FromArgb(31, 41, 55);
            cKodKopia.DefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
            cKodKopia.HeaderText = "Towar";

            // Event CellClick dla podwójnego kliknięcia na Tuszkę
            dataGridViewZamowienie.CellDoubleClick += DataGridViewZamowienie_CellDoubleClick;
        }

        private void DataGridViewZamowienie_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = (dataGridViewZamowienie.Rows[e.RowIndex].DataBoundItem as DataRowView)?.Row;
            if (row == null) return;

            string kodTowaru = row.Field<string>("Kod") ?? "";
            bool isTuszka = kodTowaru.ToUpper().Contains("KURCZAK A") || kodTowaru.ToUpper().Contains("TUSZKA");
            decimal ilosc = row.Field<decimal?>("Ilosc") ?? 0;

            if (isTuszka && ilosc > 0)
            {
                PokazDialogKlasWagowych(ilosc);
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

                    case "Cena":
                        var cellValue = dataGridViewZamowienie.Rows[e.RowIndex].Cells["Cena"].Value;

                        if (cellValue == null || cellValue == DBNull.Value)
                        {
                            row["Cena"] = DBNull.Value;
                        }
                        else
                        {
                            string cenaText = cellValue.ToString()?.Trim() ?? "";

                            if (string.IsNullOrWhiteSpace(cenaText))
                            {
                                row["Cena"] = DBNull.Value;
                            }
                            else
                            {
                                cenaText = cenaText.Replace("zł", "").Replace("PLN", "").Trim();

                                if (decimal.TryParse(cenaText, NumberStyles.Any, _pl, out decimal cenaValue))
                                {
                                    // Zapisz jako STRING (bo w bazie to VARCHAR)
                                    row["Cena"] = cenaValue > 0 ? cenaValue.ToString("F2", _pl) : DBNull.Value;
                                }
                                else
                                {
                                    row["Cena"] = DBNull.Value;
                                }
                            }
                        }
                        break;
                }

                decimal iloscAktualna = ParseDec(row["Ilosc"]);
                row["MaWartosci"] = (iloscAktualna > 0) ? 0 : 1;
            }
            finally
            {
                _blokujObslugeZmian = false;
            }
            RecalcSum();

            // Uruchom pulsowanie wiersza
            TriggerRowPulse(e.RowIndex);
        }

        private void TriggerRowPulse(int rowIndex)
        {
            _pulseRows[rowIndex] = 6; // 6 kroków animacji (3 fade-in + 3 fade-out)

            if (_pulseTimer == null)
            {
                _pulseTimer = new System.Windows.Forms.Timer { Interval = 80 };
                _pulseTimer.Tick += (s, e) =>
                {
                    var finished = new List<int>();
                    foreach (var kvp in _pulseRows)
                    {
                        _pulseRows[kvp.Key] = kvp.Value - 1;
                        if (_pulseRows[kvp.Key] <= 0) finished.Add(kvp.Key);
                    }
                    foreach (var idx in finished) _pulseRows.Remove(idx);

                    dataGridViewZamowienie.InvalidateRow(dataGridViewZamowienie.Rows.Count > 0 ? Math.Min(rowIndex, dataGridViewZamowienie.Rows.Count - 1) : 0);

                    if (_pulseRows.Count == 0) _pulseTimer.Stop();
                };
            }

            if (!_pulseTimer.Enabled) _pulseTimer.Start();
        }
        #endregion

        #region Zapis i Odczyt Zamówienia

        private async Task LoadZamowienieAsync(int id)
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            bool dataProdukcjiExists = false;
            bool walutaExists = false;
            bool strefaExists = false;
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

            try
            {
                await using var cmdCheckWaluta = new SqlCommand(@"
            SELECT COUNT(*)
            FROM sys.columns
            WHERE object_id = OBJECT_ID(N'[dbo].[ZamowieniaMieso]')
            AND name = 'Waluta'", cn);
                int countWaluta = (int)await cmdCheckWaluta.ExecuteScalarAsync();
                walutaExists = countWaluta > 0;
            }
            catch { }

            string walutaSelect = walutaExists ? ", ISNULL(Waluta, 'PLN') as Waluta" : "";
            string sqlQuery = dataProdukcjiExists
                ? $"SELECT DataZamowienia, KlientId, Uwagi, DataPrzyjazdu, DataProdukcji, ISNULL(TransportStatus, 'Oczekuje') as TransportStatus{walutaSelect} FROM [dbo].[ZamowieniaMieso] WHERE Id=@Id"
                : $"SELECT DataZamowienia, KlientId, Uwagi, DataPrzyjazdu, ISNULL(TransportStatus, 'Oczekuje') as TransportStatus{walutaSelect} FROM [dbo].[ZamowieniaMieso] WHERE Id=@Id";

            // Inicjalizacja śledzenia zmian
            _oryginalneWartosci = new OryginalneWartosciZamowienia();

            await using (var cmdZ = new SqlCommand(sqlQuery, cn))
            {
                cmdZ.Parameters.AddWithValue("@Id", id);
                await using var rd = await cmdZ.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    var dataZamowienia = rd.GetDateTime(0);
                    var klientId = rd.GetInt32(1);
                    var uwagi = await rd.IsDBNullAsync(2) ? "" : rd.GetString(2);
                    var dataPrzyjazdu = rd.GetDateTime(3);
                    DateTime? dataProdukcji = null;
                    string transportStatus = "Oczekuje";

                    dateTimePickerSprzedaz.Value = dataZamowienia;
                    UstawOdbiorce(klientId.ToString());
                    textBoxUwagi.Text = uwagi;
                    dateTimePickerGodzinaPrzyjazdu.Value = dataPrzyjazdu;

                    if (dateTimePickerProdukcji != null)
                    {
                        if (dataProdukcjiExists && rd.FieldCount > 4 && !await rd.IsDBNullAsync(4))
                        {
                            dataProdukcji = rd.GetDateTime(4);
                            dateTimePickerProdukcji.Value = dataProdukcji.Value;
                        }
                        else
                        {
                            dateTimePickerProdukcji.Value = DateTime.Today;
                        }
                    }

                    // Wczytanie statusu transportu i ustawienie checkboxa
                    if (chkWlasnyOdbior != null)
                    {
                        int transportStatusIndex = dataProdukcjiExists ? 5 : 4;
                        if (rd.FieldCount > transportStatusIndex && !await rd.IsDBNullAsync(transportStatusIndex))
                        {
                            transportStatus = rd.GetString(transportStatusIndex);
                            chkWlasnyOdbior.Checked = (transportStatus == "Wlasny");
                        }
                        else
                        {
                            chkWlasnyOdbior.Checked = false;
                        }
                    }

                    // Wczytanie waluty
                    if (cbWaluta != null && walutaExists)
                    {
                        int walutaIndex = dataProdukcjiExists ? 6 : 5;
                        if (rd.FieldCount > walutaIndex && !await rd.IsDBNullAsync(walutaIndex))
                        {
                            string waluta = rd.GetString(walutaIndex);
                            cbWaluta.SelectedItem = waluta;
                        }
                        else
                        {
                            cbWaluta.SelectedIndex = 0; // PLN
                        }
                    }

                    // Zapisz oryginalne wartości do śledzenia zmian
                    _oryginalneWartosci.DataZamowienia = dataZamowienia;
                    _oryginalneWartosci.DataPrzyjazdu = dataPrzyjazdu;
                    _oryginalneWartosci.DataProdukcji = dataProdukcji;
                    _oryginalneWartosci.KlientId = klientId;
                    _oryginalneWartosci.KlientNazwa = _kontrahenci.FirstOrDefault(k => k.Id == klientId.ToString())?.Nazwa;
                    _oryginalneWartosci.Uwagi = uwagi;
                    _oryginalneWartosci.TransportStatus = transportStatus;
                }
            }

            _blokujObslugeZmian = true;
            foreach (DataRow r in _dt.Rows)
            {
                r["E2"] = false;
                r["Folia"] = false;
                r["Hallal"] = false;
                r["Strefa"] = false;
                r["Ilosc"] = 0m;
                r["Pojemniki"] = 0m;
                r["Palety"] = 0m;
                r["Cena"] = DBNull.Value;  // NULL
                r["MaWartosci"] = 1;
            }

            bool strefaTowarExists = await CheckStrefaTowarColumnExists(cn);
            var zamowienieTowary = new List<(int KodTowaru, decimal Ilosc, int Pojemniki, decimal Palety, bool E2, bool Folia, bool Hallal, string Cena, bool Strefa)>();

            string strefaCol = strefaTowarExists ? ", ISNULL(zmt.Strefa, 0) as Strefa" : "";
            await using (var cmdT = new SqlCommand($@"
        SELECT zmt.KodTowaru, zmt.Ilosc, ISNULL(zmt.Pojemniki, 0) as Pojemniki,
               ISNULL(zmt.Palety, 0) as Palety, ISNULL(zmt.E2, 0) as E2,
               ISNULL(zmt.Folia, 0) as Folia, ISNULL(zmt.Hallal, 0) as Hallal,
               ISNULL(zmt.Cena, '0') as Cena{strefaCol}
        FROM [dbo].[ZamowieniaMiesoTowar] zmt
        WHERE zmt.ZamowienieId=@Id", cn))
            {
                cmdT.Parameters.AddWithValue("@Id", id);
                await using var rd = await cmdT.ExecuteReaderAsync();

                while (await rd.ReadAsync())
                {
                    int kodTowaru = rd.GetInt32(0);
                    decimal ilosc = await rd.IsDBNullAsync(1) ? 0m : rd.GetDecimal(1);
                    int pojemniki = rd.GetInt32(2);
                    decimal palety = rd.GetDecimal(3);
                    bool e2 = rd.GetBoolean(4);
                    bool folia = rd.GetBoolean(5);
                    bool hallal = rd.GetBoolean(6);
                    string cena = rd.GetString(7);
                    bool strefa = strefaTowarExists ? rd.GetBoolean(8) : false;

                    zamowienieTowary.Add((kodTowaru, ilosc, pojemniki, palety, e2, folia, hallal, cena, strefa));

                    // Zapisz oryginalne wartości produktu do śledzenia zmian
                    if (_oryginalneWartosci != null && ilosc > 0)
                    {
                        // Pobierz nazwę/kod towaru z _dt
                        string nazwaTowaru = $"Towar #{kodTowaru}";
                        var towarRows = _dt.Select($"Id = {kodTowaru}");
                        if (towarRows.Any())
                        {
                            nazwaTowaru = towarRows[0].Field<string>("Kod") ?? nazwaTowaru;
                        }

                        _oryginalneWartosci.Towary[kodTowaru] = new OryginalnyTowar
                        {
                            Nazwa = nazwaTowaru,
                            Ilosc = ilosc,
                            Cena = cena,
                            E2 = e2,
                            Folia = folia,
                            Hallal = hallal,
                            Pojemniki = pojemniki,
                            Palety = palety
                        };
                    }
                }
            }

            bool czySaMrozonki = false;

            foreach (var towar in zamowienieTowary)
            {
                var rows = _dt.Select($"Id = {towar.KodTowaru}");
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
                    row["Hallal"] = towar.Hallal;
                    row["Strefa"] = towar.Strefa;

                    // STRING - zapisz jako string lub NULL
                    if (!string.IsNullOrWhiteSpace(towar.Cena) &&
                        decimal.TryParse(towar.Cena, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal cenaVal) &&
                        cenaVal > 0)
                    {
                        row["Cena"] = cenaVal.ToString("F2", _pl);
                    }
                    else
                    {
                        row["Cena"] = DBNull.Value;
                    }

                    row["MaWartosci"] = (towar.Ilosc > 0) ? 0 : 1;
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

            // Wczytaj rezerwacje klas - najpierw z tabeli w bazie
            _rozkladKlasKurczakA = await RezerwacjeKlasManager.PobierzRezerwacjeZamowieniaAsync(_connLibra, id);
            
            // Jeśli nie ma w tabeli, spróbuj sparsować z notatki (kompatybilność wsteczna)
            if (_rozkladKlasKurczakA == null)
            {
                _rozkladKlasKurczakA = ParseRozkladZNotatki(textBoxUwagi.Text);
            }

            // Aktualizuj pasek klas wagowych
            AktualizujPasekKlasWagowych();

            _blokujObslugeZmian = false;
            RecalcSum();
        }
        private async void btnZapisz_Click(object? sender, EventArgs e)
        {
            if (!ValidateBeforeSave(out var msg))
            {
                ShowWarning(msg, "Błąd danych");
                return;
            }

            decimal sumaPaletCalkowita = 0m;
            foreach (DataRow r in _dt.Rows)
            {
                sumaPaletCalkowita += r.Field<decimal?>("Palety") ?? 0m;
            }

            if (sumaPaletCalkowita > LIMIT_PALET_OSTRZEZENIE)
            {
                var result = ShowWarningQuestion(
                    $"Łączna liczba palet ({sumaPaletCalkowita:N1}) przekracza limit TIR ({LIMIT_PALET_OSTRZEZENIE}).\n\n" +
                    "Czy na pewno chcesz zapisać to zamówienie?",
                    "Uwaga - przekroczenie limitu");

                if (result == DialogResult.No) return;
            }

            if (!_idZamowieniaDoEdycji.HasValue && !string.IsNullOrEmpty(_selectedKlientId))
            {
                DateTime dataProdukcji = dateTimePickerProdukcji?.Value.Date ?? DateTime.Today;
                var existingOrder = await CheckForExistingOrder(_selectedKlientId, dataProdukcji);

                if (existingOrder != null)
                {
                    var odbiorcaNazwa = _kontrahenci.FirstOrDefault(k => k.Id == _selectedKlientId)?.Nazwa ?? "Nieznany";

                    using var duplicateDialog = new DuplicateOrderDialog(odbiorcaNazwa, dataProdukcji, existingOrder);
                    if (duplicateDialog.ShowDialog(this) != System.Windows.Forms.DialogResult.OK || !duplicateDialog.ProceedWithSave)
                    {
                        return;
                    }
                }
            }

            Cursor = Cursors.WaitCursor;
            btnZapisz.Enabled = false;

            try
            {
                await SaveOrderAsync();
                string summary = BuildOrderSummary();
                bool isEdit = _idZamowieniaDoEdycji.HasValue;

                // Zbuduj mapę kod→zdjęcie dla podsumowania
                var kodToImage = new Dictionary<string, Image>();
                foreach (DataRow r in _dt.Rows)
                {
                    int id = r.Field<int>("Id");
                    string kod = r.Field<string>("Kod") ?? "";
                    if (_productImages.TryGetValue(id, out var img) && img != null && !kodToImage.ContainsKey(kod))
                        kodToImage[kod] = img;
                }

                using var afterSaveDialog = new AfterSaveDialog(summary, isEdit, kodToImage);
                afterSaveDialog.ShowDialog(this);

                if (afterSaveDialog.CreateAnother)
                {
                    ClearFormForNewOrder();
                }
                else
                {
                    this.DialogResult = System.Windows.Forms.DialogResult.OK;
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                ShowError("Błąd zapisu: " + ex.Message);
            }
            finally
            {
                Cursor = Cursors.Default;
                btnZapisz.Enabled = true;
            }
        }
        private void DataGridViewZamowienie_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.RowIndex < 0) return;

            var columnName = dataGridViewZamowienie.Columns[e.ColumnIndex].Name;

            if (columnName == "Cena")
            {
                if (e.Value == null || e.Value == DBNull.Value)
                {
                    e.Value = "";
                    e.FormattingApplied = true;
                }
                else if (e.Value is string cenaStr)
                {
                    if (string.IsNullOrWhiteSpace(cenaStr))
                    {
                        e.Value = "";
                        e.FormattingApplied = true;
                    }
                    else if (decimal.TryParse(cenaStr, NumberStyles.Any, _pl, out decimal cenaValue))
                    {
                        if (cenaValue == 0m)
                        {
                            e.Value = "";
                            e.FormattingApplied = true;
                        }
                        else
                        {
                            e.Value = cenaValue.ToString("N2", _pl);
                            e.FormattingApplied = true;
                        }
                    }
                }
            }
        }
        private async Task SaveOrderAsync()
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            bool dataProdukcjiExists = await CheckIfColumnExists(cn, "DataProdukcji");
            bool dataUbojuExists = await CheckIfColumnExists(cn, "DataUboju");
            bool walutaExists = await CheckIfColumnExists(cn, "Waluta");
            bool strefaTowarExists = await CheckStrefaTowarColumnExists(cn);
            bool czyModMagazynuExists = await EnsureColumnExists(cn, "CzyZmodyfikowaneDlaMagazynu", "BIT NULL DEFAULT 0");
            bool czyModProdukcjiExists = await EnsureColumnExists(cn, "CzyZmodyfikowaneDlaProdukcji", "BIT NULL DEFAULT 0");

            await using var tr = (SqlTransaction)await cn.BeginTransactionAsync();

            int orderId;
            decimal sumaPojemnikow = 0;
            decimal sumaPalet = 0;
            bool czyJakikolwiekE2 = false;

            foreach (DataRow r in _dt.Rows)
            {
                if ((r.Field<decimal?>("Ilosc") ?? 0m) > 0m)
                {
                    sumaPojemnikow += r.Field<decimal?>("Pojemniki") ?? 0m;
                    sumaPalet += r.Field<decimal?>("Palety") ?? 0m;
                    if (r.Field<bool>("E2")) czyJakikolwiekE2 = true;
                }
            }

            DateTime dataProdukcji = dateTimePickerProdukcji?.Value.Date ?? DateTime.Today;
            string transportStatus = (chkWlasnyOdbior?.Checked == true) ? "Wlasny" : "Oczekuje";

            if (_idZamowieniaDoEdycji.HasValue)
            {
                orderId = _idZamowieniaDoEdycji.Value;

                string updateSql = @"UPDATE [dbo].[ZamowieniaMieso] SET
            DataZamowienia = @dz, DataPrzyjazdu = @dp, KlientId = @kid, Uwagi = @uw,
            KtoMod = @km, KiedyMod = SYSDATETIME(), LiczbaPojemnikow = @poj,
            LiczbaPalet = @pal, TrybE2 = @e2, TransportStatus = @ts,
            CzyZmodyfikowaneDlaFaktur = 1, DataOstatniejModyfikacji = SYSDATETIME(), ModyfikowalPrzez = @fullName";

                if (dataProdukcjiExists) updateSql += ", DataProdukcji = @dprod";
                if (dataUbojuExists) updateSql += ", DataUboju = @duboj";
                if (walutaExists) updateSql += ", Waluta = @waluta";
                if (czyModMagazynuExists) updateSql += ", CzyZmodyfikowaneDlaMagazynu = 1";
                if (czyModProdukcjiExists) updateSql += ", CzyZmodyfikowaneDlaProdukcji = 1";

                updateSql += " WHERE Id = @id";

                var cmdUpdate = new SqlCommand(updateSql, cn, tr);
                cmdUpdate.Parameters.AddWithValue("@dz", dateTimePickerSprzedaz.Value.Date);
                var dataPrzyjazdu = dateTimePickerSprzedaz.Value.Date.Add(dateTimePickerGodzinaPrzyjazdu.Value.TimeOfDay);
                cmdUpdate.Parameters.AddWithValue("@dp", dataPrzyjazdu);

                if (dataProdukcjiExists) cmdUpdate.Parameters.AddWithValue("@dprod", dataProdukcji);
                if (dataUbojuExists) cmdUpdate.Parameters.AddWithValue("@duboj", dataProdukcji);

                cmdUpdate.Parameters.AddWithValue("@kid", int.Parse(_selectedKlientId!));
                cmdUpdate.Parameters.AddWithValue("@uw", string.IsNullOrWhiteSpace(textBoxUwagi.Text) ? (object)DBNull.Value : textBoxUwagi.Text);
                cmdUpdate.Parameters.AddWithValue("@km", UserID);
                cmdUpdate.Parameters.AddWithValue("@fullName", App.UserFullName ?? UserID);
                cmdUpdate.Parameters.AddWithValue("@id", orderId);
                cmdUpdate.Parameters.AddWithValue("@poj", (int)Math.Round(sumaPojemnikow));
                cmdUpdate.Parameters.AddWithValue("@pal", sumaPalet);
                cmdUpdate.Parameters.AddWithValue("@e2", czyJakikolwiekE2);
                cmdUpdate.Parameters.AddWithValue("@ts", transportStatus);
                if (walutaExists) cmdUpdate.Parameters.AddWithValue("@waluta", cbWaluta?.SelectedItem?.ToString() ?? "PLN");

                await cmdUpdate.ExecuteNonQueryAsync();

                var cmdDelete = new SqlCommand(@"DELETE FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId = @id", cn, tr);
                cmdDelete.Parameters.AddWithValue("@id", orderId);
                await cmdDelete.ExecuteNonQueryAsync();
            }
            else
            {
                var cmdGetId = new SqlCommand(@"SELECT ISNULL(MAX(Id), 0) + 1 FROM [dbo].[ZamowieniaMieso]", cn, tr);
                orderId = Convert.ToInt32(await cmdGetId.ExecuteScalarAsync());

                string insertColumns = "Id, DataZamowienia, DataPrzyjazdu, KlientId, Uwagi, IdUser, DataUtworzenia, LiczbaPojemnikow, LiczbaPalet, TrybE2, TransportStatus";
                string insertValues = "@id, @dz, @dp, @kid, @uw, @u, GETDATE(), @poj, @pal, @e2, @ts";

                if (dataProdukcjiExists) { insertColumns += ", DataProdukcji"; insertValues += ", @dprod"; }
                if (dataUbojuExists) { insertColumns += ", DataUboju"; insertValues += ", @duboj"; }
                if (walutaExists) { insertColumns += ", Waluta"; insertValues += ", @waluta"; }

                string insertSql = $@"INSERT INTO [dbo].[ZamowieniaMieso] ({insertColumns}) VALUES ({insertValues})";

                var cmdInsert = new SqlCommand(insertSql, cn, tr);
                cmdInsert.Parameters.AddWithValue("@id", orderId);
                cmdInsert.Parameters.AddWithValue("@dz", dateTimePickerSprzedaz.Value.Date);
                var dataPrzyjazdu = dateTimePickerSprzedaz.Value.Date.Add(dateTimePickerGodzinaPrzyjazdu.Value.TimeOfDay);
                cmdInsert.Parameters.AddWithValue("@dp", dataPrzyjazdu);

                if (dataProdukcjiExists) cmdInsert.Parameters.AddWithValue("@dprod", dataProdukcji);
                if (dataUbojuExists) cmdInsert.Parameters.AddWithValue("@duboj", dataProdukcji);

                cmdInsert.Parameters.AddWithValue("@kid", int.Parse(_selectedKlientId!));
                cmdInsert.Parameters.AddWithValue("@uw", string.IsNullOrWhiteSpace(textBoxUwagi.Text) ? (object)DBNull.Value : textBoxUwagi.Text);
                cmdInsert.Parameters.AddWithValue("@u", UserID);
                cmdInsert.Parameters.AddWithValue("@poj", (int)Math.Round(sumaPojemnikow));
                cmdInsert.Parameters.AddWithValue("@pal", sumaPalet);
                cmdInsert.Parameters.AddWithValue("@e2", czyJakikolwiekE2);
                cmdInsert.Parameters.AddWithValue("@ts", transportStatus);
                if (walutaExists) cmdInsert.Parameters.AddWithValue("@waluta", cbWaluta?.SelectedItem?.ToString() ?? "PLN");

                await cmdInsert.ExecuteNonQueryAsync();
            }

            string strefaInsCol = strefaTowarExists ? ", Strefa" : "";
            string strefaInsVal = strefaTowarExists ? ", @strefa" : "";
            var cmdInsertItem = new SqlCommand($@"INSERT INTO [dbo].[ZamowieniaMiesoTowar]
        (ZamowienieId, KodTowaru, Ilosc, Cena, Pojemniki, Palety, E2, Folia, Hallal{strefaInsCol})
        VALUES (@zid, @kt, @il, @ce, @poj, @pal, @e2, @folia, @hallal{strefaInsVal})", cn, tr);

            cmdInsertItem.Parameters.Add("@zid", SqlDbType.Int);
            cmdInsertItem.Parameters.Add("@kt", SqlDbType.Int);
            cmdInsertItem.Parameters.Add("@il", SqlDbType.Decimal);
            cmdInsertItem.Parameters.Add("@ce", SqlDbType.VarChar, 20);  // VARCHAR!
            cmdInsertItem.Parameters.Add("@poj", SqlDbType.Int);
            cmdInsertItem.Parameters.Add("@pal", SqlDbType.Decimal);
            cmdInsertItem.Parameters.Add("@e2", SqlDbType.Bit);
            cmdInsertItem.Parameters.Add("@folia", SqlDbType.Bit);
            cmdInsertItem.Parameters.Add("@hallal", SqlDbType.Bit);
            if (strefaTowarExists) cmdInsertItem.Parameters.Add("@strefa", SqlDbType.Bit);

            foreach (DataRow r in _dt.Rows)
            {
                if ((r.Field<decimal?>("Ilosc") ?? 0m) <= 0m) continue;

                cmdInsertItem.Parameters["@zid"].Value = orderId;
                cmdInsertItem.Parameters["@kt"].Value = r.Field<int>("Id");
                cmdInsertItem.Parameters["@il"].Value = r.Field<decimal?>("Ilosc") ?? 0m;

                // OBSŁUGA CENY - STRING (VARCHAR w bazie)
                if (r.IsNull("Cena"))
                {
                    cmdInsertItem.Parameters["@ce"].Value = "0";
                }
                else
                {
                    string cenaStr = r.Field<string>("Cena") ?? "";
                    if (string.IsNullOrWhiteSpace(cenaStr) || !decimal.TryParse(cenaStr, NumberStyles.Any, _pl, out decimal cenaVal) || cenaVal <= 0)
                    {
                        cmdInsertItem.Parameters["@ce"].Value = "0";
                    }
                    else
                    {
                        cmdInsertItem.Parameters["@ce"].Value = cenaVal.ToString("F2", CultureInfo.InvariantCulture);
                    }
                }

                cmdInsertItem.Parameters["@poj"].Value = (int)Math.Round(r.Field<decimal?>("Pojemniki") ?? 0m);
                cmdInsertItem.Parameters["@pal"].Value = r.Field<decimal?>("Palety") ?? 0m;
                cmdInsertItem.Parameters["@e2"].Value = r.Field<bool>("E2");
                cmdInsertItem.Parameters["@folia"].Value = r.Field<bool>("Folia");
                cmdInsertItem.Parameters["@hallal"].Value = r.Field<bool>("Hallal");
                if (strefaTowarExists) cmdInsertItem.Parameters["@strefa"].Value = r.Field<bool>("Strefa");

                await cmdInsertItem.ExecuteNonQueryAsync();
            }

            await tr.CommitAsync();

            // === LOGOWANIE HISTORII ZMIAN ===
            try
            {
                var odbiorca = _kontrahenci.FirstOrDefault(k => k.Id == _selectedKlientId);
                string odbiorcaNazwaLog = odbiorca?.Nazwa ?? "Nieznany odbiorca";
                var dataPrzyjazdu = dateTimePickerSprzedaz.Value.Date.Add(dateTimePickerGodzinaPrzyjazdu.Value.TimeOfDay);

                if (_idZamowieniaDoEdycji.HasValue && _oryginalneWartosci != null)
                {
                    // Edycja istniejącego zamówienia - szczegółowe śledzenie zmian
                    await LogujSzczegoloweZmianyAsync(orderId, dataProdukcji, dataPrzyjazdu, transportStatus, odbiorcaNazwaLog);
                }
                else
                {
                    // Nowe zamówienie
                    await HistoriaZmianService.LogujUtworzenie(
                        orderId, UserID, App.UserFullName,
                        $"Utworzono zamówienie dla: {odbiorcaNazwaLog}, " +
                        $"data produkcji: {dataProdukcji:yyyy-MM-dd}, " +
                        $"godzina przyjazdu: {dataPrzyjazdu:HH:mm}, " +
                        $"pojemniki: {sumaPojemnikow:N0}, palety: {sumaPalet:N1}");
                }
            }
            catch (Exception exLog)
            {
                // Logowanie nie powinno przerywać zapisu zamówienia
                System.Diagnostics.Debug.WriteLine($"Błąd logowania historii: {exLog.Message}");
            }

            // === ZAPIS REZERWACJI KLAS WAGOWYCH ===
            // Po zapisie zamówienia zapisujemy rezerwacje do osobnej tabeli
            if (_rozkladKlasKurczakA != null && _rozkladKlasKurczakA.SumaPojemnikow > 0)
            {
                try
                {
                    // Pobierz nazwę odbiorcy
                    string? odbiorcaNazwa = null;
                    if (!string.IsNullOrEmpty(_selectedKlientId))
                    {
                        var klient = _kontrahenci.FirstOrDefault(k => k.Id == _selectedKlientId);
                        odbiorcaNazwa = klient?.Nazwa;
                    }

                    // Pobierz handlowca
                    string? handlowiec = null;
                    var cbHandlowiec = this.Controls.Find("cbHandlowiecFilter", true).FirstOrDefault() as ComboBox;
                    if (cbHandlowiec?.SelectedItem != null)
                        handlowiec = cbHandlowiec.SelectedItem.ToString();

                    await RezerwacjeKlasManager.ZapiszRezerwacjeAsync(
                        _connLibra,
                        orderId,
                        dataProdukcji,
                        _rozkladKlasKurczakA,
                        handlowiec,
                        odbiorcaNazwa);
                }
                catch (Exception exRez)
                {
                    // Nie przerywaj zapisu zamówienia z powodu błędu rezerwacji
                    System.Diagnostics.Debug.WriteLine($"Błąd zapisu rezerwacji: {exRez.Message}");
                }
            }
        }

        /// <summary>
        /// Loguje szczegółowe zmiany w zamówieniu porównując oryginalne i nowe wartości
        /// </summary>
        private async Task LogujSzczegoloweZmianyAsync(int orderId, DateTime dataProdukcji, DateTime dataPrzyjazdu,
            string transportStatus, string odbiorcaNazwa)
        {
            if (_oryginalneWartosci == null) return;

            var listaZmian = new List<string>();

            // === Porównanie podstawowych pól ===

            // Data zamówienia (sprzedaży)
            if (_oryginalneWartosci.DataZamowienia.Date != dateTimePickerSprzedaz.Value.Date)
            {
                await HistoriaZmianService.LogujEdycje(orderId, UserID, App.UserFullName,
                    "Data sprzedaży",
                    _oryginalneWartosci.DataZamowienia.ToString("yyyy-MM-dd"),
                    dateTimePickerSprzedaz.Value.ToString("yyyy-MM-dd"));
                listaZmian.Add($"Data sprzedaży: {_oryginalneWartosci.DataZamowienia:dd.MM} → {dateTimePickerSprzedaz.Value:dd.MM}");
            }

            // Godzina przyjazdu
            if (_oryginalneWartosci.DataPrzyjazdu.TimeOfDay != dataPrzyjazdu.TimeOfDay)
            {
                await HistoriaZmianService.LogujEdycje(orderId, UserID, App.UserFullName,
                    "Godzina przyjazdu",
                    _oryginalneWartosci.DataPrzyjazdu.ToString("HH:mm"),
                    dataPrzyjazdu.ToString("HH:mm"));
                listaZmian.Add($"Godzina: {_oryginalneWartosci.DataPrzyjazdu:HH:mm} → {dataPrzyjazdu:HH:mm}");
            }

            // Data produkcji
            if (dateTimePickerProdukcji != null)
            {
                DateTime nowaDataProdukcji = dateTimePickerProdukcji.Value.Date;
                if (_oryginalneWartosci.DataProdukcji.HasValue && _oryginalneWartosci.DataProdukcji.Value.Date != nowaDataProdukcji)
                {
                    await HistoriaZmianService.LogujEdycje(orderId, UserID, App.UserFullName,
                        "Data produkcji",
                        _oryginalneWartosci.DataProdukcji.Value.ToString("yyyy-MM-dd"),
                        nowaDataProdukcji.ToString("yyyy-MM-dd"));
                    listaZmian.Add($"Data produkcji: {_oryginalneWartosci.DataProdukcji.Value:dd.MM} → {nowaDataProdukcji:dd.MM}");
                }
            }

            // Zmiana odbiorcy
            int nowyKlientId = int.Parse(_selectedKlientId ?? "0");
            if (_oryginalneWartosci.KlientId != nowyKlientId)
            {
                await HistoriaZmianService.LogujEdycje(orderId, UserID, App.UserFullName,
                    "Odbiorca",
                    _oryginalneWartosci.KlientNazwa ?? _oryginalneWartosci.KlientId.ToString(),
                    odbiorcaNazwa);
                listaZmian.Add($"Odbiorca: {_oryginalneWartosci.KlientNazwa} → {odbiorcaNazwa}");
            }

            // Transport status
            if (_oryginalneWartosci.TransportStatus != transportStatus)
            {
                string staraWartoscDisplay = _oryginalneWartosci.TransportStatus == "Wlasny" ? "Własny odbiór" : "Transport firmy";
                string nowaWartoscDisplay = transportStatus == "Wlasny" ? "Własny odbiór" : "Transport firmy";
                await HistoriaZmianService.LogujEdycje(orderId, UserID, App.UserFullName,
                    "Typ transportu",
                    staraWartoscDisplay,
                    nowaWartoscDisplay);
                listaZmian.Add($"Transport: {staraWartoscDisplay} → {nowaWartoscDisplay}");
            }

            // Uwagi/notatka
            string staraNotatka = _oryginalneWartosci.Uwagi ?? "";
            string nowaNotatka = textBoxUwagi.Text ?? "";
            if (staraNotatka.Trim() != nowaNotatka.Trim())
            {
                await HistoriaZmianService.LogujZmianeNotatki(orderId, UserID, staraNotatka, nowaNotatka, App.UserFullName);
                listaZmian.Add("Zmieniono notatkę");
            }

            // === Porównanie produktów ===
            var produktyZmiany = new List<string>();
            var noweIlosciProduktow = new Dictionary<int, (string nazwa, decimal ilosc, string cena, bool e2, bool folia, bool hallal)>();

            // Zbierz aktualne wartości produktów
            foreach (DataRow r in _dt.Rows)
            {
                decimal ilosc = r.Field<decimal?>("Ilosc") ?? 0m;
                if (ilosc > 0)
                {
                    int kodTowaru = r.Field<int>("Id");
                    string nazwa = r.Field<string>("Kod") ?? $"Towar #{kodTowaru}";
                    string cena = r.IsNull("Cena") ? "" : (r.Field<string>("Cena") ?? "");
                    bool e2 = r.Field<bool>("E2");
                    bool folia = r.Field<bool>("Folia");
                    bool hallal = r.Field<bool>("Hallal");
                    noweIlosciProduktow[kodTowaru] = (nazwa, ilosc, cena, e2, folia, hallal);
                }
            }

            // Porównaj z oryginalnymi
            foreach (var orig in _oryginalneWartosci.Towary)
            {
                int kodTowaru = orig.Key;
                var origTowar = orig.Value;

                if (noweIlosciProduktow.TryGetValue(kodTowaru, out var nowy))
                {
                    // Produkt istniał i nadal istnieje - sprawdź zmiany
                    if (origTowar.Ilosc != nowy.ilosc)
                    {
                        await HistoriaZmianService.LogujEdycje(orderId, UserID, App.UserFullName,
                            $"Ilość - {origTowar.Nazwa}",
                            $"{origTowar.Ilosc:N0} kg",
                            $"{nowy.ilosc:N0} kg");
                        produktyZmiany.Add($"{origTowar.Nazwa}: {origTowar.Ilosc:N0} → {nowy.ilosc:N0} kg");
                    }

                    // Sprawdź zmianę ceny
                    string staraCena = origTowar.Cena ?? "";
                    string nowaCena = nowy.cena ?? "";
                    if (decimal.TryParse(staraCena, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal cenaStara) &&
                        decimal.TryParse(nowaCena, NumberStyles.Any, _pl, out decimal cenaNowa))
                    {
                        if (cenaStara != cenaNowa && (cenaStara > 0 || cenaNowa > 0))
                        {
                            await HistoriaZmianService.LogujEdycje(orderId, UserID, App.UserFullName,
                                $"Cena - {origTowar.Nazwa}",
                                cenaStara > 0 ? $"{cenaStara:N2} zł" : "brak",
                                cenaNowa > 0 ? $"{cenaNowa:N2} zł" : "brak");
                            produktyZmiany.Add($"Cena {origTowar.Nazwa}: {cenaStara:N2} → {cenaNowa:N2} zł");
                        }
                    }

                    // Sprawdź zmianę E2
                    if (origTowar.E2 != nowy.e2)
                    {
                        await HistoriaZmianService.LogujEdycje(orderId, UserID, App.UserFullName,
                            $"Tryb E2 - {origTowar.Nazwa}",
                            origTowar.E2 ? "TAK" : "NIE",
                            nowy.e2 ? "TAK" : "NIE");
                    }

                    // Sprawdź zmianę Folia
                    if (origTowar.Folia != nowy.folia)
                    {
                        await HistoriaZmianService.LogujEdycje(orderId, UserID, App.UserFullName,
                            $"Folia - {origTowar.Nazwa}",
                            origTowar.Folia ? "TAK" : "NIE",
                            nowy.folia ? "TAK" : "NIE");
                    }

                    // Sprawdź zmianę Hallal
                    if (origTowar.Hallal != nowy.hallal)
                    {
                        await HistoriaZmianService.LogujEdycje(orderId, UserID, App.UserFullName,
                            $"Hallal - {origTowar.Nazwa}",
                            origTowar.Hallal ? "TAK" : "NIE",
                            nowy.hallal ? "TAK" : "NIE");
                    }
                }
                else
                {
                    // Produkt został usunięty
                    await HistoriaZmianService.LogujEdycje(orderId, UserID, App.UserFullName,
                        $"Usunięto produkt",
                        $"{origTowar.Nazwa} ({origTowar.Ilosc:N0} kg)",
                        "(usunięto)");
                    produktyZmiany.Add($"Usunięto: {origTowar.Nazwa} ({origTowar.Ilosc:N0} kg)");
                }
            }

            // Sprawdź dodane produkty
            foreach (var nowy in noweIlosciProduktow)
            {
                if (!_oryginalneWartosci.Towary.ContainsKey(nowy.Key))
                {
                    await HistoriaZmianService.LogujEdycje(orderId, UserID, App.UserFullName,
                        $"Dodano produkt",
                        "(brak)",
                        $"{nowy.Value.nazwa} ({nowy.Value.ilosc:N0} kg)");
                    produktyZmiany.Add($"Dodano: {nowy.Value.nazwa} ({nowy.Value.ilosc:N0} kg)");
                }
            }

            // Jeśli były jakiekolwiek zmiany produktów, dodaj do listy
            if (produktyZmiany.Count > 0)
            {
                listaZmian.AddRange(produktyZmiany);
            }

            // Jeśli nie było żadnych szczegółowych zmian, zaloguj ogólną edycję
            if (listaZmian.Count == 0)
            {
                await HistoriaZmianService.LogujEdycje(orderId, UserID, App.UserFullName,
                    opisDodatkowy: $"Zapisano zamówienie bez wykrytych zmian dla: {odbiorcaNazwa}");
            }
        }
        #endregion

        #region Grid Ostatnich Odbiorców

        private void SetupOstatniOdbiorcyGrid()
        {
            if (panelOstatniOdbiorcy == null || gridOstatniOdbiorcy == null) return;

            panelOstatniOdbiorcy.AutoSize = false;
            panelOstatniOdbiorcy.Height = 220;

            if (lblOstatniOdbiorcy != null)
            {
                lblOstatniOdbiorcy.Location = new Point(15, 10);
                lblOstatniOdbiorcy.Size = new Size(800, 20);
            }

            gridOstatniOdbiorcy.Location = new Point(15, 35);
            gridOstatniOdbiorcy.Size = new Size(panelOstatniOdbiorcy.Width - 30, 175);
            gridOstatniOdbiorcy.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            gridOstatniOdbiorcy.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridOstatniOdbiorcy.ReadOnly = true;
            gridOstatniOdbiorcy.AllowUserToResizeRows = false;
            gridOstatniOdbiorcy.AllowUserToResizeColumns = false;
            gridOstatniOdbiorcy.ColumnHeadersVisible = false;
            gridOstatniOdbiorcy.RowHeadersVisible = false;
            gridOstatniOdbiorcy.MultiSelect = false;
            gridOstatniOdbiorcy.SelectionMode = DataGridViewSelectionMode.CellSelect;
            gridOstatniOdbiorcy.ScrollBars = ScrollBars.Vertical;
            gridOstatniOdbiorcy.BackgroundColor = Color.White;
            gridOstatniOdbiorcy.BorderStyle = BorderStyle.None;
            gridOstatniOdbiorcy.GridColor = Color.FromArgb(243, 244, 246);

            panelOstatniOdbiorcy.Paint += (s, e) =>
            {
                using var path = GetRoundedRectPath(panelOstatniOdbiorcy.ClientRectangle, 8);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(Color.FromArgb(249, 250, 251));
                e.Graphics.FillPath(brush, path);
                using var pen = new Pen(Color.FromArgb(229, 231, 235), 1);
                e.Graphics.DrawPath(pen, path);
            };

            gridOstatniOdbiorcy.DefaultCellStyle.SelectionBackColor = Color.FromArgb(92, 138, 58);
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
                gridOstatniOdbiorcy.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.FromArgb(224, 242, 215);
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
                        r["Hallal"] = false;
                        r["Strefa"] = false;
                        r["Ilosc"] = 0m;
                        r["Pojemniki"] = 0m;
                        r["Palety"] = 0m;
                        r["Cena"] = DBNull.Value;  // POPRAWIONE - było 0m
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
                lblOstatniOdbiorcy.Text = "Wybierz handlowca aby zobaczyć odbiorców";
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
                lblOstatniOdbiorcy.Text = $"Brak odbiorców dla: {handlowiec}";
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
                            row.Cells[col].Style.ForeColor = Color.FromArgb(92, 138, 58);
                        }
                    }
                }
            }

            lblOstatniOdbiorcy.Text = $"Odbiorcy ({odbiorcy.Count}) - kliknij aby wybrać:";
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
                ShowWarning($"Błąd pobierania ostatnich zamówień: {ex.Message}");
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
                        Text = "Preferowane:",
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
                            BackColor = Color.FromArgb(224, 242, 215),
                            ForeColor = Color.FromArgb(92, 138, 58),
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
                SetProgressBarColor(progressSolowka, Color.FromArgb(92, 138, 58));
                lblSolowkaInfo.ForeColor = Color.FromArgb(92, 138, 58);
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
                SetProgressBarColor(progressTir, Color.FromArgb(92, 138, 58));
                lblTirInfo.ForeColor = Color.FromArgb(92, 138, 58);
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
                Height = 75,
                BackColor = Color.FromArgb(92, 138, 58),
                Parent = dataGridViewZamowienie.Parent
            };

            // Lewa strona - statystyki
            var flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                Width = 640,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(20, 12, 20, 12)
            };

            lblPozycji = CreateSummaryLabel("POZYCJI", "0");
            lblSumaPalet = CreateSummaryLabel("PALETY", "0");
            lblSumaPojemnikow = CreateSummaryLabel("POJEMNIKI", "0");
            lblSumaKg = CreateSummaryLabel("KILOGRAMY", "0");

            flowPanel.Controls.Add(lblPozycji);
            flowPanel.Controls.Add(CreateSeparator());
            flowPanel.Controls.Add(lblSumaPalet);
            flowPanel.Controls.Add(CreateSeparator());
            flowPanel.Controls.Add(lblSumaPojemnikow);
            flowPanel.Controls.Add(CreateSeparator());
            flowPanel.Controls.Add(lblSumaKg);

            // Prawa strona - panel informacyjny klas wagowych (rozbudowany)
            panelKlasyWagowe = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15, 5, 20, 5)
            };

            var lblKlasyTitle = new Label
            {
                Text = "KLASY WAGOWE TUSZKI",
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 230, 190),
                Location = new Point(10, 3),
                AutoSize = true
            };

            // Pasek postępu klas - szerszy
            pnlKlasyPasek = new Panel
            {
                Location = new Point(10, 20),
                Size = new Size(320, 22),
                BackColor = Color.FromArgb(60, 100, 40)
            };
            pnlKlasyPasek.Paint += PaintKlasyPasek;

            // Linia szczegółów - ile pojemników zabrano per klasa
            lblKlasyInfo = new Label
            {
                Text = "Kliknij 2x na Kurczak A",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(200, 230, 190),
                Location = new Point(10, 46),
                Size = new Size(450, 22),
                AutoSize = false
            };

            panelKlasyWagowe.Controls.Add(lblKlasyTitle);
            panelKlasyWagowe.Controls.Add(pnlKlasyPasek);
            panelKlasyWagowe.Controls.Add(lblKlasyInfo);

            panelSummary.Controls.Add(flowPanel);
            panelSummary.Controls.Add(panelKlasyWagowe);

            if (dataGridViewZamowienie != null)
            {
                dataGridViewZamowienie.Height -= 75;
            }
        }

        private static readonly Dictionary<int, Color> KLASY_KOLORY = new()
        {
            { 5, Color.FromArgb(220, 38, 38) },   // Czerwony
            { 6, Color.FromArgb(234, 88, 12) },   // Pomarańczowy
            { 7, Color.FromArgb(202, 138, 4) },   // Żółty
            { 8, Color.FromArgb(101, 163, 13) },  // Limonka
            { 9, Color.FromArgb(22, 163, 74) },   // Zielony
            { 10, Color.FromArgb(8, 145, 178) },  // Cyan
            { 11, Color.FromArgb(37, 99, 235) },  // Niebieski
            { 12, Color.FromArgb(124, 58, 237) }, // Fioletowy
        };

        private void PaintKlasyPasek(object? sender, PaintEventArgs e)
        {
            if (pnlKlasyPasek == null) return;

            int width = pnlKlasyPasek.Width;
            int height = pnlKlasyPasek.Height;

            // Tło
            using var bgBrush = new SolidBrush(Color.FromArgb(50, 80, 35));
            e.Graphics.FillRectangle(bgBrush, 0, 0, width, height);

            if (_rozkladKlasKurczakA == null || _rozkladKlasKurczakA.SumaPojemnikow == 0)
            {
                // Puste - pokaż tekst zachęcający
                using var font = new Font("Segoe UI", 8f);
                using var brush = new SolidBrush(Color.FromArgb(150, 180, 140));
                e.Graphics.DrawString("Kliknij 2x na wiersz Kurczak A", font, brush, 60, 4);
                return;
            }

            // Rysuj segmenty klas proporcjonalnie do pojemników
            int x = 0;
            int sumaPojemnikow = _rozkladKlasKurczakA.SumaPojemnikow;

            foreach (var kv in _rozkladKlasKurczakA.KlasyPojemniki.Where(k => k.Value > 0).OrderBy(k => k.Key))
            {
                int segmentWidth = (int)(width * kv.Value / (decimal)sumaPojemnikow);
                if (segmentWidth < 1) segmentWidth = 1;

                if (KLASY_KOLORY.TryGetValue(kv.Key, out var kolor))
                {
                    using var brush = new SolidBrush(kolor);
                    e.Graphics.FillRectangle(brush, x, 0, segmentWidth, height);

                    // Etykieta klasy jeśli segment wystarczająco szeroki
                    if (segmentWidth > 30)
                    {
                        using var font = new Font("Segoe UI", 8f, FontStyle.Bold);
                        string txt = $"{kv.Key}";
                        var textSize = e.Graphics.MeasureString(txt, font);
                        int textX = x + (segmentWidth - (int)textSize.Width) / 2;
                        e.Graphics.DrawString(txt, font, Brushes.White, textX, 3);
                    }
                }

                x += segmentWidth;
            }

            // Ramka
            using var pen = new Pen(Color.FromArgb(100, 140, 80), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, width - 1, height - 1);
        }

        private void AktualizujPasekKlasWagowych()
        {
            if (lblKlasyInfo == null || pnlKlasyPasek == null) return;

            if (_rozkladKlasKurczakA == null || _rozkladKlasKurczakA.SumaPojemnikow == 0)
            {
                lblKlasyInfo.Text = "Kliknij 2x na wiersz Kurczak A aby rozdzielić na klasy wagowe";
                lblKlasyInfo.ForeColor = Color.FromArgb(180, 210, 170);
                lblKlasyInfo.Font = new Font("Segoe UI", 9f);
            }
            else
            {
                int sumaPoj = _rozkladKlasKurczakA.SumaPojemnikow;
                decimal sumaKg = sumaPoj * 15m;
                decimal palety = sumaPoj / 36m;
                
                // Pobierz ilość zamówioną tuszki z grida
                decimal zamowioneKgTuszki = 0;
                foreach (DataRow r in _dt.Rows)
                {
                    string kod = r.Field<string>("Kod") ?? "";
                    if (kod.Contains("Kurczak A") || kod.Contains("TUSZKA") || kod.Contains("Tuszka"))
                    {
                        zamowioneKgTuszki = r.Field<decimal?>("Ilosc") ?? 0m;
                        break;
                    }
                }

                int zamowionePoj = zamowioneKgTuszki > 0 ? (int)Math.Ceiling(zamowioneKgTuszki / 15m) : 0;
                int roznica = zamowionePoj > 0 ? sumaPoj - zamowionePoj : 0;
                
                // Buduj szczegółowy tekst z ilościami pojemników per klasa
                var szczegoly = _rozkladKlasKurczakA.KlasyPojemniki
                    .Where(k => k.Value > 0)
                    .OrderBy(k => k.Key)
                    .Select(k => $"Kl.{k.Key}:{k.Value}")
                    .ToList();

                string rozbicie = string.Join("  ", szczegoly);
                
                // Informacja o różnicy
                string roznicaText = "";
                if (zamowionePoj > 0)
                {
                    if (roznica == 0)
                        roznicaText = " ✓";
                    else if (roznica > 0)
                        roznicaText = $" (+{roznica})";
                    else
                        roznicaText = $" ({roznica})";
                }

                // Pełna informacja: suma + kilogramy + rozbicie na klasy
                lblKlasyInfo.Text = $"Zabrano: {sumaPoj} poj. ({sumaKg:N0} kg){roznicaText}  │  {rozbicie}";
                
                // Kolor w zależności od tego czy się zgadza
                if (zamowionePoj > 0 && roznica == 0)
                {
                    lblKlasyInfo.ForeColor = Color.FromArgb(180, 255, 180); // Jasny zielony - OK
                }
                else if (roznica > 0)
                {
                    lblKlasyInfo.ForeColor = Color.FromArgb(255, 220, 150); // Pomarańczowy - za dużo
                }
                else if (roznica < 0)
                {
                    lblKlasyInfo.ForeColor = Color.FromArgb(255, 200, 150); // Żółto-pomarańczowy - za mało
                }
                else
                {
                    lblKlasyInfo.ForeColor = Color.White; // Biały - domyślnie
                }
                
                lblKlasyInfo.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            }

            pnlKlasyPasek.Invalidate();
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
                ForeColor = Color.FromArgb(224, 242, 215),
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
                BackColor = Color.FromArgb(75, 115, 47),
                Margin = new Padding(0, 0, 0, 0)
            };
        }

        #endregion

        #region Inicjalizacja i Ustawienia UI

        private void ApplyModernUIStyles()
        {
            this.BackColor = Color.FromArgb(249, 250, 251);

            if (btnZapisz != null)
            {
                btnZapisz.Text = "Zapisz";
                StyleButton(btnZapisz, Color.FromArgb(92, 138, 58), Color.White);
            }

            if (btnAnuluj != null)
            {
                btnAnuluj.Text = "Anuluj";
                StyleButton(btnAnuluj, Color.FromArgb(243, 244, 246), Color.FromArgb(75, 85, 99));
            }

            // Waluta zamówienia - przy przyciskach
            if (panelAkcji != null)
            {
                var lblWaluta = new Label
                {
                    Text = "Waluta:",
                    Font = new Font("Segoe UI", 9f),
                    ForeColor = Color.FromArgb(75, 85, 99),
                    AutoSize = true,
                    Location = new Point(15, 24)
                };

                cbWaluta = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new Font("Segoe UI", 9f),
                    Width = 65,
                    Location = new Point(65, 20)
                };
                cbWaluta.Items.AddRange(new object[] { "PLN", "EUR" });
                cbWaluta.SelectedIndex = 0;
                StyleComboBox(cbWaluta);

                panelAkcji.Controls.Add(lblWaluta);
                panelAkcji.Controls.Add(cbWaluta);
            }

            if (panelMaster != null)
            {
                panelMaster.BackColor = Color.White;
                panelMaster.Padding = new Padding(0);
            }

            if (panelOdbiorca != null)
            {
                panelOdbiorca.BackColor = Color.White;
                panelOdbiorca.Padding = new Padding(10, 5, 10, 5);
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
                if (c is Label lbl && lbl.Font.Bold)
                {
                    lbl.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                    lbl.ForeColor = Color.FromArgb(107, 114, 128);
                }
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
            dtp.CalendarTitleBackColor = Color.FromArgb(92, 138, 58);
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

            dataGridViewZamowienie.RowTemplate.Height = 36;
            dataGridViewZamowienie.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 242, 215);
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
            btnZapisz.Enabled = false;
            var dzis = DateTime.Now.Date;

            if (dateTimePickerProdukcji != null)
            {
                dateTimePickerProdukcji.Value = DateTime.Today;
            }

            dateTimePickerSprzedaz.Value = (dzis.DayOfWeek == DayOfWeek.Friday) ? dzis.AddDays(3) : dzis.AddDays(1);

            if (dateTimePickerGodzinaPrzyjazdu != null)
            {
                dateTimePickerGodzinaPrzyjazdu.Format = DateTimePickerFormat.Custom;
                dateTimePickerGodzinaPrzyjazdu.CustomFormat = "HH:mm";
                dateTimePickerGodzinaPrzyjazdu.ShowUpDown = true;
                dateTimePickerGodzinaPrzyjazdu.Value = DateTime.Today.AddHours(8);
            }

            if (chkWlasnyOdbior != null)
            {
                chkWlasnyOdbior.Checked = false;
            }

            RecalcSum();
        }
        private void WireUpUIEvents()
        {
            dataGridViewZamowienie.CellValueChanged += DataGridViewZamowienie_CellValueChanged;
            dataGridViewZamowienie.EditingControlShowing += DataGridViewZamowienie_EditingControlShowing;
            dataGridViewZamowienie.CellPainting += DataGridViewZamowienie_CellPainting;
            dataGridViewZamowienie.RowPostPaint += DataGridViewZamowienie_RowPostPaint;
            dataGridViewZamowienie.ColumnWidthChanged += (s, e) => dataGridViewZamowienie.Invalidate();
            dataGridViewZamowienie.CurrentCellDirtyStateChanged += DataGridViewZamowienie_CurrentCellDirtyStateChanged;
            dataGridViewZamowienie.CellFormatting += DataGridViewZamowienie_CellFormatting;

            txtSzukajOdbiorcy.TextChanged += TxtSzukajOdbiorcy_TextChanged;
            txtSzukajOdbiorcy.KeyDown += TxtSzukajOdbiorcy_KeyDown;

            listaWynikowOdbiorcy.Click += ListaWynikowOdbiorcy_Click;
            listaWynikowOdbiorcy.KeyDown += ListaWynikowOdbiorcy_KeyDown;
            listaWynikowOdbiorcy.DrawMode = DrawMode.OwnerDrawFixed;
            listaWynikowOdbiorcy.ItemHeight = 38;
            listaWynikowOdbiorcy.DrawItem += ListaWynikowOdbiorcy_DrawItem;

            var hands = _kontrahenci.Select(k => k.Handlowiec).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList();
            hands.Insert(0, "— Wszyscy —");
            cbHandlowiecFilter.Items.Clear();
            cbHandlowiecFilter.Items.AddRange(hands.ToArray());
            cbHandlowiecFilter.SelectedIndex = 0;
            cbHandlowiecFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cbHandlowiecFilter.SelectedIndexChanged += CbHandlowiecFilter_SelectedIndexChanged;
        }
        #endregion

        #region Asynchroniczne Ładowanie Danych

        private async Task LoadInitialDataInBackground()
        {
            var towaryTask = LoadTowaryAsRowsAsync();
            var kontrahenciTask = LoadKontrahenciAsync();
            var imagesTask = LoadProductImagesAsync();
            await Task.WhenAll(towaryTask, kontrahenciTask, imagesTask);
        }

        private async Task LoadProductImagesAsync()
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Sprawdź czy tabela istnieje
                const string checkSql = @"SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TowarZdjecia') THEN 1 ELSE 0 END";
                await using (var cmdCheck = new SqlCommand(checkSql, cn))
                {
                    var exists = (int)(await cmdCheck.ExecuteScalarAsync())!;
                    if (exists == 0) return;
                }

                const string sql = @"SELECT TowarId, Zdjecie FROM dbo.TowarZdjecia WHERE Aktywne = 1";
                await using var cmd = new SqlCommand(sql, cn);
                await using var rdr = await cmd.ExecuteReaderAsync();

                while (await rdr.ReadAsync())
                {
                    int towarId = rdr.GetInt32(0);
                    if (!rdr.IsDBNull(1))
                    {
                        byte[] data = (byte[])rdr[1];
                        try
                        {
                            // MemoryStream NIE może być using - GDI+ wymaga otwartego strumienia
                            var ms = new System.IO.MemoryStream(data);
                            var bmp = new Bitmap(ms);
                            // Stwórz niezależną miniaturkę 100x100
                            var thumb = new Bitmap(bmp, 100, 100);
                            bmp.Dispose();
                            ms.Dispose();
                            _productImages[towarId] = thumb;
                        }
                        catch { }
                    }
                }

                // Odśwież DataGridView aby obrazki pojawiły się od razu
                if (dataGridViewZamowienie != null && !dataGridViewZamowienie.IsDisposed)
                    dataGridViewZamowienie.Invalidate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadProductImages] Błąd: {ex.Message}");
            }
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
                    // POPRAWIONE - ostatni parametr to DBNull.Value zamiast 0m
                    _dt.Rows.Add(item.Id, item.Kod, item.Katalog, false, false, false, false, 0m, 0m, 0m, DBNull.Value, item.Kod, item.Kod, 1);
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
            bool hallal = dataRow.Field<bool>("Hallal");

            // POPRAWIONE - odczytywanie STRING
            decimal cena = 0m;
            if (!dataRow.IsNull("Cena"))
            {
                string cenaStr = dataRow.Field<string>("Cena") ?? "";
                decimal.TryParse(cenaStr, NumberStyles.Any, _pl, out cena);
            }

            bool maWartosci = (ilosc > 0 || pojemniki > 0 || palety > 0 || cena > 0);

            if (maWartosci)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(240, 253, 244);
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(187, 247, 208);
                row.DefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            }
            else
            {
                row.DefaultCellStyle.BackColor = (e.RowIndex % 2 == 0) ? Color.White : Color.FromArgb(249, 250, 251);
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 242, 215);
                row.DefaultCellStyle.Font = new Font("Segoe UI", 9.5f);
            }

            // Pulsowanie zielone po wpisaniu wartości
            if (_pulseRows.TryGetValue(e.RowIndex, out int pulseStep) && pulseStep > 0)
            {
                int alpha = pulseStep > 3 ? (6 - pulseStep) * 30 : pulseStep * 30; // fade in/out
                using var overlay = new SolidBrush(Color.FromArgb(alpha, 34, 197, 94));
                e.Graphics.FillRectangle(overlay, e.RowBounds);
            }

            var e2Cell = row.Cells["E2"];
            var foliaCell = row.Cells["Folia"];
            var hallalCell = row.Cells["Hallal"];

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
                foliaCell.Style.BackColor = Color.FromArgb(224, 242, 215);
                foliaCell.Style.SelectionBackColor = Color.FromArgb(187, 247, 208);
            }
            else if (!maWartosci)
            {
                foliaCell.Style.BackColor = Color.FromArgb(240, 247, 237);
                foliaCell.Style.SelectionBackColor = Color.FromArgb(224, 242, 215);
            }
            else
            {
                foliaCell.Style.BackColor = Color.FromArgb(245, 250, 243);
                foliaCell.Style.SelectionBackColor = Color.FromArgb(220, 252, 231);
            }

            if (hallal)
            {
                hallalCell.Style.BackColor = Color.FromArgb(200, 255, 200);
                hallalCell.Style.SelectionBackColor = Color.FromArgb(170, 240, 170);
            }
            else if (!maWartosci)
            {
                hallalCell.Style.BackColor = Color.FromArgb(240, 255, 240);
                hallalCell.Style.SelectionBackColor = Color.FromArgb(220, 245, 220);
            }
            else
            {
                hallalCell.Style.BackColor = Color.FromArgb(235, 255, 235);
                hallalCell.Style.SelectionBackColor = Color.FromArgb(220, 252, 231);
            }

            if (CzyRysowacSeparatorPo(kod))
            {
                using var pen = new Pen(Color.FromArgb(209, 213, 219), 2);
                var bounds = e.RowBounds;
                e.Graphics.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
            }
        }
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
                listaWynikowOdbiorcy.Width = 380;
                listaWynikowOdbiorcy.Height = Math.Min(300, wyniki.Count * 38 + 5);
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

        private void ListaWynikowOdbiorcy_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            e.DrawBackground();

            var item = listaWynikowOdbiorcy.Items[e.Index] as KontrahentInfo;
            if (item == null) return;

            bool selected = (e.State & DrawItemState.Selected) != 0;
            var bgColor = selected ? Color.FromArgb(25, 118, 210) : Color.White;
            var fgName = selected ? Color.White : Color.FromArgb(33, 33, 33);
            var fgDetail = selected ? Color.FromArgb(200, 220, 255) : Color.Gray;

            using (var bg = new SolidBrush(bgColor))
                e.Graphics.FillRectangle(bg, e.Bounds);

            using var fontName = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            using var fontDetail = new Font("Segoe UI", 8f);
            using var brushName = new SolidBrush(fgName);
            using var brushDetail = new SolidBrush(fgDetail);

            e.Graphics.DrawString(item.Nazwa, fontName, brushName, e.Bounds.X + 4, e.Bounds.Y + 2);

            var detail = item.Miejscowosc;
            if (!string.IsNullOrEmpty(item.NIP))
                detail += (string.IsNullOrEmpty(detail) ? "" : "  |  ") + "NIP: " + item.NIP;

            e.Graphics.DrawString(detail, fontDetail, brushDetail, e.Bounds.X + 4, e.Bounds.Y + 19);
        }

        private void WybierzOdbiorceZListy()
        {
            if (listaWynikowOdbiorcy.SelectedItem is KontrahentInfo wybrany)
            {
                UstawOdbiorce(wybrany.Id);
            }
        }

        private async void BtnZapiszSzablon_Click(object? sender, EventArgs e)
        {
            // Zbierz produkty z wartościami
            var towary = new List<(int Id, string Kod, decimal Ilosc, int Pojemniki, decimal Palety, bool E2, bool Folia, bool Hallal, bool Strefa)>();
            foreach (DataRow r in _dt.Rows)
            {
                decimal il = r.Field<decimal?>("Ilosc") ?? 0m;
                if (il > 0)
                {
                    towary.Add((
                        r.Field<int>("Id"),
                        r.Field<string>("Kod") ?? "",
                        il,
                        (int)(r.Field<decimal?>("Pojemniki") ?? 0m),
                        r.Field<decimal?>("Palety") ?? 0m,
                        r.Field<bool>("E2"),
                        r.Field<bool>("Folia"),
                        r.Field<bool>("Hallal"),
                        r.Field<bool>("Strefa")
                    ));
                }
            }

            if (!towary.Any())
            {
                ToastNotification.Show(this, "Brak produktów do zapisania w szablonie", ToastNotification.ToastType.Warning);
                return;
            }

            // Pytanie o nazwę szablonu
            var inputForm = new Form
            {
                Text = "Nazwa szablonu",
                Size = new Size(360, 150),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };
            var txtNazwa = new TextBox { Location = new Point(20, 20), Width = 300, Font = new Font("Segoe UI", 11) };
            var lblInfo = new Label { Text = "Podaj nazwę szablonu:", Location = new Point(20, 2), AutoSize = true, Font = new Font("Segoe UI", 9) };
            var btnOk = new Button { Text = "Zapisz", DialogResult = DialogResult.OK, Location = new Point(220, 60), Size = new Size(100, 32) };
            inputForm.Controls.AddRange(new Control[] { lblInfo, txtNazwa, btnOk });
            inputForm.AcceptButton = btnOk;

            string defaultName = _kontrahenci.FirstOrDefault(k => k.Id == _selectedKlientId)?.Nazwa ?? "Szablon";
            txtNazwa.Text = $"{defaultName} - {DateTime.Now:dd.MM}";

            if (inputForm.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(txtNazwa.Text))
                return;

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Stwórz tabelę jeśli nie istnieje
                await using (var cmdCreate = new SqlCommand(@"
                    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SzablonyZamowien')
                    CREATE TABLE dbo.SzablonyZamowien (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Nazwa NVARCHAR(200) NOT NULL,
                        KlientId INT NULL,
                        TworzonyPrzez NVARCHAR(50),
                        DataUtworzenia DATETIME2 DEFAULT SYSDATETIME(),
                        Aktywny BIT DEFAULT 1
                    );
                    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SzablonyZamowienTowar')
                    CREATE TABLE dbo.SzablonyZamowienTowar (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        SzablonId INT NOT NULL,
                        KodTowaru INT NOT NULL,
                        Ilosc DECIMAL(18,2),
                        Pojemniki INT DEFAULT 0,
                        Palety DECIMAL(18,2) DEFAULT 0,
                        E2 BIT DEFAULT 0,
                        Folia BIT DEFAULT 0,
                        Hallal BIT DEFAULT 0
                    );", cn))
                {
                    await cmdCreate.ExecuteNonQueryAsync();
                }

                // Zapisz szablon
                await using var cmdSzablon = new SqlCommand(@"
                    INSERT INTO dbo.SzablonyZamowien (Nazwa, KlientId, TworzonyPrzez)
                    VALUES (@nazwa, @klient, @user);
                    SELECT SCOPE_IDENTITY();", cn);
                cmdSzablon.Parameters.AddWithValue("@nazwa", txtNazwa.Text.Trim());
                cmdSzablon.Parameters.AddWithValue("@klient",
                    string.IsNullOrEmpty(_selectedKlientId) ? (object)DBNull.Value : int.Parse(_selectedKlientId));
                cmdSzablon.Parameters.AddWithValue("@user", UserID);

                int szablonId = Convert.ToInt32(await cmdSzablon.ExecuteScalarAsync());

                // Zapisz towary szablonu
                bool strefaSzablonExists = false;
                try
                {
                    await using var cmdChk = new SqlCommand("SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SzablonyZamowienTowar') AND name = 'Strefa'", cn);
                    strefaSzablonExists = (int)await cmdChk.ExecuteScalarAsync() > 0;
                    if (!strefaSzablonExists)
                    {
                        await using var alter = new SqlCommand(
                            "ALTER TABLE [dbo].[SzablonyZamowienTowar] ADD Strefa BIT NULL DEFAULT 0", cn);
                        await alter.ExecuteNonQueryAsync();
                        strefaSzablonExists = true;
                    }
                }
                catch { }
                string strCol = strefaSzablonExists ? ", Strefa" : "";
                string strVal = strefaSzablonExists ? ", @str" : "";

                foreach (var t in towary)
                {
                    await using var cmdT = new SqlCommand($@"
                        INSERT INTO dbo.SzablonyZamowienTowar (SzablonId, KodTowaru, Ilosc, Pojemniki, Palety, E2, Folia, Hallal{strCol})
                        VALUES (@sid, @kod, @il, @poj, @pal, @e2, @fol, @hal{strVal})", cn);
                    cmdT.Parameters.AddWithValue("@sid", szablonId);
                    cmdT.Parameters.AddWithValue("@kod", t.Id);
                    cmdT.Parameters.AddWithValue("@il", t.Ilosc);
                    cmdT.Parameters.AddWithValue("@poj", t.Pojemniki);
                    cmdT.Parameters.AddWithValue("@pal", t.Palety);
                    cmdT.Parameters.AddWithValue("@e2", t.E2);
                    cmdT.Parameters.AddWithValue("@fol", t.Folia);
                    cmdT.Parameters.AddWithValue("@hal", t.Hallal);
                    if (strefaSzablonExists) cmdT.Parameters.AddWithValue("@str", t.Strefa);
                    await cmdT.ExecuteNonQueryAsync();
                }

                ToastNotification.Show(this, $"Szablon \"{txtNazwa.Text.Trim()}\" zapisany ({towary.Count} poz.)", ToastNotification.ToastType.Success);
            }
            catch (Exception ex)
            {
                ShowError($"Błąd zapisu szablonu: {ex.Message}");
            }
        }

        private async void BtnWczytajSzablon_Click(object? sender, EventArgs e)
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Sprawdź czy tabela istnieje
                await using (var cmdCheck = new SqlCommand(
                    "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SzablonyZamowien') THEN 1 ELSE 0 END", cn))
                {
                    if ((int)(await cmdCheck.ExecuteScalarAsync())! == 0)
                    {
                        ToastNotification.Show(this, "Brak zapisanych szablonów", ToastNotification.ToastType.Info);
                        return;
                    }
                }

                // Pobierz listę szablonów
                var szablony = new List<(int Id, string Nazwa, string Data, int Pozycji)>();
                await using (var cmdList = new SqlCommand(@"
                    SELECT s.Id, s.Nazwa, s.DataUtworzenia, COUNT(t.Id) as Pozycji
                    FROM dbo.SzablonyZamowien s
                    LEFT JOIN dbo.SzablonyZamowienTowar t ON s.Id = t.SzablonId
                    WHERE s.Aktywny = 1
                    ORDER BY s.DataUtworzenia DESC", cn))
                {
                    await using var rd = await cmdList.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        szablony.Add((
                            rd.GetInt32(0),
                            rd.GetString(1),
                            rd.GetDateTime(2).ToString("dd.MM.yyyy HH:mm"),
                            rd.GetInt32(3)
                        ));
                    }
                }

                if (!szablony.Any())
                {
                    ToastNotification.Show(this, "Brak zapisanych szablonów", ToastNotification.ToastType.Info);
                    return;
                }

                // Dialog wyboru szablonu
                var selectForm = new Form
                {
                    Text = "Wybierz szablon",
                    Size = new Size(420, 350),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };

                var listBox = new ListBox
                {
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 10),
                    DrawMode = DrawMode.OwnerDrawFixed,
                    ItemHeight = 36
                };

                foreach (var s in szablony)
                    listBox.Items.Add(s);

                listBox.DrawItem += (s2, e2) =>
                {
                    if (e2.Index < 0) return;
                    e2.DrawBackground();
                    var item = ((int Id, string Nazwa, string Data, int Pozycji))listBox.Items[e2.Index];

                    bool sel = (e2.State & DrawItemState.Selected) != 0;
                    var bg = sel ? Color.FromArgb(79, 70, 229) : Color.White;
                    var fg1 = sel ? Color.White : Color.Black;
                    var fg2 = sel ? Color.FromArgb(200, 200, 255) : Color.Gray;

                    using var bgBr = new SolidBrush(bg);
                    e2.Graphics.FillRectangle(bgBr, e2.Bounds);

                    using var f1 = new Font("Segoe UI", 10f, FontStyle.Bold);
                    using var f2 = new Font("Segoe UI", 8f);
                    using var br1 = new SolidBrush(fg1);
                    using var br2 = new SolidBrush(fg2);

                    e2.Graphics.DrawString(item.Nazwa, f1, br1, e2.Bounds.X + 6, e2.Bounds.Y + 2);
                    e2.Graphics.DrawString($"{item.Data}  |  {item.Pozycji} pozycji", f2, br2, e2.Bounds.X + 6, e2.Bounds.Y + 19);
                };

                listBox.DoubleClick += (s2, e2) => { selectForm.DialogResult = DialogResult.OK; selectForm.Close(); };

                var btnLoad = new Button
                {
                    Text = "Wczytaj",
                    DialogResult = DialogResult.OK,
                    Dock = DockStyle.Bottom,
                    Height = 40,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(79, 70, 229),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold)
                };
                btnLoad.FlatAppearance.BorderSize = 0;

                var btnUsun = new Button
                {
                    Text = "Usuń szablon",
                    Dock = DockStyle.Bottom,
                    Height = 30,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(239, 68, 68),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 8.5f)
                };
                btnUsun.FlatAppearance.BorderSize = 0;
                btnUsun.Click += async (s2, e2) =>
                {
                    if (listBox.SelectedIndex < 0) return;
                    var sel = ((int Id, string Nazwa, string Data, int Pozycji))listBox.SelectedItem;
                    if (MessageBox.Show($"Usunąć szablon \"{sel.Nazwa}\"?", "Potwierdzenie",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

                    await using var cn2 = new SqlConnection(_connLibra);
                    await cn2.OpenAsync();
                    await using var cmdDel = new SqlCommand("UPDATE dbo.SzablonyZamowien SET Aktywny = 0 WHERE Id = @id", cn2);
                    cmdDel.Parameters.AddWithValue("@id", sel.Id);
                    await cmdDel.ExecuteNonQueryAsync();
                    listBox.Items.RemoveAt(listBox.SelectedIndex);
                    ToastNotification.Show(this, "Szablon usunięty", ToastNotification.ToastType.Info);
                };

                selectForm.Controls.Add(listBox);
                selectForm.Controls.Add(btnUsun);
                selectForm.Controls.Add(btnLoad);

                if (selectForm.ShowDialog(this) != DialogResult.OK || listBox.SelectedIndex < 0)
                    return;

                var selected = ((int Id, string Nazwa, string Data, int Pozycji))listBox.SelectedItem;

                // Wczytaj towary szablonu
                bool strefaSzablonEx = false;
                try
                {
                    await using var cmdChkS = new SqlCommand("SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SzablonyZamowienTowar') AND name = 'Strefa'", cn);
                    strefaSzablonEx = (int)await cmdChkS.ExecuteScalarAsync() > 0;
                }
                catch { }
                string strSel = strefaSzablonEx ? ", ISNULL(Strefa, 0) as Strefa" : "";

                await using var cmdTowary = new SqlCommand($@"
                    SELECT KodTowaru, Ilosc, Pojemniki, Palety, E2, Folia, Hallal{strSel}
                    FROM dbo.SzablonyZamowienTowar
                    WHERE SzablonId = @id", cn);
                cmdTowary.Parameters.AddWithValue("@id", selected.Id);

                _blokujObslugeZmian = true;
                foreach (DataRow r in _dt.Rows)
                {
                    r["E2"] = false; r["Folia"] = false; r["Hallal"] = false; r["Strefa"] = false;
                    r["Ilosc"] = 0m; r["Pojemniki"] = 0m; r["Palety"] = 0m;
                    r["Cena"] = DBNull.Value; r["MaWartosci"] = 1;
                }

                int loaded = 0;
                await using var rdT = await cmdTowary.ExecuteReaderAsync();
                while (await rdT.ReadAsync())
                {
                    int kodTowaru = rdT.GetInt32(0);
                    var rows = _dt.Select($"Id = {kodTowaru}");
                    if (rows.Any())
                    {
                        var row = rows[0];
                        row["Ilosc"] = rdT.IsDBNull(1) ? 0m : rdT.GetDecimal(1);
                        row["Pojemniki"] = rdT.GetInt32(2);
                        row["Palety"] = rdT.GetDecimal(3);
                        row["E2"] = rdT.GetBoolean(4);
                        row["Folia"] = rdT.GetBoolean(5);
                        row["Hallal"] = rdT.GetBoolean(6);
                        row["Strefa"] = strefaSzablonEx ? rdT.GetBoolean(7) : false;
                        decimal ilTmpl = rdT.IsDBNull(1) ? 0m : rdT.GetDecimal(1);
                        row["MaWartosci"] = ilTmpl > 0 ? 0 : 1;
                        loaded++;
                    }
                }

                _blokujObslugeZmian = false;
                RecalcSum();

                ToastNotification.Show(this, $"Wczytano \"{selected.Nazwa}\" ({loaded} poz.)", ToastNotification.ToastType.Success);
            }
            catch (Exception ex)
            {
                ShowError($"Błąd wczytywania szablonu: {ex.Message}");
            }
        }

        private async void BtnKopiujOstatnie_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedKlientId))
            {
                ToastNotification.Show(this, "Najpierw wybierz odbiorcę", ToastNotification.ToastType.Warning);
                return;
            }

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Znajdź ostatnie zamówienie tego klienta
                await using var cmdLast = new SqlCommand(@"
                    SELECT TOP 1 Id FROM [dbo].[ZamowieniaMieso]
                    WHERE KlientId = @KlientId
                    ORDER BY DataZamowienia DESC, Id DESC", cn);
                cmdLast.Parameters.AddWithValue("@KlientId", int.Parse(_selectedKlientId));

                var lastId = await cmdLast.ExecuteScalarAsync();
                if (lastId == null)
                {
                    ToastNotification.Show(this, "Brak poprzednich zamówień dla tego odbiorcy", ToastNotification.ToastType.Info);
                    return;
                }

                int zamId = (int)lastId;

                // Załaduj produkty z ostatniego zamówienia
                bool strefaCopyExists = await CheckStrefaTowarColumnExists(cn);
                string strefaCopyCol = strefaCopyExists ? ", ISNULL(zmt.Strefa, 0) as Strefa" : "";
                await using var cmdTowary = new SqlCommand($@"
                    SELECT zmt.KodTowaru, zmt.Ilosc, ISNULL(zmt.Pojemniki, 0) as Pojemniki,
                           ISNULL(zmt.Palety, 0) as Palety, ISNULL(zmt.E2, 0) as E2,
                           ISNULL(zmt.Folia, 0) as Folia, ISNULL(zmt.Hallal, 0) as Hallal{strefaCopyCol}
                    FROM [dbo].[ZamowieniaMiesoTowar] zmt
                    WHERE zmt.ZamowienieId = @Id AND zmt.Ilosc > 0", cn);
                cmdTowary.Parameters.AddWithValue("@Id", zamId);

                _blokujObslugeZmian = true;

                // Wyzeruj obecne wartości
                foreach (DataRow r in _dt.Rows)
                {
                    r["E2"] = false;
                    r["Folia"] = false;
                    r["Hallal"] = false;
                    r["Strefa"] = false;
                    r["Ilosc"] = 0m;
                    r["Pojemniki"] = 0m;
                    r["Palety"] = 0m;
                    r["Cena"] = DBNull.Value;
                    r["MaWartosci"] = 1;
                }

                int loaded = 0;
                await using var rd = await cmdTowary.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int kodTowaru = rd.GetInt32(0);
                    var rows = _dt.Select($"Id = {kodTowaru}");
                    if (rows.Any())
                    {
                        var row = rows[0];
                        row["Ilosc"] = await rd.IsDBNullAsync(1) ? 0m : rd.GetDecimal(1);
                        row["Pojemniki"] = rd.GetInt32(2);
                        row["Palety"] = rd.GetDecimal(3);
                        row["E2"] = rd.GetBoolean(4);
                        row["Folia"] = rd.GetBoolean(5);
                        row["Hallal"] = rd.GetBoolean(6);
                        row["Strefa"] = strefaCopyExists ? rd.GetBoolean(7) : false;
                        decimal ilCopy = await rd.IsDBNullAsync(1) ? 0m : rd.GetDecimal(1);
                        row["MaWartosci"] = ilCopy > 0 ? 0 : 1;
                        loaded++;
                    }
                }

                _blokujObslugeZmian = false;
                RecalcSum();

                if (loaded > 0)
                    ToastNotification.Show(this, $"Skopiowano {loaded} pozycji z zamówienia #{zamId}", ToastNotification.ToastType.Success);
                else
                    ToastNotification.Show(this, "Ostatnie zamówienie nie zawierało produktów", ToastNotification.ToastType.Info);
            }
            catch (Exception ex)
            {
                ShowError($"Błąd kopiowania zamówienia: {ex.Message}");
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

                await LoadPreferredHours(id);
                await LoadPlatnosciOdbiorcy(id);

                dataGridViewZamowienie.Focus();
            }
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
                row["Cena"] = DBNull.Value;  // NULL, nie 0m ani ""
                row["MaWartosci"] = 1;
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
            _headerIcons["Cena"] = CreatePriceIcon();
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

        private Image CreatePriceIcon()
        {
            var bmp = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            using var font = new Font("Arial", 9f, FontStyle.Bold);
            using var brush = new SolidBrush(Color.FromArgb(41, 128, 185));
            g.DrawString("$", font, brush, -1, 1);
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

        #region Klasy Wagowe dla Kurczak A

        /// <summary>
        /// Wyświetla dialog wyboru klas wagowych dla tuszki (Kurczak A)
        /// </summary>
        private void PokazDialogKlasWagowych(decimal iloscKg)
        {
            if (iloscKg <= 0) return;

            // Pobierz datę produkcji
            DateTime dataProdukcji = dateTimePickerProdukcji?.Value.Date ?? DateTime.Today;

            // Przekaż dane do dialogu
            using var dialog = new KlasyWagoweDialog(
                iloscKg, 
                dataProdukcji, 
                _connLibra, 
                _rozkladKlasKurczakA, 
                _idZamowieniaDoEdycji);

            if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Zatwierdzono)
            {
                _rozkladKlasKurczakA = dialog.Rozklad;
                AktualizujNotatkeKlasami();
                AktualizujPasekKlasWagowych();
            }
        }

        /// <summary>
        /// Aktualizuje pole notatki o informację o rozkładzie klas
        /// </summary>
        private void AktualizujNotatkeKlasami()
        {
            if (_rozkladKlasKurczakA == null || textBoxUwagi == null) return;

            string notatkaKlas = _rozkladKlasKurczakA.ToNotatkString();
            if (string.IsNullOrEmpty(notatkaKlas)) return;

            string obecnaNotatka = textBoxUwagi.Text ?? "";

            // Usuń starą notatkę o klasach jeśli istnieje
            int startIndex = obecnaNotatka.IndexOf("[Klasy:");
            if (startIndex >= 0)
            {
                int endIndex = obecnaNotatka.IndexOf("]", startIndex);
                if (endIndex >= 0)
                {
                    obecnaNotatka = obecnaNotatka.Remove(startIndex, endIndex - startIndex + 1).Trim();
                }
            }

            // Dodaj nową notatkę o klasach na początku
            textBoxUwagi.Text = string.IsNullOrEmpty(obecnaNotatka)
                ? notatkaKlas
                : $"{notatkaKlas} {obecnaNotatka}";
        }

        /// <summary>
        /// Parsuje rozkład klas z notatki (przy edycji zamówienia)
        /// </summary>
        private RozkladKlasWagowych? ParseRozkladZNotatki(string notatka)
        {
            if (string.IsNullOrEmpty(notatka)) return null;

            // Szukaj wzorca: [Klasy: Kl.5:20%(10poj), Kl.6:15%(8poj), ...]
            int startIndex = notatka.IndexOf("[Klasy:");
            if (startIndex < 0) return null;

            int endIndex = notatka.IndexOf("]", startIndex);
            if (endIndex < 0) return null;

            string klasyCzesc = notatka.Substring(startIndex + 7, endIndex - startIndex - 7);

            var rozklad = new RozkladKlasWagowych();
            var parts = klasyCzesc.Split(',');

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                // Format nowy: Kl.5:20%(10poj) lub stary: Kl.5:100poj lub Kl.5:100kg
                if (trimmed.StartsWith("Kl."))
                {
                    var colonIndex = trimmed.IndexOf(':');
                    if (colonIndex > 3)
                    {
                        if (int.TryParse(trimmed.Substring(3, colonIndex - 3), out int klasa) &&
                            klasa >= 5 && klasa <= 12)
                        {
                            var valueStr = trimmed.Substring(colonIndex + 1);
                            
                            // Nowy format z procentami: 20%(10poj)
                            if (valueStr.Contains("%"))
                            {
                                var procentIndex = valueStr.IndexOf('%');
                                if (decimal.TryParse(valueStr.Substring(0, procentIndex), out decimal procent))
                                {
                                    rozklad.KlasyProcent[klasa] = procent;
                                    
                                    // Spróbuj też wyciągnąć pojemniki
                                    var pojStart = valueStr.IndexOf('(');
                                    var pojEnd = valueStr.IndexOf("poj");
                                    if (pojStart >= 0 && pojEnd > pojStart)
                                    {
                                        if (int.TryParse(valueStr.Substring(pojStart + 1, pojEnd - pojStart - 1), out int poj))
                                        {
                                            rozklad.KlasyPojemniki[klasa] = poj;
                                        }
                                    }
                                }
                            }
                            // Stary format: poj lub kg
                            else
                            {
                                valueStr = valueStr.Replace("poj", "").Replace("kg", "").Replace("(", "").Replace(")", "").Trim();
                                if (int.TryParse(valueStr, out int wartosc))
                                {
                                    rozklad.KlasyPojemniki[klasa] = wartosc;
                                    // Szacuj procent (zakładając że notatka była dla tego samego zamówienia)
                                    rozklad.KlasyProcent[klasa] = 12.5m; // domyślnie równo
                                }
                            }
                        }
                    }
                }
            }

            return rozklad.SumaProcent > 0 || rozklad.SumaPojemnikow > 0 ? rozklad : null;
        }

        #endregion

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

            if (lblPozycji != null) lblPozycji.Text = _dt.Rows.Count.ToString();
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
            // Nagłówki z ikonami
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
                return;
            }

            // Miniaturki produktów w kolumnach Kod i KodKopia
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                string colName2 = dataGridViewZamowienie.Columns[e.ColumnIndex].Name;
                if (colName2 == "Kod" || colName2 == "KodKopia")
                {
                    var dataRow = (dataGridViewZamowienie.Rows[e.RowIndex].DataBoundItem as DataRowView)?.Row;
                    if (dataRow != null)
                    {
                        int id = dataRow.Field<int>("Id");
                        if (_productImages.TryGetValue(id, out var img) && img != null)
                        {
                            e.PaintBackground(e.CellBounds, true);

                            int imgSize = Math.Min(e.CellBounds.Height - 6, 28);
                            int yImg = e.CellBounds.Y + (e.CellBounds.Height - imgSize) / 2;
                            int pad = 4;
                            string text = e.Value?.ToString() ?? "";

                            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

                            if (colName2 == "Kod")
                            {
                                // Kod (początek): tekst wyrównany w prawo, obrazek po prawej stronie tekstu
                                int xImg = e.CellBounds.Right - imgSize - pad;
                                e.Graphics.DrawImage(img, xImg, yImg, imgSize, imgSize);

                                var textRect = new Rectangle(
                                    e.CellBounds.X + pad,
                                    e.CellBounds.Y,
                                    e.CellBounds.Width - imgSize - pad * 3,
                                    e.CellBounds.Height);

                                TextRenderer.DrawText(e.Graphics, text, e.CellStyle.Font, textRect,
                                    e.CellStyle.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
                            }
                            else
                            {
                                // KodKopia (koniec): obrazek po lewej, tekst wyrównany w lewo
                                int xImg = e.CellBounds.X + pad;
                                e.Graphics.DrawImage(img, xImg, yImg, imgSize, imgSize);

                                var textRect = new Rectangle(
                                    xImg + imgSize + pad,
                                    e.CellBounds.Y,
                                    e.CellBounds.Width - imgSize - pad * 3,
                                    e.CellBounds.Height);

                                TextRenderer.DrawText(e.Graphics, text, e.CellStyle.Font, textRect,
                                    e.CellStyle.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                            }

                            e.Handled = true;
                            return;
                        }
                    }
                }
            }
        }

        private void ClearFormForNewOrder()
        {
            var selectedHandlowiec = cbHandlowiecFilter.SelectedItem;

            _idZamowieniaDoEdycji = null;
            _selectedKlientId = null;
            txtSzukajOdbiorcy.Text = "";
            listaWynikowOdbiorcy.Visible = false;

            _blokujObslugeZmian = true;
            foreach (DataRow r in _dt.Rows)
            {
                r["E2"] = false;
                r["Folia"] = false;
                r["Hallal"] = false;
                r["Strefa"] = false;
                r["Ilosc"] = 0m;
                r["Pojemniki"] = 0m;
                r["Palety"] = 0m;
                r["Cena"] = DBNull.Value;  // NULL, nie 0m ani ""
                r["MaWartosci"] = 1;
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

            if (dateTimePickerGodzinaPrzyjazdu != null)
            {
                dateTimePickerGodzinaPrzyjazdu.Value = DateTime.Today.AddHours(8);
            }

            if (chkWlasnyOdbior != null)
            {
                chkWlasnyOdbior.Checked = false;
            }

            btnZapisz.Text = "Zapisz";

            cbHandlowiecFilter.SelectedItem = selectedHandlowiec;

            if (panelSugestieGodzin != null) panelSugestieGodzin.Controls.Clear();

            _limitKredytowy = 0;
            _doZaplacenia = 0;
            if (lblLimitInfo != null)
            {
                lblLimitInfo.Text = "Wybierz odbiorcę";
                lblLimitInfo.ForeColor = Color.FromArgb(107, 114, 128);
                lblLimitInfo.Font = new Font("Segoe UI", 8f, FontStyle.Regular);
            }
            if (progressLimit != null) progressLimit.Value = 0;
            if (panelPlatnosci != null) panelPlatnosci.BackColor = Color.White;

            RecalcSum();
            txtSzukajOdbiorcy.Focus();
        }
        #endregion

        #region Helper Methods

        private bool ValidateBeforeSave(out string message)
        {
            if (string.IsNullOrWhiteSpace(_selectedKlientId))
            {
                message = "Wybierz odbiorcę.";
                return false;
            }

            bool czyMaJakiekolwiekIlosci = false;
            foreach (DataRow r in _dt.Rows)
            {
                if ((r.Field<decimal?>("Ilosc") ?? 0m) > 0m)
                {
                    czyMaJakiekolwiekIlosci = true;
                    break;
                }
            }

            if (!czyMaJakiekolwiekIlosci)
            {
                message = "Wpisz ilość dla przynajmniej jednego towaru.";
                return false;
            }

            if (_dt.AsEnumerable().Any(r => (r.Field<decimal?>("Ilosc") ?? 0m) < 0m))
            {
                message = "Ilość nie może być ujemna.";
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

        private async Task<string?> CheckForExistingOrder(string klientId, DateTime dataProdukcji)
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

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

                string whereClause = dataProdukcjiExists
                    ? "DataProdukcji = @DataProdukcji"
                    : "DataZamowienia = @DataProdukcji";

                string sql = $@"
            SELECT TOP 1 zm.Id, zm.DataZamowienia, zm.DataPrzyjazdu, zm.LiczbaPalet, zm.LiczbaPojemnikow
            FROM [dbo].[ZamowieniaMieso] zm
            WHERE zm.KlientId = @KlientId 
            AND {whereClause}
            AND zm.Status <> 'Anulowane'
            ORDER BY zm.DataUtworzenia DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@KlientId", int.Parse(klientId));
                cmd.Parameters.AddWithValue("@DataProdukcji", dataProdukcji.Date);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    int existingId = reader.GetInt32(0);
                    DateTime dataZam = reader.GetDateTime(1);
                    DateTime dataPrzyjazdu = reader.GetDateTime(2);
                    decimal palety = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3);
                    int pojemniki = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);

                    var sb = new StringBuilder();
                    sb.AppendLine($"ID Zamówienia: {existingId}");
                    sb.AppendLine($"Data zamówienia: {dataZam:yyyy-MM-dd}");
                    sb.AppendLine($"Termin odbioru: {dataPrzyjazdu:yyyy-MM-dd HH:mm}");
                    sb.AppendLine($"Liczba palet: {palety:N1}");
                    sb.AppendLine($"Liczba pojemników: {pojemniki}");
                    sb.AppendLine();
                    sb.AppendLine("Lista produktów:");

                    reader.Close();

                    var cmdItems = new SqlCommand(@"
                SELECT tw.Kod, zmt.Ilosc, zmt.Pojemniki, zmt.Palety, ISNULL(zmt.Cena, '0') as Cena
                FROM [dbo].[ZamowieniaMiesoTowar] zmt
                JOIN [HANDEL].[HM].[TW] tw ON zmt.KodTowaru = tw.Id
                WHERE zmt.ZamowienieId = @Id", cn);
                    cmdItems.Parameters.AddWithValue("@Id", existingId);

                    await using var readerItems = await cmdItems.ExecuteReaderAsync();
                    while (await readerItems.ReadAsync())
                    {
                        string kod = readerItems.GetString(0);
                        decimal ilosc = readerItems.IsDBNull(1) ? 0 : readerItems.GetDecimal(1);
                        int poj = readerItems.IsDBNull(2) ? 0 : readerItems.GetInt32(2);
                        decimal pal = readerItems.IsDBNull(3) ? 0 : readerItems.GetDecimal(3);

                        // STRING!
                        string cenaStr = readerItems.IsDBNull(4) ? "0" : readerItems.GetString(4);
                        decimal cena = 0m;
                        decimal.TryParse(cenaStr, NumberStyles.Any, CultureInfo.InvariantCulture, out cena);

                        string cenaDisplay = cena > 0 ? $", {cena:N2} zł/kg" : "";
                        sb.AppendLine($"  • {kod}: {ilosc:N0} kg ({poj} poj., {pal:N1} pal.{cenaDisplay})");
                    }

                    return sb.ToString();
                }

                return null;
            }
            catch
            {
                return null;
            }
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

        private async Task<bool> EnsureColumnExists(SqlConnection cn, string columnName, string columnDef)
        {
            try
            {
                bool exists = await CheckIfColumnExists(cn, columnName);
                if (!exists)
                {
                    await using var cmd = new SqlCommand(
                        $"ALTER TABLE [dbo].[ZamowieniaMieso] ADD [{columnName}] {columnDef}", cn);
                    await cmd.ExecuteNonQueryAsync();
                }
                return true;
            }
            catch { return false; }
        }

        private bool? _strefaTowarColumnExists;
        private async Task<bool> CheckStrefaTowarColumnExists(SqlConnection cn)
        {
            if (_strefaTowarColumnExists.HasValue) return _strefaTowarColumnExists.Value;
            try
            {
                await using var cmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'[dbo].[ZamowieniaMiesoTowar]')
                    AND name = 'Strefa'", cn);
                bool exists = (int)await cmd.ExecuteScalarAsync() > 0;
                if (!exists)
                {
                    // Auto-tworzenie kolumny Strefa jeśli nie istnieje
                    await using var alter = new SqlCommand(
                        "ALTER TABLE [dbo].[ZamowieniaMiesoTowar] ADD Strefa BIT NULL DEFAULT 0", cn);
                    await alter.ExecuteNonQueryAsync();
                    exists = true;
                }
                _strefaTowarColumnExists = exists;
            }
            catch { _strefaTowarColumnExists = false; }
            return _strefaTowarColumnExists.Value;
        }

        private string BuildOrderSummary()
        {
            var sb = new StringBuilder();
            var orderedItems = _dt.AsEnumerable().Where(r => r.Field<decimal?>("Ilosc") > 0m).ToList();

            var odbiorca = _kontrahenci.FirstOrDefault(k => k.Id == _selectedKlientId);
            string nazwaOdbiorcy = odbiorca?.Nazwa ?? "Nieznany odbiorca";

            sb.AppendLine($"Odbiorca: {nazwaOdbiorcy}");
            sb.AppendLine($"Data produkcji: {dateTimePickerProdukcji?.Value:yyyy-MM-dd (dddd)}");
            sb.AppendLine($"Data sprzedaży: {dateTimePickerSprzedaz.Value:yyyy-MM-dd (dddd)}");

            if (chkWlasnyOdbior?.Checked == true)
            {
                sb.AppendLine($"Transport: WŁASNY ODBIÓR");
            }

            var swiezeItems = orderedItems.Where(r => r.Field<string>("Katalog") == "67095").ToList();
            var mrozoneItems = orderedItems.Where(r => r.Field<string>("Katalog") == "67153").ToList();

            if (swiezeItems.Any() && mrozoneItems.Any())
            {
                sb.AppendLine($"\nZAMÓWIENIE MIESZANE: {swiezeItems.Count} świeżych + {mrozoneItems.Count} mrożonych");
            }
            else if (mrozoneItems.Any())
            {
                sb.AppendLine($"\nProdukty mrożone: {mrozoneItems.Count}");
            }

            var e2Items = orderedItems.Where(r => r.Field<bool>("E2")).ToList();
            if (e2Items.Any()) sb.AppendLine($"Towary E2 (40 poj./pal.): {e2Items.Count}");

            var foliaItems = orderedItems.Where(r => r.Field<bool>("Folia")).ToList();
            if (foliaItems.Any()) sb.AppendLine($"Towary w folii: {foliaItems.Count}");

            var hallalItems = orderedItems.Where(r => r.Field<bool>("Hallal")).ToList();
            if (hallalItems.Any()) sb.AppendLine($"🔪 Towary Hallal: {hallalItems.Count}");

            sb.AppendLine("\nZamówione towary:");

            decimal totalPojemniki = 0;
            decimal totalPalety = 0;
            decimal totalCena = 0;

            foreach (var item in orderedItems)
            {
                string katalog = item.Field<string>("Katalog") == "67153" ? " [MROŻONY]" : "";
                string e2Marker = item.Field<bool>("E2") ? " [E2]" : "";
                string foliaMarker = item.Field<bool>("Folia") ? " [FOLIA]" : "";
                string hallalMarker = item.Field<bool>("Hallal") ? " [🔪 HALLAL]" : "";
                decimal pojemniki = item.Field<decimal?>("Pojemniki") ?? 0m;
                decimal palety = item.Field<decimal?>("Palety") ?? 0m;

                // POPRAWIONE - odczytywanie STRING
                decimal cena = 0m;
                if (!item.IsNull("Cena"))
                {
                    string cenaStr = item.Field<string>("Cena") ?? "";
                    decimal.TryParse(cenaStr, NumberStyles.Any, _pl, out cena);
                }

                totalPojemniki += pojemniki;
                totalPalety += palety;

                if (cena > 0)
                {
                    totalCena += cena * (item.Field<decimal?>("Ilosc") ?? 0m);
                }

                string cenaStrDisplay = cena > 0 ? $", {cena:N2} zł/kg" : "";

                sb.AppendLine($"  {item.Field<string>("Kod")}{katalog}{e2Marker}{foliaMarker}{hallalMarker}: {item.Field<decimal?>("Ilosc") ?? 0m:N0} kg " +
                            $"({pojemniki:N0} poj., {palety:N1} pal.{cenaStrDisplay})");
            }

            decimal totalKg = orderedItems.Sum(r => r.Field<decimal?>("Ilosc") ?? 0m);
            sb.AppendLine($"\nPodsumowanie:");
            sb.AppendLine($"  Łącznie: {totalKg:N0} kg");
            sb.AppendLine($"  Pojemników: {totalPojemniki:N0}");
            sb.AppendLine($"  Palet: {totalPalety:N1}");
            if (totalCena > 0)
            {
                sb.AppendLine($"  Wartość: {totalCena:N2} zł");
            }

            return sb.ToString();
        }
        #endregion

        #region MessageBox Helpers
        private void ShowInfo(string message, string title = "Informacja")
        {
            ToastNotification.Show(this, message, ToastNotification.ToastType.Info);
        }

        private void ShowWarning(string message, string title = "Ostrzeżenie")
        {
            ToastNotification.Show(this, message, ToastNotification.ToastType.Warning);
        }

        private void ShowError(string message, string title = "Błąd")
        {
            MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private DialogResult ShowQuestion(string message, string title = "Pytanie")
        {
            return MessageBox.Show(this, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        }

        private DialogResult ShowWarningQuestion(string message, string title = "Uwaga")
        {
            return MessageBox.Show(this, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        }
        #endregion

        #region Nested Classes - Dialogs

        private class DuplicateOrderDialog : Form
        {
            public bool ProceedWithSave { get; private set; } = false;

            public DuplicateOrderDialog(string odbiorcaNazwa, DateTime dataProdukcji, string existingOrderDetails)
            {
                Text = "⚠ Ostrzeżenie - Znaleziono istniejące zamówienie";
                Size = new Size(650, 520);
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                BackColor = Color.White;
                Font = new Font("Segoe UI", 9.5f);

                var headerPanel = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 80,
                    BackColor = Color.FromArgb(231, 76, 60)
                };

                var iconLabel = new Label
                {
                    Text = "⚠",
                    Font = new Font("Segoe UI", 36f, FontStyle.Bold),
                    ForeColor = Color.White,
                    Location = new Point(20, 15),
                    Size = new Size(50, 50),
                    TextAlign = ContentAlignment.MiddleCenter
                };

                var titleLabel = new Label
                {
                    Text = "UWAGA - DUPLIKAT ZAMÓWIENIA",
                    Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                    ForeColor = Color.White,
                    Location = new Point(80, 25),
                    AutoSize = true
                };

                headerPanel.Controls.AddRange(new Control[] { iconLabel, titleLabel });

                var infoPanel = new Panel
                {
                    Location = new Point(20, 100),
                    Size = new Size(610, 280),
                    BackColor = Color.FromArgb(255, 243, 224),
                    BorderStyle = BorderStyle.FixedSingle
                };

                var lblInfo = new Label
                {
                    Text = $"⚠ ZNALEZIONO ISTNIEJĄCE ZAMÓWIENIE\n\n" +
                           $"Dla odbiorcy: {odbiorcaNazwa}\n" +
                           $"Z datą produkcji: {dataProdukcji:yyyy-MM-dd (dddd)}\n\n" +
                           $"Czy na pewno chcesz utworzyć kolejne zamówienie na ten sam dzień?\n\n" +
                           $"Szczegóły istniejącego zamówienia:",
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(120, 53, 15),
                    Location = new Point(15, 15),
                    Size = new Size(580, 120),
                    TextAlign = ContentAlignment.TopLeft
                };

                var txtDetails = new TextBox
                {
                    Text = existingOrderDetails,
                    Location = new Point(15, 140),
                    Size = new Size(580, 125),
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    Font = new Font("Segoe UI", 9f),
                    BackColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };

                infoPanel.Controls.AddRange(new Control[] { lblInfo, txtDetails });

                var btnYes = new Button
                {
                    Text = "TAK - Utwórz nowe zamówienie",
                    Size = new Size(200, 45),
                    Location = new Point(120, 405),
                    BackColor = Color.FromArgb(231, 76, 60),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                btnYes.FlatAppearance.BorderSize = 0;
                btnYes.Click += (s, e) => { ProceedWithSave = true; DialogResult = DialogResult.OK; Close(); };

                var btnNo = new Button
                {
                    Text = "NIE - Anuluj",
                    Size = new Size(160, 45),
                    Location = new Point(330, 405),
                    BackColor = Color.FromArgb(149, 165, 166),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                btnNo.FlatAppearance.BorderSize = 0;
                btnNo.Click += (s, e) => { ProceedWithSave = false; DialogResult = DialogResult.Cancel; Close(); };

                Controls.AddRange(new Control[] { headerPanel, infoPanel, btnYes, btnNo });
            }
        }

        private class AfterSaveDialog : Form
        {
            public bool CreateAnother { get; private set; } = false;
            private readonly Dictionary<string, Image> _images;

            public AfterSaveDialog(string orderSummary, bool isEdit, Dictionary<string, Image>? productImages = null)
            {
                _images = productImages ?? new();
                Text = isEdit ? "✓ Zamówienie zaktualizowane" : "✓ Zamówienie zapisane";
                Size = new Size(700, 750);
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                TopMost = true;
                BackColor = Color.White;
                Font = new Font("Segoe UI", 9.5f);

                var mainPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    Padding = new Padding(0)
                };

                var headerPanel = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 90,
                    BackColor = Color.FromArgb(92, 138, 58)
                };

                var iconLabel = new Label
                {
                    Text = "✓",
                    Font = new Font("Segoe UI", 40f, FontStyle.Bold),
                    ForeColor = Color.White,
                    Location = new Point(20, 20),
                    Size = new Size(60, 50),
                    TextAlign = ContentAlignment.MiddleCenter
                };

                var titleLabel = new Label
                {
                    Text = (isEdit ? "ZAMÓWIENIE ZAKTUALIZOWANE" : "ZAMÓWIENIE ZAPISANE").ToUpper(),
                    Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                    ForeColor = Color.White,
                    Location = new Point(90, 30),
                    AutoSize = true
                };

                headerPanel.Controls.AddRange(new Control[] { iconLabel, titleLabel });

                var contentPanel = new Panel
                {
                    Location = new Point(0, 90),
                    Size = new Size(684, 560),
                    AutoScroll = true,
                    Padding = new Padding(20)
                };

                int yPos = 20;

                var lines = orderSummary.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                var odbiorcy = lines.FirstOrDefault(l => l.StartsWith("Odbiorca:"))?.Replace("Odbiorca:", "").Trim();
                if (!string.IsNullOrEmpty(odbiorcy))
                {
                    var odbPanel = CreateInfoBox("📦 ODBIORCA", odbiorcy, Color.FromArgb(52, 152, 219), yPos);
                    contentPanel.Controls.Add(odbPanel);
                    yPos += 80;
                }

                var dataProdukcji = lines.FirstOrDefault(l => l.StartsWith("Data produkcji:"))?.Replace("Data produkcji:", "").Trim();
                var dataSprzedazy = lines.FirstOrDefault(l => l.StartsWith("Data sprzedaży:"))?.Replace("Data sprzedaży:", "").Trim();
                var transport = lines.FirstOrDefault(l => l.StartsWith("Transport:"))?.Replace("Transport:", "").Trim();

                if (!string.IsNullOrEmpty(dataProdukcji))
                {
                    var prodPanel = CreateInfoBox("🏭 DATA PRODUKCJI", dataProdukcji, Color.FromArgb(155, 89, 182), yPos);
                    contentPanel.Controls.Add(prodPanel);
                    yPos += 80;
                }

                if (!string.IsNullOrEmpty(dataSprzedazy))
                {
                    var sprzPanel = CreateInfoBox("🚚 DATA ODBIORU", dataSprzedazy, Color.FromArgb(230, 126, 34), yPos);
                    contentPanel.Controls.Add(sprzPanel);
                    yPos += 80;
                }

                if (!string.IsNullOrEmpty(transport))
                {
                    var transPanel = CreateInfoBox("🚗 TRANSPORT", transport, Color.FromArgb(46, 204, 113), yPos);
                    contentPanel.Controls.Add(transPanel);
                    yPos += 80;
                }

                var towarStartIndex = Array.FindIndex(lines, l => l.Contains("Zamówione towary:"));
                var podsumowanieIndex = Array.FindIndex(lines, l => l.Contains("Podsumowanie:"));

                if (towarStartIndex >= 0 && podsumowanieIndex > towarStartIndex)
                {
                    var towaryPanel = new Panel
                    {
                        Location = new Point(20, yPos),
                        Size = new Size(640, 200),
                        BackColor = Color.FromArgb(240, 247, 237),
                        BorderStyle = BorderStyle.FixedSingle,
                        AutoScroll = true
                    };

                    var towaryLabel = new Label
                    {
                        Text = "📋 ZAMÓWIONE TOWARY",
                        Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                        ForeColor = Color.FromArgb(31, 41, 55),
                        Location = new Point(10, 10),
                        AutoSize = true
                    };
                    towaryPanel.Controls.Add(towaryLabel);

                    int towYPos = 40;
                    for (int i = towarStartIndex + 1; i < podsumowanieIndex; i++)
                    {
                        var line = lines[i].Trim();
                        if (string.IsNullOrEmpty(line)) continue;

                        // Dopasuj zdjęcie po kodzie produktu (pierwsza część linii przed " - ")
                        int xOffset = 15;
                        var kodPart = line.Split(new[] { " - " }, StringSplitOptions.None)[0].Trim();
                        Image? matchedImg = null;
                        foreach (var kvp in _images)
                        {
                            if (kodPart.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                                kodPart.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                            {
                                matchedImg = kvp.Value;
                                break;
                            }
                        }

                        if (matchedImg != null)
                        {
                            var pb = new PictureBox
                            {
                                Image = matchedImg,
                                SizeMode = PictureBoxSizeMode.Zoom,
                                Size = new Size(22, 22),
                                Location = new Point(15, towYPos)
                            };
                            towaryPanel.Controls.Add(pb);
                            xOffset = 42;
                        }

                        var itemLabel = new Label
                        {
                            Text = line,
                            Font = new Font("Segoe UI", 9f),
                            ForeColor = Color.FromArgb(55, 65, 81),
                            Location = new Point(xOffset, towYPos),
                            Size = new Size(600 - (xOffset - 15), 22)
                        };
                        towaryPanel.Controls.Add(itemLabel);
                        towYPos += 26;
                    }

                    contentPanel.Controls.Add(towaryPanel);
                    yPos += 210;
                }

                var podsumowanie = lines.Skip(podsumowanieIndex + 1).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

                var summaryPanel = new Panel
                {
                    Location = new Point(20, yPos),
                    Size = new Size(640, 120),
                    BackColor = Color.FromArgb(224, 242, 215),
                    BorderStyle = BorderStyle.FixedSingle
                };

                var summTitle = new Label
                {
                    Text = "✓ PODSUMOWANIE",
                    Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(92, 138, 58),
                    Location = new Point(15, 10),
                    AutoSize = true
                };
                summaryPanel.Controls.Add(summTitle);

                int summYPos = 45;
                foreach (var summLine in podsumowanie)
                {
                    var parts = summLine.Split(':');
                    if (parts.Length == 2)
                    {
                        var summLabel = new Label
                        {
                            Text = parts[0].Trim() + ":",
                            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                            ForeColor = Color.FromArgb(55, 65, 81),
                            Location = new Point(20, summYPos),
                            Size = new Size(150, 20)
                        };

                        var summValue = new Label
                        {
                            Text = parts[1].Trim(),
                            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                            ForeColor = Color.FromArgb(92, 138, 58),
                            Location = new Point(180, summYPos - 2),
                            AutoSize = true
                        };

                        summaryPanel.Controls.AddRange(new Control[] { summLabel, summValue });
                        summYPos += 25;
                    }
                }

                contentPanel.Controls.Add(summaryPanel);

                var questionPanel = new Panel
                {
                    Location = new Point(0, 655),
                    Size = new Size(700, 95),
                    BackColor = Color.FromArgb(236, 240, 241),
                    Dock = DockStyle.Bottom
                };

                var lblQuestion = new Label
                {
                    Text = "Co chcesz zrobić teraz?",
                    Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(44, 62, 80),
                    Location = new Point(0, 15),
                    Size = new Size(700, 25),
                    TextAlign = ContentAlignment.MiddleCenter
                };

                var btnNewOrder = new Button
                {
                    Text = "➕ UTWÓRZ KOLEJNE ZAMÓWIENIE",
                    Size = new Size(280, 48),
                    Location = new Point(80, 45),
                    BackColor = Color.FromArgb(92, 138, 58),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                btnNewOrder.FlatAppearance.BorderSize = 0;
                btnNewOrder.Click += (s, e) => { CreateAnother = true; DialogResult = DialogResult.OK; Close(); };

                var btnBackToSummary = new Button
                {
                    Text = "◀ WRÓĆ DO PODSUMOWANIA",
                    Size = new Size(280, 48),
                    Location = new Point(370, 45),
                    BackColor = Color.FromArgb(52, 152, 219),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                btnBackToSummary.FlatAppearance.BorderSize = 0;
                btnBackToSummary.Click += (s, e) => { CreateAnother = false; DialogResult = DialogResult.OK; Close(); };

                questionPanel.Controls.AddRange(new Control[] { lblQuestion, btnNewOrder, btnBackToSummary });

                mainPanel.Controls.Add(contentPanel);
                Controls.Add(headerPanel);
                Controls.Add(mainPanel);
                Controls.Add(questionPanel);
            }

            private static Panel CreateInfoBox(string title, string value, Color accentColor, int yPos)
            {
                var panel = new Panel
                {
                    Location = new Point(20, yPos),
                    Size = new Size(640, 70),
                    BackColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };

                panel.Paint += (s, e) =>
                {
                    using var brush = new SolidBrush(accentColor);
                    e.Graphics.FillRectangle(brush, 0, 0, 6, panel.Height);
                };

                var titleLabel = new Label
                {
                    Text = title,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    ForeColor = accentColor,
                    Location = new Point(15, 12),
                    AutoSize = true
                };

                var valueLabel = new Label
                {
                    Text = value,
                    Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(31, 41, 55),
                    Location = new Point(15, 35),
                    Size = new Size(610, 28)
                };

                panel.Controls.AddRange(new Control[] { titleLabel, valueLabel });
                return panel;
            }
        }

        #endregion
    }
}