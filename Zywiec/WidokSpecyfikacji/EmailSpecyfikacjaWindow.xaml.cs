using System;
using System.IO;
using System.Windows;

namespace Kalendarz1.Zywiec.WidokSpecyfikacji
{
    public partial class EmailSpecyfikacjaWindow : Window
    {
        private string _sciezkaPDF;
        private string _nazwaDostawcy;
        private DateTime _dataUboju;
        private int _sztuki;
        private decimal _kilogramy;
        private decimal _wartosc;

        public EmailSpecyfikacjaWindow(
            string nazwaDostawcy,
            string emailDostawcy,
            DateTime dataUboju,
            int sztuki,
            decimal kilogramy,
            decimal wartosc,
            string sciezkaPDF)
        {
            InitializeComponent();

            _nazwaDostawcy = nazwaDostawcy;
            _dataUboju = dataUboju;
            _sztuki = sztuki;
            _kilogramy = kilogramy;
            _wartosc = wartosc;
            _sciezkaPDF = sciezkaPDF;

            UstawDaneFormularza(emailDostawcy);
        }

        private void UstawDaneFormularza(string emailDostawcy)
        {
            // Naglowek
            txtDostawca.Text = _nazwaDostawcy;

            // Informacje o specyfikacji
            txtDataUboju.Text = _dataUboju.ToString("dd.MM.yyyy");
            txtSztuki.Text = $"{_sztuki:N0} szt";
            txtKilogramy.Text = $"{_kilogramy:N0} kg";
            txtWartosc.Text = $"{_wartosc:N0} zl";

            // Email - jesli jest w bazie to wstaw, jesli nie to puste
            txtDo.Text = !string.IsNullOrWhiteSpace(emailDostawcy) ? emailDostawcy : "";

            // Temat
            txtTemat.Text = $"Specyfikacja przyjecia drobiu - {_nazwaDostawcy} - {_dataUboju:dd.MM.yyyy} - Piorkowscy";

            // Tresc wiadomosci
            txtWiadomosc.Text = GenerujTrescWiadomosci();

            // Zalacznik
            if (File.Exists(_sciezkaPDF))
            {
                txtZalacznik.Text = _sciezkaPDF;
            }
            else
            {
                txtZalacznik.Text = "Plik PDF nie zostal jeszcze wygenerowany";
            }
        }

        private string GenerujTrescWiadomosci()
        {
            decimal sredniaWaga = _sztuki > 0 ? _kilogramy / _sztuki : 0;

            return $@"Szanowny Panie/Pani,

W zalaczeniu przesylamy specyfikacje przyjecia drobiu z dnia {_dataUboju:dd MMMM yyyy}.

PODSUMOWANIE DOSTAWY:
--------------------------------------------
  Dostawca:       {_nazwaDostawcy}
  Data uboju:     {_dataUboju:dd.MM.yyyy}
  Sztuki:         {_sztuki:N0} szt
  Kilogramy:      {_kilogramy:N0} kg
  Srednia waga:   {sredniaWaga:N2} kg/szt
  DO WYPLATY:     {_wartosc:N0} zl
--------------------------------------------

Szczegolowa specyfikacja znajduje sie w zalaczonym pliku PDF.

W razie pytan prosimy o kontakt.

Z powazaniem,
Ubojnia Drobiu ""Piorkowscy""
Koziolki 40, 95-061 Dmosin
Tel: +48 46 874 68 55
Email: rozliczenia@piorkowscy.pl

---
Ta wiadomosc zostala wygenerowana automatycznie.";
        }

        private void BtnWyslij_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string email = txtDo.Text.Trim();
                string temat = txtTemat.Text;
                string tresc = txtWiadomosc.Text;

                // Jesli jest email - otworz klienta pocztowego
                if (!string.IsNullOrWhiteSpace(email))
                {
                    string mailto = $"mailto:{email}?subject={Uri.EscapeDataString(temat)}&body={Uri.EscapeDataString(tresc)}";

                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(mailto)
                    {
                        UseShellExecute = true
                    });

                    // Skopiuj sciezke PDF do schowka
                    if (File.Exists(_sciezkaPDF))
                    {
                        Clipboard.SetText(_sciezkaPDF);
                    }

                    MessageBox.Show(
                        $"Otwarto domyslny klient email.\n\n" +
                        $"WAZNE: Dolacz recznie plik PDF jako zalacznik.\n\n" +
                        $"Sciezka do pliku PDF zostala skopiowana do schowka:\n{_sciezkaPDF}",
                        "Informacja",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    // Brak emaila - skopiuj tresc i otworz folder z PDF
                    Clipboard.SetText($"Temat: {temat}\n\n{tresc}");

                    MessageBox.Show(
                        "Brak adresu email dostawcy w bazie.\n\n" +
                        "Tresc wiadomosci zostala skopiowana do schowka.\n\n" +
                        $"Plik PDF: {_sciezkaPDF}",
                        "Brak adresu email",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad: {ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnKopiuj_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string doSkopiowania = $"Do: {txtDo.Text}\nTemat: {txtTemat.Text}\n\n{txtWiadomosc.Text}\n\nZalacznik: {_sciezkaPDF}";
                Clipboard.SetText(doSkopiowania);

                MessageBox.Show("Tresc wiadomosci zostala skopiowana do schowka.", "Skopiowano",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad kopiowania: {ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnOtworzFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists(_sciezkaPDF))
                {
                    // Otworz folder i zaznacz plik
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{_sciezkaPDF}\"");
                }
                else
                {
                    string folder = Path.GetDirectoryName(_sciezkaPDF);
                    if (Directory.Exists(folder))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", folder);
                    }
                    else
                    {
                        MessageBox.Show("Folder z plikiem PDF nie istnieje.", "Informacja",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad otwierania folderu: {ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
