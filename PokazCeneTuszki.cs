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
            dateTimePicker1.Value = DateTime.Now; // Ustaw datę na dzisiejszą
            Load += PokazCeneTuszki_Load;
        }


        private void PokazCeneTuszki_Load(object sender, EventArgs e)
        {
            // Pobierz datę z dateTimePicker1
            DateTime selectedDate = dateTimePicker1.Value.Date;

            // Formatuj datę jako string
            string formattedDate = selectedDate.ToString("yyyy-MM-dd");

            dataGridView1.ColumnHeadersVisible = false;
            dataGridView1.RowHeadersVisible = false;

            // Zapytanie SQL z dynamiczną datą
            string query = $@"
                SELECT
                    C.Shortcut AS KontrahentNazwa,
                    SUM(DP.Ilosc) AS SumaIlosci,
                    ROUND(SUM(DP.[wartNetto]) / NULLIF(SUM(DP.[ilosc]), 0), 2) AS Cena
                FROM 
                    [HANDEL].[HM].[DP] DP 
                INNER JOIN 
                    [HANDEL].[HM].[TW] TW ON DP.[idtw] = TW.[id] 
                INNER JOIN 
                    [HANDEL].[HM].[DK] DK ON DP.[super] = DK.[id] 
                INNER JOIN 
                    [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                WHERE 
                    DP.[data] >= '{formattedDate}'
                    AND DP.[data] < DATEADD(DAY, 1, '{formattedDate}') 
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

                // Dopasowanie szerokości kolumn
                dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

                // Ustawienie szerokości kolumn
                if (dataGridView1.Columns.Count > 0)
                {
                    dataGridView1.Columns[0].Width = 85; // Pierwsza kolumna
                    dataGridView1.Columns[1].Width = 50;  // Druga kolumna
                    dataGridView1.Columns[2].Width = 50;  // Trzecia kolumna

                    // Formatowanie kolumny KG z separatorem tysięcy
                    dataGridView1.Columns[1].DefaultCellStyle.Format = "N0";
                }

                // Obliczenie średniej wartości kolumny Cena
                if (dataTable.Rows.Count > 0)
                {
                    double sumaCen = 0;
                    int liczbaWierszy = dataTable.Rows.Count;


                    //double cenaTuszki = CenoweMetody.PobierzCeneKurczakaA();

                    double sredniaCena = sumaCen / liczbaWierszy;

                    // Wyświetlenie średniej wartości w TextBoxie
                    textBox1.Text = sredniaCena.ToString("F2") + " zł";
                }
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

        private void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
            PokazCeneTuszki_Load(sender, null);
        }
    }
}