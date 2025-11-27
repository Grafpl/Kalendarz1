using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    /// <summary>
    /// Dialog do edycji danych kontaktowych pracownika (email, telefon, stanowisko)
    /// </summary>
    public class EditOperatorContactDialog : Form
    {
        private string connectionString;
        private string operatorId;
        private string operatorName;

        private TextBox txtEmail;
        private TextBox txtTelefon;
        private TextBox txtStanowisko;
        private Button btnZapisz;
        private Button btnAnuluj;

        public EditOperatorContactDialog(string connString, string opId, string opName)
        {
            connectionString = connString;
            operatorId = opId;
            operatorName = opName;
            InitializeComponents();
            LoadCurrentData();
        }

        private void InitializeComponents()
        {
            this.Text = "Edycja danych kontaktowych";
            this.Size = new Size(500, 350);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = ColorTranslator.FromHtml("#ECF0F1");

            // Tytuł
            var titleLabel = new Label
            {
                Text = "📞 Dane kontaktowe pracownika",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#2C3E50"),
                Location = new Point(30, 20),
                AutoSize = true
            };
            this.Controls.Add(titleLabel);

            // Nazwa pracownika
            var nameLabel = new Label
            {
                Text = $"Pracownik: {operatorName} (ID: {operatorId})",
                Font = new Font("Segoe UI", 11),
                ForeColor = ColorTranslator.FromHtml("#7F8C8D"),
                Location = new Point(30, 55),
                AutoSize = true
            };
            this.Controls.Add(nameLabel);

            // Email
            var emailLabel = new Label
            {
                Text = "Email:",
                Font = new Font("Segoe UI", 10),
                Location = new Point(30, 100),
                AutoSize = true
            };
            this.Controls.Add(emailLabel);

            txtEmail = new TextBox
            {
                Location = new Point(140, 97),
                Size = new Size(310, 28),
                Font = new Font("Segoe UI", 11),
                PlaceholderText = "np. jan.kowalski@piorkowscy.com.pl"
            };
            this.Controls.Add(txtEmail);

            // Telefon
            var telefonLabel = new Label
            {
                Text = "Telefon:",
                Font = new Font("Segoe UI", 10),
                Location = new Point(30, 145),
                AutoSize = true
            };
            this.Controls.Add(telefonLabel);

            txtTelefon = new TextBox
            {
                Location = new Point(140, 142),
                Size = new Size(310, 28),
                Font = new Font("Segoe UI", 11),
                PlaceholderText = "np. +48 123 456 789"
            };
            this.Controls.Add(txtTelefon);

            // Stanowisko
            var stanowiskoLabel = new Label
            {
                Text = "Stanowisko:",
                Font = new Font("Segoe UI", 10),
                Location = new Point(30, 190),
                AutoSize = true
            };
            this.Controls.Add(stanowiskoLabel);

            txtStanowisko = new TextBox
            {
                Location = new Point(140, 187),
                Size = new Size(310, 28),
                Font = new Font("Segoe UI", 11),
                PlaceholderText = "np. Handlowiec, Kierownik sprzedaży"
            };
            this.Controls.Add(txtStanowisko);

            // Przyciski
            btnZapisz = new Button
            {
                Text = "💾 Zapisz",
                Location = new Point(140, 250),
                Size = new Size(130, 45),
                BackColor = ColorTranslator.FromHtml("#27AE60"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnZapisz.FlatAppearance.BorderSize = 0;
            btnZapisz.Click += BtnZapisz_Click;
            this.Controls.Add(btnZapisz);

            btnAnuluj = new Button
            {
                Text = "Anuluj",
                Location = new Point(280, 250),
                Size = new Size(130, 45),
                BackColor = ColorTranslator.FromHtml("#7F8C8D"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnAnuluj.FlatAppearance.BorderSize = 0;
            this.Controls.Add(btnAnuluj);
        }

        private void LoadCurrentData()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"SELECT Email, Telefon, Stanowisko 
                                     FROM OperatorzyKontakt 
                                     WHERE OperatorID = @operatorId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@operatorId", operatorId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                txtEmail.Text = reader["Email"]?.ToString() ?? "";
                                txtTelefon.Text = reader["Telefon"]?.ToString() ?? "";
                                txtStanowisko.Text = reader["Stanowisko"]?.ToString() ?? "";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Tabela może nie istnieć - to OK, użytkownik musi najpierw uruchomić skrypt SQL
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania danych kontaktowych: {ex.Message}");
            }
        }

        private void BtnZapisz_Click(object sender, EventArgs e)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Sprawdź czy rekord istnieje
                    string checkQuery = "SELECT COUNT(*) FROM OperatorzyKontakt WHERE OperatorID = @operatorId";
                    bool exists = false;

                    using (var checkCmd = new SqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@operatorId", operatorId);
                        exists = (int)checkCmd.ExecuteScalar() > 0;
                    }

                    string query;
                    if (exists)
                    {
                        query = @"UPDATE OperatorzyKontakt 
                                  SET Email = @email, 
                                      Telefon = @telefon, 
                                      Stanowisko = @stanowisko,
                                      DataModyfikacji = GETDATE()
                                  WHERE OperatorID = @operatorId";
                    }
                    else
                    {
                        query = @"INSERT INTO OperatorzyKontakt (OperatorID, Email, Telefon, Stanowisko)
                                  VALUES (@operatorId, @email, @telefon, @stanowisko)";
                    }

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@operatorId", operatorId);
                        cmd.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(txtEmail.Text) ? DBNull.Value : txtEmail.Text.Trim());
                        cmd.Parameters.AddWithValue("@telefon", string.IsNullOrWhiteSpace(txtTelefon.Text) ? DBNull.Value : txtTelefon.Text.Trim());
                        cmd.Parameters.AddWithValue("@stanowisko", string.IsNullOrWhiteSpace(txtStanowisko.Text) ? DBNull.Value : txtStanowisko.Text.Trim());

                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("✓ Dane kontaktowe zostały zapisane.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (SqlException ex) when (ex.Message.Contains("Invalid object name"))
            {
                MessageBox.Show(
                    "Tabela 'OperatorzyKontakt' nie istnieje w bazie danych.\n\n" +
                    "Uruchom najpierw skrypt SQL_OperatorzyKontakt.sql aby utworzyć tabelę.",
                    "Brak tabeli",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania:\n{ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}