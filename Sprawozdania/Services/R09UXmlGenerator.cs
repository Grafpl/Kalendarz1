using System;
using System.Globalization;
using System.Xml.Linq;
using Kalendarz1.Sprawozdania.Models;

namespace Kalendarz1.Sprawozdania.Services
{
    // ════════════════════════════════════════════════════════════════════
    // R-09U — Generator XML (1:1 z formatem Sage Symfonia v2.0)
    // Wzorzec: BAZA_WIEDZY/GUS/spr_R-09U_wzorzec_2026.xml
    //
    // Struktura:
    //   Pola automatyczne: regon, woj, email_o, email_j, RA_F=01
    //   Pola statyczne: p1, n_ubm, p1a="1", n_ubma="X"
    //   Dział 1   D1   22 wiersze × 5 kolumn  (w01..w22, r1..r5)
    //   Dział 1A  D1A  19 wierszy × 5 kolumn  (id "d1aw01r1" itp.)
    //   Dział 2   D2   22 wiersze × 5 kolumn
    //   Dział 3   D3   10 wierszy × 6 kolumn  (r1..r6)
    //   Sekcje ukryte: s_s1, s_s2, s_s1a, s00, s0, s0a, s1_w01..w22, s1a_w01..w19,
    //                  s2_w01..w22  (statyczna lista z wzorca)
    //
    // PIÓROSKOWSCY: wypełniony jest tylko wiersz w14 (brojlery kurze) w D1
    // (i opcjonalnie w D2 dla ubojów zleconych — domyślnie wszystko 0).
    // ════════════════════════════════════════════════════════════════════
    public class R09UXmlGenerator
    {
        private static readonly XNamespace Ns = "http://ps.stat.gov.pl/ps/schema/sprawozdanie";
        private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

        private const string FormularzSymbol = "R-09U";
        // Wersja brana z GusSettings.R09UFormularzWersja (default 2.0 dla 2026)

