using System;
using System.IO;
using System.Windows;

namespace Kalendarz1.PrzypomnieniePlatnosci
{
    public partial class EmailPrzypomnienieWindow : Window
    {
        private string _nazwaKontrahenta;
        private string _sciezkaPDF;
        private decimal _kwota;
        private int _liczbaDokumentow;
        private DateTime _najpozniejszyTermin;
        private WersjaPrzypomnienia _wersja;
        private int _liczbaDni;

        public EmailPrzypomnienieWindow(string nazwaKontrahenta, string emailOdbiorcy,
            decimal kwota, int liczbaDokumentow, DateTime najpozniejszyTermin,
            string sciezkaPDF, WersjaPrzypomnienia wersja, int liczbaDni)
        {
            InitializeComponent();

            _nazwaKontrahenta = nazwaKontrahenta;
            _sciezkaPDF = sciezkaPDF;
            _kwota = kwota;
            _liczbaDokumentow = liczbaDokumentow;
            _najpozniejszyTermin = najpozniejszyTermin;
            _wersja = wersja;
            _liczbaDni = liczbaDni;

            UstawDaneFormularza(emailOdbiorcy);
        }

        private void UstawDaneFormularza(string emailOdbiorcy)
        {
            txtKontrahent.Text = _nazwaKontrahenta;
            txtKwota.Text = $"{_kwota:N2} zł";
            txtLiczbaDokumentow.Text = $"{_liczbaDokumentow} szt.";
            txtNajpozniejszyTermin.Text = _najpozniejszyTermin.ToString("dd.MM.yyyy");

            txtDo.Text = emailOdbiorcy;
            txtDW.Text = "kasa@piorkowscy.com.pl";

            // Temat w zależności od wersji
            string temat = _wersja switch
            {
                WersjaPrzypomnienia.Przedsadowa => $"PILNE - Ostateczne wezwanie do zapłaty - {_nazwaKontrahenta}",
                WersjaPrzypomnienia.Mocna => $"WAŻNE - Przypomnienie o zaległych płatnościach - {_nazwaKontrahenta}",
                _ => $"Przypomnienie o płatności - {_nazwaKontrahenta}"
            };
            txtTemat.Text = temat;

            // Treść wiadomości w zależności od wersji
            string wiadomosc = _wersja switch
            {
                WersjaPrzypomnienia.Przedsadowa => GenerujTrescPrzedsadowa(),
                WersjaPrzypomnienia.Mocna => GenerujTrescMocna(),
                _ => GenerujTrescLagodna()
            };
            txtWiadomosc.Text = wiadomosc;

            txtZalacznik.Text = Path.GetFileName(_sciezkaPDF);
        }

        private string GenerujTrescLagodna()
        {
            return $@"Szanowni Państwo,

Uprzejmie przypominamy o należnościach wobec naszej firmy.

Szczegóły zadłużenia:
• Kwota do zapłaty: {_kwota:N2} zł
• Liczba dokumentów: {_liczbaDokumentow} szt.
• Najpóźniejszy termin płatności: {_najpozniejszyTermin:dd.MM.yyyy}

Szczegółowe zestawienie faktur wraz z danymi do przelewu znajduje się w załączniku PDF.

Będziemy wdzięczni za uregulowanie należności w ciągu {_liczbaDni} {OdmianaDni(_liczbaDni)}.

W razie pytań lub problemów z płatnością prosimy o kontakt - chętnie ustalimy dogodny termin spłaty.

Dane do przelewu:
Bank Pekao S.A.
Numer konta: 60 1240 3060 1111 0010 4888 9213

Z poważaniem,
Dział Księgowości
Ubojnia Drobiu ""Piórkowscy"" Jerzy Piórkowski w spadku
tel: +48 46 874 71 70
email: kasa@piorkowscy.com.pl
www.piorkowscy.com.pl";
        }

