// Plik: /Pakowanie/ModelePakowania.cs
// Modele domenowe dla systemu pakowania transportu

using System;

namespace Kalendarz1.Transport.Pakowanie
{
    /// <summary>
    /// Reprezentuje pojedynczą pozycję ładunku
    /// </summary>
    public class PozycjaLike
    {
        public string KodKlienta { get; set; } = string.Empty;
        public int PojemnikiE2 { get; set; }
        public int Kolejnosc { get; set; }
        public string? Uwagi { get; set; }
        
        // Pomocnicze właściwości
        public int PaletyNominal => (int)Math.Ceiling(PojemnikiE2 / 36.0);
        public int PaletyMax => (int)Math.Ceiling(PojemnikiE2 / 40.0);
        
        public PozycjaLike Clone()
        {
            return new PozycjaLike
            {
                KodKlienta = this.KodKlienta,
                PojemnikiE2 = this.PojemnikiE2,
                Kolejnosc = this.Kolejnosc,
                Uwagi = this.Uwagi
            };
        }
    }

    /// <summary>
    /// Wynik obliczenia pakowania kursu
    /// </summary>
    public class WynikPakowania
    {
        public int SumaE2 { get; set; }
        public int PaletyNominal { get; set; }
        public int PaletyMax { get; set; }
        public decimal ProcNominal { get; set; }
        public decimal ProcMax { get; set; }
        public int NadwyzkaNominal { get; set; }
        public int NadwyzkaMax { get; set; }
        
        // Pomocnicze właściwości
        public decimal FinalProc => ProcNominal;
        public bool JestNadwyzka => NadwyzkaNominal > 0 || NadwyzkaMax > 0;
        
        public string PodsumowanieText
        {
            get
            {
                var result = $"Suma E2: {SumaE2}\n";
                result += $"Palety (36/pal): {PaletyNominal} ({ProcNominal:F1}%)\n";
                result += $"Palety (40/pal): {PaletyMax} ({ProcMax:F1}%)\n";
                
                if (JestNadwyzka)
                {
                    result += $"\nNadwyżka:\n";
                    result += $"  Nominal (36): {NadwyzkaNominal} palet\n";
                    result += $"  Max (40): {NadwyzkaMax} palet";
                }
                
                return result;
            }
        }
    }

    /// <summary>
    /// Model kierowcy
    /// </summary>
    public class Kierowca
    {
        public int KierowcaID { get; set; }
        public string Imie { get; set; } = string.Empty;
        public string Nazwisko { get; set; } = string.Empty;
        public string? Telefon { get; set; }
        public bool Aktywny { get; set; } = true;
        public DateTime UtworzonoUTC { get; set; }
        public DateTime? ZmienionoUTC { get; set; }
        
        public string PelneNazwisko => $"{Imie} {Nazwisko}";
        
        public override string ToString() => PelneNazwisko;
    }

    /// <summary>
    /// Model pojazdu
    /// </summary>
    public class Pojazd
    {
        public int PojazdID { get; set; }
        public string Rejestracja { get; set; } = string.Empty;
        public string? Marka { get; set; }
        public string? Model { get; set; }
        public int PaletyH1 { get; set; }
        public bool Aktywny { get; set; } = true;
        public DateTime UtworzonoUTC { get; set; }
        public DateTime? ZmienionoUTC { get; set; }
        
        public string Opis => string.IsNullOrWhiteSpace(Marka) || string.IsNullOrWhiteSpace(Model) 
            ? Rejestracja 
            : $"{Rejestracja} - {Marka} {Model}";
        
        public override string ToString() => Opis;
    }

    /// <summary>
    /// Model kursu transportowego
    /// </summary>
    public class Kurs
    {
        public long KursID { get; set; }
        public DateTime DataKursu { get; set; }
        public int KierowcaID { get; set; }
        public int PojazdID { get; set; }
        public string? Trasa { get; set; }
        public TimeSpan? GodzWyjazdu { get; set; }
        public TimeSpan? GodzPowrotu { get; set; }
        public string Status { get; set; } = "Planowany";
        public byte PlanE2NaPalete { get; set; } = 36;
        public DateTime UtworzonoUTC { get; set; }
        public string? Utworzyl { get; set; }
        public DateTime? ZmienionoUTC { get; set; }
        public string? Zmienil { get; set; }
        
        // Właściwości nawigacyjne (wypełniane przez repo)
        public string? KierowcaNazwa { get; set; }
        public string? PojazdRejestracja { get; set; }
        public int PaletyPojazdu { get; set; }
        public int SumaE2 { get; set; }
        public int PaletyNominal { get; set; }
        public int PaletyMax { get; set; }
        public decimal ProcNominal { get; set; }
        public decimal ProcMax { get; set; }
        
        public decimal FinalProc => ProcNominal;
        
        public string StatusDisplay => Status switch
        {
            "Planowany" => "📋 Planowany",
            "WTrakcie" => "🚚 W trakcie",
            "Zakonczony" => "✅ Zakończony",
            "Anulowany" => "❌ Anulowany",
            _ => Status
        };
    }

    /// <summary>
    /// Model ładunku w kursie
    /// </summary>
    public class Ladunek
    {
        public long LadunekID { get; set; }
        public long KursID { get; set; }
        public int Kolejnosc { get; set; }
        public string? KodKlienta { get; set; }
        public int PojemnikiE2 { get; set; }
        public int? PaletyH1 { get; set; }
        public byte? PlanE2NaPaleteOverride { get; set; }
        public string? Uwagi { get; set; }
        public DateTime UtworzonoUTC { get; set; }
        
        // Właściwości pomocnicze
        public int PaletyWyliczone => (int)Math.Ceiling(PojemnikiE2 / 36.0);
        public int PaletyMax => (int)Math.Ceiling(PojemnikiE2 / 40.0);
        
        public PozycjaLike ToPozycjaLike()
        {
            return new PozycjaLike
            {
                KodKlienta = KodKlienta ?? string.Empty,
                PojemnikiE2 = PojemnikiE2,
                Kolejnosc = Kolejnosc,
                Uwagi = Uwagi
            };
        }
    }

    /// <summary>
    /// Model zamówienia (integracja z istniejącym systemem)
    /// </summary>
    public class ZamowienieTransport
    {
        public int ZamowienieID { get; set; }
        public int KlientID { get; set; }
        public string KlientNazwa { get; set; } = string.Empty;
        public DateTime DataZamowienia { get; set; }
        public decimal IloscKg { get; set; }
        public string? Status { get; set; }
        public string? Handlowiec { get; set; }
        
        // Konwersja kg na pojemniki E2 (estymacja)
        public int EstymowanePojemnikiE2 => (int)Math.Ceiling(IloscKg / 15.0m);
        
        public override string ToString() => $"{KlientNazwa} - {IloscKg:N0} kg (~{EstymowanePojemnikiE2} E2)";
    }
}