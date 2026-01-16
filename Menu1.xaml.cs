using Microsoft.Data.SqlClient;
using System;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace Kalendarz1
{
    public partial class Menu1 : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public Menu1()
        {
            InitializeComponent();
            this.Loaded += (s, e) => PasswordBox.Focus();
            SetFooterText();
        }

        private void SetFooterText()
        {
            // Automatycznie pobiera wersję i rok
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            string versionString = $"{version.Major}.{version.Minor}";
            FooterText.Text = $"© {DateTime.Now.Year} Piórkowscy | Wersja {versionString}";
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

            if (string.IsNullOrWhiteSpace(username))
            {
                ShowMessage("Proszę wprowadzić identyfikator.", isError: true);
                return;
            }

            if (ValidateUser(username))
            {
                App.UserID = username;
                App.UserFullName = GetUserFullName(username) ?? username;

                // Uruchom serwis powiadomień o spotkaniach
                App.StartNotyfikacjeService();

                try
                {
                    this.Hide();

                    // Pokaż ekran powitalny z avatarem (2 sekundy)
                    try
                    {
                        WelcomeScreen.ShowAndWait(username, App.UserFullName);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"WelcomeScreen error: {ex.Message}");
                    }

                    MENU menuWindow = new MENU();
                    menuWindow.FormClosed += (s, args) =>
                    {
                        App.StopNotyfikacjeService(); // Zatrzymaj serwis przy zamknięciu
                        Application.Current.Shutdown();
                    };
                    menuWindow.Show();
                }
                catch (Exception ex)
                {
                    this.Show();
                    ShowMessage($"Krytyczny błąd podczas ładowania menu:\n{ex.Message}", isError: true);
                }
            }
            else
            {
                ShowMessage("Nieprawidłowy identyfikator. Spróbuj ponownie.", isError: true);
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
                    string query = "SELECT COUNT(1) FROM operators WHERE ID = @username";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@username", userId);
                        return (int)command.ExecuteScalar() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Błąd połączenia z bazą danych:\n{ex.Message}", isError: true);
                return false;
            }
        }

        // Użyj bardziej estetycznego okna komunikatu zamiast standardowego MessageBox
        private void ShowMessage(string message, bool isError = false)
        {
            MessageBox.Show(message, isError ? "Błąd Logowania" : "Informacja",
               MessageBoxButton.OK, isError ? MessageBoxImage.Error : MessageBoxImage.Information);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private string GetUserFullName(string userId)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    // Próba pobrania pełnej nazwy użytkownika z tabeli operators
                    string query = "SELECT TOP 1 ISNULL(Name, ID) FROM operators WHERE ID = @username";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@username", userId);
                        var result = command.ExecuteScalar();
                        return result?.ToString() ?? userId;
                    }
                }
            }
            catch
            {
                return userId;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        #region Klawiatura dotykowa

        private void TouchKey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button)
            {
                string digit = button.Content.ToString();
                // Dodajemy cyfrę do PasswordBox poprzez manipulację SecureString
                // Używamy prostszego podejścia - ustawiamy nową wartość
                string currentPassword = PasswordBox.Password;
                PasswordBox.Password = currentPassword + digit;
                PasswordBox.Focus();
            }
        }

        private void TouchKey_Backspace(object sender, RoutedEventArgs e)
        {
            string currentPassword = PasswordBox.Password;
            if (currentPassword.Length > 0)
            {
                PasswordBox.Password = currentPassword.Substring(0, currentPassword.Length - 1);
            }
            PasswordBox.Focus();
        }

        private void TouchKey_Enter(object sender, RoutedEventArgs e)
        {
            LoginButton_Click(sender, e);
        }

        private void TouchKey_Clear(object sender, RoutedEventArgs e)
        {
            PasswordBox.Clear();
            PasswordBox.Focus();
        }

        #endregion
    }
}