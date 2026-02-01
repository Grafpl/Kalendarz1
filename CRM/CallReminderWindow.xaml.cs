using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Data.SqlClient;
using Kalendarz1.CRM.Models;
using Kalendarz1.CRM.Services;
using Kalendarz1.CRM.Dialogs;

namespace Kalendarz1.CRM
{
    public class CallPhase
    {
        public string Name { get; set; }
        public string IconPath { get; set; }
        public string[] Scripts { get; set; }
        public string[] Tips { get; set; }
    }

    public class Objection
    {
        public string ClientSays { get; set; }
        public string Response { get; set; }
    }

    public partial class CallReminderWindow : Window
    {
        private readonly string _connectionString;
        private readonly string _userID;
        private readonly CallReminderConfig _config;
        private ObservableCollection<ContactToCall> _contacts;
        private ContactToCall _selectedContact;
        private int _reminderLogID;
        private int _callsCount = 0;
        private int _notesCount = 0;
        private int _statusChangesCount = 0;

        private int _currentPhase = 0;
        private int _currentScriptIndex = 0;
        private readonly Random _rng = new Random();

        // SVG Path data for phase icons
        private static readonly string IconWstep = "M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm3.5-9c.83 0 1.5-.67 1.5-1.5S16.33 8 15.5 8 14 8.67 14 9.5s.67 1.5 1.5 1.5zm-7 0c.83 0 1.5-.67 1.5-1.5S9.33 8 8.5 8 7 8.67 7 9.5 7.67 11 8.5 11zm3.5 6.5c2.33 0 4.31-1.46 5.11-3.5H6.89c.8 2.04 2.78 3.5 5.11 3.5z";
        private static readonly string IconPotrzeby = "M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm.5-13H11v6l5.25 3.15.75-1.23-4.5-2.67z";
        private static readonly string IconOferta = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z";
        private static readonly string IconZamknij = "M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-5 14H7v-2h7v2zm3-4H7v-2h10v2zm0-4H7V7h10v2z";

