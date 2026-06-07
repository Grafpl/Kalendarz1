using Kalendarz1.Pasza.Models;
using Kalendarz1.Pasza.Services;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.Pasza
{
    public partial class CennikEditDialog : Window
    {
        private readonly PaszaService _svc;
        private readonly CennikItem _item;

        public CennikEditDialog(PaszaService svc, CennikItem item)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _svc = svc;
            _item = item;
            lblTytul.Text = item.Id == 0 ? "Nowy wpis cennika" : $"Edycja wpisu #{item.Id}";
            Loaded += async (_, __) => await ZainicjujAsync();
        }

        private async Task ZainicjujAsync()
        {
            var kontrahenci = await _svc.GetKontrahenciAsync();
            var towary = await _svc.GetTowaryAsync();
            cbHodowca.ItemsSource = kontrahenci;
            cbTowar.ItemsSource = towary;

            if (_item.Id != 0)
            {
                cbHodowca.SelectedItem = kontrahenci.FirstOrDefault(k => k.Shortcut == _item.HodowcaKhKod);
                cbTowar.SelectedItem = towary.FirstOrDefault(t => t.Kod == _item.TowarKod);
                txtMarza.Text = _item.MarzaKwota.ToString("F2", CultureInfo.InvariantCulture);
                dpOd.SelectedDate = _item.DataOd;
                dpDo.SelectedDate = _item.DataDo;
                chkAktywny.IsChecked = _item.Aktywny;
                txtUwagi.Text = _item.Uwagi;
            }
            else
            {
                dpOd.SelectedDate = DateTime.Today;
            }
        }

        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            var hod = cbHodowca.SelectedItem as KontrahentSymfonia;
            var twr = cbTowar.SelectedItem as TowarPasza;
            decimal marza = ParseDec(txtMarza.Text);

            if (hod == null) { MessageBox.Show("Wybierz hodowcę."); return; }
            if (twr == null) { MessageBox.Show("Wybierz towar."); return; }
            if (marza <= 0) { MessageBox.Show("Marża musi być > 0."); return; }
            if (dpOd.SelectedDate == null) { MessageBox.Show("Wybierz datę 'od'."); return; }
            if (dpDo.SelectedDate.HasValue && dpDo.SelectedDate < dpOd.SelectedDate)
            {
                MessageBox.Show("Data 'do' nie może być wcześniejsza niż 'od'.");
                return;
            }

            _item.HodowcaKhKod = hod.Shortcut;
            _item.HodowcaNazwa = hod.Name;
            _item.TowarKod = twr.Kod;
            _item.TowarNazwa = twr.Nazwa;
            _item.MarzaKwota = marza;
            _item.DataOd = dpOd.SelectedDate.Value;
            _item.DataDo = dpDo.SelectedDate;
            _item.Aktywny = chkAktywny.IsChecked == true;
            _item.Uwagi = txtUwagi.Text?.Trim() ?? "";

            try
            {
                btnZapisz.IsEnabled = false;
                await _svc.UpsertCennikAsync(_item, App.UserID ?? Environment.UserName);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zapisu: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnZapisz.IsEnabled = true;
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static decimal ParseDec(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0m;
            s = s.Replace(',', '.').Trim();
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;
        }
    }
}
