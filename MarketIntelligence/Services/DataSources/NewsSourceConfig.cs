using System;
using System.Collections.Generic;

namespace Kalendarz1.MarketIntelligence.Services.DataSources
{
    /// <summary>
    /// Konfiguracja wszystkich źródeł newsów - polskie i międzynarodowe portale
    /// </summary>
    public static class NewsSourceConfig
    {
        #region Polish Agricultural & Poultry Sources

        /// <summary>
        /// Polskie portale rolnicze i drobiarskie z RSS
        /// </summary>
        public static readonly List<NewsSource> PolishAgricultureRss = new()
        {
            // === GŁÓWNE PORTALE ROLNICZE ===
            new NewsSource
            {
                Id = "farmer_pl",
                Name = "Farmer.pl",
                Url = "https://www.farmer.pl/rss/rss.xml",
                AlternateUrls = new[] { "https://www.farmer.pl/feed", "https://www.farmer.pl/rss" },
                Type = SourceType.Rss,
                Category = "Rolnictwo",
                Language = "pl",
                Priority = 1,
                Keywords = new[] { "drób", "kurczak", "HPAI", "ptasia grypa", "ceny", "skup", "ubojnia" },
                Description = "Główny polski portal rolniczy"
            },
            new NewsSource
            {
                Id = "topagrar_pl",
                Name = "Top Agrar Polska",
                Url = "https://www.topagrar.pl/feed/",
                AlternateUrls = new[] { "https://www.topagrar.pl/rss", "https://www.topagrar.pl/feed/rss" },
                Type = SourceType.Rss,
                Category = "Rolnictwo",
                Language = "pl",
                Priority = 1,
                Keywords = new[] { "drób", "hodowla", "ceny", "rynek" },
                Description = "Niemiecko-polski portal rolniczy"
            },
            new NewsSource
            {
                Id = "agrofakt",
                Name = "Agrofakt.pl",
                Url = "https://www.agrofakt.pl/feed/",
                AlternateUrls = new[] { "https://agrofakt.pl/feed", "https://www.agrofakt.pl/rss" },
                Type = SourceType.Rss,
                Category = "Rolnictwo",
                Language = "pl",
                Priority = 2,
                Keywords = new[] { "drób", "rolnictwo", "dotacje", "ARiMR" },
                Description = "Portal dla rolników"
            },
            new NewsSource
            {
                Id = "agropolska",
                Name = "Agropolska.pl",
                Url = "https://www.agropolska.pl/feed/",
                AlternateUrls = new[] { "https://agropolska.pl/rss" },
                Type = SourceType.Rss,
                Category = "Rolnictwo",
                Language = "pl",
                Priority = 2,
                Keywords = new[] { "drób", "produkcja", "ceny" },
                Description = "Portal rolniczy"
            },
            new NewsSource
            {
                Id = "tygodnik_rolniczy",
                Name = "Tygodnik Rolniczy",
                Url = "https://www.tygodnikrolniczy.pl/feed/",
                AlternateUrls = new[] { "https://tygodnikrolniczy.pl/rss" },
                Type = SourceType.Rss,
                Category = "Rolnictwo",
                Language = "pl",
                Priority = 2,
                Keywords = new[] { "drób", "hodowla", "rynek" },
                Description = "Tygodnik rolniczy online"
            },
            new NewsSource
            {
                Id = "wir_pl",
                Name = "Wiadomości Rolnicze",
                Url = "https://wiadomoscirolnicze.pl/feed/",
                AlternateUrls = new[] { "https://www.wiadomoscirolnicze.pl/rss" },
                Type = SourceType.Rss,
                Category = "Rolnictwo",
                Language = "pl",
                Priority = 2,
                Keywords = new[] { "drób", "hodowla" },
                Description = "Wiadomości dla rolników"
            },
            new NewsSource
            {
                Id = "ppr_pl",
                Name = "PPR.pl - Portal Przemysłowy",
                Url = "https://ppr.pl/feed/",
                AlternateUrls = new[] { "https://www.ppr.pl/rss" },
                Type = SourceType.Rss,
                Category = "Przemysł",
                Language = "pl",
                Priority = 2,
                Keywords = new[] { "drób", "mięso", "przetwórstwo" },
                Description = "Portal przemysłu rolno-spożywczego"
            },

            // === PORTALE SPOŻYWCZE I HANDLOWE ===
            new NewsSource
            {
                Id = "portal_spozywczy",
                Name = "Portal Spożywczy",
                Url = "https://www.portalspozywczy.pl/rss.xml",
                AlternateUrls = new[] { "https://portalspozywczy.pl/feed", "https://www.portalspozywczy.pl/rss" },
                Type = SourceType.Rss,
                Category = "Spożywczy",
                Language = "pl",
                Priority = 1,
                Keywords = new[] { "drób", "mięso", "ceny", "sieci", "Biedronka", "Lidl", "eksport" },
                Description = "Portal branży spożywczej"
            },
            new NewsSource
            {
                Id = "wiadomosci_handlowe",
                Name = "Wiadomości Handlowe",
                Url = "https://www.wiadomoscihandlowe.pl/rss.xml",
                AlternateUrls = new[] { "https://wiadomoscihandlowe.pl/feed" },
                Type = SourceType.Rss,
                Category = "Handel",
                Language = "pl",
                Priority = 1,
                Keywords = new[] { "sieci handlowe", "promocje", "ceny", "Biedronka", "Lidl", "mięso" },
                Description = "Portal handlu detalicznego"
            },
            new NewsSource
            {
                Id = "dlahandlu",
                Name = "DlaHandlu.pl",
                Url = "https://www.dlahandlu.pl/rss.xml",
                AlternateUrls = new[] { "https://dlahandlu.pl/feed" },
                Type = SourceType.Rss,
                Category = "Handel",
                Language = "pl",
                Priority = 2,
                Keywords = new[] { "retail", "sieci", "ceny" },
                Description = "Portal dla handlu"
            },
            new NewsSource
            {
                Id = "retailnet",
                Name = "RetailNet.pl",
                Url = "https://retailnet.pl/feed/",
                AlternateUrls = new[] { "https://www.retailnet.pl/rss" },
                Type = SourceType.Rss,
                Category = "Handel",
                Language = "pl",
                Priority = 3,
                Keywords = new[] { "retail", "sieci handlowe" },
                Description = "Portal o handlu detalicznym"
            },

            // === PORTALE DROBIARSKIE SPECJALISTYCZNE ===
            new NewsSource
            {
                Id = "e_drob",
                Name = "e-Drob.pl",
                Url = "https://e-drob.pl/feed/",
                AlternateUrls = new[] { "https://www.e-drob.pl/rss" },
                Type = SourceType.Rss,
                Category = "Drób",
                Language = "pl",
                Priority = 1,
                Keywords = new[] { "drób", "kurczak", "indyk", "ceny skupu", "ubojnia" },
                Description = "Specjalistyczny portal drobiarski"
            },
            new NewsSource
            {
                Id = "drobiarstwo_pl",
                Name = "Drobiarstwo.pl",
                Url = "https://drobiarstwo.pl/feed/",
                AlternateUrls = new[] { "https://www.drobiarstwo.pl/rss" },
                Type = SourceType.Rss,
                Category = "Drób",
                Language = "pl",
                Priority = 1,
                Keywords = new[] { "drób", "kurczak", "brojler", "hodowla" },
                Description = "Portal branży drobiarskiej"
            },
            new NewsSource
            {
                Id = "kip_polskidrob",
                Name = "Krajowa Izba Producentów Drobiu",
                Url = "https://www.kipd.pl/feed/",
                AlternateUrls = new[] { "https://kipd.pl/rss", "https://polskidrob.pl/feed/" },
                Type = SourceType.Rss,
                Category = "Drób",
                Language = "pl",
                Priority = 1,
                Keywords = new[] { "drób", "produkcja", "eksport", "statystyki" },
                Description = "Oficjalna izba producentów drobiu"
            },

            // === PORTALE MIĘSNE ===
            new NewsSource
            {
                Id = "portalmieso",
                Name = "PortalMięso.pl",
                Url = "https://www.portalmieso.pl/feed/",
                AlternateUrls = new[] { "https://portalmieso.pl/rss" },
                Type = SourceType.Rss,
                Category = "Mięso",
                Language = "pl",
                Priority = 2,
                Keywords = new[] { "mięso", "drób", "wołowina", "wieprzowina", "ceny" },
                Description = "Portal branży mięsnej"
            },
            new NewsSource
            {
                Id = "mieso_pl",
                Name = "Mieso.pl",
                Url = "https://mieso.pl/feed/",
                AlternateUrls = new[] { "https://www.mieso.pl/rss" },
                Type = SourceType.Rss,
                Category = "Mięso",
                Language = "pl",
                Priority = 2,
                Keywords = new[] { "mięso", "przetwórstwo" },
                Description = "Portal mięsny"
            },

            // === PORTALE EKONOMICZNE ===
            new NewsSource
            {
                Id = "money_pl",
                Name = "Money.pl",
                Url = "https://www.money.pl/rss/",
                AlternateUrls = new[] { "https://money.pl/feed" },
                Type = SourceType.Rss,
                Category = "Ekonomia",
                Language = "pl",
                Priority = 2,
                Keywords = new[] { "giełda", "waluty", "rolnictwo", "eksport" },
                Description = "Portal ekonomiczny"
            },
            new NewsSource
            {
                Id = "bankier",
                Name = "Bankier.pl",
                Url = "https://www.bankier.pl/rss/wiadomosci.xml",
                AlternateUrls = new[] { "https://bankier.pl/feed" },
                Type = SourceType.Rss,
                Category = "Finanse",
                Language = "pl",
                Priority = 2,
                Keywords = new[] { "giełda", "waluty", "EUR", "USD" },
                Description = "Portal finansowy"
            },
            new NewsSource
            {
                Id = "pb_pl",
                Name = "Puls Biznesu",
                Url = "https://www.pb.pl/rss/all.xml",
                AlternateUrls = new[] { "https://pb.pl/feed" },
                Type = SourceType.Rss,
                Category = "Biznes",
                Language = "pl",
                Priority = 2,
                Keywords = new[] { "biznes", "firmy", "eksport" },
                Description = "Dziennik biznesowy"
            },

            // === ŹRÓDŁA RZĄDOWE I INSTYTUCJONALNE ===
            new NewsSource
            {
                Id = "arimr",
                Name = "ARiMR",
                Url = "https://www.arimr.gov.pl/rss.xml",
                AlternateUrls = new[] { "https://arimr.gov.pl/feed" },
                Type = SourceType.Rss,
                Category = "Instytucje",
                Language = "pl",
                Priority = 1,
                Keywords = new[] { "dotacje", "dopłaty", "programy", "rolnictwo" },
                Description = "Agencja Restrukturyzacji i Modernizacji Rolnictwa"
            },
            new NewsSource
            {
                Id = "gov_rolnictwo",
                Name = "Ministerstwo Rolnictwa",
                Url = "https://www.gov.pl/web/rolnictwo/rss",
                AlternateUrls = new[] { "https://gov.pl/web/rolnictwo/feed" },
                Type = SourceType.Rss,
                Category = "Instytucje",
                Language = "pl",
                Priority = 1,
                Keywords = new[] { "regulacje", "prawo", "drób", "HPAI" },
                Description = "MRiRW"
            },
        };

