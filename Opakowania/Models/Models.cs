using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Kalendarz1.Opakowania.Models
{
    /// <summary>
    /// Reprezentuje typ opakowania w systemie
    /// </summary>
    public class TypOpakowania
    {
        public string Kod { get; set; }
        public string Nazwa { get; set; }
        public string NazwaSystemowa { get; set; }
        public string IkonaPath { get; set; }
        public string IkonaKod { get; set; } // Kod ikony Segoe MDL2 Assets
        public SolidColorBrush Kolor { get; set; }

        public static TypOpakowania[] WszystkieTypy => new[]
        {
            new TypOpakowania { Kod = "E2", Nazwa = "Pojemnik E2", NazwaSystemowa = "Pojemnik Drobiowy E2", IkonaKod = "\uE7AC", Kolor = new SolidColorBrush(Color.FromRgb(59, 130, 246)) },
            new TypOpakowania { Kod = "H1", Nazwa = "Paleta H1", NazwaSystemowa = "Paleta H1", IkonaKod = "\uE7AC", Kolor = new SolidColorBrush(Color.FromRgb(249, 115, 22)) },
            new TypOpakowania { Kod = "EURO", Nazwa = "Paleta EURO", NazwaSystemowa = "Paleta EURO", IkonaKod = "\uE7AC", Kolor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
            new TypOpakowania { Kod = "PCV", Nazwa = "Paleta Plastikowa", NazwaSystemowa = "Paleta plastikowa", IkonaKod = "\uE7AC", Kolor = new SolidColorBrush(Color.FromRgb(139, 92, 246)) },
            new TypOpakowania { Kod = "DREW", Nazwa = "Paleta Drewniana", NazwaSystemowa = "Paleta Drewniana", IkonaKod = "\uE7AC", Kolor = new SolidColorBrush(Color.FromRgb(245, 158, 11)) }
        };
    }

    /// <summary>
    /// Reprezentuje kontrahenta (odbiorcę) w systemie z pełnymi danymi kontaktowymi
    /// </summary>
    public class Kontrahent : ObservableObject
    {
        private int _id;
        private string _shortcut;
        private string _nazwa;
        private string _handlowiec;
        private string _adres;
        private string _miasto;
        private string _kodPocztowy;
        private string _telefon;
        private string _email;
        private string _nip;
        private bool _maPotwierdzenie;
        private DateTime? _ostatniePotwierdzenie;
        private DateTime? _dataOstatniegoDokumentu;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Shortcut
        {
            get => _shortcut;
            set => SetProperty(ref _shortcut, value);
        }

        public string Nazwa
        {
            get => _nazwa;
            set => SetProperty(ref _nazwa, value);
        }

        public string Handlowiec
        {
            get => _handlowiec;
            set => SetProperty(ref _handlowiec, value);
        }

        public string Adres
        {
            get => _adres;
            set => SetProperty(ref _adres, value);
        }

        public string Miasto
        {
            get => _miasto;
            set => SetProperty(ref _miasto, value);
        }

        public string KodPocztowy
        {
            get => _kodPocztowy;
            set => SetProperty(ref _kodPocztowy, value);
        }

        public string Telefon
        {
            get => _telefon;
            set => SetProperty(ref _telefon, value);
        }

        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public string NIP
        {
            get => _nip;
            set => SetProperty(ref _nip, value);
        }

        public bool MaPotwierdzenie
        {
            get => _maPotwierdzenie;
            set => SetProperty(ref _maPotwierdzenie, value);
        }

        public DateTime? OstatniePotwierdzenie
        {
            get => _ostatniePotwierdzenie;
            set => SetProperty(ref _ostatniePotwierdzenie, value);
        }

        public DateTime? DataOstatniegoDokumentu
        {
            get => _dataOstatniegoDokumentu;
            set => SetProperty(ref _dataOstatniegoDokumentu, value);
        }

        public string NazwaWyswietlana => $"{Shortcut} - {Nazwa}";
        public string PelnyAdres => string.IsNullOrEmpty(Adres) ? "-" : $"{Adres}, {KodPocztowy} {Miasto}";
        public string TelefonWyswietlany => string.IsNullOrEmpty(Telefon) ? "-" : Telefon;
        public string EmailWyswietlany => string.IsNullOrEmpty(Email) ? "-" : Email;
        public string NIPWyswietlany => string.IsNullOrEmpty(NIP) ? "-" : NIP;
    }

    /// <summary>
    /// Reprezentuje saldo opakowania dla kontrahenta
    /// </summary>
    public class SaldoOpakowania : ObservableObject
    {
        private string _kontrahent;
        private int _kontrahentId;
        private string _handlowiec;
        private int _saldoE2;
        private int _saldoH1;
        private int _saldoEURO;
        private int _saldoPCV;
        private int _saldoDREW;
        private DateTime? _ostatniePotwierdzenie;
        private bool _jestPotwierdzone;
        private DateTime? _dataOstatniegoDokumentu;
        private string _towarZDokumentu;
        
        // Dane kontaktowe kontrahenta
        private string _telefon;
        private string _email;
        private string _adres;
        private string _nip;

        // Progi dla koloryzacji (ustawiane globalnie)
        public static int ProgOstrzezenia { get; set; } = 50;
        public static int ProgKrytyczny { get; set; } = 100;

        public string Kontrahent
        {
            get => _kontrahent;
            set => SetProperty(ref _kontrahent, value);
        }

        public int KontrahentId
        {
            get => _kontrahentId;
            set => SetProperty(ref _kontrahentId, value);
        }

        public string Handlowiec
        {
            get => _handlowiec;
            set => SetProperty(ref _handlowiec, value);
        }

        public int SaldoE2
        {
            get => _saldoE2;
            set { SetProperty(ref _saldoE2, value); OnPropertyChanged(nameof(SaldoE2Tekst)); OnPropertyChanged(nameof(SaldoCalkowite)); }
        }

        public int SaldoH1
        {
            get => _saldoH1;
            set { SetProperty(ref _saldoH1, value); OnPropertyChanged(nameof(SaldoH1Tekst)); OnPropertyChanged(nameof(SaldoCalkowite)); }
        }

        public int SaldoEURO
        {
            get => _saldoEURO;
            set { SetProperty(ref _saldoEURO, value); OnPropertyChanged(nameof(SaldoEUROTekst)); OnPropertyChanged(nameof(SaldoCalkowite)); }
        }

        public int SaldoPCV
        {
            get => _saldoPCV;
            set { SetProperty(ref _saldoPCV, value); OnPropertyChanged(nameof(SaldoPCVTekst)); OnPropertyChanged(nameof(SaldoCalkowite)); }
        }

        public int SaldoDREW
        {
            get => _saldoDREW;
            set { SetProperty(ref _saldoDREW, value); OnPropertyChanged(nameof(SaldoDREWTekst)); OnPropertyChanged(nameof(SaldoCalkowite)); }
        }

        public DateTime? OstatniePotwierdzenie
        {
            get => _ostatniePotwierdzenie;
            set => SetProperty(ref _ostatniePotwierdzenie, value);
        }

        public bool JestPotwierdzone
        {
            get => _jestPotwierdzone;
            set => SetProperty(ref _jestPotwierdzone, value);
        }

        public DateTime? DataOstatniegoDokumentu
        {
            get => _dataOstatniegoDokumentu;
            set => SetProperty(ref _dataOstatniegoDokumentu, value);
        }

        public string TowarZDokumentu
        {
            get => _towarZDokumentu;
            set => SetProperty(ref _towarZDokumentu, value);
        }

        public string Telefon
        {
            get => _telefon;
            set => SetProperty(ref _telefon, value);
        }

        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public string Adres
        {
            get => _adres;
            set => SetProperty(ref _adres, value);
        }

        public string NIP
        {
            get => _nip;
            set => SetProperty(ref _nip, value);
        }

        // Suma wszystkich sald (do sortowania)
        public int SaldoCalkowite => Math.Abs(SaldoE2) + Math.Abs(SaldoH1) + Math.Abs(SaldoEURO) + Math.Abs(SaldoPCV) + Math.Abs(SaldoDREW);
        
        // Maksymalne saldo dodatnie (kontrahent winny)
        public int MaxSaldoDodatnie => Math.Max(0, Math.Max(SaldoE2, Math.Max(SaldoH1, Math.Max(SaldoEURO, Math.Max(SaldoPCV, SaldoDREW)))));

        // Właściwości tekstowe - format: "150 (wydane)" lub "-50 (zwrot)"
        public string SaldoE2Tekst => FormatujSaldo(_saldoE2);
        public string SaldoH1Tekst => FormatujSaldo(_saldoH1);
        public string SaldoEUROTekst => FormatujSaldo(_saldoEURO);
        public string SaldoPCVTekst => FormatujSaldo(_saldoPCV);
        public string SaldoDREWTekst => FormatujSaldo(_saldoDREW);

        public string OstatniePotwierdzenieText => OstatniePotwierdzenie?.ToString("dd.MM.yyyy") ?? "-";

        private string FormatujSaldo(int saldo)
        {
            if (saldo == 0) return "0";
            if (saldo > 0) return $"{saldo} (wydane)";
            return $"{Math.Abs(saldo)} (zwrot)";
        }

        // Kolor tła wiersza na podstawie progu
        public SolidColorBrush KolorTlaWiersza
        {
            get
            {
                if (MaxSaldoDodatnie >= ProgKrytyczny)
                    return new SolidColorBrush(Color.FromArgb(40, 244, 67, 54)); // Czerwone tło
                if (MaxSaldoDodatnie >= ProgOstrzezenia)
                    return new SolidColorBrush(Color.FromArgb(40, 255, 152, 0)); // Żółte/pomarańczowe tło
                return new SolidColorBrush(Colors.Transparent);
            }
        }

        public SolidColorBrush GetKolorSalda(int saldo)
        {
            if (saldo > 0) return new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Czerwony - kontrahent winny
            if (saldo < 0) return new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Zielony - my winni
            return new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Szary - zero
        }
    }

    /// <summary>
    /// Reprezentuje wiersz dokumentu w szczegółowym saldzie
    /// </summary>
    public class DokumentOpakowania : ObservableObject
    {
        private int _dokumentId;
        private string _nrDok;
        private string _typDokumentu; // MW lub MP
        private DateTime? _data;
        private string _dzienTyg;
        private string _dokumenty;
        private int _e2;
        private int _h1;
        private int _euro;
        private int _pcv;
        private int _drew;
        private bool _jestSaldem;
        private string _magazyn;
        private string _uwagi;

        public int DokumentId { get => _dokumentId; set => SetProperty(ref _dokumentId, value); }
        public string NrDok { get => _nrDok; set => SetProperty(ref _nrDok, value); }
        public string TypDokumentu { get => _typDokumentu; set => SetProperty(ref _typDokumentu, value); }
        public DateTime? Data { get => _data; set => SetProperty(ref _data, value); }
        public string DzienTyg { get => _dzienTyg; set => SetProperty(ref _dzienTyg, value); }
        public string Dokumenty { get => _dokumenty; set => SetProperty(ref _dokumenty, value); }
        public int E2 { get => _e2; set => SetProperty(ref _e2, value); }
        public int H1 { get => _h1; set => SetProperty(ref _h1, value); }
        public int EURO { get => _euro; set => SetProperty(ref _euro, value); }
        public int PCV { get => _pcv; set => SetProperty(ref _pcv, value); }
        public int DREW { get => _drew; set => SetProperty(ref _drew, value); }
        public bool JestSaldem { get => _jestSaldem; set => SetProperty(ref _jestSaldem, value); }
        public string Magazyn { get => _magazyn; set => SetProperty(ref _magazyn, value); }
        public string Uwagi { get => _uwagi; set => SetProperty(ref _uwagi, value); }

        public string DataText => Data?.ToString("dd.MM.yyyy") ?? "-";
        public string TypDokumentuText => TypDokumentu == "MW1" ? "Wydanie" : (TypDokumentu == "MP" ? "Przyjęcie" : TypDokumentu);

        // Numer dokumentu dla bindingu (alias dla NrDok)
        public string NumerDokumentu => NrDok;

        // Formatowanie z etykietą Wydanie/Zwrot - format: "150 (wydane)"
        public string E2Tekst => FormatujZEtykieta(E2);
        public string H1Tekst => FormatujZEtykieta(H1);
        public string EUROTekst => FormatujZEtykieta(EURO);
        public string PCVTekst => FormatujZEtykieta(PCV);
        public string DREWTekst => FormatujZEtykieta(DREW);

        // Etykiety dla kolumn - pokazują "wydano" lub "przyjęto"
        public string E2Etykieta => GetEtykieta(E2);
        public string H1Etykieta => GetEtykieta(H1);
        public string EUROEtykieta => GetEtykieta(EURO);
        public string PCVEtykieta => GetEtykieta(PCV);
        public string DREWEtykieta => GetEtykieta(DREW);

        private string GetEtykieta(int wartosc)
        {
            if (wartosc == 0) return "";
            if (JestSaldem)
            {
                return wartosc > 0 ? "winni" : "zwrot";
            }
            return wartosc > 0 ? "wyd." : "przyj.";
        }

        private string FormatujZEtykieta(int wartosc)
        {
            if (wartosc == 0) return "-";
            if (JestSaldem)
            {
                if (wartosc > 0) return $"{wartosc} (wydane)";
                return $"{Math.Abs(wartosc)} (zwrot)";
            }
            // Dla dokumentów
            if (wartosc > 0) return $"{wartosc} (wydane)";
            return $"{Math.Abs(wartosc)} (zwrot)";
        }

        // Kolor tła dla wiersza salda
        public SolidColorBrush KolorTla => JestSaldem 
            ? new SolidColorBrush(Color.FromArgb(60, 255, 193, 7)) // Żółte podświetlenie dla salda
            : new SolidColorBrush(Colors.Transparent);
    }

    /// <summary>
    /// Reprezentuje potwierdzenie salda
    /// </summary>
    public class PotwierdzenieSalda : ObservableObject
    {
        private int _id;
        private int _kontrahentId;
        private string _kontrahentNazwa;
        private string _kontrahentShortcut;
        private string _typOpakowania;
        private string _kodOpakowania;
        private DateTime _dataPotwierdzenia;
        private int _iloscPotwierdzona;
        private int _saldoSystemowe;
        private int _roznica;
        private string _statusPotwierdzenia;
        private string _numerDokumentu;
        private string _sciezkaZalacznika;
        private string _uwagi;
        private string _uzytkownikId;
        private string _uzytkownikNazwa;
        private DateTime _dataWprowadzenia;
        private DateTime? _dataModyfikacji;

        public int Id { get => _id; set => SetProperty(ref _id, value); }
        public int KontrahentId { get => _kontrahentId; set => SetProperty(ref _kontrahentId, value); }
        public string KontrahentNazwa { get => _kontrahentNazwa; set => SetProperty(ref _kontrahentNazwa, value); }
        public string KontrahentShortcut { get => _kontrahentShortcut; set => SetProperty(ref _kontrahentShortcut, value); }
        public string TypOpakowania { get => _typOpakowania; set => SetProperty(ref _typOpakowania, value); }
        public string KodOpakowania { get => _kodOpakowania; set => SetProperty(ref _kodOpakowania, value); }
        public DateTime DataPotwierdzenia { get => _dataPotwierdzenia; set => SetProperty(ref _dataPotwierdzenia, value); }
        public int IloscPotwierdzona { get => _iloscPotwierdzona; set => SetProperty(ref _iloscPotwierdzona, value); }
        public int SaldoSystemowe { get => _saldoSystemowe; set => SetProperty(ref _saldoSystemowe, value); }
        public int Roznica { get => _roznica; set => SetProperty(ref _roznica, value); }
        public string StatusPotwierdzenia { get => _statusPotwierdzenia; set => SetProperty(ref _statusPotwierdzenia, value); }
        public string NumerDokumentu { get => _numerDokumentu; set => SetProperty(ref _numerDokumentu, value); }
        public string SciezkaZalacznika { get => _sciezkaZalacznika; set => SetProperty(ref _sciezkaZalacznika, value); }
        public string Uwagi { get => _uwagi; set => SetProperty(ref _uwagi, value); }
        public string UzytkownikId { get => _uzytkownikId; set => SetProperty(ref _uzytkownikId, value); }
        public string UzytkownikNazwa { get => _uzytkownikNazwa; set => SetProperty(ref _uzytkownikNazwa, value); }
        public DateTime DataWprowadzenia { get => _dataWprowadzenia; set => SetProperty(ref _dataWprowadzenia, value); }
        public DateTime? DataModyfikacji { get => _dataModyfikacji; set => SetProperty(ref _dataModyfikacji, value); }

        public string DataPotwierdzeniaText => DataPotwierdzenia.ToString("dd.MM.yyyy");
        public string DataWprowadzeniaText => DataWprowadzenia.ToString("dd.MM.yyyy HH:mm");

        public SolidColorBrush StatusKolor => StatusPotwierdzenia switch
        {
            "Potwierdzone" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            "Rozbieżność" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
            "Oczekujące" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
            "Anulowane" => new SolidColorBrush(Color.FromRgb(158, 158, 158)),
            _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
        };
    }

    /// <summary>
    /// Punkt danych dla wykresu historii salda
    /// </summary>
    public class HistoriaSaldaPunkt
    {
        public DateTime Data { get; set; }
        public int Saldo { get; set; }
        public int SaldoE2 { get; set; }
        public int SaldoH1 { get; set; }
        public int SaldoEURO { get; set; }
        public int SaldoPCV { get; set; }
        public int SaldoDREW { get; set; }
        public string DataText => Data.ToString("dd.MM");
        public string DataPelna => Data.ToString("dd.MM.yyyy");
    }

    /// <summary>
    /// Saldo tygodniowe dla wykresu E2/H1 (końcówka tygodnia = niedziela)
    /// </summary>
    public class SaldoTygodniowe
    {
        public DateTime DataNiedziela { get; set; }
        public int SaldoE2 { get; set; }
        public int SaldoH1 { get; set; }
        public int NumerTygodnia { get; set; }

        public string EtykietaTygodnia => $"Tydz. {NumerTygodnia}\n{DataNiedziela:dd.MM}";
        public string DataText => DataNiedziela.ToString("dd.MM");
        public string DataPelna => $"Niedziela {DataNiedziela:dd.MM.yyyy}";
    }

    /// <summary>
    /// Zestawienie salda dla konkretnego typu opakowania (dla widoku zestawienia)
    /// </summary>
    public class ZestawienieSalda : ObservableObject
    {
        private string _kontrahent;
        private int _kontrahentId;
        private string _handlowiec;
        private int _iloscPierwszyZakres;
        private int _iloscDrugiZakres;
        private int _roznica;
        private DateTime? _dataOstatniegoDokumentu;
        private string _towarZDokumentu;
        private bool _jestPotwierdzone;
        private DateTime? _dataPotwierdzenia;

        public string Kontrahent { get => _kontrahent; set => SetProperty(ref _kontrahent, value); }
        public int KontrahentId { get => _kontrahentId; set => SetProperty(ref _kontrahentId, value); }
        public string Handlowiec { get => _handlowiec; set => SetProperty(ref _handlowiec, value); }
        public int IloscPierwszyZakres { get => _iloscPierwszyZakres; set => SetProperty(ref _iloscPierwszyZakres, value); }
        public int IloscDrugiZakres { get => _iloscDrugiZakres; set => SetProperty(ref _iloscDrugiZakres, value); }
        public int Roznica { get => _roznica; set => SetProperty(ref _roznica, value); }
        public DateTime? DataOstatniegoDokumentu { get => _dataOstatniegoDokumentu; set => SetProperty(ref _dataOstatniegoDokumentu, value); }
        public string TowarZDokumentu { get => _towarZDokumentu; set => SetProperty(ref _towarZDokumentu, value); }
        public bool JestPotwierdzone { get => _jestPotwierdzone; set => SetProperty(ref _jestPotwierdzone, value); }
        public DateTime? DataPotwierdzenia { get => _dataPotwierdzenia; set => SetProperty(ref _dataPotwierdzenia, value); }

        public string IloscPierwszyZakresTekst => FormatujSaldo(IloscPierwszyZakres);
        public string IloscDrugiZakresTekst => FormatujSaldo(IloscDrugiZakres);
        public string RoznicaTekst => FormatujSaldo(Roznica);
        public string DataOstatniegoDokumentuTekst => DataOstatniegoDokumentu?.ToString("dd.MM.yyyy") ?? "-";
        public string DataPotwierdzeniaTekst => DataPotwierdzenia?.ToString("dd.MM.yyyy") ?? "-";

        private string FormatujSaldo(int saldo)
        {
            if (saldo == 0) return "0";
            if (saldo > 0) return $"{saldo} (wydane)";
            return $"{Math.Abs(saldo)} (zwrot)";
        }

        public SolidColorBrush KolorSalda => IloscDrugiZakres switch
        {
            > 0 => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
            < 0 => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
        };
    }

    /// <summary>
    /// Ustawienia użytkownika - zapisywane lokalnie
    /// </summary>
    public class UstawieniaUzytkownika : ObservableObject
    {
        private int _progOstrzezenia = 50;
        private int _progKrytyczny = 100;
        private string _domyslnyHandlowiec;
        private bool _pokazujTylkoSwoich = true;
        private string _domyslnySortowanie = "SaldoCalkowite";
        private bool _sortowanieRosnaco = false;
        private bool _grupujPoHandlowcu = false;
        private int _domyslnyOkresDni = 7;
        private string _sciezkaPDF1 = @"\\192.168.0.170\Public\Salda Opakowan";
        private string _sciezkaPDF2 = @"\\192.168.0.171\Public\Salda Opakowan";

        public int ProgOstrzezenia
        {
            get => _progOstrzezenia;
            set => SetProperty(ref _progOstrzezenia, value);
        }

        public int ProgKrytyczny
        {
            get => _progKrytyczny;
            set => SetProperty(ref _progKrytyczny, value);
        }

        public string DomyslnyHandlowiec
        {
            get => _domyslnyHandlowiec;
            set => SetProperty(ref _domyslnyHandlowiec, value);
        }

        public bool PokazujTylkoSwoich
        {
            get => _pokazujTylkoSwoich;
            set => SetProperty(ref _pokazujTylkoSwoich, value);
        }

        public string DomyslnySortowanie
        {
            get => _domyslnySortowanie;
            set => SetProperty(ref _domyslnySortowanie, value);
        }

        public bool SortowanieRosnaco
        {
            get => _sortowanieRosnaco;
            set => SetProperty(ref _sortowanieRosnaco, value);
        }

        public bool GrupujPoHandlowcu
        {
            get => _grupujPoHandlowcu;
            set => SetProperty(ref _grupujPoHandlowcu, value);
        }

        public int DomyslnyOkresDni
        {
            get => _domyslnyOkresDni;
            set => SetProperty(ref _domyslnyOkresDni, value);
        }

        public string SciezkaPDF1
        {
            get => _sciezkaPDF1;
            set => SetProperty(ref _sciezkaPDF1, value);
        }

        public string SciezkaPDF2
        {
            get => _sciezkaPDF2;
            set => SetProperty(ref _sciezkaPDF2, value);
        }
    }

    /// <summary>
    /// Alert dotyczący przekroczenia progu salda
    /// </summary>
    public class AlertSalda : ObservableObject
    {
        private int _id;
        private int _kontrahentId;
        private string _kontrahentNazwa;
        private string _typOpakowania;
        private int _saldo;
        private int _prog;
        private string _poziomAlertu; // Ostrzeżenie, Krytyczny
        private DateTime _dataWygenerowania;
        private bool _przeczytany;
        private string _handlowiec;

        public int Id { get => _id; set => SetProperty(ref _id, value); }
        public int KontrahentId { get => _kontrahentId; set => SetProperty(ref _kontrahentId, value); }
        public string KontrahentNazwa { get => _kontrahentNazwa; set => SetProperty(ref _kontrahentNazwa, value); }
        public string TypOpakowania { get => _typOpakowania; set => SetProperty(ref _typOpakowania, value); }
        public int Saldo { get => _saldo; set => SetProperty(ref _saldo, value); }
        public int Prog { get => _prog; set => SetProperty(ref _prog, value); }
        public string PoziomAlertu { get => _poziomAlertu; set => SetProperty(ref _poziomAlertu, value); }
        public DateTime DataWygenerowania { get => _dataWygenerowania; set => SetProperty(ref _dataWygenerowania, value); }
        public bool Przeczytany { get => _przeczytany; set => SetProperty(ref _przeczytany, value); }
        public string Handlowiec { get => _handlowiec; set => SetProperty(ref _handlowiec, value); }

        public string Komunikat => $"{KontrahentNazwa}: {TypOpakowania} = {Saldo} (próg: {Prog})";
        public SolidColorBrush KolorAlertu => PoziomAlertu == "Krytyczny"
            ? new SolidColorBrush(Color.FromRgb(244, 67, 54))
            : new SolidColorBrush(Color.FromRgb(255, 152, 0));
    }

    /// <summary>
    /// Historia zmian w systemie
    /// </summary>
    public class HistoriaZmian : ObservableObject
    {
        private int _id;
        private DateTime _dataZmiany;
        private string _uzytkownikId;
        private string _uzytkownikNazwa;
        private string _typZmiany; // Potwierdzenie, Korekta, Usunięcie
        private string _tabela;
        private int _rekordId;
        private string _starWartosc;
        private string _nowaWartosc;
        private string _opis;

        public int Id { get => _id; set => SetProperty(ref _id, value); }
        public DateTime DataZmiany { get => _dataZmiany; set => SetProperty(ref _dataZmiany, value); }
        public string UzytkownikId { get => _uzytkownikId; set => SetProperty(ref _uzytkownikId, value); }
        public string UzytkownikNazwa { get => _uzytkownikNazwa; set => SetProperty(ref _uzytkownikNazwa, value); }
        public string TypZmiany { get => _typZmiany; set => SetProperty(ref _typZmiany, value); }
        public string Tabela { get => _tabela; set => SetProperty(ref _tabela, value); }
        public int RekordId { get => _rekordId; set => SetProperty(ref _rekordId, value); }
        public string StaraWartosc { get => _starWartosc; set => SetProperty(ref _starWartosc, value); }
        public string NowaWartosc { get => _nowaWartosc; set => SetProperty(ref _nowaWartosc, value); }
        public string Opis { get => _opis; set => SetProperty(ref _opis, value); }

        public string DataZmianyText => DataZmiany.ToString("dd.MM.yyyy HH:mm:ss");
    }

    /// <summary>
    /// Przypomnienie o potwierdzeniu salda
    /// </summary>
    public class PrzypomnienieSalda : ObservableObject
    {
        private int _id;
        private int _kontrahentId;
        private string _kontrahentNazwa;
        private DateTime? _ostatniePotwierdzenie;
        private int _dniOdPotwierdzenia;
        private string _handlowiec;
        private bool _wyslano;
        private DateTime? _dataWyslania;

        public int Id { get => _id; set => SetProperty(ref _id, value); }
        public int KontrahentId { get => _kontrahentId; set => SetProperty(ref _kontrahentId, value); }
        public string KontrahentNazwa { get => _kontrahentNazwa; set => SetProperty(ref _kontrahentNazwa, value); }
        public DateTime? OstatniePotwierdzenie { get => _ostatniePotwierdzenie; set => SetProperty(ref _ostatniePotwierdzenie, value); }
        public int DniOdPotwierdzenia { get => _dniOdPotwierdzenia; set => SetProperty(ref _dniOdPotwierdzenia, value); }
        public string Handlowiec { get => _handlowiec; set => SetProperty(ref _handlowiec, value); }
        public bool Wyslano { get => _wyslano; set => SetProperty(ref _wyslano, value); }
        public DateTime? DataWyslania { get => _dataWyslania; set => SetProperty(ref _dataWyslania, value); }

        public string OstatniePotwierdzenieText => OstatniePotwierdzenie?.ToString("dd.MM.yyyy") ?? "Nigdy";
        public string Status => DniOdPotwierdzenia > 90 ? "Krytyczny" : (DniOdPotwierdzenia > 30 ? "Ostrzeżenie" : "OK");
    }

    /// <summary>
    /// Statystyki dashboardu
    /// </summary>
    public class StatystykiDashboard : ObservableObject
    {
        private int _liczbaKontrahentow;
        private int _liczbaKontrahentowZSaldem;
        private int _sumaE2;
        private int _sumaH1;
        private int _sumaEURO;
        private int _sumaPCV;
        private int _sumaDREW;
        private int _liczbaAlertow;
        private int _liczbaPrzypomnien;
        private List<SaldoOpakowania> _top10Dluznikow;

        public int LiczbaKontrahentow { get => _liczbaKontrahentow; set => SetProperty(ref _liczbaKontrahentow, value); }
        public int LiczbaKontrahentowZSaldem { get => _liczbaKontrahentowZSaldem; set => SetProperty(ref _liczbaKontrahentowZSaldem, value); }
        public int SumaE2 { get => _sumaE2; set => SetProperty(ref _sumaE2, value); }
        public int SumaH1 { get => _sumaH1; set => SetProperty(ref _sumaH1, value); }
        public int SumaEURO { get => _sumaEURO; set => SetProperty(ref _sumaEURO, value); }
        public int SumaPCV { get => _sumaPCV; set => SetProperty(ref _sumaPCV, value); }
        public int SumaDREW { get => _sumaDREW; set => SetProperty(ref _sumaDREW, value); }
        public int LiczbaAlertow { get => _liczbaAlertow; set => SetProperty(ref _liczbaAlertow, value); }
        public int LiczbaPrzypomnien { get => _liczbaPrzypomnien; set => SetProperty(ref _liczbaPrzypomnien, value); }
        public List<SaldoOpakowania> Top10Dluznikow { get => _top10Dluznikow; set => SetProperty(ref _top10Dluznikow, value); }

        public int SumaWszystkich => SumaE2 + SumaH1 + SumaEURO + SumaPCV + SumaDREW;
    }

    /// <summary>
    /// Szybki filtr daty
    /// </summary>
    public class FiltrDaty
    {
        public string Nazwa { get; set; }
        public DateTime DataOd { get; set; }
        public DateTime DataDo { get; set; }

        public static List<FiltrDaty> GetFiltryDomyslne()
        {
            var dzisiaj = DateTime.Today;
            var poniedzialek = dzisiaj.AddDays(-(int)dzisiaj.DayOfWeek + (int)DayOfWeek.Monday);
            if (dzisiaj.DayOfWeek == DayOfWeek.Sunday) poniedzialek = poniedzialek.AddDays(-7);
            var poprzedniPoniedzialek = poniedzialek.AddDays(-7);
            var poprzedniaNiedziela = poniedzialek.AddDays(-1);

            return new List<FiltrDaty>
            {
                new FiltrDaty 
                { 
                    Nazwa = "Poprzedni tydzień", 
                    DataOd = poprzedniPoniedzialek, 
                    DataDo = poprzedniaNiedziela 
                },
                new FiltrDaty 
                { 
                    Nazwa = "Ten tydzień", 
                    DataOd = poniedzialek, 
                    DataDo = dzisiaj 
                },
                new FiltrDaty 
                { 
                    Nazwa = "Ten miesiąc", 
                    DataOd = new DateTime(dzisiaj.Year, dzisiaj.Month, 1), 
                    DataDo = dzisiaj 
                },
                new FiltrDaty 
                { 
                    Nazwa = "Poprzedni miesiąc", 
                    DataOd = new DateTime(dzisiaj.Year, dzisiaj.Month, 1).AddMonths(-1), 
                    DataDo = new DateTime(dzisiaj.Year, dzisiaj.Month, 1).AddDays(-1) 
                },
                new FiltrDaty 
                { 
                    Nazwa = "Ten kwartał", 
                    DataOd = new DateTime(dzisiaj.Year, ((dzisiaj.Month - 1) / 3) * 3 + 1, 1), 
                    DataDo = dzisiaj 
                },
                new FiltrDaty 
                { 
                    Nazwa = "Ten rok", 
                    DataOd = new DateTime(dzisiaj.Year, 1, 1), 
                    DataDo = dzisiaj 
                },
                new FiltrDaty 
                { 
                    Nazwa = "Ostatnie 30 dni", 
                    DataOd = dzisiaj.AddDays(-30), 
                    DataDo = dzisiaj 
                },
                new FiltrDaty 
                { 
                    Nazwa = "Ostatnie 90 dni", 
                    DataOd = dzisiaj.AddDays(-90), 
                    DataDo = dzisiaj 
                }
            };
        }
    }

    /// <summary>
    /// Bazowa klasa dla obsługi INotifyPropertyChanged
    /// </summary>
    public class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
