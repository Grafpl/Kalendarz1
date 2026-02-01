using System;
using System.Collections.Generic;
using Kalendarz1.HandlowiecDashboard.Constants;

namespace Kalendarz1.HandlowiecDashboard.Models
{
    /// <summary>
    /// Saldo opakowań kontrahenta z analizą ryzyka
    /// </summary>
    public class SaldoOpakowanKontrahenta
    {
        public int KontrahentId { get; set; }
        public string Kontrahent { get; set; }
        public string Handlowiec { get; set; }
        
        // Salda
        public int SaldoE2 { get; set; }
        public int SaldoH1 { get; set; }
        public decimal WartoscE2 => SaldoE2 * BusinessConstants.Opakowania.CenaE2;
        public decimal WartoscH1 => SaldoH1 * BusinessConstants.Opakowania.CenaH1;
        public decimal WartoscCalkowita => WartoscE2 + WartoscH1;
        
        // Limity
        public int LimitE2 { get; set; }
        public int LimitH1 { get; set; }
        public bool PrzekroczonyLimitE2 => SaldoE2 > LimitE2 && LimitE2 > 0;
        public bool PrzekroczonyLimitH1 => SaldoH1 > LimitH1 && LimitH1 > 0;
        public int PrzekroczenieE2 => Math.Max(0, SaldoE2 - LimitE2);
        public int PrzekroczenieH1 => Math.Max(0, SaldoH1 - LimitH1);
        
        // Czasowe
        public DateTime? OstatniZwrot { get; set; }
        public DateTime? OstatnieWydanie { get; set; }
        public int DniOdOstatniegoZwrotu => OstatniZwrot.HasValue 
            ? (int)(DateTime.Today - OstatniZwrot.Value).TotalDays 
            : 999;
        public int DniOdOstatniegoWydania => OstatnieWydanie.HasValue
            ? (int)(DateTime.Today - OstatnieWydanie.Value).TotalDays
            : 999;
        
        // Płatności (korelacja z ryzykiem)
        public decimal ZaleglosciPlatnosci { get; set; }
        public bool MaZaleglosciPlatnosci => ZaleglosciPlatnosci > 0;
        
        // Risk Score (0-100)
        public double RiskScore { get; set; }
        
        public string PoziomRyzyka => RiskScore switch
        {
            >= 80 => "KRYTYCZNY",
            >= 60 => "WYSOKI",
            >= 40 => "ŚREDNI",
            >= 20 => "NISKI",
            _ => "MINIMALNY"
        };
        
        public string KolorRyzyka => RiskScore switch
        {
            >= 80 => BusinessConstants.Kolory.Niebezpieczenstwo,
            >= 60 => BusinessConstants.Kolory.Uwaga,
            >= 40 => BusinessConstants.Kolory.Ostrzezenie,
            >= 20 => BusinessConstants.Kolory.Sukces,
            _ => "#2ECC71"
        };
        
        public string IkonaRyzyka => RiskScore switch
        {
            >= 80 => "🔴",
            >= 60 => "🟠",
            >= 40 => "🟡",
            _ => "🟢"
        };
        
        // Formatowanie
        public string SaldoE2Tekst => $"{SaldoE2:N0}";
        public string SaldoH1Tekst => $"{SaldoH1:N0}";
        public string WartoscTekst => $"{WartoscCalkowita:N0} zł";
        public string DniOdZwrotuTekst => OstatniZwrot.HasValue 
            ? $"{DniOdOstatniegoZwrotu} dni" 
            : "Brak zwrotów";
        public string LimitE2Tekst => LimitE2 > 0 ? $"{SaldoE2}/{LimitE2}" : $"{SaldoE2}";
        public string LimitH1Tekst => LimitH1 > 0 ? $"{SaldoH1}/{LimitH1}" : $"{SaldoH1}";
        public string ZaleglosciTekst => MaZaleglosciPlatnosci ? $"{ZaleglosciPlatnosci:N0} zł" : "OK";
    }

