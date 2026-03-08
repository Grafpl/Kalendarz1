using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace Kalendarz1.Partie.Models
{
    // ═══════════════════════════════════════════════════════════════
    // STATUS ENUM (10-state lifecycle)
    // ═══════════════════════════════════════════════════════════════

    public enum PartiaStatusEnum
    {
        PLANNED,           // Zaplanowana z harmonogramu
        IN_TRANSIT,        // Kierowca wyjezdzal po drob
        AT_RAMP,           // Auto na rampie
        VET_CHECK,         // Kontrola weterynaryjna
        APPROVED,          // Zaakceptowana przez weta
        IN_PRODUCTION,     // W produkcji (wazenia trwaja)
        PROD_DONE,         // Produkcja zakonczona
        CLOSED,            // Zamknieta poprawnie (QC OK)
        CLOSED_INCOMPLETE, // Zamknieta z brakami QC
        REJECTED           // Odrzucona
    }

    public static class PartiaStatusHelper
    {
        public static string ToDisplayText(this PartiaStatusEnum status) => status switch
        {
            PartiaStatusEnum.PLANNED => "Zaplanowana",
            PartiaStatusEnum.IN_TRANSIT => "W trasie",
            PartiaStatusEnum.AT_RAMP => "Na rampie",
            PartiaStatusEnum.VET_CHECK => "Kontrola wet.",
            PartiaStatusEnum.APPROVED => "Zaakceptowana",
            PartiaStatusEnum.IN_PRODUCTION => "W produkcji",
            PartiaStatusEnum.PROD_DONE => "Prod. zakonczona",
            PartiaStatusEnum.CLOSED => "Zamknieta",
            PartiaStatusEnum.CLOSED_INCOMPLETE => "Zamkn. (braki)",
            PartiaStatusEnum.REJECTED => "Odrzucona",
            _ => status.ToString()
        };

        public static string ToColorHex(this PartiaStatusEnum status) => status switch
        {
            PartiaStatusEnum.PLANNED => "#95A5A6",
            PartiaStatusEnum.IN_TRANSIT => "#3498DB",
            PartiaStatusEnum.AT_RAMP => "#F39C12",
            PartiaStatusEnum.VET_CHECK => "#E67E22",
            PartiaStatusEnum.APPROVED => "#27AE60",
            PartiaStatusEnum.IN_PRODUCTION => "#2980B9",
            PartiaStatusEnum.PROD_DONE => "#1ABC9C",
            PartiaStatusEnum.CLOSED => "#27AE60",
            PartiaStatusEnum.CLOSED_INCOMPLETE => "#D4AF37",
            PartiaStatusEnum.REJECTED => "#E74C3C",
            _ => "#7F8C8D"
        };

        public static string ToRowBackgroundHex(this PartiaStatusEnum status) => status switch
        {
            PartiaStatusEnum.IN_PRODUCTION => "#E8F4FD",
            PartiaStatusEnum.CLOSED => "#F0F0F0",
            PartiaStatusEnum.CLOSED_INCOMPLETE => "#FFF8E1",
            PartiaStatusEnum.REJECTED => "#FFEBEE",
            PartiaStatusEnum.PLANNED => "#F5F5F5",
            PartiaStatusEnum.AT_RAMP => "#FFF3E0",
            PartiaStatusEnum.APPROVED => "#E8F5E9",
            _ => "#FFFFFF"
        };

        public static PartiaStatusEnum? GetNextStatus(PartiaStatusEnum current) => current switch
        {
            PartiaStatusEnum.PLANNED => PartiaStatusEnum.IN_TRANSIT,
            PartiaStatusEnum.IN_TRANSIT => PartiaStatusEnum.AT_RAMP,
            PartiaStatusEnum.AT_RAMP => PartiaStatusEnum.VET_CHECK,
            PartiaStatusEnum.VET_CHECK => PartiaStatusEnum.APPROVED,
            PartiaStatusEnum.APPROVED => PartiaStatusEnum.IN_PRODUCTION,
            PartiaStatusEnum.IN_PRODUCTION => PartiaStatusEnum.PROD_DONE,
            _ => null
        };

        public static PartiaStatusEnum Parse(string s)
        {
            if (string.IsNullOrEmpty(s)) return PartiaStatusEnum.IN_PRODUCTION;
            return Enum.TryParse<PartiaStatusEnum>(s, true, out var result)
                ? result : PartiaStatusEnum.IN_PRODUCTION;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // MAIN PARTIA MODEL
    // ═══════════════════════════════════════════════════════════════

    public class PartiaModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Identyfikacja
        public string GUID { get; set; }
        public string DirID { get; set; }
        public string Partia { get; set; }
        public string CreateData { get; set; }
        public string CreateGodzina { get; set; }
        public string ArticleID { get; set; }

        // Status (legacy + V2)
        public int IsClose { get; set; }
        public string CloseData { get; set; }
        public string CloseGodzina { get; set; }
        public string CreateOperator { get; set; }
        public string CloseOperator { get; set; }
        public string StatusV2String { get; set; }
        public int? HarmonogramLp { get; set; }

        public PartiaStatusEnum StatusV2 =>
            !string.IsNullOrEmpty(StatusV2String)
                ? PartiaStatusHelper.Parse(StatusV2String)
                : (IsClose == 1 ? PartiaStatusEnum.CLOSED : PartiaStatusEnum.IN_PRODUCTION);

        // Dostawca
        public string CustomerID { get; set; }
        public string CustomerName { get; set; }

        // Operatorzy (nazwy)
        public string OtworzylNazwa { get; set; }
        public string ZamknalNazwa { get; set; }

        // Skup (FarmerCalc)
        public int SztDekl { get; set; }
        public decimal NettoSkup { get; set; }
        public int Padle { get; set; }
        public decimal CenaSkup { get; set; }

        // Vet
        public string VetNo { get; set; }
        public string VetComment { get; set; }

        // Wazenia - sumy
        public decimal WydanoKg { get; set; }
        public int WydanoSzt { get; set; }
        public decimal PrzyjetoKg { get; set; }
        public int PrzyjetoSzt { get; set; }

        // QC
        public decimal? KlasaBProc { get; set; }
        public decimal? PrzekarmienieKg { get; set; }
        public decimal? TempRampa { get; set; }
        public int? SkrzydlaOcena { get; set; }
        public int? NogiOcena { get; set; }
        public int? OparzeniaOcena { get; set; }
        public bool MaTemperatury { get; set; }
        public bool MaWady { get; set; }
        public int IloscZdjec { get; set; }

        // Computed
        public decimal NaStanieKg => WydanoKg - PrzyjetoKg;

        public decimal? WydajnoscProc =>
            NettoSkup > 0 ? Math.Round(WydanoKg / NettoSkup * 100, 1) : (decimal?)null;

        public string StatusText => StatusV2.ToDisplayText();
        public string StatusColor => StatusV2.ToColorHex();

        public string QCBadge
        {
            get
            {
                if (MaTemperatury && MaWady) return "OK";
                if (MaTemperatury || MaWady) return "Czesciowe";
                return "Brak";
            }
        }

        public string ZamkniecieInfo =>
            IsClose == 1 ? $"{CloseData} {CloseGodzina}" : "";

        public bool IsActive => StatusV2 != PartiaStatusEnum.CLOSED
                             && StatusV2 != PartiaStatusEnum.CLOSED_INCOMPLETE
                             && StatusV2 != PartiaStatusEnum.REJECTED;

        // Row coloring (Feature 1)
        public string RowBackgroundColor => StatusV2.ToRowBackgroundHex();

        // Sparkline (Feature 4)
        public PointCollection SparklinePoints { get; set; }
        public bool HasSparkline => SparklinePoints != null && SparklinePoints.Count >= 2;

        // Quick status (Feature 5)
        public string NextStatusText => PartiaStatusHelper.GetNextStatus(StatusV2)?.ToDisplayText();
        public bool CanAdvanceStatus => PartiaStatusHelper.GetNextStatus(StatusV2) != null;
    }

    public class WazenieModel
    {
        public string GUID { get; set; }
        public string ArticleID { get; set; }
        public string ArticleName { get; set; }
        public string JM { get; set; }
        public decimal ActWeight { get; set; }
        public int Quantity { get; set; }
        public decimal Weight { get; set; }
        public decimal Tara { get; set; }
        public string Data { get; set; }
        public string Godzina { get; set; }
        public string Wagowy { get; set; }
        public string Direction { get; set; }
        public string TruckID { get; set; }
        public string P1 { get; set; }
        public string P2 { get; set; }
        public bool IsStorno => ActWeight < 0;
        public string Zrodlo { get; set; } // "Out1A", "In0E" etc
    }

    public class ProduktPartiiModel
    {
        public string ArticleID { get; set; }
        public string ArticleName { get; set; }
        public string JM { get; set; }
        public decimal WydanoDodatnie { get; set; }
        public decimal StornoUjemne { get; set; }
        public decimal NettoKg { get; set; }
        public int SztDodatnie { get; set; }
        public int IleWazen { get; set; }
        public decimal ProcentUdzialu { get; set; }
    }

    public class QCDataModel
    {
        // Temperatury
        public List<TemperaturaModel> Temperatury { get; set; } = new();

        // Wady
        public int? SkrzydlaOcena { get; set; }
        public int? NogiOcena { get; set; }
        public int? OparzeniaOcena { get; set; }

        // Podsumowanie
        public decimal? KlasaBProc { get; set; }
        public decimal? PrzekarmienieKg { get; set; }
        public string Notatka { get; set; }

        // Zdjecia
        public List<ZdjecieModel> Zdjecia { get; set; } = new();
    }

    public class TemperaturaModel
    {
        public string Miejsce { get; set; }
        public decimal? Proba1 { get; set; }
        public decimal? Proba2 { get; set; }
        public decimal? Proba3 { get; set; }
        public decimal? Proba4 { get; set; }
        public decimal? Srednia { get; set; }
        public string Wykonal { get; set; }
    }

    public class ZdjecieModel
    {
        public string SciezkaPliku { get; set; }
        public string Opis { get; set; }
        public string WadaTyp { get; set; }
        public string Wykonal { get; set; }
    }

    public class SkupDataModel
    {
        public int FarmerCalcID { get; set; }
        public DateTime? CalcDate { get; set; }
        public string CustomerName { get; set; }
        public string CustomerID { get; set; }
        public string KierowcaNazwa { get; set; }
        public string CarID { get; set; }
        public string TrailerID { get; set; }
        public decimal BruttoWeight { get; set; }
        public decimal EmptyWeight { get; set; }
        public decimal NettoWeight { get; set; }
        public int DeclI1 { get; set; }
        public int DeclI2 { get; set; }
        public decimal Price { get; set; }
        public DateTime? Wyjazd { get; set; }
        public DateTime? Zaladunek { get; set; }
        public DateTime? Przyjazd { get; set; }
        public int StartKM { get; set; }
        public int StopKM { get; set; }

        public decimal WartoscNetto => NettoWeight * Price;
        public int KmTrasy => StopKM > StartKM ? StopKM - StartKM : 0;
    }

    public class HaccpModel
    {
        public string ZDzialu { get; set; }
        public string Artykul { get; set; }
        public string PartiaZrodlowa { get; set; }
        public string NaDzial { get; set; }
        public string ArtykulDocelowy { get; set; }
        public string PartiaDocelowa { get; set; }
        public decimal SumaKg { get; set; }
        public string MinDate { get; set; }
        public string MaxDate { get; set; }
    }

    public class TimelineEvent
    {
        public string EventTime { get; set; }
        public string EventType { get; set; }
        public string Description { get; set; }

        public string Icon => EventType switch
        {
            "OPEN" => "\U0001F7E2",      // green circle
            "CLOSE" => "\U0001F534",     // red circle
            "WEIGHT" => "\u2696",        // scales
            "TEMP" => "\U0001F321",      // thermometer
            "QC" => "\U0001F50D",        // magnifier
            "PHOTO" => "\U0001F4F7",     // camera
            "TRANSPORT" => "\U0001F69A", // truck
            _ => "\u2022"                // bullet
        };
    }

    public class PartieStatsModel
    {
        public int LiczbaPartii { get; set; }
        public int Otwartych { get; set; }
        public int Zamknietych { get; set; }
        public int DzisPartii { get; set; }
        public decimal DzisKg { get; set; }
        public decimal SrWydajnosc { get; set; }
        public decimal SrKlasaB { get; set; }
        public decimal SrTempRampa { get; set; }
    }

    public class DostawcaComboItem
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string Display => $"{ID} - {Name}";
    }

    // ═══════════════════════════════════════════════════════════════
    // HARMONOGRAM DOSTAW ITEM (for NowaPartiaDialog integration)
    // ═══════════════════════════════════════════════════════════════

    public class HarmonogramItem
    {
        public int Lp { get; set; }
        public string DataOdbioru { get; set; }
        public string Dostawca { get; set; }
        public int? Auta { get; set; }
        public int? SztukiDek { get; set; }
        public decimal? WagaDek { get; set; }
        public string TypCeny { get; set; }
        public decimal? Cena { get; set; }
        public string Bufor { get; set; }
        public int? LpW { get; set; }
        public bool MaPartie { get; set; }

        public string Display => $"Lp{Lp}: {Dostawca} - {SztukiDek} szt, {WagaDek:N0} kg ({DataOdbioru})";
    }

    // ═══════════════════════════════════════════════════════════════
    // QC NORMA MODEL (configurable norms from DB)
    // ═══════════════════════════════════════════════════════════════

    public class QCNormaModel
    {
        public int ID { get; set; }
        public string Nazwa { get; set; }
        public string Opis { get; set; }
        public decimal? MinWartosc { get; set; }
        public decimal? MaxWartosc { get; set; }
        public string JednostkaMiary { get; set; }
        public string Kategoria { get; set; }
        public int Kolejnosc { get; set; }

        public bool IsInNorm(decimal? value)
        {
            if (!value.HasValue) return false;
            if (MinWartosc.HasValue && value.Value < MinWartosc.Value) return false;
            if (MaxWartosc.HasValue && value.Value > MaxWartosc.Value) return false;
            return true;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // QC CHECKLIST ITEM (for ZamknijPartieDialog)
    // ═══════════════════════════════════════════════════════════════

    public class ChecklistItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string Nazwa { get; set; }
        public string Opis { get; set; }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set { _isChecked = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked))); }
        }

        private string _status;
        public string Status
        {
            get => _status;
            set { _status = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status))); }
        }

        public bool IsOK { get; set; }
        public bool IsWarning { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════
    // PARTIA STATUS HISTORY
    // ═══════════════════════════════════════════════════════════════

    public class PartiaStatusHistoryItem
    {
        public int ID { get; set; }
        public string Status { get; set; }
        public string StatusPoprzedni { get; set; }
        public string OperatorNazwa { get; set; }
        public string Komentarz { get; set; }
        public DateTime CreatedAtUTC { get; set; }

        public string StatusDisplay => PartiaStatusHelper.Parse(Status).ToDisplayText();
        public string TimeDisplay => CreatedAtUTC.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }

    // ═══════════════════════════════════════════════════════════════
    // ALERT MODEL (Feature 7)
    // ═══════════════════════════════════════════════════════════════

    public class AlertModel
    {
        public string Severity { get; set; } // "ERROR", "WARNING", "INFO"
        public string Message { get; set; }
        public string Partia { get; set; }

        public string SeverityColor => Severity switch
        {
            "ERROR" => "#E74C3C",
            "WARNING" => "#F39C12",
            "INFO" => "#3498DB",
            _ => "#7F8C8D"
        };

        public string SeverityIcon => Severity switch
        {
            "ERROR" => "\u26A0",
            "WARNING" => "\u26A0",
            "INFO" => "\u2139",
            _ => "\u2022"
        };

        public string SeverityBg => Severity switch
        {
            "ERROR" => "#FFEBEE",
            "WARNING" => "#FFF8E1",
            "INFO" => "#E3F2FD",
            _ => "#F5F5F5"
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // DOSTAWCA COMPARISON MODEL (Feature 6)
    // ═══════════════════════════════════════════════════════════════

    public class DostawcaComparisonModel
    {
        public string CustomerID { get; set; }
        public string CustomerName { get; set; }
        public int IloscPartii { get; set; }
        public decimal SrWydajnosc { get; set; }
        public decimal SrKlasaB { get; set; }
        public decimal SrTempRampa { get; set; }
        public decimal SumKg { get; set; }
        public int SumSzt { get; set; }
        public decimal SrPadle { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════
    // HOURLY PRODUCTION (for sparkline, Feature 4)
    // ═══════════════════════════════════════════════════════════════

    public class HourlyProductionPoint
    {
        public int Hour { get; set; }
        public decimal CumulativeKg { get; set; }
    }
}
