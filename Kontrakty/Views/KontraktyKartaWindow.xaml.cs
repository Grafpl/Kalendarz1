using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kalendarz1.Kontrakty.Models;
using Kalendarz1.Kontrakty.Services;

namespace Kalendarz1.Kontrakty.Views
{
    /// <summary>
    /// Karta kontraktu: nagłówek + bieżące warunki + oś wersji + akcje
    /// (przedłuż, edytuj wersję, aktywuj, zmień status, dodaj skan, otwórz plik).
    /// </summary>
    public partial class KontraktyKartaWindow : Window
    {
        private readonly KontraktyService _svc = new();
        private readonly WordTemplateService _word = new();
        private readonly int _kontraktId;
        private KontraktDetail? _header;
        private List<KontraktWersja> _wersje = new();
        private KontraktWersja? _sel;
        private KontraktWersja? _biezaca;

        private readonly bool _autoGenerujWord;

        public KontraktyKartaWindow(int kontraktId) : this(kontraktId, false) { }

        public KontraktyKartaWindow(int kontraktId, bool autoGenerujWord)
        {
            InitializeComponent();
            _kontraktId = kontraktId;
            _autoGenerujWord = autoGenerujWord;
            Loaded += async (_, _) =>
            {
                await ZaladujAsync();
                if (_autoGenerujWord) await GenerujWordAsync();
            };
        }

        private async System.Threading.Tasks.Task ZaladujAsync()
        {
            _header = await _svc.GetDetailAsync(_kontraktId);
            _wersje = await _svc.GetWersjeAsync(_kontraktId);
            _biezaca = _wersje.FirstOrDefault(w => w.IsAktualna) ?? _wersje.FirstOrDefault();

            if (_header != null)
            {
                txtNumer.Text = $"📜 Kontrakt {_header.NumerKontraktu} · {_header.TypLabel}";
                txtHodowca.Text = $"{_header.NazwaHodowcySnapshot}  ·  NIP {_header.NipSnapshot}  ·  " +
                                  $"gosp. {_header.NrGospodarstwaSnapshot}  ·  {_header.PodmiotLabel}" +
                                  (_header.LiczySieDoArimr ? "  ·  ARiMR" : "");
            }

            if (_biezaca != null)
            {
                txtStatus.Text = _biezaca.StatusLabel;
                chipStatus.Background = StatusBrush(_biezaca.Status);

                tileWarunki.Text = string.IsNullOrWhiteSpace(_biezaca.WarunkiLabel) ? "—" : _biezaca.WarunkiLabel;
                tileOkres.Text = _biezaca.OkresLabel;
                tileOkresSub.Text = $"wypowiedzenie {_biezaca.OkresWypowiedzeniaDni} dni" +
                    (_biezaca.DataPodpisania.HasValue ? $"  ·  podpis {_biezaca.DataPodpisania:dd.MM.yyyy}" : "");
                txtBiezaceKlauzule.Text = string.IsNullOrWhiteSpace(_biezaca.KlauzuleSzczegolne)
                    ? "" : "Klauzule: " + _biezaca.KlauzuleSzczegolne;

                // kafelek terminu + kolor pilności
                if (_biezaca.ObowiazujeDo is { } d)
                {
                    int dni = (d - DateTime.Today).Days;
                    txtWygasa.Text = dni < 0 ? $"wygasł {-dni} dni temu" : $"wygasa za {dni} dni";
                    tileTermin.Text = dni < 0 ? $"wygasł {-dni} dni temu" : $"za {dni} dni";
                    tileTerminSub.Text = "do " + d.ToString("dd.MM.yyyy");
                    (string tlo, string fg) = dni < 0 ? ("#FEE2E2", "#991B1B")
                        : dni <= 30 ? ("#FEE2E2", "#991B1B")
                        : dni <= 90 ? ("#FEF3C7", "#92400E")
                        : ("#DCFCE7", "#166534");
                    boxTermin.Background = Brush(tlo);
                    tileTermin.Foreground = Brush(fg);
                }
                else
                {
                    txtWygasa.Text = "bezterminowy";
                    tileTermin.Text = "bezterminowy";
                    tileTerminSub.Text = "";
                    boxTermin.Background = Brush("#F1F5F9");
                    tileTermin.Foreground = Brush("#0F172A");
                }
                BudujPliki();
            }
            else
            {
                txtStatus.Text = "—";
                tileWarunki.Text = "Brak wersji.";
                tileOkres.Text = "—"; tileOkresSub.Text = "";
                tileTermin.Text = "—"; tileTerminSub.Text = "";
            }

            dgWersje.ItemsSource = null;
            dgWersje.ItemsSource = _wersje;
            icTimeline.ItemsSource = null;
            icTimeline.ItemsSource = _wersje;
            if (_biezaca != null) dgWersje.SelectedItem = _biezaca;
            _sel = _biezaca;
            dgZalaczniki.ItemsSource = await _svc.GetZalacznikiAsync(_kontraktId);

            // transformacja (9.2)
            var tr = await _svc.GetTransformacjaAsync(_kontraktId);
            foreach (var it in cbTransform.Items.OfType<ComboBoxItem>())
                if ((it.Tag?.ToString() ?? "") == (tr?.Decyzja ?? "NIEOKRESLONE")) { cbTransform.SelectedItem = it; break; }
            txtTransformUzas.Text = tr?.Uzasadnienie ?? "";
            txtTransformInfo.Text = tr?.DataDecyzji is { } dd
                ? $"Ostatnia decyzja: {tr!.DecyzjaLabel} • {dd:dd.MM.yyyy} • {tr.UserId}" : "";

            // audyt (8.3)
            var audyt = await _svc.GetAuditLogAsync(_kontraktId);
            dgAudyt.ItemsSource = audyt;
            txtAudytPusto.Visibility = audyt.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            OdswiezPrzyciski();
        }

