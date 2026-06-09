using System;
using System.Windows;
using Kalendarz1.SkrzynkaZakupu.Models;
using Kalendarz1.SkrzynkaZakupu.Services;

namespace Kalendarz1.SkrzynkaZakupu.Views
{
    public partial class HasloSkrzynkiDialog : Window
    {
        public MailAccountSettings Settings { get; private set; }

        public HasloSkrzynkiDialog()
        {
            InitializeComponent();
            Settings = MailSecretsStore.Load();
            TxtEmail.Text = Settings.Email;
            TxtPass.Password = Settings.Password;
            TxtImap.Text = Settings.ImapHost;
            TxtImapPort.Text = Settings.ImapPort.ToString();
            TxtSmtp.Text = Settings.SmtpHost;
            TxtSmtpPort.Text = Settings.SmtpPort.ToString();
            TxtPodpis.Text = Settings.Signature;
        }

        private MailAccountSettings Zbierz()
        {
            int.TryParse(TxtImapPort.Text, out int imapPort);
            int.TryParse(TxtSmtpPort.Text, out int smtpPort);
            return new MailAccountSettings
            {
                Email = TxtEmail.Text.Trim(),
                Login = TxtEmail.Text.Trim(),
                Password = TxtPass.Password,
                ImapHost = TxtImap.Text.Trim(),
                ImapPort = imapPort == 0 ? 993 : imapPort,
                SmtpHost = TxtSmtp.Text.Trim(),
                SmtpPort = smtpPort == 0 ? 587 : smtpPort,
                DisplayName = Settings.DisplayName,
                Signature = TxtPodpis.Text
            };
        }

        private async void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            TxtStatus.Foreground = System.Windows.Media.Brushes.Gray;
            TxtStatus.Text = "Łączę…";
            BtnTest.IsEnabled = false;
            try
            {
                var svc = new ImapMailService(Zbierz());
                var (ok, err) = await svc.TestAsync();
                if (ok)
                {
                    TxtStatus.Foreground = System.Windows.Media.Brushes.Green;
                    TxtStatus.Text = "✓ Połączenie OK";
                }
                else
                {
                    TxtStatus.Foreground = System.Windows.Media.Brushes.DarkRed;
                    TxtStatus.Text = "✗ " + err;
                }
            }
            finally
            {
                BtnTest.IsEnabled = true;
            }
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            var s = Zbierz();
            if (string.IsNullOrWhiteSpace(s.Email) || string.IsNullOrWhiteSpace(s.Password))
            {
                TxtStatus.Foreground = System.Windows.Media.Brushes.DarkRed;
                TxtStatus.Text = "Podaj adres e-mail i hasło.";
                return;
            }
            MailSecretsStore.Save(s);
            Settings = s;
            DialogResult = true;
            Close();
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
