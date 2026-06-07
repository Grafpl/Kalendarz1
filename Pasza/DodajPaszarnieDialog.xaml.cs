using Kalendarz1.Pasza.Models;
using Kalendarz1.Pasza.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.Pasza
{
    public partial class DodajPaszarnieDialog : Window
    {
        private readonly PaszaService _svc;
        private List<KontrahentSymfonia> _wszyscy = new();   // pełna lista załadowana raz
        private List<KontrahentSymfonia> _widoczne = new();  // po filtrze

        public int IloscDodanych { get; private set; }

        public DodajPaszarnieDialog(PaszaService svc)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _svc = svc;
            Loaded += async (_, __) =>
            {
                await ZaladujWszystkichAsync();
                txtFiltr.Focus();
            };
        }

        private async Task ZaladujWszystkichAsync()
        {
            try
            {
                _wszyscy = await _svc.GetKontrahenciAsync("");
                // Subskrybuj PropertyChanged — żeby licznik „zaznaczonych" reagował na każdy klik checkboxa
                foreach (var k in _wszyscy) k.PropertyChanged += Item_PropertyChanged;
                AplikujFiltr();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd ładowania kontrahentów:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(KontrahentSymfonia.IsSelected))
                AktualizujLiczniki();
        }

        private void AplikujFiltr()
        {
            string f = (txtFiltr.Text ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(f))
            {
                _widoczne = _wszyscy;
            }
            else
            {
                _widoczne = _wszyscy.Where(k =>
                    k.Shortcut.ToLowerInvariant().Contains(f) ||
                    k.Name.ToLowerInvariant().Contains(f) ||
                    (k.NIP ?? "").Replace("-", "").Replace(" ", "").Contains(f)
                ).ToList();
            }
            dgKontrahenci.ItemsSource = _widoczne;
            emptyState.Visibility = _widoczne.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (_widoczne.Count == 0 && !string.IsNullOrEmpty(f))
                lblEmptyHint.Text = $"Filtr '{f}' nie pasuje do żadnego z {_wszyscy.Count} kontrahentów. Zmień fragment lub wyczyść pole.";
            AktualizujLiczniki();
        }

        private void AktualizujLiczniki()
        {
            lblWidocznych.Text = _widoczne.Count.ToString();
            int zazn = _wszyscy.Count(x => x.IsSelected);
            lblZaznaczonych.Text = zazn.ToString();
            btnDodaj.IsEnabled = zazn > 0;
            btnDodaj.Content = zazn > 0 ? $"➕ Dodaj {zazn} do słownika" : "➕ Dodaj zaznaczone";
            lblFooterInfo.Text = zazn > 0
                ? $"Gotowe do zapisu: {zazn} {Odmien(zazn, "paszarnia", "paszarnie", "paszarni")}"
                : "Zaznacz przynajmniej jedną pozycję checkboxem (lub kliknij 'Zaznacz widoczne').";
        }

        private static string Odmien(int n, string mian, string mn2, string mn5)
        {
            int last = n % 10;
            int last2 = n % 100;
            if (n == 1) return mian;
            if (last >= 2 && last <= 4 && (last2 < 12 || last2 > 14)) return mn2;
            return mn5;
        }

        private void TxtFiltr_TextChanged(object sender, TextChangedEventArgs e) => AplikujFiltr();

        private void BtnZaznaczWszystkie_Click(object sender, RoutedEventArgs e)
        {
            foreach (var k in _widoczne) k.IsSelected = true;
        }

        private void BtnOdznacz_Click(object sender, RoutedEventArgs e)
        {
            foreach (var k in _wszyscy) k.IsSelected = false;
        }

        private async void BtnDodaj_Click(object sender, RoutedEventArgs e)
        {
            var wybrane = _wszyscy.Where(k => k.IsSelected).ToList();
            if (wybrane.Count == 0) return;

            try
            {
                btnDodaj.IsEnabled = false;
                string user = App.UserID ?? Environment.UserName;
                int ok = 0;
                foreach (var k in wybrane)
                {
                    await _svc.DodajPaszarnieDoSlownikaAsync(k, user);
                    ok++;
                }
                IloscDodanych = ok;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zapisu: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnDodaj.IsEnabled = true;
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
