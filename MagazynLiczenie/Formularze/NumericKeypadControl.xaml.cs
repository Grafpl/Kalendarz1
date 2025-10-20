using System;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.MagazynLiczenie.Formularze
{
    public partial class NumericKeypadControl : UserControl
    {
        private string _currentValue = "0";

        public event EventHandler<decimal> ValueEntered;

        public NumericKeypadControl()
        {
            InitializeComponent();
        }

        public void SetInitialValue(decimal value)
        {
            _currentValue = value.ToString("0");
            txtDisplay.Text = _currentValue;
        }

        private void NumberButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string digit = button.Content.ToString();

                if (_currentValue == "0")
                {
                    _currentValue = digit;
                }
                else
                {
                    _currentValue += digit;
                }

                txtDisplay.Text = _currentValue;
            }
        }

        private void BackspaceButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentValue.Length > 1)
            {
                _currentValue = _currentValue.Substring(0, _currentValue.Length - 1);
            }
            else
            {
                _currentValue = "0";
            }

            txtDisplay.Text = _currentValue;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(_currentValue, out decimal value))
            {
                ValueEntered?.Invoke(this, value);
            }
        }

        public void Reset()
        {
            _currentValue = "0";
            txtDisplay.Text = _currentValue;
        }
    }
}