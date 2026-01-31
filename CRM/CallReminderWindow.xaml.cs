using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using Kalendarz1.CRM.Models;
using Kalendarz1.CRM.Services;
using Kalendarz1.CRM.Dialogs;

namespace Kalendarz1.CRM
{
    public class CallPhase
    {
        public string Icon { get; set; }
        public string Name { get; set; }
        public string Script { get; set; }
        public string Tip { get; set; }
    }

    public partial class CallReminderWindow : Window
    {
        private readonly string _connectionString;
        private readonly string _userID;
        private readonly CallReminderConfig _config;
        private ObservableCollection<ContactToCall> _contacts;
        private ContactToCall _selectedContact;
        private int _reminderLogID;
        private int _callsCount = 0;
        private int _notesCount = 0;
        private int _statusChangesCount = 0;

        private int _currentPhase = 0;
        private readonly List<CallPhase> _phases = new List<CallPhase>
        {
            new CallPhase
            {
                Icon = "👋",
                Name = "Wstęp",
                Script = "\"Dzień dobry, [imię] z [firma]. Czy rozmawiam z osobą odpowiedzialną za zakupy?\"",
                Tip = "💡 Mów pewnie i wyraźnie. Pierwsze 10 sekund decyduje o rozmowie."
            },
            new CallPhase
            {
                Icon = "⏱️",
                Name = "Czas",
                Script = "\"Czy ma Pan/Pani minutę na krótką rozmowę o naszej ofercie? Nie zajmę więcej niż 2 minuty.\"",
                Tip = "💡 Szanuj czas rozmówcy. Jeśli jest zajęty, zapytaj kiedy oddzwonić."
            },
            new CallPhase
            {
                Icon = "💎",
                Name = "Oferta",
                Script = "\"Dostarczamy [produkt/usługę] dopasowaną do Państwa potrzeb. Elastyczne warunki, konkurencyjne ceny. Z kim obecnie współpracujecie?\"",
                Tip = "💡 Słuchaj uważnie i notuj. Zadawaj pytania otwarte zamiast zamkniętych."
            },
            new CallPhase
            {
                Icon = "🎯",
                Name = "Zamknięcie",
                Script = "\"Przygotuję ofertę dopasowaną do Państwa potrzeb. Czy mogę przesłać ją mailem? Jaki adres?\"",
                Tip = "💡 Ustal konkretny następny krok. Zawsze zakończ z planem działania."
            }
        };

        private static readonly string[] Statuses = new[]
        {
            "Nowy", "W trakcie", "Gorący", "Oferta wysłana", "Negocjacje",
            "Zgoda na dalszy kontakt", "Nie zainteresowany", "Zamknięty"
        };