        private async void BtnZapiszTransform_Click(object sender, RoutedEventArgs e)
        {
            var t = new TransformacjaDecyzja
            {
                KontraktId = _kontraktId,
                Decyzja = (cbTransform.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "NIEOKRESLONE",
                Uzasadnienie = string.IsNullOrWhiteSpace(txtTransformUzas.Text) ? null : txtTransformUzas.Text.Trim()
            };
            try
            {
                await _svc.SaveTransformacjaAsync(t, Kalendarz1.App.UserID ?? "");
                await ZaladujAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się zapisać decyzji: " + ex.Message, "Transformacja",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BudujPliki()
        {
            panelPliki.Children.Clear();
            if (_biezaca == null) return;
            if (!string.IsNullOrWhiteSpace(_biezaca.SciezkaPdfSkan))
                panelPliki.Children.Add(Chip("📎 skan PDF", "#DBEAFE", "#1E40AF"));
            if (!string.IsNullOrWhiteSpace(_biezaca.SciezkaWord))
                panelPliki.Children.Add(Chip("📄 Word", "#DCFCE7", "#166534"));
            if (panelPliki.Children.Count == 0)
                panelPliki.Children.Add(Chip("brak podpiętych plików", "#F1F5F9", "#64748B"));
        }

        private static Border Chip(string text, string bg, string fg) => new()
        {
            Background = Brush(bg),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(10, 3, 10, 3),
            Margin = new Thickness(0, 0, 8, 0),
            Child = new TextBlock { Text = text, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = Brush(fg) }
        };

        private void TimelineItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is KontraktWersja w)
            {
                dgWersje.SelectedItem = w;
                dgWersje.ScrollIntoView(w);
            }
        }

        private void DgWersje_SelChanged(object sender, SelectionChangedEventArgs e)
        {
            _sel = dgWersje.SelectedItem as KontraktWersja;
            OdswiezPrzyciski();
        }

        private void OdswiezPrzyciski()
        {
            btnPrzedluz.IsEnabled = _header != null;
            btnEdytuj.IsEnabled = _sel is { Edytowalna: true };
            btnAktywuj.IsEnabled = _sel is { IsAktualna: false };
            btnStatus.IsEnabled = _sel != null;
        }

        // ── Akcje ────────────────────────────────────────────────────────────
        private async void BtnPrzedluz_Click(object sender, RoutedEventArgs e)
        {
            var w = new KontraktyEditorWindow(EditorMode.Przedluzenie, _kontraktId) { Owner = this };
            if (w.ShowDialog() == true) await ZaladujAsync();
        }

        private async void BtnEdytuj_Click(object sender, RoutedEventArgs e)
        {
            if (_sel == null) return;
            if (!_sel.Edytowalna)
            {
                MessageBox.Show("Edytować można tylko wersję w stanie szkic / w negocjacji.", "Karta kontraktu",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var w = new KontraktyEditorWindow(EditorMode.Edycja, _kontraktId, _sel.Id) { Owner = this };
            if (w.ShowDialog() == true) await ZaladujAsync();
        }

        private async void BtnAktywuj_Click(object sender, RoutedEventArgs e)
        {
            if (_sel == null) return;
            if (MessageBox.Show($"Ustawić wersję {_sel.NrLabel} jako bieżącą (aktywną)? " +
                "Poprzednia aktywna wersja zostanie oznaczona jako zastąpiona.",
                "Aktywacja wersji", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            try
            {
                await _svc.ActivateWersjaAsync(_kontraktId, _sel.Id, Kalendarz1.App.UserID);
                await ZaladujAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd aktywacji: " + ex.Message, "Karta kontraktu",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnStatus_Click(object sender, RoutedEventArgs e)
        {
            if (_sel == null) return;
            string status = (cbNowyStatus.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
            if (string.IsNullOrEmpty(status))
            {
                MessageBox.Show("Wybierz status z listy.", "Karta kontraktu",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                await _svc.ChangeStatusWersjaAsync(_sel.Id, status, Kalendarz1.App.UserID);
                await ZaladujAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zmiany statusu: " + ex.Message, "Karta kontraktu",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDodajZal_Click(object sender, RoutedEventArgs e)
        {
            if (_header == null) return;
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Wybierz plik (PDF)",
                Filter = "PDF (*.pdf)|*.pdf|Wszystkie pliki (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;
            string typ = (cbTypZal.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "INNE";
            try
            {
                string docel = await ZalacznikiHelper.UploadAsync(_svc, _kontraktId, _biezaca?.Id,
                    _header.NumerKontraktu, _header.Rok, typ, dlg.FileName, Kalendarz1.App.UserID ?? "");
                if (typ == "SKAN_PODPISANY" && _biezaca != null)
                    await _svc.SetSciezkiAsync(_biezaca.Id, null, docel);
                await ZaladujAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd dodawania załącznika: " + ex.Message, "Karta kontraktu",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnPodglad_Click(object sender, RoutedEventArgs e)
        {
            if (_header == null || _biezaca == null)
            {
                MessageBox.Show("Brak wersji do podglądu.", "Podgląd", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string? szablon = await _svc.GetTemplatePathAsync(_header.TypKontraktu);
            if (string.IsNullOrWhiteSpace(szablon) || !File.Exists(szablon))
            {
                MessageBox.Show("Brak/niedostępny szablon Word dla typu „" + _header.TypLabel + "”.", "Podgląd",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string temp = Path.Combine(Path.GetTempPath(), $"podglad_{Guid.NewGuid():N}.docx");
            try
            {
                var cykle = await _svc.GetHarmonogramAsync(_biezaca.Id);
                var tokeny = WordTemplateService.BuildKontraktacjaTokens(_header, _biezaca, _header.NumerKontraktu);
                _word.GenerujKontraktacja(szablon!, temp, tokeny, cykle);
                new KontraktPodgladWindow(temp, $"Podgląd — {_header.NumerKontraktu}") { Owner = this }.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się przygotować podglądu: " + ex.Message, "Podgląd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
        }

        private async void BtnGenerujWord_Click(object sender, RoutedEventArgs e) => await GenerujWordAsync();

        /// <summary>Generuje dokument Word z aktualnej wersji (reuse z menu listy + przycisku karty).</summary>
        public async System.Threading.Tasks.Task GenerujWordAsync()
        {
            if (_header == null || _biezaca == null)
            {
                MessageBox.Show("Brak wersji do wygenerowania.", "Karta kontraktu",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                string? szablon = await _svc.GetTemplatePathAsync(_header.TypKontraktu);
                if (string.IsNullOrWhiteSpace(szablon) || !File.Exists(szablon))
                {
                    MessageBox.Show("Brak/niedostępny szablon Word dla typu „" + _header.TypLabel +
                        "”. Umieść .docx w _SZABLON\\ i uruchom AddBookmark.", "Generator Word",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var cykle = await _svc.GetHarmonogramAsync(_biezaca.Id);
                var tokeny = WordTemplateService.BuildKontraktacjaTokens(_header, _biezaca, _header.NumerKontraktu);

                string folder = Path.Combine(ZalacznikiHelper.Root, _header.Rok.ToString());
                string nazwa = $"Umowa_{ZalacznikiHelper.SanitizeNumer(_header.NumerKontraktu)}_{Bezpieczne(_header.NazwaHodowcySnapshot ?? "hodowca")}.docx";
                string output = Path.Combine(folder, nazwa);

                _word.GenerujKontraktacja(szablon!, output, tokeny, cykle);
                await _svc.SetSciezkiAsync(_biezaca.Id, output, null);
                try { Process.Start(new ProcessStartInfo(output) { UseShellExecute = true }); } catch { }
                await ZaladujAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się wygenerować Word: " + ex.Message, "Generator Word",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string Bezpieczne(string s)
        { foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s.Replace('/', '-').Trim(); }

        private void BtnOtworzZal_Click(object sender, RoutedEventArgs e)
            => OtworzZal(dgZalaczniki.SelectedItem as KontraktZalacznik);

        private void DgZalaczniki_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => OtworzZal(dgZalaczniki.SelectedItem as KontraktZalacznik);

        private void OtworzZal(KontraktZalacznik? z)
        {
            if (z == null)
            {
                MessageBox.Show("Zaznacz załącznik na liście.", "Karta kontraktu",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                if (File.Exists(z.SciezkaUnc))
                    Process.Start(new ProcessStartInfo(z.SciezkaUnc) { UseShellExecute = true });
                else
                    MessageBox.Show("Plik nie istnieje:\n" + z.SciezkaUnc, "Karta kontraktu",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się otworzyć pliku: " + ex.Message, "Karta kontraktu",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ── Kolory ───────────────────────────────────────────────────────────
        private static Brush StatusBrush(string? status) => status switch
        {
            "ACTIVE" or "SIGNED" => Brush("#DCFCE7"),
            "EXPIRING" => Brush("#FEF3C7"),
            "NEGOCJACJE" or "SENT" => Brush("#E0E7FF"),
            "EXPIRED" or "TERMINATED" => Brush("#FEE2E2"),
            _ => Brush("#E2E8F0")
        };

        private static Brush Brush(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
    }
}