        private readonly List<CallPhase> _phases = new List<CallPhase>
        {
            // ═══════════════════════════════════════════════════════
            // FAZA 1: WSTĘP - Przedstawienie się i nawiązanie kontaktu
            // ═══════════════════════════════════════════════════════
            new CallPhase
            {
                Name = "Wstęp",
                IconPath = "M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm3.5-9c.83 0 1.5-.67 1.5-1.5S16.33 8 15.5 8 14 8.67 14 9.5s.67 1.5 1.5 1.5zm-7 0c.83 0 1.5-.67 1.5-1.5S9.33 8 8.5 8 7 8.67 7 9.5 7.67 11 8.5 11zm3.5 6.5c2.33 0 4.31-1.46 5.11-3.5H6.89c.8 2.04 2.78 3.5 5.11 3.5z",
                Scripts = new[]
                {
                    "Dzień dobry! Nazywam się [imię] i dzwonię z Ubojni Drobiu Piórkowscy. Czy rozmawiam z osobą odpowiedzialną za zaopatrzenie w mięso drobiowe?",
                    "Dzień dobry, [imię], Ubojnia Drobiu Piórkowscy. Dzwonię, ponieważ współpracujemy z firmami z Państwa branży w zakresie dostaw świeżego mięsa z kurczaka. Czy mogę rozmawiać z osobą decyzyjną?",
                    "Dzień dobry! Dzwonię z Ubojni Drobiu Piórkowscy - jesteśmy producentem i dostawcą mięsa drobiowego, tuszki kurczaka i elementy. Szukam osoby odpowiedzialnej za zakupy.",
                    "Dzień dobry, z tej strony [imię] z Ubojni Drobiu Piórkowscy. Zajmujemy się dostawami świeżego drobiu dla sklepów i gastronomii. Chciałbym porozmawiać z kimś z działu zakupów.",
                    "Dzień dobry! [imię], Ubojnia Drobiu Piórkowscy. Widzę, że Państwa firma działa w branży spożywczej - dostarczamy świeży drób z krótkim łańcuchem dostaw prosto z naszej ubojni. Z kim mogę porozmawiać?",
                    "Dzień dobry, dzwonię z Ubojni Drobiu Piórkowscy. Specjalizujemy się w dostawach tuszek kurcząt i elementów drobiowych. Czy Pan/Pani zajmuje się zamówieniami mięsa?",
                    "Dzień dobry! Nazywam się [imię], Ubojnia Drobiu Piórkowscy. Pomagamy sklepom i restauracjom w zaopatrzeniu w najświeższy drób w regionie. Czy to dobry numer do rozmowy o współpracy?",
                    "Dzień dobry, tu [imię] z Ubojni Drobiu Piórkowscy. Dzwonię do Państwa, bo chcielibyśmy zaproponować stałe dostawy świeżego kurczaka prosto z ubojni. Z kim najlepiej porozmawiać?",
                    "Dzień dobry! Ubojnia Drobiu Piórkowscy, [imię] przy telefonie. Jesteśmy bezpośrednim dostawcą drobiu - tuszka kurczaka, filet, skrzydełka, udka. Czy jest ktoś od zaopatrzenia?",
                    "Dzień dobry, [imię] z Ubojni Drobiu Piórkowscy. Widziałem, że prowadzicie [typ działalności]. Dostarczamy drób najwyższej jakości firmom w Państwa regionie. Czy mogę chwilę porozmawiać?",
                },
                Tips = new[]
                {
                    "Mów pewnie i wyraźnie. Pierwsze 10 sekund decyduje o rozmowie.",
                    "Uśmiechnij się - rozmówca usłyszy to w Twoim głosie!",
                    "Mów wolno i spokojnie. Pośpiech = brak profesjonalizmu.",
                    "Stań podczas rozmowy - Twój głos będzie bardziej energiczny.",
                    "Przed telefonem przeczytaj nazwę firmy głośno, żeby się nie zająknąć.",
                    "Pamiętaj: to nie jest prośba - oferujesz wartość!",
                    "⚠️ ZAWSZE zapytaj o EMAIL i od razu zapisz w notatce! To Twój najważniejszy cel.",
                    "Nawet jeśli odmówi - zapisz notatkę KTO odebrał, KIEDY dzwonić ponownie.",
                    "Każdy kontakt to szansa! Nie marnuj - jeśli nie kupuje, zapytaj KTO jest ich dostawcą.",
                }
            },

            // ═══════════════════════════════════════════════════════
            // FAZA 2: BADANIE POTRZEB - Pytania i rozpoznanie
            // ═══════════════════════════════════════════════════════
            new CallPhase
            {
                Name = "Potrzeby",
                IconPath = "M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm.5-13H11v6l5.25 3.15.75-1.23-4.5-2.67z",
                Scripts = new[]
                {
                    "Czy ma Pan/Pani chwilę? Chciałbym dowiedzieć się, jakie produkty drobiowe obecnie kupujecie i w jakich ilościach? U Piórkowscy mamy pełen asortyment kurczaka.",
                    "Rozumiem, że czas jest cenny. Powiem krótko - Piórkowscy to ubojnia drobiu, dostarczamy świeży drób prosto od nas. Jakie elementy kurczaka kupujecie najczęściej?",
                    "Czy obecnie macie stałego dostawcę drobiu? Co jest dla Państwa najważniejsze - cena, jakość, regularność dostaw? U Piórkowscy łączymy wszystko.",
                    "Ile mniej więcej kilogramów drobiu zamawiają Państwo tygodniowo? Zależy Państwu bardziej na tuszkach całych czy elementach - filet, udka, skrzydła?",
                    "Z kim obecnie współpracujecie w zakresie drobiu? Co Państwu pasuje, a co byście chcieli poprawić? Piórkowscy jako ubojnia dajemy najkrótszy łańcuch dostaw.",
                    "Jak wygląda Państwa typowe zamówienie drobiu? Potrzebujecie dostaw codziennych, czy 2-3 razy w tygodniu? Piórkowscy dostarczamy 6 dni w tygodniu.",
                    "Czy oprócz tuszki kurczaka interesują Państwa elementy - filet z piersi, ćwiartki, udka, skrzydełka, podudzia? U Piórkowscy rozbieramy na miejscu w ubojni.",
                    "Jakie standardy jakości są dla Państwa kluczowe? U Piórkowscy mamy pełne certyfikaty - HACCP, weterynaryjne. Wszystko ze świeżych ubojów, nie mrożone.",
                    "Czy ważny jest dla Państwa termin przydatności? U Piórkowscy drób jedzie prosto z ubojni - max 24h od uboju do Państwa chłodni.",
                    "W jakich opakowaniach preferujecie dostawy? Piórkowscy pakujemy w karton, tacki lub worki. Możemy dopasować gramaturę do Państwa potrzeb.",
                    "Jak duży mają Państwo obrót mięsem drobiowym tygodniowo? Pytam, bo u Piórkowscy mamy progi cenowe zależne od wolumenu - chcę dać najlepszą ofertę.",
                    "Czy kupujecie również drób mrożony do zapasu, czy tylko świeży? U Piórkowscy mamy oba warianty prosto z ubojni w konkurencyjnych cenach.",
                    "A kto Państwu obecnie dostarcza drób? Pytam, bo chcę wiedzieć z kim konkuruję i dać lepszą ofertę. Ile mniej więcej płacicie za kg tuszki?",
                    "Na jaki email mogę wysłać ofertę cenową z Ubojni Piórkowscy? Chcę żebyście mieli czarno na białym do porównania z obecnym dostawcą.",
                },
                Tips = new[]
                {
                    "Słuchaj 70%, mów 30%. Im więcej klient mówi, tym bliżej jesteś zamknięcia.",
                    "Notuj słowa kluczowe klienta i powtarzaj je - poczuje się wysłuchany.",
                    "Pytania otwarte dają 5x więcej informacji niż zamknięte.",
                    "Nie przerywaj! Pauza po pytaniu = klient powie więcej.",
                    "⚠️ ZAPISZ W NOTATCE: ile kg/tydzień, jakie elementy, jak często, kto decydent, EMAIL!",
                    "Jeśli mówi o problemach z obecnym dostawcą - NOTUJ! To Twoja szansa.",
                    "🏪 Mały sklep? ZAPYTAJ: «Kto Państwu dostarcza drób?» - zapisz nazwę dostawcy, potem DO NIEGO zadzwoń!",
                    "⚠️ Zapytaj: «Na jaki email mogę wysłać ofertę?» - BEZ MAILA nie ma follow-up!",
                    "NOTATKA OBOWIĄZKOWA: imię rozmówcy, stanowisko, email, telefon bezpośredni, co go interesuje.",
                    "Sklep mówi «mamy dostawcę»? Zapisz KTO to jest! Ten dostawca = potencjalny DUŻY klient Piórkowscy!",
                }
            },

            // ═══════════════════════════════════════════════════════
            // FAZA 3: OFERTA - Prezentacja wartości
            // ═══════════════════════════════════════════════════════
            new CallPhase
            {
                Name = "Oferta",
                IconPath = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z",
                Scripts = new[]
                {
                    "Piórkowscy mają pełną gamę drobiu: tuszki kurczaka klasy A, filet z piersi, udka, podudzia, skrzydełka, ćwiartki. Wszystko świeże, prosto z naszej ubojni. Dostarczamy 6 dni w tygodniu, max 24h od uboju.",
                    "Ubojnia Piórkowscy to przede wszystkim świeżość i ceny producenckie. Tuszka kurczaka klasy A, elementy pakowane wg Państwa specyfikacji. Bez pośredników - prosto z ubojni do Państwa.",
                    "Piórkowscy współpracują z wieloma firmami w Państwa regionie. Oferujemy: stałe ceny na ustalony okres, elastyczne terminy dostaw i pełen asortyment kurczaka - od tuszki po podroby.",
                    "Wyróżnia nas to, że jesteśmy ubojnią - drób od uboju u Piórkowscy do Państwa chłodni w max 24h. Gwarantujemy certyfikat weterynaryjny, stałą jakość i terminowość.",
                    "Dla stałych odbiorców Piórkowscy mają specjalne warunki: gwarantowane ceny na 2-4 tygodnie, priorytet dostaw, elastyczne minimum. Tuszka, filet, udka - pełna gama elementów.",
                    "Piórkowscy to polska ubojnia drobiu. Pełna dokumentacja, certyfikaty, badania weterynaryjne. Pakujemy w karton lub tacki - jak Państwu wygodniej. Ceny producenckie bez marży pośrednika.",
                    "Co wyróżnia Ubojnię Piórkowscy: 1) Ceny producenckie bez pośredników, 2) Dostawy 6 dni w tygodniu, 3) Świeżość max 24h od uboju, 4) Elastyczne pakowanie, 5) Stały opiekun handlowy.",
                    "Z Ubojni Piórkowscy mogę zaproponować tuszki kurczaka klasy A w cenie producenckiej. Do tego elementy: filet, udka, skrzydełka. Przygotować szczegółowy cennik?",
                    "U Piórkowscy dla firm zamawiających powyżej 200kg/tydzień mamy specjalny program: stałe ceny, priorytet dostaw, reklamacje w 24h. Czy to ilości, które Państwo zamawiają?",
                    "Ubojnia Piórkowscy - mięso drobiowe to nasza specjalność od lat. Tuszka, filet z piersi, noga ćwiartkowa, udko, podudzie, skrzydło, filet z udka - mamy w ciągłej dostępności.",
                    "U Piórkowscy pracujemy z najlepszymi fermami. Kurczaki karmione paszą najwyższej jakości, pełne certyfikaty. Gwarantuję - nie znajdziecie świeższego drobiu w regionie.",
                    "Mogę wysłać próbną partię z Ubojni Piórkowscy, żebyście ocenili jakość. Bez zobowiązań. Jeśli Państwu odpowie - ustalamy warunki stałej współpracy. Co Pan/Pani na to?",
                },
                Tips = new[]
                {
                    "Mów językiem korzyści, nie cech. Nie 'mamy X' ale 'dzięki X zaoszczędzicie Y'.",
                    "Używaj konkretnych liczb: '24h od uboju' brzmi lepiej niż 'bardzo świeże'.",
                    "Odwołuj się do tego, co klient powiedział wcześniej o swoich potrzebach.",
                    "Social proof: 'Inne firmy z branży zauważyły, że...' działa świetnie.",
                    "Nie dawaj ceny od razu. Najpierw pokaż wartość, potem rozmawiaj o pieniądzach.",
                    "Próbna dostawa to świetny sposób na obniżenie bariery wejścia!",
                    "⚠️ Podaj cenę dopiero gdy znasz ilości! Najpierw zapytaj o wolumen, potem daj najlepszą cenę.",
                    "ZAPISZ w notatce: jakie elementy go interesują, ile kg, jak często, obecna cena jeśli poda.",
                    "🏪 Mały sklep nie kupi dużo? Zapytaj KTO ICH ZAOPATRUJE - ten hurtownik to Twój następny telefon!",
                    "⚠️ Przed końcem rozmowy: «Na jaki email wysłać ofertę?» - TO JEST OBOWIĄZKOWE!",
                }
            },

            // ═══════════════════════════════════════════════════════
            // FAZA 4: ZAMKNIĘCIE - Ustalenie następnych kroków
            // ═══════════════════════════════════════════════════════
            new CallPhase
            {
                Name = "Zamknięcie",
                IconPath = "M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-5 14H7v-2h7v2zm3-4H7v-2h10v2zm0-4H7V7h10v2z",
                Scripts = new[]
                {
                    "Świetnie! Przygotuję ofertę cenową z Ubojni Piórkowscy dopasowaną do Państwa potrzeb. Czy mogę przesłać ją mailem? Jaki adres? Kiedy mogę oddzwonić, żeby omówić?",
                    "Rozumiem Państwa potrzeby. Proponuję tak: wyślę cennik z Ubojni Piórkowscy, a jutro/pojutrze oddzwonię. Na jaki email wysłać ofertę?",
                    "Czy możemy umówić próbną dostawę z Ubojni Piórkowscy? Bez zobowiązań, żebyście przetestowali jakość naszego drobiu. Jakie ilości i elementy przygotować?",
                    "Widzę potencjał współpracy. Mogę przyjechać z próbkami od Piórkowscy i cennikiem. Kiedy Państwu pasuje spotkanie?",
                    "Dobrze, żebyśmy nie tracili czasu - wyślę ofertę Piórkowscy do końca dnia. Kiedy mogę zadzwonić, żeby ustalić pierwszą dostawę? Czwartek, piątek?",
                    "Zaproponuję tak: wyślę cennik Ubojni Piórkowscy na interesujące Państwa elementy, a w przyszłym tygodniu odezwę się po szczegóły. Zgoda?",
                    "Jestem przekonany, że będziecie zadowoleni drobiem od Piórkowscy. Mogę przygotować pierwszą dostawę próbną na przyszły tydzień. Ile kg tuszek/elementów zaplanować?",
                    "Podsumowując: interesują Państwa [elementy] z Ubojni Piórkowscy, dostawy [częstotliwość], ok. [ilość] kg. Przygotowuję ofertę i dzwonię w [dzień]. Dobrze?",
                    "Bardzo się cieszę z rozmowy. Następny krok: wysyłam ofertę Ubojni Piórkowscy mailem. Oddzwonię we wtorek. Jaki najlepszy email?",
                    "To co proponuję: 1) Dziś wysyłam cennik Piórkowscy, 2) Jutro dzwonię omówić, 3) Ustalamy pierwszą dostawę próbną. Brzmi dobrze?",
                    "Dziękuję za rozmowę! Przygotuję indywidualną ofertę z Ubojni Piórkowscy. Wolą Państwo kontakt mailowy czy telefoniczny?",
                    "Świetna rozmowa. Zapiszę: oddzwonić [data], oferta Piórkowscy na [elementy]. Czy jest coś jeszcze, o czym powinienem pamiętać?",
                    "Zanim się rozłączymy - jaki jest Pana/Pani najlepszy email? Wyślę ofertę Piórkowscy jeszcze dziś, żebyście mieli wszystko czarno na białym.",
                    "Jeszcze jedno - chcę mieć pewność że oferta do Państwa dotrze. Jaki email? I czy jest numer bezpośredni, żebym nie musiał przechodzić przez centralę?",
                },
                Tips = new[]
                {
                    "Zawsze ustal KONKRETNY następny krok: data, godzina, co wyślesz.",
                    "⚠️ OBOWIĄZKOWA NOTATKA PO ROZMOWIE: email, ilości, elementy, termin follow-up, imię rozmówcy!",
                    "Próbna dostawa to najlepsze zamknięcie - obniża ryzyko klienta do zera.",
                    "Podsumuj rozmowę własnymi słowami - klient poczuje się wysłuchany.",
                    "Nie kończ rozmowy bez planu! Bez follow-up = stracona szansa.",
                    "Umów KONKRETNY dzień oddzwonienia - nie 'kiedyś w przyszłym tygodniu'. ZAPISZ W NOTATCE!",
                    "⚠️ BEZ EMAILA = stracony kontakt! Zapytaj: «Jaki najlepszy email do przesłania oferty?»",
                    "NOTATKA MUSI ZAWIERAĆ: 1) Email 2) Imię rozmówcy 3) Co go interesuje 4) Kiedy oddzwonić 5) Ile kg",
                    "🏪 Nawet przy odmowie ZAPISZ: kto dostawca, ile zamawiają, kiedy kończy się umowa. Każda info = wartość!",
                    "Po rozmowie OD RAZU pisz notatkę! Za godzinę zapomnisz szczegóły. Rób to NATYCHMIAST.",
                    "Jeśli mały sklep podał nazwę dostawcy - ZAPISZ i dodaj do kontaktów! To może być Twój największy klient!",
                }
            }
        };