        private int _currentTipIndex = 0;
        private static readonly string[] ColdCallTips = new[]
        {
            // Otwarcie rozmowy
            "Uśmiechnij się przed podniesieniem słuchawki - rozmówca to usłyszy w Twoim głosie!",
            "Pierwsze 10 sekund decyduje o rozmowie. Mów z energią i pewnością siebie.",
            "\"Dzwonię, bo widzę że Państwo zajmujecie się...\" - pokaż, że odrobiliśmy lekcje.",
            "Zacznij od wartości: \"Pomagamy firmom takim jak Państwa zaoszczędzić...\"",
            "Zamiast \"Czy mogę zaproponować...\" powiedz \"Chciałbym podzielić się rozwiązaniem...\"",
            "Przedstaw się krótko i konkretnie - max 15 sekund na wstęp, potem pytanie.",
            "\"Dzień dobry, nie zajmę więcej niż 2 minuty\" - buduje szacunek do czasu klienta.",
            "Zacznij od pytania: \"Czy to dobry moment na krótką rozmowę?\" - daje klientowi kontrolę.",

            // Techniki sprzedaży
            "Nie sprzedawaj od razu - najpierw zapytaj, czym się firma zajmuje i co ich boli.",
            "Cel cold call to NIE sprzedaż, a umówienie spotkania lub wysłanie oferty.",
            "Słuchaj 70%, mów 30%. Im więcej klient mówi, tym bliżej jesteś zamknięcia.",
            "Notuj słowa kluczowe klienta i powtarzaj je - poczuje się wysłuchany.",
            "\"Inne firmy z Państwa branży zauważyły, że...\" - social proof działa najlepiej.",
            "Zadawaj pytania otwarte: \"Jak obecnie rozwiązujecie...?\" zamiast \"Czy potrzebujecie...?\"",
            "Stosuj metodę SPIN: Sytuacja, Problem, Implikacja, Naprowadzenie na rozwiązanie.",
            "Mów językiem korzyści, nie cech. Nie \"mamy system X\" ale \"dzięki temu zaoszczędzicie Y\".",
            "\"Co by się zmieniło, gdybyście mogli...?\" - pozwól klientowi sam zobaczyć wartość.",
            "Używaj konkretnych liczb: \"firmy oszczędzają średnio 30%\" brzmi lepiej niż \"dużo\".",

            // Radzenie sobie z odmową
            "Po usłyszeniu \"nie\" zapytaj: \"Rozumiem, a gdybyśmy mogli...?\" - otwierasz nową drogę.",
            "Po odmowie zawsze zakończ pozytywnie: \"Dziękuję za czas, życzę miłego dnia!\"",
            "\"Nie jestem zainteresowany\" często znaczy \"nie teraz\". Zapytaj o lepszy termin.",
            "Statystycznie potrzebujesz 5-8 prób kontaktu. Nie poddawaj się po pierwszej!",
            "Odmowa to nie porażka - to informacja. Zapisz powód i wróć z lepszym podejściem.",
            "\"Rozumiem, wiele osób na początku tak reagowało, a potem...\" - normalizuj obawy.",
            "Jeśli klient jest zajęty, zapytaj: \"Kiedy mogę zadzwonić w lepszym momencie?\"",
            "\"Nie\" na cold call to \"nie\" dla oferty, nie dla Ciebie osobiście. Nie bierz do siebie.",

            // Timing i organizacja
            "Dzwoń w najlepszych godzinach: 10:00-11:30 i 14:00-16:00. Unikaj poniedziałku rano.",
            "Rób przerwy co 45 minut - Twoja energia wpływa na jakość rozmów.",
            "Przygotuj 2-3 pytania otwarte zanim zadzwonisz. Bądź ciekawy, nie nachalny.",
            "Prowadź tracker wyników - zobaczenie postępów motywuje do dalszej pracy!",
            "Wtorek i środa to statystycznie najlepsze dni na cold calling.",
            "Blokuj czas na dzwonienie - np. 2h rano bez przerw. Rytm buduje pewność.",
            "Po każdych 10 telefonach zrób krótką analizę: co działało, co poprawić?",
            "Przygotuj skrypt, ale nie czytaj z kartki. Znaj kluczowe punkty na pamięć.",

            // Follow-up
            "Jeśli klient mówi \"wyślij maila\" - uzgodnij konkretny termin follow-up.",
            "Follow-up w ciągu 24h po rozmowie podwaja szanse na zamknięcie.",
            "W mailu po rozmowie odwołaj się do konkretnych słów klienta - pokaż, że słuchałeś.",
            "Ustaw przypomnienie o follow-up od razu po rozmowie - nie odkładaj na później.",
            "\"Jak rozmawialiśmy w ubiegłym tygodniu...\" - kontynuacja buduje relację.",

            // Głos i komunikacja
            "Mów powoli i wyraźnie. Szybka mowa = nerwowość = brak zaufania.",
            "Stój podczas rozmowy - Twój głos będzie bardziej energiczny i pewny.",
            "Moduluj głos - monotonny ton usypia. Podkreślaj kluczowe słowa intonacją.",
            "Rób pauzy po ważnych zdaniach - daj klientowi czas na przemyślenie.",
            "Używaj imienia klienta (ale nie za często) - personalizuje rozmowę.",

            // Obiekcje cenowe
            "Najlepsza odpowiedź na \"ile to kosztuje?\" to pytanie: \"Co jest dla Państwa najważniejsze?\"",
            "\"To za drogo\" - odpowiedz: \"W porównaniu do czego?\" Poznaj punkt odniesienia.",
            "Nie dawaj rabatu od razu. Najpierw pokaż wartość, potem rozmawiaj o cenie.",
            "\"Jaki budżet Państwo przewidujecie?\" - pozwól klientowi określić ramy.",

            // Motywacja
            "Każde \"nie\" przybliża Cię do \"tak\". Średnio 1 na 10 rozmów kończy się sukcesem.",
            "Wyobraź sobie sukces przed podniesieniem słuchawki - pozytywna wizualizacja działa.",
            "Porównuj się z sobą z zeszłego tygodnia, nie z innymi. Liczy się Twój progres.",
            "Świętuj małe sukcesy: dobra rozmowa, nowy kontakt, wysłana oferta - wszystko się liczy!",
            "Najlepsi handlowcy to nie ci, co się nie boją, ale ci co dzwonią mimo strachu."
        };

