using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.MarketIntelligence.Models;

namespace Kalendarz1.MarketIntelligence.Services
{
    public class MarketIntelligenceService
    {
        private readonly string _connectionString;

        public MarketIntelligenceService(string connectionString = null)
        {
            _connectionString = connectionString ??
                "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        }

        #region Inicjalizacja i Seed Data

        public async Task EnsureTablesExistAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                -- Tabela artykułów
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_Articles')
                BEGIN
                    CREATE TABLE dbo.intel_Articles (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Title NVARCHAR(500) NOT NULL,
                        Body NVARCHAR(MAX),
                        Source NVARCHAR(200),
                        SourceUrl NVARCHAR(500),
                        Category NVARCHAR(50) NOT NULL,
                        Severity NVARCHAR(20) NOT NULL,
                        AiAnalysis NVARCHAR(MAX),
                        PublishDate DATETIME NOT NULL,
                        CreatedAt DATETIME DEFAULT GETDATE(),
                        IsActive BIT DEFAULT 1,
                        Tags NVARCHAR(500)
                    );
                    CREATE INDEX IX_intel_Articles_Category ON intel_Articles(Category);
                    CREATE INDEX IX_intel_Articles_Severity ON intel_Articles(Severity);
                    CREATE INDEX IX_intel_Articles_PublishDate ON intel_Articles(PublishDate DESC);
                END;

                -- Tabela cen
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_Prices')
                BEGIN
                    CREATE TABLE dbo.intel_Prices (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Date DATE NOT NULL,
                        PriceType NVARCHAR(50) NOT NULL,
                        Value DECIMAL(10,2) NOT NULL,
                        Unit NVARCHAR(20) DEFAULT 'PLN/kg',
                        Source NVARCHAR(100),
                        CreatedAt DATETIME DEFAULT GETDATE()
                    );
                    CREATE INDEX IX_intel_Prices_Date ON intel_Prices(Date DESC);
                END;

                -- Tabela cen pasz
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_FeedPrices')
                BEGIN
                    CREATE TABLE dbo.intel_FeedPrices (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Date DATE NOT NULL,
                        Commodity NVARCHAR(50) NOT NULL,
                        Value DECIMAL(10,2) NOT NULL,
                        Unit NVARCHAR(20) DEFAULT 'EUR/t',
                        Market NVARCHAR(50) DEFAULT 'MATIF',
                        CreatedAt DATETIME DEFAULT GETDATE()
                    );
                    CREATE INDEX IX_intel_FeedPrices_Date ON intel_FeedPrices(Date DESC);
                END;

                -- Tabela HPAI
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_HpaiOutbreaks')
                BEGIN
                    CREATE TABLE dbo.intel_HpaiOutbreaks (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Date DATE NOT NULL,
                        Region NVARCHAR(100) NOT NULL,
                        Country NVARCHAR(50) DEFAULT 'PL',
                        BirdsAffected INT,
                        OutbreakCount INT DEFAULT 1,
                        Notes NVARCHAR(500),
                        CreatedAt DATETIME DEFAULT GETDATE()
                    );
                    CREATE INDEX IX_intel_HpaiOutbreaks_Date ON intel_HpaiOutbreaks(Date DESC);
                END;

                -- Tabela konkurencji
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_Competitors')
                BEGIN
                    CREATE TABLE dbo.intel_Competitors (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Name NVARCHAR(200) NOT NULL,
                        Owner NVARCHAR(200),
                        Country NVARCHAR(50),
                        Revenue NVARCHAR(100),
                        DailyCapacity NVARCHAR(100),
                        Notes NVARCHAR(MAX),
                        LastUpdated DATETIME DEFAULT GETDATE()
                    );
                END;

                -- Tabela benchmark EU
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_EuBenchmark')
                BEGIN
                    CREATE TABLE dbo.intel_EuBenchmark (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Date DATE NOT NULL,
                        Country NVARCHAR(50) NOT NULL,
                        PricePer100kg DECIMAL(10,2) NOT NULL,
                        ChangeMonthPercent DECIMAL(5,2),
                        CreatedAt DATETIME DEFAULT GETDATE()
                    );
                    CREATE INDEX IX_intel_EuBenchmark_Date ON intel_EuBenchmark(Date DESC);
                END;
            ";

            using var cmd = new SqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task SeedDataIfEmptyAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Sprawdź czy są dane
            using var checkCmd = new SqlCommand("SELECT COUNT(*) FROM intel_Articles", conn);
            var count = (int)await checkCmd.ExecuteScalarAsync();

            if (count > 0) return; // Dane już istnieją

            // === SEED ARTICLES ===
            var articles = GetSeedArticles();
            foreach (var a in articles)
            {
                var sql = @"INSERT INTO intel_Articles (Title, Body, Source, Category, Severity, AiAnalysis, PublishDate, Tags)
                            VALUES (@Title, @Body, @Source, @Category, @Severity, @AiAnalysis, @PublishDate, @Tags)";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Title", a.Title);
                cmd.Parameters.AddWithValue("@Body", (object)a.Body ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Source", (object)a.Source ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Category", a.Category);
                cmd.Parameters.AddWithValue("@Severity", a.Severity);
                cmd.Parameters.AddWithValue("@AiAnalysis", (object)a.AiAnalysis ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PublishDate", a.PublishDate);
                cmd.Parameters.AddWithValue("@Tags", (object)a.Tags ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }

            // === SEED PRICES ===
            await SeedPricesAsync(conn);

            // === SEED FEED PRICES ===
            await SeedFeedPricesAsync(conn);

            // === SEED HPAI ===
            await SeedHpaiAsync(conn);

            // === SEED COMPETITORS ===
            await SeedCompetitorsAsync(conn);

            // === SEED EU BENCHMARK ===
            await SeedEuBenchmarkAsync(conn);
        }

        private List<IntelArticle> GetSeedArticles()
        {
            return new List<IntelArticle>
            {
                // === KATEGORIA: HPAI/Choroby ===
                new IntelArticle
                {
                    Title = "HPAI Polska 2026: 18 ognisk w styczniu, 1.5M ptaków",
                    Body = "Główny Lekarz Weterynarii potwierdził 18 ognisk HPAI H5N1 w styczniu 2026. Dotknięte województwa: wielkopolskie (5), podlaskie (4), mazowieckie (3), łódzkie (2), lubuskie (2), lubelskie (2). Łącznie 1.5M ptaków do likwidacji. Budżet odszkodowań 2026: 1.1 mld PLN.",
                    Source = "GLW/PIW",
                    Category = "HPAI",
                    Severity = "critical",
                    AiAnalysis = "KRYTYCZNE: 2 ogniska w woj. łódzkim = nasz region! Brzeziny potencjalnie w strefie zagrożonej. NATYCHMIAST: (1) wzmocnić bioasekurację na rampie przyjęć, (2) zweryfikować status zdrowotny dostawców z łódzkiego, (3) sprawdzić strefy restriction u PIW Brzeziny.",
                    PublishDate = new DateTime(2026, 1, 31),
                    Tags = "HPAI,GLW,łódzkie,bioasekuracja"
                },
                new IntelArticle
                {
                    Title = "Prokuratura bada ubojnię w Żaganiu — ptaki z ogniska HPAI trafiły na ubój",
                    Body = "Prokuratura w Żaganiu wszczęła śledztwo ws. ubojni, do której trafiły ptaki z fermy objętej HPAI. Podejrzenie naruszenia przepisów weterynaryjnych. Grozi cofnięcie pozwolenia weterynaryjnego.",
                    Source = "RMF24",
                    Category = "HPAI",
                    Severity = "critical",
                    AiAnalysis = "PRECEDENS! Jeśli ubojnia straci pozwolenie za przyjęcie ptaków z ogniska — to sygnał dla całej branży. SPRAWDZIĆ: procedury weryfikacji świadectw zdrowia przy każdej dostawie. Żaden transport bez aktualnego dokumentu od PLW.",
                    PublishDate = new DateTime(2026, 1, 28),
                    Tags = "HPAI,prokuratura,ubojnia,prawo"
                },
                new IntelArticle
                {
                    Title = "Pomorze: martwe ptaki w Sopocie i Gdyni — HPAI potwierdzone",
                    Body = "W Sopocie (~100 ptaków), Gdyni (~100) i Pruszczu Gdańskim (32) potwierdzono HPAI u dzikich ptaków. Strefy ochronne nie dotyczą ferm, ale podwyższono nadzór w województwie pomorskim.",
                    Source = "PAP",
                    Category = "HPAI",
                    Severity = "warning",
                    AiAnalysis = "Pomorze to nasz rynek zbytu (dostawy do Trójmiasta). Monitorować czy nie wprowadzą ograniczeń transportowych. Dziki ptak = wirus krąży, wiosenna migracja za 4-6 tygodni spotęguje ryzyko.",
                    PublishDate = new DateTime(2026, 1, 25),
                    Tags = "HPAI,pomorskie,dzikie ptaki"
                },
                new IntelArticle
                {
                    Title = "UE: 467 ognisk HPAI wrzesień-grudzień 2025",
                    Body = "EFSA raportuje 467 ognisk w UE w okresie IX-XII 2025. Najwięcej: Niemcy (163), Francja (106), Włochy (46), Polska (44), Holandia (28). Presja na sezon 2026 utrzymuje się.",
                    Source = "EFSA",
                    Category = "HPAI",
                    Severity = "warning",
                    AiAnalysis = "Polska 4. miejsce w UE pod względem ognisk. Niemcy i Francja mają więcej — ale też więcej eksportują. Każde ognisko u konkurenta (DE, FR) = szansa eksportowa dla PL, ale też ryzyko regionalnych zakazów.",
                    PublishDate = new DateTime(2026, 1, 20),
                    Tags = "HPAI,EFSA,UE,statystyki"
                },
                new IntelArticle
                {
                    Title = "Wielkopolskie: ND + HPAI — GLW wysłał 2 zespoły śledcze",
                    Body = "W woj. wielkopolskim wykryto jednoczesne ogniska choroby Newcastle (ND) i HPAI. Główny Lekarz Weterynarii wysłał dwa zespoły dochodzeniowe. Sytuacja bez precedensu w ostatnich latach.",
                    Source = "GLW",
                    Category = "HPAI",
                    Severity = "warning",
                    AiAnalysis = "Wielkopolskie to główny region hodowlany PL. Jeśli sytuacja się pogorszy — możliwe REGIONALNE braki żywca. Rozważyć zabezpieczenie dodatkowych kontraktów z dostawcami spoza wielkopolskiego.",
                    PublishDate = new DateTime(2026, 1, 22),
                    Tags = "HPAI,Newcastle,wielkopolskie"
                },
                new IntelArticle
                {
                    Title = "KFHDiP proponuje pilotaż szczepień kur niosek przeciw HPAI",
                    Body = "Krajowa Federacja Hodowców Drobiu i Producentów proponuje pilotażowy program szczepień kur niosek preparatem INNOVAX-ND-H5 zatwierdzonym przez UE. Program objąłby 2-3 regiony o najwyższym ryzyku.",
                    Source = "farmer.pl",
                    Category = "HPAI",
                    Severity = "info",
                    AiAnalysis = "Szczepienia niosek, nie broilerów — więc bezpośrednio nas nie dotyczy. ALE: jeśli zmniejszy straty niosek → ceny jaj spadną → mniejsza presja na zamienniki białka → stabilizacja popytu na kurczaka.",
                    PublishDate = new DateTime(2026, 1, 15),
                    Tags = "HPAI,szczepienia,nioski"
                },

                // === KATEGORIA: Konkurencja ===
                new IntelArticle
                {
                    Title = "ADQ z Abu Dhabi rozmawia o przejęciu Cedrobu — wycena ~8 mld PLN",
                    Body = "Fundusz ADQ z Abu Dhabi (aktywa $150 mld) prowadzi zaawansowane rozmowy o przejęciu Grupy Cedrob, największego producenta drobiu w Polsce. Wycena szacowana na ok. 8 mld PLN. ADQ już kontroluje LDC Group, właściciela Drosedu.",
                    Source = "PulsHR/Bloomberg",
                    Category = "Konkurencja",
                    Severity = "critical",
                    AiAnalysis = "MEGA NEWS! Jeśli ADQ kupi Cedrob — jeden fundusz kontrolowałby #1 (Cedrob) i #2-3 (Drosed/Indykpol) producenta w PL! Potencjalna dominacja rynkowa. Monitorować UOKiK — mogą zablokować. Dla nas: ryzyko monopolizacji cen skupu w regionie.",
                    PublishDate = new DateTime(2026, 1, 30),
                    Tags = "Cedrob,ADQ,przejęcie,M&A"
                },
                new IntelArticle
                {
                    Title = "UOKiK zatwierdził przejęcie aktywów Animex Foods przez Cedrob",
                    Body = "UOKiK wydał zgodę na przejęcie przez Cedrob S.A. aktywów Animex Foods — wylęgarni gęsi w Bielsku Podlaskim i Siemiatyczach, zakładu pierza w Dobczycach, zakładu szywalniczego w Kawcu. Cedrob zatrudni 100+ nowych pracowników.",
                    Source = "UOKiK",
                    Category = "Konkurencja",
                    Severity = "info",
                    AiAnalysis = "Cedrob wchodzi w segment gęsi i pierza — dywersyfikacja poza kurczaki. Animex (Smithfield/WH Group) oddaje aktywa = restrukturyzacja? Sprawdzić czy Animex nie sprzedaje więcej zakładów — możliwość przejęcia klientów.",
                    PublishDate = new DateTime(2025, 5, 30),
                    Tags = "Cedrob,Animex,UOKiK,przejęcie"
                },
                new IntelArticle
                {
                    Title = "Zbigniew Jagiełło dołączył do Rady Nadzorczej SuperDrob/LipCo",
                    Body = "Zbigniew Jagiełło, były prezes PKO BP i twórca BLIKa, dołączył 1 stycznia 2026 do Rady Nadzorczej LipCo Foods (SuperDrob). Grupa planuje podwoić przychody z $1 mld do $2 mld, otworzyć fabryki za granicą. Inwestycje 2025: 180 mln PLN. Partnerstwo z CPF Tajlandia.",
                    Source = "LipCo Foods",
                    Category = "Konkurencja",
                    Severity = "warning",
                    AiAnalysis = "POWAŻNE! Jagiełło to najlepszy bankier w PL — nie przychodzi do firmy za 1 mld USD bez powodu. LipCo szykuje się do IPO lub mega-ekspansji. SuperDrob z profesjonalnym zarządzaniem + kapitałem CPF = coraz groźniejszy konkurent. Mogą agresywnie wejść w naszych klientów.",
                    PublishDate = new DateTime(2026, 1, 1),
                    Tags = "SuperDrob,LipCo,Jagiełło,CPF"
                },
                new IntelArticle
                {
                    Title = "Drosed/LDC: mega-konsolidacja — Indykpol + Konspol + ECF Germany",
                    Body = "LDC Group (Francja, €5.1 mld) przez Drosedu przejął: Indykpol (lipiec 2024), Konspol Nowy Sącz od Cargill (czerwiec 2024, €35M, 600 pracowników), ECF Group Niemcy (październik 2024, €80M, mrożonki + plant-based). Drosed stał się mega-grupą.",
                    Source = "LDC Group",
                    Category = "Konkurencja",
                    Severity = "warning",
                    AiAnalysis = "LDC/Drosed to teraz konglomerat: indyk (Indykpol) + kurczak (Drosed) + przetwory (Konspol) + mrożonki/roślinne (ECF) + eksport. Mają skalę, której my nie osiągniemy. Strategia: NIE konkurować ceną z gigantami, ale świeżością i lokalnością.",
                    PublishDate = new DateTime(2025, 10, 15),
                    Tags = "Drosed,LDC,Indykpol,Konspol,konsolidacja"
                },
                new IntelArticle
                {
                    Title = "Plukon Food Group: 38 zakładów w UE, 2 w Polsce (Sieradz, Katowice)",
                    Body = "Holenderski Plukon Food Group operuje 38 zakładami w UE (NL, DE, BE, FR, PL, ES). W Polsce: zakład w Sieradzu i Katowicach. Specjalizacja: fresh poultry, convenience, plant-based. Obroty grupy szacowane na €3+ mld.",
                    Source = "Plukon",
                    Category = "Konkurencja",
                    Severity = "info",
                    AiAnalysis = "SIERADZ = 80 km od Brzezin! Plukon to bezpośredni konkurent na rynku żywca w regionie łódzkim. 38 zakładów = ogromna siła negocjacyjna wobec hodowców. Monitorować ich ceny skupu — mogą podbijać naszych dostawców.",
                    PublishDate = new DateTime(2025, 12, 1),
                    Tags = "Plukon,Sieradz,Holandia,konkurencja lokalna"
                },
                new IntelArticle
                {
                    Title = "Drobimex Szczecin pod skrzydłami PHW-Gruppe (Wiesenhof)",
                    Body = "Drobimex w Szczecinie należy do niemieckiej PHW-Gruppe, właściciela marki Wiesenhof, #1 producenta drobiu w Niemczech. PHW produkuje 460 000 ton/rok. Drobimex działa na rynku polskim i eksportowym.",
                    Source = "dane branżowe",
                    Category = "Konkurencja",
                    Severity = "info",
                    AiAnalysis = "PHW/Wiesenhof = #1 Niemcy. Drobimex to ich polska baza. Raczej daleko od nas (Szczecin), ale na rynku eksportowym konkurujemy o tych samych klientów w DE.",
                    PublishDate = new DateTime(2025, 11, 1),
                    Tags = "Drobimex,PHW,Wiesenhof,Niemcy"
                },
                new IntelArticle
                {
                    Title = "Exdrob Kutno przejął Sadrobem — nowy gracz w łódzkim",
                    Body = "Exdrob z Kutna (nie mylić z bankrutem Exdrob Łódź z 2022) przejął zakład Sadrobem. Rozbudowuje moce produkcyjne w regionie łódzkim. Firma rodzinna z ambicjami regionalnego lidera.",
                    Source = "dane lokalne",
                    Category = "Konkurencja",
                    Severity = "info",
                    AiAnalysis = "UWAGA LOKALNA! Kutno = 100 km od Brzezin. Nowy konkurent w regionie łódzkim. Mogą zacząć podbierać nam dostawców żywca i klientów. Obserwować ich ceny skupu i asortyment.",
                    PublishDate = new DateTime(2025, 10, 1),
                    Tags = "Exdrob,Kutno,łódzkie,lokalny"
                },

                // === KATEGORIA: Ceny & Rynek ===
                new IntelArticle
                {
                    Title = "Ceny skupu 02.02.2026: wolny rynek 4.72 zł/kg, kontraktacja 4.87 zł/kg",
                    Body = "Ceny skupu kurcząt: wolny rynek 4.72 zł/kg (-0.02 tydzień), kontraktacja 4.87 zł/kg (stabilna). Tuszka hurtem 7.33 zł/kg (-0.23 tydzień). Relacja żywiec/pasza: 4.24 (rok temu 3.39).",
                    Source = "e-drób/PIR",
                    Category = "Ceny",
                    Severity = "info",
                    AiAnalysis = "Spadek tuszki -0.23 w tydzień to sygnał słabnącego popytu w hurcie. Ale relacja żywiec/pasza 4.24 vs 3.39 rok temu = hodowcy mają świetną rentowność → będą zwiększać produkcję → więcej żywca za 6-8 tyg.",
                    PublishDate = new DateTime(2026, 2, 2),
                    Tags = "ceny,skup,tuszka,relacja"
                },
                new IntelArticle
                {
                    Title = "Ceny elementów: filet 24.50, udko 8.90, skrzydło 7.80 zł/kg",
                    Body = "Filet z piersi 24.50 zł/kg (+2.1% tydzień), udko 8.90 (+2.7%), podudzie 10.20, skrzydło 7.80 (-1.2%), ćwiartka 7.10, noga 8.90, korpus 3.20 zł/kg.",
                    Source = "giełda drobiowa",
                    Category = "Ceny",
                    Severity = "info",
                    AiAnalysis = "Filet i udko rosną — popyt konsumencki stabilny. Skrzydło spada — sezon grillowy daleko. Korpus 3.20 = najniżej opłacalny element. Optymalizować rozkrój pod filet i udko.",
                    PublishDate = new DateTime(2026, 2, 2),
                    Tags = "ceny,elementy,filet,udko"
                },
                new IntelArticle
                {
                    Title = "Ceny detaliczne: rozpiętość 9.80-11.99 zł/kg tuszki w sieciach",
                    Body = "Tuszka w detalu: Makro 9.80, Kaufland 10.49, Biedronka 10.99, Netto 10.99, Dino 11.49, Lidl 11.49, Auchan 11.99, Carrefour 11.99 zł/kg. Filet: 22.99-25.99 zł/kg. Marże detaliczne 40-55% nad hurtem.",
                    Source = "monitoring cen",
                    Category = "Ceny",
                    Severity = "info",
                    AiAnalysis = "Makro najtańszy (HoReCa) a Carrefour najdroższy. Marże sieci 40-55% = tam jest kasa, nie u producenta. Rozważyć rozbudowę własnego sklepu/e-commerce aby przechwycić część marży detalicznej.",
                    PublishDate = new DateTime(2026, 2, 2),
                    Tags = "ceny,detal,sieci,marże"
                },
                new IntelArticle
                {
                    Title = "Relacja żywiec/pasza: 4.24 — najlepsza od 2 lat",
                    Body = "Wskaźnik relacji ceny żywca do paszy wynosi 4.24, wobec 3.39 rok temu. Oznacza to bardzo dobrą rentowność hodowców — za 1 kg paszy dostają równowartość 4.24 kg żywca. Historyczna średnia: 3.5-3.8.",
                    Source = "obliczenia własne",
                    Category = "Ceny",
                    Severity = "positive",
                    AiAnalysis = "DOBRA WIADOMOŚĆ dla hodowców = ZŁA dla nas w perspektywie. Wysoka rentowność hodowli → wszyscy zwiększają stada → za 2-3 miesiące nadpodaż żywca → SPADEK cen skupu. Przygotować się na negocjacje w dół za Q2.",
                    PublishDate = new DateTime(2026, 2, 2),
                    Tags = "relacja,pasza,rentowność"
                },
                new IntelArticle
                {
                    Title = "Produkcja drobiu Q3 2025: +7.1% rok do roku",
                    Body = "GUS raportuje wzrost produkcji drobiu w Q3 2025 o 7.1% rok do roku. Polska pozostaje #1 producentem w UE z szacowaną produkcją ~3M ton/rok. Ponad 50% idzie na eksport.",
                    Source = "GUS",
                    Category = "Ceny",
                    Severity = "info",
                    AiAnalysis = "+7.1% to silny wzrost. Przy stabilnym eksporcie = więcej towaru na rynku krajowym = presja na ceny. Sprawdzić czy wzrost idzie w kurczaki (nasz segment) czy indyki/kaczki.",
                    PublishDate = new DateTime(2026, 1, 15),
                    Tags = "produkcja,GUS,statystyki"
                },
                new IntelArticle
                {
                    Title = "Kryzys jajeczny: ceny jaj +12-30%, 5M niosek zlikwidowanych przez HPAI",
                    Body = "Ceny jaj konsumpcyjnych wzrosły o 12-30% w zależności od klasy. Przyczyna: likwidacja ~5M kur niosek w wyniku HPAI. Import jaj z Ukrainy (102k ton w 2025) nie kompensuje niedoboru.",
                    Source = "PIR/IERiGŻ",
                    Category = "Ceny",
                    Severity = "warning",
                    AiAnalysis = "Kryzys jajeczny to SZANSA pośrednia: droższe jaja → konsumenci przechodzą na mięso kurczaka jako tańsze białko. Handlowcy powinni podkreślać klientom: 'kurczak to najtańsze białko na rynku'.",
                    PublishDate = new DateTime(2026, 1, 25),
                    Tags = "jaja,kryzys,HPAI,ceny"
                },
                new IntelArticle
                {
                    Title = "MATIF 02.02: kukurydza 192.50 EUR/t, pszenica 189.25 EUR/t",
                    Body = "Kukurydza MAR26: 192.50 EUR/t (-0.39%). Pszenica MAR26: 189.25 EUR/t (stabilna). Rzepak: 474.25 EUR/t. USDA raportuje rekordową globalną produkcję kukurydzy. Niskie ceny pasz wspierają rentowność hodowli.",
                    Source = "MATIF/Euronext",
                    Category = "Ceny",
                    Severity = "positive",
                    AiAnalysis = "Rekordowa kukurydza = tanie pasze = niskie koszty hodowców. OKAZJA: negocjować kontrakty paszowe na Q2-Q3 po obecnych niskich cenach. Zabezpieczyć 3-6 miesięcy z góry.",
                    PublishDate = new DateTime(2026, 2, 2),
                    Tags = "MATIF,kukurydza,pszenica,pasze"
                },

                // === KATEGORIA: Eksport ===
                new IntelArticle
                {
                    Title = "Chiny: umowa o regionalizacji podpisana 15.09.2025, ale wdrożenie opóźnione 5+ miesięcy",
                    Body = "Umowa o regionalizacji HPAI między Polską a Chinami (GACC) podpisana 15 września 2025. System NUTS-3 (73 subregiony). 9 polskich zakładów z pozwoleniami, 40+ czeka. Przed 2020 eksport wynosił 55M EUR/rok. Mimo podpisania, chińska administracja wciąż finalizuje procedury. Biuro KRD-IG w Szanghaju aktywne.",
                    Source = "KRD-IG/MRiRW",
                    Category = "Eksport",
                    Severity = "warning",
                    AiAnalysis = "Rozczarowanie. Podpisanie we wrześniu miało otworzyć rynek w październiku. Styczeń 2026 — wciąż nic. Chińczycy grają na czas. DLA NAS: nie planować przychodów z Chin na Q1-Q2 2026. Długoterminowo: łapki i skrzydła to złoto na rynku chińskim.",
                    PublishDate = new DateTime(2026, 1, 15),
                    Tags = "Chiny,eksport,regionalizacja,GACC"
                },
                new IntelArticle
                {
                    Title = "Chiny: cła na wieprzowiną UE (18.12) i nabiał 21.9-42.7% (23.12)",
                    Body = "Chiny nałożyły cła na import wieprzowiny z UE (18 grudnia 2025) i nabiału 21.9-42.7% (23 grudnia 2025). Retorsja za cła UE na chińskie EV. Oczekiwane cła na wołowinę od 26.01.2026. Drób na razie nie objęty.",
                    Source = "Reuters/MOFCOM",
                    Category = "Eksport",
                    Severity = "warning",
                    AiAnalysis = "Drób nie objęty cłami — NA RAZIE. Ale klimat handlowy UE-Chiny się pogarsza. Jeśli Chiny nałożą cła na drób = katastrofa dla planów eksportowych. Monitorować sytuację. Paradoks: cła na wieprz/nabiał mogą ZWIĘKSZYĆ zapotrzebowanie Chin na drób.",
                    PublishDate = new DateTime(2025, 12, 28),
                    Tags = "Chiny,cła,handel,UE"
                },
                new IntelArticle
                {
                    Title = "Mercosur: 180k ton drobiu duty-free do UE — umowa podpisana mimo sprzeciwu PL",
                    Body = "Umowa UE-Mercosur podpisana mimo sprzeciwu Polski, Francji i innych krajów. Przewiduje bezcłowy import 180 000 ton drobiu z Brazylii i Argentyny. Brazylijskie koszty produkcji 40-60% niższe od europejskich.",
                    Source = "KE/MRiRW",
                    Category = "Eksport",
                    Severity = "critical",
                    AiAnalysis = "ZAGROŻENIE #1 DŁUGOTERMINOWE. 180k ton taniego brazylijskiego kurczaka na rynku UE = presja cenowa na lata. Brazylia produkuje za 40-60% naszych kosztów. STRATEGIA: uciekać w jakość, świeżość, lokalność. Mrożony brazylijski filet to nie to samo co świeża polska tuszka dostarczana tego samego dnia.",
                    PublishDate = new DateTime(2025, 12, 15),
                    Tags = "Mercosur,Brazylia,import,UE"
                },
                new IntelArticle
                {
                    Title = "Rekord eksportu żywności PL: 48.5 mld EUR, drób 3.5 mld EUR",
                    Body = "Polska pobiła rekord eksportu żywności: 48.5 mld EUR w 2025. Eksport drobiu: 3.5 mld EUR. Główne kierunki: Niemcy, UK, Francja, Holandia. Drób to #2 kategoria eksportowa po mięsie wieprzowym.",
                    Source = "MRiRW/KOWR",
                    Category = "Eksport",
                    Severity = "positive",
                    AiAnalysis = "3.5 mld EUR eksportu drobiu = Polska jest potęgą. Ale MY eksportujemy minimalnie. Pytanie strategiczne: czy wchodzić w eksport (wyższe marże na zachodzie) czy zostać na rynku krajowym (mniejsze ryzyko, stabilność)?",
                    PublishDate = new DateTime(2026, 1, 15),
                    Tags = "eksport,rekord,statystyki"
                },
                new IntelArticle
                {
                    Title = "Brazylia: pre-listing jaj do UE — precedens dla drobiu?",
                    Body = "Brazylia otrzymała wstępną zgodę na eksport jaj konsumpcyjnych do UE. To precedens — do tej pory brazylijski drób wchodził głównie jako mrożonki. Jaja to nowa kategoria. AVEC ostrzega przed dalszą liberalizacją.",
                    Source = "Euractiv/AVEC",
                    Category = "Eksport",
                    Severity = "warning",
                    AiAnalysis = "Jeśli jaja wejdą, to ścieżka jest utorowana dla świeżego brazylijskiego drobiu. Na razie mrożonki, ale co jeśli za 2-3 lata Brazylia zacznie wysyłać świeże filety? Wspierać lobby AVEC/KRD-IG przeciw liberalizacji.",
                    PublishDate = new DateTime(2026, 1, 10),
                    Tags = "Brazylia,jaja,import,AVEC"
                },
                new IntelArticle
                {
                    Title = "ASF w Hiszpanii: wpływ na europejski rynek białka",
                    Body = "Afrykański pomór świń dotarł do Hiszpanii, #1 producenta wieprzowiny w UE. Możliwe ograniczenia eksportowe dla hiszpańskiej wieprzowiny. Historycznie, ograniczenia w wieprzowinie zwiększają popyt na drób.",
                    Source = "OIE/EFSA",
                    Category = "Eksport",
                    Severity = "info",
                    AiAnalysis = "ASF w Hiszpanii = potencjalny BOOST dla drobiu. Mniej wieprzowiny → konsumenci i przetwórcy szukają zamiennika → kurczak. Przygotować ofertę dla klientów HoReCa i przetwórców, którzy mogą przestawiać się z wieprzowiny.",
                    PublishDate = new DateTime(2026, 1, 20),
                    Tags = "ASF,Hiszpania,wieprzowina,szansa"
                },
                new IntelArticle
                {
                    Title = "Eksport PL do UK rośnie — Brexit nie zaszkodził drobiu",
                    Body = "Eksport polskiego drobiu do Wielkiej Brytanii wzrósł pomimo Brexitu. UK jest 2. największym odbiorcą po Niemczech. Preferencyjne warunki handlowe w ramach TCA. Rosnący popyt na tańsze białko w UK.",
                    Source = "KRD-IG",
                    Category = "Eksport",
                    Severity = "positive",
                    AiAnalysis = "UK to rynek premium vs polska cena. Jeśli myślimy o eksporcie, UK może być łatwiejszy start niż Chiny — język angielski, znane regulacje, stabilny popyt. Ale wymaga certyfikatów eksportowych i logistyki chłodniczej.",
                    PublishDate = new DateTime(2026, 1, 18),
                    Tags = "UK,eksport,Brexit"
                },

                // === KATEGORIA: Analizy ===
                new IntelArticle
                {
                    Title = "PKO BP: ceny skupu 2026 w przedziale -6% do +3%",
                    Body = "Analitycy PKO BP prognozują ceny skupu kurcząt w 2026 w przedziale od -6% do +3% vs 2025. Scenariusz bazowy: stabilizacja. Scenariusz pesymistyczny: nadpodaż + Mercosur + silny PLN = spadek 6%.",
                    Source = "PKO BP Analizy Sektorowe",
                    Category = "Analizy",
                    Severity = "warning",
                    AiAnalysis = "Scenariusz -6% przy 200t/dzień: ~30 gr/kg mniej = ~60 000 zł/dzień mniejszy przychód = ~1.3M zł/miesiąc straty vs plan. KLUCZOWE: przygotować plan cięcia kosztów na wypadek scenariusza pesymistycznego.",
                    PublishDate = new DateTime(2026, 1, 20),
                    Tags = "prognozy,PKO BP,ceny"
                },
                new IntelArticle
                {
                    Title = "Credit Agricole: cena skupu 5.80 zł/kg pod koniec 2026",
                    Body = "Credit Agricole prognozuje wzrost cen skupu do 5.80 zł/kg pod koniec 2026, głównie dzięki rosnącemu popytowi eksportowemu i ograniczonej podaży po HPAI. Obecna cena: 4.72 zł/kg.",
                    Source = "Credit Agricole Polska",
                    Category = "Analizy",
                    Severity = "positive",
                    AiAnalysis = "5.80 vs obecne 4.72 = +22.9% wzrost! Jeśli Credit Agricole ma rację: 200t × 1.08 zł więcej = +216 000 zł/dzień dodatkowego przychodu. Optymistyczne, ale scenariusz wymaga: (1) brak mega-ognisk HPAI w PL, (2) otwarcie Chin, (3) spadek PLN.",
                    PublishDate = new DateTime(2026, 1, 22),
                    Tags = "prognozy,Credit Agricole,ceny"
                },
                new IntelArticle
                {
                    Title = "Rabobank: globalny wzrost produkcji drobiu +2.5% w 2026",
                    Body = "Rabobank prognozuje globalny wzrost produkcji drobiu o 2.5% w 2026 — spowolnienie po 3 latach ~3% wzrostu. Czynniki wzrostu: akceptacja kulturowa, przystępność cenowa, Azja/Afryka/LatAm, trend GLP-1. Ryzyka: HPAI, geopolityka, Mercosur.",
                    Source = "Rabobank Global Poultry Q1/2026",
                    Category = "Analizy",
                    Severity = "info",
                    AiAnalysis = "Globalnie +2.5% to wciąż wzrost, ale wolniejszy. Rabobank zwraca uwagę na trend GLP-1 (leki odchudzające) — pacjenci preferują białko lean, a kurczak jest #1. Nowy driver popytu, którego nie było 2 lata temu.",
                    PublishDate = new DateTime(2026, 1, 25),
                    Tags = "Rabobank,prognozy,globalne"
                },
                new IntelArticle
                {
                    Title = "Rabobank Polska: silny PLN osłabia konkurencyjność eksportu",
                    Body = "Rabobank wskazuje na silny złoty (EUR/PLN ~4.22 vs ~4.40 rok temu) jako zagrożenie dla polskiego eksportu drobiu. ~4% straty na kursie walutowym. Niskie ceny zbóż (rekordowa kukurydza) wspierają stronę kosztową.",
                    Source = "Rabobank",
                    Category = "Analizy",
                    Severity = "warning",
                    AiAnalysis = "4% straty na kursie to dużo przy marżach 3-5% w branży! Eksporterzy tracą, ale MY jako producent na rynek krajowy — paradoksalnie zyskujemy, bo tani import zbóż (w EUR) = niższe koszty pasz w PLN.",
                    PublishDate = new DateTime(2026, 1, 25),
                    Tags = "Rabobank,PLN,kurs,eksport"
                },
                new IntelArticle
                {
                    Title = "Rabobank: wołowina -3.1% globalnie, kurczak zyskuje udziały",
                    Body = "Rabobank prognozuje spadek globalnej produkcji wołowiny o 3.1% — pierwszy spadek gatunków lądowych od 6 lat. Drób i ryby prowadzą wzrost. Konsumenci wrażliwi na ceny przechodzą na tańsze białka. GLP-1 dodatkowo wspiera drób.",
                    Source = "Rabobank Global Animal Protein 2026",
                    Category = "Analizy",
                    Severity = "positive",
                    AiAnalysis = "MEGA TREND: wołowina spada, drób rośnie. To nie jest chwilowy trend — to strukturalna zmiana diety globalnej. Pozycjonować Piórkowscy jako 'zdrowe, lean białko' — marketing pod GLP-1 i health-conscious konsumenta.",
                    PublishDate = new DateTime(2026, 1, 26),
                    Tags = "Rabobank,wołowina,trend,GLP-1"
                },

                // === KATEGORIA: Regulacje ===
                new IntelArticle
                {
                    Title = "KSeF obowiązkowy: od 01.02 dla dużych firm, od 01.04.2026 dla wszystkich",
                    Body = "Krajowy System e-Faktur obowiązkowy od 1 lutego 2026 dla firm z przychodem >200M PLN, od 1 kwietnia 2026 dla wszystkich przedsiębiorców. Od 1 lutego wszystkie firmy muszą odbierać e-faktury. Integracja z systemami ERP wymagana.",
                    Source = "MF/KSeF.gov.pl",
                    Category = "Regulacje",
                    Severity = "critical",
                    AiAnalysis = "DEADLINE 01.04 DLA NAS! Musimy zintegrować ZPSP i Sage Symfonia z KSeF do końca marca. Sprawdzić: (1) czy Symfonia ma moduł KSeF, (2) czy API KSeF działa z naszym systemem fakturowania, (3) przeszkolić księgowość.",
                    PublishDate = new DateTime(2026, 2, 1),
                    Tags = "KSeF,faktury,prawo,deadline"
                },
                new IntelArticle
                {
                    Title = "Ustawa o ochronie ziemi rolnej — do połowy 2026",
                    Body = "Minister Krajewski zapowiada uchwalenie ustawy chroniącej ziemię rolną do połowy 2026. 'Polska tarcza' przed skutkami Mercosur — monitoring importu, standardy jakościowe. Celem ochrona polskiego rolnictwa.",
                    Source = "MRiRW/Min. Krajewski",
                    Category = "Regulacje",
                    Severity = "info",
                    AiAnalysis = "Dobry sygnał polityczny, ale realne efekty dopiero za lata. 'Polska tarcza' to bardziej PR niż realne narzędzie. Mercosur to umowa międzynarodowa — Polska sama jej nie zablokuje.",
                    PublishDate = new DateTime(2026, 1, 20),
                    Tags = "prawo,Mercosur,MRiRW"
                },
                new IntelArticle
                {
                    Title = "Tyson Foods: $48M kary za zmowę cenową na rynku kurczaków",
                    Body = "Tyson Foods (USA) zapłaci $48M kary w ramach ugody za udział w zmowie cenowej na amerykańskim rynku kurczaków. Precedens dla globalnego enforcement antymonopolowego.",
                    Source = "Reuters/USDA",
                    Category = "Regulacje",
                    Severity = "info",
                    AiAnalysis = "Precedens z USA! W Polsce UOKiK też zaczyna być bardziej aktywny. WAŻNE: nie uczestniczyć w żadnych nieformalnych ustaleniach cenowych z konkurentami — nawet 'niewinnych' rozmowach o cenach na targach branżowych.",
                    PublishDate = new DateTime(2026, 1, 15),
                    Tags = "Tyson,zmowa,UOKiK,prawo"
                },
                new IntelArticle
                {
                    Title = "Budżet odszkodowań HPAI 2026: 1.1 mld PLN",
                    Body = "Ministerstwo Rolnictwa zabezpieczyło 1.1 mld PLN na odszkodowania za ptaki zlikwidowane w związku z HPAI w 2026. W 2025 wypłacono ponad 1.14 mld PLN. Hodowcy otrzymują rekompensatę za wartość rynkową ptaków.",
                    Source = "MRiRW",
                    Category = "Regulacje",
                    Severity = "info",
                    AiAnalysis = "1.1 mld PLN = poważne pieniądze podatników. Odszkodowania idą do HODOWCÓW, nie do ubojni. Dla nas: hodowca po likwidacji dostaje kasę i może szybko odbudować stado, ale proces trwa 3-6 miesięcy = tymczasowy brak żywca z tego regionu.",
                    PublishDate = new DateTime(2026, 1, 18),
                    Tags = "HPAI,odszkodowania,budżet"
                },

                // === KATEGORIA: Świat ===
                new IntelArticle
                {
                    Title = "USDA: rekordowa globalna produkcja kukurydzy",
                    Body = "USDA w styczniowym raporcie WASDE potwierdza rekordową globalną produkcję kukurydzy. Zapasy rosną, ceny pod presją spadkową. Kukurydza MATIF na najniższych poziomach od 3 lat.",
                    Source = "USDA WASDE",
                    Category = "Swiat",
                    Severity = "positive",
                    AiAnalysis = "SUPER WIADOMOŚĆ dla kosztów! Kukurydza = 60-70% kosztów paszy. Rekordowa produkcja = niskie ceny = tańszy żywiec za 2-3 miesiące. Negocjować długoterminowe kontrakty z hodowcami TERAZ, zanim ceny skupu spadną.",
                    PublishDate = new DateTime(2026, 1, 20),
                    Tags = "USDA,kukurydza,pasze,globalne"
                },
                new IntelArticle
                {
                    Title = "Tyson USA: straty na wołowinie, zyski na kurczaku",
                    Body = "Tyson Foods raportuje straty na segmencie wołowiny, ale rosnące zyski z kurczaka. Globalny trend: wołowina drożeje (susza, koszty), kurczak zyskuje udziały. Segment chicken: marża operacyjna 8-10%.",
                    Source = "Tyson Foods IR",
                    Category = "Swiat",
                    Severity = "info",
                    AiAnalysis = "Nawet Tyson (#1 świat) zarabia na kurczaku lepiej niż na wołowinie. Marża 8-10% operacyjna — jeśli my mamy mniej, to znaczy że mamy miejsce na optymalizację. Benchmark do porównania z naszymi wynikami.",
                    PublishDate = new DateTime(2026, 1, 22),
                    Tags = "Tyson,USA,marże,benchmark"
                },
                new IntelArticle
                {
                    Title = "MHP Ukraina: #1 producent drobiu w UE (mimo wojny)",
                    Body = "Myronivsky Hliboproduct (MHP) z Ukrainy jest de facto największym producentem drobiu w Europie. Mimo wojny utrzymuje produkcję. Eksportuje 102k ton jaj i rosnące ilości mięsa do UE po preferencyjnych warunkach.",
                    Source = "MHP/Latifundist",
                    Category = "Swiat",
                    Severity = "info",
                    AiAnalysis = "MHP to lewiatan — i ma preferencyjne warunki importu do UE (brak ceł, ulgi wojenne). Ukraiński drób jest 30-40% tańszy od polskiego. To realne zagrożenie, nie tylko Brazylia. Lobbować przez KRD-IG za wyrównaniem warunków.",
                    PublishDate = new DateTime(2026, 1, 24),
                    Tags = "MHP,Ukraina,konkurencja"
                },
                new IntelArticle
                {
                    Title = "Import jaj do UE +65%: Ukraina 102k ton",
                    Body = "Import jaj do UE wzrósł o 65% w 2025, głównie z Ukrainy (102 000 ton). Preferencyjne warunki handlowe w ramach wsparcia Ukrainy. AVEC i organizacje rolnicze protestują przeciw nieograniczonemu importowi.",
                    Source = "Eurostat/AVEC",
                    Category = "Swiat",
                    Severity = "warning",
                    AiAnalysis = "102k ton ukraińskich jaj to DUMPINGOWY import — tańsze jaja z Ukrainy wypierają europejską produkcję. DOBRA STRONA: jeśli tanie jaja z UA nasycą rynek, konsumenci mogą mniej kupować kurczaka jako zamiennik. Ale raczej efekt minimalny.",
                    PublishDate = new DateTime(2026, 1, 26),
                    Tags = "Ukraina,jaja,import,AVEC"
                },
                new IntelArticle
                {
                    Title = "Chiny: kontyngenty na wołowinę, preferencje dla drobiu?",
                    Body = "Chiny rozważają kontyngenty importowe na wołowinę (ograniczenie importu z Brazylii i Australii). Analitycy sugerują, że może to zwiększyć popyt na drób jako tańsze białko na rynku chińskim.",
                    Source = "MOFCOM/analitycy",
                    Category = "Swiat",
                    Severity = "info",
                    AiAnalysis = "Chiny ograniczają wołowinę = więcej miejsca dla kurczaka. Jeśli regionalizacja PL-Chiny WRESZCIE ruszy, to timing jest idealny. Chińczycy lubią łapki, skrzydła, podroby — elementy, które w PL idą po 3-5 zł/kg, a w Chinach po 8-12 zł/kg.",
                    PublishDate = new DateTime(2026, 1, 28),
                    Tags = "Chiny,wołowina,szansa"
                },
                new IntelArticle
                {
                    Title = "Globalne przesunięcia handlowe: Brazylia i Chiny zyskują udziały",
                    Body = "Rabobank: globalny handel drobiem +1.5-2% w 2026 (poniżej wzrostu produkcji). Brazylia i Chiny zyskują udziały rynkowe kosztem UE i USA. Handel rośnie wolniej niż produkcja = więcej towaru na rynkach wewnętrznych.",
                    Source = "Rabobank/FAO",
                    Category = "Swiat",
                    Severity = "info",
                    AiAnalysis = "Handel +1.5% < produkcja +2.5% = nadwyżka zostaje w krajach. Dla PL to znaczy: więcej polskiego kurczaka na polskim rynku = presja na ceny krajowe. Eksport jako 'zawór bezpieczeństwa' jest kluczowy.",
                    PublishDate = new DateTime(2026, 1, 29),
                    Tags = "handel,globalne,Rabobank"
                },

                // === KATEGORIA: Klienci/Sieci ===
                new IntelArticle
                {
                    Title = "Biedronka zmienia program współpracy z dostawcami",
                    Body = "Biedronka (Jeronimo Martins) modyfikuje warunki współpracy z dostawcami mięsa. Nowe wymagania: krótsze terminy dostaw, wyższe kary za niedostarczenie, ale potencjalnie wyższe wolumeny. 3300+ sklepów w Polsce.",
                    Source = "Jeronimo Martins/branża",
                    Category = "Klienci",
                    Severity = "warning",
                    AiAnalysis = "Biedronka to potencjalny MEGA klient (3300 sklepów), ale warunki są mordercze: kary, terminy, marże 2-3%. Jeśli mamy tam handlowca — sprawdzić nowe warunki. Alternatywa: dostawca 2. tier przez pośrednika.",
                    PublishDate = new DateTime(2026, 1, 20),
                    Tags = "Biedronka,sieci,dostawcy"
                },
                new IntelArticle
                {
                    Title = "Dino Polska: 300 nowych sklepów planowanych na 2026",
                    Body = "Dino Polska planuje otwarcie 300 nowych sklepów w 2026 (łącznie 2700+). Sieć rozwija się głównie w mniejszych miastach i na wsi. Priorytet: polscy dostawcy, świeże mięso.",
                    Source = "Dino Polska IR",
                    Category = "Klienci",
                    Severity = "positive",
                    AiAnalysis = "SZANSA! Dino preferuje POLSKICH LOKALNYCH dostawców + jest silne w mniejszych miastach (nasz profil). 300 nowych sklepów = 300 nowych punktów zbytu. Handlowcy powinni nawiązać kontakt z kupcem Dino na region łódzki.",
                    PublishDate = new DateTime(2026, 1, 22),
                    Tags = "Dino,sieci,ekspansja,szansa"
                },
                new IntelArticle
                {
                    Title = "Lidl Polska: strategia premium w kategorii mięs",
                    Body = "Lidl Polska rozwija segment premium w mięsie: marka własna 'Pikok', certyfikowane produkty, wyższe standardy welfare. Poszukuje dostawców z certyfikatami jakości i dobrostanem.",
                    Source = "Lidl/branża",
                    Category = "Klienci",
                    Severity = "info",
                    AiAnalysis = "Lidl premium = wyższe marże ale wyższe wymagania. Czy mamy certyfikaty welfare (KFC, GAP)? Jeśli nie — rozważyć inwestycję. Lidl płaci więcej za certified poultry niż za commodity.",
                    PublishDate = new DateTime(2026, 1, 24),
                    Tags = "Lidl,premium,certyfikaty"
                },
                new IntelArticle
                {
                    Title = "Makro (Metro) stawia na HoReCa i format cash&carry",
                    Body = "Makro (Metro AG) koncentruje się na obsłudze HoReCa (hotele, restauracje, catering). Najniższe ceny tuszki (9.80 zł/kg) wśród sieci. Preferuje dostawców z elastycznym rozbiorem i dostawą dzień-na-dzień.",
                    Source = "Metro AG/Makro",
                    Category = "Klienci",
                    Severity = "info",
                    AiAnalysis = "Makro = HoReCa = NASZ IDEALNY KLIENT. Elastyczny rozbiór, szybka dostawa, świeżość — dokładnie to, co potrafimy lepiej niż giganci. Nawiązać kontakt z kupcem Makro. Cena 9.80 jest niska, ale wolumeny stabilne.",
                    PublishDate = new DateTime(2026, 1, 25),
                    Tags = "Makro,HoReCa,szansa"
                },
                new IntelArticle
                {
                    Title = "Morliny (WH Group) przejmują niemieckiego producenta",
                    Body = "Morliny (część Animex/WH Group) rozszerzają działalność o niemiecki zakład. WH Group konsoliduje europejskie aktywa. Animex to również właściciel zakładów drobiowych w Polsce.",
                    Source = "WH Group/Animex",
                    Category = "Klienci",
                    Severity = "info",
                    AiAnalysis = "WH Group (Chiny/USA) przez Animex/Morliny rośnie w Europie. Jeśli Cedrob przejmie resztki Animex, a WH Group odchudza portfolio — możliwość przejęcia klientów, których WH Group zostawia.",
                    PublishDate = new DateTime(2026, 1, 26),
                    Tags = "Morliny,Animex,WH Group"
                },

                // === KATEGORIA: Pogoda ===
                new IntelArticle
                {
                    Title = "'Bestia ze wschodu': -30°C na Podlasiu, mrozy do połowy lutego",
                    Body = "Siarczysty mróz dotarł do Polski. Na Podlasiu -30°C, w centrum -15 do -20°C. 300 szkół zamkniętych, 3 ofiary. IMGW: mróz utrzyma się do połowy lutego. Krótka odwilż 4-8 lutego (ryzyko gołoledzi), potem powrót mrozu. Przedwiośnie dopiero po 20 lutego.",
                    Source = "IMGW",
                    Category = "Pogoda",
                    Severity = "critical",
                    AiAnalysis = "KRYTYCZNE DLA OPERACJI: (1) Transport żywca: zamarzanie poidełek i ramp, kurczaki narażone na hipotermię podczas załadunku — straty do 2-3%. (2) Fermy: wyższe koszty ogrzewania = hodowcy podniosą ceny. (3) Drogi: gołoledź 4-8 lutego = opóźnienia dostaw. (4) Magazyny: sprawdzić ogrzewanie ramp. ZLECIĆ kierownikom transportu kontrolę pojazdów i izolacji naczep.",
                    PublishDate = new DateTime(2026, 2, 2),
                    Tags = "pogoda,mróz,transport,operacje"
                },
                new IntelArticle
                {
                    Title = "Prognoza luty 2026: mróz, odwilż, znów mróz, przedwiośnie od 20.02",
                    Body = "Luty 2026: I dekada — siarczysty mróz (do -25°C nocą). 4-8 lutego — krótka odwilż, temperatura do +5°C (ryzyko gołoledzi). II dekada — powrót mrozu -10 do -15°C. Od 20 lutego — stopniowe ocieplenie, przedwiośnie.",
                    Source = "IMGW/ICM",
                    Category = "Pogoda",
                    Severity = "warning",
                    AiAnalysis = "PLANOWANIE: (1) 3-8 luty: największe problemy transportowe (mróz + gołoledź). (2) Od 20 luty: stabilizacja. (3) Koszty hodowców: ogrzewanie kurników to +15-25% kosztów produkcji w mrozy = presja na skup od marca.",
                    PublishDate = new DateTime(2026, 2, 2),
                    Tags = "pogoda,prognoza,luty"
                }
            };
        }

        private async Task SeedPricesAsync(SqlConnection conn)
        {
            var prices = new[]
            {
                // Ceny skupu - ostatnie 30 dni
                ("2026-02-02", "WolnyRynek", 4.72m, "PLN/kg"),
                ("2026-02-02", "Kontraktacja", 4.87m, "PLN/kg"),
                ("2026-02-02", "TuszkaHurt", 7.33m, "PLN/kg"),
                ("2026-02-02", "Filet", 24.50m, "PLN/kg"),
                ("2026-02-02", "Udko", 8.90m, "PLN/kg"),
                ("2026-02-02", "Skrzydlo", 7.80m, "PLN/kg"),
                ("2026-02-02", "Podudzie", 10.20m, "PLN/kg"),
                ("2026-02-02", "Cwiartka", 7.10m, "PLN/kg"),
                ("2026-02-02", "Korpus", 3.20m, "PLN/kg"),
                ("2026-02-02", "Noga", 8.90m, "PLN/kg"),

                ("2026-01-26", "WolnyRynek", 4.74m, "PLN/kg"),
                ("2026-01-26", "Kontraktacja", 4.87m, "PLN/kg"),
                ("2026-01-26", "TuszkaHurt", 7.56m, "PLN/kg"),
                ("2026-01-26", "Filet", 24.00m, "PLN/kg"),
                ("2026-01-26", "Udko", 8.66m, "PLN/kg"),

                ("2026-01-19", "WolnyRynek", 4.70m, "PLN/kg"),
                ("2026-01-19", "Kontraktacja", 4.85m, "PLN/kg"),
                ("2026-01-19", "TuszkaHurt", 7.45m, "PLN/kg"),
                ("2026-01-19", "Filet", 23.80m, "PLN/kg"),
                ("2026-01-19", "Udko", 8.50m, "PLN/kg"),

                ("2026-01-12", "WolnyRynek", 4.68m, "PLN/kg"),
                ("2026-01-12", "Kontraktacja", 4.82m, "PLN/kg"),
                ("2026-01-12", "TuszkaHurt", 7.40m, "PLN/kg"),

                ("2026-01-05", "WolnyRynek", 4.65m, "PLN/kg"),
                ("2026-01-05", "Kontraktacja", 4.80m, "PLN/kg"),
                ("2026-01-05", "TuszkaHurt", 7.35m, "PLN/kg"),

                ("2025-12-29", "WolnyRynek", 4.60m, "PLN/kg"),
                ("2025-12-29", "TuszkaHurt", 7.25m, "PLN/kg"),

                ("2025-12-22", "WolnyRynek", 4.55m, "PLN/kg"),
                ("2025-12-22", "TuszkaHurt", 7.15m, "PLN/kg"),
            };

            foreach (var (date, type, value, unit) in prices)
            {
                var sql = "INSERT INTO intel_Prices (Date, PriceType, Value, Unit, Source) VALUES (@Date, @Type, @Value, @Unit, 'e-drób/PIR')";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Date", DateTime.Parse(date));
                cmd.Parameters.AddWithValue("@Type", type);
                cmd.Parameters.AddWithValue("@Value", value);
                cmd.Parameters.AddWithValue("@Unit", unit);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task SeedFeedPricesAsync(SqlConnection conn)
        {
            var feedPrices = new[]
            {
                ("2026-02-02", "Kukurydza", 192.50m, "EUR/t", "MATIF"),
                ("2026-02-02", "Pszenica", 189.25m, "EUR/t", "MATIF"),
                ("2026-02-02", "Rzepak", 474.25m, "EUR/t", "MATIF"),
                ("2026-02-02", "Soja", 385.00m, "EUR/t", "CBOT"),

                ("2026-01-26", "Kukurydza", 193.25m, "EUR/t", "MATIF"),
                ("2026-01-26", "Pszenica", 190.00m, "EUR/t", "MATIF"),

                ("2026-01-19", "Kukurydza", 194.50m, "EUR/t", "MATIF"),
                ("2026-01-19", "Pszenica", 191.25m, "EUR/t", "MATIF"),

                ("2026-01-12", "Kukurydza", 196.00m, "EUR/t", "MATIF"),
                ("2026-01-12", "Pszenica", 193.00m, "EUR/t", "MATIF"),

                ("2026-01-05", "Kukurydza", 198.25m, "EUR/t", "MATIF"),
                ("2026-01-05", "Pszenica", 195.50m, "EUR/t", "MATIF"),

                ("2025-12-29", "Kukurydza", 200.00m, "EUR/t", "MATIF"),
                ("2025-12-29", "Pszenica", 197.00m, "EUR/t", "MATIF"),
            };

            foreach (var (date, commodity, value, unit, market) in feedPrices)
            {
                var sql = "INSERT INTO intel_FeedPrices (Date, Commodity, Value, Unit, Market) VALUES (@Date, @Commodity, @Value, @Unit, @Market)";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Date", DateTime.Parse(date));
                cmd.Parameters.AddWithValue("@Commodity", commodity);
                cmd.Parameters.AddWithValue("@Value", value);
                cmd.Parameters.AddWithValue("@Unit", unit);
                cmd.Parameters.AddWithValue("@Market", market);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task SeedHpaiAsync(SqlConnection conn)
        {
            var outbreaks = new[]
            {
                ("2026-01-31", "wielkopolskie", "PL", 150000, 5),
                ("2026-01-31", "podlaskie", "PL", 120000, 4),
                ("2026-01-31", "mazowieckie", "PL", 95000, 3),
                ("2026-01-31", "łódzkie", "PL", 80000, 2),
                ("2026-01-31", "lubuskie", "PL", 60000, 2),
                ("2026-01-31", "lubelskie", "PL", 55000, 2),

                ("2026-01-25", "pomorskie", "PL", 0, 1), // dzikie ptaki

                ("2025-12-31", "Niemcy", "DE", 500000, 163),
                ("2025-12-31", "Francja", "FR", 350000, 106),
                ("2025-12-31", "Włochy", "IT", 150000, 46),
                ("2025-12-31", "Polska", "PL", 1200000, 44),
                ("2025-12-31", "Holandia", "NL", 100000, 28),
            };

            foreach (var (date, region, country, birds, count) in outbreaks)
            {
                var sql = "INSERT INTO intel_HpaiOutbreaks (Date, Region, Country, BirdsAffected, OutbreakCount) VALUES (@Date, @Region, @Country, @Birds, @Count)";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Date", DateTime.Parse(date));
                cmd.Parameters.AddWithValue("@Region", region);
                cmd.Parameters.AddWithValue("@Country", country);
                cmd.Parameters.AddWithValue("@Birds", birds);
                cmd.Parameters.AddWithValue("@Count", count);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task SeedCompetitorsAsync(SqlConnection conn)
        {
            var competitors = new[]
            {
                ("Cedrob S.A.", "Rodzina Gowin / (negocjacje ADQ)", "PL", "~5 mld PLN", "800k szt/dzień", "Największy producent w PL. Potencjalne przejęcie przez ADQ z Abu Dhabi za ~8 mld PLN."),
                ("Drosed / LDC Group", "LDC Group (FR) / ADQ (UAE)", "PL", "~2 mld PLN", "400k szt/dzień", "Francuzi + Indykpol + Konspol + ECF Germany. Mega-konsolidacja."),
                ("SuperDrob / LipCo Foods", "Rodzina Lipka + CPF (Tajlandia)", "PL", "~1 mld USD", "350k szt/dzień", "Jagiełło w RN. Plany podwojenia przychodów. Partnerstwo z CPF."),
                ("Plukon Food Group", "Plukon (Holandia)", "NL", "€3+ mld", "38 zakładów UE", "Zakłady w Sieradzu i Katowicach. Bezpośredni konkurent w łódzkim!"),
                ("Animex Foods / WH Group", "WH Group (Chiny/USA)", "PL", "~1.5 mld PLN", "200k szt/dzień", "Restrukturyzacja. Sprzedają aktywa Cedrobowi. Morliny to ich część."),
                ("Drobimex Szczecin / PHW", "PHW-Gruppe (DE) / Wiesenhof", "PL", "~500 mln PLN", "150k szt/dzień", "Baza PHW/Wiesenhof (#1 Niemcy) w Polsce. Eksport do DE."),
                ("Exdrob Kutno", "Rodzina (PL)", "PL", "~100 mln PLN", "50k szt/dzień", "Nowy gracz w regionie łódzkim. Przejął Sadrobem. 100 km od Brzezin!"),
                ("Indykpol (LDC)", "LDC Group (FR)", "PL", "~800 mln PLN", "Indyki", "Przejęty przez LDC/Drosed w lipcu 2024. Lider indyków."),
                ("MHP Ukraina", "Koszowy (UA)", "UA", "~2 mld USD", "1M+ szt/dzień", "#1 producent w Europie mimo wojny. Preferencyjny import do UE.")
            };

            foreach (var (name, owner, country, revenue, capacity, notes) in competitors)
            {
                var sql = "INSERT INTO intel_Competitors (Name, Owner, Country, Revenue, DailyCapacity, Notes) VALUES (@Name, @Owner, @Country, @Revenue, @Capacity, @Notes)";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Name", name);
                cmd.Parameters.AddWithValue("@Owner", owner);
                cmd.Parameters.AddWithValue("@Country", country);
                cmd.Parameters.AddWithValue("@Revenue", revenue);
                cmd.Parameters.AddWithValue("@Capacity", capacity);
                cmd.Parameters.AddWithValue("@Notes", notes);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task SeedEuBenchmarkAsync(SqlConnection conn)
        {
            var benchmarks = new[]
            {
                ("2026-02-02", "PL", 189.5m, 5.2m),
                ("2026-02-02", "DE", 215.3m, 2.1m),
                ("2026-02-02", "FR", 225.8m, -1.3m),
                ("2026-02-02", "NL", 198.6m, 3.4m),
                ("2026-02-02", "IT", 242.1m, 4.5m),
                ("2026-02-02", "ES", 208.4m, -0.8m),
                ("2026-02-02", "HU", 178.3m, 6.1m),
                ("2026-02-02", "UA", 125.0m, 1.5m),
                ("2026-02-02", "BR", 125.5m, -2.8m),
                ("2026-02-02", "US", 225.2m, 1.8m),
            };

            foreach (var (date, country, price, change) in benchmarks)
            {
                var sql = "INSERT INTO intel_EuBenchmark (Date, Country, PricePer100kg, ChangeMonthPercent) VALUES (@Date, @Country, @Price, @Change)";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Date", DateTime.Parse(date));
                cmd.Parameters.AddWithValue("@Country", country);
                cmd.Parameters.AddWithValue("@Price", price);
                cmd.Parameters.AddWithValue("@Change", change);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        #endregion

        #region Pobieranie danych

        public async Task<List<IntelArticle>> GetArticlesAsync(string category = null, string severity = null, string searchText = null)
        {
            var result = new List<IntelArticle>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = "SELECT * FROM intel_Articles WHERE IsActive = 1";
            if (!string.IsNullOrEmpty(category) && category != "Wszystkie")
                sql += " AND Category = @Category";
            if (!string.IsNullOrEmpty(severity) && severity != "Wszystkie")
                sql += " AND Severity = @Severity";
            if (!string.IsNullOrEmpty(searchText))
                sql += " AND (Title LIKE @Search OR Body LIKE @Search OR AiAnalysis LIKE @Search)";
            sql += " ORDER BY PublishDate DESC";

            using var cmd = new SqlCommand(sql, conn);
            if (!string.IsNullOrEmpty(category) && category != "Wszystkie")
                cmd.Parameters.AddWithValue("@Category", category);
            if (!string.IsNullOrEmpty(severity) && severity != "Wszystkie")
                cmd.Parameters.AddWithValue("@Severity", severity);
            if (!string.IsNullOrEmpty(searchText))
                cmd.Parameters.AddWithValue("@Search", $"%{searchText}%");

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new IntelArticle
                {
                    Id = (int)reader["Id"],
                    Title = reader["Title"].ToString(),
                    Body = reader["Body"]?.ToString(),
                    Source = reader["Source"]?.ToString(),
                    SourceUrl = reader["SourceUrl"]?.ToString(),
                    Category = reader["Category"].ToString(),
                    Severity = reader["Severity"].ToString(),
                    AiAnalysis = reader["AiAnalysis"]?.ToString(),
                    PublishDate = (DateTime)reader["PublishDate"],
                    CreatedAt = reader["CreatedAt"] != DBNull.Value ? (DateTime)reader["CreatedAt"] : DateTime.Now,
                    IsActive = reader["IsActive"] != DBNull.Value && (bool)reader["IsActive"],
                    Tags = reader["Tags"]?.ToString()
                });
            }

            return result;
        }

        public async Task<List<IntelArticle>> GetAlertsAsync()
        {
            return await GetArticlesAsync(severity: null, searchText: null);
        }

        public async Task<List<IntelPrice>> GetLatestPricesAsync()
        {
            var result = new List<IntelPrice>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"SELECT p.* FROM intel_Prices p
                        INNER JOIN (SELECT PriceType, MAX(Date) as MaxDate FROM intel_Prices GROUP BY PriceType) latest
                        ON p.PriceType = latest.PriceType AND p.Date = latest.MaxDate";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new IntelPrice
                {
                    Id = (int)reader["Id"],
                    Date = (DateTime)reader["Date"],
                    PriceType = reader["PriceType"].ToString(),
                    Value = (decimal)reader["Value"],
                    Unit = reader["Unit"]?.ToString() ?? "PLN/kg",
                    Source = reader["Source"]?.ToString()
                });
            }

            return result;
        }

        public async Task<List<IntelPrice>> GetPriceHistoryAsync(string priceType, int days = 30)
        {
            var result = new List<IntelPrice>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"SELECT * FROM intel_Prices
                        WHERE PriceType = @Type AND Date >= DATEADD(day, -@Days, GETDATE())
                        ORDER BY Date ASC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Type", priceType);
            cmd.Parameters.AddWithValue("@Days", days);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new IntelPrice
                {
                    Id = (int)reader["Id"],
                    Date = (DateTime)reader["Date"],
                    PriceType = reader["PriceType"].ToString(),
                    Value = (decimal)reader["Value"],
                    Unit = reader["Unit"]?.ToString() ?? "PLN/kg"
                });
            }

            return result;
        }

        public async Task<List<IntelFeedPrice>> GetLatestFeedPricesAsync()
        {
            var result = new List<IntelFeedPrice>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"SELECT p.* FROM intel_FeedPrices p
                        INNER JOIN (SELECT Commodity, MAX(Date) as MaxDate FROM intel_FeedPrices GROUP BY Commodity) latest
                        ON p.Commodity = latest.Commodity AND p.Date = latest.MaxDate";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new IntelFeedPrice
                {
                    Id = (int)reader["Id"],
                    Date = (DateTime)reader["Date"],
                    Commodity = reader["Commodity"].ToString(),
                    Value = (decimal)reader["Value"],
                    Unit = reader["Unit"]?.ToString() ?? "EUR/t",
                    Market = reader["Market"]?.ToString() ?? "MATIF"
                });
            }

            return result;
        }

        public async Task<List<IntelHpaiOutbreak>> GetHpaiOutbreaksAsync(string country = "PL")
        {
            var result = new List<IntelHpaiOutbreak>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = country == "ALL"
                ? "SELECT * FROM intel_HpaiOutbreaks ORDER BY Date DESC"
                : "SELECT * FROM intel_HpaiOutbreaks WHERE Country = @Country ORDER BY Date DESC";

            using var cmd = new SqlCommand(sql, conn);
            if (country != "ALL")
                cmd.Parameters.AddWithValue("@Country", country);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new IntelHpaiOutbreak
                {
                    Id = (int)reader["Id"],
                    Date = (DateTime)reader["Date"],
                    Region = reader["Region"].ToString(),
                    Country = reader["Country"]?.ToString() ?? "PL",
                    BirdsAffected = reader["BirdsAffected"] != DBNull.Value ? (int)reader["BirdsAffected"] : 0,
                    OutbreakCount = reader["OutbreakCount"] != DBNull.Value ? (int)reader["OutbreakCount"] : 1,
                    Notes = reader["Notes"]?.ToString()
                });
            }

            return result;
        }

        public async Task<int> GetTotalHpaiOutbreaks2026Async()
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = "SELECT ISNULL(SUM(OutbreakCount), 0) FROM intel_HpaiOutbreaks WHERE Country = 'PL' AND YEAR(Date) = 2026";
            using var cmd = new SqlCommand(sql, conn);
            return (int)await cmd.ExecuteScalarAsync();
        }

