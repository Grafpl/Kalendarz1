using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;

namespace Kalendarz1
{
    public partial class SzczegolyDokumentuForm : Form
    {
        private string connectionString;
        private int idDokumentu;
        private string numerDokumentu;
        private DataGridView dataGridViewPozycje;
        private Panel panelGorny;
        private Panel panelStatystyki;
        private SplitContainer splitContainer;

        private static class ColorScheme
        {
            public static Color Primary = ColorTranslator.FromHtml("#2c3e50");
            public static Color Secondary = ColorTranslator.FromHtml("#34495e");
            public static Color Accent = ColorTranslator.FromHtml("#3498db");
            public static Color Info = ColorTranslator.FromHtml("#16a085");
            public static Color Light = ColorTranslator.FromHtml("#ecf0f1");
            public static Color Background = ColorTranslator.FromHtml("#f8f9fa");
            public static Color GridAlt = ColorTranslator.FromHtml("#e8f8f5");
            public static Color Success = ColorTranslator.FromHtml("#27ae60");
            public static Color Warning = ColorTranslator.FromHtml("#f39c12");
        }

        public SzczegolyDokumentuForm(string connString, int dokId, string numerDok)
        {
            connectionString = connString;
            idDokumentu = dokId;
            numerDokumentu = numerDok;

            InitializeComponent();
            this.Load += SzczegolyDokumentuForm_Load;
            this.Resize += SzczegolyDokumentuForm_Resize;
        }

        private void InitializeComponent()
        {
            this.Text = $"Szczegóły dokumentu: {numerDokumentu}";
            this.Size = new Size(1100, 750);
            this.MinimumSize = new Size(900, 600);

            // Zmiana pozycjonowania - bardziej w prawo
            this.StartPosition = FormStartPosition.Manual;

            // Ustaw pozycję okna znacznie bardziej w prawo
            if (this.Owner != null)
            {
                // Pozycjonuj okno bliżej prawej krawędzi ekranu rodzica
                int x = this.Owner.Right - this.Width - 50; // 50px marginesu od prawej
                int y = this.Owner.Top + 80; // trochę od góry

                // Upewnij się, że okno mieści się na ekranie
                var screen = Screen.FromControl(this.Owner);
                if (x + this.Width > screen.WorkingArea.Right)
                    x = screen.WorkingArea.Right - this.Width - 10;
                if (x < screen.WorkingArea.Left)
                    x = screen.WorkingArea.Left + 10;

                this.Location = new Point(x, y);
            }
            else
            {
                this.StartPosition = FormStartPosition.CenterScreen;
            }

            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.BackColor = ColorScheme.Background;
            this.Font = new Font("Segoe UI", 9.75F);

            // Panel górny z tytułem
            panelGorny = new Panel
            {
                Height = 70,
                Dock = DockStyle.Top,
                BackColor = ColorScheme.Light,
                Padding = new Padding(20, 15, 20, 10)
            };
            this.Controls.Add(panelGorny);

            Label lblTytul = new Label
            {
                Text = $"📄 Dokument: {numerDokumentu}",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = ColorScheme.Primary,
                AutoSize = true,
                Location = new Point(20, 15)
            };
            panelGorny.Controls.Add(lblTytul);

            Label lblInfo = new Label
            {
                Text = "Szczegółowe informacje o pozycjach i podsumowanie dokumentu",
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = ColorTranslator.FromHtml("#7f8c8d"),
                AutoSize = true,
                Location = new Point(20, 42)
            };
            panelGorny.Controls.Add(lblInfo);

            // Split Container - główny podział
            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 350,
                BorderStyle = BorderStyle.None,
                SplitterWidth = 8,
                BackColor = ColorScheme.Background
            };
            this.Controls.Add(splitContainer);

