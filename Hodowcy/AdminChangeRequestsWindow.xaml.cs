#nullable enable

using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Kalendarz1.Hodowcy
{
    public partial class AdminChangeRequestsWindow : Window
    {
        private readonly string _connString;
        private readonly string _appUser;
        private ObservableCollection<CrHeaderItem> _headers = new();
        private ObservableCollection<CrDetailItem> _items = new();

        public AdminChangeRequestsWindow(string connString, string appUser)
        {
            _connString = connString;
            _appUser = string.IsNullOrWhiteSpace(appUser) ? (App.UserID ?? Environment.UserName) : appUser;
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            cbStatus.Items.Add("Wszystkie");
            cbStatus.Items.Add("Proposed");
            cbStatus.Items.Add("Zdecydowany");
            cbStatus.SelectedIndex = 0;

            dgHeaders.ItemsSource = _headers;
            icItems.ItemsSource = _items;

            LoadHeaders();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5) { LoadHeaders(); e.Handled = true; }
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        }

        // ── Filters ──────────────────────────────────────────────────────

        private void CbStatus_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => LoadHeaders();
        private void TbSearch_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) LoadHeaders(); }
        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => LoadHeaders();

        // ── Load Headers ─────────────────────────────────────────────────

        private void LoadHeaders()
        {
            long? selectedCrid = (dgHeaders.SelectedItem as CrHeaderItem)?.CRID;

            try
            {
                var sql = new StringBuilder(@"
                    SELECT cr.CRID, cr.DostawcaID, d.Name AS DostawcaNazwa, cr.Reason,
                           cr.RequestedBy, COALESCE(ur.Name, cr.RequestedBy) AS RequestedByName,
                           cr.RequestedAtUTC AS RequestedAt,
                           cr.EffectiveFrom, cr.Status, cr.DecyzjaTyp,
                           COALESCE(ud.Name, cr.DecyzjaKto) AS DecyzjaKtoName,
                           cr.DecyzjaKiedyUTC AS DecyzjaKiedy,
                           cr.UzasadnienieDecyzji AS Notatka
                    FROM dbo.DostawcyCR cr
                    LEFT JOIN dbo.Dostawcy d ON d.ID = cr.DostawcaID
                    LEFT JOIN dbo.operators ur ON TRY_CAST(cr.RequestedBy AS INT) = ur.ID
                    LEFT JOIN dbo.operators ud ON TRY_CAST(cr.DecyzjaKto AS INT) = ud.ID
                    WHERE 1=1 ");

                string st = cbStatus.SelectedItem?.ToString() ?? "Wszystkie";
                if (st != "Wszystkie") sql.AppendLine(" AND cr.Status = @st");
                string q = tbSearch.Text.Trim();
                if (!string.IsNullOrEmpty(q)) sql.AppendLine(" AND (cr.DostawcaID LIKE @q OR d.Name LIKE @q)");
                sql.AppendLine(" ORDER BY cr.RequestedAtUTC DESC;");

                using var conn = new SqlConnection(_connString);
                conn.Open();
                using var cmd = new SqlCommand(sql.ToString(), conn);
                if (st != "Wszystkie") cmd.Parameters.Add("@st", SqlDbType.NVarChar, 16).Value = st;
                if (!string.IsNullOrEmpty(q)) cmd.Parameters.Add("@q", SqlDbType.NVarChar, 100).Value = "%" + q + "%";

                var list = new List<CrHeaderItem>();
                int pendingCount = 0;

                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        var item = new CrHeaderItem
                        {
                            CRID = rd.GetInt64(rd.GetOrdinal("CRID")),
                            DostawcaID = rd["DostawcaID"] as string ?? "",
                            DostawcaNazwa = rd["DostawcaNazwa"] as string ?? "",
                            Reason = rd["Reason"] as string ?? "",
                            RequestedBy = rd["RequestedBy"]?.ToString() ?? "",
                            RequestedByName = rd["RequestedByName"] as string ?? "",
                            RequestedAt = rd["RequestedAt"] as DateTime?,
                            EffectiveFrom = rd["EffectiveFrom"] as DateTime?,
                            Status = rd["Status"] as string ?? "",
                            DecyzjaTyp = rd["DecyzjaTyp"] as string ?? "",
                            DecyzjaKtoName = rd["DecyzjaKtoName"] as string ?? "",
                            Notatka = rd["Notatka"] as string ?? ""
                        };
                        list.Add(item);
                        if (item.Status.Equals("Proposed", StringComparison.OrdinalIgnoreCase))
                            pendingCount++;
                    }
                }

                _headers.Clear();
                foreach (var h in list) _headers.Add(h);

                lblPendingCount.Text = pendingCount.ToString();
                lblTotalCount.Text = list.Count.ToString();
                lblRowCount.Text = $"Znaleziono wniosków: {list.Count}";

                // Re-select
                if (selectedCrid.HasValue)
                {
                    var match = _headers.FirstOrDefault(h => h.CRID == selectedCrid.Value);
                    if (match != null) dgHeaders.SelectedItem = match;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania wniosków: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Selection ────────────────────────────────────────────────────

        private void DgHeaders_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var sel = dgHeaders.SelectedItem as CrHeaderItem;
            bool hasSelection = sel != null;
            bool isProposed = hasSelection && sel!.Status.Equals("Proposed", StringComparison.OrdinalIgnoreCase);

            // Check if current user is the creator (self-approval block)
            bool isSelfRequest = hasSelection &&
                sel!.RequestedBy.Equals(_appUser, StringComparison.OrdinalIgnoreCase);

            bool canDecide = isProposed && !isSelfRequest;

            btnAccept.IsEnabled = canDecide;
            btnReject.IsEnabled = canDecide;
            tbNote.IsEnabled = hasSelection;
            btnSaveNote.IsEnabled = hasSelection;

            // Show/hide self-approval warning
            pnlSelfWarning.Visibility = (isProposed && isSelfRequest) ? Visibility.Visible : Visibility.Collapsed;

            if (hasSelection)
            {
                // Fill creator info panel
                lblCreator.Text = $"{sel!.RequestedByName} (ID: {sel.RequestedBy})";
                lblCreatedAt.Text = sel.RequestedAt.HasValue
                    ? sel.RequestedAt.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
                    : "—";
                lblReason.Text = string.IsNullOrWhiteSpace(sel.Reason) ? "—" : sel.Reason;
                lblEffective.Text = sel.EffectiveFrom?.ToString("dd.MM.yyyy") ?? "—";
                tbNote.Text = sel.Notatka;
                LoadItems(sel.CRID);
            }
            else
            {
                lblCreator.Text = "";
                lblCreatedAt.Text = "";
                lblReason.Text = "";
                lblEffective.Text = "";
                tbNote.Text = "";
                _items.Clear();
                lblItemCount.Text = "";
            }
        }

        private void LoadItems(long crid)
        {
            _items.Clear();
            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();
                using var cmd = new SqlCommand(
                    "SELECT i.Field, i.OldValue, i.ProposedNewValue AS NewValue FROM dbo.DostawcyCRItem i WHERE i.CRID = @id ORDER BY i.ItemID;", conn);
                cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = crid;
                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    string field = rd["Field"] as string ?? "";
                    string oldVal = rd["OldValue"] as string ?? "";
                    string newVal = rd["NewValue"] as string ?? "";
                    _items.Add(new CrDetailItem
                    {
                        Field = field,
                        FieldLabel = FieldLabels.TryGetValue(field, out var label) ? label : field,
                        OldValue = oldVal,
                        NewValue = newVal,
                        OldValueDisplay = string.IsNullOrWhiteSpace(oldVal) ? "(puste)" : oldVal,
                        NewValueDisplay = string.IsNullOrWhiteSpace(newVal) ? "(puste)" : newVal
                    });
                }
                lblItemCount.Text = _items.Count > 0 ? $"{_items.Count} zmian(y)" : "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania pozycji: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Field name → Polish label mapping ───────────────────────────

        private static readonly Dictionary<string, string> FieldLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = "Nazwa",
            ["Nip"] = "NIP",
            ["Address"] = "Adres",
            ["PostalCode"] = "Kod pocztowy",
            ["City"] = "Miejscowość",
            ["ProvinceID"] = "Województwo",
            ["PriceTypeID"] = "Typ cennika",
            ["Addition"] = "Dodatek",
            ["Loss"] = "Strata",
            ["Halt"] = "Halt (wstrzymanie)",
            ["Regon"] = "REGON",
            ["Pesel"] = "PESEL",
            ["AnimNo"] = "Nr siedziby stada",
            ["IRZPlus"] = "IRZ Plus",
            ["IDCard"] = "Dowód osobisty",
            ["IDCardDate"] = "Data wydania dowodu",
            ["IDCardAuth"] = "Wydany przez",
            ["FarmAddress"] = "Adres fermy",
            ["FarmPostalCode"] = "Kod pocztowy fermy",
            ["FarmCity"] = "Miejscowość fermy",
            ["FarmProvinceID"] = "Województwo fermy"
        };

        // ── Save Note ────────────────────────────────────────────────────

        private void BtnSaveNote_Click(object sender, RoutedEventArgs e)
        {
            var sel = dgHeaders.SelectedItem as CrHeaderItem;
            if (sel == null) return;

            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();
                using var cmd = new SqlCommand("UPDATE dbo.DostawcyCR SET UzasadnienieDecyzji = @note WHERE CRID = @id", conn);
                string note = tbNote.Text.Trim();
                cmd.Parameters.AddWithValue("@note", string.IsNullOrEmpty(note) ? DBNull.Value : note);
                cmd.Parameters.AddWithValue("@id", sel.CRID);
                cmd.ExecuteNonQuery();
                LoadHeaders();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu notatki: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Accept ───────────────────────────────────────────────────────

        private void BtnAccept_Click(object sender, RoutedEventArgs e)
        {
            var sel = dgHeaders.SelectedItem as CrHeaderItem;
            if (sel == null) return;

            // Double-check self-approval
            if (sel.RequestedBy.Equals(_appUser, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Nie możesz zatwierdzić własnego wniosku. Musi to zrobić inna osoba.",
                    "Brak uprawnień", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Zaakceptować wniosek #{sel.CRID} dla dostawcy {sel.DostawcaNazwa}?",
                    "Potwierdzenie akceptacji", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();

                string status = GetStatus(sel.CRID, conn);
                if (!status.Equals("Proposed", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Wniosek nie ma już statusu 'Proposed'.", "Pominięto", MessageBoxButton.OK, MessageBoxImage.Warning);
                    LoadHeaders();
                    return;
                }

                using (var cmd = new SqlCommand(
                    "UPDATE dbo.DostawcyCR SET Status = N'Zdecydowany', DecyzjaTyp = N'Zaakceptowano', DecyzjaKto = @u, DecyzjaKiedyUTC = SYSUTCDATETIME() WHERE CRID=@id AND Status='Proposed';", conn))
                {
                    cmd.Parameters.AddWithValue("@u", _appUser);
                    cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = sel.CRID;
                    cmd.ExecuteNonQuery();
                }

                DateTime? eff = GetEffectiveFrom(sel.CRID, conn);
                if (eff.HasValue && eff.Value.Date > DateTime.Today)
                {
                    MessageBox.Show($"Wniosek zaakceptowany. Zmiany zostaną zastosowane od {eff.Value:yyyy-MM-dd}.",
                        "Zaakceptowano (przyszła data)", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    if (ApplyChanges(sel.CRID, conn, out string msg))
                        MessageBox.Show("Wniosek zaakceptowany i zmiany zastosowane.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show($"Zaakceptowano, ale nie udało się zastosować zmian: {msg}", "Ostrzeżenie", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                LoadHeaders();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd akceptacji: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Reject ───────────────────────────────────────────────────────

        private void BtnReject_Click(object sender, RoutedEventArgs e)
        {
            var sel = dgHeaders.SelectedItem as CrHeaderItem;
            if (sel == null) return;

            // Double-check self-rejection
            if (sel.RequestedBy.Equals(_appUser, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Nie możesz odrzucić własnego wniosku. Musi to zrobić inna osoba.",
                    "Brak uprawnień", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Odrzucić wniosek #{sel.CRID} dla dostawcy {sel.DostawcaNazwa}?",
                    "Potwierdzenie odrzucenia", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();

                string status = GetStatus(sel.CRID, conn);
                if (!status.Equals("Proposed", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Wniosek nie ma już statusu 'Proposed'.", "Pominięto", MessageBoxButton.OK, MessageBoxImage.Warning);
                    LoadHeaders();
                    return;
                }

                using var cmd = new SqlCommand(
                    "UPDATE dbo.DostawcyCR SET Status = N'Zdecydowany', DecyzjaTyp = N'Odrzucono', DecyzjaKto = @u, DecyzjaKiedyUTC = SYSUTCDATETIME() WHERE CRID=@id AND Status='Proposed';", conn);
                cmd.Parameters.AddWithValue("@u", _appUser);
                cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = sel.CRID;
                cmd.ExecuteNonQuery();

                MessageBox.Show("Wniosek odrzucony.", "Odrzucono", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadHeaders();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd odrzucenia: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Apply Changes (ported from AdminChangeRequestsForm) ─────────

        private bool ApplyChanges(long crid, SqlConnection conn, out string msg)
        {
            msg = "Wystąpił nieznany błąd.";
            using var trx = conn.BeginTransaction();

            string? id; string? status; string? decyzjaTyp; DateTime? eff;
            using (var cmd = new SqlCommand("SELECT DostawcaID, Status, DecyzjaTyp, EffectiveFrom FROM dbo.DostawcyCR WHERE CRID=@id;", conn, trx))
            {
                cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = crid;
                using var rd = cmd.ExecuteReader();
                if (!rd.Read()) { msg = "Wniosek nie istnieje."; trx.Rollback(); return false; }
                id = rd["DostawcaID"] as string;
                status = rd["Status"] as string;
                decyzjaTyp = rd["DecyzjaTyp"] as string;
                eff = rd["EffectiveFrom"] as DateTime?;
            }

            if (id == null || !status!.Equals("Zdecydowany", StringComparison.OrdinalIgnoreCase)
                || !decyzjaTyp!.Equals("Zaakceptowano", StringComparison.OrdinalIgnoreCase))
            {
                msg = $"Status/Decyzja nie pozwala na zastosowanie (Status={status}, Decyzja={decyzjaTyp}).";
                trx.Rollback();
                return false;
            }

            if (!eff.HasValue || eff.Value.Date > DateTime.Today)
            {
                msg = "Data wejścia w życie w przyszłości.";
                trx.Rollback();
                return false;
            }

            var items = new DataTable();
            using (var da = new SqlDataAdapter("SELECT Field, ProposedNewValue FROM dbo.DostawcyCRItem WHERE CRID=@id ORDER BY ItemID;", conn))
            {
                da.SelectCommand.Transaction = trx;
                da.SelectCommand.Parameters.Add("@id", SqlDbType.BigInt).Value = crid;
                da.Fill(items);
            }

            if (items.Rows.Count == 0) { msg = "Wniosek nie ma pozycji."; trx.Rollback(); return false; }

            using (var setCtx = new SqlCommand("EXEC sp_set_session_context @k1,@v1; EXEC sp_set_session_context @k2,@v2;", conn, trx))
            {
                setCtx.Parameters.AddWithValue("@k1", "AppUserID");
                setCtx.Parameters.AddWithValue("@v1", _appUser);
                setCtx.Parameters.AddWithValue("@k2", "ChangeReason");
                setCtx.Parameters.AddWithValue("@v2", $"CR {crid}: akceptacja=zastosowanie");
                setCtx.ExecuteNonQuery();
            }

            foreach (DataRow r in items.Rows)
            {
                string? field = r["Field"].ToString();
                if (field == null) continue;
                string? newVal = r["ProposedNewValue"]?.ToString();
                if (!AllowedColumns.TryGetValue(field, out var kind))
                {
                    msg = $"Nieobsługiwane pole: {field}";
                    trx.Rollback();
                    return false;
                }

                string sqlUpd = $"UPDATE dbo.Dostawcy SET [{field}] = @val WHERE ID=@ID;";
                using var up = new SqlCommand(sqlUpd, conn, trx);
                up.Parameters.Add("@ID", SqlDbType.VarChar, 10).Value = id;
                if (!TryBindValueParameter(up, "@val", kind, newVal, out msg))
                {
                    trx.Rollback();
                    return false;
                }
                up.ExecuteNonQuery();
            }

            trx.Commit();
            return true;
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static string GetStatus(long crid, SqlConnection conn)
        {
            using var c = new SqlCommand("SELECT Status FROM dbo.DostawcyCR WHERE CRID=@id;", conn);
            c.Parameters.Add("@id", SqlDbType.BigInt).Value = crid;
            return c.ExecuteScalar() as string ?? "";
        }

        private static DateTime? GetEffectiveFrom(long crid, SqlConnection conn)
        {
            using var c = new SqlCommand("SELECT EffectiveFrom FROM dbo.DostawcyCR WHERE CRID=@id;", conn);
            c.Parameters.Add("@id", SqlDbType.BigInt).Value = crid;
            return c.ExecuteScalar() as DateTime?;
        }

        private enum ColKind { VarChar, Int, Decimal, BitLike, DateString }

        private static readonly Dictionary<string, ColKind> AllowedColumns = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = ColKind.VarChar, ["Nip"] = ColKind.VarChar, ["Address"] = ColKind.VarChar,
            ["PostalCode"] = ColKind.VarChar, ["City"] = ColKind.VarChar, ["ProvinceID"] = ColKind.Int,
            ["PriceTypeID"] = ColKind.Int, ["Addition"] = ColKind.Decimal, ["Loss"] = ColKind.Decimal,
            ["Halt"] = ColKind.BitLike, ["Regon"] = ColKind.VarChar, ["Pesel"] = ColKind.VarChar,
            ["AnimNo"] = ColKind.VarChar, ["IRZPlus"] = ColKind.VarChar, ["IDCard"] = ColKind.VarChar,
            ["IDCardDate"] = ColKind.DateString, ["IDCardAuth"] = ColKind.VarChar,
            ["FarmAddress"] = ColKind.VarChar, ["FarmPostalCode"] = ColKind.VarChar,
            ["FarmCity"] = ColKind.VarChar, ["FarmProvinceID"] = ColKind.Int
        };

        private static bool TryBindValueParameter(SqlCommand cmd, string pname, ColKind kind, string? src, out string error)
        {
            error = null!;
            switch (kind)
            {
                case ColKind.VarChar:
                    cmd.Parameters.Add(pname, SqlDbType.VarChar, 4000).Value = (object?)src ?? DBNull.Value;
                    return true;
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
                    cmd.Parameters.Add(pname, SqlDbType.Int).Value = bv;
                    return true;
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

        // ── Static method for badge count (used by Menu) ────────────────

        public static int GetPendingCount(string connString)
        {
            try
            {
                using var conn = new SqlConnection(connString);
                conn.Open();
                using var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.DostawcyCR WHERE Status = 'Proposed';", conn);
                return (int)cmd.ExecuteScalar();
            }
            catch { return 0; }
        }
    }

    // ── Data models ──────────────────────────────────────────────────

    public class CrHeaderItem
    {
        public long CRID { get; set; }
        public string DostawcaID { get; set; } = "";
        public string DostawcaNazwa { get; set; } = "";
        public string Reason { get; set; } = "";
        public string RequestedBy { get; set; } = "";
        public string RequestedByName { get; set; } = "";
        public DateTime? RequestedAt { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public string Status { get; set; } = "";
        public string DecyzjaTyp { get; set; } = "";
        public string DecyzjaKtoName { get; set; } = "";
        public string Notatka { get; set; } = "";

        // Computed display properties for DataGrid columns
        public string RequestedAtLocal => RequestedAt.HasValue
            ? RequestedAt.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
            : "";
        public string EffectiveFromDisplay => EffectiveFrom?.ToString("dd.MM.yyyy") ?? "";
    }

    public class CrDetailItem
    {
        public string Field { get; set; } = "";
        public string FieldLabel { get; set; } = "";
        public string OldValue { get; set; } = "";
        public string NewValue { get; set; } = "";
        public string OldValueDisplay { get; set; } = "";
        public string NewValueDisplay { get; set; } = "";
    }
}
