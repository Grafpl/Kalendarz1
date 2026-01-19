#nullable enable

using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class AdminChangeRequestsForm : Form
    {
        private readonly string _connString;
        private readonly string _appUser;

        public AdminChangeRequestsForm(string connString, string appUser)
        {
            _connString = connString;
            _appUser = string.IsNullOrWhiteSpace(appUser) ? Environment.UserName : appUser;

            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            ApplyModernStyling(); // Apply new visual style
            HookEvents();
            ZaladujNaglowki();
            WyswietlDaneZaznaczenia();
        }

        private void ApplyModernStyling()
        {
            // General Form Style
            this.Font = new Font("Segoe UI", 9f);
            this.BackColor = Color.FromArgb(245, 247, 249);

            // Style DataGridViews
            StyleDataGridView(dgvNaglowki);
            StyleDataGridView(dgvPozycje);
            dgvPozycje.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // Style Panels and GroupBoxes
            panelAkcji.Text = "Akcje dla zaznaczonego wniosku";
            panelAkcji.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            panelAkcji.ForeColor = Color.FromArgb(45, 57, 69);

            pasekFiltrow.BackColor = Color.White;
            pasekFiltrow.Padding = new Padding(10);

            // Style Buttons
            StyleButton(btnOdswiez, Color.FromArgb(0, 123, 255), "Odśwież");
            StyleButton(btnAkceptuj, Color.FromArgb(40, 167, 69), "✔ Akceptuj");
            StyleButton(btnOdrzuc, Color.FromArgb(220, 53, 69), "✖ Odrzuć");
            StyleButton(btnZapiszNotatke, Color.FromArgb(23, 162, 184), "Zapisz notatkę");
            StyleButton(btnZamknij, Color.FromArgb(108, 117, 125));

            // Style TextBoxes and ComboBoxes
            tbSzukaj.BorderStyle = BorderStyle.FixedSingle;
            tbSzukaj.Font = new Font("Segoe UI", 10f);
            txtNotatka.BorderStyle = BorderStyle.FixedSingle;
            cbStatus.FlatStyle = FlatStyle.Flat;

            lblWierszy.Font = new Font("Segoe UI", 9f, FontStyle.Italic);
            lblWierszy.ForeColor = Color.Gray;
        }

        private void StyleDataGridView(DataGridView dgv)
        {
            dgv.BorderStyle = BorderStyle.None;
            dgv.BackgroundColor = Color.White;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.RowHeadersVisible = false;
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9.5f);
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(228, 241, 254);
            dgv.DefaultCellStyle.SelectionForeColor = Color.Black;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(232, 234, 237);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(52, 58, 64);
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dgv.EnableHeadersVisualStyles = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        }

        private void StyleButton(Button btn, Color color, string? text = null)
        {
            if (text != null) btn.Text = text;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = color;
            btn.ForeColor = Color.White;
            btn.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            btn.Size = new Size(text?.Contains("notatkę") ?? false ? 150 : 120, 38);
            btn.Cursor = Cursors.Hand;
        }

        private void HookEvents()
        {
            btnOdswiez.Click += (_, __) => ZaladujNaglowki();
            cbStatus.SelectedIndexChanged += (_, __) => ZaladujNaglowki();
            tbSzukaj.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) ZaladujNaglowki(); };
            dgvNaglowki.SelectionChanged += (_, __) => WyswietlDaneZaznaczenia();
            btnZamknij.Click += (_, __) => Close();
            btnZapiszNotatke.Click += ZapiszNotatke_Click;
            btnAkceptuj.Click += (_, __) => AkceptujZaznaczone();
            btnOdrzuc.Click += (_, __) => OdrzucZaznaczone();
        }

        // --- UNCHANGED CORE LOGIC ---
        private void ZaladujNaglowki()
        {
            var zaznaczoneId = WybraneCrid();
            using var conn = new SqlConnection(_connString);
            conn.Open();
            var sql = new StringBuilder(@"
                SELECT cr.CRID AS [ID Wniosku], cr.DostawcaID AS [ID Dostawcy], d.Name AS [Dostawca], cr.Reason AS [Uzasadnienie zgłoszenia], 
                ur.Name AS [Utworzył], cr.RequestedAtUTC AS [Data utworzenia (UTC)], cr.EffectiveFrom AS [Obowiązuje od], cr.Status AS [Status], 
                cr.DecyzjaTyp AS [Decyzja], ud.Name AS [Zdecydował], cr.DecyzjaKiedyUTC AS [Data decyzji (UTC)], cr.UzasadnienieDecyzji AS [Notatka administratora]
                FROM dbo.DostawcyCR cr
                LEFT JOIN dbo.Dostawcy d ON d.ID = cr.DostawcaID
                LEFT JOIN dbo.operators ur ON TRY_CAST(cr.RequestedBy AS INT) = ur.ID
                LEFT JOIN dbo.operators ud ON TRY_CAST(cr.DecyzjaKto AS INT) = ud.ID
                WHERE 1=1 ");
            string? st = cbStatus.SelectedItem?.ToString() ?? "Wszystkie";
            if (st != "Wszystkie") sql.AppendLine(" AND cr.Status = @st");
            if (!string.IsNullOrWhiteSpace(tbSzukaj.Text)) sql.AppendLine(" AND (cr.DostawcaID LIKE @q OR d.Name LIKE @q)");
            sql.AppendLine(" ORDER BY cr.RequestedAtUTC DESC;");
            using var da = new SqlDataAdapter(sql.ToString(), conn);
            if (st != "Wszystkie") da.SelectCommand.Parameters.Add("@st", SqlDbType.NVarChar, 16).Value = st;
            if (!string.IsNullOrWhiteSpace(tbSzukaj.Text)) da.SelectCommand.Parameters.Add("@q", SqlDbType.NVarChar, 100).Value = "%" + tbSzukaj.Text.Trim() + "%";
            var dt = new DataTable();
            da.Fill(dt);
            dgvNaglowki.DataSource = dt;
            lblWierszy.Text = $"Znaleziono wniosków: {dt.Rows.Count}";
            if (zaznaczoneId.Any())
            {
                dgvNaglowki.ClearSelection();
                var idSet = new HashSet<long>(zaznaczoneId);
                foreach (DataGridViewRow row in dgvNaglowki.Rows)
                {
                    if (row.DataBoundItem is DataRowView rv && idSet.Contains(Convert.ToInt64(rv["ID Wniosku"]))) row.Selected = true;
                }
            }
            WyswietlDaneZaznaczenia();
        }
        private void WyswietlDaneZaznaczenia()
        {
            bool singleRowSelected = dgvNaglowki.SelectedRows.Count == 1;
            panelAkcji.Enabled = singleRowSelected;
            txtNotatka.Enabled = singleRowSelected;
            btnZapiszNotatke.Enabled = singleRowSelected;

            if (singleRowSelected && dgvNaglowki.CurrentRow?.DataBoundItem is DataRowView rv)
            {
                long crid = Convert.ToInt64(rv["ID Wniosku"]);
                txtNotatka.Text = rv["Notatka administratora"]?.ToString() ?? "";
                ZaladujPozycje(crid);
            }
            else
            {
                txtNotatka.Clear();
                dgvPozycje.DataSource = null;
            }
        }
        private void ZaladujPozycje(long crid)
        {
            using var conn = new SqlConnection(_connString);
            using var da = new SqlDataAdapter("SELECT i.Field AS [Pole], i.OldValue AS [Stara wartość], i.ProposedNewValue AS [Nowa wartość] FROM dbo.DostawcyCRItem i WHERE i.CRID = @id ORDER BY i.ItemID;", conn);
            da.SelectCommand.Parameters.Add("@id", SqlDbType.BigInt).Value = crid;
            var dt = new DataTable();
            da.Fill(dt);
            dgvPozycje.DataSource = dt;
        }
        private List<long> WybraneCrid() => dgvNaglowki.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as DataRowView).Where(rv => rv != null).Select(rv => Convert.ToInt64(rv!["ID Wniosku"])).ToList();
        private void ZapiszNotatke_Click(object? sender, EventArgs e)
        {
            if (dgvNaglowki.SelectedRows.Count != 1) return;
            long crid = WybraneCrid().First();
            string notatka = txtNotatka.Text.Trim();
            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();
                using var cmd = new SqlCommand("UPDATE dbo.DostawcyCR SET UzasadnienieDecyzji = @note WHERE CRID = @id", conn);
                cmd.Parameters.AddWithValue("@note", string.IsNullOrEmpty(notatka) ? DBNull.Value : notatka);
                cmd.Parameters.AddWithValue("@id", crid);
                cmd.ExecuteNonQuery();
                ZaladujNaglowki();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania notatki: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void AkceptujZaznaczone()
        {
            var list = WybraneCrid();
            if (list.Count == 0) { MessageBox.Show("Zaznacz co najmniej jeden wniosek.", "Brak zaznaczenia", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            int ok = 0, zastosowane = 0, pominiete = 0, przyszlosc = 0;
            using var conn = new SqlConnection(_connString);
            conn.Open();
            foreach (var crid in list)
            {
                string status = PobierzStatus(crid, conn) ?? "";
                if (!status.Equals("Proposed", StringComparison.OrdinalIgnoreCase)) { pominiete++; continue; }
                using (var cmd = new SqlCommand("UPDATE dbo.DostawcyCR SET Status = N'Zdecydowany', DecyzjaTyp = N'Zaakceptowano', DecyzjaKto = @u, DecyzjaKiedyUTC = SYSUTCDATETIME() WHERE CRID=@id AND Status='Proposed';", conn))
                {
                    cmd.Parameters.AddWithValue("@u", _appUser);
                    cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = crid;
                    ok += cmd.ExecuteNonQuery();
                }
                DateTime? eff = PobierzEffectiveFrom(crid, conn);
                if (eff.HasValue && eff.Value.Date > DateTime.Today) { przyszlosc++; }
                else
                {
                    if (ZastosujWniosek(crid, conn, out string msg)) zastosowane++;
                    else MessageBox.Show($"CRID={crid}: {msg}", "Zastosowanie przy akceptacji", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            MessageBox.Show($"Zaakceptowano: {ok}\nZastosowano od razu: {zastosowane}\nZ datą w przyszłości: {przyszlosc}\nPominięto (zły status): {pominiete}", "Akceptacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ZaladujNaglowki();
        }
        private void OdrzucZaznaczone()
        {
            var list = WybraneCrid();
            if (list.Count == 0) { MessageBox.Show("Zaznacz co najmniej jeden wniosek.", "Brak zaznaczenia", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            int ok = 0, pominiete = 0;
            using var conn = new SqlConnection(_connString);
            conn.Open();
            foreach (var crid in list)
            {
                string status = PobierzStatus(crid, conn) ?? "";
                if (!status.Equals("Proposed", StringComparison.OrdinalIgnoreCase)) { pominiete++; continue; }
                using var cmd = new SqlCommand("UPDATE dbo.DostawcyCR SET Status = N'Zdecydowany', DecyzjaTyp = N'Odrzucono', DecyzjaKto = @u, DecyzjaKiedyUTC = SYSUTCDATETIME() WHERE CRID=@id AND Status='Proposed';", conn);
                cmd.Parameters.AddWithValue("@u", _appUser);
                cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = crid;
                ok += cmd.ExecuteNonQuery();
            }
            MessageBox.Show($"Odrzucono: {ok}\nPominięto: {pominiete}", "Odrzucenie", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ZaladujNaglowki();
        }
        #region Helpers
        private bool ZastosujWniosek(long crid, SqlConnection conn, out string msg) { msg = "Wystąpił nieznany błąd."; using var trx = conn.BeginTransaction(); string? id; string? status; string? decyzjaTyp; DateTime? eff; using (var cmd = new SqlCommand("SELECT DostawcaID, Status, DecyzjaTyp, EffectiveFrom FROM dbo.DostawcyCR WHERE CRID=@id;", conn, trx)) { cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = crid; using var rd = cmd.ExecuteReader(); if (!rd.Read()) { msg = "Wniosek nie istnieje."; trx.Rollback(); return false; } id = rd["DostawcaID"] as string; status = rd["Status"] as string; decyzjaTyp = rd["DecyzjaTyp"] as string; eff = rd["EffectiveFrom"] as DateTime?; } if (id == null || !status!.Equals("Zdecydowany", StringComparison.OrdinalIgnoreCase) || !decyzjaTyp!.Equals("Zaakceptowano", StringComparison.OrdinalIgnoreCase)) { msg = $"Status/Decyzja nie pozwala na zastosowanie (Status={status}, Decyzja={decyzjaTyp})."; trx.Rollback(); return false; } if (!eff.HasValue || eff.Value.Date > DateTime.Today) { msg = "Data wejścia w życie w przyszłości."; trx.Rollback(); return false; } var items = new DataTable(); using (var da = new SqlDataAdapter("SELECT Field, ProposedNewValue FROM dbo.DostawcyCRItem WHERE CRID=@id ORDER BY ItemID;", conn)) { da.SelectCommand.Transaction = trx; da.SelectCommand.Parameters.Add("@id", SqlDbType.BigInt).Value = crid; da.Fill(items); } if (items.Rows.Count == 0) { msg = "Wniosek nie ma pozycji."; trx.Rollback(); return false; } using (var setCtx = new SqlCommand("EXEC sp_set_session_context @k1,@v1; EXEC sp_set_session_context @k2,@v2;", conn, trx)) { setCtx.Parameters.AddWithValue("@k1", "AppUserID"); setCtx.Parameters.AddWithValue("@v1", _appUser); setCtx.Parameters.AddWithValue("@k2", "ChangeReason"); setCtx.Parameters.AddWithValue("@v2", $"CR {crid}: akceptacja=zastosowanie"); setCtx.ExecuteNonQuery(); } foreach (DataRow r in items.Rows) { string? field = r["Field"].ToString(); if (field == null) continue; string? newVal = r["ProposedNewValue"]?.ToString(); if (!AllowedColumns.TryGetValue(field, out var kind)) { msg = $"Nieobsługiwane pole: {field}"; trx.Rollback(); return false; } string sqlUpd = $"UPDATE dbo.Dostawcy SET [{field}] = @val WHERE ID=@ID;"; using var up = new SqlCommand(sqlUpd, conn, trx); up.Parameters.Add("@ID", SqlDbType.VarChar, 10).Value = id; if (!TryBindValueParameter(up, "@val", kind, newVal, out msg)) { trx.Rollback(); return false; } up.ExecuteNonQuery(); } trx.Commit(); return true; }
        private string? PobierzStatus(long crid, SqlConnection conn) { using var c = new SqlCommand("SELECT Status FROM dbo.DostawcyCR WHERE CRID=@id;", conn); c.Parameters.Add("@id", SqlDbType.BigInt).Value = crid; return c.ExecuteScalar() as string; }
        private DateTime? PobierzEffectiveFrom(long crid, SqlConnection conn) { using var c = new SqlCommand("SELECT EffectiveFrom FROM dbo.DostawcyCR WHERE CRID=@id;", conn); c.Parameters.Add("@id", SqlDbType.BigInt).Value = crid; return c.ExecuteScalar() as DateTime?; }
        private enum ColKind { VarChar, Int, Decimal, BitLike, DateString }
        private static readonly Dictionary<string, ColKind> AllowedColumns = new(StringComparer.OrdinalIgnoreCase) { ["Name"] = ColKind.VarChar, ["Nip"] = ColKind.VarChar, ["Address"] = ColKind.VarChar, ["PostalCode"] = ColKind.VarChar, ["City"] = ColKind.VarChar, ["ProvinceID"] = ColKind.Int, ["PriceTypeID"] = ColKind.Int, ["Addition"] = ColKind.Decimal, ["Loss"] = ColKind.Decimal, ["Halt"] = ColKind.BitLike, ["Regon"] = ColKind.VarChar, ["Pesel"] = ColKind.VarChar, ["AnimNo"] = ColKind.VarChar, ["IRZPlus"] = ColKind.VarChar, ["IDCard"] = ColKind.VarChar, ["IDCardDate"] = ColKind.DateString, ["IDCardAuth"] = ColKind.VarChar, ["FarmAddress"] = ColKind.VarChar, ["FarmPostalCode"] = ColKind.VarChar, ["FarmCity"] = ColKind.VarChar, ["FarmProvinceID"] = ColKind.Int };
        private static bool TryBindValueParameter(SqlCommand cmd, string pname, ColKind kind, string? src, out string error) { error = null!; switch (kind) { case ColKind.VarChar: cmd.Parameters.Add(pname, SqlDbType.VarChar, 4000).Value = (object?)src ?? DBNull.Value; return true; case ColKind.Int: if (string.IsNullOrWhiteSpace(src)) { cmd.Parameters.Add(pname, SqlDbType.Int).Value = DBNull.Value; return true; } if (int.TryParse(src.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int iv)) { cmd.Parameters.Add(pname, SqlDbType.Int).Value = iv; return true; } error = "oczekiwano liczby całkowitej"; return false; case ColKind.Decimal: if (string.IsNullOrWhiteSpace(src)) { cmd.Parameters.Add(pname, SqlDbType.Decimal).Value = DBNull.Value; return true; } var norm = src.Trim().Replace(',', '.'); if (decimal.TryParse(norm, NumberStyles.Any, CultureInfo.InvariantCulture, out var dv)) { var p = cmd.Parameters.Add(pname, SqlDbType.Decimal); p.Precision = 19; p.Scale = 4; p.Value = dv; return true; } error = "oczekiwano liczby (np. 1.2345)"; return false; case ColKind.BitLike: if (string.IsNullOrWhiteSpace(src)) { cmd.Parameters.Add(pname, SqlDbType.Int).Value = DBNull.Value; return true; } var s = src.Trim().ToLowerInvariant(); int bv; if (s is "true" or "1") bv = 1; else if (s is "false" or "0") bv = 0; else if (s == "-1") bv = -1; else if (!int.TryParse(s, out bv)) { error = "oczekiwano 0/1/-1 lub true/false"; return false; } cmd.Parameters.Add(pname, SqlDbType.Int).Value = bv; return true; case ColKind.DateString: if (DateTime.TryParse(src, out var dt)) cmd.Parameters.Add(pname, SqlDbType.VarChar, 10).Value = dt.ToString("yyyy-MM-dd"); else cmd.Parameters.Add(pname, SqlDbType.VarChar, 10).Value = (src ?? "").Trim(); return true; default: error = "nieznany typ"; return false; } }
        #endregion
    }
}