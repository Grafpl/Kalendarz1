using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Azure.Core.HttpHeader;

namespace Kalendarz1
{
    public partial class WidokCenWszystkich : Form
    {
        static string connectionPermission = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        public string TextBoxValue { get; set; }
        public WidokCenWszystkich()
        {
            InitializeComponent();
            DisplayDataInDataGridView();
        }
        public void SetTextBoxValue()
        {
            textBox1.Text = TextBoxValue;
        }
        private void DisplayDataInDataGridView()
        {
            // Zapytanie SQL zależne od stanu checkboxa
            string query = @"WITH CTE_Ministerialna AS (
    SELECT 
        [Data], 
        CAST([Cena] AS DECIMAL(10, 2)) AS CenaMinisterialna
    FROM 
        [LibraNet].[dbo].[CenaMinisterialna]
    WHERE 
        [Data] >= '2024-01-01'
),
CTE_Rolnicza AS (
    SELECT 
        [Data], 
        CAST([Cena] AS DECIMAL(10, 2)) AS CenaRolnicza
    FROM 
        [LibraNet].[dbo].[CenaRolnicza]
    WHERE 
        [Data] >= '2024-01-01'
),
CTE_HANDEL AS (
    SELECT 
        CONVERT(DATE, DP.[data]) AS Data,
        ROUND(SUM(DP.[wartNetto]) / SUM(DP.[ilosc]), 2) AS Cena
    FROM 
        [RemoteServer].[HANDEL].[HM].[DP] DP 
    INNER JOIN 
        [RemoteServer].[HANDEL].[HM].[TW] TW ON DP.[idtw] = TW.[id] 
    INNER JOIN 
        [RemoteServer].[HANDEL].[HM].[DK] DK ON DP.[super] = DK.[id] 
    WHERE 
        DP.[kod] = 'Kurczak A' 
        AND TW.[katalog] = 67095
        AND DP.[data] >= '2024-01-01'
    GROUP BY 
        CONVERT(DATE, DP.[data])
)
SELECT 
    FORMAT(COALESCE(M.Data, R.Data, H.Data), 'yyyy-MM-dd') + ' ' + DATENAME(WEEKDAY, COALESCE(M.Data, R.Data, H.Data)) AS Data,
    CONCAT(FORMAT(M.CenaMinisterialna, 'N2'), ' zł') AS Mini,
    CONCAT(FORMAT(R.CenaRolnicza, 'N2'), ' zł') AS Rolni,
    CONCAT(FORMAT((ISNULL(M.CenaMinisterialna, 0) + ISNULL(R.CenaRolnicza, 0)) / 2.0, 'N2'), ' zł') AS Laczo,
    CONCAT(FORMAT(H.Cena, 'N2'), ' zł') AS HandelCena
FROM 
    CTE_Ministerialna M
FULL OUTER JOIN 
    CTE_Rolnicza R ON M.Data = R.Data
FULL OUTER JOIN 
    CTE_HANDEL H ON COALESCE(M.Data, R.Data) = H.Data
WHERE
    COALESCE(M.Data, R.Data, H.Data) >= '2024-01-01'
ORDER BY 
    Data DESC;
";

            // Utworzenie połączenia z bazą danych
            using (SqlConnection connection = new SqlConnection(connectionPermission))
            {
                // Utworzenie adaptera danych
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);

                // Utworzenie tabeli danych
                DataTable table = new DataTable();

                // Wypełnienie tabeli danymi z adaptera
                adapter.Fill(table);

                // Ustawienie DataTable jako DataSource dla DataGridView
                dataGridView1.DataSource = table;
            }
            SetRowHeights(15);
        }


        // Metoda do ustawienia wysokości wierszy
        private void SetRowHeights(int height)
        {
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                row.Height = height;
            }
        }


    }
}
