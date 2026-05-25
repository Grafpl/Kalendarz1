// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/Models/TransportWpfModels.cs
// ════════════════════════════════════════════════════════════════════════════
// Modele dla NOWEJ (sandbox) wersji WPF planowania transportu.
// Izolowane od WinForms (transport-panel-main.cs / transport-editor.cs) — nic z
// tamtego nie jest dotykane. Reuse: TransportRepozytorium + Kurs/Ladunek/Kierowca/Pojazd.
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.ComponentModel;

namespace Kalendarz1.Transport.WPF.Models
{
    /// <summary>
    /// Wolne zamówienie (z LibraNet) gotowe do dołożenia do kursu.
    /// Nazwa/handlowiec/adres uzupełniane z HANDEL (cross-DB, łączone w .NET).
    /// </summary>
    public class WolneZamowienieWpf
    {
        public int ZamowienieId { get; set; }
        public int KlientId { get; set; }
        public string KlientNazwa { get; set; } = "";
        public string Handlowiec { get; set; } = "";
        public string Adres { get; set; } = "";
        public int Pojemniki { get; set; }
        public decimal Palety { get; set; }
        public bool TrybE2 { get; set; }
        public DateTime DataPrzyjazdu { get; set; }   // awizacja
        public DateTime? DataUboju { get; set; }
        public decimal IloscKg { get; set; }
        public string TransportStatus { get; set; } = "Oczekuje";

        // ── pola wyświetlane w gridzie ──
        public string GodzAwizacji => DataPrzyjazdu.ToString("HH:mm");
        public string KodKlienta => $"ZAM_{ZamowienieId}";
        public string PojemnikiDisplay => $"{Pojemniki} poj.";
        public string HandlowiecDisplay => string.IsNullOrWhiteSpace(Handlowiec) ? "—" : Handlowiec;

        public string Tooltip =>
            $"{KlientNazwa}\n" +
            $"Handlowiec: {(string.IsNullOrWhiteSpace(Handlowiec) ? "—" : Handlowiec)}\n" +
            $"Adres: {(string.IsNullOrWhiteSpace(Adres) ? "—" : Adres)}\n" +
            $"Awizacja: {GodzAwizacji}  ·  {Pojemniki} poj.  ·  {ZamowienieId}";
    }

    /// <summary>
    /// Wiersz ładunku w edytowanym kursie. LadunekID == 0 ⇒ nowy (jeszcze niezapisany).
    /// INotifyPropertyChanged żeby kolejność/edycje odświeżały DataGrid.
    /// </summary>
    public class LadunekWierszWpf : INotifyPropertyChanged
    {
        public long LadunekID { get; set; }           // 0 = nowy
        public long KursID { get; set; }

        private int _kolejnosc;
        public int Kolejnosc
        {
            get => _kolejnosc;
            set { _kolejnosc = value; OnChanged(nameof(Kolejnosc)); }
        }

        public string? KodKlienta { get; set; }       // "ZAM_123" lub tekst ręczny
        public int PojemnikiE2 { get; set; }
        public string? Uwagi { get; set; }
        public bool TrybE2 { get; set; }
        public byte? PlanE2NaPaleteOverride { get; set; }

        private string _nazwaKlienta = "";
        public string NazwaKlienta
        {
            get => _nazwaKlienta;
            set { _nazwaKlienta = value; OnChanged(nameof(NazwaKlienta)); }
        }

        public DateTime? Awizacja { get; set; }
        public string Handlowiec { get; set; } = "";

        // ── pola wyświetlane ──
        public string AwizacjaDisplay => Awizacja?.ToString("HH:mm") ?? "—";
        public string PojemnikiDisplay => $"{PojemnikiE2} poj.";
        public string NazwaDisplay => string.IsNullOrWhiteSpace(NazwaKlienta) ? (KodKlienta ?? "—") : NazwaKlienta;

        /// <summary>ID zamówienia LibraNet z KodKlienta "ZAM_{id}" (null jeśli wpis ręczny).</summary>
        public int? ZamowienieId
        {
            get
            {
                if (KodKlienta != null && KodKlienta.StartsWith("ZAM_") &&
                    int.TryParse(KodKlienta.Substring(4), out var id))
                    return id;
                return null;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    /// <summary>Rozwiązane dane klienta dla istniejącego ładunku ZAM_*.</summary>
    public class ZamowienieNazwaInfo
    {
        public int ZamowienieId { get; set; }
        public int KlientId { get; set; }
        public string Nazwa { get; set; } = "";
        public string Handlowiec { get; set; } = "";
        public string Adres { get; set; } = "";
        public DateTime? Awizacja { get; set; }
        public int Pojemniki { get; set; }
        public decimal IloscKg { get; set; }
    }
}
