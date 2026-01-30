using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ClosedXML.Excel;
using Microsoft.Win32;
using Newtonsoft.Json;
using Kalendarz1.Kartoteka.Models;
using Kalendarz1.Kartoteka.Services;

namespace Kalendarz1.Kartoteka.Views
{
    public partial class KartotekaOdbiorcowWindow : Window
    {
        private KartotekaService _service;
        private string _userId;
        private string _userName;

        private readonly string _libraNetConn = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _handelConn = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        public string UserID
        {
            get => _userId;
            set => _userId = value;
        }

        public KartotekaOdbiorcowWindow()
        {
            InitializeComponent();
            _userId = App.UserID ?? "11111";
            _userName = App.UserFullName ?? "Administrator";
        }

        public KartotekaOdbiorcowWindow(string userId, string userName) : this()
        {
            _userId = userId;
            _userName = userName;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _service = new KartotekaService(_libraNetConn, _handelConn);

            // Konfiguracja admin
            bool isAdmin = _userId == "11111";
            if (isAdmin)
            {
                ComboBoxHandlowiec.Visibility = Visibility.Visible;
                ColumnHandlowiec.Visibility = Visibility.Visible;
                TextBlockHandlowiec.Text = "Administrator - wszystkie dane";
            }
            else
            {
                TextBlockHandlowiec.Text = $"Handlowiec: {_userName}";
            }

            LoadColumnWidths();
            await LoadData();
        }

        private async System.Threading.Tasks.Task LoadData()
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;

                string handlowiec = null;
                bool pokazWszystkich = false;

                if (_userId == "11111")
                {
                    var selected = ComboBoxHandlowiec.SelectedItem as ComboBoxItem;
                    var selectedText = selected?.Content?.ToString();
                    if (string.IsNullOrEmpty(selectedText) || selectedText == "Wszyscy")
                    {
                        pokazWszystkich = true;
                    }
                    else
                    {
                        handlowiec = selectedText;
                    }
                }
                else
                {
                    handlowiec = _userName;
                }

                await _service.EnsureTablesExistAsync();

                var odbiorcy = await _service.PobierzOdbiorcowAsync(handlowiec, pokazWszystkich);
                await _service.WczytajDaneWlasneAsync(odbiorcy);

                // Pokaż dane natychmiast (bez asortymentu)
                DataGridOdbiorcy.ItemsSource = odbiorcy;
                _allOdbiorcy = odbiorcy;

                // Załaduj handlowców dla admina
                if (_userId == "11111" && ComboBoxHandlowiec.Items.Count <= 1)
                {
                    ComboBoxHandlowiec.Items.Clear();
                    ComboBoxHandlowiec.Items.Add(new ComboBoxItem { Content = "Wszyscy", IsSelected = true });
                    var handlowcy = await _service.PobierzHandlowcowAsync();
                    foreach (var h in handlowcy)
                    {
                        ComboBoxHandlowiec.Items.Add(new ComboBoxItem { Content = h });
                    }
                }

                UpdateStatystyki();
                UpdateLicznik();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }

