using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.Hodowcy.Models;
using Kalendarz1.Hodowcy.Services;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Win32;

namespace Kalendarz1.Hodowcy
{
    /// <summary>
    /// Karta Hodowcy 360° — dashboard agregujący dane hodowcy z 6 tabel LibraNet:
    /// Dostawcy, listapartii, PartiaDostawca, FarmerCalc, In0E, HarmonogramDostaw, DostawcyAdresy.
    /// 5 tabów: Przegląd (KPI + 2 wykresy), Historia partii, Harmonogram, Fermy, Dane podstawowe.
    /// </summary>
    public partial class KartaHodowcyWindow : Window
    {
        private readonly string _customerID;
        private readonly HodowcaProfilService _service = new();
        private HodowcaKartaDane? _dane;
        private int _wybranyOkresDni = 90;   // domyślnie 90 dni; 0 = cały zakres
        private bool _zaladowanoStartowo = false;

        public KartaHodowcyWindow(string customerID)
        {
            InitializeComponent();
            _customerID = customerID;
            KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
            Loaded += async (_, _) => await ZaladujAsync();
        }

        /// <summary>Czytelna nazwa wybranego okresu, np. "90 DNI", "rok", "całego zakresu".</summary>
        private string OkresNazwa() => _wybranyOkresDni switch
        {
            30 => "30 DNI",
            90 => "90 DNI",
            180 => "6 MIESIĘCY",
            365 => "ROKU",
            730 => "2 LAT",
            0 => "CAŁEGO ZAKRESU",
            _ => $"{_wybranyOkresDni} DNI"
        };

        /// <summary>Format etykiet osi X wykresu zależny od długości okresu.</summary>
        private string FormatEtykietyOsi() => _wybranyOkresDni switch
        {
            30 => "dd.MM",
            90 => "dd.MM",
            180 => "MM.yyyy",
            365 => "MM.yyyy",
            730 => "MM.yyyy",
            _ => "MM.yyyy"   // dla cały zakres
        };

