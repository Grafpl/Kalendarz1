using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class PanelAdministracyjny : Form
    {
        private string connectionString;
        private DataGridView dataGridViewOperatorzy;
        private DataGridView dataGridViewUprawnienia;
        private ComboBox comboBoxWojewodztwo;
        private ComboBox comboBoxPowiat;
        private ComboBox comboBoxGmina;
        private Button buttonDodajUprawnienie;
        private Button buttonUsunUprawnienie;

        public PanelAdministracyjny(string connString)
        {
            connectionString = connString;
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
        }

        private void InitializeComponent()
        {
            this.Text = "Panel Administracyjny - Zarządzanie Uprawnieniami";
            this.Size = new Size(1200, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            var labelOperatorzy = new Label
            {
                Text = "OPERATORZY (HANDLOWCY)",
                Location = new Point(20, 10),
                Size = new Size(300, 25),
                Font = new Font("Segoe UI", 12, FontStyle.Bold)
            };

            dataGridViewOperatorzy = new DataGridView
            {
                Location = new Point(20, 40),
                Size = new Size(500, 400),
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                AllowUserToAddRows = false
            };
            dataGridViewOperatorzy.SelectionChanged += DataGridViewOperatorzy_SelectionChanged;

            var labelUprawnienia = new Label
            {
                Text = "UPRAWNIENIA TERYTORIALNE",
                Location = new Point(550, 10),
                Size = new Size(300, 25),
                Font = new Font("Segoe UI", 12, FontStyle.Bold)
            };

            dataGridViewUprawnienia = new DataGridView
            {
                Location = new Point(550, 40),
                Size = new Size(600, 300),
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                AllowUserToAddRows = false
            };

            var labelDodajUpr = new Label
            {
                Text = "Dodaj uprawnienie:",
                Location = new Point(550, 360),
                Size = new Size(200, 20),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            var labelWoj = new Label { Text = "Województwo:", Location = new Point(550, 390), Size = new Size(100, 20) };
            comboBoxWojewodztwo = new ComboBox
            {
                Location = new Point(660, 390),
                Size = new Size(200, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            var labelPow = new Label { Text = "Powiat:", Location = new Point(550, 420), Size = new Size(100, 20) };
            comboBoxPowiat = new ComboBox { Location = new Point(660, 420), Size = new Size(200, 25) };

            var labelGm = new Label { Text = "Gmina:", Location = new Point(550, 450), Size = new Size(100, 20) };
            comboBoxGmina = new ComboBox { Location = new Point(660, 450), Size = new Size(200, 25) };

            buttonDodajUprawnienie = new Button
            {
                Text = "Dodaj Uprawnienie",
                Location = new Point(880, 390),
                Size = new Size(150, 40),
                BackColor = Color.Blue,
                ForeColor = Color.White
            };
            buttonDodajUprawnienie.Click += ButtonDodajUprawnienie_Click;

            buttonUsunUprawnienie = new Button
            {
                Text = "Usuń Uprawnienie",
                Location = new Point(880, 440),
                Size = new Size(150, 40),
                BackColor = Color.Red,
                ForeColor = Color.White
            };
            buttonUsunUprawnienie.Click += ButtonUsunUprawnienie_Click;

            this.Controls.AddRange(new Control[] {
                labelOperatorzy, dataGridViewOperatorzy,
                labelUprawnienia, dataGridViewUprawnienia,
                labelDodajUpr, labelWoj, comboBoxWojewodztwo,
                labelPow, comboBoxPowiat, labelGm, comboBoxGmina,
                buttonDodajUprawnienie, buttonUsunUprawnienie
            });

            this.Load += PanelAdministracyjny_Load;
        }

        private void PanelAdministracyjny_Load(object sender, EventArgs e)
        {
            WczytajOperatorow();
            WczytajWojewodztwa();
        }

        private void WczytajOperatorow()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT ID, Name, CreateData
                    FROM operators 
                    WHERE ID != ''
                    ORDER BY Name", conn);

                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);
                dataGridViewOperatorzy.DataSource = dt;
            }
        }

        private void WczytajWojewodztwa()
        {
            comboBoxWojewodztwo.Items.Clear();
            comboBoxWojewodztwo.Items.AddRange(new string[] {
                "Dolnośląskie", "Kujawsko-Pomorskie", "Lubelskie", "Lubuskie",
                "Łódzkie", "Małopolskie", "Mazowieckie", "Opolskie",
                "Podkarpackie", "Podlaskie", "Pomorskie", "Śląskie",
                "Świętokrzyskie", "Warmińsko-Mazurskie", "Wielkopolskie", "Zachodniopomorskie"
            });
        }

        private void DataGridViewOperatorzy_SelectionChanged(object sender, EventArgs e)
        {
            if (dataGridViewOperatorzy.CurrentRow != null)
            {
                string operatorID = dataGridViewOperatorzy.CurrentRow.Cells["ID"].Value?.ToString();
                if (!string.IsNullOrEmpty(operatorID))
                    WczytajUprawnienia(operatorID);
            }
        }

        private void WczytajUprawnienia(string operatorID)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT ID, Wojewodztwo, Powiat, Gmina
                    FROM UprawnieniaTerytorialne 
                    WHERE OperatorID = @id", conn);
                cmd.Parameters.AddWithValue("@id", operatorID);

                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);
                dataGridViewUprawnienia.DataSource = dt;
            }
        }

        private void ButtonDodajUprawnienie_Click(object sender, EventArgs e)
        {
            if (dataGridViewOperatorzy.CurrentRow == null)
            {
                MessageBox.Show("Wybierz operatora");
                return;
            }

            string operatorID = dataGridViewOperatorzy.CurrentRow.Cells["ID"].Value?.ToString();
            string wojewodztwo = comboBoxWojewodztwo.SelectedItem?.ToString();
            string powiat = string.IsNullOrWhiteSpace(comboBoxPowiat.Text) ? null : comboBoxPowiat.Text.Trim();
            string gmina = string.IsNullOrWhiteSpace(comboBoxGmina.Text) ? null : comboBoxGmina.Text.Trim();

            if (string.IsNullOrEmpty(wojewodztwo))
            {
                MessageBox.Show("Wybierz województwo");
                return;
            }

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    INSERT INTO UprawnieniaTerytorialne (OperatorID, Wojewodztwo, Powiat, Gmina)
                    VALUES (@oid, @woj, @pow, @gm)", conn);

                cmd.Parameters.AddWithValue("@oid", operatorID);
                cmd.Parameters.AddWithValue("@woj", wojewodztwo);
                cmd.Parameters.AddWithValue("@pow", (object)powiat ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@gm", (object)gmina ?? DBNull.Value);

                cmd.ExecuteNonQuery();
                MessageBox.Show("Dodano uprawnienie");
                WczytajUprawnienia(operatorID);
            }
        }

        private void ButtonUsunUprawnienie_Click(object sender, EventArgs e)
        {
            if (dataGridViewUprawnienia.CurrentRow != null)
            {
                int uprawnId = Convert.ToInt32(dataGridViewUprawnienia.CurrentRow.Cells["ID"].Value);

                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("DELETE FROM UprawnieniaTerytorialne WHERE ID = @id", conn);
                    cmd.Parameters.AddWithValue("@id", uprawnId);
                    cmd.ExecuteNonQuery();
                }

                string operatorID = dataGridViewOperatorzy.CurrentRow.Cells["ID"].Value?.ToString();
                WczytajUprawnienia(operatorID);
                MessageBox.Show("Usunięto uprawnienie");
            }
        }
    }
}