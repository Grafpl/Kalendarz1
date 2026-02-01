using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Kalendarz1.CRM.Models
{
    public class ContactToCall : INotifyPropertyChanged
    {
        public int ID { get; set; }
        public string Nazwa { get; set; }
        public string Telefon { get; set; }
        public string Telefon2 { get; set; }
        public string Email { get; set; }
        public string Miasto { get; set; }
        public string Wojewodztwo { get; set; }
        public string Adres { get; set; }
        public string KodPocztowy { get; set; }
        public string NIP { get; set; }
        public string PKD { get; set; }
        public string PKDNazwa { get; set; }
        public string Status { get; set; }
        public string Priority { get; set; } // urgent, high, medium, low
        public string Branza { get; set; }
        public string OstatniaNota { get; set; }
        public string OstatniaNotaAutor { get; set; }
        public DateTime? DataOstatniejNotatki { get; set; }
        public int CallCount { get; set; }
        public DateTime? LastCallDate { get; set; }
        public string AssignedTo { get; set; }
        public double OdlegloscKm { get; set; } // Distance in km

        // Display helpers
        public bool HasWojewodztwo => !string.IsNullOrWhiteSpace(Wojewodztwo);
        public bool HasKodPocztowy => !string.IsNullOrWhiteSpace(KodPocztowy);
        public bool HasAdres => !string.IsNullOrWhiteSpace(Adres);
        public bool HasOdleglosc => OdlegloscKm > 0;
        public bool HasCallCount => CallCount > 0;
        public bool HasPhone2OrEmail => HasPhone2 || HasEmail;

        public string MiastoFull
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(KodPocztowy)) parts.Add(KodPocztowy);
                if (!string.IsNullOrWhiteSpace(Miasto)) parts.Add(Miasto);
                return parts.Count > 0 ? string.Join(" ", parts) : "-";
            }
        }

        public string OdlegloscDisplay => OdlegloscKm > 0 ? $"{OdlegloscKm:F0} km" : "";

        public string WojewodztwoDisplay
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Wojewodztwo)) return "";
                // Capitalize first letter
                var woj = Wojewodztwo.Trim().ToLower();
                return "woj. " + char.ToUpper(woj[0]) + woj.Substring(1);
            }
        }

        public string BranzaDisplay => !string.IsNullOrWhiteSpace(Branza) ? Branza : (!string.IsNullOrWhiteSpace(PKDNazwa) ? PKDNazwa : "");
        public bool HasBranzaOrPKDNazwa => !string.IsNullOrWhiteSpace(Branza) || !string.IsNullOrWhiteSpace(PKDNazwa);

        // UI state
        private bool _wasCalled;
        public bool WasCalled
        {
            get => _wasCalled;
            set { _wasCalled = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCompleted)); }
        }

        private bool _noteAdded;
        public bool NoteAdded
        {
            get => _noteAdded;
            set { _noteAdded = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCompleted)); }
        }

        private bool _statusChanged;
        public bool StatusChanged
        {
            get => _statusChanged;
            set { _statusChanged = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCompleted)); }
        }

        public string NewStatus { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public bool IsCompleted => WasCalled || NoteAdded || StatusChanged;

        // Enhanced UI helpers
        public bool HasBranza => !string.IsNullOrWhiteSpace(Branza);
        public bool HasLastNote => !string.IsNullOrWhiteSpace(OstatniaNota);
        public bool HasPKD => !string.IsNullOrWhiteSpace(PKD);
        public bool HasPhone2 => !string.IsNullOrWhiteSpace(Telefon2);
        public bool HasEmail => !string.IsNullOrWhiteSpace(Email);
        public bool HasNIP => !string.IsNullOrWhiteSpace(NIP);

        public string FullAddress
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(Adres)) parts.Add(Adres);
                if (!string.IsNullOrWhiteSpace(KodPocztowy)) parts.Add(KodPocztowy);
                if (!string.IsNullOrWhiteSpace(Miasto)) parts.Add(Miasto);
                return parts.Count > 0 ? string.Join(", ", parts) : "-";
            }
        }

        public string LastNotePreview
        {
            get
            {
                if (string.IsNullOrWhiteSpace(OstatniaNota)) return string.Empty;
                return OstatniaNota.Length > 80 ? OstatniaNota.Substring(0, 80) + "..." : OstatniaNota;
            }
        }

        public string LastNoteDate
        {
            get
            {
                if (!DataOstatniejNotatki.HasValue) return string.Empty;
                var diff = DateTime.Now - DataOstatniejNotatki.Value;
                if (diff.TotalDays < 1) return "dzisiaj";
                if (diff.TotalDays < 2) return "wczoraj";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} dni temu";
                return DataOstatniejNotatki.Value.ToString("dd.MM.yyyy");
            }
        }

        public string LastCallFormatted
        {
            get
            {
                if (!LastCallDate.HasValue) return "-";
                var diff = DateTime.Now - LastCallDate.Value;
                if (diff.TotalDays < 1) return "Dzisiaj";
                if (diff.TotalDays < 2) return "Wczoraj";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} dni temu";
                return LastCallDate.Value.ToString("dd.MM.yyyy");
            }
        }

        // Priority color (for glow dot)
        public Color PriorityColor
        {
            get
            {
                return Priority?.ToLower() switch
                {
                    "urgent" => Color.FromRgb(248, 81, 73),   // #f85149
                    "high" => Color.FromRgb(210, 153, 34),    // #d29922
                    "medium" => Color.FromRgb(88, 166, 255),  // #58a6ff
                    "low" => Color.FromRgb(63, 185, 80),      // #3fb950
                    _ => Color.FromRgb(88, 166, 255)          // default blue
                };
            }
        }

        // Status colors for dark theme (Glassmorphism)
        public SolidColorBrush StatusColor
        {
            get
            {
                return Status switch
                {
                    "Nowy" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),           // #22c55e green
                    "Do zadzwonienia" => new SolidColorBrush(Color.FromRgb(100, 116, 139)), // slate
                    "Próba kontaktu" => new SolidColorBrush(Color.FromRgb(249, 158, 11)),  // #f59e0b orange
                    "W trakcie" => new SolidColorBrush(Color.FromRgb(59, 130, 246)),        // #3b82f6 blue
                    "Gorący" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),            // #ef4444 red
                    "Nawiązano kontakt" => new SolidColorBrush(Color.FromRgb(34, 197, 94)), // green
                    "Zgoda na dalszy kontakt" => new SolidColorBrush(Color.FromRgb(20, 184, 166)), // teal
                    "Oferta wysłana" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),   // #f59e0b orange
                    "Do wysłania oferta" => new SolidColorBrush(Color.FromRgb(8, 145, 178)), // cyan
                    "Negocjacje" => new SolidColorBrush(Color.FromRgb(139, 92, 246)),       // #8b5cf6 purple
                    "Zamknięty" => new SolidColorBrush(Color.FromRgb(107, 114, 128)),       // #6b7280 gray
                    "Nie zainteresowany" => new SolidColorBrush(Color.FromRgb(239, 68, 68)), // red
                    "Odrzucony" => new SolidColorBrush(Color.FromRgb(220, 38, 38)),         // #dc2626 dark red
                    _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))                   // gray
                };
            }
        }

        public SolidColorBrush StatusBackground
        {
            get
            {
                // Dark theme backgrounds (with transparency effect)
                return Status switch
                {
                    "Nowy" => new SolidColorBrush(Color.FromArgb(38, 34, 197, 94)),          // green 15%
                    "Do zadzwonienia" => new SolidColorBrush(Color.FromArgb(38, 100, 116, 139)), // slate 15%
                    "Próba kontaktu" => new SolidColorBrush(Color.FromArgb(38, 249, 158, 11)),   // orange 15%
                    "W trakcie" => new SolidColorBrush(Color.FromArgb(38, 59, 130, 246)),        // blue 15%
                    "Gorący" => new SolidColorBrush(Color.FromArgb(38, 239, 68, 68)),            // red 15%
                    "Nawiązano kontakt" => new SolidColorBrush(Color.FromArgb(38, 34, 197, 94)), // green 15%
                    "Zgoda na dalszy kontakt" => new SolidColorBrush(Color.FromArgb(38, 20, 184, 166)), // teal 15%
                    "Oferta wysłana" => new SolidColorBrush(Color.FromArgb(38, 245, 158, 11)),   // orange 15%
                    "Do wysłania oferta" => new SolidColorBrush(Color.FromArgb(38, 8, 145, 178)), // cyan 15%
                    "Negocjacje" => new SolidColorBrush(Color.FromArgb(38, 139, 92, 246)),       // purple 15%
                    "Zamknięty" => new SolidColorBrush(Color.FromArgb(38, 107, 114, 128)),       // gray 15%
                    "Nie zainteresowany" => new SolidColorBrush(Color.FromArgb(38, 239, 68, 68)), // red 15%
                    "Odrzucony" => new SolidColorBrush(Color.FromArgb(38, 220, 38, 38)),         // dark red 15%
                    _ => new SolidColorBrush(Color.FromArgb(38, 148, 163, 184))                   // gray 15%
                };
            }
        }

        public string StatusShort
        {
            get
            {
                return Status switch
                {
                    "Nowy" => "Nowy",
                    "Do zadzwonienia" => "Do zadzw.",
                    "Próba kontaktu" => "Próba",
                    "W trakcie" => "W trakcie",
                    "Gorący" => "Gorący",
                    "Nawiązano kontakt" => "Kontakt",
                    "Zgoda na dalszy kontakt" => "Zgoda",
                    "Oferta wysłana" => "Oferta",
                    "Do wysłania oferta" => "Oferta",
                    "Negocjacje" => "Negocjacje",
                    "Zamknięty" => "Zamknięty",
                    "Nie zainteresowany" => "Odmowa",
                    "Odrzucony" => "Odrzucony",
                    _ => Status?.Length > 10 ? Status.Substring(0, 10) + "..." : Status ?? "-"
                };
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
