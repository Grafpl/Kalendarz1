using System;
using System.Collections.Generic;
using System.Linq;

namespace Kalendarz1.AnalitykaPelna.Models
{
    /// <summary>
    /// Stan magazynu w okresie: ile weszło, ile wyszło, czy się wyzerował.
    /// Wykorzystywane w Bilansie materiałowym (zakładka "Stan magazynów").
    /// </summary>
    public class StanMagazynu
    {
        public int MagazynID { get; set; }
        public string MagazynNazwa { get; set; } = "";
        public string MagazynPelnaNazwa { get; set; } = "";
        public string MagazynKolorHex { get; set; } = "#94A3B8";

        public decimal PrzychodKg { get; set; }    // sumarycznie wszystko co weszło (PZ, PWU, PWP, sMM+, MP, sPKM in)
        public decimal RozchodKg { get; set; }     // sumarycznie wszystko co wyszło (WZ, RWU, RWP, sMM-, MW, sPKM out)
        public int LiczbaDokumentow { get; set; }

        public List<StanMagazynuSeria> RozkladSerii { get; set; } = new();

        // ─── Pola wyliczane ────────────────────────────────────────────────

        /// <summary>Saldo netto = Przychód − Rozchód. Idealnie = 0 dla magazynów świeżych (bez zapasu).</summary>
        public decimal Saldo => PrzychodKg - RozchodKg;

        /// <summary>% odchylenia salda względem przychodu (|saldo|/przychod × 100).</summary>
        public decimal SaldoProcent => PrzychodKg > 0 ? System.Math.Abs(Saldo) / PrzychodKg * 100m : 0;

        /// <summary>Czy magazyn jest wyzerowany — saldo &lt; 1% przychodu (tolerancja).</summary>
        public bool CzyWyzerowany => SaldoProcent < 1m;

        /// <summary>Status tekstowy.</summary>
        public string Status =>
            PrzychodKg <= 0 && RozchodKg <= 0 ? "— Brak ruchu" :
            CzyWyzerowany ? "✓ Wyzerowany" :
            SaldoProcent < 5m ? "✓ OK" :
            SaldoProcent < 20m ? "⚠ Saldo" :
            "❌ Duże saldo";

        /// <summary>Kolor statusu (do badge'a).</summary>
        public string StatusKolorHex =>
            PrzychodKg <= 0 && RozchodKg <= 0 ? "#94A3B8" :
            CzyWyzerowany ? "#10B981" :
            SaldoProcent < 5m ? "#10B981" :
            SaldoProcent < 20m ? "#F59E0B" :
            "#DC2626";

        public string KierunekIkona => Saldo > 0 ? "⬆" : Saldo < 0 ? "⬇" : "≈";

        /// <summary>Suma działalności = Przychód + Rozchód (do sortowania top-magazynów).</summary>
        public decimal AktywnoscKg => PrzychodKg + RozchodKg;

        public string SaldoOpis =>
            PrzychodKg <= 0 && RozchodKg <= 0 ? "Brak ruchu w okresie" :
            CzyWyzerowany ? "Magazyn wyzerowany — wszystko co weszło, wyszło" :
            Saldo > 0 ? $"Pozostało na magazynie: {Saldo:N0} kg ({SaldoProcent:F1}% przychodu)" :
            $"Wyszło więcej niż weszło o {System.Math.Abs(Saldo):N0} kg ({SaldoProcent:F1}%)";
    }

    /// <summary>Pojedyncza seria w magazynie z kierunkiem (IN/OUT).</summary>
    public class StanMagazynuSeria
    {
        public string Seria { get; set; } = "";
        public string Kierunek { get; set; } = "";   // "IN" lub "OUT"
        public decimal Kg { get; set; }
        public int LiczbaDok { get; set; }
        public string OpisSerii { get; set; } = ""; // np. "PZ — przyjęcie zewnętrzne"

        public string KierunekIkona => Kierunek == "IN" ? "⬇" : "⬆";
        public string KierunekKolor => Kierunek == "IN" ? "#10B981" : "#DC2626";
    }

