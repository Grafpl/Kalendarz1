using System;
using System.Collections.Generic;
using System.Linq;

namespace Kalendarz1.Kartoteka.Features.Scoring
{
    public static class ScoringCalculator
    {
        /// <summary>
        /// Terminowość płatności (max 40 pkt)
        /// </summary>
        public static int ObliczTerminowosc(int fakturaOgolem, int terminowych)
        {
            if (fakturaOgolem == 0) return 20; // Brak danych = średnia

            double procent = (double)terminowych / fakturaOgolem * 100;
            return procent switch
            {
                >= 100 => 40,
                >= 90 => 35,
                >= 80 => 25,
                >= 70 => 15,
                _ => 0
            };
        }

        /// <summary>
        /// Historia współpracy (max 20 pkt)
        /// </summary>
        public static int ObliczHistorie(DateTime? pierwszaFaktura)
        {
            if (!pierwszaFaktura.HasValue) return 5;

            int lata = (int)((DateTime.Now - pierwszaFaktura.Value).TotalDays / 365.25);
            return lata switch
            {
                > 5 => 20,
                >= 3 => 15,
                >= 1 => 10,
                _ => 5
            };
        }

        /// <summary>
        /// Regularność zamówień (max 20 pkt)
        /// Na podstawie średniej liczby dni między zamówieniami w ostatnich 6 miesiącach
        /// </summary>
        public static int ObliczRegularnosc(List<DateTime> datyZamowien)
        {
            if (datyZamowien == null || datyZamowien.Count < 2) return 5;

            var posortowane = datyZamowien.OrderBy(d => d).ToList();
            var odstepy = new List<double>();
            for (int i = 1; i < posortowane.Count; i++)
                odstepy.Add((posortowane[i] - posortowane[i - 1]).TotalDays);

            double sredniOdstep = odstepy.Average();
            return sredniOdstep switch
            {
                <= 7 => 20,    // Co tydzień
                <= 14 => 15,   // Co 2 tygodnie
                <= 30 => 10,   // Co miesiąc
                _ => 5         // Nieregularnie
            };
        }

        /// <summary>
        /// Trend obrotów (max 10 pkt)
        /// Porównanie obrotów bieżącego vs poprzedniego okresu
        /// </summary>
        public static int ObliczTrend(decimal obrotyBiezace, decimal obrotyPoprzednie)
        {
            if (obrotyPoprzednie == 0) return 7; // Brak danych

            double zmiana = (double)((obrotyBiezace - obrotyPoprzednie) / obrotyPoprzednie * 100);
            return zmiana switch
            {
                > 10 => 10,  // Wzrost >10%
                >= -5 => 7,  // Stabilnie
                _ => 3       // Spadek
            };
        }

        /// <summary>
        /// Wykorzystanie limitu (max 10 pkt)
        /// </summary>
        public static int ObliczWykorzystanieLimitu(decimal limit, decimal wykorzystano)
        {
            if (limit <= 0) return 7; // Brak limitu

            double procent = (double)(wykorzystano / limit * 100);
            return procent switch
            {
                <= 50 => 10,
                <= 80 => 7,
                <= 100 => 3,
                _ => 0     // Przekroczony
            };
        }

        public static string KategoryzujScore(int score) => score switch
        {
            >= 90 => "Doskonały",
            >= 70 => "Dobry",
            >= 50 => "Średni",
            >= 30 => "Słaby",
            _ => "Krytyczny"
        };

        public static string RekomendacjaOpisu(int score, decimal aktualnyLimit) => score switch
        {
            >= 90 => $"Klient doskonały. Rekomendacja: zwiększenie limitu o 20% (do {aktualnyLimit * 1.2m:N0} zł)",
            >= 70 => "Klient dobry. Standardowe warunki handlowe.",
            >= 50 => "Klient wymaga monitorowania. Rozważyć utrzymanie obecnego limitu.",
            >= 30 => $"Klient słaby. Rekomendacja: obniżenie limitu o 30% (do {aktualnyLimit * 0.7m:N0} zł)",
            _ => "Klient krytyczny. Rekomendacja: wstrzymanie kredytu kupieckiego."
        };
    }
}
