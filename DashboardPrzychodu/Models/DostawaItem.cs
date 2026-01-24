using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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
        private int _sztukiPlan;
        private decimal _kgPlan;
        private decimal? _sredniaWagaPlan;
        private decimal? _wagaDeklHarmonogram;  // Średnia waga z HarmonogramDostaw
        private decimal? _sztPojPlan;            // Szt/pojemnik z harmonogramu
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
        /// Wyświetlana nazwa hodowcy - skrót lub pełna nazwa
        /// </summary>
        public string HodowcaDisplay => !string.IsNullOrWhiteSpace(HodowcaSkrot) ? HodowcaSkrot : Hodowca;

        #endregion

        #region Properties - Plan (deklarowane przez hodowcę)

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
