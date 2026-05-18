using Microsoft.Data.SqlClient;
using System;
using System.Windows;
using System.Windows.Media;

namespace Kalendarz1.Admin
{
    /// <summary>
    /// Dialog zarządzania kontem użytkownika dla admina:
    /// - Reset hasła (PasswordHash = NULL → user ustawi przy następnym logowaniu)
    /// - Odblokuj konto (FailedAttempts=0, LockedUntil=NULL)
    /// - Zmiana flagi IsAdmin
    /// - Podgląd statusu (last login, failed attempts, locked until)
    /// </summary>
    public partial class AccountManagementDialog : Window
    {
        private const string ConnectionString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private readonly string _userId;
        private readonly string _userName;
        private bool _originalIsAdmin;

        public AccountManagementDialog(string userId, string userName)
        {
            InitializeComponent();
            _userId = userId;
            _userName = userName ?? userId;
            UserHeaderText.Text = $"Użytkownik: {_userName} (ID: {_userId})";

            LoadAccountStatus();
        }

        private void LoadAccountStatus()
        {
            try
            {
                using var conn = new SqlConnection(ConnectionString);
                conn.Open();
                using var cmd = new SqlCommand(@"
                    SELECT PasswordHash, PasswordSetAt, IsAdmin, FailedAttempts, LockedUntil, LastSuccessfulLogin
                    FROM dbo.operators
                    WHERE ID = @id", conn);
                cmd.Parameters.AddWithValue("@id", _userId);
                using var r = cmd.ExecuteReader();
                if (!r.Read())
                {
                    ShowError("Nie znaleziono użytkownika w bazie.");
                    return;
                }

                var hash = r.IsDBNull(0) ? null : r.GetString(0);
                var setAt = r.IsDBNull(1) ? (DateTime?)null : r.GetDateTime(1);
                _originalIsAdmin = !r.IsDBNull(2) && r.GetBoolean(2);
                var failed = r.IsDBNull(3) ? 0 : r.GetInt32(3);
                var locked = r.IsDBNull(4) ? (DateTime?)null : r.GetDateTime(4);
                var lastLogin = r.IsDBNull(5) ? (DateTime?)null : r.GetDateTime(5);

                if (string.IsNullOrEmpty(hash))
                {
                    PwdStatusText.Text = "⚠ NIE USTAWIONE (user ustawi przy pierwszym logowaniu)";
                    PwdStatusText.Foreground = new SolidColorBrush(Color.FromRgb(217, 119, 6));
                }
                else
                {
                    var setInfo = setAt.HasValue
                        ? $"  (ustawione: {setAt:yyyy-MM-dd HH:mm})"
                        : "";
                    PwdStatusText.Text = $"✅ Aktywne{setInfo}";
                    PwdStatusText.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
                }

                LastLoginText.Text = lastLogin?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(nigdy się nie logował)";
                FailedText.Text = failed.ToString();
                if (failed > 0)
                    FailedText.Foreground = new SolidColorBrush(Color.FromRgb(217, 119, 6));

                if (locked.HasValue && locked > DateTime.Now)
                {
                    var minutesLeft = (int)Math.Ceiling((locked.Value - DateTime.Now).TotalMinutes);
                    LockedText.Text = $"🔒 ZABLOKOWANE do {locked:HH:mm} ({minutesLeft} min)";
                    LockedText.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                }
                else
                {
                    LockedText.Text = "✅ Odblokowane";
                    LockedText.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
                }

                IsAdminCheck.IsChecked = _originalIsAdmin;
            }
            catch (Exception ex)
            {
                ShowError($"Błąd połączenia z bazą: {ex.Message}");
            }
        }

        private void ResetPwd_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                $"Czy na pewno zresetować hasło użytkownika {_userName}?\n\n" +
                $"Po resetcie user przy następnym logowaniu ustawi nowe hasło.",
                "Reset hasła",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                using var conn = new SqlConnection(ConnectionString);
                conn.Open();
                using var cmd = new SqlCommand(@"
                    UPDATE dbo.operators
                    SET PasswordHash = NULL,
                        PasswordSetAt = NULL,
                        FailedAttempts = 0,
                        LockedUntil = NULL
                    WHERE ID = @id", conn);
                cmd.Parameters.AddWithValue("@id", _userId);
                cmd.ExecuteNonQuery();

                ShowSuccess($"Hasło zresetowane. {_userName} ustawi nowe przy następnym logowaniu.");
                LoadAccountStatus();
            }
            catch (Exception ex)
            {
                ShowError($"Błąd: {ex.Message}");
            }
        }

        private void Unlock_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var conn = new SqlConnection(ConnectionString);
                conn.Open();
                using var cmd = new SqlCommand(@"
                    UPDATE dbo.operators
                    SET FailedAttempts = 0,
                        LockedUntil = NULL
                    WHERE ID = @id", conn);
                cmd.Parameters.AddWithValue("@id", _userId);
                cmd.ExecuteNonQuery();

                ShowSuccess("Konto odblokowane. Licznik nieudanych prób wyzerowany.");
                LoadAccountStatus();
            }
            catch (Exception ex)
            {
                ShowError($"Błąd: {ex.Message}");
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var newIsAdmin = IsAdminCheck.IsChecked == true;
            if (newIsAdmin == _originalIsAdmin)
            {
                ShowInfo("Nic się nie zmieniło — flaga Admin pozostaje bez zmian.");
                return;
            }

            try
            {
                using var conn = new SqlConnection(ConnectionString);
                conn.Open();
                using var cmd = new SqlCommand(
                    "UPDATE dbo.operators SET IsAdmin = @v WHERE ID = @id", conn);
                cmd.Parameters.AddWithValue("@v", newIsAdmin);
                cmd.Parameters.AddWithValue("@id", _userId);
                cmd.ExecuteNonQuery();

                _originalIsAdmin = newIsAdmin;
                ShowSuccess(newIsAdmin
                    ? $"{_userName} ma teraz uprawnienia administratora."
                    : $"{_userName} nie jest już administratorem.");
            }
            catch (Exception ex)
            {
                ShowError($"Błąd: {ex.Message}");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ShowError(string msg)
        {
            StatusText.Text = "❌ " + msg;
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
        }
        private void ShowSuccess(string msg)
        {
            StatusText.Text = "✅ " + msg;
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
        }
        private void ShowInfo(string msg)
        {
            StatusText.Text = "ℹ " + msg;
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
        }
    }
}
