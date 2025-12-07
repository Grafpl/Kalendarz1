using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kalendarz1.HandlowiecDashboard.Models
{
    /// <summary>
    /// Podsumowanie KPI dla handlowca
    /// </summary>
    public class HandlowiecKPI
    {
        // Bieżący miesiąc
        public int LiczbaZamowienMiesiac { get; set; }
        public decimal SumaKgMiesiac { get; set; }
        public decimal SumaWartoscMiesiac { get; set; }
        public int LiczbaOdbiorcowMiesiac { get; set; }
        public decimal SredniWartoscZamowienia { get; set; }

        // Poprzedni miesiąc (do porównania)
        public int LiczbaZamowienPoprzedni { get; set; }
        public decimal SumaKgPoprzedni { get; set; }
        public decimal SumaWartoscPoprzedni { get; set; }
        public int LiczbaOdbiorcowPoprzedni { get; set; }

        // Zmiany procentowe
        public decimal ZmianaZamowienProcent => LiczbaZamowienPoprzedni > 0
            ? ((decimal)(LiczbaZamowienMiesiac - LiczbaZamowienPoprzedni) / LiczbaZamowienPoprzedni) * 100
            : 0;

        public decimal ZmianaKgProcent => SumaKgPoprzedni > 0
            ? ((SumaKgMiesiac - SumaKgPoprzedni) / SumaKgPoprzedni) * 100
            : 0;

        public decimal ZmianaWartoscProcent => SumaWartoscPoprzedni > 0
            ? ((SumaWartoscMiesiac - SumaWartoscPoprzedni) / SumaWartoscPoprzedni) * 100
            : 0;

        public decimal ZmianaOdbiorcowProcent => LiczbaOdbiorcowPoprzedni > 0
            ? ((decimal)(LiczbaOdbiorcowMiesiac - LiczbaOdbiorcowPoprzedni) / LiczbaOdbiorcowPoprzedni) * 100
            : 0;

        // Formatowanie dla wyświetlania
        public string LiczbaZamowienTekst => $"{LiczbaZamowienMiesiac:N0}";
        public string SumaKgTekst => $"{SumaKgMiesiac:N0} kg";
        public string SumaWartoscTekst => $"{SumaWartoscMiesiac:N2} zł";
        public string LiczbaOdbiorcowTekst => $"{LiczbaOdbiorcowMiesiac:N0}";
        public string SredniWartoscTekst => $"{SredniWartoscZamowienia:N2} zł";

        public string ZmianaZamowienTekst => FormatZmiana(ZmianaZamowienProcent);
        public string ZmianaKgTekst => FormatZmiana(ZmianaKgProcent);
        public string ZmianaWartoscTekst => FormatZmiana(ZmianaWartoscProcent);
        public string ZmianaOdbiorcowTekst => FormatZmiana(ZmianaOdbiorcowProcent);

        private string FormatZmiana(decimal zmiana)
        {
            var prefix = zmiana >= 0 ? "+" : "";
            return $"{prefix}{zmiana:N1}%";
        }

        public bool ZmianaZamowienPozytywna => ZmianaZamowienProcent >= 0;
        public bool ZmianaKgPozytywna => ZmianaKgProcent >= 0;
        public bool ZmianaWartoscPozytywna => ZmianaWartoscProcent >= 0;
        public bool ZmianaOdbiorcowPozytywna => ZmianaOdbiorcowProcent >= 0;
    }

    /// <summary>
    /// Dane miesięczne dla wykresów trendów
    /// </summary>
    public class DaneMiesieczne
    {
        public int Rok { get; set; }
        public int Miesiac { get; set; }
        public string MiesiacNazwa { get; set; }
        public string MiesiacKrotki => $"{Miesiac:00}/{Rok % 100}";
        public int LiczbaZamowien { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SumaWartosc { get; set; }
        public int LiczbaOdbiorcow { get; set; }
        public decimal SredniaCena { get; set; }
    }

    /// <summary>
    /// Top odbiorca handlowca
    /// </summary>
    public class TopOdbiorca
    {
        public int Pozycja { get; set; }
        public int OdbiorcaId { get; set; }
        public string Nazwa { get; set; }
        public string Miasto { get; set; }
        public int LiczbaZamowien { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SumaWartosc { get; set; }
        public decimal UdzialProcent { get; set; }

        public string SumaKgTekst => $"{SumaKg:N0} kg";
        public string SumaWartoscTekst => $"{SumaWartosc:N2} zł";
        public string UdzialTekst => $"{UdzialProcent:N1}%";
    }

    /// <summary>
    /// Statystyka kategorii produktów
    /// </summary>
    public class KategoriaProduktow
    {
        public string Nazwa { get; set; }
        public string Kod { get; set; }
        public int LiczbaZamowien { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SumaWartosc { get; set; }
        public decimal UdzialProcent { get; set; }

        public string SumaKgTekst => $"{SumaKg:N0} kg";
        public string SumaWartoscTekst => $"{SumaWartosc:N2} zł";
        public string UdzialTekst => $"{UdzialProcent:N1}%";
    }

    /// <summary>
    /// Statystyka statusów zamówień
    /// </summary>
    public class StatusZamowienia
    {
        public string Status { get; set; }
        public int Liczba { get; set; }
        public decimal Procent { get; set; }
        public string Kolor { get; set; }

        public string ProcentTekst => $"{Procent:N1}%";
    }

    /// <summary>
    /// Porównanie miesięcy
    /// </summary>
    public class PorownanieOkresu
    {
        public string OkresNazwa { get; set; }
        public int LiczbaZamowien { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SumaWartosc { get; set; }
        public int LiczbaOdbiorcow { get; set; }
        public bool JestBiezacy { get; set; }
    }

    /// <summary>
    /// Zamówienie dla listy ostatnich zamówień
    /// </summary>
    public class OstatnieZamowienie
    {
        public int Id { get; set; }
        public DateTime DataOdbioru { get; set; }
        public string Odbiorca { get; set; }
        public decimal Kg { get; set; }
        public decimal Wartosc { get; set; }
        public string Status { get; set; }
        public string TransportStatus { get; set; }

        public string DataOdbioruTekst => DataOdbioru.ToString("dd.MM.yyyy");
        public string KgTekst => $"{Kg:N0} kg";
        public string WartoscTekst => Wartosc > 0 ? $"{Wartosc:N2} zł" : "-";
        public string TransportTekst => TransportStatus == "Wlasny" ? "Własny" : "Dostawa";
    }

    /// <summary>
    /// Filtr dla dashboardu
    /// </summary>
    public class DashboardFiltr : INotifyPropertyChanged
    {
        private DateTime? _dataOd;
        private DateTime? _dataDo;
        private string _handlowiec;
        private string _okresTyp = "Miesiąc";

        public DateTime? DataOd
        {
            get => _dataOd;
            set { _dataOd = value; OnPropertyChanged(); }
        }

        public DateTime? DataDo
        {
            get => _dataDo;
            set { _dataDo = value; OnPropertyChanged(); }
        }

        public string Handlowiec
        {
            get => _handlowiec;
            set { _handlowiec = value; OnPropertyChanged(); }
        }

        public string OkresTyp
        {
            get => _okresTyp;
            set { _okresTyp = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Dane wykresu słupkowego
    /// </summary>
    public class WykresSlupkowy
    {
        public List<string> Etykiety { get; set; } = new List<string>();
        public List<double> Wartosci { get; set; } = new List<double>();
        public string Tytul { get; set; }
        public string JednostkaTekst { get; set; }
    }

    /// <summary>
    /// Dane wykresu liniowego
    /// </summary>
    public class WykresLiniowy
    {
        public List<string> Etykiety { get; set; } = new List<string>();
        public List<SeriaWykresu> Serie { get; set; } = new List<SeriaWykresu>();
        public string Tytul { get; set; }
    }

    /// <summary>
    /// Seria danych dla wykresu
    /// </summary>
    public class SeriaWykresu
    {
        public string Nazwa { get; set; }
        public List<double> Wartosci { get; set; } = new List<double>();
        public string Kolor { get; set; }
    }

    /// <summary>
    /// Dane dzienne dla wykresów (sprzedaż dzień po dniu)
    /// </summary>
    public class DaneDzienne
    {
        public DateTime Data { get; set; }
        public int LiczbaZamowien { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SumaWartosc { get; set; }
        public int LiczbaOdbiorcow { get; set; }

        public string DataTekst => Data.ToString("dd.MM");
        public string DzienTygodnia => Data.ToString("ddd", new System.Globalization.CultureInfo("pl-PL"));
        public string WartoscTekst => $"{SumaWartosc:N0}";
    }

    /// <summary>
    /// Sprzedaż według regionu (województwa)
    /// </summary>
    public class SprzedazRegionalna
    {
        public int Pozycja { get; set; }
        public string Wojewodztwo { get; set; }
        public int LiczbaZamowien { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SumaWartosc { get; set; }
        public int LiczbaOdbiorcow { get; set; }
        public decimal UdzialProcent { get; set; }

        public string WartoscTekst => $"{SumaWartosc:N0} zł";
        public string KgTekst => $"{SumaKg:N0} kg";
        public string UdzialTekst => $"{UdzialProcent:N1}%";
    }

    /// <summary>
    /// Statystyki CRM dla handlowca
    /// </summary>
    public class CRMStatystyki
    {
        // Dzisiejsze zadania
        public int KontaktyDzisiaj { get; set; }
        public int KontaktyZalegle { get; set; }
        public int ProbyKontaktu { get; set; }
        public int NawiazaneKontakty { get; set; }
        public int ZgodyNaKontakt { get; set; }
        public int DoWyslaniOferty { get; set; }
        public int PriorytetoweBranze { get; set; }
        public int RazemAktywnych { get; set; }

        // Notatki i aktywność
        public int NotatekDzisiaj { get; set; }
        public int NotatekTenTydzien { get; set; }
        public int ZmianStatusuDzisiaj { get; set; }
        public int ZmianStatusuTenMiesiac { get; set; }

        // Teksty formatowane
        public string KontaktyDzisiajTekst => KontaktyDzisiaj.ToString("N0");
        public string KontaktyZalegleTekst => KontaktyZalegle.ToString("N0");
        public string AktywnoscTekst => $"{NotatekTenTydzien} notatek / {ZmianStatusuTenMiesiac} zmian";
    }

    /// <summary>
    /// Podsumowanie 30-dniowe (jak na screenshocie Amazon)
    /// </summary>
    public class Podsumowanie30Dni
    {
        public decimal SumaSprzedazy { get; set; }
        public int LiczbaZamowien { get; set; }
        public int ZwrotyAnulowane { get; set; }
        public decimal SredniaWartoscZamowienia { get; set; }
        public decimal SredniaCenaKg { get; set; }

        public string SumaSprzedazyTekst => $"{SumaSprzedazy:N0} zł";
        public string ZamowieniaTekst => LiczbaZamowien.ToString("N0");
        public string ZwrotyTekst => ZwrotyAnulowane.ToString("N0");
        public string SredniaTekst => $"{SredniaWartoscZamowienia:N0} zł";
        public string SredniaCenaTekst => $"{SredniaCenaKg:N2} zł/kg";
    }

    /// <summary>
    /// Średnia wartość zamówienia dziennie z porównaniem tygodniowym
    /// </summary>
    public class SredniaZamowieniaDziennie
    {
        public DateTime Data { get; set; }
        public decimal SredniaTenTydzien { get; set; }
        public decimal SredniaPoprzedniTydzien { get; set; }
        public decimal CelTygodniowy { get; set; }

        public string DataTekst => Data.ToString("dd.MM");
        public string DzienTekst => Data.ToString("ddd", new System.Globalization.CultureInfo("pl-PL"));
    }

    /// <summary>
    /// Statystyki transportu / dostawy
    /// </summary>
    public class StatystykiDostawy
    {
        public string TypDostawy { get; set; }
        public int Liczba { get; set; }
        public decimal Procent { get; set; }
        public string Kolor { get; set; }

        public string ProcentTekst => $"{Procent:N1}%";
    }

    /// <summary>
    /// Ranking handlowców (dla managera)
    /// </summary>
    public class RankingHandlowca
    {
        public int Pozycja { get; set; }
        public string Handlowiec { get; set; }
        public int LiczbaZamowien { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SumaWartosc { get; set; }
        public int LiczbaOdbiorcow { get; set; }
        public decimal ZmianaProcent { get; set; }

        public string WartoscTekst => $"{SumaWartosc:N0} zł";
        public string ZmianaTekst => $"{(ZmianaProcent >= 0 ? "+" : "")}{ZmianaProcent:N1}%";
        public bool ZmianaPozytywna => ZmianaProcent >= 0;
    }
}
