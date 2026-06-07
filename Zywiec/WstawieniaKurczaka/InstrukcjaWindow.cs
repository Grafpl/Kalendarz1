using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
// Aliasy — QuestPDF.Infrastructure ma własne Color/HorizontalAlignment/VerticalAlignment, ujednoznaczniamy do WPF
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace Kalendarz1
{
    // Graficzna instrukcja obsługi modułu Cykle Wstawień + eksport PDF (QuestPDF).
    // Layout: kafelki/karty z ikonami zamiast jednej długiej strony tekstu.
    public class InstrukcjaWindow : Window
    {
        // Kategoria sekcji = jedna z 4 zakładek na górze (TabControl)
        public enum Kategoria
        {
            Priorytet,     // 🔥 Najważniejsze (1 sekcja)
            Codzienna,     // 📞 Codzienne workflow (Twoja praca dnia)
            Problem,       // 🚨 Co zrobić gdy... (rozwiązywanie problemów)
            Referencja     // 📚 Słowniki, skróty, narzędzia
        }

        // Sekcja = jeden kafelek na ekranie + jeden blok w PDF.
        // Punkty mogą mieć prefiksy specjalne renderowane na inny styl:
        //   "⚠️ tekst" → żółty box ostrzeżenia (przerywa numerację)
        //   "💡 tekst" → niebieski box porady (przerywa numerację)
        //   "✓ tekst"  → zielony box "co osiągniesz"
        public class Sekcja
        {
            public string Ikona { get; set; } = "";
            public string Tytul { get; set; } = "";
            public string Opis { get; set; } = "";
            public string KolorAkcent { get; set; } = "#5C8A3A";
            public List<string> Punkty { get; set; } = new();
            public bool Krokowe { get; set; } = false;
            public bool Szeroka { get; set; } = false;
            public Kategoria Kat { get; set; } = Kategoria.Codzienna;
            // Chip czasu pokazany w nagłówku (np. "~30 min", "~30 sek/wiersz") — pusty = nie pokazuj
            public string Czas { get; set; } = "";
            // Zielona belka "Co osiągniesz" na dole sekcji workflow — pusty = nie pokazuj
            public string Wynik { get; set; } = "";
        }

        private readonly List<Sekcja> _sekcje;

        public InstrukcjaWindow()
        {
            _sekcje = BudujSekcje();

            Title = "📚 Instrukcja Obsługi — Cykle Wstawień Kurczaków";
            Width = 1080;
            Height = 860;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(0xEC, 0xF0, 0xF1));
            WindowIconHelper.SetIcon(this);

            Content = BudujUI();
        }

        // === KONTENT INSTRUKCJI — workflow + kategorie do TabControl ===
        private static List<Sekcja> BudujSekcje()
        {
            return new List<Sekcja>
            {
                // ====================================================================
                // 🔥 ZAKŁADKA: PRIORYTET (jedna mocna sekcja)
                // ====================================================================
                new()
                {
                    Kat = Kategoria.Priorytet,
                    Ikona = "🔥",
                    Tytul = "PRIORYTET NR 1 — dzwoń do hodowców z OBU tabel",
                    KolorAkcent = "#C0392B",
                    Szeroka = true,
                    Krokowe = true,
                    Czas = "~30 min rano (30-50 hodowców)",
                    Wynik = "Tabela PRZYPOMNIENIA pusta + czerwone i żółte wiersze w NADCHODZĄCYCH obdzwonione",
                    Opis = "Najważniejsze zadanie dnia: skontaktować się z hodowcami z DWÓCH tabel — ⚠️ PRZYPOMNIENIA (środek góra) ORAZ 📞 NADCHODZĄCE WSTAWIENIA (środek dół). Obie zawierają innych hodowców i obie są równie ważne. Bez tego praca dnia nie jest skończona.",
                    Punkty = new()
                    {
                        "💡 Trik na szybkość: nie używaj myszki. Kliknij raz wiersz, potem klawisz S / Shift+S / F / R — schowek + odświeżenie dzieje się automatycznie",
                        "RANO: zacznij od PRZYPOMNIEŃ (środek góra) — to hodowcy z którymi mieliśmy zaległe próby kontaktu lub którzy mają zadania w ofercie. Sortowane wg pilności",
                        "Idź wiersz po wierszu w Przypomnieniach: kliknij → S (SMS krótki) lub Shift+S (SMS pełny) lub zadzwoń bezpośrednio z numeru w kolumnie Tel",
                        "Po wysłaniu SMS-a / wykonaniu telefonu wiersz znika z Przypomnień. Cel: WYZEROWAĆ tę tabelę do końca dnia",
                        "⚠️ Nie pomijaj Nadchodzących myśląc 'Przypomnienia mi wystarczą' — to dwie OSOBNE listy z innymi hodowcami. Obie są tak samo ważne",
                        "POTEM przejdź do NADCHODZĄCE WSTAWIENIA (środek dół) — wstawienia w ciągu 14 dni które wymagają potwierdzenia terminu",
                        "Najpierw czerwone wiersze (zaległe lub dzisiejsze), potem pomarańczowe (do 7 dni), na końcu zielonkawe (>7 dni)",
                        "Dla każdego wiersza w Nadchodzących: PPM → '📱 SMS — Pełne potwierdzenie' (lub Krótkie dla stałych klientów). Wiersz znika na 3 dni — masz czas żeby hodowca odpowiedział",
                        "💡 Jak ktoś odpowie 'TAK' przez SMS — wróć do programu, kliknij wiersz hodowcy, naciśnij F. Wstawienie ma teraz status Potwierdzone",
                        "⚠️ Nie kończ dnia z czerwonymi wierszami w Nadchodzących! To zaległe potwierdzenia — niewykonane oznacza ryzyko że hodowca z dnia minionego nas zaskoczy"
                    }
                },
                // ====================================================================
                // 📞 ZAKŁADKA: CODZIENNA PRACA (workflowy które robisz codziennie)
                // ====================================================================
                new()
                {
                    Kat = Kategoria.Codzienna,
                    Ikona = "🗺️",
                    Tytul = "Co widzisz na ekranie",
                    KolorAkcent = "#5C8A3A",
                    Szeroka = true,
                    Opis = "Okno składa się z górnego paska + 3 kolumn + paska statusu na dole. Każda kolumna ma swoją rolę. Zanim ruszysz dalej — zorientuj się gdzie co jest.",
                    Punkty = new()
                    {
                        "🔝 Górny pasek — szukajka, ➕ Dodaj, skróty klawiszowe (3 grupy: 📱 SMS / ✅ Akcje / 📋 Lista), narzędzia",
                        "📋 LEWA kolumna — Lista wstawień (kto, kiedy, ile sztuk, jaki typ ceny). Tu szukasz hodowcy i tu zaczynasz każdą akcję",
                        "⚠️ ŚRODEK GÓRA — Przypomnienia (do kogo zadzwonić — automatycznie posortowane po pilności)",
                        "📞 ŚRODEK DÓŁ — Nadchodzące wstawienia (czekają na potwierdzenie terminu w ciągu 14 dni)",
                        "📝 PRAWA kolumna — Historia kontaktów (90 dni: wszystkie SMS-y, notatki, próby kontaktu)",
                        "📊 Pasek statusu (dół) — liczniki w czasie rzeczywistym (ile wstawień, stałych klientów, do zadzwonienia)"
                    }
                },
                new()
                {
                    Kat = Kategoria.Codzienna,
                    Ikona = "🌅",
                    Tytul = "Pełna rutyna dnia (czeklista 6 kroków)",
                    KolorAkcent = "#E67E22",
                    Krokowe = true,
                    Czas = "~45 min dziennie",
                    Wynik = "Wszystkie tabele wyzerowane, statystyki dnia widoczne, brak alertów w pasku statusu",
                    Opis = "Co wokół głównego priorytetu. Codzienna checklista — zaczynasz rano, kończysz pod koniec dnia.",
                    Punkty = new()
                    {
                        "Otwórz aplikację i zerknij na PASEK STATUSU (dół) — alerty typu 'X zaległych'",
                        "Wykonaj PRIORYTET NR 1 (zakładka 🔥) — obdzwoń Przypomnienia + Nadchodzące",
                        "Sprawdź czy wpadły nowe rezerwacje od hodowców (lewa kolumna, lista wstawień)",
                        "Jeśli ktoś zadzwonił z nowym wstawieniem — Ctrl+N i dodaj",
                        "Pod koniec dnia zerknij na HISTORIĘ KONTAKTÓW (prawa) — żebyś widział że wpisy są kompletne",
                        "Sprawdź statystyki (📊 Statystyki w pasku) — ile zrobiłeś SMS-ów dziś"
                    }
                },
                new()
                {
                    Kat = Kategoria.Codzienna,
                    Ikona = "📞",
                    Tytul = "Jak potwierdzić wstawienie hodowcy",
                    KolorAkcent = "#1976D2",
                    Szeroka = true,
                    Krokowe = true,
                    Czas = "~1 min/hodowca",
                    Wynik = "Wstawienie ma status 'Potwierdzone' (znika z Nadchodzących), wpis trafia do Historii kontaktów",
                    Opis = "Standardowy workflow: hodowca zaplanował wstawienie, my pytamy czy termin nadal aktualny. Cel: każde nadchodzące wstawienie musi mieć status 'Potwierdzone' najpóźniej 3 dni przed datą.",
                    Punkty = new()
                    {
                        "Otwórz tabelę NADCHODZĄCE WSTAWIENIA (środek dół) — pokazuje wstawienia od 12 dni wstecz do 14 dni naprzód",
                        "Kliknij wiersz hodowcy którego chcesz potwierdzić (zwróć uwagę na kolumnę 'Za' — ile dni zostało)",
                        "Naciśnij prawy przycisk myszy → wybierz '📱 SMS — Pełne potwierdzenie' (lub Krótkie jeśli to stały klient)",
                        "Treść SMS-a została skopiowana do schowka — przełącz się na aplikację telefonu i wklej (Ctrl+V)",
                        "💡 W aplikacji telefonu (Microsoft Phone Link) numer hodowcy zwykle już masz w kontaktach — wystarczy wpisać kilka pierwszych liter nazwiska",
                        "Wyślij SMS i wróć do programu. Wiersz znika z listy na 3 dni (snooze) — żeby Ci nie przeszkadzał",
                        "Gdy hodowca odpowie 'TAK': wróć do wiersza (jeśli ukryty → PPM → '👁️ Pokaż również skontaktowanych'), naciśnij F (potwierdź)",
                        "✓ Wiersz znika na zawsze — wstawienie ma teraz status Potwierdzone. Pojawi się ⭐ przy nazwie hodowcy jeśli to dla niego 4. potwierdzona dostawa"
                    }
                },
                new()
                {
                    Kat = Kategoria.Codzienna,
                    Ikona = "📱",
                    Tytul = "Jak wysłać SMS — krok po kroku",
                    KolorAkcent = "#8E44AD",
                    Krokowe = true,
                    Czas = "~15 sek/SMS",
                    Wynik = "SMS wysłany + automatyczny wpis w Historii kontaktów z avatarem i wariantem",
                    Opis = "Mamy 8 wariantów SMS dla różnych sytuacji. Treść kopiuje się do schowka — wystarczy wkleić w aplikacji telefonu.",
                    Punkty = new()
                    {
                        "Wybierz wiersz hodowcy w dowolnej tabeli (Lista wstawień / Przypomnienia / Nadchodzące)",
                        "💡 Najszybciej: S = SMS krótki, Shift+S = SMS pełny. Wolniej ale z wyborem: PPM → '📱 SMS'",
                        "Zobacz okienko z treścią SMS-a + długością (znaki + szacowana liczba SMS-ów)",
                        "Treść jest już w schowku — otwórz aplikację telefonu (np. Microsoft Phone Link)",
                        "Wybierz odpowiedni numer hodowcy w aplikacji telefonu, wklej (Ctrl+V), wyślij",
                        "✓ Wpis pojawia się automatycznie w Historii kontaktów (po prawej) — z Twoim avatarem i nazwą wariantu"
                    }
                },
                new()
                {
                    Kat = Kategoria.Codzienna,
                    Ikona = "➕",
                    Tytul = "Jak dodać nowe wstawienie",
                    KolorAkcent = "#27AE60",
                    Krokowe = true,
                    Czas = "~30 sek",
                    Wynik = "Nowe wstawienie widoczne w Liście wstawień (lewa) i ewentualnie w Nadchodzących (jeśli <14 dni)",
                    Opis = "Standardowy proces wprowadzania nowej rezerwacji terminu od hodowcy.",
                    Punkty = new()
                    {
                        "Naciśnij Ctrl+N lub kliknij zielony przycisk ➕ Dodaj (górny pasek)",
                        "Wypełnij formularz: hodowca (lista rozwijana z Dostawcy), data, ilość sztuk, typ ceny",
                        "Kliknij Zapisz — wstawienie pojawia się od razu w lewej tabeli (Lista wstawień)",
                        "Sprawdź czy wstawienie jest też widoczne w Nadchodzących (jeśli data <14 dni)",
                        "✓ Jeśli to nowy hodowca który dotąd dostarczył 3-4 razy potwierdzone dostawy — pojawi się żółta ⭐"
                    }
                },
                new()
                {
                    Kat = Kategoria.Codzienna,
                    Ikona = "✏️",
                    Tytul = "Jak zapisać/zmienić numer telefonu hodowcy",
                    KolorAkcent = "#16A085",
                    Szeroka = true,
                    Krokowe = true,
                    Czas = "~30 sek",
                    Wynik = "Numer zapisany w dbo.Dostawcy — dostępny we WSZYSTKICH modułach ZPSP",
                    Opis = "Każdy hodowca może mieć do 3 numerów (Phone1, Phone2, Phone3). Numery są zapisywane w słowniku Dostawcy (LibraNet) — używane są przez WSZYSTKIE moduły ZPSP, nie tylko ten.",
                    Punkty = new()
                    {
                        "Znajdź hodowcę w tabeli PRZYPOMNIENIA lub NADCHODZĄCE — patrz na kolumnę 'Tel'",
                        "Dwuklik na komórce 'Tel' → otwiera się okno 'Numery telefonu hodowcy'",
                        "Wpisz numery w polach Phone1 / Phone2 / Phone3 (możesz zostawić puste — nie wymagamy 3)",
                        "Kliknij 'Zapisz' — system pokaże potwierdzenie '✅ Zapisano numery dla {hodowca}'",
                        "⚠️ Jeśli pojawi się '⚠️ Nie udało się zapisać' — przejdź do zakładki 🚨 Problemy → 'Gdy nie zapisuje się numer'"
                    }
                },

                // ====================================================================
                // 🚨 ZAKŁADKA: PROBLEMY (rozwiązywanie)
                // ====================================================================
                new()
                {
                    Kat = Kategoria.Problem,
                    Ikona = "🚨",
                    Tytul = "Gdy nie zapisuje się numer hodowcy",
                    KolorAkcent = "#E74C3C",
                    Szeroka = true,
                    Krokowe = true,
                    Czas = "~2 min diagnozy",
                    Wynik = "Wiesz dlaczego nie zapisuje + masz dalsze kroki (poprawa w Libra/Raporty.exe)",
                    Opis = "System pokazał '⚠️ Nie udało się zapisać' z listą podobnych nazw. Oto co zrobić.",
                    Punkty = new()
                    {
                        "Przeczytaj okienko od dołu — pokazuje listę 'Podobni hodowcy w bazie' (top 10 zaczynających się na te same 3 znaki)",
                        "Porównaj nazwę z tabeli z nazwami na liście. Najczęściej różnica to spacja/literówka (np. 'Kowalski Jan' vs 'Kowalski  Jan' z podwójną spacją)",
                        "💡 Czasami widać że to ten sam hodowca pod innym ShortName — wtedy poprawne ShortName jest w bazie, my widzimy literówkę gdzieś w WstawieniaKurczakow",
                        "Jeśli znajdziesz właściwą nazwę — otwórz Libra/Raporty.exe → znajdź tego hodowcę → tam ustaw numer",
                        "Jeśli na liście NIE MA pasującego hodowcy — to znaczy że hodowca jest TYLKO w WstawieniaKurczakow, nie ma w słowniku Dostawcy",
                        "⚠️ W takim przypadku trzeba najpierw dodać hodowcę w Libra/Raporty.exe, dopiero potem da się zapisać mu numer w naszej aplikacji"
                    }
                },
                new()
                {
                    Kat = Kategoria.Problem,
                    Ikona = "🐌",
                    Tytul = "Gdy program działa wolno",
                    KolorAkcent = "#F39C12",
                    Krokowe = true,
                    Czas = "~30 sek diagnozy",
                    Wynik = "Raport audytu skopiowany — można wysłać Sergiuszowi",
                    Opis = "Czasami ładowanie może trwać dłużej niż 2-3 sekundy. Oto jak zdiagnozować.",
                    Punkty = new()
                    {
                        "Kliknij '🔍 Audyt' w górnym pasku (prawa strona, fioletowy przycisk)",
                        "System wykonuje pełną diagnostykę — może chwilę potrwać (3-10 sekund)",
                        "Pojawi się okno z raportem — kliknij '📋 Skopiuj raport do schowka'",
                        "Wklej raport w wiadomości do Sergiusza/informatyka — w tym widać dokładnie który krok ile zajął i co go spowalnia"
                    }
                },
                new()
                {
                    Kat = Kategoria.Problem,
                    Ikona = "❓",
                    Tytul = "Gdy hodowca nie odpowiada na SMS",
                    KolorAkcent = "#7F8C8D",
                    Krokowe = true,
                    Opis = "Wysłałeś SMS o potwierdzenie 2-3 dni temu, hodowca milczy. Co zrobić.",
                    Punkty = new()
                    {
                        "PPM na wierszu w Nadchodzących → '👁️ Pokaż również już skontaktowanych' — żeby zobaczyć ukryte wiersze",
                        "Spróbuj WARIANTU innego niż wcześniejszy — np. wcześniej był pełny, teraz krótki (lub odwrotnie)",
                        "💡 Spróbuj DZWONIĆ zamiast SMS-ować — dwuklik na komórce Tel pokazuje wszystkie 3 numery hodowcy",
                        "Jeśli dwukrotnie brak odpowiedzi — naciśnij R (nie odebrał + snooze 3 dni) i wróć później",
                        "⚠️ Po 3-4 nieudanych próbach kontaktu — porozmawiaj z Sergiuszem czy nie usunąć tego wstawienia jako nierealne"
                    }
                },

                // ====================================================================
                // 📚 ZAKŁADKA: REFERENCJA (słowniki, kolory, narzędzia)
                // ====================================================================
                new()
                {
                    Kat = Kategoria.Referencja,
                    Ikona = "⭐",
                    Tytul = "Co oznacza żółta gwiazdka",
                    KolorAkcent = "#F1C40F",
                    Opis = "Żółta ★ przy nazwie hodowcy = nasz aktywny stały klient. Pojawia się automatycznie — nie ustawiasz jej ręcznie.",
                    Punkty = new()
                    {
                        "Zasada: hodowca ma ★ jeśli w 4 ostatnich wstawieniach (data ≤ dziś+20 dni) była co najmniej 1 zrealizowana dostawa",
                        "Status 'zrealizowana' = Bufor jest: Potwierdzony / Sprzedany / B.Wolny / B.Kontr.",
                        "Wstawienia >20 dni do przodu (roczne rezerwacje) NIE liczą się do statystyki",
                        "Gwiazdka jest w Liście wstawień, Przypomnieniach i Nadchodzących — wszędzie ten sam algorytm",
                        "Hodowcy z ★ traktuj priorytetowo — mają większe zaufanie i nie chcemy ich stracić"
                    }
                },
                new()
                {
                    Kat = Kategoria.Referencja,
                    Ikona = "🎨",
                    Tytul = "Co oznaczają kolory wierszy",
                    KolorAkcent = "#9B59B6",
                    Opis = "Kolor tła w tabeli Nadchodzące = pilność wstawienia. Kolor pozwala szybko zorientować się co najpierw.",
                    Punkty = new()
                    {
                        "🟥 Mocno czerwony — wstawienie ZALEGŁE (data już minęła, a wciąż nie potwierdzone). Reaguj natychmiast",
                        "🩷 Jasny czerwonawy — dziś, jutro lub do 3 dni. Wymaga kontaktu dzisiaj",
                        "🟧 Pomarańczowy — 4-7 dni. Skontaktuj się w ciągu 2 dni",
                        "🟩 Zielonkawy — >7 dni. Bez pośpiechu, ale nie zapomnij",
                        "Bold + zielona kreska po lewej = stały klient (też ma ★ przy nazwie)"
                    }
                },
                new()
                {
                    Kat = Kategoria.Referencja,
                    Ikona = "⌨️",
                    Tytul = "Skróty klawiszowe (referencja)",
                    KolorAkcent = "#3498DB",
                    Opis = "Najszybsza praca: kliknij wiersz, naciśnij klawisz. Działa we wszystkich 3 tabelach. Pogrupowane jak na pasku góry.",
                    Punkty = new()
                    {
                        "📱 SMS:  S — krótki  •  Shift+S — pełny",
                        "✅ Akcje:  F — potwierdź  •  R — nie odebrał (snooze 3 dni, tylko Przypomnienia)",
                        "📋 Lista:  Ctrl+N — dodaj  •  Enter — edytuj  •  Del — usuń  •  F5 — odśwież"
                    }
                },
                new()
                {
                    Kat = Kategoria.Referencja,
                    Ikona = "📋",
                    Tytul = "Menu kontekstowe (prawy przycisk myszy)",
                    KolorAkcent = "#E67E22",
                    Opis = "PPM na wierszu = lista akcji dostępnych dla wybranego wstawienia/hodowcy.",
                    Punkty = new()
                    {
                        "✏️ Edytuj wstawienie",
                        "➕ Nowe wstawienie (z kopiowaniem danych z wybranego)",
                        "📅 Zmień datę",
                        "💰 Zmień typ ceny",
                        "✅ Potwierdź wstawienie / ↩️ Cofnij potwierdzenie",
                        "📱 SMS-y (8 wariantów ogólnych + 2 potwierdzenia w Nadchodzących)",
                        "🗑️ Usuń wstawienie",
                        "👁️ Pokaż również już skontaktowanych (tylko w Nadchodzących)"
                    }
                },
                new()
                {
                    Kat = Kategoria.Referencja,
                    Ikona = "📊",
                    Tytul = "Narzędzia w górnym pasku",
                    KolorAkcent = "#34495E",
                    Opis = "Górny pasek (od lewej do prawej): tytuł, szukajka, ➕ Dodaj, legenda skrótów, narzędzia.",
                    Punkty = new()
                    {
                        "🔍 Audyt — diagnostyka wydajności (gdy program wolno działa, użyj tu)",
                        "🐣 Ostatnie wstawienia — okno z hodowcami pogrupowanymi wg statusu (Aktywni / Do wykupienia / Anulowane)",
                        "📊 Statystyki — kto ile wstawień stworzył/potwierdził/SMS-ów wysłał (3 kolumny pracowników)",
                        "❓ Instrukcja — to okno (które właśnie czytasz)",
                        "🔍 Szukajka (lewa strona) — filtruj listę wstawień po nazwie hodowcy",
                        "➕ Dodaj (lewa strona) — utwórz nowe wstawienie (alternatywa: Ctrl+N)"
                    }
                },
                new()
                {
                    Kat = Kategoria.Referencja,
                    Ikona = "💾",
                    Tytul = "Eksport tej instrukcji do PDF",
                    KolorAkcent = "#27AE60",
                    Opis = "Możesz wyeksportować całą tę instrukcję do pliku PDF — np. żeby ją wydrukować i położyć obok komputera.",
                    Punkty = new()
                    {
                        "Kliknij '💾 Eksportuj do PDF' na dole tego okna",
                        "Wybierz lokalizację zapisu (domyślnie Pulpit z dzisiejszą datą)",
                        "Po zapisie pojawi się pytanie 'Otworzyć teraz?' — wybierz TAK żeby zobaczyć rezultat",
                        "PDF zawiera wszystkie sekcje + numerację kroków + stopkę z numerem strony",
                        "💡 Generuj nowy PDF co kwartał — instrukcja będzie się rozszerzać o nowe funkcje"
                    }
                }
            };
        }

        // === BUDOWANIE UI ===
        private FrameworkElement BudujUI()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Nagłówek
            var header = new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromRgb(0x5C, 0x8A, 0x3A),
                    Color.FromRgb(0x4B, 0x73, 0x2F),
                    new Point(0, 0), new Point(1, 1)),
                Padding = new Thickness(24, 18, 24, 18)
            };
            var headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock
            {
                Text = "📚 Instrukcja Obsługi — Cykle Wstawień Kurczaków",
                Foreground = Brushes.White,
                FontSize = 22,
                FontWeight = FontWeights.Bold
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Workflow krok po kroku — scenariusze codziennej pracy + referencja skrótów i narzędzi",
                Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0)
            });
            header.Child = headerStack;
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            // 4 zakładki — Priorytet / Codzienna / Problem / Referencja
            var tabs = new TabControl
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 8, 0, 0),
                Margin = new Thickness(12, 8, 12, 0)
            };
            tabs.Items.Add(BudujZakladke("🔥", "PRIORYTET", "#C0392B", Kategoria.Priorytet));
            tabs.Items.Add(BudujZakladke("📞", "Codzienna praca", "#1976D2", Kategoria.Codzienna));
            tabs.Items.Add(BudujZakladke("🚨", "Problemy", "#E74C3C", Kategoria.Problem));
            tabs.Items.Add(BudujZakladke("📚", "Referencja", "#7F8C8D", Kategoria.Referencja));
            tabs.SelectedIndex = 0; // start od PRIORYTETU
            Grid.SetRow(tabs, 1);
            grid.Children.Add(tabs);

            // Stopka z przyciskami (PDF + Zamknij)
            var footer = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE6, 0xE8)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(20, 12, 20, 12)
            };
            var footerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnPdf = new Button
            {
                Content = BudujGuzikContent("💾", "Eksportuj do PDF"),
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnPdf.Click += BtnEksportPdf_Click;
            footerPanel.Children.Add(btnPdf);

            var btnZamknij = new Button
            {
                Content = BudujGuzikContent("✖", "Zamknij"),
                Padding = new Thickness(16, 8, 16, 8),
                Background = new SolidColorBrush(Color.FromRgb(0x95, 0xA5, 0xA6)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnZamknij.Click += (_, _) => Close();
            footerPanel.Children.Add(btnZamknij);

            footer.Child = footerPanel;
            Grid.SetRow(footer, 2);
            grid.Children.Add(footer);

            return grid;
        }

        private static FrameworkElement BudujGuzikContent(string ikona, string tekst)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = ikona, FontSize = 13, Margin = new Thickness(0, 0, 6, 0) });
            sp.Children.Add(new TextBlock { Text = tekst, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            return sp;
        }

        // Buduje pojedynczą zakładkę TabControl — header (ikona + nazwa) + scroll z kafelkami danej kategorii
        private TabItem BudujZakladke(string ikona, string nazwa, string kolor, Kategoria kategoria)
        {
            var sekcjeKat = _sekcje.FindAll(s => s.Kat == kategoria);
            var akcentBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolor));

            // Header zakładki — ikona + nazwa + licznik sekcji
            var headerSp = new StackPanel { Orientation = Orientation.Horizontal };
            headerSp.Children.Add(new TextBlock { Text = ikona, FontSize = 16, Margin = new Thickness(0, 0, 6, 0) });
            headerSp.Children.Add(new TextBlock
            {
                Text = nazwa,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            });
            headerSp.Children.Add(new Border
            {
                Background = akcentBrush,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = sekcjeKat.Count.ToString(),
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                }
            });

            // Treść zakładki — scroll z kafelkami
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(16)
            };
            var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var s in sekcjeKat)
                wrap.Children.Add(BudujKartke(s));
            scroll.Content = wrap;

            return new TabItem
            {
                Header = headerSp,
                Content = scroll,
                Padding = new Thickness(12, 8, 12, 8)
            };
        }

        private static Border BudujKartke(Sekcja s)
        {
            var akcent = (Color)ColorConverter.ConvertFromString(s.KolorAkcent);
            var akcentBrush = new SolidColorBrush(akcent);

            var card = new Border
            {
                Width = s.Szeroka ? 980 : 480,
                Margin = new Thickness(8),
                Background = Brushes.White,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(0),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(0xC0, 0xC8, 0xCC),
                    Direction = 270,
                    ShadowDepth = 2,
                    BlurRadius = 8,
                    Opacity = 0.35
                }
            };

            var stack = new StackPanel();

            // Pasek nagłówka kafelka — kolor akcent
            var headerBar = new Border
            {
                Background = akcentBrush,
                CornerRadius = new CornerRadius(12, 12, 0, 0),
                Padding = new Thickness(16, 14, 16, 14)
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var ikona = new TextBlock
            {
                Text = s.Ikona,
                FontSize = 28,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(ikona, 0);
            headerGrid.Children.Add(ikona);

            var tytulStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            tytulStack.Children.Add(new TextBlock
            {
                Text = s.Tytul,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap
            });
            if (s.Krokowe)
            {
                int ileKrokow = s.Punkty.FindAll(p => !JestSpecjalnyPunkt(p)).Count;
                tytulStack.Children.Add(new TextBlock
                {
                    Text = $"📋 {ileKrokow} kroków",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }
            Grid.SetColumn(tytulStack, 1);
            headerGrid.Children.Add(tytulStack);

            // Chip czasu (jeśli ustawiony) — biały badge po prawej w nagłówku
            if (!string.IsNullOrEmpty(s.Czas))
            {
                var chipCzas = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 3, 8, 3),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var chipSp = new StackPanel { Orientation = Orientation.Horizontal };
                chipSp.Children.Add(new TextBlock
                {
                    Text = "⏱",
                    FontSize = 11,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 4, 0)
                });
                chipSp.Children.Add(new TextBlock
                {
                    Text = s.Czas,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White
                });
                chipCzas.Child = chipSp;
                Grid.SetColumn(chipCzas, 2);
                headerGrid.Children.Add(chipCzas);
            }
            headerBar.Child = headerGrid;
            stack.Children.Add(headerBar);

            // Treść
            var body = new StackPanel { Margin = new Thickness(16, 12, 16, 16) };

            body.Children.Add(new TextBlock
            {
                Text = s.Opis,
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(0x37, 0x47, 0x4F)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12),
                LineHeight = 18
            });

            // Lista punktów: numerowane kroki LUB bulety, z rozpoznawaniem ⚠️/💡/✓ prefiksów
            int numerKroku = 0; // licznik kroków pomija specjalne ⚠️/💡/✓
            for (int i = 0; i < s.Punkty.Count; i++)
            {
                var punkt = s.Punkty[i];
                if (JestSpecjalnyPunkt(punkt))
                {
                    body.Children.Add(BudujSpecjalnyPunkt(punkt));
                    continue;
                }
                numerKroku++;

                var row = new Grid { Margin = new Thickness(0, s.Krokowe ? 6 : 3, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                if (s.Krokowe)
                {
                    var kolko = new Border
                    {
                        Width = 26,
                        Height = 26,
                        CornerRadius = new CornerRadius(13),
                        Background = akcentBrush,
                        Margin = new Thickness(0, 0, 10, 0),
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    kolko.Child = new TextBlock
                    {
                        Text = numerKroku.ToString(),
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(kolko, 0);
                    row.Children.Add(kolko);
                }
                else
                {
                    var bullet = new Border
                    {
                        Width = 6,
                        Height = 6,
                        CornerRadius = new CornerRadius(3),
                        Background = akcentBrush,
                        Margin = new Thickness(0, 7, 8, 0),
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    Grid.SetColumn(bullet, 0);
                    row.Children.Add(bullet);
                }

                var tb = new TextBlock
                {
                    Text = punkt,
                    FontSize = s.Krokowe ? 11.5 : 10.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x37, 0x47, 0x4F)),
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = s.Krokowe ? 18 : 16,
                    VerticalAlignment = s.Krokowe ? VerticalAlignment.Center : VerticalAlignment.Top
                };
                Grid.SetColumn(tb, 1);
                row.Children.Add(tb);

                body.Children.Add(row);
            }

            // Zielona belka "Co osiągniesz" na końcu (jeśli ustawiona)
            if (!string.IsNullOrEmpty(s.Wynik))
            {
                var wynikBox = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xF5, 0xE9)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                    BorderThickness = new Thickness(0, 0, 0, 0),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 12, 0, 0)
                };
                var wsp = new StackPanel { Orientation = Orientation.Horizontal };
                wsp.Children.Add(new TextBlock
                {
                    Text = "✅",
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Top
                });
                var wynikInner = new StackPanel();
                wynikInner.Children.Add(new TextBlock
                {
                    Text = "Co osiągniesz",
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)),
                    Margin = new Thickness(0, 0, 0, 2)
                });
                wynikInner.Children.Add(new TextBlock
                {
                    Text = s.Wynik,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x1B, 0x5E, 0x20)),
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 16
                });
                wsp.Children.Add(wynikInner);
                wynikBox.Child = wsp;
                body.Children.Add(wynikBox);
            }

            stack.Children.Add(body);
            card.Child = stack;
            return card;
        }

        // Rozpoznaje czy punkt to specjalny box (ostrzeżenie/tip/wynik) — nie liczy się jako "krok"
        private static bool JestSpecjalnyPunkt(string punkt)
        {
            if (string.IsNullOrEmpty(punkt)) return false;
            return punkt.StartsWith("⚠️") || punkt.StartsWith("💡") || punkt.StartsWith("✓");
        }

        // Buduje wyróżniony box (żółty ostrzeżenie / niebieski tip / zielony wynik)
        private static Border BudujSpecjalnyPunkt(string punkt)
        {
            string tresc;
            Color tlo, ramka, tekstKolor;
            string etykieta;
            if (punkt.StartsWith("⚠️"))
            {
                tresc = punkt.Substring("⚠️".Length).TrimStart();
                tlo = Color.FromRgb(0xFF, 0xF3, 0xCD);
                ramka = Color.FromRgb(0xF1, 0xC4, 0x0F);
                tekstKolor = Color.FromRgb(0x85, 0x64, 0x04);
                etykieta = "⚠️ OSTRZEŻENIE";
            }
            else if (punkt.StartsWith("💡"))
            {
                tresc = punkt.Substring("💡".Length).TrimStart();
                tlo = Color.FromRgb(0xE3, 0xF2, 0xFD);
                ramka = Color.FromRgb(0x21, 0x96, 0xF3);
                tekstKolor = Color.FromRgb(0x0D, 0x47, 0xA1);
                etykieta = "💡 TIP";
            }
            else // "✓"
            {
                tresc = punkt.Substring(1).TrimStart();
                tlo = Color.FromRgb(0xE8, 0xF5, 0xE9);
                ramka = Color.FromRgb(0x4C, 0xAF, 0x50);
                tekstKolor = Color.FromRgb(0x1B, 0x5E, 0x20);
                etykieta = "✓ EFEKT";
            }

            var box = new Border
            {
                Background = new SolidColorBrush(tlo),
                BorderBrush = new SolidColorBrush(ramka),
                BorderThickness = new Thickness(0, 0, 0, 0),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 6, 0, 0)
            };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = etykieta,
                FontSize = 8.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(tekstKolor),
                Margin = new Thickness(0, 0, 0, 3)
            });
            sp.Children.Add(new TextBlock
            {
                Text = tresc,
                FontSize = 11,
                Foreground = new SolidColorBrush(tekstKolor),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 16
            });
            box.Child = sp;
            return box;
        }

        // === EKSPORT PDF (QuestPDF) ===
        private void BtnEksportPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var save = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"Instrukcja_Cykle_Wstawien_{DateTime.Now:yyyy-MM-dd}.pdf"
                };
                if (save.ShowDialog() != true) return;

                byte[] pdf = GenerujPdf();
                File.WriteAllBytes(save.FileName, pdf);

                var result = MessageBox.Show(
                    $"✅ Zapisano: {save.FileName}\n\nOtworzyć teraz?",
                    "Eksport PDF",
                    MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = save.FileName,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd eksportu PDF:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private byte[] GenerujPdf()
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(28);
                    page.DefaultTextStyle(t => t.FontSize(10).FontColor("#37474F"));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("📚 Instrukcja Obsługi — Cykle Wstawień Kurczaków")
                            .FontSize(20).Bold().FontColor("#5C8A3A");
                        col.Item().Text("Centrum zarządzania zakupami żywca — ZPSP / Piórkowscy")
                            .FontSize(11).FontColor("#7F8C8D");
                        col.Item().PaddingTop(2).Text($"Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}")
                            .FontSize(9).FontColor("#95A5A6");
                        col.Item().PaddingTop(6).LineHorizontal(2).LineColor("#5C8A3A");
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Strona ").FontSize(8).FontColor("#95A5A6");
                        t.CurrentPageNumber().FontSize(8).FontColor("#95A5A6");
                        t.Span(" z ").FontSize(8).FontColor("#95A5A6");
                        t.TotalPages().FontSize(8).FontColor("#95A5A6");
                        t.Span("   •   Ubojnia Drobiu Piórkowscy").FontSize(8).FontColor("#95A5A6");
                    });

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Spacing(14);

                        // Grupowanie po kategoriach + nagłówek kategorii (jak zakładki w UI)
                        var kategorie = new (Kategoria K, string Ikona, string Nazwa, string Kolor)[]
                        {
                            (Kategoria.Priorytet, "🔥", "PRIORYTET", "#C0392B"),
                            (Kategoria.Codzienna, "📞", "Codzienna praca", "#1976D2"),
                            (Kategoria.Problem, "🚨", "Problemy", "#E74C3C"),
                            (Kategoria.Referencja, "📚", "Referencja", "#7F8C8D")
                        };

                        foreach (var (kat, ikonaKat, nazwaKat, kolorKat) in kategorie)
                        {
                            var sekcjeKat = _sekcje.FindAll(s => s.Kat == kat);
                            if (sekcjeKat.Count == 0) continue;

                            // Nagłówek kategorii — kolorowa belka
                            col.Item().Background(kolorKat).Padding(10).Row(row =>
                            {
                                row.AutoItem().PaddingRight(8).Text(ikonaKat).FontSize(18);
                                row.RelativeItem().AlignMiddle().Text($"{nazwaKat}  ({sekcjeKat.Count})")
                                    .FontSize(13).Bold().FontColor("#FFFFFF");
                            });

                            foreach (var s in sekcjeKat)
                            {
                                col.Item().Background("#F8F9FA").Padding(12).Column(cInner =>
                                {
                                    cInner.Spacing(6);

                                    cInner.Item().Row(row =>
                                    {
                                        row.AutoItem().PaddingRight(8).Text(s.Ikona).FontSize(20);
                                        row.RelativeItem().AlignMiddle().Column(c =>
                                        {
                                            c.Item().Text(s.Tytul).FontSize(14).Bold().FontColor(s.KolorAkcent);
                                            if (s.Krokowe)
                                            {
                                                int ileKrokow = s.Punkty.FindAll(p => !JestSpecjalnyPunkt(p)).Count;
                                                c.Item().Text($"📋 {ileKrokow} kroków").FontSize(9).FontColor("#95A5A6");
                                            }
                                        });
                                        if (!string.IsNullOrEmpty(s.Czas))
                                        {
                                            row.AutoItem().AlignMiddle().Background(s.KolorAkcent).Padding(4)
                                                .Text($"⏱ {s.Czas}").FontSize(9).Bold().FontColor("#FFFFFF");
                                        }
                                    });

                                    cInner.Item().PaddingTop(2).Text(s.Opis).FontSize(10).FontColor("#37474F");

                                    int numerKroku = 0;
                                    for (int i = 0; i < s.Punkty.Count; i++)
                                    {
                                        var punkt = s.Punkty[i];

                                        if (JestSpecjalnyPunkt(punkt))
                                        {
                                            string tresc;
                                            string tlo, txtKol, etyk;
                                            if (punkt.StartsWith("⚠️"))
                                            {
                                                tresc = punkt.Substring("⚠️".Length).TrimStart();
                                                tlo = "#FFF3CD"; txtKol = "#856404"; etyk = "⚠️ OSTRZEŻENIE";
                                            }
                                            else if (punkt.StartsWith("💡"))
                                            {
                                                tresc = punkt.Substring("💡".Length).TrimStart();
                                                tlo = "#E3F2FD"; txtKol = "#0D47A1"; etyk = "💡 TIP";
                                            }
                                            else // ✓
                                            {
                                                tresc = punkt.Substring(1).TrimStart();
                                                tlo = "#E8F5E9"; txtKol = "#1B5E20"; etyk = "✓ EFEKT";
                                            }
                                            cInner.Item().PaddingTop(4).Background(tlo).Padding(8).Column(box =>
                                            {
                                                box.Item().Text(etyk).FontSize(8).Bold().FontColor(txtKol);
                                                box.Item().PaddingTop(2).Text(tresc).FontSize(10).FontColor(txtKol);
                                            });
                                            continue;
                                        }

                                        numerKroku++;
                                        int currentNumer = numerKroku;
                                        cInner.Item().PaddingTop(s.Krokowe ? 4 : 1).Row(row =>
                                        {
                                            if (s.Krokowe)
                                            {
                                                row.ConstantItem(22).AlignMiddle()
                                                    .Background(s.KolorAkcent).Padding(2)
                                                    .AlignCenter().AlignMiddle()
                                                    .Text(currentNumer.ToString()).FontSize(10).Bold().FontColor("#FFFFFF");
                                                row.ConstantItem(8);
                                                row.RelativeItem().AlignMiddle().Text(punkt).FontSize(10).FontColor("#37474F");
                                            }
                                            else
                                            {
                                                row.ConstantItem(16).AlignMiddle()
                                                    .Text("•").FontSize(12).FontColor(s.KolorAkcent).Bold();
                                                row.RelativeItem().Text(punkt).FontSize(10).FontColor("#546E7A");
                                            }
                                        });
                                    }

                                    if (!string.IsNullOrEmpty(s.Wynik))
                                    {
                                        cInner.Item().PaddingTop(6).Background("#E8F5E9").Padding(8).Column(box =>
                                        {
                                            box.Item().Text("✅ Co osiągniesz").FontSize(8).Bold().FontColor("#2E7D32");
                                            box.Item().PaddingTop(2).Text(s.Wynik).FontSize(10).FontColor("#1B5E20");
                                        });
                                    }
                                });
                            }
                        }
                    });
                });
            }).GeneratePdf();
        }
    }
}
