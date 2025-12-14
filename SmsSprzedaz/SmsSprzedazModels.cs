using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kalendarz1.SmsSprzedaz
{
    /// <summary>
    /// Model informacji o wydaniu towaru do wysłania w SMS
    /// </summary>
    public class WydanieInfo
    {
        public int ZamowienieId { get; set; }
        public int KlientId { get; set; }
        public string KlientNazwa { get; set; } = "";
        public string Handlowiec { get; set; } = "";
        public string HandlowiecTelefon { get; set; } = "";
        public decimal IloscKg { get; set; }
        public DateTime? DataWydania { get; set; }
        public string KtoWydal { get; set; } = "";
        public TimeSpan? CzasWyjazdu { get; set; }
        public DateTime? DataKursu { get; set; }
        public string Kierowca { get; set; } = "";
        public string NumerRejestracyjny { get; set; } = "";
        public bool WlasnyTransport { get; set; }
        public DateTime? DataPrzyjazdu { get; set; } // Dla własnego transportu
    }

    /// <summary>
    /// Model historii wysłanych SMS-ów do handlowców
    /// </summary>
    public class SmsSprzedazHistoria
    {
        public long Id { get; set; }
        public int ZamowienieId { get; set; }
        public int KlientId { get; set; }
        public string KlientNazwa { get; set; } = "";
        public string Handlowiec { get; set; } = "";
        public string TelefonHandlowca { get; set; } = "";
        public string TrescSms { get; set; } = "";
        public decimal IloscKg { get; set; }
        public DateTime? CzasWyjazdu { get; set; }
        public DateTime DataWyslania { get; set; }
        public string KtoWyslal { get; set; } = "";
        public string Status { get; set; } = ""; // Wyslany, Blad, Kopiowany
        public string BladOpis { get; set; } = "";
    }

    /// <summary>
    /// Model konfiguracji SMS dla handlowca
    /// </summary>
    public class HandlowiecSmsConfig : INotifyPropertyChanged
    {
        private bool _smsAktywny = true;
        private string _telefon = "";
        private bool _smsPoWydaniu = true;
        private bool _smsZbiorcyDzienny = false;

        public string HandlowiecNazwa { get; set; } = "";

        public bool SmsAktywny
        {
            get => _smsAktywny;
            set { _smsAktywny = value; OnPropertyChanged(); }
        }

        public string Telefon
        {
            get => _telefon;
            set { _telefon = value; OnPropertyChanged(); }
        }

        public bool SmsPoWydaniu
        {
            get => _smsPoWydaniu;
            set { _smsPoWydaniu = value; OnPropertyChanged(); }
        }

        public bool SmsZbiorcyDzienny
        {
            get => _smsZbiorcyDzienny;
            set { _smsZbiorcyDzienny = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Szablon SMS dla sprzedaży
    /// </summary>
    public class SzablonSmsSprzedaz
    {
        /// <summary>
        /// Generuje treść SMS o wydaniu towaru
        /// </summary>
        /// <param name="info">Informacje o wydaniu</param>
        /// <returns>Treść SMS</returns>
        public static string GenerujTrescSms(WydanieInfo info)
        {
            // Format SMS:
            // WYDANO: [Klient] [Ilość]kg
            // Wyjazd: [Godzina] [Dzień]
            // [Kierowca] [Rejestracja]

            string czasWyjazdu;
            if (info.WlasnyTransport)
            {
                if (info.DataPrzyjazdu.HasValue)
                    czasWyjazdu = $"Odbiór własny: {info.DataPrzyjazdu.Value:HH:mm} {GetDzienTygodnia(info.DataPrzyjazdu.Value)}";
                else
                    czasWyjazdu = "Odbiór własny";
            }
            else if (info.CzasWyjazdu.HasValue && info.DataKursu.HasValue)
            {
                czasWyjazdu = $"Wyjazd: {info.CzasWyjazdu.Value:hh\\:mm} {GetDzienTygodnia(info.DataKursu.Value)}";
            }
            else
            {
                czasWyjazdu = "Transport: brak danych";
            }

            string transport;
            if (info.WlasnyTransport)
            {
                transport = "";
            }
            else
            {
                var kierowca = string.IsNullOrEmpty(info.Kierowca) ? "" : info.Kierowca;
                var rej = string.IsNullOrEmpty(info.NumerRejestracyjny) ? "" : info.NumerRejestracyjny;
                transport = !string.IsNullOrEmpty(kierowca) || !string.IsNullOrEmpty(rej)
                    ? $"\n{kierowca} {rej}".Trim()
                    : "";
            }

            return $"WYDANO: {info.KlientNazwa} {info.IloscKg:N0}kg\n{czasWyjazdu}{transport}";
        }

        /// <summary>
        /// Generuje zbiorczy SMS dzienny dla handlowca
        /// </summary>
        public static string GenerujSmsDziennyZbiorczy(string handlowiec, DateTime data,
            System.Collections.Generic.List<(string Klient, decimal Kg, string CzasWyjazdu)> wydania)
        {
            if (wydania == null || wydania.Count == 0)
                return "";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"WYDANIA {data:dd.MM}:");

            decimal sumaKg = 0;
            foreach (var w in wydania)
            {
                sb.AppendLine($"• {w.Klient}: {w.Kg:N0}kg ({w.CzasWyjazdu})");
                sumaKg += w.Kg;
            }

            sb.AppendLine($"RAZEM: {sumaKg:N0}kg");

            return sb.ToString().Trim();
        }

        private static string GetDzienTygodnia(DateTime date)
        {
            return date.DayOfWeek switch
            {
                DayOfWeek.Monday => "pon",
                DayOfWeek.Tuesday => "wt",
                DayOfWeek.Wednesday => "śr",
                DayOfWeek.Thursday => "czw",
                DayOfWeek.Friday => "pt",
                DayOfWeek.Saturday => "sob",
                DayOfWeek.Sunday => "nd",
                _ => ""
            };
        }
    }

    /// <summary>
    /// Wynik wysyłki SMS
    /// </summary>
    public class WynikWyslaniaSms
    {
        public bool Sukces { get; set; }
        public string Wiadomosc { get; set; } = "";
        public string SmsId { get; set; } = ""; // ID z Twilio
        public bool SkopiowaDoSchowka { get; set; }
    }
}