        #endregion

        #region Polish Sources Requiring Scraping

        /// <summary>
        /// Polskie źródła bez RSS - wymagają scrapingu
        /// </summary>
        public static readonly List<NewsSource> PolishScrapingSources = new()
        {
            new NewsSource
            {
                Id = "glw_hpai",
                Name = "Główny Lekarz Weterynarii - HPAI",
                Url = "https://www.wetgiw.gov.pl/nadzor-weterynaryjny/grypa-ptakow",
                AlternateUrls = new[] { "https://wetgiw.gov.pl/hpai" },
                Type = SourceType.WebScraping,
                Category = "HPAI",
                Language = "pl",
                Priority = 1,
                Keywords = new[] { "HPAI", "ptasia grypa", "ogniska", "strefy" },
                Description = "Oficjalne dane o ogniskach HPAI w Polsce",
                ScrapingConfig = new ScrapingConfig
                {
                    ContentSelector = ".content, .article-content, main",
                    TitleSelector = "h1, .title",
                    DateSelector = ".date, time",
                    RequiresJavaScript = false
                }
            },
            new NewsSource
            {
                Id = "gielda_drobiowa",
                Name = "Giełda Drobiowa Szczecin",
                Url = "https://www.gieldadrobiowasc.pl/",
                Type = SourceType.WebScraping,
                Category = "Ceny",
                Language = "pl",
                Priority = 1,
                Keywords = new[] { "ceny", "notowania", "kurczak", "indyk" },
                Description = "Notowania giełdowe drobiu",
                ScrapingConfig = new ScrapingConfig
                {
                    ContentSelector = ".ceny, .notowania, table",
                    RequiresJavaScript = false
                }
            },
            new NewsSource
            {
                Id = "kowr_ceny",
                Name = "KOWR - Ceny rolnicze",
                Url = "https://www.kowr.gov.pl/analiza-rynkow",
                Type = SourceType.WebScraping,
                Category = "Ceny",
                Language = "pl",
                Priority = 2,
                Keywords = new[] { "ceny", "rynek", "analiza" },
                Description = "Krajowy Ośrodek Wsparcia Rolnictwa"
            },
            new NewsSource
            {
                Id = "piorin",
                Name = "PIORiN",
                Url = "https://piorin.gov.pl/aktualnosci/",
                Type = SourceType.WebScraping,
                Category = "Regulacje",
                Language = "pl",
                Priority = 3,
                Keywords = new[] { "fitosanitarne", "import", "eksport" },
                Description = "Państwowa Inspekcja Ochrony Roślin"
            },
            new NewsSource
            {
                Id = "izby_rolnicze",
                Name = "Krajowa Rada Izb Rolniczych",
                Url = "https://krir.pl/aktualnosci/",
                Type = SourceType.WebScraping,
                Category = "Instytucje",
                Language = "pl",
                Priority = 2,
                Keywords = new[] { "izby rolnicze", "rolnictwo", "prawo" },
                Description = "KRIR"
            },
            new NewsSource
            {
                Id = "wir_lodzkie",
                Name = "WIR Łódź",
                Url = "https://wir-lodz.pl/aktualnosci/",
                Type = SourceType.WebScraping,
                Category = "Regionalne",
                Language = "pl",
                Priority = 2,
                Keywords = new[] { "łódzkie", "rolnictwo", "drób" },
                Description = "Wielkopolska Izba Rolnicza oddział łódzki"
            }
        };

