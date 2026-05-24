using System;

namespace Kalendarz1.ColdChain
{
    /// <summary>Punkt kontroli krytycznej (CCP) + ostatni pomiar.</summary>
    public class CCPPunkt
    {
        public int Id { get; set; }
        public string Kod { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public string TypPomiaru { get; set; } = "TEMP";
        public decimal? LimitDolny { get; set; }
        public decimal? LimitGorny { get; set; }
        public string Jednostka { get; set; } = "°C";
        public int? CzestotliwoscMin { get; set; }
        public bool Aktywny { get; set; } = true;

        // Ostatni pomiar (dołączany przez serwis)
        public decimal? OstatniaWartosc { get; set; }
        public DateTime? OstatniPomiarDateTime { get; set; }

        public string OstatniaWartoscFormatted =>
            OstatniaWartosc.HasValue ? $"{OstatniaWartosc.Value:N1} {Jednostka}" : "— brak —";

        public string LimitFormatted =>
            $"{(LimitDolny.HasValue ? LimitDolny.Value.ToString("N0") : "—")}…{(LimitGorny.HasValue ? LimitGorny.Value.ToString("N0") : "—")} {Jednostka}";

        public bool CzyPozaLimitem =>
            OstatniaWartosc.HasValue &&
            ((LimitDolny.HasValue && OstatniaWartosc.Value < LimitDolny.Value) ||
             (LimitGorny.HasValue && OstatniaWartosc.Value > LimitGorny.Value));

        public bool CzyPrzestarzaly =>
            OstatniPomiarDateTime.HasValue && CzestotliwoscMin.HasValue &&
            (DateTime.Now - OstatniPomiarDateTime.Value).TotalMinutes > CzestotliwoscMin.Value * 2;

        public string Status =>
            !OstatniaWartosc.HasValue ? "— brak danych —" :
            CzyPozaLimitem ? "🚨 Poza limitem" :
            CzyPrzestarzaly ? "⚠ Pomiar przestarzały" : "✓ OK";

        public string StatusKolor =>
            !OstatniaWartosc.HasValue ? "#94A3B8" :
            CzyPozaLimitem ? "#DC2626" :
            CzyPrzestarzaly ? "#F59E0B" : "#10B981";

        public string TloKolor =>
            !OstatniaWartosc.HasValue ? "#F1F5F9" :
            CzyPozaLimitem ? "#FEE2E2" :
            CzyPrzestarzaly ? "#FEF3C7" : "#D1FAE5";

        public string OstatniPomiarFormatted =>
            OstatniPomiarDateTime.HasValue ? OstatniPomiarDateTime.Value.ToString("dd.MM HH:mm") : "—";
    }

    /// <summary>Pojedynczy pomiar CCP.</summary>
    public class CCPPomiar
    {
        public long Id { get; set; }
        public int PunktId { get; set; }
        public string PunktNazwa { get; set; } = "";
        public DateTime PomiarDateTime { get; set; }
        public decimal Wartosc { get; set; }
        public string Zrodlo { get; set; } = "MANUALNY";
        public string? OperatorId { get; set; }
        public string? Uwagi { get; set; }

        public string DataFormatted => PomiarDateTime.ToString("dd.MM HH:mm");
        public string WartoscFormatted => $"{Wartosc:N1}";
    }

    /// <summary>Incydent CCP (przekroczenie limitu).</summary>
    public class CCPIncydent
    {
        public long Id { get; set; }
        public int PunktId { get; set; }
        public string PunktNazwa { get; set; } = "";
        public DateTime StartDateTime { get; set; }
        public DateTime? EndDateTime { get; set; }
        public decimal? WartoscMin { get; set; }
        public decimal? WartoscMax { get; set; }
        public decimal? LimitDolny { get; set; }
        public decimal? LimitGorny { get; set; }
        public string Priorytet { get; set; } = "WYSOKI";
        public string StatusFinal { get; set; } = "OTWARTY";
        public string? KorektaOpis { get; set; }
        public string? KorektaPrzezId { get; set; }

        public string StartFormatted => StartDateTime.ToString("dd.MM HH:mm");
        public string CzasTrwania
        {
            get
            {
                var koniec = EndDateTime ?? DateTime.Now;
                var span = koniec - StartDateTime;
                return span.TotalHours >= 1
                    ? $"{(int)span.TotalHours}h {span.Minutes}min"
                    : $"{span.Minutes} min";
            }
        }
        public string WartoscFormatted =>
            WartoscMin.HasValue || WartoscMax.HasValue
                ? $"{WartoscMin:N1}…{WartoscMax:N1}" : "—";
        public bool Otwarty => StatusFinal == "OTWARTY";
        public string StatusKolor => Otwarty ? "#DC2626" : "#10B981";
    }
}
