using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Kalendarz1.DashboardPrzychodu.Models
{
    /// <summary>
    /// Poziom odchylenia wagi dostawy od deklaracji
    /// </summary>
    public enum PoziomOdchylenia
    {
        Brak,    // Nie można obliczyć (brak danych)
        OK,      // ±2% - norma
        Uwaga,   // ±5% - ostrzeżenie
        Problem  // >5% - przekłamanie hodowcy
    }

    /// <summary>
    /// Status dostawy żywca
    /// </summary>
    public enum StatusDostawy
    {
        Oczekuje = 0,       // Brak wag - czerwony
        BruttoWpisane = 1,  // Tylko FullWeight > 0 - pomarańczowy
        Zwazony = 2         // FullWeight > 0 AND EmptyWeight > 0 - zielony
    }

    /// <summary>
    /// Model pojedynczej dostawy żywca dla Dashboard Przychodu
    /// PLAN z HarmonogramDostaw, RZECZYWISTE z FarmerCalc
    /// </summary>
    public class DostawaItem : INotifyPropertyChanged
    {
        private int _id;
        private int _nrKursu;
        private DateTime _data;
        private string _hodowca;
        private string _hodowcaSkrot;
        private int? _lpDostawy;                 // FK do HarmonogramDostaw.Lp
        private int _sztukiPlan;
        private decimal _kgPlan;
        private decimal? _sredniaWagaPlan;
        private decimal? _wagaDeklHarmonogram;  // Srednia waga z HarmonogramDostaw
        private decimal? _sztPojPlan;            // Szt/pojemnik z harmonogramu

        // Plan laczny z harmonogramu
        private int _planSztukiLacznie;
        private decimal _planKgLacznie;
        private int _autaPlanowane;

        // Postep harmonogramu
        private int _autaZwazone;
        private int _autaOgolem;
        private decimal _sztukiZwazoneSuma;
        private decimal _kgZwazoneSuma;
        private decimal _sztukiPozostalo;
        private decimal _kgPozostalo;
        private decimal _realizacjaProc;
        private decimal _trendProc;
        private decimal _brutto;
        private decimal _tara;
        private decimal _kgRzeczywiste;
        private int _sztukiRzeczywiste;
        private decimal? _sredniaWagaRzeczywista;
        private decimal? _sztPojRzecz;           // Szt/pojemnik rzeczywiste
        private decimal? _odchylenieKg;
        private decimal? _odchylenieProc;
        private decimal? _odchylenieWagi;        // Różnica średnich wag
        private int _statusId;
        private int _padle;
        private int _konfiskaty;
        private DateTime? _przyjazd;
        private DateTime? _godzinaWazenia;
        private string _ktoWazyl;

        #region Properties - Identyfikacja

        public int ID
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public int NrKursu
        {
            get => _nrKursu;
            set { _nrKursu = value; OnPropertyChanged(); }
        }

        public DateTime Data
        {
            get => _data;
            set { _data = value; OnPropertyChanged(); }
        }

        #endregion

        #region Properties - Hodowca

        public string Hodowca
        {
            get => _hodowca;
            set { _hodowca = value; OnPropertyChanged(); }
        }

        public string HodowcaSkrot
        {
            get => _hodowcaSkrot;
            set { _hodowcaSkrot = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Wyswietlana nazwa hodowcy - skrot lub pelna nazwa
        /// </summary>
        public string HodowcaDisplay => !string.IsNullOrWhiteSpace(HodowcaSkrot) ? HodowcaSkrot : Hodowca;

        /// <summary>
        /// FK do HarmonogramDostaw.Lp
        /// </summary>
        public int? LpDostawy
        {
            get => _lpDostawy;
            set { _lpDostawy = value; OnPropertyChanged(); }
        }

        #endregion

        #region Properties - Plan laczny z harmonogramu

        /// <summary>
        /// Plan sztuk LACZNIE z harmonogramu (na wszystkie auta)
        /// </summary>
        public int PlanSztukiLacznie
        {
            get => _planSztukiLacznie;
            set
            {
                _planSztukiLacznie = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SztukiPlanNaAuto));
            }
        }

        /// <summary>
        /// Plan kg LACZNIE z harmonogramu (na wszystkie auta)
        /// </summary>
        public decimal PlanKgLacznie
        {
            get => _planKgLacznie;
            set
            {
                _planKgLacznie = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(KgPlanNaAuto));
            }
        }

        /// <summary>
        /// Ile aut zaplanowano w harmonogramie
        /// </summary>
        public int AutaPlanowane
        {
            get => _autaPlanowane;
            set
            {
                _autaPlanowane = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SztukiPlanNaAuto));
                OnPropertyChanged(nameof(KgPlanNaAuto));
            }
        }

        #endregion

        #region Properties - Postep harmonogramu

        /// <summary>
        /// Ile aut z tego harmonogramu juz zwazono
        /// </summary>
        public int AutaZwazone
        {
            get => _autaZwazone;
            set
            {
                _autaZwazone = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AutaCzekajacych));
                OnPropertyChanged(nameof(PostepDisplay));
                OnPropertyChanged(nameof(PostepProc));
                OnPropertyChanged(nameof(TrendHodowcy));
            }
        }

        /// <summary>
        /// Ile aut ogolem (zwazone + oczekujace) z tego harmonogramu
        /// </summary>
        public int AutaOgolem
        {
            get => _autaOgolem;
            set
            {
                _autaOgolem = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AutaCzekajacych));
                OnPropertyChanged(nameof(CzyOstatnieAuto));
                OnPropertyChanged(nameof(PostepDisplay));
                OnPropertyChanged(nameof(PostepProc));
                OnPropertyChanged(nameof(SztukiPlanNaAuto));
                OnPropertyChanged(nameof(KgPlanNaAuto));
            }
        }

        /// <summary>
        /// Ile aut jeszcze czeka na wazenie
        /// </summary>
        public int AutaCzekajacych => Math.Max(0, AutaOgolem - AutaZwazone);

        /// <summary>
        /// Suma juz zwazonych sztuk z tego harmonogramu
        /// </summary>
        public decimal SztukiZwazoneSuma
        {
            get => _sztukiZwazoneSuma;
            set
            {
                _sztukiZwazoneSuma = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Suma juz zwazonych kg z tego harmonogramu
        /// </summary>
        public decimal KgZwazoneSuma
        {
            get => _kgZwazoneSuma;
            set
            {
                _kgZwazoneSuma = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Ile sztuk pozostalo do zabrania z harmonogramu
        /// </summary>
        public decimal SztukiPozostalo
        {
            get => _sztukiPozostalo;
            set
            {
                _sztukiPozostalo = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PozostaloDisplay));
                OnPropertyChanged(nameof(SztukiPlanNaAuto));
            }
        }

        /// <summary>
        /// Ile kg pozostalo do zabrania z harmonogramu
        /// </summary>
        public decimal KgPozostalo
        {
            get => _kgPozostalo;
            set
            {
                _kgPozostalo = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PozostaloDisplay));
                OnPropertyChanged(nameof(KgPlanNaAuto));
            }
        }

        /// <summary>
        /// Procent realizacji harmonogramu
        /// </summary>
        public decimal RealizacjaProc
        {
            get => _realizacjaProc;
            set
            {
                _realizacjaProc = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Trend hodowcy: srednia na zwazone auto vs plan na auto (100% = zgodnie z planem)
        /// </summary>
        public decimal TrendProc
        {
            get => _trendProc;
            set
            {
                _trendProc = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TrendHodowcy));
            }
        }

        #endregion

        #region Properties - Dynamiczny plan na auto

        /// <summary>
        /// Dynamiczny plan kg NA TO AUTO.
        /// Jesli to ostatnie oczekujace auto: plan = POZOSTALO (reszta z harmonogramu)
        /// W przeciwnym razie: plan = lacznie / ilosc aut
        /// </summary>
        public decimal KgPlanNaAuto
        {
            get
            {
                // Jesli to ostatnie oczekujace auto i jeszcze nie zwazone
                if (CzyOstatnieAuto)
                {
                    return KgPozostalo;
                }
                // W przeciwnym razie: plan proporcjonalny
                if (AutaPlanowane > 0)
                {
                    return Math.Round(PlanKgLacznie / AutaPlanowane, 0);
                }
                // Fallback do starej logiki
                return KgPlan;
            }
        }

        /// <summary>
        /// Dynamiczny plan sztuk NA TO AUTO
        /// </summary>
        public int SztukiPlanNaAuto
        {
            get
            {
                if (CzyOstatnieAuto)
                {
                    return (int)SztukiPozostalo;
                }
                if (AutaPlanowane > 0)
                {
                    return (int)Math.Round((decimal)PlanSztukiLacznie / AutaPlanowane, 0);
                }
                return SztukiPlan;
            }
        }

        /// <summary>
        /// Czy to ostatnie oczekujace auto z harmonogramu (wtedy plan = reszta)
        /// </summary>
        public bool CzyOstatnieAuto => AutaCzekajacych <= 1 && Status == StatusDostawy.Oczekuje;

        /// <summary>
        /// Trend hodowcy tekstowo
        /// </summary>
        public string TrendHodowcy => TrendProc switch
        {
            < 95 => $"\u2193 {TrendProc:N0}% planu",
            > 105 => $"\u2191 {TrendProc:N0}% planu",
            _ => "\u2248 wg planu"
        };

        /// <summary>
        /// Postep harmonogramu display
        /// </summary>
        public string PostepDisplay => $"{AutaZwazone}/{AutaOgolem} aut";

        /// <summary>
        /// Postep harmonogramu w procentach (do ProgressBar)
        /// </summary>
        public double PostepProc => AutaOgolem > 0 ? (double)AutaZwazone / AutaOgolem * 100 : 0;

        /// <summary>
        /// Pozostalo display
        /// </summary>
        public string PozostaloDisplay => $"{KgPozostalo:N0} kg ({SztukiPozostalo:N0} szt)";

        /// <summary>
        /// Kolor paska dla odchylenia (do wiazania w XAML)
        /// </summary>
        public Brush PasekKolor
        {
            get
            {
                var proc = OdchylenieProcCalc ?? OdchylenieProc;
                if (!proc.HasValue || Status != StatusDostawy.Zwazony)
                    return new SolidColorBrush(System.Windows.Media.Color.FromRgb(107, 114, 128)); // Szary

                double absProc = Math.Abs((double)proc.Value);
                if (absProc <= 2)
                    return new SolidColorBrush(System.Windows.Media.Color.FromRgb(78, 204, 163));  // Zielony
                if (absProc <= 5)
                    return new SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 191, 36));  // Zolty
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(233, 69, 96));       // Czerwony
            }
        }

        #endregion

        #region Properties - Plan na pojedyncze auto (stare pola - fallback)

        public int SztukiPlan
        {
            get => _sztukiPlan;
            set { _sztukiPlan = value; OnPropertyChanged(); OnPropertyChanged(nameof(SredniaWagaPlanCalc)); }
        }

        public decimal KgPlan
        {
            get => _kgPlan;
            set { _kgPlan = value; OnPropertyChanged(); OnPropertyChanged(nameof(SredniaWagaPlanCalc)); }
        }

        public decimal? SredniaWagaPlan
        {
            get => _sredniaWagaPlan;
            set { _sredniaWagaPlan = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Obliczona średnia waga planu (kg/szt)
        /// </summary>
        public decimal? SredniaWagaPlanCalc => SztukiPlan > 0 && KgPlan > 0
            ? Math.Round(KgPlan / SztukiPlan, 3)
            : SredniaWagaPlan;

        /// <summary>
        /// Średnia waga deklarowana z HarmonogramDostaw [kg/szt]
        /// </summary>
        public decimal? WagaDeklHarmonogram
        {
            get => _wagaDeklHarmonogram;
            set { _wagaDeklHarmonogram = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Szt/pojemnik z harmonogramu (klasa wagowa)
        /// </summary>
        public decimal? SztPojPlan
        {
            get => _sztPojPlan;
            set { _sztPojPlan = value; OnPropertyChanged(); }
        }

        #endregion

        #region Properties - Rzeczywiste (z wagi w ubojni)

        public decimal Brutto
        {
            get => _brutto;
            set { _brutto = value; OnPropertyChanged(); OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(StatusText)); }
        }

        public decimal Tara
        {
            get => _tara;
            set { _tara = value; OnPropertyChanged(); OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(StatusText)); }
        }

        public decimal KgRzeczywiste
        {
            get => _kgRzeczywiste;
            set
            {
                _kgRzeczywiste = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SredniaWagaRzeczywistaCalc));
                OnPropertyChanged(nameof(OdchylenieKgCalc));
                OnPropertyChanged(nameof(OdchylenieProcCalc));
                OnPropertyChanged(nameof(OdchylenieDisplay));
                OnPropertyChanged(nameof(Poziom));
            }
        }

        public int SztukiRzeczywiste
        {
            get => _sztukiRzeczywiste;
            set
            {
                _sztukiRzeczywiste = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SredniaWagaRzeczywistaCalc));
            }
        }

        public decimal? SredniaWagaRzeczywista
        {
            get => _sredniaWagaRzeczywista;
            set { _sredniaWagaRzeczywista = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Obliczona średnia waga rzeczywista (kg/szt)
        /// </summary>
        public decimal? SredniaWagaRzeczywistaCalc => SztukiRzeczywiste > 0 && KgRzeczywiste > 0
            ? Math.Round(KgRzeczywiste / SztukiRzeczywiste, 3)
            : SredniaWagaRzeczywista;

        /// <summary>
        /// Szt/pojemnik rzeczywiste (klasa wagowa)
        /// </summary>
        public decimal? SztPojRzecz
        {
            get => _sztPojRzecz;
            set { _sztPojRzecz = value; OnPropertyChanged(); }
        }

        #endregion

        #region Properties - Prognoza Tuszek (78% wydajności)

        /// <summary>
        /// Planowana ilość tuszek (kg plan * 78%)
        /// </summary>
        public decimal TuszkiPlanKg => Math.Round(KgPlan * 0.78m, 0);

        /// <summary>
        /// Rzeczywista ilość tuszek (kg rzeczywiste * 78%), tylko jeśli zważono
        /// </summary>
        public decimal? TuszkiRzeczywisteKg => Status == StatusDostawy.Zwazony && KgRzeczywiste > 0
            ? Math.Round(KgRzeczywiste * 0.78m, 0)
            : null;

        #endregion

        #region Properties - Odchylenie

        public decimal? OdchylenieKg
        {
            get => _odchylenieKg;
            set
            {
                _odchylenieKg = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OdchylenieDisplay));
                OnPropertyChanged(nameof(Poziom));
            }
        }

        public decimal? OdchylenieProc
        {
            get => _odchylenieProc;
            set
            {
                _odchylenieProc = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OdchylenieDisplay));
                OnPropertyChanged(nameof(Poziom));
            }
        }

        /// <summary>
        /// Odchylenie średniej wagi [kg/szt] (rzeczywista - deklarowana)
        /// </summary>
        public decimal? OdchylenieWagi
        {
            get => _odchylenieWagi;
            set
            {
                _odchylenieWagi = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OdchylenieWagiDisplay));
                OnPropertyChanged(nameof(WagaTrend));
            }
        }

        /// <summary>
        /// Obliczone odchylenie wagi (rzeczywista - deklarowana)
        /// </summary>
        public decimal? OdchylenieWagiCalc => SredniaWagaRzeczywistaCalc.HasValue && WagaDeklHarmonogram.HasValue
            ? Math.Round(SredniaWagaRzeczywistaCalc.Value - WagaDeklHarmonogram.Value, 3)
            : OdchylenieWagi;

        /// <summary>
        /// Wyświetlane odchylenie wagi
        /// </summary>
        public string OdchylenieWagiDisplay
        {
            get
            {
                var wagi = OdchylenieWagiCalc ?? OdchylenieWagi;
                if (!wagi.HasValue || Status != StatusDostawy.Zwazony)
                    return "-";
                string znak = wagi > 0 ? "+" : "";
                return $"{znak}{wagi:N2} kg";
            }
        }

        /// <summary>
        /// Kierunek zmiany wagi (strzałka)
        /// </summary>
        public string WagaTrend
        {
            get
            {
                var wagi = OdchylenieWagiCalc ?? OdchylenieWagi;
                if (!wagi.HasValue || Status != StatusDostawy.Zwazony) return "";
                return wagi switch
                {
                    > 0.02m => "↑",    // cięższe
                    < -0.02m => "↓",   // lżejsze
                    _ => "≈"           // bez zmian
                };
            }
        }

        /// <summary>
        /// Kierunek zmiany szt/poj
        /// </summary>
        public string SztPojTrend
        {
            get
            {
                if (!SztPojPlan.HasValue || !SztPojRzecz.HasValue) return "";
                var diff = SztPojRzecz.Value - SztPojPlan.Value;
                return diff switch
                {
                    < -0.5m => "↑",  // mniej szt/poj = większe kurczaki
                    > 0.5m => "↓",   // więcej szt/poj = mniejsze kurczaki
                    _ => "≈"
                };
            }
        }

        /// <summary>
        /// Obliczone odchylenie w kg
        /// </summary>
        public decimal? OdchylenieKgCalc => KgRzeczywiste > 0 && KgPlan > 0
            ? KgRzeczywiste - KgPlan
            : OdchylenieKg;

        /// <summary>
        /// Obliczone odchylenie w procentach
        /// </summary>
        public decimal? OdchylenieProcCalc => KgRzeczywiste > 0 && KgPlan > 0
            ? Math.Round((KgRzeczywiste - KgPlan) / KgPlan * 100, 2)
            : OdchylenieProc;

        /// <summary>
        /// Wyświetlany tekst odchylenia
        /// </summary>
        public string OdchylenieDisplay
        {
            get
            {
                var kg = OdchylenieKgCalc ?? OdchylenieKg;
                var proc = OdchylenieProcCalc ?? OdchylenieProc;

                if (!kg.HasValue || !proc.HasValue || Status != StatusDostawy.Zwazony)
                    return "-";

                string znak = kg > 0 ? "+" : "";
                return $"{znak}{kg:N0} kg ({znak}{proc:N1}%)";
            }
        }

        /// <summary>
        /// Poziom odchylenia do kolorowania
        /// </summary>
        public PoziomOdchylenia Poziom
        {
            get
            {
                var proc = OdchylenieProcCalc ?? OdchylenieProc;

                if (!proc.HasValue || Status != StatusDostawy.Zwazony)
                    return PoziomOdchylenia.Brak;

                double absProcent = Math.Abs((double)proc);

                if (absProcent <= 2.0)
                    return PoziomOdchylenia.OK;
                if (absProcent <= 5.0)
                    return PoziomOdchylenia.Uwaga;
                return PoziomOdchylenia.Problem;
            }
        }

        /// <summary>
        /// Czy wiersz ma problem (odchylenie >5%)
        /// </summary>
        public bool JestProblem => Poziom == PoziomOdchylenia.Problem;

        #endregion

        #region Properties - Status

        public int StatusId
        {
            get => _statusId;
            set
            {
                _statusId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(OdchylenieDisplay));
                OnPropertyChanged(nameof(Poziom));
            }
        }

        /// <summary>
        /// Status dostawy (obliczany z wag)
        /// </summary>
        public StatusDostawy Status
        {
            get
            {
                if (Brutto > 0 && Tara > 0)
                    return StatusDostawy.Zwazony;
                if (Brutto > 0)
                    return StatusDostawy.BruttoWpisane;
                return StatusDostawy.Oczekuje;
            }
        }

        /// <summary>
        /// Tekstowy opis statusu z emoji
        /// </summary>
        public string StatusText => Status switch
        {
            StatusDostawy.Zwazony => "Zważony",
            StatusDostawy.BruttoWpisane => "Brutto",
            _ => "Oczekuje"
        };

        /// <summary>
        /// Ikona statusu (emoji)
        /// </summary>
        public string StatusIcon => Status switch
        {
            StatusDostawy.Zwazony => "✅",
            StatusDostawy.BruttoWpisane => "⏳",
            _ => "🔴"
        };

        #endregion

        #region Properties - Konfiskaty

        public int Padle
        {
            get => _padle;
            set { _padle = value; OnPropertyChanged(); }
        }

        public int Konfiskaty
        {
            get => _konfiskaty;
            set { _konfiskaty = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Suma padłych i konfiskat
        /// </summary>
        public int PadleIKonfiskaty => Padle + Konfiskaty;

        #endregion

        #region Properties - Timestampy

        public DateTime? Przyjazd
        {
            get => _przyjazd;
            set { _przyjazd = value; OnPropertyChanged(); OnPropertyChanged(nameof(PrzyjazdDisplay)); }
        }

        public DateTime? GodzinaWazenia
        {
            get => _godzinaWazenia;
            set { _godzinaWazenia = value; OnPropertyChanged(); OnPropertyChanged(nameof(GodzinaWazeniaDisplay)); }
        }

        public string KtoWazyl
        {
            get => _ktoWazyl;
            set { _ktoWazyl = value; OnPropertyChanged(); }
        }

        public string PrzyjazdDisplay => Przyjazd?.ToString("HH:mm") ?? "-";
        public string GodzinaWazeniaDisplay => GodzinaWazenia?.ToString("HH:mm") ?? "-";

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
