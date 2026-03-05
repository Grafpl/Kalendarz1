using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Kalendarz1.Hodowcy
{
    public partial class FarmAddressWpfDialog : Window
    {
        private static readonly string[] Provinces =
        {
            "(brak)", "dolnoslaskie", "kujawsko-pomorskie", "lubelskie", "lubuskie",
            "lodzkie", "malopolskie", "mazowieckie", "opolskie", "podkarpackie",
            "podlaskie", "pomorskie", "slaskie", "swietokrzyskie",
            "warminsko-mazurskie", "wielkopolskie", "zachodniopomorskie"
        };

        public FarmAddressWpfDialog(FarmAddressEntry existing = null)
        {
            InitializeComponent();

            foreach (var p in Provinces)
                cbbProvince.Items.Add(new ComboBoxItem { Content = p });
            cbbProvince.SelectedIndex = 0;

            if (existing != null)
            {
                Title = "Edycja adresu fermy";
                tbDialogTitle.Text = "Edycja adresu fermy";
                LoadEntry(existing);
            }
        }

        private void LoadEntry(FarmAddressEntry e)
        {
            edtName.Text = e.Name;
            edtAddress.Text = e.Address;
            edtPostalCode.Text = e.PostalCode;
            edtCity.Text = e.City;
            if (e.ProvinceID >= 0 && e.ProvinceID < cbbProvince.Items.Count)
                cbbProvince.SelectedIndex = e.ProvinceID;
            edtDistance.Text = e.Distance != 0 ? e.Distance.ToString(CultureInfo.InvariantCulture) : "";
            edtPhone1.Text = e.Phone1;
            edtInfo1.Text = e.Info1;
            edtAnimNo.Text = e.AnimNo;
            edtIRZPlus.Text = e.IRZPlus;
            cbHalt.IsChecked = e.Halt;
        }

        public FarmAddressEntry GetEntry() => new()
        {
            Name = edtName.Text.Trim(),
            Address = edtAddress.Text.Trim(),
            PostalCode = edtPostalCode.Text.Trim(),
            City = edtCity.Text.Trim(),
            ProvinceID = cbbProvince.SelectedIndex > 0 ? cbbProvince.SelectedIndex : 0,
            Distance = decimal.TryParse(edtDistance.Text.Replace(',', '.'),
                NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0,
            Phone1 = edtPhone1.Text.Trim(),
            Info1 = edtInfo1.Text.Trim(),
            AnimNo = edtAnimNo.Text.Trim(),
            IRZPlus = edtIRZPlus.Text.Trim(),
            Halt = cbHalt.IsChecked == true
        };

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(edtName.Text))
            {
                MessageBox.Show("Nazwa jest wymagana!", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                edtName.Focus();
                return;
            }
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) DialogResult = false;
        }
    }
}
