using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Kalendarz1
{
    public partial class Menu1 : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private DispatcherTimer clockTimer;
        private DispatcherTimer serverCheckTimer;
        private bool isServerOnline = false;

        // Lista cytatów motywacyjnych
        private readonly List<(string quote, string author)> motivationalQuotes = new List<(string, string)>
        {
            ("Sukces to suma małych wysiłków powtarzanych dzień po dniu.", "Robert Collier"),
            ("Jedynym sposobem na świetną pracę jest kochać to, co robisz.", "Steve Jobs"),
            ("Przyszłość należy do tych, którzy wierzą w piękno swoich marzeń.", "Eleanor Roosevelt"),
            ("Nie czekaj na idealny moment. Weź moment i uczyń go idealnym.", "Zoey Sayward"),
            ("Sukces nie jest kluczem do szczęścia. Szczęście jest kluczem do sukcesu.", "Albert Schweitzer"),
            ("Droga do sukcesu jest zawsze w budowie.", "Lily Tomlin"),
            ("Każdy dzień to nowa szansa, by zmienić swoje życie.", "Nieznany"),
            ("Wielkie rzeczy nigdy nie przychodzą ze strefy komfortu.", "Nieznany"),
            ("Postęp jest niemożliwy bez zmiany.", "George Bernard Shaw"),
            ("Zacznij tam, gdzie jesteś. Użyj tego, co masz. Zrób to, co możesz.", "Arthur Ashe"),
            ("Odwaga nie jest brakiem strachu, ale działaniem mimo niego.", "Mark Twain"),
            ("Najlepszy czas na posadzenie drzewa był 20 lat temu. Drugi najlepszy czas jest teraz.", "Chińskie przysłowie"),
            ("Twój czas jest ograniczony. Nie marnuj go żyjąc cudzym życiem.", "Steve Jobs"),
            ("Nie licz dni, spraw, by dni się liczyły.", "Muhammad Ali"),
            ("Jakość nie jest dziełem przypadku. Jest wynikiem inteligentnego wysiłku.", "John Ruskin"),
            ("Nie ma windy do sukcesu. Musisz iść po schodach.", "Zig Ziglar"),
            ("Rób to, czego się boisz, a strach na pewno zniknie.", "Ralph Waldo Emerson"),
            ("Praca zespołowa sprawia, że marzenia się spełniają.", "Nieznany"),
            ("Bądź zmianą, którą chcesz widzieć w świecie.", "Mahatma Gandhi"),
            ("Jedyną granicą naszych jutrzejszych osiągnięć są nasze dzisiejsze wątpliwości.", "Franklin D. Roosevelt")
        };

        public Menu1()
        {
            InitializeComponent();
            this.Loaded += (s, e) => PasswordBox.Focus();
            SetFooterText();
            LoadCompanyLogo();
            InitializeClock();
            InitializeQuoteOfTheDay();
            InitializeServerStatusCheck();
        }

        private void LoadCompanyLogo()
        {
            try
            {
                if (CompanyLogoManager.HasLogo())
                {
                    using (var logo = CompanyLogoManager.GetLogo())
                    {
                        if (logo != null)
                        {
                            var bitmapImage = ConvertToBitmapImage(logo as System.Drawing.Bitmap);
                            CompanyLogoImage.Source = bitmapImage;
                            LeftCompanyLogo.Source = bitmapImage;
                            return;
                        }
                    }
                }

                // Domyślne logo dla prawego panelu (małe)
                using (var defaultLogo = CompanyLogoManager.GenerateDefaultLogo(200, 60))
                {
                    CompanyLogoImage.Source = ConvertToBitmapImage(defaultLogo);
                }

                // Domyślne logo dla lewego panelu (duże)
                using (var defaultLogoLarge = CompanyLogoManager.GenerateDefaultLogo(300, 120))
                {
                    LeftCompanyLogo.Source = ConvertToBitmapImage(defaultLogoLarge);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadCompanyLogo error: {ex.Message}");
            }
        }

        private BitmapImage ConvertToBitmapImage(System.Drawing.Bitmap bitmap)
        {
            if (bitmap == null) return null;

            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
        }

        private void LogoBorder_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Sprawdź czy wpisany identyfikator to admin
            string currentInput = PasswordBox.Password;
            if (currentInput != "11111")
            {
                return; // Tylko admin może importować logo
            }

            var contextMenu = new System.Windows.Controls.ContextMenu();

            var importItem = new System.Windows.Controls.MenuItem { Header = "Importuj logo firmy" };
            importItem.Click += (s, args) =>
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Wybierz logo firmy",
                    Filter = "Pliki graficzne|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Wszystkie pliki|*.*"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    if (CompanyLogoManager.SaveLogo(openFileDialog.FileName))
                    {
                        LoadCompanyLogo();
                        MessageBox.Show("Logo firmy zostało zaktualizowane!", "Sukces",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Nie udało się zapisać logo.", "Błąd",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            };

            var deleteItem = new System.Windows.Controls.MenuItem { Header = "Usuń logo" };
            deleteItem.Click += (s, args) =>
            {
                if (MessageBox.Show("Czy na pewno chcesz usunąć logo firmy?", "Potwierdzenie",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    CompanyLogoManager.DeleteLogo();
                    LoadCompanyLogo();
                }
            };

            contextMenu.Items.Add(importItem);
            contextMenu.Items.Add(new System.Windows.Controls.Separator());
            contextMenu.Items.Add(deleteItem);

            contextMenu.IsOpen = true;
        }

        private void SetFooterText()
        {
            // Automatycznie pobiera wersję i rok
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            string versionString = $"{version.Major}.{version.Minor}";
            FooterText.Text = $"© {DateTime.Now.Year} Piórkowscy | Wersja {versionString}";
        }

        #region Zegar i data

        private void InitializeClock()
        {
            // Ustaw początkową wartość
            UpdateClockDisplay();

            // Uruchom timer do aktualizacji co sekundę
            clockTimer = new DispatcherTimer();
            clockTimer.Interval = TimeSpan.FromSeconds(1);
            clockTimer.Tick += ClockTimer_Tick;
            clockTimer.Start();
        }

        private void ClockTimer_Tick(object sender, EventArgs e)
        {
            UpdateClockDisplay();
        }

        private void UpdateClockDisplay()
        {
            var now = DateTime.Now;

            // Aktualizuj czas (format 24h)
            CurrentTimeText.Text = now.ToString("HH:mm");

            // Aktualizuj datę z dniem tygodnia po polsku
            var culture = new CultureInfo("pl-PL");
            string dayOfWeek = culture.DateTimeFormat.GetDayName(now.DayOfWeek);
            // Pierwsza litera wielka
            dayOfWeek = char.ToUpper(dayOfWeek[0]) + dayOfWeek.Substring(1);

            CurrentDateText.Text = $"{dayOfWeek}, {now.ToString("d MMMM yyyy", culture)}";
        }

        #endregion

        #region Cytat dnia

        private void InitializeQuoteOfTheDay()
        {
            // Wybierz cytat na podstawie dnia roku (zawsze ten sam cytat danego dnia)
            int dayOfYear = DateTime.Now.DayOfYear;
            int quoteIndex = dayOfYear % motivationalQuotes.Count;

            var (quote, author) = motivationalQuotes[quoteIndex];

            QuoteText.Text = $"\"{quote}\"";
            QuoteAuthor.Text = $"- {author}";
        }

        #endregion

        #region Status serwera

        private void InitializeServerStatusCheck()
        {
            // Sprawdź status natychmiast
            CheckServerStatus();

            // Uruchom timer do sprawdzania co 30 sekund
            serverCheckTimer = new DispatcherTimer();
            serverCheckTimer.Interval = TimeSpan.FromSeconds(30);
            serverCheckTimer.Tick += ServerCheckTimer_Tick;
            serverCheckTimer.Start();
        }

        private void ServerCheckTimer_Tick(object sender, EventArgs e)
        {
            CheckServerStatus();
        }

        private async void CheckServerStatus()
        {
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        using (SqlConnection connection = new SqlConnection(connectionString))
                        {
                            connection.Open();
                            isServerOnline = true;
                        }
                    }
                    catch
                    {
                        isServerOnline = false;
                    }
                });

                // Aktualizuj UI na głównym wątku
                Dispatcher.Invoke(() =>
                {
                    if (isServerOnline)
                    {
                        ServerStatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                        ServerStatusText.Text = "Serwer online";
                        ServerStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D"));
                    }
                    else
                    {
                        ServerStatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
                        ServerStatusText.Text = "Serwer offline";
                        ServerStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CheckServerStatus error: {ex.Message}");
            }
        }

        #endregion

        #region Personalizowane powitanie

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            string userId = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(userId) || userId.Length < 3)
            {
                // Ukryj powitanie jeśli za mało znaków
                PersonalGreeting.Visibility = Visibility.Collapsed;
                return;
            }

            // Spróbuj pobrać imię użytkownika z bazy
            try
            {
                string userName = GetUserFirstName(userId);
                if (!string.IsNullOrEmpty(userName) && userName != userId)
                {
                    // Wyświetl powitanie z imieniem
                    string greeting = GetTimeBasedGreeting();
                    PersonalGreeting.Text = $"{greeting}, {userName}!";
                    PersonalGreeting.Visibility = Visibility.Visible;
                }
                else
                {
                    PersonalGreeting.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                PersonalGreeting.Visibility = Visibility.Collapsed;
            }
        }

        private string GetTimeBasedGreeting()
        {
            int hour = DateTime.Now.Hour;

            if (hour >= 5 && hour < 12)
                return "Dzień dobry";
            else if (hour >= 12 && hour < 18)
                return "Witaj";
            else if (hour >= 18 && hour < 22)
                return "Dobry wieczór";
            else
                return "Witaj";
        }

        private string GetUserFirstName(string userId)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    // Pobierz imię (pierwszą część Name przed spacją)
                    string query = "SELECT TOP 1 Name FROM operators WHERE ID = @username";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@username", userId);
                        var result = command.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            string fullName = result.ToString();
                            // Weź pierwsze imię (przed spacją)
                            string[] parts = fullName.Split(' ');
                            return parts.Length > 0 ? parts[0] : fullName;
                        }
                        return null;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        #endregion

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
                    StopTimers();
                    this.Hide();

                    MENU menuWindow = new MENU();

                    // Pokaż ekran powitalny z avatarem (nieblokujący, na dole ekranu)
                    try
                    {
                        WelcomeScreen.Show(username, App.UserFullName);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"WelcomeScreen error: {ex.Message}");
                    }
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
            StopTimers();
            Application.Current.Shutdown();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            StopTimers();
            Application.Current.Shutdown();
        }

        private void StopTimers()
        {
            if (clockTimer != null)
            {
                clockTimer.Stop();
                clockTimer = null;
            }

            if (serverCheckTimer != null)
            {
                serverCheckTimer.Stop();
                serverCheckTimer = null;
            }
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