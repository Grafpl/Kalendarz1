using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Kalendarz1.Services
{
    /// <summary>
    /// Singleton — polluje co 30s tabelę PowiadomieniaZamowienPoGodzinie i pokazuje popup w prawym dolnym rogu.
    /// Start: wywołać Start() w Menu.cs po loginie. Stop: Stop() przy wylogowaniu/exit.
    /// </summary>
    public static class PowiadomieniaPollingService
    {
        private static DispatcherTimer? _timer;
        private static int _lastSeenId;
        private static bool _running;
        private static string _userId = "";

        // Cap żeby na starcie nie zalewać user'a 50 popupami
        private const int MAX_POPUPS_PER_TICK = 3;

        public static void Start(string userId)
        {
            if (_running) return;
            _userId = userId ?? "";
            _running = true;

            // Najpierw policz max Id w bazie → start "od teraz" (nie pokazuj historii)
            _ = InitializeLastIdAsync();

            _timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _timer.Tick += async (s, e) => await PollAsync();
            _timer.Start();
        }

        public static void Stop()
        {
            _timer?.Stop();
            _timer = null;
            _running = false;
        }

        private static async Task InitializeLastIdAsync()
        {
            try
            {
                await PowiadomieniaZamowienService.EnsureSchemaAsync();
                await using var cn = new Microsoft.Data.SqlClient.SqlConnection(
                    "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True");
                await cn.OpenAsync();
                await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                    "SELECT ISNULL(MAX(Id), 0) FROM dbo.PowiadomieniaZamowienPoGodzinie", cn);
                var r = await cmd.ExecuteScalarAsync();
                _lastSeenId = r == null || r == DBNull.Value ? 0 : Convert.ToInt32(r);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Polling init] {ex.Message}"); }
        }

        private static async Task PollAsync()
        {
            try
            {
                var nowe = await PowiadomieniaZamowienService.PollNewAsync(_lastSeenId, _userId);
                if (nowe.Count == 0) return;

                _lastSeenId = nowe.Max(n => n.Id);

                var settings = ZmianyZamowienSettingsService.GetSettingsCached();
                bool checkAccess = !string.IsNullOrWhiteSpace(settings.KafelkiDocelowe);

                if (checkAccess && !UserMaDostepDoKafelka(settings.KafelkiDocelowe))
                {
                    foreach (var n in nowe)
                        await PowiadomieniaZamowienService.MarkReadAsync(n.Id, _userId);
                    return;
                }

                // ✅ Auto-grupowanie: zmiany od tego samego usera dla tego samego zamówienia → 1 popup zbiorczy
                var grupy = nowe
                    .GroupBy(n => new { n.UtworzonoPrzez, n.ZamowienieId })
                    .ToList();

                int shown = 0;
                foreach (var grp in grupy)
                {
                    if (shown >= MAX_POPUPS_PER_TICK) break;

                    var lista = grp.OrderBy(r => r.Id).ToList();
                    try
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                // 1 zmiana → klasyczny popup. >=2 zmian → zbiorczy popup z listą.
                                var popup = lista.Count == 1
                                    ? new PowiadomieniaZamowienPopup(lista[0])
                                    : new PowiadomieniaZamowienPopup(lista);
                                popup.Show();
                            }
                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Popup show] {ex.Message}"); }
                        }));
                        // MarkRead wszystkie rekordy z tej grupy
                        foreach (var rec in lista)
                            await PowiadomieniaZamowienService.MarkReadAsync(rec.Id, _userId);
                        shown++;
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Polling tick] {ex.Message}"); }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Polling] {ex.Message}"); }
        }

        private static bool UserMaDostepDoKafelka(string kafelkiCsv)
        {
            // Sergiusz / Admin — zawsze dostęp
            try
            {
                if (App.UserID == "admin" || App.UserID == "sergiusz") return true;
                // Brak implementacji userPermissions tutaj — domyślnie true (popup pokaże się, można dopracować)
                // TODO: sprawdzić accessMap + permissions usera
                return true;
            }
            catch { return true; }
        }
    }
}
