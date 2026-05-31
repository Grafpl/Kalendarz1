// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/Models/TimelineModels.cs — modele widoku Timeline (Gantt dnia).
// Faza T (zdegradowana): bez PendingZmianCount (Faza 2) i hatch niedostępności
// (brak kolumn nieobecności w TransportPL.Kierowca).
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;

namespace Kalendarz1.Transport.WPF.Models
{
    /// <summary>Lekki model paska kursu na osi czasu (niezależny od KursRow okna planowania).</summary>
    public class KursBar
    {
        public long KursID { get; set; }
        public string Trasa { get; set; } = "—";
        public TimeSpan Wyjazd { get; set; }    // z fallbackiem (nigdy null tutaj)
        public TimeSpan Powrot { get; set; }
        public int? KierowcaID { get; set; }
        public int? PojazdID { get; set; }
        public string KierowcaNazwa { get; set; } = "";
        public string PojazdRej { get; set; } = "";
        public int LiczbaLadunkow { get; set; }
        public decimal Proc { get; set; }       // ProcNominal (% wypełnienia)
        public int Pal { get; set; }
        public int Poj { get; set; }
        public string UtworzylName { get; set; } = "";
        public string UtworzylData { get; set; } = "";
        public string ZmienilName { get; set; } = "";
        public string ZmienilData { get; set; } = "";
        public bool BylZmieniany => !string.IsNullOrEmpty(ZmienilName) && !string.IsNullOrEmpty(ZmienilData);
        public bool Konflikt { get; set; }       // ustawiane przy detekcji nakładania
        public bool BrakGodzin { get; set; }     // godziny były null → fallback
        public int LiczbaZmianOczekujacych { get; set; }  // TransportZmiany: pending dla zamówień w kursie
        public bool MaZmiany => LiczbaZmianOczekujacych > 0;

        public int WypProc => (int)Math.Round(Proc);

        /// <summary>Stan paska (priorytet): 0=pusty, 1=przeładowany, 2=brak przydziału, 3=OK.</summary>
        public int Stan
        {
            get
            {
                if (LiczbaLadunkow == 0) return 0;
                if (Proc > 100) return 1;
                if (!KierowcaID.HasValue || !PojazdID.HasValue) return 2;
                return 3;
            }
        }
    }

    /// <summary>Wiersz osi czasu = jeden kierowca + jego kursy dnia + wykryte konflikty.</summary>
    public class KierowcaWierszTimeline
    {
        public int KierowcaID { get; set; }      // 0 = pseudo-wiersz „— brak kierowcy —"
        public string Nazwisko { get; set; } = "";
        public string Imie { get; set; } = "";
        public string PojazdRej { get; set; } = "";   // pojazd z pierwszego kursu (brak kolumny PojazdGlownyID)
        public bool Dostepny { get; set; } = true;     // zawsze true — brak danych nieobecności
        public bool BrakKierowcy { get; set; }         // true dla pseudo-wiersza
        public List<KursBar> Kursy { get; set; } = new();

        public string PelneNazwisko => BrakKierowcy ? "— brak kierowcy —" : $"{Imie} {Nazwisko}".Trim();
        public string Inicjaly
        {
            get
            {
                if (BrakKierowcy) return "?";
                var a = !string.IsNullOrEmpty(Imie) ? Imie[0].ToString() : "";
                var b = !string.IsNullOrEmpty(Nazwisko) ? Nazwisko[0].ToString() : "";
                var s = (a + b).ToUpper();
                return string.IsNullOrEmpty(s) ? "?" : s;
            }
        }
        public int LiczbaKursow => Kursy.Count;
    }
}
