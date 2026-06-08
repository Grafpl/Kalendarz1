using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Transport
{
    public partial class TransportRaportForm : Form
    {
        private readonly string _connectionString;
        private DataGridView dgvKursy;
        private DateTimePicker dtpData;
        private Button btnDrukuj;
        private Button btnPodglad;
        private List<KursRaport> _kursy = new List<KursRaport>();

        public TransportRaportForm(string connectionString)
        {
            _connectionString = connectionString;
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            SetupUI();

            // Ustaw dzisiejszą datę i załaduj kursy
            dtpData.Value = DateTime.Today;
            _ = ZaladujKursyAsync();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1000, 600);
            Font = new Font("Segoe UI", 9F);
            MinimumSize = new Size(800, 500);
            Name = "TransportRaportForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Raport Transportowy - Ubojnia Drobiu Piórkowscy";

            ResumeLayout(false);
        }

        private void SetupUI()
        {
            BackColor = Color.White;

            // Panel górny
            var panelTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(240, 240, 240),
                Padding = new Padding(20, 15, 20, 15)
            };

            var lblData = new Label
            {
                Text = "Data:",
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Location = new Point(20, 18)
            };

            dtpData = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Font = new Font("Segoe UI", 10F),
                Location = new Point(70, 15),
                Width = 120
            };
            dtpData.ValueChanged += async (s, e) => await ZaladujKursyAsync();

            btnPodglad = new Button
            {
                Text = "Podgląd wydruku",
                Font = new Font("Segoe UI", 9F),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(120, 30),
                Location = new Point(220, 15)
            };
            btnPodglad.FlatAppearance.BorderSize = 0;
            btnPodglad.Click += BtnPodglad_Click;

            btnDrukuj = new Button
            {
                Text = "Drukuj",
                Font = new Font("Segoe UI", 9F),
                BackColor = Color.FromArgb(16, 137, 62),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 30),
                Location = new Point(350, 15)
            };
            btnDrukuj.FlatAppearance.BorderSize = 0;
            btnDrukuj.Click += BtnDrukuj_Click;

            panelTop.Controls.AddRange(new Control[] { lblData, dtpData, btnPodglad, btnDrukuj });

            // DataGridView
            dgvKursy = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9F),
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(0, 120, 215),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    SelectionBackColor = Color.FromArgb(0, 120, 215)
                },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    SelectionBackColor = Color.FromArgb(200, 230, 255),
                    SelectionForeColor = Color.Black
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(248, 248, 248)
                },
                RowTemplate = { Height = 28 }
            };

            // Panel statusu
            var panelStatus = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                BackColor = Color.FromArgb(240, 240, 240)
            };

            var lblStatus = new Label
            {
                Text = "Gotowy",
                AutoSize = true,
                Location = new Point(20, 8),
                Font = new Font("Segoe UI", 8.25F)
            };

            panelStatus.Controls.Add(lblStatus);

            Controls.AddRange(new Control[] { dgvKursy, panelTop, panelStatus });
        }

        private async Task ZaladujKursyAsync()
        {
            try
            {
                _kursy = await PobierzKursyAsync(dtpData.Value);
                WyswietlKursy();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania kursów: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void WyswietlKursy()
        {
            var dt = new DataTable();
            dt.Columns.Add("Kierowca", typeof(string));
            dt.Columns.Add("Pojazd", typeof(string));
            dt.Columns.Add("Trasa", typeof(string));
            dt.Columns.Add("Wyjazd", typeof(string));
            dt.Columns.Add("Powrót", typeof(string));
            dt.Columns.Add("Palety", typeof(string));
            dt.Columns.Add("Pojemniki", typeof(int));
            dt.Columns.Add("Ładunki", typeof(int));
            dt.Columns.Add("Status", typeof(string));

            foreach (var kurs in _kursy)
            {
                dt.Rows.Add(
                    kurs.KierowcaNazwa,
                    kurs.PojazdRejestracja,
                    kurs.Trasa ?? "-",
                    kurs.GodzWyjazdu?.ToString(@"hh\:mm") ?? "-",
                    kurs.GodzPowrotu?.ToString(@"hh\:mm") ?? "-",
                    $"{kurs.PaletyUzyteNominal}/{kurs.PaletyPojazdu}",
                    kurs.SumaPojemnikiE2,
                    kurs.Ladunki.Count,
                    kurs.Status
                );
            }

            dgvKursy.DataSource = dt;

            // Formatowanie kolumn
            if (dgvKursy.Columns["Kierowca"] != null) dgvKursy.Columns["Kierowca"].FillWeight = 120;
            if (dgvKursy.Columns["Pojazd"] != null) dgvKursy.Columns["Pojazd"].FillWeight = 80;
            if (dgvKursy.Columns["Trasa"] != null) dgvKursy.Columns["Trasa"].FillWeight = 150;
            if (dgvKursy.Columns["Wyjazd"] != null) dgvKursy.Columns["Wyjazd"].FillWeight = 60;
            if (dgvKursy.Columns["Powrót"] != null) dgvKursy.Columns["Powrót"].FillWeight = 60;
            if (dgvKursy.Columns["Palety"] != null) dgvKursy.Columns["Palety"].FillWeight = 70;
            if (dgvKursy.Columns["Pojemniki"] != null) dgvKursy.Columns["Pojemniki"].FillWeight = 80;
            if (dgvKursy.Columns["Ładunki"] != null) dgvKursy.Columns["Ładunki"].FillWeight = 70;
            if (dgvKursy.Columns["Status"] != null) dgvKursy.Columns["Status"].FillWeight = 80;
        }

        private async Task<List<KursRaport>> PobierzKursyAsync(DateTime data)
        {
            var kursy = new List<KursRaport>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Pobierz kursy z podstawowymi danymi + telefon kierowcy
            var sqlKursy = @"
                SELECT
                    k.KursID, k.DataKursu, k.Trasa, k.GodzWyjazdu, k.GodzPowrotu,
                    k.Status, k.PlanE2NaPalete,
                    CONCAT(ki.Imie, ' ', ki.Nazwisko) AS KierowcaNazwa,
                    ki.Telefon AS KierowcaTelefon,
                    p.Rejestracja AS PojazdRejestracja,
                    p.PaletyH1 AS PaletyPojazdu
                FROM dbo.Kurs k
                JOIN dbo.Kierowca ki ON k.KierowcaID = ki.KierowcaID
                JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID
                WHERE k.DataKursu = @Data
                ORDER BY k.GodzWyjazdu, k.KursID";

            using (var cmd = new SqlCommand(sqlKursy, connection))
            {
                cmd.Parameters.AddWithValue("@Data", data.Date);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var kurs = new KursRaport
                        {
                            KursID = reader.GetInt64(0),
                            DataKursu = reader.GetDateTime(1),
                            Trasa = reader.IsDBNull(2) ? null : reader.GetString(2),
                            GodzWyjazdu = reader.IsDBNull(3) ? null : reader.GetTimeSpan(3),
                            GodzPowrotu = reader.IsDBNull(4) ? null : reader.GetTimeSpan(4),
                            Status = reader.GetString(5),
                            PlanE2NaPalete = reader.GetByte(6),
                            KierowcaNazwa = reader.GetString(7),
                            KierowcaTelefon = reader.IsDBNull(8) ? null : reader.GetString(8),
                            PojazdRejestracja = reader.GetString(9),
                            PaletyPojazdu = reader.GetInt32(10)
                        };

                        kursy.Add(kurs);
                    }
                }
            }

            // Pobierz ładunki dla każdego kursu (po zamknięciu poprzedniego readera)
            foreach (var kurs in kursy)
            {
                kurs.Ladunki = await PobierzLadunkiKursuAsync(kurs.KursID);

                // Oblicz statystyki
                kurs.SumaPojemnikiE2 = kurs.Ladunki.Sum(l => l.PojemnikiE2);
                kurs.PaletyUzyteNominal = (int)Math.Ceiling((double)kurs.SumaPojemnikiE2 / kurs.PlanE2NaPalete);
            }

            // Fallback: dla ładunków bez nazwy klienta (cache KlientAdres pusty) dociągnij
            // bezpośrednio z HANDEL.SSCommon.STContractors przez ZamowieniaMieso.KlientId
            await UzupelnijNazwyZHandelAsync(kursy);

            return kursy;
        }

        private async Task UzupelnijNazwyZHandelAsync(List<KursRaport> kursy)
        {
            // Bierzemy WSZYSTKIE ZAM_ ładunki (nie tylko te z pustą nazwą) — bo chcemy
            // skróty z Sage.Shortcut zamiast pełnych nazw z TransportPL.KlientAdres.
            // Zgodne z widokiem kursów (TransportWpfService.PobierzNazwyKlientowAsync).
            var idy = kursy
                .SelectMany(k => k.Ladunki)
                .Where(l => l.KlientId.HasValue && l.KlientId.Value > 0)
                .Select(l => l.KlientId!.Value)
                .Distinct()
                .ToList();

            if (idy.Count == 0) return;

            var dane = new Dictionary<int, (string Nazwa, string Adres)>();
            const string connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
            try
            {
                await using var cn = new SqlConnection(connHandel);
                await cn.OpenAsync();
                using var cmd = cn.CreateCommand();
                var parametry = new List<string>();
                for (int i = 0; i < idy.Count; i++)
                {
                    var p = $"@id{i}";
                    parametry.Add(p);
                    cmd.Parameters.AddWithValue(p, idy[i]);
                }
                // Skrót (jak w widoku kursów) + adres domyślny (ulica + miasto)
                cmd.CommandText = $@"
                    SELECT c.Id,
                           ISNULL(c.Shortcut, 'KH ' + CAST(c.Id AS VARCHAR(10))) AS Skrot,
                           LTRIM(RTRIM(ISNULL(poa.Street, '') + ' ' + ISNULL(poa.Place, ''))) AS Adres
                    FROM [SSCommon].[STContractors] c
                    LEFT JOIN [SSCommon].[STPostOfficeAddresses] poa
                        ON poa.ContactGuid = c.ContactGuid
                       AND poa.AddressName = N'adres domyślny'
                    WHERE c.Id IN ({string.Join(",", parametry)})";
                cmd.CommandTimeout = 15;
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    int id = r.GetInt32(0);
                    string nazwa = r.IsDBNull(1) ? "" : r.GetString(1).Trim();
                    string adres = r.IsDBNull(2) ? "" : r.GetString(2).Trim();
                    dane[id] = (nazwa, adres);
                }
            }
            catch { /* silent — gdy Handel niedostępny pokażemy cache lub fallback */ }

            // Podstaw nazwy + adresy
            foreach (var lad in kursy.SelectMany(k => k.Ladunki))
            {
                if (!lad.KlientId.HasValue || !dane.TryGetValue(lad.KlientId.Value, out var d)) continue;

                // Nazwa: zawsze skrót z Sage (nadpisuje pełne nazwy z KlientAdres)
                if (!string.IsNullOrEmpty(d.Nazwa)) lad.NazwaKlienta = d.Nazwa;

                // Adres: priorytet ka cache (geolokalizacja), fallback Sage
                if (string.IsNullOrWhiteSpace(lad.Adres) && !string.IsNullOrWhiteSpace(d.Adres))
                    lad.Adres = d.Adres;
            }
        }

        private async Task<List<LadunekRaport>> PobierzLadunkiKursuAsync(long kursId)
        {
            var ladunki = new List<LadunekRaport>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Cross-DB JOIN (TransportPL ↔ LibraNet — ten sam serwer 192.168.0.109):
            // - ZamowieniaMieso (awizacja + agregowane kg z ZamowieniaMiesoTowar)
            // - KlientAdres (cache nazwy klienta, wypełniany przez Webfleet sync)
            // Wszystkie LEFT JOIN — ładunek może nie być ZAM_, wtedy NULL i fallback w UI.
            var sql = @"
                SELECT l.LadunekID, l.Kolejnosc, l.KodKlienta, l.PojemnikiE2, l.PaletyH1,
                       l.PlanE2NaPaleteOverride, l.Uwagi,
                       zm.DataPrzyjazdu AS DataAwizacji,
                       ISNULL((SELECT SUM(t.Ilosc) FROM LibraNet.dbo.ZamowieniaMiesoTowar t
                               WHERE t.ZamowienieId = zm.Id), 0) AS IloscKg,
                       ka.NazwaKlienta,
                       zm.KlientId,
                       LTRIM(RTRIM(ISNULL(ka.Ulica, '') + ' ' + ISNULL(ka.Miasto, ''))) AS AdresCache
                FROM dbo.Ladunek l
                LEFT JOIN LibraNet.dbo.ZamowieniaMieso zm
                    ON l.KodKlienta LIKE 'ZAM[_]%'
                   AND zm.Id = TRY_CAST(SUBSTRING(l.KodKlienta, 5, 20) AS INT)
                LEFT JOIN dbo.KlientAdres ka ON ka.KodKlienta = l.KodKlienta
                WHERE l.KursID = @KursID
                ORDER BY l.Kolejnosc";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@KursID", kursId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string adresCache = reader.IsDBNull(11) ? "" : reader.GetString(11).Trim();
                ladunki.Add(new LadunekRaport
                {
                    LadunekID = reader.GetInt64(0),
                    Kolejnosc = reader.GetInt32(1),
                    KodKlienta = reader.IsDBNull(2) ? null : reader.GetString(2),
                    PojemnikiE2 = reader.GetInt32(3),
                    PaletyH1 = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    PlanE2NaPaleteOverride = reader.IsDBNull(5) ? null : reader.GetByte(5),
                    Uwagi = reader.IsDBNull(6) ? null : reader.GetString(6),
                    DataAwizacji = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                    IloscKg = reader.IsDBNull(8) ? 0 : Convert.ToDecimal(reader.GetValue(8)),
                    NazwaKlienta = reader.IsDBNull(9) ? null : reader.GetString(9),
                    KlientId = reader.IsDBNull(10) ? null : Convert.ToInt32(reader.GetValue(10)),
                    Adres = adresCache   // może być puste, fallback Sage uzupełni
                });
            }

            return ladunki;
        }

        /// <summary>
        /// Programowy podgląd raportu dla wybranej daty — bez pokazywania okna z DataGridView.
        /// Używane przez nowy WPF (PlanowanieTransportuWpfWindow → 📊 Raport):
        /// jeden klik = od razu PrintPreviewDialog z kursami danego dnia.
        /// </summary>
        public async Task PokazPodgladDlaDatyAsync(DateTime data)
        {
            dtpData.Value = data;
            await ZaladujKursyAsync();
            BtnPodglad_Click(this, EventArgs.Empty);
        }

        private void BtnPodglad_Click(object sender, EventArgs e)
        {
            try
            {
                var printDocument = new PrintDocument();
                printDocument.DefaultPageSettings.Landscape = true;
                printDocument.PrintPage += PrintDocument_PrintPage;

                var printPreviewDialog = new PrintPreviewDialog
                {
                    Document = printDocument,
                    Width = 1000,
                    Height = 700,
                    StartPosition = FormStartPosition.CenterParent
                };

                printPreviewDialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas generowania podglądu: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDrukuj_Click(object sender, EventArgs e)
        {
            try
            {
                var printDocument = new PrintDocument();
                printDocument.DefaultPageSettings.Landscape = true;
                printDocument.PrintPage += PrintDocument_PrintPage;

                var printDialog = new PrintDialog
                {
                    Document = printDocument
                };

                if (printDialog.ShowDialog() == DialogResult.OK)
                {
                    printDocument.Print();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas drukowania: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // RAPORT — prosty, czytelny, 1 strona A4 landscape
        // ────────────────────────────────────────────────────────────────────
        // Każdy kurs:
        //   • Header 1-liniowy: HH:mm  #ID  Kierowca (telefon)  ·  Rejestracja
        //   • Lista odbiorców: dz DD.MM  HH:mm  Nazwa
        // Sortowanie: od najwcześniejszego wyjazdu
        // Layout: 2 kolumny (lewa→prawa, alternacja)
        // Bez tabel, bez kolorów, bez tła — tylko delikatne linie separujące
        // ────────────────────────────────────────────────────────────────────

        private int _printPageIndex;      // numer strony (od 1)
        private int _printKursIndex;      // index w posortowanej liście

        private const int ROW_H = 14;        // wiersz odbiorcy (więcej powietrza dla czytelności)
        private const int KART_HEAD_H = 18;  // header kursu + linia pod
        private const int GAP_KOL = 16;      // odstęp poziomy między kolumnami
        private const int GAP_KART = 14;     // odstęp pionowy między kartami
        private const int INDENT = 12;       // wcięcie wierszy odbiorcy względem headera
        private const int COL_AWIZ_W = 120;  // szerokość kolumny awizacji ("pon 08.06 10:00" = ~15 znaków × 7px)

        private void PrintDocument_PrintPage(object sender, PrintPageEventArgs e)
        {
            if (_printPageIndex == 0) { _printPageIndex = 1; _printKursIndex = 0; }

            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var bounds = e.MarginBounds;

            var fH1 = new Font("Segoe UI", 15, FontStyle.Bold);
            var fH2 = new Font("Segoe UI", 10);
            var fKurs = new Font("Segoe UI", 10, FontStyle.Bold);
            var fT  = new Font("Segoe UI", 9);
            var fTGray = new Font("Consolas", 8.5f);   // monospace — kolumna awizacji się ładnie układa
            var fS  = new Font("Segoe UI", 7.5f);
            var fStrzalka = new Font("Segoe UI", 11, FontStyle.Bold);

            var br = Brushes.Black;
            var brGray = Brushes.DimGray;
            using var penThin = new Pen(Color.Black, 0.5f);
            using var penKurs = new Pen(Color.Black, 0.9f);
            using var penThick = new Pen(Color.Black, 1.2f);

            // Sortowanie raz (rosnąco po godzinie wyjazdu)
            var kursyPosortowane = _kursy
                .OrderBy(k => k.GodzWyjazdu ?? TimeSpan.MaxValue)
                .ThenBy(k => k.KursID)
                .ToList();

            int yPos = bounds.Top;

            // ═══ HEADER strony 1 ══════════════════════════════════════════════
            if (_printPageIndex == 1)
            {
                g.DrawString("UBOJNIA DROBIU PIÓRKOWSCY", fH1, br,
                    new RectangleF(bounds.Left, yPos, bounds.Width, 22),
                    new StringFormat { Alignment = StringAlignment.Center });
                yPos += 24;

                var dataR = dtpData.Value;
                var kult = new System.Globalization.CultureInfo("pl-PL");
                string dzienTyg = dataR.ToString("dddd", kult);
                g.DrawString($"Plan kursów na {dataR:dd.MM.yyyy} ({dzienTyg})  ·  {kursyPosortowane.Count} kurs(y)",
                    fH2, br,
                    new RectangleF(bounds.Left, yPos, bounds.Width, 14),
                    new StringFormat { Alignment = StringAlignment.Center });
                yPos += 18;

                g.DrawLine(penThick, bounds.Left, yPos, bounds.Right, yPos);
                yPos += 12;
            }

            // ═══ LAYOUT 2-KOLUMNOWY (każda kolumna = pół szerokości) ═══════════
            int yStop = bounds.Bottom - 14;
            int kolW = (bounds.Width - GAP_KOL) / 2;
            int[] kolX = { bounds.Left, bounds.Left + kolW + GAP_KOL };
            int[] yKol = { yPos, yPos };
            int kolumna = 0;
            var kultPL = new System.Globalization.CultureInfo("pl-PL");
            var dataKursowa = dtpData.Value.Date;
            // Numer porządkowy w obrębie kolumny (1., 2., 3., ...)
            int[] numerWKolumnie = { 0, 0 };

            while (_printKursIndex < kursyPosortowane.Count)
            {
                var kurs = kursyPosortowane[_printKursIndex];
                int kartaH = ObliczWysokoscKarty(kurs);

                int wybrana = -1;
                if (yKol[kolumna] + kartaH <= yStop) wybrana = kolumna;
                else
                {
                    int inna = 1 - kolumna;
                    if (yKol[inna] + kartaH <= yStop) wybrana = inna;
                }

                if (wybrana == -1)
                {
                    RysujStopke(g, bounds, fS, brGray);
                    e.HasMorePages = true;
                    _printPageIndex++;
                    return;
                }

                // Strzałka między kursami w tej samej kolumnie (jeśli nie pierwszy)
                if (numerWKolumnie[wybrana] > 0)
                {
                    int xArrow = kolX[wybrana] + kolW / 2;
                    int yArrow = yKol[wybrana] - GAP_KART + 1;
                    g.DrawString("↓", fStrzalka, brGray,
                        new RectangleF(xArrow - 12, yArrow, 24, GAP_KART - 2),
                        new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                }

                numerWKolumnie[wybrana]++;
                RysujKarte(g, kolX[wybrana], yKol[wybrana], kolW, kurs, numerWKolumnie[wybrana],
                    fKurs, fT, fTGray, br, brGray, penKurs, kultPL, dataKursowa);
                yKol[wybrana] += kartaH + GAP_KART;
                kolumna = 1 - wybrana;   // rotacja LR LR
                _printKursIndex++;
            }

            // ═══ KONIEC ══════════════════════════════════════════════════════
            RysujStopke(g, bounds, fS, brGray);
            e.HasMorePages = false;
            _printPageIndex = 0;
            _printKursIndex = 0;
        }

        /// <summary>Wysokość karty: 1 linia header + N wierszy odbiorcy.</summary>
        private static int ObliczWysokoscKarty(KursRaport k)
        {
            int wierszy = Math.Max(1, k.Ladunki.Count);
            return KART_HEAD_H + wierszy * ROW_H;
        }

        /// <summary>
        /// Karta kursu = 1-linia header + lista odbiorców.
        /// Header: „06:00  #234  Kierowca (telefon)  ·  Rejestracja"
        /// Odbiorca: „pon 08.06 13:00  Nazwa Klienta"
        /// </summary>
        private static void RysujKarte(Graphics g, int x, int y, int w, KursRaport kurs, int numerKursu,
            Font fKurs, Font fT, Font fTGray, Brush br, Brush brGray, Pen penKurs,
            System.Globalization.CultureInfo kult, DateTime dataKursowa)
        {
            // ── HEADER karty: numer porządkowy + godzina + kierowca + telefon + pojazd
            string godzWyjazdu = kurs.GodzWyjazdu?.ToString(@"hh\:mm") ?? "--:--";
            string tel = !string.IsNullOrWhiteSpace(kurs.KierowcaTelefon) ? $"  tel. {kurs.KierowcaTelefon}" : "";
            string header = $"{numerKursu}.  {godzWyjazdu}  ·  {kurs.KierowcaNazwa}{tel}  ·  {kurs.PojazdRejestracja}";
            g.DrawString(header, fKurs, br,
                new RectangleF(x, y, w, 14),
                new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center });

            // Linia pod nagłówkiem kursu
            int yLine = y + 15;
            g.DrawLine(penKurs, x, yLine, x + w, yLine);

            // ── Lista odbiorców: „Klient (awizacja) · adres"
            int yRow = y + KART_HEAD_H;
            foreach (var l in kurs.Ladunki.OrderBy(z => z.Kolejnosc))
            {
                string nazwa = !string.IsNullOrEmpty(l.NazwaKlienta) ? l.NazwaKlienta : (l.KodKlienta ?? "—");

                string awiz;
                if (l.DataAwizacji.HasValue)
                {
                    var d = l.DataAwizacji.Value;
                    if (d.Date == dataKursowa)
                        awiz = d.ToString("HH:mm");
                    else
                    {
                        string dzien = d.ToString("ddd", kult).TrimEnd('.');
                        awiz = $"{dzien} {d:dd.MM} {d:HH:mm}";
                    }
                }
                else awiz = "brak awizacji";

                string adres = !string.IsNullOrWhiteSpace(l.Adres) ? $"  ·  {l.Adres}" : "";
                string wiersz = $"{nazwa} ({awiz}){adres}";
                g.DrawString(wiersz, fT, br,
                    new RectangleF(x + INDENT, yRow, w - INDENT - 2, ROW_H),
                    new StringFormat
                    {
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap,
                        LineAlignment = StringAlignment.Center
                    });

                yRow += ROW_H;
            }
        }

        private void RysujStopke(Graphics g, Rectangle bounds, Font fS, Brush brGray)
        {
            string stopka = $"Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}   ·   ZPSP Transport   ·   Strona {_printPageIndex}";
            g.DrawString(stopka, fS, brGray,
                new RectangleF(bounds.Left, bounds.Bottom - 11, bounds.Width, 10),
                new StringFormat { Alignment = StringAlignment.Center });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose managed resources
            }
            base.Dispose(disposing);
        }
    }

    // Klasy pomocnicze dla raportu
    public class KursRaport
    {
        public long KursID { get; set; }
        public DateTime DataKursu { get; set; }
        public string KierowcaNazwa { get; set; }
        public string KierowcaTelefon { get; set; }
        public string PojazdRejestracja { get; set; }
        public int PaletyPojazdu { get; set; }
        public string Trasa { get; set; }
        public TimeSpan? GodzWyjazdu { get; set; }
        public TimeSpan? GodzPowrotu { get; set; }
        public string Status { get; set; }
        public byte PlanE2NaPalete { get; set; }
        public List<LadunekRaport> Ladunki { get; set; } = new List<LadunekRaport>();
        public int SumaPojemnikiE2 { get; set; }
        public int PaletyUzyteNominal { get; set; }
        public decimal SumaKg => Ladunki.Sum(l => l.IloscKg);
    }

    public class LadunekRaport
    {
        public long LadunekID { get; set; }
        public int Kolejnosc { get; set; }
        public string KodKlienta { get; set; }
        public int PojemnikiE2 { get; set; }
        public int? PaletyH1 { get; set; }
        public byte? PlanE2NaPaleteOverride { get; set; }
        public string Uwagi { get; set; }
        // Dane z JOINów (ZamowieniaMieso + ZamowieniaMiesoTowar + KlientAdres)
        public DateTime? DataAwizacji { get; set; }
        public decimal IloscKg { get; set; }
        public string NazwaKlienta { get; set; }
        public int? KlientId { get; set; }   // do fallback lookup w HM.STContractors gdy KlientAdres puste
        public string Adres { get; set; }    // ulica + miasto (z KlientAdres lub Sage)
    }
}