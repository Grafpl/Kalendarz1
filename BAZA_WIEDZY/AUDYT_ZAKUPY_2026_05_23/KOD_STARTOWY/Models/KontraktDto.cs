// ════════════════════════════════════════════════════════════════════════════
// KontraktDto.cs — DTO dla modułu Kontrakty Hodowców
// Część 4 audytu (2026-05-23) — kod startowy do skopiowania do projektu
// Target lokalizacja w projekcie: Kontrakty/Models/KontraktDto.cs
// ════════════════════════════════════════════════════════════════════════════

using System;

namespace Kalendarz1.Kontrakty.Models
{
    /// <summary>
    /// DTO odpowiadający tabeli dbo.Kontrakty w LibraNet.
    /// Używane do listy, edycji, generacji Word.
    /// </summary>
    public class KontraktDto
    {
        public int Id { get; set; }
        public string NumerKontraktu { get; set; } = "";  // np. "1/27"
        public short Rok { get; set; }
        public int LpRoku { get; set; }
        public int DostawcaId { get; set; }
        public string TypKontraktu { get; set; } = "ARIMR_3LAT";  // ARIMR_3LAT / ROCZNY / WIECZNY / SPOT
        public string Status { get; set; } = "DRAFT";  // DRAFT / PRINTED / SENT / SIGNED / ACTIVE / EXPIRING / EXPIRED / TERMINATED

        public DateTime? DataPodpisania { get; set; }
        public DateTime DataObowiazujeOd { get; set; } = DateTime.Today;
        public DateTime? DataObowiazujeDo { get; set; }
        public int OkresWypowiedzeniaDni { get; set; } = 90;

        // Warunki handlowe
        public decimal ProcentUbytku { get; set; } = 3.00m;
        public string TypCeny { get; set; } = "wolnorynkowa";  // wolnorynkowa / rolnicza / ministerialna / laczona
        public decimal? Cena { get; set; }
        public int TerminPlatnosciDni { get; set; } = 21;
        public string RozliczanaWaga { get; set; } = "NETTO_HODOWCY";
        public int? MinimalnaIlosc { get; set; }

        // Snapshot hodowcy (chroni przed mutacjami DOSTAWCY w czasie)
        public string? NipSnapshot { get; set; }
        public string? NrGospodarstwaSnapshot { get; set; }
        public string? NazwaHodowcySnapshot { get; set; }
        public string? AdresSnapshot { get; set; }

        // ARiMR + sp. z o.o.
        public bool LiczySieDoArimr { get; set; }
        public string? PartiaPiorkowscy { get; set; }  // 'PIORKOWSCY' / 'PIORKOWSCY_SPZOO'

        // Audyt
        public string UtworzylUserId { get; set; } = "";
        public DateTime UtworzylKiedy { get; set; } = DateTime.Now;
        public string? EdytowalUserId { get; set; }
        public DateTime? EdytowalKiedy { get; set; }
        public string? PowodWypowiedzenia { get; set; }

        // Pliki
        public string? SciezkaWord { get; set; }
        public string? SciezkaPdfSkan { get; set; }

        // ── Helpers do UI / generatora ──────────────────────────────────────
        public bool JestAktywny => Status is "ACTIVE" or "EXPIRING" or "SIGNED";
        public bool JestZakonczony => Status is "EXPIRED" or "TERMINATED";
        public int? DniDoWygasniecia => DataObowiazujeDo is null
            ? null
            : (DataObowiazujeDo.Value - DateTime.Today).Days;
    }

    /// <summary>
    /// Załącznik do kontraktu (skan PDF, aneks, korespondencja).
    /// </summary>
    public class KontraktZalacznikDto
    {
        public int Id { get; set; }
        public int KontraktId { get; set; }
        public string TypZalacznika { get; set; } = "SKAN_PODPISANY";  // SKAN_PODPISANY / ANEKS / OSWIADCZENIE / KORESPONDENCJA / INNE
        public string NazwaPliku { get; set; } = "";
        public string SciezkaUnc { get; set; } = "";
        public string DodalUserId { get; set; } = "";
        public DateTime DodanyKiedy { get; set; } = DateTime.Now;
        public string? Opis { get; set; }
    }

    /// <summary>
    /// Alert dla użytkownika (Asia/Ser) o wygaśnięciu / problemie.
    /// </summary>
    public class KontraktAlertDto
    {
        public int Id { get; set; }
        public int KontraktId { get; set; }
        public string TypAlertu { get; set; } = "";  // WYGASA_3M / WYGASA_1M / WYGASA_7D / WYGASNAL / BRAK_SKANU / ARIMR_NIESPELNIONE
        public DateTime DataWygenerowania { get; set; } = DateTime.Now;
        public string Severity { get; set; } = "INFO";  // INFO / WARN / CRIT
        public string DlaUserId { get; set; } = "";
        public bool Przeczytany { get; set; }
        public DateTime? PrzeczytanyKiedy { get; set; }
        public string? PrzeczytanyKto { get; set; }
        public string Wiadomosc { get; set; } = "";
    }

    /// <summary>
    /// Snapshot compliance ARiMR (z view v_ArimrCompliance).
    /// </summary>
    public class ArimrComplianceSnapshot
    {
        public decimal SurowiecCaloscKg { get; set; }
        public decimal SurowiecArimrKg { get; set; }
        public int HodowcowOgolem { get; set; }
        public int HodowcowArimr { get; set; }
        public decimal ProcentArimr { get; set; }
        public string Status { get; set; } = "BRAK_DANYCH";  // OK / WARN / CRIT / BRAK_DANYCH
        public DateTime WyliczonoKiedy { get; set; }

        public bool CzyAlarm => Status is "WARN" or "CRIT";
    }
}
