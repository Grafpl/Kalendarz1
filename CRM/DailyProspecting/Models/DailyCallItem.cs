using System;
using System.ComponentModel;

namespace Kalendarz1.CRM.DailyProspecting
{
    /// <summary>
    /// Model pojedynczego telefonu w dziennej kolejce prospectingu.
    /// Reprezentuje rekord z tabeli CodzienaKolejkaTelefonow połączony z OdbiorcyCRM.
    /// </summary>
    public class DailyCallItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _statusRealizacji;
        private string _rezultatRozmowy;
        private string _notatka;

        // Identyfikatory
        public int KolejkaID { get; set; }
        public int OdbiorcaID { get; set; }
        public string HandlowiecID { get; set; }

        // Dane firmy
        public string NazwaFirmy { get; set; }
        public string Telefon { get; set; }
        public string Email { get; set; }
        public string Miasto { get; set; }
        public string Wojewodztwo { get; set; }
        public string Branza { get; set; }
        public string TypKlienta { get; set; }

        // Status w CRM
        public string StatusCRM { get; set; }
        public DateTime? DataNastepnegoKontaktu { get; set; }

        // Priorytet
        public int Priorytet { get; set; }
        public string PowodPriorytetu { get; set; }

        // Status realizacji telefonu
        public string StatusRealizacji
        {
            get => _statusRealizacji;
            set
            {
                _statusRealizacji = value;
                OnPropertyChanged(nameof(StatusRealizacji));
                OnPropertyChanged(nameof(CzyWykonano));
                OnPropertyChanged(nameof(CzyOczekuje));
            }
        }

        public DateTime? GodzinaWykonania { get; set; }

        public string RezultatRozmowy
        {
            get => _rezultatRozmowy;
            set
            {
                _rezultatRozmowy = value;
                OnPropertyChanged(nameof(RezultatRozmowy));
            }
        }

        public string Notatka
        {
            get => _notatka;
            set
            {
                _notatka = value;
                OnPropertyChanged(nameof(Notatka));
            }
        }

        // Ostatnia notatka z CRM
        public string OstatniaNot { get; set; }
        public int LiczbaNotatek { get; set; }

        // Właściwości pomocnicze
        public bool CzyWykonano => StatusRealizacji == "Wykonano";
        public bool CzyOczekuje => StatusRealizacji == "Oczekuje";
        public bool CzyPominiety => StatusRealizacji == "Pominięto";

        public string PriorytetDisplay => Priorytet switch
        {
            >= 100 => "!!! PILNE",
            >= 90 => "!! Gorący",
            >= 70 => "! Ważny",
            _ => "Normalny"
        };

        public string StatusCRMDisplay => StatusCRM switch
        {
            "Do zadzwonienia" => "Nowy",
            "Próba kontaktu" => "Próba",
            "Nawiązano kontakt" => "Kontakt",
            "Zgoda na dalszy kontakt" => "Zgoda!",
            "Do wysłania oferta" => "Oferta",
            _ => StatusCRM
        };

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Możliwe rezultaty rozmowy telefonicznej.
    /// </summary>
    public static class RezultatyRozmowy
    {
        public const string Rozmowa = "Rozmowa";
        public const string Nieodebrany = "Nieodebrany";
        public const string Callback = "Callback";
        public const string Odmowa = "Odmowa";
        public const string Oferta = "Oferta";
        public const string ZlyNumer = "Zły numer";
        public const string Poczta = "Poczta głosowa";

        public static string[] Wszystkie => new[]
        {
            Rozmowa,
            Nieodebrany,
            Callback,
            Odmowa,
            Oferta,
            ZlyNumer,
            Poczta
        };
    }

    /// <summary>
    /// Możliwe statusy realizacji telefonu w kolejce.
    /// </summary>
    public static class StatusyRealizacji
    {
        public const string Oczekuje = "Oczekuje";
        public const string Wykonano = "Wykonano";
        public const string Pominieto = "Pominięto";
        public const string Przeniesiono = "Przeniesiono";
    }
}
