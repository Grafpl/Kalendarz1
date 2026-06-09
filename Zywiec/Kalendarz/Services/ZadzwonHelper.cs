using System.Threading.Tasks;
using System.Windows;

namespace Kalendarz1.Zywiec.Kalendarz.Services
{
    // Wspólny helper "zadzwoń do hodowcy/dostawcy" używany w obu modułach
    // (Cykle Wstawień + Kalendarz Dostaw).
    //
    // 1. Pobiera Phone1 / Phone2 z dbo.Dostawcy
    // 2. Normalizuje numer do E.164 (+48xxxxxxxxx)
    // 3. Pokazuje MessageBox z potwierdzeniem
    // 4. Wysyła POST /call do MacroDroid przez wspólny klient
    // 5. Zwraca informację o wyniku (do toastu w UI)
    //
    // Nic NIE zapisuje w bazie (zgodnie ze specyfikacją).
    public static class ZadzwonHelper
    {
        public sealed class Wynik
        {
            public bool Sukces { get; init; }
            public string Komunikat { get; init; } = "";
            public bool BrakNumeru { get; init; }
            public bool TelefonNieSkonfigurowany { get; init; }
            public bool UserAnulowal { get; init; }
        }

        // Dzwoni do dostawcy/hodowcy. Jeśli ownerWindow podany — MessageBox modal.
        // `zrodlo` — etykieta źródła (np. "Kalendarz Dostaw", "Cykle Wstawien") do CallLog.
        public static async Task<Wynik> ZadzwonDoDostawcyAsync(
            Window? ownerWindow,
            string connectionString,
            string? userId,
            string dostawcaNazwa,
            string? telefonZRekordu = null,
            string zrodlo = "Kalendarz Dostaw")
        {
            // Sprawdź konfigurację telefonu
            if (!MacroDroidClient.CzyTelefonSkonfigurowany(userId))
            {
                return new Wynik
                {
                    Sukces = false,
                    TelefonNieSkonfigurowany = true,
                    Komunikat = "Najpierw skonfiguruj telefon (Kalendarz dostaw → PPM → 🧪 TEST SMS przez telefon)"
                };
            }

            // Pobierz numer — preferuj ten z rekordu (jeśli podano), inaczej z bazy
            string? rawNumer = telefonZRekordu;
            if (string.IsNullOrWhiteSpace(rawNumer) || rawNumer == "-")
            {
                var (raw, _) = await MacroDroidClient.PobierzNumerDostawcyAsync(connectionString, dostawcaNazwa);
                rawNumer = raw;
            }

            string? numerNorm = MacroDroidClient.NormalizujNumer(rawNumer);
            if (string.IsNullOrEmpty(numerNorm))
            {
                // Brak numeru — zaproponuj dodanie teraz przez OknoDodaniaNumeruDialog
                string? nowyNumer = await DodanieNumeruHelper.ZaproponujDodanieAsync(
                    ownerWindow, connectionString, dostawcaNazwa);
                if (string.IsNullOrEmpty(nowyNumer))
                {
                    // User anulował lub nie wpisał
                    return new Wynik
                    {
                        Sukces = false,
                        BrakNumeru = true,
                        Komunikat = $"Brak poprawnego numeru telefonu dla {dostawcaNazwa}"
                    };
                }
                numerNorm = MacroDroidClient.NormalizujNumer(nowyNumer);
                if (string.IsNullOrEmpty(numerNorm))
                {
                    return new Wynik
                    {
                        Sukces = false,
                        BrakNumeru = true,
                        Komunikat = $"Numer \"{nowyNumer}\" nie ma poprawnego formatu (potrzeba ≥ 9 cyfr)"
                    };
                }
            }

            // BEZ POTWIERDZENIA — klik Zadzwoń = od razu dzwoni.
            // Anulować można w samym telefonie (przycisk rozłącz na Androidzie).

            // Wyślij POST /call
            var wynik = await MacroDroidClient.ZadzwonAsync(userId, numerNorm);

            // Zapisz wpis w CallLog (fire-and-forget — błąd zapisu nie blokuje akcji)
            _ = CallLogService.ZapiszAsync(
                connectionString, dostawcaNazwa, numerNorm, userId ?? "", zrodlo,
                wynik.Sukces, wynik.StatusKod, wynik.Sukces ? null : wynik.Komunikat);

            return new Wynik
            {
                Sukces = wynik.Sukces,
                Komunikat = wynik.Sukces
                    ? $"📞 Telefon dzwoni do {dostawcaNazwa}"
                    : wynik.Komunikat
            };
        }
    }
}