        // ═══════════════════════════════════════════════════════
        // BAZA OBIEKCJI I RIPOST - Mięso drobiowe / kurczak
        // ═══════════════════════════════════════════════════════
        private static readonly Objection[] AllObjections = new[]
        {
            // --- Czas / Zainteresowanie ---
            new Objection { ClientSays = "Nie mam czasu", Response = "Rozumiem, jest Pan/Pani zajęty/a. Kiedy mogę oddzwonić? Rozmowa o Ubojni Piórkowscy zajmie max 3 minuty, a może zaoszczędzić sporo na dostawach drobiu." },
            new Objection { ClientSays = "Nie jestem zainteresowany", Response = "Rozumiem. Ale jeśli kupujecie drób - naprawdę warto poznać ceny Piórkowscy. Jako ubojnia dajemy ceny producenckie, klienci oszczędzają 10-15%." },
            new Objection { ClientSays = "Proszę zadzwonić później", Response = "Jasne! Kiedy dokładnie będzie dobry moment? Chcę uszanować Pana/Pani czas. Może jutro rano albo po południu?" },
            new Objection { ClientSays = "Proszę wysłać ofertę mailem", Response = "Oczywiście! Na jaki adres? Wyślę cennik Ubojni Piórkowscy z pełnym asortymentem. Kiedy mogę oddzwonić, żeby omówić?" },
            new Objection { ClientSays = "Nie potrzebujemy drobiu", Response = "Rozumiem. A czy w przyszłości planujecie wprowadzić drób? Piórkowscy chętnie zostawią kontakt na wypadek zmiany sytuacji." },
            new Objection { ClientSays = "Wyślijcie katalog, jak będziemy potrzebować to zadzwonimy", Response = "Jasne, wyślę katalog Piórkowscy. Ale pozwolę sobie oddzwonić za tydzień - oferty szybko giną w codziennej pracy. Na jaki mail?" },
            new Objection { ClientSays = "Teraz nie sezon na zmiany dostawcy", Response = "Rozumiem. A kiedy zaczyna się u Państwa sezon zakupowy? Piórkowscy mogą przygotować ofertę z wyprzedzeniem, żebyście mieli czas porównać." },

            // --- Dostawca / Konkurencja ---
            new Objection { ClientSays = "Mamy już dostawcę drobiu", Response = "To naturalne. Ale Piórkowscy jako ubojnia dajemy ceny producenckie. Czy mogę zapytać - co cenicie u obecnego dostawcy? Może damy coś lepszego?" },
            new Objection { ClientSays = "Jesteśmy zadowoleni z obecnego dostawcy", Response = "To świetnie! Wielu klientów Piórkowscy ma dwóch dostawców - dla bezpieczeństwa i porównania. Może próbna dostawa z naszej ubojni?" },
            new Objection { ClientSays = "Mamy umowę z innym dostawcą", Response = "Rozumiem. Na jak długo obowiązuje? Piórkowscy mogą przygotować ofertę, żebyście mieli porównanie gdy umowa się skończy." },
            new Objection { ClientSays = "Kupujemy drób w hurtowni/na giełdzie", Response = "Na giełdzie ceny bywają zmienne. Piórkowscy jako ubojnia gwarantujemy stałą cenę na 2-4 tygodnie i dostawę pod drzwi. Ile zamawiają Państwo tygodniowo?" },
            new Objection { ClientSays = "Mamy swojego dostawcę od lat", Response = "Szanuję lojalność. Ale Piórkowscy proponują próbną dostawę - porównacie jakość i cenę producencką bez żadnych zobowiązań." },
            new Objection { ClientSays = "Nasz dostawca daje lepsze ceny", Response = "Piórkowscy to ubojnia - nasze ceny są producenckie. Czy porównywaliście przy tych samych parametrach? Klasa A, max 24h od uboju. Wyślę próbkę?" },
            new Objection { ClientSays = "Wasz konkurent X jest tańszy", Response = "Czy to cena za ten sam produkt? U Piórkowscy tuszka klasy A, 24h od uboju, wydajność mięsna powyżej 70%. Tańszy produkt może mieć gorszą wydajność." },
            new Objection { ClientSays = "Bierzemy od lokalnego rolnika", Response = "Rozumiem. Ale czy rolnik daje certyfikaty, faktury, stałe ceny i dostawy 6 dni w tygodniu? Piórkowscy łączymy świeżość z profesjonalizmem." },

            // --- Cena ---
            new Objection { ClientSays = "Za drogie / nie stać nas", Response = "Piórkowscy to ceny producenckie - bez marży pośrednika. Ile płacicie za kg tuszki? Porównajmy - może Państwa zaskoczymy." },
            new Objection { ClientSays = "Ile to kosztuje?", Response = "U Piórkowscy ceny zależą od ilości i elementów. Ile kg tygodniowo zamawiają Państwo? Dam najlepszą cenę producencką." },
            new Objection { ClientSays = "Tańszy drób kupimy gdzie indziej", Response = "Najtańsze nie zawsze najlepsze w mięsie. Tuszka Piórkowscy klasy A, 24h od uboju, ma lepszą wydajność niż tańszy produkt. Przetestujcie?" },
            new Objection { ClientSays = "Nie mamy budżetu na zmianę dostawcy", Response = "Zmiana na Piórkowscy nic nie kosztuje! Jako ubojnia dajemy ceny producenckie - możecie zaoszczędzić. Mogę przygotować kalkulację." },
            new Objection { ClientSays = "Muszę porównać ceny", Response = "Jak najbardziej! Wyślę cennik Ubojni Piórkowscy - tuszki, filet, udka, skrzydła. Pełen katalog do porównania." },
            new Objection { ClientSays = "Na razie nie mamy pieniędzy", Response = "Rozumiem. A gdy sytuacja się poprawi - Piórkowscy będziemy gotowi. Mogę oddzwonić za miesiąc? W międzyczasie wyślę cennik." },

            // --- Jakość / Wątpliwości ---
            new Objection { ClientSays = "Skąd macie drób?", Response = "Piórkowscy to własna ubojnia drobiu. Drób z certyfikowanych polskich ferm, ubój na miejscu. Pełna dokumentacja, HACCP, certyfikaty weterynaryjne." },
            new Objection { ClientSays = "A co z jakością?", Response = "Jakość to fundament Ubojni Piórkowscy. Tuszka klasy A, max 24h od uboju, transport chłodniczy 0-4°C. Proponuję dostawę próbną - sami ocenicie." },
            new Objection { ClientSays = "Jak gwarantujecie świeżość?", Response = "Piórkowscy to ubojnia - łańcuch od uboju do Państwa chłodni max 24h. Transport własnymi chłodniami 0-4°C. Każda partia z datą uboju." },
            new Objection { ClientSays = "Czy macie certyfikaty?", Response = "Ubojnia Piórkowscy - pełna dokumentacja: certyfikat weterynaryjny, HACCP, decyzja PIW, numer zakładu. Przesłać kopie?" },
            new Objection { ClientSays = "Byliśmy już spaleni przez dostawcę", Response = "Rozumiem obawy. Piórkowscy proponują małą dostawę próbną - zero zobowiązań. Sprawdzicie jakość, terminowość - potem zdecydujecie." },
            new Objection { ClientSays = "Wolimy mrożone, bo dłużej się trzyma", Response = "Piórkowscy mamy też mrożony drób. Ale przy dostawach 2-3x/tydz. świeży z ubojni jest smaczniejszy i klienci go preferują. Mogę zaproponować oba?" },
            new Objection { ClientSays = "Ostatnio drób był słabej jakości", Response = "U Piórkowscy jakość jest stała - własna ubojnia = pełna kontrola. Każda partia sprawdzana przez lekarza weterynarii. Przetestujcie nas próbną dostawą." },
            new Objection { ClientSays = "Tuszki są za małe / za duże", Response = "U Piórkowscy mamy tuszki w różnych gramaturach - od 1.2kg do 2.2kg. Jakie wagi Państwu odpowiadają? Dopasujemy partię." },

            // --- Logistyka / Dostawy ---
            new Objection { ClientSays = "Nie dostarczacie w nasz rejon", Response = "Piórkowscy poszerzamy zasięg dostaw. W jakim rejonie jesteście? Przy stałym zamówieniu na pewno znajdziemy rozwiązanie logistyczne." },
            new Objection { ClientSays = "Potrzebujemy dostaw codziennie", Response = "Piórkowscy dostarczamy 6 dni w tygodniu - od poniedziałku do soboty. Codzienne dostawy to standard dla stałych klientów. Żaden problem!" },
            new Objection { ClientSays = "Minimalne zamówienie jest za duże", Response = "U Piórkowscy mamy elastyczne minimum dla stałych klientów. Ile kg tygodniowo Państwu odpowiada? Na pewno się dogadamy." },
            new Objection { ClientSays = "A co jeśli towar nie dotrze na czas?", Response = "Terminowość to fundament Piórkowscy. Własna flota chłodnicza, stałe trasy. Przy opóźnieniu powyżej godziny - rabat na kolejne zamówienie." },
            new Objection { ClientSays = "Za daleko od nas jesteście", Response = "Piórkowscy dostarczamy własnym transportem chłodniczym w szerokim promieniu. Podajcie adres - sprawdzę czy jesteśmy na trasie. Często dodajemy nowe punkty." },

            // --- Decyzja / Proces ---
            new Objection { ClientSays = "Muszę się zastanowić", Response = "Jasne. Co chciałby Pan/Pani przemyśleć? Mogę przesłać dodatkowe informacje o Ubojni Piórkowscy - cennik, certyfikaty, referencje." },
            new Objection { ClientSays = "Muszę porozmawiać z szefem/właścicielem", Response = "Oczywiście! Kiedy to omówicie? Oddzwonię po rozmowie. A może mógłbym porozmawiać z właścicielem bezpośrednio?" },
            new Objection { ClientSays = "Nie ja decyduję o zakupach", Response = "Rozumiem. Kto odpowiada za zamówienia mięsa? Podałby Pan/Pani numer lub nazwisko? Chcę przedstawić ofertę Piórkowscy właściwej osobie." },
            new Objection { ClientSays = "Odezwiemy się jak będziemy zainteresowani", Response = "Jasne! Ale oferta łatwo ginie w codziennej pracy. Mogę oddzwonić za tydzień krótko przypomnieć o Piórkowscy?" },
            new Objection { ClientSays = "Teraz nie jest dobry moment na zmiany", Response = "Rozumiem. Kiedy byłby lepszy? Mogę zadzwonić za miesiąc. W międzyczasie wyślę cennik Piórkowscy do porównania." },
            new Objection { ClientSays = "Muszę to skonsultować ze wspólnikiem", Response = "Oczywiście. Mogę wysłać ofertę Piórkowscy mailem, żebyście mogli razem przejrzeć? Na jaki adres? Kiedy oddzwonić?" },

            // --- Ilości / Specyficzne ---
            new Objection { ClientSays = "Potrzebujemy małe ilości", Response = "U Piórkowscy mamy klientów od 50kg tygodniowo. Przy mniejszych ilościach też dajemy uczciwe ceny producenckie. Ile potrzebujecie?" },
            new Objection { ClientSays = "Kupujemy tylko filet z piersi", Response = "Filet z piersi to bestseller Piórkowscy! Mamy go w cenie producenckiej. A filet z udka jest coraz popularniejszy i tańszy o 30-40% - też polecam." },
            new Objection { ClientSays = "Mamy własny ubój/hodowlę", Response = "Rozumiem. Ale czy zawsze pokrywacie zapotrzebowanie? Wielu producentów bierze od Piórkowscy elementy w sezonie. Mamy okazyjne ceny hurtowe." },
            new Objection { ClientSays = "Interesują nas tylko polskie kurczaki", Response = "Piórkowscy to 100% polska produkcja. Polskie fermy, ubój w naszym zakładzie, polscy pracownicy. Przesłać dokumentację pochodzenia?" },
            new Objection { ClientSays = "Nie sprzedajemy mięsa", Response = "Rozumiem. A w kuchni/gastronomii używacie drobiu? Piórkowscy dostarczamy też do restauracji, stołówek, cateringu. Ile zużywacie tygodniowo?" },
            new Objection { ClientSays = "Mięso drobiowe słabo nam się sprzedaje", Response = "U klientów Piórkowscy drób to #1 w sprzedaży mięsa. Może kwestia jakości? Nasz świeży drób 24h od uboju robi ogromną różnicę u klientów." },
            new Objection { ClientSays = "Potrzebujemy fakturę z odroczonym terminem", Response = "Piórkowscy oferujemy terminy płatności dla stałych klientów. Na początek proponuję przedpłatę za 1-2 dostawy, potem ustalamy termin 7-14 dni." },
            new Objection { ClientSays = "Bierzemy tylko z Makro/Selgros", Response = "W hurtowni płacicie marżę pośrednika. Piórkowscy jako ubojnia dajemy cenę producencką + dostawę pod drzwi. Porównajcie - różnica może być spora!" },

            // --- Email / Kontakt ---
            new Objection { ClientSays = "Nie dam emaila / nie chcę spamu", Response = "Rozumiem! To nie spam - wyślę jedną ofertę cenową Piórkowscy z konkretnym cennikiem. Jeśli nie zainteresuje - nie będziemy pisać więcej. Jaki adres?" },
            new Objection { ClientSays = "Nie mam emaila", Response = "Rozumiem. Czy jest ktoś w firmie kto ma? Mogę też wysłać ofertę na WhatsApp albo MMS. Jaki numer najlepszy do kontaktu?" },
            new Objection { ClientSays = "Dzwonią tu ciągle z ofertami", Response = "Rozumiem frustrację. Ale Piórkowscy to ubojnia - nie pośrednik. Jedno konkretne pytanie: ile płacicie za kg tuszki? Jeśli dam lepszą cenę - warto?" },
            new Objection { ClientSays = "Nie chcę podawać danych", Response = "Zupełnie rozumiem. Mogę wysłać ogólny cennik Piórkowscy bez zobowiązań. Na jaki adres? Albo mogę podać nasz email/stronę - sami napiszecie gdy będzie potrzeba." },

            // --- Małe sklepy → dostawca ---
            new Objection { ClientSays = "Zamawiamy za mało, nie opłaca się wam", Response = "U Piórkowscy nie ma za mało! A swoją drogą - kto Państwu dostarcza drób? Może znamy się z Waszym dostawcą. Jaka to firma?" },
            new Objection { ClientSays = "Bierzemy od hurtownika co przyjeżdża", Response = "Rozumiem. Jak się nazywa ta hurtownia? Pytam, bo może moglibyśmy im dostarczać drób od Piórkowscy, a Państwo zyskalibyście na cenie." },
            new Objection { ClientSays = "Przyjeżdża do nas pan z busem", Response = "Rozumiem - wygodne! Czy to stała firma? Jak się nazywa? Bo Piórkowscy też dostarczamy busem, a ceny mamy producenckie." },
        };

