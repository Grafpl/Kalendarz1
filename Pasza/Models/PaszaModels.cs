using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Kalendarz1.Pasza.Models
{
    /// <summary>Kontrahent Symfonii (paszarnia lub hodowca) — z STContractors.</summary>
    public class KontrahentSymfonia : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Shortcut { get; set; } = "";
        public string Name { get; set; } = "";
        public string NIP { get; set; } = "";

        public string Display => string.IsNullOrWhiteSpace(NIP)
            ? $"{Shortcut}  ·  {Name}"
            : $"{Shortcut}  ·  {Name}  ·  NIP {NIP}";

        public override string ToString() => Display;

        // Używane w pickerach (DodajPaszarnieDialog) do multi-selectu przez checkbox.
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>Towar pasza z HM.TW (aktywne).</summary>
    public class TowarPasza : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Kod { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public string Jm { get; set; } = "t";

        public string Display => $"{Kod}  ·  {Nazwa}  [{Jm}]";
        public override string ToString() => Display;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>Wiersz cennika marż (PaszaCennik).</summary>
    public class CennikItem
    {
        public int Id { get; set; }
        public string HodowcaKhKod { get; set; } = "";
        public string HodowcaNazwa { get; set; } = "";
        public string TowarKod { get; set; } = "";
        public string TowarNazwa { get; set; } = "";
        public decimal MarzaKwota { get; set; }
        public DateTime DataOd { get; set; } = DateTime.Today;
        public DateTime? DataDo { get; set; }
        public bool Aktywny { get; set; } = true;
        public string Uwagi { get; set; } = "";
        public string UtworzonoPrzez { get; set; } = "";
        public DateTime UtworzonoKiedy { get; set; }

        public string OkresText => DataDo.HasValue
            ? $"{DataOd:dd.MM.yyyy} – {DataDo:dd.MM.yyyy}"
            : $"od {DataOd:dd.MM.yyyy}";
    }

    /// <summary>Pozycja w kolejce importu do Symfonii (PaszaImportQueue).</summary>
    public class KolejkaItem
    {
        public int Id { get; set; }
        public string PaszarniaKhKod { get; set; } = "";
        public string PaszarniaNazwa { get; set; } = "";
        public string HodowcaKhKod { get; set; } = "";
        public string HodowcaNazwa { get; set; } = "";
        public string TowarKod { get; set; } = "";
        public string TowarNazwa { get; set; } = "";
        public string TowarJm { get; set; } = "t";
        public decimal Ilosc { get; set; }
        public decimal CenaZakNetto { get; set; }
        public decimal MarzaKwota { get; set; }
        public decimal VatProc { get; set; }
        public string NumerObcy { get; set; } = "";
        public DateTime DataWystawienia { get; set; } = DateTime.Today;
        public int TerminDni { get; set; } = 45;

        // Wyliczane przez SQL (PERSISTED computed columns), tu tylko do odczytu
        public decimal CenaSprzNetto { get; set; }
        public decimal CenaSprzBrutto { get; set; }
        public decimal WartoscZakNetto { get; set; }
        public decimal WartoscSprzNetto { get; set; }
        public decimal WartoscSprzBrutto { get; set; }
        public decimal MarzaLaczna { get; set; }

        public string Status { get; set; } = "NOWY";
        public string NrPZ { get; set; } = "";
        public string NrFVZ { get; set; } = "";
        public string NrWZ { get; set; } = "";
        public string NrFPP { get; set; } = "";
        public string BladKomunikat { get; set; } = "";

        public string UtworzonoPrzez { get; set; } = "";
        public DateTime UtworzonoKiedy { get; set; }
        public DateTime? ImportowanoKiedy { get; set; }

        public string IloscText => $"{Ilosc:N3} {TowarJm}";
        public string DokumentyText
        {
            get
            {
                if (Status != "IMPORTOWANE") return "—";
                return $"PZ {NrPZ}  ·  FVZ {NrFVZ}  ·  WZ {NrWZ}  ·  FPP {NrFPP}";
            }
        }
    }

    /// <summary>Pozycja towaru w Kreatorze (multi-towar w jednej wysyłce do kolejki).</summary>
    public class KreatorTowarPozycja : INotifyPropertyChanged
    {
        public string TowarKod { get; set; } = "";
        public string TowarNazwa { get; set; } = "";
        public string Jm { get; set; } = "t";

        private decimal _ilosc;
        public decimal Ilosc
        {
            get => _ilosc;
            set { if (_ilosc != value) { _ilosc = value; OnChanged(); PropagateComputed(); } }
        }

        private decimal _cenaZak;
        public decimal CenaZakNetto
        {
            get => _cenaZak;
            set { if (_cenaZak != value) { _cenaZak = value; OnChanged(); PropagateComputed(); } }
        }

        private decimal _marza;
        public decimal MarzaKwota
        {
            get => _marza;
            set { if (_marza != value) { _marza = value; OnChanged(); MarzaZCennika = false; PropagateComputed(); OnChanged(nameof(MarzaZCennika)); OnChanged(nameof(MarzaSourceText)); OnChanged(nameof(MarzaSourceBrush)); } }
        }

        public bool MarzaZCennika { get; set; }
        public string MarzaSourceText => MarzaZCennika ? "✓ z cennika" : "ręczna";
        public Brush MarzaSourceBrush => MarzaZCennika
            ? new SolidColorBrush(Color.FromRgb(0x06, 0x5F, 0x46))
            : new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));

        public decimal CenaSprzNetto => CenaZakNetto + MarzaKwota;
        public decimal WartoscZakNetto => Math.Round(Ilosc * CenaZakNetto, 2, MidpointRounding.AwayFromZero);
        public decimal WartoscSprzNetto => Math.Round(Ilosc * CenaSprzNetto, 2, MidpointRounding.AwayFromZero);
        public decimal MarzaLaczna => Math.Round(Ilosc * MarzaKwota, 2, MidpointRounding.AwayFromZero);

        public string IloscText => Ilosc > 0 ? $"{Ilosc:N3} {Jm}" : $"— {Jm}";
        public string WartoscZakText => WartoscZakNetto > 0 ? $"{WartoscZakNetto:N2} zł" : "—";
        public string MarzaLacznaText => MarzaLaczna > 0 ? $"{MarzaLaczna:N2} zł" : "—";
        public string CenaSprzText => CenaSprzNetto > 0 ? $"{CenaSprzNetto:N2} zł/{Jm}" : "—";

        // String wrappers do TextBox bindingu.
        // KLUCZOWA ZMIANA: backing field przechowuje DOKŁADNIE to co wpisał użytkownik (np. "100,",
        // "0,1", "abc" — co tylko). Decimal Ilosc/CenaZakNetto/MarzaKwota jest pochodną przez ParseDec.
        // Dzięki temu WPF nigdy nie „cofa" tekstu w TextBoxie podczas pisania — bo getter zwraca
        // dosłownie to co user napisał, nie sformatowaną wersję 0 lub liczby całkowitej.
        private static readonly System.Globalization.CultureInfo PL = System.Globalization.CultureInfo.GetCultureInfo("pl-PL");

        private string _iloscStr = "";
        public string IloscStr
        {
            get => _iloscStr;
            set
            {
                _iloscStr = value ?? "";
                Ilosc = ParseDecHelper(_iloscStr);
                // OnChanged dla IloscStr NIE wywołujemy — żeby WPF nie nadpisywał TextBoxa podczas typing.
            }
        }

        private string _cenaZakStr = "";
        public string CenaZakStr
        {
            get => _cenaZakStr;
            set
            {
                _cenaZakStr = value ?? "";
                CenaZakNetto = ParseDecHelper(_cenaZakStr);
            }
        }

        private string _marzaStr = "";
        public string MarzaStr
        {
            get => _marzaStr;
            set
            {
                _marzaStr = value ?? "";
                MarzaKwota = ParseDecHelper(_marzaStr);
            }
        }

        private static decimal ParseDecHelper(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0m;
            s = s!.Replace(',', '.').Trim();
            return decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;
        }

        public void UstawMarzeZCennika(decimal marza)
        {
            _marza = marza;
            // Programmatyczne ustawienie — zsynchronizuj string z formatu pl-PL (z przecinkiem)
            _marzaStr = marza > 0 ? marza.ToString("0.##", PL) : "";
            MarzaZCennika = true;
            OnChanged(nameof(MarzaKwota));
            OnChanged(nameof(MarzaStr));   // ↻ odśwież TextBox (programmatic update — bezpieczne)
            OnChanged(nameof(MarzaZCennika));
            OnChanged(nameof(MarzaSourceText));
            OnChanged(nameof(MarzaSourceBrush));
            PropagateComputed();
        }

        private void PropagateComputed()
        {
            OnChanged(nameof(CenaSprzNetto));
            OnChanged(nameof(WartoscZakNetto));
            OnChanged(nameof(WartoscSprzNetto));
            OnChanged(nameof(MarzaLaczna));
            OnChanged(nameof(IloscText));
            OnChanged(nameof(CenaSprzText));
            OnChanged(nameof(WartoscZakText));
            OnChanged(nameof(MarzaLacznaText));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>Wynik sprawdzenia duplikatu FVZ w Symfonii (HM.DK).</summary>
    public class DedupResult
    {
        public bool MaDuplikat => !string.IsNullOrEmpty(NrIstniejacejFv);
        public string NrIstniejacejFv { get; set; } = "";
        public DateTime? DataIstniejacej { get; set; }
    }

    /// <summary>Wpis kuratowanej listy paszarni (PaszaPaszarnie).</summary>
    public class PaszarniaSlownik
    {
        public int Id { get; set; }
        public string KhKod { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public string NIP { get; set; } = "";
        public int Kolejnosc { get; set; }
        public bool Aktywny { get; set; } = true;
        public string Notatki { get; set; } = "";
        public DateTime UtworzonoKiedy { get; set; }

        public string Display => string.IsNullOrWhiteSpace(NIP)
            ? $"{Nazwa}  ·  {KhKod}"
            : $"{Nazwa}  ·  {KhKod}  ·  NIP {NIP}";

        public KontrahentSymfonia ToKontrahent() => new()
        {
            Shortcut = KhKod, Name = Nazwa, NIP = NIP
        };
    }

    /// <summary>Wpis kuratowanej listy towarów pasz (PaszaTowary).</summary>
    public class TowarSlownik
    {
        public int Id { get; set; }
        public string TowarKod { get; set; } = "";
        public string TowarNazwa { get; set; } = "";
        public string Jm { get; set; } = "t";
        public int? KatalogId { get; set; }
        public string KatalogNazwa { get; set; } = "";
        public int Kolejnosc { get; set; }
        public bool Aktywny { get; set; } = true;
        public string Notatki { get; set; } = "";
        public DateTime UtworzonoKiedy { get; set; }

        public string Display => $"{TowarNazwa}  ·  {TowarKod}  [{Jm}]";
        public string KatalogText => KatalogId.HasValue
            ? (string.IsNullOrWhiteSpace(KatalogNazwa) ? $"#{KatalogId}" : $"{KatalogNazwa} (#{KatalogId})")
            : "—";

        public TowarPasza ToTowar() => new()
        {
            Kod = TowarKod, Nazwa = TowarNazwa, Jm = Jm
        };
    }

    /// <summary>Katalog towarów HM.TW.katalog — z liczbą + przykładową nazwą (do identyfikacji który to Pasza).</summary>
    public class KatalogInfo
    {
        public int Id { get; set; }
        public string Nazwa { get; set; } = "";          // znana nazwa katalogu (z HM.KT — zwykle pusta)
        public string SampleNazwa { get; set; } = "";    // przykładowa nazwa towaru z tego katalogu — pomaga zidentyfikować
        public int LiczbaTowarow { get; set; }

        public string Display
        {
            get
            {
                string sample = string.IsNullOrWhiteSpace(SampleNazwa) ? "" : $"  —  np. „{SampleNazwa}\"";
                if (Id < 0) return Nazwa; // „(wszystkie katalogi)"
                return string.IsNullOrWhiteSpace(Nazwa)
                    ? $"#{Id}  ·  {LiczbaTowarow} tow.{sample}"
                    : $"{Nazwa}  ·  #{Id}  ·  {LiczbaTowarow} tow.{sample}";
            }
        }

        public override string ToString() => Display;
    }
}
