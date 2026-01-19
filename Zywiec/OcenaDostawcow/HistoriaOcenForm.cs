using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    public partial class HistoriaOcenForm : Form
    {
        private const string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string _dostawcaId;
        private string _dostawcaNazwa;
        
        // Kontrolki
        private Panel panelHeader;
        private Label lblTitle;
        private Label lblDostawca;
        private DataGridView dgvHistoria;
        private Panel panelStatystyki;
        private Label lblStatystyki;
        private Button btnZamknij;
        private Button btnExportCSV;
        private Button btnGenerujRaport;
        
        public HistoriaOcenForm(string dostawcaId, string dostawcaNazwa)
        {
            _dostawcaId = dostawcaId;
            _dostawcaNazwa = dostawcaNazwa;
            
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            LoadHistory();
        }

        private void InitializeComponent()
        {
            this.Text = "📜 Historia Ocen Dostawcy";
            this.Size = new Size(1100, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new System.Drawing.Font("Segoe UI", 9.5f);
            this.BackColor = Color.FromArgb(245, 247, 250);
            
            // Panel nagłówka
            panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(44, 62, 80),
                Padding = new Padding(20)
            };
            
            lblTitle = new Label
            {
                Text = "📜 HISTORIA OCEN DOSTAWCY",
                Font = new System.Drawing.Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 10)
            };
            
            lblDostawca = new Label
            {
                Text = $"{_dostawcaNazwa} (ID: {_dostawcaId})",
                Font = new System.Drawing.Font("Segoe UI", 11f),
                ForeColor = Color.FromArgb(189, 195, 199),
                AutoSize = true,
                Location = new Point(20, 40)
            };
            
            panelHeader.Controls.AddRange(new Control[] { lblTitle, lblDostawca });
            
            // DataGridView
            dgvHistoria = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                EnableHeadersVisualStyles = false
            };
            
            // Stylowanie nagłówków
            dgvHistoria.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 152, 219);
            dgvHistoria.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvHistoria.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold);
            dgvHistoria.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
            dgvHistoria.ColumnHeadersHeight = 40;
            
            // Stylowanie wierszy
            dgvHistoria.DefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 9.5f);
            dgvHistoria.DefaultCellStyle.BackColor = Color.White;
            dgvHistoria.DefaultCellStyle.ForeColor = Color.FromArgb(44, 62, 80);
            dgvHistoria.DefaultCellStyle.SelectionBackColor = Color.FromArgb(41, 128, 185);
            dgvHistoria.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvHistoria.DefaultCellStyle.Padding = new Padding(5);
            dgvHistoria.RowTemplate.Height = 35;
            
            // Naprzemienne wiersze
            dgvHistoria.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);
            
            // Event do kolorowania wierszy
            dgvHistoria.CellFormatting += DgvHistoria_CellFormatting;
            dgvHistoria.CellDoubleClick += DgvHistoria_CellDoubleClick;
            
            // Panel statystyk
            panelStatystyki = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(236, 240, 241),
                Padding = new Padding(20, 15, 20, 15)
            };
            
            lblStatystyki = new Label
            {
                Text = "📊 Ładowanie statystyk...",
                Font = new System.Drawing.Font("Segoe UI", 11f, FontStyle.Regular),
                ForeColor = Color.FromArgb(44, 62, 80),
                AutoSize = true,
                Location = new Point(20, 20)
            };
            
            panelStatystyki.Controls.Add(lblStatystyki);
            
            // Panel przycisków
            Panel panelButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(236, 240, 241),
                Padding = new Padding(20, 10, 20, 10)
            };
            
            btnExportCSV = new Button
            {
                Text = "📊 Export CSV",
                Size = new Size(120, 35),
                Location = new Point(20, 12),
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnExportCSV.FlatAppearance.BorderSize = 0;
            btnExportCSV.Click += BtnExportCSV_Click;
            
            btnGenerujRaport = new Button
            {
                Text = "📄 Raport PDF",
                Size = new Size(130, 35),
                Location = new Point(150, 12),
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnGenerujRaport.FlatAppearance.BorderSize = 0;
            btnGenerujRaport.Click += BtnGenerujRaport_Click;
            
            btnZamknij = new Button
            {
                Text = "❌ Zamknij",
                Size = new Size(110, 35),
                BackColor = Color.FromArgb(149, 165, 166),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnZamknij.Location = new Point(this.ClientSize.Width - btnZamknij.Width - 40, 12);
            btnZamknij.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            btnZamknij.FlatAppearance.BorderSize = 0;
            btnZamknij.Click += (s, e) => Close();
            
            panelButtons.Controls.AddRange(new Control[] { btnExportCSV, btnGenerujRaport, btnZamknij });
            
            // Panel główny dla DataGridView
            Panel panelMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20)
            };
            panelMain.Controls.Add(dgvHistoria);
            
            // Dodaj kontrolki do formularza
            this.Controls.Add(panelMain);
            this.Controls.Add(panelStatystyki);
            this.Controls.Add(panelButtons);
            this.Controls.Add(panelHeader);
            
            // Ustaw kolejność dokowania
            panelHeader.BringToFront();
            panelStatystyki.BringToFront();
        }
        
        private async void LoadHistory()
        {
            try
            {
                string query = @"
                    SELECT 
                        ID,
                        DataOceny as [Data Oceny],
                        NumerRaportu as [Nr Raportu],
                        PunktySekcja1_5 as [Punkty 1-5],
                        PunktySekcja6_20 as [Punkty 6-20],
                        PunktyRazem as [SUMA],
                        CASE 
                            WHEN PunktyRazem >= 30 THEN 'BARDZO DOBRA'
                            WHEN PunktyRazem >= 20 THEN 'DOBRA'
                            ELSE 'NIEZADOWALAJĄCA'
                        END as [Ocena],
                        OceniajacyUserID as [Oceniający],
                        Uwagi,
                        DataUtworzenia as [Data Utworzenia]
                    FROM [LibraNet].[dbo].[OcenyDostawcow]
                    WHERE DostawcaID = @DostawcaID
                    ORDER BY DataOceny DESC";
                    
                using var connection = new SqlConnection(connectionString);
                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@DostawcaID", _dostawcaId);
                
                using var adapter = new SqlDataAdapter(command);
                var dataTable = new DataTable();
                await Task.Run(() => adapter.Fill(dataTable));
                
                dgvHistoria.DataSource = dataTable;
                
                // Ukryj kolumnę ID
                if (dgvHistoria.Columns["ID"] != null)
                    dgvHistoria.Columns["ID"].Visible = false;
                
                // Formatowanie kolumn
                if (dgvHistoria.Columns["Data Oceny"] != null)
                {
                    dgvHistoria.Columns["Data Oceny"].DefaultCellStyle.Format = "dd.MM.yyyy";
                    dgvHistoria.Columns["Data Oceny"].Width = 100;
                }
                
                if (dgvHistoria.Columns["Nr Raportu"] != null)
                    dgvHistoria.Columns["Nr Raportu"].Width = 120;
                
                if (dgvHistoria.Columns["Punkty 1-5"] != null)
                {
                    dgvHistoria.Columns["Punkty 1-5"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    dgvHistoria.Columns["Punkty 1-5"].Width = 80;
                }
                
                if (dgvHistoria.Columns["Punkty 6-20"] != null)
                {
                    dgvHistoria.Columns["Punkty 6-20"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    dgvHistoria.Columns["Punkty 6-20"].Width = 80;
                }
                
                if (dgvHistoria.Columns["SUMA"] != null)
                {
                    dgvHistoria.Columns["SUMA"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    dgvHistoria.Columns["SUMA"].DefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold);
                    dgvHistoria.Columns["SUMA"].Width = 80;
                }
                
                if (dgvHistoria.Columns["Ocena"] != null)
                {
                    dgvHistoria.Columns["Ocena"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    dgvHistoria.Columns["Ocena"].DefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 9f, FontStyle.Bold);
                    dgvHistoria.Columns["Ocena"].Width = 140;
                }
                
                if (dgvHistoria.Columns["Data Utworzenia"] != null)
                    dgvHistoria.Columns["Data Utworzenia"].DefaultCellStyle.Format = "dd.MM.yyyy HH:mm";
                
                // Oblicz statystyki
                CalculateStatistics(dataTable);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania historii: {ex.Message}", "Błąd", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void CalculateStatistics(DataTable dataTable)
        {
            if (dataTable.Rows.Count == 0)
            {
                lblStatystyki.Text = "📊 Brak ocen dla tego dostawcy";
                return;
            }
            
            int liczbaOcen = dataTable.Rows.Count;
            int sumaWszystkich = 0;
            int najwyzsza = 0;
            int najnizsza = 100;
            int bardzoDobre = 0;
            int dobre = 0;
            int niezadowalajace = 0;
            
            foreach (DataRow row in dataTable.Rows)
            {
                if (row["SUMA"] != DBNull.Value)
                {
                    int punkty = Convert.ToInt32(row["SUMA"]);
                    sumaWszystkich += punkty;
                    
                    if (punkty > najwyzsza) najwyzsza = punkty;
                    if (punkty < najnizsza) najnizsza = punkty;
                    
                    if (punkty >= 30) bardzoDobre++;
                    else if (punkty >= 20) dobre++;
                    else niezadowalajace++;
                }
            }
            
            double srednia = liczbaOcen > 0 ? (double)sumaWszystkich / liczbaOcen : 0;
            
            lblStatystyki.Text = $"📊 STATYSTYKI: " +
                                 $"Liczba ocen: {liczbaOcen} | " +
                                 $"Średnia: {srednia:F1} pkt | " +
                                 $"Najwyższa: {najwyzsza} pkt | " +
                                 $"Najniższa: {najnizsza} pkt | " +
                                 $"Bardzo dobre: {bardzoDobre} | " +
                                 $"Dobre: {dobre} | " +
                                 $"Niezadowalające: {niezadowalajace}";
            
            // Koloruj panel statystyk w zależności od średniej
            if (srednia >= 30)
                panelStatystyki.BackColor = Color.FromArgb(200, 255, 200);
            else if (srednia >= 20)
                panelStatystyki.BackColor = Color.FromArgb(255, 255, 200);
            else
                panelStatystyki.BackColor = Color.FromArgb(255, 200, 200);
        }
        
        private void DgvHistoria_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            
            // Kolorowanie wierszy na podstawie punktów
            if (dgvHistoria.Columns[e.ColumnIndex].Name == "SUMA" || 
                dgvHistoria.Columns[e.ColumnIndex].Name == "Ocena")
            {
                var row = dgvHistoria.Rows[e.RowIndex];
                if (row.Cells["SUMA"].Value != DBNull.Value)
                {
                    int punkty = Convert.ToInt32(row.Cells["SUMA"].Value);
                    
                    if (punkty >= 30)
                    {
                        e.CellStyle.BackColor = Color.FromArgb(200, 255, 200);
                        e.CellStyle.ForeColor = Color.FromArgb(27, 94, 32);
                    }
                    else if (punkty >= 20)
                    {
                        e.CellStyle.BackColor = Color.FromArgb(255, 250, 205);
                        e.CellStyle.ForeColor = Color.FromArgb(245, 127, 23);
                    }
                    else
                    {
                        e.CellStyle.BackColor = Color.FromArgb(255, 200, 200);
                        e.CellStyle.ForeColor = Color.FromArgb(198, 40, 40);
                    }
                }
            }
        }
        
        private void DgvHistoria_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            
            var row = dgvHistoria.Rows[e.RowIndex];
            if (row.Cells["ID"].Value != DBNull.Value)
            {
                int ocenaId = Convert.ToInt32(row.Cells["ID"].Value);
                string nrRaportu = row.Cells["Nr Raportu"].Value?.ToString() ?? "";
                DateTime dataOceny = Convert.ToDateTime(row.Cells["Data Oceny"].Value);
                int punkty = Convert.ToInt32(row.Cells["SUMA"].Value);
                string uwagi = row.Cells["Uwagi"].Value?.ToString() ?? "";
                
                string szczegoly = $"📋 SZCZEGÓŁY OCENY\n\n" +
                                  $"Nr raportu: {nrRaportu}\n" +
                                  $"Data oceny: {dataOceny:dd.MM.yyyy}\n" +
                                  $"Punkty razem: {punkty}\n\n" +
                                  $"Uwagi:\n{uwagi}";
                
                MessageBox.Show(szczegoly, "Szczegóły oceny", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        
        private void BtnExportCSV_Click(object sender, EventArgs e)
        {
            try
            {
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv",
                    FileName = $"Historia_Ocen_{_dostawcaId}_{DateTime.Now:yyyyMMdd}.csv",
                    DefaultExt = "csv"
                };
                
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    using var writer = new System.IO.StreamWriter(saveDialog.FileName, false, System.Text.Encoding.UTF8);
                    
                    // Nagłówki
                    var headers = new System.Collections.Generic.List<string>();
                    foreach (DataGridViewColumn column in dgvHistoria.Columns)
                    {
                        if (column.Visible)
                            headers.Add(column.HeaderText);
                    }
                    writer.WriteLine(string.Join(";", headers));
                    
                    // Dane
                    foreach (DataGridViewRow row in dgvHistoria.Rows)
                    {
                        var values = new System.Collections.Generic.List<string>();
                        foreach (DataGridViewCell cell in row.Cells)
                        {
                            if (cell.OwningColumn.Visible)
                            {
                                string value = cell.Value?.ToString() ?? "";
                                values.Add(value.Replace(";", ","));
                            }
                        }
                        writer.WriteLine(string.Join(";", values));
                    }
                    
                    MessageBox.Show($"✅ Dane zostały wyeksportowane do pliku:\n{saveDialog.FileName}", 
                        "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd eksportu: {ex.Message}", "Błąd", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void BtnGenerujRaport_Click(object sender, EventArgs e)
        {
            MessageBox.Show("📄 Funkcja generowania raportu PDF zostanie wkrótce dodana", 
                "W przygotowaniu", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
