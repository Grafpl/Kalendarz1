using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace Kalendarz1
{
    public partial class WidokKontrahenci : Form
    {
        private const string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private int _pageSize = 2000;
        private int _pageIndex = 0;
        private bool _hasMore = false;
        private readonly BindingSource _suppliersBS = new();
        private readonly BindingSource _deliveriesBS = new();
        private readonly DataTable _suppliersTable = new();
        private readonly DataTable _deliveriesTable = new();
        private List<KeyValuePair<int, string>> _priceTypeList = new();
        private Font _strikeFont;
        public string UserID { get; set; }

        // Nowe przyciski dla funkcji oceny
        private ToolStripButton btnOcena;
        private ToolStripButton btnHistoriaOcen;
        private ToolStripButton btnGenerujPustyFormularz;
        private ToolStripButton btnGenerujFormularzeDlaWszystkich;
        private ToolStripSeparator separatorOceny;

        // DataGridView dla dostawców
        private DataGridView dgvSuppliers;

        public WidokKontrahenci()
        {
            InitializeComponent();

            // Ulepszony wygląd okna
            this.Font = new Font("Segoe UI", 10f);
            this.BackColor = Color.FromArgb(240, 243, 247);
            this.WindowState = FormWindowState.Maximized;
            this.Text = "📋 KARTOTEKA HODOWCÓW - System Zarządzania Dostawcami";

            dgvSuppliers.EnableDoubleBuffering();
            dgvDeliveries.EnableDoubleBuffering();

            _suppliersBS.DataSource = _suppliersTable;
            _deliveriesBS.DataSource = _deliveriesTable;

            this.Load += async (_, __) =>
            {
                try
                {
                    BuildSuppliersColumns();
                    ApplyModernStyling();
                    AddEvaluationButtons();
                    CustomizeToolStrip();

                    dgvSuppliers.DataSource = _suppliersBS;
                    dgvDeliveries.DataSource = _deliveriesBS;

                    await LoadPriceTypesAsync();
                    await LoadSuppliersPageAsync();

                    // Ukryj panel szczegółów, pokaż tylko dostawy
                    if (tabsRight.TabPages.Contains(tabDetails))
                        tabsRight.TabPages.Remove(tabDetails);

                    // Szerszy panel dostaw (65% hodowcy, 35% dostawy)
                    split.SplitterDistance = (int)(this.Width * 0.65);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd inicjalizacji: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            // Event handlery
            dgvSuppliers.SelectionChanged += async (_, __) => await LoadDeliveriesAsync();
            dgvSuppliers.CellFormatting += dgvSuppliers_CellFormatting;
            dgvSuppliers.CellEndEdit += dgvSuppliers_CellEndEdit;
            dgvSuppliers.DataError += (s, ev) => { ev.ThrowException = false; };
            dgvSuppliers.CellDoubleClick += DgvSuppliers_CellDoubleClick;

            txtSearch.TextChanged += async (_, __) => { _pageIndex = 0; await LoadSuppliersPageAsync(); };
            cmbPriceTypeFilter.SelectedIndexChanged += async (_, __) => await ReloadPagePreservingSearchAsync();
            btnRefresh.Click += async (_, __) => await ReloadFirstPageAsync();
            btnEdit.Click += (_, __) => OpenAkceptacjaWniosku();
            btnAdd.Click += async (_, __) => await OpenNewSupplierFormAsync();
        }

        /// <summary>
        /// Dostosowuje ToolStrip - usuwa "Strona 1" i poprawia wygląd
        /// </summary>
        private void CustomizeToolStrip()
        {
            // Ukryj lblPage (Strona 1)
            if (lblPage != null)
            {
                lblPage.Visible = false;
            }

            // Stylizacja paska narzędzi
            toolStrip.BackColor = Color.FromArgb(52, 73, 94);
            toolStrip.ForeColor = Color.White;
            toolStrip.Padding = new Padding(10, 8, 10, 8);
            toolStrip.GripStyle = ToolStripGripStyle.Hidden;

            // Stylizacja przycisków
            foreach (ToolStripItem item in toolStrip.Items)
            {
                if (item is ToolStripButton btn)
                {
                    btn.ForeColor = Color.White;
                    btn.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
                    btn.Padding = new Padding(12, 6, 12, 6);
                    btn.Margin = new Padding(3, 0, 3, 0);
                }
            }

            // Stylizacja pola wyszukiwania
            if (txtSearch != null)
            {
                txtSearch.Font = new Font("Segoe UI", 11f);
                txtSearch.BackColor = Color.White;
                txtSearch.ForeColor = Color.FromArgb(44, 62, 80);
            }
        }

        // ==================== FUNKCJE OCENY I GENEROWANIA PDF ====================

        private void AddEvaluationButtons()
        {
            // Separator
            separatorOceny = new ToolStripSeparator();
            toolStrip.Items.Add(separatorOceny);

            // Przycisk Ocena
            btnOcena = new ToolStripButton
            {
                Text = "📋 OCENA",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                BackColor = Color.FromArgb(241, 196, 15),
                ForeColor = Color.FromArgb(44, 62, 80),
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = "Oceń wybranego dostawcę",
                Padding = new Padding(12, 6, 12, 6),
                Margin = new Padding(3, 0, 3, 0)
            };
            btnOcena.Click += BtnOcena_Click;
            toolStrip.Items.Add(btnOcena);

            // Przycisk Historia Ocen
            btnHistoriaOcen = new ToolStripButton
            {
                Text = "📜 HISTORIA",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                BackColor = Color.FromArgb(230, 126, 34),
                ForeColor = Color.White,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = "Zobacz historię ocen dostawcy",
                Padding = new Padding(12, 6, 12, 6),
                Margin = new Padding(3, 0, 3, 0)
            };
            btnHistoriaOcen.Click += BtnHistoriaOcen_Click;
            toolStrip.Items.Add(btnHistoriaOcen);

            // Separator
            toolStrip.Items.Add(new ToolStripSeparator());

            // Przycisk - Generuj pusty formularz
            btnGenerujPustyFormularz = new ToolStripButton
            {
                Text = "📝 FORMULARZ PDF",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = "Generuj pusty formularz oceny do wydruku",
                Padding = new Padding(12, 6, 12, 6),
                Margin = new Padding(3, 0, 3, 0)
            };
            btnGenerujPustyFormularz.Click += BtnGenerujPustyFormularz_Click;
            toolStrip.Items.Add(btnGenerujPustyFormularz);

            // Przycisk - Generuj formularze dla wszystkich
            btnGenerujFormularzeDlaWszystkich = new ToolStripButton
            {
                Text = "📋 FORMULARZE ZBIORCZE",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = "Generuj formularze dla wszystkich aktywnych dostawców",
                Padding = new Padding(12, 6, 12, 6),
                Margin = new Padding(3, 0, 3, 0)
            };
            btnGenerujFormularzeDlaWszystkich.Click += BtnGenerujFormularzeDlaWszystkich_Click;
            toolStrip.Items.Add(btnGenerujFormularzeDlaWszystkich);
        }

        private void BtnOcena_Click(object sender, EventArgs e)
        {
            if (dgvSuppliers.CurrentRow?.DataBoundItem is not DataRowView rv)
            {
                MessageBox.Show("❗ Wybierz hodowcę z listy!", "Wybierz hodowcę",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string idHodowcy = SafeGet<string>(rv.Row, "ID");
            string nazwaHodowcy = SafeGet<string>(rv.Row, "Name");

            if (string.IsNullOrWhiteSpace(idHodowcy))
            {
                MessageBox.Show("❌ Nie mogę odczytać ID hodowcy!", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                using (var oknoOceny = new OcenaDostawcyForm(idHodowcy, nazwaHodowcy, UserID ?? Environment.UserName))
                {
                    if (oknoOceny.ShowDialog(this) == DialogResult.OK)
                    {
                        MessageBox.Show(
                            "✅ Ocena została zapisana!\n\n" +
                            $"Hodowca: {nazwaHodowcy}\n" +
                            $"Punkty: {oknoOceny.PunktyRazem}",
                            "Sukces",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

                        _ = LoadSuppliersPageAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnHistoriaOcen_Click(object sender, EventArgs e)
        {
            if (dgvSuppliers.CurrentRow?.DataBoundItem is not DataRowView rv)
            {
                MessageBox.Show("❗ Wybierz hodowcę z listy!", "Wybierz hodowcę",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string idHodowcy = SafeGet<string>(rv.Row, "ID");

            if (string.IsNullOrWhiteSpace(idHodowcy))
            {
                MessageBox.Show("❌ Nie mogę odczytać ID hodowcy!", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                var oknoHistorii = new HistoriaOcenWindow(idHodowcy);
                oknoHistorii.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnGenerujPustyFormularz_Click(object sender, EventArgs e)
        {
            try
            {
                bool pokazPunkty = FormularzDialog.ZapytajOPunktacje();

                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "PDF Files (*.pdf)|*.pdf",
                    FileName = $"Formularz_Oceny_{DateTime.Now:yyyy_MM_dd}.pdf",
                    Title = "Zapisz formularz PDF"
                };

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    var generator = new BlankOcenaFormPDFGenerator();
                    generator.GenerujPustyFormularz(saveDialog.FileName, pokazPunkty);

                    if (File.Exists(saveDialog.FileName))
                    {
                        var fileInfo = new FileInfo(saveDialog.FileName);
                        if (fileInfo.Length > 0)
                        {
                            MessageBox.Show(
                                $"✅ Formularz PDF został wygenerowany!\n\n" +
                                $"Rozmiar: {fileInfo.Length:N0} bajtów",
                                "Sukces",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);

                            try
                            {
                                Process.Start(new ProcessStartInfo(saveDialog.FileName) { UseShellExecute = true });
                            }
                            catch { }
                        }
                        else
                        {
                            MessageBox.Show("❌ Plik PDF ma rozmiar 0 KB!", "Błąd",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd: {ex.Message}\n\n{ex.StackTrace}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnGenerujFormularzeDlaWszystkich_Click(object sender, EventArgs e)
        {
            try
            {
                if (MessageBox.Show(
                    "Wygenerować formularze dla wszystkich aktywnych dostawców?",
                    "Potwierdzenie",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                bool pokazPunkty = FormularzDialog.ZapytajOPunktacje();

                using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "Wybierz folder do zapisu";

                    if (folderDialog.ShowDialog() == DialogResult.OK)
                    {
                        string subFolder = Path.Combine(folderDialog.SelectedPath,
                            $"Formularze_{DateTime.Now:yyyy_MM_dd_HHmm}");
                        Directory.CreateDirectory(subFolder);

                        var dostawcy = await GetActiveSuppliers();

                        if (dostawcy.Count == 0)
                        {
                            MessageBox.Show("Brak aktywnych dostawców.", "Informacja",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        int count = 0;
                        var generator = new BlankOcenaFormPDFGenerator();

                        foreach (var dostawca in dostawcy)
                        {
                            try
                            {
                                string safeName = SanitizeFileName(dostawca.Nazwa);
                                string fileName = Path.Combine(subFolder,
                                    $"Formularz_{safeName}_{dostawca.Id}.pdf");

                                generator.GenerujPustyFormularz(fileName, pokazPunkty);
                                count++;
                            }
                            catch { }
                        }

                        MessageBox.Show($"✅ Wygenerowano {count} formularzy!\n\nFolder: {subFolder}",
                            "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        Process.Start(new ProcessStartInfo(subFolder) { UseShellExecute = true });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task<List<(string Id, string Nazwa)>> GetActiveSuppliers()
        {
            var suppliers = new List<(string Id, string Nazwa)>();

            string query = @"
                SELECT ID, Name 
                FROM [LibraNet].[dbo].[Dostawcy] 
                WHERE Type = 'Dostawca' 
                ORDER BY Name";

            using var connection = new SqlConnection(connectionString);
            using var command = new SqlCommand(query, connection);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                suppliers.Add((
                    reader["ID"].ToString(),
                    reader["Name"].ToString()
                ));
            }

            return suppliers;
        }

        private string SanitizeFileName(string fileName)
        {
            string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            foreach (char c in invalid)
            {
                fileName = fileName.Replace(c.ToString(), "_");
            }
            fileName = fileName.Replace(" ", "_");
            if (fileName.Length > 50)
                fileName = fileName.Substring(0, 50);
            return fileName;
        }

        private void ApplyModernStyling()
        {
            // ============ GŁÓWNY GRID DOSTAWCÓW - PREMIUM DESIGN ============
            dgvSuppliers.AllowUserToAddRows = false;
            dgvSuppliers.RowHeadersVisible = false;
            dgvSuppliers.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvSuppliers.MultiSelect = false;
            dgvSuppliers.AutoGenerateColumns = false;
            dgvSuppliers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvSuppliers.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dgvSuppliers.RowTemplate.Height = 60;
            dgvSuppliers.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

            // Tło grida
            dgvSuppliers.BackgroundColor = Color.FromArgb(241, 245, 249);
            dgvSuppliers.GridColor = Color.FromArgb(203, 213, 225);
            dgvSuppliers.BorderStyle = BorderStyle.None;

            // Nagłówki - granatowe eleganckie
            dgvSuppliers.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 11f);
            dgvSuppliers.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 58, 95);
            dgvSuppliers.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvSuppliers.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(30, 58, 95);
            dgvSuppliers.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 10, 0);
            dgvSuppliers.ColumnHeadersHeight = 55;
            dgvSuppliers.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvSuppliers.EnableHeadersVisualStyles = false;

            // Wiersze - czyste białe
            dgvSuppliers.DefaultCellStyle.Font = new Font("Segoe UI", 10f);
            dgvSuppliers.DefaultCellStyle.BackColor = Color.White;
            dgvSuppliers.DefaultCellStyle.ForeColor = Color.FromArgb(30, 41, 59);
            dgvSuppliers.DefaultCellStyle.SelectionBackColor = Color.FromArgb(59, 130, 246);
            dgvSuppliers.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvSuppliers.DefaultCellStyle.Padding = new Padding(10, 8, 10, 8);

            // Alternatywne wiersze - delikatny szary
            dgvSuppliers.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);

            // ============ GRID DOSTAW - ELEGANCKI DESIGN ============
            dgvDeliveries.AllowUserToAddRows = false;
            dgvDeliveries.RowHeadersVisible = false;
            dgvDeliveries.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvDeliveries.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvDeliveries.RowTemplate.Height = 50;
            dgvDeliveries.BackgroundColor = Color.FromArgb(241, 245, 249);
            dgvDeliveries.GridColor = Color.FromArgb(203, 213, 225);
            dgvDeliveries.BorderStyle = BorderStyle.None;

            // Nagłówki dostaw - złoty akcent
            dgvDeliveries.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 11f);
            dgvDeliveries.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(212, 175, 55);
            dgvDeliveries.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(30, 58, 95);
            dgvDeliveries.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 10, 0);
            dgvDeliveries.ColumnHeadersHeight = 50;
            dgvDeliveries.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvDeliveries.EnableHeadersVisualStyles = false;

            // Wiersze dostaw
            dgvDeliveries.DefaultCellStyle.Font = new Font("Segoe UI", 10f);
            dgvDeliveries.DefaultCellStyle.BackColor = Color.White;
            dgvDeliveries.DefaultCellStyle.ForeColor = Color.FromArgb(30, 41, 59);
            dgvDeliveries.DefaultCellStyle.SelectionBackColor = Color.FromArgb(251, 191, 36);
            dgvDeliveries.DefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 58, 95);
            dgvDeliveries.DefaultCellStyle.Padding = new Padding(10, 8, 10, 8);
            dgvDeliveries.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(254, 252, 232);

            // ============ STATUS BAR - ELEGANCKI ============
            statusStrip.BackColor = Color.FromArgb(30, 58, 95);
            statusStrip.ForeColor = Color.White;
            statusStrip.Font = new Font("Segoe UI", 10f);
            statusStrip.Padding = new Padding(10, 0, 10, 0);
            lblCount.ForeColor = Color.FromArgb(212, 175, 55);
            lblCount.Font = new Font("Segoe UI Semibold", 11f);

            // ============ LABEL DOSTAW - DUŻY I WIDOCZNY ============
            if (lblDeliveries != null)
            {
                lblDeliveries.Font = new Font("Segoe UI Semibold", 13f);
                lblDeliveries.ForeColor = Color.FromArgb(212, 175, 55);
                lblDeliveries.BackColor = Color.FromArgb(30, 58, 95);
                lblDeliveries.Padding = new Padding(10, 8, 10, 8);
                lblDeliveries.AutoSize = false;
                lblDeliveries.Height = 45;
                lblDeliveries.TextAlign = ContentAlignment.MiddleLeft;
            }
        }

        /// <summary>
        /// Podwójne kliknięcie otwiera modyfikację
        /// </summary>
        private void DgvSuppliers_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                OpenAkceptacjaWniosku();
            }
        }

        /// <summary>
        /// Ładuje historię dostaw ZGRUPOWANĄ PO DNIACH
        /// Każdy wiersz = suma z danego dnia (ile aut)
        /// </summary>
        private async Task LoadDeliveriesAsync()
        {
            if (dgvSuppliers.CurrentRow?.DataBoundItem is not DataRowView rv)
            {
                dgvDeliveries.DataSource = null;
                lblDeliveries.Text = "📦 Wybierz hodowcę aby zobaczyć dostawy";
                return;
            }

            string dostawcaId = SafeGet<string>(rv.Row, "ID");
            string dostawcaNazwa = SafeGet<string>(rv.Row, "Name");

            if (string.IsNullOrWhiteSpace(dostawcaId))
            {
                dgvDeliveries.DataSource = null;
                return;
            }

            lblDeliveries.Text = $"📦 DOSTAWY - {dostawcaNazwa}";

            try
            {
                // Zapytanie zgrupowane po dniach - ile aut (wierszy) każdego dnia
                string query = @"
                    SELECT 
                        CAST(PD.CreateData AS DATE) AS [Data],
                        D.Name AS [Hodowca],
                        COUNT(*) AS [Ilość aut]
                    FROM [LibraNet].[dbo].[PartiaDostawca] PD
                    LEFT JOIN [LibraNet].[dbo].[Dostawcy] D ON D.ID = PD.CustomerID
                    WHERE PD.CustomerID = @DostawcaID
                    GROUP BY CAST(PD.CreateData AS DATE), D.Name
                    ORDER BY CAST(PD.CreateData AS DATE) DESC";

                using var con = new SqlConnection(connectionString);
                using var cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@DostawcaID", dostawcaId);

                using var da = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                await Task.Run(() => da.Fill(dt));

                dgvDeliveries.DataSource = dt;

                // Formatowanie kolumn
                if (dgvDeliveries.Columns.Count > 0)
                {
                    if (dgvDeliveries.Columns.Contains("Data"))
                    {
                        dgvDeliveries.Columns["Data"].DefaultCellStyle.Format = "dd.MM.yyyy (dddd)";
                        dgvDeliveries.Columns["Data"].DefaultCellStyle.Font = new Font("Segoe UI Semibold", 11f);
                        dgvDeliveries.Columns["Data"].FillWeight = 60;
                    }
                    if (dgvDeliveries.Columns.Contains("Hodowca"))
                    {
                        dgvDeliveries.Columns["Hodowca"].Visible = false; // Ukryj - już wiemy kto
                    }
                    if (dgvDeliveries.Columns.Contains("Ilość aut"))
                    {
                        dgvDeliveries.Columns["Ilość aut"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        dgvDeliveries.Columns["Ilość aut"].DefaultCellStyle.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
                        dgvDeliveries.Columns["Ilość aut"].DefaultCellStyle.ForeColor = Color.FromArgb(30, 58, 95);
                        dgvDeliveries.Columns["Ilość aut"].DefaultCellStyle.BackColor = Color.FromArgb(254, 243, 199);
                        dgvDeliveries.Columns["Ilość aut"].FillWeight = 40;
                    }
                }
            }
            catch (Exception ex)
            {
                dgvDeliveries.DataSource = null;
                lblDeliveries.Text = $"📦 DOSTAWY - {dostawcaNazwa} (błąd)";
                System.Diagnostics.Debug.WriteLine($"LoadDeliveriesAsync error: {ex.Message}");
            }
        }

        private async Task LoadPriceTypesAsync()
        {
            const string query = @"SELECT [ID], [Name] FROM [LibraNet].[dbo].[PriceType] ORDER BY Name";
            using var con = new SqlConnection(connectionString);
            using var cmd = new SqlCommand(query, con);
            await con.OpenAsync();
            using var rdr = await cmd.ExecuteReaderAsync();
            _priceTypeList = new List<KeyValuePair<int, string>>();
            while (await rdr.ReadAsync())
                _priceTypeList.Add(new KeyValuePair<int, string>(rdr.GetInt32(0), rdr.GetString(1)));
            var filterItems = new List<KeyValuePair<int?, string>> { new(null, "Wszystkie") };
            filterItems.AddRange(_priceTypeList.Select(kv => new KeyValuePair<int?, string>(kv.Key, kv.Value)));
            cmbPriceTypeFilter.ComboBox.DisplayMember = "Value";
            cmbPriceTypeFilter.ComboBox.ValueMember = "Key";
            cmbPriceTypeFilter.ComboBox.DataSource = filterItems;
            cmbPriceTypeFilter.SelectedIndex = 0;
        }

        private async Task LoadSuppliersPageAsync()
        {
            var sbWhere = new StringBuilder(" WHERE 1=1 ");
            var priceTypeFilter = cmbPriceTypeFilter.SelectedItem as KeyValuePair<int?, string>?;
            if (priceTypeFilter.HasValue && priceTypeFilter.Value.Key.HasValue)
                sbWhere.Append(" AND D.PriceTypeID = @PriceTypeID ");

            string searchText = (txtSearch.Text ?? string.Empty).Trim();
            bool hasSearch = searchText.Length >= 2;
            if (hasSearch)
                sbWhere.Append(" AND (D.Name LIKE @Search OR D.ShortName LIKE @Search OR D.City LIKE @Search OR D.Nip LIKE @Search OR D.Pesel LIKE @Search OR D.Phone1 LIKE @Search) ");

            int offset = _pageIndex * _pageSize;

            string query = $@"
    SELECT 
      D.[ID], D.[ShortName], D.[Name], D.[Address], D.[PostalCode], D.[Halt],
      D.[City], D.[Distance] AS KM, D.[PriceTypeID], PT.[Name] AS PriceTypeName,
      D.[Addition] AS Dodatek, D.[Loss] AS Ubytek,
      D.[Phone1], D.[Phone2], D.[Phone3], D.[Nip], D.[Pesel],
      (SELECT MAX(CreateData) FROM [LibraNet].[dbo].[PartiaDostawca] PD WHERE PD.CustomerID = D.ID) AS OstatnieZdanie,
      LTRIM(RTRIM(ISNULL(D.PostalCode,'') + ' ' + ISNULL(D.City,''))) 
        + CASE WHEN ISNULL(D.Address,'') <> '' THEN CHAR(13)+CHAR(10)+D.Address ELSE '' END AS AddrBlock,
      LTRIM(RTRIM(ISNULL(D.Phone1,'') 
        + CASE WHEN D.Phone2 IS NOT NULL AND D.Phone2<>'' THEN ', ' + D.Phone2 ELSE '' END 
        + CASE WHEN D.Phone3 IS NOT NULL AND D.Phone3<>'' THEN ', ' + D.Phone3 ELSE '' END)) AS PhoneBlock,
      (SELECT TOP 1 PunktyRazem FROM [LibraNet].[dbo].[OcenyDostawcow] OD 
       WHERE OD.DostawcaID = D.ID AND OD.Status = 'Aktywna' ORDER BY DataOceny DESC) AS OstatniePunkty,
      (SELECT MAX(DataOceny) FROM [LibraNet].[dbo].[OcenyDostawcow] OD 
       WHERE OD.DostawcaID = D.ID AND OD.Status = 'Aktywna') AS OstatniaOcena,
      (SELECT COUNT(*) FROM [LibraNet].[dbo].[PartiaDostawca] PD WHERE PD.CustomerID = D.ID) AS LiczbaPartii
    FROM [LibraNet].[dbo].[Dostawcy] D 
    LEFT JOIN [LibraNet].[dbo].[PriceType] PT ON PT.ID = D.PriceTypeID
    {sbWhere}
    ORDER BY D.ID DESC 
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

    WITH _x AS (
      SELECT COUNT(1) AS Cnt 
      FROM [LibraNet].[dbo].[Dostawcy] D 
      LEFT JOIN [LibraNet].[dbo].[PriceType] PT ON PT.ID = D.PriceTypeID 
      {sbWhere}
    )
    SELECT CASE WHEN Cnt > @Offset + @PageSize THEN 1 ELSE 0 END AS HasMore FROM _x;";

            var newSuppliersTable = new DataTable();
            var ds = new DataSet();

            using (var con = new SqlConnection(connectionString))
            {
                using (var cmd = new SqlCommand(query, con))
                {
                    if (priceTypeFilter.HasValue && priceTypeFilter.Value.Key.HasValue)
                        cmd.Parameters.AddWithValue("@PriceTypeID", priceTypeFilter.Value.Key.Value);
                    cmd.Parameters.AddWithValue("@Offset", offset);
                    cmd.Parameters.AddWithValue("@PageSize", _pageSize);
                    if (hasSearch) cmd.Parameters.AddWithValue("@Search", "%" + searchText + "%");

                    using (var da = new SqlDataAdapter(cmd))
                    {
                        await Task.Run(() => da.Fill(ds));
                    }
                }
            }

            if (ds.Tables.Count > 0)
            {
                newSuppliersTable = ds.Tables[0];
            }

            _suppliersBS.DataSource = newSuppliersTable;
            _hasMore = (ds.Tables.Count > 1 && ds.Tables[1].Rows.Count > 0 && Convert.ToInt32(ds.Tables[1].Rows[0]["HasMore"]) == 1);
            lblCount.Text = $"📊 Hodowców: {newSuppliersTable.Rows.Count}";

            dgvSuppliers.Refresh();
        }

        private async Task ReloadFirstPageAsync() { _pageIndex = 0; await LoadSuppliersPageAsync(); }
        private async Task ReloadPagePreservingSearchAsync() => await LoadSuppliersPageAsync();

        private async void dgvSuppliers_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                dgvSuppliers.CommitEdit(DataGridViewDataErrorContexts.Commit);
                if (!(_suppliersBS[e.RowIndex] is DataRowView rv)) return;

                var editable = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ShortName", "Name", "Ubytek", "Halt" };
                var col = dgvSuppliers.Columns[e.ColumnIndex];
                if (!editable.Contains(col.Name)) return;

                string id = SafeGet<string>(rv.Row, "ID");
                if (string.IsNullOrWhiteSpace(id)) return;

                object uiVal = dgvSuppliers.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
                object newVal = ConvertValueForDb(col.Name, uiVal);
                await UpdateDatabaseValueAsync(id, col.Name, newVal);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas zapisu: " + ex.Message);
            }
        }

        private static object ConvertValueForDb(string columnName, object uiValue)
        {
            if (uiValue == null || uiValue == DBNull.Value) return DBNull.Value;
            switch (columnName)
            {
                case "Ubytek": return decimal.TryParse(uiValue.ToString(), out var d) ? d : (object)DBNull.Value;
                case "Halt": return (uiValue is bool b && b) || (decimal.TryParse(uiValue.ToString(), out var dec) && dec != 0) ? 1 : 0;
                default: return uiValue;
            }
        }

        private async Task UpdateDatabaseValueAsync(string id, string columnName, object newValue)
        {
            string query = $"UPDATE [LibraNet].[dbo].[Dostawcy] SET [{columnName}] = @NewValue WHERE [ID] = @ID";
            using var con = new SqlConnection(connectionString);
            using var cmd = new SqlCommand(query, con);
            cmd.Parameters.Add("@ID", SqlDbType.VarChar, 10).Value = id;
            cmd.Parameters.AddWithValue("@NewValue", newValue ?? DBNull.Value);
            await con.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        private void dgvSuppliers_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgvSuppliers.Rows[e.RowIndex].DataBoundItem is not DataRowView rv) return;

            string typ = rv.Row["PriceTypeName"] as string ?? "";
            bool isHalted = rv.Row["Halt"] != DBNull.Value && Convert.ToDecimal(rv.Row["Halt"]) == 1;

            int? punktyOceny = rv.Row["OstatniePunkty"] != DBNull.Value ?
                Convert.ToInt32(rv.Row["OstatniePunkty"]) : (int?)null;

            // Reset
            e.CellStyle.Font = dgvSuppliers.Font;
            e.CellStyle.ForeColor = (e.RowIndex % 2 == 0)
                ? dgvSuppliers.DefaultCellStyle.ForeColor
                : dgvSuppliers.AlternatingRowsDefaultCellStyle.ForeColor;
            e.CellStyle.BackColor = (e.RowIndex % 2 == 0)
                ? dgvSuppliers.DefaultCellStyle.BackColor
                : dgvSuppliers.AlternatingRowsDefaultCellStyle.BackColor;

            // Kolorowanie na podstawie typu ceny - subtelne pastelowe
            switch (typ.Trim().ToLowerInvariant())
            {
                case "rolnicza":
                    e.CellStyle.BackColor = Color.FromArgb(220, 252, 231); // Zielony pastelowy
                    break;
                case "ministerialna":
                    e.CellStyle.BackColor = Color.FromArgb(219, 234, 254); // Niebieski pastelowy
                    break;
                case "wolnorynkowa":
                    e.CellStyle.BackColor = Color.FromArgb(254, 249, 195); // Żółty pastelowy
                    break;
                case "łączona":
                case "laczona":
                    e.CellStyle.BackColor = Color.FromArgb(252, 231, 243); // Różowy pastelowy
                    break;
            }

            // Kolorowanie kolumny oceny
            if (dgvSuppliers.Columns[e.ColumnIndex].Name == "OstatniePunkty" && punktyOceny.HasValue)
            {
                if (punktyOceny >= 30)
                {
                    e.CellStyle.BackColor = Color.FromArgb(34, 197, 94);
                    e.CellStyle.ForeColor = Color.White;
                }
                else if (punktyOceny >= 20)
                {
                    e.CellStyle.BackColor = Color.FromArgb(234, 179, 8);
                    e.CellStyle.ForeColor = Color.White;
                }
                else
                {
                    e.CellStyle.BackColor = Color.FromArgb(239, 68, 68);
                    e.CellStyle.ForeColor = Color.White;
                }
            }

            // Jeśli wstrzymany
            if (isHalted)
            {
                e.CellStyle.BackColor = Color.FromArgb(226, 232, 240);
                e.CellStyle.ForeColor = Color.FromArgb(148, 163, 184);
                _strikeFont ??= new Font(dgvSuppliers.Font, FontStyle.Strikeout);
                e.CellStyle.Font = _strikeFont;
            }
        }

        private void OpenAkceptacjaWniosku()
        {
            if (dgvSuppliers.CurrentRow?.DataBoundItem is not DataRowView rv)
            {
                MessageBox.Show("Zaznacz hodowcę na liście.");
                return;
            }
            string idHodowca = SafeGet<string>(rv.Row, "ID");
            if (string.IsNullOrWhiteSpace(idHodowca))
            {
                MessageBox.Show("Brak ID hodowcy.");
                return;
            }
            string appUser = string.IsNullOrWhiteSpace(this.UserID) ? Environment.UserName : this.UserID;
            using (var f = new HodowcaForm(idHodowca, appUser))
                f.ShowDialog(this);
        }

        private static T SafeGet<T>(DataRow r, string col)
        {
            try
            {
                if (!r.Table.Columns.Contains(col) || r[col] == DBNull.Value)
                    return default!;
                var v = r[col];
                var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
                return (T)Convert.ChangeType(v, targetType);
            }
            catch { return default!; }
        }

        private async Task OpenNewSupplierFormAsync()
        {
            using var f = new NewHodowcaForm(connectionString, this.UserID ?? Environment.UserName);
            if (f.ShowDialog(this) == DialogResult.OK)
            {
                _pageIndex = 0;
                await LoadSuppliersPageAsync();
            }
        }

        private void BuildSuppliersColumns()
        {
            dgvSuppliers.Columns.Clear();

            DataGridViewTextBoxColumn Txt(string name, string header, string dataProp, float fillWeight, bool wrap = true)
            {
                var c = new DataGridViewTextBoxColumn
                {
                    Name = name,
                    HeaderText = header,
                    DataPropertyName = dataProp,
                    SortMode = DataGridViewColumnSortMode.Automatic,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    FillWeight = fillWeight
                };
                if (wrap) c.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                return c;
            }

            // ID - mała szara kolumna
            var colId = new DataGridViewTextBoxColumn
            {
                Name = "ID",
                HeaderText = "ID",
                DataPropertyName = "ID",
                Width = 55,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Consolas", 9f),
                    ForeColor = Color.FromArgb(100, 116, 139),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            };

            // Nazwa - główna kolumna
            var colName = Txt("Name", "🏠 HODOWCA", "Name", 200, true);
            colName.DefaultCellStyle.Font = new Font("Segoe UI Semibold", 11f);
            colName.DefaultCellStyle.ForeColor = Color.FromArgb(30, 58, 95);

            // Skrót
            var colShort = Txt("ShortName", "SKRÓT", "ShortName", 60, false);
            colShort.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
            colShort.DefaultCellStyle.ForeColor = Color.FromArgb(100, 116, 139);

            // Adres
            var colAddrBlock = Txt("AddrBlock", "📍 ADRES", "AddrBlock", 180, true);
            colAddrBlock.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
            colAddrBlock.DefaultCellStyle.ForeColor = Color.FromArgb(71, 85, 105);

            // Telefon
            var colPhoneBlk = Txt("PhoneBlock", "📞 TELEFON", "PhoneBlock", 100, true);
            colPhoneBlk.DefaultCellStyle.Font = new Font("Segoe UI", 10f);

            // Ostatnia dostawa
            var colLast = new DataGridViewTextBoxColumn
            {
                Name = "OstatnieZdanie",
                HeaderText = "📦 DOSTAWA",
                DataPropertyName = "OstatnieZdanie",
                Width = 100,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "dd.MM.yyyy",
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 9f)
                }
            };

            // Kolumna oceny - wyróżniona
            var colOcena = new DataGridViewTextBoxColumn
            {
                Name = "OstatniePunkty",
                HeaderText = "⭐",
                DataPropertyName = "OstatniePunkty",
                Width = 60,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 14f, FontStyle.Bold)
                }
            };

            // Checkbox wstrzymany
            var colHalt = new DataGridViewCheckBoxColumn
            {
                Name = "Halt",
                HeaderText = "⛔",
                DataPropertyName = "Halt",
                ThreeState = false,
                TrueValue = 1,
                FalseValue = 0,
                Width = 40
            };

            dgvSuppliers.Columns.AddRange(new DataGridViewColumn[]
            {
                colId, colName, colShort, colAddrBlock, colPhoneBlk, colLast, colOcena, colHalt
            });
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            // Obsługa w konstruktorze
        }
    }

    internal static class DataGridViewExtensions
    {
        public static void EnableDoubleBuffering(this DataGridView dgv)
        {
            typeof(DataGridView).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(dgv, true, null);
        }
    }
}