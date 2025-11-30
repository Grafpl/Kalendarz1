using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace Kalendarz1.Services
{
    /// <summary>
    /// Serwis do wysyłki emaili z załącznikami PDF
    /// </summary>
    public class EmailService
    {
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public EmailService()
        {
            // Domyślne ustawienia - można zmienić w konfiguracji
            _smtpHost = Properties.Settings.Default.SmtpHost ?? "smtp.gmail.com";
            _smtpPort = Properties.Settings.Default.SmtpPort > 0 ? Properties.Settings.Default.SmtpPort : 587;
            _smtpUser = Properties.Settings.Default.SmtpUser ?? "";
            _smtpPassword = Properties.Settings.Default.SmtpPassword ?? "";
            _fromEmail = Properties.Settings.Default.SmtpFromEmail ?? "rozliczenia@piorkowscy.pl";
            _fromName = Properties.Settings.Default.SmtpFromName ?? "Ubojnia Drobiu Piórkowscy";
        }

        public EmailService(string smtpHost, int smtpPort, string smtpUser, string smtpPassword, string fromEmail, string fromName)
        {
            _smtpHost = smtpHost;
            _smtpPort = smtpPort;
            _smtpUser = smtpUser;
            _smtpPassword = smtpPassword;
            _fromEmail = fromEmail;
            _fromName = fromName;
        }

        /// <summary>
        /// Wysyła email z rozliczeniem do hodowcy
        /// </summary>
        public async Task<EmailResult> SendRozliczenieAsync(
            string toEmail,
            string hodowcaNazwa,
            DateTime dataUboju,
            decimal kwotaDoZaplaty,
            string pdfPath)
        {
            try
            {
                if (string.IsNullOrEmpty(toEmail))
                    return new EmailResult { Success = false, Message = "Brak adresu email hodowcy" };

                if (!File.Exists(pdfPath))
                    return new EmailResult { Success = false, Message = "Plik PDF nie istnieje" };

                string subject = $"Rozliczenie drobiu - {dataUboju:dd.MM.yyyy} - Piórkowscy";

                string body = $@"
Szanowny/a {hodowcaNazwa},

W załączeniu przesyłamy rozliczenie przyjętego drobiu z dnia {dataUboju:dd MMMM yyyy}.

PODSUMOWANIE:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Data uboju:        {dataUboju:dd.MM.yyyy}
Kwota do zapłaty:  {kwotaDoZaplaty:N2} zł
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Szczegółowe rozliczenie znajduje się w załączonym pliku PDF.

W razie pytań prosimy o kontakt:
📞 Tel: +48 XXX XXX XXX
📧 Email: rozliczenia@piorkowscy.pl

Z poważaniem,
Ubojnia Drobiu ""Piórkowscy""
Koziołki 40, 95-061 Dmosin

---
Ta wiadomość została wygenerowana automatycznie.
";

                using (var message = new MailMessage())
                {
                    message.From = new MailAddress(_fromEmail, _fromName);
                    message.To.Add(new MailAddress(toEmail));
                    message.Subject = subject;
                    message.Body = body;
                    message.IsBodyHtml = false;

                    // Dodaj załącznik PDF
                    var attachment = new Attachment(pdfPath);
                    attachment.Name = $"Rozliczenie_{dataUboju:yyyy-MM-dd}_{hodowcaNazwa.Replace(" ", "_")}.pdf";
                    message.Attachments.Add(attachment);

                    using (var client = new SmtpClient(_smtpHost, _smtpPort))
                    {
                        client.EnableSsl = true;
                        client.UseDefaultCredentials = false;
                        client.Credentials = new NetworkCredential(_smtpUser, _smtpPassword);
                        client.DeliveryMethod = SmtpDeliveryMethod.Network;

                        await client.SendMailAsync(message);
                    }
                }

                return new EmailResult
                {
                    Success = true,
                    Message = $"Email wysłany do {toEmail}"
                };
            }
            catch (Exception ex)
            {
                return new EmailResult
                {
                    Success = false,
                    Message = $"Błąd wysyłania: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Wysyła email z załącznikiem
        /// </summary>
        public async Task<EmailResult> SendEmailAsync(string toEmail, string subject, string body, string attachmentPath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(toEmail))
                    return new EmailResult { Success = false, Message = "Brak adresu email" };

                using (var message = new MailMessage())
                {
                    message.From = new MailAddress(_fromEmail, _fromName);
                    message.To.Add(new MailAddress(toEmail));
                    message.Subject = subject;
                    message.Body = body;
                    message.IsBodyHtml = false;

                    if (!string.IsNullOrEmpty(attachmentPath) && File.Exists(attachmentPath))
                    {
                        message.Attachments.Add(new Attachment(attachmentPath));
                    }

                    using (var client = new SmtpClient(_smtpHost, _smtpPort))
                    {
                        client.EnableSsl = true;
                        client.UseDefaultCredentials = false;
                        client.Credentials = new NetworkCredential(_smtpUser, _smtpPassword);

                        await client.SendMailAsync(message);
                    }
                }

                return new EmailResult { Success = true, Message = "Email wysłany pomyślnie" };
            }
            catch (Exception ex)
            {
                return new EmailResult { Success = false, Message = $"Błąd: {ex.Message}" };
            }
        }

        /// <summary>
        /// Sprawdza konfigurację SMTP
        /// </summary>
        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(_smtpHost) &&
                   !string.IsNullOrEmpty(_smtpUser) &&
                   !string.IsNullOrEmpty(_smtpPassword);
        }
    }

    public class EmailResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}
