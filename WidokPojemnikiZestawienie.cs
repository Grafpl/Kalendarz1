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
            string query = $@"
            -- Common Table Expression dla pierwszego zakresu dat
            WITH WynikPierwszyZakres AS (
                SELECT 
                    'Suma' AS Kontrahent,
                    SUM(MZ.Ilosc) AS SumaIlosci,
                    '' AS Handlowiec
                FROM 
                    [HANDEL].[HM].[MZ] AS MZ
                INNER JOIN 
                    [HANDEL].[HM].[TW] AS TW ON MZ.[idtw] = TW.[id] 
                INNER JOIN 
                    [HANDEL].[HM].[MG] AS MG ON MZ.[super] = MG.[id] 
                INNER JOIN 
                    [HANDEL].[SSCommon].[STContractors] AS C ON MG.khid = C.id
                WHERE 
                    MZ.[data] >= '2020-01-01' 
                    AND MZ.[data] <= @DataDo 
                    AND MG.[anulowany] = 0
                    AND TW.[nazwa] = '{towar}'
                UNION ALL
                SELECT
                    C.Shortcut AS Kontrahent,
                    SUM(MZ.Ilosc) AS SumaIlosci,
                    ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec
                FROM 
                    [HANDEL].[HM].[MZ] AS MZ
                INNER JOIN 
                    [HANDEL].[HM].[TW] AS TW ON MZ.[idtw] = TW.[id] 
                INNER JOIN 
                    [HANDEL].[HM].[MG] AS MG ON MZ.[super] = MG.[id] 
                INNER JOIN 
                    [HANDEL].[SSCommon].[STContractors] AS C ON MG.khid = C.id
                LEFT JOIN  
                    [HANDEL].[SSCommon].[ContractorClassification] AS WYM ON C.Id = WYM.ElementId
                WHERE 
                    MZ.[data] >= '2020-01-01' 
                    AND MZ.[data] <= @DataDo 
                    AND MG.[anulowany] = 0
                    AND TW.[nazwa] = '{towar}'
                GROUP BY 
                    C.Shortcut, WYM.CDim_Handlowiec_Val
            ),
            -- Common Table Expression dla drugiego zakresu dat
            WynikDrugiZakres AS (
                SELECT 
                    'Suma' AS Kontrahent,
                    SUM(MZ.Ilosc) AS SumaIlosci,
                    '' AS Handlowiec
                FROM 
                    [HANDEL].[HM].[MZ] AS MZ
                INNER JOIN 
                    [HANDEL].[HM].[TW] AS TW ON MZ.[idtw] = TW.[id] 
                INNER JOIN 
                    [HANDEL].[HM].[MG] AS MG ON MZ.[super] = MG.[id] 
                INNER JOIN 
                    [HANDEL].[SSCommon].[STContractors] AS C ON MG.khid = C.id
                WHERE 
                    MZ.[data] >= '2020-01-01' 
                    AND MZ.[data] <= @DataDo 
                    AND MG.[anulowany] = 0
                    AND TW.[nazwa] = '{towar}'
                UNION ALL
                SELECT
                    C.Shortcut AS Kontrahent,
                    SUM(MZ.Ilosc) AS SumaIlosci,
                    ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec
                FROM 
                    [HANDEL].[HM].[MZ] AS MZ
                INNER JOIN 
                    [HANDEL].[HM].[TW] AS TW ON MZ.[idtw] = TW.[id] 
                INNER JOIN 
                    [HANDEL].[HM].[MG] AS MG ON MZ.[super] = MG.[id] 
                INNER JOIN 
                    [HANDEL].[SSCommon].[STContractors] AS C ON MG.khid = C.id
                LEFT JOIN  
                    [HANDEL].[SSCommon].[ContractorClassification] AS WYM ON C.Id = WYM.ElementId
                WHERE 
                    MZ.[data] >= '2020-01-01' 
                    AND MZ.[data] <= @DataDo 
                    AND MG.[anulowany] = 0
                    AND TW.[nazwa] = '{towar}'
                GROUP BY 
                    C.Shortcut, WYM.CDim_Handlowiec_Val
            )
            SELECT 
                COALESCE(Pierwszy.Kontrahent, Drugi.Kontrahent) AS [Kontrahent],
                ISNULL(Pierwszy.SumaIlosci, 0) AS [Ilość w pierwszym zakresie],
                ISNULL(Drugi.SumaIlosci, 0) AS [Ilość w drugim zakresie],
                ISNULL(Drugi.SumaIlosci, 0) - ISNULL(Pierwszy.SumaIlosci, 0) AS [Różnica],
                COALESCE(Pierwszy.Handlowiec, Drugi.Handlowiec) AS [Handlowiec]
            FROM 
                WynikPierwszyZakres AS Pierwszy
            FULL OUTER JOIN 
                WynikDrugiZakres AS Drugi ON Pierwszy.Kontrahent = Drugi.Kontrahent
            WHERE 
                ISNULL(Pierwszy.SumaIlosci, 0) != 0 
                OR ISNULL(Drugi.SumaIlosci, 0) != 0 
                OR ISNULL(Drugi.SumaIlosci, 0) - ISNULL(Pierwszy.SumaIlosci, 0) != 0
            ORDER BY 
                [Ilość w drugim zakresie] DESC, [Kontrahent]";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString2))
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@DataOd", dataOd);
                    command.Parameters.AddWithValue("@DataDo", dataDo);

                    SqlDataAdapter adapter = new SqlDataAdapter(command);
                    DataTable table = new DataTable();
                    adapter.Fill(table);

                    dataGridViewZestawienie.DataSource = table;

                    // Formatowanie tabeli
                   // FormatDataGridView();
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

            // Kolorowanie i pogrubienie wierszy dla drugiego zakresu
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
