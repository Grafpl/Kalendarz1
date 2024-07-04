using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
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
            chkShowWeekend.CheckedChanged += new EventHandler(chkShowWeekend_CheckedChanged);
        }
        public void SetTextBoxValue()
        {
            textBox1.Text = TextBoxValue;
        }



        private void DisplayDataInDataGridView()
        {
             string query = @"
            WITH CTE_Ministerialna AS (
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
            CTE_Tuszka AS (
                SELECT 
                    [Data], 
                    CAST([Cena] AS DECIMAL(10, 2)) AS CenaTuszki
                FROM 
                    [LibraNet].[dbo].[CenaTuszki]
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
            ),
            CTE_Harmonogram AS (
                SELECT 
                    DataOdbioru AS Data,
                    SUM(CAST(Auta AS DECIMAL(10, 2)) * CAST(Cena AS DECIMAL(10, 2))) / NULLIF(SUM(CAST(Auta AS DECIMAL(10, 2))), 0) AS SredniaCena
                FROM 
                    [LibraNet].[dbo].[HarmonogramDostaw]
                WHERE 
                    (TypCeny = 'Wolnorynkowa' OR TypCeny = 'wolnorynkowa')
                    AND Bufor = 'Potwierdzony'
                GROUP BY 
                    DataOdbioru
            )
            SELECT 
                FORMAT(COALESCE(M.Data, R.Data, H.Data), 'yyyy-MM-dd') + ' ' + LEFT(DATENAME(WEEKDAY, COALESCE(M.Data, R.Data, H.Data)), 3) AS Data,
                CONCAT(FORMAT(M.CenaMinisterialna, 'N2'), ' zł') AS Minister,
                CONCAT(FORMAT((ISNULL(M.CenaMinisterialna, 0) + ISNULL(R.CenaRolnicza, 0)) / 2.0, 'N2'), ' zł') AS Łączona,
                CONCAT(FORMAT(R.CenaRolnicza, 'N2'), ' zł') AS Rolnicza,
                CONCAT(FORMAT(HD.SredniaCena, 'N2'), ' zł') AS Wolny,
                CONCAT(FORMAT(R.CenaRolnicza - HD.SredniaCena, 'N2'), ' zł') AS RolniczaMinusWolny,
                CONCAT(FORMAT(H.Cena, 'N2'), ' zł') AS Tuszka,
                CONCAT(FORMAT(T.CenaTuszki, 'N2'), ' zł') AS Zrzeszenie,
                CONCAT(FORMAT( H.Cena - T.CenaTuszki, 'N2'), ' zł') AS Roznica
                
            FROM 
                CTE_Ministerialna M
            FULL OUTER JOIN 
                CTE_Rolnicza R ON M.Data = R.Data
            FULL OUTER JOIN 
                CTE_Tuszka T ON M.Data = T.Data
            FULL OUTER JOIN 
                CTE_HANDEL H ON COALESCE(M.Data, R.Data) = H.Data
            LEFT JOIN 
                CTE_Harmonogram HD ON COALESCE(M.Data, R.Data, H.Data) = HD.Data
            WHERE
                COALESCE(M.Data, R.Data, H.Data) >= '2024-01-01'
            ORDER BY 
                Data DESC;
            ";

            using (SqlConnection connection = new SqlConnection(connectionPermission))
            {
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable table = new DataTable();
                adapter.Fill(table);
                dataGridView1.DataSource = table;

                // Ustawienie autosize dla kolumny Data
                dataGridView1.Columns["Data"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;

                foreach (DataGridViewColumn column in dataGridView1.Columns)
                {
                    if (column.Name != "Data")
                    {
                        column.Width = 60;
                    }
                }

                dataGridView1.CellFormatting += new DataGridViewCellFormattingEventHandler(dataGridView1_CellFormatting);
                HideWeekendRows();
                dataGridView1.RowHeadersVisible = false;
            }
            SetRowHeights(18);
        }
        private void SetRowHeights(int height)
        {
            // Ustawienie wysokości wszystkich wierszy na określoną wartość
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                row.Height = height;
            }
        }

        private void dataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            FormatRowByDate(e.RowIndex);
        }

        public void FormatRowByDate(int rowIndex)
        {
            if (rowIndex >= 0)
            {
                DateTime parsedDate;
                var dateCell = dataGridView1.Rows[rowIndex].Cells["Data"];
                if (dateCell != null && DateTime.TryParse(dateCell.Value?.ToString().Substring(0, 10), out parsedDate))
                {
                    if (parsedDate.Date == DateTime.Today)
                    {
                        dataGridView1.Rows[rowIndex].DefaultCellStyle.Font = new Font(dataGridView1.Font.FontFamily, 9, FontStyle.Bold);
                        dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.Blue;
                        dataGridView1.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.White;
                    }
                    else
                    {
                        dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.White;
                        dataGridView1.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.Black;
                    }
                }
            }
        }

        private void chkShowWeekend_CheckedChanged(object sender, EventArgs e)
        {
            HideWeekendRows();
        }

        private void HideWeekendRows()
        {
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (row.Cells["Data"].Value != null)
                {
                    DateTime parsedDate;
                    if (DateTime.TryParse(row.Cells["Data"].Value.ToString().Substring(0, 10), out parsedDate))
                    {
                        if (parsedDate.DayOfWeek == DayOfWeek.Saturday || parsedDate.DayOfWeek == DayOfWeek.Sunday)
                        {
                            row.Visible = chkShowWeekend.Checked;
                        }
                    }
                }
            }
        }


        // Metoda do ustawienia wysokości wierszy


    }
}
