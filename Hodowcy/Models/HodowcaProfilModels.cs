using System;
using System.Collections.Generic;
using System.Linq;

namespace Kalendarz1.Hodowcy.Models
{
    /// <summary>Pełen profil hodowcy z LibraNet.dbo.Dostawcy.</summary>
    public class HodowcaProfil
    {
        public string ID { get; set; } = "";          // varchar(10), PK
        public int GID { get; set; }
        public int? IdSymf { get; set; }
        public string Name { get; set; } = "";
        public string ShortName { get; set; } = "";
        public string Nip { get; set; } = "";
        public string Regon { get; set; } = "";
        public string Pesel { get; set; } = "";
        public string IDCard { get; set; } = "";
        public string IDCardAuth { get; set; } = "";
        public DateTime? IDCardDate { get; set; }

        public string Address { get; set; } = "";
        public string PostalCode { get; set; } = "";
        public string City { get; set; } = "";
        public string ProvinceID { get; set; } = "";
        public int? Distance { get; set; }
        public string Trasa { get; set; } = "";

        public string Phone1 { get; set; } = "";
        public string Phone2 { get; set; } = "";
        public string Phone3 { get; set; } = "";
        public string Email { get; set; } = "";

        public string AnimNo { get; set; } = "";       // ARiMR — numer gospodarstwa
        public string IRZPlus { get; set; } = "";

        public int? PriceTypeID { get; set; }
        public decimal? Addition { get; set; }
        public decimal? Loss { get; set; }
        public bool IncDeadConf { get; set; }

        public bool Halt { get; set; }                  // 1 = wstrzymany / nieaktywny
        public bool IsDeliverer { get; set; }
        public bool IsCustomer { get; set; }
        public bool IsRolnik { get; set; }
        public bool IsSkupowy { get; set; }

        public string Info1 { get; set; } = "";
        public string Info2 { get; set; } = "";
        public string Info3 { get; set; } = "";
        public string TypOsobowosci { get; set; } = "";
        public string TypOsobowosci2 { get; set; } = "";

        // Computed
        public string AdresPelny => string.Join(", ",
            new[] { Address, $"{PostalCode} {City}".Trim(), ProvinceID }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

        public string KontaktTelefon
        {
            get
            {
                var p = new List<string>();
                if (!string.IsNullOrWhiteSpace(Phone1)) p.Add(Phone1);
                if (!string.IsNullOrWhiteSpace(Phone2)) p.Add(Phone2);
                if (!string.IsNullOrWhiteSpace(Phone3)) p.Add(Phone3);
                return p.Count == 0 ? "—" : string.Join(" • ", p);
            }
        }
    }

    /// <summary>Statystyki agregowane (np. ostatnie 90 dni lub całe życie).</summary>
    public class HodowcaStatystyki
    {
        public int LiczbaPartii { get; set; }
        public decimal SumaSkupKg { get; set; }
        public decimal SumaPrzyjetoKg { get; set; }
        public decimal? SrWydajnosc { get; set; }       // średnia wydajności
        public decimal? SrKlasaB { get; set; }          // średnia klasa B %
        public decimal? SrTempRampa { get; set; }
        public DateTime? OstatniaDostawa { get; set; }
        public DateTime? PierwszaDostawa { get; set; }
        public int? DniOdOstatniej { get; set; }
        public int? SrCyklDni { get; set; }             // średni cykl między dostawami
        public decimal? SumaSztukDek { get; set; }
        public decimal? SumaPadle { get; set; }
        public decimal? SrCenaSkup { get; set; }
        public decimal? SzacowanyObrot { get; set; }    // SUM(NettoSkup * CenaSkup)
        public int LiczbaPartiiZycie { get; set; }      // wszystkie partie kiedykolwiek

