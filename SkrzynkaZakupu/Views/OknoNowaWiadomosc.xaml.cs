using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Kalendarz1.SkrzynkaZakupu.Models;
using Kalendarz1.SkrzynkaZakupu.Services;

namespace Kalendarz1.SkrzynkaZakupu.Views
{
    public partial class OknoNowaWiadomosc : Window
    {
        private readonly MailAccountSettings _cfg;
        private readonly List<MailAttachmentModel> _zalaczniki = new();
        private string? _inReplyTo;
        private readonly MailContactsService _kontakty = new();
        private EmailAutoComplete _acTo = null!;
        private EmailAutoComplete _acCc = null!;
        private bool _trybUstawiony;

        private string SigBlock() => string.IsNullOrWhiteSpace(_cfg.Signature) ? "" : "\n-- \n" + _cfg.Signature + "\n";

        public OknoNowaWiadomosc(MailAccountSettings cfg)
        {
            InitializeComponent();
            _cfg = cfg;
            Loaded += OknoNowaWiadomosc_Loaded;
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter &&
                    (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
                {
                    if (BtnWyslij.IsEnabled) BtnWyslij_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
            };
        }

        private async void OknoNowaWiadomosc_Loaded(object sender, RoutedEventArgs e)
        {
            _acTo = new EmailAutoComplete(TxtTo);
            _acCc = new EmailAutoComplete(TxtCc);
            var lista = await _kontakty.GetAllAsync();
            _acTo.SetSource(lista);
            _acCc.SetSource(lista);

            // świeża wiadomość (bez trybu reply/forward) — wstaw sam podpis
            if (!_trybUstawiony && string.IsNullOrEmpty(TxtBody.Text))
            {
                TxtBody.Text = "\n\n" + SigBlock();
                TxtBody.CaretIndex = 0;
            }
            if (string.IsNullOrEmpty(TxtTo.Text)) TxtTo.Focus();
        }

        /// <summary>Ustawia okno w trybie Odpowiedz / Przekaż na podstawie wiadomości źródłowej.</summary>
        public void UstawTryb(string tryb, MailBodyModel src)
        {
            _trybUstawiony = true;
            string cytat = ZbudujCytat(src);
            string telo = "\n\n" + SigBlock() + "\n" + cytat;

            if (tryb == "reply" || tryb == "replyall")
            {
                bool wszystkim = tryb == "replyall";
                Title = wszystkim ? "Odpowiedz wszystkim" : "Odpowiedz";
                TxtNaglowek.Text = wszystkim ? "↩️↩️  Odpowiedz wszystkim" : "↩️  Odpowiedz";
                TxtTo.Text = src.FromEmail;
                if (wszystkim) TxtCc.Text = ZbudujDw(src);
                TxtSubject.Text = src.Subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase) ? src.Subject : "Re: " + src.Subject;
                TxtBody.Text = telo;
                _inReplyTo = string.IsNullOrEmpty(src.MessageId) ? null : src.MessageId;
                TxtBody.CaretIndex = 0;
            }
            else if (tryb == "forward")
            {
                Title = "Przekaż";
                TxtNaglowek.Text = "➡️  Przekaż";
                TxtSubject.Text = src.Subject.StartsWith("Fwd:", StringComparison.OrdinalIgnoreCase) ? src.Subject : "Fwd: " + src.Subject;
                TxtBody.Text = telo;
                foreach (var a in src.Attachments) _zalaczniki.Add(a);
                OdswiezZalaczniki();
                TxtBody.CaretIndex = 0;
            }
        }

        /// <summary>DW dla „odpowiedz wszystkim": wszyscy z To+Cc oprócz nas i nadawcy.</summary>
        private string ZbudujDw(MailBodyModel src)
        {
            var wykluczone = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { _cfg.Email, src.FromEmail };
            var adresy = (src.To + "," + src.Cc)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => MailContactsService.CzyPoprawnyEmail(s) && !wykluczone.Contains(s))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            return string.Join(", ", adresy);
        }

        private static string ZbudujCytat(MailBodyModel src)
        {
            var tresc = !string.IsNullOrWhiteSpace(src.TextBody)
                ? src.TextBody
                : System.Text.RegularExpressions.Regex.Replace(src.HtmlBody ?? "", "<[^>]+>", " ");
            var sb = new StringBuilder();
            sb.AppendLine("---------- Wiadomość oryginalna ----------");
            sb.AppendLine($"Od: {src.From} <{src.FromEmail}>");
            sb.AppendLine($"Data: {src.Date:dd.MM.yyyy HH:mm}");
            sb.AppendLine($"Temat: {src.Subject}");
            sb.AppendLine();
            foreach (var line in (tresc ?? "").Split('\n'))
                sb.AppendLine("> " + line.TrimEnd());
            return sb.ToString();
        }

        private void BtnZalacznik_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Multiselect = true };
            if (dlg.ShowDialog() == true)
            {
                foreach (var path in dlg.FileNames)
                {
                    try
                    {
                        _zalaczniki.Add(new MailAttachmentModel
                        {
                            FileName = Path.GetFileName(path),
                            Content = File.ReadAllBytes(path),
                            Size = new FileInfo(path).Length,
                            ContentType = "application/octet-stream"
                        });
                    }
                    catch { /* pomiń niedostępny plik */ }
                }
                OdswiezZalaczniki();
            }
        }

        private void OdswiezZalaczniki()
        {
            TxtZalaczniki.Text = _zalaczniki.Count == 0
                ? ""
                : string.Join(", ", _zalaczniki.Select(z => $"{z.FileName} ({z.SizeLabel})"));
        }

        private async void BtnWyslij_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtTo.Text))
            {
                TxtStatus.Text = "Podaj adresata.";
                return;
            }
            BtnWyslij.IsEnabled = false;
            TxtStatus.Foreground = System.Windows.Media.Brushes.Gray;
            TxtStatus.Text = "Wysyłam…";

            var smtp = new SmtpMailService(_cfg);
            var (ok, err) = await smtp.SendAsync(new SmtpMailService.WyslijRequest
            {
                To = TxtTo.Text,
                Cc = TxtCc.Text,
                Subject = TxtSubject.Text,
                Body = TxtBody.Text,
                IsHtml = false,
                InReplyTo = _inReplyTo,
                Attachments = _zalaczniki
            });

            if (ok)
            {
                // zapamiętaj adresatów do podpowiedzi na przyszłość
                try
                {
                    var adresaci = (TxtTo.Text + "," + TxtCc.Text)
                        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(MailContactsService.CzyPoprawnyEmail)
                        .Select(s => new MailContact { Email = s });
                    await _kontakty.UpsertManyAsync(adresaci, "send");
                }
                catch { }

                MessageBox.Show("Wiadomość wysłana.", "Skrzynka Zakupu", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            else
            {
                TxtStatus.Foreground = System.Windows.Media.Brushes.DarkRed;
                TxtStatus.Text = "Błąd wysyłki: " + err;
                BtnWyslij.IsEnabled = true;
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