    /// <summary>Pojedynczy przepływ między magazynami (z dokumentów MM-).</summary>
    public class PrzeplywMagazynow
    {
        public int MagazynZId { get; set; }
        public string MagazynZNazwa { get; set; } = "";
        public int MagazynDoId { get; set; }
        public string MagazynDoNazwa { get; set; } = "";
        public decimal Kg { get; set; }
        public int LiczbaDok { get; set; }

        public string Etykieta => $"{MagazynZNazwa}  →  {MagazynDoNazwa}";
        public string KgFormatted => $"{Kg:N0} kg";
        public string DokFormatted => LiczbaDok == 1 ? "1 dok." : $"{LiczbaDok:N0} dok.";
    }

    /// <summary>
    /// Towar wyprodukowany w fabryce — pojawił się w jakimś dokumencie produkcji
    /// (PWU/PWP/PPM/PPK). Pokazuje pełen cykl: produkcja → zużycie → sprzedaż.
    /// </summary>
    public class TowarProdukcyjny
    {
        public string Kod { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public int Katalog { get; set; }
        public string Kategoria { get; set; } = "";
        public string KategoriaIkona { get; set; } = "";
        public string KategoriaKolor { get; set; } = "#94A3B8";

        // Wyprodukowano (PWU + PWP + PPM + PPK)
        public decimal WyprodukowanoKg { get; set; }
        public int LiczbaDokProdukcji { get; set; }
        public string NumeryDokProdukcji { get; set; } = "";

        // Zużyto wewnętrznie (RWP + RWU + RPM + RPK = poszło do dalszego krojenia/produkcji)
        public decimal ZuzytoKg { get; set; }
        public int LiczbaDokZuzycia { get; set; }
        public string NumeryDokZuzycia { get; set; } = "";

        // Sprzedano (WZ + WZ-W + WZK)
        public decimal SprzedanoKg { get; set; }
        public int LiczbaDokSprzedazy { get; set; }
        public string NumeryDokSprzedazy { get; set; } = "";

        // Przesunięcia MM- (gdzie poszły) — dokąd / ile
        public List<TowarPrzeplyw> Przeplywy { get; set; } = new();

        // ─── Pola wyliczane ──────────────────────────────────────────────

        public decimal SaldoKg => WyprodukowanoKg - ZuzytoKg - SprzedanoKg;
        public decimal SaldoProcent => WyprodukowanoKg > 0
            ? System.Math.Abs(SaldoKg) / WyprodukowanoKg * 100m : 0;
        public bool CzyZbalansowane => SaldoProcent < 5m;

        public string Status =>
            WyprodukowanoKg <= 0 ? "—" :
            CzyZbalansowane ? "✓ Bilansuje" :
            SaldoProcent < 20m ? "⚠ Saldo" :
            "❌ Duże saldo";

        public string StatusKolor =>
            WyprodukowanoKg <= 0 ? "#94A3B8" :
            CzyZbalansowane ? "#10B981" :
            SaldoProcent < 20m ? "#F59E0B" :
            "#DC2626";

        public string SaldoIkona => SaldoKg > 0 ? "⬆" : SaldoKg < 0 ? "⬇" : "≈";

        public string SaldoOpis =>
            WyprodukowanoKg <= 0 ? "Brak produkcji w okresie" :
            CzyZbalansowane ? "Towar w pełni rozdysponowany" :
            SaldoKg > 0 ? $"+{SaldoKg:N0} kg ({SaldoProcent:F1}%) — pozostało na magazynie" :
            $"{SaldoKg:N0} kg ({SaldoProcent:F1}%) — wydano więcej niż wyprodukowano";

        public string KategoriaIkonaPelna => Kategoria switch
        {
            "Mięso" => "🥩",
            "Mrożone" => "❄️",
            "Odpady" => "🗑",
            "Żywy" => "🐔",
            _ => "📦"
        };

        /// <summary>Ścieżka do zdjęcia towaru (jpg/png z Assets/Towary/{kod}.jpg).
        /// Jeśli plik nie istnieje, zostanie wyświetlona ikona kategorii jako fallback.</summary>
        public string? ZdjecieSciezka { get; set; }

        /// <summary>True jeśli zdjęcie istnieje na dysku — używane do przełączania widoczności.</summary>
        public bool MaZdjecie => !string.IsNullOrEmpty(ZdjecieSciezka)
                                 && System.IO.File.Exists(ZdjecieSciezka);
    }

