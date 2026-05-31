using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

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

        /// <summary>
        /// Domyslne okno dnia roboczego (dla #7 pace vs plan).
        /// Mozna nadpisac przez ustawienia uzytkownika (DashboardSettings, #9).
        /// </summary>
        public static TimeSpan WorkdayStart { get; set; } = new TimeSpan(6, 0, 0);
        public static TimeSpan WorkdayEnd   { get; set; } = new TimeSpan(14, 0, 0);

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
        private DateTime? _pierwszeWazenie;
        private DateTime? _ostatnieWazenie;

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

        #region Properties - Tempo / ETA / Pace (#6 + #7)

        /// <summary>
        /// Pierwsze wazenie dnia (SlaughterWeightDate najmniejsze z FullWeight+EmptyWeight > 0).
        /// Sluzy do liczenia faktycznego tempa ubojni.
        /// </summary>
        public DateTime? PierwszeWazenie
        {
            get => _pierwszeWazenie;
            set
            {
                _pierwszeWazenie = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TempoKgPerMin));
                OnPropertyChanged(nameof(EtaZakonczenia));
                OnPropertyChanged(nameof(EtaDisplay));
                OnPropertyChanged(nameof(EtaTooltip));
            }
        }

        /// <summary>
        /// Ostatnie wazenie dnia.
        /// </summary>
        public DateTime? OstatnieWazenie
        {
            get => _ostatnieWazenie;
            set
            {
                _ostatnieWazenie = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EtaTooltip));
            }
        }

        /// <summary>
        /// Tempo ubojni [kg/min] od pierwszego wazenia do teraz.
        /// Null jesli mniej niz 5 min minelo lub brak danych (zbyt malo do estymacji).
        /// </summary>
        public decimal? TempoKgPerMin
        {
            get
            {
                if (!PierwszeWazenie.HasValue || KgZwazoneSuma <= 0) return null;
                var minuty = (DateTime.Now - PierwszeWazenie.Value).TotalMinutes;
                if (minuty < 5) return null; // za malo danych do sensownej estymacji
                return Math.Round(KgZwazoneSuma / (decimal)minuty, 1);
            }
        }

        /// <summary>
        /// ETA zakonczenia ubojni: teraz + (pozostalo / tempo).
        /// Null gdy brak tempa lub nic juz nie pozostalo.
        /// </summary>
        public DateTime? EtaZakonczenia
        {
            get
            {
                var tempo = TempoKgPerMin;
                if (!tempo.HasValue || tempo.Value <= 0) return null;
                if (KgPozostalo <= 0) return null;
                double minutyDoKonca = (double)(KgPozostalo / tempo.Value);
                if (minutyDoKonca > 24 * 60) return null; // sanity check: >1 dzien = bzdura
                return DateTime.Now.AddMinutes(minutyDoKonca);
            }
        }

        /// <summary>
        /// Display "13:42" lub "—" jak nie da sie policzyc.
        /// </summary>
        public string EtaDisplay => EtaZakonczenia?.ToString("HH:mm") ?? "—";

        /// <summary>
        /// Tooltip rozszerzony dla kafelka ETA: tempo + okno czasowe.
        /// </summary>
        public string EtaTooltip
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Szacowany koniec dnia (na podstawie tempa).");
                sb.AppendLine();
                if (TempoKgPerMin.HasValue)
                    sb.AppendLine($"Tempo: {TempoKgPerMin:N1} kg/min");
                else
                    sb.AppendLine("Tempo: brak danych (potrzeba >=5 min od pierwszego wazenia)");
                if (PierwszeWazenie.HasValue)
                    sb.AppendLine($"Pierwsze wazenie: {PierwszeWazenie:HH:mm}");
                if (OstatnieWazenie.HasValue)
                    sb.AppendLine($"Ostatnie wazenie: {OstatnieWazenie:HH:mm}");
                sb.Append($"Pozostalo: {KgPozostalo:N0} kg");
                return sb.ToString();
            }
        }

        /// <summary>
        /// Oczekiwana realizacja [%] dla aktualnej pory dnia, liniowa od WorkdayStart do WorkdayEnd.
        /// Przed startem = 0%, po koncu = 100%.
        /// </summary>
        public decimal OczekiwanaRealizacjaProc
        {
            get
            {
                var now = DateTime.Now.TimeOfDay;
                if (now <= WorkdayStart) return 0;
                if (now >= WorkdayEnd) return 100;
                var totalSec = (WorkdayEnd - WorkdayStart).TotalSeconds;
                var elapsedSec = (now - WorkdayStart).TotalSeconds;
                return (decimal)Math.Round(elapsedSec / totalSec * 100, 1);
            }
        }

        /// <summary>
        /// Roznica miedzy aktualna a oczekiwana realizacja [pp].
        /// Dodatnia = wyprzedzenie, ujemna = opoznienie.
        /// </summary>
        public decimal PaceDiffProc => ProcentRealizacjiKg - OczekiwanaRealizacjaProc;

        /// <summary>
        /// Opoznienie/wyprzedzenie w minutach, ekstrapolowane z tempa dnia roboczego.
        /// </summary>
        public int? PaceDiffMinuty
        {
            get
            {
                var workdaySec = (WorkdayEnd - WorkdayStart).TotalSeconds;
                if (workdaySec <= 0) return null;
                // Roznica % * dlugosc dnia w minutach / 100
                return (int)Math.Round((double)PaceDiffProc * workdaySec / 60.0 / 100.0);
            }
        }

        /// <summary>
        /// Display pace: "+12 min wyprzedzasz" / "-24 min opoznienie" / "wg planu" / "—".
        /// </summary>
        public string PaceDisplay
        {
            get
            {
                var now = DateTime.Now.TimeOfDay;
                if (now < WorkdayStart || now > WorkdayEnd) return "—";
                if (KgPlanSuma <= 0) return "—";
                var min = PaceDiffMinuty;
                if (!min.HasValue) return "—";
                if (Math.Abs(min.Value) < 2) return "wg planu";
                return min > 0
                    ? $"+{min} min wyprzedzasz"
                    : $"{min} min opoznienie";
            }
        }

        /// <summary>
        /// Krotki badge: "+12 min" / "-24 min" / "OK" / "—".
        /// </summary>
        public string PaceBadge
        {
            get
            {
                var now = DateTime.Now.TimeOfDay;
                if (now < WorkdayStart || now > WorkdayEnd) return "";
                if (KgPlanSuma <= 0) return "";
                var min = PaceDiffMinuty;
                if (!min.HasValue) return "";
                if (Math.Abs(min.Value) < 2) return "OK";
                return min > 0 ? $"+{min} min" : $"{min} min";
            }
        }

        /// <summary>
        /// Kolor pace badge - zielony jak wyprzedzasz, czerwony jak opoznienie, szary OK.
        /// </summary>
        public Brush PaceBrush
        {
            get
            {
                var min = PaceDiffMinuty ?? 0;
                if (min >= 2) return new SolidColorBrush(Color.FromRgb(34, 197, 94));   // green
                if (min <= -2) return new SolidColorBrush(Color.FromRgb(239, 68, 68));  // red
                return new SolidColorBrush(Color.FromRgb(168, 162, 158));               // muted
            }
        }

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
