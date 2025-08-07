using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class WidokFakturSprzedazy : Form
    {
        // 1. Poprawny, pojedynczy connection string
        private string connectionString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        // UWAGA: Kontrolki DataGridView są już zadeklarowane w pliku WidokFakturSprzedazy.Designer.cs
        // Nie deklarujemy ich ponownie tutaj. Będziemy się do nich odnosić po ich nazwach, np. 'dataGridViewOdbiorcy'.

        public WidokFakturSprzedazy()
        {
            InitializeComponent();
            KonfigurujDataGridViewDokumenty();
            KonfigurujDataGridViewPozycje();

            // Wczytanie danych do głównej siatki przy starcie formularza
            WczytajDokumentySprzedazy();
        }

        private void WidokFakturSprzedazy_Load(object? sender, EventArgs e)
        {
            // Konfiguracja wyglądu obu siatek danych
            KonfigurujDataGridViewDokumenty();
            KonfigurujDataGridViewPozycje();

            // Wczytanie danych do głównej siatki przy starcie formularza
            WczytajDokumentySprzedazy();
        }

        private void KonfigurujDataGridViewDokumenty()
        {
            // Odnosimy się do kontrolki z Designera po jej nazwie (np. dataGridViewOdbiorcy)
            dataGridViewOdbiorcy.AutoGenerateColumns = false;
            dataGridViewOdbiorcy.Columns.Clear();
            dataGridViewOdbiorcy.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewOdbiorcy.MultiSelect = false;
            dataGridViewOdbiorcy.ReadOnly = true;

            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "ID", DataPropertyName = "ID", Visible = false });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "NazwaFirmy", DataPropertyName = "NazwaFirmy", HeaderText = "Nazwa Firmy", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "WartoscNetto", DataPropertyName = "WartoscNetto", HeaderText = "Wartość Netto [zł]", Width = 150, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "DataSprzedazy", DataPropertyName = "DataSprzedazy", HeaderText = "Data Sprzedaży", Width = 120 });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Handlowiec", DataPropertyName = "Handlowiec", HeaderText = "Handlowiec", Width = 120 });

            // Podpięcie zdarzenia, które wczyta pozycje. Stara subskrypcja zostanie usunięta w kroku 3.
            dataGridViewOdbiorcy.SelectionChanged += new System.EventHandler(this.dataGridViewDokumenty_SelectionChanged);
        }

        private void KonfigurujDataGridViewPozycje()
        {
            // Odnosimy się do drugiej kontrolki z Designera (np. dataGridViewNotatki)
            dataGridViewNotatki.AutoGenerateColumns = false;
            dataGridViewNotatki.Columns.Clear();
            dataGridViewNotatki.ReadOnly = true;

            dataGridViewNotatki.Columns.Add(new DataGridViewTextBoxColumn { Name = "KodTowaru", DataPropertyName = "KodTowaru", HeaderText = "Kod Towaru", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dataGridViewNotatki.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ilosc", DataPropertyName = "Ilosc", HeaderText = "Ilość", Width = 100 });
            dataGridViewNotatki.Columns.Add(new DataGridViewTextBoxColumn { Name = "Cena", DataPropertyName = "Cena", HeaderText = "Cena Netto", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            dataGridViewNotatki.Columns.Add(new DataGridViewTextBoxColumn { Name = "Wartosc", DataPropertyName = "Wartosc", HeaderText = "Wartość Netto", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
        }

        private void WczytajDokumentySprzedazy()
        {
            string query = @"
SELECT
    DK.id AS ID,
    C.[Shortcut] AS NazwaFirmy,
    DK.walNetto AS WartoscNetto,
    CONVERT(date, DK.data) AS DataSprzedazy,
    ISNULL(WYM.CDim_Handlowiec_Val, '--- BRAK ---') AS Handlowiec -- Zmieniono na czytelniejszy tekst
FROM
    [HANDEL].[HM].[DK] DK
INNER JOIN
    [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
LEFT JOIN
    [HANDEL].[SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
WHERE
    WYM.CDim_Handlowiec_Val IN ('Daniel', 'Jola', 'Ania', 'Dawid', 'Radek') -- Ten warunek jest problemem
ORDER BY
    DK.data DESC;";
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var adapter = new SqlDataAdapter(query, conn);
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
            string query = @"
                SELECT DP.kod AS KodTowaru, DP.ilosc AS Ilosc, DP.cena AS Cena, DP.wartNetto AS Wartosc
                FROM [HANDEL].[HM].[DP] DP
                WHERE DP.super = @idDokumentu
                ORDER BY DP.lp;";
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@idDokumentu", idDokumentu);
                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    dataGridViewNotatki.DataSource = dt;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas wczytywania pozycji dokumentu: " + ex.Message, "Błąd Bazy Danych", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void dataGridViewDokumenty_SelectionChanged(object? sender, EventArgs e)
        {
            if (dataGridViewOdbiorcy.SelectedRows.Count > 0)
            {
                DataGridViewRow selectedRow = dataGridViewOdbiorcy.SelectedRows[0];
                if (selectedRow.Cells["ID"].Value != null && selectedRow.Cells["ID"].Value != DBNull.Value)
                {
                    int idDokumentu = Convert.ToInt32(selectedRow.Cells["ID"].Value);
                    WczytajPozycjeDokumentu(idDokumentu);
                }
            }
            else
            {
                dataGridViewNotatki.DataSource = null;
            }
        }
    }
}