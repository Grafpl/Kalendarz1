using System;
using System.Collections.Generic;

namespace Kalendarz1.Traceability
{
    /// <summary>Paleta wyrobu z lot numberem.</summary>
    public class PaletaWyrob
    {
        public long Id { get; set; }
        public string LotNumber { get; set; } = "";
        public DateTime DataProdukcji { get; set; }
        public string? Smiana { get; set; }
        public string? Linia { get; set; }
        public string? OperatorId { get; set; }
        public string? KodTowaru { get; set; }
        public string? NazwaTowaru { get; set; }
        public int? LiczbaSztuk { get; set; }
        public decimal WagaKg { get; set; }
        public DateTime? DataWaznosci { get; set; }
        public string Status { get; set; } = "NA_MAGAZYNIE";

        public string DataFormatted => DataProdukcji.ToString("dd.MM.yyyy");
        public string WagaFormatted => $"{WagaKg:N1} kg";
        public string StatusKolor => Status switch
        {
            "NA_MAGAZYNIE" => "#2563EB",
            "WYSLANO" => "#10B981",
            "WYCOFANO" => "#DC2626",
            "ZUTYLIZOWANO" => "#94A3B8",
            _ => "#475569"
        };
    }

    /// <summary>Składnik palety wyrobu (partia surowa + hodowca).</summary>
    public class PaletaSklad
    {
        public long Id { get; set; }
        public long PaletaWyrobId { get; set; }
        public string Partia { get; set; } = "";
        public string? CustomerID { get; set; }
        public string? CustomerName { get; set; }
        public decimal? WagaKgUdzial { get; set; }
        public string? Notatki { get; set; }

        public string UdzialFormatted => WagaKgUdzial.HasValue ? $"{WagaKgUdzial.Value:N1} kg" : "—";
    }

    /// <summary>Wydanie palety do klienta.</summary>
    public class PaletaWydanie
    {
        public long Id { get; set; }
        public long PaletaWyrobId { get; set; }
        public string? NumerDokumentu { get; set; }
        public int? KlientId { get; set; }
        public string? KlientNazwa { get; set; }
        public decimal? WagaKgWydana { get; set; }
        public DateTime DataWydania { get; set; }

        public string DataFormatted => DataWydania.ToString("dd.MM.yyyy HH:mm");
    }

    /// <summary>Wynik reverse trace (od lot number do źródła).</summary>
    public class ReverseTraceResult
    {
        public string LotNumber { get; set; } = "";
        public PaletaWyrob? Paleta { get; set; }
        public List<PaletaSklad> Sklad { get; set; } = new();
        public List<PaletaWydanie> Wydania { get; set; } = new();
        public string? Blad { get; set; }

        public bool Znaleziono => Paleta != null && Blad == null;
        public int LiczbaHodowcow
        {
            get
            {
                var set = new HashSet<string>();
                foreach (var s in Sklad)
                    if (!string.IsNullOrEmpty(s.CustomerName)) set.Add(s.CustomerName!);
                return set.Count;
            }
        }
    }

    /// <summary>Wpis recall.</summary>
    public class Recall
    {
        public long Id { get; set; }
        public string RecallNumber { get; set; } = "";
        public DateTime DataInicjacji { get; set; }
        public string? InicjowanyPrzez { get; set; }
        public string? Powod { get; set; }
        public string Kategoria { get; set; } = "JAKOSC";
        public string TypZakresu { get; set; } = "PARTIA";
        public string? ZakresIdent { get; set; }
        public int? LiczbaPalet { get; set; }
        public int? LiczbaKlientow { get; set; }
        public decimal? WagaKg { get; set; }
        public string Status { get; set; } = "OTWARTY";
        public DateTime? DataZamkniecia { get; set; }

        public string DataFormatted => DataInicjacji.ToString("dd.MM.yyyy HH:mm");
        public string ZakresFormatted => $"{TypZakresu}: {ZakresIdent}";
        public string PodsumowanieFormatted =>
            $"{LiczbaPalet ?? 0} palet • {LiczbaKlientow ?? 0} klientów • {WagaKg ?? 0:N0} kg";
        public bool Otwarty => Status == "OTWARTY";
        public string StatusKolor => Otwarty ? "#DC2626" : "#10B981";
    }

    /// <summary>Wynik inicjacji recall (podsumowanie zakresu).</summary>
    public class RecallResult
    {
        public long RecallId { get; set; }
        public string RecallNumber { get; set; } = "";
        public int LiczbaPalet { get; set; }
        public int LiczbaKlientow { get; set; }
        public decimal WagaKg { get; set; }
        public List<PaletaWyrob> Palety { get; set; } = new();
    }
}
