namespace Kalendarz1.Customer360.Services
{
    /// <summary>Skrocone formaty liczb dla wykresow C360 — jednoznaczne nazewnictwo eliminuje drift "t" vs "k".</summary>
    public static class Customer360Format
    {
        /// <summary>Kilogramy: 1500 → "1500", 12000 → "12t", 1500000 → "1.5kt".</summary>
        public static string FmtKg(decimal v) =>
            v >= 1_000_000m ? $"{v / 1_000_000m:N1}kt" :
            v >= 1000m ? $"{v / 1000m:N0}t" :
            $"{v:N0}";

        /// <summary>Zlote: 1500 → "1500", 12000 → "12k", 1500000 → "1.5M".</summary>
        public static string FmtZl(decimal v) =>
            v >= 1_000_000m ? $"{v / 1_000_000m:N1}M" :
            v >= 1000m ? $"{v / 1000m:N0}k" :
            $"{v:N0}";
    }
}