        public CallReminderWindow(string connectionString, string userID, CallReminderConfig config)
        {
            InitializeComponent();

            _connectionString = connectionString;
            _userID = userID;
            _config = config;

            _contacts = new ObservableCollection<ContactToCall>();

            // Enable window dragging
            this.MouseLeftButtonDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                    this.DragMove();
            };

            LoadContacts();
            InitializeStatusButtons();
            ShowRandomTip();
            UpdateFlowPanel();
        }

        private void LoadContacts()
        {
            var contacts = CallReminderService.Instance.GetRandomContacts(_config?.ContactsPerReminder ?? 5);

            if (contacts.Count == 0)
            {
                MessageBox.Show("Brak kontaktów do wyświetlenia.\nWszystkie kontakty zostały już dziś obsłużone lub nie ma kontaktów spełniających kryteria.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
                return;
            }

            _contacts.Clear();
            foreach (var contact in contacts)
            {
                _contacts.Add(contact);
            }

            contactsList.ItemsSource = _contacts;
            txtContactsCount.Text = $"Kontakty ({_contacts.Count})";
            UpdateProgress();

            // Create reminder log
            _reminderLogID = CallReminderService.Instance.CreateReminderLog(_contacts.Count);

            // Select first contact
            if (_contacts.Count > 0)
            {
                SelectContact(_contacts[0]);
            }

            // Add click handlers to contact items
            contactsList.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(ContactItem_Click), true);
        }

        private void ContactItem_Click(object sender, MouseButtonEventArgs e)
        {
            // Find the clicked contact
            var element = e.OriginalSource as FrameworkElement;
            while (element != null && !(element.DataContext is ContactToCall))
            {
                element = VisualTreeHelper.GetParent(element) as FrameworkElement;
            }

            if (element?.DataContext is ContactToCall contact)
            {
                SelectContact(contact);
            }
        }

