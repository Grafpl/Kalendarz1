using Microsoft.Data.SqlClient;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Kalendarz1.Zywiec.Kalendarz.Services
{
    // Wspólny helper: gdy hodowca/dostawca nie ma zapisanego numeru telefonu,
    // proponuje dodanie go teraz (otwiera OknoDodaniaNumeruDialog) i zwraca
    // świeżo wpisany numer.
    //
    // Używane w:
    //  - ZadzwonHelper (jako fallback gdy numeru brak)
    //  - Menu SMS w Kalendarz Dostaw (Kopiuj SMS / Zapytanie)
    //  - Menu SMS w Cyklach Wstawień (8 wariantów + 2 potwierdzenia)
    public static class DodanieNumeruHelper
    {
        // Otwiera dialog z propozycją dodania numeru. Zwraca surowy Phone1
        // (jeszcze przed normalizacją do +48). Null gdy user anulował.
        public static async Task<string?> ZaproponujDodanieAsync(
            Window? owner,
            string connectionString,
            string dostawca)
        {
            if (string.IsNullOrWhiteSpace(dostawca)) return null;

            // Najpierw pobierz aktualne numery (mogą być Phone2/3 nawet jeśli Phone1 pusty)
            string p1 = "", p2 = "", p3 = "";
            try
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(
                    "SELECT TOP 1 ISNULL(Phone1,''), ISNULL(Phone2,''), ISNULL(Phone3,'') " +
                    "FROM dbo.Dostawcy WHERE ShortName=@n OR Name=@n", conn);
                cmd.Parameters.AddWithValue("@n", dostawca);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    p1 = r.GetString(0);
                    p2 = r.GetString(1);
                    p3 = r.GetString(2);
                }
            }
            catch { }

            // Pytanie do użytkownika
            var pytanie = MessageBox.Show(
                owner ?? Application.Current.MainWindow,
                $"⚠ Hodowca \"{dostawca}\" nie ma zapisanego numeru telefonu.\n\n" +
                $"Chcesz dodać numer teraz?",
                "Brak numeru telefonu",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (pytanie != MessageBoxResult.Yes) return null;

            // Otwórz istniejący dialog edycji numerów (Phone1/2/3)
            var dialog = new OknoDodaniaNumeruDialog(dostawca, p1, p2, p3);
            if (owner != null) dialog.Owner = owner;
            bool? wynik = dialog.ShowDialog();
            if (wynik != true) return null;

            // Zapisz do bazy
            string nowyP1 = dialog.NumerTelefonu ?? "";
            string nowyP2 = dialog.NumerTelefonu2 ?? "";
            string nowyP3 = dialog.NumerTelefonu3 ?? "";

            // Sprawdź czy w ogóle coś wprowadzono
            string wybrany = !string.IsNullOrWhiteSpace(nowyP1) && nowyP1 != "-"
                ? nowyP1
                : (!string.IsNullOrWhiteSpace(nowyP2) && nowyP2 != "-" ? nowyP2 : "");
            if (string.IsNullOrWhiteSpace(wybrany))
            {
                MessageBox.Show(owner, "Nie wprowadzono żadnego numeru.",
                    "Brak numeru", MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }

            // 🔍 WYKRYCIE DUPLIKATU — czy ten numer jest już u innego hodowcy?
            var inniHodowcy = await CallLogService.ZnajdzInnychHodowcowZNumeremAsync(
                connectionString, wybrany, dostawca);
            if (inniHodowcy.Count > 0)
            {
                string lista = string.Join("\n", inniHodowcy.Take(10).Select(h => "  • " + h));
                if (inniHodowcy.Count > 10) lista += $"\n  ... i {inniHodowcy.Count - 10} więcej";
                var odpDup = MessageBox.Show(owner ?? Application.Current.MainWindow,
                    $"⚠ Ten numer ({wybrany}) jest już przypisany do {inniHodowcy.Count} innych hodowców:\n\n" +
                    lista + "\n\nCzy mimo to zapisać u \"" + dostawca + "\"?",
                    "Duplikat numeru telefonu",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (odpDup != MessageBoxResult.Yes)
                {
                    // User wycofuje się — nie zapisujemy
                    return null;
                }
            }

            try
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(
                    "UPDATE dbo.Dostawcy SET Phone1=@p1, Phone2=@p2, Phone3=@p3 " +
                    "WHERE ShortName=@n OR Name=@n", conn);
                cmd.Parameters.AddWithValue("@p1", nowyP1);
                cmd.Parameters.AddWithValue("@p2", nowyP2);
                cmd.Parameters.AddWithValue("@p3", nowyP3);
                cmd.Parameters.AddWithValue("@n", dostawca);
                int rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0)
                {
                    MessageBox.Show(owner,
                        $"⚠ Nie udało się zapisać — hodowca \"{dostawca}\" nie istnieje w słowniku Dostawcy.\n\n" +
                        "Numer został wybrany ale nie zapisany. Możesz go użyć jednorazowo.",
                        "Brak rekordu w Dostawcy",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, $"Błąd zapisu numeru: {ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                // Mimo błędu zapisu — zwracamy numer (user może użyć jednorazowo)
            }

            return wybrany;
        }
    }
}