        #endregion

        #region International Poultry Sources

        /// <summary>
        /// Międzynarodowe portale drobiarskie
        /// </summary>
        public static readonly List<NewsSource> InternationalPoultrySources = new()
        {
            // === GŁÓWNE PORTALE ŚWIATOWE ===
            new NewsSource
            {
                Id = "wattagnet",
                Name = "WATTAgNet (WATTPoultry)",
                Url = "https://www.wattagnet.com/rss/all",
                AlternateUrls = new[] { "https://wattagnet.com/feed", "https://www.wattpoultry.com/rss" },
                Type = SourceType.Rss,
                Category = "Drób",
                Language = "en",
                Priority = 1,
                Keywords = new[] { "poultry", "chicken", "HPAI", "avian influenza", "prices", "export" },
                Description = "Globalny portal drobiarski"
            },
            new NewsSource
            {
                Id = "poultryworld",
                Name = "Poultry World",
                Url = "https://www.poultryworld.net/rss/",
                AlternateUrls = new[] { "https://poultryworld.net/feed" },
                Type = SourceType.Rss,
                Category = "Drób",
                Language = "en",
                Priority = 1,
                Keywords = new[] { "poultry", "Europe", "market", "prices" },
                Description = "Europejski portal drobiarski"
            },
            new NewsSource
            {
                Id = "thepoultrysite",
                Name = "The Poultry Site",
                Url = "https://www.thepoultrysite.com/rss/all",
                AlternateUrls = new[] { "https://thepoultrysite.com/feed" },
                Type = SourceType.Rss,
                Category = "Drób",
                Language = "en",
                Priority = 1,
                Keywords = new[] { "poultry", "HPAI", "disease", "health" },
                Description = "Portal zdrowia drobiu"
            },
            new NewsSource
            {
                Id = "meatpoultry",
                Name = "Meat+Poultry",
                Url = "https://www.meatpoultry.com/rss",
                AlternateUrls = new[] { "https://meatpoultry.com/feed" },
                Type = SourceType.Rss,
                Category = "Mięso",
                Language = "en",
                Priority = 2,
                Keywords = new[] { "meat", "poultry", "USA", "processing" },
                Description = "Portal mięsny USA"
            },
            new NewsSource
            {
                Id = "poultry_health_today",
                Name = "Poultry Health Today",
                Url = "https://poultryhealthtoday.com/feed/",
                AlternateUrls = new[] { "https://www.poultryhealthtoday.com/rss" },
                Type = SourceType.Rss,
                Category = "Drób",
                Language = "en",
                Priority = 2,
                Keywords = new[] { "poultry health", "disease", "HPAI", "vaccination" },
                Description = "Zdrowie drobiu"
            },
            new NewsSource
            {
                Id = "globalpoultrytrends",
                Name = "Poultry Trends",
                Url = "https://www.poultrytrends.com/feed/",
                Type = SourceType.Rss,
                Category = "Drób",
                Language = "en",
                Priority = 3,
                Keywords = new[] { "trends", "market", "forecast" },
                Description = "Trendy w branży drobiarskiej"
            },

            // === EUROPEJSKIE ===
            new NewsSource
            {
                Id = "avec_eu",
                Name = "AVEC (EU Poultry)",
                Url = "https://avec-poultry.eu/feed/",
                AlternateUrls = new[] { "https://www.avec-poultry.eu/rss" },
                Type = SourceType.Rss,
                Category = "Drób",
                Language = "en",
                Priority = 1,
                Keywords = new[] { "EU", "poultry", "trade", "regulation" },
                Description = "Europejskie stowarzyszenie przetwórców drobiu"
            },
            new NewsSource
            {
                Id = "efsa_hpai",
                Name = "EFSA - Avian Influenza",
                Url = "https://www.efsa.europa.eu/en/topics/topic/avian-influenza/rss",
                Type = SourceType.Rss,
                Category = "HPAI",
                Language = "en",
                Priority = 1,
                Keywords = new[] { "HPAI", "avian influenza", "Europe", "outbreak" },
                Description = "EFSA dane o HPAI w Europie"
            },
            new NewsSource
            {
                Id = "euromeat_news",
                Name = "EuroMeatNews",
                Url = "https://euromeatnews.com/feed/",
                Type = SourceType.Rss,
                Category = "Mięso",
                Language = "en",
                Priority = 2,
                Keywords = new[] { "meat", "Europe", "trade", "prices" },
                Description = "Europejski rynek mięsa"
            },

            // === EKSPORTOWE / HANDLOWE ===
            new NewsSource
            {
                Id = "reuters_agriculture",
                Name = "Reuters Agriculture",
                Url = "https://www.reuters.com/news/archive/agricultureSector?view=feed",
                Type = SourceType.Rss,
                Category = "Surowce",
                Language = "en",
                Priority = 1,
                Keywords = new[] { "agriculture", "commodities", "grain", "trade" },
                Description = "Rynki surowców rolnych"
            },
            new NewsSource
            {
                Id = "agrimoney",
                Name = "Agrimoney",
                Url = "https://www.agrimoney.com/rss",
                Type = SourceType.Rss,
                Category = "Surowce",
                Language = "en",
                Priority = 2,
                Keywords = new[] { "commodities", "grain", "soy", "corn" },
                Description = "Ceny surowców"
            },
            new NewsSource
            {
                Id = "world_grain",
                Name = "World Grain",
                Url = "https://www.world-grain.com/rss",
                Type = SourceType.Rss,
                Category = "Pasze",
                Language = "en",
                Priority = 2,
                Keywords = new[] { "grain", "wheat", "corn", "feed" },
                Description = "Światowy rynek zbóż"
            },
            new NewsSource
            {
                Id = "feed_navigator",
                Name = "Feed Navigator",
                Url = "https://www.feednavigator.com/rss/",
                Type = SourceType.Rss,
                Category = "Pasze",
                Language = "en",
                Priority = 2,
                Keywords = new[] { "feed", "nutrition", "ingredients" },
                Description = "Portal paszowy"
            },

            // === BRAZYLIA / MERCOSUR ===
            new NewsSource
            {
                Id = "abpa_brazil",
                Name = "ABPA Brazil",
                Url = "https://abpa-br.org/feed/",
                Type = SourceType.Rss,
                Category = "Drób",
                Language = "pt",
                Priority = 1,
                Keywords = new[] { "Brazil", "poultry", "export", "Mercosur" },
                Description = "Brazylijski eksport drobiu"
            },
            new NewsSource
            {
                Id = "avisite_brazil",
                Name = "AviSite (Brazil)",
                Url = "https://www.avisite.com.br/rss/",
                Type = SourceType.Rss,
                Category = "Drób",
                Language = "pt",
                Priority = 2,
                Keywords = new[] { "Brazil", "avicultura", "export" },
                Description = "Brazylijskie wiadomości drobiarskie"
            },

            // === UKRAINA ===
            new NewsSource
            {
                Id = "apk_inform_ua",
                Name = "APK-Inform Ukraine",
                Url = "https://www.apk-inform.com/en/feed/",
                Type = SourceType.Rss,
                Category = "Rolnictwo",
                Language = "en",
                Priority = 1,
                Keywords = new[] { "Ukraine", "grain", "poultry", "export" },
                Description = "Ukraiński portal rolniczy"
            },
            new NewsSource
            {
                Id = "latifundist_ua",
                Name = "Latifundist (Ukraine)",
                Url = "https://latifundist.com/en/feed",
                Type = SourceType.Rss,
                Category = "Rolnictwo",
                Language = "en",
                Priority = 2,
                Keywords = new[] { "Ukraine", "agriculture", "export" },
                Description = "Ukraińskie rolnictwo"
            },

            // === FRANCJA / NIEMCY ===
            new NewsSource
            {
                Id = "reussir_volailles",
                Name = "Réussir Volailles (France)",
                Url = "https://www.reussir.fr/volailles/rss",
                Type = SourceType.Rss,
                Category = "Drób",
                Language = "fr",
                Priority = 2,
                Keywords = new[] { "France", "volaille", "aviculture" },
                Description = "Francuski portal drobiarski"
            },
            new NewsSource
            {
                Id = "gefluegelnews_de",
                Name = "Geflügel News (Germany)",
                Url = "https://www.gefluegelnews.de/feed/",
                Type = SourceType.Rss,
                Category = "Drób",
                Language = "de",
                Priority = 2,
                Keywords = new[] { "Germany", "Geflügel", "Preise" },
                Description = "Niemiecki portal drobiarski"
            },
        };

