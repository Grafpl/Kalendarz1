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

            // Liczby do wysyłki — z SPEC (FarmerCalc) bo to wiążące rozliczenie
            decimal kg = _specKgRazem;
            decimal wartosc = _specWartoscRazem;
            decimal tony = Math.Round(kg / 1000m, 3);
            decimal cenaKg = kg > 0 ? wartosc / kg : 0;
            decimal cenaTona = Math.Round(cenaKg * 1000m, 2);

            // Sprawdź duplikat
            if (await _zsrirRepo.ExistsForPeriodAsync(_ostatniOd, _ostatniDo, ZsrirKategoria))
            {
                if (MessageBox.Show(
                    $"Już wysłano raport dla okresu {_ostatniOd:dd.MM} – {_ostatniDo:dd.MM.yyyy} (\"{ZsrirKategoria}\").\n\nWysłać mimo to?",
                    "Duplikacja", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
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
                $"Źródło: SPECYFIKACJA PDF (FarmerCalc) — wiążące rozliczenie po uboju.";
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

                // Szukaj kategorii "Brojler" rekurencyjnie w drzewie commodityGroup
                var brojler = ZnajdzGrupeRekurencyjnie(cfg.CommodityGroup, "brojler")
                    ?? throw new Exception("Nie znaleziono kategorii \"Brojler\" w formularzu.");
                commodityGroupId = brojler.Id;

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
                                    [pricePole.Id.ToString()]  = cenaTona,
                                    [amountPole.Id.ToString()] = tony
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
