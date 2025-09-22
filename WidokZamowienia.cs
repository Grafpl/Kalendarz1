// Plik: WidokZamowienia.cs
// WERSJA 15.1 - FINALNA Z WSZYSTKIMI POPRAWKAMI
// Zmiany:
// 1. Lista rozwijana 50px poniżej textboxa
// 2. ComboBox handlowca 140px szerokości
// 3. Zielony/czerwony dla limitów transportowych
// 4. Kolumna "40 E2" zamiast "E2"
// 5. Panel odbiorców 260-380px wysokości
// 6. Wysokość wierszy grida 32px

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
        private const decimal KG_NA_PALECIE = POJEMNIKOW_NA_PALECIE * KG_NA_POJEMNIKU; // 540
        private const decimal KG_NA_PALECIE_E2 = POJEMNIKOW_NA_PALECIE_E2 * KG_NA_POJEMNIKU; // 600

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
        }

        private readonly DataTable _dt = new();
        private DataView _view = default!;
        private readonly List<KontrahentInfo> _kontrahenci = new();
        private readonly Dictionary<string, DateTime> _ostatnieZamowienia = new();

        // ===== Panel podsumowania =====
        private Panel? panelSummary;
        private Label? lblSumaPalet;
        private Label? lblSumaPojemnikow;
        private Label? lblSumaKg;

        // ===== Paski postępu transportu =====
        private Panel? panelTransport;
        private ProgressBar? progressSolowka;
        private ProgressBar? progressTir;
        private Label? lblSolowka;
        private Label? lblTir;
        private Label? lblSolowkaInfo;
        private Label? lblTirInfo;

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
            ApplyModernUIStyles();
            CreateHeaderIcons();
            SzybkiGrid();
            WireShortcuts();
            BuildDataTableSchema();
            InitDefaults();
            CreateSummaryPanel();
            CreateResponsiveTransportPanel();
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
                    lblTytul.Text = "Edycja zamówienia";
                    btnZapisz.Text = "Zapisz zmiany";
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
            // Ustaw minimalny rozmiar formularza
            this.MinimumSize = new Size(1024, 600);

            // Skonfiguruj responsywny layout dla głównego panelu
            if (panelDetaleZamowienia != null)
            {
                panelDetaleZamowienia.AutoScroll = true;
                panelDetaleZamowienia.Padding = new Padding(10, 5, 10, 5);

                // Reorganizuj kontrolki w panelu detali używając TableLayoutPanel
                ReorganizeDetailsPanel();
            }

            // Reorganizuj panel nagłówka
            ReorganizeHeaderPanel();

            // Ustaw anchor dla kontrolek w panelu odbiorcy
            if (txtSzukajOdbiorcy != null)
            {
                txtSzukajOdbiorcy.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            }

            // Ustaw responsywność dla listy wyników
            if (listaWynikowOdbiorcy != null)
            {
                listaWynikowOdbiorcy.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                listaWynikowOdbiorcy.MaximumSize = new Size(0, 155);
                listaWynikowOdbiorcy.BringToFront(); // Ważne - lista musi być na wierzchu
            }
        }

        private void ReorganizeHeaderPanel()
        {
            if (panelOdbiorca == null) return;

            // Zmniejsz padding górnego panelu
            panelOdbiorca.Padding = new Padding(10, 10, 10, 5);
            panelOdbiorca.Height = 120; // Zmniejszona wysokość

            // Usuń starą etykietę handlowca jeśli istnieje
            var oldHandlowiecLabel = panelOdbiorca.Controls["label4"];
            if (oldHandlowiecLabel != null)
            {
                panelOdbiorca.Controls.Remove(oldHandlowiecLabel);
            }

            // Przenieś combobox handlowca obok tytułu - JESZCZE MNIEJSZA szerokość
            if (lblTytul != null && cbHandlowiecFilter != null)
            {
                lblTytul.Location = new Point(10, 10);
                lblTytul.Size = new Size(180, 30);

                cbHandlowiecFilter.Location = new Point(195, 10);
                cbHandlowiecFilter.Size = new Size(140, 30);  // Zmniejszone z 170 na 140
                cbHandlowiecFilter.Font = new Font("Segoe UI", 10f);  // Zmniejszona czcionka z 11f na 10f
            }

            // Przesuń pole szukania odbiorcy wyżej
            var lblOdbiorca = panelOdbiorca.Controls["label1"];
            if (lblOdbiorca != null)
            {
                lblOdbiorca.Location = new Point(10, 50);
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

            // Usuń wszystkie kontrolki i reorganizuj je
            panelDetaleZamowienia.SuspendLayout();

            // Utworz główny TableLayoutPanel
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoScroll = true,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(5, 0, 5, 5) // Zmniejszony górny padding
            };

            // Konfiguracja wierszy - wszystko przesunięte wyżej
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Panel odbiorców
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Daty
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Notatka
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Transport
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Wypełnienie

            // 1. Panel ostatnich odbiorców - jeszcze większy
            if (panelOstatniOdbiorcy != null)
            {
                panelOstatniOdbiorcy.Dock = DockStyle.Top;
                panelOstatniOdbiorcy.MinimumSize = new Size(0, 260);  // Zwiększone z 220
                panelOstatniOdbiorcy.MaximumSize = new Size(0, 380);  // Zwiększone z 320
                panelOstatniOdbiorcy.AutoSize = false;
                panelOstatniOdbiorcy.Margin = new Padding(0, 0, 0, 5);
                mainLayout.Controls.Add(panelOstatniOdbiorcy, 0, 0);
            }

            // 2. Panel dat - kompaktowy
            var datesPanel = CreateDatesPanel();
            datesPanel.Height = 70; // Zmniejszona wysokość
            mainLayout.Controls.Add(datesPanel, 0, 1);

            // 3. Panel notatki - kompaktowy
            var notesPanel = CreateNotesPanel();
            notesPanel.Height = 100; // Zmniejszona wysokość
            mainLayout.Controls.Add(notesPanel, 0, 2);

            // 4. Panel transportu - poziomy układ
            if (panelTransport != null)
            {
                panelTransport.Dock = DockStyle.Top;
                panelTransport.Height = 100; // Zmniejszona wysokość dla poziomego układu
                mainLayout.Controls.Add(panelTransport, 0, 3);
            }

            // Dodaj główny layout do panelu
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
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Etykiety
            var lblDate = new Label
            {
                Text = "📅 Data odbioru",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(31, 41, 55),
                AutoSize = true,
                Padding = new Padding(0, 0, 0, 5)
            };

            var lblTime = new Label
            {
                Text = "🕐 Godzina odbioru",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(31, 41, 55),
                AutoSize = true,
                Padding = new Padding(5, 0, 0, 5)
            };

            // DateTimePickers
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
                Padding = new Padding(5)
            };

            var lblNotes = new Label
            {
                Text = "Notatka",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(107, 114, 128),
                Location = new Point(5, 5),
                AutoSize = true
            };

            if (textBoxUwagi != null)
            {
                textBoxUwagi.Location = new Point(5, 25);
                textBoxUwagi.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
                textBoxUwagi.Size = new Size(panel.Width - 10, 85);
            }

            panel.Controls.Add(lblNotes);
            panel.Controls.Add(textBoxUwagi);

            return panel;
        }

        private void CreateResponsiveTransportPanel()
        {
            panelTransport = new Panel
            {
                Height = 100, // Zmniejszona wysokość dla poziomego układu
                Dock = DockStyle.Top,
                BackColor = Color.White,
                Padding = new Padding(5)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2, // Dwie kolumny dla poziomego układu
                RowCount = 2,
                AutoSize = false
            };

            // Konfiguracja kolumn - równy podział
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            // Konfiguracja wierszy
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F)); // Nagłówek
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Paski postępu

            // Nagłówek
            var lblTransportHeader = new Label
            {
                Text = "LIMITY TRANSPORTOWE",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(75, 85, 99),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            layout.Controls.Add(lblTransportHeader, 0, 0);
            layout.SetColumnSpan(lblTransportHeader, 2); // Rozciągnij na 2 kolumny

            // Panel dla Solówki (lewa kolumna)
            var solowkaPanel = CreateCompactProgressPanel("Solówka (18 pal.)", 18, out progressSolowka, out lblSolowkaInfo);
            layout.Controls.Add(solowkaPanel, 0, 1);

            // Panel dla TIRa (prawa kolumna)
            var tirPanel = CreateCompactProgressPanel("TIR (33 pal.)", 33, out progressTir, out lblTirInfo);
            layout.Controls.Add(tirPanel, 1, 1);

            panelTransport.Controls.Add(layout);

            // Stylowanie panelu
            panelTransport.Paint += (s, e) =>
            {
                using var path = GetRoundedRectPath(panelTransport.ClientRectangle, 8);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(Color.FromArgb(249, 250, 251));
                e.Graphics.FillPath(brush, path);
                using var pen = new Pen(Color.FromArgb(229, 231, 235), 1);
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

            // Kolumny - pasek zajmuje więcej miejsca
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            // Wiersze
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
                Font = new Font("Segoe UI Semibold", 9f),
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

            // Ustaw responsywność panelu
            panelOstatniOdbiorcy.AutoSize = false;
            panelOstatniOdbiorcy.Dock = DockStyle.Top;

            // Ustaw responsywność grida
            gridOstatniOdbiorcy.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            gridOstatniOdbiorcy.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // Stylowanie panelu
            panelOstatniOdbiorcy.Paint += (s, e) =>
            {
                using var path = GetRoundedRectPath(panelOstatniOdbiorcy.ClientRectangle, 8);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(Color.FromArgb(249, 250, 251));
                e.Graphics.FillPath(brush, path);
                using var pen = new Pen(Color.FromArgb(229, 231, 235), 1);
                e.Graphics.DrawPath(pen, path);
            };

            // Stylowanie grida
            gridOstatniOdbiorcy.DefaultCellStyle.SelectionBackColor = Color.FromArgb(99, 102, 241);
            gridOstatniOdbiorcy.DefaultCellStyle.SelectionForeColor = Color.White;
            gridOstatniOdbiorcy.DefaultCellStyle.Padding = new Padding(8, 3, 8, 3);
            gridOstatniOdbiorcy.RowTemplate.Height = 28;
            gridOstatniOdbiorcy.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);

            // Obsługa zdarzeń
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

            for (int i = 0; i < odbiorcy.Count; i += 2)
            {
                var row = dt.NewRow();
                row["Kolumna1"] = odbiorcy[i];
                row["Kolumna2"] = (i + 1 < odbiorcy.Count) ? odbiorcy[i + 1] : "";
                dt.Rows.Add(row);
            }

            gridOstatniOdbiorcy.DataSource = dt;

            // Ustaw grid aby wykorzystywał całą dostępną wysokość
            if (gridOstatniOdbiorcy.Parent != null)
            {
                gridOstatniOdbiorcy.Location = new Point(10, 30);
                gridOstatniOdbiorcy.Size = new Size(390, panelOstatniOdbiorcy.Height - 40);  // Wykorzystaj całą wysokość panelu
            }

            // Ustaw automatyczne dopasowanie szerokości kolumn
            if (gridOstatniOdbiorcy.Columns.Count > 0)
            {
                gridOstatniOdbiorcy.Columns["Kolumna1"].FillWeight = 50;
                gridOstatniOdbiorcy.Columns["Kolumna2"].FillWeight = 50;
            }

            // Zastosuj formatowanie do wierszy
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
                            row.Cells[col].Style.ForeColor = Color.FromArgb(16, 185, 129);
                        }
                    }
                }
            }

            lblOstatniOdbiorcy.Text = $"Odbiorcy ({odbiorcy.Count}):";
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

            // Aktualizuj pasek solówki
            progressSolowka.Value = Math.Min(paletyInt, 18);
            lblSolowkaInfo!.Text = $"{paletyInt:N0} / 18";

            // Zmień kolor paska w zależności od wypełnienia - SOLÓWKA
            if (paletyInt <= 18)
            {
                // Zielony - mieści się w limicie
                SetProgressBarColor(progressSolowka, Color.FromArgb(34, 197, 94));
                lblSolowkaInfo.ForeColor = Color.FromArgb(34, 197, 94);
            }
            else
            {
                // Czerwony - przekroczony limit
                SetProgressBarColor(progressSolowka, Color.FromArgb(239, 68, 68));
                lblSolowkaInfo.ForeColor = Color.FromArgb(239, 68, 68);
                lblSolowkaInfo.Text = $"{paletyInt:N0} / 18 ⚠";
            }

            // Aktualizuj pasek TIRa
            progressTir.Value = Math.Min(paletyInt, 33);
            lblTirInfo!.Text = $"{paletyInt:N0} / 33";

            // Zmień kolor paska TIR w zależności od wypełnienia
            if (paletyInt <= 33)
            {
                // Zielony - mieści się w limicie
                SetProgressBarColor(progressTir, Color.FromArgb(34, 197, 94));
                lblTirInfo.ForeColor = Color.FromArgb(34, 197, 94);
            }
            else
            {
                // Czerwony - przekroczony limit
                SetProgressBarColor(progressTir, Color.FromArgb(239, 68, 68));
                lblTirInfo.ForeColor = Color.FromArgb(239, 68, 68);
                lblTirInfo.Text = $"{paletyInt:N0} / 33 ⚠";
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

            lblSumaPalet = CreateSummaryLabel("PALETY", "0");
            lblSumaPojemnikow = CreateSummaryLabel("POJEMNIKI", "0");
            lblSumaKg = CreateSummaryLabel("KILOGRAMY", "0");

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
                lblTytul.Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold);
                lblTytul.ForeColor = Color.FromArgb(17, 24, 39);
            }

            StyleButton(btnZapisz, Color.FromArgb(99, 102, 241), Color.White);

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

            // Ulepszenie etykiet
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
            dataGridViewZamowienie.RowHeadersVisible = false;
            dataGridViewZamowienie.SelectionMode = DataGridViewSelectionMode.CellSelect;
            dataGridViewZamowienie.MultiSelect = true;
            dataGridViewZamowienie.EditMode = DataGridViewEditMode.EditOnKeystroke;
            dataGridViewZamowienie.BackgroundColor = Color.White;
            dataGridViewZamowienie.BorderStyle = BorderStyle.None;
            dataGridViewZamowienie.GridColor = Color.FromArgb(243, 244, 246);
            dataGridViewZamowienie.Font = new Font("Segoe UI", 10f);

            dataGridViewZamowienie.EnableHeadersVisualStyles = false;
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(75, 85, 99);
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(249, 250, 251);
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(75, 85, 99);
            dataGridViewZamowienie.ColumnHeadersHeight = 42;
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewZamowienie.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;

            dataGridViewZamowienie.RowTemplate.Height = 32;  // Zmniejszone z 38 na 32
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
            cKod.DefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            cKod.DefaultCellStyle.ForeColor = Color.FromArgb(31, 41, 55);
            cKod.HeaderText = "Towar";

            var cE2 = dataGridViewZamowienie.Columns["E2"] as DataGridViewCheckBoxColumn;
            if (cE2 != null)
            {
                cE2.HeaderText = "40 E2";  // Zmieniona nazwa kolumny
                cE2.FillWeight = 50;  // Zwiększone z 40 dla dłuższej nazwy
                cE2.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                cE2.ToolTipText = "40 pojemników/paletę";
            }

            var cPalety = dataGridViewZamowienie.Columns["Palety"]!;
            cPalety.FillWeight = 80;
            cPalety.DefaultCellStyle.Format = "N0";
            cPalety.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            cPalety.DefaultCellStyle.Font = new Font("Segoe UI Semibold", 11f);
            cPalety.DefaultCellStyle.ForeColor = Color.FromArgb(239, 68, 68);

            var cPojemniki = dataGridViewZamowienie.Columns["Pojemniki"]!;
            cPojemniki.FillWeight = 100;
            cPojemniki.DefaultCellStyle.Format = "N0";
            cPojemniki.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            cPojemniki.DefaultCellStyle.ForeColor = Color.FromArgb(59, 130, 246);

            var cIlosc = dataGridViewZamowienie.Columns["Ilosc"]!;
            cIlosc.FillWeight = 110;
            cIlosc.DefaultCellStyle.Format = "N0";
            cIlosc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            cIlosc.HeaderText = "Ilość (kg)";
            cIlosc.DefaultCellStyle.Font = new Font("Segoe UI Semibold", 11f);
            cIlosc.DefaultCellStyle.ForeColor = Color.FromArgb(16, 185, 129);

            var cKodKopia = dataGridViewZamowienie.Columns["KodKopia"]!;
            cKodKopia.ReadOnly = true;
            cKodKopia.FillWeight = 180;
            cKodKopia.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            cKodKopia.DefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            cKodKopia.DefaultCellStyle.ForeColor = Color.FromArgb(31, 41, 55);
            cKodKopia.DefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
            cKodKopia.HeaderText = "Towar";
        }

        private void WireUpUIEvents()
        {
            dataGridViewZamowienie.CellValueChanged += DataGridViewZamowienie_CellValueChanged;
            dataGridViewZamowienie.EditingControlShowing += DataGridViewZamowienie_EditingControlShowing;
            dataGridViewZamowienie.CellPainting += DataGridViewZamowienie_CellPainting;
            dataGridViewZamowienie.ColumnWidthChanged += (s, e) => dataGridViewZamowienie.Invalidate();
            dataGridViewZamowienie.CurrentCellDirtyStateChanged += DataGridViewZamowienie_CurrentCellDirtyStateChanged;

            txtSzukajOdbiorcy.TextChanged += TxtSzukajOdbiorcy_TextChanged;
            txtSzukajOdbiorcy.KeyDown += TxtSzukajOdbiorcy_KeyDown;

            // Zmiana z DoubleClick na Click dla szybszego wyboru
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
            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand("SELECT Id, Kod FROM [HANDEL].[HM].[TW] WHERE katalog = '67095' ORDER BY Kod ASC", cn);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                var kod = rd.GetString(1);
                _dt.Rows.Add(rd.GetInt32(0), kod, false, 0m, 0m, 0m, kod, kod);
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
                // Ustaw pozycję listy względem textboxa - BARDZO NISKO
                var screenPoint = txtSzukajOdbiorcy.Parent.PointToScreen(txtSzukajOdbiorcy.Location);
                var clientPoint = panelDetaleZamowienia.PointToClient(screenPoint);

                listaWynikowOdbiorcy.Location = new Point(
                    clientPoint.X,
                    clientPoint.Y + txtSzukajOdbiorcy.Height + 50  // Zwiększone z 35 na 50 pikseli - bardzo duży odstęp
                );
                listaWynikowOdbiorcy.Width = txtSzukajOdbiorcy.Width;
                listaWynikowOdbiorcy.Height = Math.Min(180, wyniki.Count * 22 + 5);

                listaWynikowOdbiorcy.BackColor = Color.White;
                listaWynikowOdbiorcy.ForeColor = Color.FromArgb(31, 41, 55);
                listaWynikowOdbiorcy.BorderStyle = BorderStyle.FixedSingle;
                listaWynikowOdbiorcy.Font = new Font("Segoe UI", 9.5f);

                listaWynikowOdbiorcy.Visible = true;
                listaWynikowOdbiorcy.BringToFront(); // Upewnij się, że lista jest na wierzchu
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
                lblAdres.Text = $"{info.KodPocztowy} {info.Miejscowosc}";
                lblHandlowiec.Text = $"Opiekun: {info.Handlowiec}";
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

            bool useE2 = row.Field<bool>("E2");
            decimal pojemnikNaPalete = useE2 ? POJEMNIKOW_NA_PALECIE_E2 : POJEMNIKOW_NA_PALECIE;
            decimal kgNaPalete = useE2 ? KG_NA_PALECIE_E2 : KG_NA_PALECIE;

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
                        row["Pojemniki"] = (ilosc > 0 && KG_NA_POJEMNIKU > 0) ? Math.Round(ilosc / KG_NA_POJEMNIKU, 0) : 0m;
                        row["Palety"] = (ilosc > 0 && kgNaPalete > 0) ? ilosc / kgNaPalete : 0m;
                        MarkInvalid(dataGridViewZamowienie.Rows[e.RowIndex].Cells["Ilosc"], ilosc < 0);
                        break;

                    case "Pojemniki":
                        decimal pojemniki = ParseDec(row["Pojemniki"]);
                        row["Ilosc"] = pojemniki * KG_NA_POJEMNIKU;
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
            if (e.RowIndex != -1 || e.ColumnIndex < 0) return;

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

            lblTytul.Text = "Nowe zamówienie";
            btnZapisz.Text = "Zapisz";

            cbHandlowiecFilter.SelectedItem = selectedHandlowiec;

            RecalcSum();
            txtSzukajOdbiorcy.Focus();
        }

        #endregion

        #region Zapis i Odczyt Zamówienia

        private async Task LoadZamowienieAsync(int id)
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            await using (var cmdZ = new SqlCommand("SELECT DataZamowienia, KlientId, Uwagi, DataPrzyjazdu FROM [dbo].[ZamowieniaMieso] WHERE Id=@Id", cn))
            {
                cmdZ.Parameters.AddWithValue("@Id", id);
                await using var rd = await cmdZ.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    dateTimePickerSprzedaz.Value = rd.GetDateTime(0);
                    UstawOdbiorce(rd.GetInt32(1).ToString());
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
                string title = _idZamowieniaDoEdycji.HasValue ? "Zamówienie zaktualizowane" : "Zamówienie zapisane";

                MessageBox.Show(summary, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadOstatnieZamowienia();
                UpdateOstatniOdbiorcyGrid(cbHandlowiecFilter.SelectedItem?.ToString());
                ClearFormForNewOrder();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zapisu: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            sb.AppendLine($"Odbiorca: {lblWybranyOdbiorca.Text}");
            sb.AppendLine($"Data sprzedaży: {dateTimePickerSprzedaz.Value:yyyy-MM-dd}");

            var e2Items = orderedItems.Where(r => r.Field<bool>("E2")).ToList();
            if (e2Items.Any())
            {
                sb.AppendLine($"Towary E2 (40 poj./pal.): {e2Items.Count}");
            }

            sb.AppendLine("\nZamówione towary:");

            decimal totalPojemniki = 0;
            decimal totalPalety = 0;

            foreach (var item in orderedItems)
            {
                string e2Marker = item.Field<bool>("E2") ? " [E2]" : "";
                decimal pojemniki = item.Field<decimal>("Pojemniki");
                decimal palety = item.Field<decimal>("Palety");

                totalPojemniki += pojemniki;
                totalPalety += palety;

                sb.AppendLine($"  {item.Field<string>("Kod")}{e2Marker}: {item.Field<decimal>("Ilosc"):N0} kg " +
                            $"({pojemniki:N0} poj., {palety:N1} pal.)");
            }

            decimal totalKg = orderedItems.Sum(r => r.Field<decimal>("Ilosc"));
            sb.AppendLine($"\nPodsumowanie:");
            sb.AppendLine($"  Łącznie: {totalKg:N0} kg");
            sb.AppendLine($"  Pojemników: {totalPojemniki:N0}");
            sb.AppendLine($"  Palet: {totalPalety:N1}");

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
}