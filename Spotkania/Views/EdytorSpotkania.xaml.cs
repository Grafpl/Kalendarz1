using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.Spotkania.Models;
using Kalendarz1.Spotkania.Services;
using Kalendarz1.NotatkiZeSpotkan;

namespace Kalendarz1.Spotkania.Views
{
    public partial class EdytorSpotkania : Window
    {
        private readonly string _userID;
        private readonly long? _spotkaniID;
        private readonly SpotkaniaService _spotkaniaService;

        private List<OperatorDTO> _operatorzy = new();
        private List<KontrahentDTO> _odbiorcy = new();
        private List<KontrahentDTO> _hodowcy = new();
        private SpotkanieModel? _spotkanie;

        private bool _isEditing;

        public EdytorSpotkania(string userID, long? spotkaniID = null)
        {
            InitializeComponent();
            _userID = userID;
            _spotkaniID = spotkaniID;
            _isEditing = spotkaniID.HasValue;

            var notyfikacje = NotyfikacjeManager.GetInstance(userID);
            _spotkaniaService = new SpotkaniaService(notyfikacje);

            // Inicjalizacja list czasowych
            InitializeCzasLists();

            // Domyślna data
            DpData.SelectedDate = DateTime.Today;

            Loaded += async (s, e) => await LoadDataAsync();
        }

        private void InitializeCzasLists()
        {
            // Godziny
            for (int i = 6; i <= 22; i++)
            {
                CmbGodzina.Items.Add(i.ToString("00"));
            }
            CmbGodzina.SelectedIndex = 3; // 09:00

            // Minuty
            for (int i = 0; i < 60; i += 5)
            {
                CmbMinuta.Items.Add(i.ToString("00"));
            }
            CmbMinuta.SelectedIndex = 0; // :00
        }