        #endregion

        #region API Data Sources

        /// <summary>
        /// Źródła danych przez API (ceny, kursy, pogoda)
        /// </summary>
        public static readonly List<NewsSource> ApiSources = new()
        {
            new NewsSource
            {
                Id = "nbp_rates",
                Name = "NBP - Kursy walut",
                Url = "https://api.nbp.pl/api/exchangerates/rates/a/{currency}/",
                Type = SourceType.Api,
                Category = "Finanse",
                Language = "pl",
                Priority = 1,
                Description = "Oficjalne kursy NBP",
                ApiConfig = new ApiConfig
                {
                    Method = "GET",
                    ResponseFormat = "json",
                    RateLimitPerMinute = 50,
                    RequiresAuth = false
                }
            },
            new NewsSource
            {
                Id = "nbp_tables",
                Name = "NBP - Tabele kursów",
                Url = "https://api.nbp.pl/api/exchangerates/tables/A/",
                Type = SourceType.Api,
                Category = "Finanse",
                Language = "pl",
                Priority = 1,
                Description = "Tabele kursów A (średnie)"
            },
            new NewsSource
            {
                Id = "imgw_warnings",
                Name = "IMGW - Ostrzeżenia",
                Url = "https://danepubliczne.imgw.pl/api/data/warningi/",
                Type = SourceType.Api,
                Category = "Pogoda",
                Language = "pl",
                Priority = 1,
                Description = "Ostrzeżenia meteorologiczne",
                Keywords = new[] { "mróz", "upał", "burze" }
            },
            new NewsSource
            {
                Id = "imgw_synop",
                Name = "IMGW - Dane synoptyczne",
                Url = "https://danepubliczne.imgw.pl/api/data/synop/",
                Type = SourceType.Api,
                Category = "Pogoda",
                Language = "pl",
                Priority = 2,
                Description = "Dane pogodowe stacji"
            },
            new NewsSource
            {
                Id = "investing_corn",
                Name = "Corn Futures (MATIF)",
                Url = "https://www.investing.com/commodities/corn",
                Type = SourceType.WebScraping,
                Category = "Pasze",
                Language = "en",
                Priority = 1,
                Description = "Ceny futures kukurydzy",
                Keywords = new[] { "corn", "MATIF", "futures" }
            },
            new NewsSource
            {
                Id = "investing_wheat",
                Name = "Wheat Futures (MATIF)",
                Url = "https://www.investing.com/commodities/us-wheat",
                Type = SourceType.WebScraping,
                Category = "Pasze",
                Language = "en",
                Priority = 1,
                Description = "Ceny futures pszenicy"
            },
            new NewsSource
            {
                Id = "barchart_soy",
                Name = "Soybean Prices",
                Url = "https://www.barchart.com/futures/quotes/ZS*0/overview",
                Type = SourceType.WebScraping,
                Category = "Pasze",
                Language = "en",
                Priority = 2,
                Description = "Ceny soi"
            }
        };