    /// <summary>Pojedynczy przepływ MM- towaru: skąd → dokąd.</summary>
    public class TowarPrzeplyw
    {
        public int MagazynZId { get; set; }
        public string MagazynZNazwa { get; set; } = "";
        public int MagazynDoId { get; set; }
        public string MagazynDoNazwa { get; set; } = "";
        public decimal Kg { get; set; }
        public int LiczbaDok { get; set; }
        public string NumeryDok { get; set; } = "";
    }

    /// <summary>
    /// Główna oś produkcyjna: ŻYWIEC → UBÓJ → PRODUKCJA → DYSTRYBUCJA → KLIENCI
    /// Plus odgałęzienia (mroźnia, karma, odpady).
    /// Liczy też wydajności i straty per etap.
    /// </summary>
    public class FlowChainSummary
    {
        public FlowChainNode Zywiec { get; set; } = new() { Etap = "ŻYWIEC", Ikona = "🐔", Kolor = "#F59E0B" };
        public FlowChainNode Uboj { get; set; } = new() { Etap = "UBÓJ", Ikona = "⚙", Kolor = "#DC2626" };
        public FlowChainNode Produkcja { get; set; } = new() { Etap = "PRODUKCJA", Ikona = "🔪", Kolor = "#7C3AED" };
        public FlowChainNode Dystrybucja { get; set; } = new() { Etap = "DYSTRYBUCJA", Ikona = "📦", Kolor = "#2563EB" };
        public FlowChainNode Klienci { get; set; } = new() { Etap = "KLIENCI", Ikona = "🚚", Kolor = "#10B981" };

        // Odgałęzienia (z PRODUKCJI lub po DYSTRYBUCJI — wizualnie pod DYST)
        public FlowChainNode Mroznia { get; set; } = new() { Etap = "MROŹNIA", Ikona = "❄", Kolor = "#0EA5E9" };
        public FlowChainNode Karma { get; set; } = new() { Etap = "KARMA", Ikona = "🌾", Kolor = "#CA8A04" };
        public FlowChainNode Odpady { get; set; } = new() { Etap = "ODPADY", Ikona = "🗑", Kolor = "#94A3B8" };
        public FlowChainNode Masarnia { get; set; } = new() { Etap = "MASARNIA", Ikona = "🥓", Kolor = "#9A3412" };

        // Rozchód do krojenia (sRWP) — input do produkcji
        public FlowChainNode RozchodKrojenia { get; set; } = new() { Etap = "RW PROD", Ikona = "🔪", Kolor = "#7C3AED" };

        // ─── Wydajności i straty (wyliczane) ─────────────────────────────

        /// <summary>Wydajność uboju = Tuszki+Podroby+Odpady / Żywiec × 100% (norma ~85% z podrobami).</summary>
        public decimal WydajnoscUbojuProc => Zywiec.Kg > 0 ? Uboj.Kg / Zywiec.Kg * 100m : 0;

        /// <summary>Wydajność krojenia = sPWP (przychód) / sRWP (rozchód do krojenia) × 100% (norma ~62%).
        /// Używa rzeczywistego rozchodu sRWP zamiast pełnego Uboj.Kg (bo nie wszystko z UBOJ idzie do krojenia).</summary>
        public decimal WydajnoscKrojeniaProc => RozchodKrojenia.Kg > 0 ? Produkcja.Kg / RozchodKrojenia.Kg * 100m : 0;

        /// <summary>Strata krojenia w kg = sRWP - sPWP (kości, ścinki, ubytki).</summary>
        public decimal StrataKrojeniaKg => RozchodKrojenia.Kg - Produkcja.Kg;
        public decimal StrataKrojeniaProc => RozchodKrojenia.Kg > 0 ? StrataKrojeniaKg / RozchodKrojenia.Kg * 100m : 0;

