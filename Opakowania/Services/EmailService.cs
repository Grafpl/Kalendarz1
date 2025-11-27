using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Kalendarz1.Opakowania.Models;

namespace Kalendarz1.Opakowania.Services
{
    /// <summary>
    /// Serwis wysyłania emaili z raportami opakowań
    /// </summary>
    public class EmailService
    {
        // Konfiguracja SMTP - dostosuj do swojego serwera
        private readonly string _smtpServer = "smtp.yourdomain.com";
        private readonly int _smtpPort = 587;
        private readonly string _smtpUser = "opakowania@pronova.pl";
        private readonly string _smtpPassword = "your_password";
        private readonly string _fromEmail = "opakowania@pronova.pl";
        private readonly string _fromName = "PRONOVA - Dział Opakowań";
        private readonly bool _enableSsl = true;

        /// <summary>
        /// Wysyła email z potwierdzeniem salda
        /// </summary>
        public async Task<bool> WyslijPotwierdzenieSaldaAsync(
            string emailOdbiorcy,
            string nazwaKontrahenta,
            TypOpakowania typOpakowania,
            int saldo,
            DateTime dataSalda,
            string sciezkaZalacznika = null)
        {
            try
            {
                var subject = $"Potwierdzenie salda opakowań - {typOpakowania.Nazwa}";
                var body = GenerujTrescPotwierdzenia(nazwaKontrahenta, typOpakowania, saldo, dataSalda);

                var attachments = new List<string>();
                if (!string.IsNullOrEmpty(sciezkaZalacznika) && File.Exists(sciezkaZalacznika))
                {
                    attachments.Add(sciezkaZalacznika);
                }

                return await WyslijEmailAsync(emailOdbiorcy, subject, body, attachments);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd wysyłania emaila: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Wysyła zestawienie sald do wielu odbiorców
        /// </summary>
        public async Task<Dictionary<string, bool>> WyslijZestawienieSaldAsync(
            Dictionary<string, string> odbiorcyEmails, // Kontrahent -> Email
            IEnumerable<ZestawienieSalda> zestawienie,
            TypOpakowania typOpakowania,
            DateTime dataOd,
            DateTime dataDo,
            string sciezkaPdf)
        {
            var wyniki = new Dictionary<string, bool>();

            foreach (var odbiorca in odbiorcyEmails)
            {
                try
                {
                    var subject = $"Zestawienie sald opakowań {typOpakowania.Nazwa} za okres {dataOd:dd.MM} - {dataDo:dd.MM.yyyy}";
                    var body = GenerujTrescZestawienia(odbiorca.Key, typOpakowania, dataOd, dataDo);

                    var attachments = new List<string>();
                    if (!string.IsNullOrEmpty(sciezkaPdf) && File.Exists(sciezkaPdf))
                    {
                        attachments.Add(sciezkaPdf);
                    }

                    var success = await WyslijEmailAsync(odbiorca.Value, subject, body, attachments);
                    wyniki[odbiorca.Key] = success;
                }
                catch (Exception)
                {
                    wyniki[odbiorca.Key] = false;
                }
            }

            return wyniki;
        }

        /// <summary>
        /// Wysyła raport kontrahenta
        /// </summary>
        public async Task<bool> WyslijRaportKontrahentaAsync(
            string emailOdbiorcy,
            string nazwaKontrahenta,
            DateTime dataOd,
            DateTime dataDo,
            string sciezkaPdf)
        {
            try
            {
                var subject = $"Raport salda opakowań - {nazwaKontrahenta}";
                var body = GenerujTrescRaportu(nazwaKontrahenta, dataOd, dataDo);

                var attachments = new List<string>();
                if (!string.IsNullOrEmpty(sciezkaPdf) && File.Exists(sciezkaPdf))
                {
                    attachments.Add(sciezkaPdf);
                }

                return await WyslijEmailAsync(emailOdbiorcy, subject, body, attachments);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd wysyłania emaila: {ex.Message}");
                return false;
            }
        }

        #region Wysyłka Email

        private async Task<bool> WyslijEmailAsync(
            string emailOdbiorcy,
            string subject,
            string body,
            List<string> attachments = null)
        {
            try
            {
                using (var message = new MailMessage())
                {
                    message.From = new MailAddress(_fromEmail, _fromName);
                    message.To.Add(new MailAddress(emailOdbiorcy));
                    message.Subject = subject;
                    message.Body = body;
                    message.IsBodyHtml = true;
                    message.BodyEncoding = Encoding.UTF8;
                    message.SubjectEncoding = Encoding.UTF8;

                    // Dodaj załączniki
                    if (attachments != null)
                    {
                        foreach (var attachmentPath in attachments)
                        {
                            if (File.Exists(attachmentPath))
                            {
                                message.Attachments.Add(new Attachment(attachmentPath));
                            }
                        }
                    }

                    using (var client = new SmtpClient(_smtpServer, _smtpPort))
                    {
                        client.Credentials = new NetworkCredential(_smtpUser, _smtpPassword);
                        client.EnableSsl = _enableSsl;
                        client.Timeout = 30000; // 30 sekund

                        await client.SendMailAsync(message);
                    }
                }

                return true;
            }
            catch (SmtpException ex)
            {
                System.Diagnostics.Debug.WriteLine($"SMTP Error: {ex.StatusCode} - {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Email Error: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Generatory treści

        private string GenerujTrescPotwierdzenia(
            string nazwaKontrahenta,
            TypOpakowania typOpakowania,
            int saldo,
            DateTime dataSalda)
        {
            var saldoText = saldo == 0 ? "0" : (saldo > 0 ? $"+{saldo}" : saldo.ToString());
            var saldoColor = saldo > 0 ? "#CC2F37" : "#4B833C";
            var saldoOpis = saldo > 0
                ? "opakowań do zwrotu do PRONOVA"
                : (saldo < 0 ? "opakowań do odebrania z PRONOVA" : "brak należności");

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #4B833C, #2D5016); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #fff; padding: 30px; border: 1px solid #ddd; }}
        .saldo-box {{ background: #f8f8f8; border-radius: 8px; padding: 20px; text-align: center; margin: 20px 0; }}
        .saldo-value {{ font-size: 48px; font-weight: bold; color: {saldoColor}; }}
        .saldo-label {{ color: #666; margin-top: 10px; }}
        .footer {{ background: #f5f5f5; padding: 15px; text-align: center; font-size: 12px; color: #666; border-radius: 0 0 8px 8px; }}
        .btn {{ display: inline-block; background: #4B833C; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>PRONOVA SP. Z O.O.</h1>
            <p>Potwierdzenie salda opakowań</p>
        </div>
        <div class='content'>
            <p>Szanowni Państwo,</p>
            <p>Uprzejmie prosimy o potwierdzenie salda opakowań na dzień <strong>{dataSalda:dd.MM.yyyy}</strong>.</p>
            
            <div class='saldo-box'>
                <div><strong>Typ opakowania:</strong> {typOpakowania.Nazwa}</div>
                <div class='saldo-value'>{saldoText}</div>
                <div class='saldo-label'>{saldoOpis}</div>
            </div>
            
            <p>W przypadku zgodności prosimy o potwierdzenie poprzez odpowiedź na niniejszą wiadomość.</p>
            <p>W razie rozbieżności prosimy o kontakt z działem opakowań.</p>
            
            <p style='margin-top: 30px;'>Z poważaniem,<br/>
            <strong>Dział Opakowań</strong><br/>
            PRONOVA SP. Z O.O.</p>
        </div>
        <div class='footer'>
            <p>Ta wiadomość została wygenerowana automatycznie. Prosimy nie odpowiadać bezpośrednio na ten email.</p>
            <p>W przypadku pytań prosimy o kontakt: opakowania@pronova.pl</p>
        </div>
    </div>
</body>
</html>";
        }

        private string GenerujTrescZestawienia(
            string nazwaKontrahenta,
            TypOpakowania typOpakowania,
            DateTime dataOd,
            DateTime dataDo)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #4B833C, #2D5016); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #fff; padding: 30px; border: 1px solid #ddd; }}
        .info-box {{ background: #E8F5E9; border-left: 4px solid #4B833C; padding: 15px; margin: 20px 0; }}
        .footer {{ background: #f5f5f5; padding: 15px; text-align: center; font-size: 12px; color: #666; border-radius: 0 0 8px 8px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>PRONOVA SP. Z O.O.</h1>
            <p>Zestawienie sald opakowań</p>
        </div>
        <div class='content'>
            <p>Szanowni Państwo,</p>
            <p>W załączeniu przesyłamy zestawienie sald opakowań <strong>{typOpakowania.Nazwa}</strong> 
               za okres <strong>{dataOd:dd.MM.yyyy}</strong> - <strong>{dataDo:dd.MM.yyyy}</strong>.</p>
            
            <div class='info-box'>
                <strong>Kontrahent:</strong> {nazwaKontrahenta}<br/>
                <strong>Typ opakowania:</strong> {typOpakowania.Nazwa}<br/>
                <strong>Okres:</strong> {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}
            </div>
            
            <p>Prosimy o weryfikację załączonego dokumentu i potwierdzenie zgodności sald.</p>
            <p>W razie rozbieżności prosimy o niezwłoczny kontakt z działem opakowań.</p>
            
            <p style='margin-top: 30px;'>Z poważaniem,<br/>
            <strong>Dział Opakowań</strong><br/>
            PRONOVA SP. Z O.O.</p>
        </div>
        <div class='footer'>
            <p>Ta wiadomość została wygenerowana automatycznie.</p>
            <p>Kontakt: opakowania@pronova.pl | Tel: +48 XXX XXX XXX</p>
        </div>
    </div>
</body>
</html>";
        }

        private string GenerujTrescRaportu(
            string nazwaKontrahenta,
            DateTime dataOd,
            DateTime dataDo)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #4B833C, #2D5016); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #fff; padding: 30px; border: 1px solid #ddd; }}
        .info-box {{ background: #E8F5E9; border-left: 4px solid #4B833C; padding: 15px; margin: 20px 0; }}
        .footer {{ background: #f5f5f5; padding: 15px; text-align: center; font-size: 12px; color: #666; border-radius: 0 0 8px 8px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>PRONOVA SP. Z O.O.</h1>
            <p>Raport salda opakowań</p>
        </div>
        <div class='content'>
            <p>Szanowni Państwo,</p>
            <p>W załączeniu przesyłamy szczegółowy raport salda opakowań 
               za okres <strong>{dataOd:dd.MM.yyyy}</strong> - <strong>{dataDo:dd.MM.yyyy}</strong>.</p>
            
            <div class='info-box'>
                <strong>Kontrahent:</strong> {nazwaKontrahenta}<br/>
                <strong>Okres:</strong> {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}<br/>
                <strong>Data wygenerowania:</strong> {DateTime.Now:dd.MM.yyyy HH:mm}
            </div>
            
            <p>Raport zawiera:</p>
            <ul>
                <li>Aktualne salda wszystkich typów opakowań</li>
                <li>Historię dokumentów w wybranym okresie</li>
                <li>Historię potwierdzeń sald</li>
            </ul>
            
            <p>W razie pytań lub rozbieżności prosimy o kontakt z działem opakowań.</p>
            
            <p style='margin-top: 30px;'>Z poważaniem,<br/>
            <strong>Dział Opakowań</strong><br/>
            PRONOVA SP. Z O.O.</p>
        </div>
        <div class='footer'>
            <p>Ta wiadomość została wygenerowana automatycznie.</p>
            <p>Kontakt: opakowania@pronova.pl | Tel: +48 XXX XXX XXX</p>
        </div>
    </div>
</body>
</html>";
        }

        #endregion

        #region Konfiguracja

        /// <summary>
        /// Konfiguruje parametry SMTP
        /// </summary>
        public static EmailService CreateWithConfig(
            string smtpServer,
            int smtpPort,
            string smtpUser,
            string smtpPassword,
            string fromEmail,
            string fromName,
            bool enableSsl = true)
        {
            var service = new EmailService();

            // Użyj refleksji lub dodaj właściwości set do konfiguracji
            // Na razie zwracamy domyślny serwis
            return service;
        }

        #endregion
    }
}
