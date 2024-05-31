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
    public partial class WidokAvilogPlan : Form
    {
        static string connectionPermission = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        public WidokAvilogPlan()
        {
            InitializeComponent();
        }

        private void WidokAvilogPlan_Load(object sender, EventArgs e)
        {
            LoadData();
        }

        private void LoadData()
        {
            string query = @"
            SELECT 
                  [DataOdbioru],
                  [Dostawca],
                  [Auta],
                  [SztukiDek],
                  [WagaDek],
                  [KmH]
            FROM [LibraNet].[dbo].[HarmonogramDostaw]
            WHERE [Bufor] = 'potwierdzony'
            AND [DataOdbioru] >= CAST(GETDATE() AS DATE)
            ORDER BY [DataOdbioru]";

            using (SqlConnection connection = new SqlConnection(connectionPermission))
            {
                SqlDataAdapter dataAdapter = new SqlDataAdapter(query, connection);
                DataTable dataTable = new DataTable();
                dataAdapter.Fill(dataTable);

                DataView view = new DataView(dataTable);
                DataTable distinctDates = view.ToTable(true, "DataOdbioru");

                DataSet dataSet = new DataSet();
                foreach (DataRow row in distinctDates.Rows)
                {
                    string dateFilter = $"DataOdbioru = '{row["DataOdbioru"]}'";
                    DataTable filteredTable = dataTable.Select(dateFilter).CopyToDataTable();
                    filteredTable.TableName = row["DataOdbioru"].ToString();
                    dataSet.Tables.Add(filteredTable);
                }

                foreach (DataTable table in dataSet.Tables)
                {
                    DataGridView gridView = new DataGridView
                    {
                        DataSource = table,
                        Dock = DockStyle.Top,
                        Height = 150
                    };

                    Label dateLabel = new Label
                    {
                        Text = table.TableName,
                        Dock = DockStyle.Top,
                        Font = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold)
                    };

                    this.Controls.Add(dateLabel);
                    this.Controls.Add(gridView);
                }
            }
        }
    }
}