        /// <summary>% przepływu do dystrybucji vs produkcja całkowita.</summary>
        public decimal ProcDoDystProc => Produkcja.Kg > 0 ? Dystrybucja.Kg / Produkcja.Kg * 100m : 0;
        public decimal ProcDoMrozniProc => Produkcja.Kg > 0 ? Mroznia.Kg / Produkcja.Kg * 100m : 0;
        public decimal ProcDoKarmyProc => Produkcja.Kg > 0 ? Karma.Kg / Produkcja.Kg * 100m : 0;
        public decimal ProcDoOdpadowProc => Produkcja.Kg > 0 ? Odpady.Kg / Produkcja.Kg * 100m : 0;

        /// <summary>% sprzedaży vs dystrybucja.</summary>
        public decimal ProcSprzedanoProc => Dystrybucja.Kg > 0 ? Klienci.Kg / Dystrybucja.Kg * 100m : 0;

        /// <summary>% udziału masarni w produkcji.</summary>
        public decimal ProcDoMasarniProc => Produkcja.Kg > 0 ? Masarnia.Kg / Produkcja.Kg * 100m : 0;

        /// <summary>Łączny rozchód z PRODUKCJI we wszystkich kierunkach (suma 5 magazynów).</summary>
        public decimal RazemRozchoduKg =>
            Dystrybucja.Kg + Mroznia.Kg + Masarnia.Kg + Karma.Kg + Odpady.Kg;

        /// <summary>% łącznego rozchodu vs wyprodukowane.</summary>
        public decimal RazemRozchoduProc => Produkcja.Kg > 0 ? RazemRozchoduKg / Produkcja.Kg * 100m : 0;

        /// <summary>Pozostało w magazynie PROD (wyprodukowano - rozchodowano). Może być ujemne jeśli
        /// w okresie rozchodowano towar wyprodukowany wcześniej.</summary>
        public decimal ZostaloProdKg => Produkcja.Kg - RazemRozchoduKg;

        /// <summary>% pozostałego w magazynie PROD vs wyprodukowane.</summary>
        public decimal ZostaloProdProc => Produkcja.Kg > 0 ? ZostaloProdKg / Produkcja.Kg * 100m : 0;

        /// <summary>Strata na uboju (różnica żywiec - wyjście uboju, czyli pióra/krew/woda).</summary>
        public decimal StratyUbojuKg => Zywiec.Kg - Uboj.Kg;
        public decimal StratyUbojuProc => Zywiec.Kg > 0 ? StratyUbojuKg / Zywiec.Kg * 100m : 0;

        /// <summary>Łączna liczba dokumentów Symfonii w łańcuchu.</summary>
        public int LiczbaDokumentowCalkowita =>
            Zywiec.LiczbaDok + Uboj.LiczbaDok + Produkcja.LiczbaDok +
            Dystrybucja.LiczbaDok + Klienci.LiczbaDok +
            Mroznia.LiczbaDok + Karma.LiczbaDok + Odpady.LiczbaDok;

        // ─── Statusy z normami (do badge'ów) ─────────────────────────────

        public string WydajnoscUbojuStatus =>
            Zywiec.Kg <= 0 ? "—" :
            WydajnoscUbojuProc >= 80m ? "✓ OK" :
            WydajnoscUbojuProc >= 70m ? "⚠ Niska" : "❌ Bardzo niska";

        public string WydajnoscUbojuKolor =>
            Zywiec.Kg <= 0 ? "#94A3B8" :
            WydajnoscUbojuProc >= 80m ? "#10B981" :
            WydajnoscUbojuProc >= 70m ? "#F59E0B" : "#DC2626";

        public string WydajnoscKrojeniaStatus =>
            Uboj.Kg <= 0 ? "—" :
            WydajnoscKrojeniaProc >= 55m ? "✓ OK" :
            WydajnoscKrojeniaProc >= 45m ? "⚠ Niska" : "❌ Bardzo niska";

        public string WydajnoscKrojeniaKolor =>
            Uboj.Kg <= 0 ? "#94A3B8" :
            WydajnoscKrojeniaProc >= 55m ? "#10B981" :
            WydajnoscKrojeniaProc >= 45m ? "#F59E0B" : "#DC2626";

        public string StratyKolor =>
            Zywiec.Kg <= 0 ? "#94A3B8" :
            StratyUbojuProc <= 18m ? "#10B981" :
            StratyUbojuProc <= 25m ? "#F59E0B" : "#DC2626";

