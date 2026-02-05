using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kalendarz1.MarketIntelligence.Models
{
    #region Enums

    public enum SeverityLevel
    {
        Critical,
        Warning,
        Positive,
        Info
    }

    public enum PriceDirection
    {
        Up,
        Down,
        Stable
    }

    public enum UserRole
    {
        CEO,
        Sales,
        Buyer
    }

    public enum FarmerCategory
    {
        A,
        B,
        C
    }

    /// <summary>
    /// Poziom wpływu wiadomości na biznes
    /// </summary>
    public enum ImpactLevel
    {
        Low,        // Informacyjny, mały wpływ
        Medium,     // Umiarkowany wpływ, warto obserwować
        High,       // Wysoki wpływ, wymaga uwagi
        Critical    // Krytyczny - natychmiastowa reakcja
    }

    #endregion

    #region Base Class

    public abstract class NotifyBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    #endregion

    #region Article Model

    public class BriefingArticle : NotifyBase
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string ShortPreview { get; set; }
        public string FullContent { get; set; }

        // NOWE: Kontekst rynkowy
        public string MarketContext { get; set; }

        // Sekcja edukacyjna - kim jest / co to jest
        public string EducationalSection { get; set; }

        // NOWE: Tlumaczenie pojec branzowych
        public string TermsExplanation { get; set; }

        // Analizy dla roznych rol
        public string AiAnalysisCeo { get; set; }
        public string AiAnalysisSales { get; set; }
        public string AiAnalysisBuyer { get; set; }

        // Akcje dla roznych rol
        public string RecommendedActionsCeo { get; set; }
        public string RecommendedActionsSales { get; set; }
        public string RecommendedActionsBuyer { get; set; }

        // NOWE: Lekcja branzowa - edukacja
        public string IndustryLesson { get; set; }

        // NOWE: Pytania strategiczne
        public string StrategicQuestions { get; set; }

        // NOWE: Zrodla do monitorowania
        public string SourcesToMonitor { get; set; }

        // Metadane
        public string Category { get; set; }
        public string Source { get; set; }
        public string SourceUrl { get; set; }
        public DateTime PublishDate { get; set; }
        public SeverityLevel Severity { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public bool IsFeatured { get; set; }

        // NOWE: Executive Dashboard fields
        /// <summary>
        /// Krótki, biznesowy nagłówek generowany przez AI (max 80 znaków)
        /// </summary>
        public string SmartTitle { get; set; }

        /// <summary>
        /// Wynik sentymentu: -1 (bardzo negatywny dla ubojni) do +1 (bardzo pozytywny)
        /// </summary>
        public double SentimentScore { get; set; }

        /// <summary>
        /// Poziom wpływu na biznes
        /// </summary>
        public ImpactLevel Impact { get; set; } = ImpactLevel.Medium;

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        // Computed properties
        public string FormattedDate => PublishDate.ToString("dd.MM.yyyy");
        public string SeverityText => Severity switch
        {
            SeverityLevel.Critical => "KRYTYCZNE",
            SeverityLevel.Warning => "OSTRZEZENIE",
            SeverityLevel.Positive => "POZYTYWNE",
            SeverityLevel.Info => "INFO",
            _ => "INFO"
        };

        /// <summary>
        /// Kolor paska bocznego na podstawie ImpactLevel
        /// </summary>
        public string ImpactColor => Impact switch
        {
            ImpactLevel.Critical => "#E53935",   // Czerwony
            ImpactLevel.High => "#FB8C00",       // Pomarańczowy
            ImpactLevel.Medium => "#FDD835",     // Żółty
            ImpactLevel.Low => "#43A047",        // Zielony
            _ => "#78909C"                       // Szary
        };

        /// <summary>
        /// Tekst wpływu po polsku
        /// </summary>
        public string ImpactText => Impact switch
        {
            ImpactLevel.Critical => "KRYTYCZNY",
            ImpactLevel.High => "WYSOKI",
            ImpactLevel.Medium => "ŚREDNI",
            ImpactLevel.Low => "NISKI",
            _ => "NIEZNANY"
        };

        /// <summary>
        /// Ikona sentymentu (Unicode)
        /// </summary>
        public string SentimentIcon => SentimentScore switch
        {
            >= 0.5 => "▲",    // Bardzo pozytywny
            >= 0.1 => "↗",    // Pozytywny
            <= -0.5 => "▼",   // Bardzo negatywny
            <= -0.1 => "↘",   // Negatywny
            _ => "━"          // Neutralny
        };

        /// <summary>
        /// Kolor sentymentu
        /// </summary>
        public string SentimentColor => SentimentScore switch
        {
            >= 0.3 => "#4CAF50",   // Zielony
            >= 0 => "#8BC34A",     // Jasnozielony
            <= -0.3 => "#F44336", // Czerwony
            _ => "#FF9800"        // Pomarańczowy
        };

        /// <summary>
        /// Wyświetlany tytuł - SmartTitle lub skrócony Title
        /// </summary>
        public string DisplayTitle => !string.IsNullOrEmpty(SmartTitle)
            ? SmartTitle
            : (Title?.Length > 80 ? Title.Substring(0, 77) + "..." : Title);

        /// <summary>
        /// Kolor kategorii dla badge
        /// </summary>
        public string CategoryColor => Category?.ToUpperInvariant() switch
        {
            "HPAI" => "#D32F2F",
            "CENY" => "#1976D2",
            "KONKURENCJA" => "#7B1FA2",
            "REGULACJE" => "#455A64",
            "KLIENCI" => "#00796B",
            "EKSPORT" => "#0288D1",
            "IMPORT" => "#F57C00",
            "KOSZTY" => "#C2185B",
            _ => "#616161"
        };

        public string GetAiAnalysis(UserRole role) => role switch
        {
            UserRole.CEO => AiAnalysisCeo,
            UserRole.Sales => AiAnalysisSales,
            UserRole.Buyer => AiAnalysisBuyer,
            _ => AiAnalysisCeo
        };

        public string GetRecommendedActions(UserRole role) => role switch
        {
            UserRole.CEO => RecommendedActionsCeo,
            UserRole.Sales => RecommendedActionsSales,
            UserRole.Buyer => RecommendedActionsBuyer,
            _ => RecommendedActionsCeo
        };
    }

    #endregion

    #region Price Indicator

    public class PriceIndicator : NotifyBase
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Unit { get; set; }
        public string Change { get; set; }
        public PriceDirection Direction { get; set; }
        public string SubLabel { get; set; }
        public double[] SparkData { get; set; }

        public string DirectionSymbol => Direction switch
        {
            PriceDirection.Up => "▲",
            PriceDirection.Down => "▼",
            PriceDirection.Stable => "━",
            _ => ""
        };

        public string DisplayValue => $"{Value} {Unit}";
    }

    #endregion

    #region Competitor Model

    public class BriefingCompetitor : NotifyBase
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Owner { get; set; }
        public string CountryFlag { get; set; }
        public string CountryOrigin { get; set; }
        public string Headquarters { get; set; }
        public string Revenue { get; set; }
        public string Capacity { get; set; }
        public int ThreatLevel { get; set; }
        public string LatestNews { get; set; }
        public string Description { get; set; }
        public int Tier { get; set; }

        public string ThreatDisplay => $"{ThreatLevel}%";
        public double ThreatWidth => ThreatLevel * 2.5; // For progress bar (max 250px)
    }

    #endregion

    #region Retail Price Model

    public class RetailPrice : NotifyBase
    {
        public string ChainName { get; set; }
        public string ChainLogo { get; set; }
        public decimal FiletPrice { get; set; }
        public decimal TuszkaPrice { get; set; }
        public decimal UdkoPrice { get; set; }
        public string Source { get; set; }
        public string SourceUrl { get; set; }
        public DateTime CheckDate { get; set; }
        public string Notes { get; set; }

        public string FiletDisplay => $"{FiletPrice:N2} zl";
        public string TuszkaDisplay => $"{TuszkaPrice:N2} zl";
        public string UdkoDisplay => $"{UdkoPrice:N2} zl";
        public string CheckDateDisplay => CheckDate.ToString("dd.MM.yyyy");
    }

    #endregion

    #region Farmer Model

    public class BriefingFarmer : NotifyBase
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int DistanceKm { get; set; }
        public int Barns { get; set; }
        public FarmerCategory Category { get; set; }
        public string HpaiStatus { get; set; }
        public bool IsAtRisk { get; set; }
        public string Notes { get; set; }
        public string Phone { get; set; }

        public string DistanceDisplay => $"{DistanceKm} km";
        public string BarnsDisplay => $"{Barns} kurn.";
        public string CategoryDisplay => Category.ToString();
    }

    #endregion

    #region Calendar Event

    public class CalendarEvent : NotifyBase
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public DateTime EventDate { get; set; }
        public SeverityLevel Severity { get; set; }
        public string Description { get; set; }

        public int DaysUntil => (EventDate.Date - DateTime.Today).Days;
        public string DaysUntilDisplay => DaysUntil switch
        {
            0 => "DZIS",
            1 => "JUTRO",
            < 0 => $"{Math.Abs(DaysUntil)} dni temu",
            _ => $"za {DaysUntil} dni"
        };
        public string DateDisplay => EventDate.ToString("dd.MM");
    }

    #endregion

    #region Client Model

    public class BriefingClient : NotifyBase
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int PaletsPerMonth { get; set; }
        public string Salesperson { get; set; }
        public int ChangeAmount { get; set; }
        public string ClientType { get; set; }

        public string PaletsDisplay => $"{PaletsPerMonth} E2/mies.";
        public string ChangeDisplay => ChangeAmount >= 0 ? $"+{ChangeAmount} ▲" : $"{ChangeAmount} ▼";
        public bool IsPositiveChange => ChangeAmount >= 0;
    }

    #endregion

    #region EU Benchmark

    public class EuBenchmarkPrice : NotifyBase
    {
        public string Country { get; set; }
        public string CountryFlag { get; set; }
        public decimal PricePer100kg { get; set; }
        public decimal ChangePercent { get; set; }
        public bool IsPoland { get; set; }
        public bool IsImporter { get; set; }

        public string PriceDisplay => $"{PricePer100kg:N1} EUR";
        public string ChangeDisplay => ChangePercent >= 0 ? $"+{ChangePercent:N1}%" : $"{ChangePercent:N1}%";
        public double BarWidth => (double)PricePer100kg; // For horizontal bar
    }

    #endregion

    #region Task/Action Item

    public class BriefingTask : NotifyBase
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string AssignedTo { get; set; }
        public DateTime Deadline { get; set; }
        public SeverityLevel Severity { get; set; }
        public bool IsCompleted { get; set; }
        public string RelatedArticleTitle { get; set; }

        public string DeadlineDisplay => Deadline.ToString("dd.MM HH:mm");
        public int DaysUntil => (Deadline.Date - DateTime.Today).Days;
    }

    #endregion

    #region Summary Segment (for colored inline text)

    public class SummarySegment
    {
        public string Text { get; set; }
        public string Color { get; set; } // Hex color or "default"
        public bool IsBold { get; set; }

        public SummarySegment(string text, string color = "default", bool isBold = false)
        {
            Text = text;
            Color = color;
            IsBold = isBold;
        }
    }

    #endregion

    #region Feed Price (MATIF)

    public class FeedPrice : NotifyBase
    {
        public string Commodity { get; set; }
        public string Contract { get; set; }
        public decimal Price { get; set; }
        public string Unit { get; set; }
        public decimal ChangePercent { get; set; }

        public string PriceDisplay => $"{Price:N2} {Unit}";
        public string ChangeDisplay => ChangePercent >= 0 ? $"+{ChangePercent:N2}%" : $"{ChangePercent:N2}%";
        public bool IsPositive => ChangePercent >= 0;
    }

    #endregion

    #region Element Price

    public class ElementPrice : NotifyBase
    {
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string Unit { get; set; }
        public decimal ChangePercent { get; set; }
        public List<PricePoint> HistoricalPrices { get; set; } = new List<PricePoint>();
        public string SourceUrl { get; set; }
        public string Source { get; set; }

        public string PriceDisplay => $"{Price:N2} {Unit}";
        public string ChangeDisplay => ChangePercent >= 0 ? $"+{ChangePercent:N1}%" : $"{ChangePercent:N1}%";
        public bool IsPositive => ChangePercent >= 0;
    }

    #endregion

    #region Price Point (for charts)

    public class PricePoint
    {
        public DateTime Date { get; set; }
        public decimal Value { get; set; }

        public PricePoint() { }
        public PricePoint(DateTime date, decimal value)
        {
            Date = date;
            Value = value;
        }
    }

    #endregion

    #region Export/Import Data

    public class ExportImportData : NotifyBase
    {
        public string Country { get; set; }
        public string CountryFlag { get; set; }
        public string ProductType { get; set; } // Filet, Tuszka, Udko
        public decimal VolumeThousandTons { get; set; }
        public decimal ChangePercent { get; set; }
        public decimal ValueMillionEur { get; set; }
        public bool IsExport { get; set; } // true = export, false = import
        public int Year { get; set; }
        public string Source { get; set; }
        public string SourceUrl { get; set; }

        public string VolumeDisplay => $"{VolumeThousandTons:N1} tys. ton";
        public string ValueDisplay => $"{ValueMillionEur:N1} mln EUR";
        public string ChangeDisplay => ChangePercent >= 0 ? $"+{ChangePercent:N1}%" : $"{ChangePercent:N1}%";
        public bool IsPositive => ChangePercent >= 0;
        public string DirectionLabel => IsExport ? "EKSPORT" : "IMPORT";
    }

    #endregion

    #region Subsidy/Grant

    public class SubsidyGrant : NotifyBase
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Provider { get; set; } // ARiMR, NFOSiGW, PARP, EU
        public string Description { get; set; }
        public decimal MaxAmountPln { get; set; }
        public decimal CoFinancingPercent { get; set; }
        public DateTime DeadlineDate { get; set; }
        public string EligibleFor { get; set; } // Lista kryteriow
        public string RequiredDocuments { get; set; }
        public string ApplicationUrl { get; set; }
        public SeverityLevel Priority { get; set; }
        public bool IsActive { get; set; }
        public List<string> Tags { get; set; } = new List<string>();

        public string MaxAmountDisplay => MaxAmountPln >= 1000000
            ? $"{MaxAmountPln / 1000000:N1} mln PLN"
            : $"{MaxAmountPln / 1000:N0} tys. PLN";
        public string CoFinancingDisplay => $"do {CoFinancingPercent:N0}%";
        public int DaysUntilDeadline => (DeadlineDate.Date - DateTime.Today).Days;
        public string DeadlineDisplay => DeadlineDate.ToString("dd.MM.yyyy");
        public string DaysUntilDisplay => DaysUntilDeadline switch
        {
            <= 0 => "ZAMKNIETE",
            <= 7 => $"PILNE! {DaysUntilDeadline} dni",
            <= 30 => $"{DaysUntilDeadline} dni",
            _ => $"{DaysUntilDeadline} dni"
        };
        public string DaysLeftDisplay => DaysUntilDeadline switch
        {
            <= 0 => "(zamkniete)",
            <= 7 => $"({DaysUntilDeadline} dni!)",
            _ => $"({DaysUntilDeadline} dni)"
        };
    }

    #endregion

    #region Potential Client

    public class PotentialClient : NotifyBase
    {
        public int Id { get; set; }
        public string CompanyName { get; set; }
        public string Industry { get; set; } // Siec detaliczna, HoReCa, Przetwórstwo
        public string Description { get; set; }
        public string Location { get; set; }
        public string Region { get; set; }
        public int EstimatedVolumePerMonth { get; set; } // palety E2
        public string ContactPerson { get; set; }
        public string ContactEmail { get; set; }
        public string ContactPhone { get; set; }
        public string Website { get; set; }
        public string LatestNews { get; set; }
        public string NewsSource { get; set; }
        public string NewsSourceUrl { get; set; }
        public DateTime NewsDate { get; set; }
        public int OpportunityScore { get; set; } // 0-100
        public string RecommendedAction { get; set; }
        public string AssignedTo { get; set; }
        public SeverityLevel Priority { get; set; }

        public string VolumeDisplay => $"~{EstimatedVolumePerMonth} palet/mies.";
        public string OpportunityDisplay => $"{OpportunityScore}%";
        public string OpportunityScoreDisplay => $"{OpportunityScore}%";
        public double OpportunityWidth => OpportunityScore * 2.0;
    }

    #endregion

    #region International Market News

    public class InternationalMarketNews : NotifyBase
    {
        public int Id { get; set; }
        public string Country { get; set; }
        public string CountryFlag { get; set; }
        public string Region { get; set; } // EU, Mercosur, Asia
        public string Title { get; set; }
        public string Summary { get; set; }
        public string FullContent { get; set; }
        public string ImpactOnPoland { get; set; }
        public SeverityLevel ThreatLevel { get; set; }
        public string Source { get; set; }
        public string SourceUrl { get; set; }
        public DateTime PublishDate { get; set; }
        public List<string> Tags { get; set; } = new List<string>();

        public string DateDisplay => PublishDate.ToString("dd.MM.yyyy");
        public string ThreatText => ThreatLevel switch
        {
            SeverityLevel.Critical => "WYSOKIE ZAGROZENIE",
            SeverityLevel.Warning => "SREDNIE ZAGROZENIE",
            SeverityLevel.Positive => "SZANSA",
            SeverityLevel.Info => "INFORMACJA",
            _ => "INFO"
        };
        public string ThreatLevelText => ThreatLevel switch
        {
            SeverityLevel.Critical => "WYSOKIE",
            SeverityLevel.Warning => "SREDNIE",
            SeverityLevel.Positive => "SZANSA",
            SeverityLevel.Info => "INFO",
            _ => "INFO"
        };
    }

    #endregion

    #region Extended Competitor Model

    public class ExtendedCompetitor : NotifyBase
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string Owner { get; set; }
        public string OwnerCountry { get; set; }
        public string CountryFlag { get; set; }

        // Location for map
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string City { get; set; }
        public string Voivodeship { get; set; }
        public int DistanceFromUsKm { get; set; }

        // Business data
        public decimal RevenueMillionPln { get; set; }
        public int CapacityPerDay { get; set; } // sztuk/dzien
        public int Employees { get; set; }
        public int NumberOfPlants { get; set; }
        public decimal MarketSharePercent { get; set; }

        // Tier and threat
        public int Tier { get; set; } // 1 = giganci, 2 = regionalni, 3 = lokalni
        public int ThreatLevel { get; set; } // 0-100

        // Products and certifications
        public List<string> MainProducts { get; set; } = new List<string>();
        public List<string> Certifications { get; set; } = new List<string>();
        public List<string> MainClients { get; set; } = new List<string>();

        // History and news
        public string CompanyHistory { get; set; }
        public string LatestNews { get; set; }
        public string NewsSource { get; set; }
        public string NewsSourceUrl { get; set; }
        public DateTime NewsDate { get; set; }

        // SWOT
        public string Strengths { get; set; }
        public string Weaknesses { get; set; }
        public string OpportunitiesForUs { get; set; }
        public string ThreatsFromThem { get; set; }

        // Contact
        public string Website { get; set; }
        public string Address { get; set; }

        // Computed properties
        public string RevenueDisplay => RevenueMillionPln >= 1000
            ? $"{RevenueMillionPln / 1000:N1} mld PLN"
            : $"{RevenueMillionPln:N0} mln PLN";
        public string CapacityDisplay => CapacityPerDay >= 1000000
            ? $"{CapacityPerDay / 1000000.0:N1}M szt./dzien"
            : $"{CapacityPerDay / 1000}k szt./dzien";
        public string MarketShareDisplay => $"{MarketSharePercent:N1}%";
        public string ThreatDisplay => $"{ThreatLevel}%";
        public double ThreatWidth => ThreatLevel * 2.5;
        public string DistanceDisplay => $"{DistanceFromUsKm} km";
        public string TierDisplay => Tier switch
        {
            1 => "GIGANT",
            2 => "REGIONALNY",
            3 => "LOKALNY",
            _ => "INNY"
        };
    }

    #endregion

    #region Chart Data Series

    public class ChartDataSeries : NotifyBase
    {
        public string Name { get; set; }
        public string Color { get; set; }
        public string Unit { get; set; }
        public List<PricePoint> DataPoints { get; set; } = new List<PricePoint>();
        public decimal CurrentValue { get; set; }
        public decimal ChangePercent { get; set; }
        public decimal MinValue { get; set; }
        public decimal MaxValue { get; set; }
        public decimal AvgValue { get; set; }
        public string Source { get; set; }
        public string SourceUrl { get; set; }

        public string CurrentDisplay => $"{CurrentValue:N2} {Unit}";
        public string CurrentValueDisplay => $"{CurrentValue:N2}";
        public string ChangeDisplay => ChangePercent >= 0 ? $"+{ChangePercent:N2}%" : $"{ChangePercent:N2}%";
        public bool IsPositive => ChangePercent >= 0;
        public string RangeDisplay => $"{MinValue:N2} - {MaxValue:N2} {Unit}";
        public string MinValueDisplay => $"Min: {MinValue:N2}";
        public string MaxValueDisplay => $"Max: {MaxValue:N2}";
    }

    #endregion

    #region Retail Chain Extended

    public class RetailChainExtended : NotifyBase
    {
        public int Id { get; set; }
        public string ChainName { get; set; }
        public string ChainLogo { get; set; }
        public string OwnerCompany { get; set; }
        public string OwnerCountry { get; set; }
        public string CountryFlag { get; set; }

        // Prices
        public decimal FiletPrice { get; set; }
        public decimal FiletPricePromo { get; set; }
        public decimal TuszkaPrice { get; set; }
        public decimal TuszkaPricePromo { get; set; }
        public decimal UdkoPrice { get; set; }
        public decimal UdkoPricePromo { get; set; }
        public decimal SkrzydloPrice { get; set; }
        public decimal PodrudziePrice { get; set; }

        // Source info
        public string Source { get; set; }
        public string SourceUrl { get; set; }
        public DateTime CheckDate { get; set; }
        public string PromoValidUntil { get; set; }

        // Chain info
        public int StoreCount { get; set; }
        public string Regions { get; set; }
        public bool IsOurClient { get; set; }
        public string OurHandlowiec { get; set; }
        public string Notes { get; set; }

        // Computed
        public string FiletDisplay => $"{FiletPrice:N2} zl";
        public string FiletPromoDisplay => FiletPricePromo > 0 ? $"{FiletPricePromo:N2} zl" : "-";
        public string TuszkaDisplay => $"{TuszkaPrice:N2} zl";
        public string UdkoDisplay => $"{UdkoPrice:N2} zl";
        public string CheckDateDisplay => CheckDate.ToString("dd.MM.yyyy");
        public bool HasPromo => FiletPricePromo > 0 || TuszkaPricePromo > 0 || UdkoPricePromo > 0;
    }

    #endregion
}
