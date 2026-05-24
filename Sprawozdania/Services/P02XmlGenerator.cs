using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Kalendarz1.Sprawozdania.Models;

namespace Kalendarz1.Sprawozdania.Services
{
    // ════════════════════════════════════════════════════════════════════
    // Generator XML dla sprawozdania P-02 (wersja 16.0)
    //
    // Wzorzec strukturalny: BAZA_WIEDZY\GUS\spr_P-02_czerwiec_2023_..._.xml
    // Namespace: http://ps.stat.gov.pl/ps/schema/sprawozdanie
    //
    // Format wartości w atrybutach wartosc="":
    //   • Liczby całkowite, BEZ separatora tysięcy
    //   • BEZ części dziesiętnej (GUS oczekuje liczb całkowitych w P-02)
    //   • Puste pola: <Pole id="x"/> (bez atrybutu wartosc)
    //
    // Numerowanie indeks:
    //   • 1. pozycja:    <Pole id="d1r3" wartosc="X"/>           (brak atrybutu indeks)
    //   • 2. pozycja:    <Pole id="d1r3" indeks="1" wartosc="Y"/>
    //   • 3. pozycja:    <Pole id="d1r3" indeks="2" wartosc="Z"/>
    //   • i tak dalej
    // ════════════════════════════════════════════════════════════════════
    public class P02XmlGenerator
    {
        private static readonly XNamespace Ns = "http://ps.stat.gov.pl/ps/schema/sprawozdanie";
        private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

        private const string FormularzSymbol = "P-02";
        private const string FormularzWersja = "16.0";

