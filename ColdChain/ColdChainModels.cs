using System;
using System.Collections.Generic;
using System.Linq;

namespace Kalendarz1.ColdChain
{
    /// <summary>
    /// Norma temperatury z QC_Normy (Kategoria='TEMPERATURA').
    /// Min/Max mogą być NULL (brak ograniczenia z danej strony).
    /// </summary>
    public class TempNorma
    {
        public int Id { get; set; }
        public string Nazwa { get; set; } = "";       // TempRampa / TempChillera / TempTunel
        public string? Opis { get; set; }
        public decimal? Min { get; set; }
        public decimal? Max { get; set; }
        public string Jednostka { get; set; } = "C";

        /// <summary>Miejsce w TemperaturyMiejsca które ta norma opisuje (rampa/chiller/tunel).</summary>
        public string Miejsce => MapujMiejsce(Nazwa);

        public static string MapujMiejsce(string nazwaNormy)
        {
            string n = (nazwaNormy ?? "").ToLowerInvariant();
            if (n.Contains("oparz") || n.Contains("parzel")) return "oparzalnik";
            if (n.Contains("schlad") || n.Contains("wanna")) return "schladzalnik";
            if (n.Contains("ramp")) return "rampa";
            if (n.Contains("chil")) return "chiller";
            if (n.Contains("tun")) return "tunel";
            return n.Replace("temp", "").Trim();
        }

        public bool IsInNorm(decimal? wartosc)
        {
            if (!wartosc.HasValue) return false;
            if (Min.HasValue && wartosc.Value < Min.Value) return false;
            if (Max.HasValue && wartosc.Value > Max.Value) return false;
            return true;
        }

        public string ZakresFormatted =>
            $"{(Min.HasValue ? Min.Value.ToString("N1") : "—")}…{(Max.HasValue ? Max.Value.ToString("N1") : "—")} {Jednostka}";
    }

    /// <summary>
    /// Pojedynczy pomiar z TemperaturyMiejsca + kontekst partii + ocena vs norma.
    /// </summary>
    public class TempPomiar
    {
        public int Id { get; set; }                    // TemperaturyMiejsca.Id
        public string PartiaId { get; set; } = "";
        public string Miejsce { get; set; } = "";
        public decimal? Proba1 { get; set; }
        public decimal? Proba2 { get; set; }
        public decimal? Proba3 { get; set; }
        public decimal? Proba4 { get; set; }
        public decimal? Srednia { get; set; }
        public string? Wykonal { get; set; }
        public DateTime DataPomiaru { get; set; }

        // Kontekst partii (JOIN listapartii / PartiaDostawca)
        public string? Hodowca { get; set; }
        public string? Towar { get; set; }

        // Ocena vs norma (dołączane przez serwis)
        public decimal? NormaMin { get; set; }
        public decimal? NormaMax { get; set; }
        public bool MaKorekta { get; set; }            // czy zarejestrowano działanie naprawcze

        public bool CzyPozaNorma
        {
            get
            {
                if (!Srednia.HasValue) return false;
                if (NormaMin.HasValue && Srednia.Value < NormaMin.Value) return true;
                if (NormaMax.HasValue && Srednia.Value > NormaMax.Value) return true;
                return false;
            }
        }

        public string DataFormatted => DataPomiaru.ToString("dd.MM HH:mm");
        public string SredniaFormatted => Srednia.HasValue ? $"{Srednia.Value:N1}°C" : "—";
        public string MiejsceLabel => MiejscaCC.Label(Miejsce);
        public string ProbyFormatted =>
            string.Join(" / ", new[] { Proba1, Proba2, Proba3, Proba4 }
                .Where(p => p.HasValue).Select(p => p!.Value.ToString("N1")));

        public string NormaFormatted =>
            $"{(NormaMin.HasValue ? NormaMin.Value.ToString("N1") : "—")}…{(NormaMax.HasValue ? NormaMax.Value.ToString("N1") : "—")}";

        public string Status =>
            !Srednia.HasValue ? "— brak —" :
            !CzyPozaNorma ? "✓ OK" :
            MaKorekta ? "⚠ Poza normą (skorygowane)" : "🚨 Poza normą";

        public string StatusKolor =>
            !Srednia.HasValue ? "#94A3B8" :
            !CzyPozaNorma ? "#10B981" :
            MaKorekta ? "#F59E0B" : "#DC2626";

        public string TloKolor =>
            !Srednia.HasValue ? "#F1F5F9" :
            !CzyPozaNorma ? "#D1FAE5" :
            MaKorekta ? "#FEF3C7" : "#FEE2E2";
    }

    /// <summary>Kafelek dashboardu — agregat per miejsce w okresie.</summary>
    public class MiejsceKafel
    {
        public string Miejsce { get; set; } = "";
        public string Ikona { get; set; } = "";
        public int LiczbaPomiarow { get; set; }
        public int LiczbaWNormie { get; set; }
        public decimal? SredniaTemp { get; set; }
        public string NormaText { get; set; } = "";

        public decimal ProcWNormie =>
            LiczbaPomiarow > 0 ? (decimal)LiczbaWNormie / LiczbaPomiarow * 100m : 0m;

        public string SredniaFormatted => SredniaTemp.HasValue ? $"{SredniaTemp.Value:N1}°C" : "—";
        public string ProcFormatted => LiczbaPomiarow > 0 ? $"{ProcWNormie:N0}% w normie" : "brak pomiarów";
        public int LiczbaPoza => LiczbaPomiarow - LiczbaWNormie;

