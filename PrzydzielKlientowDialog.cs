using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    /// <summary>
    /// Dialog do przypisywania klientów CRM do handlowców z priorytetem.
    /// Layout 2026-05-12 — TableLayoutPanel zamiast Dock (deterministyczne pozycjonowanie),
    /// żaden panel nie zasłania zawartości. Styl Tailwind, kompaktowy.
    /// </summary>
    public class PrzydzielKlientowDialog : Form
    {
        // ── Paleta zsynchronizowana z AdminPermissionsWindow ──────────────
        private static readonly Color BgPage = Color.FromArgb(241, 244, 248);
        private static readonly Color Surface = Color.White;
        private static readonly Color SubtleBg = Color.FromArgb(249, 250, 251);
        private static readonly Color TextDark = Color.FromArgb(31, 41, 55);
        private static readonly Color TextGray = Color.FromArgb(107, 114, 128);
        private static readonly Color TextLight = Color.FromArgb(156, 163, 175);
        private static readonly Color BorderLight = Color.FromArgb(229, 231, 235);
        private static readonly Color BorderUltralight = Color.FromArgb(243, 244, 246);
        private static readonly Color Success = Color.FromArgb(16, 185, 129);
        private static readonly Color SuccessHover = Color.FromArgb(5, 150, 105);
        private static readonly Color Info = Color.FromArgb(59, 130, 246);
        private static readonly Color InfoHover = Color.FromArgb(37, 99, 235);
        private static readonly Color Warning = Color.FromArgb(245, 158, 11);
        private static readonly Color SelectedRow = Color.FromArgb(219, 234, 254);

        private readonly string connectionString;
        private DataGridView gridKlienci;
        private CheckedListBox listHandlowcy;
        private Button btnPrzypisz;
        private CheckBox chkPriorytet;
        private ComboBox cmbStatus;
        private TextBox txtSzukaj;
        private Label lblInfo;
        private Label lblHandlowcyCount;
        private Label lblKlienciCount;
        private Timer _searchDebounce;

        public PrzydzielKlientowDialog(string connString)
        {
            connectionString = connString;
            BuildUI();
            LoadHandlowcy();
            LoadKlienci();
        }

        private void BuildUI()
        {
            Text = "Przypisz klientów do handlowców";
            Size = new Size(1280, 720);
            MinimumSize = new Size(1100, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = BgPage;
            Font = new Font("Segoe UI", 9.5f);
            try { WindowIconHelper.SetIcon(this); }
            catch (Exception ex) { Debug.WriteLine($"[PrzydzielKlientow] {ex.Message}"); }

            // ── ROOT TableLayoutPanel: 2 wiersze (toolbar + main) ──────────
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = BgPage
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));   // toolbar
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // main
            Controls.Add(root);

            // ── TOOLBAR (Row 0) — jasny, kompaktowy ────────────────────────
            var toolbar = BuildToolbar();
            root.Controls.Add(toolbar, 0, 0);

            // ── MAIN AREA (Row 1) — 2 kolumny: grid | right panel ──────────
            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = BgPage,
                Padding = new Padding(16, 12, 16, 16)
            };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
            root.Controls.Add(main, 0, 1);

            // GRID po lewej (zaokrąglony border)
            var gridHost = BuildGridHost();
            main.Controls.Add(gridHost, 0, 0);

            // RIGHT panel handlowców (zaokrąglony border)
            var rightPanel = BuildRightPanel();
            rightPanel.Margin = new Padding(12, 0, 0, 0);
            main.Controls.Add(rightPanel, 1, 0);
        }

        // ─────────────────────────────────────────────────────────────────
        // TOOLBAR
        // ─────────────────────────────────────────────────────────────────

        private Panel BuildToolbar()
        {
            var toolbar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Surface
            };
            toolbar.Paint += (s, e) =>
            {
                using var p = new Pen(BorderLight, 1);
                e.Graphics.DrawLine(p, 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1);
            };

            // Tytuł po lewej
            var lblTitle = new Label
            {
                Text = "🎯  Przypisz klientów do handlowców",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Location = new Point(16, 13)
            };
            toolbar.Controls.Add(lblTitle);

            // Akcje po prawej — używamy panelu z FlowLayout (zawsze docs do right)
            var rightFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.Transparent,
                AutoSize = true,
                Padding = new Padding(0, 8, 16, 0)
            };

            // ➤ Odśwież (po prawej najdalej → FlowDirection.RightToLeft dodaje pierwszy = najbardziej na prawo)
            var btnOdswiez = MakeFlatButton("🔄 Odśwież", 94, Info, InfoHover);
            btnOdswiez.Click += (s, e) => LoadKlienci();
            rightFlow.Controls.Add(btnOdswiez);

            // ➤ Wyszukiwarka
            var searchBg = new Panel
            {
                Size = new Size(220, 30),
                BackColor = SubtleBg,
                Margin = new Padding(0, 0, 8, 0)
            };
            searchBg.Paint += (s, e) =>
            {
                using var path = RoundRect(0, 0, searchBg.Width - 1, searchBg.Height - 1, 5);
                using var p = new Pen(BorderLight, 1);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(p, path);
            };
            var searchIcon = new Label
            {
                Text = "🔍", Font = new Font("Segoe UI", 10F), ForeColor = TextGray,
                Location = new Point(8, 5), AutoSize = true, BackColor = Color.Transparent
            };
            txtSzukaj = new TextBox
            {
                Location = new Point(32, 5),
                Size = new Size(180, 22),
                BorderStyle = BorderStyle.None,
                BackColor = SubtleBg,
                Font = new Font("Segoe UI", 10F),
                PlaceholderText = "szukaj klienta…"
            };
            _searchDebounce = new Timer { Interval = 250 };
            _searchDebounce.Tick += (s, e) => { _searchDebounce.Stop(); LoadKlienci(); };
            txtSzukaj.TextChanged += (s, e) => { _searchDebounce.Stop(); _searchDebounce.Start(); };
            searchBg.Controls.Add(searchIcon);
            searchBg.Controls.Add(txtSzukaj);
            rightFlow.Controls.Add(searchBg);

            // ➤ Combo statusu
            cmbStatus = new ComboBox
            {
                Size = new Size(170, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F),
                Margin = new Padding(0, 0, 8, 0)
            };
            cmbStatus.Items.AddRange(new object[] { "Wszystkie nowe", "Do zadzwonienia", "Próba kontaktu", "Nowe", "Wszystkie" });
            cmbStatus.SelectedIndex = 0;
            cmbStatus.SelectedIndexChanged += (s, e) => LoadKlienci();
            rightFlow.Controls.Add(cmbStatus);

            toolbar.Controls.Add(rightFlow);
            toolbar.Resize += (s, e) =>
            {
                rightFlow.Location = new Point(toolbar.Width - rightFlow.Width, 0);
            };
            return toolbar;
        }

        private Button MakeFlatButton(string text, int width, Color bg, Color hover)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(width, 30),
                BackColor = bg, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => btn.BackColor = hover;
            btn.MouseLeave += (s, e) => btn.BackColor = bg;
            return btn;
        }

        // ─────────────────────────────────────────────────────────────────
        // LEFT — Grid klientów (zaokrąglony container)
        // ─────────────────────────────────────────────────────────────────

        private Panel BuildGridHost()
        {
            // Outer: rounded border. Inner: grid.
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Surface,
                Padding = new Padding(1)
            };
            host.Paint += (s, e) =>
            {
                using var p = new Pen(BorderLight, 1);
                using var path = RoundRect(0, 0, host.Width - 1, host.Height - 1, 8);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(p, path);
            };

            gridKlienci = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToResizeColumns = true,
                ReadOnly = true,
                BackgroundColor = Surface,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                EnableHeadersVisualStyles = false,
                GridColor = BorderUltralight,
                Font = new Font("Segoe UI", 9.5F),
                RowTemplate = { Height = 30 },
                ColumnHeadersHeight = 36,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };
            gridKlienci.DefaultCellStyle.SelectionBackColor = SelectedRow;
            gridKlienci.DefaultCellStyle.SelectionForeColor = TextDark;
            gridKlienci.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            gridKlienci.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = SubtleBg
            };
            gridKlienci.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = SubtleBg,
                ForeColor = TextDark,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 8, 0),
                SelectionBackColor = SubtleBg,
                SelectionForeColor = TextDark
            };
            gridKlienci.SelectionChanged += (s, e) => UpdateInfo();

            host.Controls.Add(gridKlienci);
            return host;
        }

        // ─────────────────────────────────────────────────────────────────
        // RIGHT — Handlowcy + akcja
        // ─────────────────────────────────────────────────────────────────

        private Panel BuildRightPanel()
        {
            var host = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Surface,
                Padding = new Padding(14, 12, 14, 12)
            };
            host.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));   // header
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // lista handlowców (rośnie)
            host.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));   // checkbox priorytet
            host.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));   // bottom actions
            host.Paint += (s, e) =>
            {
                using var p = new Pen(BorderLight, 1);
                using var path = RoundRect(0, 0, host.Width - 1, host.Height - 1, 8);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(p, path);
            };

            // HEADER
            var headerPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            var lblHandlowcy = new Label
            {
                Text = "👔  Handlowcy",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = TextDark,
                Location = new Point(0, 0),
                AutoSize = true
            };
            lblHandlowcyCount = new Label
            {
                Text = "Wybierz aby przypisać",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = TextLight,
                Location = new Point(0, 24),
                AutoSize = true
            };
            headerPanel.Controls.Add(lblHandlowcy);
            headerPanel.Controls.Add(lblHandlowcyCount);
            host.Controls.Add(headerPanel, 0, 0);

            // LISTA HANDLOWCÓW (rośnie)
            listHandlowcy = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                Font = new Font("Segoe UI", 10F),
                BorderStyle = BorderStyle.None,
                BackColor = SubtleBg,
                Margin = new Padding(0, 2, 0, 4)
            };
            listHandlowcy.ItemCheck += (s, e) => UpdateHandlowcyCount();
            host.Controls.Add(listHandlowcy, 0, 1);

            // PRIORYTET checkbox
            chkPriorytet = new CheckBox
            {
                Text = "⭐  Priorytet (pokaż jako pierwsze)",
                Dock = DockStyle.Fill,
                AutoSize = false,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Warning,
                Checked = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            host.Controls.Add(chkPriorytet, 0, 2);

            // BOTTOM AKCJE
            var bottomActions = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            lblInfo = new Label
            {
                Text = "Zaznacz klientów (Ctrl/Shift)\ni handlowców obok, potem ✓ Przypisz",
                Location = new Point(0, 0),
                Size = new Size(280, 32),
                ForeColor = TextLight,
                Font = new Font("Segoe UI", 8.5F),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            bottomActions.Controls.Add(lblInfo);

            btnPrzypisz = new Button
            {
                Text = "✓ Przypisz wybranych",
                Size = new Size(280, 40),
                Location = new Point(0, 36),
                BackColor = Success, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            btnPrzypisz.FlatAppearance.BorderSize = 0;
            btnPrzypisz.MouseEnter += (s, e) => btnPrzypisz.BackColor = SuccessHover;
            btnPrzypisz.MouseLeave += (s, e) => btnPrzypisz.BackColor = Success;
            btnPrzypisz.Click += BtnPrzypisz_Click;
            bottomActions.Controls.Add(btnPrzypisz);

            host.Controls.Add(bottomActions, 0, 3);

            return host;
        }

        // ── Helper: zaokrąglony prostokąt ─────────────────────────────────
        private static GraphicsPath RoundRect(int x, int y, int w, int h, int r)
        {
            var path = new GraphicsPath();
            path.AddArc(x, y, r * 2, r * 2, 180, 90);
            path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
            path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ─────────────────────────────────────────────────────────────────
        // STATUS / INFO updates
        // ─────────────────────────────────────────────────────────────────

        private void UpdateHandlowcyCount()
        {
            // ItemCheck fired PRZED zmianą — opóźnij 1 tick
            BeginInvoke((Action)(() =>
            {
                int n = listHandlowcy.CheckedItems.Count;
                lblHandlowcyCount.Text = n == 0 ? "Wybierz aby przypisać" : $"Zaznaczonych: {n}";
            }));
        }

        private void UpdateInfo()
        {
            int sel = gridKlienci.SelectedRows.Count;
            int total = gridKlienci.Rows.Count;
            if (sel > 0)
                lblInfo.Text = $"Zaznaczonych: {sel} z {total} klientów\n(Ctrl/Shift = multi-select)";
            else
                lblInfo.Text = $"Znaleziono {total} klientów.\nZaznacz aby przypisać (Ctrl/Shift).";
        }

        // ─────────────────────────────────────────────────────────────────
        // DATA — bez zmian logiki SQL
        // ─────────────────────────────────────────────────────────────────

        private void LoadHandlowcy()
        {
            listHandlowcy.Items.Clear();
            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();
                using var cmd = new SqlCommand(@"
                    SELECT DISTINCT o.ID, o.Name
                    FROM operators o
                    WHERE o.ID IS NOT NULL AND o.Name IS NOT NULL
                    ORDER BY o.Name", conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string id = reader.GetString(0);
                    string name = reader.IsDBNull(1) ? id : reader.GetString(1);
                    listHandlowcy.Items.Add(new HandlowiecItem { ID = id, Name = name });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania handlowców: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadKlienci()
        {
            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();

                using (var cmdAlter = new SqlCommand(@"
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('WlascicieleOdbiorcow') AND name = 'Priorytet')
                    ALTER TABLE WlascicieleOdbiorcow ADD Priorytet BIT DEFAULT 0", conn))
                {
                    cmdAlter.ExecuteNonQuery();
                }

                string statusFilter = cmbStatus.SelectedIndex switch
                {
                    0 => "AND ISNULL(o.Status, '') IN ('Do zadzwonienia', 'Próba kontaktu', 'Nowe', '')",
                    1 => "AND ISNULL(o.Status, '') = 'Do zadzwonienia'",
                    2 => "AND ISNULL(o.Status, '') = 'Próba kontaktu'",
                    3 => "AND ISNULL(o.Status, '') IN ('Nowe', '')",
                    _ => ""
                };

                bool hasSearch = !string.IsNullOrWhiteSpace(txtSzukaj.Text);
                string searchFilter = hasSearch ? "AND o.Nazwa LIKE @szukaj" : "";

                using var cmd = new SqlCommand($@"
                    SELECT
                        o.ID,
                        o.Nazwa,
                        ISNULL(o.MIASTO, '') as Miasto,
                        ISNULL(o.Status, 'Brak') as Status,
                        ISNULL(o.Telefon_K, '') as Telefon,
                        (SELECT COUNT(*) FROM WlascicieleOdbiorcow w WHERE w.IDOdbiorcy = o.ID) as LiczbaHandlowcow,
                        ISNULL((SELECT STRING_AGG(op.Name, ', ')
                                FROM WlascicieleOdbiorcow w2
                                JOIN operators op ON w2.OperatorID = op.ID
                                WHERE w2.IDOdbiorcy = o.ID), '') as PrzypisaniDo
                    FROM OdbiorcyCRM o
                    WHERE 1=1 {statusFilter} {searchFilter}
                    ORDER BY o.Nazwa", conn);

                if (hasSearch) cmd.Parameters.AddWithValue("@szukaj", "%" + txtSzukaj.Text + "%");

                var dt = new DataTable();
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(dt);

                gridKlienci.DataSource = dt;

                if (gridKlienci.Columns.Contains("ID")) gridKlienci.Columns["ID"].Visible = false;
                if (gridKlienci.Columns.Contains("Nazwa"))
                {
                    gridKlienci.Columns["Nazwa"].HeaderText = "Nazwa firmy";
                    gridKlienci.Columns["Nazwa"].FillWeight = 26;
                }
                if (gridKlienci.Columns.Contains("Miasto")) gridKlienci.Columns["Miasto"].FillWeight = 14;
                if (gridKlienci.Columns.Contains("Status")) gridKlienci.Columns["Status"].FillWeight = 13;
                if (gridKlienci.Columns.Contains("Telefon")) gridKlienci.Columns["Telefon"].FillWeight = 12;
                if (gridKlienci.Columns.Contains("LiczbaHandlowcow"))
                {
                    gridKlienci.Columns["LiczbaHandlowcow"].HeaderText = "Przyp.";
                    gridKlienci.Columns["LiczbaHandlowcow"].FillWeight = 7;
                    gridKlienci.Columns["LiczbaHandlowcow"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }
                if (gridKlienci.Columns.Contains("PrzypisaniDo"))
                {
                    gridKlienci.Columns["PrzypisaniDo"].HeaderText = "Przypisani handlowcy";
                    gridKlienci.Columns["PrzypisaniDo"].FillWeight = 28;
                }

                UpdateInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania klientów: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnPrzypisz_Click(object sender, EventArgs e)
        {
            var zaznaczeniKlienci = new List<int>();
            foreach (DataGridViewRow row in gridKlienci.SelectedRows)
                if (row.Cells["ID"].Value != null)
                    zaznaczeniKlienci.Add(Convert.ToInt32(row.Cells["ID"].Value));

            var zaznaczeniHandlowcy = new List<string>();
            foreach (var item in listHandlowcy.CheckedItems)
                if (item is HandlowiecItem h) zaznaczeniHandlowcy.Add(h.ID);

            if (zaznaczeniKlienci.Count == 0)
            {
                MessageBox.Show("Zaznacz przynajmniej jednego klienta w tabeli.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (zaznaczeniHandlowcy.Count == 0)
            {
                MessageBox.Show("Zaznacz przynajmniej jednego handlowca na liście po prawej.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool priorytet = chkPriorytet.Checked;

            var result = MessageBox.Show(
                $"Czy na pewno chcesz przypisać {zaznaczeniKlienci.Count} klientów do {zaznaczeniHandlowcy.Count} handlowców?\n\n" +
                (priorytet ? "⭐ Z PRIORYTETEM — pojawią się jako pierwsi!" : "Bez priorytetu"),
                "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            try
            {
                int dodano = 0;
                int pominięto = 0;
                using var conn = new SqlConnection(connectionString);
                conn.Open();

                foreach (int klientId in zaznaczeniKlienci)
                {
                    foreach (string handlowiecId in zaznaczeniHandlowcy)
                    {
                        using var cmdCheck = new SqlCommand(
                            "SELECT COUNT(*) FROM WlascicieleOdbiorcow WHERE IDOdbiorcy = @klient AND OperatorID = @handlowiec", conn);
                        cmdCheck.Parameters.AddWithValue("@klient", klientId);
                        cmdCheck.Parameters.AddWithValue("@handlowiec", handlowiecId);
                        int exists = (int)cmdCheck.ExecuteScalar();

                        if (exists > 0)
                        {
                            if (priorytet)
                            {
                                using var cmdUpdate = new SqlCommand(
                                    "UPDATE WlascicieleOdbiorcow SET Priorytet = 1 WHERE IDOdbiorcy = @klient AND OperatorID = @handlowiec", conn);
                                cmdUpdate.Parameters.AddWithValue("@klient", klientId);
                                cmdUpdate.Parameters.AddWithValue("@handlowiec", handlowiecId);
                                cmdUpdate.ExecuteNonQuery();
                            }
                            pominięto++;
                        }
                        else
                        {
                            using var cmdInsert = new SqlCommand(
                                "INSERT INTO WlascicieleOdbiorcow (IDOdbiorcy, OperatorID, Priorytet) VALUES (@klient, @handlowiec, @priorytet)", conn);
                            cmdInsert.Parameters.AddWithValue("@klient", klientId);
                            cmdInsert.Parameters.AddWithValue("@handlowiec", handlowiecId);
                            cmdInsert.Parameters.AddWithValue("@priorytet", priorytet ? 1 : 0);
                            cmdInsert.ExecuteNonQuery();
                            dodano++;
                        }
                    }
                }

                MessageBox.Show(
                    $"Zakończono!\n\nDodano nowych przypisań: {dodano}\nPominięto (już istniały): {pominięto}" +
                    (priorytet ? "\n\n⭐ Klienci pojawią się jako pierwsi u handlowców!" : ""),
                    "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);

                LoadKlienci();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas przypisywania: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private class HandlowiecItem
        {
            public string ID { get; set; } = "";
            public string Name { get; set; } = "";
            public override string ToString() => Name;
        }
    }
}
