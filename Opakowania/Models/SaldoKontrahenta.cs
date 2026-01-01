using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Kalendarz1.Opakowania.Models
{
    /// <summary>
    /// Główny model - saldo wszystkich opakowań dla kontrahenta
    /// </summary>
    public class SaldoKontrahenta : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Kontrahent { get; set; }  // Shortcut
        public string Nazwa { get; set; }
        public string Handlowiec { get; set; }

        // Salda wszystkich typów
        public int E2 { get; set; }
        public int H1 { get; set; }
        public int EURO { get; set; }
        public int PCV { get; set; }
        public int DREW { get; set; }

        // Daty ostatnich dokumentów
        public DateTime? OstatniDokument { get; set; }

        // Potwierdzenia (per typ opakowania)
        public bool E2Potwierdzone { get; set; }
        public bool H1Potwierdzone { get; set; }
        public bool EUROPotwierdzone { get; set; }
        public bool PCVPotwierdzone { get; set; }
        public bool DREWPotwierdzone { get; set; }

        public DateTime? E2DataPotwierdzenia { get; set; }
        public DateTime? H1DataPotwierdzenia { get; set; }
        public DateTime? EURODataPotwierdzenia { get; set; }
        public DateTime? PCVDataPotwierdzenia { get; set; }
        public DateTime? DREWDataPotwierdzenia { get; set; }

        // Dane kontaktowe
        public string Email { get; set; }
        public string Telefon { get; set; }

        // === Obliczane ===

        public int SumaWszystkich => Math.Abs(E2) + Math.Abs(H1) + Math.Abs(EURO) + Math.Abs(PCV) + Math.Abs(DREW);
        public bool MaSaldo => SumaWszystkich > 0;

        // Formatowanie tekstowe
        public string E2Tekst => FormatSaldo(E2);
        public string H1Tekst => FormatSaldo(H1);
        public string EUROTekst => FormatSaldo(EURO);
        public string PCVTekst => FormatSaldo(PCV);
        public string DREWTekst => FormatSaldo(DREW);

        public string OstatniDokumentTekst => OstatniDokument?.ToString("dd.MM.yy") ?? "-";

        // Kolory
        public SolidColorBrush E2Kolor => GetKolor(E2);
        public SolidColorBrush H1Kolor => GetKolor(H1);
        public SolidColorBrush EUROKolor => GetKolor(EURO);
        public SolidColorBrush PCVKolor => GetKolor(PCV);
        public SolidColorBrush DREWKolor => GetKolor(DREW);

        private static string FormatSaldo(int s)
        {
            if (s == 0) return "-";
            return s > 0 ? $"{s}" : $"{s}";
        }

        private static SolidColorBrush GetKolor(int saldo)
        {
            if (saldo > 0) return new SolidColorBrush(Color.FromRgb(220, 38, 38));   // Czerwony - kontrahent winny
            if (saldo < 0) return new SolidColorBrush(Color.FromRgb(22, 163, 74));   // Zielony - my winni
            return new SolidColorBrush(Color.FromRgb(156, 163, 175));                 // Szary - zero
        }

        // Pobierz saldo dla danego typu
        public int GetSaldo(string typ) => typ switch
        {
            "E2" => E2,
            "H1" => H1,
            "EURO" => EURO,
            "PCV" => PCV,
            "DREW" => DREW,
            _ => 0
        };

        public bool GetPotwierdzone(string typ) => typ switch
        {
            "E2" => E2Potwierdzone,
            "H1" => H1Potwierdzone,
            "EURO" => EUROPotwierdzone,
            "PCV" => PCVPotwierdzone,
            "DREW" => DREWPotwierdzone,
            _ => false
        };

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Dokument opakowania (MW/MP)
    /// </summary>
    public class DokumentSalda
    {
        public int Id { get; set; }
        public string NrDokumentu { get; set; }
        public DateTime Data { get; set; }
        public string Opis { get; set; }

        public int E2 { get; set; }
        public int H1 { get; set; }
        public int EURO { get; set; }
        public int PCV { get; set; }
        public int DREW { get; set; }

        public bool JestSaldem { get; set; }

        // Formatowanie
        public string DataTekst => Data.ToString("dd.MM.yyyy");
        public string DzienTygodnia => Data.ToString("ddd");

        public string E2Tekst => FormatIlosc(E2);
        public string H1Tekst => FormatIlosc(H1);
        public string EUROTekst => FormatIlosc(EURO);
        public string PCVTekst => FormatIlosc(PCV);
        public string DREWTekst => FormatIlosc(DREW);

        private static string FormatIlosc(int i)
        {
            if (i == 0) return "-";
            return i.ToString();
        }

        // Kolory
        public SolidColorBrush E2Kolor => GetKolor(E2);
        public SolidColorBrush H1Kolor => GetKolor(H1);
        public SolidColorBrush EUROKolor => GetKolor(EURO);
        public SolidColorBrush PCVKolor => GetKolor(PCV);
        public SolidColorBrush DREWKolor => GetKolor(DREW);

        public SolidColorBrush TloWiersza => JestSaldem
            ? new SolidColorBrush(Color.FromArgb(40, 59, 130, 246))  // Niebieskie tło dla salda
            : new SolidColorBrush(Colors.Transparent);

        private static SolidColorBrush GetKolor(int val)
        {
            if (val > 0) return new SolidColorBrush(Color.FromRgb(220, 38, 38));   // Czerwony - wydanie
            if (val < 0) return new SolidColorBrush(Color.FromRgb(22, 163, 74));   // Zielony - przyjęcie
            return new SolidColorBrush(Color.FromRgb(156, 163, 175));               // Szary
        }
    }

    /// <summary>
    /// Potwierdzenie salda
    /// </summary>
    public class Potwierdzenie
    {
        public int Id { get; set; }
        public int KontrahentId { get; set; }
        public string TypOpakowania { get; set; }
        public DateTime DataPotwierdzenia { get; set; }
        public int IloscPotwierdzona { get; set; }
        public int SaldoSystemowe { get; set; }
        public string Status { get; set; }  // Potwierdzone, Rozbieżność, Oczekujące
        public string Uwagi { get; set; }
        public string Uzytkownik { get; set; }
        public DateTime DataWprowadzenia { get; set; }

        public string DataPotwierdzeniaTekst => DataPotwierdzenia.ToString("dd.MM.yyyy");
        public int Roznica => IloscPotwierdzona - SaldoSystemowe;

        public SolidColorBrush StatusKolor => Status switch
        {
            "Potwierdzone" => new SolidColorBrush(Color.FromRgb(22, 163, 74)),
            "Rozbieżność" => new SolidColorBrush(Color.FromRgb(220, 38, 38)),
            _ => new SolidColorBrush(Color.FromRgb(245, 158, 11))
        };
    }
}
