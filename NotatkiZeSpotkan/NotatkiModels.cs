using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Kalendarz1.NotatkiZeSpotkan
{
    public class NotatkaZeSpotkaniaModel : INotifyPropertyChanged
    {
        public long NotatkaID { get; set; }
        public string TypSpotkania { get; set; } = "Zespół";
        public DateTime DataSpotkania { get; set; } = DateTime.Today;
        public DateTime DataUtworzenia { get; set; } = DateTime.Now;
        public DateTime? DataModyfikacji { get; set; }

        public string TworcaID { get; set; } = string.Empty;
        public string TworcaNazwa { get; set; } = string.Empty;

        public string? KontrahentID { get; set; }
        public string? KontrahentNazwa { get; set; }
        public string? KontrahentTyp { get; set; }

        public string Temat { get; set; } = string.Empty;
        public string TrescNotatki { get; set; } = string.Empty;
        public string? OsobaKontaktowa { get; set; }
        public string? DodatkoweInfo { get; set; }

        public List<OperatorDTO> Uczestnicy { get; set; } = new List<OperatorDTO>();
        public List<OperatorDTO> Widocznosc { get; set; } = new List<OperatorDTO>();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class OperatorDTO
    {
        public string ID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public override string ToString() => $"{Name} ({ID})";
    }

    public class KontrahentDTO
    {
        public string ID { get; set; } = string.Empty;
        public string Nazwa { get; set; } = string.Empty;
        public string? Adres { get; set; }
        public string? Telefon { get; set; }

        public override string ToString() => Nazwa;
    }

    public class NotatkaListItemDTO
    {
        public long NotatkaID { get; set; }
        public string TypSpotkania { get; set; } = string.Empty;
        public DateTime DataSpotkania { get; set; }
        public string Temat { get; set; } = string.Empty;
        public string TworcaNazwa { get; set; } = string.Empty;
        public string? KontrahentNazwa { get; set; }
        public DateTime DataUtworzenia { get; set; }
    }
}