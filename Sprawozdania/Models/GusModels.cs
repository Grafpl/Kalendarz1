using System;
using System.Collections.Generic;

namespace Kalendarz1.Sprawozdania.Models
{
    // ════════════════════════════════════════════════════════════════════
    // Typ formularza GUS — rozszerzaj wraz z implementacją kolejnych
    // ════════════════════════════════════════════════════════════════════
    public enum GusFormType
    {
        P02,    // Meldunek o produkcji wyrobów przemysłowych — miesięczny
        R09A,   // Pogłowie drobiu — półroczny (planowany)
        DG1,    // Działalność gospodarcza — miesięczny (planowany)
        C01     // Handel hurtowy — miesięczny (planowany)
    }

    public static class GusFormTypeExtensions
    {
        public static string ToSymbol(this GusFormType t) => t switch
        {
            GusFormType.P02 => "P-02",
            GusFormType.R09A => "R-09A",
            GusFormType.DG1 => "DG-1",
            GusFormType.C01 => "C-01",
            _ => t.ToString()
        };
    }

    // ════════════════════════════════════════════════════════════════════
    // Wiersz historii GusSubmissions
    // ════════════════════════════════════════════════════════════════════
    public class GusSubmissionRow
    {
        public int Id { get; set; }
        public string Formularz { get; set; } = "";
        public string FormularzWersja { get; set; } = "";
        public DateTime OkresOd { get; set; }
        public DateTime OkresDo { get; set; }
        public int Rok { get; set; }
        public int? Miesiac { get; set; }
        public string Regon { get; set; } = "";
        public string? GeneratedXml { get; set; }
        public string? PlikXml { get; set; }
        public string Status { get; set; } = "Draft";   // Draft/Generated/Exported/Sent/Failed
        public string? ValidationLog { get; set; }
        public string? ErrorMessage { get; set; }
        public string? NumerWPortalu { get; set; }
        public int IloscPozycji { get; set; }
        public decimal SumaWartosc { get; set; }        // dla P-02: suma sprzedaży MC (tony) — wgląd w listingu historii
        public int? GeneratedBy { get; set; }
        public string? GeneratedByImie { get; set; }
        public DateTime GeneratedAt { get; set; }
        public DateTime? ExportedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public string? Notatki { get; set; }
    }

    // ════════════════════════════════════════════════════════════════════
    // P-02 — pozycja sprawozdania (1 wiersz PKWiU)
    // Wszystkie wartości w jednostce 00130 = tona (1000 kg). Liczby całkowite.
    // Odpowiada parze pól d1r3 (PKWiU) + d1r5..d1r10 (6 kolumn liczbowych).
    // ════════════════════════════════════════════════════════════════════
    public class P02PozycjaModel
    {
        // PKWiU/PRODPOL znormalizowany (myślnik)
        public string Pkwiu { get; set; } = "";

        // Nazwa wyrobu wg PKWiU (display + audyt)
        public string NazwaWyrobu { get; set; } = "";

        // Jednostka — kod GUS (00130 = tona). Dla drobiu zawsze tona.
        public string JednostkaKod { get; set; } = "00130";

        // 6 kolumn liczbowych — wszystko w tonach, zaokrąglone do int przy emisji
        public decimal ProdukcjaWMiesiacuTony { get; set; }      // d1r5
        public decimal ProdukcjaOdPoczatkuRokuTony { get; set; } // d1r6
        public decimal SprzedazWMiesiacuTony { get; set; }       // d1r7
        public decimal SprzedazOdPoczatkuRokuTony { get; set; }  // d1r8
        public decimal ZapasyWyrobowTony { get; set; }           // d1r9 — domyślnie 0
        public decimal ZapasyTowarowTony { get; set; }           // d1r10 — domyślnie 0

        // Audyt: lista kodów towarów Sage które weszły do tej pozycji
        public List<string> KodyTowarow { get; set; } = new();
        public int LiczbaTowarow => KodyTowarow.Count;

        // Czy pozycja jest pusta (wszystkie liczby = 0)
        public bool JestPusta =>
            ProdukcjaWMiesiacuTony == 0 && ProdukcjaOdPoczatkuRokuTony == 0 &&
            SprzedazWMiesiacuTony == 0 && SprzedazOdPoczatkuRokuTony == 0 &&
            ZapasyWyrobowTony == 0 && ZapasyTowarowTony == 0;
    }

    // ════════════════════════════════════════════════════════════════════
    // P-02 — dane wsadowe do generatora XML
    // ════════════════════════════════════════════════════════════════════
    public class P02ReportData
    {
        public int Rok { get; set; }
        public int Miesiac { get; set; }                 // 1-12
        public DateTime OkresOd { get; set; }
        public DateTime OkresDo { get; set; }
        public List<P02Pozycja> Pozycje { get; set; } = new();
    }

    // Alias dla czytelności w generatorze
    public class P02Pozycja : P02PozycjaModel { }

    // ════════════════════════════════════════════════════════════════════
    // Konfiguracja modułu GUS (REGON + osoba + PKD)
    // %LOCALAPPDATA%\Kalendarz1\Gus\settings.json
    // ════════════════════════════════════════════════════════════════════
    public class GusSettings
    {
        public string Regon { get; set; } = "75004547600000";
        public string Pkd { get; set; } = "1012";
        public string OsobaImie { get; set; } = "";
        public string OsobaNazwisko { get; set; } = "";
        public string OsobaTelefon { get; set; } = "";
        public string EmailJednostki { get; set; } = "sekretariat@piorkowscy.com.pl";
        public string EmailOsoby { get; set; } = "sekretariat@piorkowscy.com.pl";
        public string FolderEksportu { get; set; } = "";

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Regon) &&
            !string.IsNullOrWhiteSpace(OsobaNazwisko);
    }

    public class GusPkwiuMappingRow
    {
        public int Id { get; set; }
        public string KodTowaru { get; set; } = "";
        public int? IdTowaru { get; set; }
        public string PkwiuKod { get; set; } = "";
        public string Jednostka { get; set; } = "kg";
        public bool UwzgledniacP02 { get; set; } = true;
        public string? Komentarz { get; set; }
    }
}
