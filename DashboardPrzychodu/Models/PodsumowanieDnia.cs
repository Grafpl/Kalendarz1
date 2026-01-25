using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kalendarz1.DashboardPrzychodu.Models
{
    /// <summary>
    /// Podsumowanie dzienne przychodu żywca
    /// </summary>
    public class PodsumowanieDnia : INotifyPropertyChanged
    {
        // Współczynniki produkcyjne
        private const decimal WspolczynnikTuszek = 0.78m;    // 78% żywca → tuszki
        private const decimal WspolczynnikKlasaA = 0.80m;    // 80% tuszek → klasa A
        private const decimal WspolczynnikKlasaB = 0.20m;    // 20% tuszek → klasa B (krojenie)

        private int _sztukiPlanSuma;
        private decimal _kgPlanSuma;
        private decimal? _srWagaPlanSrednia;      // Średnia waga plan z harmonogramu
        private int _sztukiZwazoneSuma;
        private decimal _kgZwazoneSuma;
        private decimal? _srWagaRzeczSrednia;     // Średnia waga rzeczywista
        private decimal _kgPlanDoZwazonych;
        private int _liczbaDostawOgolem;
        private int _liczbaZwazonych;
        private int _liczbaCzekaNaTare;
        private int _liczbaOczekujacych;
        private decimal _odchylenieKgSuma;

        #region Properties - Plan (deklarowane)

        /// <summary>
        /// Suma sztuk deklarowanych na dzień
        /// </summary>
        public int SztukiPlanSuma
        {
            get => _sztukiPlanSuma;
            set { _sztukiPlanSuma = value; OnPropertyChanged(); OnPropertyChanged(nameof(SztukiPozostalo)); OnPropertyChanged(nameof(ProcentRealizacjiSztuki)); }
        }

        /// <summary>
        /// Suma kg deklarowanych na dzień
        /// </summary>
        public decimal KgPlanSuma
        {
            get => _kgPlanSuma;
            set { _kgPlanSuma = value; OnPropertyChanged(); OnPropertyChanged(nameof(KgPozostalo)); OnPropertyChanged(nameof(ProcentRealizacjiKg)); OnPropertyChanged(nameof(TuszkiPlanKg)); }
        }

        /// <summary>
        /// Średnia waga deklarowana z harmonogramu [kg/szt]
        /// </summary>
        public decimal? SrWagaPlanSrednia
        {
            get => _srWagaPlanSrednia;
            set { _srWagaPlanSrednia = value; OnPropertyChanged(); OnPropertyChanged(nameof(OdchylenieWagiSrednie)); }
        }

        #endregion

        #region Properties - Zważone (rzeczywiste)

        /// <summary>
        /// Suma sztuk już zważonych (kompletne dostawy)
        /// </summary>
        public int SztukiZwazoneSuma
        {
            get => _sztukiZwazoneSuma;
            set { _sztukiZwazoneSuma = value; OnPropertyChanged(); OnPropertyChanged(nameof(SztukiPozostalo)); OnPropertyChanged(nameof(ProcentRealizacjiSztuki)); }
        }

        /// <summary>
        /// Suma kg już zważonych (kompletne dostawy)
        /// </summary>
        public decimal KgZwazoneSuma
        {
            get => _kgZwazoneSuma;
            set
            {
                _kgZwazoneSuma = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(KgPozostalo));
                OnPropertyChanged(nameof(ProcentRealizacjiKg));
                OnPropertyChanged(nameof(PrognozaTuszekKg));
                OnPropertyChanged(nameof(PrognozaKlasaAKg));
                OnPropertyChanged(nameof(PrognozaKlasaBKg));
            }
        }

        /// <summary>
        /// Średnia waga rzeczywista [kg/szt]
        /// </summary>
        public decimal? SrWagaRzeczSrednia
        {
            get => _srWagaRzeczSrednia;
            set { _srWagaRzeczSrednia = value; OnPropertyChanged(); OnPropertyChanged(nameof(OdchylenieWagiSrednie)); }
        }

        /// <summary>
        /// Plan kg dla dostaw które zostały już zważone (do obliczenia odchylenia)
        /// </summary>
        public decimal KgPlanDoZwazonych
        {
            get => _kgPlanDoZwazonych;
            set
            {
                _kgPlanDoZwazonych = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OdchylenieProc));
                OnPropertyChanged(nameof(Poziom));
            }
        }

        #endregion

        #region Properties - Porównanie wag

        /// <summary>
        /// Odchylenie średniej wagi [kg/szt] (rzeczywista - deklarowana)
        /// </summary>
        public decimal? OdchylenieWagiSrednie => SrWagaRzeczSrednia.HasValue && SrWagaPlanSrednia.HasValue
            ? Math.Round(SrWagaRzeczSrednia.Value - SrWagaPlanSrednia.Value, 3)
            : null;

        /// <summary>
        /// Wyświetlane odchylenie wagi
        /// </summary>
        public string OdchylenieWagiDisplay => OdchylenieWagiSrednie.HasValue
            ? $"{(OdchylenieWagiSrednie > 0 ? "+" : "")}{OdchylenieWagiSrednie:N2} kg/szt"
            : "-";

        /// <summary>
        /// Trend wagi (interpretacja dla handlowców)
        /// </summary>
        public string OdchylenieWagiTrend => OdchylenieWagiSrednie switch
        {
            > 0.02m => "↑ Cięższe",
            < -0.02m => "↓ Lżejsze",
            _ => "≈ Zgodne"
        };

        #endregion

        #region Properties - Pozostałe (do przyjęcia)

        /// <summary>
        /// Ile sztuk jeszcze do zważenia
        /// </summary>
        public int SztukiPozostalo => Math.Max(0, SztukiPlanSuma - SztukiZwazoneSuma);

        /// <summary>
        /// Ile kg jeszcze do zważenia
        /// </summary>
        public decimal KgPozostalo => Math.Max(0, KgPlanSuma - KgZwazoneSuma);

        #endregion

        #region Properties - Odchylenie

        /// <summary>
        /// Suma odchyleń kg (rzeczywiste - plan dla zważonych)
        /// </summary>
        public decimal OdchylenieKgSuma
        {
            get => _odchylenieKgSuma;
            set
            {
                _odchylenieKgSuma = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OdchylenieDisplay));
                OnPropertyChanged(nameof(OdchylenieProc));
                OnPropertyChanged(nameof(Poziom));
            }
        }

        /// <summary>
        /// Odchylenie procentowe (dla zważonych dostaw)
        /// </summary>
        public decimal OdchylenieProc => KgPlanDoZwazonych > 0
            ? Math.Round(OdchylenieKgSuma / KgPlanDoZwazonych * 100, 2)
            : 0;

        /// <summary>
        /// Wyświetlany tekst odchylenia
        /// </summary>
        public string OdchylenieDisplay
        {
            get
            {
                if (KgZwazoneSuma == 0)
                    return "Brak danych";

                string znak = OdchylenieKgSuma > 0 ? "+" : "";
                return $"{znak}{OdchylenieKgSuma:N0} kg ({znak}{OdchylenieProc:N1}%)";
            }
        }

        /// <summary>
        /// Poziom odchylenia do kolorowania
        /// </summary>
        /// Więcej niż plan = zawsze OK (to dobrze!)
        /// Mniej niż plan = OK/Uwaga/Problem w zależności od wielkości
        public PoziomOdchylenia Poziom
        {
            get
            {
                if (KgZwazoneSuma == 0)
                    return PoziomOdchylenia.Brak;

                double procValue = (double)OdchylenieProc;

                // Więcej niż plan = zawsze OK (to dobrze!)
                if (procValue >= 0)
                    return PoziomOdchylenia.OK;

                // Mniej niż plan - sprawdzamy jak dużo brakuje
                double absProc = Math.Abs(procValue);
                if (absProc <= 2.0)
                    return PoziomOdchylenia.OK;
                if (absProc <= 5.0)
                    return PoziomOdchylenia.Uwaga;
                return PoziomOdchylenia.Problem;
            }
        }

        #endregion

        #region Properties - Prognoza produkcji

        /// <summary>
        /// Plan tuszek z harmonogramu [kg]
        /// </summary>
        public decimal TuszkiPlanKg => Math.Round(KgPlanSuma * WspolczynnikTuszek, 0);

        /// <summary>
        /// Prognoza tuszek z już zważonego żywca [kg]
        /// </summary>
        public decimal PrognozaTuszekKg => Math.Round(KgZwazoneSuma * WspolczynnikTuszek, 0);

        /// <summary>
        /// Prognoza klasy A (dobre tuszki) [kg]
        /// </summary>
        public decimal PrognozaKlasaAKg => Math.Round(PrognozaTuszekKg * WspolczynnikKlasaA, 0);

        /// <summary>
        /// Prognoza klasy B (do krojenia) [kg]
        /// </summary>
        public decimal PrognozaKlasaBKg => Math.Round(PrognozaTuszekKg * WspolczynnikKlasaB, 0);

        /// <summary>
        /// Info o przelicznikach
        /// </summary>
        public string PrognozaInfo => $"Tuszki: {WspolczynnikTuszek:P0} żywca | A: {WspolczynnikKlasaA:P0} | B: {WspolczynnikKlasaB:P0}";

        #endregion

        #region Properties - Faktyczny przychód z Symfonia (PWU)

        private decimal _faktKlasaAKg;
        private decimal _faktKlasaBKg;

        /// <summary>
        /// Faktyczny przychód klasy A z systemu Symfonia (dokumenty sPWU) [kg]
        /// Produkty gdzie kod zawiera "Kurczak A"
        /// </summary>
        public decimal FaktKlasaAKg
        {
            get => _faktKlasaAKg;
            set { _faktKlasaAKg = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Faktyczny przychód klasy B z systemu Symfonia (dokumenty sPWU) [kg]
        /// Produkty gdzie kod zawiera "Kurczak B"
        /// </summary>
        public decimal FaktKlasaBKg
        {
            get => _faktKlasaBKg;
            set { _faktKlasaBKg = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Suma faktycznego przychodu A + B [kg]
        /// </summary>
        public decimal FaktSumaKg => FaktKlasaAKg + FaktKlasaBKg;

        #endregion

        #region Properties - Liczniki dostaw

        /// <summary>
        /// Łączna liczba dostaw na dzień
        /// </summary>
        public int LiczbaDostawOgolem
        {
            get => _liczbaDostawOgolem;
            set { _liczbaDostawOgolem = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Liczba dostaw w pełni zważonych
        /// </summary>
        public int LiczbaZwazonych
        {
            get => _liczbaZwazonych;
            set { _liczbaZwazonych = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Liczba dostaw z brutto (czekających na tarę)
        /// </summary>
        public int LiczbaCzekaNaTare
        {
            get => _liczbaCzekaNaTare;
            set { _liczbaCzekaNaTare = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Liczba dostaw oczekujących (brak wag)
        /// </summary>
        public int LiczbaOczekujacych
        {
            get => _liczbaOczekujacych;
            set { _liczbaOczekujacych = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Tekst podsumowania dostaw
        /// </summary>
        public string DostawyPodsumowanie =>
            $"Zważone: {LiczbaZwazonych} | Na wadze: {LiczbaCzekaNaTare} | Oczekują: {LiczbaOczekujacych}";

        #endregion

        #region Properties - Procent realizacji

        /// <summary>
        /// Procent realizacji planu (sztuki)
        /// </summary>
        public int ProcentRealizacjiSztuki => SztukiPlanSuma > 0
            ? (int)Math.Round(SztukiZwazoneSuma * 100.0 / SztukiPlanSuma)
            : 0;

        /// <summary>
        /// Procent realizacji planu (kg)
        /// </summary>
        public int ProcentRealizacjiKg => KgPlanSuma > 0
            ? (int)Math.Round((double)KgZwazoneSuma * 100.0 / (double)KgPlanSuma)
            : 0;

        /// <summary>
        /// Wyświetlany procent realizacji
        /// </summary>
        public string ProcentRealizacjiDisplay => $"{ProcentRealizacjiKg}%";

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
