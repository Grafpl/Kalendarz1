using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    public partial class MainWindow : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private Dictionary<string, bool> userPermissions = new Dictionary<string, bool>();
        private bool isAdmin = false;
        private DispatcherTimer clockTimer;

        public MainWindow()
        {
            InitializeComponent();
            InitializeApp();
        }

        private void InitializeApp()
        {
            // Ustawienie etykiet
            UserLabel.Text = $"👤 {App.UserID}";
            DateLabel.Text = $"Data: {DateTime.Now:dd.MM.yyyy}";

            // Uruchomienie zegara
            StartClock();

            // Ładowanie uprawnień i modułów
            LoadUserPermissions();
            SetupMenuItems();
        }

        private void StartClock()
        {
            clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            clockTimer.Tick += (s, e) => TimeLabel.Text = DateTime.Now.ToString("HH:mm:ss");
            clockTimer.Start();
            TimeLabel.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        private void LoadUserPermissions()
        {
            string userId = App.UserID;
            isAdmin = (userId == "11111");

            LoadAllPermissions(false);

            if (isAdmin)
            {
                SidePanel.Visibility = Visibility.Visible;
                LoadAllPermissions(true);
            }
            else
            {
                LoadUserAccessFromDatabase(userId);
            }
        }

        private void LoadUserAccessFromDatabase(string userId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT Access FROM operators WHERE ID = @userId";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        var result = cmd.ExecuteScalar();

                        if (result != null && result != DBNull.Value && !string.IsNullOrEmpty(result.ToString()))
                        {
                            ParseAccessString(result.ToString());
                        }
                        else
                        {
                            MessageBox.Show("Użytkownik nie ma zdefiniowanych uprawnień.\nSkontaktuj się z administratorem.",
                                "Brak uprawnień", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania uprawnień:\n{ex.Message}",
                    "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
                LoadAllPermissions(false);
            }
        }

        private void ParseAccessString(string accessString)
        {
            var accessMap = new Dictionary<int, string>
            {
                [0] = "DaneHodowcy",
                [1] = "ZakupPaszyPisklak",
                [2] = "WstawieniaHodowcy",
                [3] = "TerminyDostawyZywca",
                [4] = "PlachtyAviloga",
                [5] = "DokumentyZakupu",
                [6] = "Specyfikacje",
                [7] = "PlatnosciHodowcy",
                [8] = "CRM",
                [9] = "ZamowieniaOdbiorcow",
                [10] = "KalkulacjaKrojenia",
                [11] = "PrzychodMrozni",
                [12] = "DokumentySprzedazy",
                [13] = "PodsumowanieSaldOpak",
                [14] = "SaldaOdbiorcowOpak",
                [15] = "DaneFinansowe",
                [16] = "UstalanieTranportu",
                [17] = "ZmianyUHodowcow",
                [18] = "ProdukcjaPodglad",
                [19] = "OfertaCenowa",
                [20] = "PrognozyUboju",
                [21] = "AnalizaTygodniowa",
                [22] = "Monitoring"
            };

            for (int i = 0; i < accessString.Length && i < accessMap.Count; i++)
            {
                if (accessMap.ContainsKey(i) && accessString[i] == '1')
                {
                    userPermissions[accessMap[i]] = true;
                }
            }

            // Monitoring dostępny dla wszystkich użytkowników
            userPermissions["Monitoring"] = true;
        }

        private void LoadAllPermissions(bool grantAll)
        {
            var allModules = GetAllModules();
            if (userPermissions.Count == 0)
            {
                foreach (var module in allModules)
                    userPermissions.Add(module, grantAll);
            }
            else
            {
                foreach (var module in allModules)
                    userPermissions[module] = grantAll;
            }
        }

        private List<string> GetAllModules()
        {
            return new List<string>
            {
                "DaneHodowcy", "ZakupPaszyPisklak", "WstawieniaHodowcy", "TerminyDostawyZywca",
                "PlachtyAviloga", "DokumentyZakupu", "Specyfikacje", "PlatnosciHodowcy",
                "CRM", "ZamowieniaOdbiorcow", "KalkulacjaKrojenia", "PrzychodMrozni",
                "DokumentySprzedazy", "PodsumowanieSaldOpak", "SaldaOdbiorcowOpak", "DaneFinansowe",
                "UstalanieTranportu", "ZmianyUHodowcow", "ProdukcjaPodglad", "OfertaCenowa",
                "PrognozyUboju", "AnalizaTygodniowa", "Monitoring"
            };
        }

        private void SetupMenuItems()
        {
            var categories = new Dictionary<string, List<ModuleConfig>>
            {
                ["Zaopatrzenie"] = new List<ModuleConfig>
                {
                    new ModuleConfig("DaneHodowcy", "Dane Hodowcy", "Zarządzaj bazą hodowców", "#27AE60", () => new WidokKontrahenci(), "📋"),
                    new ModuleConfig("ZakupPaszyPisklak", "Zakup Paszy", "Rejestruj zakupy paszy i piskląt", "#5C8A3A", null, "🌾"),
                    new ModuleConfig("WstawieniaHodowcy", "Wstawienia", "Zarządzaj cyklami wstawień", "#5C8A3A", () => new WidokWstawienia(), "🐣"),
                    new ModuleConfig("TerminyDostawyZywca", "Kalendarz Dostaw", "Planuj terminy dostaw żywca", "#27AE60", () => new WidokKalendarza { UserID = App.UserID, WindowState = System.Windows.Forms.FormWindowState.Maximized }, "📅"),
                    new ModuleConfig("DokumentyZakupu", "Dokumenty Zakupu", "Archiwizuj dokumenty i umowy", "#5C8A3A", () => new SprawdzalkaUmow { UserID = App.UserID }, "📄"),
                    new ModuleConfig("PlatnosciHodowcy", "Płatności", "Monitoruj płatności dla hodowców", "#F39C12", () => new Platnosci(), "💰"),
                    new ModuleConfig("ZmianyUHodowcow", "Wnioski o Zmianę", "Zatwierdzaj zmiany w danych", "#3498DB", () => new AdminChangeRequestsForm(connectionString, App.UserID), "✏️"),
                    new ModuleConfig("Specyfikacje", "Specyfikacja Surowca", "Definiuj specyfikacje produktów", "#7F8C8D", () => new WidokSpecyfikacje(), "📝"),
                    new ModuleConfig("PlachtyAviloga", "Transport Avilog", "Zarządzaj transportem surowca", "#7F8C8D", () => new WidokMatrycaNowy(), "🎯")
                },
                ["Produkcja"] = new List<ModuleConfig>
                {
                    new ModuleConfig("KalkulacjaKrojenia", "Kalkulacja Krojenia", "Planuj proces krojenia", "#F39C12", () => new PokazKrojenieMrozenie { WindowState = System.Windows.Forms.FormWindowState.Maximized }, "✂️"),
                    new ModuleConfig("ProdukcjaPodglad", "Podgląd Produkcji", "Monitoruj bieżącą produkcję", "#F39C12", () => new WidokPanelProdukcjaNowy { UserID = App.UserID }, "🏭"),
                    new ModuleConfig("PrzychodMrozni", "Mroźnia", "Zarządzaj stanami magazynowymi", "#3498DB", () => new Mroznia(), "❄️")
                },
                ["Sprzedaz"] = new List<ModuleConfig>
                {
                    new ModuleConfig("CRM", "CRM", "Zarządzaj relacjami z klientami", "#3498DB", () => new CRM.CRMWindow { UserID = App.UserID }, "👥"),
                    new ModuleConfig("ZamowieniaOdbiorcow", "Zamówienia Mięsa", "Przeglądaj i zarządzaj zamówieniami", "#3498DB", () => new WidokZamowieniaPodsumowanie { UserID = App.UserID }, "📦"),
                    new ModuleConfig("DokumentySprzedazy", "Faktury Sprzedaży", "Generuj i przeglądaj faktury", "#3498DB", () => new WidokFakturSprzedazy { UserID = App.UserID }, "🧾"),
                    new ModuleConfig("PrognozyUboju", "Prognoza Uboju", "Analizuj średnie tygodniowe zakupów", "#9B59B6", () => new PrognozyUboju.PrognozyUbojuWindow(), "📈"),
                    new ModuleConfig("AnalizaTygodniowa", "Dashboard Analityczny", "Analizuj bilans produkcji i sprzedaży", "#E91E63", () => new Kalendarz1.AnalizaTygodniowa.AnalizaTygodniowaWindow(), "📊"),
                    new ModuleConfig("OfertaCenowa", "Oferty Handlowe", "Twórz i zarządzaj ofertami", "#3498DB", () => new OfertaCenowa.OfertaHandlowaWindow(), "💵")
                },
                ["Opakowania"] = new List<ModuleConfig>
                {
                    
                    new ModuleConfig("UstalanieTranportu", "Transport", "Organizuj i planuj transport", "#F39C12", () => { var connTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True"; var repo = new Transport.Repozytorium.TransportRepozytorium(connTransport, connectionString); return new Transport.Formularze.TransportMainFormImproved(repo, App.UserID); }, "🚚")
                },
                ["Finanse"] = new List<ModuleConfig>
                {
                    new ModuleConfig("DaneFinansowe", "Wynik Finansowy", "Analizuj dane finansowe firmy", "#7F8C8D", () => new WidokSprzeZakup(), "💼"),
                    new ModuleConfig("Monitoring", "Monitoring", "Podgląd kamer Hikvision NVR", "#E74C3C", () => new Monitoring.MonitoringWindow(), "📹")
                }
            };

            PopulateCategory(ZaopatrzeniePanel, categories["Zaopatrzenie"], CategoryZaopatrzenie);
            PopulateCategory(ProdukcjaPanel, categories["Produkcja"], CategoryProdukcja);
            PopulateCategory(SprzedazPanel, categories["Sprzedaz"], CategorySprzedaz);
            PopulateCategory(OpakowaniaPanel, categories["Opakowania"], CategoryOpakowania);
            PopulateCategory(FinansePanel, categories["Finanse"], CategoryFinanse);
        }

        private void PopulateCategory(Panel panel, List<ModuleConfig> modules, StackPanel categoryContainer)
        {
            var permittedModules = modules.Where(m =>
                userPermissions.ContainsKey(m.ModuleName) && userPermissions[m.ModuleName]
            ).ToList();

            if (permittedModules.Any() || isAdmin)
            {
                categoryContainer.Visibility = Visibility.Visible;
                var itemsToDisplay = isAdmin ? modules : permittedModules;

                foreach (var module in itemsToDisplay)
                {
                    var button = CreateModuleButton(module);
                    panel.Children.Add(button);
                }
            }
            else
            {
                categoryContainer.Visibility = Visibility.Collapsed;
            }
        }

        private Button CreateModuleButton(ModuleConfig config)
        {
            var button = new Button
            {
                Style = (Style)FindResource("ModuleButtonStyle"),
                DataContext = config,
                ToolTip = $"{config.DisplayName}\n{config.Description}"
            };

            button.Click += (s, e) => OpenModule(config);

            return button;
        }

        private void OpenModule(ModuleConfig config)
        {
            try
            {
                if (config.FormFactory != null)
                {
                    var formularz = config.FormFactory();

                    if (formularz is Window wpfWindow)
                    {
                        wpfWindow.ShowDialog();
                    }
                    else if (formularz is System.Windows.Forms.Form winForm)
                    {
                        winForm.Show();
                    }
                    else if (formularz != null)
                    {
                        MessageBox.Show($"Nieobsługiwany typ okna: {formularz.GetType().Name}",
                            "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show($"Funkcja '{config.DisplayName}' jest w trakcie rozwoju.",
                        "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas otwierania modułu:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AdminPanelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var adminForm = new AdminPermissionsForm();
                adminForm.ShowDialog();

                // Przeładuj uprawnienia po zamknięciu panelu
                LoadUserPermissions();

                // Wyczyść i przeładuj moduły
                ZaopatrzeniePanel.Children.Clear();
                ProdukcjaPanel.Children.Clear();
                SprzedazPanel.Children.Clear();
                OpakowaniaPanel.Children.Clear();
                FinansePanel.Children.Clear();

                SetupMenuItems();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas otwierania panelu administracyjnego:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Czy na pewno chcesz się wylogować?",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                clockTimer?.Stop();
                System.Windows.Forms.Application.Restart();
                Application.Current.Shutdown();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            clockTimer?.Stop();
        }
    }

    // ============ KLASY POMOCNICZE ============

    public class ModuleConfig
    {
        public string ModuleName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string ColorHex { get; set; }
        public Brush ColorBrush => new SolidColorBrush((Color)ColorConverter.ConvertFromString(ColorHex));
        public Func<object> FormFactory { get; set; }
        public string IconText { get; set; }

        public ModuleConfig(string moduleName, string displayName, string description,
            string colorHex, Func<object> formFactory, string iconText = null)
        {
            ModuleName = moduleName;
            DisplayName = displayName;
            Description = description;
            ColorHex = colorHex;
            FormFactory = formFactory;
            IconText = iconText;
        }
    }
}