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

namespace Kalendarz1
{
    public partial class PokazCeneTuszki : Form
    {
        private string connectionString2 = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        public PokazCeneTuszki()
        {
            InitializeComponent();
            Load += PokazCeneTuszki_Load;
        }

        private void PokazCeneTuszki_Load(object sender, EventArgs e)
        {
            // Zapytanie SQL
            string query = @"
            SELECT
                C.Shortcut AS KontrahentNazwa,
                SUM(DP.Ilosc) AS SumaIlosci,
                ROUND(SUM(DP.[wartNetto]) / SUM(DP.[ilosc]), 2) AS Cena
            FROM 
                [HANDEL].[HM].[DP] DP 
            INNER JOIN 
                [HANDEL].[HM].[TW] TW ON DP.[idtw] = TW.[id] 
            INNER JOIN 
                [HANDEL].[HM].[DK] DK ON DP.[super] = DK.[id] 
            INNER JOIN 
                [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
            WHERE 
                DP.[data] >= CAST(GETDATE() AS DATE)
                AND DP.[data] < DATEADD(DAY, 1, CAST(GETDATE() AS DATE)) 
                AND DP.[kod] = 'Kurczak A' 
                AND TW.[katalog] = 67095
            GROUP BY 
                C.Shortcut, CONVERT(date, DP.[data])
            ORDER BY 
                SumaIlosci DESC";

            // Utwórz połączenie z bazą danych
            using (SqlConnection connection = new SqlConnection(connectionString2))
            {
                // Utwórz adapter danych i uzupełnij DataGridView
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable dataTable = new DataTable();
                adapter.Fill(dataTable);
                dataGridView1.DataSource = dataTable;
            }
        }
    }
}