            // Panel górny split - pozycje faktury
            Panel panelPozycje = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15, 10, 15, 10),
                BackColor = ColorScheme.Background
            };
            splitContainer.Panel1.Controls.Add(panelPozycje);

            Label lblPozycje = new Label
            {
                Text = "Pozycje faktury",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = ColorScheme.Primary,
                Location = new Point(5, 0),
                AutoSize = true
            };
            panelPozycje.Controls.Add(lblPozycje);

            dataGridViewPozycje = new DataGridView
            {
                Location = new Point(5, 30),
                Size = new Size(panelPozycje.Width - 10, panelPozycje.Height - 35),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoGenerateColumns = false,
                RowHeadersVisible = false,
                BorderStyle = BorderStyle.None,
                BackgroundColor = Color.White
            };

            StylizujDataGridView();
            KonfigurujKolumny();
            panelPozycje.Controls.Add(dataGridViewPozycje);

            // Panel dolny split - statystyki
            panelStatystyki = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15, 10, 15, 15),
                BackColor = ColorScheme.Background
            };
            splitContainer.Panel2.Controls.Add(panelStatystyki);

            Label lblStatystyki = new Label
            {
                Text = "Podsumowanie dokumentu",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = ColorScheme.Primary,
                Location = new Point(5, 0),
                AutoSize = true
            };
            panelStatystyki.Controls.Add(lblStatystyki);

            StworzPanelStatystyk();

            // Panel dolny z przyciskiem
            Panel panelDolny = new Panel
            {
                Height = 65,
                Dock = DockStyle.Bottom,
                BackColor = ColorScheme.Light,
                Padding = new Padding(15, 15, 15, 15)
            };
            this.Controls.Add(panelDolny);

            Button btnZamknij = new Button
            {
                Text = "✖ Zamknij",
                Size = new Size(130, 38),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = ColorTranslator.FromHtml("#95a5a6"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            btnZamknij.FlatAppearance.BorderSize = 0;
            btnZamknij.Location = new Point(panelDolny.Width - 145, 14);
            btnZamknij.Click += (s, e) => this.Close();
            panelDolny.Controls.Add(btnZamknij);

            Button btnDrukuj = new Button
            {
                Text = "🖨 Drukuj",
                Size = new Size(130, 38),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = ColorScheme.Accent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            btnDrukuj.FlatAppearance.BorderSize = 0;
            btnDrukuj.Location = new Point(panelDolny.Width - 290, 14);
            btnDrukuj.Click += (s, e) => MessageBox.Show("Funkcja drukowania zostanie wkrótce dodana.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
            panelDolny.Controls.Add(btnDrukuj);
        }

        private void StworzPanelStatystyk()
        {
            TableLayoutPanel statsLayout = new TableLayoutPanel
            {
                Location = new Point(5, 35),
                Size = new Size(panelStatystyki.Width - 10, panelStatystyki.Height - 45),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = Color.Transparent
            };

            statsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            statsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            statsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            statsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            statsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            // Karty statystyk
            var kartaSumaWartosci = StworzKarteStatystyki("💰 Wartość całkowita", "0.00 zł", ColorScheme.Success);
            var kartaLiczbaPozycji = StworzKarteStatystyki("📦 Liczba pozycji", "0", ColorScheme.Accent);
            var kartaSumaIlosci = StworzKarteStatystyki("⚖ Suma ilości", "0.00 kg", ColorScheme.Info);
            var kartaSredniaCena = StworzKarteStatystyki("📊 Średnia cena", "0.00 zł/kg", ColorScheme.Warning);
            var kartaNajdrozsza = StworzKarteStatystyki("⭐ Najdroższa pozycja", "---", ColorTranslator.FromHtml("#e74c3c"));
            var kartaNajtansza = StworzKarteStatystyki("💎 Najtańsza pozycja", "---", ColorTranslator.FromHtml("#9b59b6"));

            statsLayout.Controls.Add(kartaSumaWartosci, 0, 0);
            statsLayout.Controls.Add(kartaLiczbaPozycji, 1, 0);
            statsLayout.Controls.Add(kartaSumaIlosci, 2, 0);
            statsLayout.Controls.Add(kartaSredniaCena, 0, 1);
            statsLayout.Controls.Add(kartaNajdrozsza, 1, 1);
            statsLayout.Controls.Add(kartaNajtansza, 2, 1);

            panelStatystyki.Controls.Add(statsLayout);
        }

        private Panel StworzKarteStatystyki(string tytul, string wartosc, Color kolor)
        {
            Panel karta = new Panel
            {
                Margin = new Padding(5),
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill
            };

            // Kolorowy pasek na górze
            Panel pasek = new Panel
            {
                Height = 4,
                Dock = DockStyle.Top,
                BackColor = kolor
            };
            karta.Controls.Add(pasek);

            Label lblTytul = new Label
            {
                Text = tytul,
                Font = new Font("Segoe UI", 9F),
                ForeColor = ColorTranslator.FromHtml("#7f8c8d"),
                Location = new Point(15, 15),
                AutoSize = true
            };
            karta.Controls.Add(lblTytul);

            Label lblWartosc = new Label
            {
                Text = wartosc,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = ColorScheme.Primary,
                Location = new Point(15, 40),
                AutoSize = true,
                Tag = "wartosc" // Tag do łatwej identyfikacji przy aktualizacji
            };
            karta.Controls.Add(lblWartosc);

            return karta;
        }

        private void SzczegolyDokumentuForm_Resize(object? sender, EventArgs e)
        {
            if (this.WindowState != FormWindowState.Minimized)
            {
                AktualizujRozmiaryCzcionek();
            }
        }

        private void AktualizujRozmiaryCzcionek()
        {
            try
            {
                float baseSize = Math.Max(8.5f, Math.Min(11f, this.Width / 100f));

                if (dataGridViewPozycje != null)
                {
                    dataGridViewPozycje.DefaultCellStyle.Font = new Font("Segoe UI", baseSize);
                    dataGridViewPozycje.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", baseSize, FontStyle.Bold);
                }
            }
            catch { }
        }

        private void StylizujDataGridView()
        {
            if (dataGridViewPozycje == null) return;

            dataGridViewPozycje.EnableHeadersVisualStyles = false;
            dataGridViewPozycje.ColumnHeadersDefaultCellStyle.BackColor = ColorScheme.Info;
            dataGridViewPozycje.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dataGridViewPozycje.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dataGridViewPozycje.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewPozycje.ColumnHeadersHeight = 42;
            dataGridViewPozycje.AlternatingRowsDefaultCellStyle.BackColor = ColorScheme.GridAlt;
            dataGridViewPozycje.DefaultCellStyle.SelectionBackColor = ColorScheme.Accent;
            dataGridViewPozycje.DefaultCellStyle.SelectionForeColor = Color.White;
            dataGridViewPozycje.DefaultCellStyle.BackColor = Color.White;
            dataGridViewPozycje.GridColor = ColorTranslator.FromHtml("#bdc3c7");
            dataGridViewPozycje.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            dataGridViewPozycje.RowTemplate.Height = 36;
            dataGridViewPozycje.AllowUserToResizeRows = false;
            dataGridViewPozycje.RowTemplate.Resizable = DataGridViewTriState.False;
        }

        private void KonfigurujKolumny()
        {
            if (dataGridViewPozycje == null) return;

            dataGridViewPozycje.Columns.Clear();

            var rightAlignStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleRight,
                Format = "N2"
            };

            var centerAlignStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter
            };

            dataGridViewPozycje.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Lp",
                DataPropertyName = "Lp",
                HeaderText = "Lp",
                Width = 60,
                DefaultCellStyle = centerAlignStyle
            });

            dataGridViewPozycje.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "KodTowaru",
                DataPropertyName = "KodTowaru",
                HeaderText = "Kod Towaru / Opis",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 250
            });

            dataGridViewPozycje.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Ilosc",
                DataPropertyName = "Ilosc",
                HeaderText = "Ilość",
                Width = 120,
                DefaultCellStyle = rightAlignStyle
            });

            dataGridViewPozycje.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Cena",
                DataPropertyName = "Cena",
                HeaderText = "Cena Netto",
                Width = 130,
                DefaultCellStyle = rightAlignStyle
            });

            dataGridViewPozycje.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Wartosc",
                DataPropertyName = "Wartosc",
                HeaderText = "Wartość Netto",
                Width = 150,
                DefaultCellStyle = rightAlignStyle
            });
        }

        private void SzczegolyDokumentuForm_Load(object? sender, EventArgs e)
        {
            try
            {
                WczytajPozycjeDokumentu();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania szczegółów dokumentu: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void WczytajPozycjeDokumentu()
        {
            string query = @"
                SELECT 
                    DP.lp AS Lp,
                    DP.kod AS KodTowaru, 
                    DP.ilosc AS Ilosc, 
                    DP.cena AS Cena, 
                    DP.wartNetto AS Wartosc 
                FROM [HANDEL].[HM].[DP] DP 
                WHERE DP.super = @idDokumentu 
                ORDER BY DP.lp;";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@idDokumentu", idDokumentu);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    dataGridViewPozycje.DataSource = dt;

                    // Oblicz statystyki
                    if (dt.Rows.Count > 0)
                    {
                        decimal sumaIlosc = 0;
                        decimal sumaWartosc = 0;
                        decimal maxCena = 0;
                        decimal minCena = decimal.MaxValue;
                        string najdrozszaPozycja = "---";
                        string najtanszaPozycja = "---";

                        foreach (DataRow row in dt.Rows)
                        {
                            if (row["Ilosc"] != DBNull.Value)
                                sumaIlosc += Convert.ToDecimal(row["Ilosc"]);

                            if (row["Wartosc"] != DBNull.Value)
                                sumaWartosc += Convert.ToDecimal(row["Wartosc"]);

                            if (row["Cena"] != DBNull.Value)
                            {
                                decimal cena = Convert.ToDecimal(row["Cena"]);
                                if (cena > maxCena)
                                {
                                    maxCena = cena;
                                    najdrozszaPozycja = row["KodTowaru"]?.ToString() ?? "---";
                                }
                                if (cena < minCena)
                                {
                                    minCena = cena;
                                    najtanszaPozycja = row["KodTowaru"]?.ToString() ?? "---";
                                }
                            }
                        }

                        decimal sredniaCena = sumaIlosc > 0 ? sumaWartosc / sumaIlosc : 0;

                        // Aktualizuj karty statystyk
                        AktualizujStatystyki(
                            sumaWartosc,
                            dt.Rows.Count,
                            sumaIlosc,
                            sredniaCena,
                            najdrozszaPozycja,
                            najtanszaPozycja
                        );
                    }

                    // Dodaj event do formatowania komórek
                    dataGridViewPozycje.CellFormatting -= DataGridViewPozycje_CellFormatting;
                    dataGridViewPozycje.CellFormatting += DataGridViewPozycje_CellFormatting;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas pobierania danych z bazy: {ex.Message}", ex);
            }
        }

        private void AktualizujStatystyki(decimal sumaWartosc, int liczbaPozycji, decimal sumaIlosc,
            decimal sredniaCena, string najdrozsza, string najtansza)
        {
            try
            {
                if (panelStatystyki?.Controls[1] is TableLayoutPanel statsLayout)
                {
                    // Wartość całkowita
                    if (statsLayout.GetControlFromPosition(0, 0) is Panel p1)
                    {
                        var lblWartosc = p1.Controls.OfType<Label>().FirstOrDefault(l => l.Tag?.ToString() == "wartosc");
                        if (lblWartosc != null) lblWartosc.Text = $"{sumaWartosc:N2} zł";
                    }

                    // Liczba pozycji
                    if (statsLayout.GetControlFromPosition(1, 0) is Panel p2)
                    {
                        var lblWartosc = p2.Controls.OfType<Label>().FirstOrDefault(l => l.Tag?.ToString() == "wartosc");
                        if (lblWartosc != null) lblWartosc.Text = liczbaPozycji.ToString();
                    }

                    // Suma ilości
                    if (statsLayout.GetControlFromPosition(2, 0) is Panel p3)
                    {
                        var lblWartosc = p3.Controls.OfType<Label>().FirstOrDefault(l => l.Tag?.ToString() == "wartosc");
                        if (lblWartosc != null) lblWartosc.Text = $"{sumaIlosc:N2} kg";
                    }

                    // Średnia cena
                    if (statsLayout.GetControlFromPosition(0, 1) is Panel p4)
                    {
                        var lblWartosc = p4.Controls.OfType<Label>().FirstOrDefault(l => l.Tag?.ToString() == "wartosc");
                        if (lblWartosc != null) lblWartosc.Text = $"{sredniaCena:N2} zł/kg";
                    }

                    // Najdroższa
                    if (statsLayout.GetControlFromPosition(1, 1) is Panel p5)
                    {
                        var lblWartosc = p5.Controls.OfType<Label>().FirstOrDefault(l => l.Tag?.ToString() == "wartosc");
                        if (lblWartosc != null)
                        {
                            lblWartosc.Text = najdrozsza.Length > 20 ? najdrozsza.Substring(0, 20) + "..." : najdrozsza;
                            lblWartosc.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
                        }
                    }

                    // Najtańsza
                    if (statsLayout.GetControlFromPosition(2, 1) is Panel p6)
                    {
                        var lblWartosc = p6.Controls.OfType<Label>().FirstOrDefault(l => l.Tag?.ToString() == "wartosc");
                        if (lblWartosc != null)
                        {
                            lblWartosc.Text = najtansza.Length > 20 ? najtansza.Substring(0, 20) + "..." : najtansza;
                            lblWartosc.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
                        }
                    }
                }
            }
            catch { }
        }

        private void DataGridViewPozycje_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.Value == null || e.ColumnIndex < 0 || dataGridViewPozycje == null) return;

            try
            {
                var colName = dataGridViewPozycje.Columns[e.ColumnIndex].Name;

                if (colName == "Ilosc" || colName == "Cena" || colName == "Wartosc")
                {
                    if (e.Value != DBNull.Value && decimal.TryParse(e.Value.ToString(), out decimal val))
                    {
                        if (colName == "Wartosc")
                        {
                            e.Value = val.ToString("N2") + " zł";
                            e.FormattingApplied = true;
                        }
                        else if (colName == "Cena")
                        {
                            e.Value = val.ToString("N2") + " zł/kg";
                            e.FormattingApplied = true;
                        }
                        else if (colName == "Ilosc")
                        {
                            e.Value = val.ToString("N2") + " kg";
                            e.FormattingApplied = true;
                        }
                    }
                }
            }
            catch { }
        }
    }
}