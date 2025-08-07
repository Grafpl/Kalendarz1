using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class WidokFakturSprzedazy : Form
    {
        private string connectionString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private bool isDataLoading = true;

        // Właściwość do ustawienia ID zalogowanego użytkownika z zewnątrz
        public string UserID { get; set; }

        // Mapa UserID na nazwę handlowca w bazie danych
        private readonly Dictionary<string, string> mapaHandlowcow = new Dictionary<string, string>
        {
            { "9991", "Dawid" },
            { "9998", "Daniel" },
            { "871231", "Radek" },
            { "432143", "Ania" }
            // Można tu dodać więcej mapowań
        };

        // Przechowuje nazwę handlowca do filtrowania. null oznacza brak filtru (admin)
        private string? _docelowyHandlowiec;

        public WidokFakturSprzedazy()
        {
            InitializeComponent();
            UserID = "11111"; // Przykładowa wartość domyślna dla testów - admin
        }

        private void WidokFakturSprzedazy_Load(object? sender, EventArgs e)
        {
            // === NOWA LOGIKA USTAWIANIA FILTRU NA PODSTAWIE USERID ===
            if (UserID == "11111") // Specjalny UserID dla administratora
            {
                _docelowyHandlowiec = null; // null oznacza "pokaż wszystkich"
            }
            else if (mapaHandlowcow.ContainsKey(UserID))
            {
                _docelowyHandlowiec = mapaHandlowcow[UserID];
            }
            else
            {
                // Jeśli UserID nie pasuje, nie pokazuj niczego i zablokuj formularz
                MessageBox.Show("Nieznany lub nieprawidłowy identyfikator użytkownika.", "Błąd logowania", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _docelowyHandlowiec = "____BRAK_UPRAWNIEN____"; // Wartość, która na pewno nic nie znajdzie
            }
            // ==========================================================

            KonfigurujDataGridViewDokumenty();
            KonfigurujDataGridViewPozycje();
            KonfigurujDataGridViewAnalizy();

            ZaladujTowary();
            ZaladujKontrahentow();
            dataGridViewNotatki.RowHeadersVisible = false;
            dataGridViewOdbiorcy.RowHeadersVisible = false;
            dataGridViewAnaliza.RowHeadersVisible = false;
            isDataLoading = false;
            OdswiezDaneGlownejSiatki();
        }

        // =================================================================
        // METODY KONFIGURACYJNE
        // =================================================================

        private void KonfigurujDataGridViewDokumenty()
        {
            dataGridViewOdbiorcy.AutoGenerateColumns = false;
            dataGridViewOdbiorcy.Columns.Clear();
            dataGridViewOdbiorcy.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewOdbiorcy.MultiSelect = false;
            dataGridViewOdbiorcy.ReadOnly = true;
            dataGridViewOdbiorcy.AllowUserToAddRows = false;
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "khid", DataPropertyName = "khid", Visible = false });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "ID", DataPropertyName = "ID", Visible = false });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsGroupRow", DataPropertyName = "IsGroupRow", Visible = false });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "SortDate", DataPropertyName = "SortDate", Visible = false });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "NumerDokumentu", DataPropertyName = "NumerDokumentu", HeaderText = "Numer Dokumentu", Width = 150 });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "NazwaFirmy", DataPropertyName = "NazwaFirmy", HeaderText = "Nazwa Firmy", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "IloscKG", DataPropertyName = "IloscKG", HeaderText = "Ilość KG", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "SredniaCena", DataPropertyName = "SredniaCena", HeaderText = "Śr. Cena KG", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Handlowiec", DataPropertyName = "Handlowiec", HeaderText = "Handlowiec", Width = 120 });
            dataGridViewOdbiorcy.SelectionChanged += new EventHandler(this.dataGridViewDokumenty_SelectionChanged);
            dataGridViewOdbiorcy.RowPrePaint += new DataGridViewRowPrePaintEventHandler(this.dataGridViewOdbiorcy_RowPrePaint);
            dataGridViewOdbiorcy.CellFormatting += new DataGridViewCellFormattingEventHandler(this.dataGridViewOdbiorcy_CellFormatting);
        }

        private void KonfigurujDataGridViewPozycje()
        {
            dataGridViewNotatki.AutoGenerateColumns = false;
            dataGridViewNotatki.Columns.Clear();
            dataGridViewNotatki.ReadOnly = true;
            dataGridViewNotatki.Columns.Add(new DataGridViewTextBoxColumn { Name = "KodTowaru", DataPropertyName = "KodTowaru", HeaderText = "Kod Towaru", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dataGridViewNotatki.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ilosc", DataPropertyName = "Ilosc", HeaderText = "Ilość", Width = 60 });
            dataGridViewNotatki.Columns.Add(new DataGridViewTextBoxColumn { Name = "Cena", DataPropertyName = "Cena", HeaderText = "Cena Netto", Width = 60, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            dataGridViewNotatki.Columns.Add(new DataGridViewTextBoxColumn { Name = "Wartosc", DataPropertyName = "Wartosc", HeaderText = "Wartość Netto", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
        }

        private void KonfigurujDataGridViewAnalizy()
        {
            dataGridViewAnaliza.AutoGenerateColumns = false;
            dataGridViewAnaliza.Columns.Clear();
            dataGridViewAnaliza.ReadOnly = true;
            dataGridViewAnaliza.Columns.Add(new DataGridViewTextBoxColumn { Name = "NazwaTowaru", DataPropertyName = "NazwaTowaru", HeaderText = "Nazwa Towaru", Frozen = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
            for (int i = 9; i >= 0; i--) { dataGridViewAnaliza.Columns.Add(new DataGridViewTextBoxColumn { Name = $"Tydzien_{i}", DataPropertyName = $"Tydzien_{i}", HeaderText = $"Tydzień {-i}\n{PobierzDatyTygodnia(i)}", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } }); }
        }

        private string PobierzDatyTygodnia(int tygodniWstecz)
        {
            DateTime dzis = DateTime.Today;
            int przesuniecieDni = (int)dzis.DayOfWeek - (int)DayOfWeek.Monday;
            if (przesuniecieDni < 0) { przesuniecieDni += 7; }
            DateTime poczatekTygodnia = dzis.AddDays(-przesuniecieDni - (tygodniWstecz * 7));
            DateTime koniecTygodnia = poczatekTygodnia.AddDays(6);
            return $"({poczatekTygodnia:dd.MM}-{koniecTygodnia:dd.MM})";
        }

        // =================================================================
        // METODY WCZYTYWANIA DANYCH
        // =================================================================

        private void ZaladujTowary()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT [ID], [kod] FROM [HANDEL].[HM].[TW] WHERE katalog = '67095' ORDER BY Kod ASC";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable towary = new DataTable();
                adapter.Fill(towary);
                DataRow dr = towary.NewRow();
                dr["ID"] = 0;
                dr["kod"] = "--- Wszystkie towary ---";
                towary.Rows.InsertAt(dr, 0);
                comboBoxTowar.DisplayMember = "kod";
                comboBoxTowar.ValueMember = "ID";
                comboBoxTowar.DataSource = towary;
            }
        }

        private void ZaladujKontrahentow()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Zapytanie filtruje kontrahentów na podstawie zalogowanego handlowca
                string query = @"
                    DECLARE @NazwaHandlowca NVARCHAR(100) = @pNazwaHandlowca;
                    SELECT DISTINCT C.id, C.shortcut AS nazwa
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
                    WHERE @NazwaHandlowca IS NULL OR WYM.CDim_Handlowiec_Val = @NazwaHandlowca
                    ORDER BY C.shortcut ASC;";

                var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@pNazwaHandlowca", (object)_docelowyHandlowiec ?? DBNull.Value);

                SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                DataTable kontrahenci = new DataTable();
                adapter.Fill(kontrahenci);

                DataRow dr = kontrahenci.NewRow();
                dr["id"] = 0;
                dr["nazwa"] = "--- Wszyscy kontrahenci ---";
                kontrahenci.Rows.InsertAt(dr, 0);
                comboBoxKontrahent.DisplayMember = "nazwa";
                comboBoxKontrahent.ValueMember = "id";
                comboBoxKontrahent.DataSource = kontrahenci;
            }
        }

        private void WczytajDokumentySprzedazy(int? towarId, int? kontrahentId)
        {
            string query = @"
DECLARE @TowarID INT = @pTowarID;
DECLARE @KontrahentID INT = @pKontrahentID;
DECLARE @NazwaHandlowca NVARCHAR(100) = @pNazwaHandlowca;

WITH AgregatyDokumentu AS (
    SELECT super AS id_dk, SUM(ilosc) AS SumaKG, SUM(wartNetto) / NULLIF(SUM(ilosc), 0) AS SredniaCena
    FROM [HANDEL].[HM].[DP] WHERE @TowarID IS NULL OR idtw = @TowarID GROUP BY super
),
DokumentyFiltrowane AS (
    SELECT DISTINCT DK.*, WYM.CDim_Handlowiec_Val 
    FROM [HANDEL].[HM].[DK] DK
    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
    WHERE 
        (@NazwaHandlowca IS NULL OR WYM.CDim_Handlowiec_Val = @NazwaHandlowca) -- FILTR HANDLOWCA
        AND (@KontrahentID IS NULL OR DK.khid = @KontrahentID)
        AND (@TowarID IS NULL OR EXISTS (SELECT 1 FROM [HANDEL].[HM].[DP] DP WHERE DP.super = DK.id AND DP.idtw = @TowarID))
)
-- Wiersze z danymi dokumentów
SELECT 
    CONVERT(date, DF.data) AS SortDate, 1 AS SortOrder, 0 AS IsGroupRow,
    DF.kod AS NumerDokumentu, C.shortcut AS NazwaFirmy,
    ISNULL(AD.SumaKG, 0) AS IloscKG, ISNULL(AD.SredniaCena, 0) AS SredniaCena,
    ISNULL(DF.CDim_Handlowiec_Val, '-') AS Handlowiec, DF.khid, DF.id
FROM DokumentyFiltrowane DF
INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DF.khid = C.id
INNER JOIN AgregatyDokumentu AD ON DF.id = AD.id_dk
UNION ALL
-- Wiersze grupujące (daty) - również filtrowane
SELECT DISTINCT
    CONVERT(date, data) AS SortDate, 0 AS SortOrder, 1 AS IsGroupRow,
    NULL, NULL, NULL, NULL, NULL, NULL, NULL
FROM DokumentyFiltrowane
ORDER BY SortDate DESC, SortOrder ASC, SredniaCena DESC;
            ";
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@pTowarID", (object)towarId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@pKontrahentID", (object)kontrahentId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@pNazwaHandlowca", (object)_docelowyHandlowiec ?? DBNull.Value);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    dataGridViewOdbiorcy.DataSource = dt;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas wczytywania dokumentów: " + ex.Message, "Błąd Bazy Danych", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void WczytajPozycjeDokumentu(int idDokumentu)
        {
            string query = @"SELECT DP.kod AS KodTowaru, DP.ilosc AS Ilosc, DP.cena AS Cena, DP.wartNetto AS Wartosc FROM [HANDEL].[HM].[DP] DP WHERE DP.super = @idDokumentu ORDER BY DP.lp;";
            try { using (var conn = new SqlConnection(connectionString)) { var cmd = new SqlCommand(query, conn); cmd.Parameters.AddWithValue("@idDokumentu", idDokumentu); var adapter = new SqlDataAdapter(cmd); var dt = new DataTable(); adapter.Fill(dt); dataGridViewNotatki.DataSource = dt; } } catch (Exception ex) { MessageBox.Show("Błąd podczas wczytywania pozycji dokumentu: " + ex.Message, "Błąd Bazy Danych", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void WczytajAnalizeTygodniowa(int kontrahentId, int? towarId)
        {
            string query = @"
DECLARE @TowarID INT = @pTowarID;
WITH DaneZrodlowe AS (
    SELECT DP.kod AS NazwaTowaru, DP.ilosc, DATEDIFF(week, DK.data, GETDATE()) AS TydzienWstecz
    FROM [HANDEL].[HM].[DP] DP INNER JOIN [HANDEL].[HM].[DK] DK ON DP.super = DK.id
    WHERE DK.khid = @KontrahentID AND DATEDIFF(week, DK.data, GETDATE()) < 10 AND (@TowarID IS NULL OR DP.idtw = @TowarID)
)
SELECT NazwaTowaru,
    SUM(CASE WHEN TydzienWstecz = 9 THEN ilosc ELSE 0 END) AS Tydzien_9, SUM(CASE WHEN TydzienWstecz = 8 THEN ilosc ELSE 0 END) AS Tydzien_8, SUM(CASE WHEN TydzienWstecz = 7 THEN ilosc ELSE 0 END) AS Tydzien_7, SUM(CASE WHEN TydzienWstecz = 6 THEN ilosc ELSE 0 END) AS Tydzien_6, SUM(CASE WHEN TydzienWstecz = 5 THEN ilosc ELSE 0 END) AS Tydzien_5,
    SUM(CASE WHEN TydzienWstecz = 4 THEN ilosc ELSE 0 END) AS Tydzien_4, SUM(CASE WHEN TydzienWstecz = 3 THEN ilosc ELSE 0 END) AS Tydzien_3, SUM(CASE WHEN TydzienWstecz = 2 THEN ilosc ELSE 0 END) AS Tydzien_2, SUM(CASE WHEN TydzienWstecz = 1 THEN ilosc ELSE 0 END) AS Tydzien_1, SUM(CASE WHEN TydzienWstecz = 0 THEN ilosc ELSE 0 END) AS Tydzien_0
FROM DaneZrodlowe GROUP BY NazwaTowaru HAVING SUM(ilosc) > 0 ORDER BY NazwaTowaru;";
            try { using (var conn = new SqlConnection(connectionString)) { var cmd = new SqlCommand(query, conn); cmd.Parameters.AddWithValue("@KontrahentID", kontrahentId); cmd.Parameters.AddWithValue("@pTowarID", (object)towarId ?? DBNull.Value); var adapter = new SqlDataAdapter(cmd); var dt = new DataTable(); adapter.Fill(dt); dataGridViewAnaliza.DataSource = dt; } } catch (Exception ex) { MessageBox.Show("Błąd podczas wczytywania analizy tygodniowej: " + ex.Message, "Błąd Bazy Danych", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // =================================================================
        // ZDARZENIA I METODY POMOCNICZE
        // =================================================================

        private void OdswiezDaneGlownejSiatki()
        {
            if (isDataLoading) return;
            int? selectedTowarId = (comboBoxTowar.SelectedValue != null && (int)comboBoxTowar.SelectedValue != 0) ? (int?)comboBoxTowar.SelectedValue : null;
            int? selectedKontrahentId = (comboBoxKontrahent.SelectedValue != null && (int)comboBoxKontrahent.SelectedValue != 0) ? (int?)comboBoxKontrahent.SelectedValue : null;
            WczytajDokumentySprzedazy(selectedTowarId, selectedKontrahentId);
        }

        private void comboBoxTowar_SelectedIndexChanged(object? sender, EventArgs e)
        {
            OdswiezDaneGlownejSiatki();
        }

        private void comboBoxKontrahent_SelectedIndexChanged(object? sender, EventArgs e)
        {
            OdswiezDaneGlownejSiatki();
        }

        private void dataGridViewDokumenty_SelectionChanged(object? sender, EventArgs e)
        {
            if (dataGridViewOdbiorcy.SelectedRows.Count == 0 || Convert.ToBoolean(dataGridViewOdbiorcy.SelectedRows[0].Cells["IsGroupRow"].Value))
            {
                dataGridViewNotatki.DataSource = null;
                dataGridViewAnaliza.DataSource = null;
                return;
            }
            DataGridViewRow selectedRow = dataGridViewOdbiorcy.SelectedRows[0];
            if (selectedRow.Cells["ID"].Value != DBNull.Value)
            {
                int idDokumentu = Convert.ToInt32(selectedRow.Cells["ID"].Value);
                WczytajPozycjeDokumentu(idDokumentu);
            }
            if (selectedRow.Cells["khid"].Value != DBNull.Value)
            {
                int idKontrahenta = Convert.ToInt32(selectedRow.Cells["khid"].Value);
                int? towarId = (comboBoxTowar.SelectedValue != null && (int)comboBoxTowar.SelectedValue != 0) ? (int?)comboBoxTowar.SelectedValue : null;
                WczytajAnalizeTygodniowa(idKontrahenta, towarId);
            }
        }

        private void dataGridViewOdbiorcy_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (Convert.ToBoolean(dataGridViewOdbiorcy.Rows[e.RowIndex].Cells["IsGroupRow"].Value)) { var row = dataGridViewOdbiorcy.Rows[e.RowIndex]; row.DefaultCellStyle.BackColor = Color.FromArgb(220, 220, 220); row.DefaultCellStyle.ForeColor = Color.Black; row.DefaultCellStyle.Font = new Font(dataGridViewOdbiorcy.Font, FontStyle.Bold); row.Height = 30; row.DefaultCellStyle.SelectionBackColor = row.DefaultCellStyle.BackColor; row.DefaultCellStyle.SelectionForeColor = row.DefaultCellStyle.ForeColor; }
        }

        private void dataGridViewOdbiorcy_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (Convert.ToBoolean(dataGridViewOdbiorcy.Rows[e.RowIndex].Cells["IsGroupRow"].Value)) { if (e.ColumnIndex == dataGridViewOdbiorcy.Columns["NumerDokumentu"].Index) { if (dataGridViewOdbiorcy.Rows[e.RowIndex].Cells["SortDate"].Value is DateTime dateValue) { e.Value = dateValue.ToString("dddd, dd MMMM yyyy", new CultureInfo("pl-PL")); } } else { e.Value = ""; e.FormattingApplied = true; } }
        }
    }
}