// WidokKontrahenci.cs
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
        // ==== KONFIG ====
        private const string connectionString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private int _pageSize = 200;
        private int _pageIndex = 0;
        private bool _hasMore = false;

        // ==== STAN ====
        private readonly BindingSource _suppliersBS = new();
        private readonly BindingSource _deliveriesBS = new();
        private readonly DataTable _suppliersTable = new();
        private readonly DataTable _deliveriesTable = new();

        private List<KeyValuePair<int, string>> _priceTypeList = new();

        // zewnętrzny edytor (nieużywany tutaj – zastąpiliśmy go HodowcaForm)
        public Action<string> OnEditRequested { get; set; }

        // (opcjonalnie) użytkownik aplikacji
        public string UserID { get; set; }

        public WidokKontrahenci()
        {
            InitializeComponent();

            // wygląd
            this.Font = new Font("Segoe UI", 9.5f);
            dgvSuppliers.EnableDoubleBuffering();
            dgvDeliveries.EnableDoubleBuffering();

            // bindowanie
            _suppliersBS.DataSource = _suppliersTable;
            _deliveriesBS.DataSource = _deliveriesTable;
            dgvSuppliers.DataSource = _suppliersBS;
            dgvDeliveries.DataSource = _deliveriesBS;

            // zdarzenia
            this.Load += async (_, __) =>
            {
                try
                {
                    await LoadPriceTypesAsync();
                    BuildSuppliersColumns();
                    ApplyNiceStyling();
                    await LoadSuppliersPageAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd inicjalizacji: " + ex.Message);
                }
            };

            dgvSuppliers.SelectionChanged += async (_, __) => await LoadSelectedSupplierDetailsAsync();
            dgvSuppliers.CellFormatting += dgvSuppliers_CellFormatting;
            dgvSuppliers.CellEndEdit += dgvSuppliers_CellEndEdit;
            dgvSuppliers.EditingControlShowing += dgvSuppliers_EditingControlShowing;
            dgvSuppliers.DataError += (s, ev) => { ev.ThrowException = false; };

            // filtry/paginacja
            txtSearch.TextChanged += (_, __) => ApplyClientSearchFilter();
            cmbPriceTypeFilter.SelectedIndexChanged += async (_, __) => await ReloadPagePreservingSearchAsync();
            cmbStatusFilter.SelectedIndexChanged += async (_, __) => await ReloadPagePreservingSearchAsync();

            btnRefresh.Click += async (_, __) => await ReloadFirstPageAsync();
            btnPrev.Click += async (_, __) => await PrevPageAsync();
            btnNext.Click += async (_, __) => await NextPageAsync();
            btnDuplicates.Click += (_, __) => SprawdzDuplikaty();
            btnExportCsv.Click += (_, __) => ExportSuppliersToCsv();
            btnAdd.Click += async (_, __) => await AddNewSupplierInlineAsync();

            // „Modyfikuj” → otwórz HodowcaForm (akceptacja wniosku)
            btnEdit.Click += (_, __) => OpenAkceptacjaWniosku();
        }

        // =========================
        //  ŁADOWANIE DANYCH
        // =========================
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

            // filtr ToolStrip
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

            // filtr typu ceny
            var priceTypeFilter = cmbPriceTypeFilter.SelectedItem as KeyValuePair<int?, string>?;
            if (priceTypeFilter.HasValue && priceTypeFilter.Value.Key.HasValue)
                sbWhere.Append(" AND D.PriceTypeID = @PriceTypeID ");

            // filtr statusu (poprawiony)
            var status = cmbStatusFilter.Text;
            if (status == "Aktywni")
                sbWhere.Append(" AND ISNULL(D.Halt,0) = 0 ");
            else if (status == "Wstrzymani")
                sbWhere.Append(" AND ISNULL(D.Halt,0) = 1 ");

            int offset = _pageIndex * _pageSize;

            string query = $@"
SELECT 
    D.[ID],
    D.[ShortName],
    D.[Name],
    D.[Address],
    D.[PostalCode],
    D.[Halt],
    D.[City],
    D.[Distance] AS KM,
    D.[PriceTypeID],
    PT.[Name] AS PriceTypeName,
    D.[Addition] AS Dodatek,
    D.[Loss] AS Ubytek,
    D.[Phone1],
    D.[Phone2],
    D.[Phone3],
    D.[Nip],
    D.[Pesel]