        private static readonly string[] Statuses = new[]
        {
            "Nowy", "W trakcie", "Gorący", "Oferta wysłana", "Negocjacje",
            "Zgoda na dalszy kontakt", "Nie zainteresowany", "Zamknięty"
        };

        private int _currentTipIndex = 0;
        private static readonly string[] ColdCallTips = new[]
        {
            // Otwarcie rozmowy
            "Uśmiechnij się przed podniesieniem słuchawki - rozmówca to usłyszy w Twoim głosie!",
            "Pierwsze 10 sekund decyduje o rozmowie. Mów z energią i pewnością siebie.",
            "\"Dzwonię, bo widzę że Państwo zajmujecie się...\" - pokaż, że odrobiliśmy lekcje.",
            "Zacznij od wartości: \"Pomagamy firmom takim jak Państwa zaoszczędzić...\"",
            "Zamiast \"Czy mogę zaproponować...\" powiedz \"Chciałbym podzielić się rozwiązaniem...\"",
            "Przedstaw się krótko i konkretnie - max 15 sekund na wstęp, potem pytanie.",
            "\"Dzień dobry, nie zajmę więcej niż 2 minuty\" - buduje szacunek do czasu klienta.",
            "Zacznij od pytania: \"Czy to dobry moment na krótką rozmowę?\" - daje klientowi kontrolę.",

            // Techniki sprzedaży
            "Nie sprzedawaj od razu - najpierw zapytaj, czym się firma zajmuje i co ich boli.",
            "Cel cold call to NIE sprzedaż, a umówienie spotkania lub wysłanie oferty.",
            "Słuchaj 70%, mów 30%. Im więcej klient mówi, tym bliżej jesteś zamknięcia.",
            "Notuj słowa kluczowe klienta i powtarzaj je - poczuje się wysłuchany.",
            "\"Inne firmy z Państwa branży zauważyły, że...\" - social proof działa najlepiej.",
            "Zadawaj pytania otwarte: \"Jak obecnie rozwiązujecie...?\" zamiast \"Czy potrzebujecie...?\"",
            "Stosuj metodę SPIN: Sytuacja, Problem, Implikacja, Naprowadzenie na rozwiązanie.",
            "Mów językiem korzyści, nie cech. Nie \"mamy system X\" ale \"dzięki temu zaoszczędzicie Y\".",
            "\"Co by się zmieniło, gdybyście mogli...?\" - pozwól klientowi sam zobaczyć wartość.",
            "Używaj konkretnych liczb: \"firmy oszczędzają średnio 30%\" brzmi lepiej niż \"dużo\".",

            // Radzenie sobie z odmową
            "Po usłyszeniu \"nie\" zapytaj: \"Rozumiem, a gdybyśmy mogli...?\" - otwierasz nową drogę.",
            "Po odmowie zawsze zakończ pozytywnie: \"Dziękuję za czas, życzę miłego dnia!\"",
            "\"Nie jestem zainteresowany\" często znaczy \"nie teraz\". Zapytaj o lepszy termin.",
            "Statystycznie potrzebujesz 5-8 prób kontaktu. Nie poddawaj się po pierwszej!",
            "Odmowa to nie porażka - to informacja. Zapisz powód i wróć z lepszym podejściem.",
            "\"Rozumiem, wiele osób na początku tak reagowało, a potem...\" - normalizuj obawy.",
            "Jeśli klient jest zajęty, zapytaj: \"Kiedy mogę zadzwonić w lepszym momencie?\"",
            "\"Nie\" na cold call to \"nie\" dla oferty, nie dla Ciebie osobiście. Nie bierz do siebie.",

            // Timing i organizacja
            "Dzwoń w najlepszych godzinach: 10:00-11:30 i 14:00-16:00. Unikaj poniedziałku rano.",
            "Rób przerwy co 45 minut - Twoja energia wpływa na jakość rozmów.",
            "Przygotuj 2-3 pytania otwarte zanim zadzwonisz. Bądź ciekawy, nie nachalny.",
            "Prowadź tracker wyników - zobaczenie postępów motywuje do dalszej pracy!",
            "Wtorek i środa to statystycznie najlepsze dni na cold calling.",
            "Blokuj czas na dzwonienie - np. 2h rano bez przerw. Rytm buduje pewność.",
            "Po każdych 10 telefonach zrób krótką analizę: co działało, co poprawić?",
            "Przygotuj skrypt, ale nie czytaj z kartki. Znaj kluczowe punkty na pamięć.",

            // Follow-up
            "Jeśli klient mówi \"wyślij maila\" - uzgodnij konkretny termin follow-up.",
            "Follow-up w ciągu 24h po rozmowie podwaja szanse na zamknięcie.",
            "W mailu po rozmowie odwołaj się do konkretnych słów klienta - pokaż, że słuchałeś.",
            "Ustaw przypomnienie o follow-up od razu po rozmowie - nie odkładaj na później.",
            "\"Jak rozmawialiśmy w ubiegłym tygodniu...\" - kontynuacja buduje relację.",

            // Głos i komunikacja
            "Mów powoli i wyraźnie. Szybka mowa = nerwowość = brak zaufania.",
            "Stój podczas rozmowy - Twój głos będzie bardziej energiczny i pewny.",
            "Moduluj głos - monotonny ton usypia. Podkreślaj kluczowe słowa intonacją.",
            "Rób pauzy po ważnych zdaniach - daj klientowi czas na przemyślenie.",
            "Używaj imienia klienta (ale nie za często) - personalizuje rozmowę.",

            // Obiekcje cenowe
            "Najlepsza odpowiedź na \"ile to kosztuje?\" to pytanie: \"Co jest dla Państwa najważniejsze?\"",
            "\"To za drogo\" - odpowiedz: \"W porównaniu do czego?\" Poznaj punkt odniesienia.",
            "Nie dawaj rabatu od razu. Najpierw pokaż wartość, potem rozmawiaj o cenie.",
            "\"Jaki budżet Państwo przewidujecie?\" - pozwól klientowi określić ramy.",

            // Motywacja
            "Każde \"nie\" przybliża Cię do \"tak\". Średnio 1 na 10 rozmów kończy się sukcesem.",
            "Wyobraź sobie sukces przed podniesieniem słuchawki - pozytywna wizualizacja działa.",
            "Porównuj się z sobą z zeszłego tygodnia, nie z innymi. Liczy się Twój progres.",
            "Świętuj małe sukcesy: dobra rozmowa, nowy kontakt, wysłana oferta - wszystko się liczy!",
            "Najlepsi handlowcy to nie ci, co się nie boją, ale ci co dzwonią mimo strachu."
        };

