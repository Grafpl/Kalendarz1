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

            var sql = @"
                SELECT LadunekID, Kolejnosc, KodKlienta, PojemnikiE2, PaletyH1, 
                       PlanE2NaPaleteOverride, Uwagi
                FROM dbo.Ladunek 
                WHERE KursID = @KursID
                ORDER BY Kolejnosc";

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
                    Uwagi = reader.IsDBNull(6) ? null : reader.GetString(6)
                });
            }

            return ladunki;
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

        private void PrintDocument_PrintPage(object sender, PrintPageEventArgs e)
        {
            var g = e.Graphics;
            var bounds = e.MarginBounds;
            var yPos = bounds.Top;

            // Czcionki
            var fontNaglowek = new Font("Arial", 14, FontStyle.Bold);
            var fontPodnaglowek = new Font("Arial", 10, FontStyle.Bold);
            var fontTekst = new Font("Arial", 9);
            var fontMaly = new Font("Arial", 8);

            // Nagłówek firmy
            var naglowekFirmy = "UBOJNIA DROBIU \"PIÓRKOWSCY\"";
            var adresFirmy = "Koziołki 40, 95-061 Dmosin";
            var nipFirmy = "NIP: 726-162-54-06";
            var kontaktFirmy = "Ilona Krakowiak  •  Tel: 508 309 314";

            var sizeNaglowek = g.MeasureString(naglowekFirmy, fontNaglowek);
            g.DrawString(naglowekFirmy, fontNaglowek, Brushes.Black,
                bounds.Left + (bounds.Width - sizeNaglowek.Width) / 2, yPos);
            yPos += (int)sizeNaglowek.Height + 5;

            g.DrawString(adresFirmy, fontTekst, Brushes.Black,
                bounds.Left + (bounds.Width - g.MeasureString(adresFirmy, fontTekst).Width) / 2, yPos);
            yPos += 15;

            g.DrawString($"{nipFirmy}  •  {kontaktFirmy}", fontTekst, Brushes.Black,
                bounds.Left + (bounds.Width - g.MeasureString($"{nipFirmy}  •  {kontaktFirmy}", fontTekst).Width) / 2, yPos);
            yPos += 25;

            // Linia oddzielająca
            g.DrawLine(Pens.Black, bounds.Left, yPos, bounds.Right, yPos);
            yPos += 15;

            // Tytuł raportu
            var tytulRaportu = $"RAPORT TRANSPORTOWY - {dtpData.Value:dd.MM.yyyy}";
            var sizeTytul = g.MeasureString(tytulRaportu, fontPodnaglowek);
            g.DrawString(tytulRaportu, fontPodnaglowek, Brushes.Black,
                bounds.Left + (bounds.Width - sizeTytul.Width) / 2, yPos);
            yPos += (int)sizeTytul.Height + 20;

            // Informacje podsumowujące
            var infoText = $"Łączna liczba kursów: {_kursy.Count}  •  " +
                          $"Łączna liczba ładunków: {_kursy.Sum(k => k.Ladunki.Count)}  •  " +
                          $"Łączne pojemniki: {_kursy.Sum(k => k.SumaPojemnikiE2)}";
            g.DrawString(infoText, fontTekst, Brushes.DarkBlue, bounds.Left, yPos);
            yPos += 25;

            // Tabela kursów
            var kolumny = new[]
            {
                new { Nazwa = "Lp.", Szerokosc = 30 },
                new { Nazwa = "Kierowca", Szerokosc = 120 },
                new { Nazwa = "Pojazd", Szerokosc = 80 },
                new { Nazwa = "Trasa", Szerokosc = 150 },
                new { Nazwa = "Wyjazd", Szerokosc = 50 },
                new { Nazwa = "Powrót", Szerokosc = 50 },
                new { Nazwa = "Palety", Szerokosc = 60 },
                new { Nazwa = "Pojemniki", Szerokosc = 70 },
                new { Nazwa = "Status", Szerokosc = 80 },
                new { Nazwa = "Ładunki", Szerokosc = 300 }
            };

            // Nagłówki kolumn
            var xPos = bounds.Left;
            foreach (var kolumna in kolumny)
            {
                var rect = new Rectangle(xPos, yPos, kolumna.Szerokosc, 20);
                g.FillRectangle(Brushes.LightGray, rect);
                g.DrawRectangle(Pens.Black, rect);
                g.DrawString(kolumna.Nazwa, fontPodnaglowek, Brushes.Black,
                    rect.X + 2, rect.Y + 2);
                xPos += kolumna.Szerokosc;
            }
            yPos += 22;

            // Dane kursów
            for (int i = 0; i < _kursy.Count && yPos < bounds.Bottom - 100; i++)
            {
                var kurs = _kursy[i];
                xPos = bounds.Left;

                // Wysokość wiersza (może być większa dla wielu ładunków)
                var wysokoscWiersza = Math.Max(20, kurs.Ladunki.Count * 12 + 8);

                var wartosci = new string[]
                {
                    (i + 1).ToString(),
                    kurs.KierowcaNazwa,
                    kurs.PojazdRejestracja,
                    kurs.Trasa ?? "-",
                    kurs.GodzWyjazdu?.ToString(@"hh\:mm") ?? "-",
                    kurs.GodzPowrotu?.ToString(@"hh\:mm") ?? "-",
                    $"{kurs.PaletyUzyteNominal}/{kurs.PaletyPojazdu}",
                    kurs.SumaPojemnikiE2.ToString(),
                    kurs.Status,
                    string.Join("; ", kurs.Ladunki.Select(l =>
                        $"{l.Uwagi ?? l.KodKlienta ?? "?"} ({l.PojemnikiE2}poj)"))
                };

                for (int j = 0; j < kolumny.Length; j++)
                {
                    var rect = new Rectangle(xPos, yPos, kolumny[j].Szerokosc, wysokoscWiersza);
                    g.DrawRectangle(Pens.Gray, rect);

                    // Dla kolumny ładunków użyj mniejszej czcionki
                    var font = j == kolumny.Length - 1 ? fontMaly : fontTekst;
                    var textRect = new RectangleF(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4);
                    g.DrawString(wartosci[j], font, Brushes.Black, textRect);

                    xPos += kolumny[j].Szerokosc;
                }

                yPos += wysokoscWiersza;
            }

            // Stopka
            var stopka = $"Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}  •  Strona 1";
            g.DrawString(stopka, fontMaly, Brushes.Gray, bounds.Left, bounds.Bottom - 20);
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
    }
}