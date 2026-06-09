using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kalendarz1.SkrzynkaZakupu.Models;
using MailKit.Net.Smtp;
using MimeKit;

namespace Kalendarz1.SkrzynkaZakupu.Services
{
    /// <summary>
    /// Wysyłka i odpowiadanie na maile przez SMTP dpoczta (MailKit).
    /// </summary>
    public class SmtpMailService
    {
        private readonly MailAccountSettings _cfg;

        public SmtpMailService(MailAccountSettings cfg) => _cfg = cfg;

        public class WyslijRequest
        {
            public string To { get; set; } = "";
            public string Cc { get; set; } = "";
            public string Subject { get; set; } = "";
            public string Body { get; set; } = "";
            public bool IsHtml { get; set; }
            public string? InReplyTo { get; set; }          // Message-Id wiadomości, na którą odpowiadamy
            public List<MailAttachmentModel> Attachments { get; set; } = new();
        }

        public async Task<(bool ok, string error)> SendAsync(WyslijRequest req, CancellationToken ct = default)
        {
            try
            {
                var msg = new MimeMessage();
                msg.From.Add(new MailboxAddress(_cfg.DisplayName, _cfg.Email));

                foreach (var a in SplitAddresses(req.To)) msg.To.Add(a);
                foreach (var a in SplitAddresses(req.Cc)) msg.Cc.Add(a);

                if (msg.To.Count == 0)
                    return (false, "Brak adresata.");

                msg.Subject = req.Subject ?? "";

                if (!string.IsNullOrEmpty(req.InReplyTo))
                {
                    msg.InReplyTo = req.InReplyTo;
                    msg.References.Add(req.InReplyTo);
                }

                var builder = new BodyBuilder();
                if (req.IsHtml) builder.HtmlBody = req.Body;
                else builder.TextBody = req.Body;

                foreach (var att in req.Attachments)
                    builder.Attachments.Add(att.FileName, att.Content,
                        ContentType.Parse(string.IsNullOrWhiteSpace(att.ContentType) ? "application/octet-stream" : att.ContentType));

                msg.Body = builder.ToMessageBody();

                using var client = new SmtpClient();
                var secure = _cfg.SmtpPort == 465
                    ? MailKit.Security.SecureSocketOptions.SslOnConnect
                    : MailKit.Security.SecureSocketOptions.StartTls;
                await client.ConnectAsync(_cfg.SmtpHost, _cfg.SmtpPort, secure, ct);
                await client.AuthenticateAsync(_cfg.Login, _cfg.Password, ct);
                await client.SendAsync(msg, ct);
                await client.DisconnectAsync(true, ct);
                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static IEnumerable<MailboxAddress> SplitAddresses(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) yield break;
            foreach (var part in raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var s = part.Trim();
                if (s.Length == 0) continue;
                if (MailboxAddress.TryParse(s, out var addr))
                    yield return addr;
            }
        }
    }
}
