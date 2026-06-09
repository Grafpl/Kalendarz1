using System;
using System.Threading;
using System.Threading.Tasks;
using Kalendarz1.SkrzynkaZakupu.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;

namespace Kalendarz1.SkrzynkaZakupu.Services
{
    /// <summary>
    /// Monitoruje skrzynkę odbiorczą przez IMAP IDLE na OSOBNYM połączeniu (jak Outlook/Thunderbird).
    /// Gdy przyjdzie nowa wiadomość — zgłasza zdarzenie NowaWiadomosc (poza wątkiem UI).
    /// Best-effort: przy zerwaniu łączy się ponownie; jeśli serwer nie wspiera IDLE — odpytuje co 60 s.
    /// </summary>
    public class ImapIdleMonitor
    {
        private readonly MailAccountSettings _cfg;
        private CancellationTokenSource? _cts;
        private Task? _loop;

        /// <summary>Zgłaszane gdy w INBOX pojawi się nowa wiadomość (NIE na wątku UI).</summary>
        public event Action? NowaWiadomosc;

        public ImapIdleMonitor(MailAccountSettings cfg) => _cfg = cfg;

        public void Start()
        {
            if (_loop != null) return;
            _cts = new CancellationTokenSource();
            _loop = Task.Run(() => PetlaAsync(_cts.Token));
        }

        public async Task StopAsync()
        {
            try { _cts?.Cancel(); } catch { }
            if (_loop != null) { try { await _loop; } catch { } }
            _loop = null;
            _cts?.Dispose();
            _cts = null;
        }

        private async Task PetlaAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var client = new ImapClient();
                    var secure = _cfg.ImapPort == 993 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
                    await client.ConnectAsync(_cfg.ImapHost, _cfg.ImapPort, secure, ct);
                    await client.AuthenticateAsync(_cfg.Login, _cfg.Password, ct);

                    var inbox = client.Inbox;
                    await inbox.OpenAsync(FolderAccess.ReadOnly, ct);
                    int last = inbox.Count;

                    inbox.CountChanged += (_, _) =>
                    {
                        if (inbox.Count > last) { last = inbox.Count; NowaWiadomosc?.Invoke(); }
                        else last = inbox.Count;
                    };

                    while (!ct.IsCancellationRequested && client.IsConnected)
                    {
                        if (client.Capabilities.HasFlag(ImapCapabilities.Idle))
                        {
                            // odświeżamy IDLE co ~9 min (serwery zrywają IDLE po ~30 min)
                            using var done = new CancellationTokenSource(TimeSpan.FromMinutes(9));
                            using var link = CancellationTokenSource.CreateLinkedTokenSource(ct, done.Token);
                            await client.IdleAsync(link.Token, ct);
                        }
                        else
                        {
                            await Task.Delay(TimeSpan.FromSeconds(60), ct);
                            await client.NoOpAsync(ct); // wymusza CountChanged
                        }
                    }

                    try { await client.DisconnectAsync(true, CancellationToken.None); } catch { }
                }
                catch (OperationCanceledException) { }
                catch { /* zerwane połączenie — reconnect po chwili */ }

                if (!ct.IsCancellationRequested)
                {
                    try { await Task.Delay(TimeSpan.FromSeconds(15), ct); } catch { }
                }
            }
        }
    }
}
