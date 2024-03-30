﻿using System;
using Microsoft.Data.SqlClient;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class LoginForm : Form
    {
        private string connectionPermission = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public LoginForm()
        {
            InitializeComponent();
        }

        // Właściwość publiczna zwracająca tekst z UsernameTextBox
        public string Username
        {
            get { return UsernameTextBox.Text; }
        }

        private void LoginButton_Click(object sender, EventArgs e)
        {
            string username = UsernameTextBox.Text;

            // Upewnij się, że pole nazwy użytkownika nie jest puste
            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show("Wprowadź nazwę użytkownika.", "Błąd logowania", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Sprawdź nazwę użytkownika w bazie danych
            using (SqlConnection connection = new SqlConnection(connectionPermission))
            {
                connection.Open();
                string query = "SELECT COUNT(*) FROM operators WHERE ID = @username";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@username", username);
                    int count = Convert.ToInt32(command.ExecuteScalar());
                    if (count > 0)
                    {
                        // Ustawienie ID użytkownika
                        App.UserID = username;

                        // Ustaw wynik formularza na OK i zamknij formularz logowania
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show("Nieprawidłowa nazwa użytkownika.", "Błąd logowania", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            // Zamyka formularz logowania bez ustawiania DialogResult
            Close();
        }

        private void LoginForm_Load(object sender, EventArgs e)
        {

        }
    }
}