    /// <summary>
    /// Aging opakowań - ile w każdym przedziale czasowym
    /// </summary>
    public class AgingOpakowan
    {
        public string Przedzial { get; set; }
        public int MinDni { get; set; }
        public int MaxDni { get; set; }
        public int IloscE2 { get; set; }
        public int IloscH1 { get; set; }
        public decimal Wartosc { get; set; }
        public int LiczbaKontrahentow { get; set; }
        public string Kolor { get; set; }
        
        public string WartoscTekst => $"{Wartosc:N0} zł";
    }

    /// <summary>
    /// KPI opakowań
    /// </summary>
    public class OpakowaniaKPI
    {
        public int SumaE2 { get; set; }
        public int SumaH1 { get; set; }
        public decimal WartoscZamrozona { get; set; }
        
        public int SumaE2Poprzedni { get; set; }
        public int SumaH1Poprzedni { get; set; }
        
        public double ZmianaE2Procent => SumaE2Poprzedni > 0 
            ? ((double)(SumaE2 - SumaE2Poprzedni) / SumaE2Poprzedni) * 100 : 0;
        public double ZmianaH1Procent => SumaH1Poprzedni > 0 
            ? ((double)(SumaH1 - SumaH1Poprzedni) / SumaH1Poprzedni) * 100 : 0;
        
        public int LiczbaPrzekroczonychLimitow { get; set; }
        public int LiczbaKrytycznych { get; set; }
        public int LiczbaDoObslugiDzis { get; set; }
        public int LiczbaKontrahentow { get; set; }
        
        public int ZwrotyE2Miesiac { get; set; }
        public int ZwrotyH1Miesiac { get; set; }
        
        // Formatowanie
        public string SumaE2Tekst => $"{SumaE2:N0}";
        public string SumaH1Tekst => $"{SumaH1:N0}";
        public string WartoscTekst => $"{WartoscZamrozona:N0} zł";
        public string ZmianaE2Tekst => $"{(ZmianaE2Procent >= 0 ? "+" : "")}{ZmianaE2Procent:F1}%";
        public string ZmianaH1Tekst => $"{(ZmianaH1Procent >= 0 ? "+" : "")}{ZmianaH1Procent:F1}%";
    }

    /// <summary>
    /// Trend opakowań miesięczny
    /// </summary>
    public class TrendOpakowan
    {
        public int Rok { get; set; }
        public int Miesiac { get; set; }
        public string MiesiacNazwa { get; set; }
        public string MiesiacKrotki => $"{Miesiac:00}/{Rok % 100}";
        public int SaldoE2 { get; set; }
        public int SaldoH1 { get; set; }
        public int ZwrotyE2 { get; set; }
        public int ZwrotyH1 { get; set; }
        public int WydaniaE2 { get; set; }
        public int WydaniaH1 { get; set; }
        public decimal Wartosc => SaldoE2 * BusinessConstants.Opakowania.CenaE2 
                                + SaldoH1 * BusinessConstants.Opakowania.CenaH1;
    }

    /// <summary>
    /// Alert opakowań
    /// </summary>
    public class AlertOpakowania
    {
        public int Id { get; set; }
        public string Typ { get; set; }  // "KRYTYCZNY", "OSTRZEZENIE", "INFO"
        public string Kontrahent { get; set; }
        public string Handlowiec { get; set; }
        public string Komunikat { get; set; }
        public DateTime DataUtworzenia { get; set; }
        public bool Przeczytany { get; set; }
        
        public string KolorTla => Typ switch
        {
            "KRYTYCZNY" => "#E74C3C",
            "OSTRZEZENIE" => "#F39C12",
            _ => "#3498DB"
        };
        
        public string Ikona => Typ switch
        {
            "KRYTYCZNY" => "🔴",
            "OSTRZEZENIE" => "🟡",
            _ => "ℹ️"
        };
    }

    /// <summary>
    /// Punkt na scatter chart (mapa ryzyka)
    /// </summary>
    public class RiskMapPoint
    {
        public string Kontrahent { get; set; }
        public string Handlowiec { get; set; }
        public double X { get; set; }  // Dni od zwrotu
        public double Y { get; set; }  // Wartość opakowań
        public double Size { get; set; }  // Wielkość bąbelka (sprzedaż)
        public string Kolor { get; set; }  // Kolor (status płatności)
        public double RiskScore { get; set; }
    }
}
