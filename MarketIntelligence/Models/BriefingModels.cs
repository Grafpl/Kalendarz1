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
        public string EducationalSection { get; set; }
        public string AiAnalysisCeo { get; set; }
        public string AiAnalysisSales { get; set; }
        public string AiAnalysisBuyer { get; set; }
        public string RecommendedActionsCeo { get; set; }
        public string RecommendedActionsSales { get; set; }
        public string RecommendedActionsBuyer { get; set; }
        public string Category { get; set; }
        public string Source { get; set; }
        public string SourceUrl { get; set; }
        public DateTime PublishDate { get; set; }
        public SeverityLevel Severity { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public bool IsFeatured { get; set; }

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

        public string PriceDisplay => $"{Price:N2} {Unit}";
        public string ChangeDisplay => ChangePercent >= 0 ? $"+{ChangePercent:N1}%" : $"{ChangePercent:N1}%";
        public bool IsPositive => ChangePercent >= 0;
    }

    #endregion
}
