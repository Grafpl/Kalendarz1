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
        private readonly BindingSource _missingBS = new();
        private DataTable _missingTable = new();
        private Panel _missingPanel;
        private DataGridView dgvMissing = new();
        private List<KeyValuePair<int, string>> _priceTypeList = new();
        private Font _strikeFont;
        public string UserID { get; set; }
        
        // Nowe przyciski dla funkcji oceny
        private ToolStripButton btnOcena;
        private ToolStripButton btnHistoriaOcen;
        private ToolStripSeparator separatorOceny;

        public WidokKontrahenci()
        {
            InitializeComponent();
            BuildDetailsPanel();

            // Ulepszony wygląd
            this.Font = new Font("Segoe UI", 9.5f);
            this.BackColor = Color.FromArgb(245, 247, 250);
            
            dgvSuppliers.EnableDoubleBuffering();
            dgvDeliveries.EnableDoubleBuffering();

            _suppliersBS.DataSource = _suppliersTable;
            _deliveriesBS.DataSource = _deliveriesTable;

            this.Load += async (_, __) =>
            {
                try
                {
                    BuildSuppliersColumns();
                    ApplyModernStyling(); // Ulepszone style
                    AddEvaluationButtons(); // Dodaj przyciski oceny
                    
                    dgvSuppliers.DataSource = _suppliersBS;
                    dgvDeliveries.DataSource = _deliveriesBS;
                    
                    await LoadPriceTypesAsync();
                    await LoadSuppliersPageAsync();
                    await LoadMissingAsync();
                    
                    // Pokaż statystyki ocen
                    await LoadEvaluationStatistics();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd inicjalizacji: " + ex.Message, "Błąd Krytyczny", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            BuildMissingPanel();
            BuildEvaluationPanel(); // Nowy panel ze statystykami ocen

            // Event handlery
            dgvSuppliers.SelectionChanged += async (_, __) => await LoadSelectedSupplierDetailsAsync();
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

        // ==================== NOWE FUNKCJE OCENY ====================
        
        /// <summary>
        /// Dodaje przyciski związane z oceną dostawców
        /// </summary>
        private void AddEvaluationButtons()
        {
            // Separator
            separatorOceny = new ToolStripSeparator();
            toolStrip.Items.Add(separatorOceny);
            
            // Przycisk Ocena
            btnOcena = new ToolStripButton
            {
                Text = "📋 Ocena",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = Color.FromArgb(255, 193, 7),
                ForeColor = Color.Black,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = "Oceń wybranego dostawcę",
                Padding = new Padding(5)
            };
            btnOcena.Click += BtnOcena_Click;
            toolStrip.Items.Add(btnOcena);
            
            // Przycisk Historia Ocen
            btnHistoriaOcen = new ToolStripButton
            {
                Text = "📜 Historia",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = Color.FromArgb(243, 156, 18),
                ForeColor = Color.White,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = "Zobacz historię ocen dostawcy",
                Padding = new Padding(5)
            };
            btnHistoriaOcen.Click += BtnHistoriaOcen_Click;
            toolStrip.Items.Add(btnHistoriaOcen);
        }
        
        /// <summary>
        /// Obsługa przycisku Ocena
        /// </summary>
        private void BtnOcena_Click(object sender, EventArgs e)
        {
            if (dgvSuppliers.CurrentRow?.DataBoundItem is not DataRowView rv)
            {
                MessageBox.Show(
                    "❗ Wybierz hodowcę z listy!\n\n" +
                    "Zaznacz wiersz hodowcy którego chcesz ocenić.",
                    "Wybierz hodowcę",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
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
                            "✅ Ocena została zapisana pomyślnie!\n\n" +
                            $"Hodowca: {nazwaHodowcy}\n" +
                            $"Punkty: {oknoOceny.PunktyRazem}",
                            "Sukces",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        
                        // Odśwież widok
                        _ = LoadSuppliersPageAsync();
                        _ = LoadEvaluationStatistics();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd podczas otwierania okna oceny:\n\n{ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// Obsługa przycisku Historia Ocen
        /// </summary>
        private void BtnHistoriaOcen_Click(object sender, EventArgs e)
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
                using (var oknoHistorii = new HistoriaOcenForm(idHodowcy, nazwaHodowcy))
                {
                    oknoHistorii.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd podczas otwierania historii:\n\n{ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// Panel ze statystykami ocen
        /// </summary>
        private Panel _evaluationStatsPanel;
        private Label lblEvaluationStats;
        
        private void BuildEvaluationPanel()
        {
            _evaluationStatsPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(52, 73, 94),
                Padding = new Padding(10, 8, 10, 8)
            };
            
            lblEvaluationStats = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "📊 Ładowanie statystyk ocen..."
            };
            
            _evaluationStatsPanel.Controls.Add(lblEvaluationStats);
            Controls.Add(_evaluationStatsPanel);
        }
        
        /// <summary>
        /// Ładuje statystyki ocen
        /// </summary>
        private async Task LoadEvaluationStatistics()
        {
            try
            {
                string query = @"
                    SELECT 
                        COUNT(DISTINCT DostawcaID) as LiczbaOcenionych,
                        COUNT(*) as LiczbaOcen,
                        AVG(CAST(PunktyRazem as FLOAT)) as SredniaPunktow,
                        MAX(DataOceny) as OstatniaOcena
                    FROM [LibraNet].[dbo].[OcenyDostawcow]
                    WHERE Status = 'Aktywna'";
                
                using var con = new SqlConnection(connectionString);
                using var cmd = new SqlCommand(query, con);
                await con.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    int liczbaOcenionych = reader["LiczbaOcenionych"] != DBNull.Value ? 
                        Convert.ToInt32(reader["LiczbaOcenionych"]) : 0;
                    int liczbaOcen = reader["LiczbaOcen"] != DBNull.Value ? 
                        Convert.ToInt32(reader["LiczbaOcen"]) : 0;
                    double srednia = reader["SredniaPunktow"] != DBNull.Value ? 
                        Convert.ToDouble(reader["SredniaPunktow"]) : 0;
                    DateTime? ostatnia = reader["OstatniaOcena"] != DBNull.Value ? 
                        Convert.ToDateTime(reader["OstatniaOcena"]) : null;
                    
                    string statText = $"📊 STATYSTYKI OCEN: " +
                        $"Ocenionych dostawców: {liczbaOcenionych} | " +
                        $"Łączna liczba ocen: {liczbaOcen} | " +
                        $"Średnia punktów: {srednia:F1} | ";
                    
                    if (ostatnia.HasValue)
                    {
                        statText += $"Ostatnia ocena: {ostatnia:dd.MM.yyyy}";
                    }
                    
                    lblEvaluationStats.Text = statText;
                    
                    // Kolorowanie w zależności od średniej
                    if (srednia >= 30)
                        _evaluationStatsPanel.BackColor = Color.FromArgb(39, 174, 96);
                    else if (srednia >= 20)
                        _evaluationStatsPanel.BackColor = Color.FromArgb(243, 156, 18);
                    else
                        _evaluationStatsPanel.BackColor = Color.FromArgb(231, 76, 60);
                }
            }
            catch
            {
                lblEvaluationStats.Text = "📊 System ocen dostawców gotowy do użycia";
            }
        }
        
        // ==================== ULEPSZONY WYGLĄD ====================
        
        private void ApplyModernStyling()
        {
            // Podstawowe ustawienia siatki
            dgvSuppliers.AllowUserToAddRows = false;
            dgvSuppliers.RowHeadersVisible = false;
            dgvSuppliers.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvSuppliers.MultiSelect = false;
            dgvSuppliers.AutoGenerateColumns = false;
            dgvSuppliers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvSuppliers.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dgvSuppliers.RowTemplate.Height = 48;
            dgvSuppliers.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            
            // Ulepszone kolory i czcionki
            dgvSuppliers.BackgroundColor = Color.FromArgb(250, 251, 252);
            dgvSuppliers.GridColor = Color.FromArgb(230, 234, 237);
            dgvSuppliers.BorderStyle = BorderStyle.None;
            
            // Nagłówki kolumn
            dgvSuppliers.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            dgvSuppliers.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 73, 94);
            dgvSuppliers.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvSuppliers.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 73, 94);
            dgvSuppliers.ColumnHeadersHeight = 40;
            dgvSuppliers.EnableHeadersVisualStyles = false;
            
            // Wiersze
            dgvSuppliers.DefaultCellStyle.Font = new Font("Segoe UI", 9.5f);
            dgvSuppliers.DefaultCellStyle.BackColor = Color.White;
            dgvSuppliers.DefaultCellStyle.ForeColor = Color.FromArgb(44, 62, 80);
            dgvSuppliers.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
            dgvSuppliers.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvSuppliers.DefaultCellStyle.Padding = new Padding(5, 3, 5, 3);
            
            // Naprzemienne wiersze
            dgvSuppliers.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);
            dgvSuppliers.AlternatingRowsDefaultCellStyle.ForeColor = Color.FromArgb(44, 62, 80);
            
            // ToolStrip styling
            toolStrip.BackColor = Color.FromArgb(236, 240, 241);
            toolStrip.RenderMode = ToolStripRenderMode.Professional;
            toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            
            // StatusStrip styling
            statusStrip.BackColor = Color.FromArgb(44, 62, 80);
            statusStrip.ForeColor = Color.White;
            lblCount.ForeColor = Color.White;
            
            // Ulepsz przyciski
            foreach (ToolStripItem item in toolStrip.Items)
            {
                if (item is ToolStripButton btn)
                {
                    btn.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                    btn.Margin = new Padding(2);
                    btn.Padding = new Padding(5, 2, 5, 2);
                }
            }
        }
        
        /// <summary>
        /// Podwójne kliknięcie otwiera ocenę
        /// </summary>
        private void DgvSuppliers_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                BtnOcena_Click(sender, e);
            }
        }

        // ==================== ISTNIEJĄCE METODY (BEZ ZMIAN) ====================
        
        private void BuildMissingPanel()
        {
            _missingPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 220,
                Padding = new Padding(8),
                BackColor = Color.White
            };

            var header = new Label
            {
                Text = "⚠️ Braki w danych dostawców (uzupełnij i zniknie z listy)",
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Color.FromArgb(231, 76, 60),
                Padding = new Padding(0, 0, 0, 6)
            };

            dgvMissing.Dock = DockStyle.Fill;
            dgvMissing.ReadOnly = true;
            dgvMissing.AllowUserToAddRows = false;
            dgvMissing.AllowUserToDeleteRows = false;
            dgvMissing.RowHeadersVisible = false;
            dgvMissing.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvMissing.AutoGenerateColumns = false;
            dgvMissing.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvMissing.RowTemplate.Height = 36;
            dgvMissing.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvMissing.BackgroundColor = Color.FromArgb(250, 251, 252);

            // Kolumny
            dgvMissing.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ID",
                HeaderText = "ID",
                DataPropertyName = "ID",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells
            });
            dgvMissing.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Name",
                HeaderText = "Nazwa",
                DataPropertyName = "Name",
                FillWeight = 140
            });
            dgvMissing.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Missing",
                HeaderText = "Brakuje",
                DataPropertyName = "Missing",
                FillWeight = 160
            });

            dgvMissing.DataSource = _missingBS;

            // podwójne kliknięcie — skocz do rekordu w głównej siatce
            dgvMissing.CellDoubleClick += (_, e) =>
            {
                if (e.RowIndex < 0) return;
                if (dgvMissing.Rows[e.RowIndex].DataBoundItem is DataRowView rv)
                {
                    var id = rv.Row["ID"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(id))
                        SelectSupplierById(id);
                }
            };

            _missingPanel.Controls.Add(dgvMissing);
            _missingPanel.Controls.Add(header);
            Controls.Add(_missingPanel);
        }

        private async Task LoadMissingAsync()
        {
            const string q = @"
SELECT
    D.ID,
    D.Name,
    LTRIM(STUFF(
        (CASE WHEN NULLIF(LTRIM(RTRIM(D.City       )),'') IS NULL THEN ', Miasto'     ELSE '' END) +
        (CASE WHEN NULLIF(LTRIM(RTRIM(D.PostalCode )),'') IS NULL THEN ', Kod'        ELSE '' END) +
        (CASE WHEN NULLIF(LTRIM(RTRIM(D.Address    )),'') IS NULL THEN ', Adres'      ELSE '' END) +
        (CASE WHEN (NULLIF(LTRIM(RTRIM(D.Phone1)),'') IS NULL
                AND NULLIF(LTRIM(RTRIM(D.Phone2)),'') IS NULL
                AND NULLIF(LTRIM(RTRIM(D.Phone3)),'') IS NULL)
              THEN ', Telefon' ELSE '' END) +
        (CASE WHEN (NULLIF(LTRIM(RTRIM(D.Nip  )),'') IS NULL
                AND NULLIF(LTRIM(RTRIM(D.Pesel)),'') IS NULL)
              THEN ', NIP/PESEL' ELSE '' END) +
        (CASE WHEN NULLIF(LTRIM(RTRIM(D.Email)),'') IS NULL THEN ', Email' ELSE '' END) +
        (CASE WHEN D.PriceTypeID IS NULL THEN ', Typ ceny' ELSE '' END)
    ,1,2,'')) AS Missing
FROM LibraNet.dbo.Dostawcy D
WHERE
    (NULLIF(LTRIM(RTRIM(D.City       )),'') IS NULL
     OR NULLIF(LTRIM(RTRIM(D.PostalCode )),'') IS NULL
     OR NULLIF(LTRIM(RTRIM(D.Address    )),'') IS NULL
     OR (NULLIF(LTRIM(RTRIM(D.Phone1)),'') IS NULL
         AND NULLIF(LTRIM(RTRIM(D.Phone2)),'') IS NULL
         AND NULLIF(LTRIM(RTRIM(D.Phone3)),'') IS NULL)
     OR (NULLIF(LTRIM(RTRIM(D.Nip  )),'') IS NULL
         AND NULLIF(LTRIM(RTRIM(D.Pesel)),'') IS NULL)
     OR NULLIF(LTRIM(RTRIM(D.Email)),'') IS NULL
     OR D.PriceTypeID IS NULL)
ORDER BY D.ID DESC;";

            using var con = new SqlConnection(connectionString);
            using var cmd = new SqlCommand(q, con);
            await con.OpenAsync();

            using var da = new SqlDataAdapter(cmd);
            var dt = new DataTable();
            await Task.Run(() => da.Fill(dt));
            _missingTable = dt;
            _missingBS.DataSource = _missingTable;
        }

        private void SelectSupplierById(string id)
        {
            for (int i = 0; i < dgvSuppliers.Rows.Count; i++)
            {
                var row = dgvSuppliers.Rows[i];
                if (row.DataBoundItem is DataRowView rv && string.Equals(rv.Row["ID"]?.ToString(), id, StringComparison.OrdinalIgnoreCase))
                {
                    dgvSuppliers.ClearSelection();
                    row.Selected = true;
                    dgvSuppliers.FirstDisplayedScrollingRowIndex = i;
                    break;
                }
            }
        }

        private void BuildDetailsPanel()
        {
            var panelDet = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 12, Padding = new Padding(10), AutoScroll = true };
            panelDet.BackColor = Color.White;
            panelDet.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            panelDet.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            panelDet.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            panelDet.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            
            int r = 0;
            Label L(string t) => new Label { 
                Text = t, 
                AutoSize = true, 
                Anchor = AnchorStyles.Left, 
                Padding = new Padding(0, 6, 0, 0),
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = Color.FromArgb(52, 73, 94)
            };
            TextBox T(TextBox txt) { 
                txt.ReadOnly = true; 
                txt.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                txt.Font = new Font("Segoe UI", 9f);
                txt.BorderStyle = BorderStyle.FixedSingle;
                return txt; 
            }
            
            panelDet.Controls.Add(L("ID"), 0, r); panelDet.Controls.Add(T(txtDetId), 1, r);
            panelDet.Controls.Add(L("Nazwa"), 0, ++r); panelDet.Controls.Add(T(txtDetName), 1, r);
            panelDet.Controls.Add(L("Skrót"), 0, ++r); panelDet.Controls.Add(T(txtDetShort), 1, r);
            panelDet.Controls.Add(L("Miasto"), 0, ++r); panelDet.Controls.Add(T(txtDetCity), 1, r);
            panelDet.Controls.Add(L("Adres"), 0, ++r); panelDet.Controls.Add(T(txtDetAddress), 1, r);
            panelDet.Controls.Add(L("Kod"), 0, ++r); panelDet.Controls.Add(T(txtDetPostal), 1, r);
            panelDet.Controls.Add(L("Telefon"), 0, ++r); panelDet.Controls.Add(T(txtDetPhone), 1, r);
            panelDet.Controls.Add(L("Email"), 0, ++r); panelDet.Controls.Add(T(txtDetEmail), 1, r);
            panelDet.Controls.Add(L("Halt"), 0, ++r); chkDetHalt.Anchor = AnchorStyles.Left; chkDetHalt.Enabled = false; panelDet.Controls.Add(chkDetHalt, 1, r);
            r = 0;
            panelDet.Controls.Add(L("NIP"), 2, r); panelDet.Controls.Add(T(txtDetNip), 3, r);
            panelDet.Controls.Add(L("REGON"), 2, ++r); panelDet.Controls.Add(T(txtDetRegon), 3, r);
            panelDet.Controls.Add(L("PESEL"), 2, ++r); panelDet.Controls.Add(T(txtDetPesel), 3, r);
            panelDet.Controls.Add(L("Typ Ceny"), 2, ++r); panelDet.Controls.Add(T(txtDetTypCeny), 3, r);
            panelDet.Controls.Add(L("KM"), 2, ++r); panelDet.Controls.Add(T(txtDetKm), 3, r);
            panelDet.Controls.Add(L("Dodatek"), 2, ++r); panelDet.Controls.Add(T(txtDetDodatek), 3, r);
            panelDet.Controls.Add(L("Ubytek"), 2, ++r); panelDet.Controls.Add(T(txtDetUbytek), 3, r);
            panelDet.Controls.Add(L("Ost. dostawa"), 2, ++r); panelDet.Controls.Add(T(txtDetOstatnie), 3, r);
            
            // Dodaj info o ocenie
            r++;
            panelDet.Controls.Add(L("📊 Ostatnia ocena"), 2, r); 
            txtDetOstatniaOcena = new TextBox();
            panelDet.Controls.Add(T(txtDetOstatniaOcena), 3, r);
            
            r++;
            panelDet.Controls.Add(L("🏆 Punkty"), 2, r);
            txtDetPunktyOceny = new TextBox();
            panelDet.Controls.Add(T(txtDetPunktyOceny), 3, r);
            
            tabDetails.Controls.Clear();
            tabDetails.Controls.Add(panelDet);
        }
        
        private TextBox txtDetOstatniaOcena;
        private TextBox txtDetPunktyOceny;

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
                sbWhere.Append(" AND (D.Name LIKE @Search OR D.ShortName LIKE @Search OR D.City LIKE @Search OR D.Nip LIKE @Search OR D.Pesel LIKE @Search OR D.Phone1 LIKE @Search OR D.Phone2 LIKE @Search OR D.Phone3 LIKE @Search) ");

            int offset = _pageIndex * _pageSize;
            
            // Dodaj info o ocenach
            string query = $@"
    SELECT 
      D.[ID], D.[ShortName], D.[Name], D.[Address], D.[PostalCode], D.[Halt],
      D.[City], D.[Distance] AS KM, D.[PriceTypeID], PT.[Name] AS PriceTypeName,
      D.[Addition] AS Dodatek, D.[Loss] AS Ubytek,
      D.[Phone1], D.[Phone2], D.[Phone3], D.[Nip], D.[Pesel],
      (SELECT MAX(CreateData) 
         FROM [LibraNet].[dbo].[PartiaDostawca] PD 
        WHERE PD.CustomerID = D.ID) AS OstatnieZdanie,
      LTRIM(RTRIM(ISNULL(D.PostalCode,'') + ' ' + ISNULL(D.City,''))) 
        + CASE WHEN ISNULL(D.Address,'') <> '' THEN CHAR(13)+CHAR(10)+D.Address ELSE '' END AS AddrBlock,
      LTRIM(RTRIM(ISNULL(D.Phone1,'') 
        + CASE WHEN D.Phone2 IS NOT NULL AND D.Phone2<>'' THEN CHAR(13)+CHAR(10)+D.Phone2 ELSE '' END 
        + CASE WHEN D.Phone3 IS NOT NULL AND D.Phone3<>'' THEN CHAR(13)+CHAR(10)+D.Phone3 ELSE '' END)) AS PhoneBlock,
      ISNULL(PT.Name,'') + CHAR(13)+CHAR(10) + 'Dodatek: ' 
        + CASE WHEN D.Addition IS NULL THEN '-' ELSE CONVERT(varchar(32), CAST(D.Addition AS decimal(18,4))) END AS TypeAddBlock,
      LTRIM(RTRIM(CASE WHEN ISNULL(D.Nip,'') <> '' THEN 'NIP: ' + D.Nip ELSE '' END 
        + CASE WHEN ISNULL(D.Pesel,'') <> '' THEN CASE WHEN ISNULL(D.Nip,'') <> '' THEN CHAR(13)+CHAR(10) ELSE '' END + 'PESEL: ' + D.Pesel ELSE '' END)) AS NipPeselBlock,
      -- Dodaj info o ostatniej ocenie
      (SELECT MAX(DataOceny) FROM [LibraNet].[dbo].[OcenyDostawcow] OD WHERE OD.DostawcaID = D.ID AND OD.Status = 'Aktywna') AS OstatniaOcena,
      (SELECT TOP 1 PunktyRazem FROM [LibraNet].[dbo].[OcenyDostawcow] OD WHERE OD.DostawcaID = D.ID AND OD.Status = 'Aktywna' ORDER BY DataOceny DESC) AS OstatniePunkty
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
            lblPage.Text = $"Strona: {_pageIndex + 1}";
            lblCount.Text = $"Rekordy: {newSuppliersTable.Rows.Count}";

            dgvSuppliers.Refresh();
        }

        private async Task ReloadFirstPageAsync() { _pageIndex = 0; await LoadSuppliersPageAsync(); }
        private async Task PrevPageAsync() { if (_pageIndex > 0) { _pageIndex--; await LoadSuppliersPageAsync(); } }
        private async Task NextPageAsync() { if (_hasMore) { _pageIndex++; await LoadSuppliersPageAsync(); } }
        private async Task ReloadPagePreservingSearchAsync() => await LoadSuppliersPageAsync();

        private async Task LoadSelectedSupplierDetailsAsync()
        {
            if (dgvSuppliers.CurrentRow?.DataBoundItem is DataRowView rv)
            {
                FillDetailsPanel(rv.Row);
                
                // Załaduj info o ocenie
                await LoadSupplierEvaluationInfo(SafeGet<string>(rv.Row, "ID"));
            }
        }
        
        private async Task LoadSupplierEvaluationInfo(string dostawcaId)
        {
            if (string.IsNullOrWhiteSpace(dostawcaId))
            {
                txtDetOstatniaOcena.Text = "";
                txtDetPunktyOceny.Text = "";
                return;
            }
            
            try
            {
                string query = @"
                    SELECT TOP 1 DataOceny, PunktyRazem 
                    FROM [LibraNet].[dbo].[OcenyDostawcow] 
                    WHERE DostawcaID = @DostawcaID AND Status = 'Aktywna'
                    ORDER BY DataOceny DESC";
                    
                using var con = new SqlConnection(connectionString);
                using var cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@DostawcaID", dostawcaId);
                
                await con.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    DateTime dataOceny = Convert.ToDateTime(reader["DataOceny"]);
                    int punkty = Convert.ToInt32(reader["PunktyRazem"]);
                    
                    txtDetOstatniaOcena.Text = dataOceny.ToString("dd.MM.yyyy");
                    txtDetPunktyOceny.Text = $"{punkty} pkt";
                    
                    // Kolorowanie
                    if (punkty >= 30)
                    {
                        txtDetPunktyOceny.BackColor = Color.FromArgb(200, 255, 200);
                        txtDetPunktyOceny.ForeColor = Color.DarkGreen;
                    }
                    else if (punkty >= 20)
                    {
                        txtDetPunktyOceny.BackColor = Color.FromArgb(255, 255, 200);
                        txtDetPunktyOceny.ForeColor = Color.DarkOrange;
                    }
                    else
                    {
                        txtDetPunktyOceny.BackColor = Color.FromArgb(255, 200, 200);
                        txtDetPunktyOceny.ForeColor = Color.DarkRed;
                    }
                }
                else
                {
                    txtDetOstatniaOcena.Text = "Brak oceny";
                    txtDetPunktyOceny.Text = "";
                    txtDetPunktyOceny.BackColor = SystemColors.Control;
                    txtDetPunktyOceny.ForeColor = SystemColors.ControlText;
                }
            }
            catch
            {
                txtDetOstatniaOcena.Text = "";
                txtDetPunktyOceny.Text = "";
            }
        }

        private void FillDetailsPanel(DataRow r)
        {
            txtDetId.Text = SafeGet<string>(r, "ID");
            txtDetName.Text = SafeGet<string>(r, "Name");
            txtDetShort.Text = SafeGet<string>(r, "ShortName");
            txtDetCity.Text = SafeGet<string>(r, "City");
            txtDetAddress.Text = SafeGet<string>(r, "Address");
            txtDetPostal.Text = SafeGet<string>(r, "PostalCode");
            txtDetPhone.Text = SafeGet<string>(r, "Phone1");
            txtDetEmail.Text = "";
            txtDetNip.Text = SafeGet<string>(r, "Nip");
            txtDetRegon.Text = "";
            txtDetPesel.Text = SafeGet<string>(r, "Pesel");
            txtDetTypCeny.Text = SafeGet<string>(r, "PriceTypeName");
            txtDetKm.Text = SafeGet<int?>(r, "KM")?.ToString() ?? "";
            txtDetDodatek.Text = SafeGet<decimal?>(r, "Dodatek")?.ToString("0.0000") ?? "";
            txtDetUbytek.Text = SafeGet<decimal?>(r, "Ubytek")?.ToString("0.0000") ?? "";
            chkDetHalt.Checked = SafeGet<decimal?>(r, "Halt") == 1;
            var dt = SafeGet<DateTime?>(r, "OstatnieZdanie");
            txtDetOstatnie.Text = dt?.ToString("yyyy-MM-dd") ?? "";
        }

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
                MessageBox.Show("Błąd podczas zapisu zmiany: " + ex.Message);
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
            
            // Sprawdź ocenę
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

            // Kolorowanie na podstawie typu ceny
            switch (typ.Trim().ToLowerInvariant())
            {
                case "rolnicza": e.CellStyle.BackColor = Color.LightGreen; break;
                case "ministerialna": e.CellStyle.BackColor = Color.LightBlue; break;
                case "wolnorynkowa": e.CellStyle.BackColor = Color.LightYellow; break;
                case "łączona": case "laczona": e.CellStyle.BackColor = Color.PaleVioletRed; break;
            }
            
            // Dodaj ikonkę oceny w kolumnie Nazwa
            if (dgvSuppliers.Columns[e.ColumnIndex].Name == "Name" && punktyOceny.HasValue)
            {
                string ikona = punktyOceny >= 30 ? " ✅" : 
                              punktyOceny >= 20 ? " ⚠️" : " ❌";
                e.Value = e.Value?.ToString() + ikona;
            }

            // Jeśli wstrzymany
            if (isHalted)
            {
                e.CellStyle.BackColor = Color.Gainsboro;
                e.CellStyle.ForeColor = Color.DarkGray;
                _strikeFont ??= new Font(dgvSuppliers.Font, FontStyle.Strikeout);
                e.CellStyle.Font = _strikeFont;
            }
        }

        private async Task AddNewSupplierInlineAsync()
        {
            string newId;
            const string getMaxIdQuery = "SELECT MAX(CAST(ID AS INT)) FROM [LibraNet].[dbo].[Dostawcy] WHERE ISNUMERIC(ID) = 1";
            using (var con = new SqlConnection(connectionString))
            {
                using var cmd = new SqlCommand(getMaxIdQuery, con);
                await con.OpenAsync();
                object result = await cmd.ExecuteScalarAsync();
                int maxId = (result == DBNull.Value) ? 0 : Convert.ToInt32(result);
                newId = (maxId + 1).ToString();
            }

            const string insertQuery = "INSERT INTO [LibraNet].[dbo].[Dostawcy] (ID, Name, ShortName) VALUES (@ID, @Name, @ShortName)";
            using (var con = new SqlConnection(connectionString))
            {
                using var cmd = new SqlCommand(insertQuery, con);
                cmd.Parameters.AddWithValue("@ID", newId);
                cmd.Parameters.AddWithValue("@Name", "Nowy hodowca " + newId);
                cmd.Parameters.AddWithValue("@ShortName", "Nowy " + newId);
                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            await ReloadFirstPageAsync();
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

                if (!string.IsNullOrWhiteSpace(f.CreatedSupplierId) && dgvSuppliers.DataSource is BindingSource bs && bs.List is System.ComponentModel.IListSource src)
                {
                    for (int i = 0; i < dgvSuppliers.Rows.Count; i++)
                    {
                        var row = dgvSuppliers.Rows[i];
                        if (row.DataBoundItem is DataRowView rv)
                        {
                            var id = rv.Row["ID"]?.ToString();
                            if (id == f.CreatedSupplierId)
                            {
                                dgvSuppliers.ClearSelection();
                                row.Selected = true;
                                dgvSuppliers.FirstDisplayedScrollingRowIndex = i;
                                break;
                            }
                        }
                    }
                }
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

            var colId = Txt("ID", "ID", "ID", 50, false);
            var colName = Txt("Name", "Nazwa", "Name", 120, true);
            var colShort = Txt("ShortName", "Skrót", "ShortName", 80, true);
            var colAddrBlock = Txt("AddrBlock", "Adres", "AddrBlock", 140, true);
            var colLast = new DataGridViewTextBoxColumn
            {
                Name = "OstatnieZdanie",
                HeaderText = "Ost. dostawa",
                DataPropertyName = "OstatnieZdanie",
                SortMode = DataGridViewColumnSortMode.Automatic,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            };
            var colPhoneBlk = Txt("PhoneBlock", "Telefon", "PhoneBlock", 90, true);
            var colTypeAdd = Txt("TypeAddBlock", "Typ + Dodatek", "TypeAddBlock", 90, true);
            var colLoss = Txt("Ubytek", "Ubytek", "Ubytek", 60, false);
            var colNipPesel = Txt("NipPeselBlock", "NIP / PESEL", "NipPeselBlock", 90, true);
            
            // Nowa kolumna - ocena
            var colOcena = new DataGridViewTextBoxColumn
            {
                Name = "OstatniePunkty",
                HeaderText = "📊 Ocena",
                DataPropertyName = "OstatniePunkty",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold)
                }
            };
            
            var colHalt = new DataGridViewCheckBoxColumn
            {
                Name = "Halt",
                HeaderText = "Wstrzymany",
                DataPropertyName = "Halt",
                ThreeState = false,
                TrueValue = 1,
                FalseValue = 0,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            };

            dgvSuppliers.Columns.AddRange(new DataGridViewColumn[]
            {
                colId, colName, colShort, colAddrBlock, colLast, colPhoneBlk, 
                colTypeAdd, colLoss, colNipPesel, colOcena, colHalt
            });
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            // Obsługa już jest
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