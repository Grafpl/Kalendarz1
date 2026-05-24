using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Kalendarz1.Sprawozdania.Services
{
    // ════════════════════════════════════════════════════════════════════
    // Klasyfikator PKWiU dla wyrobów drobiowych Piórkowskich
    //
    // Reguły wg ustaleń z 2026-05-20 (Sergiusz Piórkowski):
    //
    //   • 10.12.10-10  Ptactwo Gallus Domesticus, CAŁE, świeże/schłodzone
    //                  Kody Sage: "Kurczak A", "Kurczak B"
    //                  Produkcja: dokument PWU (przyjęcie z uboju)
    //                  Sprzedaż: TYLKO "Kurczak A" (B nie idzie do klienta)
    //
    //   • 10.12.10-50  Kawałki z ptactwa Gallus Domesticus, świeże/schłodzone
    //                  Kody Sage: wszystkie elementy świeże (filet, korpus,
    //                  ćwiartka, skrzydło, noga, podroby, polędwiczki itd.)
    //                  Produkcja: dokument PWP (przyjęcie z produkcji)
    //
    //   • 10.12.20-13  CAŁE tuszki MROŻONE
    //                  Kod Sage: "Kurczak A Mrożony" itp.
    //                  W praktyce u Piórkowskich = 0 (nie sprzedają całych mrożonych)
    //                  Klasyfikujemy żeby pozycja była widoczna w XML
    //
    //   • 10.12.20-53  Elementy MROŻONE
    //                  Kody Sage: wszystkie z katalogu 67153 oprócz całych tuszek
    //                  ("Filet Mrożony", "Skrzydło Mrożone", "Ćwiartka Mrożona"...)
    //
    // Pozostałe (poza prefixem 10.12) → pominięte (nie wchodzi do P-02 dla PKD 1012).
    // ════════════════════════════════════════════════════════════════════
    public static class PkwiuKlasyfikator
    {
        // Katalogi HANDEL.HM.TW (per BAZA_WIEDZY/23 sec.2):
        public const int KatalogZywiec      = 65882;
        public const int KatalogOdpady      = 67094;
        public const int KatalogMiesoSwieze = 67095;
        public const int KatalogMiesoInne   = 67104;
        public const int KatalogMiesoMrozne = 67153;

        // Standardowe nazwy PKWiU (wchodzą do P-02 jako display)
        public static readonly Dictionary<string, string> NazwyWyrobow = new(StringComparer.OrdinalIgnoreCase)
        {
            ["10.12.10-10"] = "Ptactwo gatunku Gallus Domesticus (kura domowa), całe, świeże lub schłodzone",
            ["10.12.10-50"] = "Kawałki z ptactwa gatunku Gallus Domesticus (kura domowa), świeże lub schłodzone",
            ["10.12.20-13"] = "Ptactwo gatunku Gallus Domesticus (kura domowa), całe, zamrożone",
            ["10.12.20-53"] = "Kawałki z ptactwa gatunku Gallus Domesticus (kura domowa), zamrożone"
        };

        // Stałe symbole klasyfikacyjne (porządek pozycji w sprawozdaniu)
        public static readonly string[] PkwiuKolejnosc = {
            "10.12.10-10",
            "10.12.10-50",
            "10.12.20-13",
            "10.12.20-53"
        };

        // ═══════════════════════════════════════════════════════════════════
        // Klasyfikuje towar w kontekście SPRZEDAŻY (faktury FVS/FKS)
        //   • Kurczak A → 10.12.10-10
        //   • Kurczak B → null (nie sprzedajemy — pomijamy)
        //   • element świeży (katalog ≠ 67153) → 10.12.10-50
        //   • element mrożony (katalog 67153) → 10.12.20-53
        //   • cały kurczak mrożony → 10.12.20-13
        // ═══════════════════════════════════════════════════════════════════
        public static string? KlasyfikujSprzedaz(string kod, string nazwa, int? katalog, string? swwBaza)
        {
            string k = (kod ?? "").Trim();
            string n = (nazwa ?? "").Trim();
            int kat = katalog ?? 0;

            // Kurczak A — jedyny pełny kurczak który sprzedajemy
            if (PasujeKurczak(k, "A") && !ZawieraMrozony(k, n))
                return "10.12.10-10";

            // Kurczak B — produkujemy ale NIE sprzedajemy (per ustaleniu)
            if (PasujeKurczak(k, "B") && !ZawieraMrozony(k, n))
                return null;

            // Cały kurczak mrożony (Kurczak A/B Mrożony)
            if (PasujeKurczak(k, null) && ZawieraMrozony(k, n))
                return "10.12.20-13";

            // Pozostałe — kawałki/elementy. Rozdziel po mrożeniu.
            if (kat == KatalogMiesoMrozne || ZawieraMrozony(k, n))
                return "10.12.20-53";

            if (kat == KatalogMiesoSwieze || kat == KatalogMiesoInne)
                return "10.12.10-50";

            // Fallback — użyj sww z bazy jeśli pasuje do schematu drobiu
            string norm = NormalizujPkwiu(swwBaza);
            if (!string.IsNullOrEmpty(norm) && norm.StartsWith("10.12", StringComparison.OrdinalIgnoreCase))
                return norm;

            return null;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Klasyfikuje towar w kontekście PRODUKCJI (dokumenty PWU/PWP/PWK)
        //   • Kurczak A LUB Kurczak B → 10.12.10-10 (oba liczymy w produkcji)
        //   • element świeży → 10.12.10-50
        //   • cały kurczak mrożony → 10.12.20-13
        //   • element mrożony → 10.12.20-53
        // ═══════════════════════════════════════════════════════════════════
        public static string? KlasyfikujProdukcje(string kod, string nazwa, int? katalog, string? swwBaza, string? typDokumentu)
        {
            string k = (kod ?? "").Trim();
            string n = (nazwa ?? "").Trim();
            int kat = katalog ?? 0;

            // Kurczak A lub B (świeży) — produkcja z PWU
            if (PasujeKurczak(k, null) && !ZawieraMrozony(k, n))
                return "10.12.10-10";

            // Cały kurczak mrożony
            if (PasujeKurczak(k, null) && ZawieraMrozony(k, n))
                return "10.12.20-13";

            // Element mrożony
            if (kat == KatalogMiesoMrozne || ZawieraMrozony(k, n))
                return "10.12.20-53";

            // Element świeży (zakładamy że PWP/PWK produkuje elementy)
            if (kat == KatalogMiesoSwieze || kat == KatalogMiesoInne)
                return "10.12.10-50";

            string norm = NormalizujPkwiu(swwBaza);
            if (!string.IsNullOrEmpty(norm) && norm.StartsWith("10.12", StringComparison.OrdinalIgnoreCase))
                return norm;

            return null;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Helpery
        // ═══════════════════════════════════════════════════════════════════

        // Czy kod to "Kurczak A" / "Kurczak B" (z dowolnym sufiksem typu "Mrożony")?
        // klasa = "A" lub "B" lub null (cokolwiek)
        private static bool PasujeKurczak(string kod, string? klasa)
        {
            if (string.IsNullOrEmpty(kod)) return false;
            // "Kurczak A", "Kurczak A Mrożony", "Kurczak B"
            string pattern = klasa == null
                ? @"^Kurczak\s+[AB](\s|$)"
                : $@"^Kurczak\s+{Regex.Escape(klasa)}(\s|$)";
            return Regex.IsMatch(kod, pattern, RegexOptions.IgnoreCase);
        }

        private static bool ZawieraMrozony(string kod, string nazwa)
        {
            string s = (kod ?? "") + " " + (nazwa ?? "");
            // "Mrożone", "Mrożony", "Mrożona" — dopasowanie z PL-diakrytykami
            return Regex.IsMatch(s, @"mro[żz]o", RegexOptions.IgnoreCase);
        }

        // Normalizacja PKWiU:
        //   "10.12.10.50"  → "10.12.10-50"
        //   "10.12.10-50 " → "10.12.10-50"
        public static string NormalizujPkwiu(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string s = raw.Trim();
            var m = Regex.Match(s, @"^(\d{2}\.\d{2}\.\d{2})\.(\d{1,2})$");
            if (m.Success)
                return $"{m.Groups[1].Value}-{m.Groups[2].Value.PadLeft(2, '0')}";
            return s;
        }
    }
}
