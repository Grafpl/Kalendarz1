using System;
using System.Collections.Generic;

namespace Kalendarz1.Customer360.Models
{
    /// <summary>Wynik wyszukiwania klienta — używane w selektorze górnym.</summary>
    public class KlientSearchItem
    {
        public int Id { get; set; }
        public string Nazwa { get; set; } = "";
        public string NIP { get; set; } = "";
        public string Miasto { get; set; } = "";
        public string Handlowiec { get; set; } = "";
        public string Display => string.IsNullOrWhiteSpace(NIP)
            ? $"{Nazwa} · {Miasto}"
            : $"{Nazwa} · NIP {NIP} · {Miasto}";
    }

    /// <summary>Pełne dane klienta (dla nagłówka 360).</summary>
    public class KlientHeader
    {
        public int Id { get; set; }
        public string Nazwa { get; set; } = "";
        public string NIP { get; set; } = "";
        public string Adres { get; set; } = "";
        public string KodPocztowy { get; set; } = "";
        public string Miasto { get; set; } = "";
        public string Telefon { get; set; } = "";
        public string Email { get; set; } = "";
        public string Handlowiec { get; set; } = "";
        public string Kategoria { get; set; } = "";     // A/B/C/D z KartotekaOdbiorcyDane

        public string AdresPelny => $"{Adres}, {KodPocztowy} {Miasto}".Trim(' ', ',');
    }

    /// <summary>Główne KPI klienta (na cards nagłówka).</summary>
    public class KlientKpi
    {
        public decimal Obrot12M { get; set; }             // Suma wartości netto ostatnich 12 mies
        public decimal Obrot12MPrev { get; set; }         // 12-24 mies temu (porównanie YoY)
        public decimal Marza12M { get; set; }             // Łączna marża (cena - koszt) ostatnich 12 mies
        public decimal SredniaMarzaKg { get; set; }       // zł/kg
        public int LiczbaZamowien12M { get; set; }
        public decimal SumaKg12M { get; set; }
        public DateTime? OstatnieZamowienie { get; set; }
        public int DniOdOstatniegoZamowienia => OstatnieZamowienie.HasValue
            ? (int)(DateTime.Today - OstatnieZamowienie.Value.Date).TotalDays
            : -1;
        public decimal SredniCzasMiedzyZamowieniami { get; set; }  // dni
        public string ChurnRiskLevel { get; set; } = "";  // OK / WATCH / WARNING / CRITICAL
        public string ChurnRiskReason { get; set; } = "";

        // Finanse
        public decimal LimitKredytowy { get; set; }
        public decimal DoZaplaty { get; set; }
        public decimal Terminowe { get; set; }
        public decimal Przeterminowane { get; set; }
        public int MaxDniOpoznienia { get; set; }
        public int LiczbaFaktur { get; set; }
        public decimal WykorzystanieLimitProc => LimitKredytowy > 0 ? DoZaplaty / LimitKredytowy * 100m : 0m;

        // Reklamacje
        public int LiczbaReklamacji12M { get; set; }
        public decimal WartoscReklamacji12M { get; set; }
        public decimal RelativeReklamacjeProc => Obrot12M > 0
            ? WartoscReklamacji12M / Obrot12M * 100m : 0m;
    }

    /// <summary>Wiersz historii zamówień (tabela).</summary>
    public class OrderHistoryItem
    {
        public int Id { get; set; }
        public DateTime DataZamowienia { get; set; }
        public DateTime? DataUboju { get; set; }
        public DateTime? DataWydania { get; set; }
        public string Status { get; set; } = "";
        public decimal SumaKg { get; set; }
        public int LiczbaPozycji { get; set; }
        public decimal Wartosc { get; set; }
        public string Handlowiec { get; set; } = "";
        public string KodPrincipal { get; set; } = "";  // główny towar (z najwyższą wagą)
    }

