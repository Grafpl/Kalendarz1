using System;
using System.Windows.Media;

namespace Kalendarz1.CRM.Models
{
    public class ContactToCall
    {
        public int ID { get; set; }
        public string Nazwa { get; set; }
        public string Telefon { get; set; }
        public string Email { get; set; }
        public string Miasto { get; set; }
        public string Wojewodztwo { get; set; }
        public string Status { get; set; }
        public string Branza { get; set; }
        public string OstatniaNota { get; set; }
        public DateTime? DataOstatniejNotatki { get; set; }

        // UI helpers
        public bool WasCalled { get; set; }
        public bool NoteAdded { get; set; }
        public bool StatusChanged { get; set; }
        public string NewStatus { get; set; }

        public bool IsCompleted => WasCalled || NoteAdded || StatusChanged;

        public SolidColorBrush StatusColor
        {
            get
            {
                return Status switch
                {
                    "Do zadzwonienia" => new SolidColorBrush(Color.FromRgb(100, 116, 139)), // slate
                    "Próba kontaktu" => new SolidColorBrush(Color.FromRgb(249, 115, 22)), // orange
                    "Nawiązano kontakt" => new SolidColorBrush(Color.FromRgb(34, 197, 94)), // green
                    "Zgoda na dalszy kontakt" => new SolidColorBrush(Color.FromRgb(20, 184, 166)), // teal
                    "Do wysłania oferta" => new SolidColorBrush(Color.FromRgb(8, 145, 178)), // cyan
                    "Nie zainteresowany" => new SolidColorBrush(Color.FromRgb(239, 68, 68)), // red
                    _ => new SolidColorBrush(Color.FromRgb(148, 163, 184)) // gray
                };
            }
        }

        public SolidColorBrush StatusBackground
        {
            get
            {
                return Status switch
                {
                    "Do zadzwonienia" => new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                    "Próba kontaktu" => new SolidColorBrush(Color.FromRgb(255, 237, 213)),
                    "Nawiązano kontakt" => new SolidColorBrush(Color.FromRgb(220, 252, 231)),
                    "Zgoda na dalszy kontakt" => new SolidColorBrush(Color.FromRgb(204, 251, 241)),
                    "Do wysłania oferta" => new SolidColorBrush(Color.FromRgb(207, 250, 254)),
                    "Nie zainteresowany" => new SolidColorBrush(Color.FromRgb(254, 226, 226)),
                    _ => new SolidColorBrush(Color.FromRgb(241, 245, 249))
                };
            }
        }

        public string StatusShort
        {
            get
            {
                return Status switch
                {
                    "Do zadzwonienia" => "Do zadzw.",
                    "Próba kontaktu" => "Próba",
                    "Nawiązano kontakt" => "Kontakt",
                    "Zgoda na dalszy kontakt" => "Zgoda",
                    "Do wysłania oferta" => "Oferta",
                    "Nie zainteresowany" => "Odmowa",
                    _ => Status?.Length > 10 ? Status.Substring(0, 10) + "..." : Status
                };
            }
        }
    }
}
