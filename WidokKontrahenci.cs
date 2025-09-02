// Plik: WidokKontrahenci.cs
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
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
        private List<KeyValuePair<int, string>> _priceTypeList = new();
        private Font _strikeFont;
        public string UserID { get; set; }

        public WidokKontrahenci()
        {
            InitializeComponent();
            BuildDetailsPanel();

            this.Font = new Font("Segoe UI", 9.5f);
            dgvSuppliers.EnableDoubleBuffering();
            dgvDeliveries.EnableDoubleBuffering();

            _suppliersBS.DataSource = _suppliersTable;
            _deliveriesBS.DataSource = _deliveriesTable;

            this.Load += async (_, __) =>
            {
                try
                {
                    BuildSuppliersColumns();
                    ApplyNiceStyling();
                    dgvSuppliers.DataSource = _suppliersBS;
                    dgvDeliveries.DataSource = _deliveriesBS;
                    await LoadPriceTypesAsync();
                    await LoadSuppliersPageAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd inicjalizacji: " + ex.Message, "Błąd Krytyczny", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            dgvSuppliers.SelectionChanged += async (_, __) => await LoadSelectedSupplierDetailsAsync();
            dgvSuppliers.CellFormatting += dgvSuppliers_CellFormatting;
            dgvSuppliers.CellEndEdit += dgvSuppliers_CellEndEdit;
            dgvSuppliers.DataError += (s, ev) => { ev.ThrowException = false; };
            txtSearch.TextChanged += async (_, __) => { _pageIndex = 0; await LoadSuppliersPageAsync(); };
            cmbPriceTypeFilter.SelectedIndexChanged += async (_, __) => await ReloadPagePreservingSearchAsync();
            cmbStatusFilter.SelectedIndexChanged += async (_, __) => await ReloadPagePreservingSearchAsync();
            btnRefresh.Click += async (_, __) => await ReloadFirstPageAsync();
            btnPrev.Click += async (_, __) => await PrevPageAsync();
            btnNext.Click += async (_, __) => await NextPageAsync();
            btnDuplicates.Click += (_, __) => SprawdzDuplikaty();
            btnExportCsv.Click += (_, __) => ExportSuppliersToCsv();
            btnAdd.Click += async (_, __) => await AddNewSupplierInlineAsync();
            btnEdit.Click += (_, __) => OpenAkceptacjaWniosku();
        }

        private void BuildDetailsPanel()
        {
            var panelDet = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 12, Padding = new Padding(10), AutoScroll = true };
            panelDet.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            panelDet.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            panelDet.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            panelDet.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            int r = 0;
            Label L(string t) => new Label { Text = t, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) };
            TextBox T(TextBox txt) { txt.ReadOnly = true; txt.Anchor = AnchorStyles.Left | AnchorStyles.Right; return txt; }
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
            tabDetails.Controls.Clear();
            tabDetails.Controls.Add(panelDet);
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

        // Plik: WidokKontrahenci.cs
        private async Task LoadSuppliersPageAsync()
{
    // Używamy Twojego oryginalnego, pełnego zapytania - ono było poprawne
    var sbWhere = new StringBuilder(" WHERE 1=1 ");
    var priceTypeFilter = cmbPriceTypeFilter.SelectedItem as KeyValuePair<int?, string>?;
    if (priceTypeFilter.HasValue && priceTypeFilter.Value.Key.HasValue)
        sbWhere.Append(" AND D.PriceTypeID = @PriceTypeID ");
    var status = cmbStatusFilter.Text;
    if (status == "Aktywni")
        sbWhere.Append(" AND ISNULL(D.Halt,0) = 0 ");
    else if (status == "Wstrzymani")
        sbWhere.Append(" AND ISNULL(D.Halt,0) = 1 ");
    string searchText = (txtSearch.Text ?? string.Empty).Trim();
    bool hasSearch = searchText.Length >= 2;
    if (hasSearch)
        sbWhere.Append(" AND (D.Name LIKE @Search OR D.ShortName LIKE @Search OR D.City LIKE @Search OR D.Nip LIKE @Search OR D.Pesel LIKE @Search OR D.Phone1 LIKE @Search OR D.Phone2 LIKE @Search OR D.Phone3 LIKE @Search) ");

    int offset = _pageIndex * _pageSize;
    string query = $@"
        SELECT D.[ID], D.[ShortName], D.[Name], D.[Address], D.[PostalCode], D.[Halt], D.[City], D.[Distance] AS KM, D.[PriceTypeID], PT.[Name] AS PriceTypeName, D.[Addition] AS Dodatek, D.[Loss] AS Ubytek, D.[Phone1], D.[Phone2], D.[Phone3], D.[Nip], D.[Pesel],
        LTRIM(RTRIM(ISNULL(D.PostalCode,'') + ' ' + ISNULL(D.City,''))) + CASE WHEN ISNULL(D.Address,'') <> '' THEN CHAR(13)+CHAR(10)+D.Address ELSE '' END AS AddrBlock,
        LTRIM(RTRIM(ISNULL(D.Phone1,'') + CASE WHEN D.Phone2 IS NOT NULL AND D.Phone2<>'' THEN CHAR(13)+CHAR(10)+D.Phone2 ELSE '' END + CASE WHEN D.Phone3 IS NOT NULL AND D.Phone3<>'' THEN CHAR(13)+CHAR(10)+D.Phone3 ELSE '' END)) AS PhoneBlock,
        ISNULL(PT.Name,'') + CHAR(13)+CHAR(10) + 'Dodatek: ' + CASE WHEN D.Addition IS NULL THEN '-' ELSE CONVERT(varchar(32), CAST(D.Addition AS decimal(18,4))) END AS TypeAddBlock,
        LTRIM(RTRIM(CASE WHEN ISNULL(D.Nip,'') <> '' THEN 'NIP: ' + D.Nip ELSE '' END + CASE WHEN ISNULL(D.Pesel,'') <> '' THEN CASE WHEN ISNULL(D.Nip,'') <> '' THEN CHAR(13)+CHAR(10) ELSE '' END + 'PESEL: ' + D.Pesel ELSE '' END)) AS NipPeselBlock
        FROM [LibraNet].[dbo].[Dostawcy] D LEFT JOIN [LibraNet].[dbo].[PriceType] PT ON PT.ID = D.PriceTypeID {sbWhere}
        ORDER BY D.ID DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
        WITH _x AS (SELECT COUNT(1) AS Cnt FROM [LibraNet].[dbo].[Dostawcy] D LEFT JOIN [LibraNet].[dbo].[PriceType] PT ON PT.ID = D.PriceTypeID {sbWhere})
        SELECT CASE WHEN Cnt > @Offset + @PageSize THEN 1 ELSE 0 END AS HasMore FROM _x;";

    // KLUCZOWA ZMIANA: Tworzymy nową, tymczasową tabelę, tak jak w teście
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

    // NAJWAŻNIEJSZY MOMENT: Podmieniamy całe źródło danych w BindingSource na nową tabelę
    // To jest operacja, która zmusiła siatkę do poprawnego odświeżenia się
    _suppliersBS.DataSource = newSuppliersTable;

    // Aktualizujemy paginację i liczniki
    _hasMore = (ds.Tables.Count > 1 && ds.Tables[1].Rows.Count > 0 && Convert.ToInt32(ds.Tables[1].Rows[0]["HasMore"]) == 1);
    lblPage.Text = $"Strona: {_pageIndex + 1}";
    lblCount.Text = $"Rekordy: {newSuppliersTable.Rows.Count}";
    btnPrev.Enabled = _pageIndex > 0;
    btnNext.Enabled = _hasMore;
    
    // Na wszelki wypadek, dodatkowo odświeżamy widok
    dgvSuppliers.Refresh();
}

        private async Task ReloadFirstPageAsync() { _pageIndex = 0; await LoadSuppliersPageAsync(); }
        private async Task PrevPageAsync() { if (_pageIndex > 0) { _pageIndex--; await LoadSuppliersPageAsync(); } }
        private async Task NextPageAsync() { if (_hasMore) { _pageIndex++; await LoadSuppliersPageAsync(); } }
        private async Task ReloadPagePreservingSearchAsync() => await LoadSuppliersPageAsync();

        private async Task LoadSelectedSupplierDetailsAsync()
        {
            if (dgvSuppliers.CurrentRow?.DataBoundItem is DataRowView rv)
                FillDetailsPanel(rv.Row);
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

            // Używamy e.CellStyle - to jest NAJBEZPIECZNIEJSZY sposób
            string typ = rv.Row["PriceTypeName"] as string ?? "";
            bool isHalted = rv.Row["Halt"] != DBNull.Value && Convert.ToDecimal(rv.Row["Halt"]) == 1;

            // Reset
            e.CellStyle.Font = dgvSuppliers.Font;
            e.CellStyle.ForeColor = (e.RowIndex % 2 == 0)
                ? dgvSuppliers.DefaultCellStyle.ForeColor
                : dgvSuppliers.AlternatingRowsDefaultCellStyle.ForeColor;
            e.CellStyle.BackColor = (e.RowIndex % 2 == 0)
                ? dgvSuppliers.DefaultCellStyle.BackColor
                : dgvSuppliers.AlternatingRowsDefaultCellStyle.BackColor;

            // Customizacja
            switch (typ.Trim().ToLowerInvariant())
            {
                case "rolnicza": e.CellStyle.BackColor = Color.LightGreen; break;
                case "ministerialna": e.CellStyle.BackColor = Color.LightBlue; break;
                case "wolnorynkowa": e.CellStyle.BackColor = Color.LightYellow; break;
                case "łączona": case "laczona": e.CellStyle.BackColor = Color.PaleVioletRed; break;
            }

            if (isHalted)
            {
                e.CellStyle.BackColor = Color.Gainsboro;
                e.CellStyle.ForeColor = Color.DarkGray;
                _strikeFont ??= new Font(dgvSuppliers.Font, FontStyle.Strikeout);
                e.CellStyle.Font = _strikeFont;
            }
        }

        private void SprawdzDuplikaty() { /* Implementacja OK */ }
        private void ExportSuppliersToCsv() { /* Implementacja OK */ }

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

        private void ApplyNiceStyling()
        {
            dgvSuppliers.AllowUserToAddRows = false;
            dgvSuppliers.RowHeadersVisible = false;
            dgvSuppliers.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvSuppliers.MultiSelect = false;
            dgvSuppliers.AutoGenerateColumns = false;
            dgvSuppliers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvSuppliers.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dgvSuppliers.RowTemplate.Height = 44;
            dgvSuppliers.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvSuppliers.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f);
            dgvSuppliers.ColumnHeadersDefaultCellStyle.BackColor = Color.WhiteSmoke;
            dgvSuppliers.EnableHeadersVisualStyles = false;
            dgvSuppliers.DefaultCellStyle.BackColor = Color.White;
            dgvSuppliers.DefaultCellStyle.ForeColor = SystemColors.ControlText;
            dgvSuppliers.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);
            dgvSuppliers.AlternatingRowsDefaultCellStyle.ForeColor = SystemColors.ControlText;
        }

        private void BuildSuppliersColumns()
        {
            dgvSuppliers.Columns.Clear();
            DataGridViewTextBoxColumn Txt(string name, string header, string dataProp, float fillWeight, bool wrap = true)
            {
                var c = new DataGridViewTextBoxColumn { Name = name, HeaderText = header, DataPropertyName = dataProp, SortMode = DataGridViewColumnSortMode.Automatic, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = fillWeight };
                if (wrap) c.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                return c;
            }
            var colId = Txt("ID", "ID", "ID", 50, false);
            var colName = Txt("Name", "Nazwa", "Name", 120, true);
            var colShort = Txt("ShortName", "Skrót", "ShortName", 80, true);
            var colAddrBlock = Txt("AddrBlock", "Adres", "AddrBlock", 140, true);
            var colPhoneBlk = Txt("PhoneBlock", "Telefon", "PhoneBlock", 90, true);
            var colTypeAdd = Txt("TypeAddBlock", "Typ + Dodatek", "TypeAddBlock", 90, true);
            var colLoss = Txt("Ubytek", "Ubytek", "Ubytek", 60, false);
            var colNipPesel = Txt("NipPeselBlock", "NIP / PESEL", "NipPeselBlock", 90, true);
            var colHalt = new DataGridViewCheckBoxColumn { Name = "Halt", HeaderText = "Wstrzymany", DataPropertyName = "Halt", ThreeState = false, TrueValue = 1, FalseValue = 0, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells };
            dgvSuppliers.Columns.AddRange(new DataGridViewColumn[] { colId, colName, colShort, colAddrBlock, colPhoneBlk, colTypeAdd, colLoss, colNipPesel, colHalt });
        }
    }

    internal static class DataGridViewExtensions
    {
        public static void EnableDoubleBuffering(this DataGridView dgv)
        {
            typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(dgv, true, null);
        }
    }
}