        #endregion

        #region Helper Methods

        /// <summary>
        /// Pobierz wszystkie źródła RSS
        /// </summary>
        public static List<NewsSource> GetAllRssSources()
        {
            var sources = new List<NewsSource>();
            sources.AddRange(PolishAgricultureRss);
            sources.AddRange(InternationalPoultrySources.FindAll(s => s.Type == SourceType.Rss));
            return sources;
        }

        /// <summary>
        /// Pobierz źródła według kategorii
        /// </summary>
        public static List<NewsSource> GetSourcesByCategory(string category)
        {
            var all = GetAllSources();
            return all.FindAll(s => s.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Pobierz wszystkie źródła
        /// </summary>
        public static List<NewsSource> GetAllSources()
        {
            var sources = new List<NewsSource>();
            sources.AddRange(PolishAgricultureRss);
            sources.AddRange(PolishScrapingSources);
            sources.AddRange(InternationalPoultrySources);
            sources.AddRange(ApiSources);
            return sources;
        }

        /// <summary>
        /// Pobierz słowa kluczowe do filtrowania (język polski)
        /// </summary>
        public static readonly string[] PolishKeywords = new[]
        {
            // Drób
            "drób", "drobiowy", "drobiu", "kurczak", "kurczaka", "brojler", "brojlera",
            "indyk", "indyka", "kaczka", "gęś", "nioska", "jaja", "jaj",

            // Produkty
            "filet", "fileta", "tuszka", "tuszki", "udko", "skrzydło", "podudzie",
            "żołądek", "wątroba", "serce", "korpus", "elementy", "mięso drobiowe",

            // Choroby
            "HPAI", "ptasia grypa", "grypa ptaków", "influenza", "ognisko", "ogniska",
            "strefa", "strefy", "zakażenie", "wirus", "salmonella",

            // Ceny i rynek
            "cena", "ceny", "skup", "skupu", "notowania", "giełda", "rynek",
            "hurt", "hurtowy", "detal", "detaliczny", "promocja", "obniżka",

            // Firmy
            "ubojnia", "ubojni", "zakład", "przetwórnia", "hodowla", "ferma",
            "Cedrob", "SuperDrob", "Drosed", "Animex", "Indykpol", "Drobimex",

            // Handel
            "eksport", "eksportu", "import", "importu", "handel", "sprzedaż",
            "Biedronka", "Lidl", "Kaufland", "Auchan", "Carrefour", "Dino",

            // Pasze
            "pasza", "paszy", "kukurydza", "pszenica", "soja", "śruta",
            "MATIF", "notowania zbóż",

            // Instytucje
            "ARiMR", "MRiRW", "GLW", "weterynaryjny", "inspekcja", "kontrola",
            "dotacja", "dopłata", "dofinansowanie", "program",

            // Produkcja
            "produkcja", "ubój", "przetwórstwo", "wydajność", "zdolność",

            // Geografia
            "Polska", "polski", "polskie", "UE", "Unia", "europejski",
            "Brazylia", "Ukraina", "Niemcy", "Francja"
        };

        /// <summary>
        /// Słowa kluczowe po angielsku
        /// </summary>
        public static readonly string[] EnglishKeywords = new[]
        {
            "poultry", "chicken", "broiler", "turkey", "duck", "goose", "egg",
            "HPAI", "avian influenza", "bird flu", "outbreak", "culling",
            "price", "prices", "export", "import", "trade", "market",
            "Brazil", "Ukraine", "Poland", "EU", "European", "Mercosur",
            "feed", "corn", "wheat", "soybean", "MATIF",
            "slaughter", "processing", "production"
        };

        #endregion
    }

    #region Supporting Classes

    public class NewsSource
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public string[] AlternateUrls { get; set; } = Array.Empty<string>();
        public SourceType Type { get; set; }
        public string Category { get; set; }
        public string Language { get; set; } = "pl";
        public int Priority { get; set; } = 5; // 1 = highest
        public string[] Keywords { get; set; } = Array.Empty<string>();
        public string Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? LastFetchTime { get; set; }
        public int ConsecutiveFailures { get; set; }
        public ScrapingConfig ScrapingConfig { get; set; }
        public ApiConfig ApiConfig { get; set; }
    }

    public class ScrapingConfig
    {
        public string ContentSelector { get; set; }
        public string TitleSelector { get; set; }
        public string DateSelector { get; set; }
        public string LinkSelector { get; set; }
        public bool RequiresJavaScript { get; set; }
        public string UserAgent { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new();
    }

    public class ApiConfig
    {
        public string Method { get; set; } = "GET";
        public string ResponseFormat { get; set; } = "json";
        public int RateLimitPerMinute { get; set; } = 60;
        public bool RequiresAuth { get; set; }
        public string AuthType { get; set; } // Bearer, ApiKey, Basic
        public string ApiKeyHeader { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new();
    }

    public enum SourceType
    {
        Rss,
        Api,
        WebScraping
    }

    #endregion
}