        private async Task LoadDataAsync()
        {
            try
            {
                // Ładuj operatorów
                _operatorzy = await _spotkaniaService.PobierzOperatorow();
                GenerujCheckboksyUczestnikow();

                if (_isEditing && _spotkaniID.HasValue)
                {
                    TxtTytulOkna.Text = "Edycja spotkania";
                    Title = "Edycja spotkania";

                    _spotkanie = await _spotkaniaService.PobierzSpotkanie(_spotkaniID.Value);
                    if (_spotkanie != null)
                    {
                        await WypelnijFormularz();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GenerujCheckboksyUczestnikow()
        {
            PanelUczestnicy.Children.Clear();

            foreach (var op in _operatorzy)
            {
                var cb = new CheckBox
                {
                    Content = op.Name,
                    Tag = op,
                    Margin = new Thickness(0, 0, 0, 8),
                    FontSize = 13
                };

                // Zaznacz organizatora domyślnie
                if (op.ID == _userID)
                {
                    cb.IsChecked = true;
                }

                PanelUczestnicy.Children.Add(cb);
            }
        }

        private async Task WypelnijFormularz()
        {
            if (_spotkanie == null) return;

            // Typ spotkania
            switch (_spotkanie.TypSpotkania)
            {
                case TypSpotkania.Zespol: RbZespol.IsChecked = true; break;
                case TypSpotkania.Odbiorca: RbOdbiorca.IsChecked = true; break;
                case TypSpotkania.Hodowca: RbHodowca.IsChecked = true; break;
                case TypSpotkania.Online: RbOnline.IsChecked = true; break;
            }

            // Podstawowe dane
            TxtTytul.Text = _spotkanie.Tytul;
            TxtOpis.Text = _spotkanie.Opis;
            TxtLokalizacja.Text = _spotkanie.Lokalizacja;
            TxtLink.Text = _spotkanie.LinkSpotkania;

            // Data i czas
            DpData.SelectedDate = _spotkanie.DataSpotkania.Date;
            CmbGodzina.SelectedItem = _spotkanie.DataSpotkania.Hour.ToString("00");
            CmbMinuta.SelectedItem = (_spotkanie.DataSpotkania.Minute / 5 * 5).ToString("00");

            // Czas trwania
            CmbCzasTrwania.SelectedIndex = _spotkanie.CzasTrwaniaMin switch
            {
                15 => 0,
                30 => 1,
                60 => 2,
                90 => 3,
                120 => 4,
                180 => 5,
                _ => 6
            };

            // Priorytet
            CmbPriorytet.SelectedIndex = (int)_spotkanie.Priorytet;

            // Kolor
            for (int i = 0; i < CmbKolor.Items.Count; i++)
            {
                if ((CmbKolor.Items[i] as ComboBoxItem)?.Tag?.ToString() == _spotkanie.Kolor)
                {
                    CmbKolor.SelectedIndex = i;
                    break;
                }
            }

            // Przypomnienia
            Chk24h.IsChecked = _spotkanie.PrzypomnienieMinuty.Contains(1440);
            Chk1h.IsChecked = _spotkanie.PrzypomnienieMinuty.Contains(60);
            Chk15m.IsChecked = _spotkanie.PrzypomnienieMinuty.Contains(15);
            Chk5m.IsChecked = _spotkanie.PrzypomnienieMinuty.Contains(5);

            // Kontrahent
            if (!string.IsNullOrEmpty(_spotkanie.KontrahentID))
            {
                if (_spotkanie.TypSpotkania == TypSpotkania.Odbiorca)
                {
                    _odbiorcy = await _spotkaniaService.PobierzOdbiorcow();
                    CmbKontrahent.ItemsSource = _odbiorcy;
                    CmbKontrahent.SelectedValue = _spotkanie.KontrahentID;
                }
                else if (_spotkanie.TypSpotkania == TypSpotkania.Hodowca)
                {
                    _hodowcy = await _spotkaniaService.PobierzHodowcow();
                    CmbKontrahent.ItemsSource = _hodowcy;
                    CmbKontrahent.SelectedValue = _spotkanie.KontrahentID;
                }
            }

            // Uczestnicy
            foreach (CheckBox cb in PanelUczestnicy.Children)
            {
                if (cb.Tag is OperatorDTO op)
                {
                    cb.IsChecked = _spotkanie.Uczestnicy.Any(u => u.OperatorID == op.ID);
                }
            }
        }

        #region Event Handlers

        private async void TypSpotkania_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;

            bool showKontrahent = RbOdbiorca.IsChecked == true || RbHodowca.IsChecked == true;
            PanelKontrahent.Visibility = showKontrahent ? Visibility.Visible : Visibility.Collapsed;

            if (RbOdbiorca.IsChecked == true)
            {
                LblKontrahent.Text = "Odbiorca *";
                if (_odbiorcy.Count == 0)
                {
                    _odbiorcy = await _spotkaniaService.PobierzOdbiorcow();
                }
                CmbKontrahent.ItemsSource = _odbiorcy;
            }
            else if (RbHodowca.IsChecked == true)
            {
                LblKontrahent.Text = "Hodowca *";
                if (_hodowcy.Count == 0)
                {
                    _hodowcy = await _spotkaniaService.PobierzHodowcow();
                }
                CmbKontrahent.ItemsSource = _hodowcy;
            }
        }

        private async void BtnSzukajKontrahenta_Click(object sender, RoutedEventArgs e)
        {
            string szukaj = CmbKontrahent.Text;
            if (string.IsNullOrWhiteSpace(szukaj)) return;

            try
            {
                if (RbOdbiorca.IsChecked == true)
                {
                    _odbiorcy = await _spotkaniaService.PobierzOdbiorcow(szukaj);
                    CmbKontrahent.ItemsSource = _odbiorcy;
                }
                else if (RbHodowca.IsChecked == true)
                {
                    _hodowcy = await _spotkaniaService.PobierzHodowcow(szukaj);
                    CmbKontrahent.ItemsSource = _hodowcy;
                }

                CmbKontrahent.IsDropDownOpen = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wyszukiwania: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChkWszyscy_Checked(object sender, RoutedEventArgs e)
        {
            foreach (CheckBox cb in PanelUczestnicy.Children)
            {
                cb.IsChecked = true;
            }
        }

        private void ChkWszyscy_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (CheckBox cb in PanelUczestnicy.Children)
            {
                cb.IsChecked = false;
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (!Waliduj())
                return;

            try
            {
                BtnZapisz.IsEnabled = false;
                BtnZapisz.Content = "Zapisywanie...";

                var spotkanie = PobierzDaneZFormularza();

                if (_isEditing && _spotkanie != null)
                {
                    spotkanie.SpotkaniID = _spotkanie.SpotkaniID;
                    await _spotkaniaService.AktualizujSpotkanie(spotkanie);
                    MessageBox.Show("Spotkanie zostało zaktualizowane.", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    await _spotkaniaService.UtworzSpotkanie(spotkanie);
                    MessageBox.Show("Spotkanie zostało utworzone. Zaproszenia zostały wysłane do uczestników.",
                        "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisywania spotkania: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnZapisz.IsEnabled = true;
                BtnZapisz.Content = "💾 Zapisz spotkanie";
            }
        }

        #endregion

        #region Pomocnicze

        private bool Waliduj()
        {
            if (string.IsNullOrWhiteSpace(TxtTytul.Text))
            {
                MessageBox.Show("Wprowadź tytuł spotkania.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtTytul.Focus();
                return false;
            }

            if (!DpData.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz datę spotkania.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                DpData.Focus();
                return false;
            }

            if ((RbOdbiorca.IsChecked == true || RbHodowca.IsChecked == true) && CmbKontrahent.SelectedItem == null)
            {
                MessageBox.Show("Wybierz kontrahenta.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                CmbKontrahent.Focus();
                return false;
            }

            // Sprawdź czy jest przynajmniej jeden uczestnik
            bool maUczestnika = false;
            foreach (CheckBox cb in PanelUczestnicy.Children)
            {
                if (cb.IsChecked == true)
                {
                    maUczestnika = true;
                    break;
                }
            }

            if (!maUczestnika)
            {
                MessageBox.Show("Wybierz przynajmniej jednego uczestnika.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private SpotkanieModel PobierzDaneZFormularza()
        {
            var spotkanie = new SpotkanieModel
            {
                Tytul = TxtTytul.Text.Trim(),
                Opis = TxtOpis.Text.Trim(),
                Lokalizacja = TxtLokalizacja.Text.Trim(),
                LinkSpotkania = TxtLink.Text.Trim(),
                OrganizatorID = _userID
            };

            // Pobierz nazwę organizatora
            var organizator = _operatorzy.FirstOrDefault(o => o.ID == _userID);
            spotkanie.OrganizatorNazwa = organizator?.Name;

            // Typ spotkania
            if (RbZespol.IsChecked == true) spotkanie.TypSpotkania = TypSpotkania.Zespol;
            else if (RbOdbiorca.IsChecked == true) spotkanie.TypSpotkania = TypSpotkania.Odbiorca;
            else if (RbHodowca.IsChecked == true) spotkanie.TypSpotkania = TypSpotkania.Hodowca;
            else if (RbOnline.IsChecked == true) spotkanie.TypSpotkania = TypSpotkania.Online;

            // Kontrahent
            if (CmbKontrahent.SelectedItem is KontrahentDTO kontrahent)
            {
                spotkanie.KontrahentID = kontrahent.ID;
                spotkanie.KontrahentNazwa = kontrahent.Nazwa;
                spotkanie.KontrahentTyp = RbOdbiorca.IsChecked == true ? "Odbiorca" : "Hodowca";
            }

            // Data i czas
            var data = DpData.SelectedDate!.Value;
            int godzina = int.Parse(CmbGodzina.SelectedItem?.ToString() ?? "9");
            int minuta = int.Parse(CmbMinuta.SelectedItem?.ToString() ?? "0");
            spotkanie.DataSpotkania = new DateTime(data.Year, data.Month, data.Day, godzina, minuta, 0);

            // Czas trwania
            spotkanie.CzasTrwaniaMin = CmbCzasTrwania.SelectedIndex switch
            {
                0 => 15,
                1 => 30,
                2 => 60,
                3 => 90,
                4 => 120,
                5 => 180,
                6 => 480, // Cały dzień
                _ => 60
            };

            spotkanie.DataZakonczenia = spotkanie.DataSpotkania.AddMinutes(spotkanie.CzasTrwaniaMin);

            // Priorytet
            spotkanie.Priorytet = (PriorytetSpotkania)CmbPriorytet.SelectedIndex;

            // Kolor
            spotkanie.Kolor = (CmbKolor.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "#2196F3";

            // Przypomnienia
            spotkanie.PrzypomnienieMinuty = new List<int>();
            if (Chk24h.IsChecked == true) spotkanie.PrzypomnienieMinuty.Add(1440);
            if (Chk1h.IsChecked == true) spotkanie.PrzypomnienieMinuty.Add(60);
            if (Chk15m.IsChecked == true) spotkanie.PrzypomnienieMinuty.Add(15);
            if (Chk5m.IsChecked == true) spotkanie.PrzypomnienieMinuty.Add(5);

            // Uczestnicy
            foreach (CheckBox cb in PanelUczestnicy.Children)
            {
                if (cb.IsChecked == true && cb.Tag is OperatorDTO op)
                {
                    spotkanie.Uczestnicy.Add(new UczestnikSpotkaniaModel
                    {
                        OperatorID = op.ID,
                        OperatorNazwa = op.Name,
                        StatusZaproszenia = op.ID == _userID ? StatusZaproszenia.Zaakceptowane : StatusZaproszenia.Oczekuje
                    });
                }
            }

            return spotkanie;
        }

        #endregion
    }
}