        public string Kolor =>
            LiczbaPomiarow == 0 ? "#94A3B8" :
            ProcWNormie >= 98m ? "#10B981" :
            ProcWNormie >= 90m ? "#F59E0B" : "#DC2626";

        public string TloKolor =>
            LiczbaPomiarow == 0 ? "#F1F5F9" :
            ProcWNormie >= 98m ? "#D1FAE5" :
            ProcWNormie >= 90m ? "#FEF3C7" : "#FEE2E2";
    }

    /// <summary>Punkt trendu (do wykresu) — średnia temp w czasie dla miejsca.</summary>
    public class TempTrendPunkt
    {
        public DateTime Data { get; set; }
        public decimal Srednia { get; set; }
    }

    /// <summary>Partia do wyboru w formularzu wpisu.</summary>
    public class PartiaItem
    {
        public string Partia { get; set; } = "";
        public string? Hodowca { get; set; }
        public string Wyswietl => string.IsNullOrEmpty(Hodowca) ? Partia : $"{Partia} — {Hodowca}";
    }

    /// <summary>Pozycja rankingu hodowców po zgodności temperatur.</summary>
    public class RankingHodowcaTemp
    {
        public int Pozycja { get; set; }
        public string Hodowca { get; set; } = "";
        public int LiczbaPomiarow { get; set; }
        public int LiczbaPoza { get; set; }

        public decimal ProcZgodnosci =>
            LiczbaPomiarow > 0 ? (decimal)(LiczbaPomiarow - LiczbaPoza) / LiczbaPomiarow * 100m : 0m;

        public string ZgodnoscFormatted => $"{ProcZgodnosci:N1}%";
        public string PozaFormatted => $"{LiczbaPoza} / {LiczbaPomiarow}";
        public string Status =>
            ProcZgodnosci >= 98m ? "✓ OK" :
            ProcZgodnosci >= 90m ? "⚠ Uwaga" : "🚨 Problem";
        public string StatusKolor =>
            ProcZgodnosci >= 98m ? "#10B981" :
            ProcZgodnosci >= 90m ? "#F59E0B" : "#DC2626";
    }

    /// <summary>Opcja miejsca do ComboBox (Kod = wartość, Display = etykieta z ikoną).</summary>
    public class MiejsceOpcja
    {
        public string Kod { get; set; } = "";
        public string Display { get; set; } = "";
    }

    /// <summary>
    /// Centralna definicja miejsc pomiaru Cold Chain (kolejność = porządek termiczny w łańcuchu).
    /// Jedno źródło dla: kafelków dashboardu, combo wpisu/trendu, kolejności krzywej schładzania.
    /// </summary>
    public static class MiejscaCC
    {
        // Kolejność termiczna: oparzalnik (gorąca woda) → schładzalnik (wanna) → chłodnia → mroźnia.
        // Rampa to przyjęcie żywca (poza łańcuchem chłodzenia, na końcu listy).
        public static readonly (string Kod, string Ikona, string Label)[] Lista =
        {
            ("oparzalnik",  "🔥", "Oparzalnik (woda gorąca)"),
            ("schladzalnik","💧", "Schładzalnik (wanna)"),
            ("chiller",     "❄",  "Chłodnia"),
            ("tunel",       "🧊", "Tunel / Mroźnia"),
            ("rampa",       "🚛", "Rampa przyjęcia"),
        };

        /// <summary>Miejsca w kolejności krzywej schładzania (bez rampy — to nie etap chłodzenia).</summary>
        public static readonly string[] KolejnoscKrzywej =
            { "oparzalnik", "schladzalnik", "chiller", "tunel" };

        public static List<MiejsceOpcja> Opcje() =>
            Lista.Select(m => new MiejsceOpcja { Kod = m.Kod, Display = $"{m.Ikona} {m.Label}" }).ToList();

        public static string Label(string kod)
        {
            foreach (var m in Lista)
                if (string.Equals(m.Kod, kod, StringComparison.OrdinalIgnoreCase))
                    return $"{m.Ikona} {m.Label}";
            return kod ?? "";
        }

        public static string Ikona(string kod)
        {
            foreach (var m in Lista)
                if (string.Equals(m.Kod, kod, StringComparison.OrdinalIgnoreCase))
                    return m.Ikona;
            return "📍";
        }

        /// <summary>Domyślne (fallback) normy w kodzie — używane gdy QC_Normy nie ma danego miejsca.
        /// Dzięki temu moduł ocenia temperatury vs norma NAWET bez uruchomienia SQL.</summary>
        public static readonly Dictionary<string, (decimal? Min, decimal? Max)> DomyslneNormy = new()
        {
            ["oparzalnik"]   = (50m, 62m),
            ["schladzalnik"] = (0m, 4m),
            ["chiller"]      = (-2m, 4m),
            ["tunel"]        = (null, -18m),
            ["rampa"]        = (null, 4m),
        };
    }

    /// <summary>Partia bez kompletu pomiarów (luka HACCP — brak pomiaru w którymś miejscu).</summary>
    public class NiekompletnaPartia
    {
        public string Partia { get; set; } = "";
        public string? Hodowca { get; set; }
        public List<string> MaMiejsca { get; set; } = new();
        public List<string> BrakujeMiejsc { get; set; } = new();
        public DateTime OstatniPomiar { get; set; }

        public string MaFormatted => string.Join(", ", MaMiejsca);
        public string BrakFormatted => string.Join(", ", BrakujeMiejsc);
        public string DataFormatted => OstatniPomiar.ToString("dd.MM.yyyy");
    }
}
