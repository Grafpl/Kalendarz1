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

            // Ulepszony wygląd
            this.Font = new Font("Segoe UI", 10f);
            this.BackColor = Color.FromArgb(245, 247, 250);

            // Maksymalizuj okno
            this.WindowState = FormWindowState.Maximized;

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

                    dgvSuppliers.DataSource = _suppliersBS;
                    dgvDeliveries.DataSource = _deliveriesBS;

                    await LoadPriceTypesAsync();
                    await LoadSuppliersPageAsync();

                    // Ukryj panel szczegółów, pokaż tylko dostawy
                    if (tabsRight.TabPages.Contains(tabDetails))
                        tabsRight.TabPages.Remove(tabDetails);

                    // Ustaw szerokość paneli
                    split.SplitterDistance = (int)(this.Width * 0.75);
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
                BackColor = Color.FromArgb(255, 193, 7),
                ForeColor = Color.Black,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = "Oceń wybranego dostawcę",
                Padding = new Padding(8, 5, 8, 5),
                Margin = new Padding(2)
            };
            btnOcena.Click += BtnOcena_Click;
            toolStrip.Items.Add(btnOcena);

            // Przycisk Historia Ocen
            btnHistoriaOcen = new ToolStripButton
            {
                Text = "📜 HISTORIA",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                BackColor = Color.FromArgb(243, 156, 18),
                ForeColor = Color.White,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = "Zobacz historię ocen dostawcy",
                Padding = new Padding(8, 5, 8, 5),
                Margin = new Padding(2)
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
                Padding = new Padding(8, 5, 8, 5),
                Margin = new Padding(2)
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
                Padding = new Padding(8, 5, 8, 5),
                Margin = new Padding(2)
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
                // Bez using - WPF Window nie implementuje IDisposable
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
                // Zapytaj o punktację
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

                    MessageBox.Show(
                        "✅ Formularz PDF został wygenerowany!\n\n" +
                        "Gotowy do wydruku i wypełnienia.",
                        "Sukces",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    try
                    {
                        Process.Start(new ProcessStartInfo(saveDialog.FileName) { UseShellExecute = true });
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

                // Zapytaj o punktację
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
            // GridView styling
            dgvSuppliers.AllowUserToAddRows = false;
            dgvSuppliers.RowHeadersVisible = false;
            dgvSuppliers.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvSuppliers.MultiSelect = false;
            dgvSuppliers.AutoGenerateColumns = false;
            dgvSuppliers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvSuppliers.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dgvSuppliers.RowTemplate.Height = 50;
            dgvSuppliers.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

            // Kolory
            dgvSuppliers.BackgroundColor = Color.FromArgb(250, 251, 252);
            dgvSuppliers.GridColor = Color.FromArgb(230, 234, 237);
            dgvSuppliers.BorderStyle = BorderStyle.None;

            // Nagłówki
            dgvSuppliers.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 11f);
            dgvSuppliers.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 73, 94);
            dgvSuppliers.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvSuppliers.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 73, 94);
            dgvSuppliers.ColumnHeadersHeight = 45;
            dgvSuppliers.EnableHeadersVisualStyles = false;

            // Wiersze
            dgvSuppliers.DefaultCellStyle.Font = new Font("Segoe UI", 10f);
            dgvSuppliers.DefaultCellStyle.BackColor = Color.White;
            dgvSuppliers.DefaultCellStyle.ForeColor = Color.FromArgb(44, 62, 80);
            dgvSuppliers.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
            dgvSuppliers.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvSuppliers.DefaultCellStyle.Padding = new Padding(5, 3, 5, 3);

            dgvSuppliers.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);

            // ToolStrip
            toolStrip.BackColor = Color.FromArgb(236, 240, 241);
            toolStrip.RenderMode = ToolStripRenderMode.Professional;
            toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            toolStrip.Padding = new Padding(0, 5, 0, 5);
            toolStrip.Font = new Font("Segoe UI", 10f);

            // StatusStrip
            statusStrip.BackColor = Color.FromArgb(44, 62, 80);
            statusStrip.ForeColor = Color.White;
            lblCount.ForeColor = Color.White;
            lblCount.Font = new Font("Segoe UI", 10f);
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

        private async Task LoadDeliveriesAsync()
        {
            if (dgvSuppliers.CurrentRow?.DataBoundItem is not DataRowView rv)
            {
                dgvDeliveries.DataSource = null;
                lblDeliveries.Text = "Wybierz dostawcę aby zobaczyć historię dostaw";
                return;
            }

            string dostawcaId = SafeGet<string>(rv.Row, "ID");
            string dostawcaNazwa = SafeGet<string>(rv.Row, "Name");

            if (string.IsNullOrWhiteSpace(dostawcaId))
            {
                dgvDeliveries.DataSource = null;
                return;
            }

            lblDeliveries.Text = $"Historia dostaw - {dostawcaNazwa}:";

            string query = @"
                SELECT TOP 100
                    PD.PartNo as [Nr Partii],
                    PD.CreateData as [Data],
                    PD.Quantity as [Ilość],
                    PD.Weight as [Waga kg],
                    PD.Price as [Cena],
                    PD.Value as [Wartość]
                FROM [LibraNet].[dbo].[PartiaDostawca] PD
                WHERE PD.CustomerID = @DostawcaID
                ORDER BY PD.CreateData DESC";

            try
            {
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
                    dgvDeliveries.Columns["Data"].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
                    dgvDeliveries.Columns["Ilość"].DefaultCellStyle.Format = "N0";
                    dgvDeliveries.Columns["Waga kg"].DefaultCellStyle.Format = "N2";
                    dgvDeliveries.Columns["Cena"].DefaultCellStyle.Format = "N2";
                    dgvDeliveries.Columns["Wartość"].DefaultCellStyle.Format = "N2";
                    dgvDeliveries.AutoResizeColumns();
                }
            }
            catch
            {
                dgvDeliveries.DataSource = null;
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
       WHERE OD.DostawcaID = D.ID AND OD.Status = 'Aktywna') AS OstatniaOcena
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
            lblCount.Text = $"Rekordów: {newSuppliersTable.Rows.Count}";

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

            // Kolorowanie na podstawie typu ceny
            switch (typ.Trim().ToLowerInvariant())
            {
                case "rolnicza": e.CellStyle.BackColor = Color.FromArgb(200, 255, 200); break;
                case "ministerialna": e.CellStyle.BackColor = Color.FromArgb(200, 220, 255); break;
                case "wolnorynkowa": e.CellStyle.BackColor = Color.FromArgb(255, 255, 200); break;
                case "łączona": case "laczona": e.CellStyle.BackColor = Color.FromArgb(255, 200, 220); break;
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

            var colId = Txt("ID", "ID", "ID", 40, false);
            var colName = Txt("Name", "📋 Nazwa", "Name", 150, true);
            var colShort = Txt("ShortName", "Skrót", "ShortName", 80, true);
            var colAddrBlock = Txt("AddrBlock", "📍 Adres", "AddrBlock", 140, true);
            var colPhoneBlk = Txt("PhoneBlock", "📞 Telefon", "PhoneBlock", 100, true);

            var colLast = new DataGridViewTextBoxColumn
            {
                Name = "OstatnieZdanie",
                HeaderText = "📦 Ostatnia dostawa",
                DataPropertyName = "OstatnieZdanie",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "dd.MM.yyyy" }
            };

            // Kolumna oceny z kolorem
            var colOcena = new DataGridViewTextBoxColumn
            {
                Name = "OstatniePunkty",
                HeaderText = "⭐ OCENA",
                DataPropertyName = "OstatniePunkty",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 11f, FontStyle.Bold)
                }
            };

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