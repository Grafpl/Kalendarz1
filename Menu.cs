using Kalendarz1.OfertaCenowa;
using Kalendarz1.Opakowania.Views;  // Nowe okna opakowań WPF
using Kalendarz1.Reklamacje;
using Kalendarz1.Zywiec.RaportyStatystyki;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class MENU : Form
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string connectionHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private Dictionary<string, bool> userPermissions = new Dictionary<string, bool>();
        private bool isAdmin = false;
        private Panel headerPanel;
        private Panel sidePanel;
        private Label welcomeLabel;
        private TableLayoutPanel mainLayout;

        public MENU()
        {
            InitializeComponent();
            InitializeCustomComponents();
            LoadUserPermissions();
            SetupMenuItems();
            ApplyModernStyle();
        }

        private void InitializeCustomComponents()
        {
            this.WindowState = FormWindowState.Maximized;
            this.Text = "System Zarządzania - Piórkowscy";

            headerPanel = new Panel { Dock = DockStyle.Top, Height = 90, BackColor = Color.FromArgb(45, 57, 69) };

            // Logo obrazek
            try
            {
                var logoPictureBox = new PictureBox
                {
                    Size = new Size(70, 70),
                    Location = new Point(15, 10),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Transparent
                };

                // Szukaj Logo.png w różnych lokalizacjach
                string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logo.png");
                if (!File.Exists(logoPath))
                {
                    logoPath = Path.Combine(Directory.GetCurrentDirectory(), "Logo.png");
                }
                if (File.Exists(logoPath))
                {
                    logoPictureBox.Image = Image.FromFile(logoPath);
                    headerPanel.Controls.Add(logoPictureBox);
                }

                // Tekst firmy obok logo
                Label logoLabel = new Label
                {
                    Text = "PIÓRKOWSCY",
                    Font = new Font("Segoe UI", 20, FontStyle.Bold),
                    ForeColor = Color.White,
                    AutoSize = true,
                    Location = new Point(95, 18)
                };
                headerPanel.Controls.Add(logoLabel);

                // Podtytuł
                Label subtitleLabel = new Label
                {
                    Text = "System Zarządzania Produkcją",
                    Font = new Font("Segoe UI", 10),
                    ForeColor = Color.FromArgb(180, 180, 180),
                    AutoSize = true,
                    Location = new Point(97, 52)
                };
                headerPanel.Controls.Add(subtitleLabel);
            }
            catch { }

            welcomeLabel = new Label { Text = $"👤 Zalogowany jako: {App.UserID}", Font = new Font("Segoe UI", 12), ForeColor = Color.White, AutoSize = true };
            headerPanel.Controls.Add(welcomeLabel);

            sidePanel = new Panel { Dock = DockStyle.Left, Width = 220, BackColor = Color.FromArgb(35, 45, 55), Visible = false };

            var adminPanelButton = new Button { Text = "⚙ Panel Administracyjny", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.FromArgb(229, 57, 53), FlatStyle = FlatStyle.Flat, Size = new Size(190, 45), Location = new Point(15, 20), Cursor = Cursors.Hand };
            adminPanelButton.FlatAppearance.BorderSize = 0;
            adminPanelButton.Click += AdminPanelButton_Click;
            sidePanel.Controls.Add(adminPanelButton);

            var logoutButton = new Button { Text = "🚪 Wyloguj", Font = new Font("Segoe UI", 10), ForeColor = Color.White, BackColor = Color.FromArgb(76, 88, 100), FlatStyle = FlatStyle.Flat, Size = new Size(190, 40), Location = new Point(15, 75), Cursor = Cursors.Hand };
            logoutButton.FlatAppearance.BorderSize = 0;
            logoutButton.Click += LogoutButton_Click;
            sidePanel.Controls.Add(logoutButton);

            mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(236, 239, 241),
                Padding = new Padding(10),
                ColumnCount = 2,
                RowCount = 1,
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            this.Controls.Add(mainLayout);
            this.Controls.Add(sidePanel);
            this.Controls.Add(headerPanel);
        }

        private void LoadUserPermissions()
        {
            string userId = App.UserID;
            isAdmin = (userId == "11111");

            LoadAllPermissions(false);

            if (isAdmin)
            {
                sidePanel.Visible = true;
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
                            MessageBox.Show("Użytkownik nie ma zdefiniowanych uprawnień. Dostęp został zablokowany.", "Brak uprawnień", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania uprawnień: {ex.Message}\n\nDostęp został zablokowany z powodu błędu.", "Błąd krytyczny", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                [22] = "NotatkiZeSpotkan",
                [23] = "PlanTygodniowy",
                [24] = "LiczenieMagazynu",
                [25] = "PanelMagazyniera",
                [26] = "KartotekaOdbiorcow",
                [27] = "AnalizaWydajnosci",
                [28] = "RezerwacjaKlas",
                [29] = "DashboardWyczerpalnosci",
                [30] = "ListaOfert",
                [31] = "DashboardOfert",
                [32] = "PanelReklamacji",
                [33] = "ReklamacjeJakosc",
                [34] = "RaportyHodowcow",
                [35] = "AdminPermissions"
            };

            for (int i = 0; i < accessString.Length && i < accessMap.Count; i++)
            {
                if (accessMap.ContainsKey(i) && accessString[i] == '1')
                {
                    userPermissions[accessMap[i]] = true;
                }
            }
        }

        private void LoadAllPermissions(bool grantAll)
        {
            var allModules = GetAllModules();
            if (userPermissions.Count == 0)
            {
                foreach (var module in allModules)
                {
                    userPermissions.Add(module, grantAll);
                }
            }
            else
            {
                foreach (var module in allModules)
                {
                    userPermissions[module] = grantAll;
                }
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
                "PrognozyUboju", "AnalizaTygodniowa", "NotatkiZeSpotkan", "PlanTygodniowy",
                "LiczenieMagazynu", "PanelMagazyniera", "KartotekaOdbiorcow", "AnalizaWydajnosci",
                "RezerwacjaKlas", "DashboardWyczerpalnosci",
                "ListaOfert", "DashboardOfert",
                "PanelReklamacji", "ReklamacjeJakosc", "RaportyHodowcow",
                "AdminPermissions"
            };
        }

        private void SetupMenuItems()
        {
            mainLayout.Controls.Clear();

            // ══════════════════════════════════════════════════════════════════════════════
            // KOLORY DZIAŁÓW - GRADIENT OD JAŚNIEJSZEGO DO CIEMNIEJSZEGO
            // ══════════════════════════════════════════════════════════════════════════════
            // ZAKUP/ZAOPATRZENIE - Odcienie zielonego (od jasnego do ciemnego)
            // SPRZEDAŻ/CRM - Odcienie niebieskiego (od jasnego do ciemnego)
            // PRODUKCJA/MAGAZYN - Odcienie pomarańczowego (od jasnego do ciemnego)
            // OPAKOWANIA/TRANSPORT - Odcienie turkusowego (od jasnego do ciemnego)
            // FINANSE/ZARZĄDZANIE - Odcienie szaroniebieskiego (od jasnego do ciemnego)
            // ADMINISTRACJA - Odcienie czerwonego (od jasnego do ciemnego)
            // ══════════════════════════════════════════════════════════════════════════════

            var leftColumnCategories = new Dictionary<string, List<MenuItemConfig>>
            {
                // ═══════════════════════════════════════════════════════════════════════════
                // DZIAŁ ZAKUPÓW - KOLOR ZIELONY (gradient od jasnego #A5D6A7 do ciemnego #1B5E20)
                // ═══════════════════════════════════════════════════════════════════════════
                ["ZAOPATRZENIE I ZAKUPY"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("DaneHodowcy", "Baza Hodowców",
                        "Kompletna kartoteka wszystkich dostawców żywca kurczaków z danymi kontaktowymi i historią współpracy",
                        Color.FromArgb(165, 214, 167), // Jasny zielony #A5D6A7
                        () => new WidokKontrahenci(), "🧑‍🌾"),

                    new MenuItemConfig("WstawieniaHodowcy", "Cykle Wstawień",
                        "Rejestracja i monitorowanie cykli hodowlanych piskląt u hodowców wraz z terminami odbioru",
                        Color.FromArgb(129, 199, 132), // #81C784
                        () => new WidokWstawienia(), "🐣"),

                    new MenuItemConfig("TerminyDostawyZywca", "Kalendarz Dostaw Żywca",
                        "Interaktywny kalendarz planowania terminów dostaw żywca od hodowców do ubojni",
                        Color.FromArgb(102, 187, 106), // #66BB6A
                        () => new WidokKalendarza { UserID = App.UserID, WindowState = FormWindowState.Maximized }, "📅"),

                    new MenuItemConfig("PlachtyAviloga", "Matryca Transportu",
                        "Zaawansowane planowanie tras transportu żywca z optymalizacją załadunku i wysyłką SMS",
                        Color.FromArgb(76, 175, 80), // #4CAF50
                        () => new WidokMatrycaWPF(), "🚛"),

                    new MenuItemConfig("Specyfikacje", "Specyfikacja Surowca",
                        "Definiowanie parametrów jakościowych surowca od poszczególnych dostawców żywca",
                        Color.FromArgb(67, 160, 71), // #43A047
                        () => new WidokSpecyfikacje(), "📋"),

                    new MenuItemConfig("DokumentyZakupu", "Dokumenty i Umowy",
                        "Archiwum umów handlowych, certyfikatów i dokumentów związanych z zakupem żywca",
                        Color.FromArgb(56, 142, 60), // #388E3C
                        () => new SprawdzalkaUmow { UserID = App.UserID }, "📑"),

                    new MenuItemConfig("PlatnosciHodowcy", "Rozliczenia z Hodowcami",
                        "Monitorowanie należności i płatności dla dostawców żywca wraz z historią transakcji",
                        Color.FromArgb(46, 125, 50), // #2E7D32
                        () => new Platnosci(), "💵"),

                    new MenuItemConfig("ZakupPaszyPisklak", "Zakup Paszy i Piskląt",
                        "Ewidencja zakupów pasz i piskląt dla hodowców kontraktowych",
                        Color.FromArgb(27, 94, 32), // Ciemny zielony #1B5E20
                        null, "🌾"),

                    new MenuItemConfig("RaportyHodowcow", "Statystyki Hodowców",
                        "Raporty i analizy współpracy z hodowcami - wydajność, jakość, terminowość dostaw",
                        Color.FromArgb(27, 94, 32), // #1B5E20
                        () => new RaportyStatystykiWindow(), "📊")
                },

                // ═══════════════════════════════════════════════════════════════════════════
                // DZIAŁ PRODUKCJI - KOLOR POMARAŃCZOWY (gradient od jasnego #FFCC80 do ciemnego #E65100)
                // ═══════════════════════════════════════════════════════════════════════════
                ["PRODUKCJA I MAGAZYN"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("ProdukcjaPodglad", "Panel Produkcji",
                        "Bieżący monitoring procesu uboju i krojenia z podglądem wydajności linii",
                        Color.FromArgb(255, 204, 128), // Jasny pomarańczowy #FFCC80
                        () => {
                            var window = new Kalendarz1.ProdukcjaPanel();
                            window.UserID = App.UserID;
                            return window;
                        }, "🏭"),

                    new MenuItemConfig("KalkulacjaKrojenia", "Kalkulacja Rozbioru",
                        "Planowanie procesu krojenia tuszek z kalkulacją wydajności poszczególnych elementów",
                        Color.FromArgb(255, 183, 77), // #FFB74D
                        () => new PokazKrojenieMrozenie { WindowState = FormWindowState.Maximized }, "✂️"),

                    new MenuItemConfig("PrzychodMrozni", "Magazyn Mroźni",
                        "Zarządzanie stanami magazynowymi produktów mrożonych z kontrolą partii i dat",
                        Color.FromArgb(255, 152, 0), // #FF9800
                        () => new Mroznia(), "❄️"),

                    new MenuItemConfig("LiczenieMagazynu", "Inwentaryzacja Magazynu",
                        "Codzienna rejestracja stanów magazynowych produktów gotowych i surowców",
                        Color.FromArgb(251, 140, 0), // #FB8C00
                        () => {
                            return new Kalendarz1.MagazynLiczenie.Formularze.LiczenieStanuWindow(
                                connectionString,
                                connectionHandel,
                                App.UserID
                            );
                        }, "📦"),

                    new MenuItemConfig("PanelMagazyniera", "Panel Magazyniera",
                        "Kompleksowe narzędzie do zarządzania wydaniami towarów i dokumentacją magazynową",
                        Color.FromArgb(245, 124, 0), // #F57C00
                        () => {
                            var panel = new Kalendarz1.MagazynPanel();
                            panel.UserID = App.UserID;
                            return panel;
                        }, "🗃️"),

                    new MenuItemConfig("AnalizaWydajnosci", "Analiza Wydajności",
                        "Porównanie masy żywca do masy tuszek - analiza strat i efektywności uboju",
                        Color.FromArgb(230, 81, 0), // Ciemny pomarańczowy #E65100
                        () => new AnalizaWydajnosciKrojenia(connectionHandel), "📈")
                },

                // ═══════════════════════════════════════════════════════════════════════════
                // DZIAŁ ADMINISTRACJI - KOLOR CZERWONY (gradient od jasnego #EF9A9A do ciemnego #B71C1C)
                // ═══════════════════════════════════════════════════════════════════════════
                ["ADMINISTRACJA SYSTEMU"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("ZmianyUHodowcow", "Wnioski o Zmiany",
                        "Przeglądanie i zatwierdzanie wniosków o zmiany danych hodowców zgłoszonych przez użytkowników",
                        Color.FromArgb(239, 154, 154), // Jasny czerwony #EF9A9A
                        () => new AdminChangeRequestsForm(connectionString, App.UserID), "📝"),

                    new MenuItemConfig("AdminPermissions", "Zarządzanie Uprawnieniami",
                        "Panel administratora do nadawania i odbierania uprawnień dostępu użytkownikom systemu",
                        Color.FromArgb(183, 28, 28), // Ciemny czerwony #B71C1C
                        () => new AdminPermissionsForm(), "🔐")
                }
            };

            var rightColumnCategories = new Dictionary<string, List<MenuItemConfig>>
            {
                // ═══════════════════════════════════════════════════════════════════════════
                // DZIAŁ SPRZEDAŻY - KOLOR NIEBIESKI (gradient od jasnego #90CAF9 do ciemnego #0D47A1)
                // ═══════════════════════════════════════════════════════════════════════════
                ["SPRZEDAŻ I CRM"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("CRM", "Relacje z Klientami",
                        "Zarządzanie relacjami z odbiorcami - kontakty, notatki, historia współpracy",
                        Color.FromArgb(144, 202, 249), // Jasny niebieski #90CAF9
                        () => new CRM { UserID = App.UserID }, "🤝"),

                    new MenuItemConfig("KartotekaOdbiorcow", "Kartoteka Odbiorców",
                        "Pełna baza danych klientów z danymi kontaktowymi, warunkami handlowymi i historią zamówień",
                        Color.FromArgb(100, 181, 246), // #64B5F6
                        () => {
                            var window = new Kalendarz1.KartotekaOdbiorcowWindow();
                            window.UserID = App.UserID;
                            return window;
                        }, "👤"),

                    new MenuItemConfig("ZamowieniaOdbiorcow", "Zamówienia Klientów",
                        "Przyjmowanie i realizacja zamówień na produkty mięsne od odbiorców hurtowych",
                        Color.FromArgb(66, 165, 245), // #42A5F5
                        () => {
                            var window = new Kalendarz1.WPF.MainWindow();
                            window.UserID = App.UserID;
                            return window;
                        }, "🛒"),

                    new MenuItemConfig("DokumentySprzedazy", "Faktury Sprzedaży",
                        "Przeglądanie i drukowanie faktur sprzedaży wraz z dokumentami WZ",
                        Color.FromArgb(33, 150, 243), // #2196F3
                        () => new WidokFakturSprzedazy { UserID = App.UserID }, "🧾"),

                    new MenuItemConfig("OfertaCenowa", "Kreator Ofert",
                        "Tworzenie profesjonalnych ofert cenowych dla klientów z aktualnym cennikiem produktów",
                        Color.FromArgb(30, 136, 229), // #1E88E5
                        () => {
                            var window = new OfertaHandlowaWindow();
                            window.UserID = App.UserID;
                            return window;
                        }, "💰"),

                    new MenuItemConfig("ListaOfert", "Archiwum Ofert",
                        "Historia wszystkich wysłanych ofert handlowych z możliwością kopiowania i edycji",
                        Color.FromArgb(25, 118, 210), // #1976D2
                        () => {
                            var window = new OfertyListaWindow();
                            window.UserID = App.UserID;
                            return window;
                        }, "📂"),

                    new MenuItemConfig("DashboardOfert", "Analiza Ofert",
                        "Statystyki skuteczności ofert - konwersja, wartości, porównania okresów",
                        Color.FromArgb(21, 101, 192), // #1565C0
                        () => {
                            return new OfertyDashboardWindow();
                        }, "📊"),

                    new MenuItemConfig("DashboardWyczerpalnosci", "Klasy Wagowe",
                        "Rozdzielanie dostępnych klas wagowych tuszek pomiędzy zamówienia klientów",
                        Color.FromArgb(13, 71, 161), // Ciemny niebieski #0D47A1
                        () => {
                            var window = new DashboardKlasWagowychWindow();
                            window.UserID = App.UserID;
                            return window;
                        }, "⚖️"),

                    new MenuItemConfig("PanelReklamacji", "Reklamacje Klientów",
                        "Rejestracja i obsługa reklamacji jakościowych zgłaszanych przez odbiorców",
                        Color.FromArgb(21, 101, 192), // #1565C0
                        () => new FormPanelReklamacjiWindow(connectionString, App.UserID), "⚠️")
                },

                // ═══════════════════════════════════════════════════════════════════════════
                // DZIAŁ PLANOWANIA - KOLOR FIOLETOWY (gradient od jasnego #CE93D8 do ciemnego #4A148C)
                // ═══════════════════════════════════════════════════════════════════════════
                ["PLANOWANIE I ANALIZY"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("PrognozyUboju", "Prognoza Uboju",
                        "Analiza średnich tygodniowych zakupów żywca z prognozą zapotrzebowania",
                        Color.FromArgb(206, 147, 216), // Jasny fioletowy #CE93D8
                        () => new PrognozyUboju.PrognozyUbojuWindow(), "🔮"),

                    new MenuItemConfig("PlanTygodniowy", "Plan Tygodniowy",
                        "Harmonogram uboju i krojenia na nadchodzący tydzień z podziałem na dni",
                        Color.FromArgb(171, 71, 188), // #AB47BC
                        () => new Kalendarz1.TygodniowyPlan(), "🗓️"),

                    new MenuItemConfig("AnalizaTygodniowa", "Dashboard Analityczny",
                        "Kompleksowa analiza bilansu produkcji i sprzedaży z wykresami i wskaźnikami",
                        Color.FromArgb(74, 20, 140), // Ciemny fioletowy #4A148C
                        () => new Kalendarz1.AnalizaTygodniowa.AnalizaTygodniowaWindow(), "📉")
                },

                // ═══════════════════════════════════════════════════════════════════════════
                // DZIAŁ OPAKOWAŃ - KOLOR TURKUSOWY (gradient od jasnego #80DEEA do ciemnego #006064)
                // ═══════════════════════════════════════════════════════════════════════════
                ["OPAKOWANIA I TRANSPORT"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("PodsumowanieSaldOpak", "Zestawienie Opakowań",
                        "Zbiorcze zestawienie sald opakowań zwrotnych wg typu z podsumowaniem wartości",
                        Color.FromArgb(128, 222, 234), // Jasny turkusowy #80DEEA
                        () => new ZestawienieOpakowanWindow(), "📦"),

                    new MenuItemConfig("SaldaOdbiorcowOpak", "Salda Opakowań Klientów",
                        "Szczegółowe salda opakowań zwrotnych dla każdego kontrahenta z historią obrotów",
                        Color.FromArgb(0, 172, 193), // #00ACC1
                        () => new SaldaWszystkichOpakowanWindow(), "🏷️"),

                    new MenuItemConfig("UstalanieTranportu", "Planowanie Transportu",
                        "Organizacja tras dostaw do klientów z przydziałem pojazdów i kierowców",
                        Color.FromArgb(0, 96, 100), // Ciemny turkusowy #006064
                        () => {
                            var connTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                            var repo = new Transport.Repozytorium.TransportRepozytorium(connTransport, connectionString);
                            return new Transport.Formularze.TransportMainFormImproved(repo, App.UserID);
                        }, "🚚")
                },

                // ═══════════════════════════════════════════════════════════════════════════
                // DZIAŁ FINANSÓW - KOLOR SZARONIEBIESKI (gradient od jasnego #B0BEC5 do ciemnego #263238)
                // ═══════════════════════════════════════════════════════════════════════════
                ["FINANSE I ZARZĄDZANIE"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("DaneFinansowe", "Wyniki Finansowe",
                        "Zestawienie wyników finansowych firmy - przychody, koszty, marże i rentowność",
                        Color.FromArgb(176, 190, 197), // Jasny szaroniebieski #B0BEC5
                        () => new WidokSprzeZakup(), "💼"),

                    new MenuItemConfig("NotatkiZeSpotkan", "Notatki Służbowe",
                        "Rejestr notatek ze spotkań biznesowych, ustaleń i zadań do wykonania",
                        Color.FromArgb(38, 50, 56), // Ciemny szaroniebieski #263238
                        () => new Kalendarz1.NotatkiZeSpotkan.NotatkirGlownyWindow(App.UserID), "📝")
                }
            };

            var leftPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoScroll = true, WrapContents = false };
            PopulateColumn(leftPanel, leftColumnCategories);
            mainLayout.Controls.Add(leftPanel, 0, 0);

            var rightPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoScroll = true, WrapContents = false };
            PopulateColumn(rightPanel, rightColumnCategories);
            mainLayout.Controls.Add(rightPanel, 1, 0);
        }

        private void PopulateColumn(FlowLayoutPanel columnPanel, Dictionary<string, List<MenuItemConfig>> categories)
        {
            foreach (var category in categories)
            {
                var permittedItems = category.Value.Where(item =>
                    (userPermissions.ContainsKey(item.ModuleName) && userPermissions[item.ModuleName])
                ).ToList();

                if (permittedItems.Any() || isAdmin)
                {
                    var categoryLabel = new Label
                    {
                        Text = "▎" + category.Key,
                        Font = new Font("Segoe UI", 14, FontStyle.Bold),
                        ForeColor = Color.FromArgb(45, 57, 69),
                        AutoSize = false,
                        Width = columnPanel.Width - 40,
                        Height = 40,
                        TextAlign = ContentAlignment.MiddleLeft,
                        Margin = new Padding(10, 20, 10, 5)
                    };
                    columnPanel.Controls.Add(categoryLabel);

                    var buttonsPanel = new FlowLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        AutoSize = true,
                        Padding = new Padding(5, 0, 5, 10)
                    };

                    var itemsToDisplay = isAdmin ? category.Value : permittedItems;
                    foreach (var item in itemsToDisplay)
                    {
                        var buttonPanel = CreateMenuButton(item);
                        buttonsPanel.Controls.Add(buttonPanel);
                    }
                    columnPanel.Controls.Add(buttonsPanel);
                }
            }
        }

        private Panel CreateMenuButton(MenuItemConfig config)
        {
            var panel = new Panel { Size = new Size(180, 120), BackColor = Color.White, Margin = new Padding(10), Cursor = Cursors.Hand, Tag = config };
            var bottomBorder = new Panel { Height = 5, Dock = DockStyle.Bottom, BackColor = config.Color };
            var iconLabel = new Label { Text = config.IconText, Font = new Font("Segoe UI Emoji", 24), Size = new Size(50, 50), Location = new Point(15, 15), ForeColor = config.Color };
            var titleLabel = new Label { Text = config.DisplayName, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.FromArgb(55, 71, 79), Location = new Point(15, 65), AutoSize = true };
            var descriptionLabel = new Label { Text = config.Description, Font = new Font("Segoe UI", 8), ForeColor = Color.Gray, Location = new Point(15, 85), Size = new Size(150, 30) };

            panel.Controls.Add(titleLabel);
            panel.Controls.Add(descriptionLabel);
            panel.Controls.Add(iconLabel);
            panel.Controls.Add(bottomBorder);

            panel.Paint += (sender, e) => {
                ControlPaint.DrawBorder(e.Graphics, panel.ClientRectangle,
                    Color.FromArgb(220, 220, 220), 1, ButtonBorderStyle.Solid,
                    Color.FromArgb(220, 220, 220), 1, ButtonBorderStyle.Solid,
                    Color.FromArgb(220, 220, 220), 1, ButtonBorderStyle.Solid,
                    Color.FromArgb(220, 220, 220), 1, ButtonBorderStyle.Solid);
            };

            Action<Control> attachClickEvent = null;
            attachClickEvent = (control) =>
            {
                control.Click += Panel_Click;
                foreach (Control child in control.Controls)
                {
                    attachClickEvent(child);
                }
            };

            panel.MouseEnter += (s, e) => panel.BackColor = Color.FromArgb(248, 249, 250);
            panel.MouseLeave += (s, e) => panel.BackColor = Color.White;
            attachClickEvent(panel);
            return panel;
        }

        private void Panel_Click(object sender, EventArgs e)
        {
            Control control = sender as Control;
            Panel panel = control as Panel ?? control.Parent as Panel;
            if (panel?.Tag is MenuItemConfig config)
            {
                try
                {
                    if (config.FormFactory != null)
                    {
                        var formularz = config.FormFactory();

                        if (formularz is System.Windows.Window wpfWindow)
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
                                "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show($"Funkcja '{config.DisplayName}' jest w trakcie rozwoju.",
                            "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas otwierania modułu: {ex.Message}\n\nSzczegóły: {ex.StackTrace}",
                        "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ApplyModernStyle()
        {
            this.BackColor = Color.FromArgb(236, 239, 241);
            this.Font = new Font("Segoe UI", 10);
        }

        private void AdminPanelButton_Click(object sender, EventArgs e)
        {
            var adminForm = new AdminPermissionsForm();
            adminForm.ShowDialog();
            LoadUserPermissions();
            SetupMenuItems();
        }

        private void LogoutButton_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Czy na pewno chcesz się wylogować?", "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Application.Restart();
            }
        }

        private void MENU_Load(object sender, EventArgs e) => HandleResize();

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            HandleResize();
        }

        private void HandleResize()
        {
            if (welcomeLabel != null)
            {
                welcomeLabel.Location = new Point(this.Width - welcomeLabel.Width - 40, (headerPanel.Height - welcomeLabel.Height) / 2);
            }
        }
    }

    public class MenuItemConfig
    {
        public string ModuleName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public Color Color { get; set; }
        public Func<object> FormFactory { get; set; }
        public string IconText { get; set; }

        public MenuItemConfig(string moduleName, string displayName, string description,
            Color color, Func<object> formFactory, string iconText = null)
        {
            ModuleName = moduleName;
            DisplayName = displayName;
            Description = description;
            Color = color;
            FormFactory = formFactory;
            IconText = iconText;
        }
    }
}
