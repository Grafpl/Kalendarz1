using System;
using System.Data;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    public partial class WidokPojemnikiZestawienie : Form
    {
        private string connectionString2 = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        public WidokPojemnikiZestawienie()
        {
            InitializeComponent();
        }

        private void LoadData(DateTime dataOd, DateTime dataDo, string towar)
        {
            string query = @"
WITH WynikPierwszyZakres AS (
    SELECT 
        'Suma' AS Kontrahent,
        SUM(MZ.Ilosc) AS SumaIlosci,
        '' AS Handlowiec
    FROM [HANDEL].[HM].[MZ] MZ
    INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id 
    INNER JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id 
    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON MG.khid = C.id
    WHERE MZ.data >= '2020-01-01' 
      AND MZ.data <= @DataOd 
      AND MG.anulowany = 0
      AND TW.nazwa = @Towar

    UNION ALL

    SELECT
        C.Shortcut AS Kontrahent,
        SUM(MZ.Ilosc) AS SumaIlosci,
        ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec
    FROM [HANDEL].[HM].[MZ] MZ
    INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id 
    INNER JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id 
    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON MG.khid = C.id
    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
    WHERE MZ.data >= '2020-01-01' 
      AND MZ.data <= @DataOd 
      AND MG.anulowany = 0
      AND TW.nazwa = @Towar
    GROUP BY C.Shortcut, WYM.CDim_Handlowiec_Val
),

WynikDrugiZakres AS (
    SELECT 
        'Suma' AS Kontrahent,
        SUM(MZ.Ilosc) AS SumaIlosci,
        '' AS Handlowiec
    FROM [HANDEL].[HM].[MZ] MZ
    INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id 
    INNER JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id 
    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON MG.khid = C.id
    WHERE MZ.data >= '2020-01-01' 
      AND MZ.data <= @DataDo 
      AND MG.anulowany = 0
      AND TW.nazwa = @Towar

    UNION ALL

    SELECT
        C.Shortcut AS Kontrahent,
        SUM(MZ.Ilosc) AS SumaIlosci,
        ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec
    FROM [HANDEL].[HM].[MZ] MZ
    INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id 
    INNER JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id 
    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON MG.khid = C.id
    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
    WHERE MZ.data >= '2020-01-01' 
      AND MZ.data <= @DataDo 
      AND MG.anulowany = 0
      AND TW.nazwa = @Towar
    GROUP BY C.Shortcut, WYM.CDim_Handlowiec_Val
)

SELECT 
    COALESCE(P.Kontrahent, D.Kontrahent) AS [Kontrahent],
    ISNULL(P.SumaIlosci, 0) AS [Ilość w pierwszym zakresie],
    ISNULL(D.SumaIlosci, 0) AS [Ilość w drugim zakresie],
    ISNULL(D.SumaIlosci, 0) - ISNULL(P.SumaIlosci, 0) AS [Różnica],
    COALESCE(P.Handlowiec, D.Handlowiec) AS [Handlowiec],
    OD.DataOstatniegoDokumentu,
    OD.TowarZDokumentu
FROM WynikPierwszyZakres P
FULL OUTER JOIN WynikDrugiZakres D
    ON P.Kontrahent = D.Kontrahent

OUTER APPLY (
    SELECT TOP 1 *
    FROM (
        SELECT 
            MG.Data AS DataOstatniegoDokumentu,
            MG.Nazwa AS TowarZDokumentu,
            MG.seria
        FROM [HANDEL].[HM].[MG] MG
        INNER JOIN [HANDEL].[HM].[MZ] MZ ON MZ.super = MG.id
        INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id
        INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON MG.khid = C.id
        WHERE MG.anulowany = 0 
          AND MG.seria IN ('sMW', 'sMP')
          AND C.Shortcut = COALESCE(P.Kontrahent, D.Kontrahent)
          AND MG.Data <= @DataDo
    ) AS Sub
    WHERE Sub.DataOstatniegoDokumentu = (
        SELECT MAX(MG2.Data)
        FROM [HANDEL].[HM].[MG] MG2
        INNER JOIN [HANDEL].[SSCommon].[STContractors] C2 ON MG2.khid = C2.id
        WHERE MG2.anulowany = 0 
          AND MG2.seria IN ('sMW', 'sMP')
          AND C2.Shortcut = COALESCE(P.Kontrahent, D.Kontrahent)
          AND MG2.Data <= @DataDo
    )
    ORDER BY 
        CASE WHEN Sub.seria = 'sMP' THEN 1 ELSE 2 END
) OD


WHERE 
    ISNULL(P.SumaIlosci, 0) != 0 
    OR ISNULL(D.SumaIlosci, 0) != 0 
    OR ISNULL(D.SumaIlosci, 0) - ISNULL(P.SumaIlosci, 0) != 0

ORDER BY 
    [Ilość w drugim zakresie] DESC, [Kontrahent];
";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString2))
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@DataOd", dataOd);
                    command.Parameters.AddWithValue("@DataDo", dataDo);
                    command.Parameters.AddWithValue("@Towar", towar);

                    SqlDataAdapter adapter = new SqlDataAdapter(command);
                    DataTable table = new DataTable();
                    adapter.Fill(table);

                    dataGridViewZestawienie.DataSource = table;

                    // Ustaw szerokość ostatniej kolumny na 300
                    if (dataGridViewZestawienie.Columns.Contains("TowarZDokumentu"))
                    {
                        dataGridViewZestawienie.Columns["TowarZDokumentu"].Width = 300;
                    }

                    // Opcjonalnie:
                    // dataGridViewZestawienie.AutoResizeColumns();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}");
            }
        }


        /*private void FormatDataGridView()
        {
            // Ustawienia ogólne
            dataGridViewZestawienie.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dataGridViewZestawienie.ColumnHeadersDefaultCellStyle.Font = new Font("Arial", 12, FontStyle.Bold);
            dataGridViewZestawienie.DefaultCellStyle.Font = new Font("Arial", 11);

            // Stała szerokość kolumny "Kontrahent"
            dataGridViewZestawienie.Columns["Kontrahent"].Width = 200;

            // Kolorowanie i pogrubienie wierszy dla drugiIlość w drugim zakresieego zakresu
            foreach (DataGridViewRow row in dataGridViewZestawienie.Rows)
            {
                if (row.Cells["Ilość w drugim zakresie"].Value != null)
                {
                    int.TryParse(row.Cells["Ilość w drugim zakresie"].Value.ToString(), out int value);

                    // Kolorowanie wartości dodatnich i ujemnych
                    if (value > 0)
                        row.Cells["Ilość w drugim zakresie"].Style.ForeColor = Color.Red;
                    else if (value < 0)
                        row.Cells["Ilość w drugim zakresie"].Style.ForeColor = Color.Green;

                    // Pogrubienie i zwiększona czcionka dla drugiego zakresu
                    row.Cells["Ilość w drugim zakresie"].Style.Font = new Font("Arial", 12, FontStyle.Bold);
                }
            }
        }*/


        private void btnSearch_Click(object sender, EventArgs e)
        {
            if (dateTimePickerOd.Value == null || dateTimePickerDo.Value == null)
            {
                MessageBox.Show("Określ zakres dat.");
                return;
            }

            DateTime dataOd = dateTimePickerOd.Value;
            DateTime dataDo = dateTimePickerDo.Value;

            LoadData(dataOd, dataDo, "Pojemnik Drobiowy E2");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (dateTimePickerOd.Value == null || dateTimePickerDo.Value == null)
            {
                MessageBox.Show("Określ zakres dat.");
                return;
            }

            DateTime dataOd = dateTimePickerOd.Value;
            DateTime dataDo = dateTimePickerDo.Value;

            LoadData(dataOd, dataDo, "Paleta H1");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (dateTimePickerOd.Value == null || dateTimePickerDo.Value == null)
            {
                MessageBox.Show("Określ zakres dat.");
                return;
            }

            DateTime dataOd = dateTimePickerOd.Value;
            DateTime dataDo = dateTimePickerDo.Value;

            LoadData(dataOd, dataDo, "Paleta EURO");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (dateTimePickerOd.Value == null || dateTimePickerDo.Value == null)
            {
                MessageBox.Show("Określ zakres dat.");
                return;
            }

            DateTime dataOd = dateTimePickerOd.Value;
            DateTime dataDo = dateTimePickerDo.Value;

            LoadData(dataOd, dataDo, "Paleta Drewniana");
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (dateTimePickerOd.Value == null || dateTimePickerDo.Value == null)
            {
                MessageBox.Show("Określ zakres dat.");
                return;
            }

            DateTime dataOd = dateTimePickerOd.Value;
            DateTime dataDo = dateTimePickerDo.Value;

            LoadData(dataOd, dataDo, "Paleta plastikowa");
        }
    }
}
