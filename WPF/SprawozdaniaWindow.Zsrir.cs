using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kalendarz1.ZSRIR.Models;
using Kalendarz1.ZSRIR.Services;
using Kalendarz1.ZSRIR.Views;

namespace Kalendarz1.WPF
{
    // Partial class — integracja ZSRIR (wysyłka raportu do MRiRW) z SprawozdaniaWindow.
    // Wszystko co dotyczy API ZSRIR żyje tutaj, żeby główny xaml.cs nie puchł.
    public partial class SprawozdaniaWindow
    {
        // Kategoria towarowa raportowana (dla Piórkowskich = brojler kurzy).
        // ID kategorii (commodityGroupId) pobierane runtime z GetFormConfiguration.
        private const string ZsrirKategoria = "Kurczęta brojler";

        private readonly ZsrirSubmissionsRepo _zsrirRepo = new();
        private SubmissionRow? _ostatniaWyslka;

        // ============ STATUS / BADGE ============
        // Wywoływane po Loaded i po każdym refresh danych (PobierzAsync).
        private async Task AktualizujZsrirStatusAsync()
        {
            var secrets = ZsrirSecretsManager.Load();

            // Stan 1: nie skonfigurowane
            if (!secrets.IsConfigured)
            {
                SetZsrirStatus("Amber", "Nieskonfigurowane",
                    "Kliknij ⚙ Konfiguracja żeby podać login/hasło API");
                btnZsrirSend.IsEnabled = false;
                return;
            }

            // Stan 2: skonfigurowane ale brak dostawcy/formularza
            if (secrets.DataSupplierId == null || secrets.FormId == null)
            {
                SetZsrirStatus("Amber", "Wybierz dostawcę / formularz",
                    "Konfiguracja → 'Pobierz dostawców i formularze z API'");
                btnZsrirSend.IsEnabled = false;
                return;
            }

            // Stan 3: sprawdź czy bieżący okres został wysłany
            try
            {
                if (_ostatniOd != default && _ostatniDo != default)
                {
                    var recent = await _zsrirRepo.GetRecentAsync(20);
                    _ostatniaWyslka = recent
                        .FirstOrDefault(r => r.OkresOd.Date == _ostatniOd.Date
                                          && r.OkresDo.Date == _ostatniDo.Date
                                          && r.KategoriaTowaru == ZsrirKategoria);

                    if (_ostatniaWyslka != null && (_ostatniaWyslka.Status == "Sent" || _ostatniaWyslka.Status == "Zero"))
                    {
                        string when = _ostatniaWyslka.WyslanyDataCzas?.ToString("dd.MM.yyyy HH:mm") ?? "?";
                        string kto = _ostatniaWyslka.WyslanyPrzezImie ?? "nieznany";
                        SetZsrirStatus("Green", $"✓ Wysłane · {when}", $"Przez: {kto}");
                        btnZsrirSend.IsEnabled = true;
                        return;
                    }
                    if (_ostatniaWyslka?.Status == "Failed")
                    {
                        SetZsrirStatus("Red", "❌ Ostatnia próba: błąd",
                            _ostatniaWyslka.ErrorMessage ?? "Sprawdź historię i wyślij ponownie");
                        btnZsrirSend.IsEnabled = true;
                        return;
                    }
                }

                // Stan 4: skonfigurowane, brak wysyłki dla tego okresu
                SetZsrirStatus("Blue", "Gotowe do wysłania", "Kliknij 'Wyślij do MRiRW' żeby zaraportować");
                btnZsrirSend.IsEnabled = true;
            }
            catch (Exception ex)
            {
                SetZsrirStatus("Red", "Błąd sprawdzania statusu", ex.Message);
            }
        }