        // ─── Dodatkowe statusy do audytu ───────────────────────────────────

        /// <summary>Status odpadów wobec normy 3-5%.</summary>
        public string OdpadyStatus =>
            Produkcja.Kg <= 0 ? "—" :
            ProcDoOdpadowProc <= 5m ? "✓ OK" :
            ProcDoOdpadowProc <= 8m ? "⚠ Wysokie" : "🚨 Bardzo wysokie";

        public string OdpadyKolor =>
            Produkcja.Kg <= 0 ? "#94A3B8" :
            ProcDoOdpadowProc <= 5m ? "#10B981" :
            ProcDoOdpadowProc <= 8m ? "#F59E0B" : "#DC2626";

        /// <summary>Status pozostałości w PROD (norma <10% = zdrowa rotacja).</summary>
        public string ZostaloStatus =>
            Produkcja.Kg <= 0 ? "—" :
            ZostaloProdKg < 0 ? "❌ Ujemne (z zapasu)" :
            ZostaloProdProc < 10m ? "✓ OK" :
            ZostaloProdProc < 25m ? "⚠ Zalega" : "🚨 Duża stagnacja";

        public string ZostaloKolor =>
            Produkcja.Kg <= 0 ? "#94A3B8" :
            ZostaloProdKg < 0 ? "#DC2626" :
            ZostaloProdProc < 10m ? "#10B981" :
            ZostaloProdProc < 25m ? "#F59E0B" : "#DC2626";

        /// <summary>Status sprzedaży z DYST do klientów (norma >90%).</summary>
        public string SprzedazStatus =>
            Dystrybucja.Kg <= 0 ? "—" :
            ProcSprzedanoProc >= 90m ? "✓ Świetna rotacja" :
            ProcSprzedanoProc >= 70m ? "⚠ Średnia rotacja" : "🚨 Niska rotacja";

        public string SprzedazKolor =>
            Dystrybucja.Kg <= 0 ? "#94A3B8" :
            ProcSprzedanoProc >= 90m ? "#10B981" :
            ProcSprzedanoProc >= 70m ? "#F59E0B" : "#DC2626";

        // ─── Mass balance check (bilans masy fizycznej) ────────────────────

        /// <summary>Bilans masy — różnica między tym co przyjęto a co rozchodowano (Σ przepływów wewnętrznych).
        /// Jeśli żywiec wszedł i wszystko zostało udokumentowane, suma powinna się zamykać.
        /// Wzór: Żywiec - Ubój = Strata uboju (pióra/krew/woda — szacunkowo).
        /// Sprawdzamy też: Ubój ≈ Sprzedaż bezpośrednia + Wsad krojenia + co zostało w UBOJ.</summary>
        public decimal BilansMasyRoznicaKg => StratyUbojuKg;
        public decimal BilansMasyRoznicaProc => StratyUbojuProc;

        /// <summary>Strata uboju w typowym zakresie 12-20% to OK (pióra ~7%, krew ~3%, woda ~4%, jelita ~3%).</summary>
        public bool BilansMasyOk =>
            Zywiec.Kg <= 0 || (StratyUbojuProc >= 10m && StratyUbojuProc <= 22m);

        public string BilansMasyStatus =>
            Zywiec.Kg <= 0 ? "—" :
            BilansMasyOk ? "✓ Bilans w normie (10-22%)" :
            StratyUbojuProc < 10m ? "⚠ Zbyt mała strata (sprawdź dokumenty)" :
            "⚠ Zbyt duża strata — możliwe wycieki";

        public string BilansMasyKolor =>
            Zywiec.Kg <= 0 ? "#94A3B8" :
            BilansMasyOk ? "#10B981" : "#F59E0B";

        // ─── Sprzedaż bezpośrednia z UBÓJ vs przez krojenie ────────────────

        /// <summary>Ile z UBOJ nie poszło na krojenie (sRWP) — czyli prawdopodobnie poszło na sprzedaż lub do MROŹ jako całe tuszki.</summary>
        public decimal UbojBezKrojeniaKg => Math.Max(0m, Uboj.Kg - RozchodKrojenia.Kg);
        public decimal UbojBezKrojeniaProc => Uboj.Kg > 0 ? UbojBezKrojeniaKg / Uboj.Kg * 100m : 0;

