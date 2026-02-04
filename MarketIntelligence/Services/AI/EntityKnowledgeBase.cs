using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Kalendarz1.MarketIntelligence.Services.AI
{
    /// <summary>
    /// Baza wiedzy o podmiotach branży drobiarskiej
    ///
    /// Zawiera informacje o:
    /// - Konkurentach (Cedrob, SuperDrob, Drosed, Animex, Wipasz, itd.)
    /// - Inwestorach (ADQ, LDC Group, CPF, WH Group)
    /// - Organizacjach branżowych (KRD-IG, GLW)
    /// - Importerach (BRF, MHP, JBS)
    /// - Regulacjach (Mercosur, KSeF)
    /// - Osobach (Zbigniew Jagiełło, Piotr Hera)
    ///
    /// Używana do generowania sekcji "KIM JEST" w analizach artykułów
    /// </summary>
    public static class EntityKnowledgeBase
    {
        /// <summary>
        /// Główny słownik wszystkich podmiotów
        /// </summary>
        public static readonly Dictionary<string, EntityInfo> Entities = new(StringComparer.OrdinalIgnoreCase)
        {
            #region === KONKURENCI POLSCY ===

            ["Cedrob"] = new EntityInfo
            {
                Name = "Cedrob S.A.",
                Type = EntityType.Competitor,
                Category = "Producent drobiu",
                ShortDescription = "Największy producent drobiu w Polsce",
                FullDescription = @"Cedrob S.A. z siedzibą w Ujazdówku to NAJWIĘKSZY producent drobiu w Polsce, kontrolujący około 25-30% krajowego rynku.

Kluczowe fakty:
• Roczne przychody: ok. 6-7 mld PLN
• Zatrudnienie: ok. 8 000 pracowników
• Zdolności produkcyjne: ponad 500 000 szt. drobiu dziennie
• Właściciel: Piotr Hera (założyciel)

AKTUALNA SYTUACJA (2025-2026):
ADQ (państwowy fundusz z Abu Dhabi) negocjuje przejęcie Cedrob za szacowane 8 mld PLN. Jeśli dojdzie do transakcji, ADQ (który już kontroluje Drosed i Indykpol przez LDC Group) będzie miał około 40% polskiego rynku drobiarskiego.

DLA PIÓRKOWSCY: ZAGROŻENIE KRYTYCZNE
- Konsolidacja rynku może wykluczyć małych graczy
- Cedrob dyktuje ceny skupu w regionie
- Przewaga negocjacyjna wobec sieci handlowych",
                ThreatLevel = ThreatLevel.Critical,
                Aliases = new[] { "Cedrob SA", "Grupa Cedrob", "Cedrob Ujazdówek", "GK Cedrob" },
                RelatedEntities = new[] { "ADQ", "Piotr Hera", "Gobarto", "LDC Group" },
                Website = "cedrob.com.pl",
                Location = "Ujazdówek"
            },

            ["SuperDrob"] = new EntityInfo
            {
                Name = "SuperDrob / LipCo Foods",
                Type = EntityType.Competitor,
                Category = "Producent drobiu",
                ShortDescription = "Dynamiczny producent drobiu z azjatyckim wsparciem",
                FullDescription = @"SuperDrob (obecnie LipCo Foods Sp. z o.o.) z Karczewa to jeden z głównych producentów drobiu w Polsce.

Kluczowe fakty:
• Lokalizacja: Karczew (mazowieckie)
• Zdolności: ok. 150 000 szt. drobiu dziennie
• Inwestycje: planowane ~180 mln PLN w rozbudowę

STRUKTURA WŁAŚCICIELSKA:
• Partner strategiczny: CPF (Charoen Pokphand Foods) z Tajlandii - globalny gigant drobiarski
• W Radzie Nadzorczej: Zbigniew Jagiełło (były prezes PKO BP 2009-2021)
• Dostęp do azjatyckiego kapitału i know-how

DLA PIÓRKOWSCY: ZAGROŻENIE WYSOKIE
- Dynamiczna ekspansja wspierana przez globalnego gracza
- Nowoczesne technologie z Azji
- Agresywna polityka cenowa",
                ThreatLevel = ThreatLevel.High,
                Aliases = new[] { "Super Drob", "LipCo", "LipCo Foods", "SuperDrob Karczew" },
                RelatedEntities = new[] { "CPF", "Zbigniew Jagiełło", "CP Group" },
                Website = "lipcofoods.pl",
                Location = "Karczew"
            },

            ["Drosed"] = new EntityInfo
            {
                Name = "Drosed Sp. z o.o.",
                Type = EntityType.Competitor,
                Category = "Producent drobiu",
                ShortDescription = "Duży producent kontrolowany przez ADQ",
                FullDescription = @"Drosed Sp. z o.o. z Siedlec to jeden z wiodących producentów drobiu w Polsce, należący do LDC Group (Francja).

Kluczowe fakty:
• Lokalizacja: Siedlce (mazowieckie)
• Właściciel: LDC Group → kontrolowany przez ADQ (Abu Dhabi)
• Zdolności: ok. 250 000 szt. drobiu dziennie

STRUKTURA WŁAŚCICIELSKA:
LDC Group (Francja) jest kontrolowane przez ADQ - ten sam fundusz, który negocjuje przejęcie Cedrob.

DLA PIÓRKOWSCY: ZAGROŻENIE KRYTYCZNE
- Część potencjalnego monopolu ADQ
- Drosed + Cedrob = ~40% rynku polskiego
- Wspólna polityka cenowa może zdominować rynek",
                ThreatLevel = ThreatLevel.Critical,
                Aliases = new[] { "Drosed Siedlce", "LDC Drosed" },
                RelatedEntities = new[] { "LDC Group", "ADQ", "Cedrob", "Indykpol" },
                Website = "drosed.pl",
                Location = "Siedlce"
            },

            ["Animex"] = new EntityInfo
            {
                Name = "Animex Foods Sp. z o.o.",
                Type = EntityType.Competitor,
                Category = "Producent mięsa",
                ShortDescription = "Producent mięsa z chińskim kapitałem",
                FullDescription = @"Animex Foods to polski producent mięsa (wieprzowina, drób) należący do WH Group z Chin.

Kluczowe fakty:
• Właściciel: WH Group (Chiny) - największy producent wieprzowiny na świecie
• WH Group jest też właścicielem Smithfield Foods (USA)
• Stabilna pozycja na rynku, bez agresywnej ekspansji w drobiu
• Główny fokus: wieprzowina

DLA PIÓRKOWSCY: ZAGROŻENIE UMIARKOWANE
- Stabilny konkurent, nie agresywny w drobiu
- Może zmienić strategię w dowolnym momencie
- Dostęp do chińskiego kapitału",
                ThreatLevel = ThreatLevel.Medium,
                Aliases = new[] { "Animex", "WH Group Polska" },
                RelatedEntities = new[] { "WH Group", "Smithfield" },
                Website = "animex.pl",
                Location = "Różne lokalizacje"
            },

            ["Wipasz"] = new EntityInfo
            {
                Name = "Wipasz S.A.",
                Type = EntityType.Competitor,
                Category = "Producent drobiu",
                ShortDescription = "Zintegrowany pionowo producent z polskim kapitałem",
                FullDescription = @"Wipasz S.A. z Olsztyna to polski producent drobiu zintegrowany pionowo.

Kluczowe fakty:
• Lokalizacja: Olsztyn (warmińsko-mazurskie)
• Model biznesowy: integracja pionowa (pasza → hodowla → ubój → sprzedaż)
• Własne wytwórnie pasz = przewaga kosztowa
• Polski kapitał, bez zagranicznego właściciela

MODEL INTEGRACJI PIONOWEJ:
Wipasz kontroluje cały łańcuch wartości, co daje niższe koszty i większą kontrolę jakości.

DLA PIÓRKOWSCY: ZAGROŻENIE UMIARKOWANE
- Model biznesowy wart rozważenia (integracja pionowa)
- Przewaga kosztowa dzięki własnej paszy
- Potencjalny partner lub wzór do naśladowania",
                ThreatLevel = ThreatLevel.Medium,
                Aliases = new[] { "Wipasz Olsztyn" },
                RelatedEntities = new string[] { },
                Website = "wipasz.pl",
                Location = "Olsztyn"
            },

            ["Drobimex"] = new EntityInfo
            {
                Name = "Drobimex - PHW Group",
                Type = EntityType.Competitor,
                Category = "Producent drobiu",
                ShortDescription = "Szczeciński producent z niemieckim właścicielem",
                FullDescription = @"Drobimex ze Szczecina należy do niemieckiej Grupy PHW (właściciel marki Wiesenhof).

Kluczowe fakty:
• Lokalizacja: Szczecin
• Właściciel: PHW Group (Niemcy) - marka Wiesenhof
• Fokus na eksport do Niemiec i UE
• Silne zaplecze kapitałowe i technologiczne z Niemiec

DLA PIÓRKOWSCY: ZAGROŻENIE UMIARKOWANE
- Konkurencja głównie na rynku eksportowym
- Standardy jakości UE
- Mniejsza konkurencja na rynku krajowym",
                ThreatLevel = ThreatLevel.Medium,
                Aliases = new[] { "PHW Drobimex", "Wiesenhof Polska", "PHW Poland" },
                RelatedEntities = new[] { "PHW Group", "Wiesenhof" },
                Website = "drobimex.pl",
                Location = "Szczecin"
            },

            ["Indykpol"] = new EntityInfo
            {
                Name = "Indykpol S.A.",
                Type = EntityType.Competitor,
                Category = "Producent indyków",
                ShortDescription = "Lider rynku indyków w Polsce (LDC/ADQ)",
                FullDescription = @"Indykpol S.A. z Olsztyna to największy producent indyków w Polsce.

Kluczowe fakty:
• Lokalizacja: Olsztyn
• Specjalizacja: indyki (nie kurczaki - inna kategoria)
• Właściciel: LDC Group → ADQ
• Lider rynku indyków w Polsce

DLA PIÓRKOWSCY: ZAGROŻENIE NISKIE (bezpośrednie)
- Inna kategoria produktowa (indyki vs kurczaki)
- Ale część imperium ADQ
- Może wejść na rynek kurczaków",
                ThreatLevel = ThreatLevel.Low,
                Aliases = new[] { "Indykpol Olsztyn" },
                RelatedEntities = new[] { "LDC Group", "ADQ", "Drosed" },
                Website = "indykpol.pl",
                Location = "Olsztyn"
            },

            ["Plukon"] = new EntityInfo
            {
                Name = "Plukon Food Group",
                Type = EntityType.Competitor,
                Category = "Producent drobiu",
                ShortDescription = "Holenderski gigant drobiarski aktywny w Polsce",
                FullDescription = @"Plukon Food Group to holenderski producent drobiu, jeden z największych w Europie.

Kluczowe fakty:
• Siedziba: Holandia
• Obecność w Polsce przez przejęcia
• Strategia: konsolidacja europejskiego rynku drobiarskiego
• Aktywne przejęcia mniejszych producentów

DLA PIÓRKOWSCY: ZAGROŻENIE DO OBSERWACJI
- Może przejmować polskie ubojnie
- Potencjalny nabywca lub konkurent
- Europejska skala działania",
                ThreatLevel = ThreatLevel.Medium,
                Aliases = new[] { "Plukon", "Plukon Poland" },
                RelatedEntities = new string[] { },
                Website = "plukonfoodgroup.com",
                Location = "Holandia, Polska"
            },

            #endregion

            #region === INWESTORZY / WŁAŚCICIELE ===

            ["ADQ"] = new EntityInfo
            {
                Name = "ADQ (Abu Dhabi Developmental Holding Company)",
                Type = EntityType.Investor,
                Category = "Fundusz inwestycyjny",
                ShortDescription = "Fundusz z Abu Dhabi przejmujący polskie drobiarstwo",
                FullDescription = @"ADQ to państwowy fundusz inwestycyjny ze Zjednoczonych Emiratów Arabskich (Abu Dhabi).

OBECNOŚĆ W POLSCE:
• Kontroluje LDC Group (Francja) → właściciel Drosed i Indykpol
• Negocjuje przejęcie Cedrob za ~8 mld PLN
• Jeśli kupi Cedrob → ~40% polskiego rynku drobiu

STRATEGIA:
ADQ buduje globalny portfel w sektorze żywnościowym. Polska jest kluczowym rynkiem w UE.

DLA PIÓRKOWSCY: NAJWAŻNIEJSZE ZAGROŻENIE STRATEGICZNE
- Monopolizacja rynku przez zagraniczny kapitał państwowy
- Wspólna polityka cenowa Drosed + Cedrob
- Mali producenci mogą zostać wypchnięci z rynku
- Brak możliwości konkurowania z funduszem wartym setki miliardów dolarów",
                ThreatLevel = ThreatLevel.Critical,
                Aliases = new[] { "Abu Dhabi", "fundusz z Abu Dhabi", "ADQ Abu Dhabi", "Abu Dhabi Developmental Holding" },
                RelatedEntities = new[] { "LDC Group", "Cedrob", "Drosed", "Indykpol" },
                Website = "adq.ae",
                Location = "Abu Dhabi, ZEA"
            },

            ["LDC Group"] = new EntityInfo
            {
                Name = "LDC Group (Francja)",
                Type = EntityType.Investor,
                Category = "Grupa kapitałowa",
                ShortDescription = "Francuski gigant drobiarski kontrolowany przez ADQ",
                FullDescription = @"LDC Group to jeden z największych producentów drobiu w Europie, z siedzibą we Francji.

FAKTY:
• Przychody: ponad 5 mld EUR rocznie
• Obecność w wielu krajach UE
• Kontrolowany przez ADQ z Abu Dhabi

W POLSCE:
• Właściciel Drosed (Siedlce)
• Właściciel Indykpol (Olsztyn)

DLA PIÓRKOWSCY: ZAGROŻENIE WYSOKIE
- Pośrednik w ekspansji ADQ na polski rynek
- Doświadczenie w konsolidacji rynków europejskich",
                ThreatLevel = ThreatLevel.High,
                Aliases = new[] { "LDC", "LDC Groupe", "Groupe LDC" },
                RelatedEntities = new[] { "ADQ", "Drosed", "Indykpol" },
                Website = "ldc.fr",
                Location = "Francja"
            },

            ["CPF"] = new EntityInfo
            {
                Name = "Charoen Pokphand Foods (CPF)",
                Type = EntityType.Investor,
                Category = "Globalny koncern spożywczy",
                ShortDescription = "Tajlandzki gigant drobiarski, partner SuperDrob",
                FullDescription = @"Charoen Pokphand Foods (CPF) to jeden z największych producentów drobiu na świecie, część tajlandzkiego konglomeratu CP Group.

FAKTY:
• Siedziba: Bangkok, Tajlandia
• Przychody: ponad 20 mld USD rocznie
• Obecność w 17 krajach
• Lider eksportu drobiu z Azji do UE

W POLSCE:
• Partner strategiczny SuperDrob/LipCo Foods
• Transfer technologii i know-how
• Potencjalne inwestycje kapitałowe

DLA PIÓRKOWSCY: ZAGROŻENIE WYSOKIE
- Wejście globalnego gracza na polski rynek
- Nowoczesne azjatyckie technologie produkcji
- SuperDrob zyskuje potężnego partnera",
                ThreatLevel = ThreatLevel.High,
                Aliases = new[] { "CP Foods", "Charoen Pokphand", "CP Group" },
                RelatedEntities = new[] { "SuperDrob", "LipCo Foods" },
                Website = "cpfworldwide.com",
                Location = "Tajlandia"
            },

            ["WH Group"] = new EntityInfo
            {
                Name = "WH Group (Chiny)",
                Type = EntityType.Investor,
                Category = "Globalny koncern mięsny",
                ShortDescription = "Największy producent wieprzowiny na świecie",
                FullDescription = @"WH Group to chiński koncern mięsny, największy producent wieprzowiny na świecie.

FAKTY:
• Siedziba: Hongkong/Chiny
• Właściciel Smithfield Foods (USA)
• Właściciel Animex (Polska)

DLA PIÓRKOWSCY: ZAGROŻENIE UMIARKOWANE
- Główny fokus na wieprzowinę
- Może zdecydować o ekspansji w drób w dowolnym momencie",
                ThreatLevel = ThreatLevel.Medium,
                Aliases = new[] { "Shuanghui", "WH Group Limited" },
                RelatedEntities = new[] { "Animex", "Smithfield" },
                Website = "wh-group.com",
                Location = "Chiny/Hongkong"
            },

            #endregion

            #region === IMPORTERZY / KONKURENCJA ZAGRANICZNA ===

            ["BRF"] = new EntityInfo
            {
                Name = "BRF S.A. (Brazylia)",
                Type = EntityType.Importer,
                Category = "Globalny eksporter drobiu",
                ShortDescription = "Brazylijski gigant - główne zagrożenie cenowe",
                FullDescription = @"BRF S.A. to jeden z największych producentów żywności na świecie, z siedzibą w Brazylii.

FAKTY:
• Właściciel marek: Sadia, Perdigão
• Jeden z głównych eksporterów mrożonego drobiu do UE
• Koszty produkcji znacznie niższe niż w UE

CENY (orientacyjne):
• Filet z piersi brazylijski: ~13 zł/kg
• Filet z piersi polski: 15-17 zł/kg
• Różnica: 15-25% na niekorzyść polskich producentów

DLA PIÓRKOWSCY: ZAGROŻENIE KRYTYCZNE (CENOWE)
- Import z Brazylii obniża ceny na rynku
- Niemożliwa konkurencja kosztowa
- Szczególnie mrożonki do przetwórstwa",
                ThreatLevel = ThreatLevel.Critical,
                Aliases = new[] { "BRF Brasil", "Sadia", "Perdigão", "BRF Foods" },
                RelatedEntities = new[] { "JBS", "Marfrig" },
                Website = "bfrlobal.com",
                Location = "Brazylia"
            },

            ["MHP"] = new EntityInfo
            {
                Name = "MHP (Myronivsky Hliboproduct)",
                Type = EntityType.Importer,
                Category = "Ukraiński eksporter drobiu",
                ShortDescription = "Największy producent drobiu na Ukrainie",
                FullDescription = @"MHP to największy producent drobiu na Ukrainie, aktywnie eksportujący do UE.

FAKTY:
• Siedziba: Kijów, Ukraina
• Zdolności: ponad 700 000 ton drobiu rocznie
• Eksport do UE: bezcłowy (umowa stowarzyszeniowa)
• Znaczący wzrost eksportu po 2022 roku

DLA PIÓRKOWSCY: ZAGROŻENIE WYSOKIE
- Bezcłowy eksport do UE
- Niższe koszty produkcji niż w Polsce
- Ukraiński drób bezpośrednio konkuruje na polskim rynku",
                ThreatLevel = ThreatLevel.High,
                Aliases = new[] { "Myronivsky", "MHP Ukraina", "Nasha Ryaba" },
                RelatedEntities = new string[] { },
                Website = "mhp.com.ua",
                Location = "Ukraina"
            },

            ["JBS"] = new EntityInfo
            {
                Name = "JBS S.A. (Brazylia)",
                Type = EntityType.Importer,
                Category = "Największy producent mięsa na świecie",
                ShortDescription = "Globalny gigant mięsny z Brazylii",
                FullDescription = @"JBS S.A. to największy producent mięsa na świecie.

FAKTY:
• Siedziba: Brazylia
• Właściciel Pilgrim's Pride (USA) - #2 w produkcji drobiu USA
• Właściciel Moy Park (Europa) - duży producent drobiu w UK/Irlandii
• Przychody: ponad 70 mld USD rocznie

DLA PIÓRKOWSCY: ZAGROŻENIE WYSOKIE
- Globalny gracz z niskimi kosztami
- Może wejść na polski rynek przez przejęcie",
                ThreatLevel = ThreatLevel.High,
                Aliases = new[] { "JBS Brasil", "Pilgrim's Pride", "Moy Park" },
                RelatedEntities = new[] { "BRF", "Marfrig" },
                Website = "jbs.com.br",
                Location = "Brazylia"
            },

            #endregion

            #region === ORGANIZACJE BRANŻOWE / URZĘDY ===

            ["KRD-IG"] = new EntityInfo
            {
                Name = "Krajowa Rada Drobiarstwa - Izba Gospodarcza",
                Type = EntityType.Organization,
                Category = "Organizacja branżowa",
                ShortDescription = "Główna izba gospodarcza polskiego drobiarstwa",
                FullDescription = @"KRD-IG to główna organizacja branżowa polskiego sektora drobiarskiego.

FUNKCJE:
• Reprezentacja producentów wobec rządu i UE
• Publikowanie raportów o cenach i sytuacji rynkowej
• Lobbing na rzecz branży
• Wsparcie w sprawach regulacyjnych

DLA PIÓRKOWSCY: ŹRÓDŁO INFORMACJI I WSPARCIA
- Warto być członkiem
- Dostęp do raportów branżowych
- Możliwość lobbingu",
                ThreatLevel = ThreatLevel.Info,
                Aliases = new[] { "KRD", "Izba Drobiarska", "KRDG" },
                RelatedEntities = new string[] { },
                Website = "krd-ig.com.pl",
                Location = "Warszawa"
            },

            ["GLW"] = new EntityInfo
            {
                Name = "Główny Lekarz Weterynarii",
                Type = EntityType.Government,
                Category = "Urząd państwowy",
                ShortDescription = "Centralny organ nadzoru weterynaryjnego",
                FullDescription = @"Główny Lekarz Weterynarii to centralny organ administracji rządowej odpowiedzialny za nadzór weterynaryjny w Polsce.

KOMPETENCJE:
• Zwalczanie chorób zwierząt (HPAI!)
• Nadzór nad ubojniami
• Wyznaczanie stref ochronnych przy ogniskach chorób
• Kontrole sanitarne

DLA PIÓRKOWSCY: KLUCZOWY URZĄD
- Decyzje GLW wpływają bezpośrednio na działalność ubojni
- Strefy HPAI mogą zablokować skup
- Kontrole weterynaryjne",
                ThreatLevel = ThreatLevel.Info,
                Aliases = new[] { "Główny Inspektorat Weterynarii", "wetgiw", "PIW", "Inspekcja Weterynaryjna" },
                RelatedEntities = new string[] { },
                Website = "wetgiw.gov.pl",
                Location = "Warszawa"
            },

            ["EFSA"] = new EntityInfo
            {
                Name = "Europejski Urząd ds. Bezpieczeństwa Żywności",
                Type = EntityType.Government,
                Category = "Agencja UE",
                ShortDescription = "Europejska agencja ds. bezpieczeństwa żywności",
                FullDescription = @"EFSA (European Food Safety Authority) to agencja UE odpowiedzialna za ocenę ryzyka w łańcuchu żywnościowym.

FUNKCJE:
• Publikowanie raportów o HPAI w Europie
• Ocena ryzyka chorób zwierząt
• Rekomendacje dla Komisji Europejskiej

DLA PIÓRKOWSCY: ŹRÓDŁO INFORMACJI
- Raporty EFSA o HPAI są kluczowe
- Wczesne ostrzeżenia o zagrożeniach",
                ThreatLevel = ThreatLevel.Info,
                Aliases = new[] { "EFSA", "European Food Safety Authority" },
                RelatedEntities = new[] { "GLW" },
                Website = "efsa.europa.eu",
                Location = "Parma, Włochy"
            },

            #endregion

            #region === REGULACJE / UMOWY ===

            ["Mercosur"] = new EntityInfo
            {
                Name = "Umowa UE-Mercosur",
                Type = EntityType.Regulation,
                Category = "Umowa handlowa",
                ShortDescription = "Umowa handlowa zagrażająca europejskiemu drobiarstwu",
                FullDescription = @"Umowa UE-Mercosur to negocjowana umowa handlowa między Unią Europejską a krajami Ameryki Południowej (Brazylia, Argentyna, Paragwaj, Urugwaj).

KLUCZOWE ZAPISY DLA DROBIARSTWA:
• 180 000 ton bezcłowego importu drobiu do UE rocznie
• Brazylijski drób znacznie tańszy od europejskiego
• Asymetria standardów (niższe wymagania w Brazylii)

STATUS (2025-2026):
• Umowa jest w fazie zatwierdzania
• Silny opór sektora rolniczego UE
• Niepewność co do ostatecznego kształtu

DLA PIÓRKOWSCY: KRYTYCZNE ZAGROŻENIE DŁUGOTERMINOWE
- Masowy import taniego drobiu z Ameryki Południowej
- Konkurencja cenowa niemożliwa do wygrania
- Presja na ceny na polskim rynku",
                ThreatLevel = ThreatLevel.Critical,
                Aliases = new[] { "Mercosur", "umowa Mercosur", "UE-Mercosur", "EU-Mercosur" },
                RelatedEntities = new[] { "BRF", "JBS" },
                Website = null,
                Location = "UE / Ameryka Południowa"
            },

            ["KSeF"] = new EntityInfo
            {
                Name = "Krajowy System e-Faktur",
                Type = EntityType.Regulation,
                Category = "Regulacja podatkowa",
                ShortDescription = "Obowiązkowy system e-faktur od 2026",
                FullDescription = @"KSeF (Krajowy System e-Faktur) to system elektronicznego fakturowania wdrażany przez Ministerstwo Finansów.

TERMINY:
• 01.04.2026 - planowany termin obowiązku dla wszystkich firm
• Wymaga integracji z systemami ERP (Sage, Symfonia, itd.)

DLA PIÓRKOWSCY: PILNE DO WDROŻENIA
- Wymaga integracji z Sage Symfonia
- Koszty wdrożenia
- Szkolenie pracowników",
                ThreatLevel = ThreatLevel.Info,
                Aliases = new[] { "KSeF", "e-faktura", "Krajowy System eFaktur" },
                RelatedEntities = new string[] { },
                Website = "ksef.mf.gov.pl",
                Location = "Polska"
            },

            #endregion

            #region === OSOBY ===

            ["Zbigniew Jagiełło"] = new EntityInfo
            {
                Name = "Zbigniew Jagiełło",
                Type = EntityType.Person,
                Category = "Manager/Inwestor",
                ShortDescription = "Były prezes PKO BP, w Radzie Nadzorczej SuperDrob",
                FullDescription = @"Zbigniew Jagiełło to polski manager i bankier.

KARIERA:
• Prezes PKO BP: 2009-2021 (12 lat, najdłużej w historii banku)
• Pod jego kierownictwem PKO BP stał się największym bankiem w Europie Środkowo-Wschodniej
• Obecnie w Radzie Nadzorczej LipCo Foods (SuperDrob)

DLA PIÓRKOWSCY: SYGNAŁ OSTRZEGAWCZY
- Jego obecność w SuperDrob oznacza poważne ambicje właścicieli
- Doświadczenie w finansowaniu dużych inwestycji
- SuperDrob ma 'poważne pieniądze' za sobą",
                ThreatLevel = ThreatLevel.Info,
                Aliases = new[] { "Jagiełło", "prezes Jagiełło" },
                RelatedEntities = new[] { "SuperDrob", "LipCo Foods", "PKO BP" },
                Website = null,
                Location = "Polska"
            },

            ["Piotr Hera"] = new EntityInfo
            {
                Name = "Piotr Hera",
                Type = EntityType.Person,
                Category = "Właściciel/Założyciel",
                ShortDescription = "Założyciel i główny właściciel Grupy Cedrob",
                FullDescription = @"Piotr Hera to założyciel i główny właściciel Grupy Cedrob - największego producenta drobiu w Polsce.

OSIĄGNIĘCIA:
• Zbudował Cedrob od podstaw
• Stworzył największą firmę drobiarską w Polsce
• Majątek szacowany na miliardy PLN

AKTUALNA SYTUACJA:
• Negocjuje sprzedaż Cedrob do ADQ za ok. 8 mld PLN
• Jego decyzja wpłynie na cały polski rynek drobiarski

DLA PIÓRKOWSCY: KLUCZOWA POSTAĆ
- Jego decyzje kształtują rynek
- Sprzedaż do ADQ = konsolidacja rynku",
                ThreatLevel = ThreatLevel.Info,
                Aliases = new[] { "Hera", "właściciel Cedrob" },
                RelatedEntities = new[] { "Cedrob", "ADQ" },
                Website = null,
                Location = "Polska"
            },

            #endregion

            #region === SIECI HANDLOWE (kluczowi klienci) ===

            ["Dino"] = new EntityInfo
            {
                Name = "Dino Polska S.A.",
                Type = EntityType.Customer,
                Category = "Sieć handlowa",
                ShortDescription = "Najszybciej rosnąca sieć w Polsce - potencjał sprzedażowy",
                FullDescription = @"Dino Polska to jedna z najszybciej rosnących sieci sklepów spożywczych w Polsce.

FAKTY:
• Ponad 2 400 sklepów (2025)
• Plan: 300+ nowych sklepów rocznie
• Fokus na mniejsze miejscowości
• Preferuje lokalnych dostawców mięsa

DLA PIÓRKOWSCY: SZANSA SPRZEDAŻOWA
- Dynamiczny rozwój = rosnące zapotrzebowanie
- Preferuje lokalnych dostawców
- Potencjalny nowy klient",
                ThreatLevel = ThreatLevel.Opportunity,
                Aliases = new[] { "Dino", "Dino Polska" },
                RelatedEntities = new string[] { },
                Website = "dino.pl",
                Location = "Krotoszyn (centrala)"
            },

            ["Biedronka"] = new EntityInfo
            {
                Name = "Biedronka (Jeronimo Martins)",
                Type = EntityType.Customer,
                Category = "Sieć handlowa",
                ShortDescription = "Największa sieć sklepów w Polsce",
                FullDescription = @"Biedronka to największa sieć sklepów spożywczych w Polsce, należąca do portugalskiej grupy Jeronimo Martins.

FAKTY:
• Ponad 3 500 sklepów
• Lider rynku FMCG w Polsce
• Agresywna polityka cenowa

DLA PIÓRKOWSCY: DUŻY POTENCJALNY KLIENT
- Ogromny wolumen zakupów
- Ale twarde negocjacje cenowe
- Wymaga skali i konkurencyjnych cen",
                ThreatLevel = ThreatLevel.Opportunity,
                Aliases = new[] { "Biedronka", "Jeronimo Martins Polska" },
                RelatedEntities = new[] { "Jeronimo Martins" },
                Website = "biedronka.pl",
                Location = "Kostrzyn (centrala)"
            },

            #endregion
        };

        #region === METODY WYSZUKIWANIA ===

        /// <summary>
        /// Znajdź wszystkie podmioty wymienione w tekście
        /// </summary>
        /// <param name="text">Tekst do przeszukania (tytuł + treść artykułu)</param>
        /// <returns>Lista znalezionych podmiotów</returns>
        public static List<EntityInfo> FindEntitiesInText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<EntityInfo>();

            var found = new List<EntityInfo>();

            foreach (var entity in Entities.Values)
            {
                // Sprawdź główną nazwę
                if (ContainsWord(text, entity.Name))
                {
                    if (!found.Contains(entity))
                        found.Add(entity);
                    continue;
                }

                // Sprawdź aliasy
                if (entity.Aliases != null)
                {
                    foreach (var alias in entity.Aliases)
                    {
                        if (ContainsWord(text, alias))
                        {
                            if (!found.Contains(entity))
                                found.Add(entity);
                            break;
                        }
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// Wygeneruj sekcję "KIM JEST" dla znalezionych podmiotów
        /// Format przygotowany do wstawienia do promptu Claude
        /// </summary>
        public static string GenerateWhoIsSection(List<EntityInfo> entities)
        {
            if (entities == null || !entities.Any())
                return "Brak rozpoznanych podmiotów wymagających wyjaśnienia w tym artykule.";

            var sb = new StringBuilder();

            // Sortuj: najpierw Critical, potem High, potem reszta
            var sorted = entities
                .OrderByDescending(e => e.ThreatLevel == ThreatLevel.Critical)
                .ThenByDescending(e => e.ThreatLevel == ThreatLevel.High)
                .ThenByDescending(e => e.ThreatLevel == ThreatLevel.Medium)
                .ToList();

            foreach (var entity in sorted)
            {
                sb.AppendLine($"### {entity.Name}");
                sb.AppendLine($"**Typ:** {entity.Category}");

                if (entity.ThreatLevel != ThreatLevel.Info && entity.ThreatLevel != ThreatLevel.Opportunity)
                {
                    sb.AppendLine($"**Poziom zagrożenia:** {GetThreatLevelLabel(entity.ThreatLevel)}");
                }
                else if (entity.ThreatLevel == ThreatLevel.Opportunity)
                {
                    sb.AppendLine($"**Poziom:** SZANSA BIZNESOWA");
                }

                sb.AppendLine();
                sb.AppendLine(entity.FullDescription);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Wygeneruj krótkie podsumowanie podmiotów (do display w UI)
        /// </summary>
        public static string GenerateShortSummary(List<EntityInfo> entities)
        {
            if (entities == null || !entities.Any())
                return "";

            return string.Join("\n\n", entities
                .Take(5) // Max 5 podmiotów
                .Select(e => $"**{e.Name}** - {e.ShortDescription}"));
        }

        /// <summary>
        /// Pobierz informacje o podmiocie po nazwie
        /// </summary>
        public static EntityInfo GetEntity(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            // Szukaj bezpośrednio
            if (Entities.TryGetValue(name, out var entity))
                return entity;

            // Szukaj w aliasach
            foreach (var e in Entities.Values)
            {
                if (e.Aliases != null && e.Aliases.Any(a =>
                    string.Equals(a, name, StringComparison.OrdinalIgnoreCase)))
                {
                    return e;
                }
            }

            return null;
        }

        /// <summary>
        /// Pobierz wszystkie podmioty danego typu
        /// </summary>
        public static List<EntityInfo> GetEntitiesByType(EntityType type)
        {
            return Entities.Values
                .Where(e => e.Type == type)
                .OrderBy(e => e.Name)
                .ToList();
        }

        /// <summary>
        /// Pobierz podmioty o wysokim zagrożeniu
        /// </summary>
        public static List<EntityInfo> GetHighThreatEntities()
        {
            return Entities.Values
                .Where(e => e.ThreatLevel == ThreatLevel.Critical || e.ThreatLevel == ThreatLevel.High)
                .OrderByDescending(e => e.ThreatLevel == ThreatLevel.Critical)
                .ToList();
        }

        #endregion

        #region === METODY POMOCNICZE ===

        /// <summary>
        /// Sprawdź czy tekst zawiera słowo (z word boundaries)
        /// </summary>
        private static bool ContainsWord(string text, string word)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word))
                return false;

            // Escape special regex characters in word
            var escapedWord = Regex.Escape(word);

            // Word boundary match
            var pattern = $@"\b{escapedWord}\b";

            return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Pobierz etykietę poziomu zagrożenia
        /// </summary>
        private static string GetThreatLevelLabel(ThreatLevel level)
        {
            return level switch
            {
                ThreatLevel.Critical => "KRYTYCZNE",
                ThreatLevel.High => "WYSOKIE",
                ThreatLevel.Medium => "UMIARKOWANE",
                ThreatLevel.Low => "NISKIE",
                ThreatLevel.Opportunity => "SZANSA",
                _ => "INFO"
            };
        }

        #endregion
    }

    #region === MODELE ===

    /// <summary>
    /// Informacje o podmiocie branżowym
    /// </summary>
    public class EntityInfo
    {
        /// <summary>
        /// Pełna nazwa podmiotu
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Typ podmiotu
        /// </summary>
        public EntityType Type { get; set; }

        /// <summary>
        /// Kategoria (np. "Producent drobiu", "Fundusz inwestycyjny")
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Krótki opis (1 zdanie)
        /// </summary>
        public string ShortDescription { get; set; }

        /// <summary>
        /// Pełny opis z analizą wpływu na Piórkowscy
        /// </summary>
        public string FullDescription { get; set; }

        /// <summary>
        /// Poziom zagrożenia dla Piórkowscy
        /// </summary>
        public ThreatLevel ThreatLevel { get; set; }

        /// <summary>
        /// Alternatywne nazwy (do wyszukiwania w tekście)
        /// </summary>
        public string[] Aliases { get; set; }

        /// <summary>
        /// Powiązane podmioty
        /// </summary>
        public string[] RelatedEntities { get; set; }

        /// <summary>
        /// Strona internetowa
        /// </summary>
        public string Website { get; set; }

        /// <summary>
        /// Lokalizacja
        /// </summary>
        public string Location { get; set; }
    }

    /// <summary>
    /// Typ podmiotu
    /// </summary>
    public enum EntityType
    {
        /// <summary>Konkurent (producent drobiu)</summary>
        Competitor,

        /// <summary>Inwestor/Właściciel</summary>
        Investor,

        /// <summary>Importer (konkurencja zagraniczna)</summary>
        Importer,

        /// <summary>Organizacja branżowa</summary>
        Organization,

        /// <summary>Urząd państwowy/UE</summary>
        Government,

        /// <summary>Klient (sieć handlowa)</summary>
        Customer,

        /// <summary>Osoba (manager, właściciel)</summary>
        Person,

        /// <summary>Regulacja/Umowa</summary>
        Regulation
    }

    /// <summary>
    /// Poziom zagrożenia dla Piórkowscy
    /// </summary>
    public enum ThreatLevel
    {
        /// <summary>Zagrożenie krytyczne - wymaga natychmiastowej uwagi</summary>
        Critical,

        /// <summary>Wysokie zagrożenie</summary>
        High,

        /// <summary>Umiarkowane zagrożenie</summary>
        Medium,

        /// <summary>Niskie zagrożenie</summary>
        Low,

        /// <summary>Informacyjne (brak bezpośredniego zagrożenia)</summary>
        Info,

        /// <summary>Szansa biznesowa</summary>
        Opportunity
    }

    #endregion
}
