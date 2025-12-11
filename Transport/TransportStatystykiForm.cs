// Plik: Transport/TransportStatystykiForm.cs
// Formularz statystyk transportu

using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1.Transport
{
    public class TransportStatystykiForm : Form
    {
        private readonly string _connTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        // Kontrolki
        private TabControl tabControl;
        private DateTimePicker dtpOd;
        private DateTimePicker dtpDo;
        private Button btnOdswiez;
        private Label lblStatus;

        // Zakładki
        private TabPage tabKierowcy;
        private TabPage tabPojazdy;
        private TabPage tabOdbiorcy;
        private TabPage tabOgolne;

        // Gridy
        private DataGridView dgvKierowcy;
        private DataGridView dgvPojazdy;
        private DataGridView dgvOdbiorcy;
        private DataGridView dgvOgolne;

        // Panele podsumowań
        private Panel panelSummaryKierowcy;
        private Panel panelSummaryPojazdy;
        private Panel panelSummaryOdbiorcy;

        public TransportStatystykiForm()
        {
            InitializeComponent();
            this.Load += async (s, e) => await LoadAllStatisticsAsync();
        }

        private void InitializeComponent()
        {
            Text = "Statystyki Transportu";
            Size = new Size(1200, 800);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(245, 247, 250);

            // Header z filtrem dat
            var panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Color.FromArgb(41, 44, 51),
                Padding = new Padding(20, 15, 20, 15)
            };

            var lblTytul = new Label
            {
                Text = "STATYSTYKI TRANSPORTU",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 20)
            };

            var lblOd = new Label
            {
                Text = "Od:",
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(350, 23)
            };

            dtpOd = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today.AddMonths(-1),
                Width = 110,
                Location = new Point(380, 20)
            };

            var lblDo = new Label
            {
                Text = "Do:",
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(510, 23)
            };

            dtpDo = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today,
                Width = 110,
                Location = new Point(540, 20)
            };

            btnOdswiez = new Button
            {
                Text = "ODŚWIEŻ",
                Size = new Size(100, 32),
                Location = new Point(670, 17),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnOdswiez.FlatAppearance.BorderSize = 0;
            btnOdswiez.Click += async (s, e) => await LoadAllStatisticsAsync();

            // Szybkie filtry
            var btnTenMiesiac = CreateQuickFilterButton("Ten miesiąc", 790, () =>
            {
                dtpOd.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                dtpDo.Value = DateTime.Today;
            });

            var btnPoprzedniMiesiac = CreateQuickFilterButton("Poprzedni", 900, () =>
            {
                var firstOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                dtpOd.Value = firstOfMonth.AddMonths(-1);
                dtpDo.Value = firstOfMonth.AddDays(-1);
            });

            var btnTenRok = CreateQuickFilterButton("Ten rok", 1000, () =>
            {
                dtpOd.Value = new DateTime(DateTime.Today.Year, 1, 1);
                dtpDo.Value = DateTime.Today;
            });

            lblStatus = new Label
            {
                Text = "",
                ForeColor = Color.FromArgb(150, 255, 150),
                AutoSize = true,
                Location = new Point(1100, 23)
            };

            panelHeader.Controls.AddRange(new Control[] {
                lblTytul, lblOd, dtpOd, lblDo, dtpDo, btnOdswiez,
                btnTenMiesiac, btnPoprzedniMiesiac, btnTenRok, lblStatus
            });

            // TabControl
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F)
            };

            // Zakładka Kierowcy
            tabKierowcy = new TabPage("Kierowcy");
            CreateKierowcyTab();
            tabControl.TabPages.Add(tabKierowcy);

            // Zakładka Pojazdy
            tabPojazdy = new TabPage("Pojazdy");
            CreatePojazdyTab();
            tabControl.TabPages.Add(tabPojazdy);

            // Zakładka Odbiorcy
            tabOdbiorcy = new TabPage("Odbiorcy");
            CreateOdbiorcyTab();
            tabControl.TabPages.Add(tabOdbiorcy);

            // Zakładka Ogólne
            tabOgolne = new TabPage("Podsumowanie");
            CreateOgolneTab();
            tabControl.TabPages.Add(tabOgolne);

            Controls.Add(tabControl);
            Controls.Add(panelHeader);
        }

        private Button CreateQuickFilterButton(string text, int x, Action onClick)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(90, 28),
                Location = new Point(x, 19),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 65, 75),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8F),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += async (s, e) =>
            {
                onClick();
                await LoadAllStatisticsAsync();
            };
            return btn;
        }

        private void CreateKierowcyTab()
        {
            tabKierowcy.BackColor = Color.White;
            tabKierowcy.Padding = new Padding(15);

            panelSummaryKierowcy = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(248, 249, 252)
            };

            dgvKierowcy = CreateStyledDataGridView();
            dgvKierowcy.Dock = DockStyle.Fill;

            tabKierowcy.Controls.Add(dgvKierowcy);
            tabKierowcy.Controls.Add(panelSummaryKierowcy);
        }

        private void CreatePojazdyTab()
        {
            tabPojazdy.BackColor = Color.White;
            tabPojazdy.Padding = new Padding(15);

            panelSummaryPojazdy = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(248, 249, 252)
            };

            dgvPojazdy = CreateStyledDataGridView();
            dgvPojazdy.Dock = DockStyle.Fill;

            tabPojazdy.Controls.Add(dgvPojazdy);
            tabPojazdy.Controls.Add(panelSummaryPojazdy);
        }

        private void CreateOdbiorcyTab()
        {
            tabOdbiorcy.BackColor = Color.White;
            tabOdbiorcy.Padding = new Padding(15);

            panelSummaryOdbiorcy = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(248, 249, 252)
            };

            dgvOdbiorcy = CreateStyledDataGridView();
            dgvOdbiorcy.Dock = DockStyle.Fill;

            tabOdbiorcy.Controls.Add(dgvOdbiorcy);
            tabOdbiorcy.Controls.Add(panelSummaryOdbiorcy);
        }

        private void CreateOgolneTab()
        {
            tabOgolne.BackColor = Color.White;
            tabOgolne.Padding = new Padding(15);

            dgvOgolne = CreateStyledDataGridView();
            dgvOgolne.Dock = DockStyle.Fill;

            tabOgolne.Controls.Add(dgvOgolne);
        }

        private DataGridView CreateStyledDataGridView()
        {
            var dgv = new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };

            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 247, 250);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(80, 90, 110);
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgv.ColumnHeadersHeight = 40;
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 252, 254);
            dgv.RowTemplate.Height = 35;
            dgv.GridColor = Color.FromArgb(240, 242, 246);

            return dgv;
        }

        private async Task LoadAllStatisticsAsync()
        {
            try
            {
                lblStatus.Text = "Ładowanie...";
                lblStatus.ForeColor = Color.FromArgb(241, 196, 15);
                Cursor = Cursors.WaitCursor;

                await Task.WhenAll(
                    LoadKierowcyStatystykiAsync(),
                    LoadPojazdyStatystykiAsync(),
                    LoadOdbiorcyStatystykiAsync(),
                    LoadOgolneStatystykiAsync()
                );

                lblStatus.Text = "OK";
                lblStatus.ForeColor = Color.FromArgb(150, 255, 150);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Błąd!";
                lblStatus.ForeColor = Color.FromArgb(255, 100, 100);
                MessageBox.Show($"Błąd ładowania statystyk: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private async Task LoadKierowcyStatystykiAsync()
        {
            var sql = @"
                SELECT
                    k.Imie + ' ' + k.Nazwisko AS Kierowca,
                    COUNT(DISTINCT kurs.KursID) AS LiczbaKursow,
                    SUM(ISNULL(l.PojemnikiE2, 0)) AS SumaPojemnikow,
                    SUM(ISNULL(l.PaletyH1, 0)) AS SumaPalet,
                    COUNT(DISTINCT l.LadunekID) AS LiczbaLadunkow,
                    AVG(CAST(ISNULL(l.PojemnikiE2, 0) AS FLOAT) / NULLIF(kurs.PlanE2NaPalete * ISNULL(l.PaletyH1, 1), 0) * 100) AS SrWypelnienie
                FROM dbo.Kierowca k
                LEFT JOIN dbo.Kurs kurs ON k.KierowcaID = kurs.KierowcaID
                    AND kurs.DataKursu >= @DataOd AND kurs.DataKursu <= @DataDo
                LEFT JOIN dbo.Ladunek l ON kurs.KursID = l.KursID
                WHERE k.Aktywny = 1
                GROUP BY k.KierowcaID, k.Imie, k.Nazwisko
                ORDER BY LiczbaKursow DESC";

            var dt = new DataTable();
            dt.Columns.Add("Kierowca", typeof(string));
            dt.Columns.Add("Kursy", typeof(int));
            dt.Columns.Add("Pojemniki E2", typeof(int));
            dt.Columns.Add("Palety", typeof(int));
            dt.Columns.Add("Ładunki", typeof(int));
            dt.Columns.Add("Śr. wypełnienie %", typeof(string));

            using var cn = new SqlConnection(_connTransport);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@DataOd", dtpOd.Value.Date);
            cmd.Parameters.AddWithValue("@DataDo", dtpDo.Value.Date);

            using var reader = await cmd.ExecuteReaderAsync();
            int totalKursy = 0, totalPojemniki = 0, totalPalety = 0;

            while (await reader.ReadAsync())
            {
                var kursy = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                var pojemniki = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2));
                var palety = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3));
                var ladunki = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                var wypelnienie = reader.IsDBNull(5) ? 0 : reader.GetDouble(5);

                totalKursy += kursy;
                totalPojemniki += pojemniki;
                totalPalety += palety;

                dt.Rows.Add(
                    reader.GetString(0),
                    kursy,
                    pojemniki,
                    palety,
                    ladunki,
                    wypelnienie > 0 ? $"{wypelnienie:F1}%" : "-"
                );
            }

            if (InvokeRequired)
                Invoke(new Action(() => {
                    dgvKierowcy.DataSource = dt;
                    UpdateSummaryPanel(panelSummaryKierowcy,
                        ("Kierowców aktywnych", dt.Rows.Count),
                        ("Suma kursów", totalKursy),
                        ("Suma pojemników", totalPojemniki),
                        ("Suma palet", totalPalety));
                }));
            else
            {
                dgvKierowcy.DataSource = dt;
                UpdateSummaryPanel(panelSummaryKierowcy,
                    ("Kierowców aktywnych", dt.Rows.Count),
                    ("Suma kursów", totalKursy),
                    ("Suma pojemników", totalPojemniki),
                    ("Suma palet", totalPalety));
            }
        }

        private async Task LoadPojazdyStatystykiAsync()
        {
            var sql = @"
                SELECT
                    p.Rejestracja,
                    p.Marka + ' ' + ISNULL(p.Model, '') AS Pojazd,
                    ISNULL(p.PaletyH1, 0) AS PaletyMax,
                    COUNT(DISTINCT kurs.KursID) AS LiczbaKursow,
                    SUM(ISNULL(l.PojemnikiE2, 0)) AS SumaPojemnikow,
                    SUM(ISNULL(l.PaletyH1, 0)) AS SumaPalet
                FROM dbo.Pojazd p
                LEFT JOIN dbo.Kurs kurs ON p.PojazdID = kurs.PojazdID
                    AND kurs.DataKursu >= @DataOd AND kurs.DataKursu <= @DataDo
                LEFT JOIN dbo.Ladunek l ON kurs.KursID = l.KursID
                WHERE p.Aktywny = 1
                GROUP BY p.PojazdID, p.Rejestracja, p.Marka, p.Model, p.PaletyH1
                ORDER BY LiczbaKursow DESC";

            var dt = new DataTable();
            dt.Columns.Add("Rejestracja", typeof(string));
            dt.Columns.Add("Pojazd", typeof(string));
            dt.Columns.Add("Palety max", typeof(int));
            dt.Columns.Add("Kursy", typeof(int));
            dt.Columns.Add("Pojemniki E2", typeof(int));
            dt.Columns.Add("Palety", typeof(int));

            using var cn = new SqlConnection(_connTransport);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@DataOd", dtpOd.Value.Date);
            cmd.Parameters.AddWithValue("@DataDo", dtpDo.Value.Date);

            using var reader = await cmd.ExecuteReaderAsync();
            int totalKursy = 0;

            while (await reader.ReadAsync())
            {
                var kursy = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                totalKursy += kursy;

                dt.Rows.Add(
                    reader.GetString(0),
                    reader.IsDBNull(1) ? "" : reader.GetString(1),
                    reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    kursy,
                    reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4)),
                    reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5))
                );
            }

            if (InvokeRequired)
                Invoke(new Action(() => {
                    dgvPojazdy.DataSource = dt;
                    UpdateSummaryPanel(panelSummaryPojazdy,
                        ("Pojazdów aktywnych", dt.Rows.Count),
                        ("Suma kursów", totalKursy),
                        ("", 0),
                        ("", 0));
                }));
            else
            {
                dgvPojazdy.DataSource = dt;
                UpdateSummaryPanel(panelSummaryPojazdy,
                    ("Pojazdów aktywnych", dt.Rows.Count),
                    ("Suma kursów", totalKursy),
                    ("", 0),
                    ("", 0));
            }
        }

        private async Task LoadOdbiorcyStatystykiAsync()
        {
            // Pobierz dane z TransportPL
            var sqlTransport = @"
                SELECT
                    l.KodKlienta,
                    COUNT(DISTINCT l.LadunekID) AS LiczbaLadunkow,
                    COUNT(DISTINCT kurs.KursID) AS LiczbaKursow,
                    SUM(ISNULL(l.PojemnikiE2, 0)) AS SumaPojemnikow,
                    SUM(ISNULL(l.PaletyH1, 0)) AS SumaPalet
                FROM dbo.Ladunek l
                INNER JOIN dbo.Kurs kurs ON l.KursID = kurs.KursID
                WHERE kurs.DataKursu >= @DataOd AND kurs.DataKursu <= @DataDo
                GROUP BY l.KodKlienta
                ORDER BY SumaPojemnikow DESC";

            var odbiorcy = new Dictionary<string, (int Ladunki, int Kursy, int Pojemniki, int Palety)>();

            using (var cn = new SqlConnection(_connTransport))
            {
                await cn.OpenAsync();
                using var cmd = new SqlCommand(sqlTransport, cn);
                cmd.Parameters.AddWithValue("@DataOd", dtpOd.Value.Date);
                cmd.Parameters.AddWithValue("@DataDo", dtpDo.Value.Date);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var kod = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    if (!string.IsNullOrEmpty(kod))
                    {
                        odbiorcy[kod] = (
                            reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                            reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                            reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3)),
                            reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4))
                        );
                    }
                }
            }

            var dt = new DataTable();
            dt.Columns.Add("Kod", typeof(string));
            dt.Columns.Add("Odbiorca", typeof(string));
            dt.Columns.Add("Ładunki", typeof(int));
            dt.Columns.Add("Kursy", typeof(int));
            dt.Columns.Add("Pojemniki E2", typeof(int));
            dt.Columns.Add("Palety", typeof(int));

            int totalLadunki = 0, totalPojemniki = 0;

            foreach (var kv in odbiorcy.OrderByDescending(x => x.Value.Pojemniki).Take(50))
            {
                totalLadunki += kv.Value.Ladunki;
                totalPojemniki += kv.Value.Pojemniki;

                dt.Rows.Add(
                    kv.Key,
                    kv.Key, // TODO: można pobrać nazwę z Handel
                    kv.Value.Ladunki,
                    kv.Value.Kursy,
                    kv.Value.Pojemniki,
                    kv.Value.Palety
                );
            }

            if (InvokeRequired)
                Invoke(new Action(() => {
                    dgvOdbiorcy.DataSource = dt;
                    UpdateSummaryPanel(panelSummaryOdbiorcy,
                        ("Odbiorców (top 50)", dt.Rows.Count),
                        ("Suma ładunków", totalLadunki),
                        ("Suma pojemników", totalPojemniki),
                        ("", 0));
                }));
            else
            {
                dgvOdbiorcy.DataSource = dt;
                UpdateSummaryPanel(panelSummaryOdbiorcy,
                    ("Odbiorców (top 50)", dt.Rows.Count),
                    ("Suma ładunków", totalLadunki),
                    ("Suma pojemników", totalPojemniki),
                    ("", 0));
            }
        }

        private async Task LoadOgolneStatystykiAsync()
        {
            var sql = @"
                SELECT
                    CONVERT(VARCHAR(7), kurs.DataKursu, 120) AS Miesiac,
                    COUNT(DISTINCT kurs.KursID) AS LiczbaKursow,
                    COUNT(DISTINCT kurs.KierowcaID) AS LiczbaKierowcow,
                    COUNT(DISTINCT kurs.PojazdID) AS LiczbaPojazdow,
                    SUM(ISNULL(l.PojemnikiE2, 0)) AS SumaPojemnikow,
                    SUM(ISNULL(l.PaletyH1, 0)) AS SumaPalet,
                    COUNT(DISTINCT l.KodKlienta) AS LiczbaOdbiorcow
                FROM dbo.Kurs kurs
                LEFT JOIN dbo.Ladunek l ON kurs.KursID = l.KursID
                WHERE kurs.DataKursu >= @DataOd AND kurs.DataKursu <= @DataDo
                GROUP BY CONVERT(VARCHAR(7), kurs.DataKursu, 120)
                ORDER BY Miesiac DESC";

            var dt = new DataTable();
            dt.Columns.Add("Miesiąc", typeof(string));
            dt.Columns.Add("Kursy", typeof(int));
            dt.Columns.Add("Kierowców", typeof(int));
            dt.Columns.Add("Pojazdów", typeof(int));
            dt.Columns.Add("Pojemniki E2", typeof(int));
            dt.Columns.Add("Palety", typeof(int));
            dt.Columns.Add("Odbiorców", typeof(int));

            using var cn = new SqlConnection(_connTransport);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@DataOd", dtpOd.Value.Date);
            cmd.Parameters.AddWithValue("@DataDo", dtpDo.Value.Date);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                dt.Rows.Add(
                    reader.GetString(0),
                    reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4)),
                    reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5)),
                    reader.IsDBNull(6) ? 0 : reader.GetInt32(6)
                );
            }

            if (InvokeRequired)
                Invoke(new Action(() => dgvOgolne.DataSource = dt));
            else
                dgvOgolne.DataSource = dt;
        }

        private void UpdateSummaryPanel(Panel panel, params (string Label, int Value)[] items)
        {
            panel.Controls.Clear();

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(10),
                BackColor = Color.Transparent
            };

            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.Label)) continue;

                var card = new Panel
                {
                    Size = new Size(180, 55),
                    BackColor = Color.White,
                    Margin = new Padding(5),
                    Padding = new Padding(10)
                };

                var lblTitle = new Label
                {
                    Text = item.Label,
                    Font = new Font("Segoe UI", 8F),
                    ForeColor = Color.FromArgb(120, 130, 140),
                    AutoSize = true,
                    Location = new Point(10, 5)
                };

                var lblValue = new Label
                {
                    Text = item.Value.ToString("N0"),
                    Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(52, 73, 94),
                    AutoSize = true,
                    Location = new Point(10, 25)
                };

                card.Controls.Add(lblTitle);
                card.Controls.Add(lblValue);
                flow.Controls.Add(card);
            }

            panel.Controls.Add(flow);
        }
    }
}