        private void SelectContact(ContactToCall contact)
        {
            // Deselect previous
            if (_selectedContact != null)
            {
                _selectedContact.IsSelected = false;
            }

            _selectedContact = contact;
            contact.IsSelected = true;

            // Update details panel
            txtCompanyName.Text = contact.Nazwa ?? "Brak nazwy";
            txtAddress.Text = contact.FullAddress;
            txtNIP.Text = contact.HasNIP ? $"NIP: {contact.NIP}" : "";

            // PKD Section
            if (contact.HasPKD)
            {
                pkdSection.Visibility = Visibility.Visible;
                txtPKDCode.Text = contact.PKD;
                txtPKDName.Text = contact.PKDNazwa ?? contact.Branza ?? "";
            }
            else
            {
                pkdSection.Visibility = Visibility.Collapsed;
            }

            // Phone section
            txtMainPhone.Text = FormatPhoneNumber(contact.Telefon);
            txtPhone2.Text = contact.Telefon2 ?? "";
            txtEmail.Text = contact.Email ?? "-";
            emailStack.Visibility = contact.HasEmail ? Visibility.Visible : Visibility.Collapsed;
            btnCall.IsEnabled = !string.IsNullOrWhiteSpace(contact.Telefon);

            // Status badge
            UpdateStatusBadge(contact.Status);

            // Last note
            if (contact.HasLastNote)
            {
                lastNoteSection.Visibility = Visibility.Visible;
                txtLastNote.Text = contact.OstatniaNota;
                txtLastNoteAuthor.Text = $"{contact.OstatniaNotaAutor ?? ""} • {contact.LastNoteDate}";
            }
            else
            {
                lastNoteSection.Visibility = Visibility.Collapsed;
            }

            // Clear new note
            txtNewNote.Text = "";

            // Footer stats
            txtCallCount.Text = $"{contact.CallCount} połączeń";
            txtLastCall.Text = contact.LastCallFormatted;
            txtAssignedTo.Text = contact.AssignedTo ?? "-";

            // Update status buttons selection
            UpdateStatusButtonsSelection(contact.Status);

            // Refresh list to show selection
            contactsList.Items.Refresh();
        }

