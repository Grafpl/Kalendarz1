using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    public partial class AdminChangeRequestsForm : Form
    {
        private readonly string _connString;
        private readonly string _appUser; // ID operatora albo login domenowy (tekst)

        public AdminChangeRequestsForm(string connString, string appUser)
        {
            _connString = connString;
            _appUser = string.IsNullOrWhiteSpace(appUser) ? Environment.UserName : appUser;

            InitializeComponent();     // z Designer.cs
            HookEvents();              // zdarzenia przycisków/filtrów
            ZaladujNaglowki();         // dane startowe
        }

        private void HookEvents()
        {
            btnOdswiez.Click += (_, __) => ZaladujNaglowki();
            cbStatus.SelectedIndexChanged += (_, __) => ZaladujNaglowki();
            tbSzukaj.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) ZaladujNaglowki(); };
            dgvNaglowki.SelectionChanged += (_, __) => ZaladujPozycjeDlaZaznaczonego();
            btnZamknij.Click += (_, __) => Close();
            btnAkceptuj.Click += (_, __) => AkceptujZaznaczone();
            btnOdrzuc.Click += (_, __) => OdrzucZaznaczone();
        }

        // ===== LOAD =====
        private void ZaladujNaglowki()
        {
            using var conn = new SqlConnection(_connString);
            conn.Open();

            var sql = new StringBuilder(@"
SELECT
  cr.CRID                                   AS [ID Wniosku],
  cr.DostawcaID                             AS [ID Dostawcy],
  d.Name                                    AS [Dostawca],
  cr.Reason                                 AS [Uzasadnienie zgłoszenia],
  ur.Name                                   AS [Utworzył],
  cr.RequestedAtUTC                         AS [Data utworzenia (UTC)],
  cr.EffectiveFrom                          AS [Obowiązuje od],
  cr.Status                                 AS [Status],
  cr.DecyzjaTyp                             AS [Decyzja],
  ud.Name                                   AS [Zdecydował],
  cr.DecyzjaKiedyUTC                        AS [Data decyzji (UTC)],
  cr.UzasadnienieDecyzji                    AS [Uzasadnienie decyzji]
FROM dbo.DostawcyCR cr
LEFT JOIN dbo.Dostawcy d ON d.ID = cr.DostawcaID

-- mapowanie tekstowego ID użytkownika (jeśli to liczba) -> operators.ID
LEFT JOIN dbo.operators ur
  ON cr.RequestedBy IS NOT NULL
 AND cr.RequestedBy NOT LIKE '%[^0-9]%'
 AND CAST(cr.RequestedBy AS int) = ur.ID

LEFT JOIN dbo.operators ud
  ON cr.DecyzjaKto IS NOT NULL
 AND cr.DecyzjaKto NOT LIKE '%[^0-9]%'
 AND CAST(cr.DecyzjaKto AS int) = ud.ID

WHERE 1=1
");

            string st = cbStatus.SelectedItem?.ToString() ?? "Wszystkie";
            if (st != "Wszystkie") sql.AppendLine("  AND cr.Status = @st");
            if (!string.IsNullOrWhiteSpace(tbSzukaj.Text))
                sql.AppendLine("  AND (cr.DostawcaID LIKE @q OR d.Name LIKE @q)");
            sql.AppendLine("ORDER BY cr.RequestedAtUTC DESC;");

            using var da = new SqlDataAdapter(sql.ToString(), conn);
            if (st != "Wszystkie") da.SelectCommand.Parameters.Add("@st", SqlDbType.NVarChar, 16).Value = st;
            if (!string.IsNullOrWhiteSpace(tbSzukaj.Text))
                da.SelectCommand.Parameters.Add("@q", SqlDbType.NVarChar, 100).Value = "%" + tbSzukaj.Text.Trim() + "%";

            var dt = new DataTable();
            da.Fill(dt);
            dgvNaglowki.DataSource = dt;
            lblWierszy.Text = $"Wniosków: {dt.Rows.Count}";

            ZaladujPozycjeDlaZaznaczonego();
        }

        private void ZaladujPozycjeDlaZaznaczonego()
        {
            if (dgvNaglowki.CurrentRow == null || dgvNaglowki.CurrentRow.DataBoundItem is not DataRowView rv)
            { dgvPozycje.DataSource = null; return; }

            long crid = Convert.ToInt64(rv["ID Wniosku"]);

            using var conn = new SqlConnection(_connString);
            conn.Open();

            using var da = new SqlDataAdapter(@"
SELECT 
  i.ItemID           AS [Lp],
  i.CRID             AS [ID Wniosku],
  i.Field            AS [Pole],
  i.OldValue         AS [Stara wartość],
  i.ProposedNewValue AS [Nowa wartość]
FROM dbo.DostawcyCRItem i
WHERE i.CRID = @id
ORDER BY i.ItemID;", conn);
            da.SelectCommand.Parameters.Add("@id", SqlDbType.BigInt).Value = crid;
            var dt = new DataTable();
            da.Fill(dt);
            dgvPozycje.DataSource = dt;
        }

        // ===== ACTIONS =====
        private List<long> WybraneCrid()
        {
            return dgvNaglowki.SelectedRows
                     .Cast<DataGridViewRow>()
                     .Where(r => r?.DataBoundItem is DataRowView)
                     .Select(r => (DataRowView)r.DataBoundItem)
                     .Select(rv => Convert.ToInt64(rv["ID Wniosku"]))
                     .ToList();
        }

        private void AkceptujZaznaczone()
        {
            var list = WybraneCrid();
            if (list.Count == 0) { MessageBox.Show("Zaznacz co najmniej jeden wniosek."); return; }

            string reason = Prompt("Uzasadnienie decyzji (wymagane):");
            if (string.IsNullOrWhiteSpace(reason)) return;

            int ok = 0, zastosowane = 0, pominiete = 0, przyszlosc = 0;

            using var conn = new SqlConnection(_connString);
            conn.Open();

            foreach (var crid in list)
            {
                string status = PobierzStatus(crid, conn);
                if (!string.Equals(status, "Proposed", StringComparison.OrdinalIgnoreCase))
                { pominiete++; continue; }

                // 1) aktualizacja decyzji (scalony model)
                using (var cmd = new SqlCommand(@"
UPDATE dbo.DostawcyCR
SET Status = N'Zdecydowany',
    DecyzjaTyp = N'Zaakceptowano',
    DecyzjaKto = @u,
    DecyzjaKiedyUTC = SYSUTCDATETIME(),
    UzasadnienieDecyzji = @r
WHERE CRID=@id AND Status='Proposed';", conn))
                {
                    cmd.Parameters.AddWithValue("@u", _appUser ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@r", reason);
                    cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = crid;
                    ok += cmd.ExecuteNonQuery();
                }

                // 2) „zastosowanie przy akceptacji”
                DateTime eff = PobierzEffectiveFrom(crid, conn);
                if (eff.Date > DateTime.Today)
                {
                    przyszlosc++;
                }
                else
                {
                    if (ZastosujWniosek(crid, conn, out string msg)) zastosowane++;
                    else MessageBox.Show($"CRID={crid}: {msg}", "Zastosowanie przy akceptacji", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            MessageBox.Show(
                $"Zaakceptowano: {ok}\n" +
                $"Zastosowano od razu: {zastosowane}\n" +
                $"Z datą w przyszłości: {przyszlosc}\n" +
                $"Pominięto (zły status): {pominiete}",
                "Akceptacja", MessageBoxButtons.OK, MessageBoxIcon.Information);

            ZaladujNaglowki();
        }

        private void OdrzucZaznaczone()
        {
            var list = WybraneCrid();
            if (list.Count == 0) { MessageBox.Show("Zaznacz co najmniej jeden wniosek."); return; }

            string reason = Prompt("Powód odrzucenia (wymagany):");
            if (string.IsNullOrWhiteSpace(reason)) return;

            int ok = 0, pominiete = 0;
            using var conn = new SqlConnection(_connString);
            conn.Open();

            foreach (var crid in list)
            {
                string status = PobierzStatus(crid, conn);
                if (!string.Equals(status, "Proposed", StringComparison.OrdinalIgnoreCase)) { pominiete++; continue; }

                using var cmd = new SqlCommand(@"
UPDATE dbo.DostawcyCR
SET Status = N'Zdecydowany',
    DecyzjaTyp = N'Odrzucono',
    DecyzjaKto = @u,
    DecyzjaKiedyUTC = SYSUTCDATETIME(),
    UzasadnienieDecyzji = @r
WHERE CRID=@id AND Status='Proposed';", conn);
                cmd.Parameters.AddWithValue("@u", _appUser ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@r", reason);
                cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = crid;
                ok += cmd.ExecuteNonQuery();
            }

            MessageBox.Show($"Odrzucono: {ok}\nPominięto: {pominiete}", "Odrzucenie", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ZaladujNaglowki();
        }

        // ====== CORE: zastosowanie (jedna transakcja, brak osobnych kolumn „Applied”) ======
        private bool ZastosujWniosek(long crid, SqlConnection conn, out string msg)
        {
            msg = null;
            using var trx = conn.BeginTransaction();

            // 1) nagłówek
            string id;
            string status;
            string decyzjaTyp;
            DateTime eff;

            using (var cmd = new SqlCommand(@"
SELECT DostawcaID, Status, DecyzjaTyp, EffectiveFrom
FROM dbo.DostawcyCR WHERE CRID=@id;", conn, trx))
            {
                cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = crid;
                using var rd = cmd.ExecuteReader();
                if (!rd.Read()) { msg = "Wniosek nie istnieje."; trx.Rollback(); return false; }
                id = Convert.ToString(rd["DostawcaID"]);
                status = Convert.ToString(rd["Status"]);
                decyzjaTyp = Convert.ToString(rd["DecyzjaTyp"]);
                eff = Convert.ToDateTime(rd["EffectiveFrom"]);
            }
            if (!string.Equals(status, "Zdecydowany", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(decyzjaTyp, "Zaakceptowano", StringComparison.OrdinalIgnoreCase))
            { msg = $"Status/Decyzja nie pozwala na zastosowanie (Status={status}, Decyzja={decyzjaTyp})."; trx.Rollback(); return false; }

            if (eff.Date > DateTime.Today)
            { msg = "Data wejścia w życie w przyszłości."; trx.Rollback(); return false; }

            // 2) pozycje
            var items = new DataTable();
            using (var da = new SqlDataAdapter("SELECT Field, ProposedNewValue FROM dbo.DostawcyCRItem WHERE CRID=@id ORDER BY ItemID;", conn))
            {
                da.SelectCommand.Transaction = trx;
                da.SelectCommand.Parameters.Add("@id", SqlDbType.BigInt).Value = crid;
                da.Fill(items);
            }
            if (items.Rows.Count == 0)
            { msg = "Wniosek nie ma pozycji."; trx.Rollback(); return false; }

            // 3) audyt (kontekst)
            using (var setCtx = new SqlCommand(
                "EXEC sp_set_session_context @k1,@v1; EXEC sp_set_session_context @k2,@v2;", conn, trx))
            {
                setCtx.Parameters.AddWithValue("@k1", "AppUserID");
                setCtx.Parameters.AddWithValue("@v1", _appUser ?? (object)DBNull.Value);
                setCtx.Parameters.AddWithValue("@k2", "ChangeReason");
                setCtx.Parameters.AddWithValue("@v2", $"CR {crid}: akceptacja=zastosowanie");
                setCtx.ExecuteNonQuery();
            }

            // 4) aktualizacja w Dostawcy
            foreach (DataRow r in items.Rows)
            {
                string field = r["Field"].ToString();
                string newVal = r["ProposedNewValue"]?.ToString();

                if (!AllowedColumns.TryGetValue(field, out var kind))
                { msg = $"Nieobsługiwane pole: {field}"; trx.Rollback(); return false; }

                string sqlUpd = $"UPDATE dbo.Dostawcy SET [{field}] = @val WHERE ID=@ID;";
                using var up = new SqlCommand(sqlUpd, conn, trx);
                up.Parameters.Add("@ID", SqlDbType.VarChar, 10).Value = id;

                if (!TryBindValueParameter(up, "@val", kind, newVal, out string err))
                { msg = $"Błąd wartości dla {field}: {err}"; trx.Rollback(); return false; }

                up.ExecuteNonQuery();
            }

            trx.Commit();
            return true;
        }

        // ===== Helpers =====
        private string PobierzStatus(long crid, SqlConnection conn)
        {
            using var c = new SqlCommand("SELECT Status FROM dbo.DostawcyCR WHERE CRID=@id;", conn);
            c.Parameters.Add("@id", SqlDbType.BigInt).Value = crid;
            return Convert.ToString(c.ExecuteScalar());
        }

        private DateTime PobierzEffectiveFrom(long crid, SqlConnection conn)
        {
            using var c = new SqlCommand("SELECT EffectiveFrom FROM dbo.DostawcyCR WHERE CRID=@id;", conn);
            c.Parameters.Add("@id", SqlDbType.BigInt).Value = crid;
            return Convert.ToDateTime(c.ExecuteScalar());
        }

        private enum ColKind { VarChar, Int, Decimal, BitLike, DateString }
        private static readonly Dictionary<string, ColKind> AllowedColumns = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = ColKind.VarChar,
            ["Nip"] = ColKind.VarChar,
            ["Address"] = ColKind.VarChar,
            ["PostalCode"] = ColKind.VarChar,
            ["City"] = ColKind.VarChar,
            ["ProvinceID"] = ColKind.Int,
            ["PriceTypeID"] = ColKind.Int,
            ["Addition"] = ColKind.Decimal,
            ["Loss"] = ColKind.Decimal,
            ["Halt"] = ColKind.BitLike,
            ["Regon"] = ColKind.VarChar,
            ["Pesel"] = ColKind.VarChar,
            ["AnimNo"] = ColKind.VarChar,
            ["IRZPlus"] = ColKind.VarChar,
            ["IDCard"] = ColKind.VarChar,
            ["IDCardDate"] = ColKind.DateString,
            ["IDCardAuth"] = ColKind.VarChar,
            ["FarmAddress"] = ColKind.VarChar,
            ["FarmPostalCode"] = ColKind.VarChar,
            ["FarmCity"] = ColKind.VarChar,
            ["FarmProvinceID"] = ColKind.Int
        };

        private static bool TryBindValueParameter(SqlCommand cmd, string pname, ColKind kind, string? src, out string error)
        {
            error = null!;
            switch (kind)
            {
                case ColKind.VarChar:
                    cmd.Parameters.Add(pname, SqlDbType.VarChar, 4000).Value = (object?)src ?? DBNull.Value; return true;

                case ColKind.Int:
                    if (string.IsNullOrWhiteSpace(src)) { cmd.Parameters.Add(pname, SqlDbType.Int).Value = DBNull.Value; return true; }
                    if (int.TryParse(src.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int iv))
                    { cmd.Parameters.Add(pname, SqlDbType.Int).Value = iv; return true; }
                    error = "oczekiwano liczby całkowitej"; return false;

                case ColKind.Decimal:
                    if (string.IsNullOrWhiteSpace(src)) { cmd.Parameters.Add(pname, SqlDbType.Decimal).Value = DBNull.Value; return true; }
                    var norm = src.Trim().Replace(',', '.');
                    if (decimal.TryParse(norm, NumberStyles.Any, CultureInfo.InvariantCulture, out var dv))
                    { var p = cmd.Parameters.Add(pname, SqlDbType.Decimal); p.Precision = 19; p.Scale = 4; p.Value = dv; return true; }
                    error = "oczekiwano liczby (np. 1.2345)"; return false;

                case ColKind.BitLike:
                    if (string.IsNullOrWhiteSpace(src)) { cmd.Parameters.Add(pname, SqlDbType.Int).Value = DBNull.Value; return true; }
                    var s = src.Trim().ToLowerInvariant();
                    int bv;
                    if (s is "true" or "1") bv = 1;
                    else if (s is "false" or "0") bv = 0;
                    else if (s == "-1") bv = -1;
                    else if (!int.TryParse(s, out bv)) { error = "oczekiwano 0/1/-1 lub true/false"; return false; }
                    cmd.Parameters.Add(pname, SqlDbType.Int).Value = bv; return true;

                case ColKind.DateString:
                    if (DateTime.TryParse(src, out var dt))
                        cmd.Parameters.Add(pname, SqlDbType.VarChar, 10).Value = dt.ToString("yyyy-MM-dd");
                    else
                        cmd.Parameters.Add(pname, SqlDbType.VarChar, 10).Value = (src ?? "").Trim();
                    return true;

                default:
                    error = "nieznany typ"; return false;
            }
        }

        private static string? Prompt(string title)
        {
            using var f = new Form { Width = 560, Height = 220, Text = title, StartPosition = FormStartPosition.CenterParent };
            var tb = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Dock = DockStyle.Right, Width = 100 };
            var cancel = new Button { Text = "Anuluj", DialogResult = DialogResult.Cancel, Dock = DockStyle.Right, Width = 100 };
            var pnl = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45, FlowDirection = FlowDirection.RightToLeft };
            pnl.Controls.AddRange(new Control[] { ok, cancel });
            var pad = new Label { Text = title, Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
            f.Controls.Add(tb); f.Controls.Add(pnl); f.Controls.Add(pad);
            f.AcceptButton = ok; f.CancelButton = cancel;
            return f.ShowDialog() == DialogResult.OK ? tb.Text?.Trim() : null;
        }
    }
}