    /// <summary>Statystyki miesięczne (do wykresu).</summary>
    public class MonthlyStats
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal SumaKg { get; set; }
        public decimal Wartosc { get; set; }
        public int LiczbaZamowien { get; set; }
        public string Label => $"{Month:00}/{Year % 100:00}";
    }

    /// <summary>Reklamacja klienta.</summary>
    public class KlientReklamacja
    {
        public int Id { get; set; }
        public DateTime DataZgloszenia { get; set; }
        public string Typ { get; set; } = "";
        public string Status { get; set; } = "";
        public string Opis { get; set; } = "";
        public decimal Kwota { get; set; }
        public int? OrderId { get; set; }
    }

    /// <summary>Faktura klienta (do tabu Finanse).</summary>
    public class KlientFaktura
    {
        public string NumerFaktury { get; set; } = "";
        public DateTime DataWystawienia { get; set; }
        public DateTime? TerminPlatnosci { get; set; }
        public decimal Kwota { get; set; }
        public decimal Zaplacone { get; set; }
        public decimal Saldo => Kwota - Zaplacone;
        public int DniOpoznienia => TerminPlatnosci.HasValue && Saldo > 0.01m && DateTime.Today > TerminPlatnosci.Value
            ? (int)(DateTime.Today - TerminPlatnosci.Value).TotalDays
            : 0;
        public string Status => Saldo <= 0.01m ? "Zapłacone"
                              : DniOpoznienia > 0 ? $"Przeterminowane {DniOpoznienia}d"
                              : "Oczekuje";
    }

    /// <summary>Top kupowany towar — statystyka.</summary>
    public class TopTowarItem
    {
        public int Pozycja { get; set; }
        public int KodTowaru { get; set; }
        public string Nazwa { get; set; } = "";
        public decimal SumaKg { get; set; }
        public decimal Wartosc { get; set; }
        public int LiczbaZamowien { get; set; }
        public decimal SredniaCena => SumaKg > 0 ? Wartosc / SumaKg : 0m;
        public System.Windows.Media.ImageSource? Image { get; set; }
        public System.Windows.Visibility HasImageVisibility =>
            Image != null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        public System.Windows.Visibility PlaceholderVisibility =>
            Image == null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    /// <summary>Aktywność tygodniowa (heatmap dzień × godzina).</summary>
    public class AktywnoscTygodniowa
    {
        public int DayOfWeek { get; set; }   // 0=Pn ... 6=Nd
        public int LiczbaZamowien { get; set; }
        public decimal SumaKg { get; set; }
    }

    // ════════════════════════════════════════════════════════════════════
    // WERYFIKACJA: zamówienia (LibraNet) × faktury (HANDEL Symfonia)
    // ════════════════════════════════════════════════════════════════════

    /// <summary>Porównanie per towar: zamówione kg/wartość vs zafakturowane kg/wartość.</summary>
    public class WeryfikacjaTowar
    {
        public int KodTowaru { get; set; }
        public string Nazwa { get; set; } = "";
        public decimal ZamowioneKg { get; set; }
        public decimal ZafakturowaneKg { get; set; }
        public decimal RoznicaKg => ZafakturowaneKg - ZamowioneKg;
        public decimal ZgodnoscProc => ZamowioneKg > 0 ? ZafakturowaneKg / ZamowioneKg * 100m : 0m;
        public decimal ZamowionaWartosc { get; set; }
        public decimal ZafakturowanaWartosc { get; set; }
        public decimal RoznicaWartosci => ZafakturowanaWartosc - ZamowionaWartosc;

        // Status: "✅ Zgodne" / "✂ Ucięte" / "➕ Więcej" / "⚠ Brak faktury" / "⚠ Brak zamówienia"
        public string Status =>
            ZamowioneKg == 0 && ZafakturowaneKg > 0 ? "⚠ Tylko faktura"
            : ZamowioneKg > 0 && ZafakturowaneKg == 0 ? "⚠ Brak faktury"
            : Math.Abs(RoznicaKg) < 0.5m ? "✅ Zgodne"
            : RoznicaKg < 0 ? "✂ Ucięte"
            : "➕ Więcej";
    }

    /// <summary>Sumarum weryfikacji — KPI cards.</summary>
    public class WeryfikacjaSumarum
    {
        public decimal ZamowioneKg { get; set; }
        public decimal ZafakturowaneKg { get; set; }
        public decimal RoznicaKg => ZafakturowaneKg - ZamowioneKg;
        public decimal ZgodnoscProc => ZamowioneKg > 0 ? ZafakturowaneKg / ZamowioneKg * 100m : 0m;
        public decimal ZamowionaWartosc { get; set; }
        public decimal ZafakturowanaWartosc { get; set; }
        public decimal RoznicaWartosci => ZafakturowanaWartosc - ZamowionaWartosc;
        public int LiczbaZamowien { get; set; }
        public int LiczbaFaktur { get; set; }
        public int LiczbaTowarow { get; set; }
        public int LiczbaTowarowUcietych { get; set; }     // Status = Ucięte
        public int LiczbaTowarowDodanych { get; set; }     // Status = Więcej
        public int LiczbaTowarowBrakFaktury { get; set; }  // Status = Brak faktury
    }

    /// <summary>Anulowane zamówienie klienta.</summary>
    public class AnulowaneZam
    {
        public int Id { get; set; }
        public DateTime DataZamowienia { get; set; }
        public DateTime? DataPrzyjazdu { get; set; }
        public decimal SumaKg { get; set; }
        public decimal Wartosc { get; set; }
        public string Powod { get; set; } = "";
        public string AnulowanePrzez { get; set; } = "";
        public DateTime? DataAnulowania { get; set; }
        public string Handlowiec { get; set; } = "";
    }

    /// <summary>Pełna faktura z liniami pozycji.</summary>
    public class FakturaDetail
    {
        public string NumerFaktury { get; set; } = "";
        public string TypDk { get; set; } = "";       // FVS / FVK / KFS / ...
        public DateTime DataWystawienia { get; set; }
        public DateTime? TerminPlatnosci { get; set; }
        public decimal Brutto { get; set; }
        public decimal Netto { get; set; }
        public decimal Zaplacone { get; set; }
        public decimal Saldo => Brutto - Zaplacone;
        public decimal SumaKg { get; set; }
        public int LiczbaPozycji { get; set; }
        public string Status => Saldo <= 0.01m ? "Zapłacone"
                              : TerminPlatnosci.HasValue && DateTime.Today > TerminPlatnosci.Value ? $"Przeterminowane {(int)(DateTime.Today - TerminPlatnosci.Value).TotalDays}d"
                              : "Oczekuje";
    }
}