        private string FormatPhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return "-";
            var clean = phone.Replace(" ", "").Replace("-", "");
            if (clean.Length == 9)
            {
                return $"+48 {clean.Substring(0, 3)} {clean.Substring(3, 3)} {clean.Substring(6, 3)}";
            }
            return phone;
        }

        private void UpdateStatusBadge(string status)
        {
            var contact = new ContactToCall { Status = status };
            statusBadgeMain.Background = contact.StatusBackground;
            txtStatusMain.Text = status ?? "-";
            txtStatusMain.Foreground = contact.StatusColor;
        }

        private void InitializeStatusButtons()
        {
            statusButtons.Children.Clear();

            foreach (var status in Statuses)
            {
                var btn = new RadioButton
                {
                    Content = status,
                    Tag = status,
                    GroupName = "StatusGroup",
                    Margin = new Thickness(0, 0, 8, 8)
                };

                // Style the button
                var contact = new ContactToCall { Status = status };
                btn.Style = CreateStatusButtonStyle(contact.StatusColor, contact.StatusBackground);
                btn.Checked += StatusButton_Checked;

                statusButtons.Children.Add(btn);
            }
        }

        private Style CreateStatusButtonStyle(SolidColorBrush textColor, SolidColorBrush bgColor)
        {
            var style = new Style(typeof(RadioButton));

            style.Setters.Add(new Setter(RadioButton.BackgroundProperty, new SolidColorBrush(Color.FromArgb(8, 255, 255, 255))));
            style.Setters.Add(new Setter(RadioButton.ForegroundProperty, new SolidColorBrush(Color.FromArgb(102, 255, 255, 255))));
            style.Setters.Add(new Setter(RadioButton.PaddingProperty, new Thickness(16, 8, 16, 8)));
            style.Setters.Add(new Setter(RadioButton.FontSizeProperty, 12.0));
            style.Setters.Add(new Setter(RadioButton.CursorProperty, Cursors.Hand));

            var template = new ControlTemplate(typeof(RadioButton));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "bd";
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(RadioButton.BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)));
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            borderFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(RadioButton.PaddingProperty));

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentPresenter);

            template.VisualTree = borderFactory;

            // Triggers
            var checkedTrigger = new Trigger { Property = RadioButton.IsCheckedProperty, Value = true };
            checkedTrigger.Setters.Add(new Setter(RadioButton.BackgroundProperty, bgColor, "bd"));
            checkedTrigger.Setters.Add(new Setter(RadioButton.ForegroundProperty, textColor));
            checkedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, textColor, "bd"));
            template.Triggers.Add(checkedTrigger);

            var hoverTrigger = new Trigger { Property = RadioButton.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(RadioButton.BackgroundProperty, new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), "bd"));
            template.Triggers.Add(hoverTrigger);

            style.Setters.Add(new Setter(RadioButton.TemplateProperty, template));

            return style;
        }

        private void UpdateStatusButtonsSelection(string status)
        {
            foreach (RadioButton btn in statusButtons.Children)
            {
                btn.IsChecked = btn.Tag?.ToString() == status;
            }
        }

        private void StatusButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton btn && _selectedContact != null)
            {
                var newStatus = btn.Tag?.ToString();
                if (newStatus != _selectedContact.Status)
                {
                    ChangeContactStatus(_selectedContact, newStatus);
                }
            }
        }

        private void StatusBadge_Click(object sender, MouseButtonEventArgs e)
        {
            // Could show a dropdown here, for now just scroll to status section
        }

        private void UpdateProgress()
        {
            int completed = _contacts.Count(c => c.IsCompleted);
            int total = _contacts.Count;
            double percent = total > 0 ? (completed / (double)total) * 100 : 0;

            txtProgressCount.Text = $"{completed}/{total}";

            // Animate progress bar width
            var containerWidth = 348.0; // approximate width of container
            var targetWidth = (percent / 100) * containerWidth;
            progressFill.Width = Math.Max(0, targetWidth);
        }

        private void FilterTab_Checked(object sender, RoutedEventArgs e)
        {
            // Guard against firing during InitializeComponent
            if (tabAll == null || tabNew == null || tabHot == null || _contacts == null) return;

            if (sender is ToggleButton tb)
            {
                // Uncheck other tabs
                if (tb == tabAll) { tabNew.IsChecked = false; tabHot.IsChecked = false; }
                else if (tb == tabNew) { tabAll.IsChecked = false; tabHot.IsChecked = false; }
                else if (tb == tabHot) { tabAll.IsChecked = false; tabNew.IsChecked = false; }

                // Filter contacts
                FilterContacts();
            }
        }

        private void FilterContacts()
        {
            var allContacts = CallReminderService.Instance.GetRandomContacts(_config?.ContactsPerReminder ?? 10);
            IEnumerable<ContactToCall> filtered = allContacts;

            if (tabNew?.IsChecked == true)
            {
                filtered = allContacts.Where(c => c.Status == "Nowy" || c.Status == "Do zadzwonienia");
            }
            else if (tabHot?.IsChecked == true)
            {
                filtered = allContacts.Where(c => c.Status == "Gorący" || c.Priority == "urgent" || c.Priority == "high");
            }

            _contacts.Clear();
            foreach (var contact in filtered.Take(_config?.ContactsPerReminder ?? 5))
            {
                _contacts.Add(contact);
            }

            txtContactsCount.Text = $"Kontakty ({_contacts.Count})";
            UpdateProgress();

            if (_contacts.Count > 0)
            {
                SelectContact(_contacts[0]);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Czy chcesz pobrać nowe losowe kontakty?",
                "Nowe kontakty",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _callsCount = 0;
                _notesCount = 0;
                _statusChangesCount = 0;
                LoadContacts();
            }
        }

        private void BtnCall_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedContact == null) return;

            // Open phone dialer
            try
            {
                var phone = _selectedContact.Telefon?.Replace(" ", "").Replace("-", "");
                if (!string.IsNullOrEmpty(phone))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = $"tel:{phone}",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening phone: {ex.Message}");
            }

            // Show call result dialog
            var dialog = new CallResultDialog(_selectedContact.Nazwa);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                _selectedContact.WasCalled = true;
                _callsCount++;

                // Handle status change from dialog
                if (!string.IsNullOrEmpty(dialog.SelectedStatus) && dialog.SelectedStatus != _selectedContact.Status)
                {
                    ChangeContactStatus(_selectedContact, dialog.SelectedStatus);
                }

                // Handle note from dialog
                if (!string.IsNullOrWhiteSpace(dialog.Note))
                {
                    AddNoteToContact(_selectedContact, dialog.Note);
                }

                // Log action
                CallReminderService.Instance.LogContactAction(_reminderLogID, _selectedContact.ID,
                    true, !string.IsNullOrWhiteSpace(dialog.Note), _selectedContact.StatusChanged, _selectedContact.NewStatus);

                UpdateProgress();
                contactsList.Items.Refresh();
            }
        }

        private void BtnSaveNote_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedContact == null || string.IsNullOrWhiteSpace(txtNewNote.Text)) return;

            AddNoteToContact(_selectedContact, txtNewNote.Text);
            _selectedContact.NoteAdded = true;

            CallReminderService.Instance.LogContactAction(_reminderLogID, _selectedContact.ID,
                false, true, false, null);

            txtNewNote.Text = "";
            UpdateProgress();
            contactsList.Items.Refresh();

            // Show the new note in last note section
            _selectedContact.OstatniaNota = txtNewNote.Text;
            _selectedContact.OstatniaNotaAutor = _userID;
            _selectedContact.DataOstatniejNotatki = DateTime.Now;
            SelectContact(_selectedContact); // Refresh display
        }

        private void TxtNewNote_TextChanged(object sender, TextChangedEventArgs e)
        {
            var length = txtNewNote.Text?.Length ?? 0;
            txtNoteCharCount.Text = $"{length}/500 znaków";
            btnSaveNote.IsEnabled = length > 0 && _selectedContact != null;
        }

        private void BtnGoogle_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedContact == null) return;

            try
            {
                var query = System.Net.WebUtility.UrlEncode($"{_selectedContact.Nazwa} {_selectedContact.Miasto}");
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"https://www.google.com/search?q={query}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening Google: {ex.Message}");
            }
        }

        private void BtnMap_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedContact == null) return;

            try
            {
                var origin = "Koziołki 40, 95-061 Dmosin";
                var destination = "";

                // Build destination from contact address
                if (!string.IsNullOrWhiteSpace(_selectedContact.Adres))
                    destination = _selectedContact.Adres;
                if (!string.IsNullOrWhiteSpace(_selectedContact.KodPocztowy))
                    destination += (destination.Length > 0 ? ", " : "") + _selectedContact.KodPocztowy;
                if (!string.IsNullOrWhiteSpace(_selectedContact.Miasto))
                    destination += (destination.Length > 0 ? " " : "") + _selectedContact.Miasto;

                // Fallback to company name + city if no address
                if (string.IsNullOrWhiteSpace(destination))
                    destination = $"{_selectedContact.Nazwa}, {_selectedContact.Miasto}";

                var originEncoded = System.Net.WebUtility.UrlEncode(origin);
                var destEncoded = System.Net.WebUtility.UrlEncode(destination);

                Process.Start(new ProcessStartInfo
                {
                    FileName = $"https://www.google.com/maps/dir/{originEncoded}/{destEncoded}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening map: {ex.Message}");
            }
        }

        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedContact == null) return;

            MessageBox.Show($"Historia kontaktu: {_selectedContact.Nazwa}\n\nTa funkcja zostanie wkrótce dodana.",
                "Historia", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnEmail_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedContact == null || !_selectedContact.HasEmail) return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"mailto:{_selectedContact.Email}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening email: {ex.Message}");
            }
        }

        private void BtnSkip_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedContact == null) return;

            // Mark as completed (skipped)
            _selectedContact.StatusChanged = true;
            _selectedContact.NewStatus = "Pominięty";

            CallReminderService.Instance.LogContactAction(_reminderLogID, _selectedContact.ID,
                false, false, false, "Pominięty");

            UpdateProgress();
            contactsList.Items.Refresh();

            // Select next contact
            var currentIndex = _contacts.IndexOf(_selectedContact);
            if (currentIndex < _contacts.Count - 1)
            {
                SelectContact(_contacts[currentIndex + 1]);
            }
        }

        private void BtnCloseWindow_Click(object sender, RoutedEventArgs e)
        {
            int completed = _contacts.Count(c => c.IsCompleted);

            if (completed < (_contacts.Count / 2.0))
            {
                var result = MessageBox.Show(
                    "Nie obsłużyłeś jeszcze połowy kontaktów. Czy na pewno chcesz zamknąć okno?",
                    "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;
            }

            // Complete reminder log
            CallReminderService.Instance.CompleteReminder(
                _reminderLogID,
                _callsCount,
                _notesCount,
                _statusChangesCount,
                completed < (_contacts.Count / 2.0),
                null
            );

            Close();
        }

        private void ShowRandomTip()
        {
            _currentTipIndex = new Random().Next(ColdCallTips.Length);
        }

        private void BtnNextTip_Click(object sender, RoutedEventArgs e)
        {
            _currentTipIndex = (_currentTipIndex + 1) % ColdCallTips.Length;
        }

        private void UpdateFlowPanel()
        {
            var phase = _phases[_currentPhase];

            txtPhaseIcon.Text = phase.Icon;
            txtPhaseName.Text = phase.Name;
            txtPhaseNumber.Text = $"Faza {_currentPhase + 1} z {_phases.Count}";
            txtScript.Text = phase.Script;
            txtFlowTip.Text = phase.Tip;

            var activeBg = new SolidColorBrush(Color.FromRgb(30, 58, 95));
            var inactiveBg = new SolidColorBrush(Color.FromRgb(17, 17, 17));

            btnPhase0.Background = _currentPhase == 0 ? activeBg : inactiveBg;
            btnPhase1.Background = _currentPhase == 1 ? activeBg : inactiveBg;
            btnPhase2.Background = _currentPhase == 2 ? activeBg : inactiveBg;
            btnPhase3.Background = _currentPhase == 3 ? activeBg : inactiveBg;

            btnPrevPhase.IsEnabled = _currentPhase > 0;

            // Update flow stats
            txtStatToday.Text = _callsCount.ToString();
            int completed = _contacts?.Count(c => c.IsCompleted) ?? 0;
            int total = _contacts?.Count ?? 0;
            txtStatRate.Text = total > 0 ? $"{(completed * 100 / total)}%" : "0%";
        }

        private void PhaseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int idx))
            {
                _currentPhase = idx;
                UpdateFlowPanel();
            }
        }

        private void PrevPhase_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPhase > 0)
            {
                _currentPhase--;
                UpdateFlowPanel();
            }
        }

        private void NextPhase_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPhase < _phases.Count - 1)
            {
                _currentPhase++;
                UpdateFlowPanel();
            }
        }

        private void AddNoteToContact(ContactToCall contact, string note)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                var cmd = new SqlCommand(
                    "INSERT INTO NotatkiCRM (IDOdbiorcy, Tresc, KtoDodal) VALUES (@id, @note, @user)", conn);
                cmd.Parameters.AddWithValue("@id", contact.ID);
                cmd.Parameters.AddWithValue("@note", note);
                cmd.Parameters.AddWithValue("@user", _userID);
                cmd.ExecuteNonQuery();

                _notesCount++;
                contact.NoteAdded = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd dodawania notatki: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChangeContactStatus(ContactToCall contact, string newStatus)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Update status
                var cmdUpdate = new SqlCommand(
                    "UPDATE OdbiorcyCRM SET Status = @status WHERE ID = @id", conn);
                cmdUpdate.Parameters.AddWithValue("@status", newStatus);
                cmdUpdate.Parameters.AddWithValue("@id", contact.ID);
                cmdUpdate.ExecuteNonQuery();

                // Log history
                var cmdLog = new SqlCommand(
                    "INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal, DataZmiany) " +
                    "VALUES (@id, 'Zmiana statusu', @val, @user, GETDATE())", conn);
                cmdLog.Parameters.AddWithValue("@id", contact.ID);
                cmdLog.Parameters.AddWithValue("@val", newStatus);
                cmdLog.Parameters.AddWithValue("@user", _userID);
                cmdLog.ExecuteNonQuery();

                _statusChangesCount++;
                contact.StatusChanged = true;
                contact.NewStatus = newStatus;
                contact.Status = newStatus;

                // Update UI
                UpdateStatusBadge(newStatus);
                UpdateProgress();
                contactsList.Items.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zmiany statusu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