        public XDocument Build(R09UReportData data, GusSettings cfg)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));

            var elementy = new XElement(Ns + "Elementy");

            // ═══════ Pola automatyczne (z wzorca) ═══════
            elementy.Add(PoleAuto("regon", cfg.Regon));
            elementy.Add(PoleAuto("woj", "10"));    // łódzkie
            elementy.Add(PoleAuto("email_o", cfg.EmailOsoby));
            elementy.Add(PoleAuto("email_j", cfg.EmailJednostki));
            elementy.Add(PoleAuto("RA_F", "01"));

            // ═══════ Pola D1 ═══════
            elementy.Add(PoleEmpty("p1"));
            elementy.Add(PoleAutoEmpty("n_ubm"));

            // Dział 1: 22 wiersze × 5 kolumn
            EmitujDzial(elementy, "d1w", "r", maxWiersz: 22, maxKol: 5, dane: data.Dzial1);

            // ═══════ Pola D1A ═══════
            elementy.Add(Pole("p1a", "1"));
            elementy.Add(PoleAuto("n_ubma", "X"));

            // Dział 1A: 19 wierszy × 5 kolumn (zwierzęta z importu — zwykle 0)
            EmitujDzial(elementy, "d1aw", "r", maxWiersz: 19, maxKol: 5, dane: null);

            // ═══════ Dział 2: ubój zlecony 22×5 ═══════
            EmitujDzial(elementy, "d2w", "r", maxWiersz: 22, maxKol: 5, dane: data.Dzial2);

            // ═══════ Dział 3: produkcja mięsa 10×6 ═══════
            EmitujDzial(elementy, "d3w", "r", maxWiersz: 10, maxKol: 6, dane: null);

            // ═══════ Sekcje ukryte (lista 1:1 z wzorca) ═══════
            DodajSekcjeUkryte(elementy);

            // ═══════ Root ═══════
            string wersja = string.IsNullOrWhiteSpace(cfg.R09UFormularzWersja) ? "2.0" : cfg.R09UFormularzWersja;
            var root = new XElement(Ns + "Sprawozdanie",
                new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName),
                new XAttribute(Xsi + "schemaLocation", "http://ps.stat.gov.pl/ps/schema/sprawozdanie sprawozdanie.xsd"),
                new XAttribute("formularzSymbol", FormularzSymbol),
                new XAttribute("formularzWersja", wersja),
                new XAttribute("numerSprawozdania", "0"),
                elementy);

            return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        }

        // Emituje wszystkie wiersze × kolumny danej sekcji. Default wartość = "0".
        // Jeśli `dane` zawiera P02Pozycja dla danego wiersza → wstawia realne wartości.
        private static void EmitujDzial(XElement elementy, string prefixWiersz, string prefixKol,
            int maxWiersz, int maxKol, System.Collections.Generic.List<R09UPozycja>? dane)
        {
            var slownik = new System.Collections.Generic.Dictionary<int, R09UPozycja>();
            if (dane != null)
                foreach (var p in dane)
                    if (!p.JestPusta) slownik[p.WierszNumer] = p;

            for (int w = 1; w <= maxWiersz; w++)
            {
                slownik.TryGetValue(w, out var pozycja);
                for (int k = 1; k <= maxKol; k++)
                {
                    string id = $"{prefixWiersz}{w:D2}{prefixKol}{k}";
                    string wartosc = pozycja == null
                        ? "0"
                        : WartoscKolumny(pozycja, k);
                    elementy.Add(Pole(id, wartosc));
                }
            }
        }

        // Mapuje r1..r6 na właściwości R09UPozycja
        private static string WartoscKolumny(R09UPozycja p, int kolumna) => kolumna switch
        {
            1 => FmtInt(p.LiczbaSztuk),
            2 => FmtInt(p.WagaZywaKg),
            3 => FmtInt(p.WagaPoubojowaBruttoKg),
            4 => FmtInt(p.WagaHandlowaNettoKg),
            5 => FmtInt(p.WartoscZl),
            _ => "0"
        };

        // ════════════════════════════════════════════════════════════════
        // Sekcje ukryte (lista 1:1 z wzorca XML 2026-05-23)
        // ════════════════════════════════════════════════════════════════
        private static void DodajSekcjeUkryte(XElement elementy)
        {
            string[] sekcje = {
                "s_s1", "s_s2", "s_s1a", "s00", "s0", "s0a",
                "s1_w01","s1_w02","s1_w03","s1_w04","s1_w05","s1_w06","s1_w07","s1_w08",
                "s1_w09","s1_w10","s1_w11","s1_w12","s1_w13","s1_w14","s1_w15","s1_w16",
                "s1_w17","s1_w18","s1_w19","s1_w20","s1_w21","s1_w22",
                "s1a_w01","s1a_w02","s1a_w03","s1a_w04","s1a_w05","s1a_w06","s1a_w07","s1a_w08",
                "s1a_w09","s1a_w10","s1a_w11","s1a_w12","s1a_w13","s1a_w14","s1a_w15","s1a_w16",
                "s1a_w17","s1a_w18","s1a_w19",
                "s2_w01","s2_w02","s2_w03","s2_w04","s2_w05","s2_w06","s2_w07","s2_w08",
                "s2_w09","s2_w10","s2_w11","s2_w12","s2_w13","s2_w14","s2_w15","s2_w16",
                "s2_w17","s2_w18","s2_w19","s2_w20","s2_w21","s2_w22"
            };
            foreach (var s in sekcje)
                elementy.Add(new XElement(Ns + "Sekcja",
                    new XAttribute("id", s),
                    new XAttribute("widoczna", "false")));
        }

        // ════════════════════════════════════════════════════════════════
        // Helper'y XML
        // ════════════════════════════════════════════════════════════════

        private static XElement Pole(string id, string wartosc) =>
            new XElement(Ns + "Pole",
                new XAttribute("id", id),
                new XAttribute("wartosc", wartosc ?? ""));

        private static XElement PoleEmpty(string id) =>
            new XElement(Ns + "Pole", new XAttribute("id", id));

        private static XElement PoleAuto(string id, string wartosc) =>
            new XElement(Ns + "Pole",
                new XAttribute("automatyczne", "true"),
                new XAttribute("id", id),
                new XAttribute("wartosc", wartosc ?? ""));

        private static XElement PoleAutoEmpty(string id) =>
            new XElement(Ns + "Pole",
                new XAttribute("automatyczne", "true"),
                new XAttribute("id", id));

        private static string FmtInt(decimal v) =>
            Math.Round(v, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);

        private static string FmtInt(int v) => v.ToString("0", CultureInfo.InvariantCulture);

        public static string ProponowanaNazwaPliku(int rok, int miesiac, string regon, string? salt = null)
        {
            string[] mce = {
                "", "styczen","luty","marzec","kwiecien","maj","czerwiec",
                "lipiec","sierpien","wrzesien","pazdziernik","listopad","grudzien"
            };
            string mc = (miesiac >= 1 && miesiac <= 12) ? mce[miesiac] : $"mc{miesiac}";
            string saltPart = string.IsNullOrEmpty(salt) ? Guid.NewGuid().ToString("N").Substring(0, 8) : salt;
            return $"spr_R-09U_{mc}_{rok}_{regon}_{saltPart}.xml";
        }
    }
}
