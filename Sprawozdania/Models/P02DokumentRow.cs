using System;

namespace Kalendarz1.Sprawozdania.Models
{
    // ════════════════════════════════════════════════════════════════════
    // Pozycja w drill-down — jeden dokument (lub linia dokumentu) który
    // wszedł do agregatu danej komórki w P-02.
    // ════════════════════════════════════════════════════════════════════
    public class P02DokumentRow
    {
        public DateTime Data { get; set; }
        public string TypDokumentu { get; set; } = "";   // FVS / FKS / PWU / PWP / PWK
        public string NumerDokumentu { get; set; } = "";

        // Kontrahent (tylko sprzedaż)
        public string? Kontrahent { get; set; }
        public int? KontrahentId { get; set; }

        // Pozycja
        public string KodTowaru { get; set; } = "";
        public string NazwaTowaru { get; set; } = "";
        public decimal IloscKg { get; set; }
        public decimal WartoscNetto { get; set; }        // 0 dla dokumentów produkcyjnych
        public string PkwiuKlasyfikacja { get; set; } = "";

        // Dla porządku — Lp w dokumencie
        public int Lp { get; set; }
    }
}
