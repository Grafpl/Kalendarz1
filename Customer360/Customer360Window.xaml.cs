using Kalendarz1.Customer360.Models;
using Kalendarz1.Customer360.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Kalendarz1.Customer360
{
    public partial class Customer360Window : Window
    {
        private readonly Customer360Service _service = new();
        private int? _selectedKlientId;
        private static readonly CultureInfo Pl = new("pl-PL");

        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public Customer360Window() : this(null) { }
        public Customer360Window(int? preselectKlientId)
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
            _selectedKlientId = preselectKlientId;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Preload zdjęć towarów w tle
            _ = Kalendarz1.AnalitykaPelna.Services.TowaryZdjeciaService.LoadAsync(ConnLibra);

            if (_selectedKlientId.HasValue)
            {
                await LoadKlientAsync(_selectedKlientId.Value);
            }
            else
            {
                // Auto-otwórz picker — user nie musi szukać przycisku
                await Dispatcher.BeginInvoke(new Action(() => BtnPickKlient_Click(this, new RoutedEventArgs())));
            }
        }

        // ── Picker klienta — pełnoekranowy dialog ──
        private async void BtnPickKlient_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new KlientPickerDialog { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Selected != null)
            {
                LblPickKlient.Text = dlg.Selected.Nazwa;
                _selectedKlientId = dlg.Selected.Id;
                await LoadKlientAsync(dlg.Selected.Id);
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedKlientId.HasValue) await LoadKlientAsync(_selectedKlientId.Value);
        }

        // ── Główne ładowanie ──
        private async Task LoadKlientAsync(int klientId)
        {
            try
            {
                Cursor = System.Windows.Input.Cursors.Wait;

                // Paralelnie: header + KPI + history + monthly + top towary + faktury detail + weryfikacja + anulowane
                // monthsBack = 0 → wczytaj CAŁĄ historię (wszystkie zamówienia z LibraNet + wszystkie faktury/korekty z HANDEL)
                const int OKRES = 0;  // 0 = od początku, bez limitu dat
                var tHdr = _service.GetKlientHeaderAsync(klientId);
                var tKpi = _service.GetKpiAsync(klientId);
                var tHist = _service.GetOrderHistoryAsync(klientId, OKRES);
                var tMonthly = _service.GetMonthlyStatsAsync(klientId, OKRES);
                var tTop = _service.GetTopTowaryAsync(klientId, OKRES, 5);
                var tFakDet = _service.GetFakturyDetailAsync(klientId, OKRES);
                var tWer = _service.GetWeryfikacjaAsync(klientId, OKRES);
                var tAnul = _service.GetAnulowaneZamowieniaAsync(klientId, OKRES);

                await Task.WhenAll(tHdr, tKpi, tHist, tMonthly, tTop, tFakDet, tWer, tAnul);

                var hdr = await tHdr;
                var kpi = await tKpi;
                var history = await tHist;
                var monthly = await tMonthly;
                var topT = await tTop;
                var fakturyDet = await tFakDet;
                var (werSumma, werTowary) = await tWer;
                var anulowane = await tAnul;

                if (hdr == null)
                {
                    MessageBox.Show(this, "Nie znaleziono klienta.", "Brak", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                RenderHeader(hdr, kpi);
                RenderKpi(kpi);
                RenderMonthlyChart(monthly);

                // Załaduj zdjęcia towarów (BLOB z LibraNet.TowarZdjecia) — instant z cache jeśli wcześniej preloaded
                await Kalendarz1.AnalitykaPelna.Services.TowaryZdjeciaService.LoadAsync(ConnLibra);
                foreach (var t in topT)
                    t.Image = Kalendarz1.AnalitykaPelna.Services.TowaryZdjeciaService.Get(t.KodTowaru);

                GridTopTowary.ItemsSource = topT;
                GridZamowienia.ItemsSource = history;
                GridFakturyDetail.ItemsSource = fakturyDet;

                // Baner diagnostyczny — pokazuje zakres dat faktycznie załadowanych faktur
                if (fakturyDet.Count == 0)
                {
                    FakturyDiag.Text = "⚠ Brak faktur dla tego klienta w HANDEL (sprawdź czy khid = KlientId).";
                }
                else
                {
                    var min = fakturyDet.Min(f => f.DataWystawienia);
                    var max = fakturyDet.Max(f => f.DataWystawienia);
                    int lat = fakturyDet.Select(f => f.DataWystawienia.Year).Distinct().Count();
                    FakturyDiag.Text = $"📅 Załadowano {fakturyDet.Count} faktur+korekt · zakres {min:dd.MM.yyyy} – {max:dd.MM.yyyy} · {lat} lat(a) · BEZ limitu (cała historia)";
                }
                GridWeryfikacja.ItemsSource = werTowary;
                GridAnulowane.ItemsSource = anulowane;

                RenderWeryfikacjaKpi(werSumma);
                RenderAnulowaneHeader(anulowane);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd ładowania: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }

        private void RenderHeader(KlientHeader hdr, KlientKpi kpi)
        {
            HeaderEmpty.Visibility = Visibility.Collapsed;
            HeaderLoaded.Visibility = Visibility.Visible;

            LblNazwa.Text = hdr.Nazwa;
            LblNip.Text = string.IsNullOrWhiteSpace(hdr.NIP) ? "—" : hdr.NIP;
            LblAdres.Text = string.IsNullOrWhiteSpace(hdr.AdresPelny) ? "—" : hdr.AdresPelny;
            LblTelefon.Text = string.IsNullOrWhiteSpace(hdr.Telefon) ? "—" : hdr.Telefon;
            LblEmail.Text = string.IsNullOrWhiteSpace(hdr.Email) ? "—" : hdr.Email;
            LblHandlowiec.Text = string.IsNullOrWhiteSpace(hdr.Handlowiec) ? "(brak)" : hdr.Handlowiec;

            if (!string.IsNullOrWhiteSpace(hdr.Kategoria))
            {
                LblKategoria.Text = "Kat. " + hdr.Kategoria.ToUpper();
                ChipKategoria.Visibility = Visibility.Visible;
                (string bg, string brd, string fg) = hdr.Kategoria.ToUpper() switch
                {
                    "A" => ("#DCFCE7", "#86EFAC", "#15803D"),
                    "B" => ("#DBEAFE", "#93C5FD", "#1E40AF"),
                    "C" => ("#FEF3C7", "#FCD34D", "#92400E"),
                    "D" => ("#FEE2E2", "#FCA5A5", "#991B1B"),
                    _ => ("#F1F5F9", "#CBD5E1", "#475569")
                };
                try
                {
                    var bc = new BrushConverter();
                    ChipKategoria.Background = (Brush)bc.ConvertFromString(bg)!;
                    ChipKategoria.BorderBrush = (Brush)bc.ConvertFromString(brd)!;
                    LblKategoria.Foreground = (Brush)bc.ConvertFromString(fg)!;
                }
                catch { }
            }

            // Churn risk badge
            ChipChurn.Visibility = Visibility.Visible;
            (string icon, string text, string churnBg, string churnBrd, string churnFg) = kpi.ChurnRiskLevel switch
            {
                "OK" => ("✅", "Aktywny", "#DCFCE7", "#86EFAC", "#15803D"),
                "WATCH" => ("👀", "Obserwuj", "#FEF3C7", "#FCD34D", "#92400E"),
                "WARNING" => ("⚠", "Uwaga", "#FED7AA", "#FB923C", "#9A3412"),
                "CRITICAL" => ("🚨", "Krytyczne", "#FEE2E2", "#FCA5A5", "#991B1B"),
                _ => ("❓", "Brak danych", "#F1F5F9", "#CBD5E1", "#475569")
            };
            LblChurnIcon.Text = icon;
            LblChurnLevel.Text = text;
            try
            {
                var bc = new BrushConverter();
                ChipChurn.Background = (Brush)bc.ConvertFromString(churnBg)!;
                ChipChurn.BorderBrush = (Brush)bc.ConvertFromString(churnBrd)!;
                LblChurnLevel.Foreground = (Brush)bc.ConvertFromString(churnFg)!;
            }
            catch { }
            ChipChurn.ToolTip = kpi.ChurnRiskReason;
        }

        private void RenderKpi(KlientKpi kpi)
        {
            KpiObrot.Text = $"{kpi.Obrot12M:N0} zł";

            // YoY
            if (kpi.Obrot12MPrev > 0)
            {
                decimal yoy = (kpi.Obrot12M - kpi.Obrot12MPrev) / kpi.Obrot12MPrev * 100m;
                string arrow = yoy >= 0 ? "▲" : "▼";
                string color = yoy >= 0 ? "#16A34A" : "#DC2626";
                KpiObrotYoY.Text = $"{arrow} {Math.Abs(yoy):N1}% YoY";
                try { KpiObrotYoY.Foreground = (Brush)new BrushConverter().ConvertFromString(color)!; } catch { }
            }
            else
            {
                KpiObrotYoY.Text = "Brak danych YoY";
            }

            KpiMarza.Text = $"{kpi.Marza12M:N0} zł";
            KpiMarzaKg.Text = $"{kpi.SredniaMarzaKg:N2} zł/kg średnio";

            KpiLiczbaZam.Text = kpi.LiczbaZamowien12M.ToString("N0");
            KpiSumaKg.Text = $"{kpi.SumaKg12M:N0} kg łącznie";

            if (kpi.OstatnieZamowienie.HasValue)
            {
                int dni = kpi.DniOdOstatniegoZamowienia;
                KpiOstatnie.Text = $"{dni} dni temu";
                KpiSredniOdstep.Text = $"Norma: co {kpi.SredniCzasMiedzyZamowieniami:N0} dni";
            }
            else
            {
                KpiOstatnie.Text = "Brak";
                KpiSredniOdstep.Text = "—";
            }

            KpiLimit.Text = $"{kpi.LimitKredytowy:N0} zł";
            KpiLimitWyk.Text = kpi.LimitKredytowy > 0 ? $"Wykorzystanie: {kpi.WykorzystanieLimitProc:N0}%" : "—";

            KpiDoZap.Text = $"{kpi.DoZaplaty:N0} zł";
            KpiFaktur.Text = $"{kpi.LiczbaFaktur} faktur";

            KpiPrzeterm.Text = $"{kpi.Przeterminowane:N0} zł";
            KpiMaxDni.Text = kpi.MaxDniOpoznienia > 0 ? $"Max {kpi.MaxDniOpoznienia} dni opóźnienia" : "—";

            KpiReklamacje.Text = kpi.LiczbaReklamacji12M.ToString();
            KpiReklamacjeProc.Text = kpi.LiczbaReklamacji12M > 0
                ? $"{kpi.RelativeReklamacjeProc:N2}% obrotu"
                : "Brak reklamacji ✓";
        }

        private void RenderWeryfikacjaKpi(Kalendarz1.Customer360.Models.WeryfikacjaSumarum s)
        {
            VerKpiZamKg.Text = $"{s.ZamowioneKg:N0} kg";
            VerKpiZamLZ.Text = $"{s.LiczbaZamowien} zamówień";

            VerKpiFakKg.Text = $"{s.ZafakturowaneKg:N0} kg";
            VerKpiFakLF.Text = $"{s.LiczbaFaktur} faktur";

            string znak = s.RoznicaKg >= 0 ? "+" : "";
            VerKpiRoznicaKg.Text = $"{znak}{s.RoznicaKg:N0} kg";
            try
            {
                string color = Math.Abs(s.RoznicaKg) < 5 ? "#15803D"        // bliskie zeru = zielone
                             : s.RoznicaKg < 0 ? "#92400E"                  // ucięte = pomarańczowe
                             : "#1E40AF";                                    // dodane = niebieskie
                VerKpiRoznicaKg.Foreground = (Brush)new BrushConverter().ConvertFromString(color)!;
            }
            catch { }
            VerKpiZgodnoscProc.Text = $"Zgodność {s.ZgodnoscProc:N1}%";

            string znakW = s.RoznicaWartosci >= 0 ? "+" : "";
            VerKpiRoznicaWart.Text = $"{znakW}{s.RoznicaWartosci:N0} zł";
            try
            {
                string color = s.RoznicaWartosci >= 0 ? "#15803D" : "#92400E";
                VerKpiRoznicaWart.Foreground = (Brush)new BrushConverter().ConvertFromString(color)!;
            }
            catch { }
            VerKpiSumaWart.Text = $"Zam {s.ZamowionaWartosc:N0} → Fak {s.ZafakturowanaWartosc:N0}";

            VerKpiZgodne.Text = (s.LiczbaTowarow - s.LiczbaTowarowUcietych - s.LiczbaTowarowDodanych - s.LiczbaTowarowBrakFaktury).ToString();
            VerKpiUciete.Text = s.LiczbaTowarowUcietych.ToString();
            VerKpiDodane.Text = s.LiczbaTowarowDodanych.ToString();
            VerKpiBrakFak.Text = s.LiczbaTowarowBrakFaktury.ToString();

            // Banner diagnostyczny gdy 0 faktur — Sergiusz widzi natychmiast bez zaglądania do logów
            if (s.LiczbaFaktur == 0 && s.LiczbaZamowien > 0)
            {
                VerBannerDiag.Visibility = Visibility.Visible;
                VerDiagTitle.Text = "⚠ Brak faktur w HANDEL dla tego klienta";
                VerDiagText.Text = $"Zamówienia w LibraNet: {s.LiczbaZamowien} ({s.ZamowioneKg:N0} kg), ale w HANDEL.HM.DK nie znaleziono żadnej faktury sprzedaży dla tego klienta w ostatnich 12 mies. Sprawdź: 1) czy KlientId w LibraNet pasuje do HANDEL.khid, 2) czy klient jest fakturowany pod innym kontrahentem (sieć/grupa)? Szczegóły w Visual Studio Output.";
            }
            else if (s.LiczbaFaktur == 0 && s.LiczbaZamowien == 0)
            {
                VerBannerDiag.Visibility = Visibility.Visible;
                VerDiagTitle.Text = "ℹ Brak danych — klient nieaktywny w ostatnich 12 mies";
                VerDiagText.Text = "Brak zamówień i brak faktur. Klient niezamawiał ostatnio.";
            }
            else
            {
                VerBannerDiag.Visibility = Visibility.Collapsed;
            }
        }

        private void RenderAnulowaneHeader(System.Collections.Generic.List<Kalendarz1.Customer360.Models.AnulowaneZam> anul)
        {
            int liczba = anul.Count;
            decimal sumaKg = anul.Sum(a => a.SumaKg);
            decimal sumaWart = anul.Sum(a => a.Wartosc);

            if (liczba == 0)
            {
                AnulHeader.Text = "✅ Brak anulowanych zamówień w ostatnich 12 mies";
                AnulSummary.Text = "Świetna relacja z klientem — nic nie anulowano.";
            }
            else
            {
                AnulHeader.Text = $"❌ {liczba} anulowanych zamówień w ostatnich 12 mies";
                AnulSummary.Text = $"Łącznie {sumaKg:N0} kg / {sumaWart:N0} zł utraconego obrotu. Sprawdź powody.";
            }
        }

        // ── Wykres miesięczny (proste bars w canvas) ──
        private void RenderMonthlyChart(List<MonthlyStats> data)
        {
            ChartMonthly.Children.Clear();
            ChartMonthly.ColumnDefinitions.Clear();

            if (data == null || data.Count == 0)
            {
                ChartMonthly.Children.Add(new TextBlock
                {
                    Text = "Brak danych do wyświetlenia",
                    Foreground = (Brush)new BrushConverter().ConvertFromString("#94A3B8")!,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
                return;
            }

            decimal max = data.Max(d => d.Wartosc);
            if (max == 0) max = 1;

            for (int i = 0; i < data.Count; i++)
                ChartMonthly.ColumnDefinitions.Add(new ColumnDefinition());

            var barBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
            barBrush.Freeze();
            var labelBrush = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
            labelBrush.Freeze();
            var valueBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x40, 0xAF));
            valueBrush.Freeze();

            for (int i = 0; i < data.Count; i++)
            {
                var d = data[i];
                double heightProc = (double)(d.Wartosc / max) * 160;
                if (heightProc < 4) heightProc = 4;

                var stack = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(3, 0, 3, 0)
                };

                // wartość
                stack.Children.Add(new TextBlock
                {
                    Text = $"{d.Wartosc / 1000m:N0}k",
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = valueBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 2)
                });

                // pasek
                stack.Children.Add(new Rectangle
                {
                    Fill = barBrush,
                    Width = 40,
                    Height = heightProc,
                    RadiusX = 4,
                    RadiusY = 4
                });

                // label miesiąca
                stack.Children.Add(new TextBlock
                {
                    Text = d.Label,
                    FontSize = 10,
                    Foreground = labelBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 0)
                });

                Grid.SetColumn(stack, i);
                ChartMonthly.Children.Add(stack);
            }
        }
    }
}