        private void SetZsrirStatus(string color, string title, string hint)
        {
            zsrirStatusTitle.Text = title;
            zsrirStatusHint.Text = hint;
            (Color bg, Color border, Color dot) = color switch
            {
                "Green" => (Hex("#E8F5E9"), Hex("#2E7D32"), Hex("#2E7D32")),
                "Blue"  => (Hex("#E3F2FD"), Hex("#1565C0"), Hex("#1565C0")),
                "Amber" => (Hex("#FFF4E0"), Hex("#E89614"), Hex("#E89614")),
                "Red"   => (Hex("#FDEAEA"), Hex("#D93B3B"), Hex("#D93B3B")),
                _       => (Hex("#F5F7FA"), Hex("#D7DBE0"), Hex("#8B95A0"))
            };
            zsrirStatusBorder.Background = new SolidColorBrush(bg);
            zsrirStatusBorder.BorderBrush = new SolidColorBrush(border);
            zsrirStatusDot.Fill = new SolidColorBrush(dot);
        }
        private static Color Hex(string s) => (Color)ColorConverter.ConvertFromString(s);

        // Rekurencyjnie szuka grupy towarowej zawierającej tekst w nazwie (case-insensitive).
        // Najpierw szuka w bieżącej grupie, potem rekurencyjnie w podgrupach (commodityGroups).
        private static CommodityGroup? ZnajdzGrupeRekurencyjnie(CommodityGroup? root, string keyword)
        {
            if (root == null) return null;
            if ((root.Name ?? "").IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                return root;
            foreach (var sub in root.CommodityGroups)
            {
                var found = ZnajdzGrupeRekurencyjnie(sub, keyword);
                if (found != null) return found;
            }
            return null;
        }

        // Szuka grupy kurczak/kurczęta brojler. KRYTYCZNE: samo "brojler" pasuje też do "gęsi typu brojler" w drzewie ZSRIR,
        // co w przeszłości spowodowało wysyłkę raportu w złej kategorii (gęsi zamiast kurczaków).
        // Priorytet: zawiera "kurcz" (kurczak/kurczęta) AND "brojler"; fallback "kurcz" sam; potem dowolny "brojler" z logiem.
        private static CommodityGroup? ZnajdzKurczakaBrojlera(CommodityGroup? root, out string trace)
        {
            trace = "";
            if (root == null) return null;
            var wszystkie = new List<CommodityGroup>();
            Spłaszcz(root, wszystkie);

            CommodityGroup? Pasuje(Func<string, bool> pred) =>
                wszystkie.FirstOrDefault(g => pred((g.Name ?? "").ToLowerInvariant()));

            var k1 = Pasuje(n => n.Contains("kurcz") && n.Contains("brojler"));
            if (k1 != null) { trace = $"PRIO1 kurcz+brojler → '{k1.Name}' (id={k1.Id})"; return k1; }

            var k2 = Pasuje(n => n.Contains("kurcz"));
            if (k2 != null) { trace = $"PRIO2 kurcz → '{k2.Name}' (id={k2.Id})"; return k2; }

            var k3 = Pasuje(n => n.Contains("brojler"));
            if (k3 != null) { trace = $"FALLBACK brojler (UWAGA — może być gęś!) → '{k3.Name}' (id={k3.Id})"; return k3; }

            return null;
        }
        private static void Spłaszcz(CommodityGroup g, List<CommodityGroup> output)
        {
            output.Add(g);
            foreach (var sub in g.CommodityGroups) Spłaszcz(sub, output);
        }

        // ============ BUTTON HANDLERS ============
        private async void BtnZsrirSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ZsrirSettingsDialog { Owner = this };
            if (dlg.ShowDialog() == true) await AktualizujZsrirStatusAsync();
        }

