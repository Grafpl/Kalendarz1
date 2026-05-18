using System.Linq;

namespace Kalendarz1.Services
{
    /// <summary>
    /// BCrypt password hashing dla logowania ZPSP.
    /// Używa BCrypt.Net-Next z workFactor=12 (1s na hash, brute-force niemożliwe).
    /// </summary>
    public static class PasswordHasher
    {
        // workFactor=12 to dobry kompromis 2026: ~1s/hash, niemożliwe do złamania
        private const int WorkFactor = 12;

        /// <summary>Hashuje hasło. Zwraca string ~60 znaków zawierający sól + hash.</summary>
        public static string Hash(string password)
            => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

        /// <summary>Weryfikuje hasło przeciw zahashowanej wartości.</summary>
        public static bool Verify(string password, string hash)
        {
            if (string.IsNullOrEmpty(hash)) return false;
            try { return BCrypt.Net.BCrypt.Verify(password, hash); }
            catch { return false; }
        }

        /// <summary>
        /// Walidacja hasła. Minimum 4 znaki — bez wymogu cyfr/liter/znaków specjalnych.
        /// Rekomendacje (silne hasło 8+ znaków + cyfra + litera) są w UI dialogu,
        /// ale NIE są wymuszane.
        /// </summary>
        public static (bool ok, string error) Validate(string password)
        {
            if (string.IsNullOrEmpty(password))
                return (false, "Hasło nie może być puste.");
            if (password.Length < 4)
                return (false, "Hasło musi mieć minimum 4 znaki.");
            return (true, "");
        }

        /// <summary>
        /// Czy hasło spełnia rekomendacje "silnego hasła" (8+ znaków + cyfra + litera).
        /// Używane tylko do wyświetlenia podpowiedzi — nie do blokowania.
        /// </summary>
        public static bool IsStrong(string password)
        {
            return !string.IsNullOrEmpty(password)
                && password.Length >= 8
                && password.Any(char.IsDigit)
                && password.Any(char.IsLetter);
        }
    }
}
