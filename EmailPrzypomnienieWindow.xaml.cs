using Microsoft.Win32;
using System;
using System.Net;
using System.Net.Mail;
using System.Windows;

namespace Kalendarz1
{
    public partial class EmailPrzypomnienieWindow : Window
    {
        private string sciezkaPDF = "";
        private WersjaPrzypomnienia wersja;

        public EmailPrzypomnienieWindow(string nazwaKontrahenta, string emailOdbiorcy, decimal kwotaPrzeterminowana,
            int liczbaDokumentow, DateTime najpozniejszyTermin, string sciezkaPdfDomyslna, WersjaPrzypomnienia wersjaPrzypomnienia)
        {
            InitializeComponent();

            wersja = wersjaPrzypomnienia;
            txtNazwaKontrahenta.Text = nazwaKontrahenta;
            txtEmailOdbiorcy.Text = emailOdbiorcy;
            sciezkaPDF = sciezkaPdfDomyslna;

            if (!string.IsNullOrEmpty(sciezkaPdfDomyslna))
            {
                txtZalacznik.Text = System.IO.Path.GetFileName(sciezkaPdfDomyslna);
            }

            // Generuj temat
            if (wersja == WersjaPrzypomnienia.Lagodna)
            {
                txtTemat.Text = $"Przypomnienie o płatności - {nazwaKontrahenta}";
            }
            else
            {
                txtTemat.Text = $"PILNE - Przypomnienie o płatności - {nazwaKontrahenta}";
            }

            // Generuj treść
            GenerujTresc(nazwaKontrahenta, kwotaPrzeterminowana, liczbaDokumentow, najpozniejszyTermin, wersja);
        }

        private void GenerujTresc(string nazwaKontrahenta, decimal kwotaPrzeterminowana, int liczbaDokumentow,
            DateTime najpozniejszyTermin, WersjaPrzypomnienia wersja)
        {
            string tresc;

            if (wersja == WersjaPrzypomnienia.Lagodna)
            {
                tresc = $@"Szanowni Państwo,

Uprzejmie przypominamy o płatnościach, które oczekują na uregulowanie.

📊 Informacja o zaległościach:
- Liczba dokumentów: {liczbaDokumentow} szt.
- Kwota do uregulowania: {kwotaPrzeterminowana:N2} zł
- Termin płatności: {najpozniejszyTermin:dd.MM.yyyy}

Będziemy wdzięczni za uregulowanie należności w najbliższym możliwym terminie. W przypadku jakichkolwiek pytań lub trudności, prosimy o kontakt - chętnie ustalimy dogodny termin płatności.

💳 Dane do przelewu:
Odbiorca: Ubojnia Drobiu ""Piórkowscy"" Jerzy Piórkowski w spadku
NIP: 726-162-54-06
Bank Pekao S.A.
Konto: 60 1240 3060 1111 0010 4888 9213
SWIFT: PKOPPLPW

W tytule przelewu prosimy podać numery dokumentów.

📎 W załączeniu przesyłamy szczegółowe zestawienie dokumentów.

Dziękujemy za dotychczasową współpracę i liczymy na dalsze dobre relacje biznesowe.

W razie pytań jesteśmy do Państwa dyspozycji:
📞 Tel: +48 46 874 71 70
📧 Email: kasa@piorkowscy.com.pl
🕐 Godziny: Pn-Pt 8:00-16:00

Z poważaniem,
Dział Księgowości
Ubojnia Drobiu ""Piórkowscy""
www.piorkowscy.com.pl";
            }
            else
            {
                tresc = $@"Szanowni Państwo,

Uprzejmie informujemy, że na Państwa koncie widnieją zaległe płatności wymagające pilnej regulacji.

📊 PODSUMOWANIE ZALEGŁOŚCI:
- Liczba przeterminowanych dokumentów: {liczbaDokumentow} szt.
- Kwota do pilnej zapłaty: {kwotaPrzeterminowana:N2} zł
- Najpóźniejszy termin płatności: {najpozniejszyTermin:dd.MM.yyyy}

⚠️ PILNE:
Prosimy o niezwłoczną płatność kwoty przeterminowanej w ciągu 3 dni roboczych.

💳 DANE DO PRZELEWU:
Odbiorca: Ubojnia Drobiu ""Piórkowscy"" Jerzy Piórkowski w spadku
NIP: 726-162-54-06
Bank Pekao S.A.
Konto: 60 1240 3060 1111 0010 4888 9213
SWIFT: PKOPPLPW

W tytule przelewu prosimy podać numery dokumentów.

📎 W załączeniu przesyłamy szczegółowe zestawienie zaległych faktur.

⚠️ Brak płatności może skutkować wstrzymaniem dostaw i naliczeniem odsetek ustawowych.

W razie problemów z płatnością prosimy o natychmiastowy kontakt:
📞 Tel: +48 46 874 71 70
📧 Email: kasa@piorkowscy.com.pl
🕐 Godziny: Pn-Pt 8:00-16:00

Z poważaniem,
Dział Księgowości
Ubojnia Drobiu ""Piórkowscy""
www.piorkowscy.com.pl";
            }

            txtTresc.Text = tresc;
        }

