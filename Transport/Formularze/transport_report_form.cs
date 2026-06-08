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
        // RAPORT — bogaty layout z paginacją
        // ────────────────────────────────────────────────────────────────────
        // Każdy kurs = osobny „karta" w pełnej szerokości strony:
        //   header (kierowca + auto) → tabela ładunków (lp/klient/awizacja/kg/poj/palety/uwagi) → suma
        // Paginacja: gdy karta nie mieści się — przeskok na następną stronę.
        // ────────────────────────────────────────────────────────────────────

        private int _printPageIndex;        // numer aktualnie drukowanej strony (od 1)
        private int _printKursIndex;        // index kursu od którego zaczynamy tę stronę

        private void PrintDocument_PrintPage(object sender, PrintPageEventArgs e)
        {
            // Reset stanu przy pierwszej stronie
            if (_printPageIndex == 0) { _printPageIndex = 1; _printKursIndex = 0; }

            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var bounds = e.MarginBounds;

            // Kolory & czcionki
            var clrAccent     = Color.FromArgb(30, 64, 175);    // indygo-700
            var clrAccentSoft = Color.FromArgb(224, 231, 255);  // indygo-100
            var clrText       = Color.FromArgb(31, 41, 51);
            var clrMuted      = Color.FromArgb(123, 135, 148);
            var clrLine       = Color.FromArgb(226, 232, 240);
            var clrZebra      = Color.FromArgb(248, 250, 252);
            var clrSumaBg     = Color.FromArgb(243, 244, 246);

            var fH1 = new Font("Segoe UI Semibold", 16, FontStyle.Bold);
            var fH2 = new Font("Segoe UI", 11, FontStyle.Bold);
            var fH3 = new Font("Segoe UI Semibold", 10, FontStyle.Bold);
            var fT  = new Font("Segoe UI", 9.5f);
            var fTB = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            var fS  = new Font("Segoe UI", 8);

            int yPos = bounds.Top;

            // ─── HEADER (tylko na 1. stronie) ─────────────────────────────────
            if (_printPageIndex == 1)
            {
                // Nazwa firmy (centered)
                g.DrawString("UBOJNIA DROBIU PIÓRKOWSCY", fH1, new SolidBrush(clrAccent),
                    new RectangleF(bounds.Left, yPos, bounds.Width, 24),
                    new StringFormat { Alignment = StringAlignment.Center });
                yPos += 28;

                g.DrawString("Koziołki 40, 95-061 Dmosin   ·   NIP: 726-162-54-06   ·   Ilona Krakowiak: tel. 508 309 314",
                    fT, new SolidBrush(clrMuted),
                    new RectangleF(bounds.Left, yPos, bounds.Width, 16),
                    new StringFormat { Alignment = StringAlignment.Center });
                yPos += 22;

                // Akcent linia
                using (var penAccent = new Pen(clrAccent, 2)) g.DrawLine(penAccent, bounds.Left, yPos, bounds.Right, yPos);
                yPos += 16;

                // Tytuł raportu + data + dzień tygodnia
                var data = dtpData.Value;
                var kultura = new System.Globalization.CultureInfo("pl-PL");
                string dzienTyg = data.ToString("dddd", kultura);
                g.DrawString($"📅 RAPORT TRANSPORTOWY — {data:dd.MM.yyyy} ({dzienTyg})", fH2,
                    new SolidBrush(clrText), bounds.Left, yPos);
                yPos += 22;

                // Sumaryzacja
                int sumPoj = _kursy.Sum(k => k.SumaPojemnikiE2);
                int sumPal = _kursy.Sum(k => k.PaletyUzyteNominal);
                int sumLad = _kursy.Sum(k => k.Ladunki.Count);
                decimal sumKg = _kursy.Sum(k => k.SumaKg);
                string podsum = $"{_kursy.Count} kurs(y/ów)   ·   {sumLad} klient(ów)   ·   {sumKg:N0} kg   ·   {sumPoj} pojemników   ·   {sumPal} palet";
                using (var brBg = new SolidBrush(clrAccentSoft))
                    g.FillRectangle(brBg, bounds.Left, yPos, bounds.Width, 22);
                g.DrawString(podsum, fTB, new SolidBrush(clrAccent), bounds.Left + 8, yPos + 4);
                yPos += 32;
            }

            // ─── KARTY KURSÓW ─────────────────────────────────────────────────
            while (_printKursIndex < _kursy.Count)
            {
                var kurs = _kursy[_printKursIndex];
                int kartaH = ObliczWysokoscKarty(kurs);

                // Czy karta zmieści się na tej stronie? (zostaw 30px na stopkę)
                if (yPos + kartaH > bounds.Bottom - 30)
                {
                    if (_printKursIndex == 0) // gigantyczny pojedynczy kurs → wymuś rysowanie z paginacją wewnątrz
                        break;
                    e.HasMorePages = true;
                    _printPageIndex++;
                    RysujStopke(g, bounds, fS, clrMuted);
                    return;
                }

                yPos = RysujKarteKursu(g, bounds, yPos, kurs, fH3, fT, fTB, fS,
                    clrAccent, clrText, clrMuted, clrLine, clrZebra, clrSumaBg);
                yPos += 12; // odstęp między kartami
                _printKursIndex++;
            }

            // ─── STOPKA ───────────────────────────────────────────────────────
            RysujStopke(g, bounds, fS, clrMuted);
            e.HasMorePages = false;
            // Reset na koniec — żeby kolejny BtnPodglad_Click zaczynał od pierwszej strony
            _printPageIndex = 0;
            _printKursIndex = 0;
        }

        private int ObliczWysokoscKarty(KursRaport k)
        {
            // header (50) + tabela header (22) + N wierszy (18) + suma (22) + padding (16)
            int wierszy = Math.Max(1, k.Ladunki.Count);
            return 50 + 22 + wierszy * 18 + 22 + 16;
        }

        private int RysujKarteKursu(Graphics g, Rectangle bounds, int y0, KursRaport k,
            Font fH3, Font fT, Font fTB, Font fS,
            Color clrAccent, Color clrText, Color clrMuted, Color clrLine, Color clrZebra, Color clrSumaBg)
        {
            int x = bounds.Left;
            int w = bounds.Width;
            int y = y0;

            // ── HEADER karty (akcent na lewo + tytuł + info)
            using (var brAcc = new SolidBrush(clrAccent))
                g.FillRectangle(brAcc, x, y, 4, 50);

            // Linia 1: KURS #ID · WYJAZD → POWRÓT · Status
            string godziny = $"{k.GodzWyjazdu?.ToString(@"hh\:mm") ?? "--"} → {k.GodzPowrotu?.ToString(@"hh\:mm") ?? "--"}";
            string linia1 = $"KURS #{k.KursID}   ·   {godziny}   ·   {k.DataKursu:dd.MM.yyyy}   ·   Status: {k.Status}";
            g.DrawString(linia1, fH3, new SolidBrush(clrText), x + 12, y + 4);

            // Linia 2: 👤 Kierowca   ·   🚛 Pojazd
            string linia2 = $"👤 {k.KierowcaNazwa}    🚛 {k.PojazdRejestracja}   (pojemność {k.PaletyPojazdu} palet · zaplanowano {k.PaletyUzyteNominal})";
            g.DrawString(linia2, fT, new SolidBrush(clrMuted), x + 12, y + 22);

            // Trasa (skrócona, jeśli zbyt długa)
            if (!string.IsNullOrWhiteSpace(k.Trasa))
                g.DrawString($"🛣 {k.Trasa}", fT, new SolidBrush(clrMuted), x + 12, y + 36);
            y += 52;

            // ── TABELA ładunków
            int[] szer = { 28, w - 28 - 70 - 70 - 50 - 60 - 110, 70, 70, 50, 60, 110 };
            string[] headers = { "Lp", "Klient", "Awizacja", "Kg", "Poj.", "Palet", "Uwagi" };
            StringAlignment[] aligns = {
                StringAlignment.Center, StringAlignment.Near, StringAlignment.Center,
                StringAlignment.Far, StringAlignment.Center, StringAlignment.Center, StringAlignment.Near
            };

            // Tło + tekst nagłówków
            using (var brBg = new SolidBrush(Color.FromArgb(243, 244, 246)))
                g.FillRectangle(brBg, x, y, w, 22);
            int colX = x;
            for (int i = 0; i < headers.Length; i++)
            {
                g.DrawString(headers[i], fTB, new SolidBrush(clrText),
                    new RectangleF(colX + 4, y + 4, szer[i] - 8, 18),
                    new StringFormat { Alignment = aligns[i], LineAlignment = StringAlignment.Center });
                colX += szer[i];
            }
            using (var penLine = new Pen(clrLine, 1)) g.DrawLine(penLine, x, y + 22, x + w, y + 22);
            y += 22;

            // Wiersze ładunków
            int row = 0;
            foreach (var l in k.Ladunki.OrderBy(z => z.Kolejnosc))
            {
                if (row % 2 == 1)
                    using (var brZ = new SolidBrush(clrZebra))
                        g.FillRectangle(brZ, x, y, w, 18);

                string nazwa = !string.IsNullOrEmpty(l.NazwaKlienta) ? l.NazwaKlienta : (l.KodKlienta ?? "—");
                string awiz = l.DataAwizacji?.ToString("HH:mm") ?? "—";
                string kg = l.IloscKg > 0 ? l.IloscKg.ToString("N0") : "—";
                string poj = l.PojemnikiE2.ToString();
                string palet = l.PaletyH1.HasValue ? l.PaletyH1.ToString() : "—";
                string uwagi = l.Uwagi ?? "";

                string[] cells = { l.Kolejnosc.ToString(), nazwa, awiz, kg, poj, palet, uwagi };
                colX = x;
                for (int i = 0; i < cells.Length; i++)
                {
                    g.DrawString(cells[i], fT, new SolidBrush(clrText),
                        new RectangleF(colX + 4, y + 2, szer[i] - 8, 16),
                        new StringFormat
                        {
                            Alignment = aligns[i],
                            LineAlignment = StringAlignment.Center,
                            Trimming = StringTrimming.EllipsisCharacter,
                            FormatFlags = StringFormatFlags.NoWrap
                        });
                    colX += szer[i];
                }
                y += 18;
                row++;
            }

            // Wiersz SUMA (wyróżniony)
            using (var brBg = new SolidBrush(clrSumaBg))
                g.FillRectangle(brBg, x, y, w, 22);
            using (var penLine = new Pen(clrLine, 1)) g.DrawLine(penLine, x, y, x + w, y);

            string[] sumaCells = { "", "SUMA", "", k.SumaKg.ToString("N0"), k.SumaPojemnikiE2.ToString(), $"{k.PaletyUzyteNominal}/{k.PaletyPojazdu}", "" };
            colX = x;
            for (int i = 0; i < sumaCells.Length; i++)
            {
                g.DrawString(sumaCells[i], fTB, new SolidBrush(clrAccent),
                    new RectangleF(colX + 4, y + 4, szer[i] - 8, 18),
                    new StringFormat { Alignment = aligns[i], LineAlignment = StringAlignment.Center });
                colX += szer[i];
            }
            y += 24;

            return y;
        }

        private void RysujStopke(Graphics g, Rectangle bounds, Font fS, Color clrMuted)
        {
            string stopka = $"Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}   ·   ZPSP Transport   ·   Strona {_printPageIndex}";
            g.DrawString(stopka, fS, new SolidBrush(clrMuted),
                new RectangleF(bounds.Left, bounds.Bottom - 14, bounds.Width, 12),
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