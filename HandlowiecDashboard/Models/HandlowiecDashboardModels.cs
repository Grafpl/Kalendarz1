using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

    /// <summary>
    /// Analiza cen produktu dla handlowca (z Faktur Sprzedaży)
    /// </summary>
    public class AnalizaCenHandlowca
    {
        public string Handlowiec { get; set; }
        public string Produkt { get; set; }
        public decimal SredniaCena { get; set; }
        public decimal CenaWczoraj { get; set; }
        public decimal CenaDzisiaj { get; set; }
        public decimal ZmianaZl { get; set; }
        public decimal ZmianaProcent { get; set; }
        public decimal MinCena { get; set; }
        public decimal MaxCena { get; set; }
        public int LiczbaTransakcji { get; set; }
        public decimal TrendProcentowy { get; set; }

        public string SredniaCenaTekst => $"{SredniaCena:N2} zł/kg";
        public string ZmianaTekst => $"{(ZmianaZl >= 0 ? "+" : "")}{ZmianaZl:N2} zł";
        public string TrendTekst => $"{(TrendProcentowy >= 0 ? "+" : "")}{TrendProcentowy:N1}%";
        public bool ZmianaPozytywna => ZmianaZl >= 0;
        public bool TrendPozytywny => TrendProcentowy >= 0;
    }

    /// <summary>
    /// Udział handlowca w sprzedaży (z Faktur)
    /// </summary>
    public class UdzialHandlowcaWSprzedazy
    {
        public string Handlowiec { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SumaWartosc { get; set; }
        public decimal UdzialProcent { get; set; }
        public int LiczbaFaktur { get; set; }
        public int LiczbaOdbiorcow { get; set; }
        public int Pozycja { get; set; }

        public string WartoscTekst => $"{SumaWartosc:N0} zł";
        public string KgTekst => $"{SumaKg:N0} kg";
        public string UdzialTekst => $"{UdzialProcent:N1}%";
    }

    /// <summary>
    /// Zamówienia na dziś/jutro (z Zamówień Klientów)
    /// </summary>
    public class ZamowieniaNaDzien
    {
        public int LiczbaZamowienDzis { get; set; }
        public decimal SumaKgDzis { get; set; }
        public decimal SumaWartoscDzis { get; set; }
        public int LiczbaZamowienJutro { get; set; }
        public decimal SumaKgJutro { get; set; }
        public decimal SumaWartoscJutro { get; set; }
        public int ZamowieniaNieprzypisane { get; set; }
        public int ZamowieniaWRealizacji { get; set; }

        public string DzisTekst => $"{LiczbaZamowienDzis} zam. / {SumaKgDzis:N0} kg";
        public string JutroTekst => $"{LiczbaZamowienJutro} zam. / {SumaKgJutro:N0} kg";
    }

    /// <summary>
    /// Płatności i zaległości odbiorców (z Faktur)
    /// </summary>
    public class StatystykiPlatnosci
    {
        public int LiczbaOdbiorcowZZalegloscia { get; set; }
        public decimal SumaZaleglosci { get; set; }
        public decimal NajwiekszaZaleglosc { get; set; }
        public string NajwiekszyDluznik { get; set; }
        public int FakturyPoPlatnosciDoTygodnia { get; set; }
        public int FakturyPoPlatnosciPonadTydzien { get; set; }

        public string SumaZalegosciTekst => $"{SumaZaleglosci:N0} zł";
    }

    /// <summary>
    /// Top produkt handlowca (z Faktur)
    /// </summary>
    public class TopProduktHandlowca
    {
        public int Pozycja { get; set; }
        public string NazwaProduktu { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SumaWartosc { get; set; }
        public decimal SredniaCena { get; set; }
        public int LiczbaTransakcji { get; set; }

        public string WartoscTekst => $"{SumaWartosc:N0} zł";
        public string KgTekst => $"{SumaKg:N0} kg";
        public string CenaTekst => $"{SredniaCena:N2} zł/kg";
    }

    #region Performance Optimization Classes

    /// <summary>
    /// Cache dla danych dashboardu - przechowuje dane przez określony czas
    /// </summary>
    public class DashboardCache
    {
        private readonly Dictionary<string, CacheEntry> _cache = new Dictionary<string, CacheEntry>();
        private readonly object _lock = new object();

        /// <summary>
        /// Domyślny czas wygaśnięcia wpisu w cache
        /// </summary>
        public TimeSpan DefaultExpiry { get; set; } = TimeSpan.FromMinutes(5);

        private class CacheEntry
        {
            public object Data { get; set; }
            public DateTime CreatedAt { get; set; }
            public TimeSpan Expiry { get; set; }

            public bool IsExpired => DateTime.Now - CreatedAt > Expiry;
        }

        /// <summary>
        /// Pobiera dane z cache jeśli istnieją i nie wygasły
        /// </summary>
        public T Get<T>(string key) where T : class
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired)
                {
                    return entry.Data as T;
                }
                return null;
            }
        }

        /// <summary>
        /// Zapisuje dane w cache
        /// </summary>
        public void Set<T>(string key, T data, TimeSpan? expiry = null)
        {
            lock (_lock)
            {
                _cache[key] = new CacheEntry
                {
                    Data = data,
                    CreatedAt = DateTime.Now,
                    Expiry = expiry ?? DefaultExpiry
                };
            }
        }

        /// <summary>
        /// Sprawdza czy klucz istnieje w cache i nie wygasł
        /// </summary>
        public bool Contains(string key)
        {
            lock (_lock)
            {
                return _cache.TryGetValue(key, out var entry) && !entry.IsExpired;
            }
        }

        /// <summary>
        /// Unieważnia wpisy w cache pasujące do wzorca
        /// </summary>
        public void Invalidate(string keyPattern = null)
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(keyPattern))
                {
                    _cache.Clear();
                }
                else
                {
                    var keysToRemove = _cache.Keys.Where(k => k.Contains(keyPattern)).ToList();
                    foreach (var key in keysToRemove)
                    {
                        _cache.Remove(key);
                    }
                }
            }
        }

        /// <summary>
        /// Pobiera dane z cache lub ładuje je asynchronicznie jeśli nie istnieją
        /// </summary>
        public async System.Threading.Tasks.Task<T> GetOrLoadAsync<T>(string key, Func<System.Threading.Tasks.Task<T>> loader, TimeSpan? expiry = null) where T : class
        {
            var cached = Get<T>(key);
            if (cached != null) return cached;

            var data = await loader();
            Set(key, data, expiry);
            return data;
        }

        /// <summary>
        /// Usuwa wygasłe wpisy z cache
        /// </summary>
        public void CleanupExpired()
        {
            lock (_lock)
            {
                var expiredKeys = _cache.Where(kv => kv.Value.IsExpired).Select(kv => kv.Key).ToList();
                foreach (var key in expiredKeys)
                {
                    _cache.Remove(key);
                }
            }
        }

        /// <summary>
        /// Zwraca liczbę wpisów w cache
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _cache.Count;
                }
            }
        }
    }

    /// <summary>
    /// Kontener na wszystkie dane dashboardu (dla Multiple Result Sets)
    /// </summary>
    public class DashboardCompleteData
    {
        public DashboardKPI KPI { get; set; }
        public List<SprzedazDziennaItem> SprzedazDzienna { get; set; } = new List<SprzedazDziennaItem>();
        public List<TopOdbiorca> TopOdbiorcy { get; set; } = new List<TopOdbiorca>();
        public List<SprzedazHandlowca> SprzedazHandlowcy { get; set; } = new List<SprzedazHandlowca>();
        public List<UdzialHandlowcaWSprzedazy> UdzialHandlowcow { get; set; } = new List<UdzialHandlowcaWSprzedazy>();
        public List<AnalizaCenHandlowca> AnalizaCen { get; set; } = new List<AnalizaCenHandlowca>();
        public DateTime LoadedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Dane KPI dla dashboardu
    /// </summary>
    public class DashboardKPI
    {
        public int ZamowieniaDzis { get; set; }
        public decimal KgDzis { get; set; }
        public int ZamowieniaJutro { get; set; }
        public decimal KgJutro { get; set; }
        public decimal WartoscDzis { get; set; }
        public decimal WartoscJutro { get; set; }
    }

    /// <summary>
    /// Sprzedaż dzienna dla wykresów trendu
    /// </summary>
    public class SprzedazDziennaItem
    {
        public DateTime Dzien { get; set; }
        public int LiczbaZamowien { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SumaWartosc { get; set; }
    }

    /// <summary>
    /// Sprzedaż handlowca dla wykresów
    /// </summary>
    public class SprzedazHandlowca
    {
        public string Handlowiec { get; set; }
        public int LiczbaZamowien { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SumaWartosc { get; set; }
    }

    /// <summary>
    /// Dane płatności z bazy Handel
    /// </summary>
    public class DanePlatnosciHandel
    {
        public decimal SumaDoZaplaty { get; set; }
        public decimal SumaTerminowe { get; set; }
        public decimal SumaPrzeterminowane { get; set; }
        public int LiczbaKlientow { get; set; }
        public int LiczbaKlientowPrzeterminowanych { get; set; }
        public List<PlatnoscKontrahentaRow> Kontrahenci { get; set; } = new List<PlatnoscKontrahentaRow>();
    }

    /// <summary>
    /// Wiersz płatności kontrahenta
    /// </summary>
    public class PlatnoscKontrahentaRow
    {
        public string Kontrahent { get; set; }
        public string Handlowiec { get; set; }
        public decimal LimitKredytu { get; set; }
        public decimal DoZaplaty { get; set; }
        public decimal Terminowe { get; set; }
        public decimal Przeterminowane { get; set; }
        public decimal PrzekroczonyLimit { get; set; }
        public int? DniPrzeterminowania { get; set; }
    }

    /// <summary>
    /// Dane opakowań z bazy Handel
    /// </summary>
    public class DaneOpakowanHandel
    {
        public decimal SumaE2 { get; set; }
        public decimal SumaH1 { get; set; }
        public decimal ZmianaE2 { get; set; }
        public decimal ZmianaH1 { get; set; }
        public List<OpakowanieKontrahentaRow> Kontrahenci { get; set; } = new List<OpakowanieKontrahentaRow>();
    }

    /// <summary>
    /// Wiersz opakowań kontrahenta
    /// </summary>
    public class OpakowanieKontrahentaRow
    {
        public string Kontrahent { get; set; }
        public string Handlowiec { get; set; }
        public decimal E2 { get; set; }
        public decimal H1 { get; set; }
        public decimal ZmianaE2 { get; set; }
        public decimal ZmianaH1 { get; set; }
    }

    #endregion
}
