using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.Services;

namespace Kalendarz1.WPF
{
    /// <summary>
    /// Dialog do wpisywania (Verify) lub ustawiania nowego hasła (Set).
    /// Po DialogResult=true właściwość Password zawiera wpisane hasło.
    /// </summary>
    public partial class PasswordPromptWindow : Window
    {
        public enum Mode { Verify, Set }

        private readonly Mode _mode;
        public string Password { get; private set; } = "";

        /// <summary>
        /// Tworzy dialog hasła.
        /// </summary>
        /// <param name="mode">Verify = wpisz istniejące hasło. Set = ustaw nowe (pierwsze logowanie).</param>
        /// <param name="userId">Login użytkownika (do wyświetlenia w tytule)</param>
        /// <param name="userFullName">Imię/nazwisko (do wyświetlenia)</param>
        public PasswordPromptWindow(Mode mode, string userId, string? userFullName = null)
        {
            InitializeComponent();
            _mode = mode;

            var greeting = string.IsNullOrEmpty(userFullName) ? userId : userFullName;

            if (mode == Mode.Set)
            {
                TitleText.Text = "Ustaw nowe hasło";
                DescriptionText.Text =
                    $"Witaj {greeting}.\n" +
                    $"To Twoje pierwsze logowanie — ustaw hasło, które będziesz wpisywać przy każdym następnym logowaniu.\n\n" +
                    $"Minimum 4 znaki. Dla bezpieczeństwa zalecamy minimum 8 znaków z cyfrą i literą — ale nie jest to wymagane.";
                ConfirmPanel.Visibility = Visibility.Visible;
                OkButton.Content = "Ustaw hasło";
            }
            else
            {
                TitleText.Text = "Wpisz hasło";
                DescriptionText.Text = $"Witaj {greeting}. Podaj swoje hasło.";
                ConfirmPanel.Visibility = Visibility.Collapsed;
                OkButton.Content = "Zaloguj";
            }

            Loaded += (s, e) => PwdBox.Focus();

            // Live-update wskazówki siły hasła (tylko w trybie Set)
            if (mode == Mode.Set)
            {
                PwdBox.PasswordChanged += (s, e) => UpdateStrengthHint();
            }
        }

        private void UpdateStrengthHint()
        {
            var pwd = PwdBox.Password;
            if (string.IsNullOrEmpty(pwd))
            {
                StrengthHint.Text = "💡 Wskazówka: silne hasło ma minimum 8 znaków + cyfrę + literę";
                StrengthHint.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)); // szary
            }
            else if (PasswordHasher.IsStrong(pwd))
            {
                StrengthHint.Text = "✅ Silne hasło — dobrze!";
                StrengthHint.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74)); // zielony
            }
            else if (pwd.Length < 4)
            {
                StrengthHint.Text = "⚠ Hasło jest bardzo krótkie (min. 4 znaki)";
                StrengthHint.Foreground = new SolidColorBrush(Color.FromRgb(217, 119, 6)); // pomarańcz
            }
            else
            {
                StrengthHint.Text = "ℹ Hasło akceptowane, ale słabe. Lepsze: 8+ znaków + cyfra + litera";
                StrengthHint.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)); // szary
            }
        }

        private void Field_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Ok_Click(sender, e);
                e.Handled = true;
            }
        }

        private void Ok_Click(object? sender, RoutedEventArgs e)
        {
            var pwd = PwdBox.Password;

            if (_mode == Mode.Set)
            {
                var (ok, error) = PasswordHasher.Validate(pwd);
                if (!ok)
                {
                    StatusText.Text = error;
                    return;
                }
                if (pwd != PwdConfirmBox.Password)
                {
                    StatusText.Text = "Hasła nie są takie same. Wpisz ponownie.";
                    return;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(pwd))
                {
                    StatusText.Text = "Wpisz hasło.";
                    return;
                }
            }

            Password = pwd;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
