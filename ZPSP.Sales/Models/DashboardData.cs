using System;
using System.Collections.Generic;

namespace ZPSP.Sales.Models
{
    /// <summary>
    /// Dane dashboardu dla wybranego dnia
    /// </summary>
    public class DashboardData
    {
        /// <summary>
        /// Data której dotyczą dane
        /// </summary>
        public DateTime Data { get; set; }

        /// <summary>
        /// Suma zamówień w kg
        /// </summary>
        public decimal SumaZamowien { get; set; }

        /// <summary>
        /// Suma wydań w kg
        /// </summary>
        public decimal SumaWydan { get; set; }

        /// <summary>
        /// Różnica: Zamówienia - Wydania
        /// </summary>
        public decimal Roznica => SumaZamowien - SumaWydan;

        /// <summary>
        /// Liczba zamówień (bez anulowanych)
        /// </summary>
        public int LiczbaZamowien { get; set; }

        /// <summary>
        /// Liczba zamówień anulowanych
        /// </summary>
        public int LiczbaAnulowanych { get; set; }

        /// <summary>
        /// Suma palet
        /// </summary>
        public decimal SumaPalet { get; set; }

        /// <summary>
        /// Liczba unikalnych klientów
        /// </summary>
        public int LiczbaKlientow { get; set; }

        /// <summary>
        /// Agregacje per produkt
        /// </summary>
        public List<ProductAggregation> Produkty { get; set; } = new List<ProductAggregation>();

        /// <summary>
        /// Agregacje per handlowiec
        /// </summary>
        public List<SalesmanSummary> Handlowcy { get; set; } = new List<SalesmanSummary>();

        /// <summary>
        /// Pula Kurczaka A (planowana)
        /// </summary>
        public decimal PulaKurczakA { get; set; }

        /// <summary>
        /// Pula Kurczaka B (planowana)
        /// </summary>
        public decimal PulaKurczakB { get; set; }

        /// <summary>
        /// Faktyczny przychód Kurczaka A
        /// </summary>
        public decimal FaktKurczakA { get; set; }

        /// <summary>
        /// Faktyczny przychód Kurczaka B (suma elementów)
        /// </summary>
        public decimal FaktKurczakB { get; set; }

        /// <summary>
        /// Bilans całkowity
        /// </summary>
        public decimal BilansCalkowity { get; set; }

        /// <summary>
        /// Współczynnik wydajności (%)
        /// </summary>
        public decimal WspolczynnikWydajnosci { get; set; }
    }

    /// <summary>
    /// Podsumowanie per handlowiec
    /// </summary>
    public class SalesmanSummary
    {
        /// <summary>
        /// Kod/inicjały handlowca
        /// </summary>
        public string Handlowiec { get; set; }

        /// <summary>
        /// Liczba zamówień
        /// </summary>
        public int LiczbaZamowien { get; set; }

        /// <summary>
        /// Suma kg zamówień
        /// </summary>
        public decimal SumaKg { get; set; }

        /// <summary>
        /// Suma wartości zamówień
        /// </summary>
        public decimal SumaWartosc { get; set; }

        /// <summary>
        /// Liczba unikalnych klientów
        /// </summary>
        public int LiczbaKlientow { get; set; }

        /// <summary>
        /// Średnia wartość zamówienia
        /// </summary>
        public decimal SredniaWartosc => LiczbaZamowien > 0 ? SumaWartosc / LiczbaZamowien : 0;

        /// <summary>
        /// Średnia kg na zamówienie
        /// </summary>
        public decimal SredniaKg => LiczbaZamowien > 0 ? SumaKg / LiczbaZamowien : 0;
    }

    /// <summary>
    /// Konfiguracja produktów na dany dzień (procent udziału w puli)
    /// </summary>
    public class ProductConfiguration
    {
        /// <summary>
        /// ID produktu
        /// </summary>
        public int ProductId { get; set; }

        /// <summary>
        /// Procent udziału w puli Kurczaka B
        /// </summary>
        public decimal ProcentUdzialu { get; set; }

        /// <summary>
        /// Nazwa grupy scalania (opcjonalna)
        /// </summary>
        public string GrupaScalania { get; set; }

        /// <summary>
        /// Kolejność wyświetlania
        /// </summary>
        public int Kolejnosc { get; set; }
    }

    /// <summary>
    /// Konfiguracja wydajności na dany dzień
    /// </summary>
    public class YieldConfiguration
    {
        /// <summary>
        /// Współczynnik wydajności (%)
        /// </summary>
        public decimal Wspolczynnik { get; set; }

        /// <summary>
        /// Procent Kurczaka A
        /// </summary>
        public decimal ProcentA { get; set; }

        /// <summary>
        /// Procent Kurczaka B (elementów)
        /// </summary>
        public decimal ProcentB { get; set; }
    }

    /// <summary>
    /// Historia zmian zamówienia
    /// </summary>
    public class OrderChangeHistory
    {
        /// <summary>
        /// ID rekordu historii
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID zamówienia
        /// </summary>
        public int ZamowienieId { get; set; }

        /// <summary>
        /// Data zmiany
        /// </summary>
        public DateTime DataZmiany { get; set; }

        /// <summary>
        /// Typ zmiany: UTWORZENIE, EDYCJA, ANULOWANIE, PRZYWROCENIE, USUNIECIE
        /// </summary>
        public string TypZmiany { get; set; }

        /// <summary>
        /// Pole które zostało zmienione
        /// </summary>
        public string PoleZmienione { get; set; }

        /// <summary>
        /// Wartość przed zmianą
        /// </summary>
        public string WartoscPoprzednia { get; set; }

        /// <summary>
        /// Wartość po zmianie
        /// </summary>
        public string WartoscNowa { get; set; }

        /// <summary>
        /// ID użytkownika który dokonał zmiany
        /// </summary>
        public string Uzytkownik { get; set; }

        /// <summary>
        /// Nazwa użytkownika
        /// </summary>
        public string UzytkownikNazwa { get; set; }

        /// <summary>
        /// Opis zmiany (dla złożonych zmian)
        /// </summary>
        public string OpisZmiany { get; set; }

        /// <summary>
        /// Nazwa odbiorcy (dołączona z zamówienia)
        /// </summary>
        public string Odbiorca { get; set; }

        /// <summary>
        /// Handlowiec (dołączony z klienta)
        /// </summary>
        public string Handlowiec { get; set; }

        /// <summary>
        /// Data uboju z zamówienia
        /// </summary>
        public DateTime? DataUboju { get; set; }

        /// <summary>
        /// Ikona typu zmiany
        /// </summary>
        public string Ikona => TypZmiany switch
        {
            "UTWORZENIE" => "➕",
            "EDYCJA" => "✏️",
            "ANULOWANIE" => "❌",
            "PRZYWROCENIE" => "✅",
            "USUNIECIE" => "🗑️",
            _ => "📝"
        };
    }
}
