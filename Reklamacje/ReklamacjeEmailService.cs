using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace Kalendarz1.Reklamacje
{
    /// <summary>
    /// Serwis do wysyłania powiadomień email o reklamacjach
    /// </summary>
    public class ReklamacjeEmailService
    {
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;
        private readonly string _fromName;

        // Email do powiadomień o reklamacjach
        private readonly string _reklamacjeEmail;

        public ReklamacjeEmailService()
        {
            // Domyślne ustawienia - skonfiguruj przed użyciem
            _smtpHost = "smtp.gmail.com";
            _smtpPort = 587;
            _smtpUser = "";
            _smtpPassword = "";
            _fromEmail = "reklamacje@piorkowscy.pl";
            _fromName = "System Reklamacji - Piórkowscy";
            _reklamacjeEmail = "reklamacje@piorkowscy.pl";
        }

        public ReklamacjeEmailService(string smtpHost, int smtpPort, string smtpUser, string smtpPassword,
            string fromEmail, string fromName, string reklamacjeEmail)
        {
            _smtpHost = smtpHost;
            _smtpPort = smtpPort;
            _smtpUser = smtpUser;
            _smtpPassword = smtpPassword;
            _fromEmail = fromEmail;
            _fromName = fromName;
            _reklamacjeEmail = reklamacjeEmail;
        }

        /// <summary>
        /// Wysyła powiadomienie o nowej reklamacji
        /// </summary>
        public async Task<EmailResult> WyslijPowiadomienieNowaReklamacja(
            int idReklamacji,
            string nrDokumentu,
            string kontrahent,
            string opis,
            decimal sumaKg,
            string zglosil)
        {
            try
            {
                string subject = $"[NOWA REKLAMACJA] #{idReklamacji} - {kontrahent}";

                string body = $@"
NOWA REKLAMACJA W SYSTEMIE
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

ID Reklamacji:    #{idReklamacji}
Data zgłoszenia:  {DateTime.Now:dd.MM.yyyy HH:mm}
Zgłosił:          {zglosil}

KONTRAHENT:       {kontrahent}
Nr dokumentu:     {nrDokumentu}
Suma kg:          {sumaKg:N2} kg

OPIS PROBLEMU:
{opis}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Proszę o jak najszybsze rozpatrzenie reklamacji.
Zaloguj się do systemu, aby zobaczyć szczegóły i zmienić status.

---
Wiadomość wygenerowana automatycznie przez System Reklamacji
";

                return await WyslijEmail(_reklamacjeEmail, subject, body);
            }
            catch (Exception ex)
            {
                return new EmailResult { Success = false, Message = $"Błąd: {ex.Message}" };
            }
        }

        /// <summary>
        /// Wysyła powiadomienie o zmianie statusu reklamacji
        /// </summary>
        public async Task<EmailResult> WyslijPowiadomienieZmianaStatusu(
            int idReklamacji,
            string kontrahent,
            string statusPoprzedni,
            string statusNowy,
            string zmienilUzytkownik,
            string komentarz,
            string rozwiazanie = null)
        {
            try
            {
                string subject = $"[ZMIANA STATUSU] Reklamacja #{idReklamacji}: {statusPoprzedni} → {statusNowy}";

                string rozwiazanieText = "";
                if (!string.IsNullOrWhiteSpace(rozwiazanie))
                {
                    rozwiazanieText = $@"

ROZWIĄZANIE / UZASADNIENIE:
{rozwiazanie}
";
                }

                string body = $@"
ZMIANA STATUSU REKLAMACJI
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

ID Reklamacji:    #{idReklamacji}
Kontrahent:       {kontrahent}

ZMIANA STATUSU:
  {statusPoprzedni}  →  {statusNowy}

Data zmiany:      {DateTime.Now:dd.MM.yyyy HH:mm}
Zmienił:          {zmienilUzytkownik}

KOMENTARZ:
{komentarz}
{rozwiazanieText}
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

---
Wiadomość wygenerowana automatycznie przez System Reklamacji
";

                return await WyslijEmail(_reklamacjeEmail, subject, body);
            }
            catch (Exception ex)
            {
                return new EmailResult { Success = false, Message = $"Błąd: {ex.Message}" };
            }
        }

        /// <summary>
        /// Wysyła raport PDF reklamacji
        /// </summary>
        public async Task<EmailResult> WyslijRaportReklamacji(
            string toEmail,
            int idReklamacji,
            string kontrahent,
            string pdfPath)
        {
            try
            {
                if (string.IsNullOrEmpty(toEmail))
                    return new EmailResult { Success = false, Message = "Brak adresu email" };

                if (!File.Exists(pdfPath))
                    return new EmailResult { Success = false, Message = "Plik raportu nie istnieje" };

                string subject = $"Raport reklamacji #{idReklamacji} - {kontrahent}";

                string body = $@"
Szanowni Państwo,

W załączeniu przesyłamy raport reklamacji #{idReklamacji} dla kontrahenta {kontrahent}.

Raport został wygenerowany automatycznie z systemu zarządzania reklamacjami.
Plik w załączeniu można otworzyć w przeglądarce internetowej i wydrukować do PDF.

W razie pytań prosimy o kontakt.

Z poważaniem,
Dział Reklamacji
Ubojnia Drobiu ""Piórkowscy""
Koziołki 40, 95-061 Dmosin

---
Wiadomość wygenerowana automatycznie przez System Reklamacji
";

                return await WyslijEmail(toEmail, subject, body, pdfPath);
            }
            catch (Exception ex)
            {
                return new EmailResult { Success = false, Message = $"Błąd: {ex.Message}" };
            }
        }

        /// <summary>
        /// Wysyła email na podany adres
        /// </summary>
        private async Task<EmailResult> WyslijEmail(string toEmail, string subject, string body, string attachmentPath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(toEmail))
                    return new EmailResult { Success = false, Message = "Brak adresu email" };

                // Sprawdź konfigurację
                if (!IsConfigured())
                    return new EmailResult { Success = false, Message = "Serwer SMTP nie jest skonfigurowany. Skontaktuj się z administratorem." };

                using (var message = new MailMessage())
                {
                    message.From = new MailAddress(_fromEmail, _fromName);
                    message.To.Add(new MailAddress(toEmail));
                    message.Subject = subject;
                    message.Body = body;
                    message.IsBodyHtml = false;

                    if (!string.IsNullOrEmpty(attachmentPath) && File.Exists(attachmentPath))
                    {
                        var attachment = new Attachment(attachmentPath);
                        message.Attachments.Add(attachment);
                    }

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
        /// Sprawdza czy serwis jest skonfigurowany
        /// </summary>
        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(_smtpHost) &&
                   !string.IsNullOrEmpty(_smtpUser) &&
                   !string.IsNullOrEmpty(_smtpPassword);
        }
    }

    /// <summary>
    /// Wynik wysyłki email
    /// </summary>
    public class EmailResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}