        // ─── NOWE: cykl produkcyjny + zdawalność + waga sztuki ────────────────
        public decimal? SrWiekDni { get; set; }         // średni wiek partii przy uboju (dni)
        public int? MinWiekDni { get; set; }
        public int? MaxWiekDni { get; set; }
        public decimal? SrWagaSzt { get; set; }         // średnia waga sztuki (kg żywca)
        public int? SumaStratSzt { get; set; }          // SztDekl − PrzyjetoSzt
        public decimal? StratySztProc { get; set; }     // % straty śmiertelności łącznie
        public int LiczbaPartiiZWstawieniem { get; set; } // ile partii ma poprawne powiązanie HarmonogramLp

        // ─── KARTA SPECYFIKACJI: konfiskaty + ubytek transportowy ─────────────
        public int SumaCH { get; set; }                 // suma Charłaków
        public int SumaNW { get; set; }                 // suma Niewykrwawionych
        public int SumaZM { get; set; }                 // suma ZM
        public int SumaLUMEL { get; set; }              // suma tuszek z LUMEL
        public int SumaKonfiskat { get; set; }          // CH + NW + ZM
        public decimal? KonfiskatyProc { get; set; }    // łączny % konfiskat
        public decimal? SrUbytekTransProc { get; set; } // średni ubytek transportowy %
        public decimal? SumaUbytekTransKg { get; set; } // łączny ubytek transportowy kg
    }

    /// <summary>Pojedyncza partia ubojowa hodowcy (z listapartii + FarmerCalc + In0E + HarmonogramDostaw join).</summary>
    public class HodowcaPartia
    {
        public int? LpDostawy { get; set; }                  // FarmerCalc.CarLp — numer porządkowy dostawy w danym dniu
        public string Partia { get; set; } = "";
        public DateTime CreateData { get; set; }
        public string CreateGodzina { get; set; } = "";
        public decimal? NettoSkup { get; set; }              // kg żywiec deklarowany (FarmerCalc.NettoWeight)
        public int? SztDekl { get; set; }                    // FarmerCalc.DeclI1 — sztuki deklarowane
        public decimal? WydajnoscProc { get; set; }
        public decimal? KlasaBProc { get; set; }
        public decimal? TempRampa { get; set; }
        public bool IsClose { get; set; }
        public string StatusV2 { get; set; } = "";
        public decimal? CenaSkup { get; set; }
        public decimal? Padle { get; set; }                  // FarmerCalc.DeclI2
        public string VetNo { get; set; } = "";
        public string VetComment { get; set; } = "";
        public decimal? PrzyjetoKg { get; set; }             // SUM(In0E.ActWeight) per P1
        public int? PrzyjetoSzt { get; set; }                // COUNT(In0E) per P1

        // ─── NOWE: dane z HarmonogramDostaw (wstawienie kurczaków) ─────────────
        public DateTime? DataWstawienia { get; set; }        // HarmonogramDostaw.DataOdbioru
        public int? SztDekHarm { get; set; }                 // HarmonogramDostaw.SztukiDek
        public decimal? WagaDekHarm { get; set; }            // HarmonogramDostaw.WagaDek
        public string AutaHarm { get; set; } = "";           // HarmonogramDostaw.Auta

        // ─── KARTA SPECYFIKACJI SUROWCA (FarmerCalc) ──────────────────────────
        // Klasyfikacja sztuk
        public int? CH { get; set; }                         // DeclI3 — Charłaki (małe)
        public int? NW { get; set; }                         // DeclI4 — Niewykrwawione
        public int? ZM { get; set; }                         // DeclI5 — ZM (Zmiażdżone? — TODO weryfikacja)
        public int? LUMEL { get; set; }                      // LumQnt — licznik tuszek z urządzenia
        public int? SztWyb { get; set; }                     // ProdQnt — sztuki wybite w segregacji
        public decimal? KgWyb { get; set; }                  // ProdWgt — kg wybite

