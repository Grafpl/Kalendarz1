using System;
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
            WindowIconHelper.SetIcon(this);
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
                string query = "SELECT Name FROM operators WHERE ID = @username";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@username", username);
                    var result = command.ExecuteScalar();

                    if (result != null)
                    {
                        string userName = result.ToString();

                        // Ustawienie ID użytkownika
                        App.UserID = username;

                        // Ukryj formularz logowania
                        this.Hide();

                        // Pokaż ekran powitalny z avatarem
                        try
                        {
                            WelcomeScreen.ShowAndWait(username, userName);
                        }
                        catch (Exception ex)
                        {
                            // Jeśli ekran powitalny nie zadziała, kontynuuj logowanie
                            System.Diagnostics.Debug.WriteLine($"WelcomeScreen error: {ex.Message}");
                        }

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