        private async void CbOkres_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!_zaladowanoStartowo) return;
            if (cbOkres.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out int dni))
            {
                _wybranyOkresDni = dni;
                if (_dane != null) PrzelicszStatystykiIRender();
            }
            await Task.Yield();
        }

        /// <summary>Po zmianie okresu — przelicz statystyki + wszystkie KPI + wykresy.</summary>
        private void PrzelicszStatystykiIRender()
        {
            if (_dane == null) return;
            int? okno = _wybranyOkresDni == 0 ? null : (int?)_wybranyOkresDni;
            _dane.Stat90Dni = _service.BudujStatystyki(_dane.Partie, okno);
            // Renderujemy ponownie KPI + wykresy + mini-stat (tabele zostają — pokazują wszystkie partie)
            RenderujKpi();
            RenderujKpi2();
            RenderujKpi3();
            RenderujKpi4();
            RenderujMiniStat();
            RenderujWykresy();
        }

        private async Task ZaladujAsync()
        {
            var stoper = Stopwatch.StartNew();
            try
            {
                _dane = await _service.LoadAllAsync(_customerID);
                if (_dane == null)
                {
                    MessageBox.Show($"Nie znaleziono hodowcy o ID = {_customerID}",
                        "Karta hodowcy", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Close();
                    return;
                }
                Renderuj();
                _zaladowanoStartowo = true;
                stoper.Stop();

                string status = $"Załadowano w {stoper.Elapsed.TotalSeconds:F1}s  •  " +
                                $"{_dane.Partie.Count} partii  •  {_dane.Harmonogram.Count} harmonogram  •  " +
                                $"{_dane.Fermy.Count} ferm  •  {_dane.Klasy.Count} klas wag.";

                if (_dane.Bledy.Count > 0)
                {
                    status += $"  •  ⚠ {_dane.Bledy.Count} źródeł nie załadowano (kliknij ikonę ⚠ obok zegarka)";
                    txtCzasLad.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                    txtCzasLad.ToolTip = "BŁĘDY ŁADOWANIA:\n\n" + string.Join("\n", _dane.Bledy);
                    txtCzasLad.Cursor = Cursors.Help;
                }
                txtCzasLad.Text = status;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd ładowania karty hodowcy:\n" + ex.Message,
                    "Karta hodowcy", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Renderuj()
        {
            if (_dane == null) return;
            RenderujHeader();
            RenderujKpi();
            RenderujKpi2();
            RenderujKpi3();
            RenderujKpi4();
            RenderujSpecyfikacje();
            RenderujTransport();
            RenderujMiniStat();
            RenderujWykresy();
            RenderujPartie();
            RenderujHarmonogram();
            RenderujFermy();
            RenderujDanePodstawowe();
            RenderujKlasy();
            RenderujRanking();
            RenderujAnomalie();
            RenderujJakosc();
            RenderujRozliczenia();
        }

        // ─── Header ───────────────────────────────────────────────────────────

        private void RenderujHeader()
        {
            var p = _dane!.Profil;
            txtNagNazwa.Text = string.IsNullOrWhiteSpace(p.Name) ? "(brak nazwy)" : p.Name;
            txtNagId.Text = $"ID: {p.ID}";
            txtNagNip.Text = $"NIP: {(string.IsNullOrWhiteSpace(p.Nip) ? "—" : p.Nip)}";
            txtNagAnimNo.Text = $"Gospod.: {(string.IsNullOrWhiteSpace(p.AnimNo) ? "—" : p.AnimNo)}";
            txtNagAdres.Text = $"📍 {p.AdresPelny}" + (p.Distance.HasValue ? $"  •  {p.Distance} km" : "");
            txtNagKontakt.Text = $"📞 {p.KontaktTelefon}" + (string.IsNullOrWhiteSpace(p.Email) ? "" : $"  •  ✉️ {p.Email}");

            // Status badge
            if (p.Halt)
            {
                txtBadgeStatus.Text = "⏸ Wstrzymany";
                badgeStatus.Background = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
            }
            else if (_dane.Stat90Dni.LiczbaPartii == 0)
            {
                txtBadgeStatus.Text = "⚠ Bez dostaw 90 dni";
                badgeStatus.Background = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            }
            else
            {
                txtBadgeStatus.Text = "✓ Aktywny";
                badgeStatus.Background = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
            }

            badgeRol.Visibility = p.IsRolnik ? Visibility.Visible : Visibility.Collapsed;
            badgeSkup.Visibility = p.IsSkupowy ? Visibility.Visible : Visibility.Collapsed;
        }

        // ─── KPI ──────────────────────────────────────────────────────────────

        private void RenderujKpi()
        {
            var s90 = _dane!.Stat90Dni;
            var sAll = _dane.StatCaleZycie;

            // Dynamiczna etykieta: "PARTII (90 DNI)" / "PARTII (ROKU)" / itd.
            kpiPartiiLabel.Text = $"📦  PARTII ({OkresNazwa()})";

            kpiPartii.Text = s90.LiczbaPartii.ToString("N0");
            kpiPartiiZycie.Text = $"życie: {sAll.LiczbaPartiiZycie:N0} partii";

            kpiSkupKg.Text = s90.SumaSkupKg.ToString("N0");
            kpiPrzyjetoKg.Text = s90.SumaPrzyjetoKg > 0
                ? $"In0E: {s90.SumaPrzyjetoKg:N0} kg"
                : "(In0E: 0)";

            kpiWydaj.Text = s90.SrWydajnosc.HasValue && s90.SrWydajnosc.Value > 0
                ? $"{s90.SrWydajnosc.Value:F2}%"
                : "—";
            kpiWydaj.Foreground = WydajnoscKolor(s90.SrWydajnosc);

            kpiKlasaB.Text = s90.SrKlasaB.HasValue && s90.SrKlasaB.Value > 0
                ? $"klasa B: {s90.SrKlasaB.Value:F1}%"
                : "";

            if (s90.OstatniaDostawa.HasValue)
            {
                kpiOstatnia.Text = s90.OstatniaDostawa.Value.ToString("dd.MM.yyyy");
                kpiOstatniaDni.Text = s90.DniOdOstatniej.HasValue
                    ? $"{s90.DniOdOstatniej} dni temu"
                    : "";
            }
            else
            {
                kpiOstatnia.Text = "—";
                kpiOstatniaDni.Text = "brak dostaw 90d";
            }

            kpiCykl.Text = s90.SrCyklDni.HasValue ? $"{s90.SrCyklDni}" : "—";

            if (s90.SzacowanyObrot.HasValue && s90.SzacowanyObrot.Value > 0)
            {
                kpiObrot.Text = $"{s90.SzacowanyObrot.Value:N0} zł";
                kpiCenaSkup.Text = s90.SrCenaSkup.HasValue
                    ? $"śr. cena: {s90.SrCenaSkup.Value:F2} zł/kg"
                    : "";
            }
            else
            {
                kpiObrot.Text = "—";
                kpiCenaSkup.Text = "brak danych z FarmerCalc";
            }
        }

        private void RenderujKpi2()
        {
            var r = _dane!.Ranking;
            var s90 = _dane.Stat90Dni;

            // Ranking
            if (r.LiczbaHodowcow > 0 && r.Pozycja > 0)
            {
                kpiRanking.Text = $"#{r.Pozycja} / {r.LiczbaHodowcow}";
                kpiRankingOcena.Text = r.OcenaTextowa;
                kpiRankingOcena.Foreground = OcenaKolor(r.Pozycja, r.LiczbaHodowcow);
            }
            else
            {
                kpiRanking.Text = "—";
                kpiRankingOcena.Text = "brak danych z innych hodowców";
            }

            kpiUdzial.Text = r.RynekUdzial > 0 ? $"{r.RynekUdzial:F2}%" : "—";
            kpiUdzialSub.Text = r.MojaSumaKg > 0 ? $"{r.MojaSumaKg:N0} kg / {r.LiczbaHodowcow} hodowców" : "";

            // Klasy wagowe — udział duży/mały
            decimal sumaKlasy = _dane.Klasy.Sum(k => k.SumaKg);
            if (sumaKlasy > 0)
            {
                decimal duzy = _dane.Klasy.Where(k => k.Klasa is >= 4 and <= 7).Sum(k => k.SumaKg);
                decimal maly = _dane.Klasy.Where(k => k.Klasa is >= 8 and <= 12).Sum(k => k.SumaKg);
                kpiDuzy.Text = $"{duzy / sumaKlasy * 100m:F1}%";
                kpiMaly.Text = $"{maly / sumaKlasy * 100m:F1}%";
            }
            else
            {
                kpiDuzy.Text = "—";
                kpiMaly.Text = "—";
            }

            // Temp rampy + padłe
            kpiTempRampa.Text = s90.SrTempRampa.HasValue && s90.SrTempRampa.Value > 0
                ? $"{s90.SrTempRampa.Value:F1}°C"
                : "—";

            // Padłe % = padłe / sztuki * 100
            if (s90.SumaPadle.HasValue && s90.SumaSztukDek.HasValue && s90.SumaSztukDek.Value > 0)
            {
                decimal proc = s90.SumaPadle.Value / s90.SumaSztukDek.Value * 100m;
                kpiPadle.Text = $"{proc:F2}%";
                kpiPadle.Foreground = proc > 5m
                    ? new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26))
                    : (proc > 2m
                        ? new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B))
                        : new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69)));
            }
            else
            {
                kpiPadle.Text = "—";
            }
        }

        private void RenderujKpi3()
        {
            var s90 = _dane!.Stat90Dni;
            var sAll = _dane.StatCaleZycie;

            // Mini-stat dolny przy ostatniej dostawie też zależy od okresu
            kpiOstatniaDni.Text = s90.OstatniaDostawa.HasValue
                ? (s90.DniOdOstatniej.HasValue ? $"{s90.DniOdOstatniej} dni temu" : "")
                : $"brak dostaw w okresie";

            // Średni wiek partii (dni od wstawienia)
            if (s90.SrWiekDni.HasValue && s90.LiczbaPartiiZWstawieniem > 0)
            {
                kpiWiekDni.Text = $"{s90.SrWiekDni.Value:F1} dni";
                kpiWiekZakres.Text = (s90.MinWiekDni.HasValue && s90.MaxWiekDni.HasValue)
                    ? $"zakres: {s90.MinWiekDni}–{s90.MaxWiekDni} dni"
                    : "";
                // Norma 35-42 dni — kolorujemy
                double v = (double)s90.SrWiekDni.Value;
                if (v >= 35 && v <= 42) kpiWiekDni.Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x76, 0x6E));
                else if (v >= 30 && v <= 45) kpiWiekDni.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                else kpiWiekDni.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
            }
            else
            {
                kpiWiekDni.Text = "—";
                kpiWiekZakres.Text = "brak HarmonogramLp";
            }

            // Średnia waga sztuki
            if (s90.SrWagaSzt.HasValue && s90.SrWagaSzt.Value > 0)
                kpiWagaSzt.Text = $"{s90.SrWagaSzt.Value:F3} kg";
            else
                kpiWagaSzt.Text = "—";

            // Straty śmiertelności
            if (s90.StratySztProc.HasValue)
            {
                kpiStraty.Text = $"{s90.StratySztProc.Value:F2}%";
                kpiStratySzt.Text = s90.SumaStratSzt.HasValue ? $"{s90.SumaStratSzt:N0} szt. razem" : "";
                // Norma branżowa < 2% — kolorujemy
                double v = (double)s90.StratySztProc.Value;
                if (v <= 2) kpiStraty.Foreground = new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69));
                else if (v <= 4) kpiStraty.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                else kpiStraty.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
            }
            else
            {
                kpiStraty.Text = "—";
                kpiStratySzt.Text = "brak danych In0E";
            }

            // Pierwsza / ostatnia dostawa + długość współpracy
            if (sAll.PierwszaDostawa.HasValue && sAll.OstatniaDostawa.HasValue)
            {
                kpiZakresDat.Text = $"{sAll.PierwszaDostawa.Value:dd.MM.yyyy}  →  {sAll.OstatniaDostawa.Value:dd.MM.yyyy}";
                int dni = (sAll.OstatniaDostawa.Value - sAll.PierwszaDostawa.Value).Days;
                if (dni >= 365)
                    kpiOkresLat.Text = $"{dni / 365.0:F1} lat współpracy";
                else
                    kpiOkresLat.Text = $"{dni} dni współpracy";
            }
            else
            {
                kpiZakresDat.Text = "—";
                kpiOkresLat.Text = "";
            }

            // Partii z prawidłowym Harmonogramem
            int liczbaPow = sAll.LiczbaPartiiZWstawieniem;
            int liczbaCalk = sAll.LiczbaPartiiZycie;
            kpiPartiiZWstaw.Text = $"{liczbaPow} / {liczbaCalk}";
            kpiPartiiZWstawSub.Text = liczbaCalk > 0
                ? $"{liczbaPow * 100.0 / liczbaCalk:F0}% partii powiązanych"
                : "";
        }

        // ─── KPI 4: konfiskaty (CH/NW/ZM/LUMEL) + ubytek transportowy ─────────

        private void RenderujKpi4()
        {
            var s = _dane!.Stat90Dni;
            kpiCH.Text = s.SumaCH > 0 ? s.SumaCH.ToString("N0") : "—";
            kpiNW.Text = s.SumaNW > 0 ? s.SumaNW.ToString("N0") : "—";
            kpiZM.Text = s.SumaZM > 0 ? s.SumaZM.ToString("N0") : "—";
            kpiLUMEL.Text = s.SumaLUMEL > 0 ? s.SumaLUMEL.ToString("N0") : "—";

            if (s.KonfiskatyProc.HasValue && s.KonfiskatyProc.Value > 0)
            {
                kpiKonfProc.Text = $"{s.KonfiskatyProc.Value:F2}%";
                kpiKonfSzt.Text = $"{s.SumaKonfiskat:N0} szt. razem";
                // Norma: konfiskaty < 1% to świetnie, > 3% problem
                double v = (double)s.KonfiskatyProc.Value;
                if (v <= 1) kpiKonfProc.Foreground = new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69));
                else if (v <= 3) kpiKonfProc.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                else kpiKonfProc.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
            }
            else
            {
                kpiKonfProc.Text = "—";
                kpiKonfSzt.Text = s.SumaKonfiskat > 0 ? $"{s.SumaKonfiskat:N0} szt. razem" : "brak danych";
            }

            if (s.SrUbytekTransProc.HasValue)
            {
                kpiUbytekTransProc.Text = $"{s.SrUbytekTransProc.Value:F2}%";
                kpiUbytekTransKg.Text = s.SumaUbytekTransKg.HasValue
                    ? $"łącznie {s.SumaUbytekTransKg.Value:N0} kg"
                    : "";
                // Ubytek transportowy 1-3% norma, > 5% problem
                double v = (double)s.SrUbytekTransProc.Value;
                if (v <= 3) kpiUbytekTransProc.Foreground = new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69));
                else if (v <= 5) kpiUbytekTransProc.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                else kpiUbytekTransProc.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
            }
            else
            {
                kpiUbytekTransProc.Text = "—";
                kpiUbytekTransKg.Text = "brak NettoH";
            }
        }

        private void RenderujSpecyfikacje() => dgSpecyfikacja.ItemsSource = _dane!.Partie;

        private void RenderujTransport() =>
            dgTransport.ItemsSource = _dane!.Partie.Where(p =>
                p.Przyjazd.HasValue || p.Zaladunek.HasValue || !string.IsNullOrEmpty(p.Kierowca)).ToList();

        private static Brush OcenaKolor(int poz, int liczba)
        {
            if (liczba == 0) return new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            double percentyl = (double)poz / liczba * 100.0;
            if (percentyl <= 10) return new SolidColorBrush(Color.FromRgb(0xCA, 0x8A, 0x04)); // złoty
            if (percentyl <= 25) return new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69));
            if (percentyl <= 50) return new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
            if (percentyl <= 75) return new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            return new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
        }

        // ─── Renderowanie nowych tabów ────────────────────────────────────────

        private void RenderujKlasy()
        {
            if (_dane!.Klasy.Count == 0)
            {
                wykresKlasyHod.Series = new SeriesCollection();
                osXKlasyHod.Labels = new List<string>();
                dgKlasyHod.ItemsSource = null;
                txtKlasyTitle.Text = "📊  Brak danych z In0E dla tego hodowcy w ostatnim roku";
                return;
            }

            // 9 słupków (klasy 4-12), kolor zależny od grupy
            var klasy = _dane.Klasy.OrderBy(k => k.Klasa).ToList();
            var values = new ChartValues<double>(klasy.Select(k => (double)k.SumaKg));
            var kolory = klasy.Select(k => k.Klasa is >= 4 and <= 7
                ? Color.FromRgb(0x25, 0x63, 0xEB)
                : Color.FromRgb(0xF9, 0x73, 0x16)).ToList();

            // LiveCharts.Wpf 0.9 nie wspiera per-bar fill bez własnego mappera — używamy 2 serii
            var serieDuzy = new ChartValues<double>(klasy.Select(k =>
                k.Klasa is >= 4 and <= 7 ? (double)k.SumaKg : 0.0));
            var serieMaly = new ChartValues<double>(klasy.Select(k =>
                k.Klasa is >= 8 and <= 12 ? (double)k.SumaKg : 0.0));

            wykresKlasyHod.Series = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "🍗 Duży 4-7",
                    Values = serieDuzy,
                    Fill = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)),
                    Stroke = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x8A)),
                    StrokeThickness = 1,
                    DataLabels = true,
                    LabelPoint = p => p.Y > 0 ? $"{p.Y:N0} kg" : "",
                    MaxColumnWidth = 60,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold
                },
                new ColumnSeries
                {
                    Title = "🐥 Mały 8-12",
                    Values = serieMaly,
                    Fill = new SolidColorBrush(Color.FromRgb(0xF9, 0x73, 0x16)),
                    Stroke = new SolidColorBrush(Color.FromRgb(0x9A, 0x34, 0x12)),
                    StrokeThickness = 1,
                    DataLabels = true,
                    LabelPoint = p => p.Y > 0 ? $"{p.Y:N0} kg" : "",
                    MaxColumnWidth = 60,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold
                }
            };
            osXKlasyHod.Labels = klasy.Select(k => $"Klasa {k.Klasa}").ToList();
            osYKlasyHod.LabelFormatter = v => v.ToString("N0");

            dgKlasyHod.ItemsSource = klasy;
        }

        private void RenderujRanking()
        {
            var r = _dane!.Ranking;
            if (r.LiczbaHodowcow == 0)
            {
                txtRankingNagl.Text = "🏆  Brak danych z innych hodowców do porównania";
                txtRankingOpis.Text = "—";
                rPoz.Text = rPozKg.Text = rUdzial.Text = rWydaj.Text = rSrednia.Text = rTop10.Text = "—";
                return;
            }

            txtRankingOpis.Text = $"{r.OcenaTextowa}  •  Pozycja {r.Pozycja} z {r.LiczbaHodowcow} hodowców  •  " +
                                  $"Twoja wydajność {r.MojaWydajnosc:F2}% vs średnia zakładu {r.SredniaZakladu:F2}% " +
                                  $"({(r.RoznicaDoSredniej >= 0 ? "+" : "")}{r.RoznicaDoSredniej:F2}pp)";
            rPoz.Text = $"#{r.Pozycja} / {r.LiczbaHodowcow}";
            rPozKg.Text = $"#{r.RankingKg} / {r.LiczbaHodowcow}";
            rUdzial.Text = $"{r.RynekUdzial:F2}%";
            rWydaj.Text = $"{r.MojaWydajnosc:F2}%";
            rWydaj.Foreground = WydajnoscKolor(r.MojaWydajnosc);
            rSrednia.Text = $"{r.SredniaZakladu:F2}% / {r.MedianaZakladu:F2}%";
            rTop10.Text = $"{r.Top10Wydajnosc:F2}%";
        }

        private void RenderujAnomalie()
        {
            dgAnomalie.ItemsSource = _dane!.Anomalie;
        }

        private void RenderujJakosc()
        {
            var trend = _dane!.Trend.OrderBy(t => t.Data).ToList();
            if (trend.Count == 0)
            {
                wykresTemp.Series = new SeriesCollection();
                wykresKlasaB.Series = new SeriesCollection();
                wykresPadle.Series = new SeriesCollection();
                return;
            }

            var partie = _dane.Partie.OrderBy(p => p.CreateData).ToList();
            string fmt = partie.Count > 0 && (partie.Last().CreateData - partie.First().CreateData).TotalDays > 180
                ? "MM.yyyy" : "dd.MM.yy";
            var labels = partie.Select(p => p.CreateData.ToString(fmt)).ToList();

            // Temperatura rampy
            var temps = partie.Select(p => (double)(p.TempRampa ?? 0m)).ToList();
            wykresTemp.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Temp. rampy",
                    Values = new ChartValues<double>(temps),
                    Stroke = new SolidColorBrush(Color.FromRgb(0x08, 0x91, 0xB2)),
                    StrokeThickness = 2.5,
                    Fill = new SolidColorBrush(Color.FromArgb(0x22, 0x08, 0x91, 0xB2)),
                    PointGeometry = LiveCharts.Wpf.DefaultGeometries.Circle,
                    PointGeometrySize = 8,
                    LineSmoothness = 0.2,
                    DataLabels = false
                }
            };
            osXTemp.Labels = labels;
            osYTemp.LabelFormatter = v => $"{v:F1}°C";

            // Klasa B
            var klasaB = partie.Select(p => (double)(p.KlasaBProc ?? 0m)).ToList();
            wykresKlasaB.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Klasa B %",
                    Values = new ChartValues<double>(klasaB),
                    Stroke = new SolidColorBrush(Color.FromRgb(0xF9, 0x73, 0x16)),
                    StrokeThickness = 2.5,
                    Fill = new SolidColorBrush(Color.FromArgb(0x22, 0xF9, 0x73, 0x16)),
                    PointGeometry = LiveCharts.Wpf.DefaultGeometries.Diamond,
                    PointGeometrySize = 8,
                    LineSmoothness = 0.2,
                    DataLabels = false
                }
            };
            osXKlasaB.Labels = labels;
            osYKlasaB.LabelFormatter = v => $"{v:F1}%";

            // Padłe (kolumny czerwone)
            var padle = partie.Select(p => (double)(p.Padle ?? 0m)).ToList();
            wykresPadle.Series = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "Padłe",
                    Values = new ChartValues<double>(padle),
                    Fill = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),
                    Stroke = new SolidColorBrush(Color.FromRgb(0x99, 0x1B, 0x1B)),
                    StrokeThickness = 1,
                    MaxColumnWidth = 30,
                    DataLabels = true,
                    LabelPoint = p => p.Y > 0 ? p.Y.ToString("N0") : "",
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x1B, 0x1B))
                }
            };
            osXPadle.Labels = labels;
            osYPadle.LabelFormatter = v => v.ToString("N0");
        }

        private void RenderujRozliczenia()
        {
            var partie = _dane!.Partie;
            decimal w90 = _dane.Stat90Dni.SzacowanyObrot ?? 0m;
            decimal wAll = _dane.StatCaleZycie.SzacowanyObrot ?? 0m;
            var ceny = partie.Where(p => p.CenaSkup.HasValue && p.CenaSkup.Value > 0).Select(p => p.CenaSkup!.Value).ToList();

            finWartosc90.Text = w90 > 0 ? $"{w90:N0} zł" : "—";
            finWartoscZycie.Text = wAll > 0 ? $"{wAll:N0} zł" : "—";
            finMaxCena.Text = ceny.Count > 0 ? $"{ceny.Max():F2} zł" : "—";
            finMinCena.Text = ceny.Count > 0 ? $"{ceny.Min():F2} zł" : "—";

            // Wykres ceny
            var cenoPartie = partie.Where(p => p.CenaSkup.HasValue && p.CenaSkup.Value > 0)
                .OrderBy(p => p.CreateData).ToList();
            if (cenoPartie.Count == 0)
            {
                wykresCeny.Series = new SeriesCollection();
                osXCeny.Labels = new List<string>();
            }
            else
            {
                wykresCeny.Series = new SeriesCollection
                {
                    new LineSeries
                    {
                        Title = "Cena zł/kg",
                        Values = new ChartValues<double>(cenoPartie.Select(p => (double)p.CenaSkup!.Value)),
                        Stroke = new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED)),
                        StrokeThickness = 3,
                        Fill = new SolidColorBrush(Color.FromArgb(0x25, 0x7C, 0x3A, 0xED)),
                        PointGeometry = LiveCharts.Wpf.DefaultGeometries.Circle,
                        PointGeometrySize = 10,
                        LineSmoothness = 0.2,
                        DataLabels = true,
                        LabelPoint = p => $"{p.Y:F2}",
                        FontSize = 9,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x5B, 0x21, 0xB6))
                    }
                };
                string fmtC = cenoPartie.Count > 0 && (cenoPartie.Last().CreateData - cenoPartie.First().CreateData).TotalDays > 180
                    ? "MM.yyyy" : "dd.MM.yy";
                osXCeny.Labels = cenoPartie.Select(p => p.CreateData.ToString(fmtC)).ToList();
                osYCeny.LabelFormatter = v => $"{v:F2}";
            }

            // Tabela: agregacja miesięczna
            var miesieczne = HodowcaProfilService.AgregujOkresami(
                partie, AnalitykaPelna.Models.OkresAgregacji.Miesieczna);
            dgRozliczenia.ItemsSource = miesieczne;
        }

        private static Brush WydajnoscKolor(decimal? w)
        {
            if (!w.HasValue || w.Value <= 0) return new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            // Norma: ~95%, tolerancja ±2pp
            double v = (double)w.Value;
            if (Math.Abs(v - 95.0) <= 2.0) return new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69));
            if (Math.Abs(v - 95.0) <= 4.0) return new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            return new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
        }

        // ─── Mini-stat (na zakładce Przegląd) ─────────────────────────────────

        private void RenderujMiniStat()
        {
            var s = _dane!.StatCaleZycie;
            var s90 = _dane.Stat90Dni;
            var sb = new StringBuilder();

            if (s.LiczbaPartiiZycie > 0)
            {
                sb.Append($"📊  Hodowca aktywny od ");
                sb.Append(s.PierwszaDostawa?.ToString("dd.MM.yyyy") ?? "—");
                sb.Append($" ({(DateTime.Today - (s.PierwszaDostawa ?? DateTime.Today)).Days} dni)  •  ");
                sb.Append($"Życie: {s.LiczbaPartiiZycie:N0} partii / {s.SumaSkupKg:N0} kg żywca / wydajność śr. {s.SrWydajnosc:F2}%  •  ");
            }
            else
            {
                sb.Append("📊  Brak partii w bazie listapartii  •  ");
            }

            if (s90.LiczbaPartii > 0)
            {
                sb.Append($"Ostatnie 90 dni: {s90.LiczbaPartii} partii / {s90.SumaSkupKg:N0} kg / cykl {s90.SrCyklDni ?? 0} dni  •  ");
                if (s90.SumaPadle.HasValue && s90.SumaPadle.Value > 0)
                    sb.Append($"⚠ padłe: {s90.SumaPadle:N0} szt.  •  ");
            }
            else
            {
                sb.Append("⚠ Brak partii w ostatnich 90 dniach  •  ");
            }

            sb.Append($"Harmonogram: {_dane.Harmonogram.Count(h => !h.MaPartie):N0} planowanych");
            txtMiniStat.Text = sb.ToString();
        }

        // ─── Wykresy ──────────────────────────────────────────────────────────

        private void RenderujWykresy()
        {
            // Filtr według wybranego okresu
            DateTime? cutoff = _wybranyOkresDni > 0
                ? (DateTime?)DateTime.Today.AddDays(-_wybranyOkresDni)
                : null;
            var trend = _dane!.Trend
                .Where(t => t.WydajnoscProc.HasValue || t.KlasaBProc.HasValue)
                .Where(t => cutoff == null || t.Data >= cutoff.Value)
                .ToList();

            if (trend.Count == 0)
            {
                wykresWydaj.Series = new SeriesCollection();
                osXWydaj.Labels = new List<string>();
                wykresWolumen.Series = new SeriesCollection();
                osXWolumen.Labels = new List<string>();
                return;
            }

            string fmt = FormatEtykietyOsi();

            // Wykres 1: Wydajność + Klasa B (linia + linia)
            wykresWydaj.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "🎯 Wydajność %",
                    Values = new ChartValues<double>(trend.Select(t => (double)(t.WydajnoscProc ?? 0m))),
                    Stroke = new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69)),
                    StrokeThickness = 3,
                    Fill = new SolidColorBrush(Color.FromArgb(0x22, 0x05, 0x96, 0x69)),
                    PointGeometry = LiveCharts.Wpf.DefaultGeometries.Circle,
                    PointGeometrySize = 10,
                    PointForeground = new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69)),
                    LineSmoothness = 0.25,
                    DataLabels = false,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69))
                },
                new LineSeries
                {
                    Title = "🏷 Klasa B %",
                    Values = new ChartValues<double>(trend.Select(t => (double)(t.KlasaBProc ?? 0m))),
                    Stroke = new SolidColorBrush(Color.FromRgb(0xF9, 0x73, 0x16)),
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent,
                    PointGeometry = LiveCharts.Wpf.DefaultGeometries.Diamond,
                    PointGeometrySize = 9,
                    PointForeground = new SolidColorBrush(Color.FromRgb(0xF9, 0x73, 0x16)),
                    LineSmoothness = 0.2,
                    DataLabels = false,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xF9, 0x73, 0x16))
                }
            };
            osXWydaj.Labels = trend.Select(t => t.Data.ToString(fmt)).ToList();
            osYWydaj.LabelFormatter = v => v.ToString("F1") + "%";

            // Wykres 2: Wolumen kg (kolumny niebieskie)
            wykresWolumen.Series = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "Skup żywca",
                    Values = new ChartValues<double>(trend.Select(t => (double)t.NettoSkup)),
                    Fill = new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69)),
                    Stroke = new SolidColorBrush(Color.FromRgb(0x06, 0x4E, 0x3B)),
                    StrokeThickness = 1,
                    DataLabels = true,
                    LabelPoint = p => p.Y > 0 ? p.Y.ToString("N0") : "",
                    MaxColumnWidth = 50,
                    ColumnPadding = 4,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x06, 0x4E, 0x3B))
                }
            };
            osXWolumen.Labels = trend.Select(t => t.Data.ToString(fmt)).ToList();
            osYWolumen.LabelFormatter = v => v.ToString("N0");
        }

        // ─── Tabele ───────────────────────────────────────────────────────────

        private void RenderujPartie()
        {
            dgPartie.ItemsSource = _dane!.Partie;
            txtPartiiInfo.Text = $"Wyświetlono {_dane.Partie.Count} partii (sortowanie: najnowsze najpierw)";
        }

        private void RenderujHarmonogram()
        {
            dgHarmonogram.ItemsSource = _dane!.Harmonogram;
            int planowane = _dane.Harmonogram.Count(h => !h.MaPartie);
            int zrealizowane = _dane.Harmonogram.Count(h => h.MaPartie);
            txtHarmInfo.Text = $"Plan dostaw na 60 dni do przodu i 30 wstecz  •  Planowanych: {planowane}  •  Zrealizowanych: {zrealizowane}";
        }

        private void RenderujFermy()
        {
            dgFermy.ItemsSource = _dane!.Fermy;
            txtFermyInfo.Text = $"Adresy ferm hodowcy (DostawcyAdresy.Kind=2)  •  Liczba ferm: {_dane.Fermy.Count}";
        }

        private void RenderujDanePodstawowe()
        {
            var p = _dane!.Profil;
            dpID.Text = string.IsNullOrEmpty(p.ID) ? "—" : p.ID;
            dpGID.Text = p.GID.ToString();
            dpName.Text = string.IsNullOrEmpty(p.Name) ? "—" : p.Name;
            dpShortName.Text = string.IsNullOrEmpty(p.ShortName) ? "—" : p.ShortName;
            dpNip.Text = string.IsNullOrEmpty(p.Nip) ? "—" : p.Nip;
            dpRegon.Text = string.IsNullOrEmpty(p.Regon) ? "—" : p.Regon;
            dpPesel.Text = string.IsNullOrEmpty(p.Pesel) ? "—" : p.Pesel;
            dpIDCard.Text = string.IsNullOrEmpty(p.IDCard) ? "—" : p.IDCard;
            dpIDCardDate.Text = p.IDCardDate.HasValue ? p.IDCardDate.Value.ToString("dd.MM.yyyy") : "—";
            dpIDCardAuth.Text = string.IsNullOrEmpty(p.IDCardAuth) ? "—" : p.IDCardAuth;
            dpAnimNo.Text = string.IsNullOrEmpty(p.AnimNo) ? "—" : p.AnimNo;
            dpIRZPlus.Text = string.IsNullOrEmpty(p.IRZPlus) ? "—" : p.IRZPlus;

            dpAddress.Text = string.IsNullOrEmpty(p.Address) ? "—" : p.Address;
            dpPostal.Text = string.IsNullOrEmpty(p.PostalCode) ? "—" : p.PostalCode;
            dpCity.Text = string.IsNullOrEmpty(p.City) ? "—" : p.City;
            dpProv.Text = string.IsNullOrEmpty(p.ProvinceID) ? "—" : p.ProvinceID;
            dpDistance.Text = p.Distance.HasValue ? $"{p.Distance} km" : "—";
            dpTrasa.Text = string.IsNullOrEmpty(p.Trasa) ? "—" : p.Trasa;
            dpPhone1.Text = string.IsNullOrEmpty(p.Phone1) ? "—" : p.Phone1;
            dpPhone2.Text = string.IsNullOrEmpty(p.Phone2) ? "—" : p.Phone2;
            dpPhone3.Text = string.IsNullOrEmpty(p.Phone3) ? "—" : p.Phone3;
            dpEmail.Text = string.IsNullOrEmpty(p.Email) ? "—" : p.Email;

            dpPriceType.Text = p.PriceTypeID.HasValue ? p.PriceTypeID.Value.ToString() : "—";
            dpAddition.Text = p.Addition.HasValue ? p.Addition.Value.ToString("F2") + " zł" : "—";
            dpLoss.Text = p.Loss.HasValue ? p.Loss.Value.ToString("F2") + " kg" : "—";
            dpIncDead.Text = p.IncDeadConf ? "✓ TAK — odliczamy padłe i konfiskaty od skupu" : "✗ NIE";

            var flagi = new List<string>();
            if (p.IsDeliverer) flagi.Add("Dostawca");
            if (p.IsCustomer) flagi.Add("Odbiorca");
            if (p.IsRolnik) flagi.Add("Rolnik");
            if (p.IsSkupowy) flagi.Add("Skupowy");
            dpFlagi.Text = flagi.Count == 0 ? "—" : string.Join(", ", flagi);

            dpHalt.Text = p.Halt ? "⏸ WSTRZYMANY (Halt = 1)" : "✓ Aktywny";
            dpHalt.Foreground = p.Halt
                ? new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26))
                : new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69));

            var osob = new List<string>();
            if (!string.IsNullOrWhiteSpace(p.TypOsobowosci)) osob.Add(p.TypOsobowosci);
            if (!string.IsNullOrWhiteSpace(p.TypOsobowosci2)) osob.Add(p.TypOsobowosci2);
            dpOsob.Text = osob.Count == 0 ? "—" : string.Join(" / ", osob);

            dpInfo1.Text = string.IsNullOrEmpty(p.Info1) ? "—" : p.Info1;
            dpInfo2.Text = p.Info2 ?? "";
            dpInfo3.Text = p.Info3 ?? "";
        }

        // ─── Akcje przycisków ─────────────────────────────────────────────────

        private void BtnEdytuj_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string user = App.UserID ?? Environment.UserName;
                string conn = AnalitykaPelna.Services.AnalitykaConfig.ConnLibraNet;
                var wiz = new HodowcaWizardWindow(conn, user, _customerID)
                {
                    Owner = this
                };
                wiz.ShowDialog();
                // Po edycji — przeładuj
                _ = ZaladujAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się otworzyć edytora: " + ex.Message,
                    "Edytuj hodowcę", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnMapa_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var url = $"https://www.google.com/maps/search/{Uri.EscapeDataString(_dane?.Profil.AdresPelny ?? "Polska")}";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się otworzyć mapy: " + ex.Message,
                    "Mapa", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnEksport_Click(object sender, RoutedEventArgs e)
        {
            // Eksport zbiorczy: profil + statystyki w jednym CSV
            if (_dane == null) return;
            var sb = new StringBuilder();
            sb.AppendLine("Sekcja;Pole;Wartość");
            void S(string sek, string pole, object? val) => sb.AppendLine($"{sek};{pole};{val}");

            var p = _dane.Profil;
            S("Profil", "ID", p.ID);
            S("Profil", "Nazwa", p.Name);
            S("Profil", "NIP", p.Nip);
            S("Profil", "REGON", p.Regon);
            S("Profil", "Adres", p.AdresPelny);
            S("Profil", "Telefon", p.KontaktTelefon);
            S("Profil", "Email", p.Email);
            S("Profil", "AnimNo", p.AnimNo);
            S("Profil", "IRZPlus", p.IRZPlus);
            S("Profil", "Halt", p.Halt ? "TAK" : "NIE");

            var s90 = _dane.Stat90Dni;
            S("Statystyki 90 dni", "Liczba partii", s90.LiczbaPartii);
            S("Statystyki 90 dni", "Σ skup kg", s90.SumaSkupKg.ToString("F0", CultureInfo.InvariantCulture));
            S("Statystyki 90 dni", "Σ przyjęto kg", s90.SumaPrzyjetoKg.ToString("F0", CultureInfo.InvariantCulture));
            S("Statystyki 90 dni", "Wydajność %", s90.SrWydajnosc?.ToString("F2", CultureInfo.InvariantCulture));
            S("Statystyki 90 dni", "Klasa B %", s90.SrKlasaB?.ToString("F2", CultureInfo.InvariantCulture));
            S("Statystyki 90 dni", "Ostatnia dostawa", s90.OstatniaDostawa?.ToString("yyyy-MM-dd"));
            S("Statystyki 90 dni", "Średni cykl (dni)", s90.SrCyklDni);
            S("Statystyki 90 dni", "Szacowany obrót zł", s90.SzacowanyObrot?.ToString("F2", CultureInfo.InvariantCulture));
            S("Statystyki 90 dni", "Średni wiek partii (dni)", s90.SrWiekDni?.ToString("F1", CultureInfo.InvariantCulture));
            S("Statystyki 90 dni", "Min/max wiek (dni)", $"{s90.MinWiekDni}/{s90.MaxWiekDni}");
            S("Statystyki 90 dni", "Średnia waga sztuki (kg)", s90.SrWagaSzt?.ToString("F3", CultureInfo.InvariantCulture));
            S("Statystyki 90 dni", "Straty sztuk", s90.SumaStratSzt);
            S("Statystyki 90 dni", "Straty %", s90.StratySztProc?.ToString("F2", CultureInfo.InvariantCulture));
            S("Statystyki 90 dni", "Partii z wstawieniem", s90.LiczbaPartiiZWstawieniem);

            var sAll = _dane.StatCaleZycie;
            S("Statystyki całe życie", "Liczba partii", sAll.LiczbaPartiiZycie);
            S("Statystyki całe życie", "Σ skup kg", sAll.SumaSkupKg.ToString("F0", CultureInfo.InvariantCulture));
            S("Statystyki całe życie", "Pierwsza dostawa", sAll.PierwszaDostawa?.ToString("yyyy-MM-dd"));
            S("Statystyki całe życie", "Wydajność śr. %", sAll.SrWydajnosc?.ToString("F2", CultureInfo.InvariantCulture));

            S("Liczby zbiorcze", "Liczba ferm", _dane.Fermy.Count);
            S("Liczby zbiorcze", "Harmonogram (planowane)", _dane.Harmonogram.Count(h => !h.MaPartie));

            ZapiszCsv($"Karta_hodowcy_{p.ID}_{DateTime.Now:yyyy-MM-dd_HHmm}.csv", sb.ToString());
        }

        private void BtnEksportPartii_Click(object sender, RoutedEventArgs e)
        {
            if (_dane == null || _dane.Partie.Count == 0)
            {
                MessageBox.Show("Brak partii do eksportu.", "Eksport CSV",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var sb = new StringBuilder();
            sb.AppendLine("LP;Partia;DataUboju;Godzina;DataWstawienia;WiekDni;Status;NettoSkup_kg;NettoH_kg;Ubytek_kg;Ubytek_proc;SztDekl;SztPrzyjete;StratySzt;StratyProc;CH;NW;ZM;Konfiskaty;KonfProc;LUMEL;SztWyb;KgWyb;SrWagaSzt_kg;Wydajnosc_proc;KlasaB_proc;Padle;CenaSkup;Wartosc;PiK;Opasienie;TempRampa;PrzyjetoKg;Kierowca;Auto;Naczepa;Auta_harm;VetNo;VetComment");
            foreach (var pa in _dane.Partie)
            {
                sb.AppendLine(string.Join(";",
                    F(pa.LpDostawy),
                    pa.Partia,
                    pa.CreateData.ToString("yyyy-MM-dd"),
                    pa.CreateGodzina ?? "",
                    pa.DataWstawienia?.ToString("yyyy-MM-dd") ?? "",
                    F(pa.WiekDni),
                    pa.IsClose ? "Zamknięta" : "Otwarta",
                    F(pa.NettoSkup), F(pa.NettoH), F(pa.UbytekTransKg), F(pa.UbytekTransProc),
                    F(pa.SztDekl), F(pa.PrzyjetoSzt),
                    F(pa.StratySzt), F(pa.StratySztProc),
                    F(pa.CH), F(pa.NW), F(pa.ZM), F(pa.Konfiskaty), F(pa.KonfiskatyProc),
                    F(pa.LUMEL), F(pa.SztWyb), F(pa.KgWyb),
                    F(pa.SrWagaSzt),
                    F(pa.WydajnoscProc), F(pa.KlasaBProc),
                    F(pa.Padle), F(pa.CenaSkup), F(pa.Wartosc),
                    pa.PiK ? "1" : "0",
                    F(pa.Opasienie),
                    F(pa.TempRampa), F(pa.PrzyjetoKg),
                    pa.Kierowca ?? "", pa.Auto ?? "", pa.Naczepa ?? "",
                    pa.AutaHarm ?? "",
                    pa.VetNo ?? "", (pa.VetComment ?? "").Replace(';', ',').Replace("\r\n", " ").Replace('\n', ' ')));
            }
            ZapiszCsv($"Karta_hodowcy_{_dane.Profil.ID}_partie_{DateTime.Now:yyyy-MM-dd_HHmm}.csv", sb.ToString());
        }

        private static string F(object? v) =>
            v switch
            {
                null => "",
                decimal d => d.ToString("F2", CultureInfo.InvariantCulture),
                double db => db.ToString("F2", CultureInfo.InvariantCulture),
                int i => i.ToString(CultureInfo.InvariantCulture),
                _ => v.ToString() ?? ""
            };

        private void ZapiszCsv(string sugestia, string tresc)
        {
            var dlg = new SaveFileDialog
            {
                FileName = sugestia,
                Filter = "Plik CSV (*.csv)|*.csv",
                DefaultExt = "csv"
            };
            if (dlg.ShowDialog(this) != true) return;
            File.WriteAllText(dlg.FileName, tresc, new UTF8Encoding(true));
            MessageBox.Show($"Zapisano:\n{dlg.FileName}", "Eksport CSV",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e) => Close();
    }
}
