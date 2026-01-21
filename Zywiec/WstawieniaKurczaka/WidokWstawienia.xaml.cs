using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Kalendarz1.Zywiec.Kalendarz;

namespace Kalendarz1
{
    public class UserStat
    {
        public string User { get; set; }
        public int Count { get; set; }
        public double PerMonth { get; set; }
    }

    public class DeliveryInfo
    {
        public DateTime DataOdbioru { get; set; }
        public int Auta { get; set; }
        public int SztukiDek { get; set; }
        public decimal WagaDek { get; set; }
        public decimal Cena { get; set; }
        public string TypCeny { get; set; }
        public int RoznicaDni { get; set; }
        public string Bufor { get; set; }
    }

    public partial class WidokWstawienia : Window, INotifyPropertyChanged
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string lpDostawa;
        private static ZapytaniaSQL zapytaniasql = new ZapytaniaSQL();

        // Cache dla danych dostaw - ładowany raz przy starcie
        private Dictionary<int, List<DeliveryInfo>> _deliveryCache = new Dictionary<int, List<DeliveryInfo>>();
        private bool _deliveryCacheLoaded = false;

        // Aktualnie otwarty tooltip - tylko jeden naraz
        private ToolTip _currentOpenTooltip = null;

        public event PropertyChangedEventHandler PropertyChanged;

        public WidokWstawienia()
        {
            InitializeComponent();
            LoadLogo();
            InitializeData();
            SetupEventHandlers();
            AnimateWindow();
        }

        private void LoadLogo()
        {
            try
            {
                string logoPath = @"C:\Users\PC\source\repos\Grafpl\Kalendarz1\logo.png";
                if (System.IO.File.Exists(logoPath))
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(logoPath, UriKind.Absolute);
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    imgLogo.Source = bitmap;
                }
                else
                {
                    Console.WriteLine("Logo nie znalezione: " + logoPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd ładowania logo: {ex.Message}");
            }
        }

        private void AnimateWindow()
        {
            var storyboard = (Storyboard)this.Resources["FadeIn"];
            storyboard?.Begin(this);
        }

        private void InitializeData()
        {
            PreloadDeliveryCache();
            LoadWstawienia();
            LoadPrzypomnienia();
            LoadHistoria();
            LoadDoPotwierdzenia();
            UpdateStatistics();
        }

        private void PreloadDeliveryCache()
        {
            try
            {
                _deliveryCache.Clear();
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT
                            HD.LpW,
                            HD.DataOdbioru,
                            HD.Auta,
                            HD.SztukiDek,
                            HD.WagaDek,
                            HD.Cena,
                            HD.typCeny,
                            HD.bufor,
                            WK.DataWstawienia
                        FROM dbo.HarmonogramDostaw HD
                        LEFT JOIN dbo.WstawieniaKurczakow WK ON HD.LpW = WK.Lp
                        ORDER BY HD.LpW, HD.DataOdbioru";

                    using (var cmd = new SqlCommand(query, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // LpW może być int lub string - konwertuj do int
                            var lpWValue = reader[0];
                            if (lpWValue == DBNull.Value) continue;

                            int lpW;
                            if (lpWValue is int intVal)
                                lpW = intVal;
                            else if (!int.TryParse(lpWValue.ToString(), out lpW))
                                continue;

                            if (!_deliveryCache.ContainsKey(lpW))
                                _deliveryCache[lpW] = new List<DeliveryInfo>();

                            var dataOdbioru = reader[1] != DBNull.Value ? Convert.ToDateTime(reader[1]) : DateTime.MinValue;
                            var dataWstawienia = reader[8] != DBNull.Value ? Convert.ToDateTime(reader[8]) : DateTime.MinValue;
                            int roznicaDni = dataOdbioru != DateTime.MinValue && dataWstawienia != DateTime.MinValue
                                ? (dataOdbioru - dataWstawienia).Days : 0;

                            _deliveryCache[lpW].Add(new DeliveryInfo
                            {
                                DataOdbioru = dataOdbioru,
                                Auta = reader[2] != DBNull.Value ? Convert.ToInt32(reader[2]) : 0,
                                SztukiDek = reader[3] != DBNull.Value ? Convert.ToInt32(reader[3]) : 0,
                                WagaDek = reader[4] != DBNull.Value ? Convert.ToDecimal(reader[4]) : 0,
                                Cena = reader[5] != DBNull.Value ? Convert.ToDecimal(reader[5]) : 0,
                                TypCeny = reader[6] != DBNull.Value ? reader[6].ToString() : "-",
                                Bufor = reader[7] != DBNull.Value ? reader[7].ToString() : "",
                                RoznicaDni = roznicaDni
                            });
                        }
                    }
                }
                _deliveryCacheLoaded = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd ładowania cache dostaw: {ex.Message}");
            }
        }

        private void SetupEventHandlers()
        {
            textBoxFilter.TextChanged += TextBoxFilter_TextChanged;
            dataGridWstawienia.SelectionChanged += DataGridWstawienia_SelectionChanged;
            dataGridWstawienia.MouseDoubleClick += DataGridWstawienia_DoubleClick;
            dataGridPrzypomnienia.SelectionChanged += DataGridPrzypomnienia_SelectionChanged;
            dataGridPrzypomnienia.MouseDoubleClick += DataGridPrzypomnienia_DoubleClick;
            chkPokazPrzyszle.Checked += ChkPokazPrzyszle_Changed;
            chkPokazPrzyszle.Unchecked += ChkPokazPrzyszle_Changed;
            datePickerOd.SelectedDateChanged += DatePickerOd_Changed;
        }

