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

            dataGridViewOdbiorcy.EditMode = DataGridViewEditMode.EditOnEnter;

            // Inicjalizacja filtrów
            comboBoxStatusFilter.Items.Clear();
            comboBoxStatusFilter.Items.Add("Wszystkie statusy");
            comboBoxStatusFilter.Items.AddRange(new object[] {
                "Do zadzwonienia", "Próba kontaktu", "Nawiązano kontakt", "Zgoda na dalszy kontakt",
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

            // Ustawienia splittera
            splitContainerMain.SplitterDistance = (int)(this.ClientSize.Width * 0.75);

            // Dodaj efekty hover dla przycisków
            DodajHoverEffect(button2);
            DodajHoverEffect(button3);
            DodajHoverEffect(buttonDodajNotatke);
        }

        private void DodajHoverEffect(Button btn)
        {
            Color originalColor = btn.BackColor;
            btn.MouseEnter += (s, e) => {
                btn.BackColor = ControlPaint.Light(originalColor, 0.2f);
                btn.Cursor = Cursors.Hand;
            };
            btn.MouseLeave += (s, e) => {
                btn.BackColor = originalColor;
            };
        }

        private void CRM_Load(object sender, EventArgs e)
        {
            operatorID = UserID;

            KonfigurujDataGridView();
            WczytajOdbiorcow();
            WczytajRankingHandlowcow();

            // Dodaj przyciski w odpowiedniej kolejności
            DodajPrzyciskOdswiez();
            DodajPrzyciskMapa();  // ✅ DODAJ TĘ LINIĘ!

            if (operatorID == "11111")
            {
                DodajPrzyciskAdmin();
            }

            DodajPrzyciskZadania();
            DodajPrzyciskDodajOdbiorcę();
        }
        private void DodajPrzyciskOdswiez()
        {
            var btnOdswiez = new Button
            {
                Text = "Odśwież",
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(41, 128, 185),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnOdswiez.FlatAppearance.BorderSize = 0;
            btnOdswiez.Click += (s, e) => {
                dataGridViewOdbiorcy.DataSource = null;
                KonfigurujDataGridView();
                WczytajOdbiorcow();
                WczytajRankingHandlowcow();
            };

            panelSearch.Controls.Add(btnOdswiez);
            btnOdswiez.Location = new Point(panelSearch.Width - 754, 10);
            DodajHoverEffect(btnOdswiez);
        }

        private void DodajPrzyciskAdmin()
        {
            var btnAdmin = new Button
            {
                Text = "Panel Admin",
                Size = new Size(110, 30),
                BackColor = Color.FromArgb(243, 156, 18),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnAdmin.FlatAppearance.BorderSize = 0;
            btnAdmin.Click += (s, e) => {
                var panel = new PanelAdministracyjny(connectionString);
                panel.ShowDialog();
                WczytajOdbiorcow();
            };

            panelSearch.Controls.Add(btnAdmin);
            btnAdmin.Location = new Point(panelSearch.Width - 542, 10); // zmieniona pozycja
            DodajHoverEffect(btnAdmin);
        }

        private void DodajPrzyciskZadania()
        {
            var btnZadania = new Button
            {
                Text = "Zadania",
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnZadania.FlatAppearance.BorderSize = 0;
            btnZadania.Click += (s, e) => {
                var formZadania = new FormZadania(connectionString, operatorID);
                formZadania.ShowDialog();
            };

            panelSearch.Controls.Add(btnZadania);
            btnZadania.Location = new Point(panelSearch.Width - 436, 10); // zmieniona pozycja
            DodajHoverEffect(btnZadania);
        }

        private void DodajPrzyciskDodajOdbiorcę()
        {
            var btnDodaj = new Button
            {
                Text = "+ Dodaj",
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnDodaj.FlatAppearance.BorderSize = 0;
            btnDodaj.Click += (s, e) => {
                var formDodaj = new FormDodajOdbiorce(connectionString, operatorID);
                if (formDodaj.ShowDialog() == DialogResult.OK)
                {
                    WczytajOdbiorcow();
                }
            };

            panelSearch.Controls.Add(btnDodaj);
            btnDodaj.Location = new Point(panelSearch.Width - 330, 10); // zmieniona pozycja
            DodajHoverEffect(btnDodaj);
        }

        private void WczytajOdbiorcow()
        {
            isDataLoading = true;
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("sp_PobierzOdbiorcow", conn);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@OperatorID", operatorID);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    // Zamień "Nowy" na "Do zadzwonienia"
                    foreach (DataRow row in dt.Rows)
                    {
                        if (row["Status"].ToString() == "Nowy")
                            row["Status"] = "Do zadzwonienia";
                    }

                    dataGridViewOdbiorcy.DataSource = dt;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania danych: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isDataLoading = false;
                dataGridViewOdbiorcy.Refresh();
                WypelnijFiltrPowiatow();
                WypelnijFiltrPKD();
                WypelnijFiltrWoj();
            }
        }

        private void WczytajRankingHandlowcow()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("sp_PobierzRankingHandlowcow", conn);
                    cmd.CommandType = CommandType.StoredProcedure;

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    // Zamień nazwę kolumny "Nowy" na "Do zadzwonienia"
                    if (dt.Columns.Contains("Nowy"))
                    {
                        dt.Columns["Nowy"].ColumnName = "Do zadzwonienia";
                    }

                    dataGridViewRanking.DataSource = dt;

                    // Konfiguracja kolumn z odpowiednimi wagami
                    if (dataGridViewRanking.Columns.Count > 0)
                    {
                        // Pierwsza kolumna (Nazwa Handlowca) - większa waga, aby wyświetlić pełne nazwy
                        dataGridViewRanking.Columns[0].FillWeight = 200;
                        dataGridViewRanking.Columns[0].MinimumWidth = 150;

                        // Pozostałe kolumny - równa waga
                        for (int i = 1; i < dataGridViewRanking.Columns.Count; i++)
                        {
                            dataGridViewRanking.Columns[i].FillWeight = 80;
                        }
                    }

                    // Stylizacja pierwszego miejsca (złote tło)
                    if (dataGridViewRanking.Rows.Count > 0)
                    {
                        dataGridViewRanking.Rows[0].DefaultCellStyle.BackColor = Color.FromArgb(255, 243, 205);
                        dataGridViewRanking.Rows[0].DefaultCellStyle.ForeColor = Color.FromArgb(183, 110, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania rankingu: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void KonfigurujDataGridView()
        {
            dataGridViewOdbiorcy.AutoGenerateColumns = false;
            dataGridViewOdbiorcy.Columns.Clear();
            dataGridViewOdbiorcy.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dataGridViewOdbiorcy.RowTemplate.Height = 50;
            dataGridViewOdbiorcy.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewOdbiorcy.AllowUserToResizeColumns = true;
            dataGridViewOdbiorcy.AllowUserToResizeRows = false;

            // Kolumna "Mój klient"
            var colCzyMoj = new DataGridViewTextBoxColumn
            {
                Name = "CzyMoj",
                DataPropertyName = "CzyMoj",
                HeaderText = "*",
                FillWeight = 3,
                MinimumWidth = 30,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 14, FontStyle.Bold),
                    ForeColor = Color.FromArgb(241, 196, 15)
                }
            };
            dataGridViewOdbiorcy.Columns.Add(colCzyMoj);

            // Nazwa
            var colNazwa = new DataGridViewTextBoxColumn
            {
                Name = "Nazwa",
                DataPropertyName = "Nazwa",
                HeaderText = "Nazwa Firmy",
                FillWeight = 25,
                MinimumWidth = 200,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    WrapMode = DataGridViewTriState.True,
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(44, 62, 80)
                }
            };
            dataGridViewOdbiorcy.Columns.Add(colNazwa);

            // Status
            var statusColumn = new DataGridViewComboBoxColumn
            {
                Name = "StatusColumn",
                DataPropertyName = "Status",
                HeaderText = "Status",
                FlatStyle = FlatStyle.Flat,
                FillWeight = 15,
                MinimumWidth = 140
            };
            statusColumn.Items.AddRange("Do zadzwonienia", "Próba kontaktu", "Nawiązano kontakt", "Zgoda na dalszy kontakt",
                "Do wysłania oferta", "Nie zainteresowany", "Poprosił o usunięcie", "Błędny rekord (do raportu)");
            dataGridViewOdbiorcy.Columns.Add(statusColumn);

            // Dane kontaktowe i lokalizacyjne z odpowiednimi wagami
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "KodPocztowy", DataPropertyName = "KodPocztowy", HeaderText = "Kod", FillWeight = 5, MinimumWidth = 60, ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "MIASTO", DataPropertyName = "MIASTO", HeaderText = "Miasto", FillWeight = 8, MinimumWidth = 80, ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ulica", DataPropertyName = "Ulica", HeaderText = "Ulica", FillWeight = 10, MinimumWidth = 100, ReadOnly = true });

            // Telefon - pogrubiony i większa czcionka
            var colTelefon = new DataGridViewTextBoxColumn
            {
                Name = "Telefon_K",
                DataPropertyName = "Telefon_K",
                HeaderText = "Telefon",
                FillWeight = 8,
                MinimumWidth = 90,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(44, 62, 80)
                }
            };
            dataGridViewOdbiorcy.Columns.Add(colTelefon);

            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Wojewodztwo", DataPropertyName = "Wojewodztwo", HeaderText = "Województwo", FillWeight = 9, MinimumWidth = 100, ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Powiat", DataPropertyName = "Powiat", HeaderText = "Powiat", FillWeight = 9, MinimumWidth = 100, ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "PKD_Opis", DataPropertyName = "PKD_Opis", HeaderText = "Branza (PKD)", FillWeight = 15, MinimumWidth = 150, ReadOnly = true });

            // Data ostatniej notatki
            var colData = new DataGridViewTextBoxColumn
            {
                Name = "DataOstatniejNotatki",
                DataPropertyName = "DataOstatniejNotatki",
                HeaderText = "Ostatnia Notatka",
                FillWeight = 10,
                MinimumWidth = 110,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "dd.MM.yyyy HH:mm",
                    ForeColor = Color.FromArgb(127, 140, 141)
                }
            };
            dataGridViewOdbiorcy.Columns.Add(colData);

            dataGridViewOdbiorcy.DefaultCellStyle.Font = new Font("Segoe UI", 9);
            dataGridViewOdbiorcy.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
            dataGridViewOdbiorcy.DefaultCellStyle.SelectionForeColor = Color.White;
            dataGridViewOdbiorcy.DefaultCellStyle.Padding = new Padding(5, 5, 5, 5);
            dataGridViewOdbiorcy.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 252);
        }

        private void dataGridViewOdbiorcy_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (!isDataLoading && e.RowIndex >= 0 && dataGridViewOdbiorcy.Columns[e.ColumnIndex].Name == "StatusColumn")
            {
                // Pobierz ID z DataSource zamiast z kolumny (bo kolumna ID nie istnieje)
                DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource;
                if (dt == null) return;

                DataRowView rowView = dataGridViewOdbiorcy.Rows[e.RowIndex].DataBoundItem as DataRowView;
                if (rowView == null) return;

                int idOdbiorcy = Convert.ToInt32(rowView["ID"]);
                string nowyStatus = dataGridViewOdbiorcy.Rows[e.RowIndex].Cells["StatusColumn"].Value?.ToString() ?? "";

                // Konwersja "Do zadzwonienia" -> "Nowy" dla bazy danych
                string statusDlaBazy = nowyStatus == "Do zadzwonienia" ? "Nowy" : nowyStatus;
                string staryStatus = PobierzAktualnyStatus(idOdbiorcy);

                AktualizujStatusWBazie(idOdbiorcy, statusDlaBazy, staryStatus);
                dataGridViewOdbiorcy.InvalidateRow(e.RowIndex);

                var sugestia = ZaproponujNastepnyKrok(nowyStatus);
                if (sugestia != null)
                {
                    var result = MessageBox.Show(
                        $"Sugestia nastepnego kroku:\n\n{sugestia.Opis}\n\nCzy zaplanowac zadanie?",
                        "Nastepny krok",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question
                    );

                    if (result == DialogResult.Yes)
                        UtworzZadanie(idOdbiorcy, sugestia);
                }
            }
        }

        private string PobierzAktualnyStatus(int idOdbiorcy)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("SELECT Status FROM OdbiorcyCRM WHERE ID = @id", conn);
                    cmd.Parameters.AddWithValue("@id", idOdbiorcy);
                    return cmd.ExecuteScalar()?.ToString() ?? "Nowy";
                }
            }
            catch
            {
                return "Nowy";
            }
        }

        private void AktualizujStatusWBazie(int idOdbiorcy, string nowyStatus, string staryStatus)
        {
            try
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
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd aktualizacji statusu: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private SugestiaKroku ZaproponujNastepnyKrok(string status) => status switch
        {
            "Próba kontaktu" => new SugestiaKroku { TypZadania = "Telefon", Opis = "Proba ponownego kontaktu telefonicznego", Termin = DateTime.Now.AddDays(2), Priorytet = 2 },
            "Nawiązano kontakt" => new SugestiaKroku { TypZadania = "Email", Opis = "Wyslac prezentacje firmy i oferty", Termin = DateTime.Now.AddDays(1), Priorytet = 3 },
            "Zgoda na dalszy kontakt" => new SugestiaKroku { TypZadania = "Oferta", Opis = "Przygotowac spersonalizowana oferte", Termin = DateTime.Now.AddHours(4), Priorytet = 3 },
            _ => null
        };

        private void UtworzZadanie(int idOdbiorcy, SugestiaKroku sugestia)
        {
            try
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

                    MessageBox.Show("Zadanie zostalo utworzone pomyslnie!", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd tworzenia zadania: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void WczytajNotatki(int idOdbiorcy)
        {
            try
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

                    // Konfiguracja kolumn aby wypełniły całą szerokość
                    if (dataGridViewNotatki.Columns.Count > 0)
                    {
                        dataGridViewNotatki.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

                        dataGridViewNotatki.Columns[0].HeaderText = "Tresc";
                        dataGridViewNotatki.Columns[0].FillWeight = 70;
                        dataGridViewNotatki.Columns[0].DefaultCellStyle.WrapMode = DataGridViewTriState.True;

                        if (dataGridViewNotatki.Columns.Count > 1)
                        {
                            dataGridViewNotatki.Columns[1].HeaderText = "Data";
                            dataGridViewNotatki.Columns[1].FillWeight = 30;
                            dataGridViewNotatki.Columns[1].DefaultCellStyle.Format = "dd.MM HH:mm";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania notatek: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DodajNotatke(int idOdbiorcy, string tresc)
        {
            try
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

                            MessageBox.Show("Notatka zostala dodana!", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
                WczytajNotatki(idOdbiorcy);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd dodawania notatki: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void WypelnijFiltrPowiatow()
        {
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource;
            if (dt == null) return;

            var powiaty = dt.AsEnumerable()
                .Select(row => row.Field<string>("Powiat"))
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .OrderBy(p => p)
                .ToArray();

            comboBoxPowiatFilter.Items.Clear();
            comboBoxPowiatFilter.Items.Add("Wszystkie powiaty");
            comboBoxPowiatFilter.Items.AddRange(powiaty);
            comboBoxPowiatFilter.SelectedIndex = 0;
        }

        private void WypelnijFiltrPKD()
        {
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource;
            if (dt == null) return;

            var grupy = dt.AsEnumerable()
                .Where(row => !string.IsNullOrWhiteSpace(row.Field<string>("PKD_Opis")))
                .GroupBy(row => row.Field<string>("PKD_Opis"))
                .Select(g => g.Key)
                .OrderBy(x => x)
                .ToList();

            comboBoxPKD.Items.Clear();
            comboBoxPKD.Items.Add("Wszystkie Rodzaje");
            foreach (var pkd in grupy)
                comboBoxPKD.Items.Add(pkd);
            comboBoxPKD.SelectedIndex = 0;
        }

        private void WypelnijFiltrWoj()
        {
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource;
            if (dt == null) return;

            var woj = dt.AsEnumerable()
                .Select(row => row.Field<string>("Wojewodztwo"))
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .OrderBy(p => p)
                .ToArray();

            comboBoxWoj.Items.Clear();
            comboBoxWoj.Items.Add("Wszystkie Woj.");
            comboBoxWoj.Items.AddRange(woj);
            comboBoxWoj.SelectedIndex = 0;
        }

        private void comboBoxStatusFilter_SelectedIndexChanged(object sender, EventArgs e) => ZastosujFiltry(sender, e);
        private void comboBoxPowiatFilter_SelectedIndexChanged(object sender, EventArgs e) => ZastosujFiltry(sender, e);

        private void ZastosujFiltry(object sender, EventArgs e)
        {
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource;
            if (dt == null) return;

            var filtry = new System.Collections.Generic.List<string>();

            if (comboBoxStatusFilter.SelectedIndex > 0)
                filtry.Add($"Status = '{comboBoxStatusFilter.SelectedItem}'");

            if (comboBoxPowiatFilter.SelectedIndex > 0)
                filtry.Add($"Powiat = '{comboBoxPowiatFilter.SelectedItem}'");

            if (comboBoxPKD.SelectedIndex > 0)
                filtry.Add($"PKD_Opis = '{comboBoxPKD.SelectedItem}'");

            if (comboBoxWoj.SelectedIndex > 0)
                filtry.Add($"Wojewodztwo = '{comboBoxWoj.SelectedItem}'");

            dt.DefaultView.RowFilter = string.Join(" AND ", filtry);
        }

        private void dataGridViewOdbiorcy_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dataGridViewOdbiorcy.Rows.Count) return;

            DataGridViewRow row = dataGridViewOdbiorcy.Rows[e.RowIndex];
            string status = row.Cells["StatusColumn"].Value?.ToString() ?? "Do zadzwonienia";

            Color kolorWiersza = status switch
            {
                "Do zadzwonienia" => Color.FromArgb(236, 240, 241),
                "Próba kontaktu" => Color.FromArgb(174, 214, 241),
                "Nawiązano kontakt" => Color.FromArgb(133, 193, 233),
                "Zgoda na dalszy kontakt" => Color.FromArgb(169, 223, 191),
                "Do wysłania oferta" => Color.FromArgb(250, 219, 216),
                "Nie zainteresowany" => Color.FromArgb(245, 183, 177),
                "Poprosił o usunięcie" => Color.FromArgb(241, 148, 138),
                "Błędny rekord (do raportu)" => Color.FromArgb(248, 196, 113),
                _ => Color.White
            };

            row.DefaultCellStyle.BackColor = kolorWiersza;
            row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(
                kolorWiersza.A,
                Math.Max(0, kolorWiersza.R - 50),
                Math.Max(0, kolorWiersza.G - 50),
                Math.Max(0, kolorWiersza.B - 50)
            );
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

                // Pobierz ID z DataSource zamiast z kolumny
                DataRowView rowView = row.DataBoundItem as DataRowView;
                if (rowView == null) return;

                // Sprawdź czy wartość nie jest null
                if (rowView["ID"] == null || rowView["ID"] == DBNull.Value)
                    return;

                int idOdbiorcy = Convert.ToInt32(rowView["ID"]);
                if (aktualnyOdbiorcaID != idOdbiorcy)
                {
                    WczytajNotatki(idOdbiorcy);
                    aktualnyOdbiorcaID = idOdbiorcy;
                }
            }
        }

        private void DataGridViewRanking_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            // Pogrubienie TYLKO dla kolumny "Nazwa Handlowca" (pierwsza kolumna) i "Suma"
            if (e.ColumnIndex == 0) // Pierwsza kolumna - Nazwa Handlowca
            {
                e.CellStyle.Font = new Font(dataGridViewRanking.Font, FontStyle.Bold);
            }
            else if (dataGridViewRanking.Columns[e.ColumnIndex].Name == "Suma" ||
                     dataGridViewRanking.Columns[e.ColumnIndex].HeaderText == "Suma")
            {
                e.CellStyle.Font = new Font(dataGridViewRanking.Font, FontStyle.Bold);
                e.CellStyle.ForeColor = Color.FromArgb(0, 100, 0);
            }
            else
            {
                // Wszystkie inne kolumny - normalna czcionka (bez pogrubienia)
                e.CellStyle.Font = new Font(dataGridViewRanking.Font, FontStyle.Regular);
            }

            // Kolumna "Do zadzwonienia" na szaro
            if (dataGridViewRanking.Columns[e.ColumnIndex].HeaderText == "Do zadzwonienia")
            {
                e.CellStyle.ForeColor = Color.FromArgb(149, 165, 166);
            }
        }

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
                else
                    MessageBox.Show("Brak nazwy firmy do wyszukania.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                else
                    MessageBox.Show("Brak danych adresowych.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        private void DodajPrzyciskMapa()
        {
            var btnMapa = new Button
            {
                Text = "🗺 Mapa",
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(155, 89, 182),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnMapa.FlatAppearance.BorderSize = 0;
            btnMapa.Click += (s, e) => {
                var formMapa = new FormMapaWojewodztwa(connectionString, operatorID);
                formMapa.ShowDialog();
            };

            panelSearch.Controls.Add(btnMapa);
            btnMapa.Location = new Point(panelSearch.Width - 648, 10);
            DodajHoverEffect(btnMapa);
        }
        private void buttonDodajNotatke_Click(object sender, EventArgs e)
        {
            if (aktualnyOdbiorcaID > 0 && !string.IsNullOrWhiteSpace(textBoxNotatka.Text))
            {
                DodajNotatke(aktualnyOdbiorcaID, textBoxNotatka.Text);
                textBoxNotatka.Clear();
            }
            else
                MessageBox.Show("Wybierz odbiorce i wpisz tresc notatki.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void textBoxSzukaj_TextChanged(object sender, EventArgs e)
        {
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource;
            if (dt == null) return;

            string szukanyTekst = textBoxSzukaj.Text.Trim().Replace("'", "''");
            dt.DefaultView.RowFilter = string.IsNullOrEmpty(szukanyTekst) ? "" : $"Nazwa LIKE '%{szukanyTekst}%'";
        }
    }

    public class SugestiaKroku
    {
        public string TypZadania { get; set; }
        public string Opis { get; set; }
        public DateTime Termin { get; set; }
        public int Priorytet { get; set; }
    }
}