        // Wagi: hodowca + ubojnia
        public decimal? BruttoH { get; set; }                // FullFarmWeight
        public decimal? TaraH { get; set; }                  // EmptyFarmWeight
        public decimal? NettoH { get; set; }                 // NettoFarmWeight (waga z auta hodowcy)
        public decimal? BruttoU { get; set; }                // FullWeight
        public decimal? TaraU { get; set; }                  // EmptyWeight
        // NettoSkup powyżej = NettoWeight = Netto U (waga ubojni)

        public decimal? UbytekProc { get; set; }             // Loss × 100 (Loss zapisany jako ułamek)
        public bool PiK { get; set; }                        // IncDeadConf per partia
        public decimal? Opasienie { get; set; }              // stopień opasieniania

        // Transport (z FarmerCalc + Driver)
        public string Kierowca { get; set; } = "";
        public string Auto { get; set; } = "";
        public string Naczepa { get; set; } = "";
        public DateTime? Przyjazd { get; set; }
        public DateTime? DojazdHodowca { get; set; }
        public DateTime? Zaladunek { get; set; }
        public DateTime? ZaladunekKoniec { get; set; }
        public DateTime? WyjazdHodowca { get; set; }
        public DateTime? KoniecUslugi { get; set; }

        // ─── Computed: konfiskaty + zdatne + ubytek transportowy ──────────────
        /// <summary>Konfiskaty (sztuk) = CH + NW + ZM. Wartości NULL traktujemy jako 0.</summary>
        public int? Konfiskaty
        {
            get
            {
                int? sum = null;
                if (CH.HasValue) sum = (sum ?? 0) + CH.Value;
                if (NW.HasValue) sum = (sum ?? 0) + NW.Value;
                if (ZM.HasValue) sum = (sum ?? 0) + ZM.Value;
                return sum;
            }
        }

        /// <summary>Sztuki zdatne do uboju = SztDekl − Padle − Konfiskaty.</summary>
        public int? Zdatne => SztDekl.HasValue
            ? SztDekl.Value - (int)(Padle ?? 0m) - (Konfiskaty ?? 0)
            : null;

        /// <summary>% konfiskat = Konfiskaty / SztDekl × 100.</summary>
        public decimal? KonfiskatyProc => (Konfiskaty.HasValue && SztDekl.HasValue && SztDekl.Value > 0)
            ? Konfiskaty.Value * 100m / SztDekl.Value
            : (decimal?)null;

        /// <summary>Różnica wag transportowa (kg) = NettoH − NettoU. Dodatnia = ubytek.</summary>
        public decimal? UbytekTransKg => (NettoH.HasValue && NettoSkup.HasValue)
            ? NettoH.Value - NettoSkup.Value
            : null;

        /// <summary>% ubytku transportowego = (NettoH − NettoU) / NettoH × 100.</summary>
        public decimal? UbytekTransProc => (UbytekTransKg.HasValue && NettoH.HasValue && NettoH.Value > 0)
            ? UbytekTransKg.Value * 100m / NettoH.Value
            : (decimal?)null;

        /// <summary>Czas załadunku = ZaladunekKoniec − Zaladunek.</summary>
        public TimeSpan? CzasZaladunek => (Zaladunek.HasValue && ZaladunekKoniec.HasValue)
            ? ZaladunekKoniec.Value - Zaladunek.Value
            : (TimeSpan?)null;

        /// <summary>Całkowity czas usługi = KoniecUslugi − Przyjazd.</summary>
        public TimeSpan? CzasCalkowity => (Przyjazd.HasValue && KoniecUslugi.HasValue)
            ? KoniecUslugi.Value - Przyjazd.Value
            : (TimeSpan?)null;

        // ─── NOWE: pola wyliczane ──────────────────────────────────────────────
        /// <summary>Wiek partii: ile dni od wstawienia (HarmonogramDostaw.DataOdbioru → listapartii.CreateData).</summary>
        public int? WiekDni => DataWstawienia.HasValue
            ? (int?)(CreateData.Date - DataWstawienia.Value.Date).Days
            : null;

