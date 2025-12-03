using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kalendarz1.FakturyPanel.Models
{
    /// <summary>
    /// Bazowa klasa z implementacją INotifyPropertyChanged
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
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// Model zamówienia dla panelu fakturzystek - uproszczony widok z notatkami
    /// </summary>
    public class ZamowienieFaktury : ObservableObject
    {
        private int _id;
        private string _odbiorca;
        private int _odbiorcaId;
        private string _handlowiec;
        private DateTime _dataProdukcji;
        private DateTime _dataOdbioru;
        private string _godzinaOdbioru;
        private string _notatka;
        private string _status;
        private bool _wlasnyOdbior;
        private decimal _sumaPalet;
        private decimal _sumaPojemnikow;
        private decimal _sumaKg;
        private decimal _wartosc;
        private DateTime _dataUtworzenia;
        private string _utworzonePrzez;
        private DateTime? _dataModyfikacji;
        private string _zmodyfikowanePrzez;
        private bool _jestAnulowane;
        private string _numerZamowienia;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Odbiorca
        {
            get => _odbiorca;
            set => SetProperty(ref _odbiorca, value);
        }

        public int OdbiorcaId
        {
            get => _odbiorcaId;
            set => SetProperty(ref _odbiorcaId, value);
        }

        public string Handlowiec
        {
            get => _handlowiec;
            set => SetProperty(ref _handlowiec, value);
        }

        public DateTime DataProdukcji
        {
            get => _dataProdukcji;
            set => SetProperty(ref _dataProdukcji, value);
        }

        public DateTime DataOdbioru
        {
            get => _dataOdbioru;
            set => SetProperty(ref _dataOdbioru, value);
        }

        public string GodzinaOdbioru
        {
            get => _godzinaOdbioru;
            set => SetProperty(ref _godzinaOdbioru, value);
        }

        public string Notatka
        {
            get => _notatka;
            set => SetProperty(ref _notatka, value);
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public bool WlasnyOdbior
        {
            get => _wlasnyOdbior;
            set => SetProperty(ref _wlasnyOdbior, value);
        }

        public decimal SumaPalet
        {
            get => _sumaPalet;
            set => SetProperty(ref _sumaPalet, value);
        }

        public decimal SumaPojemnikow
        {
            get => _sumaPojemnikow;
            set => SetProperty(ref _sumaPojemnikow, value);
        }

        public decimal SumaKg
        {
            get => _sumaKg;
            set => SetProperty(ref _sumaKg, value);
        }

        public decimal Wartosc
        {
            get => _wartosc;
            set => SetProperty(ref _wartosc, value);
        }

        public DateTime DataUtworzenia
        {
            get => _dataUtworzenia;
            set => SetProperty(ref _dataUtworzenia, value);
        }

        public string UtworzonyPrzez
        {
            get => _utworzonePrzez;
            set => SetProperty(ref _utworzonePrzez, value);
        }

        public DateTime? DataModyfikacji
        {
            get => _dataModyfikacji;
            set => SetProperty(ref _dataModyfikacji, value);
        }

        public string ZmodyfikowanePrzez
        {
            get => _zmodyfikowanePrzez;
            set => SetProperty(ref _zmodyfikowanePrzez, value);
        }

        public bool JestAnulowane
        {
            get => _jestAnulowane;
            set => SetProperty(ref _jestAnulowane, value);
        }

        public string NumerZamowienia
        {
            get => _numerZamowienia;
            set => SetProperty(ref _numerZamowienia, value);
        }

        // Właściwości pomocnicze do wyświetlania
        public string DataProdukcjiTekst => DataProdukcji.ToString("yyyy-MM-dd (dddd)");
        public string DataOdbioruTekst => DataOdbioru.ToString("yyyy-MM-dd (dddd)");
        public string TransportTekst => WlasnyOdbior ? "Własny odbiór" : $"Dostawa o {GodzinaOdbioru}";
        public string StatusWyswietlany => JestAnulowane ? "ANULOWANE" : Status ?? "Aktywne";
        public string PodsumowanieTekst => $"{SumaKg:N0} kg | {SumaPojemnikow:N0} poj. | {SumaPalet:N1} pal.";
    }

    /// <summary>
    /// Model historii zmian zamówienia
    /// </summary>
    public class HistoriaZmianZamowienia : ObservableObject
    {
        private int _id;
        private int _zamowienieId;
        private string _typZmiany;
        private string _poleZmienione;
        private string _wartoscPoprzednia;
        private string _wartoscNowa;
        private string _uzytkownik;
        private string _uzytkownikNazwa;
        private DateTime _dataZmiany;
        private string _opisZmiany;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public int ZamowienieId
        {
            get => _zamowienieId;
            set => SetProperty(ref _zamowienieId, value);
        }

        /// <summary>
        /// Typ zmiany: UTWORZENIE, EDYCJA, ANULOWANIE, PRZYWROCENIE, USUNIECIE
        /// </summary>
        public string TypZmiany
        {
            get => _typZmiany;
            set => SetProperty(ref _typZmiany, value);
        }

        /// <summary>
        /// Nazwa zmienionego pola (np. "Notatka", "DataOdbioru", "GodzinaOdbioru")
        /// </summary>
        public string PoleZmienione
        {
            get => _poleZmienione;
            set => SetProperty(ref _poleZmienione, value);
        }

        public string WartoscPoprzednia
        {
            get => _wartoscPoprzednia;
            set => SetProperty(ref _wartoscPoprzednia, value);
        }

        public string WartoscNowa
        {
            get => _wartoscNowa;
            set => SetProperty(ref _wartoscNowa, value);
        }

        /// <summary>
        /// ID użytkownika który dokonał zmiany
        /// </summary>
        public string Uzytkownik
        {
            get => _uzytkownik;
            set => SetProperty(ref _uzytkownik, value);
        }

        /// <summary>
        /// Pełna nazwa użytkownika
        /// </summary>
        public string UzytkownikNazwa
        {
            get => _uzytkownikNazwa;
            set => SetProperty(ref _uzytkownikNazwa, value);
        }

        public DateTime DataZmiany
        {
            get => _dataZmiany;
            set => SetProperty(ref _dataZmiany, value);
        }

        /// <summary>
        /// Pełny opis zmiany w czytelnej formie
        /// </summary>
        public string OpisZmiany
        {
            get => _opisZmiany;
            set => SetProperty(ref _opisZmiany, value);
        }

        // Właściwości pomocnicze
        public string DataZmianyTekst => DataZmiany.ToString("yyyy-MM-dd HH:mm:ss");

        public string TypZmianyIkona => TypZmiany switch
        {
            "UTWORZENIE" => "➕",
            "EDYCJA" => "✏️",
            "ANULOWANIE" => "❌",
            "PRZYWROCENIE" => "✅",
            "USUNIECIE" => "🗑️",
            _ => "📝"
        };

        public string ZmianaTekst
        {
            get
            {
                if (!string.IsNullOrEmpty(OpisZmiany))
                    return OpisZmiany;

                return TypZmiany switch
                {
                    "UTWORZENIE" => "Utworzono zamówienie",
                    "EDYCJA" when !string.IsNullOrEmpty(PoleZmienione) =>
                        $"Zmieniono {PoleZmienione}: '{WartoscPoprzednia}' → '{WartoscNowa}'",
                    "ANULOWANIE" => "Anulowano zamówienie",
                    "PRZYWROCENIE" => "Przywrócono zamówienie",
                    "USUNIECIE" => "Usunięto zamówienie",
                    _ => $"{TypZmiany}: {PoleZmienione}"
                };
            }
        }
    }

    /// <summary>
    /// Szczegóły produktu w zamówieniu
    /// </summary>
    public class PozycjaZamowienia : ObservableObject
    {
        private int _id;
        private int _zamowienieId;
        private string _kodProduktu;
        private string _nazwaProduktu;
        private decimal _iloscKg;
        private decimal _iloscPojemnikow;
        private decimal _iloscPalet;
        private decimal? _cena;
        private bool _e2;
        private bool _folia;
        private bool _hallal;
        private string _katalog;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public int ZamowienieId
        {
            get => _zamowienieId;
            set => SetProperty(ref _zamowienieId, value);
        }

        public string KodProduktu
        {
            get => _kodProduktu;
            set => SetProperty(ref _kodProduktu, value);
        }

        public string NazwaProduktu
        {
            get => _nazwaProduktu;
            set => SetProperty(ref _nazwaProduktu, value);
        }

        public decimal IloscKg
        {
            get => _iloscKg;
            set => SetProperty(ref _iloscKg, value);
        }

        public decimal IloscPojemnikow
        {
            get => _iloscPojemnikow;
            set => SetProperty(ref _iloscPojemnikow, value);
        }

        public decimal IloscPalet
        {
            get => _iloscPalet;
            set => SetProperty(ref _iloscPalet, value);
        }

        public decimal? Cena
        {
            get => _cena;
            set => SetProperty(ref _cena, value);
        }

        public bool E2
        {
            get => _e2;
            set => SetProperty(ref _e2, value);
        }

        public bool Folia
        {
            get => _folia;
            set => SetProperty(ref _folia, value);
        }

        public bool Hallal
        {
            get => _hallal;
            set => SetProperty(ref _hallal, value);
        }

        public string Katalog
        {
            get => _katalog;
            set => SetProperty(ref _katalog, value);
        }

        // Właściwości pomocnicze
        public string TypProduktu => Katalog == "67153" ? "Mrożony" : "Świeży";
        public string CenaTekst => Cena.HasValue && Cena.Value > 0 ? $"{Cena:N2} zł/kg" : "-";
        public decimal Wartosc => Cena.HasValue ? Cena.Value * IloscKg : 0;
        public string WartoscTekst => Wartosc > 0 ? $"{Wartosc:N2} zł" : "-";

        public string OznaczeniaTekst
        {
            get
            {
                var oznaczenia = new List<string>();
                if (E2) oznaczenia.Add("E2");
                if (Folia) oznaczenia.Add("Folia");
                if (Hallal) oznaczenia.Add("Hallal");
                return oznaczenia.Count > 0 ? string.Join(", ", oznaczenia) : "-";
            }
        }
    }

    /// <summary>
    /// Filtr dla listy zamówień
    /// </summary>
    public class FiltrZamowien : ObservableObject
    {
        private DateTime? _dataOd;
        private DateTime? _dataDo;
        private string _handlowiec;
        private string _szukajTekst;
        private bool _pokazAnulowane;
        private string _sortowanie;

        public DateTime? DataOd
        {
            get => _dataOd;
            set => SetProperty(ref _dataOd, value);
        }

        public DateTime? DataDo
        {
            get => _dataDo;
            set => SetProperty(ref _dataDo, value);
        }

        public string Handlowiec
        {
            get => _handlowiec;
            set => SetProperty(ref _handlowiec, value);
        }

        public string SzukajTekst
        {
            get => _szukajTekst;
            set => SetProperty(ref _szukajTekst, value);
        }

        public bool PokazAnulowane
        {
            get => _pokazAnulowane;
            set => SetProperty(ref _pokazAnulowane, value);
        }

        public string Sortowanie
        {
            get => _sortowanie;
            set => SetProperty(ref _sortowanie, value);
        }
    }
}
