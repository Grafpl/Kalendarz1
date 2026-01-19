// Plik: KlasyWagoweDialog.cs
// WERSJA 18.0 - SUWAKI + PALETY + PROCENTY + WIDOK ZBIORCZY KLIENTÓW
#nullable enable
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1
{
    public class RozkladKlasWagowych
    {
        public Dictionary<int, decimal> KlasyProcent { get; set; } = new();
        public Dictionary<int, int> KlasyPojemniki { get; set; } = new();
        
        public decimal SumaProcent => KlasyProcent.Values.Sum();
        public int SumaPojemnikow => KlasyPojemniki.Values.Sum();

        public RozkladKlasWagowych()
        {
            for (int i = 5; i <= 12; i++)
            {
                KlasyProcent[i] = 0;
                KlasyPojemniki[i] = 0;
            }
        }

        public string ToNotatkString()
        {
            var parts = new List<string>();
            foreach (var kv in KlasyProcent.Where(k => k.Value > 0).OrderBy(k => k.Key))
            {
                int pojemniki = KlasyPojemniki.TryGetValue(kv.Key, out var p) ? p : 0;
                parts.Add($"Kl.{kv.Key}:{kv.Value:N0}%({pojemniki}poj)");
            }
            return parts.Count > 0 ? $"[Klasy: {string.Join(", ", parts)}]" : "";
        }

        public RozkladKlasWagowych Clone()
        {
            var klon = new RozkladKlasWagowych();
            foreach (var kv in KlasyProcent) klon.KlasyProcent[kv.Key] = kv.Value;
            foreach (var kv in KlasyPojemniki) klon.KlasyPojemniki[kv.Key] = kv.Value;
            return klon;
        }
    }

    public static class RezerwacjeKlasManager
    {
        public static async Task UtwsorzTabeleJesliNieIstniejeAsync(string connectionString)
        {
            await using var cn = new SqlConnection(connectionString);
            await cn.OpenAsync();
            var checkSql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RezerwacjeKlasWagowych'";
            await using var checkCmd = new SqlCommand(checkSql, cn);
            if ((int)await checkCmd.ExecuteScalarAsync() > 0) return;

            var createSql = @"CREATE TABLE [dbo].[RezerwacjeKlasWagowych] (
                [ID] INT IDENTITY(1,1) PRIMARY KEY, [ZamowienieId] INT NOT NULL,
                [DataProdukcji] DATE NOT NULL, [Klasa] INT NOT NULL, [IloscPojemnikow] INT NOT NULL,
                [Handlowiec] NVARCHAR(100) NULL, [Odbiorca] NVARCHAR(200) NULL,
                [DataRezerwacji] DATETIME DEFAULT GETDATE(), [Status] NVARCHAR(20) DEFAULT 'Aktywna');
                CREATE INDEX [IX_RezKlas_DataProd] ON [dbo].[RezerwacjeKlasWagowych] ([DataProdukcji], [Status]);
                CREATE INDEX [IX_RezKlas_ZamId] ON [dbo].[RezerwacjeKlasWagowych] ([ZamowienieId]);";
            await using var createCmd = new SqlCommand(createSql, cn);
            await createCmd.ExecuteNonQueryAsync();
        }

        public static async Task<Dictionary<int, int>> PobierzZajetoscAsync(string connectionString, DateTime dataProdukcji, int? wykluczonaZamowienieId = null)
        {
            var zajete = new Dictionary<int, int>();
            for (int i = 5; i <= 12; i++) zajete[i] = 0;
            try
            {
                await using var cn = new SqlConnection(connectionString);
                await cn.OpenAsync();
                var checkSql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RezerwacjeKlasWagowych'";
                await using var checkCmd = new SqlCommand(checkSql, cn);
                if ((int)await checkCmd.ExecuteScalarAsync() == 0) return zajete;

                var sql = @"SELECT Klasa, SUM(IloscPojemnikow) AS Zajete FROM [dbo].[RezerwacjeKlasWagowych]
                    WHERE DataProdukcji = @Data AND Status = 'Aktywna' AND (@WyklId IS NULL OR ZamowienieId != @WyklId) GROUP BY Klasa";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Data", dataProdukcji.Date);
                cmd.Parameters.AddWithValue("@WyklId", wykluczonaZamowienieId.HasValue ? wykluczonaZamowienieId.Value : (object)DBNull.Value);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync()) { int k = rd.GetInt32(0); if (k >= 5 && k <= 12) zajete[k] = rd.GetInt32(1); }
            } catch { }
            return zajete;
        }

        public static async Task ZapiszRezerwacjeAsync(string connectionString, int zamowienieId, DateTime dataProdukcji, RozkladKlasWagowych rozklad, string? handlowiec = null, string? odbiorca = null)
        {
            await using var cn = new SqlConnection(connectionString);
            await cn.OpenAsync();
            var checkSql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RezerwacjeKlasWagowych'";
            await using var checkCmd = new SqlCommand(checkSql, cn);
            if ((int)await checkCmd.ExecuteScalarAsync() == 0) await UtwsorzTabeleJesliNieIstniejeAsync(connectionString);

            await using var transaction = cn.BeginTransaction();
            try
            {
                await using var deleteCmd = new SqlCommand("DELETE FROM [dbo].[RezerwacjeKlasWagowych] WHERE ZamowienieId = @ZamId", cn, transaction);
                deleteCmd.Parameters.AddWithValue("@ZamId", zamowienieId);
                await deleteCmd.ExecuteNonQueryAsync();

                var insertSql = @"INSERT INTO [dbo].[RezerwacjeKlasWagowych] (ZamowienieId, DataProdukcji, Klasa, IloscPojemnikow, Handlowiec, Odbiorca, Status) VALUES (@ZamId, @Data, @Klasa, @Ilosc, @Hand, @Odb, 'Aktywna')";
                foreach (var kv in rozklad.KlasyPojemniki.Where(x => x.Value > 0))
                {
                    await using var insertCmd = new SqlCommand(insertSql, cn, transaction);
                    insertCmd.Parameters.AddWithValue("@ZamId", zamowienieId);
                    insertCmd.Parameters.AddWithValue("@Data", dataProdukcji.Date);
                    insertCmd.Parameters.AddWithValue("@Klasa", kv.Key);
                    insertCmd.Parameters.AddWithValue("@Ilosc", kv.Value);
                    insertCmd.Parameters.AddWithValue("@Hand", handlowiec ?? (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@Odb", odbiorca ?? (object)DBNull.Value);
                    await insertCmd.ExecuteNonQueryAsync();
                }
                await transaction.CommitAsync();
            } catch { await transaction.RollbackAsync(); throw; }
        }

        public static async Task<RozkladKlasWagowych?> PobierzRezerwacjeZamowieniaAsync(string connectionString, int zamowienieId)
        {
            try
            {
                await using var cn = new SqlConnection(connectionString);
                await cn.OpenAsync();
                var checkSql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RezerwacjeKlasWagowych'";
                await using var checkCmd = new SqlCommand(checkSql, cn);
                if ((int)await checkCmd.ExecuteScalarAsync() == 0) return null;

                var sql = @"SELECT Klasa, IloscPojemnikow FROM [dbo].[RezerwacjeKlasWagowych] WHERE ZamowienieId = @ZamId AND Status = 'Aktywna'";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@ZamId", zamowienieId);
                var rozklad = new RozkladKlasWagowych();
                bool znaleziono = false;
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync()) { int k = rd.GetInt32(0); if (k >= 5 && k <= 12) { rozklad.KlasyPojemniki[k] = rd.GetInt32(1); znaleziono = true; } }
                return znaleziono ? rozklad : null;
            } catch { return null; }
        }

        public static async Task AnulujRezerwacjeAsync(string connectionString, int zamowienieId)
        {
            try
            {
                await using var cn = new SqlConnection(connectionString);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(@"UPDATE [dbo].[RezerwacjeKlasWagowych] SET Status = 'Anulowana' WHERE ZamowienieId = @ZamId AND Status = 'Aktywna'", cn);
                cmd.Parameters.AddWithValue("@ZamId", zamowienieId);
                await cmd.ExecuteNonQueryAsync();
            } catch { }
        }

        public static async Task<List<RezerwacjaKlientaInfo>> PobierzWszystkieRezerwacjeNaDzienAsync(string connectionString, DateTime dataProdukcji)
        {
            var lista = new List<RezerwacjaKlientaInfo>();
            try
            {
                await using var cn = new SqlConnection(connectionString);
                await cn.OpenAsync();
                var checkSql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RezerwacjeKlasWagowych'";
                await using var checkCmd = new SqlCommand(checkSql, cn);
                if ((int)await checkCmd.ExecuteScalarAsync() == 0) return lista;

                var sql = @"SELECT r.ZamowienieId, r.Odbiorca, r.Handlowiec, r.Klasa, r.IloscPojemnikow FROM [dbo].[RezerwacjeKlasWagowych] r WHERE r.DataProdukcji = @Data AND r.Status = 'Aktywna' ORDER BY r.Odbiorca, r.Klasa";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Data", dataProdukcji.Date);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                    lista.Add(new RezerwacjaKlientaInfo { ZamowienieId = rd.GetInt32(0), Odbiorca = rd.IsDBNull(1) ? "Nieznany" : rd.GetString(1), Handlowiec = rd.IsDBNull(2) ? "" : rd.GetString(2), Klasa = rd.GetInt32(3), IloscPojemnikow = rd.GetInt32(4) });
            } catch { }
            return lista;
        }
    }

    public class RezerwacjaKlientaInfo { public int ZamowienieId { get; set; } public string Odbiorca { get; set; } = ""; public string Handlowiec { get; set; } = ""; public int Klasa { get; set; } public int IloscPojemnikow { get; set; } }

    public class KlasyWagoweDialog : Form
    {
        private static readonly Color COLOR_PRIMARY = Color.FromArgb(92, 138, 58);
        private static readonly Color COLOR_BG = Color.FromArgb(248, 250, 246);
        private static readonly Color COLOR_CARD = Color.White;
        private static readonly Color COLOR_BORDER = Color.FromArgb(210, 220, 200);
        private static readonly Color COLOR_TEXT = Color.FromArgb(40, 50, 35);
        private static readonly Color COLOR_TEXT_LIGHT = Color.FromArgb(100, 115, 90);
        private static readonly Color COLOR_ACCENT = Color.FromArgb(59, 130, 246);
        private static readonly Color COLOR_WARNING = Color.FromArgb(234, 88, 12);
        private static readonly Color COLOR_DANGER = Color.FromArgb(220, 38, 38);

        private static readonly Dictionary<int, Color> KLASY_KOLORY = new() {
            { 5, Color.FromArgb(220, 38, 38) }, { 6, Color.FromArgb(234, 88, 12) }, { 7, Color.FromArgb(202, 138, 4) }, { 8, Color.FromArgb(101, 163, 13) },
            { 9, Color.FromArgb(22, 163, 74) }, { 10, Color.FromArgb(8, 145, 178) }, { 11, Color.FromArgb(37, 99, 235) }, { 12, Color.FromArgb(124, 58, 237) } };

        private static readonly Dictionary<int, (string Nazwa, decimal WagaSzt)> KLASY = new() {
            { 5, ("Klasa 5 (duża)", 3.00m) }, { 6, ("Klasa 6", 2.40m) }, { 7, ("Klasa 7", 2.10m) }, { 8, ("Klasa 8", 1.87m) },
            { 9, ("Klasa 9", 1.67m) }, { 10, ("Klasa 10", 1.50m) }, { 11, ("Klasa 11", 1.36m) }, { 12, ("Klasa 12 (mała)", 1.25m) } };

        private const decimal KG_NA_POJEMNIK = 15m;
        private const int POJEMNIKOW_NA_PALECIE = 36;

        private readonly decimal _zamowioneKg;
        private readonly int _zamowionePojemnikow;
        private readonly decimal _zamowionePalet;
        private readonly DateTime _dataProdukcji;
        private readonly string _connLibra;
        private readonly int? _zamowienieId;

        private readonly Dictionary<int, TrackBar> _suwaki = new();
        private readonly Dictionary<int, TextBox> _txtProcent = new();
        private readonly Dictionary<int, TextBox> _txtPojemniki = new();
        private readonly Dictionary<int, TextBox> _txtPalety = new();
        private readonly Dictionary<int, Label> _lblWolne = new();
        private readonly Dictionary<int, Panel> _pnlPasekZajete = new();
        private readonly Dictionary<int, Panel> _pnlPasekTwoje = new();
        private readonly Dictionary<int, int> _prognozaPojemnikow = new();
        private readonly Dictionary<int, int> _zajetePojemnikow = new();

        private Label? _lblHeaderInfo;
        private Label? _lblSuma;
        private Label? _lblPodsumowanie;
        private Button? _btnOK;
        private Panel? _pnlKlasyContainer;
        private bool _blokujZmiany = false;

        public RozkladKlasWagowych Rozklad { get; private set; } = new();
        public bool Zatwierdzono { get; private set; } = false;

        public KlasyWagoweDialog(decimal zamowioneKg, DateTime dataProdukcji, string connLibra, RozkladKlasWagowych? istniejacy = null, int? zamowienieId = null)
        {
            _zamowioneKg = zamowioneKg;
            _zamowionePojemnikow = (int)Math.Ceiling(zamowioneKg / KG_NA_POJEMNIK);
            _zamowionePalet = _zamowionePojemnikow / (decimal)POJEMNIKOW_NA_PALECIE;
            _dataProdukcji = dataProdukcji;
            _connLibra = connLibra;
            _zamowienieId = zamowienieId;
            if (istniejacy != null) Rozklad = istniejacy.Clone();
            for (int i = 5; i <= 12; i++) { _prognozaPojemnikow[i] = 0; _zajetePojemnikow[i] = 0; }
            InitializeUI();
            WindowIconHelper.SetIcon(this);
            _ = LoadDaneAsync();
        }

        private void InitializeUI()
        {
            Text = "Rezerwacja Klas Wagowych Tuszki";
            Size = new Size(1100, 850);
            MinimumSize = new Size(1000, 750);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = COLOR_BG;
            Font = new Font("Segoe UI", 10f);

            // HEADER
            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 120, BackColor = COLOR_PRIMARY };
            pnlHeader.Controls.Add(new Label { Text = "Rozdzielenie Tuszki na Klasy Wagowe", Font = new Font("Segoe UI", 20f, FontStyle.Bold), ForeColor = Color.White, Location = new Point(25, 12), AutoSize = true });
            pnlHeader.Controls.Add(new Label { Text = $"Zamówiono: {_zamowioneKg:N0} kg  =  {_zamowionePojemnikow} pojemników  =  {_zamowionePalet:N2} palet", Font = new Font("Segoe UI", 13f), ForeColor = Color.FromArgb(220, 240, 210), Location = new Point(25, 50), AutoSize = true });
            _lblHeaderInfo = new Label { Text = $"Data produkcji: {_dataProdukcji:dd.MM.yyyy (dddd)} — Ładuję...", Font = new Font("Segoe UI", 11f), ForeColor = Color.FromArgb(200, 230, 190), Location = new Point(25, 80), AutoSize = true };
            pnlHeader.Controls.Add(_lblHeaderInfo);

            var btnWidokZbiorczy = new Button { Text = "📊 Pokaż wszystkich klientów dnia", Size = new Size(280, 40), Location = new Point(780, 65), BackColor = Color.FromArgb(70, 110, 45), ForeColor = Color.White, Font = new Font("Segoe UI", 10f, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnWidokZbiorczy.FlatAppearance.BorderSize = 0;
            btnWidokZbiorczy.Click += BtnWidokZbiorczy_Click;
            pnlHeader.Controls.Add(btnWidokZbiorczy);

            // PRZYCISKI SZYBKIE
            var pnlPrzyciski = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.White, Padding = new Padding(20, 10, 20, 10) };
            pnlPrzyciski.Paint += (s, e) => { using var pen = new Pen(COLOR_BORDER, 1); e.Graphics.DrawLine(pen, 0, pnlPrzyciski.Height - 1, pnlPrzyciski.Width, pnlPrzyciski.Height - 1); };

            int btnX = 15;
            var btnRowno = CreateQuickButton("⚖️ Równo", COLOR_TEXT_LIGHT, btnX, 100); btnX += 110; btnRowno.Click += (s, e) => RozdzielRowno();
            var btnDuze = CreateQuickButton("🔴 Duże 5-8", KLASY_KOLORY[5], btnX, 110); btnX += 120; btnDuze.Click += (s, e) => RozdzielGrupa(5, 8);
            var btnMale = CreateQuickButton("🔵 Małe 9-12", KLASY_KOLORY[11], btnX, 115); btnX += 125; btnMale.Click += (s, e) => RozdzielGrupa(9, 12);
            var btnSrodek = CreateQuickButton("🟢 Środek 7-10", KLASY_KOLORY[9], btnX, 125); btnX += 135; btnSrodek.Click += (s, e) => RozdzielGrupa(7, 10);
            var btnWgWolnych = CreateQuickButton("📊 Wg dostępności", Color.FromArgb(124, 58, 237), btnX, 145); btnX += 155; btnWgWolnych.Click += (s, e) => RozdzielWgDostepnosci();
            var btn1Paleta = CreateQuickButton("1 paleta", COLOR_ACCENT, btnX, 85); btnX += 95; btn1Paleta.Click += (s, e) => UstawPalety(1);
            var btn05Palety = CreateQuickButton("½ palety", COLOR_ACCENT, btnX, 85); btnX += 95; btn05Palety.Click += (s, e) => UstawPalety(0.5m);
            var btnWyczysc = CreateQuickButton("🗑️ Wyczyść", COLOR_DANGER, btnX, 100); btnWyczysc.Click += (s, e) => WyczyscWszystko();
            pnlPrzyciski.Controls.AddRange(new Control[] { btnRowno, btnDuze, btnMale, btnSrodek, btnWgWolnych, btn1Paleta, btn05Palety, btnWyczysc });

            // NAGŁÓWEK TABELI
            var pnlNaglowek = new Panel { Dock = DockStyle.Top, Height = 35, BackColor = Color.FromArgb(60, 85, 45) };
            var headers = new[] { ("KLASA", 15, 120), ("SUWAK (przeciągnij)", 140, 280), ("%", 430, 50), ("POJ.", 490, 60), ("PALETY", 560, 70), ("DOSTĘPNOŚĆ", 640, 200), ("WOLNE", 850, 80) };
            foreach (var (text, x, w) in headers) pnlNaglowek.Controls.Add(new Label { Text = text, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.White, Location = new Point(x, 9), Size = new Size(w, 20) });

            // KONTENER NA KLASY
            _pnlKlasyContainer = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = COLOR_BG, Padding = new Padding(10) };

            // FOOTER
            var pnlFooter = new Panel { Dock = DockStyle.Bottom, Height = 100, BackColor = Color.White };
            pnlFooter.Paint += (s, e) => { using var pen = new Pen(COLOR_BORDER, 2); e.Graphics.DrawLine(pen, 0, 0, pnlFooter.Width, 0); };
            _lblSuma = new Label { Text = "SUMA: 0%  |  0 pojemników  |  0.00 palet", Font = new Font("Segoe UI", 16f, FontStyle.Bold), ForeColor = COLOR_TEXT_LIGHT, Location = new Point(25, 18), AutoSize = true };
            _lblPodsumowanie = new Label { Text = "Przeciągnij suwaki lub wpisz wartości", Font = new Font("Segoe UI", 11f), ForeColor = COLOR_TEXT_LIGHT, Location = new Point(25, 55), AutoSize = true };
            _btnOK = new Button { Text = "✅ ZATWIERDŹ REZERWACJĘ", Size = new Size(280, 60), Location = new Point(780, 20), BackColor = COLOR_TEXT_LIGHT, ForeColor = Color.White, Font = new Font("Segoe UI", 13f, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Enabled = false, Cursor = Cursors.Hand };
            _btnOK.FlatAppearance.BorderSize = 0;
            _btnOK.Click += (s, e) => { Zatwierdzono = true; DialogResult = DialogResult.OK; Close(); };
            var btnAnuluj = new Button { Text = "Anuluj", Size = new Size(100, 60), Location = new Point(670, 20), BackColor = Color.FromArgb(240, 240, 240), ForeColor = COLOR_TEXT, Font = new Font("Segoe UI", 11f), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, DialogResult = DialogResult.Cancel };
            btnAnuluj.FlatAppearance.BorderSize = 1; btnAnuluj.FlatAppearance.BorderColor = COLOR_BORDER;
            pnlFooter.Controls.AddRange(new Control[] { _lblSuma, _lblPodsumowanie, _btnOK, btnAnuluj });

            Controls.Add(_pnlKlasyContainer);
            Controls.Add(pnlNaglowek);
            Controls.Add(pnlPrzyciski);
            Controls.Add(pnlHeader);
            Controls.Add(pnlFooter);

            BuildKlasyRows();
        }

        private void BuildKlasyRows()
        {
            if (_pnlKlasyContainer == null) return;
            int y = 5;
            foreach (int klasa in KLASY.Keys.OrderBy(k => k)) { _pnlKlasyContainer.Controls.Add(CreateKlasaRow(klasa, y)); y += 70; }
        }

        private Panel CreateKlasaRow(int klasa, int y)
        {
            var info = KLASY[klasa];
            var kolor = KLASY_KOLORY[klasa];
            int istniejacePoj = Rozklad.KlasyPojemniki.GetValueOrDefault(klasa, 0);
            int istniejacyProcent = _zamowionePojemnikow > 0 ? (int)Math.Round(istniejacePoj * 100m / _zamowionePojemnikow) : 0;

            var pnl = new Panel { Location = new Point(5, y), Size = new Size(950, 65), BackColor = COLOR_CARD };
            pnl.Paint += (s, e) => { using var pen = new Pen(COLOR_BORDER, 1); e.Graphics.DrawRectangle(pen, 0, 0, pnl.Width - 1, pnl.Height - 1); using var brush = new SolidBrush(kolor); e.Graphics.FillRectangle(brush, 0, 0, 8, pnl.Height); };

            pnl.Controls.Add(new Label { Text = info.Nazwa, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = kolor, Location = new Point(15, 8), AutoSize = true });
            pnl.Controls.Add(new Label { Text = $"{info.WagaSzt:N2} kg/szt", Font = new Font("Segoe UI", 8f), ForeColor = COLOR_TEXT_LIGHT, Location = new Point(15, 32), AutoSize = true });

            var suwak = new TrackBar { Location = new Point(135, 15), Size = new Size(280, 40), Minimum = 0, Maximum = 100, Value = Math.Min(istniejacyProcent, 100), TickFrequency = 10, LargeChange = 10, SmallChange = 5 };
            suwak.ValueChanged += (s, e) => OnSuwakChanged(klasa);
            _suwaki[klasa] = suwak;
            pnl.Controls.Add(suwak);

            var txtProcent = new TextBox { Location = new Point(430, 18), Size = new Size(45, 28), Font = new Font("Segoe UI", 10f, FontStyle.Bold), Text = istniejacyProcent.ToString(), TextAlign = HorizontalAlignment.Center };
            txtProcent.Leave += (s, e) => OnTxtProcentChanged(klasa);
            txtProcent.KeyPress += TxtNumeric_KeyPress;
            _txtProcent[klasa] = txtProcent;
            pnl.Controls.Add(txtProcent);
            pnl.Controls.Add(new Label { Text = "%", Location = new Point(478, 22), AutoSize = true, ForeColor = COLOR_TEXT_LIGHT });

            var txtPojemniki = new TextBox { Location = new Point(495, 18), Size = new Size(55, 28), Font = new Font("Segoe UI", 10f), Text = istniejacePoj.ToString(), TextAlign = HorizontalAlignment.Center };
            txtPojemniki.Leave += (s, e) => OnTxtPojemnikiChanged(klasa);
            txtPojemniki.KeyPress += TxtNumeric_KeyPress;
            _txtPojemniki[klasa] = txtPojemniki;
            pnl.Controls.Add(txtPojemniki);

            decimal istniejacePalety = istniejacePoj / (decimal)POJEMNIKOW_NA_PALECIE;
            var txtPalety = new TextBox { Location = new Point(560, 18), Size = new Size(55, 28), Font = new Font("Segoe UI", 10f), Text = istniejacePalety.ToString("N2"), TextAlign = HorizontalAlignment.Center };
            txtPalety.Leave += (s, e) => OnTxtPaletyChanged(klasa);
            _txtPalety[klasa] = txtPalety;
            pnl.Controls.Add(txtPalety);
            pnl.Controls.Add(new Label { Text = "pal", Location = new Point(618, 22), AutoSize = true, ForeColor = COLOR_TEXT_LIGHT, Font = new Font("Segoe UI", 8f) });

            var pnlPasekBg = new Panel { Location = new Point(650, 20), Size = new Size(180, 25), BackColor = Color.FromArgb(230, 235, 225) };
            var pnlZajete = new Panel { Location = new Point(0, 0), Size = new Size(0, 25), BackColor = Color.FromArgb(254, 202, 202) };
            _pnlPasekZajete[klasa] = pnlZajete;
            pnlPasekBg.Controls.Add(pnlZajete);
            var pnlTwoje = new Panel { Location = new Point(0, 0), Size = new Size(0, 25), BackColor = kolor };
            _pnlPasekTwoje[klasa] = pnlTwoje;
            pnlPasekBg.Controls.Add(pnlTwoje);
            pnl.Controls.Add(pnlPasekBg);

            var lblWolne = new Label { Text = "?", Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = COLOR_PRIMARY, Location = new Point(840, 20), Size = new Size(80, 25), TextAlign = ContentAlignment.MiddleLeft };
            _lblWolne[klasa] = lblWolne;
            pnl.Controls.Add(lblWolne);

            return pnl;
        }

        private Button CreateQuickButton(string text, Color color, int x, int width)
        {
            var btn = new Button { Text = text, Size = new Size(width, 38), Location = new Point(x, 10), BackColor = color, ForeColor = Color.White, Font = new Font("Segoe UI", 9f, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void TxtNumeric_KeyPress(object? sender, KeyPressEventArgs e) { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != ',' && e.KeyChar != '.') e.Handled = true; }

        private void OnSuwakChanged(int klasa)
        {
            if (_blokujZmiany) return;
            _blokujZmiany = true;
            try
            {
                int procent = _suwaki[klasa].Value;
                int pojemniki = (int)Math.Round(_zamowionePojemnikow * procent / 100m);
                decimal palety = pojemniki / (decimal)POJEMNIKOW_NA_PALECIE;
                Rozklad.KlasyProcent[klasa] = procent;
                Rozklad.KlasyPojemniki[klasa] = pojemniki;
                _txtProcent[klasa].Text = procent.ToString();
                _txtPojemniki[klasa].Text = pojemniki.ToString();
                _txtPalety[klasa].Text = palety.ToString("N2");
                AktualizujPasek(klasa);
                AktualizujSume();
            }
            finally { _blokujZmiany = false; }
        }

        private void OnTxtProcentChanged(int klasa)
        {
            if (_blokujZmiany) return;
            _blokujZmiany = true;
            try
            {
                if (int.TryParse(_txtProcent[klasa].Text, out int procent))
                {
                    procent = Math.Max(0, Math.Min(100, procent));
                    int pojemniki = (int)Math.Round(_zamowionePojemnikow * procent / 100m);
                    decimal palety = pojemniki / (decimal)POJEMNIKOW_NA_PALECIE;
                    Rozklad.KlasyProcent[klasa] = procent;
                    Rozklad.KlasyPojemniki[klasa] = pojemniki;
                    _suwaki[klasa].Value = procent;
                    _txtPojemniki[klasa].Text = pojemniki.ToString();
                    _txtPalety[klasa].Text = palety.ToString("N2");
                    AktualizujPasek(klasa);
                    AktualizujSume();
                }
            }
            finally { _blokujZmiany = false; }
        }

        private void OnTxtPojemnikiChanged(int klasa)
        {
            if (_blokujZmiany) return;
            _blokujZmiany = true;
            try
            {
                if (int.TryParse(_txtPojemniki[klasa].Text, out int pojemniki))
                {
                    pojemniki = Math.Max(0, pojemniki);
                    int procent = _zamowionePojemnikow > 0 ? (int)Math.Round(pojemniki * 100m / _zamowionePojemnikow) : 0;
                    decimal palety = pojemniki / (decimal)POJEMNIKOW_NA_PALECIE;
                    Rozklad.KlasyProcent[klasa] = procent;
                    Rozklad.KlasyPojemniki[klasa] = pojemniki;
                    _suwaki[klasa].Value = Math.Min(procent, 100);
                    _txtProcent[klasa].Text = procent.ToString();
                    _txtPalety[klasa].Text = palety.ToString("N2");
                    AktualizujPasek(klasa);
                    AktualizujSume();
                }
            }
            finally { _blokujZmiany = false; }
        }

        private void OnTxtPaletyChanged(int klasa)
        {
            if (_blokujZmiany) return;
            _blokujZmiany = true;
            try
            {
                string text = _txtPalety[klasa].Text.Replace('.', ',');
                if (decimal.TryParse(text, out decimal palety))
                {
                    palety = Math.Max(0, palety);
                    int pojemniki = (int)Math.Round(palety * POJEMNIKOW_NA_PALECIE);
                    int procent = _zamowionePojemnikow > 0 ? (int)Math.Round(pojemniki * 100m / _zamowionePojemnikow) : 0;
                    Rozklad.KlasyProcent[klasa] = procent;
                    Rozklad.KlasyPojemniki[klasa] = pojemniki;
                    _suwaki[klasa].Value = Math.Min(procent, 100);
                    _txtProcent[klasa].Text = procent.ToString();
                    _txtPojemniki[klasa].Text = pojemniki.ToString();
                    AktualizujPasek(klasa);
                    AktualizujSume();
                }
            }
            finally { _blokujZmiany = false; }
        }

        private void AktualizujPasek(int klasa)
        {
            int prognoza = _prognozaPojemnikow.GetValueOrDefault(klasa, 0);
            int zajete = _zajetePojemnikow.GetValueOrDefault(klasa, 0);
            int twoje = Rozklad.KlasyPojemniki.GetValueOrDefault(klasa, 0);
            int wolne = Math.Max(0, prognoza - zajete);

            if (_pnlPasekZajete.TryGetValue(klasa, out var pnlZ) && _pnlPasekTwoje.TryGetValue(klasa, out var pnlT))
            {
                int maxWidth = 178;
                if (prognoza > 0)
                {
                    int zajeteSzer = Math.Min((int)(zajete * maxWidth / (decimal)prognoza), maxWidth);
                    int twojeSzer = Math.Min((int)(twoje * maxWidth / (decimal)prognoza), maxWidth - zajeteSzer);
                    pnlZ.Size = new Size(zajeteSzer, 25);
                    pnlT.Location = new Point(zajeteSzer, 0);
                    pnlT.Size = new Size(twojeSzer, 25);
                }
                else { pnlZ.Size = new Size(0, 25); pnlT.Size = new Size(0, 25); }
            }

            if (_lblWolne.TryGetValue(klasa, out var lbl))
            {
                int pozostalo = wolne - twoje;
                if (prognoza == 0) { lbl.Text = "brak"; lbl.ForeColor = COLOR_TEXT_LIGHT; }
                else if (pozostalo < 0) { lbl.Text = $"{pozostalo}!"; lbl.ForeColor = COLOR_DANGER; }
                else { lbl.Text = $"{pozostalo} poj"; lbl.ForeColor = pozostalo > 0 ? COLOR_PRIMARY : COLOR_WARNING; }
            }
        }

        private void AktualizujSume()
        {
            int sumaPoj = Rozklad.KlasyPojemniki.Values.Sum();
            decimal sumaProcent = _zamowionePojemnikow > 0 ? sumaPoj * 100m / _zamowionePojemnikow : 0;
            decimal sumaPalet = sumaPoj / (decimal)POJEMNIKOW_NA_PALECIE;
            decimal sumaKg = sumaPoj * KG_NA_POJEMNIK;

            if (_lblSuma != null)
            {
                _lblSuma.Text = $"SUMA: {sumaProcent:N0}%  |  {sumaPoj} poj.  |  {sumaPalet:N2} palet  |  {sumaKg:N0} kg";
                _lblSuma.ForeColor = sumaPoj == 0 ? COLOR_TEXT_LIGHT : (sumaProcent >= 98 && sumaProcent <= 102) ? COLOR_PRIMARY : sumaProcent > 102 ? COLOR_WARNING : COLOR_ACCENT;
            }

            if (_lblPodsumowanie != null)
            {
                int roznica = sumaPoj - _zamowionePojemnikow;
                if (sumaPoj == 0) { _lblPodsumowanie.Text = "Przeciągnij suwaki lub wpisz wartości"; _lblPodsumowanie.ForeColor = COLOR_TEXT_LIGHT; }
                else if (roznica == 0) { _lblPodsumowanie.Text = "✅ Dokładnie tyle ile zamówiono!"; _lblPodsumowanie.ForeColor = COLOR_PRIMARY; }
                else if (roznica > 0) { _lblPodsumowanie.Text = $"⚠️ O {roznica} pojemników więcej"; _lblPodsumowanie.ForeColor = COLOR_WARNING; }
                else { _lblPodsumowanie.Text = $"📦 Brakuje jeszcze {-roznica} pojemników"; _lblPodsumowanie.ForeColor = COLOR_ACCENT; }
            }

            if (_btnOK != null) { _btnOK.Enabled = sumaPoj > 0; _btnOK.BackColor = sumaPoj > 0 ? COLOR_PRIMARY : COLOR_TEXT_LIGHT; }
        }

        private void RozdzielRowno() { int naKlase = 100 / 8; int reszta = 100 % 8; int i = 0; foreach (int k in KLASY.Keys.OrderBy(k => k)) _suwaki[k].Value = naKlase + (i++ < reszta ? 1 : 0); }
        private void RozdzielGrupa(int od, int doK) { WyczyscWszystko(); int ileKlas = doK - od + 1; int naKlase = 100 / ileKlas; int reszta = 100 % ileKlas; int i = 0; for (int k = od; k <= doK; k++) if (_suwaki.TryGetValue(k, out var s)) s.Value = naKlase + (i++ < reszta ? 1 : 0); }
        private void RozdzielWgDostepnosci()
        {
            WyczyscWszystko();
            var wolne = new Dictionary<int, int>();
            int sumaWolne = 0;
            foreach (int k in KLASY.Keys) { int w = Math.Max(0, _prognozaPojemnikow.GetValueOrDefault(k, 0) - _zajetePojemnikow.GetValueOrDefault(k, 0)); wolne[k] = w; sumaWolne += w; }
            if (sumaWolne == 0) { MessageBox.Show(this, "Brak wolnych miejsc!", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            foreach (var kvp in wolne) { int procent = (int)Math.Round(kvp.Value * 100m / sumaWolne); if (_suwaki.TryGetValue(kvp.Key, out var s)) s.Value = Math.Min(100, procent); }
        }
        private void UstawPalety(decimal ilePalet)
        {
            WyczyscWszystko();
            int pojemniki = (int)Math.Round(ilePalet * POJEMNIKOW_NA_PALECIE);
            int procent = _zamowionePojemnikow > 0 ? (int)Math.Round(pojemniki * 100m / _zamowionePojemnikow) : 0;
            foreach (int k in KLASY.Keys.OrderBy(k => k)) { int wolne = Math.Max(0, _prognozaPojemnikow.GetValueOrDefault(k, 0) - _zajetePojemnikow.GetValueOrDefault(k, 0)); if (wolne >= pojemniki || wolne > 0) { _suwaki[k].Value = Math.Min(100, procent); break; } }
        }
        private void WyczyscWszystko()
        {
            _blokujZmiany = true;
            foreach (var s in _suwaki.Values) s.Value = 0;
            foreach (var t in _txtProcent.Values) t.Text = "0";
            foreach (var t in _txtPojemniki.Values) t.Text = "0";
            foreach (var t in _txtPalety.Values) t.Text = "0,00";
            foreach (int k in KLASY.Keys) { Rozklad.KlasyProcent[k] = 0; Rozklad.KlasyPojemniki[k] = 0; AktualizujPasek(k); }
            _blokujZmiany = false;
            AktualizujSume();
        }

        private void BtnWidokZbiorczy_Click(object? sender, EventArgs e)
        {
            using var widok = new WidokKlasWagowychDnia(_dataProdukcji, _connLibra, _prognozaPojemnikow);
            widok.ShowDialog(this);
        }

        private async Task LoadDaneAsync()
        {
            try
            {
                await RezerwacjeKlasManager.UtwsorzTabeleJesliNieIstniejeAsync(_connLibra);
                await LoadPrognozaAsync();
                var zajete = await RezerwacjeKlasManager.PobierzZajetoscAsync(_connLibra, _dataProdukcji, _zamowienieId);
                foreach (var kv in zajete) _zajetePojemnikow[kv.Key] = kv.Value;

                this.Invoke(() =>
                {
                    int sumaPrognoza = _prognozaPojemnikow.Values.Sum();
                    int sumaZajete = _zajetePojemnikow.Values.Sum();
                    int wolne = sumaPrognoza - sumaZajete;
                    if (sumaPrognoza > 0) { _lblHeaderInfo!.Text = $"Prognoza: {sumaPrognoza} poj. ({sumaPrognoza / 36m:N1} pal)  |  Zajęte: {sumaZajete}  |  Wolne: {wolne}"; _lblHeaderInfo.ForeColor = wolne >= _zamowionePojemnikow ? Color.FromArgb(200, 255, 200) : Color.FromArgb(255, 220, 180); }
                    else { _lblHeaderInfo!.Text = "Brak danych prognozy - wprowadź ręcznie"; _lblHeaderInfo.ForeColor = Color.FromArgb(255, 220, 180); }
                    foreach (int k in KLASY.Keys) AktualizujPasek(k);
                    AktualizujSume();
                });
            }
            catch (Exception ex) { this.Invoke(() => { _lblHeaderInfo!.Text = $"Błąd: {ex.Message.Substring(0, Math.Min(60, ex.Message.Length))}"; _lblHeaderInfo.ForeColor = Color.FromArgb(255, 200, 200); }); }
        }

        private async Task LoadPrognozaAsync()
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                var checkSql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'In0E'";
                await using var checkCmd = new SqlCommand(checkSql, cn);
                if ((int)await checkCmd.ExecuteScalarAsync() == 0) return;

                var sql = @"SELECT QntInCont AS Klasa, SUM(Quantity) / 3 AS Srednia FROM [dbo].[In0E] WHERE ArticleID = 40 AND QntInCont BETWEEN 5 AND 12 AND CreateData >= DATEADD(week, -3, @Data) AND CreateData < @Data AND DATEPART(WEEKDAY, CreateData) = DATEPART(WEEKDAY, @Data) GROUP BY QntInCont";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Data", _dataProdukcji.Date);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync()) { int klasa = Convert.ToInt32(rd["Klasa"]); int srednia = rd.IsDBNull(1) ? 0 : Convert.ToInt32(rd["Srednia"]); _prognozaPojemnikow[klasa] = srednia; }
            }
            catch { }
        }
    }
}