            // Ładuj asortyment w tle (nie blokuje UI)
            _ = LoadAsortymentInBackground();
        }

        private async System.Threading.Tasks.Task LoadAsortymentInBackground()
        {
            try
            {
                if (_allOdbiorcy == null || _allOdbiorcy.Count == 0) return;

                var khids = _allOdbiorcy.Select(o => o.IdSymfonia).ToList();
                var asortyment = await _service.PobierzAsortymentAsync(khids, 6);

                // Przypisz asortyment do odbiorców
                foreach (var odbiorca in _allOdbiorcy)
                {
                    if (asortyment.TryGetValue(odbiorca.IdSymfonia, out var lista))
                    {
                        odbiorca.Asortyment = string.Join(", ", lista);
                    }
                }

                // Odśwież DataGrid aby pokazać nowe dane asortymentu
                DataGridOdbiorcy.Items.Refresh();
            }
            catch
            {
                // Asortyment w tle - nie pokazuj błędu użytkownikowi
            }
        }

        private List<OdbiorcaHandlowca> _allOdbiorcy = new();

        private void ApplyFilters()
        {
            if (_allOdbiorcy == null) return;

            var filtered = _allOdbiorcy.AsEnumerable();

            // Filtr tekstu
            var szukaj = TextBoxSzukaj.Text?.ToLower()?.Trim();
            if (!string.IsNullOrEmpty(szukaj))
            {
                filtered = filtered.Where(o =>
                    (o.NazwaFirmy?.ToLower().Contains(szukaj) ?? false) ||
                    (o.Miasto?.ToLower().Contains(szukaj) ?? false) ||
                    (o.NIP?.Contains(szukaj) ?? false) ||
                    (o.OsobaKontaktowa?.ToLower().Contains(szukaj) ?? false) ||
                    (o.TelefonKontakt?.Contains(szukaj) ?? false));
            }

            // Filtr kategorii
            if (ComboBoxKategoria.SelectedIndex > 0)
            {
                var kat = (ComboBoxKategoria.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (!string.IsNullOrEmpty(kat))
                    filtered = filtered.Where(o => o.KategoriaHandlowca == kat);
            }

            // Filtr alertów
            if (CheckBoxAlerty.IsChecked == true)
            {
                filtered = filtered.Where(o => o.AlertType != "None");
            }

            var result = filtered.ToList();
            DataGridOdbiorcy.ItemsSource = result;
            UpdateLicznik();
            UpdateStatystyki();
        }

        private void UpdateStatystyki()
        {
            var items = DataGridOdbiorcy.ItemsSource as IEnumerable<OdbiorcaHandlowca>;
            if (items == null) return;

            var list = items.ToList();
            TextStopkaSumaLimitow.Text = list.Sum(o => o.LimitKupiecki).ToString("N0");
            TextStopkaWykorzystano.Text = list.Sum(o => o.WykorzystanoLimit).ToString("N0");
            TextStopkaWolne.Text = list.Sum(o => o.WolnyLimit).ToString("N0");
            TextStopkaPrzeterminowane.Text = list.Sum(o => o.KwotaPrzeterminowana).ToString("N0");
        }

        private void UpdateLicznik()
        {
            var count = (DataGridOdbiorcy.ItemsSource as IEnumerable<OdbiorcaHandlowca>)?.Count() ?? 0;
            TextBlockLicznik.Text = $"Wyświetlono: {count} z {_allOdbiorcy?.Count ?? 0} odbiorców";
        }

        // ═══════════════════════════════════════════
        // SZCZEGÓŁY - aktualizacja paneli
        // ═══════════════════════════════════════════

        private async void DataGridOdbiorcy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var odbiorca = DataGridOdbiorcy.SelectedItem as OdbiorcaHandlowca;
            if (odbiorca == null) return;

            await LoadSzczegoly(odbiorca);
        }

        private async System.Threading.Tasks.Task LoadSzczegoly(OdbiorcaHandlowca odbiorca)
        {
            // Kontakty
            try
            {
                var kontakty = await _service.PobierzKontaktyAsync(odbiorca.IdSymfonia);
                WypelnijKontakty(kontakty, odbiorca.IdSymfonia);
            }
            catch { }

            // Finanse
            try
            {
                var faktury = await _service.PobierzFakturyAsync(odbiorca.IdSymfonia);
                WypelnijFinanse(odbiorca, faktury);
                DataGridFaktury.ItemsSource = faktury;
                DataGridHistoria.ItemsSource = faktury;
            }
            catch { }

            // Asortyment - preferencje
            TextAsortymentPakowanie.Text = string.IsNullOrEmpty(odbiorca.PreferencjePakowania) ? "-" : odbiorca.PreferencjePakowania;
            TextAsortymentJakosc.Text = string.IsNullOrEmpty(odbiorca.PreferencjeJakosci) ? "-" : odbiorca.PreferencjeJakosci;
            TextAsortymentLista.Text = string.IsNullOrEmpty(odbiorca.Asortyment) ? "-" : odbiorca.Asortyment;
            TextAsortymentDni.Text = string.IsNullOrEmpty(odbiorca.PreferowanyDzienDostawy) ? "-" : odbiorca.PreferowanyDzienDostawy;
            TextAsortymentGodzina.Text = string.IsNullOrEmpty(odbiorca.PreferowanaGodzinaDostawy) ? "-" : odbiorca.PreferowanaGodzinaDostawy;
            TextAsortymentAdres.Text = string.IsNullOrEmpty(odbiorca.AdresDostawyInny) ? "= adres główny" : odbiorca.AdresDostawyInny;
            TextAsortymentTrasa.Text = string.IsNullOrEmpty(odbiorca.Trasa) ? "-" : odbiorca.Trasa;

            // Asortyment - szczegóły z historii sprzedaży
            try
            {
                var asortymentSzczegoly = await _service.PobierzAsortymentSzczegolyAsync(odbiorca.IdSymfonia, 12);
                DataGridAsortyment.ItemsSource = asortymentSzczegoly;

                // Podsumowanie
                TextAsortymentIloscProduktow.Text = asortymentSzczegoly.Count.ToString();
                var sumaKg = asortymentSzczegoly.Sum(a => a.SumaKg);
                var sumaWartosc = asortymentSzczegoly.Sum(a => a.SumaWartosc);
                TextAsortymentSumaKg.Text = sumaKg.ToString("N0");
                TextAsortymentSumaWartosc.Text = $"{sumaWartosc:N0} zł";
                TextAsortymentSredniaCena.Text = sumaKg > 0
                    ? $"{(sumaWartosc / sumaKg):N2} zł"
                    : "-";
            }
            catch
            {
                DataGridAsortyment.ItemsSource = null;
                TextAsortymentIloscProduktow.Text = "0";
                TextAsortymentSumaKg.Text = "0";
                TextAsortymentSumaWartosc.Text = "0 zł";
                TextAsortymentSredniaCena.Text = "-";
            }

            // Notatki
            TextBoxNotatki.Text = odbiorca.Notatki ?? "";

            // Dostawy
            TextDostawyDni.Text = $"Preferowane dni: {(string.IsNullOrEmpty(odbiorca.PreferowanyDzienDostawy) ? "-" : odbiorca.PreferowanyDzienDostawy)}";
            TextDostawyGodzina.Text = $"Godzina: {(string.IsNullOrEmpty(odbiorca.PreferowanaGodzinaDostawy) ? "-" : odbiorca.PreferowanaGodzinaDostawy)}";
            TextDostawyAdres.Text = $"Adres: {(string.IsNullOrEmpty(odbiorca.AdresDostawyInny) ? "= adres główny" : odbiorca.AdresDostawyInny)}";
            TextDostawyTrasa.Text = $"Trasa: {(string.IsNullOrEmpty(odbiorca.Trasa) ? "-" : odbiorca.Trasa)}";
            TextDostawyFirma.Text = odbiorca.NazwaFirmy;
            TextDostawyUlica.Text = odbiorca.Ulica ?? "-";
            TextDostawyMiasto.Text = $"{odbiorca.KodPocztowy} {odbiorca.Miasto}";
            TextDostawyNIP.Text = $"NIP: {odbiorca.NIP ?? "-"}";
        }

        private void WypelnijKontakty(List<KontaktOdbiorcy> kontakty, int idSymfonia)
        {
            WrapPanelKontakty.Children.Clear();

            foreach (var k in kontakty)
            {
                var card = CreateKontaktCard(k);
                WrapPanelKontakty.Children.Add(card);
            }

            // Przycisk "Dodaj kontakt"
            var addBtn = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(134, 239, 172)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(4),
                Width = 180,
                Cursor = Cursors.Hand
            };
            var addText = new TextBlock
            {
                Text = "+ Dodaj kontakt",
                Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold
            };
            addBtn.Child = addText;
            addBtn.MouseLeftButtonDown += (s, e) =>
            {
                var dialog = new KontaktEdycjaWindow(new KontaktOdbiorcy { IdSymfonia = idSymfonia });
                if (dialog.ShowDialog() == true)
                {
                    _ = ReloadKontakty(idSymfonia);
                }
            };
            WrapPanelKontakty.Children.Add(addBtn);
        }

        private Border CreateKontaktCard(KontaktOdbiorcy kontakt)
        {
            var card = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 252, 231)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(4),
                Width = 180,
                Cursor = Cursors.Hand
            };
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                ShadowDepth = 0,
                Color = Colors.Black,
                Opacity = 0.05,
                BlurRadius = 8
            };

            var stack = new StackPanel();

            // Typ kontaktu
            var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            headerStack.Children.Add(new TextBlock { Text = kontakt.IkonaTypu, FontSize = 16 });
            headerStack.Children.Add(new TextBlock
            {
                Text = kontakt.TypKontaktu,
                Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                FontSize = 11,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            stack.Children.Add(headerStack);

            // Imię i nazwisko
            stack.Children.Add(new TextBlock { Text = kontakt.PelneNazwisko, FontWeight = FontWeights.Bold });

            // Stanowisko
            if (!string.IsNullOrEmpty(kontakt.Stanowisko))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = kontakt.Stanowisko,
                    Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                    FontSize = 11
                });
            }

            // Telefon
            if (!string.IsNullOrEmpty(kontakt.Telefon))
            {
                var telBlock = new TextBlock { Margin = new Thickness(0, 8, 0, 0), FontSize = 12 };
                var telLink = new Hyperlink();
                telLink.Inlines.Add($"📞 {kontakt.Telefon}");
                telLink.Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                telLink.TextDecorations = null;
                telLink.Click += (s, e) =>
                {
                    try { Process.Start(new ProcessStartInfo { FileName = $"tel:{kontakt.Telefon}", UseShellExecute = true }); } catch { }
                };
                telBlock.Inlines.Add(telLink);
                stack.Children.Add(telBlock);
            }

            // Email
            if (!string.IsNullOrEmpty(kontakt.Email))
            {
                var emailBlock = new TextBlock { FontSize = 12 };
                var emailLink = new Hyperlink();
                emailLink.Inlines.Add($"📧 {kontakt.Email}");
                emailLink.Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                emailLink.TextDecorations = null;
                emailLink.Click += (s, e) =>
                {
                    try { Process.Start(new ProcessStartInfo { FileName = $"mailto:{kontakt.Email}", UseShellExecute = true }); } catch { }
                };
                emailBlock.Inlines.Add(emailLink);
                stack.Children.Add(emailBlock);
            }

            card.Child = stack;

            // Dwuklik na kartę = edycja
            card.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    var dialog = new KontaktEdycjaWindow(kontakt);
                    if (dialog.ShowDialog() == true)
                    {
                        _ = ReloadKontakty(kontakt.IdSymfonia);
                    }
                }
            };

            // Context menu
            card.ContextMenu = new ContextMenu();
            var editItem = new MenuItem { Header = "📝 Edytuj" };
            editItem.Click += (s, e) =>
            {
                var dialog = new KontaktEdycjaWindow(kontakt);
                if (dialog.ShowDialog() == true)
                    _ = ReloadKontakty(kontakt.IdSymfonia);
            };
            var deleteItem = new MenuItem { Header = "🗑️ Usuń" };
            deleteItem.Click += async (s, e) =>
            {
                if (MessageBox.Show($"Usunąć kontakt {kontakt.PelneNazwisko}?", "Potwierdź", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    await _service.UsunKontaktAsync(kontakt.Id);
                    await ReloadKontakty(kontakt.IdSymfonia);
                }
            };
            card.ContextMenu.Items.Add(editItem);
            card.ContextMenu.Items.Add(deleteItem);

            return card;
        }

        private async System.Threading.Tasks.Task ReloadKontakty(int idSymfonia)
        {
            var kontakty = await _service.PobierzKontaktyAsync(idSymfonia);
            WypelnijKontakty(kontakty, idSymfonia);
        }

        private void WypelnijFinanse(OdbiorcaHandlowca odbiorca, List<FakturaOdbiorcy> faktury)
        {
            TextFinanseLimitKwota.Text = $"{odbiorca.LimitKupiecki:N0} zł";
            var procent = odbiorca.ProcentWykorzystania;
            ProgressFinanseLimit.Value = Math.Min(procent, 100);
            if (procent > 100)
                ProgressFinanseLimit.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            else if (procent > 80)
                ProgressFinanseLimit.Foreground = new SolidColorBrush(Color.FromRgb(234, 179, 8));
            else
                ProgressFinanseLimit.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));

            TextFinanseWolne.Text = $"Wolne: {odbiorca.WolnyLimit:N0} zł ({procent:F0}% wykorzystane)";
            TextFinansePlatnosc.Text = $"{odbiorca.FormaPlatnosci} {odbiorca.TerminPlatnosci} dni";

            var doZaplaty = faktury.Where(f => !f.Anulowany && f.DoZaplaty > 0).Sum(f => f.DoZaplaty);
            TextFinanseDoZaplaty.Text = $"Do zapłaty: {doZaplaty:N0} zł";
            TextFinanseDoZaplaty.Foreground = doZaplaty > 0
                ? new SolidColorBrush(Color.FromRgb(202, 138, 4))
                : new SolidColorBrush(Color.FromRgb(22, 163, 74));

            TextFinansePrzeteminowane.Text = $"Przeterminowane: {odbiorca.KwotaPrzeterminowana:N0} zł";
            TextFinansePrzeteminowane.Foreground = odbiorca.KwotaPrzeterminowana > 0
                ? new SolidColorBrush(Color.FromRgb(220, 38, 38))
                : new SolidColorBrush(Color.FromRgb(22, 163, 74));

            var obrot = faktury.Where(f => !f.Anulowany).Sum(f => f.Brutto);
            TextFinanseObrot.Text = $"{obrot:N0} zł";
            TextFinanseFakturCount.Text = $"Faktur: {faktury.Count(f => !f.Anulowany)}";
        }

        // ═══════════════════════════════════════════
        // EVENTS - Filtry
        // ═══════════════════════════════════════════

        private void TextBoxSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ComboBoxKategoria_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) ApplyFilters();
        }

        private async void ComboBoxHandlowiec_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) await LoadData();
        }

        private void CheckBoxAlerty_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) ApplyFilters();
        }

        // ═══════════════════════════════════════════
        // EVENTS - Przyciski
        // ═══════════════════════════════════════════

        private async void ButtonOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await LoadData();
        }

        private void ButtonExcel_Click(object sender, RoutedEventArgs e)
        {
            ExportToExcel();
        }

        private async void ButtonZapiszNotatki_Click(object sender, RoutedEventArgs e)
        {
            var odbiorca = DataGridOdbiorcy.SelectedItem as OdbiorcaHandlowca;
            if (odbiorca == null) return;

            try
            {
                odbiorca.Notatki = TextBoxNotatki.Text;
                await _service.ZapiszDaneWlasneAsync(odbiorca, _userName);
                MessageBox.Show("Notatki zapisane.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═══════════════════════════════════════════
        // EVENTS - DataGrid
        // ═══════════════════════════════════════════

        private void DataGridOdbiorcy_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OtworzEdycje();
        }

        private void OtworzEdycje()
        {
            var odbiorca = DataGridOdbiorcy.SelectedItem as OdbiorcaHandlowca;
            if (odbiorca == null) return;

            var edycja = new OdbiorcaEdycjaWindow(odbiorca, _service, _userName);
            if (edycja.ShowDialog() == true)
            {
                _ = LoadData();
            }
        }

        // ═══════════════════════════════════════════
        // EVENTS - Menu kontekstowe
        // ═══════════════════════════════════════════

        private void MenuEdytuj_Click(object sender, RoutedEventArgs e) => OtworzEdycje();

        private void MenuZadzwon_Click(object sender, RoutedEventArgs e)
        {
            var odbiorca = DataGridOdbiorcy.SelectedItem as OdbiorcaHandlowca;
            if (odbiorca != null && !string.IsNullOrEmpty(odbiorca.TelefonKontakt))
            {
                try { Process.Start(new ProcessStartInfo { FileName = $"tel:{odbiorca.TelefonKontakt}", UseShellExecute = true }); } catch { }
            }
        }

        private void MenuEmail_Click(object sender, RoutedEventArgs e)
        {
            var odbiorca = DataGridOdbiorcy.SelectedItem as OdbiorcaHandlowca;
            if (odbiorca != null && !string.IsNullOrEmpty(odbiorca.EmailKontakt))
            {
                try { Process.Start(new ProcessStartInfo { FileName = $"mailto:{odbiorca.EmailKontakt}", UseShellExecute = true }); } catch { }
            }
        }

        private void MenuKopiuj_Click(object sender, RoutedEventArgs e)
        {
            var odbiorca = DataGridOdbiorcy.SelectedItem as OdbiorcaHandlowca;
            if (odbiorca == null) return;

            var text = $"{odbiorca.NazwaFirmy}\n{odbiorca.Ulica}\n{odbiorca.KodPocztowy} {odbiorca.Miasto}\nNIP: {odbiorca.NIP}\nTel: {odbiorca.TelefonKontakt}\nEmail: {odbiorca.EmailKontakt}";
            Clipboard.SetText(text);
        }

        // ═══════════════════════════════════════════
        // EVENTS - Hyperlinks
        // ═══════════════════════════════════════════

        private void Hyperlink_Telefon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Hyperlink hyperlink && hyperlink.NavigateUri != null)
            {
                try { Process.Start(new ProcessStartInfo { FileName = hyperlink.NavigateUri.ToString(), UseShellExecute = true }); } catch { }
            }
        }

        private void Hyperlink_Email_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Hyperlink hyperlink && hyperlink.NavigateUri != null)
            {
                try { Process.Start(new ProcessStartInfo { FileName = hyperlink.NavigateUri.ToString(), UseShellExecute = true }); } catch { }
            }
        }

        // ═══════════════════════════════════════════
        // EVENTS - Skróty klawiszowe
        // ═══════════════════════════════════════════

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                _ = LoadData();
                e.Handled = true;
            }
            else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                TextBoxSzukaj.Focus();
                TextBoxSzukaj.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && DataGridOdbiorcy.SelectedItem != null)
            {
                OtworzEdycje();
                e.Handled = true;
            }
        }

        // ═══════════════════════════════════════════
        // EVENTS - Zakładki
        // ═══════════════════════════════════════════

        private void TabControlSzczegoly_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Placeholder for lazy-loading tab content
        }

        // ═══════════════════════════════════════════
        // Export Excel
        // ═══════════════════════════════════════════

        private void ExportToExcel()
        {
            var items = DataGridOdbiorcy.ItemsSource as IEnumerable<OdbiorcaHandlowca>;
            if (items == null || !items.Any())
            {
                MessageBox.Show("Brak danych do eksportu.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx",
                FileName = $"Kartoteka_Odbiorcow_{DateTime.Now:yyyy-MM-dd}.xlsx"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    using var workbook = new XLWorkbook();
                    var ws = workbook.Worksheets.Add("Odbiorcy");

                    // Nagłówki
                    var headers = new[] { "Firma", "Miasto", "NIP", "Kontakt", "Telefon", "Email",
                        "Limit kupiecki", "Wykorzystano", "%", "Kategoria", "Handlowiec",
                        "Forma płatności", "Termin płatności", "Asortyment", "Trasa" };
                    for (int i = 0; i < headers.Length; i++)
                        ws.Cell(1, i + 1).Value = headers[i];

                    int row = 2;
                    foreach (var o in items)
                    {
                        ws.Cell(row, 1).Value = o.NazwaFirmy;
                        ws.Cell(row, 2).Value = o.Miasto;
                        ws.Cell(row, 3).Value = o.NIP;
                        ws.Cell(row, 4).Value = o.OsobaKontaktowa;
                        ws.Cell(row, 5).Value = o.TelefonKontakt;
                        ws.Cell(row, 6).Value = o.EmailKontakt;
                        ws.Cell(row, 7).Value = (double)o.LimitKupiecki;
                        ws.Cell(row, 8).Value = (double)o.WykorzystanoLimit;
                        ws.Cell(row, 9).Value = o.ProcentWykorzystania;
                        ws.Cell(row, 10).Value = o.KategoriaHandlowca;
                        ws.Cell(row, 11).Value = o.Handlowiec;
                        ws.Cell(row, 12).Value = o.FormaPlatnosci;
                        ws.Cell(row, 13).Value = o.TerminPlatnosci;
                        ws.Cell(row, 14).Value = o.Asortyment;
                        ws.Cell(row, 15).Value = o.Trasa;
                        row++;
                    }

                    // Formatowanie
                    ws.Row(1).Style.Font.Bold = true;
                    ws.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#DCFCE7");
                    ws.Columns().AdjustToContents();

                    // Format kolumn numerycznych
                    ws.Column(7).Style.NumberFormat.Format = "#,##0";
                    ws.Column(8).Style.NumberFormat.Format = "#,##0";
                    ws.Column(9).Style.NumberFormat.Format = "0%";

                    workbook.SaveAs(saveDialog.FileName);

                    MessageBox.Show("Wyeksportowano pomyślnie!", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd eksportu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ═══════════════════════════════════════════
        // Zapamiętywanie szerokości kolumn
        // ═══════════════════════════════════════════

        private static string ColumnWidthsFilePath =>
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ZPSP", "KartotekaColumnWidths.json");

        private void SaveColumnWidths()
        {
            try
            {
                var widths = new Dictionary<string, double>();
                foreach (var col in DataGridOdbiorcy.Columns)
                {
                    if (col.Header != null)
                        widths[col.Header.ToString()] = col.ActualWidth;
                }
                var dir = System.IO.Path.GetDirectoryName(ColumnWidthsFilePath);
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(ColumnWidthsFilePath, JsonConvert.SerializeObject(widths));
            }
            catch { }
        }

        private void LoadColumnWidths()
        {
            try
            {
                if (!System.IO.File.Exists(ColumnWidthsFilePath)) return;
                var json = System.IO.File.ReadAllText(ColumnWidthsFilePath);
                if (string.IsNullOrEmpty(json)) return;

                var widths = JsonConvert.DeserializeObject<Dictionary<string, double>>(json);
                foreach (var col in DataGridOdbiorcy.Columns)
                {
                    if (col.Header != null && widths.ContainsKey(col.Header.ToString()))
                        col.Width = new DataGridLength(widths[col.Header.ToString()]);
                }
            }
            catch { }
        }

        // ═══════════════════════════════════════════
        // DEBUG / PROFILER
        // ═══════════════════════════════════════════

        private async void ButtonDebug_Click(object sender, RoutedEventArgs e)
        {
            ButtonDebug.IsEnabled = false;
            ButtonDebug.Content = "⏳ Test...";

            try
            {
                var sw = new System.Diagnostics.Stopwatch();
                var log = new System.Text.StringBuilder();
                log.AppendLine("══════════════════════════════════════════");
                log.AppendLine("  KARTOTEKA ODBIORCÓW - PROFILER DEBUG");
                log.AppendLine($"  Data: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                log.AppendLine($"  User: {_userName} (ID: {_userId})");
                log.AppendLine("══════════════════════════════════════════");
                log.AppendLine();

                var totalSw = System.Diagnostics.Stopwatch.StartNew();

                // 1. EnsureTablesExist
                sw.Restart();
                await _service.EnsureTablesExistAsync();
                sw.Stop();
                log.AppendLine($"[1] EnsureTablesExist:     {sw.ElapsedMilliseconds,6} ms");

                // 2. PobierzOdbiorcow (główne zapytanie + limity + przeterminowane)
                sw.Restart();
                var odbiorcy = await _service.PobierzOdbiorcowAsync(null, true);
                sw.Stop();
                log.AppendLine($"[2] PobierzOdbiorcow:      {sw.ElapsedMilliseconds,6} ms  ({odbiorcy.Count} rekordów)");

                // 3. WczytajDaneWlasne (LibraNet)
                sw.Restart();
                await _service.WczytajDaneWlasneAsync(odbiorcy);
                sw.Stop();
                log.AppendLine($"[3] WczytajDaneWlasne:     {sw.ElapsedMilliseconds,6} ms");

                // 4. PobierzAsortyment (bulk DK→DP→TW)
                sw.Restart();
                var khids = odbiorcy.Select(o => o.IdSymfonia).ToList();
                var asortyment = await _service.PobierzAsortymentAsync(khids, 6);
                sw.Stop();
                log.AppendLine($"[4] PobierzAsortyment:     {sw.ElapsedMilliseconds,6} ms  ({asortyment.Count} z asortymentem)");

                // 5. PobierzHandlowcow
                sw.Restart();
                var handlowcy = await _service.PobierzHandlowcowAsync();
                sw.Stop();
                log.AppendLine($"[5] PobierzHandlowcow:     {sw.ElapsedMilliseconds,6} ms  ({handlowcy.Count} handlowców)");

                // 6. PobierzKontakty (dla pierwszego odbiorcy)
                if (odbiorcy.Count > 0)
                {
                    var testId = odbiorcy[0].IdSymfonia;
                    sw.Restart();
                    var kontakty = await _service.PobierzKontaktyAsync(testId);
                    sw.Stop();
                    log.AppendLine($"[6] PobierzKontakty:       {sw.ElapsedMilliseconds,6} ms  (test: {odbiorcy[0].NazwaFirmy}, {kontakty.Count} kontaktów)");

                    // 7. PobierzFaktury
                    sw.Restart();
                    var faktury = await _service.PobierzFakturyAsync(testId);
                    sw.Stop();
                    log.AppendLine($"[7] PobierzFaktury:        {sw.ElapsedMilliseconds,6} ms  ({faktury.Count} faktur)");

                    // 8. PobierzAsortymentSzczegoly
                    sw.Restart();
                    var asSzczeg = await _service.PobierzAsortymentSzczegolyAsync(testId, 12);
                    sw.Stop();
                    log.AppendLine($"[8] AsortymentSzczegoly:   {sw.ElapsedMilliseconds,6} ms  ({asSzczeg.Count} produktów)");
                }

                totalSw.Stop();
                log.AppendLine();
                log.AppendLine($"══════════════════════════════════════════");
                log.AppendLine($"  TOTAL:                   {totalSw.ElapsedMilliseconds,6} ms");
                log.AppendLine($"══════════════════════════════════════════");

                // Pokaż wynik i skopiuj do schowka
                var result = log.ToString();
                Clipboard.SetText(result);
                MessageBox.Show(result + "\n\n(Skopiowano do schowka)", "Debug Profiler",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd debuggera:\n{ex.Message}\n\n{ex.StackTrace}", "Debug Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ButtonDebug.IsEnabled = true;
                ButtonDebug.Content = "🐛 Debug";
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            SaveColumnWidths();
        }
    }
}
