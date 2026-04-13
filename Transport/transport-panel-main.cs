// Plik: Transport/TransportMainFormImproved.cs
// Naprawiony główny panel zarządzania transportem

using Kalendarz1.Transport.Formularze;
using Kalendarz1.Transport.Pakowanie;
using Kalendarz1.Transport.Repozytorium;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1.Transport.Formularze
{
    /// <summary>
    /// DataGridView bez obramowań komórek. WinForms standardowy DataGridView
    /// ignoruje CellBorderStyle.None w wielu sytuacjach — jedynym pewnym sposobem
    /// jest nadpisanie AdjustColumnHeaderBorderStyle i AdjustedTopLeftHeaderBorderStyle.
    /// </summary>
    internal class BorderlessDataGridView : DataGridView
    {
        public BorderlessDataGridView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
        {
            // Rysuj wszystko OPRÓCZ obramowań
            e.AdvancedBorderStyle.Top = DataGridViewAdvancedCellBorderStyle.None;
            e.AdvancedBorderStyle.Bottom = DataGridViewAdvancedCellBorderStyle.None;
            e.AdvancedBorderStyle.Left = DataGridViewAdvancedCellBorderStyle.None;
            e.AdvancedBorderStyle.Right = DataGridViewAdvancedCellBorderStyle.None;
            base.OnCellPainting(e);
        }

        protected override void OnRowPrePaint(DataGridViewRowPrePaintEventArgs e)
        {
            // Usuń domyślne rysowanie obramowań wierszy
            e.PaintParts &= ~DataGridViewPaintParts.Border;
            base.OnRowPrePaint(e);
        }
    }

    public partial class TransportMainFormImproved : Form
    {
        private readonly TransportRepozytorium _repozytorium;
        private readonly string _currentUser;
        private DateTime _selectedDate;

        // Główne kontrolki
        private Panel panelHeader;
        private Panel panelContent;
        private Panel panelSummary;

        // Nawigacja
        private DateTimePicker dtpData;
        private Button btnPrevDay;
        private Button btnNextDay;
        private Button btnToday;
        private Label lblDayName;

        // Grid kursów
        private DataGridView dgvKursy;

        // Przyciski akcji
        private Button btnNowyKurs;
        private Button btnEdytuj;
        private Button btnUsun;
        private Button btnDebug;
        private Button btnMapa;
        private Button btnKierowcy;
        private Button btnPojazdy;
        private Button btnPrzydziel; // Szybkie przypisanie kierowcy/pojazdu

        // Filtry
        private Panel panelFilters;
        private ComboBox cboFiltrKierowca;
        private ComboBox cboFiltrPojazd;
        private ComboBox cboFiltrStatus;
        private Button btnWyczyscFiltry;
        private List<Kierowca> _wszyscyKierowcy;
        private List<Pojazd> _wszystkiePojazdy;

        // Menu kontekstowe
        private ContextMenuStrip contextMenuKurs;

        // Panel boczny - wolne zamówienia
        private Panel panelWolneZamowienia;
        private DataGridView dgvWolneZamowienia;
        private Label lblWolneZamowieniaInfo;
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        // Podsumowanie
        private Label lblSummaryKursy;
        private Label lblSummaryPojemniki;
        private Label lblSummaryPalety;
        private Label lblSummaryWypelnienie;

        // Cache zdjęć produktów (TowarId → Image)
        private readonly Dictionary<int, Image> _productImages = new();



        // Tryb prawego panelu: kurs-specific changes vs wolne zamówienia
        private Panel panelKursZmiany;        // Panel zmian per kurs (zamienia wolne zamówienia)
        private Label lblKursZmianyHeader;
        private FlowLayoutPanel flowKursZmianyCards;
        private Button btnAkceptujKursZmiany;
        private Panel _wolneGridContainer;    // Referencja do gridContainer w prawy panel
        private Panel _wolneHeader;           // Referencja do nagłówka "WOLNE ZAMÓWIENIA"
        private long _selectedKursIdForZmiany = -1;

        // Mapa kursId → lista zamIds (do wyszukiwania zmian per kurs)
        private Dictionary<long, List<int>> _kursZamIds = new();

        // ═══ Cached GDI resources (utworzone raz, zwolnione w FormClosed) ═══
        private Font _fontAlertBang;        // "!" w kółku alertu
        private Font _fontNameBold;         // Imię w Utworzył
        private Font _fontDateSmall;        // Data w Utworzył
        private Font _fontHandlText;        // Tekst w Handlowcy
        private Font _fontGroupBold;        // Nagłówek grupy w wolnych zam.
        private Font _fontGroupText;        // Tekst w wolnych zam.
        private SolidBrush _brushAlertOrange;
        private SolidBrush _brushWhite;
        private SolidBrush _brushGroupText;  // kolor tekstu grup
        private SolidBrush _brushDarkText;   // ciemny tekst danych
        private Pen _penGroupLine;           // linia pod grupą
        private StringFormat _sfCenter;     // Center+Center
        private StringFormat _sfLeftCenter; // Left+Center+Ellipsis

        // Pulsacja alertu
        private Timer _pulseTimer;
        private float _pulseAlpha = 1f;
        private bool _pulseGrowing = false;

        // Dane
        private List<Kurs> _kursy;
        private Dictionary<long, WynikPakowania> _wypelnienia;
        private Timer _autoRefreshTimer;
        private bool _isRefreshing;
        private int _lastPendingCount = -1; // Feature 11: track alert count for sound

        // Cache klientów dla szybszego ładowania
        private static Dictionary<int, string> _klienciCache = new Dictionary<int, string>();
        private static Dictionary<int, string> _klienciAdresCache = new Dictionary<int, string>();
        private static DateTime _klienciCacheTime = DateTime.MinValue;

        // Cache użytkowników (userId → fullName) i avatarów
        private Dictionary<string, string> _userNameCache = new Dictionary<string, string>();
        private Dictionary<string, Image> _avatarCache = new Dictionary<string, Image>();

        // Mapowanie handlowiec name → userId (do avatarów z sieci)
        private Dictionary<string, string> _handlowiecMapowanie;

        // Odwrotne mapowanie: fullName → userId (do avatarów po ModyfikowalPrzez)
        private Dictionary<string, string> _fullNameToUserIdCache = new(StringComparer.OrdinalIgnoreCase);

        public TransportMainFormImproved(TransportRepozytorium repozytorium, string uzytkownik = null)
        {
            _repozytorium = repozytorium ?? throw new ArgumentNullException(nameof(repozytorium));
            _currentUser = uzytkownik ?? Environment.UserName;
            _selectedDate = DateTime.Today;

            // Inicjalizuj puste kolekcje
            _kursy = new List<Kurs>();
            _wypelnienia = new Dictionary<long, WynikPakowania>();

            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            this.Load += async (s, e) =>
            {
                await LoadInitialDataAsync();
                // Initialize pending count baseline
                _lastPendingCount = TransportZmianyService.GetPendingCount();

                // Pulsacja alertu (co 50ms zmienia alpha 1.0→0.35→1.0)
                _pulseTimer = new Timer { Interval = 50 };
                _pulseTimer.Tick += (pt, pe) =>
                {
                    if (_pulseGrowing) { _pulseAlpha += 0.04f; if (_pulseAlpha >= 1f) { _pulseAlpha = 1f; _pulseGrowing = false; } }
                    else { _pulseAlpha -= 0.04f; if (_pulseAlpha <= 0.35f) { _pulseAlpha = 0.35f; _pulseGrowing = true; } }
                    // Invaliduj całą kolumnę Alert jednym wywołaniem (zamiast iteracji po wierszach)
                    if (dgvKursy != null && !dgvKursy.IsDisposed && dgvKursy.Columns.Count > 0 && dgvKursy.Columns["Alert"] != null)
                    {
                        try { dgvKursy.InvalidateColumn(dgvKursy.Columns["Alert"].Index); }
                        catch { }
                    }
                };
                _pulseTimer.Start();

                // Auto-odświeżanie co 30 sekund
                _autoRefreshTimer = new Timer { Interval = 30000 };
                _autoRefreshTimer.Tick += async (ts, te) =>
                {
                    if (_isRefreshing) return;
                    _isRefreshing = true;
                    try
                    {
                        // Zapamiętaj poprzedni count przed detekcją
                        var beforeCount = TransportZmianyService.GetPendingCount();
                        // Detekcja zmian w tle, odświeżenie kursów na UI
                        _ = Task.Run(async () => { try { await LoadZmianyAsync(); } catch { } });
                        await LoadKursyAsync();
                        // Feature 11: Alert dźwiękowy gdy pojawiły się nowe zmiany
                        var afterCount = TransportZmianyService.GetPendingCount();
                        if (afterCount > beforeCount && _lastPendingCount >= 0)
                        {
                            try { System.Media.SystemSounds.Exclamation.Play(); }
                            catch { }
                        }
                        _lastPendingCount = afterCount;
                    }
                    catch { }
                    finally { _isRefreshing = false; }
                };
                _autoRefreshTimer.Start();
            };

            this.FormClosed += (s, e) =>
            {
                _pulseTimer?.Stop(); _pulseTimer?.Dispose();
                _autoRefreshTimer?.Stop(); _autoRefreshTimer?.Dispose();
                // Zwolnij cache avatarów i zdjęć produktów
                foreach (var img in _avatarCache.Values) img?.Dispose();
                _avatarCache.Clear();
                foreach (var img in _productImages.Values) img?.Dispose();
                _productImages.Clear();
                // Cached GDI resources
                _fontAlertBang?.Dispose(); _fontNameBold?.Dispose();
                _fontDateSmall?.Dispose(); _fontHandlText?.Dispose();
                _fontGroupBold?.Dispose(); _fontGroupText?.Dispose();
                _brushAlertOrange?.Dispose(); _brushWhite?.Dispose();
                _brushGroupText?.Dispose(); _brushDarkText?.Dispose();
                _penGroupLine?.Dispose();
                _sfCenter?.Dispose(); _sfLeftCenter?.Dispose();
            };
        }

        private void InitializeComponent()
        {
            Text = "Transport - Panel Zarządzania";
            Size = new Size(1400, 800);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(240, 242, 247);
            WindowState = FormWindowState.Maximized;
            DoubleBuffered = true;
            KeyPreview = true;

            // Cached GDI resources — tworzone RAZ, zwolnione w FormClosed
            _fontAlertBang = new Font("Segoe UI", 12F, FontStyle.Bold);
            _fontNameBold = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _fontDateSmall = new Font("Segoe UI", 7.5F);
            _fontHandlText = new Font("Segoe UI", 8F);
            _fontGroupBold = new Font("Segoe UI", 8F, FontStyle.Bold);
            _fontGroupText = new Font("Segoe UI", 8F);
            _brushAlertOrange = new SolidBrush(Color.FromArgb(230, 126, 34));
            _brushWhite = new SolidBrush(Color.White);
            _brushGroupText = new SolidBrush(Color.FromArgb(100, 60, 140));
            _brushDarkText = new SolidBrush(Color.FromArgb(40, 50, 65));
            _penGroupLine = new Pen(Color.FromArgb(210, 200, 225));
            _sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            _sfLeftCenter = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };

            // ========== HEADER ==========
            CreateHeader();

            // ========== FILTERS ==========
            // CreateFilters(); // Usunięte - filtry niepotrzebne

            // ========== CONTENT ==========
            CreateContent();

            // ========== SIDE PANEL - WOLNE ZAMÓWIENIA ==========
            CreateWolneZamowieniaPanel();

            // ========== CONTEXT MENU ==========
            CreateContextMenu();

            // ========== SUMMARY ==========
            CreateSummary();

            // Layout główny z dwoma kolumnami w środku
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));  // Header
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Content + Side panel
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));  // Summary - karty

            // Panel środkowy z dwoma kolumnami
            var contentWrapper = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            contentWrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));  // Kursy
            contentWrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));  // Wolne zamówienia

            contentWrapper.Controls.Add(panelContent, 0, 0);
            contentWrapper.Controls.Add(panelWolneZamowienia, 1, 0);

            mainLayout.Controls.Add(panelHeader, 0, 0);
            mainLayout.Controls.Add(contentWrapper, 0, 1);
            mainLayout.Controls.Add(panelSummary, 0, 2);

            Controls.Add(mainLayout);
        }

        private void CreateHeader()
        {
            panelHeader = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(41, 44, 51),
                Padding = new Padding(20, 15, 20, 15)
            };

            // Panel nawigacji dat (lewa strona) - bez zmian
            var panelDate = new Panel
            {
                Location = new Point(20, 15),
                Size = new Size(500, 50),
                BackColor = Color.Transparent
            };

            btnPrevDay = CreateNavButton("◀", 0, -1);

            var datePanel = new Panel
            {
                Location = new Point(50, 0),
                Size = new Size(150, 50),
                BackColor = Color.FromArgb(52, 56, 64),
                BorderStyle = BorderStyle.None
            };

            dtpData = new DateTimePicker
            {
                Location = new Point(5, 12),
                Size = new Size(140, 26),
                Format = DateTimePickerFormat.Short,
                Value = _selectedDate,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                CalendarMonthBackground = Color.FromArgb(52, 56, 64),
                CalendarForeColor = Color.White
            };
            dtpData.ValueChanged += DtpData_ValueChanged;
            datePanel.Controls.Add(dtpData);

            btnNextDay = CreateNavButton("▶", 210, 1);

            btnToday = new Button
            {
                Text = "DZIŚ",
                Location = new Point(260, 0),
                Size = new Size(80, 50),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnToday.FlatAppearance.BorderSize = 0;
            btnToday.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 105, 217);
            btnToday.Click += (s, e) => { _selectedDate = DateTime.Today; dtpData.Value = _selectedDate; };

            lblDayName = new Label
            {
                Location = new Point(355, 0),
                Size = new Size(140, 50),
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 193, 7),
                TextAlign = ContentAlignment.MiddleLeft
            };

            panelDate.Controls.AddRange(new Control[] { btnPrevDay, datePanel, btnNextDay, btnToday, lblDayName });

            // Panel przycisków (prawa strona) - DODANY PRZYCISK RAPORT
            var panelButtons = new FlowLayoutPanel
            {
                Location = new Point(600, 15),
                Size = new Size(1300, 50), // Zwiększono szerokość dla nowego przycisku
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            // Grupa 1: Akcje na kursach (niebieski)
            var colorAkcje = Color.FromArgb(52, 119, 219);
            btnNowyKurs = CreateActionButton("+ NOWY KURS", colorAkcje, 130);
            btnNowyKurs.Click += BtnNowyKurs_Click;

            btnEdytuj = CreateActionButton("✏ EDYTUJ", ControlPaint.Light(colorAkcje, 0.15f), 100);
            btnEdytuj.Click += BtnEdytujKurs_Click;

            btnDebug = CreateActionButton("🔍 DEBUG", Color.FromArgb(192, 57, 43), 90);
            btnDebug.Click += BtnDebug_Click;

            btnUsun = CreateActionButton("✕ USUŃ", Color.FromArgb(220, 53, 69), 80);
            btnUsun.Click += BtnUsunKurs_Click;

            // Separator 1
            var sep1 = new Panel { Width = 2, Height = 30, BackColor = Color.FromArgb(70, 75, 85), Margin = new Padding(8, 10, 8, 2) };

            // Grupa 2: Widoki/Raporty (fioletowy)
            var colorWidoki = Color.FromArgb(126, 87, 194);
            var btnRaport = CreateActionButton("📊 RAPORT", colorWidoki, 110);
            btnRaport.Click += BtnRaport_Click;

            var btnStatystyki = CreateActionButton("📈 STATYSTYKI", ControlPaint.Light(colorWidoki, 0.12f), 120);
            btnStatystyki.Click += BtnStatystyki_Click;

            btnMapa = CreateActionButton("🗺 MAPA", ControlPaint.Light(colorWidoki, 0.25f), 85);
            btnMapa.Click += BtnMapa_Click;

            // Separator 2
            var sep2 = new Panel { Width = 2, Height = 30, BackColor = Color.FromArgb(70, 75, 85), Margin = new Padding(8, 10, 8, 2) };

            // Grupa 3: Zasoby (ciemny szary)
            var colorZasoby = Color.FromArgb(75, 85, 99);
            btnKierowcy = CreateActionButton("KIEROWCY", colorZasoby, 100);
            btnKierowcy.Click += SafeBtnKierowcy_Click;

            btnPojazdy = CreateActionButton("POJAZDY", colorZasoby, 100);
            btnPojazdy.Click += SafeBtnPojazdy_Click;

            btnPrzydziel = CreateActionButton("�� PRZYDZIEL", Color.FromArgb(217, 119, 6), 120);
            btnPrzydziel.Click += BtnPrzydziel_Click;
            btnPrzydziel.Enabled = false;

            // Separator 3
            var sep3 = new Panel { Width = 2, Height = 30, BackColor = Color.FromArgb(70, 75, 85), Margin = new Padding(8, 10, 8, 2) };

            // Webfleet sync
            var btnWebfleet = CreateActionButton("📡 WEBFLEET", Color.FromArgb(21, 101, 192), 120);
            btnWebfleet.Click += BtnWebfleetSync_Click;

            // Dodanie wszystkich przycisków z separatorami (RightToLeft)
            panelButtons.Controls.AddRange(new Control[] {
                btnUsun, btnDebug, sep1, btnMapa, btnStatystyki, btnRaport, sep2, btnKierowcy, btnPojazdy, btnPrzydziel, btnEdytuj, btnNowyKurs, sep3, btnWebfleet
            });

            panelHeader.Controls.Add(panelDate);
            panelHeader.Controls.Add(panelButtons);

            // Obsługa zmiany rozmiaru
            panelHeader.Resize += (s, e) =>
            {
                if (panelButtons != null)
                {
                    panelButtons.Location = new Point(panelHeader.Width - panelButtons.Width - 20, 15);
                }
            };
        }

        private async void BtnDebug_Click(object sender, EventArgs e)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== TRANSPORT DEBUG ===");
            sb.AppendLine($"Data: {_selectedDate:yyyy-MM-dd}");
            sb.AppendLine($"Kursy: {_kursy?.Count ?? 0}");
            sb.AppendLine($"ConnLibra: {_connLibra}");
            sb.AppendLine();

            Dictionary<int, (int Pojemniki, bool TrybE2, int KlientId, string TransportStatus, int Palety, decimal IloscKg, DateTime? DataPrzyjazdu)> debugLiveMap = null;

            // 1. Test PobierzLiveZamowieniaMapAsync
            try
            {
                debugLiveMap = await PobierzLiveZamowieniaMapAsync();
                var liveMap = debugLiveMap;
                sb.AppendLine($"[LiveZamowienia] Zaladowano: {liveMap.Count} zamowien");
                if (_lastLiveZamError != null)
                    sb.AppendLine($"  >>> BLAD WEWN: {_lastLiveZamError}");
                if (liveMap.Count > 0)
                {
                    foreach (var kvp in liveMap.Take(5))
                        sb.AppendLine($"  ZamId={kvp.Key}: Pal={kvp.Value.Palety}, Poj={kvp.Value.Pojemniki}, KG={kvp.Value.IloscKg}, KlientId={kvp.Value.KlientId}");
                    if (liveMap.Count > 5)
                        sb.AppendLine($"  ... i {liveMap.Count - 5} wiecej");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[LiveZamowienia] BLAD: {ex.Message}");
            }
            sb.AppendLine();

            // 2. Test ladunki per kurs (first 3 kursy)
            if (_kursy != null && _kursy.Count > 0)
            {
                var liveMap = debugLiveMap ?? await PobierzLiveZamowieniaMapAsync();
                foreach (var kurs in _kursy.Take(3))
                {
                    try
                    {
                        var ladunki = await _repozytorium.PobierzLadunkiAsync(kurs.KursID);
                        sb.AppendLine($"[Kurs {kurs.KursID}] Ladunkow: {ladunki.Count}");
                        int sumPal = 0, sumPoj = 0; decimal sumKg = 0;
                        foreach (var lad in ladunki)
                        {
                            sb.Append($"  Lad: KodKlienta='{lad.KodKlienta}', PojE2={lad.PojemnikiE2}");
                            if (lad.KodKlienta?.StartsWith("ZAM_") == true &&
                                int.TryParse(lad.KodKlienta.Substring(4), out int zamId))
                            {
                                if (liveMap.TryGetValue(zamId, out var live))
                                {
                                    sb.Append($" → LIVE: Pal={live.Palety}, Poj={live.Pojemniki}, KG={live.IloscKg}");
                                    sumPal += live.Palety; sumPoj += live.Pojemniki; sumKg += live.IloscKg;
                                }
                                else
                                    sb.Append($" → ZamId={zamId} NIE ZNALEZIONO w LiveMap!");
                            }
                            else
                                sb.Append(" → NIE jest ZAM_ ladunek");
                            sb.AppendLine();
                        }
                        sb.AppendLine($"  SUMA: Pal={sumPal}, Poj={sumPoj}, KG={sumKg}");
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"  [Kurs {kurs.KursID}] BLAD: {ex.Message}");
                    }
                }
            }
            sb.AppendLine();

            // 3. Test snapshot & zmiany tables
            try
            {
                var connStr = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                using var cmdCount = new SqlCommand("SELECT COUNT(*) FROM TransportOrderSnapshot", conn);
                var snapCount = (int)(await cmdCount.ExecuteScalarAsync() ?? 0);
                sb.AppendLine($"[Snapshots] Rekordow: {snapCount}");

                using var cmdSample = new SqlCommand("SELECT TOP 5 ZamowienieId, ISNULL(LiczbaPalet,0), ISNULL(LiczbaPojemnikow,0), ISNULL(IloscKg,0), DataZamowienia, KlientNazwa FROM TransportOrderSnapshot ORDER BY LastChecked DESC", conn);
                using var rdr = await cmdSample.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                    sb.AppendLine($"  Snap: ZamId={rdr.GetInt32(0)}, Pal={rdr.GetInt32(1)}, Poj={rdr.GetInt32(2)}, Kg={rdr.GetDecimal(3)}, Data={(!rdr.IsDBNull(4) ? rdr.GetDateTime(4).ToString("yyyy-MM-dd") : "NULL")}, Klient={(!rdr.IsDBNull(5) ? rdr.GetString(5) : "NULL")}");
                rdr.Close();

                // TransportZmiany - all statuses
                using var cmdAll = new SqlCommand("SELECT StatusZmiany, COUNT(*) FROM TransportZmiany GROUP BY StatusZmiany", conn);
                using var rdrAll = await cmdAll.ExecuteReaderAsync();
                sb.AppendLine();
                sb.AppendLine("[TransportZmiany] Statusy:");
                while (await rdrAll.ReadAsync())
                    sb.AppendLine($"  {rdrAll.GetString(0)}: {rdrAll.GetInt32(1)}");
                rdrAll.Close();

                // Pending details
                using var cmdPendingSample = new SqlCommand("SELECT TOP 10 ZamowienieId, TypZmiany, StareWartosc, NowaWartosc, DataZgloszenia, ZgloszonePrzez FROM TransportZmiany WHERE StatusZmiany = 'Oczekuje' ORDER BY DataZgloszenia DESC", conn);
                using var rdr2 = await cmdPendingSample.ExecuteReaderAsync();
                if (rdr2.HasRows) sb.AppendLine("\n[TransportZmiany] Oczekujace:");
                while (await rdr2.ReadAsync())
                    sb.AppendLine($"  ZamId={rdr2.GetInt32(0)}, {rdr2.GetString(1)}: '{(!rdr2.IsDBNull(2) ? rdr2.GetString(2) : "")}'→'{(!rdr2.IsDBNull(3) ? rdr2.GetString(3) : "")}' ({rdr2.GetDateTime(4):MM-dd HH:mm}, {(!rdr2.IsDBNull(5) ? rdr2.GetString(5) : "?")})");
                rdr2.Close();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[DB Test] BLAD: {ex.Message}");
            }
            sb.AppendLine();

            // 4. Grid state
            sb.AppendLine($"[Grid] Rows: {dgvKursy.Rows.Count}, Columns: {dgvKursy.Columns.Count}");
            if (dgvKursy.Rows.Count > 0)
            {
                for (int r = 0; r < Math.Min(3, dgvKursy.Rows.Count); r++)
                {
                    var row = dgvKursy.Rows[r];
                    var kursId = row.Cells["KursID"].Value;
                    var trasa = row.Cells["Trasa"].Value;
                    var pal = row.Cells["Pal"].Value;
                    var poj = row.Cells["Poj"].Value;
                    var kg = row.Cells["KG"].Value;
                    var alert = row.Cells["Alert"].Value;
                    var maZmiany = row.Cells["MaZmiany"].Value;
                    sb.AppendLine($"  Row{r}: KursID={kursId}, Trasa={trasa}, Pal={pal}, Poj={poj}, KG={kg}, Alert={alert}, MaZmiany={maZmiany}");
                }
            }
            sb.AppendLine();

            // 5. Kurs → ZamIds mapping
            sb.AppendLine($"[KursZamIds] {_kursZamIds.Count} kursow z zamowieniami");
            foreach (var kvp in _kursZamIds.Take(5))
                sb.AppendLine($"  Kurs {kvp.Key}: [{string.Join(", ", kvp.Value)}]");
            sb.AppendLine();

            // 6. Test: what does the DetectNewOrders date-filtered query actually return?
            try
            {
                await using var cnLibraTest = new SqlConnection(_connLibra);
                await cnLibraTest.OpenAsync();
                // Same date filter as DetectNewOrdersAsync
                await using var cmdDateTest = new SqlCommand(@"
                    SELECT COUNT(*) FROM dbo.ZamowieniaMieso zm
                    WHERE zm.DataZamowienia >= DATEADD(day, -1, CAST(GETDATE() AS date))
                      AND zm.DataZamowienia <= DATEADD(day, 14, CAST(GETDATE() AS date))
                      AND ISNULL(zm.Status, 'Nowe') NOT IN ('Anulowane')", cnLibraTest);
                var detectCount = (int)(await cmdDateTest.ExecuteScalarAsync() ?? 0);
                sb.AppendLine($"[DetectQuery] Zamowienia w oknie dat (-1d..+14d od GETDATE): {detectCount}");

                // Check: what date do our kurs orders have?
                if (_kursy != null && _kursy.Count > 0)
                {
                    var kurs = _kursy.First();
                    var ladunki = await _repozytorium.PobierzLadunkiAsync(kurs.KursID);
                    foreach (var lad in ladunki.Take(3))
                    {
                        if (lad.KodKlienta?.StartsWith("ZAM_") == true &&
                            int.TryParse(lad.KodKlienta.Substring(4), out int zamId))
                        {
                            await using var cmdDate = new SqlCommand(
                                "SELECT DataZamowienia, DataPrzyjazdu, LiczbaPalet, LiczbaPojemnikow, Status FROM dbo.ZamowieniaMieso WHERE Id = @Id", cnLibraTest);
                            cmdDate.Parameters.AddWithValue("@Id", zamId);
                            await using var rdrDate = await cmdDate.ExecuteReaderAsync();
                            if (await rdrDate.ReadAsync())
                                sb.AppendLine($"  ZAM_{zamId}: DataZam={(!rdrDate.IsDBNull(0) ? rdrDate.GetDateTime(0).ToString("yyyy-MM-dd") : "NULL")}, Pal={rdrDate.GetInt32(3)}, Poj={rdrDate.GetInt32(3)}, Status={(!rdrDate.IsDBNull(4) ? rdrDate.GetString(4) : "NULL")}");
                        }
                    }
                }
                // Check GETDATE on server
                await using var cmdNow = new SqlCommand("SELECT GETDATE()", cnLibraTest);
                var serverNow = (DateTime)(await cmdNow.ExecuteScalarAsync()!);
                sb.AppendLine($"  Server GETDATE(): {serverNow:yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[DetectQuery] BLAD: {ex.Message}");
            }
            sb.AppendLine();

            // 6. Run detect and show result
            try
            {
                var user = App.UserFullName ?? App.UserID ?? "system";
                var detected = await TransportZmianyService.DetectNewOrdersAsync(user);
                sb.AppendLine($"[DetectNewOrders] Wykryto zmian: {detected}");
                if (TransportZmianyService.LastDetectError != null)
                    sb.AppendLine($"  >>> WEWN BLAD: {TransportZmianyService.LastDetectError}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[DetectNewOrders] BLAD: {ex.Message}");
                sb.AppendLine($"  StackTrace: {ex.StackTrace?.Substring(0, Math.Min(500, ex.StackTrace?.Length ?? 0))}");
            }

            // 7. Check snapshots AFTER detect ran
            try
            {
                var connStr2 = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                using var conn2 = new SqlConnection(connStr2);
                await conn2.OpenAsync();
                using var cmdSnap2 = new SqlCommand("SELECT COUNT(*) FROM TransportOrderSnapshot", conn2);
                var snapAfter = (int)(await cmdSnap2.ExecuteScalarAsync() ?? 0);
                sb.AppendLine($"[Snapshots AFTER detect] Rekordow: {snapAfter}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[Snapshots AFTER] BLAD: {ex.Message}");
            }

            var debugText = sb.ToString();
            System.Diagnostics.Debug.WriteLine(debugText);

            // Show in a scrollable textbox dialog
            var dlg = new Form
            {
                Text = "Transport Debug",
                Size = new Size(800, 650),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            var txt = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9.5F),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(0, 255, 100),
                Text = debugText,
                WordWrap = false
            };
            dlg.Controls.Add(txt);
            dlg.Show(this);
        }

        // NOWA METODA obsługi przycisku RAPORT
        private void BtnRaport_Click(object sender, EventArgs e)
        {
            // Raport transportowy
            var connString = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
            try
            {
                var raportForm = new Kalendarz1.Transport.TransportRaportForm(connString);
                raportForm.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas otwierania raportu transportowego: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // METODA obsługi przycisku STATYSTYKI
        private void BtnStatystyki_Click(object sender, EventArgs e)
        {
            try
            {
                var statystykiForm = new TransportStatystykiForm();
                statystykiForm.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas otwierania statystyk transportu: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // BEZPIECZNE obsługa przycisków - z pełną obsługą błędów
        private void SafeBtnKierowcy_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Opening KierowcyForm...");

                // Sprawdź czy formularz można utworzyć
                using (var testForm = new Form())
                {
                    testForm.Dispose();
                }

                var frm = new KierowcyForm();
                frm.ShowDialog(this);

                System.Diagnostics.Debug.WriteLine("KierowcyForm closed successfully");
            }
            catch (System.IO.FileNotFoundException ex)
            {
                MessageBox.Show($"Nie można znaleźć wymaganych plików dla formularza kierowców.\nBłąd: {ex.Message}",
                    "Brak plików", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (System.TypeLoadException ex)
            {
                MessageBox.Show($"Błąd ładowania typu formularza kierowców.\nSprawdź czy wszystkie klasy są poprawnie zdefiniowane.\nBłąd: {ex.Message}",
                    "Błąd typu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (System.MissingMethodException ex)
            {
                MessageBox.Show($"Brak konstruktora dla formularza kierowców.\nBłąd: {ex.Message}",
                    "Błąd konstruktora", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SafeBtnKierowcy_Click: {ex}");
                MessageBox.Show($"Nie można otworzyć formularza kierowców.\n\nSzczegóły błędu:\n{ex.Message}\n\nTyp błędu: {ex.GetType().Name}",
                    "Błąd krytyczny", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SafeBtnPojazdy_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Opening PojazdyForm...");

                // Sprawdź czy formularz można utworzyć
                using (var testForm = new Form())
                {
                    testForm.Dispose();
                }

                var frm = new PojazdyForm();
                frm.ShowDialog(this);

                System.Diagnostics.Debug.WriteLine("PojazdyForm closed successfully");
            }
            catch (System.IO.FileNotFoundException ex)
            {
                MessageBox.Show($"Nie można znaleźć wymaganych plików dla formularza pojazdów.\nBłąd: {ex.Message}",
                    "Brak plików", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (System.TypeLoadException ex)
            {
                MessageBox.Show($"Błąd ładowania typu formularza pojazdów.\nSprawdź czy wszystkie klasy są poprawnie zdefiniowane.\nBłąd: {ex.Message}",
                    "Błąd typu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (System.MissingMethodException ex)
            {
                MessageBox.Show($"Brak konstruktora dla formularza pojazdów.\nBłąd: {ex.Message}",
                    "Błąd konstruktora", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SafeBtnPojazdy_Click: {ex}");
                MessageBox.Show($"Nie można otworzyć formularza pojazdów.\n\nSzczegóły błędu:\n{ex.Message}\n\nTyp błędu: {ex.GetType().Name}",
                    "Błąd krytyczny", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Button CreateNavButton(string text, int x, int dayChange)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, 0),
                Size = new Size(40, 50),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 56, 64),
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(72, 76, 84);

            if (dayChange != 0)
            {
                btn.Click += (s, e) => { _selectedDate = _selectedDate.AddDays(dayChange); dtpData.Value = _selectedDate; };
            }

            return btn;
        }

        private Button CreateActionButton(string text, Color color, int width)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(width, 42),
                FlatStyle = FlatStyle.Flat,
                BackColor = color,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(6, 4, 6, 4)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(color, 0.15f);

            return btn;
        }

        private void CreateFilters()
        {
            panelFilters = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(20, 0, 20, 0)
            };

            // Dolna linia separatora
            var bottomLine = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = Color.FromArgb(230, 232, 235)
            };

            var flowLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 8, 0, 8)
            };

            // Ikona filtra
            var lblIkona = new Label
            {
                Text = "⚙",
                Font = new Font("Segoe UI", 14F),
                ForeColor = Color.FromArgb(155, 89, 182),
                AutoSize = true,
                Margin = new Padding(0, 6, 8, 0)
            };

            // Etykiety filtrów
            var lblFiltrKierowca = new Label
            {
                Text = "Kierowca:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(75, 85, 99),
                AutoSize = true,
                Margin = new Padding(0, 12, 2, 0)
            };

            cboFiltrKierowca = new ComboBox
            {
                Width = 170,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 8, 12, 0),
                FlatStyle = FlatStyle.Flat
            };
            cboFiltrKierowca.SelectedIndexChanged += FiltrChanged;

            var lblFiltrPojazd = new Label
            {
                Text = "Pojazd:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(75, 85, 99),
                AutoSize = true,
                Margin = new Padding(0, 12, 2, 0)
            };

            cboFiltrPojazd = new ComboBox
            {
                Width = 140,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 8, 12, 0),
                FlatStyle = FlatStyle.Flat
            };
            cboFiltrPojazd.SelectedIndexChanged += FiltrChanged;

            var lblFiltrStatus = new Label
            {
                Text = "Status:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(75, 85, 99),
                AutoSize = true,
                Margin = new Padding(0, 12, 2, 0)
            };

            cboFiltrStatus = new ComboBox
            {
                Width = 120,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 8, 12, 0),
                FlatStyle = FlatStyle.Flat
            };
            cboFiltrStatus.Items.AddRange(new object[] {
                "Wszystkie statusy",
                "Planowany",
                "W realizacji",
                "Zakończony",
                "Anulowany"
            });
            cboFiltrStatus.SelectedIndex = 0;
            cboFiltrStatus.SelectedIndexChanged += FiltrChanged;

            // Przycisk wyczyść filtry - minimalistyczny
            btnWyczyscFiltry = new Button
            {
                Text = "Wyczyść",
                Size = new Size(70, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(240, 240, 245),
                ForeColor = Color.FromArgb(100, 100, 120),
                Font = new Font("Segoe UI", 8F),
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 9, 0, 0)
            };
            btnWyczyscFiltry.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 210);
            btnWyczyscFiltry.FlatAppearance.BorderSize = 1;
            btnWyczyscFiltry.FlatAppearance.MouseOverBackColor = Color.FromArgb(230, 230, 235);
            btnWyczyscFiltry.Click += BtnWyczyscFiltry_Click;

            flowLayout.Controls.AddRange(new Control[] {
                lblIkona, lblFiltrKierowca, cboFiltrKierowca, lblFiltrPojazd, cboFiltrPojazd, lblFiltrStatus, cboFiltrStatus, btnWyczyscFiltry
            });

            panelFilters.Controls.Add(flowLayout);
            panelFilters.Controls.Add(bottomLine);
        }

        private void CreateContextMenu()
        {
            contextMenuKurs = new ContextMenuStrip();
            contextMenuKurs.Font = new Font("Segoe UI", 10F);

            var menuPodglad = new ToolStripMenuItem("📦 Podgląd ładunków", null, MenuPodgladLadunkow_Click);
            var menuHistoria = new ToolStripMenuItem("📋 Historia zmian", null, MenuHistoriaZmian_Click);
            var menuSeparator = new ToolStripSeparator();
            var menuEdytuj = new ToolStripMenuItem("✏️ Edytuj kurs", null, (s, e) => BtnEdytujKurs_Click(s, e));
            var menuUsun = new ToolStripMenuItem("🗑️ Usuń kurs", null, (s, e) => BtnUsunKurs_Click(s, e));

            var menuSeparator2 = new ToolStripSeparator();
            var menuWebfleet = new ToolStripMenuItem("📡 Wyślij do Webfleet", null, MenuWebfleetWyslij_Click);
            menuWebfleet.Font = new Font("Segoe UI", 10F, FontStyle.Bold);

            contextMenuKurs.Items.AddRange(new ToolStripItem[] {
                menuPodglad, menuHistoria, menuSeparator, menuEdytuj, menuUsun, menuSeparator2, menuWebfleet
            });
        }

        private void CreateContent()
        {
            panelContent = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 12, 10, 12),
                BackColor = Color.FromArgb(245, 247, 250)
            };

            // Kontener dla grida z ramką
            var gridContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(220, 222, 228),
                Padding = new Padding(1)
            };

            dgvKursy = new BorderlessDataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                CellBorderStyle = DataGridViewCellBorderStyle.None
            };
            EnableDoubleBuffering(dgvKursy);

            dgvKursy.EnableHeadersVisualStyles = false;
            dgvKursy.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 253);
            dgvKursy.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(80, 90, 110);
            dgvKursy.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(232, 218, 239);
            dgvKursy.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(60, 40, 80);
            dgvKursy.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            dgvKursy.ColumnHeadersDefaultCellStyle.Padding = new Padding(3, 2, 3, 2);
            dgvKursy.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvKursy.ColumnHeadersHeight = 34;
            dgvKursy.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

            dgvKursy.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            dgvKursy.DefaultCellStyle.Padding = new Padding(4, 4, 4, 4);
            dgvKursy.DefaultCellStyle.SelectionBackColor = Color.FromArgb(232, 218, 239);
            dgvKursy.DefaultCellStyle.SelectionForeColor = Color.FromArgb(60, 40, 80);
            dgvKursy.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 252, 254);
            dgvKursy.RowTemplate.Height = 46;
            dgvKursy.GridColor = Color.White;

            dgvKursy.CellFormatting += DgvKursy_CellFormatting;
            dgvKursy.CellPainting += DgvKursy_CellPainting;
            dgvKursy.CellDoubleClick += (s, e) => BtnEdytujKurs_Click(s, e);
            dgvKursy.SelectionChanged += DgvKursy_SelectionChanged;
            dgvKursy.MouseClick += DgvKursy_MouseClick;
            dgvKursy.CellMouseEnter += DgvKursy_CellMouseEnter;
            dgvKursy.ShowCellToolTips = true;

            gridContainer.Controls.Add(dgvKursy);

            panelContent.Controls.Add(gridContainer);
        }

        private void DgvKursy_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var hitTest = dgvKursy.HitTest(e.X, e.Y);
                if (hitTest.RowIndex >= 0)
                {
                    dgvKursy.ClearSelection();
                    dgvKursy.Rows[hitTest.RowIndex].Selected = true;
                    var firstVisibleCol = dgvKursy.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => c.Visible);
                    if (firstVisibleCol != null)
                        dgvKursy.CurrentCell = dgvKursy.Rows[hitTest.RowIndex].Cells[firstVisibleCol.Index];
                    contextMenuKurs.Show(dgvKursy, e.Location);
                }
            }
        }

        private void CreateWolneZamowieniaPanel()
        {
            panelWolneZamowienia = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(0)
            };

            // Lewa krawędź jako separator wizualny
            var leftBorder = new Panel
            {
                Dock = DockStyle.Left,
                Width = 3,
                BackColor = Color.FromArgb(155, 89, 182)
            };

            // Główny kontener z marginesem
            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(250, 251, 253),
                Padding = new Padding(12, 8, 12, 8)
            };

            // Nagłówek - kompaktowy
            var panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.FromArgb(155, 89, 182),
                Padding = new Padding(12, 0, 12, 0)
            };

            // Tytuł i info w jednej linii
            var headerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var lblTytul = new Label
            {
                Text = "WOLNE ZAMÓWIENIA",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 12, 8, 0)
            };

            lblWolneZamowieniaInfo = new Label
            {
                Text = "0 zamówień",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(220, 220, 255),
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 14, 0, 0)
            };

            headerLayout.Controls.Add(lblTytul, 0, 0);
            headerLayout.Controls.Add(lblWolneZamowieniaInfo, 1, 0);
            panelHeader.Controls.Add(headerLayout);

            // Grid zamówień
            dgvWolneZamowienia = new BorderlessDataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                CellBorderStyle = DataGridViewCellBorderStyle.None
            };

            EnableDoubleBuffering(dgvWolneZamowienia);
            dgvWolneZamowienia.EnableHeadersVisualStyles = false;
            dgvWolneZamowienia.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 247, 250);
            dgvWolneZamowienia.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(100, 100, 120);
            dgvWolneZamowienia.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(232, 218, 239);
            dgvWolneZamowienia.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(60, 40, 80);
            dgvWolneZamowienia.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            dgvWolneZamowienia.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 6, 4, 6);
            dgvWolneZamowienia.ColumnHeadersHeight = 30;
            dgvWolneZamowienia.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dgvWolneZamowienia.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F);
            dgvWolneZamowienia.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
            dgvWolneZamowienia.DefaultCellStyle.SelectionBackColor = Color.FromArgb(232, 218, 239);
            dgvWolneZamowienia.DefaultCellStyle.SelectionForeColor = Color.FromArgb(60, 40, 80);
            dgvWolneZamowienia.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 252);
            dgvWolneZamowienia.RowTemplate.Height = 24;
            dgvWolneZamowienia.GridColor = Color.White;
            dgvWolneZamowienia.CellPainting += DgvWolneZamowienia_CellPainting;

            // Menu kontekstowe - zmiana na własny odbiór
            var contextMenuWolne = new ContextMenuStrip();
            contextMenuWolne.Font = new Font("Segoe UI", 9.5F);
            var menuWlasnyOdbior = new ToolStripMenuItem("🚗 Ustaw jako własny odbiór", null, async (s, ev) => await ZmienNaWlasnyOdbiorAsync());
            contextMenuWolne.Items.Add(menuWlasnyOdbior);
            dgvWolneZamowienia.ContextMenuStrip = contextMenuWolne;

            // Kontener dla grida z ramką
            var gridContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(230, 232, 235),
                Padding = new Padding(1)
            };
            gridContainer.Controls.Add(dgvWolneZamowienia);

            // ═══════ PANEL ZMIAN PER KURS (zamienia wolne zamówienia) ═══════
            panelKursZmiany = new Panel
            {
                Dock = DockStyle.Fill,
                Visible = false,
                BackColor = Color.FromArgb(245, 246, 250) // jasny szaro-niebieski
            };

            // Nagłówek kursu zmian
            var kursZmianyHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                Padding = new Padding(12, 0, 8, 0)
            };
            kursZmianyHeader.Paint += (s, ev) =>
            {
                using var gradBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Rectangle(0, 0, kursZmianyHeader.Width, kursZmianyHeader.Height),
                    Color.FromArgb(231, 76, 60), Color.FromArgb(243, 156, 18),
                    System.Drawing.Drawing2D.LinearGradientMode.Horizontal);
                ev.Graphics.FillRectangle(gradBrush, 0, 0, kursZmianyHeader.Width, kursZmianyHeader.Height);
            };

            var lblKursZmianyIcon = new Label
            {
                Text = "\u26A0",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(10, 10)
            };

            lblKursZmianyHeader = new Label
            {
                Text = "ZMIANY W KURSIE",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(36, 14)
            };

            btnAkceptujKursZmiany = new Button
            {
                Text = "\u2714 AKCEPTUJ",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Size = new Size(110, 32),
                Cursor = Cursors.Hand
            };
            btnAkceptujKursZmiany.FlatAppearance.BorderSize = 0;
            btnAkceptujKursZmiany.FlatAppearance.MouseOverBackColor = Color.FromArgb(39, 174, 96);
            btnAkceptujKursZmiany.Click += BtnAkceptujZmianyKursu_Click;

            var btnWrocDoWolnych = new Button
            {
                Text = "\u25C0 WRÓĆ",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 73, 94),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                Size = new Size(75, 32),
                Cursor = Cursors.Hand
            };
            btnWrocDoWolnych.FlatAppearance.BorderSize = 0;
            btnWrocDoWolnych.FlatAppearance.MouseOverBackColor = Color.FromArgb(44, 62, 80);
            btnWrocDoWolnych.Click += (s, ev) => ShowRightPanelMode(false, 0);

            kursZmianyHeader.Controls.Add(lblKursZmianyIcon);
            kursZmianyHeader.Controls.Add(lblKursZmianyHeader);
            kursZmianyHeader.Controls.Add(btnAkceptujKursZmiany);
            kursZmianyHeader.Controls.Add(btnWrocDoWolnych);
            kursZmianyHeader.Resize += (s, ev) =>
            {
                btnAkceptujKursZmiany.Location = new Point(
                    kursZmianyHeader.Width - btnAkceptujKursZmiany.Width - 6, 8);
                btnWrocDoWolnych.Location = new Point(
                    kursZmianyHeader.Width - btnAkceptujKursZmiany.Width - btnWrocDoWolnych.Width - 12, 8);
                kursZmianyHeader.Invalidate(); // repaint gradient on resize
            };

            flowKursZmianyCards = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.FromArgb(245, 246, 250),
                Padding = new Padding(8, 8, 8, 8)
            };
            flowKursZmianyCards.Resize += (s, ev) =>
            {
                var w = flowKursZmianyCards.Width - 30;
                if (w < 200) w = 280;
                foreach (Control c in flowKursZmianyCards.Controls)
                    if (c is Panel p) p.Width = w;
            };

            panelKursZmiany.Controls.Add(flowKursZmianyCards);
            panelKursZmiany.Controls.Add(kursZmianyHeader);

            // Zapisz referencje do przełączania trybów
            _wolneGridContainer = gridContainer;
            _wolneHeader = panelHeader;

            // Dock order: Header (top), KursZmiany (fill), Grid (fill) — add in reverse
            mainContainer.Controls.Add(panelKursZmiany);
            mainContainer.Controls.Add(gridContainer);
            mainContainer.Controls.Add(panelHeader);

            panelWolneZamowienia.Controls.Add(mainContainer);
            panelWolneZamowienia.Controls.Add(leftBorder);
        }

        /// <summary>
        /// Przełącza prawy panel między trybem "wolne zamówienia" a "zmiany w kursie"
        /// </summary>
        private void ShowRightPanelMode(bool showKursZmiany, long kursId)
        {
            var parent = _wolneGridContainer.Parent;
            if (parent == null) return;

            parent.SuspendLayout();
            try
            {
                if (showKursZmiany)
                {
                    _wolneGridContainer.Visible = false;
                    _wolneHeader.Visible = false;
                    panelKursZmiany.Visible = true;
                    panelKursZmiany.Dock = DockStyle.Fill;
                    panelKursZmiany.BringToFront();
                    _selectedKursIdForZmiany = kursId;
                }
                else
                {
                    panelKursZmiany.Visible = false;
                    _wolneHeader.Visible = true;
                    _wolneGridContainer.Visible = true;
                    _wolneGridContainer.BringToFront();
                    _selectedKursIdForZmiany = -1;
                }
            }
            finally
            {
                parent.ResumeLayout(true);
            }
        }

        /// <summary>
        /// Ładuje zmiany per kurs do prawego panelu (z avatarem i zdjęciami towarów)
        /// </summary>
        private async Task LoadKursZmianyAsync(long kursId, string trasa)
        {
            flowKursZmianyCards.Controls.Clear();

            var headerText = $"ZMIANY W KURSIE";
            if (!string.IsNullOrEmpty(trasa)) headerText += $"  —  {trasa}";
            lblKursZmianyHeader.Text = headerText;

            // Pobierz oczekujące zmiany z TransportZmiany dla zamówień w tym kursie
            var zamIds = _kursZamIds.ContainsKey(kursId) ? _kursZamIds[kursId] : new List<int>();
            if (zamIds.Count == 0) { ShowEmptyChanges(); return; }

            var zmianyPerZam = new Dictionary<int, List<(string Typ, string Stare, string Nowe, string Kto, DateTime Kiedy, string KlientNazwa)>>();
            try
            {
                var connStr = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                var zamIdsStr = string.Join(",", zamIds);
                using var cmd = new SqlCommand($@"
                    SELECT ZamowienieId, TypZmiany, StareWartosc, NowaWartosc, ZgloszonePrzez, DataZgloszenia, KlientNazwa
                    FROM TransportZmiany
                    WHERE StatusZmiany = 'Oczekuje' AND TypZmiany NOT IN ('NoweZamowienie', 'ZmianaStatusu')
                      AND ZamowienieId IN ({zamIdsStr})
                    ORDER BY DataZgloszenia DESC", conn);
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var zamId = Convert.ToInt32(rdr[0]);
                    if (!zmianyPerZam.ContainsKey(zamId))
                        zmianyPerZam[zamId] = new List<(string, string, string, string, DateTime, string)>();
                    zmianyPerZam[zamId].Add((
                        rdr[1]?.ToString() ?? "",
                        rdr.IsDBNull(2) ? "" : rdr[2].ToString(),
                        rdr.IsDBNull(3) ? "" : rdr[3].ToString(),
                        rdr.IsDBNull(4) ? "" : rdr[4].ToString(),
                        Convert.ToDateTime(rdr[5]),
                        rdr.IsDBNull(6) ? "" : rdr[6].ToString()
                    ));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadKursZmiany] Error: {ex.Message}");
            }

            if (zmianyPerZam.Count == 0) { ShowEmptyChanges(); return; }

            // Pobierz towary zamówień (do zdjęć) z LibraNet
            var zamTowary = new Dictionary<int, List<(int TowarId, string Kod, decimal Ilosc)>>();
            try
            {
                using var connLibra = new SqlConnection(_connLibra);
                await connLibra.OpenAsync();
                var zamIdsStr = string.Join(",", zmianyPerZam.Keys);
                using var cmdTow = new SqlCommand($@"
                    SELECT zmt.ZamowienieId, zmt.KodTowaru, tw.Kod, ISNULL(zmt.Ilosc, 0)
                    FROM dbo.ZamowieniaMiesoTowar zmt
                    JOIN [HANDEL].[HM].[TW] tw ON zmt.KodTowaru = tw.Id
                    WHERE zmt.ZamowienieId IN ({zamIdsStr}) AND ISNULL(zmt.Ilosc, 0) > 0
                    ORDER BY zmt.ZamowienieId, tw.Kod", connLibra);
                using var rdrTow = await cmdTow.ExecuteReaderAsync();
                while (await rdrTow.ReadAsync())
                {
                    var zId = Convert.ToInt32(rdrTow[0]);
                    if (!zamTowary.ContainsKey(zId))
                        zamTowary[zId] = new List<(int, string, decimal)>();
                    zamTowary[zId].Add((
                        Convert.ToInt32(rdrTow[1]),
                        rdrTow[2]?.ToString() ?? "",
                        Convert.ToDecimal(rdrTow[3])
                    ));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadKursZmiany-Towary] Error: {ex.Message}");
            }

            btnAkceptujKursZmiany.Visible = true;
            var cardWidth = flowKursZmianyCards.Width - 30;
            if (cardWidth < 200) cardWidth = 280;

            // ═══ CAPACITY BAR — wpływ zmian na wypełnienie kursu ═══
            {
                int paletyBefore = 0, paletyAfter = 0;
                foreach (var kvp in zmianyPerZam)
                {
                    foreach (var z in kvp.Value)
                    {
                        if (z.Typ == "ZmianaIlosci")
                        {
                            int.TryParse(z.Stare, out int oldP);
                            int.TryParse(z.Nowe, out int newP);
                            paletyBefore += oldP;
                            paletyAfter += newP;
                        }
                    }
                }
                // Dodaj palety z kursów bez zmian
                int currentTotal = 0;
                if (_kursZamIds.ContainsKey(_selectedKursIdForZmiany))
                {
                    var row = FindKursRow(_selectedKursIdForZmiany);
                    if (row != null && row.Cells["Pal"]?.Value != null)
                        currentTotal = Convert.ToInt32(row.Cells["Pal"].Value);
                }
                int beforeTotal = currentTotal - (paletyAfter - paletyBefore);
                int afterTotal = currentTotal;

                if (paletyBefore != paletyAfter || beforeTotal > 0)
                {
                    var barPanel = CreateCapacityBar(beforeTotal, afterTotal, 33, cardWidth);
                    flowKursZmianyCards.Controls.Add(barPanel);
                }
            }

            foreach (var kvp in zmianyPerZam)
            {
                var zamId = kvp.Key;
                var zmiany = kvp.Value;
                var klientNazwa = zmiany.FirstOrDefault().KlientNazwa;
                if (string.IsNullOrEmpty(klientNazwa)) klientNazwa = $"Zamówienie #{zamId}";
                var kto = zmiany.FirstOrDefault().Kto ?? "";
                var kiedy = zmiany.FirstOrDefault().Kiedy;
                var towary = zamTowary.ContainsKey(zamId) ? zamTowary[zamId] : new();

                var card = CreateSnapshotChangeCard(zamId, klientNazwa, kto, kiedy,
                    zmiany.Select(z => (TypToLabel(z.Typ), z.Stare, z.Nowe, z.Typ)).ToList(),
                    towary, cardWidth);
                flowKursZmianyCards.Controls.Add(card);
            }
        }

        private void ShowEmptyChanges()
        {
            var lblEmpty = new Label
            {
                Text = "Brak oczekujących zmian w tym kursie.",
                Font = new Font("Segoe UI", 10F, FontStyle.Italic),
                ForeColor = Color.FromArgb(127, 140, 141),
                AutoSize = true,
                Margin = new Padding(10, 20, 10, 10)
            };
            flowKursZmianyCards.Controls.Add(lblEmpty);
            btnAkceptujKursZmiany.Visible = false;
        }

        private DataGridViewRow FindKursRow(long kursId)
        {
            if (dgvKursy == null || dgvKursy.IsDisposed) return null;
            foreach (DataGridViewRow row in dgvKursy.Rows)
            {
                if (row.IsNewRow) continue;
                var val = row.Cells["KursID"]?.Value;
                if (val != null && Convert.ToInt64(val) == kursId)
                    return row;
            }
            return null;
        }

        private Panel CreateCapacityBar(int before, int after, int maxCapacity, int width)
        {
            var panel = new Panel
            {
                Width = width,
                Height = 56,
                Margin = new Padding(0, 0, 0, 6),
                BackColor = Color.White
            };
            panel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                int barX = 8, barY = 22, barW = width - 16, barH = 14;

                // Header
                using (var f = new Font("Segoe UI", 8.5F, FontStyle.Bold))
                    g.DrawString($"WYPEŁNIENIE KURSU", f, Brushes.Gray, barX, 4);

                // Background track
                using (var bgBrush = new SolidBrush(Color.FromArgb(235, 235, 235)))
                {
                    var bgRect = new Rectangle(barX, barY, barW, barH);
                    using (var path = CreateRoundedRectPath(bgRect, 7))
                        g.FillPath(bgBrush, path);
                }

                // "Before" bar
                if (before > 0 && maxCapacity > 0)
                {
                    float pctBefore = Math.Min((float)before / maxCapacity, 1f);
                    int wBefore = Math.Max((int)(barW * pctBefore), 4);
                    var cBefore = Color.FromArgb(80, 149, 165, 166); // szary, przezroczysty
                    using (var br = new SolidBrush(cBefore))
                    {
                        var r = new Rectangle(barX, barY, wBefore, barH);
                        using (var path = CreateRoundedRectPath(r, 7))
                            g.FillPath(br, path);
                    }
                }

                // "After" bar (overlay)
                if (after > 0 && maxCapacity > 0)
                {
                    float pctAfter = Math.Min((float)after / maxCapacity, 1f);
                    int wAfter = Math.Max((int)(barW * pctAfter), 4);
                    Color cAfter;
                    if (pctAfter < 0.6f) cAfter = Color.FromArgb(39, 174, 96);       // zielony
                    else if (pctAfter < 0.85f) cAfter = Color.FromArgb(243, 156, 18); // żółty
                    else cAfter = Color.FromArgb(192, 57, 43);                         // czerwony

                    using (var br = new SolidBrush(cAfter))
                    {
                        var r = new Rectangle(barX, barY, wAfter, barH);
                        using (var path = CreateRoundedRectPath(r, 7))
                            g.FillPath(br, path);
                    }
                }

                // Labels below bar
                using (var fSmall = new Font("Segoe UI", 7.5F))
                {
                    string beforeTxt = $"Przed: {before}/{maxCapacity}";
                    string afterTxt = $"Po: {after}/{maxCapacity}";
                    g.DrawString(beforeTxt, fSmall, Brushes.Gray, barX, barY + barH + 2);
                    var afterSize = g.MeasureString(afterTxt, fSmall);
                    Color afterColor = after > maxCapacity ? Color.FromArgb(192, 57, 43) : Color.FromArgb(39, 174, 96);
                    using (var afterBr = new SolidBrush(afterColor))
                        g.DrawString(afterTxt, fSmall, afterBr, barX + barW - afterSize.Width, barY + barH + 2);
                }
            };
            return panel;
        }

        private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // Kolory ikon i etykiet zmian — każdy typ ma inny, wyrazisty kolor
        private static readonly Dictionary<string, (string Label, string Icon, Color Color)> _zmianaMeta = new()
        {
            ["ZmianaIlosci"]      = ("PALETY",         "\U0001F7E0", Color.FromArgb(230, 126, 34)),   // pomarańczowy
            ["ZmianaPojemnikow"]  = ("POJEMNIKI",      "\U0001F535", Color.FromArgb(41, 128, 185)),   // niebieski
            ["ZmianaKg"]          = ("ILOŚĆ KG",       "\U0001F534", Color.FromArgb(192, 57, 43)),    // czerwony
            ["ZmianaAwizacji"]    = ("DATA AWIZACJI",   "\U0001F7E2", Color.FromArgb(39, 174, 96)),   // zielony
            ["ZmianaStatusu"]     = ("STATUS",          "\U0001F7E3", Color.FromArgb(142, 68, 173)),  // fioletowy
            ["ZmianaTerminu"]     = ("DATA ZAMÓWIENIA", "\U0001F7E1", Color.FromArgb(241, 196, 15)),  // żółty
            ["ZmianaUwag"]        = ("UWAGI",           "\U0001F7E4", Color.FromArgb(121, 85, 72)),   // brązowy
            ["ZmianaOdbiorcy"]    = ("ODBIORCA",        "\u26AA",     Color.FromArgb(0, 150, 136)),   // teal
        };

        private static (string Label, string Icon, Color Color) GetZmianaMeta(string typ)
        {
            if (_zmianaMeta.TryGetValue(typ, out var meta)) return meta;
            return (typ, "\u2B24", Color.FromArgb(100, 100, 100));
        }

        private static string TypToLabel(string typ) => GetZmianaMeta(typ).Label;

        private Panel CreateSnapshotChangeCard(int zamId, string klientNazwa, string zgloszonePrzez,
            DateTime dataZgloszenia, List<(string Field, string OldVal, string NewVal, string RawTyp)> changes,
            List<(int TowarId, string Kod, decimal Ilosc)> towary, int width)
        {
            var card = new Panel
            {
                Width = width,
                BackColor = Color.White,
                Padding = new Padding(0),
                Margin = new Padding(5, 6, 5, 6)
            };

            // Gradient-like top accent bar + subtle shadow via Paint
            card.Paint += (s, ev) =>
            {
                var g = ev.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Górny pasek gradientowy (pomarańczowy → żółty)
                using (var gradBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Rectangle(0, 0, card.Width, 5),
                    Color.FromArgb(243, 156, 18), Color.FromArgb(241, 196, 15),
                    System.Drawing.Drawing2D.LinearGradientMode.Horizontal))
                    g.FillRectangle(gradBrush, 0, 0, card.Width, 5);

                // Cień dolny
                using var shadowPen = new Pen(Color.FromArgb(30, 0, 0, 0));
                g.DrawLine(shadowPen, 2, card.Height - 1, card.Width - 2, card.Height - 1);

                // Obramowanie (zaokrąglone wrażenie)
                using var borderPen = new Pen(Color.FromArgb(220, 215, 200));
                g.DrawRectangle(borderPen, 0, 0, card.Width - 1, card.Height - 1);
            };

            int y = 12; // pod górnym paskiem
            int leftPad = 14;
            int rightPad = 10;

            // ════════════════════════════════════════════════════════
            // SEKCJA 1: Avatar + kto zmienił + czas temu
            // ════════════════════════════════════════════════════════
            int avatarSize = 38;
            var avatarBox = new PictureBox
            {
                Size = new Size(avatarSize, avatarSize),
                Location = new Point(leftPad, y),
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Transparent
            };
            try
            {
                var avatarImg = GetAvatarByFullName(zgloszonePrzez, avatarSize);
                if (avatarImg != null) avatarBox.Image = avatarImg;
            }
            catch { }
            card.Controls.Add(avatarBox);

            var displayName = string.IsNullOrEmpty(zgloszonePrzez) ? "Nieznany" : zgloszonePrzez;
            var lblKto = new Label
            {
                Text = displayName,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                AutoSize = true,
                MaximumSize = new Size(width - avatarSize - leftPad - rightPad - 80, 0),
                Location = new Point(leftPad + avatarSize + 10, y + 2)
            };
            card.Controls.Add(lblKto);

            var timeAgo = FormatTimeAgo(dataZgloszenia);
            var lblCzas = new Label
            {
                Text = timeAgo,
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(149, 165, 166),
                AutoSize = true,
                Location = new Point(leftPad + avatarSize + 10, y + 20)
            };
            card.Controls.Add(lblCzas);

            // Badge "zmieniono" po prawej
            var lblBadge = new Label
            {
                Text = "ZMIENIONO",
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(231, 76, 60),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                Size = new Size(70, 18),
                Padding = new Padding(0)
            };
            lblBadge.Location = new Point(width - 70 - rightPad, y + 4);
            card.Controls.Add(lblBadge);

            y += avatarSize + 10;

            // ════════════════════════════════════════════════════════
            // SEKCJA 2: Klient (kolorowa etykieta)
            // ════════════════════════════════════════════════════════
            var lblKlient = new Label
            {
                Text = $"\U0001F4E6  {klientNazwa}",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 118, 210), // niebieski
                AutoSize = true,
                MaximumSize = new Size(width - leftPad - rightPad - 80, 0),
                Location = new Point(leftPad, y)
            };
            card.Controls.Add(lblKlient);

            var lblZamId = new Label
            {
                Text = $"# {zamId}",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(120, 120, 120),
                BackColor = Color.FromArgb(240, 240, 245),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                Size = new Size(60, 18)
            };
            lblZamId.Location = new Point(width - 60 - rightPad, y + 1);
            card.Controls.Add(lblZamId);

            y += lblKlient.PreferredHeight + 6;

            // ════════════════════════════════════════════════════════
            // SEKCJA 3: Separator
            // ════════════════════════════════════════════════════════
            var sep = new Panel
            {
                Location = new Point(leftPad, y),
                Width = width - leftPad - rightPad,
                Height = 1,
                BackColor = Color.FromArgb(220, 220, 225)
            };
            card.Controls.Add(sep);
            y += 8;

            // ════════════════════════════════════════════════════════
            // SEKCJA 4: Zmiany — każda z kolorową ikoną i wyrazistymi wartościami
            // ════════════════════════════════════════════════════════
            foreach (var ch in changes)
            {
                var meta = GetZmianaMeta(ch.RawTyp);

                // Kolorowa kropka
                var dotPanel = new Panel
                {
                    Size = new Size(10, 10),
                    Location = new Point(leftPad + 2, y + 4),
                    BackColor = meta.Color
                };
                dotPanel.Paint += (s2, e2) =>
                {
                    e2.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using var b = new SolidBrush(((Panel)s2).BackColor);
                    e2.Graphics.Clear(Color.White);
                    e2.Graphics.FillEllipse(b, 0, 0, 9, 9);
                };
                card.Controls.Add(dotPanel);

                // Nazwa pola — w kolorze typu
                var lblField = new Label
                {
                    Text = ch.Field,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    ForeColor = meta.Color,
                    AutoSize = true,
                    Location = new Point(leftPad + 16, y)
                };
                card.Controls.Add(lblField);
                y += 18;

                // Stara wartość (przekreślona szarym) → Nowa wartość (gruby, zielony)
                var lblOld = new Label
                {
                    Text = ch.OldVal,
                    Font = new Font("Segoe UI", 10F, FontStyle.Strikeout),
                    ForeColor = Color.FromArgb(180, 180, 180),
                    AutoSize = true,
                    Location = new Point(leftPad + 18, y)
                };
                card.Controls.Add(lblOld);

                var lblArrow = new Label
                {
                    Text = "\u27A1",
                    Font = new Font("Segoe UI", 10F),
                    ForeColor = Color.FromArgb(243, 156, 18),
                    AutoSize = true,
                    Location = new Point(leftPad + 18 + lblOld.PreferredWidth + 6, y)
                };
                card.Controls.Add(lblArrow);

                var lblNew = new Label
                {
                    Text = ch.NewVal,
                    Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(39, 174, 96), // zielony — nowa wartość
                    AutoSize = true,
                    Location = new Point(lblArrow.Location.X + lblArrow.PreferredWidth + 6, y)
                };
                card.Controls.Add(lblNew);
                y += 24;
            }
            y += 2;

            // ════════════════════════════════════════════════════════
            // SEKCJA 5: Zdjęcia towarów
            // ════════════════════════════════════════════════════════
            if (towary.Count > 0)
            {
                var sep2 = new Panel
                {
                    Location = new Point(leftPad, y),
                    Width = width - leftPad - rightPad,
                    Height = 1,
                    BackColor = Color.FromArgb(235, 235, 240)
                };
                card.Controls.Add(sep2);
                y += 6;

                var lblTowHeader = new Label
                {
                    Text = $"PRODUKTY  ({towary.Count})",
                    Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(0, 150, 136), // teal
                    AutoSize = true,
                    Location = new Point(leftPad, y)
                };
                card.Controls.Add(lblTowHeader);
                y += 16;

                int imgSize = 42;
                int gap = 4;
                int imgX = leftPad;
                var cardTooltip = new ToolTip { InitialDelay = 200 };

                foreach (var tow in towary.Take(8))
                {
                    if (_productImages.TryGetValue(tow.TowarId, out var img) && img != null)
                    {
                        var pb = new PictureBox
                        {
                            Size = new Size(imgSize, imgSize),
                            Location = new Point(imgX, y),
                            SizeMode = PictureBoxSizeMode.Zoom,
                            Image = img,
                            BackColor = Color.FromArgb(250, 250, 252),
                            BorderStyle = BorderStyle.FixedSingle
                        };
                        cardTooltip.SetToolTip(pb, $"{tow.Kod}\n{tow.Ilosc:N0} kg");
                        card.Controls.Add(pb);
                    }
                    else
                    {
                        // Kolorowy placeholder z inicjałami towaru
                        var placeholderColor = Color.FromArgb(
                            100 + Math.Abs(tow.Kod.GetHashCode()) % 80,
                            100 + Math.Abs(tow.Kod.GetHashCode() >> 8) % 80,
                            140 + Math.Abs(tow.Kod.GetHashCode() >> 16) % 80);

                        var lblPlaceholder = new Label
                        {
                            Text = SkrocKodTowaru(tow.Kod),
                            Font = new Font("Segoe UI", 6F, FontStyle.Bold),
                            ForeColor = Color.White,
                            BackColor = placeholderColor,
                            TextAlign = ContentAlignment.MiddleCenter,
                            Size = new Size(imgSize, imgSize),
                            Location = new Point(imgX, y)
                        };
                        cardTooltip.SetToolTip(lblPlaceholder, $"{tow.Kod}\n{tow.Ilosc:N0} kg");
                        card.Controls.Add(lblPlaceholder);
                    }

                    imgX += imgSize + gap;
                    if (imgX + imgSize > width - rightPad)
                    {
                        imgX = leftPad;
                        y += imgSize + gap;
                    }
                }
                y += imgSize + 6;

                if (towary.Count > 8)
                {
                    var lblMore = new Label
                    {
                        Text = $"+{towary.Count - 8} więcej produktów",
                        Font = new Font("Segoe UI", 7.5F, FontStyle.Italic),
                        ForeColor = Color.FromArgb(0, 150, 136),
                        AutoSize = true,
                        Location = new Point(leftPad, y)
                    };
                    card.Controls.Add(lblMore);
                    y += 16;
                }
            }

            y += 6;
            card.Height = y;
            return card;
        }

        private static string SkrocKodTowaru(string kod)
        {
            if (string.IsNullOrEmpty(kod)) return "?";
            var parts = kod.Split(' ');
            return parts.Length >= 2 ? $"{parts[0]}\n{parts[1]}" : kod;
        }

        private static string FormatTimeAgo(DateTime dt)
        {
            var diff = DateTime.Now - dt;
            if (diff.TotalMinutes < 1) return "przed chwilą";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min temu";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} godz. temu";
            return $"{(int)diff.TotalDays} dni temu";
        }

        private async Task LoadProductImagesAsync()
        {
            try
            {
                using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                const string checkSql = @"SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TowarZdjecia') THEN 1 ELSE 0 END";
                using (var cmdCheck = new SqlCommand(checkSql, cn))
                {
                    var exists = (int)(await cmdCheck.ExecuteScalarAsync())!;
                    if (exists == 0) return;
                }

                const string sql = @"SELECT TowarId, Zdjecie FROM dbo.TowarZdjecia WHERE Aktywne = 1";
                using var cmd = new SqlCommand(sql, cn);
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    int towarId = Convert.ToInt32(rdr[0]);
                    if (!rdr.IsDBNull(1))
                    {
                        byte[] data = (byte[])rdr[1];
                        try
                        {
                            var ms = new System.IO.MemoryStream(data);
                            var bmp = new Bitmap(ms);
                            var thumb = new Bitmap(bmp, 100, 100);
                            bmp.Dispose();
                            ms.Dispose();
                            _productImages[towarId] = thumb;
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadProductImages] Error: {ex.Message}");
            }
        }

        private void DgvWolneZamowienia_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvWolneZamowienia.Rows[e.RowIndex];
            var isGroupRow = row.Cells["IsGroupRow"]?.Value != null && Convert.ToBoolean(row.Cells["IsGroupRow"].Value);
            var colName = dgvWolneZamowienia.Columns[e.ColumnIndex].Name;

            // Renderowanie avatara handlowca w zwykłych wierszach
            if (!isGroupRow && colName == "Handl.")
            {
                var cellValue = e.Value?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(cellValue)) return;

                e.Handled = true;
                var selected = row.Selected;

                using (var bgBrush = new SolidBrush(selected ? e.CellStyle.SelectionBackColor : e.CellStyle.BackColor))
                    e.Graphics.FillRectangle(bgBrush, e.CellBounds);

                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                var avatarSize = 26;
                var avatarX = e.CellBounds.Left + 4;
                var avatarY = e.CellBounds.Top + (e.CellBounds.Height - avatarSize) / 2;

                var avatarImg = GetHandlowiecAvatar(cellValue, avatarSize);
                if (avatarImg != null)
                {
                    using (var pen = new Pen(selected ? e.CellStyle.SelectionBackColor : Color.White, 2))
                        e.Graphics.DrawEllipse(pen, avatarX - 1, avatarY - 1, avatarSize + 1, avatarSize + 1);
                    e.Graphics.DrawImage(avatarImg, avatarX, avatarY, avatarSize, avatarSize);
                }

                var textX = avatarX + avatarSize + 5;
                var textBrush = selected ? _brushWhite : _brushDarkText;
                var displayText = SkrocNazwe(cellValue);
                var textRect = new RectangleF(textX, e.CellBounds.Top, e.CellBounds.Right - textX - 4, e.CellBounds.Height);
                e.Graphics.DrawString(displayText, _fontHandlText, textBrush, textRect, _sfLeftCenter);
                return;
            }

            // Wiersze grupujące - tylko one
            if (!isGroupRow) return;

            e.Handled = true;
            var bgColorGrp = row.Selected ? e.CellStyle.SelectionBackColor : Color.FromArgb(235, 230, 245);
            using (var brush = new SolidBrush(bgColorGrp))
                e.Graphics.FillRectangle(brush, e.CellBounds);

            // Rysuj tekst grupy w kolumnie Odbiór — scalony przez Odbiór+Godz.+Palety+Poj.
            if (colName == "Odbiór")
            {
                var headerText = row.Cells["Odbiór"]?.Value?.ToString() ?? "";
                var mergedWidth = (dgvWolneZamowienia.Columns["Odbiór"]?.Width ?? 0)
                    + (dgvWolneZamowienia.Columns["Godz."]?.Width ?? 0)
                    + (dgvWolneZamowienia.Columns["Palety"]?.Width ?? 0)
                    + (dgvWolneZamowienia.Columns["Poj."]?.Width ?? 0);

                e.Graphics.DrawString(headerText, _fontGroupBold, _brushGroupText,
                    new RectangleF(e.CellBounds.Left + 4, e.CellBounds.Top, mergedWidth - 6, e.CellBounds.Height), _sfLeftCenter);
            }
        }

        private async Task LoadZmianyAsync()
        {
            try
            {
                var user = App.UserFullName ?? App.UserID ?? "system";
                var detectedCount = await TransportZmianyService.DetectNewOrdersAsync(user);
                System.Diagnostics.Debug.WriteLine($"[Transport] DetectNewOrdersAsync detected {detectedCount} new changes");
            }
            catch { }
        }

        private void CreateSummary()
        {
            panelSummary = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(38, 42, 50),
                Padding = new Padding(15, 6, 15, 6)
            };

            // Górna linia dekoracyjna
            var topLine = new Panel
            {
                Dock = DockStyle.Top,
                Height = 2,
                BackColor = Color.FromArgb(60, 65, 75)
            };

            // Główny layout z kartami - kompaktowy
            var mainLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 2, 0, 0)
            };

            var cardKursy = CreateSummaryCard("KURSY", Color.FromArgb(52, 152, 219), out lblSummaryKursy);
            var cardPojemniki = CreateSummaryCard("POJEMNIKI", Color.FromArgb(241, 196, 15), out lblSummaryPojemniki);
            var cardPalety = CreateSummaryCard("PALETY", Color.FromArgb(155, 89, 182), out lblSummaryPalety);
            var cardWypelnienie = CreateSummaryCard("WYPEŁNIENIE", Color.FromArgb(46, 204, 113), out lblSummaryWypelnienie);
            lblSummaryWypelnienie.Text = "0%";

            mainLayout.Controls.AddRange(new Control[] { cardKursy, cardPojemniki, cardPalety, cardWypelnienie });

            panelSummary.Controls.Add(mainLayout);
            panelSummary.Controls.Add(topLine);
        }

        private Panel CreateSummaryCard(string title, Color accentColor, out Label valueLabel)
        {
            var card = new Panel
            {
                Size = new Size(180, 42),
                BackColor = Color.FromArgb(48, 52, 60),
                Margin = new Padding(0, 0, 12, 0)
            };

            // Lewa linia akcentu
            var accent = new Panel
            {
                Dock = DockStyle.Left,
                Width = 3,
                BackColor = accentColor
            };

            // Tytuł - po lewej
            var lblTitle = new Label
            {
                Text = title,
                Location = new Point(12, 13),
                AutoSize = true,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(140, 150, 165)
            };

            // Wartość - po prawej
            valueLabel = new Label
            {
                Text = "0",
                Location = new Point(100, 6),
                Size = new Size(72, 30),
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = accentColor
            };

            card.Controls.Add(lblTitle);
            card.Controls.Add(valueLabel);
            card.Controls.Add(accent);

            return card;
        }

        private Panel CreateSummaryTile(string title, string value, Color color)
        {
            var tile = new Panel
            {
                Size = new Size(220, 70),
                BackColor = Color.FromArgb(52, 56, 64),
                Margin = new Padding(10, 5, 10, 5)
            };

            var lblTitle = new Label
            {
                Text = title,
                Location = new Point(15, 10),
                Size = new Size(190, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(173, 181, 189),
                Name = "lblTitle"
            };

            var lblValue = new Label
            {
                Text = value,
                Location = new Point(15, 32),
                Size = new Size(190, 30),
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = color,
                Name = "lblValue" // Dodajemy nazwę dla łatwiejszego znalezienia
            };

            var sideBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(4, 70),
                BackColor = color,
                Name = "sideBar"
            };

            tile.Controls.Add(lblTitle);    // Indeks [0]
            tile.Controls.Add(lblValue);    // Indeks [1] 
            tile.Controls.Add(sideBar);     // Indeks [2]

            return tile;
        }

        private async Task LoadInitialDataAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== LoadInitialDataAsync START ===");
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // Ustaw nazwę dnia tygodnia
                lblDayName.Text = _selectedDate.ToString("dddd", new System.Globalization.CultureInfo("pl-PL"));

                // FAZA 1: Mapowanie handlowców (potrzebne przed kursami) + dane krytyczne RÓWNOLEGLE
                // Kursy i wolne zamówienia ruszają natychmiast — nie czekają na zmiany ani zdjęcia
                var mappingTask = EnsureHandlowiecMappingLoadedAsync();
                var kursyTask = LoadKursyAsync();
                var wolneTask = LoadWolneZamowieniaAsync();

                // Mapowanie jest lekkie, czekamy na nie + gridy
                await Task.WhenAll(mappingTask, kursyTask, wolneTask);

                System.Diagnostics.Debug.WriteLine($"=== LoadInitialDataAsync GRIDS READY in {sw.ElapsedMilliseconds}ms ===");

                // FAZA 2: Niekrytyczne — zdjęcia produktów i detekcja zmian w tle
                // Nie blokują UI — uruchamiamy fire-and-forget
                _ = Task.Run(async () =>
                {
                    try { await LoadProductImagesAsync(); }
                    catch { }
                });
                _ = Task.Run(async () =>
                {
                    try { await LoadZmianyAsync(); }
                    catch { }
                });

                System.Diagnostics.Debug.WriteLine($"=== LoadInitialDataAsync END total {sw.ElapsedMilliseconds}ms ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LoadInitialDataAsync: {ex.Message}");
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void DtpData_ValueChanged(object sender, EventArgs e)
        {
            _selectedDate = dtpData.Value.Date;
            lblDayName.Text = _selectedDate.ToString("dddd", new System.Globalization.CultureInfo("pl-PL"));
            System.Diagnostics.Debug.WriteLine($"Date changed to: {_selectedDate:yyyy-MM-dd}");

            // Kursy + wolne zamówienia równolegle; DetectNewOrders w tle
            _ = Task.Run(async () => { try { await LoadZmianyAsync(); } catch { } });
            await Task.WhenAll(LoadKursyAsync(), LoadWolneZamowieniaAsync());

            System.Diagnostics.Debug.WriteLine("Force calling UpdateSummary after date change");
            UpdateSummary();
        }

        private void DgvKursy_SelectionChanged(object sender, EventArgs e)
        {
            bool hasSelection = dgvKursy.CurrentRow != null;
            btnEdytuj.Enabled = hasSelection;
            btnUsun.Enabled = hasSelection;
            btnDebug.Enabled = true;
            btnMapa.Enabled = hasSelection;

            // Aktywuj przycisk PRZYDZIEL tylko dla kursów wymagających przypisania
            if (hasSelection && dgvKursy.CurrentRow.Cells["WymagaPrzydzialu"]?.Value != null)
            {
                btnPrzydziel.Enabled = Convert.ToBoolean(dgvKursy.CurrentRow.Cells["WymagaPrzydzialu"].Value);
            }
            else
            {
                btnPrzydziel.Enabled = false;
            }

            // Pokaż zmiany w kursie w PRAWYM PANELU (zamienia wolne zamówienia)
            bool maZmiany = false;
            if (hasSelection && dgvKursy.CurrentRow.Cells["MaZmiany"]?.Value != null &&
                dgvKursy.CurrentRow.Cells["MaZmiany"].Value != DBNull.Value)
            {
                maZmiany = Convert.ToBoolean(dgvKursy.CurrentRow.Cells["MaZmiany"].Value);
            }

            if (maZmiany && hasSelection)
            {
                var kursId = Convert.ToInt64(dgvKursy.CurrentRow.Cells["KursID"].Value);
                var trasa = dgvKursy.CurrentRow.Cells["Trasa"]?.Value?.ToString() ?? "";
                ShowRightPanelMode(true, kursId);
                _ = LoadKursZmianyAsync(kursId, trasa);
            }
            else if (_selectedKursIdForZmiany > 0)
            {
                // Wróć do wolnych zamówień gdy zaznaczono kurs bez zmian
                ShowRightPanelMode(false, 0);
            }
        }


        private async Task LoadKursyAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // ═══ FAZA A: Równoległe pobranie kursy + live zamówienia (różne bazy) ═══
                var kursyTask = _repozytorium.PobierzKursyPoDacieAsync(_selectedDate);
                var liveTask = PobierzLiveZamowieniaMapAsync();
                await Task.WhenAll(kursyTask, liveTask);

                _kursy = kursyTask.Result;
                _wypelnienia = new Dictionary<long, WynikPakowania>();
                var liveZamowieniaMap = liveTask.Result;

                System.Diagnostics.Debug.WriteLine($"[Transport] Faza A: {_kursy?.Count ?? 0} kursów, {liveZamowieniaMap.Count} live zam. w {sw.ElapsedMilliseconds}ms");

                // ═══ FAZA B: Bulk pobranie WSZYSTKICH ładunków jednym zapytaniem ═══
                var allKursIds = _kursy?.Select(k => k.KursID).ToList() ?? new List<long>();
                var allLadunkiMap = allKursIds.Count > 0
                    ? await _repozytorium.PobierzLadunkiDlaKursowAsync(allKursIds)
                    : new Dictionary<long, List<Ladunek>>();

                System.Diagnostics.Debug.WriteLine($"[Transport] Faza B: bulk ładunki w {sw.ElapsedMilliseconds}ms");

                // ═══ FAZA C: Przetworzenie ładunków (CPU, bez I/O) ═══
                var kursHandlowcy = new Dictionary<long, HashSet<string>>();
                var allKlientIds = new HashSet<int>();
                var ladunkiCountMap = new Dictionary<long, int>();
                var kursZamIds = new Dictionary<long, List<int>>();
                var allZamIds = new HashSet<int>();
                var kursPalety = new Dictionary<long, int>();
                var kursPojemniki = new Dictionary<long, int>();
                var kursKg = new Dictionary<long, decimal>();

                if (_kursy != null)
                {
                    foreach (var kurs in _kursy)
                    {
                        var ladunki = allLadunkiMap.TryGetValue(kurs.KursID, out var l) ? l : new List<Ladunek>();
                        ladunkiCountMap[kurs.KursID] = ladunki.Count;
                        int sumPal = 0, sumPoj = 0;
                        decimal sumKg = 0;

                        foreach (var lad in ladunki)
                        {
                            if (lad.KodKlienta?.StartsWith("ZAM_") == true &&
                                int.TryParse(lad.KodKlienta.Substring(4), out int zamId))
                            {
                                if (liveZamowieniaMap.TryGetValue(zamId, out var liveData))
                                {
                                    lad.PojemnikiE2 = liveData.Pojemniki;
                                    lad.TrybE2 = liveData.TrybE2;
                                    allKlientIds.Add(liveData.KlientId);
                                    sumPal += liveData.Palety;
                                    sumPoj += liveData.Pojemniki;
                                    sumKg += liveData.IloscKg;

                                    if (!kursHandlowcy.ContainsKey(kurs.KursID))
                                        kursHandlowcy[kurs.KursID] = new HashSet<string>();
                                    kursHandlowcy[kurs.KursID].Add(liveData.KlientId.ToString());
                                }

                                if (!kursZamIds.ContainsKey(kurs.KursID))
                                    kursZamIds[kurs.KursID] = new List<int>();
                                kursZamIds[kurs.KursID].Add(zamId);
                                allZamIds.Add(zamId);
                            }
                        }

                        kursPalety[kurs.KursID] = sumPal;
                        kursPojemniki[kurs.KursID] = sumPoj;
                        kursKg[kurs.KursID] = sumKg;

                        try
                        {
                            _wypelnienia[kurs.KursID] = _repozytorium.ObliczPakowanieZLadunkow(ladunki, kurs.PojazdID ?? 0);
                        }
                        catch
                        {
                            _wypelnienia[kurs.KursID] = new WynikPakowania { SumaE2 = 0, PaletyNominal = 0, ProcNominal = 0 };
                        }
                    }
                }

                _kursZamIds = kursZamIds;

                // ═══ FAZA D: 3 niezależne zapytania RÓWNOLEGLE (3 różne serwery/bazy) ═══
                // D1: TransportZmiany (TransportPL) — pending changes
                // D2: Klienci + Handlowcy (Handel) — jedno połączenie!
                // D3: Nazwy użytkowników (LibraNet)
                var allUserIds = _kursy?.Select(k => k.Utworzyl)
                    .Concat(_kursy?.Select(k => k.Zmienil) ?? Enumerable.Empty<string>())
                    .Where(id => !string.IsNullOrEmpty(id)).Distinct() ?? Enumerable.Empty<string>();

                var pendingTask = LoadPendingChangesAsync(allZamIds);
                var klienciHandlowcyTask = LoadKlienciAndHandlowcyAsync(allKlientIds);
                var userNamesTask = PobierzNazwyUzytkownikowAsync(allUserIds);

                await Task.WhenAll(pendingTask, klienciHandlowcyTask, userNamesTask);

                var zamIdsWithPendingChanges = pendingTask.Result;
                var handlowcyMap = klienciHandlowcyTask.Result;

                System.Diagnostics.Debug.WriteLine($"[Transport] Faza D: parallel done w {sw.ElapsedMilliseconds}ms");

                // Ustal MaZmiany per kurs
                var kursHasPendingChanges = new Dictionary<long, bool>();
                foreach (var kvp in kursZamIds)
                    kursHasPendingChanges[kvp.Key] = kvp.Value.Any(zamId => zamIdsWithPendingChanges.Contains(zamId));

                // Zamień KlientId na nazwiska handlowców
                var kursHandlowcyNazwy = new Dictionary<long, string>();
                foreach (var kvp in kursHandlowcy)
                {
                    var nazwy = new HashSet<string>();
                    foreach (var klientIdStr in kvp.Value)
                    {
                        if (int.TryParse(klientIdStr, out int kId) && handlowcyMap.TryGetValue(kId, out var nazwa))
                            nazwy.Add(nazwa);
                    }
                    kursHandlowcyNazwy[kvp.Key] = nazwy.Count > 0 ? string.Join(", ", nazwy.Select(SkrocNazwe)) : "";
                }

                var dt = new DataTable();
                dt.Columns.Add("KursID", typeof(long));
                dt.Columns.Add("Godzina", typeof(string));
                dt.Columns.Add("Kierowca", typeof(string));
                dt.Columns.Add("KierowcaID", typeof(int)); // Ukryta - do filtrowania
                dt.Columns.Add("Pojazd", typeof(string));
                dt.Columns.Add("PojazdID", typeof(int)); // Ukryta - do filtrowania
                dt.Columns.Add("Trasa", typeof(string));
                dt.Columns.Add("Pal", typeof(int));
                dt.Columns.Add("Poj", typeof(int));
                dt.Columns.Add("KG", typeof(decimal));
                dt.Columns.Add("Wypełnienie", typeof(decimal));
                dt.Columns.Add("Status", typeof(string));
                dt.Columns.Add("Utworzył", typeof(string));
                dt.Columns.Add("UtworzylId", typeof(string)); // Ukryta - userId do avatara
                dt.Columns.Add("Handlowcy", typeof(string));
                dt.Columns.Add("WymagaPrzydzialu", typeof(bool)); // Ukryta kolumna do formatowania
                dt.Columns.Add("LiczbaLadunkow", typeof(int));   // Ukryta - F1
                dt.Columns.Add("MaZmiany", typeof(bool));        // Ukryta - F2
                dt.Columns.Add("OpisZmian", typeof(string));     // Ukryta - F2
                dt.Columns.Add("Alert", typeof(string));         // Widoczna - F2

                if (_kursy != null)
                {
                    foreach (var kurs in _kursy.OrderBy(k => k.GodzWyjazdu))
                    {

                        var wyp = _wypelnienia.ContainsKey(kurs.KursID) ? _wypelnienia[kurs.KursID] : null;
                        var wymagaPrzydzialu = kurs.WymagaPrzydzialu;

                        // Wyświetl informację o braku przypisania
                        var kierowcaTekst = string.IsNullOrEmpty(kurs.KierowcaNazwa)
                            ? "⚠ BRAK"
                            : kurs.KierowcaNazwa;

                        var pojazdTekst = string.IsNullOrEmpty(kurs.PojazdRejestracja)
                            ? "⚠ BRAK"
                            : kurs.PojazdRejestracja;

                        // Status z bazy lub "Planowany" jako domyślny
                        var status = kurs.Status ?? "Planowany";

                        // Kto utworzył kurs - skrócona nazwa + data
                        var utworzylId = kurs.Utworzyl ?? "";
                        var utworzylName = "";
                        if (!string.IsNullOrEmpty(utworzylId))
                        {
                            var fullName = _userNameCache.TryGetValue(utworzylId, out var n) ? n : utworzylId;
                            utworzylName = $"{SkrocNazwe(fullName)} ({kurs.UtworzonoUTC.ToLocalTime():dd.MM HH:mm})";
                        }

                        // Handlowcy (kto ostatnio modyfikował zamówienia w kursie)
                        var handlowcy = kursHandlowcyNazwy.ContainsKey(kurs.KursID)
                            ? kursHandlowcyNazwy[kurs.KursID] : "";

                        // F1+F2
                        var liczbaLadunkow = ladunkiCountMap.ContainsKey(kurs.KursID) ? ladunkiCountMap[kurs.KursID] : 0;
                        var maZmiany = kursHasPendingChanges.ContainsKey(kurs.KursID) && kursHasPendingChanges[kurs.KursID];
                        var opisZmian = "";

                        dt.Rows.Add(
                            kurs.KursID,
                            kurs.GodzWyjazdu?.ToString(@"hh\:mm") ?? "--:--",
                            kierowcaTekst,
                            kurs.KierowcaID ?? 0,
                            pojazdTekst,
                            kurs.PojazdID ?? 0,
                            kurs.Trasa ?? "",
                            kursPalety.ContainsKey(kurs.KursID) ? kursPalety[kurs.KursID] : 0,
                            kursPojemniki.ContainsKey(kurs.KursID) ? kursPojemniki[kurs.KursID] : 0,
                            kursKg.ContainsKey(kurs.KursID) ? kursKg[kurs.KursID] : 0m,
                            wyp?.ProcNominal ?? 0,
                            status,
                            utworzylName,
                            utworzylId,
                            handlowcy,
                            wymagaPrzydzialu,
                            liczbaLadunkow,
                            maZmiany,
                            opisZmian,
                            maZmiany ? "⚠" : ""
                        );
                    }
                }

                dgvKursy.SuspendLayout();
                dgvKursy.DataSource = dt;

                // Konfiguracja kolumn - ukryj pomocnicze
                foreach (var hiddenCol in new[] { "KursID", "WymagaPrzydzialu", "KierowcaID", "PojazdID", "UtworzylId", "Status", "LiczbaLadunkow", "MaZmiany", "OpisZmian" })
                    if (dgvKursy.Columns[hiddenCol] != null)
                        dgvKursy.Columns[hiddenCol].Visible = false;

                // Wyłącz AutoFill aby ustawić ręczne szerokości, Trasa dostanie Fill
                dgvKursy.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

                // Kolumna Alert - pierwsza widoczna
                if (dgvKursy.Columns["Alert"] != null)
                {
                    dgvKursy.Columns["Alert"].Width = 36;
                    dgvKursy.Columns["Alert"].HeaderText = "";
                    dgvKursy.Columns["Alert"].DisplayIndex = 0;
                    dgvKursy.Columns["Alert"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    dgvKursy.Columns["Alert"].DefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                    dgvKursy.Columns["Alert"].Resizable = DataGridViewTriState.False;
                    dgvKursy.Columns["Alert"].SortMode = DataGridViewColumnSortMode.NotSortable;
                    dgvKursy.Columns["Alert"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                }

                if (dgvKursy.Columns["Godzina"] != null)
                {
                    dgvKursy.Columns["Godzina"].Width = 55;
                    dgvKursy.Columns["Godzina"].HeaderText = "Wyj.\ngodz.";
                    dgvKursy.Columns["Godzina"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }

                if (dgvKursy.Columns["Kierowca"] != null)
                {
                    dgvKursy.Columns["Kierowca"].Width = 110;
                    dgvKursy.Columns["Kierowca"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                    dgvKursy.Columns["Kierowca"].DefaultCellStyle.Font = new Font("Segoe UI", 8F);
                }

                if (dgvKursy.Columns["Pojazd"] != null)
                {
                    dgvKursy.Columns["Pojazd"].Width = 68;
                    dgvKursy.Columns["Pojazd"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                    dgvKursy.Columns["Pojazd"].DefaultCellStyle.Font = new Font("Segoe UI", 8F);
                }

                if (dgvKursy.Columns["Trasa"] != null)
                {
                    dgvKursy.Columns["Trasa"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    dgvKursy.Columns["Trasa"].MinimumWidth = 80;
                    dgvKursy.Columns["Trasa"].DefaultCellStyle.Font = new Font("Segoe UI", 8F);
                    dgvKursy.Columns["Trasa"].DefaultCellStyle.WrapMode = DataGridViewTriState.False;
                }

                if (dgvKursy.Columns["Pal"] != null)
                {
                    dgvKursy.Columns["Pal"].Width = 50;
                    dgvKursy.Columns["Pal"].HeaderText = "Pal";
                    dgvKursy.Columns["Pal"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    dgvKursy.Columns["Pal"].DefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                }

                if (dgvKursy.Columns["Poj"] != null)
                {
                    dgvKursy.Columns["Poj"].Width = 50;
                    dgvKursy.Columns["Poj"].HeaderText = "Poj";
                    dgvKursy.Columns["Poj"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    dgvKursy.Columns["Poj"].DefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                }

                if (dgvKursy.Columns["KG"] != null)
                {
                    dgvKursy.Columns["KG"].Width = 65;
                    dgvKursy.Columns["KG"].HeaderText = "KG";
                    dgvKursy.Columns["KG"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    dgvKursy.Columns["KG"].DefaultCellStyle.Format = "N0";
                    dgvKursy.Columns["KG"].DefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                }

                if (dgvKursy.Columns["Wypełnienie"] != null)
                {
                    dgvKursy.Columns["Wypełnienie"].Width = 50;
                    dgvKursy.Columns["Wypełnienie"].HeaderText = "Wyp.%";
                    dgvKursy.Columns["Wypełnienie"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }

                if (dgvKursy.Columns["Utworzył"] != null)
                {
                    dgvKursy.Columns["Utworzył"].Width = 130;
                }

                if (dgvKursy.Columns["Handlowcy"] != null)
                {
                    dgvKursy.Columns["Handlowcy"].HeaderText = "Handl.";
                    dgvKursy.Columns["Handlowcy"].Width = 90;
                }
                dgvKursy.ResumeLayout(true);

                System.Diagnostics.Debug.WriteLine("Calling UpdateSummary...");
                UpdateSummary();
                System.Diagnostics.Debug.WriteLine("UpdateSummary completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LoadKursyAsync: {ex.Message}");
                MessageBox.Show($"Błąd podczas ładowania kursów: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateSummary();
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void UpdateSummary()
        {
            // DEBUG: Sprawdź stan labels
            System.Diagnostics.Debug.WriteLine("=== UpdateSummary START ===");
            System.Diagnostics.Debug.WriteLine($"lblSummaryKursy is null: {lblSummaryKursy == null}");
            System.Diagnostics.Debug.WriteLine($"lblSummaryPojemniki is null: {lblSummaryPojemniki == null}");
            System.Diagnostics.Debug.WriteLine($"lblSummaryPalety is null: {lblSummaryPalety == null}");
            System.Diagnostics.Debug.WriteLine($"lblSummaryWypelnienie is null: {lblSummaryWypelnienie == null}");

            // Sprawdź czy labels istnieją - jeśli nie, nie rób nic
            if (lblSummaryKursy == null || lblSummaryPojemniki == null ||
                lblSummaryPalety == null || lblSummaryWypelnienie == null)
            {
                System.Diagnostics.Debug.WriteLine("One or more labels are null, skipping UpdateSummary");

                // Spróbuj znaleźć labels ponownie
                TryFindLabelsInPanels();

                // Jeśli nadal null, wyjdź
                if (lblSummaryKursy == null || lblSummaryPojemniki == null ||
                    lblSummaryPalety == null || lblSummaryWypelnienie == null)
                {
                    System.Diagnostics.Debug.WriteLine("Still null after TryFindLabelsInPanels, exiting");
                    return;
                }
            }

            if (_kursy == null)
            {
                lblSummaryKursy.Text = "0";
                lblSummaryPojemniki.Text = "0";
                lblSummaryPalety.Text = "0";
                lblSummaryWypelnienie.Text = "0%";
                return;
            }

            int liczbaKursow = _kursy.Count;
            int sumaPojemnikow = 0;
            int sumaPalet = 0;
            decimal srednieWypelnienie = 0;

            // Bezpieczne obliczenia z sprawdzeniem null
            if (_wypelnienia != null && _wypelnienia.Any())
            {
                try
                {
                    sumaPojemnikow = _wypelnienia.Sum(w => w.Value?.SumaE2 ?? 0);
                    sumaPalet = _wypelnienia.Sum(w => w.Value?.PaletyNominal ?? 0);

                    var validEntries = _wypelnienia.Where(w => w.Value != null).ToList();
                    if (validEntries.Any())
                    {
                        srednieWypelnienie = validEntries.Average(w => w.Value.ProcNominal);
                    }
                }
                catch (Exception ex)
                {
                    // W przypadku błędu, użyj wartości domyślnych
                    System.Diagnostics.Debug.WriteLine($"Error in calculations: {ex.Message}");
                    sumaPojemnikow = 0;
                    sumaPalet = 0;
                    srednieWypelnienie = 0;
                }
            }

            try
            {
                lblSummaryKursy.Text = liczbaKursow.ToString();
                lblSummaryPojemniki.Text = sumaPojemnikow.ToString();
                lblSummaryPalety.Text = sumaPalet.ToString();
                lblSummaryWypelnienie.Text = $"{srednieWypelnienie:F0}%";

                // Kolorowanie według wypełnienia
                if (srednieWypelnienie > 90)
                    lblSummaryWypelnienie.ForeColor = Color.FromArgb(231, 76, 60);
                else if (srednieWypelnienie > 75)
                    lblSummaryWypelnienie.ForeColor = Color.FromArgb(243, 156, 18);
                else
                    lblSummaryWypelnienie.ForeColor = Color.FromArgb(46, 204, 113);

                System.Diagnostics.Debug.WriteLine("UpdateSummary completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting label values: {ex.Message}");
            }
        }

        private void TryFindLabelsInPanels()
        {
            try
            {
                if (panelSummary != null)
                {
                    // Szukaj wszystkich labels w panelSummary
                    var allLabels = FindAllLabelsRecursive(panelSummary).ToList();
                    System.Diagnostics.Debug.WriteLine($"Found {allLabels.Count} labels in panelSummary");

                    foreach (var label in allLabels)
                    {
                        System.Diagnostics.Debug.WriteLine($"Label text: '{label.Text}'");

                        // Przypisz na podstawie wartości tekstowych
                        if (label.Text == "0" && lblSummaryKursy == null &&
                            label.Font.Size >= 16) // Większa czcionka = label wartości
                        {
                            lblSummaryKursy = label;
                            System.Diagnostics.Debug.WriteLine("Assigned lblSummaryKursy");
                        }
                        else if (label.Text == "0" && lblSummaryPojemniki == null && lblSummaryKursy != null &&
                                 label.Font.Size >= 16)
                        {
                            lblSummaryPojemniki = label;
                            System.Diagnostics.Debug.WriteLine("Assigned lblSummaryPojemniki");
                        }
                        else if (label.Text == "0" && lblSummaryPalety == null && lblSummaryPojemniki != null &&
                                 label.Font.Size >= 16)
                        {
                            lblSummaryPalety = label;
                            System.Diagnostics.Debug.WriteLine("Assigned lblSummaryPalety");
                        }
                        else if (label.Text == "0%" && lblSummaryWypelnienie == null &&
                                 label.Font.Size >= 16)
                        {
                            lblSummaryWypelnienie = label;
                            System.Diagnostics.Debug.WriteLine("Assigned lblSummaryWypelnienie");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in TryFindLabelsInPanels: {ex.Message}");
            }
        }

        private IEnumerable<Label> FindAllLabelsRecursive(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is Label label)
                    yield return label;

                foreach (var childLabel in FindAllLabelsRecursive(control))
                    yield return childLabel;
            }
        }

        private void DgvKursy_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = dgvKursy.Rows[e.RowIndex];

            // === Priorytet: CZERWONY > POMARAŃCZOWY > ŻÓŁTY ===
            int liczbaLadunkow = -1;
            if (row.Cells["LiczbaLadunkow"]?.Value != null && row.Cells["LiczbaLadunkow"].Value != DBNull.Value)
                liczbaLadunkow = Convert.ToInt32(row.Cells["LiczbaLadunkow"].Value);

            bool maZmiany = false;
            if (row.Cells["MaZmiany"]?.Value != null && row.Cells["MaZmiany"].Value != DBNull.Value)
                maZmiany = Convert.ToBoolean(row.Cells["MaZmiany"].Value);

            bool wymagaPrzydzialu = false;
            if (row.Cells["WymagaPrzydzialu"]?.Value != null)
                wymagaPrzydzialu = Convert.ToBoolean(row.Cells["WymagaPrzydzialu"].Value);

            if (liczbaLadunkow == 0)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(254, 230, 230);
                row.DefaultCellStyle.ForeColor = Color.FromArgb(180, 40, 40);
            }
            else if (maZmiany)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 243, 230);
            }
            else if (wymagaPrzydzialu)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 248, 225);
            }

            // Kolumna Alert
            if (dgvKursy.Columns[e.ColumnIndex].Name == "Alert")
            {
                var alertVal = e.Value?.ToString() ?? "";
                if (!string.IsNullOrEmpty(alertVal))
                {
                    e.CellStyle.ForeColor = Color.FromArgb(231, 76, 60);
                    e.CellStyle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
                    var opis = row.Cells["OpisZmian"]?.Value?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(opis))
                        row.Cells["Alert"].ToolTipText = opis;
                }
            }

            // Formatowanie kolumny kierowca - pomarańczowy tekst gdy brak przypisania
            if (dgvKursy.Columns[e.ColumnIndex].Name == "Kierowca")
            {
                var kierowcaTekst = e.Value?.ToString() ?? "";
                if (kierowcaTekst.Contains("BRAK"))
                {
                    e.CellStyle.ForeColor = Color.FromArgb(230, 126, 34);
                    e.CellStyle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
                }
            }

            // Formatowanie kolumny pojazd
            if (dgvKursy.Columns[e.ColumnIndex].Name == "Pojazd")
            {
                var pojazdTekst = e.Value?.ToString() ?? "";
                if (pojazdTekst.Contains("BRAK"))
                {
                    e.CellStyle.ForeColor = Color.FromArgb(230, 126, 34);
                    e.CellStyle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
                }
            }

            // Formatowanie kolumny wypełnienie
            if (dgvKursy.Columns[e.ColumnIndex].Name == "Wypełnienie")
            {
                if (e.Value != null && decimal.TryParse(e.Value.ToString(), out var wypelnienie))
                {
                    e.Value = $"{wypelnienie:F0}%";

                    if (wypelnienie > 100)
                        e.CellStyle.ForeColor = Color.FromArgb(231, 76, 60);
                    else if (wypelnienie > 90)
                        e.CellStyle.ForeColor = Color.FromArgb(243, 156, 18);
                    else
                        e.CellStyle.ForeColor = Color.FromArgb(46, 204, 113);

                    e.CellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                }
            }

            // Formatowanie statusu
            if (dgvKursy.Columns[e.ColumnIndex].Name == "Status")
            {
                var status = e.Value?.ToString() ?? "";

                switch (status)
                {
                    case "Zakończony":
                        e.CellStyle.ForeColor = Color.FromArgb(46, 204, 113);
                        break;
                    case "W realizacji":
                        e.CellStyle.ForeColor = Color.FromArgb(52, 152, 219);
                        break;
                    case "Anulowany":
                        e.CellStyle.ForeColor = Color.FromArgb(231, 76, 60);
                        row.DefaultCellStyle.BackColor = Color.FromArgb(254, 245, 245);
                        break;
                    default:
                        e.CellStyle.ForeColor = Color.FromArgb(127, 140, 141);
                        break;
                }
            }
        }

        private void DgvKursy_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var colName = dgvKursy.Columns[e.ColumnIndex].Name;

            // ═══ Kolumna Alert — pulsujące kółko z "!" ═══
            if (colName == "Alert")
            {
                var alertVal = e.Value?.ToString() ?? "";
                if (string.IsNullOrEmpty(alertVal)) return;

                e.Handled = true;
                var isSel = dgvKursy.Rows[e.RowIndex].Selected;
                using (var bgBrush = new SolidBrush(isSel ? e.CellStyle.SelectionBackColor : e.CellStyle.BackColor))
                    e.Graphics.FillRectangle(bgBrush, e.CellBounds);

                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                var circleSize = 24;
                var cx = e.CellBounds.Left + (e.CellBounds.Width - circleSize) / 2;
                var cy = e.CellBounds.Top + (e.CellBounds.Height - circleSize) / 2;

                // Pulsujący glow (zewnętrzne kółko, alpha sterowane timerem)
                var glowAlpha = (int)(80 * _pulseAlpha);
                using (var glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, 230, 126, 34)))
                    e.Graphics.FillEllipse(glowBrush, cx - 4, cy - 4, circleSize + 8, circleSize + 8);

                // Główne kółko (alpha sterowane timerem)
                var mainAlpha = (int)(255 * _pulseAlpha);
                using (var brush = new SolidBrush(Color.FromArgb(mainAlpha, 230, 126, 34)))
                    e.Graphics.FillEllipse(brush, cx, cy, circleSize, circleSize);

                e.Graphics.DrawString("!", _fontAlertBang, _brushWhite,
                    new RectangleF(cx, cy, circleSize, circleSize), _sfCenter);
                return;
            }

            if (colName != "Utworzył" && colName != "Handlowcy") return;

            var cellValue = e.Value?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(cellValue)) return;

            e.Handled = true;
            var selected = dgvKursy.Rows[e.RowIndex].Selected;

            using (var bgBrush = new SolidBrush(selected ? e.CellStyle.SelectionBackColor : e.CellStyle.BackColor))
                e.Graphics.FillRectangle(bgBrush, e.CellBounds);

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            if (colName == "Utworzył")
            {
                var userId = dgvKursy.Rows[e.RowIndex].Cells["UtworzylId"]?.Value?.ToString() ?? "";
                string displayName;
                string dateInfo = "";
                var parenIdx = cellValue.IndexOf('(');
                if (parenIdx > 0) { displayName = cellValue.Substring(0, parenIdx).Trim(); dateInfo = cellValue.Substring(parenIdx).Trim(); }
                else displayName = cellValue;

                var avatarSize = 30;
                var avatarX = e.CellBounds.Left + 6;
                var avatarY = e.CellBounds.Top + (e.CellBounds.Height - avatarSize) / 2;

                var avatarImg = GetOrCreateAvatar(
                    !string.IsNullOrEmpty(userId) ? userId : displayName, displayName, avatarSize);
                if (avatarImg != null)
                    e.Graphics.DrawImage(avatarImg, avatarX, avatarY, avatarSize, avatarSize);

                var textX = avatarX + avatarSize + 6;
                var textColor = selected ? Color.White : Color.FromArgb(40, 50, 65);
                var dateColor = selected ? Color.FromArgb(200, 220, 255) : Color.FromArgb(130, 140, 155);

                using (var brush = new SolidBrush(textColor))
                    e.Graphics.DrawString(displayName, _fontNameBold, brush, textX,
                        e.CellBounds.Top + (e.CellBounds.Height / 2) - (string.IsNullOrEmpty(dateInfo) ? 7 : 12));

                if (!string.IsNullOrEmpty(dateInfo))
                    using (var brush = new SolidBrush(dateColor))
                        e.Graphics.DrawString(dateInfo, _fontDateSmall, brush, textX, e.CellBounds.Top + (e.CellBounds.Height / 2) + 2);
            }
            else // Handlowcy
            {
                var names = cellValue.Split(',').Select(n => n.Trim()).Where(n => !string.IsNullOrEmpty(n)).ToList();
                if (names.Count == 0) return;

                var avatarSize = 28;
                var overlap = 8;
                var startX = e.CellBounds.Left + 6;
                var avatarY = e.CellBounds.Top + (e.CellBounds.Height - avatarSize) / 2;

                var count = Math.Min(names.Count, 3);
                for (int i = count - 1; i >= 0; i--)
                {
                    var x = startX + i * (avatarSize - overlap);
                    var avatarImg = GetHandlowiecAvatar(names[i], avatarSize);
                    if (avatarImg != null)
                    {
                        using (var pen = new Pen(selected ? e.CellStyle.SelectionBackColor : Color.White, 2))
                            e.Graphics.DrawEllipse(pen, x - 1, avatarY - 1, avatarSize + 1, avatarSize + 1);
                        e.Graphics.DrawImage(avatarImg, x, avatarY, avatarSize, avatarSize);
                    }
                }

                var textX = startX + count * (avatarSize - overlap) + overlap + 2;
                var textColor = selected ? Color.White : Color.FromArgb(40, 50, 65);
                var displayText = names.Count <= 2 ? string.Join(", ", names) : $"{names[0]} +{names.Count - 1}";

                using (var brush = new SolidBrush(textColor))
                {
                    var textRect = new RectangleF(textX, e.CellBounds.Top, e.CellBounds.Right - textX - 4, e.CellBounds.Height);
                    e.Graphics.DrawString(displayText, _fontHandlText, brush, textRect, _sfLeftCenter);
                }
            }

        }

        // USUNIĘTE stare metody które nie działały:
        // - BtnKierowcy_Click
        // - BtnPojazdy_Click
        // - CreateSummaryTile
        // - TryFindLabelsInPanels
        // - FindAllLabelsRecursive

        #region Event Handlers - Obsługa przycisków - NAPRAWIONE

        private void BtnNowyKurs_Click(object sender, EventArgs e)
        {
            try
            {
                using var dlg = new EdytorKursuWithPalety(_repozytorium, _selectedDate, _currentUser);
                dlg.ShowDialog(this);
                // ZAWSZE odśwież po zamknięciu edytora (nie tylko po OK)
                _ = LoadKursyAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas tworzenia nowego kursu: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnEdytujKurs_Click(object sender, EventArgs e)
        {
            if (dgvKursy.CurrentRow == null)
            {
                MessageBox.Show("Proszę wybrać kurs do edycji.",
                    "Brak wyboru", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var kursId = Convert.ToInt64(dgvKursy.CurrentRow.Cells["KursID"].Value);
                var kurs = _kursy.FirstOrDefault(k => k.KursID == kursId);

                if (kurs == null) return;

                using var dlg = new EdytorKursuWithPalety(_repozytorium, kurs, _currentUser);
                dlg.ShowDialog(this);
                // ZAWSZE odśwież po zamknięciu edytora (nie tylko po OK)
                _ = LoadKursyAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas edycji kursu: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Szybkie przypisanie kierowcy i pojazdu do kursu
        /// </summary>
        private async void BtnPrzydziel_Click(object sender, EventArgs e)
        {
            if (dgvKursy.CurrentRow == null) return;

            try
            {
                var kursId = Convert.ToInt64(dgvKursy.CurrentRow.Cells["KursID"].Value);
                var kurs = _kursy.FirstOrDefault(k => k.KursID == kursId);

                if (kurs == null) return;

                // Otwórz dialog szybkiego przypisania
                using var dlg = new SzybkiePrzypisanieDialog(_repozytorium, kurs);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    await LoadKursyAsync();
                    MessageBox.Show("Zasoby zostały przydzielone do kursu.",
                        "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas przypisywania zasobów: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnUsunKurs_Click(object sender, EventArgs e)
        {
            if (dgvKursy.CurrentRow == null) return;

            if (MessageBox.Show("Czy na pewno usunąć wybrany kurs wraz ze wszystkimi ładunkami?",
                "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                var kursId = Convert.ToInt64(dgvKursy.CurrentRow.Cells["KursID"].Value);
                await _repozytorium.UsunKursAsync(kursId);
                await LoadKursyAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas usuwania kursu: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnAkceptujZmianyKursu_Click(object sender, EventArgs e)
        {
            long kursId;
            string trasa;

            if (_selectedKursIdForZmiany > 0)
            {
                kursId = _selectedKursIdForZmiany;
                trasa = dgvKursy.CurrentRow?.Cells["Trasa"]?.Value?.ToString() ?? "";
            }
            else if (dgvKursy.CurrentRow != null)
            {
                kursId = Convert.ToInt64(dgvKursy.CurrentRow.Cells["KursID"].Value);
                trasa = dgvKursy.CurrentRow.Cells["Trasa"]?.Value?.ToString() ?? "";
            }
            else return;

            var msg = $"Akceptujesz zmiany w kursie";
            if (!string.IsNullOrEmpty(trasa)) msg += $" ({trasa})";
            msg += "\n\nAktualny stan zamówień zostanie zapisany jako nowy snapshot.\n⚠ zniknie dopóki zamówienie nie zostanie ponownie zmienione.\n\nCzy kontynuować?";

            if (MessageBox.Show(msg, "Akceptacja zmian", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                btnAkceptujKursZmiany.Enabled = false;
                btnAkceptujKursZmiany.Text = "Akceptowanie...";

                var zamIds = _kursZamIds.ContainsKey(kursId) ? _kursZamIds[kursId] : new List<int>();
                var user = App.UserFullName ?? App.UserID ?? "system";

                var connStr = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                // Oznacz TransportZmiany jako zaakceptowane
                var zamIdsStr = string.Join(",", zamIds);
                using (var cmdAcc = new SqlCommand($@"
                    UPDATE TransportZmiany SET StatusZmiany = 'Zaakceptowano',
                        ZaakceptowanePrzez = @User, DataAkceptacji = GETDATE()
                    WHERE StatusZmiany = 'Oczekuje' AND TypZmiany NOT IN ('NoweZamowienie', 'ZmianaStatusu')
                      AND ZamowienieId IN ({zamIdsStr})", conn))
                {
                    cmdAcc.Parameters.AddWithValue("@User", user);
                    await cmdAcc.ExecuteNonQueryAsync();
                }

                await LoadKursyAsync();
                ShowRightPanelMode(false, 0);

                MessageBox.Show("Zmiany zaakceptowane.",
                    "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd akceptacji zmian: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnAkceptujKursZmiany.Text = "✔ Akceptuj wszystkie";
                btnAkceptujKursZmiany.Enabled = true;
            }
        }

        #endregion

        #region Obsługa Map Google

        private void BtnMapa_Click(object sender, EventArgs e)
        {
            try
            {
                var connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                var connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

                var mapWindow = new Kalendarz1.Transport.TransportMapaWindow(connLibra, connHandel, _selectedDate);
                mapWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas otwierania mapy: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task<List<string>> PobierzAdresyKursu(long kursId)
        {
            var ladunki = await _repozytorium.PobierzLadunkiAsync(kursId);

            if (!ladunki.Any())
                return new List<string>();

            var adresy = new List<string>();
            string bazaAdres = "Koziółki 40, 95-061 Dmosin, Polska";
            adresy.Add(bazaAdres);

            foreach (var ladunek in ladunki.OrderBy(l => l.Kolejnosc))
            {
                string adres = "";

                if (await CzyToZamowienie(ladunek.KodKlienta))
                {
                    adres = await PobierzAdresZZamowienia(ladunek.KodKlienta);
                }
                else
                {
                    adres = await PobierzAdresPoNazwie(ladunek.KodKlienta);
                    if (string.IsNullOrEmpty(adres) && !string.IsNullOrEmpty(ladunek.Uwagi))
                    {
                        adres = await PobierzAdresPoNazwie(ladunek.Uwagi);
                    }
                }

                if (!string.IsNullOrEmpty(adres) && adres.Trim().Length > 5)
                {
                    if (!adres.ToLower().Contains("polska"))
                    {
                        adres += ", Polska";
                    }
                    adresy.Add(adres);
                }
            }

            // Powrót do bazy
            if (adresy.Count > 1)
                adresy.Add(bazaAdres);

            return adresy;
        }

        private async Task OtworzMapeTrasy(long kursId)
        {
            try
            {
                var adresy = await PobierzAdresyKursu(kursId);

                if (adresy.Count <= 1)
                {
                    MessageBox.Show("Kurs nie ma żadnych ładunków do wyświetlenia trasy.",
                        "Brak danych", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string googleMapsUrl = UtworzUrlGoogleMaps(adresy);
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = googleMapsUrl,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    Clipboard.SetText(googleMapsUrl);
                    MessageBox.Show("URL skopiowany do schowka.", "Informacja",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task<string> PobierzAdresZZamowienia(string kodKlienta)
        {
            try
            {
                var connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

                int zamId = 0;
                if (kodKlienta.StartsWith("ZAM_"))
                {
                    int.TryParse(kodKlienta.Substring(4), out zamId);
                }
                else
                {
                    int.TryParse(kodKlienta, out zamId);
                }

                if (zamId <= 0) return "";

                await using var cnLibra = new SqlConnection(connLibra);
                await cnLibra.OpenAsync();

                var sqlZam = "SELECT KlientId FROM dbo.ZamowieniaMieso WHERE Id = @ZamId";
                using var cmdZam = new SqlCommand(sqlZam, cnLibra);
                cmdZam.Parameters.AddWithValue("@ZamId", zamId);

                var klientIdObj = await cmdZam.ExecuteScalarAsync();
                if (klientIdObj == null) return "";

                int klientId = Convert.ToInt32(klientIdObj);
                return await PobierzAdresKlienta(klientId);
            }
            catch
            {
                return "";
            }
        }

        private async Task<string> PobierzAdresKlienta(int klientId)
        {
            try
            {
                var connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

                await using var cn = new SqlConnection(connHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT 
                        ISNULL(poa.Street, '') AS Ulica,
                        ISNULL(poa.Postcode, '') AS KodPocztowy,
                        ISNULL(poa.City, '') AS Miasto
                    FROM [HANDEL].[SSCommon].[STContractors] c
                    LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] poa 
                        ON poa.ContactGuid = c.ContactGuid 
                        AND poa.AddressName = N'adres domyślny'
                    WHERE c.Id = @KlientId";

                using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@KlientId", klientId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var ulica = reader.GetString(0).Trim();
                    var kodPocztowy = reader.GetString(1).Trim();
                    var miasto = reader.GetString(2).Trim();

                    var adresParts = new List<string>();
                    if (!string.IsNullOrEmpty(ulica)) adresParts.Add(ulica);
                    if (!string.IsNullOrEmpty(kodPocztowy) && !string.IsNullOrEmpty(miasto))
                        adresParts.Add($"{kodPocztowy} {miasto}");
                    else if (!string.IsNullOrEmpty(miasto))
                        adresParts.Add(miasto);

                    return adresParts.Any() ? string.Join(", ", adresParts) : "";
                }

                return "";
            }
            catch
            {
                return "";
            }
        }

        private async Task<string> PobierzAdresPoNazwie(string nazwaKlienta)
        {
            try
            {
                var connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

                var czystaNazwa = nazwaKlienta;
                var idx = czystaNazwa.IndexOf('(');
                if (idx > 0)
                {
                    czystaNazwa = czystaNazwa.Substring(0, idx).Trim();
                }

                await using var cn = new SqlConnection(connHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT TOP 1
                        ISNULL(poa.Street, '') AS Ulica,
                        ISNULL(poa.Postcode, '') AS KodPocztowy,
                        ISNULL(poa.City, '') AS Miasto
                    FROM [HANDEL].[SSCommon].[STContractors] c
                    LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] poa 
                        ON poa.ContactGuid = c.ContactGuid 
                        AND poa.AddressName = N'adres domyślny'
                    WHERE c.Shortcut = @Nazwa";

                using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Nazwa", czystaNazwa);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var ulica = reader.GetString(0).Trim();
                    var kodPocztowy = reader.GetString(1).Trim();
                    var miasto = reader.GetString(2).Trim();

                    var adresParts = new List<string>();
                    if (!string.IsNullOrEmpty(ulica)) adresParts.Add(ulica);
                    if (!string.IsNullOrEmpty(kodPocztowy) && !string.IsNullOrEmpty(miasto))
                        adresParts.Add($"{kodPocztowy} {miasto}");
                    else if (!string.IsNullOrEmpty(miasto))
                        adresParts.Add(miasto);

                    return adresParts.Any() ? string.Join(", ", adresParts) : "";
                }

                return "";
            }
            catch
            {
                return "";
            }
        }

        private async Task<bool> CzyToZamowienie(string kodKlienta)
        {
            if (string.IsNullOrEmpty(kodKlienta)) return false;

            if (kodKlienta.StartsWith("ZAM_") && int.TryParse(kodKlienta.Substring(4), out _))
                return true;

            if (int.TryParse(kodKlienta, out int zamId))
            {
                try
                {
                    var connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                    await using var cn = new SqlConnection(connLibra);
                    await cn.OpenAsync();

                    var sql = "SELECT COUNT(*) FROM dbo.ZamowieniaMieso WHERE Id = @ZamId";
                    using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@ZamId", zamId);

                    var count = (int)await cmd.ExecuteScalarAsync();
                    return count > 0;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Pobiera LIVE dane zamówień z ZamowieniaMieso (LiczbaPojemnikow, TrybE2)
        /// aby widok główny zawsze pokazywał aktualne wartości.
        /// </summary>
        private string _lastLiveZamError = null;
        private async Task<Dictionary<int, (int Pojemniki, bool TrybE2, int KlientId, string TransportStatus, int Palety, decimal IloscKg, DateTime? DataPrzyjazdu)>> PobierzLiveZamowieniaMapAsync()
        {
            var map = new Dictionary<int, (int Pojemniki, bool TrybE2, int KlientId, string TransportStatus, int Palety, decimal IloscKg, DateTime? DataPrzyjazdu)>();
            _lastLiveZamError = null;
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                var sql = @"SELECT zm.Id,
                                   ISNULL(zm.LiczbaPojemnikow, 0),
                                   ISNULL(zm.TrybE2, 0),
                                   ISNULL(zm.KlientId, 0),
                                   ISNULL(zm.TransportStatus, 'Oczekuje'),
                                   ISNULL(zm.LiczbaPalet, 0),
                                   ISNULL(SUM(zmt.Ilosc), 0),
                                   zm.DataPrzyjazdu
                           FROM dbo.ZamowieniaMieso zm
                           LEFT JOIN dbo.ZamowieniaMiesoTowar zmt ON zm.Id = zmt.ZamowienieId
                           WHERE ISNULL(zm.Status, '') <> 'Anulowane'
                             AND zm.DataZamowienia >= @DateFrom
                             AND zm.DataZamowienia <= @DateTo
                           GROUP BY zm.Id, zm.LiczbaPojemnikow, zm.TrybE2, zm.KlientId,
                                    zm.TransportStatus, zm.LiczbaPalet, zm.DataPrzyjazdu";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@DateFrom", _selectedDate.AddDays(-7));
                cmd.Parameters.AddWithValue("@DateTo", _selectedDate.AddDays(14));
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var id = Convert.ToInt32(rdr[0]);
                    var pojemniki = Convert.ToInt32(rdr[1]);
                    var trybE2 = Convert.ToBoolean(rdr[2]);
                    var klientId = Convert.ToInt32(rdr[3]);
                    var transportStatus = rdr[4]?.ToString() ?? "Oczekuje";
                    var palety = Convert.ToInt32(rdr[5]);
                    var iloscKg = Convert.ToDecimal(rdr[6]);
                    DateTime? dataPrzyjazdu = rdr.IsDBNull(7) ? null : Convert.ToDateTime(rdr[7]);

                    map[id] = (pojemniki, trybE2, klientId, transportStatus, palety, iloscKg, dataPrzyjazdu);
                }
            }
            catch (Exception ex)
            {
                _lastLiveZamError = ex.Message;
                System.Diagnostics.Debug.WriteLine($"[Transport] PobierzLiveZamowieniaMapAsync ERROR: {ex.Message}");
            }
            System.Diagnostics.Debug.WriteLine($"[Transport] PobierzLiveZamowieniaMapAsync loaded {map.Count} orders");
            return map;
        }

        private async Task<Dictionary<int, string>> PobierzHandlowcowAsync(IEnumerable<int> klientIds)
        {
            var map = new Dictionary<int, string>();
            var ids = klientIds.Distinct().ToList();
            if (ids.Count == 0) return map;
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                var sql = $@"SELECT c.Id, ISNULL(wym.CDim_Handlowiec_Val, '')
                            FROM SSCommon.STContractors c
                            LEFT JOIN SSCommon.ContractorClassification wym ON c.Id = wym.ElementId
                            WHERE c.Id IN ({string.Join(",", ids)})";
                await using var cmd = new SqlCommand(sql, cn);
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var id = rdr.GetInt32(0);
                    var handlowiec = rdr.GetString(1);
                    if (!string.IsNullOrWhiteSpace(handlowiec))
                        map[id] = handlowiec;
                }
            }
            catch { }
            return map;
        }

        private async Task PobierzNazwyUzytkownikowAsync(IEnumerable<string> userIds)
        {
            var toFetch = userIds.Where(id => !string.IsNullOrEmpty(id) && !_userNameCache.ContainsKey(id)).Distinct().ToList();
            if (toFetch.Count == 0) return;
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                var paramNames = toFetch.Select((id, i) => $"@u{i}").ToList();
                var sql = $"SELECT ID, ISNULL(Name, ID) FROM operators WHERE ID IN ({string.Join(",", paramNames)})";
                await using var cmd = new SqlCommand(sql, cn);
                for (int i = 0; i < toFetch.Count; i++)
                    cmd.Parameters.AddWithValue($"@u{i}", toFetch[i]);
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var id = rdr.GetString(0);
                    var name = rdr.GetString(1);
                    _userNameCache[id] = name;
                }
            }
            catch { }
            // Zapewnij, że każdy userId ma wpis (fallback na sam userId)
            foreach (var id in toFetch)
                if (!_userNameCache.ContainsKey(id))
                    _userNameCache[id] = id;
        }

        /// <summary>
        /// Sprawdza które zamówienia mają oczekujące zmiany (TransportPL DB)
        /// </summary>
        private async Task<HashSet<int>> LoadPendingChangesAsync(HashSet<int> allZamIds)
        {
            var result = new HashSet<int>();
            if (allZamIds.Count == 0) return result;
            try
            {
                var connStr = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                using var connTr = new SqlConnection(connStr);
                await connTr.OpenAsync();
                var zamIdsStr = string.Join(",", allZamIds);
                using var cmd = new SqlCommand($@"
                    SELECT DISTINCT ZamowienieId FROM TransportZmiany
                    WHERE StatusZmiany = 'Oczekuje'
                      AND TypZmiany NOT IN ('NoweZamowienie', 'ZmianaStatusu')
                      AND ZamowienieId IN ({zamIdsStr})", connTr);
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                    result.Add(Convert.ToInt32(rdr[0]));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Transport] TransportZmiany query error: {ex.Message}");
            }
            return result;
        }

        /// <summary>
        /// Pobiera nazwy klientów i handlowców w jednym połączeniu (Handel DB)
        /// Zastępuje osobne LoadMissingKlienciAsync + PobierzHandlowcowAsync
        /// </summary>
        private async Task<Dictionary<int, string>> LoadKlienciAndHandlowcyAsync(HashSet<int> klientIds)
        {
            var handlowcyMap = new Dictionary<int, string>();
            try
            {
                using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                // Jedno zapytanie pobiera nazwę klienta + handlowca + adres
                var sql = @"SELECT c.Id,
                                   ISNULL(c.Shortcut, 'KH ' + CAST(c.Id AS VARCHAR(10))),
                                   ISNULL(wym.CDim_Handlowiec_Val, ''),
                                   ISNULL(poa.Postcode, '') + ' ' + ISNULL(poa.Street, '')
                            FROM SSCommon.STContractors c
                            LEFT JOIN SSCommon.ContractorClassification wym ON c.Id = wym.ElementId
                            LEFT JOIN SSCommon.STPostOfficeAddresses poa
                                ON poa.ContactGuid = c.ContactGuid AND poa.AddressName = N'adres domyślny'";
                using var cmd = new SqlCommand(sql, cn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(0);
                    _klienciCache[id] = reader.GetString(1);
                    var handlowiec = reader.GetString(2);
                    if (!string.IsNullOrWhiteSpace(handlowiec))
                        handlowcyMap[id] = handlowiec;
                    var addr = reader.GetString(3).Trim();
                    if (!string.IsNullOrWhiteSpace(addr))
                        _klienciAdresCache[id] = addr;
                }
                _klienciCacheTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Transport] LoadKlienciAndHandlowcy error: {ex.Message}");
            }
            return handlowcyMap;
        }

        private async Task EnsureHandlowiecMappingLoadedAsync()
        {
            if (_handlowiecMapowanie != null) return;
            _handlowiecMapowanie = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                // Jedno połączenie, dwa zapytania (ta sama baza LibraNet)
                using var cnLib = new SqlConnection(_connLibra);
                await cnLib.OpenAsync();

                using (var cmd = new SqlCommand("SELECT HandlowiecName, UserID FROM UserHandlowcy", cnLib))
                using (var reader = await cmd.ExecuteReaderAsync())
                    while (await reader.ReadAsync())
                        _handlowiecMapowanie[reader.GetString(0)] = reader.GetString(1);

                // Odwrotne mapowanie fullName → userId (z tabeli operators)
                using var cmd2 = new SqlCommand("SELECT ID, ISNULL(Name, ID) FROM operators WHERE Name IS NOT NULL AND Name <> ''", cnLib);
                using var rdr2 = await cmd2.ExecuteReaderAsync();
                while (await rdr2.ReadAsync())
                {
                    var uid = rdr2.GetString(0);
                    var name = rdr2.GetString(1);
                    _fullNameToUserIdCache[name] = uid;
                }
            }
            catch { }
        }

        /// <summary>
        /// Zwraca avatar po fullName (np. "Artur Danielewski") —
        /// próbuje zamienić na userId (login) aby znaleźć zdjęcie z sieci
        /// </summary>
        private Image GetAvatarByFullName(string fullName, int size)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return GetOrCreateAvatar("?", "?", size);

            // 1) Spróbuj znaleźć userId po fullName
            if (_fullNameToUserIdCache.TryGetValue(fullName, out var uid))
                return GetOrCreateAvatar(uid, fullName, size);

            // 2) Spróbuj mapowanie handlowiec
            if (_handlowiecMapowanie != null &&
                _handlowiecMapowanie.TryGetValue(fullName, out var hUid))
                return GetOrCreateAvatar(hUid, fullName, size);

            // 3) Fallback — generuj domyślny z inicjałami
            return GetOrCreateAvatar(fullName, fullName, size);
        }

        private Image GetOrCreateAvatar(string userId, string displayName, int size)
        {
            var key = $"{userId}_{size}";
            if (_avatarCache.TryGetValue(key, out var cached))
                return cached;

            Image avatar = null;
            try
            {
                if (UserAvatarManager.HasAvatar(userId))
                    avatar = UserAvatarManager.GetAvatarRounded(userId, size);
            }
            catch { }

            if (avatar == null)
                avatar = UserAvatarManager.GenerateDefaultAvatar(displayName ?? userId, userId, size);

            _avatarCache[key] = avatar;
            return avatar;
        }

        /// <summary>
        /// Zwraca avatar handlowca - jeśli jest mapowanie name→userId, szuka prawdziwego zdjęcia
        /// </summary>
        private Image GetHandlowiecAvatar(string handlowiecName, int size)
        {
            // Sprawdź mapowanie name → userId (z tabeli UserHandlowcy)
            if (_handlowiecMapowanie != null &&
                _handlowiecMapowanie.TryGetValue(handlowiecName, out var uid))
                return GetOrCreateAvatar(uid, handlowiecName, size);

            return GetOrCreateAvatar(handlowiecName, handlowiecName, size);
        }

        /// <summary>
        /// Skraca nazwę do formatu "Imie L." np. "Artur Danielewski" → "Artur D."
        /// </summary>
        private static void EnableDoubleBuffering(DataGridView dgv)
        {
            var prop = typeof(DataGridView).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            prop?.SetValue(dgv, true);
        }

        private static string SkrocNazwe(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return fullName;
            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0]} {parts[1][0]}.";
            return fullName;
        }

        private string UtworzUrlGoogleMaps(List<string> adresy)
        {
            if (adresy.Count < 2) return "";

            var origin = Uri.EscapeDataString(adresy[0]);
            var destination = Uri.EscapeDataString(adresy[adresy.Count - 1]);

            var waypoints = "";
            if (adresy.Count > 2)
            {
                var waypointList = adresy.Skip(1).Take(adresy.Count - 2)
                    .Select(Uri.EscapeDataString);
                waypoints = "&waypoints=" + string.Join("|", waypointList);
            }

            return $"https://www.google.com/maps/dir/{origin}/{destination}?travelmode=driving{waypoints}";
        }

        #endregion

        #region Filtry i menu kontekstowe

        private async Task LoadFilterDataAsync()
        {
            try
            {
                // Załaduj kierowców
                _wszyscyKierowcy = await _repozytorium.PobierzKierowcowAsync(true);
                cboFiltrKierowca.Items.Clear();
                cboFiltrKierowca.Items.Add("Wszyscy");
                foreach (var k in _wszyscyKierowcy)
                    cboFiltrKierowca.Items.Add(k);
                cboFiltrKierowca.SelectedIndex = 0;

                // Załaduj pojazdy
                _wszystkiePojazdy = await _repozytorium.PobierzPojazdyAsync(true);
                cboFiltrPojazd.Items.Clear();
                cboFiltrPojazd.Items.Add("Wszystkie");
                foreach (var p in _wszystkiePojazdy)
                    cboFiltrPojazd.Items.Add(p);
                cboFiltrPojazd.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading filter data: {ex.Message}");
            }
        }

        private async Task LoadWolneZamowieniaAsync()
        {
            try
            {
                // Wolne = TransportStatus to 'Oczekuje' (lub NULL/pusty) i nie 'Własny'/'Przypisany'
                // Spójne z logiką w EdytorKursuWithPalety.LoadWolneZamowieniaForDate
                var sql = @"
                    SELECT
                        zm.Id,
                        zm.KlientId,
                        zm.DataPrzyjazdu,
                        zm.DataUboju,
                        ISNULL(zm.LiczbaPalet, 0) AS Palety,
                        ISNULL(zm.LiczbaPojemnikow, 0) AS Pojemniki
                    FROM dbo.ZamowieniaMieso zm
                    WHERE CAST(zm.DataUboju AS DATE) = @Data
                      AND ISNULL(zm.Status, 'Nowe') NOT IN ('Anulowane')
                      AND ISNULL(zm.TransportStatus, 'Oczekuje') NOT IN ('Przypisany', 'Wlasny')
                      AND zm.TransportKursID IS NULL
                    ORDER BY zm.DataPrzyjazdu";

                var tempList = new List<(int Id, int KlientId, DateTime DataOdbioru, DateTime DataUboju, decimal Palety, int Pojemniki, string KlientNazwa, string Adres, string Handlowiec)>();

                using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Data", _selectedDate.Date);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        tempList.Add((
                            reader.GetInt32(0),
                            reader.GetInt32(1),
                            reader.IsDBNull(2) ? _selectedDate : reader.GetDateTime(2),
                            reader.IsDBNull(3) ? _selectedDate : reader.GetDateTime(3),
                            reader.IsDBNull(4) ? 0m : reader.GetDecimal(4),
                            reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                            "", "", ""
                        ));
                    }
                }

                // Pobierz nazwy klientów z Handel (osobne połączenie - tak jak w edytorze)
                // W osobnym try-catch żeby błąd nazw nie blokował wyświetlenia zamówień
                if (tempList.Any())
                {
                    try
                    {
                        var klientIds = string.Join(",", tempList.Select(z => z.KlientId).Distinct());
                        // Identyczne zapytanie jak w transport-editor.cs LoadWolneZamowieniaForDate
                        var sqlKlienci = $@"
                            SELECT
                                c.Id,
                                ISNULL(c.Shortcut, 'KH ' + CAST(c.Id AS VARCHAR(10))) AS Nazwa,
                                ISNULL(wym.CDim_Handlowiec_Val, '') AS Handlowiec,
                                ISNULL(poa.Postcode, '') + ' ' + ISNULL(poa.Street, '') AS Adres
                            FROM SSCommon.STContractors c
                            LEFT JOIN SSCommon.ContractorClassification wym ON c.Id = wym.ElementId
                            LEFT JOIN SSCommon.STPostOfficeAddresses poa ON poa.ContactGuid = c.ContactGuid
                                AND poa.AddressName = N'adres domyślny'
                            WHERE c.Id IN ({klientIds})";

                        var klienciDict = new Dictionary<int, (string Nazwa, string Adres, string Handlowiec)>();
                        using (var cnHandel = new SqlConnection(_connHandel))
                        {
                            await cnHandel.OpenAsync();
                            using var cmdKlienci = new SqlCommand(sqlKlienci, cnHandel);
                            using var readerKlienci = await cmdKlienci.ExecuteReaderAsync();
                            while (await readerKlienci.ReadAsync())
                            {
                                var id = readerKlienci.GetInt32(0);
                                var nazwa = readerKlienci.GetString(1);
                                var handlowiec = readerKlienci.GetString(2);
                                var adres = readerKlienci.GetString(3).Trim();
                                klienciDict[id] = (nazwa, adres, handlowiec);
                            }
                        }

                        for (int i = 0; i < tempList.Count; i++)
                        {
                            var zam = tempList[i];
                            if (klienciDict.TryGetValue(zam.KlientId, out var klient))
                                tempList[i] = (zam.Id, zam.KlientId, zam.DataOdbioru, zam.DataUboju, zam.Palety, zam.Pojemniki, klient.Nazwa, klient.Adres, klient.Handlowiec);
                            else
                                tempList[i] = (zam.Id, zam.KlientId, zam.DataOdbioru, zam.DataUboju, zam.Palety, zam.Pojemniki, $"KH {zam.KlientId}", "", "");
                        }
                    }
                    catch (Exception exKlienci)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading klienci names: {exKlienci.Message}");
                        for (int i = 0; i < tempList.Count; i++)
                        {
                            var zam = tempList[i];
                            tempList[i] = (zam.Id, zam.KlientId, zam.DataOdbioru, zam.DataUboju, zam.Palety, zam.Pojemniki, $"KH {zam.KlientId}", "", "");
                        }
                    }
                }

                // Budowanie tabeli - kolumny: Ubój, Odbiór, Godz., Palety, Poj., Klient
                var plCulture = new System.Globalization.CultureInfo("pl-PL");
                var dt = new DataTable();
                dt.Columns.Add("ID", typeof(int));
                dt.Columns.Add("IsGroupRow", typeof(bool));
                dt.Columns.Add("Ubój", typeof(string));
                dt.Columns.Add("Odbiór", typeof(string));
                dt.Columns.Add("Godz.", typeof(string));
                dt.Columns.Add("Palety", typeof(string));
                dt.Columns.Add("Poj.", typeof(int));
                dt.Columns.Add("Klient", typeof(string));
                dt.Columns.Add("Handl.", typeof(string));
                dt.Columns.Add("Adres", typeof(string));

                // Grupuj po dacie odbioru
                var grouped = tempList
                    .OrderBy(z => z.DataOdbioru.Date)
                    .ThenBy(z => z.DataOdbioru)
                    .GroupBy(z => z.DataOdbioru.Date);

                foreach (var group in grouped)
                {
                    // Wiersz nagłówka grupy
                    var dzienSkrot = group.Key.ToString("ddd", plCulture);
                    var groupHeader = $"▸ {group.Key:dd.MM} {dzienSkrot} — {group.Count()} zam.";
                    var groupRow = dt.NewRow();
                    groupRow["ID"] = 0;
                    groupRow["IsGroupRow"] = true;
                    groupRow["Ubój"] = "";
                    groupRow["Odbiór"] = groupHeader;
                    groupRow["Godz."] = "";
                    groupRow["Palety"] = "";
                    groupRow["Poj."] = 0;
                    groupRow["Klient"] = "";
                    groupRow["Handl."] = "";
                    groupRow["Adres"] = "";
                    dt.Rows.Add(groupRow);

                    foreach (var zam in group)
                    {
                        var klientNazwa = zam.KlientNazwa;
                        var ubojDzien = zam.DataUboju.ToString("dd.MM", plCulture)
                            + " " + zam.DataUboju.ToString("ddd", plCulture);
                        var odbiorDzien = zam.DataOdbioru.ToString("dd.MM", plCulture)
                            + " " + zam.DataOdbioru.ToString("ddd", plCulture);
                        dt.Rows.Add(
                            zam.Id,
                            false,
                            ubojDzien,
                            odbiorDzien,
                            zam.DataOdbioru.ToString("HH:mm"),
                            zam.Palety.ToString("N1"),
                            zam.Pojemniki,
                            klientNazwa,
                            zam.Handlowiec,
                            zam.Adres
                        );
                    }
                }

                dgvWolneZamowienia.SuspendLayout();
                dgvWolneZamowienia.DataSource = dt;

                // Konfiguracja kolumn
                dgvWolneZamowienia.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

                if (dgvWolneZamowienia.Columns["ID"] != null)
                    dgvWolneZamowienia.Columns["ID"].Visible = false;
                if (dgvWolneZamowienia.Columns["IsGroupRow"] != null)
                    dgvWolneZamowienia.Columns["IsGroupRow"].Visible = false;
                if (dgvWolneZamowienia.Columns["Ubój"] != null)
                    dgvWolneZamowienia.Columns["Ubój"].Width = 70;
                if (dgvWolneZamowienia.Columns["Odbiór"] != null)
                    dgvWolneZamowienia.Columns["Odbiór"].Width = 70;
                if (dgvWolneZamowienia.Columns["Godz."] != null)
                    dgvWolneZamowienia.Columns["Godz."].Width = 46;
                if (dgvWolneZamowienia.Columns["Palety"] != null)
                    dgvWolneZamowienia.Columns["Palety"].Width = 48;
                if (dgvWolneZamowienia.Columns["Poj."] != null)
                    dgvWolneZamowienia.Columns["Poj."].Width = 42;
                if (dgvWolneZamowienia.Columns["Klient"] != null)
                    dgvWolneZamowienia.Columns["Klient"].Width = 140;
                if (dgvWolneZamowienia.Columns["Handl."] != null)
                    dgvWolneZamowienia.Columns["Handl."].Width = 90;
                if (dgvWolneZamowienia.Columns["Adres"] != null)
                    dgvWolneZamowienia.Columns["Adres"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                // Formatuj wiersze grupujące
                var groupStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(235, 230, 245),
                    ForeColor = Color.FromArgb(100, 60, 140),
                    Font = _fontGroupBold
                };
                foreach (DataGridViewRow row in dgvWolneZamowienia.Rows)
                {
                    if (row.Cells["IsGroupRow"]?.Value != null && Convert.ToBoolean(row.Cells["IsGroupRow"].Value))
                    {
                        row.DefaultCellStyle = groupStyle;
                        row.Height = 30;
                    }
                }

                dgvWolneZamowienia.ResumeLayout(true);

                // Aktualizuj licznik
                lblWolneZamowieniaInfo.Text = tempList.Count.ToString();
                lblWolneZamowieniaInfo.ForeColor = tempList.Count == 0
                    ? Color.FromArgb(150, 255, 150)
                    : tempList.Count > 10
                        ? Color.FromArgb(255, 180, 120)
                        : Color.FromArgb(220, 220, 255);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading wolne zamowienia: {ex.Message}");
                lblWolneZamowieniaInfo.Text = "!";
                lblWolneZamowieniaInfo.ForeColor = Color.Red;
            }
        }

        private async Task ZmienNaWlasnyOdbiorAsync()
        {
            if (dgvWolneZamowienia.CurrentRow == null) return;

            var row = dgvWolneZamowienia.CurrentRow;
            if (row.Cells["IsGroupRow"]?.Value != null && Convert.ToBoolean(row.Cells["IsGroupRow"].Value))
                return;

            var zamId = Convert.ToInt32(row.Cells["ID"].Value);
            var klientNazwa = row.Cells["Klient"]?.Value?.ToString() ?? "";

            var result = MessageBox.Show(
                $"Czy na pewno ustawić zamówienie #{zamId}\n{klientNazwa}\njako własny odbiór?",
                "Własny odbiór",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            try
            {
                using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                var sql = "UPDATE dbo.ZamowieniaMieso SET TransportStatus = 'Wlasny' WHERE Id = @Id";
                using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Id", zamId);
                await cmd.ExecuteNonQueryAsync();

                // Log to TransportZmiany
                var user = App.UserFullName ?? App.UserID ?? "system";
                await TransportZmianyService.LogChangeAsync(zamId, zamId.ToString(), klientNazwa,
                    "ZmianaStatusu", $"Zamowienie oznaczone jako wlasny odbior",
                    "Oczekuje", "Wlasny", user);

                await LoadWolneZamowieniaAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadMissingKlienciAsync(HashSet<int> klientIds)
        {
            try
            {
                using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                // Pobierz wszystkich klientów na raz (identycznie jak w transport-editor.cs)
                var sql = @"SELECT c.Id, ISNULL(c.Shortcut, 'KH ' + CAST(c.Id AS VARCHAR(10))),
                                   ISNULL(poa.Postcode, '') + ' ' + ISNULL(poa.Street, '')
                            FROM SSCommon.STContractors c
                            LEFT JOIN SSCommon.STPostOfficeAddresses poa
                                ON poa.ContactGuid = c.ContactGuid AND poa.AddressName = N'adres domyślny'";

                using var cmd = new SqlCommand(sql, cn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(0);
                    var nazwa = reader.GetString(1);
                    var miasto = reader.GetString(2);
                    _klienciCache[id] = nazwa;
                    if (!string.IsNullOrWhiteSpace(miasto))
                        _klienciAdresCache[id] = miasto.Trim();
                }

                _klienciCacheTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading klienci cache: {ex.Message}");
            }
        }

        private async void FiltrChanged(object sender, EventArgs e)
        {
            await LoadKursyAsync();
        }

        private async void BtnWyczyscFiltry_Click(object sender, EventArgs e)
        {
            cboFiltrKierowca.SelectedIndex = 0;
            cboFiltrPojazd.SelectedIndex = 0;
            cboFiltrStatus.SelectedIndex = 0;
            await LoadKursyAsync();
        }

        private async void MenuPodgladLadunkow_Click(object sender, EventArgs e)
        {
            if (dgvKursy.CurrentRow == null) return;

            try
            {
                var kursId = Convert.ToInt64(dgvKursy.CurrentRow.Cells["KursID"].Value);
                var kurs = _kursy.FirstOrDefault(k => k.KursID == kursId);
                if (kurs == null) return;

                var ladunki = await _repozytorium.PobierzLadunkiAsync(kursId);

                using var dlg = new PodgladLadunkowDialog(kurs, ladunki);
                dlg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas pobierania ładunków: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void MenuHistoriaZmian_Click(object sender, EventArgs e)
        {
            if (dgvKursy.CurrentRow == null) return;

            try
            {
                var kursId = Convert.ToInt64(dgvKursy.CurrentRow.Cells["KursID"].Value);
                var kurs = _kursy.FirstOrDefault(k => k.KursID == kursId);
                if (kurs == null) return;

                var zamIds = _kursZamIds.ContainsKey(kursId) ? _kursZamIds[kursId] : new List<int>();
                var zmiany = await TransportZmianyService.GetByZamowienieIdsAsync(zamIds);

                using var dlg = new HistoriaZmianKursuDialog(kurs, zmiany);
                dlg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ═══ Feature 25: Skróty klawiszowe ═══
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F5:
                    _ = LoadKursyAsync();
                    return true;
                case Keys.Control | Keys.N:
                    BtnNowyKurs_Click(this, EventArgs.Empty);
                    return true;
                case Keys.Control | Keys.E:
                    BtnEdytujKurs_Click(this, EventArgs.Empty);
                    return true;
                case Keys.Delete:
                    if (dgvKursy.Focused || ActiveControl == dgvKursy)
                    {
                        BtnUsunKurs_Click(this, EventArgs.Empty);
                        return true;
                    }
                    break;
                case Keys.Left:
                    if (!(ActiveControl is TextBox || ActiveControl is ComboBox || ActiveControl is DateTimePicker))
                    {
                        _selectedDate = _selectedDate.AddDays(-1);
                        dtpData.Value = _selectedDate;
                        return true;
                    }
                    break;
                case Keys.Right:
                    if (!(ActiveControl is TextBox || ActiveControl is ComboBox || ActiveControl is DateTimePicker))
                    {
                        _selectedDate = _selectedDate.AddDays(1);
                        dtpData.Value = _selectedDate;
                        return true;
                    }
                    break;
                case Keys.T:
                    if (!(ActiveControl is TextBox || ActiveControl is ComboBox))
                    {
                        _selectedDate = DateTime.Today;
                        dtpData.Value = _selectedDate;
                        return true;
                    }
                    break;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ═══ Feature 13: Tooltip zmian przy hoverze na Alert ═══
        private int _lastTooltipRowIndex = -1;
        private void DgvKursy_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var colName = dgvKursy.Columns[e.ColumnIndex].Name;
            if (colName != "Alert") { _lastTooltipRowIndex = -1; return; }
            if (e.RowIndex == _lastTooltipRowIndex) return;
            _lastTooltipRowIndex = e.RowIndex;

            var alertVal = dgvKursy.Rows[e.RowIndex].Cells["Alert"]?.Value?.ToString() ?? "";
            if (string.IsNullOrEmpty(alertVal))
            {
                dgvKursy.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = "";
                return;
            }

            try
            {
                var kursId = Convert.ToInt64(dgvKursy.Rows[e.RowIndex].Cells["KursID"].Value);
                var zamIds = _kursZamIds.ContainsKey(kursId) ? _kursZamIds[kursId] : new List<int>();
                if (zamIds.Count == 0)
                {
                    dgvKursy.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = "Oczekujace zmiany";
                    return;
                }

                // Build tooltip from cached pending changes info
                _ = BuildAlertTooltipAsync(e.RowIndex, e.ColumnIndex, kursId, zamIds);
            }
            catch { }
        }

        private async Task BuildAlertTooltipAsync(int rowIndex, int colIndex, long kursId, List<int> zamIds)
        {
            try
            {
                var connStr = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                var zamIdsStr = string.Join(",", zamIds);
                using var cmd = new SqlCommand($@"
                    SELECT TOP 8 TypZmiany, KlientNazwa, ZgloszonePrzez, DataZgloszenia, StareWartosc, NowaWartosc
                    FROM TransportZmiany
                    WHERE StatusZmiany = 'Oczekuje' AND TypZmiany NOT IN ('NoweZamowienie', 'ZmianaStatusu')
                      AND ZamowienieId IN ({zamIdsStr})
                    ORDER BY DataZgloszenia DESC", conn);
                using var rdr = await cmd.ExecuteReaderAsync();
                var sb = new System.Text.StringBuilder();
                int count = 0;
                while (await rdr.ReadAsync())
                {
                    var typ = rdr[0]?.ToString() ?? "";
                    var klient = rdr.IsDBNull(1) ? "" : rdr[1].ToString();
                    var kto = rdr.IsDBNull(2) ? "" : rdr[2].ToString();
                    var kiedy = Convert.ToDateTime(rdr[3]);
                    var stare = rdr.IsDBNull(4) ? "" : rdr[4].ToString();
                    var nowe = rdr.IsDBNull(5) ? "" : rdr[5].ToString();
                    var meta = GetZmianaMeta(typ);
                    sb.AppendLine($"{meta.Icon} {meta.Label}: {stare} -> {nowe}");
                    sb.AppendLine($"   {klient} | {kto} | {kiedy:dd.MM HH:mm}");
                    count++;
                }
                if (count == 0) sb.Append("Oczekujace zmiany");

                if (rowIndex < dgvKursy.Rows.Count && colIndex < dgvKursy.Columns.Count)
                    dgvKursy.Rows[rowIndex].Cells[colIndex].ToolTipText = sb.ToString().TrimEnd();
            }
            catch { }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════
        // Webfleet — wysyłanie kursów na nawigacje TomTom
        // ══════════════════════════════════════════════════════════════════

        private void BtnWebfleetSync_Click(object? sender, EventArgs e)
        {
            var win = new Kalendarz1.MapaFloty.SyncKursowWindow();
            win.Show();
        }

        private async void MenuWebfleetWyslij_Click(object? sender, EventArgs e)
        {
            if (dgvKursy.CurrentRow == null) { MessageBox.Show("Zaznacz kurs do wysłania.", "Webfleet"); return; }
            try
            {
                var kursId = Convert.ToInt64(dgvKursy.CurrentRow.Cells["KursID"].Value);
                var trasa = dgvKursy.CurrentRow.Cells["Trasa"]?.Value?.ToString() ?? "";

                var confirm = MessageBox.Show(
                    $"Wysłać kurs do nawigacji Webfleet?\n\nKurs ID: {kursId}\nTrasa: {trasa}\n\nKierowca dostanie zlecenie na nawigację TomTom.",
                    "Wyślij do Webfleet", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes) return;

                var svc = new Kalendarz1.MapaFloty.WebfleetOrderService();
                var result = await svc.WyslijKursAsync(kursId, App.UserFullName ?? "system");

                if (result.Success)
                {
                    MessageBox.Show($"Kurs wysłany do Webfleet!\n\nOrder ID: {result.OrderId}\nPrzystanki: {result.StopsCount}\n\nZlecenie widoczne w panelu Webfleet.",
                        "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    var errDetail = $"Błąd wysyłania kursu {kursId} do Webfleet\n\n" +
                        $"Kod błędu: {result.ErrorCode}\n" +
                        $"Komunikat: {result.ErrorMessage}\n" +
                        $"Order ID: {result.OrderId}\n" +
                        $"Trasa: {trasa}\n" +
                        $"Czas: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                    var resp2 = MessageBox.Show(errDetail + "\n\nSkopiować szczegóły do schowka?",
                        "Błąd Webfleet", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (resp2 == DialogResult.Yes)
                        System.Windows.Forms.Clipboard.SetText(errDetail);
                }
            }
            catch (Kalendarz1.MapaFloty.WebfleetOrderService.NeedAddressException nae)
            {
                var resp = MessageBox.Show(
                    "Brak adresów klientów:\n\n" +
                    string.Join("\n", nae.BrakujaceKody.Select(k => $"  • {k}")) +
                    "\n\nCzy chcesz teraz uzupełnić adresy?",
                    "Brak adresów", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (resp == DialogResult.Yes)
                {
                    var dlg = new Kalendarz1.MapaFloty.AdresyKlientowWindow(nae.BrakujaceKody);
                    dlg.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd:\n{ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    /// <summary>
    /// Feature 34: Dialog historii zmian kursu — timeline z wszystkimi zmianami
    /// </summary>
    public class HistoriaZmianKursuDialog : Form
    {
        private readonly Kurs _kurs;
        private readonly List<TransportZmiana> _zmiany;

        public HistoriaZmianKursuDialog(Kurs kurs, List<TransportZmiana> zmiany)
        {
            _kurs = kurs;
            _zmiany = zmiany;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = $"Historia zmian kursu #{_kurs.KursID}";
            Size = new Size(620, 600);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(500, 400);
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(240, 242, 247);
            WindowIconHelper.SetIcon(this);

            // ═══ HEADER ═══
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(41, 44, 51),
                Padding = new Padding(18, 12, 18, 12)
            };
            var lblTitle = new Label
            {
                Text = $"HISTORIA ZMIAN  —  KURS #{_kurs.KursID}",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(18, 12)
            };
            var lblSubtitle = new Label
            {
                Text = $"{_kurs.DataKursu:dd.MM.yyyy}  |  {_kurs.Trasa ?? "Brak trasy"}  |  {_kurs.KierowcaNazwa ?? "Brak kierowcy"}",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(180, 190, 200),
                AutoSize = true,
                Location = new Point(18, 42)
            };
            header.Controls.AddRange(new Control[] { lblTitle, lblSubtitle });

            // ═══ STATS BAR ═══
            var statsBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = Color.White,
                Padding = new Padding(18, 8, 18, 8)
            };
            var totalCount = _zmiany.Count;
            var pendingCount = _zmiany.Count(z => z.StatusZmiany == "Oczekuje");
            var acceptedCount = _zmiany.Count(z => z.StatusZmiany == "Zaakceptowano");
            var rejectedCount = _zmiany.Count(z => z.StatusZmiany == "Odrzucono");
            var lblStats = new Label
            {
                Text = $"Razem: {totalCount}    |    Oczekuje: {pendingCount}    |    Zaakceptowane: {acceptedCount}    |    Odrzucone: {rejectedCount}",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(80, 90, 110),
                AutoSize = true,
                Location = new Point(18, 10)
            };
            statsBar.Controls.Add(lblStats);

            // ═══ TIMELINE SCROLL ═══
            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(18, 10, 18, 10),
                BackColor = Color.FromArgb(245, 247, 250)
            };

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };

            // Group by date
            var grouped = _zmiany
                .OrderByDescending(z => z.DataZgloszenia)
                .GroupBy(z => z.DataZgloszenia.Date);

            foreach (var dayGroup in grouped)
            {
                // Date header
                var lblDate = new Label
                {
                    Text = dayGroup.Key.ToString("dd MMMM yyyy (dddd)", new System.Globalization.CultureInfo("pl-PL")),
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(100, 110, 130),
                    AutoSize = true,
                    Margin = new Padding(0, 8, 0, 4),
                    Padding = new Padding(24, 0, 0, 0)
                };
                flow.Controls.Add(lblDate);

                foreach (var zmiana in dayGroup.OrderByDescending(z => z.DataZgloszenia))
                {
                    var card = CreateTimelineCard(zmiana, scrollPanel.Width - 60);
                    flow.Controls.Add(card);
                }
            }

            if (_zmiany.Count == 0)
            {
                // Kurs creation info
                var lblEmpty = new Label
                {
                    Text = $"Brak zmian w zamowieniach tego kursu.\n\nKurs utworzony: {_kurs.UtworzonoUTC.ToLocalTime():dd.MM.yyyy HH:mm}\nPrzez: {_kurs.Utworzyl ?? "Nieznany"}",
                    Font = new Font("Segoe UI", 10F),
                    ForeColor = Color.FromArgb(120, 130, 150),
                    AutoSize = true,
                    Margin = new Padding(24, 30, 0, 0)
                };
                flow.Controls.Add(lblEmpty);
            }

            scrollPanel.Controls.Add(flow);

            // ═══ FOOTER ═══
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                BackColor = Color.FromArgb(248, 249, 252),
                Padding = new Padding(18, 8, 18, 8)
            };
            var btnClose = new Button
            {
                Text = "Zamknij",
                Size = new Size(100, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => Close();
            btnClose.Location = new Point(footer.Width - 120, 8);
            footer.Resize += (s, e) => btnClose.Location = new Point(footer.Width - 120, 8);
            footer.Controls.Add(btnClose);

            Controls.Add(scrollPanel);
            Controls.Add(statsBar);
            Controls.Add(header);
            Controls.Add(footer);
        }

        private Panel CreateTimelineCard(TransportZmiana z, int maxWidth)
        {
            var cardWidth = Math.Max(maxWidth, 400);
            var card = new Panel
            {
                Width = cardWidth,
                Height = 72,
                BackColor = Color.White,
                Margin = new Padding(0, 2, 0, 2),
                Padding = new Padding(0)
            };

            // Left timeline dot + line
            card.Paint += (s, ev) =>
            {
                var g = ev.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Timeline line
                using var linePen = new Pen(Color.FromArgb(210, 215, 225), 2);
                g.DrawLine(linePen, 14, 0, 14, card.Height);

                // Dot
                var dotColor = z.StatusZmiany switch
                {
                    "Oczekuje" => z.TypColor,
                    "Zaakceptowano" => Color.FromArgb(39, 174, 96),
                    "Odrzucono" => Color.FromArgb(192, 57, 43),
                    _ => Color.FromArgb(150, 150, 150)
                };
                using var dotBrush = new SolidBrush(dotColor);
                g.FillEllipse(dotBrush, 6, 26, 16, 16);

                // Bottom border
                using var borderPen = new Pen(Color.FromArgb(235, 238, 242));
                g.DrawLine(borderPen, 30, card.Height - 1, card.Width, card.Height - 1);
            };

            // Type icon + label
            var lblType = new Label
            {
                Text = $"{z.TypIcon}  {z.TypLabel}",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = z.TypColor,
                AutoSize = true,
                Location = new Point(32, 8)
            };
            card.Controls.Add(lblType);

            // Status badge
            var statusColor = z.StatusZmiany switch
            {
                "Oczekuje" => Color.FromArgb(243, 156, 18),
                "Zaakceptowano" => Color.FromArgb(39, 174, 96),
                "Odrzucono" => Color.FromArgb(192, 57, 43),
                _ => Color.Gray
            };
            var lblStatus = new Label
            {
                Text = z.StatusZmiany.ToUpper(),
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = statusColor,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                Size = new Size(90, 18),
                Location = new Point(cardWidth - 105, 8)
            };
            card.Controls.Add(lblStatus);

            // Change details
            var detail = "";
            if (!string.IsNullOrEmpty(z.StareWartosc) || !string.IsNullOrEmpty(z.NowaWartosc))
                detail = $"{z.StareWartosc ?? "-"}  ->  {z.NowaWartosc ?? "-"}";
            else if (!string.IsNullOrEmpty(z.Opis))
                detail = z.Opis;

            if (!string.IsNullOrEmpty(detail))
            {
                var lblDetail = new Label
                {
                    Text = detail,
                    Font = new Font("Segoe UI", 8.5F),
                    ForeColor = Color.FromArgb(60, 70, 85),
                    AutoSize = true,
                    MaximumSize = new Size(cardWidth - 140, 0),
                    Location = new Point(32, 30)
                };
                card.Controls.Add(lblDetail);
            }

            // Footer: klient, kto, kiedy
            var klientInfo = !string.IsNullOrEmpty(z.KlientNazwa) ? z.KlientNazwa : $"Zam. #{z.ZamowienieId}";
            var footerText = $"{klientInfo}  |  {z.ZgloszonePrzez}  |  {z.DataZgloszenia:HH:mm}";
            if (z.StatusZmiany != "Oczekuje" && z.ZaakceptowanePrzez != null)
                footerText += $"  |  {z.StatusZmiany} przez {z.ZaakceptowanePrzez}";

            var lblFooter = new Label
            {
                Text = footerText,
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = Color.FromArgb(140, 150, 165),
                AutoSize = true,
                MaximumSize = new Size(cardWidth - 50, 0),
                Location = new Point(32, 52)
            };
            card.Controls.Add(lblFooter);

            return card;
        }
    }

    /// <summary>
    /// Dialog do szybkiego przypisania kierowcy i pojazdu do kursu
    /// </summary>
    public class SzybkiePrzypisanieDialog : Form
    {
        private readonly TransportRepozytorium _repozytorium;
        private readonly Kurs _kurs;
        private ComboBox cboKierowca;
        private ComboBox cboPojazd;
        private Label lblInfo;
        private Button btnZapisz;
        private Button btnAnuluj;

        public SzybkiePrzypisanieDialog(TransportRepozytorium repozytorium, Kurs kurs)
        {
            _repozytorium = repozytorium;
            _kurs = kurs;
            InitializeComponent();
            _ = LoadDataAsync();
        }

        private void InitializeComponent()
        {
            Text = "Szybkie przydzielenie zasobów";
            Size = new Size(450, 320);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(240, 242, 247);

            // Informacja o kursie
            lblInfo = new Label
            {
                Location = new Point(20, 20),
                Size = new Size(400, 60),
                Text = $"Przydziel kierowcę i pojazd do kursu:\n" +
                       $"Data: {_kurs.DataKursu:dd.MM.yyyy}\n" +
                       $"Trasa: {_kurs.Trasa ?? "Brak trasy"}",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(52, 73, 94)
            };

            // Kierowca
            var lblKierowca = new Label
            {
                Location = new Point(20, 95),
                Size = new Size(80, 25),
                Text = "Kierowca:",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                TextAlign = ContentAlignment.MiddleRight
            };

            cboKierowca = new ComboBox
            {
                Location = new Point(110, 95),
                Size = new Size(300, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F),
                DisplayMember = "PelneNazwisko"
            };

            // Pojazd
            var lblPojazd = new Label
            {
                Location = new Point(20, 140),
                Size = new Size(80, 25),
                Text = "Pojazd:",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                TextAlign = ContentAlignment.MiddleRight
            };

            cboPojazd = new ComboBox
            {
                Location = new Point(110, 140),
                Size = new Size(300, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F),
                DisplayMember = "Opis"
            };

            // Ostrzeżenie
            var lblOstrzezenie = new Label
            {
                Location = new Point(20, 185),
                Size = new Size(400, 40),
                Text = "⚠ Wybierz kierowcę i pojazd, aby zakończyć przydzielanie.",
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.FromArgb(230, 126, 34)
            };

            // Przyciski
            btnZapisz = new Button
            {
                Location = new Point(220, 235),
                Size = new Size(100, 35),
                Text = "Przydziel",
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnZapisz.FlatAppearance.BorderSize = 0;
            btnZapisz.Click += BtnZapisz_Click;

            btnAnuluj = new Button
            {
                Location = new Point(330, 235),
                Size = new Size(80, 35),
                Text = "Anuluj",
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnAnuluj.FlatAppearance.BorderSize = 0;

            Controls.AddRange(new Control[] {
                lblInfo, lblKierowca, cboKierowca, lblPojazd, cboPojazd,
                lblOstrzezenie, btnZapisz, btnAnuluj
            });

            AcceptButton = btnZapisz;
            CancelButton = btnAnuluj;
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var kierowcy = await _repozytorium.PobierzKierowcowAsync(true);
                var pojazdy = await _repozytorium.PobierzPojazdyAsync(true);

                cboKierowca.DataSource = kierowcy;
                cboPojazd.DataSource = pojazdy;

                // Jeśli kurs ma już przypisanego kierowcę lub pojazd, ustaw go
                if (_kurs.KierowcaID.HasValue)
                {
                    var kierowca = kierowcy.FirstOrDefault(k => k.KierowcaID == _kurs.KierowcaID.Value);
                    if (kierowca != null) cboKierowca.SelectedItem = kierowca;
                }

                if (_kurs.PojazdID.HasValue)
                {
                    var pojazd = pojazdy.FirstOrDefault(p => p.PojazdID == _kurs.PojazdID.Value);
                    if (pojazd != null) cboPojazd.SelectedItem = pojazd;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnZapisz_Click(object sender, EventArgs e)
        {
            var kierowca = cboKierowca.SelectedItem as Kierowca;
            var pojazd = cboPojazd.SelectedItem as Pojazd;

            if (kierowca == null || pojazd == null)
            {
                MessageBox.Show("Wybierz kierowcę i pojazd, aby przydzielić zasoby.",
                    "Brak wyboru", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;

                // Zaktualizuj kurs z nowymi wartościami
                _kurs.KierowcaID = kierowca.KierowcaID;
                _kurs.PojazdID = pojazd.PojazdID;
                _kurs.Status = "Planowany"; // Zachowaj status "Planowany"

                await _repozytorium.AktualizujNaglowekKursuAsync(_kurs, Environment.UserName);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
    }

    /// <summary>
    /// Dialog do szybkiego podglądu ładunków bez otwierania edytora
    /// </summary>
    public class PodgladLadunkowDialog : Form
    {
        private readonly Kurs _kurs;
        private readonly List<Ladunek> _ladunki;
        private DataGridView dgvLadunki;

        public PodgladLadunkowDialog(Kurs kurs, List<Ladunek> ladunki)
        {
            _kurs = kurs;
            _ladunki = ladunki;
            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            Text = $"📦 Podgląd ładunków - Kurs #{_kurs.KursID}";
            Size = new Size(700, 450);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(240, 242, 247);

            // Panel nagłówka
            var panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(41, 44, 51),
                Padding = new Padding(15)
            };

            var lblTytul = new Label
            {
                Text = $"🚚 {_kurs.KierowcaNazwa ?? "Brak kierowcy"} | {_kurs.PojazdRejestracja ?? "Brak pojazdu"}",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(15, 10)
            };

            var lblTrasa = new Label
            {
                Text = $"📍 {_kurs.Trasa ?? "Brak trasy"}",
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(173, 181, 189),
                AutoSize = true,
                Location = new Point(15, 45)
            };

            panelHeader.Controls.AddRange(new Control[] { lblTytul, lblTrasa });

            // Grid ładunków
            dgvLadunki = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };

            dgvLadunki.EnableHeadersVisualStyles = false;
            dgvLadunki.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 252);
            dgvLadunki.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(52, 73, 94);
            dgvLadunki.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dgvLadunki.ColumnHeadersHeight = 40;
            dgvLadunki.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            dgvLadunki.RowTemplate.Height = 35;
            dgvLadunki.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 252);

            // Panel podsumowania
            var panelFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(248, 249, 252),
                Padding = new Padding(15, 10, 15, 10)
            };

            var sumaE2 = _ladunki.Sum(l => l.PojemnikiE2);
            var lblSuma = new Label
            {
                Text = $"📊 Razem: {_ladunki.Count} pozycji | {sumaE2} pojemników E2",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize = true,
                Location = new Point(15, 15)
            };

            var btnZamknij = new Button
            {
                Text = "Zamknij",
                Size = new Size(100, 35),
                Location = new Point(550, 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            btnZamknij.FlatAppearance.BorderSize = 0;
            btnZamknij.Click += (s, e) => Close();

            panelFooter.Controls.AddRange(new Control[] { lblSuma, btnZamknij });

            Controls.Add(dgvLadunki);
            Controls.Add(panelHeader);
            Controls.Add(panelFooter);
        }

        private void LoadData()
        {
            var dt = new System.Data.DataTable();
            dt.Columns.Add("Lp", typeof(int));
            dt.Columns.Add("Klient", typeof(string));
            dt.Columns.Add("Pojemniki E2", typeof(int));
            dt.Columns.Add("Palety", typeof(string));
            dt.Columns.Add("Uwagi", typeof(string));

            int lp = 1;
            foreach (var ladunek in _ladunki.OrderBy(l => l.Kolejnosc))
            {
                var paletyTekst = ladunek.PaletyH1.HasValue ? ladunek.PaletyH1.Value.ToString() : "-";
                dt.Rows.Add(
                    lp++,
                    ladunek.KodKlienta ?? "-",
                    ladunek.PojemnikiE2,
                    paletyTekst,
                    ladunek.Uwagi ?? ""
                );
            }

            dgvLadunki.DataSource = dt;

            try
            {
                if (dgvLadunki.Columns["Lp"] != null)
                    dgvLadunki.Columns["Lp"].FillWeight = 30;
                if (dgvLadunki.Columns["Klient"] != null)
                    dgvLadunki.Columns["Klient"].FillWeight = 120;
                if (dgvLadunki.Columns["Pojemniki E2"] != null)
                    dgvLadunki.Columns["Pojemniki E2"].FillWeight = 60;
                if (dgvLadunki.Columns["Palety"] != null)
                    dgvLadunki.Columns["Palety"].FillWeight = 50;
                if (dgvLadunki.Columns["Uwagi"] != null)
                    dgvLadunki.Columns["Uwagi"].FillWeight = 100;
            }
            catch { }
        }
    }
}