        /// <summary>Średnia waga sztuki (kg) = NettoWeight / SztDekl.</summary>
        public decimal? SrWagaSzt => (NettoSkup.HasValue && SztDekl.HasValue && SztDekl.Value > 0)
            ? NettoSkup.Value / SztDekl.Value
            : (decimal?)null;

        /// <summary>Straty śmiertelności sztuk (DeclI1 − PrzyjetoSzt). Wartość dodatnia = padłe w transporcie / niedoważone.</summary>
        public int? StratySzt => (SztDekl.HasValue && PrzyjetoSzt.HasValue)
            ? SztDekl.Value - PrzyjetoSzt.Value
            : null;

        /// <summary>Straty % = StratySzt / SztDekl × 100. Norma branżowa < 2%.</summary>
        public decimal? StratySztProc => (StratySzt.HasValue && SztDekl.HasValue && SztDekl.Value > 0)
            ? StratySzt.Value * 100m / SztDekl.Value
            : (decimal?)null;

        /// <summary>Wartość zł = NettoWeight × Price.</summary>
        public decimal? Wartosc => NettoSkup * CenaSkup;

        public string StatusBadge => IsClose ? "✅ Zamknięta" : "⏳ Otwarta";
    }

    /// <summary>Punkt na wykresie trendu wydajności hodowcy.</summary>
    public class HodowcaTrendPunkt
    {
        public DateTime Data { get; set; }
        public string Partia { get; set; } = "";
        public decimal? WydajnoscProc { get; set; }
        public decimal? KlasaBProc { get; set; }
        public decimal NettoSkup { get; set; }
    }

    /// <summary>Element harmonogramu dostaw (planowane). Pełen kontekst z HarmonogramDostaw.</summary>
    public class HodowcaHarmonogramItem
    {
        public int Lp { get; set; }                          // numer porządkowy planu
        public int? LpW { get; set; }                        // LpW — wewnętrzny numer
        public DateTime DataOdbioru { get; set; }
        public int? SztukiDek { get; set; }
        public decimal? WagaDek { get; set; }
        public int? SztSzuflada { get; set; }                // sztuk w szufladzie
        public string Auta { get; set; } = "";               // numery pojazdów
        public string TypCeny { get; set; } = "";            // typ ceny (rynek/ministerial./umowa...)
        public decimal? Cena { get; set; }                   // cena bazowa
        public int? Bufor { get; set; }                      // bufor dni
        public string TypUmowy { get; set; } = "";
        public string KtoUtw { get; set; } = "";             // operator który utworzył plan
        public DateTime? KiedyUtw { get; set; }
        public bool MaPartie { get; set; }
        public string PartiaNumer { get; set; } = "";        // jeśli zrealizowana — numer partii

        public int? DniDoDostawy => (DataOdbioru.Date - DateTime.Today).Days >= 0
            ? (int?)(DataOdbioru.Date - DateTime.Today).Days
            : null;

        public decimal? SrWagaSztPlan => (WagaDek.HasValue && SztukiDek.HasValue && SztukiDek.Value > 0)
            ? WagaDek.Value / SztukiDek.Value
            : (decimal?)null;

        public string StatusBadge
        {
            get
            {
                if (MaPartie) return "✅ Zrealizowana";
                int dni = (DataOdbioru.Date - DateTime.Today).Days;
                if (dni < 0) return "❌ Spóźniona";
                if (dni == 0) return "🔴 Dzisiaj";
                if (dni <= 3) return "🟡 W ciągu 3 dni";
                return "🟢 Planowana";
            }
        }
    }

    /// <summary>Adres fermy hodowcy (Kind=2 w STAdresy).</summary>
    public class HodowcaFerma
    {
        public int Lp { get; set; }
        public string Adres { get; set; } = "";
        public string PostalCode { get; set; } = "";
        public string City { get; set; } = "";
        public string AnimNo { get; set; } = "";
        public string Uwagi { get; set; } = "";
    }

