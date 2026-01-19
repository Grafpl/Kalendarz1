using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class FormZadania : Form
    {
        private string connectionString;
        private string operatorID;
        private DataGridView dataGridViewZadania;

        public FormZadania(string connString, string opID)
        {
            connectionString = connString;
            operatorID = opID;
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
        }

        private void InitializeComponent()
        {
            this.Text = "Moje zadania";
            this.Size = new Size(1000, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            var labelTytul = new Label
            {
                Text = "ZADANIA NA DZIŚ",
                Location = new Point(20, 10),
                Size = new Size(300, 30),
                Font = new Font("Segoe UI", 14, FontStyle.Bold)
            };

            dataGridViewZadania = new DataGridView
            {
                Location = new Point(20, 50),
                Size = new Size(940, 450),
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AllowUserToAddRows = false,
                ReadOnly = false
            };
            dataGridViewZadania.CellContentClick += DataGridViewZadania_CellContentClick;

            var buttonOdswież = new Button
            {
                Text = "Odśwież",
                Location = new Point(20, 510),
                Size = new Size(100, 30)
            };
            buttonOdswież.Click += (s, e) => WczytajZadania();

            this.Controls.AddRange(new Control[] { labelTytul, dataGridViewZadania, buttonOdswież });
            this.Load += FormZadania_Load;
        }

        private void FormZadania_Load(object sender, EventArgs e)
        {
            WczytajZadania();
        }

        private void WczytajZadania()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT 
                        Z.ID,
                        O.Nazwa AS Firma,
                        Z.TypZadania,
                        Z.Opis,
                        Z.TerminWykonania,
                        Z.Priorytet,
                        Z.Wykonane,
                        CASE 
                            WHEN Z.TerminWykonania < GETDATE() THEN 'Zaległe'
                            WHEN Z.TerminWykonania < DATEADD(hour, 2, GETDATE()) THEN 'Pilne'
                            ELSE 'Zaplanowane'
                        END AS Status
                    FROM Zadania Z
                    JOIN OdbiorcyCRM O ON Z.IDOdbiorcy = O.ID
                    WHERE Z.OperatorID = @id 
                        AND Z.Wykonane = 0
                        AND CAST(Z.TerminWykonania AS DATE) = CAST(GETDATE() AS DATE)
                    ORDER BY Z.Priorytet DESC, Z.TerminWykonania", conn);

                cmd.Parameters.AddWithValue("@id", operatorID);

                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);

                dataGridViewZadania.DataSource = dt;

                if (!dataGridViewZadania.Columns.Contains("WykonaneCheckbox"))
                {
                    var checkCol = new DataGridViewCheckBoxColumn
                    {
                        Name = "WykonaneCheckbox",
                        DataPropertyName = "Wykonane",
                        HeaderText = "✓",
                        Width = 40
                    };
                    dataGridViewZadania.Columns.Insert(0, checkCol);
                }

                foreach (DataGridViewRow row in dataGridViewZadania.Rows)
                {
                    string status = row.Cells["Status"].Value?.ToString();
                    if (status == "Zaległe")
                        row.DefaultCellStyle.BackColor = Color.LightCoral;
                    else if (status == "Pilne")
                        row.DefaultCellStyle.BackColor = Color.Orange;
                }
            }
        }

        private void DataGridViewZadania_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == dataGridViewZadania.Columns["WykonaneCheckbox"].Index && e.RowIndex >= 0)
            {
                dataGridViewZadania.CommitEdit(DataGridViewDataErrorContexts.Commit);

                int zadanieID = Convert.ToInt32(dataGridViewZadania.Rows[e.RowIndex].Cells["ID"].Value);
                bool wykonane = Convert.ToBoolean(dataGridViewZadania.Rows[e.RowIndex].Cells["WykonaneCheckbox"].Value);

                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(@"
                        UPDATE Zadania 
                        SET Wykonane = @wykonane, DataWykonania = @data 
                        WHERE ID = @id", conn);

                    cmd.Parameters.AddWithValue("@wykonane", wykonane);
                    cmd.Parameters.AddWithValue("@data", wykonane ? (object)DateTime.Now : DBNull.Value);
                    cmd.Parameters.AddWithValue("@id", zadanieID);
                    cmd.ExecuteNonQuery();
                }

                WczytajZadania();
            }
        }
    }
}