        /// <summary>% UBOJ który poszedł na krojenie (sRWP).</summary>
        public decimal UbojDoKrojeniaProc => Uboj.Kg > 0 ? RozchodKrojenia.Kg / Uboj.Kg * 100m : 0;

        // ─── Łączna ocena — ile alarmów ────────────────────────────────────

        public int LiczbaAlarmow
        {
            get
            {
                int n = 0;
                if (Zywiec.Kg > 0 && WydajnoscUbojuProc < 80m) n++;
                if (Uboj.Kg > 0 && WydajnoscKrojeniaProc < 55m) n++;
                if (Produkcja.Kg > 0 && ProcDoOdpadowProc > 5m) n++;
                if (Produkcja.Kg > 0 && ZostaloProdProc > 15m) n++;
                if (Dystrybucja.Kg > 0 && ProcSprzedanoProc < 70m) n++;
                if (!BilansMasyOk && Zywiec.Kg > 0) n++;
                return n;
            }
        }

        public string OcenaOgolnaStatus =>
            LiczbaAlarmow == 0 ? "✓ Wszystko w normie" :
            LiczbaAlarmow == 1 ? "⚠ 1 wskaźnik do sprawdzenia" :
            $"🚨 {LiczbaAlarmow} alarmów — wymaga uwagi";

        public string OcenaOgolnaKolor =>
            LiczbaAlarmow == 0 ? "#10B981" :
            LiczbaAlarmow <= 2 ? "#F59E0B" : "#DC2626";
    }

    public class FlowChainNode
    {
        public string Etap { get; set; } = "";
        public string Ikona { get; set; } = "";
        public string Kolor { get; set; } = "#94A3B8";
        public decimal Kg { get; set; }
        public int LiczbaDok { get; set; }
        public string KgFormatted => Kg <= 0 ? "—" : $"{Kg:N0}";
        public string DokFormatted => LiczbaDok <= 0 ? "" : LiczbaDok == 1 ? "1 dok." : $"{LiczbaDok:N0} dok.";
    }

    /// <summary>Helper do mapowania serii Symfonii na opis.</summary>
    public static class SeriaSymfoniaHelper
    {
        public static bool JestPrzychodem(string seria)
        {
            string s = (seria ?? "").TrimStart('s').Trim();
            return s switch
            {
                "PZ" or "PZK" or "PZH" or "PZ-W" or
                "PWU" or "PWP" or "PPM" or "PPK" or
                "PKM" or "PRK" or "PrW" or
                "MM+" or "MP" => true,
                _ => false
            };
        }

        public static bool JestRozchodem(string seria) => !JestPrzychodem(seria);

        public static string Opis(string seria)
        {
            string s = (seria ?? "").TrimStart('s').Trim();
            return s switch
            {
                "PZ" => "Przyjęcie zewnętrzne",
                "PZK" => "Przyjęcie korekta",
                "PZH" => "Przyjęcie z handlu (zwrot)",
                "PWU" => "Przychód wewnętrzny — ubój",
                "PWP" => "Przychód wewnętrzny — produkcja",
                "PPM" => "Przychód wewnętrzny — masarnia",
                "PPK" => "Przychód produkcja karmy",
                "PKM" => "Przychód korekcyjny magazynowy",
                "PrW" => "Przyjęcie wewnętrzne",
                "PRK" => "Przychód karmy",
                "MM+" => "Przesunięcie międzymagazynowe (przychód)",
                "MP" => "Przyjęcie do magazynu opakowań",
                "WZ" => "Wydanie z magazynu",
                "WZK" => "Wydanie korekta",
                "WZ-W" or "WZKW" => "Wydanie wewnętrzne",
                "RWU" => "Rozchód wewnętrzny — ubój",
                "RWP" => "Rozchód wewnętrzny — produkcja",
                "RPM" => "Rozchód wewnętrzny — masarnia",
                "RPK" => "Rozchód produkcja karmy",
                "RWO" => "Rozchód operacyjny",
                "MM-" => "Przesunięcie międzymagazynowe (rozchód)",
                "MW" => "Wydanie z magazynu opakowań",
                _ => seria ?? ""
            };
        }
    }
}