        private void BtnWybierzPDF_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "Pliki PDF (*.pdf)|*.pdf",
                Title = "Wybierz plik PDF do załączenia"
            };

            if (openDialog.ShowDialog() == true)
            {
                sciezkaPDF = openDialog.FileName;
                txtZalacznik.Text = System.IO.Path.GetFileName(sciezkaPDF);
            }
        }

        private void BtnWyslij_Click(object sender, RoutedEventArgs e)
        {
            // Walidacja
            if (string.IsNullOrWhiteSpace(txtEmailOdbiorcy.Text))
            {
                MessageBox.Show("Podaj adres email odbiorcy!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtTemat.Text))
            {
                MessageBox.Show("Podaj temat wiadomości!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtTresc.Text))
            {
                MessageBox.Show("Podaj treść wiadomości!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var result = MessageBox.Show(
                    $"Czy na pewno chcesz wysłać email do:\n{txtEmailOdbiorcy.Text}?\n\n" +
                    $"Temat: {txtTemat.Text}\n" +
                    $"Załącznik: {(string.IsNullOrEmpty(sciezkaPDF) ? "Brak" : System.IO.Path.GetFileName(sciezkaPDF))}",
                    "Potwierdzenie wysyłki",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    WyslijEmail();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wysyłania emaila:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WyslijEmail()
        {
            // UWAGA: Musisz skonfigurować swój serwer SMTP
            // To jest przykład - dostosuj do swojej konfiguracji

            string smtpServer = "smtp.twojserwer.pl";
            int smtpPort = 587;
            string smtpUser = "kasa@piorkowscy.com.pl";
            string smtpPassword = "twoje_haslo";

            using (var mail = new MailMessage())
            {
                mail.From = new MailAddress(smtpUser, "Ubojnia Drobiu Piórkowscy");
                mail.To.Add(txtEmailOdbiorcy.Text);
                mail.Subject = txtTemat.Text;
                mail.Body = txtTresc.Text;
                mail.IsBodyHtml = false;

                // Dodaj załącznik jeśli istnieje
                if (!string.IsNullOrEmpty(sciezkaPDF) && System.IO.File.Exists(sciezkaPDF))
                {
                    mail.Attachments.Add(new Attachment(sciezkaPDF));
                }

                using (var smtp = new SmtpClient(smtpServer, smtpPort))
                {
                    smtp.Credentials = new NetworkCredential(smtpUser, smtpPassword);
                    smtp.EnableSsl = true;

                    smtp.Send(mail);
                }
            }

            MessageBox.Show("✓ Email został wysłany pomyślnie!", "Sukces",
                MessageBoxButton.OK, MessageBoxImage.Information);

            this.DialogResult = true;
            this.Close();
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}