    /// <summary>Rozkład klas wagowych drobiu (4–12) dla danego hodowcy.</summary>
    public class HodowcaKlasaWagowa
    {
        public int Klasa { get; set; }                  // 4..12 (QntInCont)
        public int LiczbaWazen { get; set; }            // ile palet
        public decimal SumaKg { get; set; }             // suma ActWeight
        public decimal SrWagaPalety { get; set; }       // SumaKg/LiczbaWazen
        public decimal ProcentUdzialu { get; set; }     // % w obrębie 4-12
        public string Grupa => Klasa is >= 4 and <= 7 ? "🍗 Duży" : (Klasa is >= 8 and <= 12 ? "🐥 Mały" : "❓");
        public string KlasaDisplay => $"Klasa {Klasa}";
    }

    /// <summary>Pozycja hodowcy w rankingu zakładu (vs ostatnie 90 dni wszystkich).</summary>
    public class HodowcaRanking
    {
        public int Pozycja { get; set; }
        public int LiczbaHodowcow { get; set; }
        public decimal MojaWydajnosc { get; set; }
        public decimal SredniaZakladu { get; set; }
        public decimal MedianaZakladu { get; set; }
        public decimal Top10Wydajnosc { get; set; }     // średnia top 10%
        public decimal MojaSumaKg { get; set; }
        public decimal RynekUdzial { get; set; }        // % wolumenu zakładu
        public int RankingKg { get; set; }              // pozycja po wolumenie kg
        public string OcenaTextowa { get; set; } = "";  // "Top 5%", "Powyżej średniej", "Poniżej mediany"
        public decimal RoznicaDoSredniej { get; set; }  // pp różnicy
    }

    /// <summary>Punkt agregowany (tygodniowo / miesięcznie / kwartalnie).</summary>
    public class HodowcaOkresAgregowany
    {
        public string Klucz { get; set; } = "";
        public string Etykieta { get; set; } = "";
        public string EtykietaKrotka { get; set; } = "";
        public DateTime DataOd { get; set; }
        public DateTime DataDo { get; set; }
        public int LiczbaPartii { get; set; }
        public decimal SumaKg { get; set; }
        public int SumaSztuk { get; set; }
        public decimal? SrWydajnosc { get; set; }
        public decimal? SrKlasaB { get; set; }
        public decimal? SrCena { get; set; }
        public decimal? SumaWartosc { get; set; }
        public decimal? SumaPadle { get; set; }
        public decimal? SrTempRampa { get; set; }
    }

    /// <summary>Anomalia — partia odbiegająca w jakąś stronę (najlepsza/najgorsza).</summary>
    public class HodowcaAnomalia
    {
        public string Typ { get; set; } = "";
        public string Partia { get; set; } = "";
        public DateTime Data { get; set; }
        public decimal Wartosc { get; set; }
        public string Jednostka { get; set; } = "";
        public string Komentarz { get; set; } = "";
    }

    /// <summary>Kontener: profil + statystyki + statystyki życie + listy. Plus lista błędów per źródło — niektóre źródła mogą zawieść (brak tabel/kolumn) ale karta nadal się ładuje.</summary>
    public class HodowcaKartaDane
    {
        public HodowcaProfil Profil { get; set; } = new();
        public HodowcaStatystyki Stat90Dni { get; set; } = new();
        public HodowcaStatystyki StatCaleZycie { get; set; } = new();
        public List<HodowcaPartia> Partie { get; set; } = new();
        public List<HodowcaTrendPunkt> Trend { get; set; } = new();
        public List<HodowcaHarmonogramItem> Harmonogram { get; set; } = new();
        public List<HodowcaFerma> Fermy { get; set; } = new();
        public List<HodowcaKlasaWagowa> Klasy { get; set; } = new();
        public HodowcaRanking Ranking { get; set; } = new();
        public List<HodowcaAnomalia> Anomalie { get; set; } = new();
        public List<string> Bledy { get; set; } = new();   // np. "Partie: Invalid column 'X'"
    }
}