        // ====== PRZYCISK POMOCY ======
        private void BtnPomoc_Click(object sender, RoutedEventArgs e)
        {
            var instrukcja = new Window
            {
                Title = "📚 Instrukcja Obsługi Systemu - Kompletny Przewodnik",
                Width = 900,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250))
            };

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(30, 30, 30, 30)
            };

            var stackPanel = new StackPanel();

            // Sekcja 1: Czym jest ten program?
            AddInstrukcjaSection(stackPanel, "🎯 Czym jest ten program?",
                "Ten program pomaga zarządzać wstawieniami kurczaków u hodowców. Dzięki niemu możesz:\n\n" +
                "✅ Śledzić wszystkie wstawienia - kto, kiedy i ile kurczaków dostał\n" +
                "✅ Planować dostawy - kiedy odbierać kurczaki od hodowców\n" +
                "✅ Przypominać o kontaktach - żeby nie zapomnieć zadzwonić do hodowcy\n" +
                "✅ Zobacz statystyki - ile wstawień było dzisiaj, w tym tygodniu, miesiącu\n" +
                "✅ Historia kontaktów - kto i kiedy dzwonił do hodowcy\n\n" +
                "Program automatycznie przypomina, gdy trzeba zadzwonić do hodowcy przed odebraniem kurczaków!");

            // Sekcja 2: Panel Statystyk
            AddInstrukcjaSection(stackPanel, "📊 Panel Statystyk - Co oznaczają liczby na górze?",
                "**GŁÓWNE LICZBY (Kolorowe pudełka w pierwszym wierszu):**\n\n" +
                "📅 Dziś - Ile wstawień dodano dzisiaj (dzisiejsza data)\n" +
                "📊 Tydz - Suma wstawień w tym tygodniu (poniedziałek do niedzieli)\n" +
                "📆 Mies - Wszystkie wstawienia w tym miesiącu\n" +
                "📅 Rok - Suma wstawień od stycznia do teraz\n" +
                "⏰ Przyp - Liczba przypomnień (do ilu hodowców trzeba zadzwonić)\n" +
                "📦 Suma - Wszystkie wstawienia w historii (od początku używania programu)\n\n" +
                "**DODATKOWE INFORMACJE (Małe pudełka pod spodem):**\n\n" +
                "📈 Śr/d - Średnio ile wstawień dziennie (ostatnie 30 dni)\n" +
                "👨‍🌾 Hod - Ilu różnych hodowców jest w systemie\n" +
                "📊 Śr.szt - Średnio ile sztuk kurczaków w jednym wstawieniu\n" +
                "🔝 Najw - Największa liczba kurczaków w jednym wstawieniu\n" +
                "📈 Tr - Trend: czy mamy więcej czy mniej wstawień niż tydzień temu (↗️ więcej, ↘️ mniej, ➡️ podobnie)\n" +
                "📋 W/K - Ile umów wolnych / ile umów kontraktowych w tym roku\n" +
                "⭐ TOP - Który hodowca ma najwięcej wstawień w tym miesiącu\n" +
                "📅 7d - Ile wstawień było w ostatnich 7 dniach\n\n" +
                "**STATYSTYKI PRACOWNIKÓW (Szare pudełko na dole):**\n\n" +
                "👥 Wst - Kto ile wstawień dodał w tym miesiącu\n" +
                "📞 Kont - Kto ile razy dzwonił do hodowców w tym miesiącu\n" +
                "✅ Potw - Kto ile wstawień potwierdził w tym miesiącu");

            // Sekcja 3: Lista Wstawień
            AddInstrukcjaSection(stackPanel, "📋 Lista Wstawień - Główna tabela z lewej strony",
                "**CO TO JEST:**\n" +
                "Lista wszystkich wstawień kurczaków u hodowców. Każdy wiersz = jedno wstawienie.\n\n" +
                "**KOLUMNY W TABELI:**\n" +
                "• LP - Numer wstawienia (unikalny identyfikator)\n" +
                "• Hodowca - Imię i nazwisko hodowcy lub nazwa fermy\n" +
                "• Data - Kiedy kurczaki zostały wstawione u hodowcy\n" +
                "• Ilość - Ile sztuk kurczaków zostało wstawionych (z separatorem tysięcy)\n" +
                "• Typ - Rodzaj umowy (Wolny/Kontrakt)\n" +
                "• Kto - Kto dodał to wstawienie do systemu\n" +
                "• Potw. - Czy wstawienie zostało potwierdzone\n\n" +
                "**KOLORY WIERSZY - Co oznaczają:**\n" +
                "🟢 ZIELONY - Wszystkie dostawy już się odbyły (kurczaki odebrane)\n" +
                "🟡 ŻÓŁTY - Dostawy w trakcie (część kurczaków odebrana, część jeszcze czeka)\n" +
                "⚪ SZARY - Wszystkie dostawy w przyszłości (kurczaki jeszcze rosną)\n\n" +
                "**FILTRY NAD TABELĄ:**\n\n" +
                "🔍 Wyszukiwarka - Wpisz nazwisko hodowcy aby go szybko znaleźć\n" +
                "   Przykład: wpisz \"Kowalski\" aby zobaczyć tylko wstawienia u Kowalskiego\n\n" +
                "📅 Tylko przyszłe - ZAZNACZ ten checkbox aby zobaczyć:\n" +
                "   • Każdego hodowcę tylko RAZ - pokazuje się NAJNOWSZE wstawienie każdego hodowcy\n" +
                "   • Dzięki temu szybko sprawdzisz listę wszystkich aktywnych hodowców\n" +
                "   Przykład: Jeśli hodowca Kuba ma 3 wstawienia (01.01, 02.01, 03.01),\n" +
                "   to po zaznaczeniu checkboxa zobaczysz tylko wstawienie z 03.01\n" +
                "   Wszystkie inne wstawienia będą ukryte - zobaczysz tylko najnowsze!\n\n" +
                "📆 Data od - Wybierz datę aby pokazać tylko wstawienia nie starsze niż ta data\n" +
                "   Przykład: Wybierz 01.01.2025 aby ukryć wszystkie stare wstawienia z 2024\n\n" +
                "**CO MOŻESZ ZROBIĆ:**\n" +
                "• Kliknij wiersz raz - zobaczysz dostawy tego wstawienia po prawej stronie\n" +
                "• Kliknij wiersz 2 razy - otworzy się okno z możliwością dodania nowego wstawienia z danymi hodowcy\n" +
                "• Kliknij prawym przyciskiem myszy (PPM) - zobaczysz menu:\n" +
                "  ✏️ Edytuj wstawienie - zmień dane wstawienia\n" +
                "  📅 Zmień datę wstawienia - przesuń datę wstawienia (możesz też przesunąć dostawy)\n" +
                "  🗑️ Usuń wstawienie - skasuj wstawienie z bazy (UWAGA: nie da się cofnąć!)");

            // Sekcja 4: Zaplanowane Dostawy
            AddInstrukcjaSection(stackPanel, "📦 Zaplanowane Dostawy - Górna tabela po środku",
                "**CO TO JEST:**\n" +
                "Po kliknięciu wstawienia w lewej tabeli, tutaj zobaczysz wszystkie dostawy dla tego wstawienia.\n" +
                "Jedna dostawa = jeden dzień odbioru kurczaków od hodowcy.\n\n" +
                "**KOLUMNY W TABELI:**\n" +
                "• Data - Kiedy odbieramy kurczaki (format: MM-DD dzień tygodnia)\n" +
                "• A - Ile aut (samochodów) potrzebnych do odbioru\n" +
                "• Szt - Ile sztuk kurczaków odbieramy tego dnia (z separatorem tysięcy)\n" +
                "• Waga - Jaka będzie waga kurczaków w kg (z separatorem tysięcy)\n" +
                "• Cena - Cena za kg lub za sztukę\n" +
                "• T - Typ ceny (np. kg, szt)\n" +
                "• Dni - Ile dni minęło od wstawienia do odbioru\n" +
                "• B - Bufor (dodatkowy czas)\n\n" +
                "**PRZYKŁAD:**\n" +
                "Jeśli kurczaki wstawiono 01.01.2025, a pierwsza dostawa jest 20.01.2025,\n" +
                "to w kolumnie 'Dni' zobaczysz: 19 (bo minęło 19 dni)\n\n" +
                "Wszystkie kolumny są widoczne i dopasowane do szerokości tabeli.");

            // Sekcja 5: Przypomnienia
            AddInstrukcjaSection(stackPanel, "⏰ Przypomnienia - Dolna tabela po środku",
                "**CO TO JEST:**\n" +
                "Lista hodowców, do których TRZEBA ZADZWONIĆ przed odbiorem kurczaków.\n" +
                "Program automatycznie dodaje przypomnienie 3 dni przed pierwszą dostawą.\n\n" +
                "**DLACZEGO TO WAŻNE:**\n" +
                "Trzeba zadzwonić do hodowcy żeby:\n" +
                "• Potwierdzić że kurczaki są gotowe do odbioru\n" +
                "• Ustalić dokładną godzinę przyjazdu\n" +
                "• Sprawdzić czy wszystko jest OK\n\n" +
                "**KOLUMNY W TABELI:**\n" +
                "• LP - Numer wstawienia\n" +
                "• Data - Data wstawienia kurczaków\n" +
                "• Hodowca - Do kogo dzwonić\n" +
                "• Ilość - Ile kurczaków wstawiono\n" +
                "• Tel - Numer telefonu hodowcy\n\n" +
                "**CO MOŻESZ ZROBIĆ:**\n" +
                "• Kliknij wiersz 2 razy - otworzy się okno z możliwością dodania nowego wstawienia\n" +
                "• PPM (prawy przycisk myszy) - opcje kontaktu:\n\n" +
                "📵 Nie odebrał (+3 dni)\n" +
                "   Wybierz to gdy hodowca nie odbiera telefonu.\n" +
                "   Przypomnienie wróci za 3 dni.\n\n" +
                "📞 3 próby telefonu (+1 miesiąc)\n" +
                "   Wybierz to gdy dzwoniłeś 3 razy i nikt nie odbiera.\n" +
                "   Przypomnienie wróci za miesiąc.\n\n" +
                "🕐 Odłożenie na dłużej...\n" +
                "   Wybierz to gdy chcesz sam ustalić kiedy przypomnieć.\n" +
                "   Możesz wybrać konkretną datę lub wpisać ile miesięcy.\n" +
                "   Możesz też dodać notatkę (np. \"Hodowca na wakacjach\").\n\n" +
                "➕ Dodanie numeru hodowcy\n" +
                "   Wybierz to gdy hodowca nie ma telefonu w systemie.\n" +
                "   Wpiszesz numer i zapisze się w bazie.\n\n" +
                "**WAŻNE:**\n" +
                "Gdy zadzwonisz do hodowcy i wszystko jest OK, odłóż przypomnienie!\n" +
                "Wtedy zniknie z listy i nie będzie Ci przeszkadzać.");

            // Sekcja 6: Historia Kontaktów
            AddInstrukcjaSection(stackPanel, "📜 Historia Kontaktów - Tabela po prawej stronie",
                "**CO TO JEST:**\n" +
                "Historia wszystkich kontaktów z hodowcami - kto, kiedy i dlaczego dzwonił.\n\n" +
                "**KOLUMNY W TABELI:**\n" +
                "• Hodowca - Do kogo dzwoniono\n" +
                "• User - Kto dzwonił (pracownik)\n" +
                "• Nast. - Kiedy następne przypomnienie (format: MM-DD)\n" +
                "• Notatka - Powód odłożenia kontaktu (np. \"Nie odebrał\", \"Na wakacjach\")\n" +
                "• Dodano - Kiedy dodano ten wpis (format: MM-DD HH:MM)\n\n" +
                "**PO CO TO:**\n" +
                "• Widzisz kto próbował dzwonić do hodowcy\n" +
                "• Widzisz dlaczego kontakt został odłożony\n" +
                "• Możesz sprawdzić historię problemów z danym hodowcą");

            // Sekcja 7: Dodawanie Wstawienia
            AddInstrukcjaSection(stackPanel, "➕ Jak dodać nowe wstawienie?",
                "**KROK PO KROKU:**\n\n" +
                "1️⃣ Kliknij przycisk \"➕ Dodaj\" w lewym górnym rogu listy wstawień\n\n" +
                "2️⃣ Otworzy się okno - wypełnij wszystkie pola:\n" +
                "   • Wybierz hodowcę z listy\n" +
                "   • Wpisz datę wstawienia (kiedy kurczaki zostały umieszczone u hodowcy)\n" +
                "   • Wpisz ile sztuk kurczaków\n" +
                "   • Wybierz typ umowy (Wolny/Kontrakt)\n" +
                "   • Dodaj dostawy (ile razy i kiedy odbierać kurczaki)\n\n" +
                "3️⃣ Kliknij \"Zapisz\"\n\n" +
                "4️⃣ Gotowe! Wstawienie pojawi się na liście\n\n" +
                "**WAŻNE:**\n" +
                "Program automatycznie utworzy przypomnienie 3 dni przed pierwszą dostawą!");

            // Sekcja 8: Edycja Wstawienia
            AddInstrukcjaSection(stackPanel, "✏️ Jak edytować wstawienie?",
                "**METODA 1 - Prawy przycisk myszy:**\n" +
                "1. Kliknij prawym przyciskiem na wstawienie w lewej tabeli\n" +
                "2. Wybierz \"✏️ Edytuj wstawienie\"\n" +
                "3. Zmień co chcesz\n" +
                "4. Kliknij \"Zapisz\"\n\n" +
                "**METODA 2 - Podwójne kliknięcie:**\n" +
                "1. Kliknij 2 razy szybko na wstawienie w tabeli Przypomnienia\n" +
                "2. Zostaniesz zapytany czy chcesz skopiować dane z ostatniego wstawienia\n" +
                "3. Wybierz odpowiednią opcję\n" +
                "4. Wypełnij dane nowego wstawienia\n" +
                "5. Kliknij \"Zapisz\"\n\n" +
                "**CO MOŻESZ ZMIENIĆ:**\n" +
                "• Hodowcę\n" +
                "• Datę wstawienia\n" +
                "• Ilość kurczaków\n" +
                "• Typ umowy\n" +
                "• Dostawy (dodać, usunąć, zmienić daty)");

            // Sekcja 9: Zmiana Daty
            AddInstrukcjaSection(stackPanel, "📅 Jak zmienić datę wstawienia?",
                "**KROK PO KROKU:**\n\n" +
                "1️⃣ Kliknij prawym przyciskiem na wstawienie\n\n" +
                "2️⃣ Wybierz \"📅 Zmień datę wstawienia\"\n\n" +
                "3️⃣ Wybierz nową datę w kalendarzu\n\n" +
                "4️⃣ Program zapyta: \"Czy przesunąć też dostawy?\"\n" +
                "   • TAK - dostawy przesuną się o tyle samo dni co wstawienie\n" +
                "     Przykład: Przesuwasz wstawienie o 5 dni do przodu,\n" +
                "     to wszystkie dostawy też przesuwają się o 5 dni\n" +
                "   • NIE - dostawy zostaną na starych datach\n" +
                "   • ANULUJ - nic się nie zmieni\n\n" +
                "5️⃣ Potwierdź wybór\n\n" +
                "**KIEDY TO PRZYDATNE:**\n" +
                "Gdy hodowca pomylił datę lub trzeba przesunąć cały harmonogram.");

            // Sekcja 10: Usuwanie
            AddInstrukcjaSection(stackPanel, "🗑️ Jak usunąć wstawienie?",
                "**UWAGA - TO NIEODWRACALNE!**\n\n" +
                "1️⃣ Kliknij prawym przyciskiem na wstawienie\n\n" +
                "2️⃣ Wybierz \"🗑️ Usuń wstawienie\"\n\n" +
                "3️⃣ Potwierdź że na pewno chcesz usunąć\n\n" +
                "4️⃣ Program usunie:\n" +
                "   • Wstawienie\n" +
                "   • Wszystkie dostawy tego wstawienia\n" +
                "   • UWAGA: Nie da się tego cofnąć!\n\n" +
                "**KIEDY USUWAĆ:**\n" +
                "• Gdy wstawienie zostało dodane przez pomyłkę\n" +
                "• Gdy hodowca zrezygnował z wstawienia\n" +
                "• Gdy dane są całkowicie nieprawidłowe");

            // Sekcja 11: Szybkie porady
            AddInstrukcjaSection(stackPanel, "💡 Szybkie Porady i Wskazówki",
                "**CODZIENNE UŻYTKOWANIE:**\n\n" +
                "1️⃣ Rano sprawdź statystykę \"⏰ Przyp.\" - ile przypomnień masz na dziś\n\n" +
                "2️⃣ Przejrzyj tabelę Przypomnienia - zadzwoń do wszystkich hodowców\n\n" +
                "3️⃣ Po rozmowie z hodowcą:\n" +
                "   • Jeśli wszystko OK - odłóż przypomnienie odpowiednią opcją\n" +
                "   • Jeśli nie odbiera - użyj \"📵 Nie odebrał\"\n\n" +
                "4️⃣ Sprawdź listę wstawień - czy wszystkie dostawy są aktualne\n\n" +
                "**PRZYDATNE SKRÓTY:**\n\n" +
                "• Zaznacz \"📅 Tylko przyszłe\" raz w tygodniu aby sprawdzić\n" +
                "  czy nie zapomniałeś o jakimś hodowcy na przyszły rok\n\n" +
                "• Użyj \"📆 Data od\" aby skupić się tylko na aktualnych wstawieniach\n\n" +
                "• Patrz na kolory wierszy:\n" +
                "  🟢 Zielony = można zarchiwizować (wszystko odebrane)\n" +
                "  🟡 Żółty = aktywne (dostawy w trakcie)\n" +
                "  ⚪ Szary = przyszłe (kurczaki jeszcze rosną)\n\n" +
                "**SZYBKIE DODAWANIE WSTAWIENIA:**\n\n" +
                "• Kliknij 2 razy na wiersz w tabeli Przypomnienia\n" +
                "• Zostaniesz zapytany czy skopiować dane z ostatniego wstawienia\n" +
                "• To przyspiesza dodawanie kolejnych wstawień u tego samego hodowcy!\n\n" +
                "**JEŚLI COŚ NIE DZIAŁA:**\n\n" +
                "• Sprawdź czy masz połączenie z bazą danych\n" +
                "• Upewnij się że masz uprawnienia do dodawania/edycji\n" +
                "• Skontaktuj się z administratorem systemu");

            scrollViewer.Content = stackPanel;
            instrukcja.Content = scrollViewer;
            instrukcja.ShowDialog();
        }

        private void AddInstrukcjaSection(StackPanel parent, string title, string content)
        {
            var titleBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(92, 138, 58)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15, 10, 15, 10),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var titleText = new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };

            titleBorder.Child = titleText;
            parent.Children.Add(titleBorder);

            var contentBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1, 1, 1, 1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15, 15, 15, 15),
                Margin = new Thickness(0, 0, 0, 20)
            };

            var contentText = new TextBlock
            {
                Text = content,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80))
            };

            contentBorder.Child = contentText;
            parent.Children.Add(contentBorder);
        }

        // ====== STATYSTYKI ======
        private void UpdateStatistics()
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Dziś
                    string queryDzisiaj = @"
                        SELECT COUNT(*) 
                        FROM dbo.WstawieniaKurczakow 
                        WHERE CAST(DataUtw AS DATE) = CAST(GETDATE() AS DATE)";
                    using (var cmd = new SqlCommand(queryDzisiaj, connection))
                    {
                        txtStatDzisiaj.Text = cmd.ExecuteScalar().ToString();
                    }

                    // Tydzień
                    string queryTydzien = @"
                        SELECT COUNT(*) 
                        FROM dbo.WstawieniaKurczakow 
                        WHERE DATEPART(YEAR, DataUtw) = DATEPART(YEAR, GETDATE()) 
                        AND DATEPART(WEEK, DataUtw) = DATEPART(WEEK, GETDATE())";
                    using (var cmd = new SqlCommand(queryTydzien, connection))
                    {
                        txtStatTydzien.Text = cmd.ExecuteScalar().ToString();
                    }

                    // Ten miesiąc
                    string queryMiesiac = @"
                        SELECT COUNT(*) 
                        FROM dbo.WstawieniaKurczakow 
                        WHERE YEAR(DataUtw) = YEAR(GETDATE()) AND MONTH(DataUtw) = MONTH(GETDATE())";
                    using (var cmd = new SqlCommand(queryMiesiac, connection))
                    {
                        txtStatMiesiac.Text = cmd.ExecuteScalar().ToString();
                    }

                    // Ten rok
                    string queryRok = @"
                        SELECT COUNT(*) 
                        FROM dbo.WstawieniaKurczakow 
                        WHERE YEAR(DataUtw) = YEAR(GETDATE())";
                    using (var cmd = new SqlCommand(queryRok, connection))
                    {
                        txtStatRok.Text = cmd.ExecuteScalar().ToString();
                    }

                    // Przypomnienia
                    string queryPrzypomnienia = "SELECT COUNT(*) FROM dbo.v_WstawieniaDoKontaktu";
                    using (var cmd = new SqlCommand(queryPrzypomnienia, connection))
                    {
                        int przypomnienia = Convert.ToInt32(cmd.ExecuteScalar());
                        txtStatPrzypomnienia.Text = przypomnienia.ToString();
                        txtLiczbaPrzypomnien.Text = przypomnienia.ToString();
                    }

                    // Łącznie
                    string queryLacznie = "SELECT COUNT(*) FROM dbo.WstawieniaKurczakow";
                    using (var cmd = new SqlCommand(queryLacznie, connection))
                    {
                        txtStatLacznie.Text = cmd.ExecuteScalar().ToString();
                    }

                    // === DODATKOWE STATYSTYKI ===

                    // Średnia wstawień na dzień (ostatnie 30 dni)
                    string querySredniaDzien = @"
                        SELECT COUNT(*) * 1.0 / 30 
                        FROM dbo.WstawieniaKurczakow 
                        WHERE DataUtw >= DATEADD(DAY, -30, GETDATE())";
                    using (var cmd = new SqlCommand(querySredniaDzien, connection))
                    {
                        var result = cmd.ExecuteScalar();
                        double srednia = result != DBNull.Value ? Convert.ToDouble(result) : 0;
                        txtStatSredniaDzien.Text = srednia.ToString("0.0");
                    }

                    // Liczba unikalnych hodowców
                    string queryHodowcow = @"
                        SELECT COUNT(DISTINCT Dostawca) 
                        FROM dbo.WstawieniaKurczakow";
                    using (var cmd = new SqlCommand(queryHodowcow, connection))
                    {
                        txtStatHodowcow.Text = cmd.ExecuteScalar().ToString();
                    }

                    // Średnia sztuk na wstawienie
                    string querySredniaSztuk = @"
                        SELECT AVG(CAST(IloscWstawienia AS FLOAT)) 
                        FROM dbo.WstawieniaKurczakow 
                        WHERE IloscWstawienia > 0";
                    using (var cmd = new SqlCommand(querySredniaSztuk, connection))
                    {
                        var result = cmd.ExecuteScalar();
                        double srednia = result != DBNull.Value ? Convert.ToDouble(result) : 0;
                        txtStatSredniaSztuk.Text = FormatLiczba((long)srednia);
                    }

                    // Największe wstawienie
                    string queryNajwieksze = @"
                        SELECT ISNULL(MAX(IloscWstawienia), 0) 
                        FROM dbo.WstawieniaKurczakow";
                    using (var cmd = new SqlCommand(queryNajwieksze, connection))
                    {
                        long najwieksze = Convert.ToInt64(cmd.ExecuteScalar());
                        txtStatNajwieksze.Text = FormatLiczba(najwieksze);
                    }

                    // Trend - porównanie ostatnich 2 tygodni
                    string queryTrend = @"
                        SELECT 
                            (SELECT COUNT(*) FROM dbo.WstawieniaKurczakow 
                             WHERE DataUtw >= DATEADD(DAY, -7, GETDATE())) AS LastWeek,
                            (SELECT COUNT(*) FROM dbo.WstawieniaKurczakow 
                             WHERE DataUtw >= DATEADD(DAY, -14, GETDATE()) 
                             AND DataUtw < DATEADD(DAY, -7, GETDATE())) AS PreviousWeek";
                    using (var cmd = new SqlCommand(queryTrend, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int lastWeek = reader.GetInt32(0);
                            int previousWeek = reader.GetInt32(1);

                            if (previousWeek == 0)
                            {
                                txtStatTrend.Text = lastWeek > 0 ? "📈 ↗️" : "➡️";
                            }
                            else
                            {
                                double change = ((lastWeek - previousWeek) * 100.0) / previousWeek;
                                if (change > 10)
                                    txtStatTrend.Text = "📈 ↗️";
                                else if (change < -10)
                                    txtStatTrend.Text = "📉 ↘️";
                                else
                                    txtStatTrend.Text = "➡️";
                            }
                        }
                    }

                    // Wolny vs Kontrakt
                    string queryWolnyKontrakt = @"
                        SELECT 
                            SUM(CASE WHEN TypUmowy LIKE '%Wolny%' OR TypUmowy LIKE '%W.Woln%' THEN 1 ELSE 0 END) AS Wolny,
                            SUM(CASE WHEN TypUmowy LIKE '%Kontrakt%' THEN 1 ELSE 0 END) AS Kontrakt
                        FROM dbo.WstawieniaKurczakow 
                        WHERE YEAR(DataUtw) = YEAR(GETDATE())";
                    using (var cmd = new SqlCommand(queryWolnyKontrakt, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int wolny = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                            int kontrakt = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                            txtStatWolnyKontrakt.Text = $"{wolny}/{kontrakt}";
                        }
                    }

                    // TOP Hodowca (miesiąc)
                    string queryTopHodowca = @"
                        SELECT TOP 1 Dostawca, COUNT(*) AS Cnt
                        FROM dbo.WstawieniaKurczakow 
                        WHERE YEAR(DataUtw) = YEAR(GETDATE()) 
                        AND MONTH(DataUtw) = MONTH(GETDATE())
                        GROUP BY Dostawca
                        ORDER BY Cnt DESC";
                    using (var cmd = new SqlCommand(queryTopHodowca, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string hodowca = reader.GetString(0);
                            int count = reader.GetInt32(1);
                            if (hodowca.Length > 15)
                                hodowca = hodowca.Substring(0, 12) + "...";
                            txtStatTopHodowca.Text = $"{hodowca} ({count})";
                        }
                        else
                        {
                            txtStatTopHodowca.Text = "-";
                        }
                    }

                    // Ostatnie 7 dni
                    string queryOstatnie7Dni = @"
                        SELECT COUNT(*) 
                        FROM dbo.WstawieniaKurczakow 
                        WHERE DataUtw >= DATEADD(DAY, -7, GETDATE())";
                    using (var cmd = new SqlCommand(queryOstatnie7Dni, connection))
                    {
                        txtStatOstatnie7Dni.Text = cmd.ExecuteScalar().ToString();
                    }
                }

                // Statystyki per użytkownik
                LoadWstawieniaPerUser();
                LoadKontaktyPerUser();
                LoadPotwierdzaniaPerUser();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd statystyk: {ex.Message}");
            }
        }

        private void LoadWstawieniaPerUser()
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Tydzień
                    string queryTydzien = @"
                        SELECT 
                            ISNULL(o.Name, 'Nieznany') AS UserName,
                            COUNT(*) AS TotalCount
                        FROM dbo.WstawieniaKurczakow w
                        LEFT JOIN dbo.operators o ON w.KtoStwo = o.ID
                        WHERE DATEPART(YEAR, w.DataUtw) = DATEPART(YEAR, GETDATE()) 
                        AND DATEPART(WEEK, w.DataUtw) = DATEPART(WEEK, GETDATE())
                        GROUP BY o.Name
                        ORDER BY TotalCount DESC";

                    using (var cmd = new SqlCommand(queryTydzien, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var stats = new ObservableCollection<UserStat>();
                        while (reader.Read())
                        {
                            stats.Add(new UserStat
                            {
                                User = SkrocNazwisko(reader["UserName"].ToString()),
                                Count = Convert.ToInt32(reader["TotalCount"])
                            });
                        }
                        itemsWstawieniaPerUserTydzien.ItemsSource = stats;
                    }

                    // Miesiąc
                    string queryMiesiac = @"
                        SELECT 
                            ISNULL(o.Name, 'Nieznany') AS UserName,
                            COUNT(*) AS TotalCount
                        FROM dbo.WstawieniaKurczakow w
                        LEFT JOIN dbo.operators o ON w.KtoStwo = o.ID
                        WHERE YEAR(w.DataUtw) = YEAR(GETDATE()) AND MONTH(w.DataUtw) = MONTH(GETDATE())
                        GROUP BY o.Name
                        ORDER BY TotalCount DESC";

                    using (var cmd = new SqlCommand(queryMiesiac, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var stats = new ObservableCollection<UserStat>();
                        while (reader.Read())
                        {
                            stats.Add(new UserStat
                            {
                                User = SkrocNazwisko(reader["UserName"].ToString()),
                                Count = Convert.ToInt32(reader["TotalCount"])
                            });
                        }
                        itemsWstawieniaPerUserMiesiac.ItemsSource = stats;
                    }

                    // Rok
                    string queryRok = @"
                        SELECT 
                            ISNULL(o.Name, 'Nieznany') AS UserName,
                            COUNT(*) AS TotalCount
                        FROM dbo.WstawieniaKurczakow w
                        LEFT JOIN dbo.operators o ON w.KtoStwo = o.ID
                        WHERE YEAR(w.DataUtw) = YEAR(GETDATE())
                        GROUP BY o.Name
                        ORDER BY TotalCount DESC";

                    using (var cmd = new SqlCommand(queryRok, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var stats = new ObservableCollection<UserStat>();
                        while (reader.Read())
                        {
                            stats.Add(new UserStat
                            {
                                User = SkrocNazwisko(reader["UserName"].ToString()),
                                Count = Convert.ToInt32(reader["TotalCount"])
                            });
                        }
                        itemsWstawieniaPerUserRok.ItemsSource = stats;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd statystyk wstawień per user: {ex.Message}");
            }
        }

        private void LoadKontaktyPerUser()
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Tydzień
                    string queryTydzien = @"
                        SELECT 
                            ISNULL(o.Name, ch.UserID) AS UserName,
                            COUNT(*) AS TotalCount
                        FROM dbo.ContactHistory ch
                        LEFT JOIN dbo.operators o ON ch.UserID = o.ID
                        WHERE DATEPART(YEAR, ch.CreatedAt) = DATEPART(YEAR, GETDATE()) 
                        AND DATEPART(WEEK, ch.CreatedAt) = DATEPART(WEEK, GETDATE())
                        GROUP BY o.Name, ch.UserID
                        ORDER BY TotalCount DESC";

                    using (var cmd = new SqlCommand(queryTydzien, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var stats = new ObservableCollection<UserStat>();
                        while (reader.Read())
                        {
                            stats.Add(new UserStat
                            {
                                User = SkrocNazwisko(reader["UserName"].ToString()),
                                Count = Convert.ToInt32(reader["TotalCount"])
                            });
                        }
                        itemsKontaktyPerUserTydzien.ItemsSource = stats;
                    }

                    // Miesiąc
                    string queryMiesiac = @"
                        SELECT 
                            ISNULL(o.Name, ch.UserID) AS UserName,
                            COUNT(*) AS TotalCount
                        FROM dbo.ContactHistory ch
                        LEFT JOIN dbo.operators o ON ch.UserID = o.ID
                        WHERE YEAR(ch.CreatedAt) = YEAR(GETDATE()) AND MONTH(ch.CreatedAt) = MONTH(GETDATE())
                        GROUP BY o.Name, ch.UserID
                        ORDER BY TotalCount DESC";

                    using (var cmd = new SqlCommand(queryMiesiac, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var stats = new ObservableCollection<UserStat>();
                        while (reader.Read())
                        {
                            stats.Add(new UserStat
                            {
                                User = SkrocNazwisko(reader["UserName"].ToString()),
                                Count = Convert.ToInt32(reader["TotalCount"])
                            });
                        }
                        itemsKontaktyPerUserMiesiac.ItemsSource = stats;
                    }

                    // Rok
                    string queryRok = @"
                        SELECT 
                            ISNULL(o.Name, ch.UserID) AS UserName,
                            COUNT(*) AS TotalCount
                        FROM dbo.ContactHistory ch
                        LEFT JOIN dbo.operators o ON ch.UserID = o.ID
                        WHERE YEAR(ch.CreatedAt) = YEAR(GETDATE())
                        GROUP BY o.Name, ch.UserID
                        ORDER BY TotalCount DESC";

                    using (var cmd = new SqlCommand(queryRok, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var stats = new ObservableCollection<UserStat>();
                        while (reader.Read())
                        {
                            stats.Add(new UserStat
                            {
                                User = SkrocNazwisko(reader["UserName"].ToString()),
                                Count = Convert.ToInt32(reader["TotalCount"])
                            });
                        }
                        itemsKontaktyPerUserRok.ItemsSource = stats;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd statystyk kontaktów per user: {ex.Message}");
            }
        }

        private void LoadPotwierdzaniaPerUser()
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Tydzień
                    string queryTydzien = @"
                        SELECT 
                            ISNULL(o.Name, 'Nieznany') AS UserName,
                            COUNT(*) AS TotalCount
                        FROM dbo.WstawieniaKurczakow w
                        LEFT JOIN dbo.operators o ON w.KtoConf = o.ID
                        WHERE w.isConf = 1 
                        AND DATEPART(YEAR, w.DataUtw) = DATEPART(YEAR, GETDATE()) 
                        AND DATEPART(WEEK, w.DataUtw) = DATEPART(WEEK, GETDATE())
                        GROUP BY o.Name
                        ORDER BY TotalCount DESC";

                    using (var cmd = new SqlCommand(queryTydzien, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var stats = new ObservableCollection<UserStat>();
                        while (reader.Read())
                        {
                            stats.Add(new UserStat
                            {
                                User = SkrocNazwisko(reader["UserName"].ToString()),
                                Count = Convert.ToInt32(reader["TotalCount"])
                            });
                        }
                        itemsPotwierdzaniaPerUserTydzien.ItemsSource = stats;
                    }

                    // Miesiąc
                    string queryMiesiac = @"
                        SELECT 
                            ISNULL(o.Name, 'Nieznany') AS UserName,
                            COUNT(*) AS TotalCount
                        FROM dbo.WstawieniaKurczakow w
                        LEFT JOIN dbo.operators o ON w.KtoConf = o.ID
                        WHERE w.isConf = 1 
                        AND YEAR(w.DataUtw) = YEAR(GETDATE()) 
                        AND MONTH(w.DataUtw) = MONTH(GETDATE())
                        GROUP BY o.Name
                        ORDER BY TotalCount DESC";

                    using (var cmd = new SqlCommand(queryMiesiac, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var stats = new ObservableCollection<UserStat>();
                        while (reader.Read())
                        {
                            stats.Add(new UserStat
                            {
                                User = SkrocNazwisko(reader["UserName"].ToString()),
                                Count = Convert.ToInt32(reader["TotalCount"])
                            });
                        }
                        itemsPotwierdzaniaPerUserMiesiac.ItemsSource = stats;
                    }

                    // Rok
                    string queryRok = @"
                        SELECT 
                            ISNULL(o.Name, 'Nieznany') AS UserName,
                            COUNT(*) AS TotalCount
                        FROM dbo.WstawieniaKurczakow w
                        LEFT JOIN dbo.operators o ON w.KtoConf = o.ID
                        WHERE w.isConf = 1 
                        AND YEAR(w.DataUtw) = YEAR(GETDATE())
                        GROUP BY o.Name
                        ORDER BY TotalCount DESC";

                    using (var cmd = new SqlCommand(queryRok, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var stats = new ObservableCollection<UserStat>();
                        while (reader.Read())
                        {
                            stats.Add(new UserStat
                            {
                                User = SkrocNazwisko(reader["UserName"].ToString()),
                                Count = Convert.ToInt32(reader["TotalCount"])
                            });
                        }
                        itemsPotwierdzaniaPerUserRok.ItemsSource = stats;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd statystyk potwierdzeń per user: {ex.Message}");
            }
        }

        private string FormatLiczba(long liczba)
        {
            return liczba.ToString("# ##0").Trim();
        }

        private string SkrocNazwisko(string pelneImie)
        {
            if (string.IsNullOrWhiteSpace(pelneImie))
                return pelneImie;

            var czesci = pelneImie.Trim().Split(' ');
            if (czesci.Length <= 1)
                return pelneImie;

            var wynik = czesci[0];
            for (int i = 1; i < czesci.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(czesci[i]))
                {
                    wynik += " " + czesci[i][0].ToString().ToUpper() + ".";
                }
            }
            return wynik;
        }

        // ====== ŁADOWANIE DANYCH ======
        private void LoadWstawienia()
        {
            string query = @"
                SELECT W.LP, W.Dostawca,
                       CONVERT(varchar, W.DataWstawienia, 23) AS Data,
                       W.IloscWstawienia, W.TypUmowy,
                       ISNULL(W.TypCeny, '-') AS TypCeny,
                       ISNULL(O.Name, '-') AS KtoStwo,
                       CAST(W.KtoStwo AS VARCHAR(20)) AS KtoStwoID,
                       CONVERT(varchar, W.DataUtw, 120) AS DataUtw,
                       W.[isCheck],
                       W.[isConf]
                FROM dbo.WstawieniaKurczakow W
                LEFT JOIN dbo.operators O ON W.KtoStwo = O.ID
                ORDER BY W.LP DESC, W.DataWstawienia DESC";

            using (var connection = new SqlConnection(connectionString))
            using (var adapter = new SqlDataAdapter(query, connection))
            {
                var table = new DataTable();
                adapter.Fill(table);

                foreach (DataRow row in table.Rows)
                {
                    if (row["KtoStwo"] != DBNull.Value && row["KtoStwo"].ToString() != "-")
                    {
                        row["KtoStwo"] = SkrocNazwisko(row["KtoStwo"].ToString());
                    }
                }

                dataGridWstawienia.ItemsSource = table.DefaultView;
                SetupWstawieniaColumns();
                ApplySupplierGroupingColors();
            }
        }

        private void SetupWstawieniaColumns()
        {
            dataGridWstawienia.Columns.Clear();

            dataGridWstawienia.Columns.Add(new DataGridTextColumn
            {
                Header = "LP",
                Binding = new System.Windows.Data.Binding("LP"),
                Width = 48
            });

            dataGridWstawienia.Columns.Add(new DataGridTextColumn
            {
                Header = "Hodowca",
                Binding = new System.Windows.Data.Binding("Dostawca"),
                Width = 110
            });

            dataGridWstawienia.Columns.Add(new DataGridTextColumn
            {
                Header = "Data",
                Binding = new System.Windows.Data.Binding("Data")
                {
                    StringFormat = "yyyy-MM-dd ddd"
                },
                Width = 100
            });

            dataGridWstawienia.Columns.Add(new DataGridTextColumn
            {
                Header = "Ilość",
                Binding = new System.Windows.Data.Binding("IloscWstawienia")
                {
                    StringFormat = "# ##0"
                },
                Width = 65
            });

            dataGridWstawienia.Columns.Add(new DataGridTextColumn
            {
                Header = "Typ",
                Binding = new System.Windows.Data.Binding("TypUmowy"),
                Width = 70
            });

            // Kolumna Typ Ceny z kolorowaniem
            var typCenyColumn = new DataGridTemplateColumn
            {
                Header = "Cena",
                Width = 85
            };

            var cellTemplate = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("TypCeny")
            {
                Converter = new TypCenyToColorConverter()
            });
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            factory.SetValue(Border.PaddingProperty, new Thickness(4, 2, 4, 2));
            factory.SetValue(Border.MarginProperty, new Thickness(1));

            var textFactory = new FrameworkElementFactory(typeof(TextBlock));
            textFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("TypCeny"));
            textFactory.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("TypCeny")
            {
                Converter = new TypCenyToForegroundConverter()
            });
            textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            textFactory.SetValue(TextBlock.FontSizeProperty, 10.0);
            textFactory.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            textFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);

            factory.AppendChild(textFactory);
            cellTemplate.VisualTree = factory;
            typCenyColumn.CellTemplate = cellTemplate;
            dataGridWstawienia.Columns.Add(typCenyColumn);

            // Kolumna "Kto" z avatarem
            var ktoColumn = new DataGridTemplateColumn
            {
                Header = "Kto",
                Width = 90
            };

            var ktoCellTemplate = new DataTemplate();
            var stackFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            stackFactory.SetValue(StackPanel.MarginProperty, new Thickness(2, 0, 2, 0));

            // Avatar Grid
            var gridFactory = new FrameworkElementFactory(typeof(Grid));
            gridFactory.SetValue(Grid.WidthProperty, 20.0);
            gridFactory.SetValue(Grid.HeightProperty, 20.0);
            gridFactory.SetValue(Grid.MarginProperty, new Thickness(0, 0, 4, 0));

            // Border z inicjałami (fallback)
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.WidthProperty, 20.0);
            borderFactory.SetValue(Border.HeightProperty, 20.0);
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(92, 138, 58))); // #5C8A3A
            borderFactory.SetValue(FrameworkElement.NameProperty, "avatarBorderWstawienia");

            var initialsFactory = new FrameworkElementFactory(typeof(TextBlock));
            initialsFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("KtoStwo") { Converter = new InitialsConverter() });
            initialsFactory.SetValue(TextBlock.FontSizeProperty, 8.0);
            initialsFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            initialsFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            initialsFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            initialsFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(initialsFactory);
            gridFactory.AppendChild(borderFactory);

            // Ellipse dla obrazka avatara
            var ellipseFactory = new FrameworkElementFactory(typeof(Ellipse));
            ellipseFactory.SetValue(Ellipse.WidthProperty, 20.0);
            ellipseFactory.SetValue(Ellipse.HeightProperty, 20.0);
            ellipseFactory.SetValue(Ellipse.VisibilityProperty, Visibility.Collapsed);
            ellipseFactory.SetValue(FrameworkElement.NameProperty, "avatarImageWstawienia");
            gridFactory.AppendChild(ellipseFactory);

            stackFactory.AppendChild(gridFactory);

            // TextBlock z nazwiskiem
            var nameFactory = new FrameworkElementFactory(typeof(TextBlock));
            nameFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("KtoStwo"));
            nameFactory.SetValue(TextBlock.FontSizeProperty, 9.0);
            nameFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(100, 116, 139))); // #64748B
            nameFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            stackFactory.AppendChild(nameFactory);

            ktoCellTemplate.VisualTree = stackFactory;
            ktoColumn.CellTemplate = ktoCellTemplate;
            dataGridWstawienia.Columns.Add(ktoColumn);

            // Kolumna Data i Godzina Utworzenia
            dataGridWstawienia.Columns.Add(new DataGridTextColumn
            {
                Header = "Utworzono",
                Binding = new System.Windows.Data.Binding("DataUtw"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            // Kolumna Potw. - znaczek ✓ (zielony i pogrubiony)
            var potwColumn = new DataGridTemplateColumn
            {
                Header = "✓",
                Width = 30
            };

            var potwCellTemplate = new DataTemplate();
            var potwTextFactory = new FrameworkElementFactory(typeof(TextBlock));
            potwTextFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("isConf")
            {
                Converter = new IsConfConverter()
            });
            potwTextFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0, 150, 0)));
            potwTextFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            potwTextFactory.SetValue(TextBlock.FontSizeProperty, 14.0);
            potwTextFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            potwTextFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            potwCellTemplate.VisualTree = potwTextFactory;
            potwColumn.CellTemplate = potwCellTemplate;
            dataGridWstawienia.Columns.Add(potwColumn);
        }

        private void ApplySupplierGroupingColors()
        {
            dataGridWstawienia.LoadingRow += (s, e) =>
            {
                var row = e.Row.Item as DataRowView;
                if (row != null && row["LP"] != DBNull.Value)
                {
                    int lp = Convert.ToInt32(row["LP"]);

                    // Sprawdź status dostaw dla tego wstawienia
                    var deliveryStatus = GetDeliveryStatus(lp);

                    // Ustaw kolor w zależności od statusu dostaw
                    if (deliveryStatus == DeliveryStatus.AllPast)
                    {
                        // Mocniejszy zielony - wszystkie dostawy już minęły
                        e.Row.Background = new SolidColorBrush(Color.FromRgb(180, 240, 180));
                    }
                    else if (deliveryStatus == DeliveryStatus.Ongoing)
                    {
                        // Mocniejszy żółty - dostawy trwają
                        e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 245, 170));
                    }
                    else if (deliveryStatus == DeliveryStatus.AllFuture)
                    {
                        // Mocniejszy szary - wszystkie dostawy w przyszłości
                        e.Row.Background = new SolidColorBrush(Color.FromRgb(220, 220, 220));
                    }
                    else
                    {
                        // Brak dostaw - domyślny kolor (biały)
                        e.Row.Background = new SolidColorBrush(Colors.White);
                    }

                    // Ładowanie avatara
                    string ktoStwoID = row["KtoStwoID"]?.ToString();
                    if (!string.IsNullOrEmpty(ktoStwoID))
                    {
                        e.Row.Loaded += (rowSender, rowArgs) =>
                        {
                            try
                            {
                                LoadAvatarForRow(e.Row, ktoStwoID, "avatarImageWstawienia", "avatarBorderWstawienia");
                            }
                            catch { }
                        };
                    }

                    // Tooltip z pełnymi informacjami
                    var hodowca = row["Dostawca"]?.ToString() ?? "-";
                    var data = row["Data"]?.ToString() ?? "-";
                    var ilosc = row["IloscWstawienia"] != DBNull.Value ? Convert.ToInt32(row["IloscWstawienia"]).ToString("# ##0") : "-";
                    var typUmowy = row["TypUmowy"]?.ToString() ?? "-";
                    var typCeny = row["TypCeny"]?.ToString() ?? "-";
                    var ktoStwo = row["KtoStwo"]?.ToString() ?? "-";
                    var dataUtw = row["DataUtw"]?.ToString() ?? "-";

                    var tooltipContent = new StackPanel { Margin = new Thickness(5) };

                    // Nagłówek - Hodowca
                    var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
                    headerPanel.Children.Add(new TextBlock { Text = "🐔 ", FontSize = 14 });
                    headerPanel.Children.Add(new TextBlock { Text = hodowca, FontWeight = FontWeights.Bold, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(92, 138, 58)) });
                    tooltipContent.Children.Add(headerPanel);

                    tooltipContent.Children.Add(new Separator { Margin = new Thickness(0, 2, 0, 5) });

                    // Dane podstawowe
                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    void AddInfoRow(int rowIndex, string label, string value, Brush color)
                    {
                        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        var labelTb = new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)), Margin = new Thickness(0, 1, 0, 1) };
                        Grid.SetRow(labelTb, rowIndex);
                        Grid.SetColumn(labelTb, 0);
                        grid.Children.Add(labelTb);

                        var valueTb = new TextBlock { Text = value, Foreground = color, Margin = new Thickness(0, 1, 0, 1) };
                        Grid.SetRow(valueTb, rowIndex);
                        Grid.SetColumn(valueTb, 2);
                        grid.Children.Add(valueTb);
                    }

                    AddInfoRow(0, "LP:", lp.ToString(), new SolidColorBrush(Color.FromRgb(44, 62, 80)));
                    AddInfoRow(1, "Data wstawienia:", data, new SolidColorBrush(Color.FromRgb(52, 152, 219)));
                    AddInfoRow(2, "Ilość:", ilosc, new SolidColorBrush(Color.FromRgb(46, 125, 50)));
                    AddInfoRow(3, "Typ umowy:", typUmowy, new SolidColorBrush(Color.FromRgb(142, 68, 173)));
                    AddInfoRow(4, "Typ ceny:", typCeny, new SolidColorBrush(Color.FromRgb(230, 126, 34)));
                    AddInfoRow(5, "Utworzył:", ktoStwo, new SolidColorBrush(Color.FromRgb(100, 116, 139)));
                    AddInfoRow(6, "Data utworzenia:", dataUtw, new SolidColorBrush(Color.FromRgb(149, 165, 166)));

                    tooltipContent.Children.Add(grid);

                    // Dostawy
                    var deliveries = GetDeliveryDetails(lp);
                    if (deliveries.Count > 0)
                    {
                        tooltipContent.Children.Add(new Separator { Margin = new Thickness(0, 5, 0, 5) });

                        var deliveryHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
                        deliveryHeader.Children.Add(new TextBlock { Text = "📦 Zaplanowane dostawy:", FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(92, 138, 58)) });
                        tooltipContent.Children.Add(deliveryHeader);

                        // Nagłówek tabeli
                        var headerGrid = new Grid { Margin = new Thickness(5, 0, 0, 3) };
                        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });  // Icon
                        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) }); // Data
                        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) }); // A
                        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) }); // Szt
                        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) }); // Waga
                        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); // Cena
                        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); // Dni
                        headerGrid.RowDefinitions.Add(new RowDefinition());

                        var headerColor = new SolidColorBrush(Color.FromRgb(127, 140, 141));
                        var headers = new[] { "", "Data", "A", "Szt", "Waga", "Cena", "Dni" };
                        for (int i = 0; i < headers.Length; i++)
                        {
                            var tb = new TextBlock { Text = headers[i], FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = headerColor };
                            Grid.SetColumn(tb, i);
                            headerGrid.Children.Add(tb);
                        }
                        tooltipContent.Children.Add(headerGrid);

                        var today = DateTime.Today;
                        foreach (var delivery in deliveries)
                        {
                            var rowGrid = new Grid { Margin = new Thickness(5, 1, 0, 1) };
                            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
                            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
                            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) });
                            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
                            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
                            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                            rowGrid.RowDefinitions.Add(new RowDefinition());

                            var isPast = delivery.DataOdbioru.Date < today;
                            var isToday = delivery.DataOdbioru.Date == today;

                            var dateColor = isPast ? new SolidColorBrush(Color.FromRgb(46, 125, 50)) :
                                           isToday ? new SolidColorBrush(Color.FromRgb(230, 126, 34)) :
                                           new SolidColorBrush(Color.FromRgb(100, 116, 139));

                            var icon = isPast ? "✓" : isToday ? "▶" : "○";
                            var fontWeight = isToday ? FontWeights.Bold : FontWeights.Normal;

                            var values = new (string text, Brush color)[]
                            {
                                (icon, dateColor),
                                (delivery.DataOdbioru.ToString("MM-dd ddd"), dateColor),
                                (delivery.Auta.ToString(), new SolidColorBrush(Color.FromRgb(52, 152, 219))),
                                (delivery.SztukiDek.ToString("# ##0"), new SolidColorBrush(Color.FromRgb(46, 125, 50))),
                                (delivery.WagaDek.ToString("# ##0.00"), new SolidColorBrush(Color.FromRgb(142, 68, 173))),
                                (delivery.Cena.ToString("0.00"), new SolidColorBrush(Color.FromRgb(230, 126, 34))),
                                (delivery.RoznicaDni.ToString(), new SolidColorBrush(Color.FromRgb(127, 140, 141)))
                            };

                            for (int i = 0; i < values.Length; i++)
                            {
                                var tb = new TextBlock { Text = values[i].text, FontSize = 10, Foreground = values[i].color, FontWeight = fontWeight };
                                Grid.SetColumn(tb, i);
                                rowGrid.Children.Add(tb);
                            }
                            tooltipContent.Children.Add(rowGrid);
                        }

                        // Podsumowanie
                        var totalDeliveredSzt = deliveries.Where(d => d.DataOdbioru.Date < today).Sum(d => d.SztukiDek);
                        var totalPlannedSzt = deliveries.Where(d => d.DataOdbioru.Date >= today).Sum(d => d.SztukiDek);
                        var totalDeliveredWaga = deliveries.Where(d => d.DataOdbioru.Date < today).Sum(d => d.WagaDek);
                        var totalPlannedWaga = deliveries.Where(d => d.DataOdbioru.Date >= today).Sum(d => d.WagaDek);

                        tooltipContent.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 4) });
                        var summaryPanel = new StackPanel { Margin = new Thickness(5, 0, 0, 0) };
                        var summaryRow1 = new StackPanel { Orientation = Orientation.Horizontal };
                        summaryRow1.Children.Add(new TextBlock { Text = $"✓ Odebrano: {totalDeliveredSzt:# ##0} szt. ({totalDeliveredWaga:# ##0.00} kg)", Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50)), FontSize = 10, FontWeight = FontWeights.SemiBold });
                        summaryPanel.Children.Add(summaryRow1);
                        var summaryRow2 = new StackPanel { Orientation = Orientation.Horizontal };
                        summaryRow2.Children.Add(new TextBlock { Text = $"○ Pozostało: {totalPlannedSzt:# ##0} szt. ({totalPlannedWaga:# ##0.00} kg)", Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), FontSize = 10 });
                        summaryPanel.Children.Add(summaryRow2);
                        tooltipContent.Children.Add(summaryPanel);
                    }
                    else
                    {
                        tooltipContent.Children.Add(new Separator { Margin = new Thickness(0, 5, 0, 5) });
                        tooltipContent.Children.Add(new TextBlock { Text = "📦 Brak zaplanowanych dostaw", FontStyle = FontStyles.Italic, Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)) });
                    }

                    var tooltip = new ToolTip
                    {
                        Content = tooltipContent,
                        Background = new SolidColorBrush(Colors.White),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(92, 138, 58)),
                        BorderThickness = new Thickness(2),
                        Padding = new Thickness(8),
                        StaysOpen = true
                    };

                    e.Row.ToolTip = tooltip;
                    ToolTipService.SetInitialShowDelay(e.Row, 50);
                    ToolTipService.SetShowDuration(e.Row, 30000);

                    // Kliknięcie na wiersz też pokazuje tooltip
                    e.Row.MouseLeftButtonUp += (rowSender, rowArgs) =>
                    {
                        if (e.Row.ToolTip is ToolTip tt)
                        {
                            // Zamknij poprzedni tooltip jeśli istnieje
                            if (_currentOpenTooltip != null && _currentOpenTooltip != tt)
                            {
                                _currentOpenTooltip.IsOpen = false;
                            }
                            tt.IsOpen = true;
                            _currentOpenTooltip = tt;
                        }
                    };

                    // Zamknij tooltip gdy stracił focus
                    tooltip.Closed += (s, args) =>
                    {
                        if (_currentOpenTooltip == tooltip)
                        {
                            _currentOpenTooltip = null;
                        }
                    };
                }
            };
        }

        private void LoadAvatarForRow(DataGridRow row, string userId, string imageName, string borderName)
        {
            if (string.IsNullOrEmpty(userId)) return;

            try
            {
                if (UserAvatarManager.HasAvatar(userId))
                {
                    var avatarImage = FindVisualChild<Ellipse>(row, imageName);
                    var avatarBorder = FindVisualChild<Border>(row, borderName);

                    if (avatarImage != null && avatarBorder != null)
                    {
                        using (var avatar = UserAvatarManager.GetAvatarRounded(userId, 40))
                        {
                            if (avatar != null)
                            {
                                var brush = new ImageBrush(ConvertToImageSource(avatar));
                                brush.Stretch = Stretch.UniformToFill;
                                avatarImage.Fill = brush;
                                avatarImage.Visibility = Visibility.Visible;
                                avatarBorder.Visibility = Visibility.Collapsed;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private T FindVisualChild<T>(DependencyObject parent, string name = null) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    if (name == null || (child is FrameworkElement fe && fe.Name == name))
                        return typedChild;
                }

                var found = FindVisualChild<T>(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private ImageSource ConvertToImageSource(System.Drawing.Image image)
        {
            using (var memory = new MemoryStream())
            {
                image.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
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

        private enum DeliveryStatus
        {
            NoDeliveries,
            AllPast,
            Ongoing,
            AllFuture
        }

        private DeliveryStatus GetDeliveryStatus(int lpWstawienia)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT DataOdbioru 
                        FROM dbo.HarmonogramDostaw 
                        WHERE LpW = @LP 
                        ORDER BY DataOdbioru";

                    using (var cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@LP", lpWstawienia);

                        var deliveryDates = new List<DateTime>();
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (!reader.IsDBNull(0))
                                {
                                    deliveryDates.Add(reader.GetDateTime(0));
                                }
                            }
                        }

                        if (deliveryDates.Count == 0)
                        {
                            return DeliveryStatus.NoDeliveries;
                        }

                        DateTime today = DateTime.Today;
                        bool hasPast = deliveryDates.Any(d => d.Date < today);
                        bool hasFuture = deliveryDates.Any(d => d.Date >= today);

                        if (hasPast && hasFuture)
                        {
                            return DeliveryStatus.Ongoing;
                        }
                        else if (hasPast && !hasFuture)
                        {
                            return DeliveryStatus.AllPast;
                        }
                        else
                        {
                            return DeliveryStatus.AllFuture;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd sprawdzania statusu dostaw: {ex.Message}");
                return DeliveryStatus.NoDeliveries;
            }
        }

        private List<DeliveryInfo> GetDeliveryDetails(int lpWstawienia)
        {
            // Użyj cache jeśli załadowany
            if (_deliveryCacheLoaded && _deliveryCache.ContainsKey(lpWstawienia))
            {
                return _deliveryCache[lpWstawienia];
            }
            return new List<DeliveryInfo>();
        }

        private void LoadPrzypomnienia()
        {
            string query = @"
                SELECT 
                    v.LP,
                    CAST(v.DataWstawienia AS date) AS Data,
                    v.Dostawca,
                    v.IloscWstawienia AS Ilosc,
                    d.Phone1 AS Telefon
                FROM dbo.v_WstawieniaDoKontaktu AS v
                LEFT JOIN [LibraNet].[dbo].[Dostawcy] AS d
                       ON d.ShortName = v.Dostawca
                ORDER BY Data DESC, Dostawca";

            using (var connection = new SqlConnection(connectionString))
            using (var adapter = new SqlDataAdapter(query, connection))
            {
                var table = new DataTable();
                adapter.Fill(table);

                dataGridPrzypomnienia.ItemsSource = table.DefaultView;
                SetupPrzypomieniaColumns();
            }
        }

        private void SetupPrzypomieniaColumns()
        {
            dataGridPrzypomnienia.Columns.Clear();

            // Kolumna LP z pulsacją
            var lpColumn = new DataGridTemplateColumn { Header = "LP", Width = 38 };
            lpColumn.CellTemplate = CreatePulsatingTextTemplate("LP", null);
            dataGridPrzypomnienia.Columns.Add(lpColumn);

            // Kolumna Data z pulsacją
            var dataColumn = new DataGridTemplateColumn { Header = "Data", Width = 70 };
            dataColumn.CellTemplate = CreatePulsatingTextTemplate("Data", "MM-dd ddd");
            dataGridPrzypomnienia.Columns.Add(dataColumn);

            // Kolumna Hodowca z pulsacją (trochę węższa na rzecz Tel)
            var hodowcaColumn = new DataGridTemplateColumn { Header = "Hodowca", Width = new DataGridLength(0.85, DataGridLengthUnitType.Star) };
            hodowcaColumn.CellTemplate = CreatePulsatingTextTemplate("Dostawca", null);
            dataGridPrzypomnienia.Columns.Add(hodowcaColumn);

            // Kolumna Ilość z pulsacją
            var iloscColumn = new DataGridTemplateColumn { Header = "Ilość", Width = 52 };
            iloscColumn.CellTemplate = CreatePulsatingTextTemplate("Ilosc", "# ##0");
            dataGridPrzypomnienia.Columns.Add(iloscColumn);

            // Kolumna Tel z pulsacją (szersza)
            var telColumn = new DataGridTemplateColumn { Header = "Tel", Width = 82 };
            telColumn.CellTemplate = CreatePulsatingTextTemplate("Telefon", null);
            dataGridPrzypomnienia.Columns.Add(telColumn);
        }

        private DataTemplate CreatePulsatingTextTemplate(string bindingPath, string stringFormat)
        {
            var template = new DataTemplate();
            var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));

            var binding = new System.Windows.Data.Binding(bindingPath);
            if (!string.IsNullOrEmpty(stringFormat))
                binding.StringFormat = stringFormat;
            textBlockFactory.SetBinding(TextBlock.TextProperty, binding);

            textBlockFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            textBlockFactory.AddHandler(FrameworkElement.LoadedEvent, new RoutedEventHandler(StartPulsatingAnimation));

            template.VisualTree = textBlockFactory;
            return template;
        }

        private void StartPulsatingAnimation(object sender, RoutedEventArgs e)
        {
            var textBlock = sender as TextBlock;
            if (textBlock != null)
            {
                // Animacja z klatkami kluczowymi: dłużej na 1.0, krótkie przejście do 0.7
                var animation = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames
                {
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
                };

                // Pozostaje na 1.0 przez 1.5 sekundy
                animation.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(1.0, TimeSpan.FromSeconds(0)));
                animation.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(1.0, TimeSpan.FromSeconds(1.5)));
                // Przejście do 0.7 przez 0.4 sekundy
                animation.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(0.7, TimeSpan.FromSeconds(1.9), new System.Windows.Media.Animation.SineEase()));
                // Powrót do 1.0 przez 0.4 sekundy
                animation.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.0, TimeSpan.FromSeconds(2.3), new System.Windows.Media.Animation.SineEase()));

                textBlock.BeginAnimation(TextBlock.OpacityProperty, animation);
            }
        }

        private void LoadHistoria()
        {
            string query = @"
                SELECT
                    ch.ContactID,
                    ch.Dostawca,
                    ISNULL(o.Name, ch.UserID) AS UserName,
                    CAST(ch.UserID AS VARCHAR(20)) AS UserID,
                    ch.SnoozedUntil,
                    ch.Reason,
                    ch.CreatedAt
                FROM dbo.ContactHistory ch
                LEFT JOIN dbo.operators o ON ch.UserID = o.ID
                ORDER BY
                    CASE WHEN ch.ContactDate IS NOT NULL THEN 0 ELSE 1 END,
                    ch.ContactDate DESC,
                    ch.CreatedAt DESC,
                    ch.ContactID DESC";

            using (var connection = new SqlConnection(connectionString))
            using (var adapter = new SqlDataAdapter(query, connection))
            {
                var table = new DataTable();
                adapter.Fill(table);

                foreach (DataRow row in table.Rows)
                {
                    if (row["UserName"] != DBNull.Value)
                    {
                        row["UserName"] = SkrocNazwisko(row["UserName"].ToString());
                    }
                }

                dataGridHistoria.ItemsSource = table.DefaultView;
                SetupHistoriaColumns();
            }
        }

        private void SetupHistoriaColumns()
        {
            dataGridHistoria.Columns.Clear();

            dataGridHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Hodowca",
                Binding = new System.Windows.Data.Binding("Dostawca"),
                Width = new DataGridLength(0.8, DataGridLengthUnitType.Star)
            });

            // User column with avatar
            var userTemplateColumn = new DataGridTemplateColumn
            {
                Header = "User",
                Width = 48
            };

            var userTemplate = new DataTemplate();
            var gridFactory = new FrameworkElementFactory(typeof(Grid));
            gridFactory.SetValue(Grid.WidthProperty, 22.0);
            gridFactory.SetValue(Grid.HeightProperty, 22.0);
            gridFactory.SetValue(Grid.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            gridFactory.SetValue(Grid.VerticalAlignmentProperty, VerticalAlignment.Center);
            gridFactory.SetValue(Grid.MarginProperty, new Thickness(2));

            // Background border with initials
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.WidthProperty, 22.0);
            borderFactory.SetValue(Border.HeightProperty, 22.0);
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(11));
            borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(99, 102, 241)));
            borderFactory.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            borderFactory.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);

            var initialsTextFactory = new FrameworkElementFactory(typeof(TextBlock));
            initialsTextFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            initialsTextFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            initialsTextFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            initialsTextFactory.SetValue(TextBlock.FontSizeProperty, 9.0);
            initialsTextFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            initialsTextFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("UserName")
            {
                Converter = new InitialsConverter()
            });

            borderFactory.AppendChild(initialsTextFactory);
            gridFactory.AppendChild(borderFactory);

            // Ellipse for photo (initially hidden, will be shown if photo loads)
            var ellipseFactory = new FrameworkElementFactory(typeof(Ellipse));
            ellipseFactory.SetValue(Ellipse.WidthProperty, 22.0);
            ellipseFactory.SetValue(Ellipse.HeightProperty, 22.0);
            ellipseFactory.SetValue(Ellipse.VisibilityProperty, Visibility.Collapsed);
            ellipseFactory.SetValue(Ellipse.NameProperty, "avatarEllipse");

            gridFactory.AppendChild(ellipseFactory);

            userTemplate.VisualTree = gridFactory;
            userTemplateColumn.CellTemplate = userTemplate;

            dataGridHistoria.Columns.Add(userTemplateColumn);

            dataGridHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Nast.",
                Binding = new System.Windows.Data.Binding("SnoozedUntil")
                {
                    StringFormat = "MM-dd"
                },
                Width = 55
            });

            dataGridHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Notatka",
                Binding = new System.Windows.Data.Binding("Reason"),
                Width = new DataGridLength(1.5, DataGridLengthUnitType.Star)
            });

            dataGridHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Dodano",
                Binding = new System.Windows.Data.Binding("CreatedAt")
                {
                    StringFormat = "MM-dd HH:mm"
                },
                Width = 95
            });

            // Menu kontekstowe dla historii kontaktów
            var contextMenu = new ContextMenu();

            var menuItemEdytuj = new MenuItem { Header = "✏️ Edytuj notatkę" };
            menuItemEdytuj.Click += MenuEdytujHistorie_Click;
            contextMenu.Items.Add(menuItemEdytuj);

            var menuItemUsun = new MenuItem { Header = "🗑️ Usuń wpis" };
            menuItemUsun.Click += MenuUsunHistorie_Click;
            contextMenu.Items.Add(menuItemUsun);

            dataGridHistoria.ContextMenu = contextMenu;

            // Podwójne kliknięcie - tworzenie nowego wstawienia
            dataGridHistoria.MouseDoubleClick += DataGridHistoria_MouseDoubleClick;

            // Event for loading avatars
            dataGridHistoria.LoadingRow += DataGridHistoria_LoadingRow;
        }

        private void DataGridHistoria_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.DataContext is DataRowView rowView)
            {
                string userId = rowView["UserID"]?.ToString();
                if (!string.IsNullOrEmpty(userId))
                {
                    LoadAvatarForHistoriaRow(e.Row, userId);
                }

                // Utwórz tooltip z pełnymi szczegółami
                var hodowca = rowView["Dostawca"]?.ToString() ?? "-";
                var userName = rowView["UserName"]?.ToString() ?? "-";
                var snoozedUntil = rowView["SnoozedUntil"];
                var reason = rowView["Reason"]?.ToString() ?? "-";
                var createdAt = rowView["CreatedAt"];

                var snoozedUntilStr = snoozedUntil != DBNull.Value && snoozedUntil != null
                    ? ((DateTime)snoozedUntil).ToString("dd.MM.yyyy")
                    : "-";
                var createdAtStr = createdAt != DBNull.Value && createdAt != null
                    ? ((DateTime)createdAt).ToString("dd.MM.yyyy HH:mm:ss")
                    : "-";

                // Utwórz sformatowany tooltip
                var tooltipContent = new StackPanel { Margin = new Thickness(5) };

                // Hodowca
                var hodowcaPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                hodowcaPanel.Children.Add(new TextBlock { Text = "🐔 Hodowca: ", FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(92, 138, 58)) });
                hodowcaPanel.Children.Add(new TextBlock { Text = hodowca, Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)) });
                tooltipContent.Children.Add(hodowcaPanel);

                // User
                var userPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                userPanel.Children.Add(new TextBlock { Text = "👤 Użytkownik: ", FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)) });
                userPanel.Children.Add(new TextBlock { Text = userName, Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)) });
                tooltipContent.Children.Add(userPanel);

                // Separator
                tooltipContent.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 4) });

                // Przesunięcie do
                var snoozedPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                snoozedPanel.Children.Add(new TextBlock { Text = "📅 Przesunięcie do: ", FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(243, 156, 18)) });
                snoozedPanel.Children.Add(new TextBlock { Text = snoozedUntilStr, Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)) });
                tooltipContent.Children.Add(snoozedPanel);

                // Dodano
                var createdPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                createdPanel.Children.Add(new TextBlock { Text = "🕐 Dodano: ", FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(155, 89, 182)) });
                createdPanel.Children.Add(new TextBlock { Text = createdAtStr, Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)) });
                tooltipContent.Children.Add(createdPanel);

                // Separator
                tooltipContent.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 4) });

                // Notatka - pełna treść
                var notatkaTitlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                notatkaTitlePanel.Children.Add(new TextBlock { Text = "📝 Notatka:", FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60)) });
                tooltipContent.Children.Add(notatkaTitlePanel);

                var notatkaText = new TextBlock
                {
                    Text = reason,
                    Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 350,
                    Margin = new Thickness(5, 2, 0, 0)
                };
                tooltipContent.Children.Add(notatkaText);

                var tooltip = new ToolTip
                {
                    Content = tooltipContent,
                    Background = new SolidColorBrush(Colors.White),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(155, 89, 182)),
                    BorderThickness = new Thickness(2),
                    Padding = new Thickness(8),
                    StaysOpen = true
                };

                e.Row.ToolTip = tooltip;
                ToolTipService.SetInitialShowDelay(e.Row, 50);
                ToolTipService.SetShowDuration(e.Row, 30000);

                // Kliknięcie na wiersz też pokazuje tooltip
                e.Row.MouseLeftButtonUp += (rowSender, rowArgs) =>
                {
                    if (e.Row.ToolTip is ToolTip tt)
                    {
                        // Zamknij poprzedni tooltip jeśli istnieje
                        if (_currentOpenTooltip != null && _currentOpenTooltip != tt)
                        {
                            _currentOpenTooltip.IsOpen = false;
                        }
                        tt.IsOpen = true;
                        _currentOpenTooltip = tt;
                    }
                };

                // Zamknij tooltip gdy stracił focus
                tooltip.Closed += (s, args) =>
                {
                    if (_currentOpenTooltip == tooltip)
                    {
                        _currentOpenTooltip = null;
                    }
                };
            }
        }

        private void LoadAvatarForHistoriaRow(DataGridRow row, string odbiorcaId)
        {
            Task.Run(() =>
            {
                var avatarBitmap = UserAvatarManager.GetAvatar(odbiorcaId);
                if (avatarBitmap != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var presenter = FindVisualChild<DataGridCellsPresenter>(row);
                            if (presenter != null)
                            {
                                // User column is at index 1
                                var cell = presenter.ItemContainerGenerator.ContainerFromIndex(1) as DataGridCell;
                                if (cell != null)
                                {
                                    var ellipse = FindVisualChild<Ellipse>(cell);
                                    if (ellipse != null && ellipse.Name == "avatarEllipse")
                                    {
                                        var imageSource = ConvertToImageSource(avatarBitmap);
                                        if (imageSource != null)
                                        {
                                            ellipse.Fill = new ImageBrush(imageSource) { Stretch = Stretch.UniformToFill };
                                            ellipse.Visibility = Visibility.Visible;
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    });
                }
            });
        }

        private void MenuDodajTelefonDoPotwierdzenia_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridDoPotwierdzenia.SelectedItem == null)
            {
                MessageBox.Show("Wybierz wstawienie z listy.", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var row = (DataRowView)dataGridDoPotwierdzenia.SelectedItem;
            string dostawca = Convert.ToString(row["Dostawca"]);

            // Pobierz obecne numery telefonów
            string phone1 = "", phone2 = "", phone3 = "";
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT ISNULL(Phone1, ''), ISNULL(Phone2, ''), ISNULL(Phone3, '') FROM dbo.Dostawcy WHERE ShortName = @Dostawca";
                    using (var cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Dostawca", dostawca);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                phone1 = reader.GetString(0);
                                phone2 = reader.GetString(1);
                                phone3 = reader.GetString(2);
                            }
                        }
                    }
                }
            }
            catch { }

            var dialogNumer = new OknoDodaniaNumeruDialog(dostawca, phone1, phone2, phone3);
            if (dialogNumer.ShowDialog() == true)
            {
                try
                {
                    using (var connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        string query = "UPDATE dbo.Dostawcy SET Phone1 = @Phone1, Phone2 = @Phone2, Phone3 = @Phone3 WHERE ShortName = @Dostawca";
                        using (var cmd = new SqlCommand(query, connection))
                        {
                            cmd.Parameters.AddWithValue("@Phone1", dialogNumer.NumerTelefonu ?? "");
                            cmd.Parameters.AddWithValue("@Phone2", dialogNumer.NumerTelefonu2 ?? "");
                            cmd.Parameters.AddWithValue("@Phone3", dialogNumer.NumerTelefonu3 ?? "");
                            cmd.Parameters.AddWithValue("@Dostawca", dostawca);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    var zapisaneNumery = new List<string>();
                    if (!string.IsNullOrEmpty(dialogNumer.NumerTelefonu)) zapisaneNumery.Add(dialogNumer.NumerTelefonu);
                    if (!string.IsNullOrEmpty(dialogNumer.NumerTelefonu2)) zapisaneNumery.Add(dialogNumer.NumerTelefonu2);
                    if (!string.IsNullOrEmpty(dialogNumer.NumerTelefonu3)) zapisaneNumery.Add(dialogNumer.NumerTelefonu3);

                    MessageBox.Show($"Zapisano numery telefonu:\n{string.Join("\n", zapisaneNumery)}",
                        "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    LoadDoPotwierdzenia();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd zapisu: " + ex.Message, "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void LoadDoPotwierdzenia()
        {
            string query = @"
        SELECT 
            w.LP,
            w.Dostawca,
            w.DataWstawienia,
            w.IloscWstawienia,
            ISNULL(d.Phone1, '-') AS Telefon
        FROM dbo.WstawieniaKurczakow w
        LEFT JOIN dbo.Dostawcy d ON d.ShortName = w.Dostawca
        WHERE (w.isConf IS NULL OR w.isConf = 0)
          AND w.DataWstawienia >= DATEADD(day, -30, CAST(GETDATE() AS DATE))
          AND w.DataWstawienia <= DATEADD(day, 30, CAST(GETDATE() AS DATE))
        ORDER BY w.DataWstawienia ASC";

            using (var connection = new SqlConnection(connectionString))
            using (var adapter = new SqlDataAdapter(query, connection))
            {
                var table = new DataTable();
                adapter.Fill(table);

                dataGridDoPotwierdzenia.ItemsSource = table.DefaultView;
                SetupDoPotwierdzeniaColumns();
            }
        }
        private void SetupDoPotwierdzeniaColumns()
        {
            dataGridDoPotwierdzenia.Columns.Clear();

            dataGridDoPotwierdzenia.Columns.Add(new DataGridTextColumn
            {
                Header = "LP",
                Binding = new Binding("LP"),
                Width = 38
            });

            dataGridDoPotwierdzenia.Columns.Add(new DataGridTextColumn
            {
                Header = "Data",
                Binding = new Binding("DataWstawienia")
                {
                    StringFormat = "MM-dd ddd"
                },
                Width = 70
            });

            dataGridDoPotwierdzenia.Columns.Add(new DataGridTextColumn
            {
                Header = "Hodowca",
                Binding = new Binding("Dostawca"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            dataGridDoPotwierdzenia.Columns.Add(new DataGridTextColumn
            {
                Header = "Ilość",
                Binding = new Binding("IloscWstawienia")
                {
                    StringFormat = "# ##0"
                },
                Width = 52
            });

            dataGridDoPotwierdzenia.Columns.Add(new DataGridTextColumn
            {
                Header = "Tel",
                Binding = new Binding("Telefon"),
                Width = 70
            });

            // Dodaj context menu
            var contextMenu = new ContextMenu();

            var menuItemPotwierdz = new MenuItem { Header = "✅ Potwierdź wstawienie", FontWeight = FontWeights.SemiBold };
            menuItemPotwierdz.Click += MenuPotwierdzWstawienie_Click;
            contextMenu.Items.Add(menuItemPotwierdz);

            var menuItemPotwierdzData = new MenuItem { Header = "📅 Potwierdź i zmień datę", FontWeight = FontWeights.SemiBold };
            menuItemPotwierdzData.Click += MenuPotwierdzIZmienDate_Click;
            contextMenu.Items.Add(menuItemPotwierdzData);

            contextMenu.Items.Add(new Separator());

            var menuItemDodajTel = new MenuItem { Header = "➕ Dodaj numer hodowcy" };
            menuItemDodajTel.Click += MenuDodajTelefonDoPotwierdzenia_Click;
            contextMenu.Items.Add(menuItemDodajTel);

            dataGridDoPotwierdzenia.ContextMenu = contextMenu;

            // Podwójne kliknięcie - tworzenie nowego wstawienia
            dataGridDoPotwierdzenia.MouseDoubleClick += DataGridDoPotwierdzenia_MouseDoubleClick;
        }

        // ====== OBSŁUGA ZDARZEŃ ======
        private void TextBoxFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ChkPokazPrzyszle_Changed(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void DatePickerOd_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }
        private void BtnStatystyki_Click(object sender, RoutedEventArgs e)
        {
            var statystykiWindow = new StatystykiPracownikow();
            statystykiWindow.ShowDialog();
        }
        private void ApplyFilters()
        {
            var view = dataGridWstawienia.ItemsSource as DataView;
            if (view != null)
            {
                var filters = new List<string>();

                // Filtr tekstowy
                string filterText = textBoxFilter.Text.Trim();
                if (!string.IsNullOrEmpty(filterText))
                {
                    filters.Add($"Dostawca LIKE '%{filterText}%'");
                }

                // Filtr daty od
                if (datePickerOd.SelectedDate.HasValue)
                {
                    string dateString = datePickerOd.SelectedDate.Value.ToString("yyyy-MM-dd");
                    filters.Add($"Data >= '{dateString}'");
                }

                // Filtr tylko przyszłe wstawienia (unikalni hodowcy z najwyższą datą)
                if (chkPokazPrzyszle.IsChecked == true)
                {
                    // Pobierz unikalnych hodowców z najwyższą datą >= aktualny rok
                    var uniqueSuppliers = GetUniqueSuppliersWithFutureDeliveries();
                    if (uniqueSuppliers.Any())
                    {
                        string suppliersFilter = string.Join(" OR ", uniqueSuppliers.Select(s => $"Dostawca = '{s.Replace("'", "''")}'"));
                        filters.Add($"({suppliersFilter})");
                    }
                    else
                    {
                        // Jeśli brak hodowców, pokaż pusty wynik
                        filters.Add("1 = 0");
                    }
                }

                view.RowFilter = filters.Count > 0 ? string.Join(" AND ", filters) : string.Empty;
            }
        }

        private List<string> GetUniqueSuppliersWithFutureDeliveries()
        {
            var suppliers = new List<string>();
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        WITH MaxDates AS (
                            SELECT 
                                Dostawca,
                                MAX(DataWstawienia) AS MaxData,
                                MAX(LP) AS MaxLP
                            FROM dbo.WstawieniaKurczakow
                            GROUP BY Dostawca
                        )
                        SELECT DISTINCT Dostawca
                        FROM MaxDates
                        ORDER BY Dostawca";

                    using (var cmd = new SqlCommand(query, connection))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                suppliers.Add(reader.GetString(0));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd pobierania unikalnych hodowców: {ex.Message}");
            }
            return suppliers;
        }

        private void DataGridWstawienia_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dataGridWstawienia.SelectedItem != null)
            {
                var row = (DataRowView)dataGridWstawienia.SelectedItem;
                if (row["LP"] != DBNull.Value)
                {
                    lpDostawa = row["LP"].ToString();
                }
            }
        }

        private void DataGridPrzypomnienia_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dataGridPrzypomnienia.SelectedItem != null)
            {
                var row = (DataRowView)dataGridPrzypomnienia.SelectedItem;
                if (row["LP"] != DBNull.Value)
                {
                    lpDostawa = row["LP"].ToString();
                }
            }
        }

        // ZMIANA: Nowa logika dla podwójnego kliknięcia - z dialogiem o kopiowaniu danych
        private void DataGridPrzypomnienia_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dataGridPrzypomnienia.SelectedItem != null)
            {
                var row = (DataRowView)dataGridPrzypomnienia.SelectedItem;
                if (row["LP"] == DBNull.Value) return;

                string dostawca = row["Dostawca"]?.ToString();
                int ilosc = row["Ilosc"] != DBNull.Value ? Convert.ToInt32(row["Ilosc"]) : 0;

                // Pobierz dane ostatniego dostarczonego
                var daneOstatniego = PobierzDaneOstatniegoDostarczonego(dostawca);

                // Dialog z pytaniem o kopiowanie danych
                var dialogKopiowania = new OknoKopiowaniaDanychDialog(dostawca, daneOstatniego);
                if (dialogKopiowania.ShowDialog() == true)
                {
                    var wstawienie = new WstawienieWindow
                    {
                        UserID = App.UserID
                    };

                    // Podstawowe dane
                    wstawienie.Dostawca = dostawca;
                    wstawienie.SztWstawienia = ilosc;

                    // Jeśli użytkownik chce skopiować dodatkowe dane
                    if (dialogKopiowania.KopiujDodatkoweDane)
                    {
                        if (daneOstatniego != null)
                        {
                            wstawienie.DaneOstatniegoDostarczonego = daneOstatniego;
                        }
                    }

                    wstawienie.ShowDialog();
                    RefreshAll();
                }
            }
        }

        private void DataGridWstawienia_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dataGridWstawienia.SelectedItem != null)
            {
                var row = (DataRowView)dataGridWstawienia.SelectedItem;
                if (row["LP"] == DBNull.Value) return;

                string dostawca = row["Dostawca"]?.ToString();
                int ilosc = row["IloscWstawienia"] != DBNull.Value ? Convert.ToInt32(row["IloscWstawienia"]) : 0;

                // Pobierz dane ostatniego dostarczonego
                var daneOstatniego = PobierzDaneOstatniegoDostarczonego(dostawca);

                // Dialog z pytaniem o kopiowanie danych
                var dialogKopiowania = new OknoKopiowaniaDanychDialog(dostawca, daneOstatniego);
                if (dialogKopiowania.ShowDialog() == true)
                {
                    var wstawienie = new WstawienieWindow
                    {
                        UserID = App.UserID
                    };

                    // Podstawowe dane
                    wstawienie.Dostawca = dostawca;
                    wstawienie.SztWstawienia = ilosc;

                    // Jeśli użytkownik chce skopiować dodatkowe dane
                    if (dialogKopiowania.KopiujDodatkoweDane)
                    {
                        if (daneOstatniego != null)
                        {
                            wstawienie.DaneOstatniegoDostarczonego = daneOstatniego;
                        }
                    }

                    wstawienie.ShowDialog();
                    RefreshAll();
                }
            }
        }

        private void DataGridDoPotwierdzenia_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dataGridDoPotwierdzenia.SelectedItem != null)
            {
                var row = (DataRowView)dataGridDoPotwierdzenia.SelectedItem;
                if (row["LP"] == DBNull.Value) return;

                string dostawca = row["Dostawca"]?.ToString();
                int ilosc = row["IloscWstawienia"] != DBNull.Value ? Convert.ToInt32(row["IloscWstawienia"]) : 0;

                // Pobierz dane ostatniego dostarczonego
                var daneOstatniego = PobierzDaneOstatniegoDostarczonego(dostawca);

                // Dialog z pytaniem o kopiowanie danych
                var dialogKopiowania = new OknoKopiowaniaDanychDialog(dostawca, daneOstatniego);
                if (dialogKopiowania.ShowDialog() == true)
                {
                    var wstawienie = new WstawienieWindow
                    {
                        UserID = App.UserID
                    };

                    // Podstawowe dane
                    wstawienie.Dostawca = dostawca;
                    wstawienie.SztWstawienia = ilosc;

                    // Jeśli użytkownik chce skopiować dodatkowe dane
                    if (dialogKopiowania.KopiujDodatkoweDane)
                    {
                        if (daneOstatniego != null)
                        {
                            wstawienie.DaneOstatniegoDostarczonego = daneOstatniego;
                        }
                    }

                    wstawienie.ShowDialog();
                    RefreshAll();
                }
            }
        }

        private void DataGridHistoria_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dataGridHistoria.SelectedItem != null)
            {
                var row = (DataRowView)dataGridHistoria.SelectedItem;

                string dostawca = row["Dostawca"]?.ToString();
                if (string.IsNullOrEmpty(dostawca)) return;

                // Pobierz dane ostatniego dostarczonego
                var daneOstatniego = PobierzDaneOstatniegoDostarczonego(dostawca);

                // Dialog z pytaniem o kopiowanie danych
                var dialogKopiowania = new OknoKopiowaniaDanychDialog(dostawca, daneOstatniego);
                if (dialogKopiowania.ShowDialog() == true)
                {
                    var wstawienie = new WstawienieWindow
                    {
                        UserID = App.UserID
                    };

                    // Podstawowe dane
                    wstawienie.Dostawca = dostawca;

                    // Jeśli użytkownik chce skopiować dodatkowe dane
                    if (dialogKopiowania.KopiujDodatkoweDane)
                    {
                        if (daneOstatniego != null)
                        {
                            wstawienie.DaneOstatniegoDostarczonego = daneOstatniego;
                        }
                    }

                    wstawienie.ShowDialog();
                    RefreshAll();
                }
            }
        }

        // NOWA METODA: Pobieranie danych ostatniego dostarczonego wstawienia
        private DaneOstatniegoDostarczonego PobierzDaneOstatniegoDostarczonego(string dostawca)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Szukamy ostatniego wstawienia gdzie wszystkie dostawy już się odbyły
                    string query = @"
                        SELECT TOP 1 
                            w.Lp,
                            w.DataWstawienia
                        FROM dbo.WstawieniaKurczakow w
                        WHERE w.Dostawca = @Dostawca
                        AND NOT EXISTS (
                            SELECT 1 
                            FROM dbo.HarmonogramDostaw hd 
                            WHERE hd.LpW = w.Lp 
                            AND hd.DataOdbioru >= CAST(GETDATE() AS DATE)
                        )
                        AND EXISTS (
                            SELECT 1 
                            FROM dbo.HarmonogramDostaw hd 
                            WHERE hd.LpW = w.Lp
                        )
                        ORDER BY w.DataWstawienia DESC";

                    long lpWstawienia = 0;
                    using (var cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Dostawca", dostawca);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                lpWstawienia = reader.GetInt64(0);
                            }
                        }
                    }

                    // Jeśli nie znaleziono dostarczonego, weź najwcześniejsze
                    if (lpWstawienia == 0)
                    {
                        string queryNajwczesniejsze = @"
                            SELECT TOP 1 Lp
                            FROM dbo.WstawieniaKurczakow
                            WHERE Dostawca = @Dostawca
                            ORDER BY DataWstawienia ASC";

                        using (var cmd = new SqlCommand(queryNajwczesniejsze, connection))
                        {
                            cmd.Parameters.AddWithValue("@Dostawca", dostawca);
                            var result = cmd.ExecuteScalar();
                            if (result != null && result != DBNull.Value)
                            {
                                lpWstawienia = Convert.ToInt64(result);
                            }
                        }
                    }

                    if (lpWstawienia == 0)
                        return null;

                    // Pobierz dostawy dla tego wstawienia
                    string queryDostawy = @"
                        SELECT 
                            DATEDIFF(DAY, w.DataWstawienia, hd.DataOdbioru) AS Doba,
                            hd.WagaDek,
                            hd.SztSzuflada,
                            hd.Auta,
                            hd.SztukiDek
                        FROM dbo.HarmonogramDostaw hd
                        INNER JOIN dbo.WstawieniaKurczakow w ON hd.LpW = w.Lp
                        WHERE hd.LpW = @LpW
                        ORDER BY hd.DataOdbioru";

                    var dostawy = new List<DaneDostawy>();
                    using (var cmd = new SqlCommand(queryDostawy, connection))
                    {
                        cmd.Parameters.AddWithValue("@LpW", lpWstawienia);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                dostawy.Add(new DaneDostawy
                                {
                                    Doba = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0),
                                    Waga = reader.IsDBNull(1) ? (double?)null : Convert.ToDouble(reader.GetDecimal(1)),
                                    SztPoj = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                                    Auta = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
                                    Sztuki = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4)
                                });
                            }
                        }
                    }

                    if (dostawy.Count == 0)
                        return null;

                    return new DaneOstatniegoDostarczonego
                    {
                        Dostawy = dostawy
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd pobierania danych ostatniego dostarczonego: {ex.Message}");
                return null;
            }
        }

        // ====== MENU KONTEKSTOWE - LISTA WSTAWIEŃ ======
        private void MenuEdytuj_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridWstawienia.SelectedItem == null)
            {
                MessageBox.Show("Wybierz wstawienie do edycji.", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var row = (DataRowView)dataGridWstawienia.SelectedItem;
            if (row["LP"] == DBNull.Value) return;

            int lp = Convert.ToInt32(row["LP"]);

            var wstawienie = new WstawienieWindow
            {
                UserID = App.UserID
            };

            int lpWstawienia = zapytaniasql.PobierzInformacjeZBazyDanychHarmonogram<int>(lp, "dbo.WstawieniaKurczakow", "Lp");
            string dostawca = zapytaniasql.PobierzInformacjeZBazyDanychHarmonogram<string>(lp, "dbo.WstawieniaKurczakow", "Dostawca");
            DateTime dataWstawienia = zapytaniasql.PobierzInformacjeZBazyDanychHarmonogram<DateTime>(lp, "dbo.WstawieniaKurczakow", "DataWstawienia");
            int sztWstawione = zapytaniasql.PobierzInformacjeZBazyDanychHarmonogram<int>(lp, "dbo.WstawieniaKurczakow", "IloscWstawienia");

            wstawienie.SztWstawienia = sztWstawione;
            wstawienie.Dostawca = dostawca;
            wstawienie.LpWstawienia = lpWstawienia;
            wstawienie.DataWstawienia = dataWstawienia;
            wstawienie.Modyfikacja = true;

            wstawienie.ShowDialog();

            RefreshAll();
        }

        private void MenuUsun_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridWstawienia.SelectedItem == null)
            {
                MessageBox.Show("Wybierz wstawienie do usunięcia.", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var row = (DataRowView)dataGridWstawienia.SelectedItem;
            if (row["LP"] == DBNull.Value) return;

            var result = MessageBox.Show(
                "Czy na pewno chcesz usunąć wybrany wiersz oraz powiązane z nim dane?",
                "Potwierdzenie usunięcia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    string lp = row["LP"].ToString();
                    using (var connection = new SqlConnection(connectionString))
                    {
                        connection.Open();

                        using (var cmd1 = new SqlCommand("DELETE FROM dbo.HarmonogramDostaw WHERE LpW = @LpW", connection))
                        {
                            cmd1.Parameters.AddWithValue("@LpW", lp);
                            cmd1.ExecuteNonQuery();
                        }

                        using (var cmd2 = new SqlCommand("DELETE FROM dbo.WstawieniaKurczakow WHERE Lp = @LpW", connection))
                        {
                            cmd2.Parameters.AddWithValue("@LpW", lp);
                            cmd2.ExecuteNonQuery();
                        }
                    }

                    MessageBox.Show("Wstawienie zostało usunięte pomyślnie.", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    RefreshAll();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Wystąpił błąd: " + ex.Message, "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MenuZmienDateWstawienia_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridWstawienia.SelectedItem == null)
            {
                MessageBox.Show("Wybierz wstawienie do zmiany daty.", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var row = (DataRowView)dataGridWstawienia.SelectedItem;
            if (row["LP"] == DBNull.Value || row["Data"] == DBNull.Value) return;

            int lp = Convert.ToInt32(row["LP"]);
            DateTime staraData = DateTime.Parse(row["Data"].ToString());

            var dialogZmianyDaty = new OknoZmianyDatyWstawieniaDialog(row["Dostawca"].ToString(), staraData);
            if (dialogZmianyDaty.ShowDialog() == true)
            {
                DateTime nowaData = dialogZmianyDaty.NowaData;
                int roznicaDni = (nowaData - staraData).Days;

                if (roznicaDni == 0)
                {
                    MessageBox.Show("Wybrana data jest taka sama jak poprzednia.", "Informacja",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"Data wstawienia zmieni się o {Math.Abs(roznicaDni)} dni ({(roznicaDni > 0 ? "do przodu" : "do tyłu")}).\n\n" +
                    "Czy chcesz również przesunąć wszystkie dostawy związane z tym wstawieniem o tyle samo dni?",
                    "Zmiana dat dostaw",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel)
                    return;

                try
                {
                    using (var connection = new SqlConnection(connectionString))
                    {
                        connection.Open();

                        string queryWstawienie = "UPDATE dbo.WstawieniaKurczakow SET DataWstawienia = @NowaData WHERE Lp = @LP";
                        using (var cmd = new SqlCommand(queryWstawienie, connection))
                        {
                            cmd.Parameters.AddWithValue("@NowaData", nowaData);
                            cmd.Parameters.AddWithValue("@LP", lp);
                            cmd.ExecuteNonQuery();
                        }

                        if (result == MessageBoxResult.Yes)
                        {
                            string queryDostawy = @"
                                UPDATE dbo.HarmonogramDostaw 
                                SET DataOdbioru = DATEADD(DAY, @RoznicaDni, DataOdbioru)
                                WHERE LpW = @LP";
                            using (var cmd = new SqlCommand(queryDostawy, connection))
                            {
                                cmd.Parameters.AddWithValue("@RoznicaDni", roznicaDni);
                                cmd.Parameters.AddWithValue("@LP", lp);
                                int dostaw = cmd.ExecuteNonQuery();

                                MessageBox.Show(
                                    $"Zaktualizowano datę wstawienia oraz {dostaw} dostaw(y).",
                                    "Sukces",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            }
                        }
                        else
                        {
                            MessageBox.Show(
                                "Zaktualizowano tylko datę wstawienia.",
                                "Sukces",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                    }

                    RefreshAll();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd aktualizacji: " + ex.Message, "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MenuZmienTypCeny_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridWstawienia.SelectedItem == null)
            {
                MessageBox.Show("Wybierz wstawienie do zmiany typu ceny.", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var row = (DataRowView)dataGridWstawienia.SelectedItem;
            if (row["LP"] == DBNull.Value) return;

            int lp = Convert.ToInt32(row["LP"]);
            string obecnyTyp = row["TypCeny"]?.ToString() ?? "-";
            string dostawca = row["Dostawca"]?.ToString() ?? "";

            // Wyświetl dialog wyboru typu ceny z kolorami
            string nowyTypCeny = WybierzTypCenyDialog(dostawca, obecnyTyp);

            if (string.IsNullOrEmpty(nowyTypCeny) || nowyTypCeny == obecnyTyp)
            {
                return;
            }

            var result = MessageBox.Show(
                $"Czy chcesz zmienić typ ceny z \"{obecnyTyp}\" na \"{nowyTypCeny}\"?\n\n" +
                "Zmiana zostanie zastosowana również do wszystkich powiązanych dostaw.",
                "Zmiana typu ceny",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var connection = new SqlConnection(connectionString))
                    {
                        connection.Open();

                        // Aktualizuj typ ceny w wstawieniu
                        string queryWstawienie = "UPDATE dbo.WstawieniaKurczakow SET TypCeny = @TypCeny WHERE Lp = @LP";
                        using (var cmd = new SqlCommand(queryWstawienie, connection))
                        {
                            cmd.Parameters.AddWithValue("@TypCeny", nowyTypCeny);
                            cmd.Parameters.AddWithValue("@LP", lp);
                            cmd.ExecuteNonQuery();
                        }

                        // Aktualizuj typ ceny w dostawach
                        string queryDostawy = "UPDATE dbo.HarmonogramDostaw SET typCeny = @TypCeny WHERE LpW = @LP";
                        using (var cmd = new SqlCommand(queryDostawy, connection))
                        {
                            cmd.Parameters.AddWithValue("@TypCeny", nowyTypCeny);
                            cmd.Parameters.AddWithValue("@LP", lp);
                            int dostaw = cmd.ExecuteNonQuery();

                            MessageBox.Show(
                                $"Zmieniono typ ceny na \"{nowyTypCeny}\".\n\nZaktualizowano {dostaw} dostaw(y).",
                                "Sukces",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                    }

                    RefreshAll();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd aktualizacji: " + ex.Message, "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private string WybierzTypCenyDialog(string dostawca, string obecnyTyp)
        {
            var dialog = new Window
            {
                Title = "Zmiana typu ceny",
                Width = 280,
                Height = 320,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize
            };

            var mainBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(15),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    ShadowDepth = 0,
                    Color = Colors.Black,
                    Opacity = 0.2,
                    BlurRadius = 20
                }
            };

            var panel = new StackPanel { Margin = new Thickness(20) };
            string wybrana = null;

            // Tytuł
            var titlePanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(15, 10, 15, 10),
                Margin = new Thickness(0, 0, 0, 15)
            };
            var titleText = new TextBlock
            {
                Text = $"💰 Typ ceny - {dostawca}",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center
            };
            titlePanel.Child = titleText;
            panel.Children.Add(titlePanel);

            // Info o obecnym typie
            var infoText = new TextBlock
            {
                Text = $"Obecny typ: {obecnyTyp}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };
            panel.Children.Add(infoText);

            // Kolory dla typów cen
            var opcjeKolory = new Dictionary<string, (Color bg, Color fg)>
            {
                { "łączona", (Color.FromRgb(138, 43, 226), Colors.White) },      // fioletowy
                { "rolnicza", (Color.FromRgb(92, 138, 58), Colors.White) },       // zielony
                { "wolnyrynek", (Color.FromRgb(255, 193, 7), Colors.Black) },     // żółty
                { "ministerialna", (Color.FromRgb(33, 150, 243), Colors.White) }  // niebieski
            };

            foreach (var opcja in opcjeKolory)
            {
                var btn = new Button
                {
                    Content = opcja.Key + (opcja.Key == obecnyTyp ? " ✓" : ""),
                    Margin = new Thickness(0, 5, 0, 5),
                    Padding = new Thickness(12),
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Background = new SolidColorBrush(opcja.Value.bg),
                    Foreground = new SolidColorBrush(opcja.Value.fg),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                btn.Click += (s, ev) => { wybrana = opcja.Key; dialog.DialogResult = true; };
                panel.Children.Add(btn);
            }

            // Przycisk anuluj
            var btnCancel = new Button
            {
                Content = "❌ Anuluj",
                Margin = new Thickness(0, 15, 0, 0),
                Padding = new Thickness(10),
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, ev) => { dialog.DialogResult = false; dialog.Close(); };
            panel.Children.Add(btnCancel);

            mainBorder.Child = panel;
            dialog.Content = mainBorder;
            dialog.ShowDialog();
            return wybrana;
        }

        // ====== MENU KONTEKSTOWE - PRZYPOMNIENIA ======
        private void MenuNieOdebral_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridPrzypomnienia.SelectedItem == null)
            {
                MessageBox.Show("Wybierz przypomnienie z listy.", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var row = (DataRowView)dataGridPrzypomnienia.SelectedItem;

            if (row["LP"] == DBNull.Value)
            {
                MessageBox.Show("Brak LP dla zaznaczonego wiersza.", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int lp = Convert.ToInt32(row["LP"]);
            string dostawca = Convert.ToString(row["Dostawca"]);
            DateTime until = DateTime.Today.AddDays(3);
            string note = "Brak kontaktu";

            try
            {
                AddContactHistory(lp, dostawca, until, note);

                MessageBox.Show($"Zaznaczono jako 'Nie odebrał'.\n\nKolejne przypomnienie: {until:yyyy-MM-dd}",
                    "Odłożono kontakt",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zapisu: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuTryProby_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridPrzypomnienia.SelectedItem == null)
            {
                MessageBox.Show("Wybierz przypomnienie z listy.", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var row = (DataRowView)dataGridPrzypomnienia.SelectedItem;

            if (row["LP"] == DBNull.Value)
            {
                MessageBox.Show("Brak LP dla zaznaczonego wiersza.", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int lp = Convert.ToInt32(row["LP"]);
            string dostawca = Convert.ToString(row["Dostawca"]);
            DateTime until = DateTime.Today.AddMonths(1);
            string note = "3 próby telefonu - nadal brak kontaktu";

            try
            {
                AddContactHistory(lp, dostawca, until, note);

                MessageBox.Show($"Odłożono o 1 miesiąc (3 próby telefonu).\n\nKolejne przypomnienie: {until:yyyy-MM-dd}",
                    "Odłożono kontakt",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zapisu: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuOdlozDluzej_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridPrzypomnienia.SelectedItem == null)
            {
                MessageBox.Show("Wybierz przypomnienie z listy.", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var row = (DataRowView)dataGridPrzypomnienia.SelectedItem;

            if (row["LP"] == DBNull.Value)
            {
                MessageBox.Show("Brak LP dla zaznaczonego wiersza.", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int lp = Convert.ToInt32(row["LP"]);
            string dostawca = Convert.ToString(row["Dostawca"]);

            var dialogOdlozenie = new OknoOdlozeniaDialog(dostawca);
            if (dialogOdlozenie.ShowDialog() == true)
            {
                try
                {
                    AddContactHistory(lp, dostawca, dialogOdlozenie.SnoozedUntil, dialogOdlozenie.Notatka);

                    MessageBox.Show($"Kontakt odłożono do: {dialogOdlozenie.SnoozedUntil:yyyy-MM-dd}",
                        "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    RefreshAll();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd zapisu: " + ex.Message, "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MenuDodajNumer_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridPrzypomnienia.SelectedItem == null)
            {
                MessageBox.Show("Wybierz przypomnienie z listy.", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var row = (DataRowView)dataGridPrzypomnienia.SelectedItem;
            string dostawca = Convert.ToString(row["Dostawca"]);

            // Pobierz obecne numery telefonów
            string phone1 = "", phone2 = "", phone3 = "";
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT ISNULL(Phone1, ''), ISNULL(Phone2, ''), ISNULL(Phone3, '') FROM dbo.Dostawcy WHERE ShortName = @Dostawca";
                    using (var cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Dostawca", dostawca);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                phone1 = reader.GetString(0);
                                phone2 = reader.GetString(1);
                                phone3 = reader.GetString(2);
                            }
                        }
                    }
                }
            }
            catch { }

            var dialogNumer = new OknoDodaniaNumeruDialog(dostawca, phone1, phone2, phone3);
            if (dialogNumer.ShowDialog() == true)
            {
                try
                {
                    using (var connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        string query = "UPDATE dbo.Dostawcy SET Phone1 = @Phone1, Phone2 = @Phone2, Phone3 = @Phone3 WHERE ShortName = @Dostawca";
                        using (var cmd = new SqlCommand(query, connection))
                        {
                            cmd.Parameters.AddWithValue("@Phone1", dialogNumer.NumerTelefonu ?? "");
                            cmd.Parameters.AddWithValue("@Phone2", dialogNumer.NumerTelefonu2 ?? "");
                            cmd.Parameters.AddWithValue("@Phone3", dialogNumer.NumerTelefonu3 ?? "");
                            cmd.Parameters.AddWithValue("@Dostawca", dostawca);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    var zapisaneNumery = new List<string>();
                    if (!string.IsNullOrEmpty(dialogNumer.NumerTelefonu)) zapisaneNumery.Add(dialogNumer.NumerTelefonu);
                    if (!string.IsNullOrEmpty(dialogNumer.NumerTelefonu2)) zapisaneNumery.Add(dialogNumer.NumerTelefonu2);
                    if (!string.IsNullOrEmpty(dialogNumer.NumerTelefonu3)) zapisaneNumery.Add(dialogNumer.NumerTelefonu3);

                    MessageBox.Show($"Zapisano numery telefonu:\n{string.Join("\n", zapisaneNumery)}",
                        "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    RefreshAll();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd zapisu: " + ex.Message, "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ====== MENU KONTEKSTOWE - HISTORIA KONTAKTÓW ======
        private void MenuEdytujHistorie_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridHistoria.SelectedItem == null)
            {
                MessageBox.Show("Wybierz wpis do edycji.", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var row = (DataRowView)dataGridHistoria.SelectedItem;
            if (row["ContactID"] == DBNull.Value)
            {
                MessageBox.Show("Brak ID wpisu.", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int contactId = Convert.ToInt32(row["ContactID"]);
            string obecnaNotatka = row["Reason"]?.ToString() ?? "";
            string hodowca = row["Dostawca"]?.ToString() ?? "";

            var dialog = new OknoEdycjiNotatkiHistoriiDialog(hodowca, obecnaNotatka);
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using (var connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        string query = "UPDATE dbo.ContactHistory SET Reason = @Reason WHERE ContactID = @ContactID";
                        using (var cmd = new SqlCommand(query, connection))
                        {
                            cmd.Parameters.AddWithValue("@Reason", dialog.NowaNotatka);
                            cmd.Parameters.AddWithValue("@ContactID", contactId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    MessageBox.Show("Notatka została zaktualizowana.", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    RefreshAll();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd zapisu: " + ex.Message, "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MenuUsunHistorie_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridHistoria.SelectedItem == null)
            {
                MessageBox.Show("Wybierz wpis do usunięcia.", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var row = (DataRowView)dataGridHistoria.SelectedItem;
            if (row["ContactID"] == DBNull.Value)
            {
                MessageBox.Show("Brak ID wpisu.", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int contactId = Convert.ToInt32(row["ContactID"]);
            string hodowca = row["Dostawca"]?.ToString() ?? "";

            var result = MessageBox.Show(
                $"Czy na pewno chcesz usunąć wpis historii kontaktu z hodowcą \"{hodowca}\"?\n\nTej operacji nie można cofnąć!",
                "Potwierdzenie usunięcia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        string query = "DELETE FROM dbo.ContactHistory WHERE ContactID = @ContactID";
                        using (var cmd = new SqlCommand(query, connection))
                        {
                            cmd.Parameters.AddWithValue("@ContactID", contactId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    MessageBox.Show("Wpis został usunięty.", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    RefreshAll();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd usuwania: " + ex.Message, "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MenuPotwierdzWstawienie_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridDoPotwierdzenia.SelectedItem == null)
            {
                MessageBox.Show("Wybierz wstawienie do potwierdzenia.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var row = (DataRowView)dataGridDoPotwierdzenia.SelectedItem;
            int lp = Convert.ToInt32(row["LP"]);

            var result = MessageBox.Show("Czy na pewno chcesz potwierdzić to wstawienie?", "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                PotwierdzWstawienie(lp);
            }
        }

        private void MenuPotwierdzIZmienDate_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridDoPotwierdzenia.SelectedItem == null)
            {
                MessageBox.Show("Wybierz wstawienie do potwierdzenia i zmiany daty.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var row = (DataRowView)dataGridDoPotwierdzenia.SelectedItem;
            int lp = Convert.ToInt32(row["LP"]);
            DateTime aktualnaData = Convert.ToDateTime(row["DataWstawienia"]);

            var dialog = new OknoZmianyDatyWstawieniaDialog(row["Dostawca"].ToString(), aktualnaData);
            if (dialog.ShowDialog() == true)
            {
                DateTime nowaData = dialog.NowaData;
                PotwierdzWstawienie(lp, nowaData);
            }
        }

        private void PotwierdzWstawienie(int lp, DateTime? nowaData = null)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query;

                    if (nowaData.HasValue)
                    {
                        query = "UPDATE dbo.WstawieniaKurczakow SET isConf = 1, DataConf = GETDATE(), KtoConf = @UserID, DataWstawienia = @NowaData WHERE Lp = @LP";
                    }
                    else
                    {
                        query = "UPDATE dbo.WstawieniaKurczakow SET isConf = 1, DataConf = GETDATE(), KtoConf = @UserID WHERE Lp = @LP";
                    }

                    using (var cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@UserID", App.UserID);
                        cmd.Parameters.AddWithValue("@LP", lp);
                        if (nowaData.HasValue)
                        {
                            cmd.Parameters.AddWithValue("@NowaData", nowaData.Value);
                        }
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Wstawienie zostało pomyślnie potwierdzone.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd podczas potwierdzania wstawienia: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ====== PRZYCISKI ======
        private void BtnDodaj_Click(object sender, RoutedEventArgs e)
        {
            var wstawienie = new WstawienieWindow
            {
                UserID = App.UserID
            };

            if (wstawienie.ShowDialog() == true)
            {
                if (wstawienie.PobitoRekord)
                {
                    ShowRekordConfetti();
                }
                else
                {
                    ShowConfetti();
                }
            }

            RefreshAll();
        }

        // ====== POMOCNICZE ======
        private void AddContactHistory(int lpWstawienia, string dostawca, DateTime? snoozedUntil, string reason)
        {
            using (var connection = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand("dbo.AddContactHistory", connection))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@LpWstawienia", lpWstawienia);
                cmd.Parameters.AddWithValue("@Dostawca", (object)dostawca ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@UserID", App.UserID);
                cmd.Parameters.AddWithValue("@SnoozedUntil", (object?)snoozedUntil ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Reason", (object?)reason ?? DBNull.Value);

                connection.Open();
                cmd.ExecuteNonQuery();
            }
        }

        private void RefreshAll()
        {
            LoadWstawienia();
            LoadPrzypomnienia();
            LoadHistoria();
            LoadDoPotwierdzenia();
            UpdateStatistics();
        }

        // ====== EFEKT KONFETTI ======
        private void ShowConfetti()
        {
            var random = new Random();
            var colors = new[]
            {
                Color.FromRgb(255, 107, 107),  // czerwony
                Color.FromRgb(255, 193, 7),    // żółty
                Color.FromRgb(76, 175, 80),    // zielony
                Color.FromRgb(33, 150, 243),   // niebieski
                Color.FromRgb(156, 39, 176),   // fioletowy
                Color.FromRgb(255, 152, 0),    // pomarańczowy
                Color.FromRgb(0, 188, 212),    // cyjan
                Color.FromRgb(233, 30, 99)     // różowy
            };

            int confettiCount = 80;

            for (int i = 0; i < confettiCount; i++)
            {
                var confetti = new System.Windows.Shapes.Rectangle
                {
                    Width = random.Next(8, 14),
                    Height = random.Next(8, 14),
                    Fill = new SolidColorBrush(colors[random.Next(colors.Length)]),
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new RotateTransform(random.Next(0, 360))
                };

                double startX = random.Next(0, (int)ActualWidth);
                double startY = -20;

                Canvas.SetLeft(confetti, startX);
                Canvas.SetTop(confetti, startY);
                confettiCanvas.Children.Add(confetti);

                // Animacja spadania
                double endY = ActualHeight + 50;
                double horizontalDrift = random.Next(-150, 150);
                double duration = random.NextDouble() * 2 + 2; // 2-4 sekundy
                double delay = random.NextDouble() * 0.5;

                var fallAnimation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = startY,
                    To = endY,
                    Duration = TimeSpan.FromSeconds(duration),
                    BeginTime = TimeSpan.FromSeconds(delay),
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
                };

                var driftAnimation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = startX,
                    To = startX + horizontalDrift,
                    Duration = TimeSpan.FromSeconds(duration),
                    BeginTime = TimeSpan.FromSeconds(delay)
                };

                var rotateAnimation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 0,
                    To = random.Next(360, 720) * (random.Next(2) == 0 ? 1 : -1),
                    Duration = TimeSpan.FromSeconds(duration),
                    BeginTime = TimeSpan.FromSeconds(delay)
                };

                var fadeAnimation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromSeconds(0.5),
                    BeginTime = TimeSpan.FromSeconds(delay + duration - 0.5)
                };

                var confettiRef = confetti;
                fadeAnimation.Completed += (s, e) =>
                {
                    confettiCanvas.Children.Remove(confettiRef);
                };

                confetti.BeginAnimation(Canvas.TopProperty, fallAnimation);
                confetti.BeginAnimation(Canvas.LeftProperty, driftAnimation);
                ((RotateTransform)confetti.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);
                confetti.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
            }
        }

        // ====== SPECJALNE KONFETTI DLA REKORDU ======
        private void ShowRekordConfetti()
        {
            var random = new Random();

            // Złote i celebracyjne kolory dla rekordu
            var colors = new[]
            {
                Color.FromRgb(255, 215, 0),    // złoty
                Color.FromRgb(255, 193, 7),    // złoty ciemniejszy
                Color.FromRgb(255, 245, 157),  // jasny złoty
                Color.FromRgb(255, 152, 0),    // pomarańczowy
                Color.FromRgb(255, 255, 255),  // biały
                Color.FromRgb(255, 223, 0),    // żółty złoty
                Color.FromRgb(218, 165, 32),   // goldenrod
                Color.FromRgb(255, 69, 0)      // czerwono-pomarańczowy
            };

            // Więcej konfetti dla rekordu!
            int confettiCount = 150;

            // Fale konfetti - 3 fale
            for (int wave = 0; wave < 3; wave++)
            {
                double waveDelay = wave * 0.3;

                for (int i = 0; i < confettiCount / 3; i++)
                {
                    // Mieszanka kształtów - prostokąty i gwiazdy
                    System.Windows.Shapes.Shape confetti;

                    if (random.Next(3) == 0)
                    {
                        // Gwiazda (używamy wielokąta)
                        confetti = new System.Windows.Shapes.Polygon
                        {
                            Points = CreateStarPoints(random.Next(10, 16)),
                            Fill = new SolidColorBrush(colors[random.Next(colors.Length)]),
                            RenderTransformOrigin = new Point(0.5, 0.5),
                            RenderTransform = new RotateTransform(random.Next(0, 360))
                        };
                    }
                    else
                    {
                        confetti = new System.Windows.Shapes.Rectangle
                        {
                            Width = random.Next(10, 18),
                            Height = random.Next(10, 18),
                            Fill = new SolidColorBrush(colors[random.Next(colors.Length)]),
                            RenderTransformOrigin = new Point(0.5, 0.5),
                            RenderTransform = new RotateTransform(random.Next(0, 360))
                        };
                    }

                    double startX = random.Next(0, (int)ActualWidth);
                    double startY = -30;

                    Canvas.SetLeft(confetti, startX);
                    Canvas.SetTop(confetti, startY);
                    confettiCanvas.Children.Add(confetti);

                    double endY = ActualHeight + 50;
                    double horizontalDrift = random.Next(-200, 200);
                    double duration = random.NextDouble() * 2.5 + 2.5;
                    double delay = waveDelay + random.NextDouble() * 0.4;

                    var fallAnimation = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        From = startY,
                        To = endY,
                        Duration = TimeSpan.FromSeconds(duration),
                        BeginTime = TimeSpan.FromSeconds(delay),
                        EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
                    };

                    var driftAnimation = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        From = startX,
                        To = startX + horizontalDrift,
                        Duration = TimeSpan.FromSeconds(duration),
                        BeginTime = TimeSpan.FromSeconds(delay)
                    };

                    var rotateAnimation = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        From = 0,
                        To = random.Next(720, 1440) * (random.Next(2) == 0 ? 1 : -1),
                        Duration = TimeSpan.FromSeconds(duration),
                        BeginTime = TimeSpan.FromSeconds(delay)
                    };

                    // Pulsujący efekt skali dla gwiazd
                    var scaleTransform = new ScaleTransform(1, 1);
                    var transformGroup = new TransformGroup();
                    transformGroup.Children.Add(confetti.RenderTransform);
                    transformGroup.Children.Add(scaleTransform);
                    confetti.RenderTransform = transformGroup;

                    var scaleAnimation = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        From = 1.0,
                        To = 1.3,
                        Duration = TimeSpan.FromSeconds(0.3),
                        AutoReverse = true,
                        RepeatBehavior = new System.Windows.Media.Animation.RepeatBehavior(TimeSpan.FromSeconds(duration)),
                        BeginTime = TimeSpan.FromSeconds(delay)
                    };

                    var fadeAnimation = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        From = 1,
                        To = 0,
                        Duration = TimeSpan.FromSeconds(0.5),
                        BeginTime = TimeSpan.FromSeconds(delay + duration - 0.5)
                    };

                    var confettiRef = confetti;
                    fadeAnimation.Completed += (s, e) =>
                    {
                        confettiCanvas.Children.Remove(confettiRef);
                    };

                    confetti.BeginAnimation(Canvas.TopProperty, fallAnimation);
                    confetti.BeginAnimation(Canvas.LeftProperty, driftAnimation);
                    ((RotateTransform)((TransformGroup)confetti.RenderTransform).Children[0]).BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
                    confetti.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
                }
            }
        }

        private System.Windows.Media.PointCollection CreateStarPoints(double size)
        {
            var points = new System.Windows.Media.PointCollection();
            double outerRadius = size / 2;
            double innerRadius = size / 4;

            for (int i = 0; i < 10; i++)
            {
                double angle = Math.PI / 2 + i * Math.PI / 5;
                double radius = (i % 2 == 0) ? outerRadius : innerRadius;
                points.Add(new Point(
                    size / 2 + radius * Math.Cos(angle),
                    size / 2 - radius * Math.Sin(angle)
                ));
            }

            return points;
        }
    }

    // ====== CONVERTER DLA KOLUMNY POTW. ======
    public class IsConfConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value == DBNull.Value)
                return "";

            bool isConf = System.Convert.ToBoolean(value);
            return isConf ? "✓" : "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // ====== CONVERTER DLA KOLUMNY TYP CENY Z KOLORAMI ======
    public class TypCenyToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string typCeny = value?.ToString()?.ToLower() ?? "";

            switch (typCeny)
            {
                case "łączona":
                    return new SolidColorBrush(Color.FromRgb(138, 43, 226));  // fioletowy
                case "rolnicza":
                    return new SolidColorBrush(Color.FromRgb(92, 138, 58));   // zielony
                case "wolnyrynek":
                    return new SolidColorBrush(Color.FromRgb(255, 193, 7));   // żółty
                case "ministerialna":
                    return new SolidColorBrush(Color.FromRgb(33, 150, 243));  // niebieski
                default:
                    return new SolidColorBrush(Color.FromRgb(149, 165, 166)); // szary dla innych/brak
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // ====== CONVERTER DLA KOLORU TEKSTU TYPU CENY ======
    public class TypCenyToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string typCeny = value?.ToString()?.ToLower() ?? "";

            // Wolnyrynek ma żółte tło - potrzebny czarny tekst
            if (typCeny == "wolnyrynek")
                return Brushes.Black;

            // Wszystkie pozostałe mają ciemne tło - biały tekst
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // ====== OKNO DIALOGOWE DLA KOPIOWANIA DANYCH ======
    public partial class OknoKopiowaniaDanychDialog : Window
    {
        public bool KopiujDodatkoweDane { get; private set; }

        public OknoKopiowaniaDanychDialog(string dostawca, DaneOstatniegoDostarczonego daneOstatniego = null)
        {
            InitializeComponent(dostawca, daneOstatniego);
        }

        private void InitializeComponent(string dostawca, DaneOstatniegoDostarczonego daneOstatniego)
        {
            Width = 650;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;

            var mainBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(15),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    ShadowDepth = 0,
                    Color = Colors.Black,
                    Opacity = 0.2,
                    BlurRadius = 20
                }
            };

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(30)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titlePanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(92, 138, 58)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20, 12, 20, 12)
            };
            var titleText = new TextBlock
            {
                Text = $"📋 Nowe wstawienie - {dostawca}",
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center
            };
            titlePanel.Child = titleText;
            Grid.SetRow(titlePanel, 0);

            var spacer1 = new Border { Height = 20 };
            Grid.SetRow(spacer1, 1);

            // Tekst informacyjny
            var infoText = new TextBlock
            {
                Text = "Czy chcesz skopiować dane z ostatniego zrealizowanego wstawienia?",
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 15)
            };
            Grid.SetRow(infoText, 2);

            // Panel z danymi dostaw
            var dostawyPanel = new StackPanel();
            Grid.SetRow(dostawyPanel, 3);

            if (daneOstatniego != null && daneOstatniego.Dostawy != null && daneOstatniego.Dostawy.Count > 0)
            {
                var dostawyBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(232, 245, 233)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(15),
                    Margin = new Thickness(0, 0, 0, 15)
                };

                var dostawyStack = new StackPanel();

                var headerText = new TextBlock
                {
                    Text = "📦 Dane do skopiowania z ostatniego wstawienia:",
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50)),
                    Margin = new Thickness(0, 0, 0, 10)
                };
                dostawyStack.Children.Add(headerText);

                for (int i = 0; i < daneOstatniego.Dostawy.Count; i++)
                {
                    var dostawa = daneOstatniego.Dostawy[i];
                    var dostawaBorder = new Border
                    {
                        Background = Brushes.White,
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(10),
                        Margin = new Thickness(0, 0, 0, 8)
                    };

                    var dostawaGrid = new Grid();
                    dostawaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                    dostawaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    dostawaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    dostawaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    dostawaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    dostawaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var nrText = new TextBlock
                    {
                        Text = $"Dostawa {i + 1}:",
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(92, 138, 58)),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(nrText, 0);
                    dostawaGrid.Children.Add(nrText);

                    int colIndex = 1;

                    if (dostawa.Doba.HasValue)
                    {
                        var dobaPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 15, 0) };
                        dobaPanel.Children.Add(new TextBlock { Text = "📅 ", FontSize = 10 });
                        dobaPanel.Children.Add(new TextBlock { Text = $"Doba: {dostawa.Doba} dni", FontSize = 10, FontWeight = FontWeights.SemiBold });
                        Grid.SetColumn(dobaPanel, colIndex++);
                        dostawaGrid.Children.Add(dobaPanel);
                    }

                    if (dostawa.Waga.HasValue)
                    {
                        var wagaPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 15, 0) };
                        wagaPanel.Children.Add(new TextBlock { Text = "⚖️ ", FontSize = 10 });
                        wagaPanel.Children.Add(new TextBlock { Text = $"Waga: {dostawa.Waga:0.0} kg", FontSize = 10, FontWeight = FontWeights.SemiBold });
                        Grid.SetColumn(wagaPanel, colIndex++);
                        dostawaGrid.Children.Add(wagaPanel);
                    }

                    if (dostawa.SztPoj.HasValue)
                    {
                        var sztPojPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 15, 0) };
                        sztPojPanel.Children.Add(new TextBlock { Text = "📦 ", FontSize = 10 });
                        sztPojPanel.Children.Add(new TextBlock { Text = $"Szt/poj: {dostawa.SztPoj}", FontSize = 10, FontWeight = FontWeights.SemiBold });
                        Grid.SetColumn(sztPojPanel, colIndex++);
                        dostawaGrid.Children.Add(sztPojPanel);
                    }

                    if (dostawa.Sztuki.HasValue)
                    {
                        var sztukiPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 15, 0) };
                        sztukiPanel.Children.Add(new TextBlock { Text = "🐔 ", FontSize = 10 });
                        sztukiPanel.Children.Add(new TextBlock { Text = $"Sztuki: {dostawa.Sztuki:#,##0}", FontSize = 10, FontWeight = FontWeights.SemiBold });
                        Grid.SetColumn(sztukiPanel, colIndex++);
                        dostawaGrid.Children.Add(sztukiPanel);
                    }

                    if (dostawa.Auta.HasValue)
                    {
                        var autaPanel = new StackPanel { Orientation = Orientation.Horizontal };
                        autaPanel.Children.Add(new TextBlock { Text = "🚚 ", FontSize = 10 });
                        autaPanel.Children.Add(new TextBlock { Text = $"Auta: {dostawa.Auta}", FontSize = 10, FontWeight = FontWeights.SemiBold });
                        Grid.SetColumn(autaPanel, colIndex++);
                        dostawaGrid.Children.Add(autaPanel);
                    }

                    dostawaBorder.Child = dostawaGrid;
                    dostawyStack.Children.Add(dostawaBorder);
                }

                dostawyBorder.Child = dostawyStack;
                dostawyPanel.Children.Add(dostawyBorder);
            }
            else
            {
                var brakDanychBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(255, 243, 224)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(15),
                    Margin = new Thickness(0, 0, 0, 15)
                };

                var brakDanychText = new TextBlock
                {
                    Text = "⚠️ Brak danych z ostatniego wstawienia.\n\nZostaną użyte domyślne wartości dostaw:\n• Dostawa 1: Doba 35, Waga 2.1 kg, Szt/poj 20\n• Dostawa 2: Doba 42, Waga 2.8 kg, Szt/poj 16\n\nSztuki i Auta będą puste - trzeba będzie je uzupełnić ręcznie.",
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromRgb(230, 126, 34)),
                    LineHeight = 18
                };

                brakDanychBorder.Child = brakDanychText;
                dostawyPanel.Children.Add(brakDanychBorder);
            }

            dostawyPanel.Children.Add(infoText);

            var spacer2 = new Border { Height = 20 };
            Grid.SetRow(spacer2, 4);

            // Przyciski
            var buttonStack = new StackPanel();
            Grid.SetRow(buttonStack, 5);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var btnTak = new Button
            {
                Content = "✅ TAK, SKOPIUJ DANE",
                Width = 200,
                Height = 45,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(92, 138, 58)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            var btnTakBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Child = btnTak
            };
            btnTak.Click += (s, e) =>
            {
                KopiujDodatkoweDane = true;
                DialogResult = true;
                Close();
            };

            var btnNie = new Button
            {
                Content = "⏭️ TYLKO PODSTAWOWE",
                Width = 200,
                Height = 45,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            var btnNieBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Child = btnNie
            };
            btnNie.Click += (s, e) =>
            {
                KopiujDodatkoweDane = false;
                DialogResult = true;
                Close();
            };

            buttonPanel.Children.Add(btnTakBorder);
            buttonPanel.Children.Add(btnNieBorder);
            buttonStack.Children.Add(buttonPanel);

            var btnAnuluj = new Button
            {
                Content = "❌ ANULUJ",
                Width = 150,
                Height = 40,
                Background = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var btnAnulujBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Child = btnAnuluj,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            btnAnuluj.Click += (s, e) =>
            {
                DialogResult = false;
                Close();
            };
            buttonStack.Children.Add(btnAnulujBorder);

            grid.Children.Add(titlePanel);
            grid.Children.Add(spacer1);
            grid.Children.Add(dostawyPanel);
            grid.Children.Add(spacer2);
            grid.Children.Add(buttonStack);

            scrollViewer.Content = grid;
            mainBorder.Child = scrollViewer;
            Content = mainBorder;
        }
    }

    // ====== OKNO DIALOGOWE DLA ODŁOŻENIA ======
    public partial class OknoOdlozeniaDialog : Window
    {
        public DateTime SnoozedUntil { get; private set; }
        public string Notatka { get; private set; }

        public OknoOdlozeniaDialog(string dostawca)
        {
            InitializeComponent(dostawca);
        }

        private void InitializeComponent(string dostawca)
        {
            Width = 500;
            Height = 480;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;

            var mainBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(15),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    ShadowDepth = 0,
                    Color = Colors.Black,
                    Opacity = 0.2,
                    BlurRadius = 20
                }
            };

            var grid = new Grid { Margin = new Thickness(30) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titlePanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(92, 138, 58)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20, 12, 20, 12)
            };
            var titleText = new TextBlock
            {
                Text = $"🕐 Odłożenie kontaktu - {dostawca}",
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center
            };
            titlePanel.Child = titleText;
            Grid.SetRow(titlePanel, 0);

            var spacer1 = new Border { Height = 20 };
            Grid.SetRow(spacer1, 1);

            var labelMonths = new TextBlock
            {
                Text = "Za ile miesięcy?",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(labelMonths, 2);

            var txtMonths = new TextBox
            {
                Padding = new Thickness(12, 10, 12, 10),
                FontSize = 13,
                Height = 40
            };
            var txtMonthsBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(189, 195, 199)),
                BorderThickness = new Thickness(2),
                Child = txtMonths,
                Margin = new Thickness(0, 5, 0, 0)
            };
            Grid.SetRow(txtMonthsBorder, 3);

            var spacer2 = new Border { Height = 15 };
            Grid.SetRow(spacer2, 4);

            var labelDate = new TextBlock
            {
                Text = "Lub wybierz konkretną datę",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(labelDate, 5);

            var datePicker = new DatePicker
            {
                SelectedDate = DateTime.Today,
                DisplayDateStart = DateTime.Today,
                Padding = new Thickness(12, 10, 12, 10),
                FontSize = 13,
                Height = 40,
                BorderBrush = new SolidColorBrush(Color.FromRgb(189, 195, 199)),
                BorderThickness = new Thickness(2),
                Margin = new Thickness(0, 5, 0, 0)
            };
            var datePickerBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Child = datePicker
            };
            Grid.SetRow(datePickerBorder, 6);

            txtMonths.TextChanged += (s, e) =>
            {
                if (int.TryParse(txtMonths.Text.Trim(), out int months))
                {
                    if (months < 0) months = 0;
                    if (months > 60) months = 60;
                    try
                    {
                        datePicker.SelectedDate = DateTime.Today.AddMonths(months);
                    }
                    catch { }
                }
            };

            var spacer3 = new Border { Height = 15 };
            Grid.SetRow(spacer3, 7);

            var labelNote = new TextBlock
            {
                Text = "Notatka (opcjonalnie)",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(labelNote, 8);

            var txtNote = new TextBox
            {
                Height = 90,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(12, 10, 12, 10),
                FontSize = 13
            };
            var txtNoteBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(189, 195, 199)),
                BorderThickness = new Thickness(2),
                Child = txtNote,
                Margin = new Thickness(0, 5, 0, 0)
            };
            Grid.SetRow(txtNoteBorder, 9);

            var spacer4 = new Border { Height = 20 };
            Grid.SetRow(spacer4, 10);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var btnOK = new Button
            {
                Content = "✅ ODŁÓŻ KONTAKT",
                Width = 170,
                Height = 42,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(92, 138, 58)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            var btnOKBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Child = btnOK
            };
            btnOK.Click += (s, e) =>
            {
                SnoozedUntil = datePicker.SelectedDate ?? DateTime.Today;
                Notatka = txtNote.Text?.Trim();
                DialogResult = true;
                Close();
            };

            var btnCancel = new Button
            {
                Content = "❌ ANULUJ",
                Width = 130,
                Height = 42,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            var btnCancelBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Child = btnCancel
            };
            btnCancel.Click += (s, e) =>
            {
                DialogResult = false;
                Close();
            };

            buttonPanel.Children.Add(btnOKBorder);
            buttonPanel.Children.Add(btnCancelBorder);
            Grid.SetRow(buttonPanel, 11);

            grid.Children.Add(titlePanel);
            grid.Children.Add(spacer1);
            grid.Children.Add(labelMonths);
            grid.Children.Add(txtMonthsBorder);
            grid.Children.Add(spacer2);
            grid.Children.Add(labelDate);
            grid.Children.Add(datePickerBorder);
            grid.Children.Add(spacer3);
            grid.Children.Add(labelNote);
            grid.Children.Add(txtNoteBorder);
            grid.Children.Add(spacer4);
            grid.Children.Add(buttonPanel);

            mainBorder.Child = grid;
            Content = mainBorder;
        }
    }

    // ====== OKNO DIALOGOWE DLA DODANIA NUMERU ======
    public partial class OknoDodaniaNumeruDialog : Window
    {
        public string NumerTelefonu { get; private set; }
        public string NumerTelefonu2 { get; private set; }
        public string NumerTelefonu3 { get; private set; }

        public OknoDodaniaNumeruDialog(string dostawca)
        {
            InitializeComponent(dostawca, "", "", "");
        }

        public OknoDodaniaNumeruDialog(string dostawca, string obecnyPhone1, string obecnyPhone2, string obecnyPhone3)
        {
            InitializeComponent(dostawca, obecnyPhone1, obecnyPhone2, obecnyPhone3);
        }

        private void InitializeComponent(string dostawca, string obecnyPhone1, string obecnyPhone2, string obecnyPhone3)
        {
            Width = 450;
            Height = 420;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;

            var mainBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(15),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    ShadowDepth = 0,
                    Color = Colors.Black,
                    Opacity = 0.2,
                    BlurRadius = 20
                }
            };

            var grid = new Grid { Margin = new Thickness(30) };
            for (int i = 0; i < 12; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titlePanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20, 12, 20, 12)
            };
            var titleText = new TextBlock
            {
                Text = $"📞 Numery telefonu - {dostawca}",
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center
            };
            titlePanel.Child = titleText;
            Grid.SetRow(titlePanel, 0);

            var spacer1 = new Border { Height = 15 };
            Grid.SetRow(spacer1, 1);

            // Phone 1
            var labelPhone1 = new TextBlock
            {
                Text = "📱 Telefon 1 (główny)",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(labelPhone1, 2);

            var txtPhone1 = new TextBox
            {
                Text = obecnyPhone1 ?? "",
                Padding = new Thickness(12, 8, 12, 8),
                FontSize = 14,
                Height = 38
            };
            var txtPhone1Border = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                BorderThickness = new Thickness(2),
                Child = txtPhone1,
                Margin = new Thickness(0, 5, 0, 10)
            };
            Grid.SetRow(txtPhone1Border, 3);

            // Phone 2
            var labelPhone2 = new TextBlock
            {
                Text = "📱 Telefon 2",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(labelPhone2, 4);

            var txtPhone2 = new TextBox
            {
                Text = obecnyPhone2 ?? "",
                Padding = new Thickness(12, 8, 12, 8),
                FontSize = 14,
                Height = 38
            };
            var txtPhone2Border = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                BorderThickness = new Thickness(2),
                Child = txtPhone2,
                Margin = new Thickness(0, 5, 0, 10)
            };
            Grid.SetRow(txtPhone2Border, 5);

            // Phone 3
            var labelPhone3 = new TextBlock
            {
                Text = "📱 Telefon 3",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(labelPhone3, 6);

            var txtPhone3 = new TextBox
            {
                Text = obecnyPhone3 ?? "",
                Padding = new Thickness(12, 8, 12, 8),
                FontSize = 14,
                Height = 38
            };
            var txtPhone3Border = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(155, 89, 182)),
                BorderThickness = new Thickness(2),
                Child = txtPhone3,
                Margin = new Thickness(0, 5, 0, 10)
            };
            Grid.SetRow(txtPhone3Border, 7);

            var spacer2 = new Border { Height = 10 };
            Grid.SetRow(spacer2, 8);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var btnOK = new Button
            {
                Content = "✅ ZAPISZ",
                Width = 140,
                Height = 42,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            btnOK.Click += (s, e) =>
            {
                NumerTelefonu = txtPhone1.Text?.Trim() ?? "";
                NumerTelefonu2 = txtPhone2.Text?.Trim() ?? "";
                NumerTelefonu3 = txtPhone3.Text?.Trim() ?? "";

                if (string.IsNullOrEmpty(NumerTelefonu) && string.IsNullOrEmpty(NumerTelefonu2) && string.IsNullOrEmpty(NumerTelefonu3))
                {
                    MessageBox.Show("Proszę podać przynajmniej jeden numer telefonu.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                DialogResult = true;
                Close();
            };

            var btnCancel = new Button
            {
                Content = "❌ ANULUJ",
                Width = 130,
                Height = 42,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            btnCancel.Click += (s, e) =>
            {
                DialogResult = false;
                Close();
            };

            buttonPanel.Children.Add(btnOK);
            buttonPanel.Children.Add(btnCancel);
            Grid.SetRow(buttonPanel, 9);

            grid.Children.Add(titlePanel);
            grid.Children.Add(spacer1);
            grid.Children.Add(labelPhone1);
            grid.Children.Add(txtPhone1Border);
            grid.Children.Add(labelPhone2);
            grid.Children.Add(txtPhone2Border);
            grid.Children.Add(labelPhone3);
            grid.Children.Add(txtPhone3Border);
            grid.Children.Add(spacer2);
            grid.Children.Add(buttonPanel);

            mainBorder.Child = grid;
            Content = mainBorder;
        }

    }


    // ====== OKNO DIALOGOWE DLA ZMIANY DATY WSTAWIENIA ======
    public partial class OknoZmianyDatyWstawieniaDialog : Window
    {
        public DateTime NowaData { get; private set; }

        public OknoZmianyDatyWstawieniaDialog(string dostawca, DateTime aktualnaData)
        {
            InitializeComponent(dostawca, aktualnaData);
        }

        private void InitializeComponent(string dostawca, DateTime aktualnaData)
        {
            Width = 450;
            Height = 330;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;

            var mainBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(15),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    ShadowDepth = 0,
                    Color = Colors.Black,
                    Opacity = 0.2,
                    BlurRadius = 20
                }
            };

            var grid = new Grid { Margin = new Thickness(30) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titlePanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(243, 156, 18)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20, 12, 20, 12)
            };
            var titleText = new TextBlock
            {
                Text = $"📅 Zmiana daty wstawienia - {dostawca}",
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center
            };
            titlePanel.Child = titleText;
            Grid.SetRow(titlePanel, 0);

            var spacer1 = new Border { Height = 20 };
            Grid.SetRow(spacer1, 1);

            var infoPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 243, 224)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15, 10, 15, 10)
            };
            var infoText = new TextBlock
            {
                Text = $"Aktualna data wstawienia: {aktualnaData:yyyy-MM-dd}",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(230, 126, 34)),
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center
            };
            infoPanel.Child = infoText;
            Grid.SetRow(infoPanel, 2);

            var spacer2 = new Border { Height = 20 };
            Grid.SetRow(spacer2, 3);

            var labelDate = new TextBlock
            {
                Text = "Wybierz nową datę wstawienia",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(labelDate, 4);

            var datePicker = new DatePicker
            {
                SelectedDate = aktualnaData,
                Padding = new Thickness(12, 10, 12, 10),
                FontSize = 13,
                Height = 45,
                BorderBrush = new SolidColorBrush(Color.FromRgb(243, 156, 18)),
                BorderThickness = new Thickness(2),
                Margin = new Thickness(0, 5, 0, 0)
            };
            var datePickerBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Child = datePicker
            };
            Grid.SetRow(datePickerBorder, 5);

            var spacer3 = new Border { Height = 25 };
            Grid.SetRow(spacer3, 6);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var btnOK = new Button
            {
                Content = "✅ ZMIEŃ DATĘ",
                Width = 150,
                Height = 42,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(243, 156, 18)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            var btnOKBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Child = btnOK
            };
            btnOK.Click += (s, e) =>
            {
                if (!datePicker.SelectedDate.HasValue)
                {
                    MessageBox.Show("Proszę wybrać datę.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                NowaData = datePicker.SelectedDate.Value;
                DialogResult = true;
                Close();
            };

            var btnCancel = new Button
            {
                Content = "❌ ANULUJ",
                Width = 130,
                Height = 42,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            var btnCancelBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Child = btnCancel
            };
            btnCancel.Click += (s, e) =>
            {
                DialogResult = false;
                Close();
            };

            buttonPanel.Children.Add(btnOKBorder);
            buttonPanel.Children.Add(btnCancelBorder);
            Grid.SetRow(buttonPanel, 7);

            grid.Children.Add(titlePanel);
            grid.Children.Add(spacer1);
            grid.Children.Add(infoPanel);
            grid.Children.Add(spacer2);
            grid.Children.Add(labelDate);
            grid.Children.Add(datePickerBorder);
            grid.Children.Add(spacer3);
            grid.Children.Add(buttonPanel);

            mainBorder.Child = grid;
            Content = mainBorder;
        }

    }

    // ====== OKNO DIALOGOWE DLA EDYCJI NOTATKI HISTORII KONTAKTÓW ======
    public partial class OknoEdycjiNotatkiHistoriiDialog : Window
    {
        public string NowaNotatka { get; private set; }

        public OknoEdycjiNotatkiHistoriiDialog(string hodowca, string obecnaNotatka)
        {
            InitializeComponent(hodowca, obecnaNotatka);
        }

        private void InitializeComponent(string hodowca, string obecnaNotatka)
        {
            Width = 450;
            Height = 320;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;

            var mainBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(15),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    ShadowDepth = 0,
                    Color = Colors.Black,
                    Opacity = 0.2,
                    BlurRadius = 20
                }
            };

            var grid = new Grid { Margin = new Thickness(30) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titlePanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20, 12, 20, 12)
            };
            var titleText = new TextBlock
            {
                Text = $"✏️ Edycja notatki - {hodowca}",
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center
            };
            titlePanel.Child = titleText;
            Grid.SetRow(titlePanel, 0);

            var spacer1 = new Border { Height = 20 };
            Grid.SetRow(spacer1, 1);

            var labelNotatka = new TextBlock
            {
                Text = "Treść notatki:",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(labelNotatka, 2);

            var txtNotatka = new TextBox
            {
                Text = obecnaNotatka,
                Padding = new Thickness(12, 10, 12, 10),
                FontSize = 14,
                Height = 80,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            var txtNotatkaBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                BorderThickness = new Thickness(2),
                Child = txtNotatka,
                Margin = new Thickness(0, 5, 0, 0)
            };
            Grid.SetRow(txtNotatkaBorder, 3);

            var spacer2 = new Border { Height = 20 };
            Grid.SetRow(spacer2, 4);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var btnOK = new Button
            {
                Content = "✅ ZAPISZ",
                Width = 140,
                Height = 42,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            btnOK.Click += (s, e) =>
            {
                NowaNotatka = txtNotatka.Text?.Trim() ?? "";
                DialogResult = true;
                Close();
            };

            var btnCancel = new Button
            {
                Content = "❌ ANULUJ",
                Width = 130,
                Height = 42,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            btnCancel.Click += (s, e) =>
            {
                DialogResult = false;
                Close();
            };

            buttonPanel.Children.Add(btnOK);
            buttonPanel.Children.Add(btnCancel);
            Grid.SetRow(buttonPanel, 5);

            grid.Children.Add(titlePanel);
            grid.Children.Add(spacer1);
            grid.Children.Add(labelNotatka);
            grid.Children.Add(txtNotatkaBorder);
            grid.Children.Add(spacer2);
            grid.Children.Add(buttonPanel);

            mainBorder.Child = grid;
            Content = mainBorder;
        }
    }

    /// <summary>
    /// Konwerter tekstu na inicjały (dla avatarów)
    /// </summary>
    public class InitialsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "";
            string name = value.ToString();
            if (string.IsNullOrWhiteSpace(name) || name == "-") return "";

            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            else if (parts.Length == 1 && parts[0].Length >= 2)
                return parts[0].Substring(0, 2).ToUpper();
            else if (parts.Length == 1 && parts[0].Length == 1)
                return parts[0].ToUpper();
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}