using System;
using System.Collections.Generic;

namespace Kalendarz1.HDI.Models
{
    public class HdiDokument
    {
        public int Id { get; set; }
        public int Numer { get; set; }                            // 405
        public int Rok { get; set; }                              // 26 (2026)
        public string NumerPelny => $"{Numer}/{Rok:00}";          // "405/26"

        public int? ZamowienieId { get; set; }
        public int? KlientId { get; set; }
        public string KlientNazwa { get; set; } = "";
        public string KlientAdres { get; set; } = "";

        public string OpisTowaru { get; set; } = "";              // "WĄTROBA Z KURCZAKA KL. A MROŻONA"
        public string RodzajOpakowan { get; set; } = "PALETA DREWNO, POLIBLOK";
        public int? LiczbaOpakowan { get; set; }
        public decimal? WagaNetto { get; set; }
        public decimal? WagaBrutto { get; set; }
        public string Pochodzenie { get; set; } = "KRAJOWE POLSKA";
        public string MiejscePozyskania { get; set; } = "UBOJNIA DROBIU \"PIÓRKOWSCY\" Jerzy Piórkowski w spadku";

        public DateTime? DataWysylki { get; set; }
        public string MiejscePrzeznaczenia { get; set; } = "";    // adres odbiorcy
        public string NumerRejestracyjny { get; set; } = "";       // nr rej. pojazdu (ciągnik)
        public string NumerRejNaczepy { get; set; } = "";          // nr rej. naczepy (manualnie)
        public string UwagiTransport { get; set; } = "Samochody wyposażone w agregaty chłodnicze, temperatura mrożenia -18C stopni";
        public string UwagiTechnologia { get; set; } = "";

        public bool RynekKrajowy { get; set; } = true;
        public bool RynekUE { get; set; } = false;
        public bool RynekInny { get; set; } = false;
        public string InnePanstwo { get; set; } = "";

        public string MiejscowoscWystawienia { get; set; } = "KOZIOŁKI";
        public DateTime DataWystawienia { get; set; } = DateTime.Now;
        public string UtworzonoPrzez { get; set; } = "";
        public string Wystawiajacy { get; set; } = "";            // pełne imię + nazwisko osoby wystawiającej (do druku)
        public string Status { get; set; } = "AKTYWNY";           // AKTYWNY / ANULOWANY

        public List<HdiPartia> Partie { get; set; } = new();
    }

    // Klasyfikacja produktu drobiarskiego — używana do auto-doboru terminu przydatności.
    // ŚWIEŻY: tuszka 7 dni, elementy 6 dni, podroby 4 dni. MROŻONY: +6 miesięcy.
    public enum HdiKategoriaProduktu
    {
        Tuszka,    // KURCZAK A/B/Halal, tuszka cała → 7 dni świeży
        Elementy,  // FILET, NOGA, ĆWIARTKA, SKRZYDŁO, KORPUS, POLĘDWICZKI → 6 dni świeży
        Podroby,   // SERCE, WĄTROBA, ŻOŁĄDKI → 4 dni świeży
        Inne       // opakowania, dodatki itp. — pełna logika ręcznie
    }

    public static class HdiProduktKlasyfikator
    {
        // Detektor kategorii produktu — kompatybilny z DetectKategoria z NoweZamowienieTestWindow.
        public static HdiKategoriaProduktu Detect(string nazwaLubKod)
        {
            string u = (nazwaLubKod ?? "").ToUpperInvariant();
            if (u.Contains("SERCE") || u.Contains("WĄTROBA") || u.Contains("WATROBA")
                || u.Contains("ŻOŁĄDK") || u.Contains("ZOLADK") || u.Contains("PODROB"))
                return HdiKategoriaProduktu.Podroby;
            if (u.Contains("FILET") || u.Contains("ĆWIARTKA") || u.Contains("CWIARTKA")
                || u.Contains("NOGA") || u.Contains("PAŁKA") || u.Contains("PALKA")
                || u.Contains("SKRZYDŁO") || u.Contains("SKRZYDLO")
                || u.Contains("KORPUS") || u.Contains("POLĘDWICZ") || u.Contains("POLEDWICZ"))
                return HdiKategoriaProduktu.Elementy;
            if (u.Contains("KURCZAK") || u.Contains("TUSZKA"))
                return HdiKategoriaProduktu.Tuszka;
            return HdiKategoriaProduktu.Inne;
        }

        // Czy produkt jest MROŻONY (na podstawie nazwy zawierającej "MROZ" / "MROŻ")
        public static bool IsMrozony(string nazwaLubKod)
        {
            string u = (nazwaLubKod ?? "").ToUpperInvariant();
            return u.Contains("MROŻ") || u.Contains("MROZ");
        }