        private string GenerujTrescMocna()
        {
            return $@"Szanowni Państwo,

Informujemy o zaległych płatnościach wobec naszej firmy, które wymagają pilnego uregulowania.

ZALEGŁE NALEŻNOŚCI:
• Kwota do zapłaty: {_kwota:N2} zł
• Liczba przeterminowanych dokumentów: {_liczbaDokumentow} szt.
• Najpóźniejszy termin płatności: {_najpozniejszyTermin:dd.MM.yyyy}

Szczegółowe zestawienie wraz z wyliczeniem odsetek znajduje się w załączonym dokumencie PDF.

PROSIMY O PILNĄ PŁATNOŚĆ W CIĄGU {_liczbaDni} {OdmianaDni(_liczbaDni).ToUpper()} ROBOCZYCH.

Dane do przelewu:
Bank Pekao S.A.
Numer konta: 60 1240 3060 1111 0010 4888 9213

⚠️ UWAGA: Brak reakcji w ciągu {_liczbaDni} {OdmianaDni(_liczbaDni)} skutkuje:
• Naliczeniem odsetek ustawowych
• Wstrzymaniem dostaw
• Skierowaniem sprawy do dalszego postępowania windykacyjnego

W razie problemów z płatnością prosimy o natychmiastowy kontakt.

Z poważaniem,
Dział Księgowości
Ubojnia Drobiu ""Piórkowscy"" Jerzy Piórkowski w spadku
tel: +48 46 874 71 70
email: kasa@piorkowscy.com.pl";
        }

        private string GenerujTrescPrzedsadowa()
        {
            return $@"Szanowni Państwo,

Niniejszym wzywamy do zapłaty przeterminowanych należności wobec naszej firmy.

ZADŁUŻENIE:
• Kwota do natychmiastowej zapłaty: {_kwota:N2} zł
• Liczba dokumentów: {_liczbaDokumentow} szt.
• Termin płatności: {_liczbaDni} {OdmianaDni(_liczbaDni).ToUpper()} od otrzymania niniejszego wezwania

Szczegółowe zestawienie znajduje się w załączniku PDF.

Dane do przelewu:
Bank Pekao S.A.
Numer konta: 60 1240 3060 1111 0010 4888 9213

⚠️ OSTRZEŻENIE:
Brak zapłaty w terminie {_liczbaDni} {OdmianaDni(_liczbaDni)} skutkuje:
• Skierowaniem sprawy na drogę postępowania sądowego
• Naliczeniem kosztów windykacji i sądowych
• Możliwym wpisem do Krajowego Rejestru Długów (KRD)

Jest to ostateczne przedsądowe wezwanie do zapłaty.

W razie konieczności ustalenia planu spłat prosimy o natychmiastowy kontakt telefoniczny.

Z poważaniem,
Dział Księgowości
Ubojnia Drobiu ""Piórkowscy"" Jerzy Piórkowski w spadku
tel: +48 46 874 71 70
email: kasa@piorkowscy.com.pl";
        }

        private string OdmianaDni(int liczba)
        {
            if (liczba == 1) return "dzień";

            int reszta = liczba % 100;
            if (reszta >= 10 && reszta <= 21) return "dni";

            int ostatniaCyfra = liczba % 10;
            if (ostatniaCyfra >= 2 && ostatniaCyfra <= 4) return "dni";

            return "dni";
        }

        private void BtnWyslij_Click(object sender, RoutedEventArgs e)
        {
            // Walidacja
            if (string.IsNullOrWhiteSpace(txtDo.Text))
            {
                MessageBox.Show("Podaj adres email odbiorcy.", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtTemat.Text))
            {
                MessageBox.Show("Podaj temat wiadomości.", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Tutaj możesz zaimplementować wysyłkę emaila
                // Na przykład używając System.Net.Mail lub biblioteki MailKit

                // Przykładowa implementacja z użyciem domyślnego klienta email
                var mailto = $"mailto:{txtDo.Text}?subject={Uri.EscapeDataString(txtTemat.Text)}&body={Uri.EscapeDataString(txtWiadomosc.Text)}";

                if (!string.IsNullOrWhiteSpace(txtDW.Text))
                {
                    mailto += $"&cc={txtDW.Text}";
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(mailto)
                {
                    UseShellExecute = true
                });

                MessageBox.Show(
                    "✓ Otworzyłem domyślny klient email.\n\n" +
                    "⚠️ Uwaga: Załącznik PDF musisz dołączyć ręcznie.\n" +
                    $"Plik znajduje się w: {_sciezkaPDF}",
                    "Informacja",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd wysyłania email: {ex.Message}", "Błąd",
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