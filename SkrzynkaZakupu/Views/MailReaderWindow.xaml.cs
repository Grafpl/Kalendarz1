using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.SkrzynkaZakupu.Models;

namespace Kalendarz1.SkrzynkaZakupu.Views
{
    public partial class MailReaderWindow : Window
    {
        private readonly MailAccountSettings _cfg;
        private readonly MailBodyModel _body;
        private bool _webReady;

        public MailReaderWindow(MailBodyModel body, MailAccountSettings cfg)
        {
            InitializeComponent();
            _body = body;
            _cfg = cfg;

            Title = body.Subject;
            TxtTemat.Text = body.Subject;
            TxtOd.Text = string.IsNullOrWhiteSpace(body.From) ? body.FromEmail : body.From;
            TxtDo.Text = string.IsNullOrWhiteSpace(body.To) ? body.FromEmail : $"{body.FromEmail}   •   do: {body.To}";
            TxtData.Text = body.Date.ToString("dddd, dd MMMM yyyy, HH:mm");
            Inicjaly.Text = MailAvatar.Inicjaly(body.From, body.FromEmail);
            try
            {
                Avatar.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(MailAvatar.Kolor(body.FromEmail)));
            }
            catch { }
            LstZalaczniki.ItemsSource = body.Attachments;

            Loaded += async (_, _) =>
            {
                try { await WebBody.EnsureCoreWebView2Async(); _webReady = true; WebBody.NavigateToString(MailHtml.Full(_body)); }
                catch { }
            };
        }

        private void BtnReply_Click(object sender, RoutedEventArgs e) => Otworz("reply");
        private void BtnReplyAll_Click(object sender, RoutedEventArgs e) => Otworz("replyall");
        private void BtnForward_Click(object sender, RoutedEventArgs e) => Otworz("forward");

        private void Otworz(string tryb)
        {
            var okno = new OknoNowaWiadomosc(_cfg) { Owner = this };
            okno.UstawTryb(tryb, _body);
            okno.ShowDialog();
        }

        private async void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (_webReady && WebBody.CoreWebView2 != null)
                try { await WebBody.CoreWebView2.ExecuteScriptAsync("window.print();"); } catch { }
        }

        private void Zalacznik_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is MailAttachmentModel att)
            {
                var dlg = new Microsoft.Win32.SaveFileDialog { FileName = att.FileName };
                if (dlg.ShowDialog() == true)
                {
                    try { File.WriteAllBytes(dlg.FileName, att.Content); } catch { }
                }
            }
        }
    }
}