        // Termin przydatności od daty uboju.
        // Świeże: tuszka 7 dni, elementy 6, podroby 4. Mrożone: +6 miesięcy (niezależnie od kategorii).
        public static DateTime PrzydatnoscOd(DateTime dataUboju, string nazwaLubKod)
        {
            if (IsMrozony(nazwaLubKod)) return dataUboju.AddMonths(6);
            return Detect(nazwaLubKod) switch
            {
                HdiKategoriaProduktu.Tuszka   => dataUboju.AddDays(7),
                HdiKategoriaProduktu.Elementy => dataUboju.AddDays(6),
                HdiKategoriaProduktu.Podroby  => dataUboju.AddDays(4),
                _                              => dataUboju.AddDays(6)   // fallback bezpieczny
            };
        }
    }

    public class HdiPartia : System.ComponentModel.INotifyPropertyChanged
    {
        public int Id { get; set; }
        public int HdiDokumentId { get; set; }

        private string _asortyment = "";
        public string Asortyment { get => _asortyment; set { _asortyment = value; OnPropertyChanged(nameof(Asortyment)); } }

        private string _numerPartii = "";
        public string NumerPartii { get => _numerPartii; set { _numerPartii = value; OnPropertyChanged(nameof(NumerPartii)); } }

        private DateTime? _dataUboju;
        public DateTime? DataUboju
        {
            get => _dataUboju;
            set
            {
                _dataUboju = value;
                OnPropertyChanged(nameof(DataUboju));
                OnPropertyChanged(nameof(DniDoPrzydatnosci));
                OnPropertyChanged(nameof(IsDataInvalid));
                OnPropertyChanged(nameof(PrzydatnoscTooltip));
            }
        }

        private DateTime? _dataMrozenia;
        public DateTime? DataMrozenia { get => _dataMrozenia; set { _dataMrozenia = value; OnPropertyChanged(nameof(DataMrozenia)); } }

        private DateTime? _dataPrzydatnosci;
        public DateTime? DataPrzydatnosci
        {
            get => _dataPrzydatnosci;
            set
            {
                _dataPrzydatnosci = value;
                OnPropertyChanged(nameof(DataPrzydatnosci));
                OnPropertyChanged(nameof(DniDoPrzydatnosci));
                OnPropertyChanged(nameof(IsDataInvalid));
                OnPropertyChanged(nameof(PrzydatnoscTooltip));
            }
        }

        // Liczba dni między uboj a przyd — pokazywana w tooltip
        public int? DniDoPrzydatnosci =>
            (_dataUboju.HasValue && _dataPrzydatnosci.HasValue)
                ? (int?)(_dataPrzydatnosci.Value.Date - _dataUboju.Value.Date).Days
                : null;

        // Walidacja: przyd nie może być przed uboj (false = OK, true = BŁĄD → czerwona obwódka)
        public bool IsDataInvalid =>
            _dataUboju.HasValue && _dataPrzydatnosci.HasValue
                && _dataPrzydatnosci.Value.Date < _dataUboju.Value.Date;

        // Tekstowy tooltip dla komórki przydatności
        public string PrzydatnoscTooltip
        {
            get
            {
                if (!_dataUboju.HasValue || !_dataPrzydatnosci.HasValue) return "Brak daty";
                var days = (_dataPrzydatnosci.Value.Date - _dataUboju.Value.Date).Days;
                if (days < 0) return $"⚠ BŁĄD: przyd. {days} dni PRZED ubojem!";
                if (days == 0) return "Tego samego dnia co ubój";
                if (days > 60) return $"Uboju + {days} dni (~{days / 30} mies.) — produkt mrożony";
                return $"Uboju + {days} dni";
            }
        }

        private decimal? _wagaKg;
        public decimal? WagaKg { get => _wagaKg; set { _wagaKg = value; OnPropertyChanged(nameof(WagaKg)); } }

        // Pola RUNTIME-ONLY (nie zapisywane w DB) — używane do wyświetlania miniatur
        // towarów w tabeli partii. Mapowanie po nazwie asortymentu jest trudne, więc
        // przechowujemy ID towaru z HM.TW (gdy auto-fill zna źródło).
        public int? Idtw { get; set; }

        // Image MUSI być INPC — ustawiany asynchronicznie PO zbindowaniu (ładowanie edycji,
        // picker towaru). Bez OnPropertyChanged DataGrid nie odświeży miniatury.
        private System.Windows.Media.ImageSource? _image;
        public System.Windows.Media.ImageSource? Image
        {
            get => _image;
            set { _image = value; OnPropertyChanged(nameof(Image)); }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

    public class HdiListItem
    {
        public int Id { get; set; }
        public string NumerPelny { get; set; } = "";
        public DateTime DataWystawienia { get; set; }
        public string KlientNazwa { get; set; } = "";
        public string OpisTowaru { get; set; } = "";
        public decimal? WagaNetto { get; set; }
        public int? LiczbaOpakowan { get; set; }
        public string UtworzonoPrzez { get; set; } = "";
        public string Status { get; set; } = "";
    }
}
