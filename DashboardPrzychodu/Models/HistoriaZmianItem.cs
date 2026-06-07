using System;

namespace Kalendarz1.DashboardPrzychodu.Models
{
    /// <summary>
    /// Pojedynczy wpis historii zmian wag/sztuk deklarowanych z dbo.FarmerCalcChangeLog (LibraNet).
    /// Pokazywany w sidebarze pod Expanderem AKCJE.
    /// </summary>
    public class HistoriaZmianItem
    {
        public DateTime ChangedAt { get; set; }
        public string Hodowca { get; set; } = "";
        public string FieldName { get; set; } = "";
        public string OldValue { get; set; } = "";
        public string NewValue { get; set; } = "";
        public string UserName { get; set; } = "";

        /// <summary>Krotka nazwa pola dla UI (HarmonogramDostaw_ChangeLog FieldNames).</summary>
        public string FieldShort => FieldName switch
        {
            "SztukiDek" => "Sztuki",
            "WagaDek"   => "Waga śr.",
            "Auta"      => "Auta",
            _ => FieldName
        };

        /// <summary>"12:34" - godzina zmiany.</summary>
        public string TimeDisplay => ChangedAt.ToString("HH:mm");

        /// <summary>"6000 → 6500" lub "— → 6500" / "6000 → —" (NULL czytelnie).</summary>
        public string ZmianaDisplay
        {
            get
            {
                string oldV = string.IsNullOrWhiteSpace(OldValue) ? "—" : OldValue.Trim();
                string newV = string.IsNullOrWhiteSpace(NewValue) ? "—" : NewValue.Trim();
                return $"{oldV} → {newV}";
            }
        }

        /// <summary>Tooltip: pelny opis zmiany.</summary>
        public string Tooltip
        {
            get
            {
                var user = string.IsNullOrWhiteSpace(UserName) ? "system" : UserName;
                return $"{ChangedAt:dd.MM.yyyy HH:mm:ss}\nHodowca: {Hodowca}\nPole: {FieldName}\n{OldValue} → {NewValue}\nUzytkownik: {user}";
            }
        }
    }
}