        public XDocument Build(P02ReportData data, GusSettings cfg)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));

            var elementy = new XElement(Ns + "Elementy");

            // ═══════ Pola automatyczne (PS sam je uzupełnia przy imporcie, ale wpisujemy zgodnie z wzorcem) ═══════
            elementy.Add(PoleAuto("n_REGON", cfg.Regon));
            elementy.Add(PoleAuto("rok_badania", data.Rok.ToString(CultureInfo.InvariantCulture)));
            elementy.Add(PoleAuto("kontakt", "Joanna Kirowska tel 713716355 j.kirowska@stat.gov.pl"));
            elementy.Add(PoleAuto("won", "10"));    // wojewodztwo numer (10 = łódzkie — Piórkowscy)
            elementy.Add(PoleAuto("mc_badania", data.Miesiac.ToString(CultureInfo.InvariantCulture)));
            elementy.Add(PoleAuto("WYR", "0"));     // 0 = sprawozdanie składane (nie wykluczone)

            // ═══════ Osoba odpowiedzialna ═══════
            elementy.Add(Pole("oss_imie", cfg.OsobaImie));
            elementy.Add(Pole("oss_naz", cfg.OsobaNazwisko));
            elementy.Add(Pole("oss_tel", cfg.OsobaTelefon));
            elementy.Add(Pole("email_j", cfg.EmailJednostki));
            elementy.Add(Pole("email_o", cfg.EmailOsoby));

            // ═══════ Pytania kontrolne — zostawiamy domyślne (wartości z wzorca czerwiec 2023) ═══════
            elementy.Add(Pole("pyt1", "1"));
            elementy.Add(Pole("pyt2", "0"));
            elementy.Add(PoleEmpty("pyt2a"));
            elementy.Add(PoleEmpty("pyt3"));
            elementy.Add(PoleEmpty("pyt3a"));
            elementy.Add(PoleEmpty("pyt3b"));
            elementy.Add(PoleEmpty("pyt4"));
            elementy.Add(PoleEmpty("pyt4a"));

            // ═══════ Konfiguracja sekcji D1 (część produktowa) ═══════
            elementy.Add(Pole("rub", "1"));
            elementy.Add(PoleEmpty("pusty"));
            elementy.Add(Pole("RA_F", "01"));         // rodzaj aktywności / formularza
            elementy.Add(Pole("miesiac1", "1"));      // numer miesiąca raportowego w okresie

            int n = data.Pozycje.Count;

            // ═══════ d1_lp — numery porządkowe linii (1, 2, 3, ..., n) ═══════
            for (int i = 0; i < n; i++)
                elementy.Add(PoleAutoIdx("d1_lp", i, (i + 1).ToString(CultureInfo.InvariantCulture)));

            // ═══════ d1r3 — PKWiU per pozycja ═══════
            for (int i = 0; i < n; i++)
                elementy.Add(PoleIdx("d1r3", i, data.Pozycje[i].Pkwiu));

            // ═══════ d1r4 — jednostka miary per pozycja ═══════
            for (int i = 0; i < n; i++)
                elementy.Add(PoleIdx("d1r4", i, data.Pozycje[i].JednostkaKod));

            // ═══════ d1r5 — PRODUKCJA WYTWORZONA: w miesiącu sprawozdawczym (tony) ═══════
            for (int i = 0; i < n; i++)
                elementy.Add(PoleIdx("d1r5", i, FmtInt(data.Pozycje[i].ProdukcjaWMiesiacuTony)));

            // ═══════ d1r6 — PRODUKCJA WYTWORZONA: od początku roku do końca m-ca (tony) ═══════
            for (int i = 0; i < n; i++)
                elementy.Add(PoleIdx("d1r6", i, FmtInt(data.Pozycje[i].ProdukcjaOdPoczatkuRokuTony)));

            // ═══════ d1r7 — PRODUKCJA SPRZEDANA: w miesiącu sprawozdawczym (tony) ═══════
            for (int i = 0; i < n; i++)
                elementy.Add(PoleIdx("d1r7", i, FmtInt(data.Pozycje[i].SprzedazWMiesiacuTony)));

            // ═══════ d1r8 — PRODUKCJA SPRZEDANA: od początku roku do końca m-ca (tony) ═══════
            for (int i = 0; i < n; i++)
                elementy.Add(PoleIdx("d1r8", i, FmtInt(data.Pozycje[i].SprzedazOdPoczatkuRokuTony)));

            // ═══════ d1r9 — ZAPASY WYROBÓW na koniec okresu (tony) ═══════
            for (int i = 0; i < n; i++)
                elementy.Add(PoleIdx("d1r9", i, FmtInt(data.Pozycje[i].ZapasyWyrobowTony)));

            // ═══════ d1r10 — ZAPASY TOWARÓW na koniec okresu (tony) ═══════
            for (int i = 0; i < n; i++)
                elementy.Add(PoleIdx("d1r10", i, FmtInt(data.Pozycje[i].ZapasyTowarowTony)));

            // ═══════ CN — duplikat PKWiU (kod celny, w P-02 tożsamy z PKWiU) ═══════
            for (int i = 0; i < n; i++)
                elementy.Add(PoleIdx("CN", i, data.Pozycje[i].Pkwiu));

            // ═══════ Błędy kontroli (puste = brak błędów) ═══════
            elementy.Add(PoleEmpty("bladKL01"));
            elementy.Add(PoleEmpty("bladKL02"));

            // ═══════ PKD — przetwarzanie i konserwowanie mięsa z drobiu ═══════
            // (uwaga: w wzorcu jest spacja na końcu "1012 " — zachowujemy)
            elementy.Add(PoleAuto("pkdg", string.IsNullOrWhiteSpace(cfg.Pkd) ? "1012 " : cfg.Pkd.TrimEnd() + " "));

            // ═══════ Sekcje ukryte (kontrola spójności) ═══════
            elementy.Add(SekcjaUkryta("s_D0fex"));
            elementy.Add(SekcjaUkryta("s_D1Valid"));
            elementy.Add(SekcjaUkryta("s_bladKL01"));
            elementy.Add(SekcjaUkryta("s_RA_f"));

            // ═══════ MultiSekcja — informacja dla parsera ile pozycji ═══════
            elementy.Add(new XElement(Ns + "MultiSekcja",
                new XAttribute("iloscSekcji", n.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("id", "d1_1")));

            elementy.Add(SekcjaUkryta("s_D0"));

            // ═══════ Root <Sprawozdanie> ═══════
            var root = new XElement(Ns + "Sprawozdanie",
                new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName),
                new XAttribute(Xsi + "schemaLocation", "http://ps.stat.gov.pl/ps/schema/sprawozdanie sprawozdanie.xsd"),
                new XAttribute("formularzSymbol", FormularzSymbol),
                new XAttribute("formularzWersja", FormularzWersja),
                new XAttribute("numerSprawozdania", "0"),
                elementy);

            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                root);
        }

        // ════════════════════════════════════════════════════════════════
        // Helper'y do budowy <Pole/> z różnymi kombinacjami atrybutów
        // ════════════════════════════════════════════════════════════════

        private static XElement Pole(string id, string wartosc) =>
            new XElement(Ns + "Pole",
                new XAttribute("id", id),
                new XAttribute("wartosc", wartosc ?? ""));

        private static XElement PoleEmpty(string id) =>
            new XElement(Ns + "Pole",
                new XAttribute("id", id));

        private static XElement PoleAuto(string id, string wartosc) =>
            new XElement(Ns + "Pole",
                new XAttribute("automatyczne", "true"),
                new XAttribute("id", id),
                new XAttribute("wartosc", wartosc ?? ""));

        private static XElement PoleIdx(string id, int idx, string wartosc)
        {
            var el = new XElement(Ns + "Pole", new XAttribute("id", id));
            if (idx > 0) el.Add(new XAttribute("indeks", idx.ToString(CultureInfo.InvariantCulture)));
            el.Add(new XAttribute("wartosc", wartosc ?? ""));
            return el;
        }

        private static XElement PoleAutoIdx(string id, int idx, string wartosc)
        {
            var el = new XElement(Ns + "Pole",
                new XAttribute("automatyczne", "true"),
                new XAttribute("id", id));
            if (idx > 0) el.Add(new XAttribute("indeks", idx.ToString(CultureInfo.InvariantCulture)));
            el.Add(new XAttribute("wartosc", wartosc ?? ""));
            return el;
        }

        private static XElement SekcjaUkryta(string id) =>
            new XElement(Ns + "Sekcja",
                new XAttribute("id", id),
                new XAttribute("widoczna", "false"));

        // P-02 oczekuje liczb całkowitych w atrybutach wartosc
        private static string FmtInt(decimal v) =>
            Math.Round(v, 0, MidpointRounding.AwayFromZero)
                .ToString("0", CultureInfo.InvariantCulture);

        // Nazwa pliku w stylu Symfonii: spr_P-02_kwiecien_2026_75004547600000_XXXXX.xml
        public static string ProponowanaNazwaPliku(int rok, int miesiac, string regon, string? salt = null)
        {
            string[] mce = {
                "", "styczen", "luty", "marzec", "kwiecien", "maj", "czerwiec",
                "lipiec", "sierpien", "wrzesien", "pazdziernik", "listopad", "grudzien"
            };
            string mcName = (miesiac >= 1 && miesiac <= 12) ? mce[miesiac] : $"mc{miesiac}";
            string saltPart = string.IsNullOrEmpty(salt) ? Guid.NewGuid().ToString("N").Substring(0, 8) : salt;
            return $"spr_P-02_{mcName}_{rok}_{regon}_{saltPart}.xml";
        }
    }
}
