using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Kalendarz1.DashboardPrzychodu.Models
{
    /// <summary>
    /// Model prognozy koncowej dnia z alertem redukcji zamowien.
    /// Prognozuje czy plan sie spelni i o ile trzeba redukowac zamowienia.
    /// </summary>
    public class PrognozaDnia : INotifyPropertyChanged
    {
        private const decimal WspolczynnikTuszek = 0.78m;

        private decimal _kgPlanLacznie;
        private decimal _kgZwazone;
        private int _autaZwazone;
        private int _autaOgolem;
        private decimal _kgPrognozaKoncowa;

        #region Properties - Plan

        /// <summary>
        /// Plan kg lacznie na dzien (z harmonogramow)
        /// </summary>
        public decimal KgPlanLacznie
        {
            get => _kgPlanLacznie;
            set
            {
                _kgPlanLacznie = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TuszkiPlan));
                OnPropertyChanged(nameof(KgRoznica));
                OnPropertyChanged(nameof(TuszkiBrak));
                OnPropertyChanged(nameof(TrendProc));
                OnPropertyChanged(nameof(JestAlert));
                OnPropertyChanged(nameof(PoziomAlertu));
                OnPropertyChanged(nameof(AlertKolor));
                OnPropertyChanged(nameof(RedukcjaDisplay));
            }
        }

        /// <summary>
        /// Plan tuszek [kg] = KgPlan * 78%
        /// </summary>
        public decimal TuszkiPlan => Math.Round(KgPlanLacznie * WspolczynnikTuszek, 0);

        #endregion

        #region Properties - Zwazone

        /// <summary>
        /// Kg juz zwazone
        /// </summary>
        public decimal KgZwazone
        {
            get => _kgZwazone;
            set
            {
                _kgZwazone = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(KgPrognozaKoncowa));
                OnPropertyChanged(nameof(TuszkiPrognoza));
                OnPropertyChanged(nameof(KgRoznica));
                OnPropertyChanged(nameof(TuszkiBrak));
                OnPropertyChanged(nameof(TrendProc));
                OnPropertyChanged(nameof(JestAlert));
                OnPropertyChanged(nameof(PoziomAlertu));
                OnPropertyChanged(nameof(AlertKolor));
                OnPropertyChanged(nameof(RedukcjaDisplay));
                OnPropertyChanged(nameof(PrognozaDisplay));
            }
        }

        /// <summary>
        /// Liczba aut juz zwazonych
        /// </summary>
        public int AutaZwazone
        {
            get => _autaZwazone;
            set
            {
                _autaZwazone = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(KgPrognozaKoncowa));
                OnPropertyChanged(nameof(TuszkiPrognoza));
                OnPropertyChanged(nameof(KgRoznica));
                OnPropertyChanged(nameof(TuszkiBrak));
                OnPropertyChanged(nameof(TrendProc));
                OnPropertyChanged(nameof(JestAlert));
                OnPropertyChanged(nameof(PoziomAlertu));
                OnPropertyChanged(nameof(AlertKolor));
                OnPropertyChanged(nameof(RedukcjaDisplay));
                OnPropertyChanged(nameof(PrognozaDisplay));
            }
        }

        /// <summary>
        /// Liczba aut ogolem (zwazone + oczekujace)
        /// </summary>
        public int AutaOgolem
        {
            get => _autaOgolem;
            set
            {
                _autaOgolem = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(KgPrognozaKoncowa));
                OnPropertyChanged(nameof(TuszkiPrognoza));
                OnPropertyChanged(nameof(KgRoznica));
                OnPropertyChanged(nameof(TuszkiBrak));
                OnPropertyChanged(nameof(TrendProc));
                OnPropertyChanged(nameof(JestAlert));
                OnPropertyChanged(nameof(PoziomAlertu));
                OnPropertyChanged(nameof(AlertKolor));
                OnPropertyChanged(nameof(RedukcjaDisplay));
                OnPropertyChanged(nameof(PrognozaDisplay));
            }
        }

        #endregion

        #region Properties - Prognoza

        /// <summary>
        /// Prognoza koncowa dnia w kg (pesymistyczna).
        /// Prognoza = KgZwazone * (AutaOgolem / AutaZwazone)
        /// </summary>
        public decimal KgPrognozaKoncowa
        {
            get
            {
                if (AutaZwazone > 0)
                {
                    return Math.Round(KgZwazone * ((decimal)AutaOgolem / AutaZwazone), 0);
                }
                return 0;
            }
        }

        /// <summary>
        /// Prognoza tuszek [kg] = Prognoza * 78%
        /// </summary>
        public decimal TuszkiPrognoza => Math.Round(KgPrognozaKoncowa * WspolczynnikTuszek, 0);

        /// <summary>
        /// Roznica miedzy planem a prognoza (moze byc ujemna = brak)
        /// </summary>
        public decimal KgRoznica => KgPlanLacznie - KgPrognozaKoncowa;

        /// <summary>
        /// Brak tuszek do redukcji zamowien [kg]
        /// Dodatnia = brakuje, Ujemna = nadwyzka
        /// </summary>
        public decimal TuszkiBrak => Math.Round(KgRoznica * WspolczynnikTuszek, 0);

        /// <summary>
        /// Trend procentowy: ile % planu zostanie zrealizowane
        /// </summary>
        public decimal TrendProc
        {
            get
            {
                if (AutaZwazone > 0 && KgPlanLacznie > 0)
                {
                    return Math.Round(KgPrognozaKoncowa / KgPlanLacznie * 100, 1);
                }
                return 100;
            }
        }

        #endregion

        #region Properties - Alert

        /// <summary>
        /// Czy jest alert (brakuje >2%)
        /// </summary>
        public bool JestAlert => AutaZwazone > 0 && TrendProc < 98;

        /// <summary>
        /// Poziom alertu
        /// </summary>
        public string PoziomAlertu => TrendProc switch
        {
            < 90 => "KRYTYCZNY",
            < 95 => "WYSOKI",
            < 98 => "UWAGA",
            _ => "OK"
        };

        /// <summary>
        /// Ikona alertu z poziomem
        /// </summary>
        public string AlertIkona => TrendProc switch
        {
            < 90 => "\U0001F6A8 KRYTYCZNY",
            < 95 => "\u26a0\ufe0f WYSOKI",
            < 98 => "\U0001F4C9 UWAGA",
            _ => "\u2705 OK"
        };

        /// <summary>
        /// Kolor alertu
        /// </summary>
        public Brush AlertKolor => TrendProc switch
        {
            < 90 => new SolidColorBrush(Color.FromRgb(220, 38, 38)),    // czerwony krytyczny
            < 95 => new SolidColorBrush(Color.FromRgb(251, 191, 36)),   // zolty
            < 98 => new SolidColorBrush(Color.FromRgb(251, 146, 60)),   // pomaranczowy
            _ => new SolidColorBrush(Color.FromRgb(78, 204, 163))       // zielony
        };

        /// <summary>
        /// Tekst redukcji zamowien
        /// </summary>
        public string RedukcjaDisplay
        {
            get
            {
                if (AutaZwazone == 0)
                    return "Oczekiwanie na dane...";

                if (TuszkiBrak > 0)
                    return $"REDUKUJ ZAMOWIENIA o ~{TuszkiBrak:N0} kg tuszek!";
                if (TuszkiBrak < 0)
                    return $"Nadwyzka ~{Math.Abs(TuszkiBrak):N0} kg tuszek";
                return "Plan realizowany";
            }
        }

        /// <summary>
        /// Prognoza display
        /// </summary>
        public string PrognozaDisplay => AutaZwazone > 0
            ? $"{TrendProc:N0}% planu -> {KgPrognozaKoncowa:N0} kg"
            : "Brak danych";

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
