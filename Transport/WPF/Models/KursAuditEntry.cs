// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/Models/KursAuditEntry.cs — wpis w historii zmian kursu (diff).
// ════════════════════════════════════════════════════════════════════════════

using System;

namespace Kalendarz1.Transport.WPF.Models
{
    public class KursAuditEntry
    {
        public long Id { get; set; }
        public long KursID { get; set; }
        public string Pole { get; set; } = "";
        public string? StareWartosc { get; set; }
        public string? NowaWartosc { get; set; }
        public string KtoZmienil { get; set; } = "";
        public DateTime KiedyUTC { get; set; }

        public string KiedyLokalnie => KiedyUTC.ToLocalTime().ToString("dd.MM HH:mm");
        public string PoleLabel => Pole switch
        {
            "Kierowca" => "Kierowca",
            "Pojazd" => "Pojazd",
            "DataKursu" => "Data kursu",
            "GodzWyjazdu" => "Godzina wyjazdu",
            "GodzPowrotu" => "Godzina powrotu",
            "Trasa" => "Trasa",
            "Status" => "Status",
            "LiczbaLadunkow" => "Liczba ładunków",
            _ => Pole
        };
        public string PoleEmoji => Pole switch
        {
            "Kierowca" => "👤",
            "Pojazd" => "🚚",
            "DataKursu" => "📅",
            "GodzWyjazdu" => "🕐",
            "GodzPowrotu" => "🕓",
            "Trasa" => "🗺",
            "Status" => "🏷",
            "LiczbaLadunkow" => "📦",
            _ => "✎"
        };
        public string StareDisplay => string.IsNullOrEmpty(StareWartosc) ? "—" : StareWartosc!;
        public string NowaDisplay => string.IsNullOrEmpty(NowaWartosc) ? "—" : NowaWartosc!;
    }
}
