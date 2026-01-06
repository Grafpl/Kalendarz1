using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Kalendarz1.CRM.DailyProspecting
{
    /// <summary>
    /// Model konfiguracji prospectingu dla handlowca.
    /// Odpowiada tabeli KonfiguracjaProspectingu w bazie danych.
    /// </summary>
    public class KonfiguracjaProspectingu : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private int _limitDzienny;
        private bool _aktywny;

        public int KonfigID { get; set; }
        public string HandlowiecID { get; set; }
        public string HandlowiecNazwa { get; set; }

        public int LimitDzienny
        {
            get => _limitDzienny;
            set
            {
                _limitDzienny = value;
                OnPropertyChanged(nameof(LimitDzienny));
            }
        }

        public TimeSpan GodzinaStart { get; set; } = new TimeSpan(9, 0, 0);
        public TimeSpan GodzinaKoniec { get; set; } = new TimeSpan(10, 30, 0);

        // Dni tygodnia jako string "1,2,3,4,5" (1=pon, 5=pt)
        public string DniTygodnia { get; set; } = "1,2,3,4,5";

        // Filtry - NULL oznacza "wszystkie"
        public string Wojewodztwa { get; set; }
        public string TypyKlientow { get; set; }
        public string PKD { get; set; }

        public int PriorytetMin { get; set; } = 1;
        public int PriorytetMax { get; set; } = 5;

        public bool Aktywny
        {
            get => _aktywny;
            set
            {
                _aktywny = value;
                OnPropertyChanged(nameof(Aktywny));
            }
        }

        public DateTime DataUtworzenia { get; set; }
        public DateTime? DataModyfikacji { get; set; }

        // Właściwości pomocnicze dla UI
        public string GodzinyDisplay => $"{GodzinaStart:hh\\:mm} - {GodzinaKoniec:hh\\:mm}";
        public string WojewodztwaDisplay => string.IsNullOrEmpty(Wojewodztwa) ? "Wszystkie" : Wojewodztwa;
        public string TypyDisplay => string.IsNullOrEmpty(TypyKlientow) ? "Wszystkie" : TypyKlientow;
        public string PriorytetDisplay => $"{PriorytetMin}-{PriorytetMax}";

        // Listy dla checkboxów w UI
        public List<string> WojewodztwaLista
        {
            get => string.IsNullOrEmpty(Wojewodztwa) ? new List<string>() : Wojewodztwa.Split(',').ToList();
            set => Wojewodztwa = value?.Any() == true ? string.Join(",", value) : null;
        }

        public List<string> TypyKlientowLista
        {
            get => string.IsNullOrEmpty(TypyKlientow) ? new List<string>() : TypyKlientow.Split(',').ToList();
            set => TypyKlientow = value?.Any() == true ? string.Join(",", value) : null;
        }

        public List<int> DniTygodniaLista
        {
            get => string.IsNullOrEmpty(DniTygodnia) ? new List<int>() : DniTygodnia.Split(',').Select(int.Parse).ToList();
            set => DniTygodnia = value?.Any() == true ? string.Join(",", value) : "1,2,3,4,5";
        }

        // Czy handlowiec pracuje w dany dzień
        public bool PracujeWDniu(DayOfWeek dzien)
        {
            // Konwersja: DayOfWeek.Monday = 1, ... DayOfWeek.Friday = 5
            int dzienNumer = dzien == DayOfWeek.Sunday ? 7 : (int)dzien;
            return DniTygodniaLista.Contains(dzienNumer);
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Statystyki prospectingu dla handlowca na dany dzień.
    /// Odpowiada tabeli StatystykiProspectingu.
    /// </summary>
    public class StatystykiProspectingu
    {
        public int StatID { get; set; }
        public string HandlowiecID { get; set; }
        public string HandlowiecNazwa { get; set; }
        public DateTime Data { get; set; }

        // Liczniki
        public int Przydzielone { get; set; }
        public int Wykonane { get; set; }
        public int Rozmowy { get; set; }
        public int Nieodebrane { get; set; }
        public int Callbacki { get; set; }
        public int Odmowy { get; set; }
        public int Oferty { get; set; }
        public int Pominiete { get; set; }

        // Metryki obliczane
        public decimal ProcentRealizacji => Przydzielone > 0 ? Math.Round(Wykonane * 100m / Przydzielone, 1) : 0;
        public decimal ProcentSkutecznosci => Wykonane > 0 ? Math.Round(Rozmowy * 100m / Wykonane, 1) : 0;

        // Dla progress bara
        public int Pozostalo => Przydzielone - Wykonane - Pominiete;
        public string ProgressDisplay => $"{Wykonane}/{Przydzielone}";
    }

    /// <summary>
    /// Dostępne typy klientów w systemie.
    /// </summary>
    public static class TypyKlientow
    {
        public const string Hurtownia = "Hurtownia";
        public const string Siec = "Sieć";
        public const string HoReCa = "HoReCa";
        public const string Przetwornia = "Przetwórnia";
        public const string CashAndCarry = "Cash&Carry";
        public const string Inny = "Inny";

        public static string[] Wszystkie => new[]
        {
            Hurtownia,
            Siec,
            HoReCa,
            Przetwornia,
            CashAndCarry,
            Inny
        };
    }

    /// <summary>
    /// Lista województw Polski.
    /// </summary>
    public static class Wojewodztwa
    {
        public static string[] Wszystkie => new[]
        {
            "dolnośląskie",
            "kujawsko-pomorskie",
            "lubelskie",
            "lubuskie",
            "łódzkie",
            "małopolskie",
            "mazowieckie",
            "opolskie",
            "podkarpackie",
            "podlaskie",
            "pomorskie",
            "śląskie",
            "świętokrzyskie",
            "warmińsko-mazurskie",
            "wielkopolskie",
            "zachodniopomorskie"
        };
    }
}
