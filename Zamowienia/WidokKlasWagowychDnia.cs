// Plik: WidokKlasWagowychDnia.cs
// WERSJA 2.0 - Intuicyjny widok z kartami podsumowania i kolorami
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
    /// <summary>
    /// Intuicyjny widok rezerwacji klas wagowych wszystkich klientów danego dnia
    /// </summary>
    public class WidokKlasWagowychDnia : Form
    {
        private static readonly Color COLOR_PRIMARY = Color.FromArgb(92, 138, 58);
        private static readonly Color COLOR_HEADER = Color.FromArgb(55, 80, 40);
        private static readonly Color COLOR_BG = Color.FromArgb(250, 252, 248);
        private static readonly Color COLOR_DANGER = Color.FromArgb(220, 38, 38);
        private static readonly Color COLOR_WARNING = Color.FromArgb(234, 88, 12);
        private static readonly Color COLOR_SUCCESS = Color.FromArgb(22, 163, 74);

        private static readonly Dictionary<int, Color> KLASY_KOLORY = new() {
            { 5, Color.FromArgb(220, 38, 38) }, { 6, Color.FromArgb(234, 88, 12) },
            { 7, Color.FromArgb(202, 138, 4) }, { 8, Color.FromArgb(101, 163, 13) },
            { 9, Color.FromArgb(22, 163, 74) }, { 10, Color.FromArgb(8, 145, 178) },
            { 11, Color.FromArgb(37, 99, 235) }, { 12, Color.FromArgb(124, 58, 237) } };

        private readonly DateTime _dataProdukcji;
        private readonly string _connLibra;
        private readonly Dictionary<int, int> _prognoza;
        private Dictionary<int, int> _sumaZajete = new();
        
        private DataGridView? _grid;
        private Panel? _pnlPodsumowanieKlas;
        private Label? _lblStatusInfo;

        public WidokKlasWagowychDnia(DateTime dataProdukcji, string connLibra, Dictionary<int, int> prognoza)
        {
            _dataProdukcji = dataProdukcji;
            _connLibra = connLibra;
            _prognoza = prognoza ?? new Dictionary<int, int>();
            for (int i = 5; i <= 12; i++) _sumaZajete[i] = 0;
            
            InitializeUI();
            WindowIconHelper.SetIcon(this);
            _ = LoadDataAsync();
        }

        private void InitializeUI()
        {
            Text = $"Rezerwacje Klas Wagowych - {_dataProdukcji:dd.MM.yyyy (dddd)}";
            Size = new Size(1350, 800);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = COLOR_BG;
            Font = new Font("Segoe UI", 10f);

            // === HEADER ===
            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 90, BackColor = COLOR_PRIMARY };
            
            pnlHeader.Controls.Add(new Label {
                Text = $"📊 Rezerwacje Klas Wagowych",
                Font = new Font("Segoe UI", 20f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(25, 12),
                AutoSize = true
            });
            
            pnlHeader.Controls.Add(new Label {
                Text = $"Data produkcji: {_dataProdukcji:dd.MM.yyyy (dddd)}",
                Font = new Font("Segoe UI", 12f),
                ForeColor = Color.FromArgb(220, 240, 210),
                Location = new Point(25, 50),
                AutoSize = true
            });

            var btnOdswiez = new Button {
                Text = "🔄 Odśwież",
                Size = new Size(110, 38),
                Location = new Point(1100, 25),
                BackColor = Color.FromArgb(70, 110, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnOdswiez.FlatAppearance.BorderSize = 0;
            btnOdswiez.Click += async (s, e) => await LoadDataAsync();
            pnlHeader.Controls.Add(btnOdswiez);

            var btnZamknij = new Button {
                Text = "✕ Zamknij",
                Size = new Size(100, 38),
                Location = new Point(1220, 25),
                BackColor = Color.FromArgb(60, 90, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                DialogResult = DialogResult.Cancel
            };
            btnZamknij.FlatAppearance.BorderSize = 0;
            pnlHeader.Controls.Add(btnZamknij);

            // === PANEL PODSUMOWANIA KLAS (karty z paskami) ===
            _pnlPodsumowanieKlas = new Panel {
                Dock = DockStyle.Top,
                Height = 115,
                BackColor = Color.White,
                Padding = new Padding(15, 10, 15, 10)
            };
            _pnlPodsumowanieKlas.Paint += (s, e) => {
                using var pen = new Pen(Color.FromArgb(220, 230, 215), 1);
                e.Graphics.DrawLine(pen, 0, _pnlPodsumowanieKlas!.Height - 1, _pnlPodsumowanieKlas.Width, _pnlPodsumowanieKlas.Height - 1);
            };

            // === GRID ===
            // === GRID ===
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                Font = new Font("Segoe UI", 10f),
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            _grid.RowTemplate.Height = 45;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = COLOR_HEADER;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.ColumnHeadersHeight = 45;
            _grid.EnableHeadersVisualStyles = false;
            _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 253, 248);
            _grid.CellPainting += Grid_CellPainting;

            // === STATUS INFO ===
            _lblStatusInfo = new Label {
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = COLOR_HEADER,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11f),
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Ładowanie danych..."
            };

            Controls.Add(_grid);
            Controls.Add(_pnlPodsumowanieKlas);
            Controls.Add(pnlHeader);
            Controls.Add(_lblStatusInfo);
        }

        private void BuildPodsumowanieKlas()
        {
            if (_pnlPodsumowanieKlas == null) return;
            _pnlPodsumowanieKlas.Controls.Clear();

            int x = 10;
            int cardWidth = 130;
            int cardHeight = 90;

            foreach (int klasa in new[] { 5, 6, 7, 8, 9, 10, 11, 12 })
            {
                int prognoza = _prognoza.GetValueOrDefault(klasa, 0);
                int zajete = _sumaZajete.GetValueOrDefault(klasa, 0);
                int wolne = prognoza - zajete;
                double procentZajete = prognoza > 0 ? zajete * 100.0 / prognoza : 0;

                var card = new Panel {
                    Location = new Point(x, 5),
                    Size = new Size(cardWidth, cardHeight),
                    BackColor = Color.White
                };

                int localKlasa = klasa;
                int localPrognoza = prognoza;
                double localProcent = procentZajete;
                int localWolne = wolne;

                card.Paint += (s, e) => {
                    var g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;

                    // Ramka z zaokrąglonymi rogami
                    using var path = CreateRoundedRectangle(0, 0, card.Width - 1, card.Height - 1, 6);
                    using var borderPen = new Pen(Color.FromArgb(210, 220, 200), 1);
                    g.DrawPath(borderPen, path);

                    // Kolorowy pasek na górze
                    using var topBrush = new SolidBrush(KLASY_KOLORY[localKlasa]);
                    g.FillRectangle(topBrush, 1, 1, card.Width - 2, 8);

                    // Pasek zajętości na dole
                    int barY = card.Height - 16;
                    int barWidth = card.Width - 16;
                    
                    // Tło paska
                    using var bgBrush = new SolidBrush(Color.FromArgb(235, 240, 230));
                    g.FillRectangle(bgBrush, 8, barY, barWidth, 10);

                    // Wypełnienie paska
                    if (localPrognoza > 0)
                    {
                        int fillWidth = (int)(barWidth * Math.Min(localProcent, 100) / 100);
                        Color fillColor = localProcent >= 100 ? COLOR_DANGER : 
                                         localProcent >= 80 ? COLOR_WARNING : COLOR_SUCCESS;
                        using var fillBrush = new SolidBrush(fillColor);
                        g.FillRectangle(fillBrush, 8, barY, fillWidth, 10);
                    }
                };

                // Nagłówek klasy
                card.Controls.Add(new Label {
                    Text = $"Klasa {klasa}",
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    ForeColor = KLASY_KOLORY[klasa],
                    Location = new Point(8, 14),
                    AutoSize = true
                });

                // Zajęte / Prognoza
                string statusText = prognoza > 0 ? $"{zajete} / {prognoza}" : "—";
                Color statusColor = prognoza == 0 ? Color.Gray :
                                   wolne <= 0 ? COLOR_DANGER :
                                   wolne < prognoza * 0.2 ? COLOR_WARNING : Color.FromArgb(50, 60, 45);

                card.Controls.Add(new Label {
                    Text = statusText,
                    Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                    ForeColor = statusColor,
                    Location = new Point(8, 33),
                    AutoSize = true
                });

                // Status wolnych
                if (prognoza > 0)
                {
                    string wolneText = wolne <= 0 ? "⚠️ BRAK!" : $"✓ wolne: {wolne}";
                    card.Controls.Add(new Label {
                        Text = wolneText,
                        Font = new Font("Segoe UI", 8f, wolne <= 0 ? FontStyle.Bold : FontStyle.Regular),
                        ForeColor = wolne <= 0 ? COLOR_DANGER : COLOR_SUCCESS,
                        Location = new Point(8, 55),
                        AutoSize = true
                    });
                }

                _pnlPodsumowanieKlas.Controls.Add(card);
                x += cardWidth + 6;
            }

            // Karta SUMA
            int sumaPrognoza = _prognoza.Values.Sum();
            int sumaZajeteOgolna = _sumaZajete.Values.Sum();
            int sumaWolne = sumaPrognoza - sumaZajeteOgolna;

            var cardSuma = new Panel {
                Location = new Point(x + 10, 5),
                Size = new Size(150, cardHeight),
                BackColor = COLOR_HEADER
            };

            cardSuma.Paint += (s, e) => {
                using var path = CreateRoundedRectangle(0, 0, cardSuma.Width - 1, cardSuma.Height - 1, 6);
                using var brush = new SolidBrush(COLOR_HEADER);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
            };

            cardSuma.Controls.Add(new Label {
                Text = "RAZEM",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 200, 170),
                Location = new Point(10, 10),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            cardSuma.Controls.Add(new Label {
                Text = $"{sumaZajeteOgolna} poj.",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(10, 30),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            string wolneInfo = sumaWolne >= 0 ? $"wolne: {sumaWolne}" : $"BRAK: {-sumaWolne}";
            cardSuma.Controls.Add(new Label {
                Text = wolneInfo,
                Font = new Font("Segoe UI", 9f, sumaWolne < 0 ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = sumaWolne < 0 ? Color.FromArgb(255, 150, 150) : Color.FromArgb(180, 220, 170),
                Location = new Point(10, 58),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            _pnlPodsumowanieKlas.Controls.Add(cardSuma);
        }

        private GraphicsPath CreateRoundedRectangle(int x, int y, int width, int height, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(x, y, radius * 2, radius * 2, 180, 90);
            path.AddArc(x + width - radius * 2, y, radius * 2, radius * 2, 270, 90);
            path.AddArc(x + width - radius * 2, y + height - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(x, y + height - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || _grid == null) return;

            var row = _grid.Rows[e.RowIndex];
            bool jestPodsumowaniem = row.Tag is string tag && tag == "summary";
            string colName = _grid.Columns[e.ColumnIndex].Name;

            // Dla wierszy podsumowań
            if (jestPodsumowaniem)
            {
                e.PaintBackground(e.ClipBounds, false);
                
                using var bgBrush = new SolidBrush(COLOR_HEADER);
                e.Graphics!.FillRectangle(bgBrush, e.CellBounds);

                if (e.Value != null)
                {
                    string text = e.Value.ToString() ?? "";
                    using var font = new Font("Segoe UI", 10f, FontStyle.Bold);
                    
                    // Dla wiersza WOLNE - koloruj ujemne na czerwono
                    bool isWolneRow = row.Cells["Odbiorca"].Value?.ToString()?.Contains("WOLNE") == true;
                    bool isNegative = int.TryParse(text, out int val) && val < 0;
                    
                    using var textBrush = new SolidBrush(isWolneRow && isNegative 
                        ? Color.FromArgb(255, 120, 120) 
                        : Color.White);
                    
                    var sf = new StringFormat { 
                        Alignment = colName == "Odbiorca" ? StringAlignment.Near : StringAlignment.Center,
                        LineAlignment = StringAlignment.Center 
                    };
                    
                    var rect = new RectangleF(e.CellBounds.X + 8, e.CellBounds.Y, 
                                              e.CellBounds.Width - 16, e.CellBounds.Height);
                    e.Graphics.DrawString(text, font, textBrush, rect, sf);
                }

                e.Handled = true;
                return;
            }

            // Dla kolumn klas - rysuj mini pasek w tle
            if (colName.StartsWith("Kl") && int.TryParse(colName.Replace("Kl", ""), out int klasa))
            {
                int prognoza = _prognoza.GetValueOrDefault(klasa, 0);
                if (e.Value is int wartosc && wartosc > 0 && prognoza > 0)
                {
                    e.PaintBackground(e.ClipBounds, false);

                    // Mini pasek w tle
                    double procent = Math.Min(wartosc * 100.0 / prognoza, 100);
                    int barWidth = (int)((e.CellBounds.Width - 8) * procent / 100);
                    
                    using var barBrush = new SolidBrush(Color.FromArgb(40, KLASY_KOLORY[klasa]));
                    e.Graphics!.FillRectangle(barBrush, e.CellBounds.X + 4, e.CellBounds.Y + 8, 
                                              barWidth, e.CellBounds.Height - 16);

                    // Wartość
                    using var font = new Font("Segoe UI", 10f, FontStyle.Bold);
                    using var textBrush = new SolidBrush(KLASY_KOLORY[klasa]);
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(wartosc.ToString(), font, textBrush, e.CellBounds, sf);

                    e.Handled = true;
                }
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var rezerwacje = await PobierzRezerwacjeAsync();

                // Grupuj po kliencie (bez Handlowca w wyświetlaniu)
                var klienci = rezerwacje
                    .GroupBy(r => new { r.ZamowienieId, r.Odbiorca })
                    .Select(g => new {
                        g.Key.Odbiorca,
                        Kl5 = g.Where(r => r.Klasa == 5).Sum(r => r.IloscPojemnikow),
                        Kl6 = g.Where(r => r.Klasa == 6).Sum(r => r.IloscPojemnikow),
                        Kl7 = g.Where(r => r.Klasa == 7).Sum(r => r.IloscPojemnikow),
                        Kl8 = g.Where(r => r.Klasa == 8).Sum(r => r.IloscPojemnikow),
                        Kl9 = g.Where(r => r.Klasa == 9).Sum(r => r.IloscPojemnikow),
                        Kl10 = g.Where(r => r.Klasa == 10).Sum(r => r.IloscPojemnikow),
                        Kl11 = g.Where(r => r.Klasa == 11).Sum(r => r.IloscPojemnikow),
                        Kl12 = g.Where(r => r.Klasa == 12).Sum(r => r.IloscPojemnikow)
                    })
                    .OrderBy(k => k.Odbiorca)
                    .ToList();

                // Oblicz sumy zajęte
                for (int i = 5; i <= 12; i++)
                    _sumaZajete[i] = klienci.Sum(k => GetKlasa(k, i));

                this.Invoke(() =>
                {
                    if (_grid == null) return;

                    var dt = new System.Data.DataTable();
                    dt.Columns.Add("Odbiorca", typeof(string));
                    for (int i = 5; i <= 12; i++)
                        dt.Columns.Add($"Kl{i}", typeof(int));
                    dt.Columns.Add("SUMA", typeof(int));
                    dt.Columns.Add("Palety", typeof(decimal));

                    foreach (var k in klienci)
                    {
                        int suma = k.Kl5 + k.Kl6 + k.Kl7 + k.Kl8 + k.Kl9 + k.Kl10 + k.Kl11 + k.Kl12;
                        dt.Rows.Add(k.Odbiorca, k.Kl5, k.Kl6, k.Kl7, k.Kl8, k.Kl9, k.Kl10, k.Kl11, k.Kl12, 
                                   suma, Math.Round(suma / 36m, 2));
                    }

                    // Wiersz sumy zajętych
                    int sumaOgolna = _sumaZajete.Values.Sum();
                    dt.Rows.Add("═══ ZAJĘTE ═══", _sumaZajete[5], _sumaZajete[6], _sumaZajete[7], _sumaZajete[8],
                               _sumaZajete[9], _sumaZajete[10], _sumaZajete[11], _sumaZajete[12], 
                               sumaOgolna, Math.Round(sumaOgolna / 36m, 2));

                    // Wiersz prognozy
                    int sumaPrognoza = _prognoza.Values.Sum();
                    dt.Rows.Add("═══ PROGNOZA ═══", _prognoza.GetValueOrDefault(5), _prognoza.GetValueOrDefault(6),
                               _prognoza.GetValueOrDefault(7), _prognoza.GetValueOrDefault(8),
                               _prognoza.GetValueOrDefault(9), _prognoza.GetValueOrDefault(10),
                               _prognoza.GetValueOrDefault(11), _prognoza.GetValueOrDefault(12),
                               sumaPrognoza, Math.Round(sumaPrognoza / 36m, 2));

                    // Wiersz wolnych
                    int sumaWolne = sumaPrognoza - sumaOgolna;
                    dt.Rows.Add("═══ WOLNE ═══", 
                               _prognoza.GetValueOrDefault(5) - _sumaZajete[5],
                               _prognoza.GetValueOrDefault(6) - _sumaZajete[6],
                               _prognoza.GetValueOrDefault(7) - _sumaZajete[7],
                               _prognoza.GetValueOrDefault(8) - _sumaZajete[8],
                               _prognoza.GetValueOrDefault(9) - _sumaZajete[9],
                               _prognoza.GetValueOrDefault(10) - _sumaZajete[10],
                               _prognoza.GetValueOrDefault(11) - _sumaZajete[11],
                               _prognoza.GetValueOrDefault(12) - _sumaZajete[12],
                               sumaWolne, Math.Round(sumaWolne / 36m, 2));

                    _grid.DataSource = dt;

                    // Formatowanie kolumn
                    _grid.Columns["Odbiorca"]!.FillWeight = 250;
                    _grid.Columns["Odbiorca"]!.MinimumWidth = 200;
                    _grid.Columns["Odbiorca"]!.HeaderText = "KLIENT";
                    
                    for (int i = 5; i <= 12; i++)
                    {
                        _grid.Columns[$"Kl{i}"]!.HeaderText = $"Kl.{i}";
                        _grid.Columns[$"Kl{i}"]!.Width = 75;
                        _grid.Columns[$"Kl{i}"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    }

                    _grid.Columns["SUMA"]!.Width = 85;
                    _grid.Columns["SUMA"]!.DefaultCellStyle.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
                    _grid.Columns["SUMA"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    _grid.Columns["SUMA"]!.DefaultCellStyle.BackColor = Color.FromArgb(245, 250, 242);

                    _grid.Columns["Palety"]!.Width = 80;
                    _grid.Columns["Palety"]!.DefaultCellStyle.Format = "N2";
                    _grid.Columns["Palety"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

                    // Oznacz wiersze podsumowań
                    for (int i = klienci.Count; i < _grid.Rows.Count; i++)
                        _grid.Rows[i].Tag = "summary";

                    // Buduj panel podsumowania
                    BuildPodsumowanieKlas();

                    // Status bar
                    if (_lblStatusInfo != null)
                    {
                        double procentZajete = sumaPrognoza > 0 ? sumaOgolna * 100.0 / sumaPrognoza : 0;
                        
                        // Znajdź wyczerpane klasy
                        var wyczerpane = new List<int>();
                        for (int i = 5; i <= 12; i++)
                        {
                            int prog = _prognoza.GetValueOrDefault(i, 0);
                            if (prog > 0 && _sumaZajete[i] >= prog)
                                wyczerpane.Add(i);
                        }

                        string wyczerpaneText = wyczerpane.Count > 0 
                            ? $"  ⚠️ WYCZERPANE: Kl.{string.Join(", Kl.", wyczerpane)}" 
                            : "";

                        _lblStatusInfo.Text = $"📊 Klientów: {klienci.Count}  │  " +
                            $"Zajęte: {sumaOgolna} poj. ({procentZajete:N1}%)  │  " +
                            $"Wolne: {sumaWolne} poj.  │  " +
                            $"Prognoza: {sumaPrognoza} poj. ({sumaPrognoza / 36m:N1} palet)" +
                            wyczerpaneText;
                            
                        _lblStatusInfo.BackColor = wyczerpane.Count > 0 ? COLOR_WARNING : COLOR_HEADER;
                    }
                });
            }
            catch (Exception ex)
            {
                this.Invoke(() => {
                    if (_lblStatusInfo != null)
                    {
                        _lblStatusInfo.Text = $"❌ Błąd: {ex.Message}";
                        _lblStatusInfo.BackColor = COLOR_DANGER;
                    }
                });
            }
        }

        private int GetKlasa(dynamic k, int klasa) => klasa switch {
            5 => k.Kl5, 6 => k.Kl6, 7 => k.Kl7, 8 => k.Kl8,
            9 => k.Kl9, 10 => k.Kl10, 11 => k.Kl11, 12 => k.Kl12, _ => 0
        };

        private async Task<List<RezerwacjaKlientaInfo>> PobierzRezerwacjeAsync()
        {
            var lista = new List<RezerwacjaKlientaInfo>();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                var checkSql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RezerwacjeKlasWagowych'";
                await using var checkCmd = new SqlCommand(checkSql, cn);
                if ((int)await checkCmd.ExecuteScalarAsync() == 0) return lista;

                var sql = @"SELECT r.ZamowienieId, r.Odbiorca, r.Handlowiec, r.Klasa, r.IloscPojemnikow
                    FROM [dbo].[RezerwacjeKlasWagowych] r
                    WHERE r.DataProdukcji = @Data AND r.Status = 'Aktywna'
                    ORDER BY r.Odbiorca, r.Klasa";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Data", _dataProdukcji.Date);

                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    lista.Add(new RezerwacjaKlientaInfo {
                        ZamowienieId = rd.GetInt32(0),
                        Odbiorca = rd.IsDBNull(1) ? "Nieznany" : rd.GetString(1),
                        Handlowiec = rd.IsDBNull(2) ? "" : rd.GetString(2),
                        Klasa = rd.GetInt32(3),
                        IloscPojemnikow = rd.GetInt32(4)
                    });
                }
            }
            catch { }
            return lista;
        }
    }
}
