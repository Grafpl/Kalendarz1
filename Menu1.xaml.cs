using System;
using System.Windows;
using Microsoft.Data.SqlClient;
using Kalendarz1;
using System.Windows.Input;


namespace Kalendarz1
{
    public partial class Menu1 : Window
    {
        private string connectionPermission = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        public Menu1()
        {
            InitializeComponent();

        }
        private void UsernameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LoginButton_Click(sender, null);
            }
        }
        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            NazwaZiD databaseManager = new NazwaZiD();
            string username = UsernameTextBox.Text;

            // Ensure that the username field is not empty
            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show("Please enter a username.", "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Check the username in the database
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
                        // Set the user ID
                        App.UserID = username;

                        // Utwórz i wyświetl nowe okno menu
                        MENU menuWindow = new MENU();
                        menuWindow.Show();
                        string name = databaseManager.GetNameById(username);
                     
                        // Close the current Menu1 window
                        this.Hide(); // Ukryj, zamiast zamykać
                    }
                    else
                    {
                        MessageBox.Show("Błędny login.", "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Close the login window without setting DialogResult
            Close();
        }
    }
}