        private async void BtnZsrirTest_Click(object sender, RoutedEventArgs e)
        {
            var secrets = ZsrirSecretsManager.Load();
            if (!secrets.IsConfigured)
            {
                MessageBox.Show("Skonfiguruj login/hasło w ⚙ Konfiguracja.", "ZSRIR",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            loadingOverlay.Visibility = Visibility.Visible;
            try
            {
                using var api = new ZsrirApiClient(secrets);
                var (ok, msg) = await api.TestConnectionAsync();
                MessageBox.Show((ok ? "✓ " : "✗ ") + msg, ok ? "ZSRIR — test OK" : "ZSRIR — błąd",
                    MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Error);
            }
            finally { loadingOverlay.Visibility = Visibility.Collapsed; }
        }

        private void BtnZsrirHistory_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ZsrirHistoryDialog { Owner = this };
            dlg.ShowDialog();
        }

        private void BtnZsrirDebug_Click(object sender, RoutedEventArgs e)
        {
            if (!_hasData)
            {
                MessageBox.Show("Najpierw pobierz dane (przycisk Pobierz lub F5).", "Brak danych",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            // Źródło danych do ZSRIR: HANDEL (Symfonia) — kg i wartość z faktur 7+8 razem.
            decimal handelKg = _kg7 + _kg8;
            decimal handelWart = _suma7 + _suma8;
            var secrets = ZsrirSecretsManager.Load();
            var dlg = new ZsrirDebugDialog(_ostatniOd, _ostatniDo, handelKg, handelWart, ZsrirKategoria, secrets)
            {
                Owner = this
            };
            dlg.ShowDialog();
        }

        private async void BtnZsrirSend_Click(object sender, RoutedEventArgs e)
        {
            if (!_hasData)
            {
                MessageBox.Show("Najpierw pobierz dane (przycisk Pobierz lub F5).", "Brak danych",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var secrets = ZsrirSecretsManager.Load();
            if (!secrets.IsConfigured || secrets.DataSupplierId == null || secrets.FormId == null)
            {
                MessageBox.Show("Brak konfiguracji ZSRIR. Otwórz ⚙ Konfiguracja i pobierz dostawcę/formularz z API.",
                    "ZSRIR", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Liczby do wysyłki — z HANDEL (Symfonia): kg z faktur, wartość z faktur (typy 7+8 razem).
            decimal kg = _kg7 + _kg8;
            decimal wartosc = _suma7 + _suma8;
            decimal tony = Math.Round(kg / 1000m, 3);
            decimal cenaKg = kg > 0 ? wartosc / kg : 0;
            decimal cenaTona = Math.Round(cenaKg * 1000m, 2);

            // Sprawdź duplikat — jeśli istnieje wysłany wpis, retry NADPISZE go (UPSERT).
            if (await _zsrirRepo.ExistsForPeriodAsync(_ostatniOd, _ostatniDo, ZsrirKategoria))
            {
                if (MessageBox.Show(
                    $"Raport dla okresu {_ostatniOd:dd.MM} – {_ostatniDo:dd.MM.yyyy} (\"{ZsrirKategoria}\") został już wysłany.\n\nWysłać ponownie? Poprzedni wpis w historii zostanie nadpisany.",
                    "Ponowna wysyłka", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;
            }

            // Confirm z podsumowaniem
            string podsumowanie =
                $"Wysłać do ZSRIR następujące dane?\n\n" +
                $"Kategoria:   {ZsrirKategoria}\n" +
                $"Okres:       {_ostatniOd:dd.MM.yyyy} – {_ostatniDo:dd.MM.yyyy}\n" +
                $"Ilość:       {tony:N3} ton  ({kg:N0} kg)\n" +
                $"Wartość:     {wartosc:N2} zł netto\n" +
                $"Cena:        {cenaTona:N2} zł/tona  (= {cenaKg:N2} zł/kg)\n\n" +
                $"Źródło: HANDEL (Sage Symfonia) — faktury skupu (kg + zł netto).";
            if (MessageBox.Show(podsumowanie, "Potwierdzenie wysyłki do ZSRIR",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            loadingOverlay.Visibility = Visibility.Visible;
            string? response = null, errorMsg = null;
            string status = "Pending";
            int? commodityGroupId = null;
            int? formReportingPeriodId = null;

            try
            {
                using var api = new ZsrirApiClient(secrets);

                // Znajdź okres sprawozdawczy (zgodnie z dokumentacją §4.3: dateFrom/dateTo)
                var periods = await api.GetReportingPeriodsAsync(secrets.FormId!.Value);
                var openPeriod = periods
                    .FirstOrDefault(p => p.DateFrom.Date == _ostatniOd && p.DateTo.Date == _ostatniDo)
                    ?? periods.FirstOrDefault(p => p.IsOpen && p.DateFrom.Date == _ostatniOd);
                if (openPeriod == null)
                    throw new Exception($"Brak otwartego okresu sprawozdawczego pn–niedz dla {_ostatniOd:dd.MM} – {_ostatniDo:dd.MM.yyyy}");
                formReportingPeriodId = openPeriod.Id;

                // Pobierz konfigurację (§4.4: commodityGroup może być zagnieżdżona rekurencyjnie + formFields na root)
                var cfg = await api.GetFormConfigurationAsync(secrets.FormId!.Value)
                    ?? throw new Exception("Pobranie konfiguracji formularza nieudane.");

                // Szukaj kategorii kurczak/kurczęta brojler — NIE samo "brojler" (pasuje też do gęsi typu brojler!).
                var brojler = ZnajdzKurczakaBrojlera(cfg.CommodityGroup, out string traceKat)
                    ?? throw new Exception("Nie znaleziono kategorii \"Kurczęta/Kurczak brojler\" w formularzu.");
                commodityGroupId = brojler.Id;
                System.Diagnostics.Debug.WriteLine("[ZSRIR] " + traceKat);

                // Zbierz pola: najpierw z konkretnej grupy, potem z root (formFields), znajdź typy Price + Amount
                var wszystkiePola = new List<FormField>();
                wszystkiePola.AddRange(brojler.FormFields);
                wszystkiePola.AddRange(cfg.FormFields);
                var pricePole  = wszystkiePola.FirstOrDefault(f => string.Equals(f.Type, "Price",  StringComparison.OrdinalIgnoreCase))
                    ?? throw new Exception("Nie znaleziono pola typu 'Price' (cena) w formularzu.");
                var amountPole = wszystkiePola.FirstOrDefault(f => string.Equals(f.Type, "Amount", StringComparison.OrdinalIgnoreCase))
                    ?? throw new Exception("Nie znaleziono pola typu 'Amount' (ilość) w formularzu.");

                if (tony <= 0)
                {
                    response = await api.AddFormZeroAsync(new AddFormZeroRequest
                    {
                        FormReportingPeriodId = openPeriod.Id,
                        DataSupplierId = secrets.DataSupplierId!.Value
                    });
                    status = "Zero";
                }
                else
                {
                    // KLUCZ w formFieldsValues = ID pola jako string (§4.6 dokumentacji)
                    response = await api.AddFormAsync(new AddFormRequest
                    {
                        FormReportingPeriodId = openPeriod.Id,
                        DataSupplierId = secrets.DataSupplierId!.Value,
                        Forms = new()
                        {
                            new FormPayload
                            {
                                CommodityGroupId = brojler.Id,
                                FormFieldsValues = new()
                                {
                                    // ZSRIR akceptuje TYLKO PL format (przecinek dziesiętny) — InvariantCulture (kropka)
                                    // jest odrzucany jako "niepoprawna wartość liczbowa".
                                    [pricePole.Id.ToString()]  = cenaTona.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("pl-PL")),
                                    [amountPole.Id.ToString()] = tony.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("pl-PL"))
                                }
                            }
                        }
                    });
                    status = "Sent";
                }
            }
            catch (ZsrirApiException ex) { errorMsg = ex.Message; response = ex.RawBody; status = "Failed"; }
            catch (Exception ex) { errorMsg = ex.Message; status = "Failed"; }
            finally { loadingOverlay.Visibility = Visibility.Collapsed; }

            // Zapisz historię
            int? userId = int.TryParse(App.UserID, out int u) ? u : (int?)null;
            await _zsrirRepo.InsertAsync(new SubmissionRow
            {
                OkresOd = _ostatniOd,
                OkresDo = _ostatniDo,
                KategoriaTowaru = ZsrirKategoria,
                CommodityGroupId = commodityGroupId,
                KgRazem = Math.Round(kg, 2),
                TonyRazem = tony,
                WartoscNetto = Math.Round(wartosc, 2),
                CenaZlTona = cenaTona,
                FormReportingPeriodId = formReportingPeriodId,
                DataSupplierId = secrets.DataSupplierId,
                Status = status,
                ApiResponse = response,
                ErrorMessage = errorMsg,
                WyslanyPrzez = userId,
                WyslanyDataCzas = DateTime.Now
            });

            await AktualizujZsrirStatusAsync();

            if (status == "Sent" || status == "Zero")
                MessageBox.Show($"✓ Raport wysłany do ZSRIR (status: {status}).",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show($"✗ Błąd wysyłki:\n{errorMsg}",
                    "Niepowodzenie", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
