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
    public partial class DodajTowarDialog : Window
    {
        private readonly PaszaService _svc;
        private List<TowarPasza> _wszystkie = new();   // dla bieżącego katalogu (load po wyborze katalogu)
        private List<TowarPasza> _widoczne = new();
        private List<KatalogInfo> _katalogi = new();

        public int IloscDodanych { get; private set; }

        public DodajTowarDialog(PaszaService svc)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _svc = svc;
            Loaded += async (_, __) =>
            {
                await ZaladujKatalogiAsync();
                txtFiltr.Focus();
            };
        }

        private async Task ZaladujKatalogiAsync()
        {
            try
            {
                _katalogi = await _svc.GetKatalogiAsync();
                var all = new KatalogInfo { Id = -1, Nazwa = "(wszystkie katalogi)", LiczbaTowarow = _katalogi.Sum(k => k.LiczbaTowarow) };
                var lista = new List<KatalogInfo> { all };
                lista.AddRange(_katalogi);
                cbKatalog.ItemsSource = lista;
                cbKatalog.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd ładowania katalogów:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ZaladujTowaryDlaKataloguAsync()
        {
            try
            {
                var k = cbKatalog.SelectedItem as KatalogInfo;
                int? katId = k == null || k.Id < 0 ? (int?)null : k.Id;
                _wszystkie = await _svc.GetTowaryByKatalogAsync(katId, "");
                // Subskrybuj zmianę checkboxa per item
                foreach (var t in _wszystkie) t.PropertyChanged += Item_PropertyChanged;
                AplikujFiltr();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd ładowania towarów:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TowarPasza.IsSelected))
                AktualizujLiczniki();
        }

        private void AplikujFiltr()
        {
            string f = (txtFiltr.Text ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(f))
            {
                _widoczne = _wszystkie;
            }
            else
            {
                _widoczne = _wszystkie.Where(t =>
                    t.Kod.ToLowerInvariant().Contains(f) ||
                    t.Nazwa.ToLowerInvariant().Contains(f)
                ).ToList();
            }
            dgTowary.ItemsSource = _widoczne;
            emptyState.Visibility = _widoczne.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (_widoczne.Count == 0)
            {
                if (_wszystkie.Count == 0)
                {
                    lblEmptyTitle.Text = "Wybierz katalog";
                    lblEmptyHint.Text = "Wybierz katalog z listy powyżej, by zobaczyć towary.";
                }
                else
                {
                    lblEmptyTitle.Text = "Filtr nie pasuje do żadnego towaru";
                    lblEmptyHint.Text = $"W katalogu jest {_wszystkie.Count} towarów, ale żaden nie pasuje do filtra '{f}'. Zmień fragment lub wyczyść pole.";
                }
            }
            AktualizujLiczniki();
        }

        private void AktualizujLiczniki()
        {
            lblWidocznych.Text = _widoczne.Count.ToString();
            int zazn = _wszystkie.Count(x => x.IsSelected);
            lblZaznaczonych.Text = zazn.ToString();
            btnDodaj.IsEnabled = zazn > 0;
            btnDodaj.Content = zazn > 0 ? $"➕ Dodaj {zazn} do słownika" : "➕ Dodaj zaznaczone";
            lblFooterInfo.Text = zazn > 0
                ? $"Gotowe do zapisu: {zazn} {Odmien(zazn, "towar", "towary", "towarów")}"
                : "Zaznacz przynajmniej jeden towar checkboxem (lub kliknij 'Zaznacz widoczne').";
        }

        private static string Odmien(int n, string mian, string mn2, string mn5)
        {
            int last = n % 10;
            int last2 = n % 100;
            if (n == 1) return mian;
            if (last >= 2 && last <= 4 && (last2 < 12 || last2 > 14)) return mn2;
            return mn5;
        }

        private async void CbKatalog_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await ZaladujTowaryDlaKataloguAsync();
        }

        private void TxtFiltr_TextChanged(object sender, TextChangedEventArgs e) => AplikujFiltr();

        private void BtnZaznaczWszystkie_Click(object sender, RoutedEventArgs e)
        {
            foreach (var t in _widoczne) t.IsSelected = true;
        }

        private void BtnOdznacz_Click(object sender, RoutedEventArgs e)
        {
            foreach (var t in _wszystkie) t.IsSelected = false;
        }

        private async void BtnDodaj_Click(object sender, RoutedEventArgs e)
        {
            var wybrane = _wszystkie.Where(t => t.IsSelected).ToList();
            if (wybrane.Count == 0) return;

            var katalog = cbKatalog.SelectedItem as KatalogInfo;
            if (katalog != null && katalog.Id < 0) katalog = null;

            try
            {
                btnDodaj.IsEnabled = false;
                string user = App.UserID ?? Environment.UserName;
                int ok = 0;
                foreach (var t in wybrane)
                {
                    await _svc.DodajTowarDoSlownikaAsync(t, katalog, user);
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
