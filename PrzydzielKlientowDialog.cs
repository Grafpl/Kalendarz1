using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    /// <summary>
    /// Dialog do przypisywania klientów CRM do handlowców z priorytetem
    /// </summary>
    public class PrzydzielKlientowDialog : Form
    {
        private string connectionString;
        private DataGridView gridKlienci;
        private CheckedListBox listHandlowcy;
        private Button btnPrzypisz;
        private Button btnOdswiez;
        private CheckBox chkPriorytet;
        private ComboBox cmbStatus;
        private TextBox txtSzukaj;
        private Label lblInfo;

        public PrzydzielKlientowDialog(string connString)
        {
            connectionString = connString;
            InitializeComponents();
            LoadHandlowcy();
            LoadKlienci();
        }

        private void InitializeComponents()
        {
            this.Text = "Przypisz klientów do handlowców";
            this.Size = new Size(1200, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(245, 247, 249);
            this.Font = new Font("Segoe UI", 9);

            // Panel górny - filtry
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(45, 57, 69),
                Padding = new Padding(10)
            };

            var lblStatus = new Label
            {
                Text = "Status:",
                ForeColor = Color.White,
                Location = new Point(10, 20),
                AutoSize = true
            };
            topPanel.Controls.Add(lblStatus);

            cmbStatus = new ComboBox
            {
                Location = new Point(60, 17),
                Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbStatus.Items.AddRange(new[] { "Wszystkie nowe", "Do zadzwonienia", "Próba kontaktu", "Nowe", "Wszystkie" });
            cmbStatus.SelectedIndex = 0;
            cmbStatus.SelectedIndexChanged += (s, e) => LoadKlienci();
            topPanel.Controls.Add(cmbStatus);

            var lblSzukaj = new Label
            {
                Text = "Szukaj:",
                ForeColor = Color.White,
                Location = new Point(260, 20),
                AutoSize = true
            };
            topPanel.Controls.Add(lblSzukaj);

            txtSzukaj = new TextBox
            {
                Location = new Point(310, 17),
                Width = 200
            };
            txtSzukaj.TextChanged += (s, e) => LoadKlienci();
            topPanel.Controls.Add(txtSzukaj);

            btnOdswiez = new Button
            {
                Text = "🔄 Odśwież",
                Location = new Point(530, 15),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnOdswiez.FlatAppearance.BorderSize = 0;
            btnOdswiez.Click += (s, e) => LoadKlienci();
            topPanel.Controls.Add(btnOdswiez);

            this.Controls.Add(topPanel);

            // Panel prawy - handlowcy
            var rightPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 280,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            var lblHandlowcy = new Label
            {
                Text = "Wybierz handlowców:",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(10, 10),
                AutoSize = true
            };
            rightPanel.Controls.Add(lblHandlowcy);

            listHandlowcy = new CheckedListBox
            {
                Location = new Point(10, 40),
                Size = new Size(250, 400),
                CheckOnClick = true,
                Font = new Font("Segoe UI", 10)
            };
            rightPanel.Controls.Add(listHandlowcy);

            chkPriorytet = new CheckBox
            {
                Text = "⭐ Priorytet (pokaż jako pierwsze)",
                Location = new Point(10, 450),
                AutoSize = true,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(230, 126, 34),
                Checked = true
            };
            rightPanel.Controls.Add(chkPriorytet);

            btnPrzypisz = new Button
            {
                Text = "✓ Przypisz wybranych klientów",
                Location = new Point(10, 490),
                Size = new Size(250, 45),
                BackColor = Color.FromArgb(39, 174, 96),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnPrzypisz.FlatAppearance.BorderSize = 0;
            btnPrzypisz.Click += BtnPrzypisz_Click;
            rightPanel.Controls.Add(btnPrzypisz);

            lblInfo = new Label
            {
                Text = "Zaznacz klientów i handlowców,\nnastępnie kliknij 'Przypisz'",
                Location = new Point(10, 550),
                Size = new Size(250, 60),
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 9)
            };
            rightPanel.Controls.Add(lblInfo);

            this.Controls.Add(rightPanel);

            // Panel główny - klienci
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            gridKlienci = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                Font = new Font("Segoe UI", 9),
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    SelectionBackColor = Color.FromArgb(52, 152, 219),
                    SelectionForeColor = Color.White
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(250, 251, 252)
                }
            };
            gridKlienci.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            gridKlienci.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 57, 69);
            gridKlienci.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            gridKlienci.EnableHeadersVisualStyles = false;

            mainPanel.Controls.Add(gridKlienci);
            this.Controls.Add(mainPanel);
        }

        private void LoadHandlowcy()
        {
            listHandlowcy.Items.Clear();

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    // Pobierz operatorów którzy są handlowcami (mają config w CallReminderConfig lub są aktywni)
                    var cmd = new SqlCommand(@"
                        SELECT DISTINCT o.ID, o.Name
                        FROM operators o
                        WHERE o.ID IS NOT NULL AND o.Name IS NOT NULL
                        ORDER BY o.Name", conn);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string id = reader.GetString(0);
                            string name = reader.IsDBNull(1) ? id : reader.GetString(1);
                            listHandlowcy.Items.Add(new HandlowiecItem { ID = id, Name = name });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania handlowców: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadKlienci()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Upewnij się że tabela WlascicieleOdbiorcow ma kolumnę Priorytet
                    var cmdAlter = new SqlCommand(@"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('WlascicieleOdbiorcow') AND name = 'Priorytet')
                        ALTER TABLE WlascicieleOdbiorcow ADD Priorytet BIT DEFAULT 0", conn);
                    cmdAlter.ExecuteNonQuery();

                    string statusFilter = "";
                    switch (cmbStatus.SelectedIndex)
                    {
                        case 0: // Wszystkie nowe
                            statusFilter = "AND ISNULL(o.Status, '') IN ('Do zadzwonienia', 'Próba kontaktu', 'Nowe', '')";
                            break;
                        case 1: // Do zadzwonienia
                            statusFilter = "AND ISNULL(o.Status, '') = 'Do zadzwonienia'";
                            break;
                        case 2: // Próba kontaktu
                            statusFilter = "AND ISNULL(o.Status, '') = 'Próba kontaktu'";
                            break;
                        case 3: // Nowe
                            statusFilter = "AND ISNULL(o.Status, '') IN ('Nowe', '')";
                            break;
                        case 4: // Wszystkie
                            statusFilter = "";
                            break;
                    }

                    string searchFilter = "";
                    if (!string.IsNullOrWhiteSpace(txtSzukaj.Text))
                    {
                        searchFilter = "AND o.Nazwa LIKE @szukaj";
                    }

                    var cmd = new SqlCommand($@"
                        SELECT
                            o.ID,
                            o.Nazwa,
                            ISNULL(o.MIASTO, '') as Miasto,
                            ISNULL(o.Status, 'Brak') as Status,
                            ISNULL(o.Telefon_K, '') as Telefon,
                            (SELECT COUNT(*) FROM WlascicieleOdbiorcow w WHERE w.IDOdbiorcy = o.ID) as LiczbaHandlowcow,
                            ISNULL((SELECT STRING_AGG(op.Name, ', ')
                                    FROM WlascicieleOdbiorcow w2
                                    JOIN operators op ON w2.OperatorID = op.ID
                                    WHERE w2.IDOdbiorcy = o.ID), '') as PrzypisaniDo
                        FROM OdbiorcyCRM o
                        WHERE 1=1 {statusFilter} {searchFilter}
                        ORDER BY o.Nazwa", conn);

                    if (!string.IsNullOrWhiteSpace(txtSzukaj.Text))
                    {
                        cmd.Parameters.AddWithValue("@szukaj", "%" + txtSzukaj.Text + "%");
                    }

                    var dt = new DataTable();
                    using (var adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(dt);
                    }

                    gridKlienci.DataSource = dt;

                    // Ukryj ID, zmień nazwy kolumn
                    if (gridKlienci.Columns.Contains("ID"))
                        gridKlienci.Columns["ID"].Visible = false;
                    if (gridKlienci.Columns.Contains("Nazwa"))
                        gridKlienci.Columns["Nazwa"].HeaderText = "Nazwa firmy";
                    if (gridKlienci.Columns.Contains("LiczbaHandlowcow"))
                        gridKlienci.Columns["LiczbaHandlowcow"].HeaderText = "Przypisanych";
                    if (gridKlienci.Columns.Contains("PrzypisaniDo"))
                        gridKlienci.Columns["PrzypisaniDo"].HeaderText = "Przypisani handlowcy";

                    lblInfo.Text = $"Znaleziono {dt.Rows.Count} klientów";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania klientów: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnPrzypisz_Click(object sender, EventArgs e)
        {
            // Pobierz zaznaczonych klientów
            var zaznaczeniKlienci = new List<int>();
            foreach (DataGridViewRow row in gridKlienci.SelectedRows)
            {
                if (row.Cells["ID"].Value != null)
                {
                    zaznaczeniKlienci.Add(Convert.ToInt32(row.Cells["ID"].Value));
                }
            }

            // Pobierz zaznaczonych handlowców
            var zaznaczeniHandlowcy = new List<string>();
            foreach (var item in listHandlowcy.CheckedItems)
            {
                if (item is HandlowiecItem h)
                {
                    zaznaczeniHandlowcy.Add(h.ID);
                }
            }

            if (zaznaczeniKlienci.Count == 0)
            {
                MessageBox.Show("Zaznacz przynajmniej jednego klienta w tabeli.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (zaznaczeniHandlowcy.Count == 0)
            {
                MessageBox.Show("Zaznacz przynajmniej jednego handlowca na liście po prawej.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool priorytet = chkPriorytet.Checked;

            var result = MessageBox.Show(
                $"Czy na pewno chcesz przypisać {zaznaczeniKlienci.Count} klientów do {zaznaczeniHandlowcy.Count} handlowców?\n\n" +
                (priorytet ? "⭐ Z PRIORYTETEM - pojawią się jako pierwsi!" : "Bez priorytetu"),
                "Potwierdzenie",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            try
            {
                int dodano = 0;
                int pominięto = 0;

                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    foreach (int klientId in zaznaczeniKlienci)
                    {
                        foreach (string handlowiecId in zaznaczeniHandlowcy)
                        {
                            // Sprawdź czy już istnieje
                            var cmdCheck = new SqlCommand(
                                "SELECT COUNT(*) FROM WlascicieleOdbiorcow WHERE IDOdbiorcy = @klient AND OperatorID = @handlowiec", conn);
                            cmdCheck.Parameters.AddWithValue("@klient", klientId);
                            cmdCheck.Parameters.AddWithValue("@handlowiec", handlowiecId);

                            int exists = (int)cmdCheck.ExecuteScalar();

                            if (exists > 0)
                            {
                                // Aktualizuj priorytet jeśli istnieje
                                if (priorytet)
                                {
                                    var cmdUpdate = new SqlCommand(
                                        "UPDATE WlascicieleOdbiorcow SET Priorytet = 1 WHERE IDOdbiorcy = @klient AND OperatorID = @handlowiec", conn);
                                    cmdUpdate.Parameters.AddWithValue("@klient", klientId);
                                    cmdUpdate.Parameters.AddWithValue("@handlowiec", handlowiecId);
                                    cmdUpdate.ExecuteNonQuery();
                                }
                                pominięto++;
                            }
                            else
                            {
                                // Dodaj nowe przypisanie
                                var cmdInsert = new SqlCommand(
                                    "INSERT INTO WlascicieleOdbiorcow (IDOdbiorcy, OperatorID, Priorytet) VALUES (@klient, @handlowiec, @priorytet)", conn);
                                cmdInsert.Parameters.AddWithValue("@klient", klientId);
                                cmdInsert.Parameters.AddWithValue("@handlowiec", handlowiecId);
                                cmdInsert.Parameters.AddWithValue("@priorytet", priorytet ? 1 : 0);
                                cmdInsert.ExecuteNonQuery();
                                dodano++;
                            }
                        }
                    }
                }

                MessageBox.Show(
                    $"Zakończono!\n\nDodano nowych przypisań: {dodano}\nPominięto (już istniały): {pominięto}" +
                    (priorytet ? "\n\n⭐ Klienci pojawią się jako pierwsi u handlowców!" : ""),
                    "Sukces",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                LoadKlienci();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas przypisywania: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private class HandlowiecItem
        {
            public string ID { get; set; }
            public string Name { get; set; }

            public override string ToString() => Name;
        }
    }
}
