using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.HandlowiecDashboard.Views
{
    public partial class KontrahentOpakowaniaWindow : ThemedWindow
    {
        private readonly string _kontrahent;
        private readonly string _connectionString;
        private List<DokumentOpakowaniaRow> _wszystkieDokumenty = new List<DokumentOpakowaniaRow>();

        public KontrahentOpakowaniaWindow(string kontrahent, string connectionString)
        {
            // Ustaw motyw DevExpress
            ApplicationThemeHelper.ApplicationThemeName = Theme.Office2019Black.Name;
            
            InitializeComponent();
            _kontrahent = kontrahent;
            _connectionString = connectionString;

            txtKontrahent.Text = kontrahent;

            InicjalizujFiltry();
            WczytajDane();
        }

        private void InicjalizujFiltry()
        {
            // Lata
            var lata = new List<ComboItem> { new ComboItem { Text = "Wszystkie lata", Value = 0 } };
            var aktualnyRok = DateTime.Today.Year;
            for (int i = 0; i < 5; i++)
            {
                lata.Add(new ComboItem { Text = (aktualnyRok - i).ToString(), Value = aktualnyRok - i });
            }
            cmbRok.ItemsSource = lata;
            cmbRok.EditValue = 0;

            // Typy dokumentow
            var typy = new List<ComboItem>
            {
                new ComboItem { Text = "Wszystkie typy", Value = 0 },
                new ComboItem { Text = "E2 - Pojemniki", Value = 1 },
                new ComboItem { Text = "H1 - Palety", Value = 2 }
            };
            cmbTyp.ItemsSource = typy;
            cmbTyp.EditValue = 0;
        }

        private async void WczytajDane()
        {
            _wszystkieDokumenty.Clear();

            try
            {
                await using var cn = new SqlConnection(_connectionString);
                await cn.OpenAsync();

                // Pobierz handlowca
                var sqlHandlowiec = @"
SELECT TOP 1 ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany')
FROM [HANDEL].[SSCommon].[STContractors] C
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
WHERE C.shortcut = @Kontrahent";

                await using (var cmdH = new SqlCommand(sqlHandlowiec, cn))
                {
                    cmdH.Parameters.AddWithValue("@Kontrahent", _kontrahent);
                    var handlowiec = await cmdH.ExecuteScalarAsync();
                    txtHandlowiec.Text = $"Handlowiec: {handlowiec ?? "-"}";
                }

                // Pobierz dokumenty
                var sql = @"
SELECT
    MZ.data AS Data,
    MG.kod AS NrDokumentu,
    CASE
        WHEN MG.typ = 1 THEN 'WZ'
        WHEN MG.typ = 2 THEN 'PZ'
        WHEN MG.typ = 4 THEN 'RW'
        WHEN MG.typ = 5 THEN 'PW'
        WHEN MG.typ = 9 THEN 'MM+'
        WHEN MG.typ = 10 THEN 'MM-'
        ELSE 'Inny'
    END AS Typ,
    TW.nazwa AS Opakowanie,
    CAST(MZ.Ilosc AS DECIMAL(18,0)) AS Ilosc,
    MG.opis AS Uwagi
FROM [HANDEL].[HM].[MZ] MZ
INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id
INNER JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id
INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON MG.khid = C.id
WHERE C.shortcut = @Kontrahent
  AND MG.anulowany = 0
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1')
ORDER BY MZ.data DESC, MG.kod";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Kontrahent", _kontrahent);

                decimal saldoE2 = 0;
                decimal saldoH1 = 0;
                var dokumenty = new List<DokumentOpakowaniaRow>();

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var opakowanie = reader.GetString(3);
                    var ilosc = reader.IsDBNull(4) ? 0m : Convert.ToDecimal(reader.GetValue(4));

                    if (opakowanie.Contains("E2"))
                        saldoE2 += ilosc;
                    else if (opakowanie.Contains("H1"))
                        saldoH1 += ilosc;

                    dokumenty.Add(new DokumentOpakowaniaRow
                    {
                        Data = reader.GetDateTime(0),
                        NrDokumentu = reader.GetString(1),
                        Typ = reader.GetString(2),
                        Opakowanie = opakowanie,
                        Ilosc = ilosc,
                        Uwagi = reader.IsDBNull(5) ? "" : reader.GetString(5)
                    });
                }

                // Oblicz saldo narastajaco (od najstarszego do najnowszego)
                decimal runningE2 = 0, runningH1 = 0;
                var sortedDocs = dokumenty.OrderBy(d => d.Data).ThenBy(d => d.NrDokumentu).ToList();
                foreach (var doc in sortedDocs)
                {
                    if (doc.Opakowanie.Contains("E2"))
                        runningE2 += doc.Ilosc;
                    else if (doc.Opakowanie.Contains("H1"))
                        runningH1 += doc.Ilosc;

                    doc.SaldoE2 = runningE2;
                    doc.SaldoH1 = runningH1;
                }

                _wszystkieDokumenty = sortedDocs.OrderByDescending(d => d.Data).ThenByDescending(d => d.NrDokumentu).ToList();

                txtSaldoE2.Text = $"{saldoE2:N0}";
                txtSaldoH1.Text = $"{saldoH1:N0}";
                txtLiczbaDokumentow.Text = $"{_wszystkieDokumenty.Count}";

                FiltrujDokumenty();
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Blad wczytywania dokumentow:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FiltrujDokumenty()
        {
            var wynik = _wszystkieDokumenty.AsEnumerable();

            // Filtr roku - DevExpress EditValue
            if (cmbRok.EditValue != null)
            {
                var rokValue = Convert.ToInt32(cmbRok.EditValue);
                if (rokValue > 0)
                {
                    wynik = wynik.Where(d => d.Data.Year == rokValue);
                }
            }

            // Filtr typu opakowania
            if (cmbTyp.EditValue != null)
            {
                var typValue = Convert.ToInt32(cmbTyp.EditValue);
                if (typValue == 1)
                    wynik = wynik.Where(d => d.Opakowanie.Contains("E2"));
                else if (typValue == 2)
                    wynik = wynik.Where(d => d.Opakowanie.Contains("H1"));
            }

            gridDokumenty.ItemsSource = wynik.ToList();
        }

        private void CmbRok_EditValueChanged(object sender, EditValueChangedEventArgs e)
        {
            if (IsLoaded)
                FiltrujDokumenty();
        }

        private void CmbTyp_EditValueChanged(object sender, EditValueChangedEventArgs e)
        {
            if (IsLoaded)
                FiltrujDokumenty();
        }
    }

    public class DokumentOpakowaniaRow
    {
        public DateTime Data { get; set; }
        public string NrDokumentu { get; set; }
        public string Typ { get; set; }
        public string Opakowanie { get; set; }
        public decimal Ilosc { get; set; }
        public decimal SaldoE2 { get; set; }
        public decimal SaldoH1 { get; set; }
        public string Uwagi { get; set; }
        public bool IloscDodatnia => Ilosc > 0;
    }

    // Helper class dla ComboBoxEdit
    public class ComboItem
    {
        public string Text { get; set; }
        public int Value { get; set; }
    }
}
