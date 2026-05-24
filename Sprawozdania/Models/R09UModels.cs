using System;
using System.Collections.Generic;

namespace Kalendarz1.Sprawozdania.Models
{
    // ════════════════════════════════════════════════════════════════════
    // R-09U — Sprawozdanie o wielkości ubojów zwierząt gospodarskich
    // Wersja XML: formularzWersja="2.0" (potwierdzone z wzorca Sage 2026-05-23)
    //
    // Struktura: 4 działy (D1 / D1A / D2 / D3), wiersze w01-w22 = kategorie.
    // BROJLERY KURZE = wiersz w14 (jedyny wypełniony dla Piórkowskich).
    //
    // Kolumny r1-r5 per wiersz (D1/D1A/D2):
    //   r1 = liczba sztuk
    //   r2 = waga żywa (kg)
    //   r3 = waga poubojowa brutto (kg)
    //   r4 = waga handlowa netto (kg)
    //   r5 = wartość (zł)
    // ════════════════════════════════════════════════════════════════════

    public enum R09UWiersz
    {
        // Bydło / trzoda / owce (Piórkowscy nie wypełniają — placeholder)
        Bydlo_DorosleByki = 1,
        Bydlo_Krowy = 2,
        Bydlo_Jalowki = 3,
        Bydlo_Cielaki = 4,
        Trzoda_Maciory = 5,
        Trzoda_Knury = 6,
        Trzoda_Tucznik = 7,
        Trzoda_Prosie = 8,
        Owce_Maciorki = 9,
        Owce_Tryki = 10,
        Owce_Jagniaki = 11,
        Kozy = 12,
        Konie = 13,

        // Drób (Piórkowscy = w14 brojlery)
        Brojlery_Kurze = 14,
        Kury_Rosolowe = 15,
        Indyki = 16,
        Kaczki = 17,
        Gesi = 18,
        Drob_Inny = 19,

        // Pozostałe kategorie
        Krolik = 20,
        Strus = 21,
        Inne_Zwierzeta = 22
    }

    public static class R09UWierszExtensions
    {
        public static string Label(this R09UWiersz w) => w switch
        {
            R09UWiersz.Brojlery_Kurze => "w14 — Brojlery kurze",
            R09UWiersz.Kury_Rosolowe => "w15 — Kury rosołowe",
            R09UWiersz.Indyki => "w16 — Indyki",
            R09UWiersz.Kaczki => "w17 — Kaczki",
            R09UWiersz.Gesi => "w18 — Gęsi",
            R09UWiersz.Drob_Inny => "w19 — Inny drób",
            _ => $"w{(int)w:D2} — {w}"
        };
    }

    public class R09UPozycja
    {
        public R09UWiersz Wiersz { get; set; } = R09UWiersz.Brojlery_Kurze;
        public string WierszLabel => Wiersz.Label();
        public int WierszNumer => (int)Wiersz;

        public int LiczbaSztuk { get; set; }            // r1
        public decimal WagaZywaKg { get; set; }          // r2
        public decimal WagaPoubojowaBruttoKg { get; set; } // r3
        public decimal WagaHandlowaNettoKg { get; set; }   // r4
        public decimal WartoscZl { get; set; }            // r5

        // Pochodne
        public decimal SredniaMasaSztKg =>
            LiczbaSztuk > 0 ? WagaZywaKg / LiczbaSztuk : 0;
        public decimal WydajnoscPoubojowa =>
            WagaZywaKg > 0 ? (WagaPoubojowaBruttoKg / WagaZywaKg) * 100m : 0;

        public bool JestPusta =>
            LiczbaSztuk == 0 && WagaZywaKg == 0 && WagaPoubojowaBruttoKg == 0
            && WagaHandlowaNettoKg == 0 && WartoscZl == 0;
    }

    public class R09UReportData
    {
        public int Rok { get; set; }
        public int Miesiac { get; set; }
        public DateTime OkresOd { get; set; }
        public DateTime OkresDo { get; set; }

        // Dział 1 — własny skup + ubój (główne dane Piórkowskich)
        public List<R09UPozycja> Dzial1 { get; set; } = new();

        // Dział 2 — ubój zlecony usługowy (opcjonalne, edycja ręczna)
        public List<R09UPozycja> Dzial2 { get; set; } = new();
    }
}