        public CallReminderWindow(string connectionString, string userID, CallReminderConfig config)
        {
            InitializeComponent();

            _connectionString = connectionString;
            _userID = userID;
            _config = config;

            _contacts = new ObservableCollection<ContactToCall>();

            // Enable window dragging
            this.MouseLeftButtonDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                    this.DragMove();
            };

            LoadContacts();
            InitializeStatusButtons();
            ShowRandomTip();
            UpdateFlowPanel();
        }

        private void LoadContacts()
        {
            var contacts = CallReminderService.Instance.GetRandomContacts(_config?.ContactsPerReminder ?? 5);

            if (contacts.Count == 0)
            {
                MessageBox.Show("Brak kontaktów do wyświetlenia.\nWszystkie kontakty zostały już dziś obsłużone lub nie ma kontaktów spełniających kryteria.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
                return;
            }

            _contacts.Clear();
            foreach (var contact in contacts)
            {
                _contacts.Add(contact);
            }

            contactsList.ItemsSource = _contacts;
            txtContactsCount.Text = $"Kontakty ({_contacts.Count})";
            UpdateProgress();

            // Create reminder log
            _reminderLogID = CallReminderService.Instance.CreateReminderLog(_contacts.Count);

            // Select first contact
            if (_contacts.Count > 0)
            {
                SelectContact(_contacts[0]);
            }

            // Add click handlers to contact items
            contactsList.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(ContactItem_Click), true);
        }

