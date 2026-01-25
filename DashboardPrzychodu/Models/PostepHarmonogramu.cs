using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Kalendarz1.DashboardPrzychodu.Models
{
    /// <summary>
    /// Model postepu realizacji pojedynczego harmonogramu (per hodowca).
    /// Pokazuje ile kg/szt zostalo do zabrania z danego harmonogramu.
    /// </summary>
    public class PostepHarmonogramu : INotifyPropertyChanged
    {
        private int _lpDostawy;
        private string _hodowca;
        private int _autaZwazone;
        private int _autaOgolem;
        private int _autaPlanowane;
        private decimal _planSztukiLacznie;
        private decimal _planKgLacznie;
        private decimal _sztukiZwazoneSuma;
        private decimal _kgZwazoneSuma;

        #region Properties - Identyfikacja

        /// <summary>
        /// LP harmonogramu (HarmonogramDostaw.Lp)
        /// </summary>
        public int LpDostawy
        {
            get => _lpDostawy;
            set { _lpDostawy = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Nazwa hodowcy
        /// </summary>
        public string Hodowca
        {
            get => _hodowca;
            set { _hodowca = value; OnPropertyChanged(); }
        }

        #endregion

        #region Properties - Auta

        /// <summary>
        /// Liczba aut juz zwazonych z tego harmonogramu
        /// </summary>
        public int AutaZwazone
        {
            get => _autaZwazone;
            set
            {
                _autaZwazone = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AutaCzekajacych));
                OnPropertyChanged(nameof(PasekPostepuDisplay));
                OnPropertyChanged(nameof(CzyZakonczone));
                OnPropertyChanged(nameof(TileBackground));
            }
        }

        /// <summary>
        /// Liczba aut ogolem (zwazone + oczekujace w FarmerCalc)
        /// </summary>
        public int AutaOgolem
        {
            get => _autaOgolem;
            set
            {
                _autaOgolem = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AutaCzekajacych));
                OnPropertyChanged(nameof(PasekPostepuDisplay));
                OnPropertyChanged(nameof(CzyZakonczone));
                OnPropertyChanged(nameof(TileBackground));
            }
        }

        /// <summary>
        /// Liczba aut planowanych w harmonogramie
        /// </summary>
        public int AutaPlanowane
        {
            get => _autaPlanowane;
            set { _autaPlanowane = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Ile aut jeszcze czeka na wazenie
        /// </summary>
        public int AutaCzekajacych => Math.Max(0, AutaOgolem - AutaZwazone);

        #endregion

        #region Properties - Plan

        /// <summary>
        /// Plan sztuk lacznie z harmonogramu
        /// </summary>
        public decimal PlanSztukiLacznie
        {
            get => _planSztukiLacznie;
            set
            {
                _planSztukiLacznie = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SztukiPozostalo));
                OnPropertyChanged(nameof(RealizacjaProc));
            }
        }

        /// <summary>
        /// Plan kg lacznie z harmonogramu
        /// </summary>
        public decimal PlanKgLacznie
        {
            get => _planKgLacznie;
            set
            {
                _planKgLacznie = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(KgPozostalo));
                OnPropertyChanged(nameof(RealizacjaProc));
                OnPropertyChanged(nameof(TrendProc));
                OnPropertyChanged(nameof(TrendDisplay));
            }
        }

        #endregion

        #region Properties - Zwazone

        /// <summary>
        /// Suma zwazonych sztuk z tego harmonogramu
        /// </summary>
        public decimal SztukiZwazoneSuma
        {
            get => _sztukiZwazoneSuma;
            set
            {
                _sztukiZwazoneSuma = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SztukiPozostalo));
                OnPropertyChanged(nameof(RealizacjaProc));
            }
        }

        /// <summary>
        /// Suma zwazonych kg z tego harmonogramu
        /// </summary>
        public decimal KgZwazoneSuma
        {
            get => _kgZwazoneSuma;
            set
            {
                _kgZwazoneSuma = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(KgPozostalo));
                OnPropertyChanged(nameof(RealizacjaProc));
                OnPropertyChanged(nameof(TrendProc));
                OnPropertyChanged(nameof(TrendDisplay));
            }
        }

        #endregion

        #region Properties - Pozostalo

        /// <summary>
        /// Ile sztuk jeszcze pozostalo do zabrania
        /// </summary>
        public decimal SztukiPozostalo => Math.Max(0, PlanSztukiLacznie - SztukiZwazoneSuma);

        /// <summary>
        /// Ile kg jeszcze pozostalo do zabrania
        /// </summary>
        public decimal KgPozostalo => Math.Max(0, PlanKgLacznie - KgZwazoneSuma);

        #endregion

        #region Properties - Procenty

        /// <summary>
        /// Procent realizacji harmonogramu (kg)
        /// </summary>
        public decimal RealizacjaProc
        {
            get
            {
                if (PlanKgLacznie > 0)
                {
                    return Math.Round(KgZwazoneSuma / PlanKgLacznie * 100, 1);
                }
                return 0;
            }
        }

        /// <summary>
        /// Trend: srednia na zwazone auto vs plan na auto
        /// 100% = zgodnie z planem, <100% = mniej, >100% = wiecej
        /// </summary>
        public decimal TrendProc
        {
            get
            {
                if (AutaZwazone > 0 && AutaPlanowane > 0 && PlanKgLacznie > 0)
                {
                    decimal sredniaZwazona = KgZwazoneSuma / AutaZwazone;
                    decimal planNaAuto = PlanKgLacznie / AutaPlanowane;
                    if (planNaAuto > 0)
                    {
                        return Math.Round(sredniaZwazona / planNaAuto * 100, 1);
                    }
                }
                return 100;
            }
        }

        #endregion

        #region Properties - Display

        /// <summary>
        /// Pasek postepu tekstowy [========~~]
        /// </summary>
        public string PasekPostepuDisplay
        {
            get
            {
                int filled = (int)(RealizacjaProc / 10);
                filled = Math.Min(10, Math.Max(0, filled));
                int empty = 10 - filled;
                return $"[{new string('\u2588', filled)}{new string('\u2591', empty)}]";
            }
        }

        /// <summary>
        /// Status tekstowy
        /// </summary>
        public string StatusDisplay => TrendProc switch
        {
            < 95 => $"-{(100 - TrendProc):N0}%",
            > 105 => $"+{(TrendProc - 100):N0}%",
            _ => "OK"
        };

        /// <summary>
        /// Pozostalo display
        /// </summary>
        public string PozostaloDisplay => $"{KgPozostalo:N0} kg ({SztukiPozostalo:N0} szt)";

        /// <summary>
        /// Postep display (auta)
        /// </summary>
        public string PostepDisplay => $"{AutaZwazone}/{AutaOgolem} aut";

        /// <summary>
        /// Kolor statusu
        /// </summary>
        public Brush StatusKolor => TrendProc switch
        {
            < 95 => new SolidColorBrush(Color.FromRgb(233, 69, 96)),     // Czerwony - mniej niz plan
            > 105 => new SolidColorBrush(Color.FromRgb(78, 204, 163)),   // Zielony - wiecej niz plan
            _ => new SolidColorBrush(Color.FromRgb(156, 163, 175))       // Szary - OK
        };

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
        /// Czy harmonogram jest zakończony (wszystkie auta zważone)
        /// </summary>
        public bool CzyZakonczone => AutaOgolem > 0 && AutaZwazone >= AutaOgolem;

        /// <summary>
        /// Kolor tła kafelka - zielony jeśli zakończone, pomarańczowy jeśli w trakcie
        /// </summary>
        public Brush TileBackground => CzyZakonczone
            ? new SolidColorBrush(Color.FromRgb(34, 197, 94))    // Zielony - zakończone
            : new SolidColorBrush(Color.FromRgb(217, 119, 6));   // Pomarańczowy - w trakcie

        /// <summary>
        /// Skrócona nazwa hodowcy (max 10 znaków)
        /// </summary>
        public string HodowcaSkrot => string.IsNullOrEmpty(Hodowca)
            ? "-"
            : (Hodowca.Length > 10 ? Hodowca.Substring(0, 10) + ".." : Hodowca);

        /// <summary>
        /// Wyświetlany trend dla kafelka (procent realizacji)
        /// </summary>
        public string TrendDisplay => $"{RealizacjaProc:N0}%";

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
