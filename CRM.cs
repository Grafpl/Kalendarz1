using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class CRM : Form
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string operatorID = "";
        private int aktualnyOdbiorcaID = 0;
        private bool isDataLoading = false;

        public string UserID { get; set; }

        public CRM()
        {
            InitializeComponent();

            // Zdarzenia są już podłączone w Designer.cs, więc NIE dodajemy ich ponownie tutaj
            dataGridViewOdbiorcy.EditMode = DataGridViewEditMode.EditOnEnter;

            // Inicjalizacja filtrów
            comboBoxStatusFilter.Items.Clear();
            comboBoxStatusFilter.Items.Add("Wszystkie statusy");
            comboBoxStatusFilter.Items.AddRange(new object[] {
                "Nowy", "Próba kontaktu", "Nawiązano kontakt", "Zgoda na dalszy kontakt",
                "Do wysłania oferta", "Nie zainteresowany", "Poprosił o usunięcie", "Błędny rekord (do raportu)"
            });
            comboBoxStatusFilter.SelectedIndex = 0;

            comboBoxPowiatFilter.Items.Clear();
            comboBoxPowiatFilter.Items.Add("Wszystkie powiaty");
            comboBoxPowiatFilter.SelectedIndex = 0;

            comboBoxPKD.Items.Clear();
            comboBoxPKD.Items.Add("Wszystkie Rodzaje");
            comboBoxPKD.SelectedIndex = 0;

            comboBoxWoj.Items.Clear();
            comboBoxWoj.Items.Add("Wszystkie Woj.");
            comboBoxWoj.SelectedIndex = 0;
        }

        private void CRM_Load(object sender, EventArgs e)
        {
            operatorID = UserID;

            KonfigurujDataGridView();
            WczytajOdbiorcow();
            WczytajRankingHandlowcow();

            if (operatorID == "11111")
            {
                DodajPrzyciskAdmin();
            }

            DodajPrzyciskZadania();
            DodajPrzyciskDodajOdbiorcę();
        }

        private void DodajPrzyciskAdmin()
        {
            var btnAdmin = new Button
            {
                Text = "⚙ Panel",
                Location = new Point(1100, 227),
                Size = new Size(90, 23),
                BackColor = Color.DarkOrange,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnAdmin.Click += (s, e) => {
                var panel = new PanelAdministracyjny(connectionString);
                panel.ShowDialog();
                WczytajOdbiorcow();
            };
            this.Controls.Add(btnAdmin);
        }

        private void DodajPrzyciskZadania()
        {
            var btnZadania = new Button
            {
                Text = "📋 Zadania",
                Location = new Point(1195, 227),
                Size = new Size(90, 23),
                BackColor = Color.SteelBlue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnZadania.Click += (s, e) => {
                var formZadania = new FormZadania(connectionString, operatorID);
                formZadania.ShowDialog();
            };
            this.Controls.Add(btnZadania);
        }

        private void DodajPrzyciskDodajOdbiorcę()
        {
            var btnDodaj = new Button
            {
                Text = "+ Dodaj",
                Location = new Point(352, 1),
                Size = new Size(80, 30),
                BackColor = Color.Green,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnDodaj.Click += (s, e) => {
                var formDodaj = new FormDodajOdbiorce(connectionString, operatorID);
                if (formDodaj.ShowDialog() == DialogResult.OK)
                {
                    WczytajOdbiorcow();
                }
            };
            this.Controls.Add(btnDodaj);
        }

        private void WczytajOdbiorcow()
        {
            isDataLoading = true;
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("sp_PobierzOdbiorcow", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@OperatorID", operatorID);

                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);
                dataGridViewOdbiorcy.DataSource = dt;
            }
            isDataLoading = false;
            dataGridViewOdbiorcy.Refresh();
            WypelnijFiltrPowiatow();
            WypelnijFiltrPKD();
            WypelnijFiltrWoj();
        }

        private void WczytajRankingHandlowcow()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("sp_PobierzRankingHandlowcow", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);
                dataGridViewRanking.DataSource = dt;
            }
        }

        private void KonfigurujDataGridView()
        {
            dataGridViewOdbiorcy.AutoGenerateColumns = false;
            dataGridViewOdbiorcy.Columns.Clear();
            dataGridViewOdbiorcy.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dataGridViewOdbiorcy.RowTemplate.Height = 45;
            dataGridViewOdbiorcy.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dataGridViewOdbiorcy.AllowUserToResizeColumns = false;

            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CzyMoj",
                DataPropertyName = "CzyMoj",
                HeaderText = "★",
                Width = 30,
                ReadOnly = true,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ID",
                DataPropertyName = "ID",
                HeaderText = "ID",
                Width = 50,
                ReadOnly = true
            });

            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Nazwa",
                DataPropertyName = "Nazwa",
                HeaderText = "Nazwa",
                Width = 280,
                ReadOnly = true,
                DefaultCellStyle = { WrapMode = DataGridViewTriState.True }
            });

            var statusColumn = new DataGridViewComboBoxColumn
            {
                Name = "StatusColumn",
                DataPropertyName = "Status",
                HeaderText = "Status",
                FlatStyle = FlatStyle.Flat,
                Width = 120
            };
            statusColumn.Items.AddRange("Nowy", "Próba kontaktu", "Nawiązano kontakt", "Zgoda na dalszy kontakt",
                "Do wysłania oferta", "Nie zainteresowany", "Poprosił o usunięcie", "Błędny rekord (do raportu)");
            dataGridViewOdbiorcy.Columns.Add(statusColumn);

            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "KodPocztowy", DataPropertyName = "KodPocztowy", HeaderText = "Kod", Width = 60, ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "MIASTO", DataPropertyName = "MIASTO", HeaderText = "Miasto", Width = 80, ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ulica", DataPropertyName = "Ulica", HeaderText = "Ulica", Width = 80, ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Telefon_K", DataPropertyName = "Telefon_K", HeaderText = "Telefon", Width = 90, ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Wojewodztwo", DataPropertyName = "Wojewodztwo", HeaderText = "Województwo", Width = 100, ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Powiat", DataPropertyName = "Powiat", HeaderText = "Powiat", Width = 100, ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "PKD_Opis", DataPropertyName = "PKD_Opis", HeaderText = "PKD", Width = 150, ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Score", DataPropertyName = "Score", HeaderText = "Score", Width = 50, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "DataOstatniejNotatki", DataPropertyName = "DataOstatniejNotatki", HeaderText = "Ost. Notatka", Width = 100, ReadOnly = true });

            dataGridViewOdbiorcy.DefaultCellStyle.Font = new Font("Segoe UI", 10);
        }

        private void dataGridViewOdbiorcy_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (!isDataLoading && e.RowIndex >= 0 && dataGridViewOdbiorcy.Columns[e.ColumnIndex].Name == "StatusColumn")
            {
                int idOdbiorcy = Convert.ToInt32(dataGridViewOdbiorcy.Rows[e.RowIndex].Cells["ID"].Value);
                string nowyStatus = dataGridViewOdbiorcy.Rows[e.RowIndex].Cells["StatusColumn"].Value?.ToString() ?? "";
                string staryStatus = PobierzAktualnyStatus(idOdbiorcy);

                AktualizujStatusWBazie(idOdbiorcy, nowyStatus, staryStatus);
                dataGridViewOdbiorcy.InvalidateRow(e.RowIndex);

                var sugestia = ZaproponujNastepnyKrok(nowyStatus);
                if (sugestia != null)
                {
                    var result = MessageBox.Show($"Sugestia: {sugestia.Opis}\n\nCzy zaplanować zadanie?", "Następny krok", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                        UtworzZadanie(idOdbiorcy, sugestia);
                }
            }
        }

        private string PobierzAktualnyStatus(int idOdbiorcy)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT Status FROM OdbiorcyCRM WHERE ID = @id", conn);
                cmd.Parameters.AddWithValue("@id", idOdbiorcy);
                return cmd.ExecuteScalar()?.ToString() ?? "Nowy";
            }
        }

        private void AktualizujStatusWBazie(int idOdbiorcy, string nowyStatus, string staryStatus)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        var cmdUpdate = new SqlCommand("UPDATE OdbiorcyCRM SET Status = @status WHERE ID = @id", conn, transaction);
                        cmdUpdate.Parameters.AddWithValue("@id", idOdbiorcy);
                        cmdUpdate.Parameters.AddWithValue("@status", (object)nowyStatus ?? DBNull.Value);
                        cmdUpdate.ExecuteNonQuery();

                        var cmdLog = new SqlCommand("INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscStara, WartoscNowa, KtoWykonal) VALUES (@idOdbiorcy, @typ, @stara, @nowa, @kto)", conn, transaction);
                        cmdLog.Parameters.AddWithValue("@idOdbiorcy", idOdbiorcy);
                        cmdLog.Parameters.AddWithValue("@typ", "Zmiana statusu");
                        cmdLog.Parameters.AddWithValue("@stara", (object)staryStatus ?? DBNull.Value);
                        cmdLog.Parameters.AddWithValue("@nowa", (object)nowyStatus ?? DBNull.Value);
                        cmdLog.Parameters.AddWithValue("@kto", operatorID);
                        cmdLog.ExecuteNonQuery();

                        transaction.Commit();
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        private SugestiaKroku ZaproponujNastepnyKrok(string status) => status switch
        {
            "Próba kontaktu" => new SugestiaKroku { TypZadania = "Telefon", Opis = "Próba ponownego kontaktu", Termin = DateTime.Now.AddDays(2), Priorytet = 2 },
            "Nawiązano kontakt" => new SugestiaKroku { TypZadania = "Email", Opis = "Wysłać prezentację firmy", Termin = DateTime.Now.AddDays(1), Priorytet = 3 },
            "Zgoda na dalszy kontakt" => new SugestiaKroku { TypZadania = "Oferta", Opis = "Przygotować ofertę", Termin = DateTime.Now.AddHours(4), Priorytet = 3 },
            _ => null
        };

        private void UtworzZadanie(int idOdbiorcy, SugestiaKroku sugestia)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("INSERT INTO Zadania (IDOdbiorcy, OperatorID, TypZadania, Opis, TerminWykonania, Priorytet) VALUES (@odbiorca, @operator, @typ, @opis, @termin, @priorytet)", conn);
                cmd.Parameters.AddWithValue("@odbiorca", idOdbiorcy);
                cmd.Parameters.AddWithValue("@operator", operatorID);
                cmd.Parameters.AddWithValue("@typ", sugestia.TypZadania);
                cmd.Parameters.AddWithValue("@opis", sugestia.Opis);
                cmd.Parameters.AddWithValue("@termin", sugestia.Termin);
                cmd.Parameters.AddWithValue("@priorytet", sugestia.Priorytet);
                cmd.ExecuteNonQuery();
            }
        }

        private void WczytajNotatki(int idOdbiorcy)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT Tresc, DataUtworzenia FROM NotatkiCRM WHERE IDOdbiorcy = @id ORDER BY DataUtworzenia DESC", conn);
                cmd.Parameters.AddWithValue("@id", idOdbiorcy);
                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);
                dataGridViewNotatki.DataSource = dt;
            }
        }

        private void DodajNotatke(int idOdbiorcy, string tresc)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        var cmdNotatka = new SqlCommand("INSERT INTO NotatkiCRM (IDOdbiorcy, Tresc, KtoDodal) VALUES (@id, @tresc, @kto)", conn, transaction);
                        cmdNotatka.Parameters.AddWithValue("@id", idOdbiorcy);
                        cmdNotatka.Parameters.AddWithValue("@tresc", tresc);
                        cmdNotatka.Parameters.AddWithValue("@kto", operatorID);
                        cmdNotatka.ExecuteNonQuery();

                        var cmdLog = new SqlCommand("INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal) VALUES (@id, @typ, @wartosc, @kto)", conn, transaction);
                        cmdLog.Parameters.AddWithValue("@id", idOdbiorcy);
                        cmdLog.Parameters.AddWithValue("@typ", "Dodanie notatki");
                        cmdLog.Parameters.AddWithValue("@wartosc", tresc.Length > 100 ? tresc.Substring(0, 100) + "..." : tresc);
                        cmdLog.Parameters.AddWithValue("@kto", operatorID);
                        cmdLog.ExecuteNonQuery();

                        transaction.Commit();
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
            WczytajNotatki(idOdbiorcy);
        }

        private void WypelnijFiltrPowiatow()
        {
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource;
            if (dt == null) return;
            var powiaty = dt.AsEnumerable().Select(row => row.Field<string>("Powiat")).Where(p => !string.IsNullOrEmpty(p)).Distinct().OrderBy(p => p).ToArray();
            comboBoxPowiatFilter.Items.Clear();
            comboBoxPowiatFilter.Items.Add("Wszystkie powiaty");
            comboBoxPowiatFilter.Items.AddRange(powiaty);
            comboBoxPowiatFilter.SelectedIndex = 0;
        }

        private void WypelnijFiltrPKD()
        {
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource;
            if (dt == null) return;
            var grupy = dt.AsEnumerable().Where(row => !string.IsNullOrWhiteSpace(row.Field<string>("PKD_Opis"))).GroupBy(row => row.Field<string>("PKD_Opis")).Select(g => g.Key).OrderBy(x => x).ToList();
            comboBoxPKD.Items.Clear();
            comboBoxPKD.Items.Add("Wszystkie Rodzaje");
            foreach (var pkd in grupy) comboBoxPKD.Items.Add(pkd);
            comboBoxPKD.SelectedIndex = 0;
        }

        private void WypelnijFiltrWoj()
        {
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource;
            if (dt == null) return;
            var woj = dt.AsEnumerable().Select(row => row.Field<string>("Wojewodztwo")).Where(p => !string.IsNullOrEmpty(p)).Distinct().OrderBy(p => p).ToArray();
            comboBoxWoj.Items.Clear();
            comboBoxWoj.Items.Add("Wszystkie Rodzaje");
            comboBoxWoj.Items.AddRange(woj);
            comboBoxWoj.SelectedIndex = 0;
        }

        private void comboBoxStatusFilter_SelectedIndexChanged(object sender, EventArgs e) => ZastosujFiltry(sender, e);

        private void ZastosujFiltry(object sender, EventArgs e)
        {
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource;
            if (dt == null) return;
            var filtry = new System.Collections.Generic.List<string>();
            if (comboBoxStatusFilter.SelectedIndex > 0) filtry.Add($"Status = '{comboBoxStatusFilter.SelectedItem}'");
            if (comboBoxPowiatFilter.SelectedIndex > 0) filtry.Add($"Powiat = '{comboBoxPowiatFilter.SelectedItem}'");
            if (comboBoxPKD.SelectedIndex > 0) filtry.Add($"PKD_Opis = '{comboBoxPKD.SelectedItem}'");
            if (comboBoxWoj.SelectedIndex > 0) filtry.Add($"Wojewodztwo = '{comboBoxWoj.SelectedItem}'");
            dt.DefaultView.RowFilter = string.Join(" AND ", filtry);
        }

        private void dataGridViewOdbiorcy_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dataGridViewOdbiorcy.Rows.Count) return;
            DataGridViewRow row = dataGridViewOdbiorcy.Rows[e.RowIndex];
            string status = row.Cells["StatusColumn"].Value?.ToString() ?? "Nowy";
            Color kolorWiersza = status switch
            {
                "Nowy" => Color.White,
                "Próba kontaktu" => Color.LightSkyBlue,
                "Nawiązano kontakt" => Color.CornflowerBlue,
                "Zgoda na dalszy kontakt" => Color.LightGreen,
                "Do wysłania oferta" => Color.LightYellow,
                "Nie zainteresowany" => Color.MistyRose,
                "Poprosił o usunięcie" => Color.Salmon,
                "Błędny rekord (do raportu)" => Color.Orange,
                _ => Color.White
            };
            row.DefaultCellStyle.BackColor = kolorWiersza;
            row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(kolorWiersza.A, Math.Max(0, kolorWiersza.R - 25), Math.Max(0, kolorWiersza.G - 25), Math.Max(0, kolorWiersza.B - 25));
        }

        private void dataGridViewOdbiorcy_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dataGridViewOdbiorcy.IsCurrentCellDirty)
                dataGridViewOdbiorcy.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void dataGridViewOdbiorcy_CellEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                var row = dataGridViewOdbiorcy.Rows[e.RowIndex];
                if (row.IsNewRow) return;
                int idOdbiorcy = Convert.ToInt32(row.Cells["ID"].Value);
                if (aktualnyOdbiorcaID != idOdbiorcy)
                {
                    WczytajNotatki(idOdbiorcy);
                    aktualnyOdbiorcaID = idOdbiorcy;
                }
            }
        }

        private void DataGridViewRanking_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dataGridViewRanking.Columns[e.ColumnIndex].Name == "Suma" && e.Value != null)
            {
                e.CellStyle.Font = new Font(dataGridViewRanking.Font, FontStyle.Bold);
                e.CellStyle.ForeColor = Color.Black;
            }
        }

        private void button1_Click(object sender, EventArgs e) { KonfigurujDataGridView(); WczytajOdbiorcow(); WczytajRankingHandlowcow(); }

        private void button2_Click(object sender, EventArgs e)
        {
            if (dataGridViewOdbiorcy.CurrentRow != null)
            {
                string nazwaFirmy = dataGridViewOdbiorcy.CurrentRow.Cells["Nazwa"].Value?.ToString();
                if (!string.IsNullOrWhiteSpace(nazwaFirmy))
                {
                    string url = $"https://www.google.com/search?q={Uri.EscapeDataString(nazwaFirmy)}";
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
                else MessageBox.Show("Brak nazwy firmy.");
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (dataGridViewOdbiorcy.CurrentRow != null)
            {
                string ulica = dataGridViewOdbiorcy.CurrentRow.Cells["Ulica"].Value?.ToString() ?? "";
                string miasto = dataGridViewOdbiorcy.CurrentRow.Cells["MIASTO"].Value?.ToString() ?? "";
                string kodPocztowy = dataGridViewOdbiorcy.CurrentRow.Cells["KodPocztowy"].Value?.ToString() ?? "";
                string adresOdbiorcy = $"{ulica}, {kodPocztowy} {miasto}";
                string adresStartowy = "Koziołki 40, 95-061 Dmosin";
                if (!string.IsNullOrWhiteSpace(adresOdbiorcy))
                {
                    string url = $"https://www.google.com/maps/dir/{Uri.EscapeDataString(adresStartowy)}/{Uri.EscapeDataString(adresOdbiorcy)}";
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
                else MessageBox.Show("Brak danych adresowych.");
            }
        }

        private void buttonDodajNotatke_Click(object sender, EventArgs e)
        {
            if (aktualnyOdbiorcaID > 0 && !string.IsNullOrWhiteSpace(textBoxNotatka.Text))
            {
                DodajNotatke(aktualnyOdbiorcaID, textBoxNotatka.Text);
                textBoxNotatka.Clear();
            }
            else MessageBox.Show("Wybierz odbiorcę i wpisz treść notatki.");
        }

        private void textBoxSzukaj_TextChanged(object sender, EventArgs e)
        {
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource;
            if (dt == null) return;
            string szukanyTekst = textBoxSzukaj.Text.Trim().Replace("'", "''");
            dt.DefaultView.RowFilter = string.IsNullOrEmpty(szukanyTekst) ? "" : $"Nazwa LIKE '%{szukanyTekst}%'";
        }

        private void comboBoxPowiatFilter_SelectedIndexChanged(object sender, EventArgs e) => ZastosujFiltry(sender, e);
    }

    public class SugestiaKroku
    {
        public string TypZadania { get; set; }
        public string Opis { get; set; }
        public DateTime Termin { get; set; }
        public int Priorytet { get; set; }
    }
}