using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.WPF
{
    public partial class CyclicOrdersWindow : Window
    {
        public List<DateTime> SelectedDays { get; private set; } = new();
        public DateTime StartDate { get; private set; }
        public DateTime EndDate { get; private set; }

        private CheckBox[] _dayCheckBoxes;

        public CyclicOrdersWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            dpStartDate.SelectedDate = DateTime.Today.AddDays(1);
            dpEndDate.SelectedDate = DateTime.Today.AddDays(7);

            _dayCheckBoxes = new[] { chkMon, chkTue, chkWed, chkThu, chkFri, chkSat, chkSun };

            for (int i = 0; i < 5; i++)
                _dayCheckBoxes[i].IsChecked = true;

            UpdateCheckboxesState();
            UpdatePreview();
        }

        private void DateRange_Changed(object sender, RoutedEventArgs e)
        {
            UpdatePreview();
        }

        private void RbFrequency_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            UpdateCheckboxesState();
            UpdatePreview();
        }

        private void DayCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdatePreview();
        }

        private void UpdateCheckboxesState()
        {
            bool enable = rbSelectedDays.IsChecked == true || rbWeekly.IsChecked == true;

            foreach (var cb in _dayCheckBoxes)
            {
                cb.IsEnabled = enable;
            }

            if (rbWeekly.IsChecked == true)
            {
                foreach (var cb in _dayCheckBoxes)
                    cb.IsChecked = false;

                if (_dayCheckBoxes.Length > 0)
                    _dayCheckBoxes[0].IsChecked = true;
            }
        }

        private void UpdatePreview()
        {
            CalculateSelectedDays();

            if (SelectedDays.Count > 0)
            {
                lblPreview.Text = $"Zostanie utworzonych: {SelectedDays.Count} zamówień\n" +
                                 $"Pierwsze: {SelectedDays.First():yyyy-MM-dd}\n" +
                                 $"Ostatnie: {SelectedDays.Last():yyyy-MM-dd}";
            }
            else
            {
                lblPreview.Text = "Zostanie utworzonych: 0 zamówień";
            }
        }

        private void CalculateSelectedDays()
        {
            SelectedDays.Clear();

            if (!dpStartDate.SelectedDate.HasValue || !dpEndDate.SelectedDate.HasValue)
                return;

            StartDate = dpStartDate.SelectedDate.Value.Date;
            EndDate = dpEndDate.SelectedDate.Value.Date;

            if (EndDate < StartDate)
            {
                return;
            }

            var current = StartDate;
            while (current <= EndDate)
            {
                var dayOfWeek = (int)current.DayOfWeek;
                var dayIndex = dayOfWeek == 0 ? 6 : dayOfWeek - 1;

                if (rbDaily.IsChecked == true)
                {
                    if (dayIndex < 5)
                        SelectedDays.Add(current);
                }
                else if (rbSelectedDays.IsChecked == true)
                {
                    if (_dayCheckBoxes[dayIndex].IsChecked == true)
                        SelectedDays.Add(current);
                }
                else if (rbWeekly.IsChecked == true)
                {
                    if (_dayCheckBoxes[dayIndex].IsChecked == true)
                    {
                        var weeksDiff = ((current - StartDate).Days / 7);
                        if ((current - StartDate).Days % 7 == 0 ||
                            current.DayOfWeek == StartDate.DayOfWeek)
                            SelectedDays.Add(current);
                    }
                }

                current = current.AddDays(1);
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            CalculateSelectedDays();

            if (!SelectedDays.Any())
            {
                MessageBox.Show("Brak wybranych dni. Proszę sprawdzić ustawienia.",
                    "Brak wyboru", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (EndDate < StartDate)
            {
                MessageBox.Show("Data końcowa nie może być wcześniejsza niż początkowa!",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}