        private void ContactItem_Click(object sender, MouseButtonEventArgs e)
        {
            // Find the clicked contact
            var element = e.OriginalSource as FrameworkElement;
            while (element != null && !(element.DataContext is ContactToCall))
            {
                element = VisualTreeHelper.GetParent(element) as FrameworkElement;
            }

            if (element?.DataContext is ContactToCall contact)
            {
                SelectContact(contact);
            }
        }

        private void SelectContact(ContactToCall contact)
        {
            // Deselect previous
            if (_selectedContact != null)
            {
                _selectedContact.IsSelected = false;
            }

            _selectedContact = contact;
            contact.IsSelected = true;

            // Update details panel
            txtCompanyName.Text = contact.Nazwa ?? "Brak nazwy";
            txtAddress.Text = contact.FullAddress;
            txtNIP.Text = contact.HasNIP ? $"NIP: {contact.NIP}" : "";

            // PKD Section
            if (contact.HasPKD)
            {
                pkdSection.Visibility = Visibility.Visible;
                txtPKDCode.Text = contact.PKD;
                txtPKDName.Text = contact.PKDNazwa ?? contact.Branza ?? "";
            }
            else
            {
                pkdSection.Visibility = Visibility.Collapsed;
            }

            // Phone section
            txtMainPhone.Text = FormatPhoneNumber(contact.Telefon);
            txtPhone2.Text = contact.Telefon2 ?? "";
            txtEmail.Text = contact.Email ?? "-";
            emailStack.Visibility = contact.HasEmail ? Visibility.Visible : Visibility.Collapsed;
            btnCall.IsEnabled = !string.IsNullOrWhiteSpace(contact.Telefon);

            // Status badge
            UpdateStatusBadge(contact.Status);

            // Last note
            if (contact.HasLastNote)
            {
                lastNoteSection.Visibility = Visibility.Visible;
                txtLastNote.Text = contact.OstatniaNota;
                txtLastNoteAuthor.Text = $"{contact.OstatniaNotaAutor ?? ""} • {contact.LastNoteDate}";
            }
            else
            {
                lastNoteSection.Visibility = Visibility.Collapsed;
            }

            // Clear new note
            txtNewNote.Text = "";

            // Footer stats
            txtCallCount.Text = $"{contact.CallCount} połączeń";
            txtLastCall.Text = contact.LastCallFormatted;
            txtAssignedTo.Text = contact.AssignedTo ?? "-";

            // Update status buttons selection
            UpdateStatusButtonsSelection(contact.Status);

            // Refresh list to show selection
            contactsList.Items.Refresh();
        }