FROM 
    [LibraNet].[dbo].[Dostawcy] D
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
SELECT CASE WHEN Cnt > @Offset + @PageSize THEN 1 ELSE 0 END AS HasMore FROM _x;
";

            using var con = new SqlConnection(connectionString);
            using var cmd = new SqlCommand(query, con);
            if (priceTypeFilter.HasValue && priceTypeFilter.Value.Key.HasValue)
                cmd.Parameters.AddWithValue("@PriceTypeID", priceTypeFilter.Value.Key.Value);
            cmd.Parameters.AddWithValue("@Offset", offset);
            cmd.Parameters.AddWithValue("@PageSize", _pageSize);

            using var da = new SqlDataAdapter(cmd);
            var ds = new DataSet();
            await Task.Run(() => da.Fill(ds));

            _suppliersTable.Clear();
            _suppliersTable.Merge(ds.Tables[0], false, MissingSchemaAction.Add);
            _hasMore = (ds.Tables.Count > 1 && ds.Tables[1].Rows.Count > 0 &&
                        Convert.ToInt32(ds.Tables[1].Rows[0]["HasMore"]) == 1);

            // kolumny połączone (na siatkę)
            EnsureDerivedColumns();
            FillDerivedColumns();

            lblPage.Text = $"Strona: {_pageIndex + 1}";
            lblCount.Text = $"Rekordy: {_suppliersTable.DefaultView.Count}";
            btnPrev.Enabled = _pageIndex > 0;
            btnNext.Enabled = _hasMore;

            ApplyClientSearchFilter();
            dgvSuppliers.Invalidate();
        }

        private async Task ReloadFirstPageAsync()
        {
            _pageIndex = 0;
            await LoadSuppliersPageAsync();
        }
        private async Task PrevPageAsync()
        {
            if (_pageIndex <= 0) return;
            _pageIndex--;
            await LoadSuppliersPageAsync();
        }
        private async Task NextPageAsync()
        {
            if (!_hasMore) return;
            _pageIndex++;
            await LoadSuppliersPageAsync();
        }
        private async Task ReloadPagePreservingSearchAsync()
        {
            string currentSearch = txtSearch.Text;
            await LoadSuppliersPageAsync();
            txtSearch.Text = currentSearch;
        }

        // kolumny wyliczane (stringi z \n)
        private void EnsureDerivedColumns()
        {
            void add(string name)
            {
                if (!_suppliersTable.Columns.Contains(name))
                    _suppliersTable.Columns.Add(name, typeof(string));
            }
            add("AddrBlock");      // "Kod Miasto\nAdres"
            add("PhoneBlock");     // "Tel1\nTel2\nTel3"
            add("TypeAddBlock");   // "Typ ceny\nDodatek: X"
            add("NipPeselBlock");  // "NIP\nPESEL"
        }

        private void FillDerivedColumns()
        {
            foreach (DataRow r in _suppliersTable.Rows)
            {
                string kod = SafeGet<string>(r, "PostalCode");
                string msc = SafeGet<string>(r, "City");
                string adr = SafeGet<string>(r, "Address");
                r["AddrBlock"] = $"{(string.IsNullOrWhiteSpace(kod) ? "" : kod + " ")}{msc}".Trim() +
                                 (string.IsNullOrWhiteSpace(adr) ? "" : "\n" + adr);

                var phones = new[] { SafeGet<string>(r, "Phone1"), SafeGet<string>(r, "Phone2"), SafeGet<string>(r, "Phone3") }
                             .Where(s => !string.IsNullOrWhiteSpace(s));
                r["PhoneBlock"] = string.Join("\n", phones);

                string typ = SafeGet<string>(r, "PriceTypeName");
                var dod = SafeGet<decimal?>(r, "Dodatek");
                string dodTxt = dod.HasValue ? dod.Value.ToString("0.0000") : "-";
                r["TypeAddBlock"] = $"{typ}\nDodatek: {dodTxt}";

                string nip = SafeGet<string>(r, "Nip");
                string pes = SafeGet<string>(r, "Pesel");
                r["NipPeselBlock"] = string.Join("\n", new[] { nip, pes }.Where(s => !string.IsNullOrWhiteSpace(s)));
            }
        }

        private void ApplyClientSearchFilter()
        {
            var text = (txtSearch.Text ?? "").Trim().Replace("'", "''");
            if (_suppliersTable.DefaultView == null) return;

            if (string.IsNullOrEmpty(text))
            {
                _suppliersTable.DefaultView.RowFilter = "";
            }
            else
            {
                _suppliersTable.DefaultView.RowFilter =
                    $"CONVERT(Name,'System.String') LIKE '%{text}%' " +
                    $"OR CONVERT(ShortName,'System.String') LIKE '%{text}%' " +
                    $"OR CONVERT(City,'System.String') LIKE '%{text}%' " +
                    $"OR CONVERT(Nip,'System.String') LIKE '%{text}%' " +
                    $"OR CONVERT(Pesel,'System.String') LIKE '%{text}%' " +
                    $"OR CONVERT(Phone1,'System.String') LIKE '%{text}%' " +
                    $"OR CONVERT(Phone2,'System.String') LIKE '%{text}%' " +
                    $"OR CONVERT(Phone3,'System.String') LIKE '%{text}%'";
            }
            lblCount.Text = $"Rekordy: {_suppliersTable.DefaultView.Count}";
        }

        private async Task LoadSelectedSupplierDetailsAsync()
        {
            if (dgvSuppliers.CurrentRow == null) return;
            if (dgvSuppliers.CurrentRow.DataBoundItem is DataRowView rv)
            {
                var r = rv.Row;
                FillDetailsPanel(r);
                // Dostawy zostawiamy (jeśli masz, działa jak wcześniej)
                // string id = SafeGet<string>(r, "ID");
                // await LoadDeliveriesAsync(id);
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
            txtDetEmail.Text = ""; // brak w SELECT – zostaw puste lub dołącz w SQL, jeśli chcesz
            txtDetNip.Text = SafeGet<string>(r, "Nip");
            txtDetRegon.Text = ""; // jw.
            txtDetPesel.Text = SafeGet<string>(r, "Pesel");
            txtDetTypCeny.Text = SafeGet<string>(r, "PriceTypeName");
            txtDetKm.Text = SafeGet<int?>(r, "KM")?.ToString() ?? "";
            txtDetDodatek.Text = SafeGet<decimal?>(r, "Dodatek")?.ToString("0.0000") ?? "";
            txtDetUbytek.Text = SafeGet<decimal?>(r, "Ubytek")?.ToString("0.0000") ?? "";
            chkDetHalt.Checked = SafeGet<int?>(r, "Halt") == 1;
            txtDetOstatnie.Text = "";
        }

        // =========================
        //  EDYCJA (zostawiamy jak było; Typ ceny już nieedytowalny)
        // =========================
        private async void dgvSuppliers_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (dgvSuppliers.IsCurrentCellInEditMode)
                    dgvSuppliers.CommitEdit(DataGridViewDataErrorContexts.Commit);

                if (_suppliersBS == null || _suppliersBS.Count == 0) return;
                if (e.RowIndex >= _suppliersBS.Count) return;
                if (e.RowIndex >= dgvSuppliers.Rows.Count) return;
                if (dgvSuppliers.Rows[e.RowIndex].IsNewRow) return;

                var col = dgvSuppliers.Columns[e.ColumnIndex];

                var editable = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "ShortName","Name","Ubytek","Halt" // tylko to sensownie edytować po odchudzeniu
                };
                if (!editable.Contains(col.Name)) return;

                if (!(_suppliersBS[e.RowIndex] is DataRowView rv)) return;
                var dataRow = rv.Row;

                string id = SafeGet<string>(dataRow, "ID");
                if (string.IsNullOrWhiteSpace(id)) return;

                object uiVal = dgvSuppliers.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
                object newVal = ConvertValueForDb(col.Name, uiVal);

                string dbColumn = col.Name switch
                {
                    _ => col.Name
                };

                await UpdateDatabaseValueAsync(id, dbColumn, newVal);

                // przelicz łączone kolumny po zmianach
                FillDerivedColumns();
                dgvSuppliers.InvalidateRow(e.RowIndex);
            }
            catch { /* ignoruj wyścigi / nic krytycznego */ }
        }

        private static object ConvertValueForDb(string columnName, object uiValue)
        {
            if (uiValue == null || uiValue == DBNull.Value) return DBNull.Value;

            switch (columnName)
            {
                case "Ubytek":
                    if (decimal.TryParse(uiValue.ToString(), out var d)) return d;
                    return DBNull.Value;
                case "Halt":
                    if (uiValue is bool b) return b ? 1 : 0;
                    if (int.TryParse(uiValue.ToString(), out var i)) return i != 0 ? 1 : 0;
                    return 0;
                default:
                    return uiValue; // tekst
            }
        }

        private async Task UpdateDatabaseValueAsync(string id, string columnName, object newValue)
        {
            string query = $"UPDATE [LibraNet].[dbo].[Dostawcy] SET [{columnName}] = @NewValue WHERE [ID] = @ID";

            using var con = new SqlConnection(connectionString);
            using var cmd = new SqlCommand(query, con);

            cmd.Parameters.Add("@ID", SqlDbType.VarChar, 50).Value =
                string.IsNullOrWhiteSpace(id) ? (object)DBNull.Value : id;

            switch (columnName)
            {
                case "Ubytek":
                    cmd.Parameters.Add("@NewValue", SqlDbType.Decimal).Value =
                        newValue is DBNull ? (object)DBNull.Value : Convert.ToDecimal(newValue);
                    break;
                case "Halt":
                    cmd.Parameters.Add("@NewValue", SqlDbType.Int).Value =
                        newValue is DBNull ? 0 : Convert.ToInt32(newValue);
                    break;
                default:
                    cmd.Parameters.Add("@NewValue", SqlDbType.VarChar, 256).Value =
                        newValue ?? (object)DBNull.Value;
                    break;
            }

            await con.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        private void dgvSuppliers_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            // nic – nie mamy już comboboxa z typem ceny
        }

        // =========================
        //  FORMATOWANIE WIERSZY
        // =========================
        private void dgvSuppliers_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvSuppliers.Rows[e.RowIndex];
            if (row.DataBoundItem is not DataRowView rv) return;

            string typ = rv.Row["PriceTypeName"] as string;
            int? halt = rv.Row["Halt"] as int?;

            // Kolor tła wg typu ceny
            if (!string.IsNullOrWhiteSpace(typ))
            {
                var style = row.DefaultCellStyle;
                style.ForeColor = Color.Black;
                style.Font = dgvSuppliers.Font;

                switch (typ.Trim().ToLowerInvariant())
                {
                    case "rolnicza": style.BackColor = Color.LightGreen; break;
                    case "ministerialna": style.BackColor = Color.LightBlue; break;
                    case "wolnorynkowa": style.BackColor = Color.LightYellow; break;
                    case "łączona":
                    case "laczona": style.BackColor = Color.PaleVioletRed; break;
                    default: style.BackColor = Color.White; break;
                }
            }

            // Wstrzymani -> 1
            if (halt == 1)
            {
                var style = row.DefaultCellStyle;
                style.BackColor = Color.Gainsboro;
                style.ForeColor = Color.Firebrick;
                style.Font = new Font(dgvSuppliers.Font, FontStyle.Strikeout);
            }
        }

        // =========================
        //  DODATKI
        // =========================
        private void SprawdzDuplikaty()
        {
            var set = new HashSet<string>();
            var dup = new HashSet<string>();

            foreach (DataGridViewRow row in dgvSuppliers.Rows)
            {
                var name = row.Cells["Name"].Value?.ToString()?.Trim().ToLower();
                if (string.IsNullOrEmpty(name)) continue;
                if (!set.Add(name)) dup.Add(name);
            }

            if (dup.Count == 0)
            {
                MessageBox.Show("Nie znaleziono duplikatów.", "Duplikaty",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (DataGridViewRow row in dgvSuppliers.Rows)
            {
                var name = row.Cells["Name"].Value?.ToString()?.Trim().ToLower();
                if (name != null && dup.Contains(name))
                    row.DefaultCellStyle.BackColor = Color.MistyRose;
            }

            MessageBox.Show("Znaleziono duplikaty:\n\n" + string.Join("\n", dup.OrderBy(s => s)),
                "Duplikaty", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ExportSuppliersToCsv()
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"Hodowcy_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            var dt = (_suppliersBS.List as DataView)?.ToTable() ?? _suppliersTable;
            var cols = dgvSuppliers.Columns.Cast<DataGridViewColumn>()
                         .Where(c => c.Visible)
                         .Select(c => string.IsNullOrEmpty(c.DataPropertyName) ? c.Name : c.DataPropertyName)
                         .Distinct()
                         .ToList();

            using var sw = new StreamWriter(sfd.FileName, false, Encoding.UTF8);
            sw.WriteLine(string.Join(";", cols));
            foreach (DataRow r in dt.Rows)
            {
                var vals = cols.Select(c =>
                {
                    var v = r.Table.Columns.Contains(c) ? r[c] : "";
                    var s = v == null || v == DBNull.Value ? "" : v.ToString();
                    return s?.Replace(";", ",");
                });
                sw.WriteLine(string.Join(";", vals));
            }

            MessageBox.Show("Wyeksportowano do CSV.", "Eksport",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async Task AddNewSupplierInlineAsync()
        {
            const string query = @"
INSERT INTO [LibraNet].[dbo].[Dostawcy] ([Name], [ShortName], [City])
VALUES (@Name, @ShortName, @City);
SELECT CAST(SCOPE_IDENTITY() AS int);";

            using var con = new SqlConnection(connectionString);
            using var cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Name", "Nowy hodowca");
            cmd.Parameters.AddWithValue("@ShortName", DBNull.Value);
            cmd.Parameters.AddWithValue("@City", DBNull.Value);
            await con.OpenAsync();
            _ = await cmd.ExecuteScalarAsync();

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

        // =========================
        //  POMOCNICZE i UI
        // =========================
        private static T SafeGet<T>(DataRow r, string col)
        {
            try
            {
                if (!r.Table.Columns.Contains(col)) return default!;
                var v = r[col];
                if (v == DBNull.Value || v == null) return default!;
                return (T)Convert.ChangeType(v, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
            }
            catch { return default!; }
        }

        private void ApplyNiceStyling()
        {
            dgvSuppliers.AllowUserToAddRows = false;
            dgvSuppliers.AllowUserToDeleteRows = false;
            dgvSuppliers.AllowUserToOrderColumns = true;
            dgvSuppliers.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvSuppliers.MultiSelect = false;
            dgvSuppliers.RowHeadersVisible = false;
            dgvSuppliers.AutoGenerateColumns = false;

            // zawijanie i wyższe wiersze
            dgvSuppliers.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvSuppliers.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            dgvSuppliers.RowTemplate.Height = 44;

            // kolumny będą "Fill" z wagami
            dgvSuppliers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            dgvSuppliers.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f);
            dgvSuppliers.ColumnHeadersDefaultCellStyle.BackColor = Color.WhiteSmoke;
            dgvSuppliers.EnableHeadersVisualStyles = false;
            dgvSuppliers.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);

            dgvDeliveries.AllowUserToAddRows = false;
            dgvDeliveries.RowHeadersVisible = false;
            dgvDeliveries.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            dgvDeliveries.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
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
                    FillWeight = fillWeight,
                };
                if (wrap) c.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                return c;
            }

            // Minimalny, czytelny zestaw
            var colId = Txt("ID", "ID", "ID", 50, false);
            var colName = Txt("Name", "Nazwa", "Name", 120, true);
            var colShort = Txt("ShortName", "Skrót", "ShortName", 80, true);
            var colAddrBlock = Txt("AddrBlock", "Adres", "AddrBlock", 140, true);         // "Kod Miasto\nAdres"
            var colPhoneBlk = Txt("PhoneBlock", "Telefon", "PhoneBlock", 90, true);       // "Tel1\nTel2\nTel3"
            var colTypeAdd = Txt("TypeAddBlock", "Typ + Dodatek", "TypeAddBlock", 90, true); // "Typ\nDodatek: x"
            var colLoss = Txt("Ubytek", "Ubytek", "Ubytek", 60, false);
            var colNipPesel = Txt("NipPeselBlock", "NIP / PESEL", "NipPeselBlock", 90, true);

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
                colId, colName, colShort, colAddrBlock, colPhoneBlk, colTypeAdd, colLoss, colNipPesel, colHalt
            });
        }
    }

    // anti-flicker
    internal static class DataGridViewExtensions
    {
        public static void EnableDoubleBuffering(this DataGridView dgv)
        {
            typeof(DataGridView)
                .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(dgv, true, null);
        }
    }
}
