// Plik: AnkietaPotwierdzoneForm.cs (full screen + węższe karty + mniejsze Notatki + DatePicker z obliczaną datą)
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient; // Jeśli kompilator nie widzi tych typów – doinstaluj pakiet System.Data.SqlClient
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Kalendarz1
{
    /// <summary>
    /// Jedno okno z przewijaniem, kafelek per dostawa:
    /// - Skale: Cena / Transport / Komunikacja / Elastyczność
    /// - Opcje: "Nie wiem", "Kontrakt", 1..5 (radio -> tylko jedna z opcji)
    /// - Notatka (opcjonalnie) — MNIEJSZA
    /// - "Zapisz" (UPSERT per DostawaLp + UserID; oceny mogą być NULL)
    /// - NA GÓRZE: DateTimePicker z automatycznie wyliczoną datą
    ///   (pierwszy dzień wprzód od jutra z potwierdzonymi dostawami).
    /// - Okno startuje w pełnym ekranie (bez ramek); Esc zamyka formularz.
    /// </summary>
    public partial class AnkietaPotwierdzoneForm : Form
    {
        private readonly string _connectionString;
        private readonly string _userId;
        private DateTime _day; // aktualnie wybrany dzień
        private List<DeliverySurveyItem> _deliveries = new(); // aktualnie wczytane

        private Panel _scrollHost = null!;
        private FlowLayoutPanel _flow = null!;
        private Label _lblInfo = null!;
        private DateTimePicker _dtp = null!;

        public AnkietaPotwierdzoneForm(
            string connectionString,
            string userId,
            DateTime day,
            List<DeliverySurveyItem> deliveries /* nieużywane w nowej logice; zawsze liczymy datę */)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _userId = string.IsNullOrWhiteSpace(userId) ? Environment.UserName : userId;
            _day = day.Date;

            Text = "Ankieta rozmów";
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f);

            // ZMIANA: Ustawienie okna na tryb zmaksymalizowany (pełny ekran z widocznym paskiem zadań)
            WindowState = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.Sizable;

            // Esc => wyjście z pełnego ekranu (zamknięcie formularza)
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };

            BuildUi();
            Load += OnLoadAsync;
        }

        private async void OnLoadAsync(object? sender, EventArgs e)
        {
            // Zawsze obliczamy datę od jutra wprzód
            var computed = await FindFirstFutureDayWithConfirmedDeliveriesAsync(DateTime.Today.AddDays(1));
            _day = computed ?? DateTime.Today;
            _dtp.ValueChanged -= OnDateChanged;
            _dtp.Value = _day;
            _dtp.ValueChanged += OnDateChanged;
            await ReloadForDateAsync(_day);
        }

        private void BuildUi()
        {
            BackColor = Color.White;

            // Pasek górny: DatePicker + info
            var top = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = Color.FromArgb(245, 247, 250)
            };

            var lblDate = new Label
            {
                Left = 12,
                Top = 9,
                AutoSize = true,
                Text = "Dzień:",
                Font = new Font("Segoe UI", 10f, FontStyle.Regular)
            };

            _dtp = new DateTimePicker
            {
                Left = 62,
                Top = 5,
                Width = 140,
                Format = DateTimePickerFormat.Short
            };
            _dtp.ValueChanged += OnDateChanged;

            _lblInfo = new Label
            {
                Left = 270,
                Top = 9,
                AutoSize = true,
                ForeColor = Color.FromArgb(80, 90, 100),
                Text = "Wczytywanie…"
            };

            top.Controls.Add(lblDate);
            top.Controls.Add(_dtp);
            top.Controls.Add(_lblInfo);
            Controls.Add(top);

            // Obszar scroll
            _scrollHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(10),
                BackColor = Color.White
            };
            _flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = false,
                WrapContents = true,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(0, 20, 0, 0),
                Margin = new Padding(0)
            };
            _scrollHost.Controls.Add(_flow);
            Controls.Add(_scrollHost);
        }

        private void OnDateChanged(object? sender, EventArgs e)
        {
            _day = _dtp.Value.Date;
            _ = ReloadForDateAsync(_day);
        }

        private async System.Threading.Tasks.Task ReloadForDateAsync(DateTime day)
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                _deliveries = LoadDeliveriesForDay(day);
            });

            Text = $"Ankieta rozmów • {day:yyyy-MM-dd ddd}";
            _lblInfo.Text = _deliveries.Count == 0
                ? "Brak potwierdzonych dostaw do oceny dla wybranej daty."
                : $"Dostawy do oceny: {_deliveries.Count} (użytkownik: {_userId})";

            _flow.SuspendLayout();
            _flow.Controls.Clear();
            foreach (var d in _deliveries
                                 .OrderByDescending(x => x.WagaDek)
                                 .ThenByDescending(x => x.Auta)
                                 .ThenBy(x => x.Dostawca))
            {
                _flow.Controls.Add(BuildCard(d));
            }
            _flow.ResumeLayout();
        }

        private Control BuildCard(DeliverySurveyItem d)
        {
            var card = new Panel
            {
                Width = 450,
                Height = 285,
                Margin = new Padding(6),
                Padding = new Padding(10),
                BackColor = Color.FromArgb(248, 250, 255),
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblDostawca = new Label
            {
                Left = 8,
                Top = 20,
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 10f),
                Text = d.Dostawca
            };
            card.Controls.Add(lblDostawca);

            var detailsParts = new List<string>();
            if (d.Auta.HasValue) detailsParts.Add($"Auta: {d.Auta}");
            if (d.SztukiDek.HasValue) detailsParts.Add($"{d.SztukiDek:N0} szt");
            if (d.WagaDek.HasValue) detailsParts.Add($"{d.WagaDek:0.00} kg");
            if (d.Cena.HasValue) detailsParts.Add($"{d.Cena:0.00} zł/kg");
            var detailsText = string.Join("  •  ", detailsParts);

            var lblDetails = new Label
            {
                Left = lblDostawca.Right + 8,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(64, 64, 64),
                Text = detailsText
            };
            lblDetails.Top = lblDostawca.Top + (lblDostawca.Height - lblDetails.Height) / 2;
            card.Controls.Add(lblDetails);

            var grpCena = MakeGradeGroup("Cena", 36);
            var grpTransport = MakeGradeGroup("Transport", 86);
            var grpKomunikacja = MakeGradeGroup("Komunikacja", 136);
            var grpElastycznosc = MakeGradeGroup("Elastyczność", 186);

            var txtNote = new TextBox
            {
                Left = 8,
                Top = 235,
                Width = 340,
                Height = 40,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
#if NET6_0_OR_GREATER
                ,
                PlaceholderText = "Notatka (opcjonalnie)…"
#endif
            };

            var btnSave = new Button
            {
                Left = 356,
                Top = 247,
                Width = 80,
                Height = 28,
                Text = "Zapisz",
                BackColor = Color.FromArgb(220, 240, 220),
                FlatStyle = FlatStyle.Flat
            };
            btnSave.FlatAppearance.BorderColor = Color.FromArgb(140, 200, 140);

            btnSave.Click += (s, e) =>
            {
                try
                {
                    int? ocCena = grpCena.Picker.GetScoreOrNull();
                    int? ocTrans = grpTransport.Picker.GetScoreOrNull();
                    int? ocKom = grpKomunikacja.Picker.GetScoreOrNull();
                    int? ocElas = grpElastycznosc.Picker.GetScoreOrNull();

                    UpsertFeedback(
                        d.Lp, _day, _userId,
                        ocCena, ocTrans, ocKom, ocElas,
                        NullIfEmpty(txtNote.Text));

                    card.BackColor = Color.FromArgb(225, 245, 225);
                    btnSave.Text = "Modyfikuj";
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd zapisu: " + ex.Message, "Błąd",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            grpCena.Picker.SetUnknownDefault();
            grpTransport.Picker.SetUnknownDefault();
            grpKomunikacja.Picker.SetUnknownDefault();
            grpElastycznosc.Picker.SetUnknownDefault();

            TryInitFromDb(d.Lp, _userId, grpCena.Picker, grpTransport.Picker,
                            grpKomunikacja.Picker, grpElastycznosc.Picker, txtNote, card, btnSave);

            card.Controls.Add(grpCena.Host);
            card.Controls.Add(grpTransport.Host);
            card.Controls.Add(grpKomunikacja.Host);
            card.Controls.Add(grpElastycznosc.Host);
            card.Controls.Add(txtNote);
            card.Controls.Add(btnSave);

            return card;
        }


        // ===== helpers UI / DB =====

        private static string? NullIfEmpty(string? s)
            => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private (GroupBox Host, GradePicker Picker) MakeGradeGroup(string title, int top)
        {
            var gb = new GroupBox
            {
                Left = 8,
                Top = top,
                Width = 420,
                Height = 44,
                Text = title,
                RightToLeft = RightToLeft.Yes
            };
            var picker = new GradePicker(gb);
            return (gb, picker);
        }

        private void UpsertFeedback(
            int dostawaLp,
            DateTime dataDostawy,
            string userId,
            int? ocCena,
            int? ocTransport,
            int? ocKomunikacja,
            int? ocElastycznosc,
            string? notatka)
        {
            const string sql = @"
DECLARE @now datetime2(3) = SYSUTCDATETIME();  -- lub GETDATE()

IF EXISTS (SELECT 1 FROM dbo.DostawaFeedback WHERE DostawaLp=@Lp AND Kto=@Kto)
BEGIN
    UPDATE dbo.DostawaFeedback
    SET DataDostawy       = @Data,
        DataAnkiety       = @now,
        OcenaCena         = @OcCena,
        OcenaTransport    = @OcTrans,
        OcenaKomunikacja  = @OcKom,
        OcenaElastycznosc = @OcElas,
        Notatka           = @Notatka
    WHERE DostawaLp=@Lp AND Kto=@Kto;
END
ELSE
BEGIN
    INSERT INTO dbo.DostawaFeedback
        (DostawaLp, DataDostawy, DataAnkiety, Kto,
         OcenaCena, OcenaTransport, OcenaKomunikacja, OcenaElastycznosc, Notatka)
    VALUES
        (@Lp, @Data, @now, @Kto,
         @OcCena, @OcTrans, @OcKom, @OcElas, @Notatka);
END";

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.Add("@Lp", SqlDbType.Int).Value = dostawaLp;
            cmd.Parameters.Add("@Data", SqlDbType.Date).Value = dataDostawy;
            cmd.Parameters.Add("@Kto", SqlDbType.NVarChar, 64).Value = (object)userId ?? DBNull.Value;
            cmd.Parameters.Add("@OcCena", SqlDbType.TinyInt).Value = (object?)ocCena ?? DBNull.Value;
            cmd.Parameters.Add("@OcTrans", SqlDbType.TinyInt).Value = (object?)ocTransport ?? DBNull.Value;
            cmd.Parameters.Add("@OcKom", SqlDbType.TinyInt).Value = (object?)ocKomunikacja ?? DBNull.Value;
            cmd.Parameters.Add("@OcElas", SqlDbType.TinyInt).Value = (object?)ocElastycznosc ?? DBNull.Value;
            cmd.Parameters.Add("@Notatka", SqlDbType.NVarChar, 1000).Value = (object?)notatka ?? DBNull.Value;

            conn.Open();
            cmd.ExecuteNonQuery();
        }

        private List<DeliverySurveyItem> LoadDeliveriesForDay(DateTime day)
        {
            var list = new List<DeliverySurveyItem>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand(@"
SELECT Lp, CAST(DataOdbioru AS date) AS DataOdbioru, Dostawca, SztukiDek, Auta, WagaDek, TypCeny, Cena
FROM dbo.HarmonogramDostaw
WHERE CAST(DataOdbioru AS date) = @d
  AND Bufor = 'Potwierdzony'
ORDER BY WagaDek DESC, Auta DESC, Dostawca ASC;", conn);
            cmd.Parameters.Add("@d", SqlDbType.Date).Value = day.Date;
            cmd.Parameters.Add("@Kto", SqlDbType.NVarChar, 64).Value = _userId;

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new DeliverySurveyItem
                {
                    Lp = Convert.ToInt32(r["Lp"]),
                    DataOdbioru = Convert.ToDateTime(r["DataOdbioru"]),
                    Dostawca = Convert.ToString(r["Dostawca"]),
                    SztukiDek = r["SztukiDek"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["SztukiDek"]),
                    Auta = r["Auta"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["Auta"]),
                    WagaDek = r["WagaDek"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["WagaDek"]),
                    TypCeny = r["TypCeny"] == DBNull.Value ? null : Convert.ToString(r["TypCeny"]),
                    Cena = r["Cena"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["Cena"])
                });
            }
            return list;
        }

        private System.Threading.Tasks.Task<DateTime?> FindFirstFutureDayWithConfirmedDeliveriesAsync(DateTime startExclusive)
        {
            return System.Threading.Tasks.Task.Run<DateTime?>(() =>
            {
                var probe = startExclusive.Date;
                for (int i = 0; i < 60; i++)
                {
                    probe = probe.AddDays(1);
                    if (HasConfirmedDeliveriesForDay(probe))
                        return (DateTime?)probe;
                }
                return null;
            });
        }

        private bool HasConfirmedDeliveriesForDay(DateTime day)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand(@"
SELECT TOP 1 1
FROM dbo.HarmonogramDostaw h
WHERE CAST(h.DataOdbioru AS date) = @d
  AND h.Bufor = 'Potwierdzony'
;", conn);
            cmd.Parameters.Add("@d", SqlDbType.Date).Value = day.Date;
            cmd.Parameters.Add("@Kto", SqlDbType.NVarChar, 64).Value = _userId;

            var o = cmd.ExecuteScalar();
            return o != null;
        }

        private void TryInitFromDb(
            int lp, string userId,
            GradePicker cena, GradePicker trans, GradePicker kom, GradePicker elas,
            TextBox note, Panel card, Button saveBtn)
        {
            const string sql = @"
SELECT TOP 1 OcenaCena, OcenaTransport, OcenaKomunikacja, OcenaElastycznosc, Notatka
FROM dbo.DostawaFeedback
WHERE DostawaLp=@Lp AND Kto=@Kto;";

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@Lp", SqlDbType.Int).Value = lp;
            cmd.Parameters.Add("@Kto", SqlDbType.NVarChar, 64).Value = userId;

            conn.Open();
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return;

            int? s(object o) => o == DBNull.Value ? (int?)null : Convert.ToInt32(o);

            cena.SetScoreOrUnknown(s(r["OcenaCena"]));
            trans.SetScoreOrUnknown(s(r["OcenaTransport"]));
            kom.SetScoreOrUnknown(s(r["OcenaKomunikacja"]));
            elas.SetScoreOrUnknown(s(r["OcenaElastycznosc"]));

            note.Text = r["Notatka"] == DBNull.Value ? string.Empty : Convert.ToString(r["Notatka"]) ?? string.Empty;

            card.BackColor = Color.FromArgb(225, 245, 225);
            saveBtn.Text = "Modyfikuj";
        }

        // ===== wewnętrzne klasy UI =====

        private sealed class GradePicker
        {
            private readonly RadioButton _rbNieWiem;
            private readonly RadioButton _rbKontrakt;
            private readonly List<RadioButton> _scoreRbs;

            public GroupBox Host { get; }

            public GradePicker(GroupBox host)
            {
                Host = host ?? throw new ArgumentNullException(nameof(host));

                _rbNieWiem = new RadioButton { Left = 10, Top = 16, Width = 80, Text = "Nie wiem", RightToLeft = RightToLeft.No };
                _rbKontrakt = new RadioButton { Left = 95, Top = 16, Width = 80, Text = "Kontrakt", RightToLeft = RightToLeft.No };

                _scoreRbs = new List<RadioButton>();
                int left = 180;
                for (int i = 1; i <= 5; i++)
                {
                    var rb = new RadioButton { Left = left, Top = 16, Width = 32, Text = i.ToString(), Tag = i, RightToLeft = RightToLeft.No };
                    _scoreRbs.Add(rb);
                    left += 34;
                }

                Host.Controls.Add(_rbNieWiem);
                Host.Controls.Add(_rbKontrakt);
                foreach (var rb in _scoreRbs) Host.Controls.Add(rb);
            }

            public void SetUnknownDefault() => _rbNieWiem.Checked = true;

            public void SetScoreOrUnknown(int? score)
            {
                if (score is null)
                {
                    SetUnknownDefault();
                    return;
                }

                _rbNieWiem.Checked = false;
                _rbKontrakt.Checked = false;

                foreach (var rb in _scoreRbs)
                    rb.Checked = (int)rb.Tag == score.Value;
            }

            public int? GetScoreOrNull()
            {
                if (_rbNieWiem.Checked || _rbKontrakt.Checked) return null;
                var on = _scoreRbs.FirstOrDefault(r => r.Checked);
                return on == null ? (int?)null : (int)on.Tag;
            }
        }

        public sealed class DeliverySurveyItem
        {
            public int Lp { get; set; }
            public DateTime DataOdbioru { get; set; }
            public string Dostawca { get; set; } = string.Empty;
            public int? SztukiDek { get; set; }
            public int? Auta { get; set; }
            public decimal? WagaDek { get; set; }
            public string? TypCeny { get; set; }
            public decimal? Cena { get; set; }
        }
    }
}