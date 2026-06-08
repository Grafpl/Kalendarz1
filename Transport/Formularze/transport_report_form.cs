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

            // Pobierz kursy z podstawowymi danymi
            var sqlKursy = @"
                SELECT 
                    k.KursID, k.DataKursu, k.Trasa, k.GodzWyjazdu, k.GodzPowrotu, 
                    k.Status, k.PlanE2NaPalete,
                    CONCAT(ki.Imie, ' ', ki.Nazwisko) AS KierowcaNazwa,
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
                            PojazdRejestracja = reader.GetString(8),
                            PaletyPojazdu = reader.GetInt32(9)
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

            return kursy;
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
                       ka.NazwaKlienta
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
                    NazwaKlienta = reader.IsDBNull(9) ? null : reader.GetString(9)
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
        // RAPORT — czarno-biały, zbity, paginacja per-wiersz dla długich kursów
        // ────────────────────────────────────────────────────────────────────
        // Layout:
        //   Strona 1: header firmy + tytuł + sumaryzacja
        //   Każda strona: karty kursów (header + tabela ładunków + suma)
        //   Karta dzielona między stronami jeśli za długa
        //   Stopka: data wygenerowania + strona N
        // ────────────────────────────────────────────────────────────────────

        private int _printPageIndex;      // numer strony (od 1)
        private int _printKursIndex;      // index aktualnego kursu
        private int _printLadunekIndex;   // index ładunku w aktualnym kursie (dla kursów dzielonych)
        private bool _printPokazujHeaderKarty;  // czy rysować header karty (false dla kontynuacji ze strony poprzedniej)

        // ─ kolumny tabeli — wspólne dla header + wierszy + sumy ──────────────
        private const int COL_LP = 28, COL_AWIZ = 60, COL_KG = 62, COL_POJ = 48, COL_PAL = 50, COL_UWAGI = 100;
        private const int ROW_H = 15;       // wysokość wiersza ładunku (zbicie)
        private const int TABLE_HEAD_H = 18;
        private const int KART_HEAD_H = 40;
        private const int SUMA_H = 18;

        private void PrintDocument_PrintPage(object sender, PrintPageEventArgs e)
        {
            if (_printPageIndex == 0) { _printPageIndex = 1; _printKursIndex = 0; _printLadunekIndex = 0; _printPokazujHeaderKarty = true; }

            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var bounds = e.MarginBounds;

            // Czcionki — czarno-białe, czytelne
            var fH1 = new Font("Segoe UI", 14, FontStyle.Bold);
            var fH2 = new Font("Segoe UI", 10, FontStyle.Bold);
            var fH3 = new Font("Segoe UI", 9, FontStyle.Bold);
            var fT  = new Font("Segoe UI", 8.5f);
            var fTB = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            var fS  = new Font("Segoe UI", 7.5f);

            var br = Brushes.Black;
            var brGray = Brushes.DimGray;
            using var penThin = new Pen(Color.Black, 0.6f);
            using var penThick = new Pen(Color.Black, 1.4f);
            using var brHeaderBg = new SolidBrush(Color.FromArgb(235, 235, 235));

            int yPos = bounds.Top;

            // ═══ HEADER strony 1 ═══════════════════════════════════════════════
            if (_printPageIndex == 1)
            {
                g.DrawString("UBOJNIA DROBIU PIÓRKOWSCY", fH1, br,
                    new RectangleF(bounds.Left, yPos, bounds.Width, 20),
                    new StringFormat { Alignment = StringAlignment.Center });
                yPos += 22;
                g.DrawString("Koziołki 40, 95-061 Dmosin   ·   NIP: 726-162-54-06   ·   Ilona Krakowiak: 508 309 314",
                    fT, brGray,
                    new RectangleF(bounds.Left, yPos, bounds.Width, 12),
                    new StringFormat { Alignment = StringAlignment.Center });
                yPos += 16;

                g.DrawLine(penThick, bounds.Left, yPos, bounds.Right, yPos);
                yPos += 8;

                var data = dtpData.Value;
                var kultura = new System.Globalization.CultureInfo("pl-PL");
                string dzienTyg = data.ToString("dddd", kultura);
                g.DrawString($"RAPORT TRANSPORTOWY — {data:dd.MM.yyyy} ({dzienTyg})", fH2, br,
                    bounds.Left, yPos);
                yPos += 18;

                // Sumaryzacja jako pasek z obramowaniem (B&W)
                int sumPoj = _kursy.Sum(k => k.SumaPojemnikiE2);
                int sumPal = _kursy.Sum(k => k.PaletyUzyteNominal);
                int sumLad = _kursy.Sum(k => k.Ladunki.Count);
                decimal sumKg = _kursy.Sum(k => k.SumaKg);
                string podsum = $"{_kursy.Count} kurs(y) · {sumLad} klient(ów) · {sumKg:N0} kg · {sumPoj} pojemników · {sumPal} palet";
                g.FillRectangle(brHeaderBg, bounds.Left, yPos, bounds.Width, 18);
                g.DrawRectangle(penThin, bounds.Left, yPos, bounds.Width, 18);
                g.DrawString(podsum, fTB, br, bounds.Left + 6, yPos + 3);
                yPos += 22;
            }

            // ═══ KARTY KURSÓW (z paginacją per-wiersz) ════════════════════════
            // Szerokości kolumn — szerokość klienta = całość - inne kolumny
            int wTotal = bounds.Width;
            int wKlient = wTotal - COL_LP - COL_AWIZ - COL_KG - COL_POJ - COL_PAL - COL_UWAGI;
            int[] szer = { COL_LP, wKlient, COL_AWIZ, COL_KG, COL_POJ, COL_PAL, COL_UWAGI };
            string[] headers = { "Lp", "Klient", "Awizacja", "Kg", "Poj.", "Palet", "Uwagi" };
            StringAlignment[] aligns = {
                StringAlignment.Center, StringAlignment.Near, StringAlignment.Center,
                StringAlignment.Far, StringAlignment.Center, StringAlignment.Center, StringAlignment.Near
            };

            int yStop = bounds.Bottom - 18;   // miejsce na stopkę

            while (_printKursIndex < _kursy.Count)
            {
                var kurs = _kursy[_printKursIndex];
                var ladunki = kurs.Ladunki.OrderBy(z => z.Kolejnosc).ToList();
                int liczbaPozostalych = ladunki.Count - _printLadunekIndex;

                // Wymaganie minimum: header karty + table header + 1 wiersz + suma = czytelnie nie zaczynamy karty pod koniec strony
                int wymaganeMin = (_printPokazujHeaderKarty ? KART_HEAD_H : 0) + TABLE_HEAD_H + ROW_H + SUMA_H;
                if (yPos + wymaganeMin > yStop)
                {
                    e.HasMorePages = true;
                    _printPageIndex++;
                    RysujStopke(g, bounds, fS, brGray);
                    return;
                }

                // ── HEADER karty (gdy nie kontynuacja)
                if (_printPokazujHeaderKarty)
                {
                    g.DrawRectangle(penThin, bounds.Left, yPos, wTotal, KART_HEAD_H);
                    string godziny = $"{kurs.GodzWyjazdu?.ToString(@"hh\:mm") ?? "--"} → {kurs.GodzPowrotu?.ToString(@"hh\:mm") ?? "--"}";
                    string linia1 = $"KURS #{kurs.KursID}   ·   {kurs.KierowcaNazwa}   ·   {kurs.PojazdRejestracja}";
                    string linia2 = $"Wyjazd: {kurs.DataKursu:dd.MM} {godziny}   ·   {kurs.Status}   ·   pojemność: {kurs.PaletyUzyteNominal}/{kurs.PaletyPojazdu} palet";
                    if (!string.IsNullOrWhiteSpace(kurs.Trasa)) linia2 += $"   ·   trasa: {kurs.Trasa}";
                    g.DrawString(linia1, fTB, br, bounds.Left + 6, yPos + 3);
                    g.DrawString(linia2, fT, brGray,
                        new RectangleF(bounds.Left + 6, yPos + 20, wTotal - 12, 14),
                        new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap });
                    yPos += KART_HEAD_H;
                }

                // ── HEADER tabeli (rysowany na każdej stronie kursu)
                g.FillRectangle(brHeaderBg, bounds.Left, yPos, wTotal, TABLE_HEAD_H);
                g.DrawRectangle(penThin, bounds.Left, yPos, wTotal, TABLE_HEAD_H);
                int colX = bounds.Left;
                for (int i = 0; i < headers.Length; i++)
                {
                    g.DrawString(headers[i], fTB, br,
                        new RectangleF(colX + 3, yPos + 2, szer[i] - 6, TABLE_HEAD_H - 4),
                        new StringFormat { Alignment = aligns[i], LineAlignment = StringAlignment.Center });
                    if (i < headers.Length - 1) g.DrawLine(penThin, colX + szer[i], yPos, colX + szer[i], yPos + TABLE_HEAD_H);
                    colX += szer[i];
                }
                yPos += TABLE_HEAD_H;

                // ── Wiersze ładunków — paginacja per wiersz
                int yTabelaStart = yPos;
                bool dokonczonoKurs = false;
                while (_printLadunekIndex < ladunki.Count)
                {
                    // Czy zmieści się jeszcze 1 wiersz + suma?
                    if (yPos + ROW_H + SUMA_H > yStop)
                    {
                        // Zamknij obramowanie tabeli na tej stronie + obetnij linie pionowe
                        RysujLinieKolumn(g, penThin, bounds.Left, yTabelaStart, yPos, szer, headers.Length);
                        g.DrawLine(penThin, bounds.Left, yPos, bounds.Right, yPos);

                        e.HasMorePages = true;
                        _printPageIndex++;
                        _printPokazujHeaderKarty = false;   // header karty już pokazany
                        RysujStopke(g, bounds, fS, brGray);
                        return;
                    }

                    var l = ladunki[_printLadunekIndex];
                    string nazwa = !string.IsNullOrEmpty(l.NazwaKlienta) ? l.NazwaKlienta : (l.KodKlienta ?? "—");
                    string awiz = l.DataAwizacji?.ToString("HH:mm") ?? "—";
                    string kg = l.IloscKg > 0 ? l.IloscKg.ToString("N0") : "—";
                    string poj = l.PojemnikiE2.ToString();
                    string palet = l.PaletyH1.HasValue ? l.PaletyH1.ToString() : "—";
                    string uwagi = l.Uwagi ?? "";

                    string[] cells = { l.Kolejnosc.ToString(), nazwa, awiz, kg, poj, palet, uwagi };
                    colX = bounds.Left;
                    for (int i = 0; i < cells.Length; i++)
                    {
                        g.DrawString(cells[i], fT, br,
                            new RectangleF(colX + 3, yPos + 1, szer[i] - 6, ROW_H - 2),
                            new StringFormat
                            {
                                Alignment = aligns[i],
                                LineAlignment = StringAlignment.Center,
                                Trimming = StringTrimming.EllipsisCharacter,
                                FormatFlags = StringFormatFlags.NoWrap
                            });
                        colX += szer[i];
                    }
                    yPos += ROW_H;
                    _printLadunekIndex++;
                }
                dokonczonoKurs = true;

                // ── Wiersz SUMA
                g.FillRectangle(brHeaderBg, bounds.Left, yPos, wTotal, SUMA_H);
                g.DrawRectangle(penThin, bounds.Left, yPos, wTotal, SUMA_H);
                string[] sumaCells = { "", "SUMA", "", kurs.SumaKg.ToString("N0"), kurs.SumaPojemnikiE2.ToString(), $"{kurs.PaletyUzyteNominal}/{kurs.PaletyPojazdu}", "" };
                colX = bounds.Left;
                for (int i = 0; i < sumaCells.Length; i++)
                {
                    g.DrawString(sumaCells[i], fTB, br,
                        new RectangleF(colX + 3, yPos + 2, szer[i] - 6, SUMA_H - 4),
                        new StringFormat { Alignment = aligns[i], LineAlignment = StringAlignment.Center });
                    if (i < sumaCells.Length - 1) g.DrawLine(penThin, colX + szer[i], yPos, colX + szer[i], yPos + SUMA_H);
                    colX += szer[i];
                }
                yPos += SUMA_H;

                // Dorysuj linie kolumn na całej wysokości tabeli (od yTabelaStart do yPos przed SUMA)
                RysujLinieKolumn(g, penThin, bounds.Left, yTabelaStart, yPos - SUMA_H, szer, headers.Length);

                // Następny kurs
                _printKursIndex++;
                _printLadunekIndex = 0;
                _printPokazujHeaderKarty = true;
                yPos += 6;   // odstęp między kartami (zbity)
            }

            // ═══ KONIEC — stopka + reset ═════════════════════════════════════
            RysujStopke(g, bounds, fS, brGray);
            e.HasMorePages = false;
            _printPageIndex = 0;
            _printKursIndex = 0;
            _printLadunekIndex = 0;
            _printPokazujHeaderKarty = true;
        }

        /// <summary>Rysuje pionowe linie kolumn tabeli między y1 i y2.</summary>
        private static void RysujLinieKolumn(Graphics g, Pen pen, int x0, int y1, int y2, int[] szer, int kolumnyCount)
        {
            int x = x0;
            for (int i = 0; i < kolumnyCount; i++)
            {
                if (i < kolumnyCount - 1) g.DrawLine(pen, x + szer[i], y1, x + szer[i], y2);
                x += szer[i];
            }
        }

        private void RysujStopke(Graphics g, Rectangle bounds, Font fS, Brush brGray)
        {
            string stopka = $"Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}   ·   ZPSP Transport   ·   Strona {_printPageIndex}";
            g.DrawString(stopka, fS, brGray,
                new RectangleF(bounds.Left, bounds.Bottom - 12, bounds.Width, 10),
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
    }
}