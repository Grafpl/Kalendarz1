using Microsoft.Data.SqlClient;
using System;
using System.IO;
using System.Windows;

namespace Kalendarz1.Reklamacje
{
    /// <summary>
    /// Mapuje techniczne wyjatki na przyjazne komunikaty dla uzytkownika.
    /// Uzywaj Pokaz() zamiast MessageBox.Show(ex.Message) w blokach catch.
    /// </summary>
    public static class FriendlyError
    {
        /// <summary>
        /// Pokaz przyjazny komunikat bledu. Logs technical details to Debug.
        /// </summary>
        public static void Pokaz(Exception ex, string kontekst = null, Window owner = null)
        {
            string tytul = "Problem";
            string wiadomosc = Mapuj(ex, out tytul);

            if (!string.IsNullOrEmpty(kontekst))
                wiadomosc = $"{kontekst}\n\n{wiadomosc}";

            System.Diagnostics.Debug.WriteLine($"[FriendlyError] {kontekst}: {ex}");

            if (owner != null)
                MessageBox.Show(owner, wiadomosc, tytul, MessageBoxButton.OK, MessageBoxImage.Warning);
            else
                MessageBox.Show(wiadomosc, tytul, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// Zwroc przyjazny tekst bez pokazywania okna.
        /// </summary>
        public static string Tekst(Exception ex)
        {
            return Mapuj(ex, out _);
        }

        private static string Mapuj(Exception ex, out string tytul)
        {
            tytul = "Problem";
            if (ex == null) return "Nieznany problem.";

            // SqlException - najczestsze
            if (ex is SqlException sqlEx)
            {
                tytul = "Problem z baza danych";
                switch (sqlEx.Number)
                {
                    case 2:      // timeout/network
                    case 53:     // could not open connection
                    case 10060:  // timeout
                    case 10061:  // refused
                    case 11001:  // host not found
                        return "Nie mozna polaczyc sie z serwerem bazy danych.\n\n" +
                               "Sprawdz czy jestes polaczony z siecia firmy (VPN) i czy serwer Symfonia/LibraNet dziala.\n\n" +
                               "Jesli problem sie powtarza — skontaktuj sie z administratorem.";

                    case 18456:  // login failed
                        return "Blad logowania do bazy danych — konto uzywane przez program nie ma uprawnien.\n\n" +
                               "Skontaktuj sie z administratorem.";

                    case 208:    // invalid object name
                        return "Ta operacja odwoluje sie do nieistniejacej tabeli w bazie.\n\n" +
                               "Moze brakowac migracji — zamknij i otworz ponownie Panel Reklamacji.\n" +
                               $"(szczegol techniczny: {sqlEx.Message})";

                    case 207:    // invalid column name
                        return "Ta operacja odwoluje sie do nieistniejacej kolumny w bazie.\n\n" +
                               "Moze brakowac migracji — zamknij i otworz ponownie Panel Reklamacji.\n" +
                               $"(szczegol techniczny: {sqlEx.Message})";

                    case 547:    // FK constraint
                        return "Nie mozna wykonac operacji — istnieja powiazane rekordy, ktore trzeba najpierw usunac.\n\n" +
                               $"(szczegol: {sqlEx.Message})";

                    case 2627:   // unique constraint / PK
                    case 2601:
                        return "Taki rekord juz istnieje w bazie — nie mozna dodac duplikatu.";

                    case 229:    // permission denied
                    case 230:
                    case 262:
                        return "Brak uprawnien do wykonania tej operacji w bazie danych.\n\n" +
                               "Skontaktuj sie z administratorem.";

                    case 1205:   // deadlock
                        return "Baza jest chwilowo zajeta przez inna operacje (deadlock). Sprobuj jeszcze raz za chwile.";

                    default:
                        return $"Blad bazy danych (#{sqlEx.Number}).\n\n{SkrocTekst(sqlEx.Message, 400)}";
                }
            }

            // File/IO
            if (ex is FileNotFoundException fnfe)
            {
                tytul = "Brak pliku";
                return $"Nie znaleziono pliku:\n{fnfe.FileName}";
            }
            if (ex is DirectoryNotFoundException)
            {
                tytul = "Brak katalogu";
                return "Katalog nie istnieje.";
            }
            if (ex is UnauthorizedAccessException)
            {
                tytul = "Brak uprawnien";
                return "Brak uprawnien do tego pliku lub katalogu.\n\nSprawdz czy plik nie jest otwarty w innym programie.";
            }
            if (ex is IOException)
            {
                tytul = "Blad operacji na pliku";
                return $"Nie mozna wykonac operacji na pliku.\n\n{SkrocTekst(ex.Message, 300)}";
            }

            // Arg errors - prawdopodobnie bug
            if (ex is ArgumentNullException || ex is ArgumentException || ex is InvalidOperationException)
            {
                tytul = "Nieprawidlowe dane";
                return $"Operacja nie moze zostac wykonana z podanymi danymi.\n\n{SkrocTekst(ex.Message, 300)}";
            }

            // Timeout (generyczny)
            if (ex is TimeoutException)
            {
                tytul = "Przekroczono czas operacji";
                return "Operacja trwala za dlugo i zostala przerwana.\n\nSprobuj ponownie lub sprawdz polaczenie z sieciowe.";
            }

            // Fallback
            return SkrocTekst(ex.Message, 400);
        }

        private static string SkrocTekst(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Length <= max) return s;
            return s.Substring(0, max) + "...";
        }
    }
}