        private string FormatPhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return "-";
            var clean = phone.Replace(" ", "").Replace("-", "");
            if (clean.Length == 9)
            {
                return $"+48 {clean.Substring(0, 3)} {clean.Substring(3, 3)} {clean.Substring(6, 3)}";
            }
            return phone;
        }

        private void UpdateStatusBadge(string status)
        {
            var contact = new ContactToCall { Status = status };
            statusBadgeMain.Background = contact.StatusBackground;
            txtStatusMain.Text = status ?? "-";
            txtStatusMain.Foreground = contact.StatusColor;
        }

        private void InitializeStatusButtons()
        {
            statusButtons.Children.Clear();

            foreach (var status in Statuses)
            {
                var btn = new RadioButton
                {
                    Content = status,
                    Tag = status,
                    GroupName = "StatusGroup",
                    Margin = new Thickness(0, 0, 8, 8)
                };

                // Style the button
                var contact = new ContactToCall { Status = status };
                btn.Style = CreateStatusButtonStyle(contact.StatusColor, contact.StatusBackground);
                btn.Checked += StatusButton_Checked;

                statusButtons.Children.Add(btn);
            }
        }

        private Style CreateStatusButtonStyle(SolidColorBrush textColor, SolidColorBrush bgColor)
        {
            var style = new Style(typeof(RadioButton));

            style.Setters.Add(new Setter(RadioButton.BackgroundProperty, new SolidColorBrush(Color.FromArgb(8, 255, 255, 255))));
            style.Setters.Add(new Setter(RadioButton.ForegroundProperty, new SolidColorBrush(Color.FromArgb(102, 255, 255, 255))));
            style.Setters.Add(new Setter(RadioButton.PaddingProperty, new Thickness(16, 8, 16, 8)));
            style.Setters.Add(new Setter(RadioButton.FontSizeProperty, 12.0));
            style.Setters.Add(new Setter(RadioButton.CursorProperty, Cursors.Hand));

            var template = new ControlTemplate(typeof(RadioButton));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "bd";
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(RadioButton.BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)));
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            borderFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(RadioButton.PaddingProperty));

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentPresenter);

            template.VisualTree = borderFactory;

            // Triggers
            var checkedTrigger = new Trigger { Property = RadioButton.IsCheckedProperty, Value = true };
            checkedTrigger.Setters.Add(new Setter(RadioButton.BackgroundProperty, bgColor, "bd"));
            checkedTrigger.Setters.Add(new Setter(RadioButton.ForegroundProperty, textColor));
            checkedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, textColor, "bd"));
            template.Triggers.Add(checkedTrigger);

            var hoverTrigger = new Trigger { Property = RadioButton.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(RadioButton.BackgroundProperty, new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), "bd"));
            template.Triggers.Add(hoverTrigger);

            style.Setters.Add(new Setter(RadioButton.TemplateProperty, template));

            return style;
        }

        private void UpdateStatusButtonsSelection(string status)
        {
            foreach (RadioButton btn in statusButtons.Children)
            {
                btn.IsChecked = btn.Tag?.ToString() == status;
            }
        }

        private void StatusButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton btn && _selectedContact != null)
            {
                var newStatus = btn.Tag?.ToString();
                if (newStatus != _selectedContact.Status)
                {
                    ChangeContactStatus(_selectedContact, newStatus);
                }
            }
        }

        private void StatusBadge_Click(object sender, MouseButtonEventArgs e)
        {
            // Could show a dropdown here, for now just scroll to status section
        }

        private void UpdateProgress()
        {
            int completed = _contacts.Count(c => c.IsCompleted);
            int total = _contacts.Count;
            double percent = total > 0 ? (completed / (double)total) * 100 : 0;

            txtProgressCount.Text = $"{completed}/{total}";

            // Animate progress bar width
            var containerWidth = 348.0; // approximate width of container
            var targetWidth = (percent / 100) * containerWidth;
            progressFill.Width = Math.Max(0, targetWidth);
        }

        private void FilterTab_Checked(object sender, RoutedEventArgs e)
        {
            // Guard against firing during InitializeComponent
            if (tabAll == null || tabNew == null || tabHot == null || _contacts == null) return;

            if (sender is ToggleButton tb)
            {
                // Uncheck other tabs
                if (tb == tabAll) { tabNew.IsChecked = false; tabHot.IsChecked = false; }
                else if (tb == tabNew) { tabAll.IsChecked = false; tabHot.IsChecked = false; }
                else if (tb == tabHot) { tabAll.IsChecked = false; tabNew.IsChecked = false; }

                // Filter contacts
                FilterContacts();
            }
        }

        private void FilterContacts()
        {
            var allContacts = CallReminderService.Instance.GetRandomContacts(_config?.ContactsPerReminder ?? 10);
            IEnumerable<ContactToCall> filtered = allContacts;

            if (tabNew?.IsChecked == true)
            {
                filtered = allContacts.Where(c => c.Status == "Nowy" || c.Status == "Do zadzwonienia");
            }
            else if (tabHot?.IsChecked == true)
            {
                filtered = allContacts.Where(c => c.Status == "Gorący" || c.Priority == "urgent" || c.Priority == "high");
            }

            _contacts.Clear();
            foreach (var contact in filtered.Take(_config?.ContactsPerReminder ?? 5))
            {
                _contacts.Add(contact);
            }

            txtContactsCount.Text = $"Kontakty ({_contacts.Count})";
            UpdateProgress();

            if (_contacts.Count > 0)
            {
                SelectContact(_contacts[0]);
            }
        }

        private void BtnSwapContact_Click(object sender, RoutedEventArgs e)
        {
            // Find the contact card that was clicked
            var button = sender as Button;
            if (button == null) return;

            var element = button as FrameworkElement;
            while (element != null && !(element.DataContext is ContactToCall))
            {
                element = VisualTreeHelper.GetParent(element) as FrameworkElement;
            }

            if (element?.DataContext is ContactToCall contactToSwap)
            {
                int index = _contacts.IndexOf(contactToSwap);
                if (index < 0) return;

                // Find the visual Border for animation
                Border cardBorder = FindParentBorder(button);
                if (cardBorder != null)
                {
                    // Animate slide-out to the left + fade
                    var translateTransform = cardBorder.RenderTransform as TranslateTransform;
                    if (translateTransform == null)
                    {
                        translateTransform = new TranslateTransform(0, 0);
                        cardBorder.RenderTransform = translateTransform;
                    }

                    var slideOut = new DoubleAnimation(-300, TimeSpan.FromMilliseconds(250))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                    };
                    var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));

                    int capturedIndex = index;
                    slideOut.Completed += (s2, e2) =>
                    {
                        // Get one new random contact to replace
                        var newContacts = CallReminderService.Instance.GetRandomContacts(1);
                        if (newContacts.Count > 0)
                        {
                            _contacts[capturedIndex] = newContacts[0];

                            // If the swapped contact was selected, select the new one
                            if (_selectedContact == contactToSwap)
                            {
                                SelectContact(newContacts[0]);
                            }
                        }
                        else
                        {
                            // No more contacts available, just remove
                            _contacts.RemoveAt(capturedIndex);
                        }

                        txtContactsCount.Text = $"Kontakty ({_contacts.Count})";
                        UpdateProgress();

                        // Animate slide-in from right for the new card
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                var container = contactsList.ItemContainerGenerator.ContainerFromIndex(capturedIndex) as ContentPresenter;
                                if (container != null)
                                {
                                    var newBorder = FindChildBorder(container);
                                    if (newBorder != null)
                                    {
                                        var slideInTransform = new TranslateTransform(300, 0);
                                        newBorder.RenderTransform = slideInTransform;
                                        newBorder.Opacity = 0;

                                        var slideIn = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300))
                                        {
                                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                                        };
                                        var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(250));

                                        slideInTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);
                                        newBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                                    }
                                }
                            }
                            catch { /* animation is cosmetic, don't crash */ }
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    };

                    translateTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);
                    cardBorder.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                }
                else
                {
                    // Fallback: no animation, just swap
                    var newContacts = CallReminderService.Instance.GetRandomContacts(1);
                    if (newContacts.Count > 0)
                    {
                        _contacts[index] = newContacts[0];
                        if (_selectedContact == contactToSwap)
                            SelectContact(newContacts[0]);
                    }
                    else
                    {
                        _contacts.RemoveAt(index);
                    }
                    txtContactsCount.Text = $"Kontakty ({_contacts.Count})";
                    UpdateProgress();
                }
            }
        }

        private Border FindParentBorder(DependencyObject child)
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is Border border && border.Name == "contactBorder")
                    return border;
                if (parent is ContentPresenter)
                    break;
                parent = VisualTreeHelper.GetParent(parent);
            }
            // If named border not found, find first border with CornerRadius
            parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is Border b && b.CornerRadius.TopLeft > 10)
                    return b;
                if (parent is ContentPresenter)
                    break;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private Border FindChildBorder(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Border border && border.CornerRadius.TopLeft > 10)
                    return border;
                var result = FindChildBorder(child);
                if (result != null) return result;
            }
            return null;
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Czy chcesz pobrać nowe losowe kontakty?",
                "Nowe kontakty",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _callsCount = 0;
                _notesCount = 0;
                _statusChangesCount = 0;
                LoadContacts();
            }
        }

        private void BtnCall_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedContact == null) return;

            // Open phone dialer
            try
            {
                var phone = _selectedContact.Telefon?.Replace(" ", "").Replace("-", "");
                if (!string.IsNullOrEmpty(phone))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = $"tel:{phone}",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening phone: {ex.Message}");
            }

            // Show call result dialog
            var dialog = new CallResultDialog(_selectedContact.Nazwa);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                _selectedContact.WasCalled = true;
                _callsCount++;

                // Handle status change from dialog
                if (!string.IsNullOrEmpty(dialog.SelectedStatus) && dialog.SelectedStatus != _selectedContact.Status)
                {
                    ChangeContactStatus(_selectedContact, dialog.SelectedStatus);
                }

                // Handle note from dialog
                if (!string.IsNullOrWhiteSpace(dialog.Note))
                {
                    AddNoteToContact(_selectedContact, dialog.Note);
                }

                // Log action
                CallReminderService.Instance.LogContactAction(_reminderLogID, _selectedContact.ID,
                    true, !string.IsNullOrWhiteSpace(dialog.Note), _selectedContact.StatusChanged, _selectedContact.NewStatus);

                UpdateProgress();
                contactsList.Items.Refresh();
            }
        }

        private void BtnSaveNote_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedContact == null || string.IsNullOrWhiteSpace(txtNewNote.Text)) return;

            AddNoteToContact(_selectedContact, txtNewNote.Text);
            _selectedContact.NoteAdded = true;

            CallReminderService.Instance.LogContactAction(_reminderLogID, _selectedContact.ID,
                false, true, false, null);

            txtNewNote.Text = "";
            UpdateProgress();
            contactsList.Items.Refresh();

            // Show the new note in last note section
            _selectedContact.OstatniaNota = txtNewNote.Text;
            _selectedContact.OstatniaNotaAutor = _userID;
            _selectedContact.DataOstatniejNotatki = DateTime.Now;
            SelectContact(_selectedContact); // Refresh display
        }

        private void TxtNewNote_TextChanged(object sender, TextChangedEventArgs e)
        {
            var length = txtNewNote.Text?.Length ?? 0;
            txtNoteCharCount.Text = $"{length}/500 znaków";
            btnSaveNote.IsEnabled = length > 0 && _selectedContact != null;
        }

        private void BtnGoogle_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedContact == null) return;

            try
            {
                var query = System.Net.WebUtility.UrlEncode($"{_selectedContact.Nazwa} {_selectedContact.Miasto}");
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"https://www.google.com/search?q={query}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening Google: {ex.Message}");
            }
        }

        private void BtnMap_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedContact == null) return;

            try
            {
                var origin = "Koziołki 40, 95-061 Dmosin";
                var destination = "";

                // Build destination from contact address
                if (!string.IsNullOrWhiteSpace(_selectedContact.Adres))
                    destination = _selectedContact.Adres;
                if (!string.IsNullOrWhiteSpace(_selectedContact.KodPocztowy))
                    destination += (destination.Length > 0 ? ", " : "") + _selectedContact.KodPocztowy;
                if (!string.IsNullOrWhiteSpace(_selectedContact.Miasto))
                    destination += (destination.Length > 0 ? " " : "") + _selectedContact.Miasto;

                // Fallback to company name + city if no address
                if (string.IsNullOrWhiteSpace(destination))
                    destination = $"{_selectedContact.Nazwa}, {_selectedContact.Miasto}";

                var originEncoded = System.Net.WebUtility.UrlEncode(origin);
                var destEncoded = System.Net.WebUtility.UrlEncode(destination);

                Process.Start(new ProcessStartInfo
                {
                    FileName = $"https://www.google.com/maps/dir/{originEncoded}/{destEncoded}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening map: {ex.Message}");
            }
        }

        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedContact == null) return;

            MessageBox.Show($"Historia kontaktu: {_selectedContact.Nazwa}\n\nTa funkcja zostanie wkrótce dodana.",
                "Historia", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnEmail_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedContact == null || !_selectedContact.HasEmail) return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"mailto:{_selectedContact.Email}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening email: {ex.Message}");
            }
        }

        private void BtnSkip_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedContact == null) return;

            // Mark as completed (skipped)
            _selectedContact.StatusChanged = true;
            _selectedContact.NewStatus = "Pominięty";

            CallReminderService.Instance.LogContactAction(_reminderLogID, _selectedContact.ID,
                false, false, false, "Pominięty");

            UpdateProgress();
            contactsList.Items.Refresh();

            // Select next contact
            var currentIndex = _contacts.IndexOf(_selectedContact);
            if (currentIndex < _contacts.Count - 1)
            {
                SelectContact(_contacts[currentIndex + 1]);
            }
        }

        private void BtnCloseWindow_Click(object sender, RoutedEventArgs e)
        {
            int completed = _contacts.Count(c => c.IsCompleted);

            if (completed < (_contacts.Count / 2.0))
            {
                var result = MessageBox.Show(
                    "Nie obsłużyłeś jeszcze połowy kontaktów. Czy na pewno chcesz zamknąć okno?",
                    "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;
            }

            // Complete reminder log
            CallReminderService.Instance.CompleteReminder(
                _reminderLogID,
                _callsCount,
                _notesCount,
                _statusChangesCount,
                completed < (_contacts.Count / 2.0),
                null
            );

            Close();
        }

        private void ShowRandomTip()
        {
            _currentTipIndex = new Random().Next(ColdCallTips.Length);
        }

        private void BtnNextTip_Click(object sender, RoutedEventArgs e)
        {
            _currentTipIndex = (_currentTipIndex + 1) % ColdCallTips.Length;
        }

        private void UpdateFlowPanel()
        {
            var phase = _phases[_currentPhase];

            // Update phase icon (Path data)
            try { pathPhaseIcon.Data = Geometry.Parse(phase.IconPath); } catch { }

            txtPhaseName.Text = phase.Name;
            txtPhaseNumber.Text = $"Faza {_currentPhase + 1} z {_phases.Count}";

            // Pick random script for this phase
            _currentScriptIndex = _rng.Next(phase.Scripts.Length);
            txtScript.Text = phase.Scripts[_currentScriptIndex];

            // Pick random tip
            txtFlowTip.Text = phase.Tips[_rng.Next(phase.Tips.Length)];

            // Update tab backgrounds
            var activeBg = new SolidColorBrush(Color.FromRgb(30, 58, 95));
            var inactiveBg = new SolidColorBrush(Color.FromRgb(17, 17, 17));

            btnPhase0.Background = _currentPhase == 0 ? activeBg : inactiveBg;
            btnPhase1.Background = _currentPhase == 1 ? activeBg : inactiveBg;
            btnPhase2.Background = _currentPhase == 2 ? activeBg : inactiveBg;
            btnPhase3.Background = _currentPhase == 3 ? activeBg : inactiveBg;

            btnPrevPhase.IsEnabled = _currentPhase > 0;

            // Update objections
            PopulateObjections();

            // Update flow stats
            txtStatToday.Text = _callsCount.ToString();
            int completed = _contacts?.Count(c => c.IsCompleted) ?? 0;
            int total = _contacts?.Count ?? 0;
            txtStatRate.Text = total > 0 ? $"{(completed * 100 / total)}%" : "0%";
        }

        private void PopulateObjections()
        {
            objectionsList.Children.Clear();

            // Pick 4 random objections
            var shuffled = AllObjections.OrderBy(_ => _rng.Next()).Take(4).ToArray();

            foreach (var obj in shuffled)
            {
                var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

                var clientText = new TextBlock
                {
                    Text = $"\u00AB{obj.ClientSays}\u00BB",
                    Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                    FontSize = 12,
                    FontWeight = FontWeights.Medium
                };
                sp.Children.Add(clientText);

                var responseText = new TextBlock
                {
                    Text = $"\u2192 {obj.Response}",
                    Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10, 3, 0, 0),
                    LineHeight = 18
                };
                sp.Children.Add(responseText);

                objectionsList.Children.Add(sp);
            }
        }

        private void BtnShuffleScript_Click(object sender, RoutedEventArgs e)
        {
            var phase = _phases[_currentPhase];
            int newIndex;
            if (phase.Scripts.Length > 1)
            {
                do { newIndex = _rng.Next(phase.Scripts.Length); }
                while (newIndex == _currentScriptIndex);
                _currentScriptIndex = newIndex;
            }
            else
            {
                _currentScriptIndex = 0;
            }
            txtScript.Text = phase.Scripts[_currentScriptIndex];
            txtFlowTip.Text = phase.Tips[_rng.Next(phase.Tips.Length)];
        }

        private void BtnShuffleObjections_Click(object sender, RoutedEventArgs e)
        {
            PopulateObjections();
        }

        private void PhaseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int idx))
            {
                _currentPhase = idx;
                UpdateFlowPanel();
            }
        }

        private void PrevPhase_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPhase > 0)
            {
                _currentPhase--;
                UpdateFlowPanel();
            }
        }

        private void NextPhase_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPhase < _phases.Count - 1)
            {
                _currentPhase++;
                UpdateFlowPanel();
            }
        }

        private void AddNoteToContact(ContactToCall contact, string note)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                var cmd = new SqlCommand(
                    "INSERT INTO NotatkiCRM (IDOdbiorcy, Tresc, KtoDodal) VALUES (@id, @note, @user)", conn);
                cmd.Parameters.AddWithValue("@id", contact.ID);
                cmd.Parameters.AddWithValue("@note", note);
                cmd.Parameters.AddWithValue("@user", _userID);
                cmd.ExecuteNonQuery();

                _notesCount++;
                contact.NoteAdded = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd dodawania notatki: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChangeContactStatus(ContactToCall contact, string newStatus)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Update status
                var cmdUpdate = new SqlCommand(
                    "UPDATE OdbiorcyCRM SET Status = @status WHERE ID = @id", conn);
                cmdUpdate.Parameters.AddWithValue("@status", newStatus);
                cmdUpdate.Parameters.AddWithValue("@id", contact.ID);
                cmdUpdate.ExecuteNonQuery();

                // Log history
                var cmdLog = new SqlCommand(
                    "INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal, DataZmiany) " +
                    "VALUES (@id, 'Zmiana statusu', @val, @user, GETDATE())", conn);
                cmdLog.Parameters.AddWithValue("@id", contact.ID);
                cmdLog.Parameters.AddWithValue("@val", newStatus);
                cmdLog.Parameters.AddWithValue("@user", _userID);
                cmdLog.ExecuteNonQuery();

                _statusChangesCount++;
                contact.StatusChanged = true;
                contact.NewStatus = newStatus;
                contact.Status = newStatus;

                // Update UI
                UpdateStatusBadge(newStatus);
                UpdateProgress();
                contactsList.Items.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zmiany statusu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
