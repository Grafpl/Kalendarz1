using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kalendarz1.Kartoteka.Models
{
    public class Odbiorca : INotifyPropertyChanged
    {
        public int IdSymfonia { get; set; }
        public string NazwaFirmy { get; set; }
        public string Miasto { get; set; }
        public string Ulica { get; set; }
        public string KodPocztowy { get; set; }
        public string NIP { get; set; }
        public string FormaPlatnosci { get; set; }
        public int TerminPlatnosci { get; set; }
        public decimal LimitKupiecki { get; set; }
        public string Handlowiec { get; set; }

        // Dane finansowe (obliczane)
        public decimal WykorzystanoLimit { get; set; }
        public decimal KwotaPrzeterminowana { get; set; }
        public DateTime? OstatnieZamowienie { get; set; }
        public bool CzyNowyKlient { get; set; }
        public bool IsActive { get; set; } = true;

        // Dane własne handlowca (z LibraNet)
        public string OsobaKontaktowa { get; set; }
        public string TelefonKontakt { get; set; }
        public string EmailKontakt { get; set; }
        public string Asortyment { get; set; }
        public string PreferencjePakowania { get; set; }
        public string PreferencjeJakosci { get; set; }
        public string PreferencjeDostawy { get; set; }
        public string PreferowanyDzienDostawy { get; set; }
        public string PreferowanaGodzinaDostawy { get; set; }
        public string AdresDostawyInny { get; set; }
        public string Trasa { get; set; }

        private string _kategoriaHandlowca = "C";
        public string KategoriaHandlowca
        {
            get => _kategoriaHandlowca;
            set { _kategoriaHandlowca = value; OnPropertyChanged(); }
        }

        public string Notatki { get; set; }
        public DateTime? DataModyfikacji { get; set; }
        public string ModyfikowalPrzez { get; set; }

        // Właściwości obliczane
        public double ProcentWykorzystania =>
            LimitKupiecki > 0 ? (double)(WykorzystanoLimit / LimitKupiecki * 100) : 0;

        public bool PrzekroczonyLimit => LimitKupiecki > 0 && WykorzystanoLimit > LimitKupiecki;

        public decimal WolnyLimit => Math.Max(0, LimitKupiecki - WykorzystanoLimit);

        public int DniOdOstatniegoZamowienia =>
            OstatnieZamowienie.HasValue ? (int)(DateTime.Now - OstatnieZamowienie.Value).TotalDays : 999;

        public string AsortymentSkrocony =>
            string.IsNullOrEmpty(Asortyment) ? "" :
            Asortyment.Length > 30 ? Asortyment.Substring(0, 30) + "..." : Asortyment;

        public string AlertType
        {
            get
            {
                if (PrzekroczonyLimit) return "LimitExceeded";
                if (KwotaPrzeterminowana > 0) return "Overdue";
                if (DniOdOstatniegoZamowienia >= 30) return "Inactive";
                if (CzyNowyKlient) return "NewClient";
                return "None";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
