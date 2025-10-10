using Microsoft.Data.SqlClient;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
// USUŃ: using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class Menu1 : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public Menu1()
        {
            InitializeComponent();
            ApplyModernStyle();

            // Ustaw fokus na pole hasła po załadowaniu
            this.Loaded += (s, e) => PasswordBox.Focus();
        }

        private void ApplyModernStyle()
        {
            // Nowoczesny styl dla okna logowania
            this.Background = new LinearGradientBrush(
                Color.FromRgb(41, 53, 65),
                Color.FromRgb(31, 43, 55),
                90);
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LoginButton_Click(sender, null);
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = PasswordBox.Password;

            // Sprawdź czy pole nie jest puste
            if (string.IsNullOrWhiteSpace(username))
            {
                ShowError("Wprowadź ID użytkownika");
                return;
            }

            // Sprawdź użytkownika w bazie
            if (ValidateUser(username))
            {
                // Ustaw ID użytkownika
                App.UserID = username;

                try
                {
                    // WAŻNE: Najpierw ukryj okno logowania
                    this.Hide();

                    // Utwórz okno MENU
                    MENU menuWindow = new MENU();

                    // Obsłuż zamknięcie menu - wtedy zamknij aplikację
                    menuWindow.FormClosed += (s, args) =>
                    {
                        Application.Current.Shutdown();
                    };

                    // Pokaż menu
                    menuWindow.Show();
                }
                catch (Exception ex)
                {
                    this.Show(); // Pokaż z powrotem okno logowania jeśli błąd
                    MessageBox.Show($"Błąd podczas otwierania menu:\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                        "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                ShowError("Nieprawidłowy ID użytkownika");
                PasswordBox.Clear();
                PasswordBox.Focus();
            }
        }
        private bool ValidateUser(string userId)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT COUNT(*) FROM operators WHERE ID = @username";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@username", userId);
                        int count = Convert.ToInt32(command.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"Błąd połączenia z bazą danych:\n{ex.Message}");
                return false;
            }
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Błąd logowania",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Potwierdź zamknięcie
            var result = MessageBox.Show(
                "Czy na pewno chcesz zamknąć aplikację?",
                "Potwierdzenie",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Umożliwia przesuwanie okna
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
    }
}