        public async Task<List<IntelCompetitor>> GetCompetitorsAsync()
        {
            var result = new List<IntelCompetitor>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = "SELECT * FROM intel_Competitors ORDER BY Country, Name";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new IntelCompetitor
                {
                    Id = (int)reader["Id"],
                    Name = reader["Name"].ToString(),
                    Owner = reader["Owner"]?.ToString(),
                    Country = reader["Country"]?.ToString(),
                    Revenue = reader["Revenue"]?.ToString(),
                    DailyCapacity = reader["DailyCapacity"]?.ToString(),
                    Notes = reader["Notes"]?.ToString(),
                    LastUpdated = reader["LastUpdated"] != DBNull.Value ? (DateTime)reader["LastUpdated"] : DateTime.Now
                });
            }

            return result;
        }

        public async Task<List<IntelEuBenchmark>> GetEuBenchmarkAsync()
        {
            var result = new List<IntelEuBenchmark>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"SELECT b.* FROM intel_EuBenchmark b
                        INNER JOIN (SELECT Country, MAX(Date) as MaxDate FROM intel_EuBenchmark GROUP BY Country) latest
                        ON b.Country = latest.Country AND b.Date = latest.MaxDate
                        ORDER BY b.PricePer100kg DESC";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new IntelEuBenchmark
                {
                    Id = (int)reader["Id"],
                    Date = (DateTime)reader["Date"],
                    Country = reader["Country"].ToString(),
                    PricePer100kg = (decimal)reader["PricePer100kg"],
                    ChangeMonthPercent = reader["ChangeMonthPercent"] != DBNull.Value ? (decimal)reader["ChangeMonthPercent"] : null
                });
            }

            return result;
        }

        public async Task<List<DashboardIndicator>> GetDashboardIndicatorsAsync()
        {
            var indicators = new List<DashboardIndicator>();

            // Ceny skupu
            var prices = await GetLatestPricesAsync();
            var wolnyRynek = prices.FirstOrDefault(p => p.PriceType == "WolnyRynek");
            if (wolnyRynek != null)
            {
                indicators.Add(new DashboardIndicator
                {
                    Name = "Skup wolny rynek",
                    Value = wolnyRynek.Value.ToString("N2"),
                    Unit = "zł/kg",
                    Trend = "down",
                    TrendValue = "-0.02",
                    Category = "Ceny"
                });
            }

            var tuszka = prices.FirstOrDefault(p => p.PriceType == "TuszkaHurt");
            if (tuszka != null)
            {
                indicators.Add(new DashboardIndicator
                {
                    Name = "Tuszka hurt",
                    Value = tuszka.Value.ToString("N2"),
                    Unit = "zł/kg",
                    Trend = "down",
                    TrendValue = "-0.23",
                    Category = "Ceny"
                });
            }

            // Pasze
            var feedPrices = await GetLatestFeedPricesAsync();
            var kukurydza = feedPrices.FirstOrDefault(p => p.Commodity == "Kukurydza");
            if (kukurydza != null)
            {
                indicators.Add(new DashboardIndicator
                {
                    Name = "Kukurydza MATIF",
                    Value = kukurydza.Value.ToString("N2"),
                    Unit = "EUR/t",
                    Trend = "down",
                    TrendValue = "-0.39%",
                    Category = "Pasze"
                });
            }

            // HPAI
            var hpaiCount = await GetTotalHpaiOutbreaks2026Async();
            indicators.Add(new DashboardIndicator
            {
                Name = "HPAI ogniska PL 2026",
                Value = hpaiCount.ToString(),
                Unit = "",
                Trend = hpaiCount > 10 ? "up" : "stable",
                TrendValue = "",
                Category = "HPAI"
            });

            // Eksport Chiny
            indicators.Add(new DashboardIndicator
            {
                Name = "Eksport Chiny",
                Value = "Opóźniony",
                Unit = "",
                Trend = "stable",
                TrendValue = "5+ mies.",
                Category = "Eksport"
            });

            return indicators;
        }

        #endregion
    }
}
