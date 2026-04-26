using System.Globalization;

namespace Kalendarz1.Zywiec.Kalendarz.Services
{
    public enum ValidationLevel
    {
        Ok,        // wszystko OK - zielony / brak feedbacku
        Warning,   // poza typowym zakresem ale zapis dozwolony - żółty
        Error      // błędna wartość - czerwony, blokuje zapis
    }

    public class ValidationResult
    {
        public ValidationLevel Level { get; set; }
        public string Message { get; set; } = "";

        public static ValidationResult Ok() => new ValidationResult { Level = ValidationLevel.Ok };
        public static ValidationResult Warn(string msg) => new ValidationResult { Level = ValidationLevel.Warning, Message = msg };
        public static ValidationResult Err(string msg) => new ValidationResult { Level = ValidationLevel.Error, Message = msg };
    }

    /// <summary>
    /// Walidacja wartości wprowadzanych w inline edit popup dla pól dostawy.
    /// Trzy stany: Ok (zielony), Warning (żółty - poza typowym ale można), Error (czerwony - blokuje).
    /// </summary>
    public static class InlineEditValidator
    {
        public static ValidationResult Validate(string fieldName, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return ValidationResult.Err("Wartość nie może być pusta");

            string normalized = text.Trim().Replace(",", ".");

            switch (fieldName)
            {
                case "Auta":
                    return ValidateAuta(normalized);
                case "SztukiDek":
                    return ValidateSztuki(normalized);
                case "WagaDek":
                    return ValidateWaga(normalized);
                case "Cena":
                    return ValidateCena(normalized);
                default:
                    return ValidationResult.Ok();
            }
        }

        private static ValidationResult ValidateAuta(string text)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                return ValidationResult.Err("Niepoprawna liczba całkowita");
            if (v < 1) return ValidationResult.Err("Liczba aut musi być >= 1");
            if (v > 99) return ValidationResult.Err("Liczba aut musi być <= 99");
            if (v > 20) return ValidationResult.Warn($"Nietypowo dużo aut ({v})");
            return ValidationResult.Ok();
        }

        private static ValidationResult ValidateSztuki(string text)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                return ValidationResult.Err("Niepoprawna liczba");
            if (v < 1) return ValidationResult.Err("Sztuki muszą być > 0");
            if (v > 1_000_000) return ValidationResult.Err("Wartość zbyt duża");
            if (v > 100_000) return ValidationResult.Warn($"Nietypowo dużo sztuk ({v:#,0})");
            return ValidationResult.Ok();
        }

        private static ValidationResult ValidateWaga(string text)
        {
            if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal v))
                return ValidationResult.Err("Niepoprawna liczba");
            if (v <= 0) return ValidationResult.Err("Waga musi być > 0");
            if (v > 10) return ValidationResult.Err("Waga musi być <= 10 kg");
            if (v < 0.5m) return ValidationResult.Warn("Waga poniżej 0,5 kg - sprawdź");
            if (v > 5m) return ValidationResult.Warn("Waga powyżej 5 kg - sprawdź");
            return ValidationResult.Ok();
        }

        private static ValidationResult ValidateCena(string text)
        {
            if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal v))
                return ValidationResult.Err("Niepoprawna liczba");
            if (v < 0) return ValidationResult.Err("Cena nie może być ujemna");
            if (v > 100) return ValidationResult.Err("Cena musi być <= 100 zł/kg");
            if (v > 0 && v < 1) return ValidationResult.Warn("Cena poniżej 1 zł/kg - sprawdź");
            if (v > 30) return ValidationResult.Warn("Cena powyżej 30 zł/kg - sprawdź");
            return ValidationResult.Ok();
        }
    }
}
