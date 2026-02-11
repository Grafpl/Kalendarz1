using System;
using System.Collections.Generic;

namespace Kalendarz1.DyrektorDashboard.Models
{
    // ════════════════════════════════════════════════════════════════════════
    // MODELE DANYCH - PANEL DYREKTORA
    // Agregacja KPI ze wszystkich działów zakładu
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Kontener główny - 6 kart KPI na górze dashboardu
    /// </summary>
    public class KpiKartyDyrektora
    {
        // ── ŻYWIEC ──
        public decimal ZywiecDzisKg { get; set; }
        public int ZywiecDzisDostawy { get; set; }
        public decimal ZywiecPlanKg { get; set; }
        public int ZywiecRealizacjaProcent => ZywiecPlanKg > 0
            ? (int)(ZywiecDzisKg / ZywiecPlanKg * 100) : 0;
        public int ZywiecSztukiDzis { get; set; }

        // ── ZAMÓWIENIA ──
        public int ZamowieniaDzisLiczba { get; set; }
        public decimal ZamowieniaDzisKg { get; set; }
        public decimal ZamowieniaDzisWartosc { get; set; }
        public int ZamowieniaJutroLiczba { get; set; }
        public decimal ZamowieniaJutroKg { get; set; }

        // ── PRODUKCJA ──
        public decimal ProdukcjaDzisKg { get; set; }
        public decimal ProdukcjaPlanKg { get; set; }
        public int ProdukcjaRealizacjaProcent => ProdukcjaPlanKg > 0
            ? (int)(ProdukcjaDzisKg / ProdukcjaPlanKg * 100) : 0;
        public decimal ProdukcjaLWPKg { get; set; }

        // ── MAGAZYN ──
        public decimal MagazynStanKg { get; set; }
        public decimal MagazynStanWartosc { get; set; }
        public decimal MagazynSwiezyKg { get; set; }
        public decimal MagazynMrozonyKg { get; set; }

        // ── TRANSPORT ──
        public int TransportDzisKursy { get; set; }
        public int TransportAktywneKursy { get; set; }
        public int TransportZakonczoneKursy { get; set; }
        public int TransportKierowcyAktywni { get; set; }

        // ── REKLAMACJE ──
        public int ReklamacjeOtwarte { get; set; }
        public int ReklamacjeNowe { get; set; }
        public decimal ReklamacjeSumaKg { get; set; }
    }

    // ════════════════════════════════════════════════════════════════════════
    // DANE ZAKŁADEK (ładowane leniwie per tab)
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zakładka ŻYWIEC - przychód żywca, hodowcy, trend
    /// </summary>
    public class DaneZywiec
    {
        public decimal DzisKg { get; set; }
        public int DzisDostawy { get; set; }
        public int DzisSztuki { get; set; }
        public decimal DzisWartosc { get; set; }
        public decimal SredniaCenaDzis { get; set; }
        public decimal SredniUbytekDzis { get; set; }
        public int PadnieteDzis { get; set; }

        public decimal TydzienKg { get; set; }
        public decimal TydzienWartosc { get; set; }
        public decimal MiesiacKg { get; set; }
        public decimal MiesiacWartosc { get; set; }

        public decimal PlanDzisKg { get; set; }

        public List<TopHodowcaItem> TopHodowcy { get; set; } = new();
        public List<TrendTygodniowyItem> Trend8Tygodni { get; set; } = new();
        public List<DostawaDzisItem> DostawyDzis { get; set; } = new();
    }

    public class TopHodowcaItem
    {
        public int Pozycja { get; set; }
        public string Nazwa { get; set; }
        public string Miasto { get; set; }
        public decimal WagaKg { get; set; }
        public decimal Wartosc { get; set; }
        public int LiczbaDostaw { get; set; }
    }

    public class TrendTygodniowyItem
    {
        public int NumerTygodnia { get; set; }
        public DateTime PoczatekTygodnia { get; set; }
        public decimal WagaKg { get; set; }
        public decimal Wartosc { get; set; }
        public decimal SredniaCena { get; set; }
        public int LiczbaDostaw { get; set; }
        public string Etykieta => $"Tydz. {NumerTygodnia}";
    }

    public class DostawaDzisItem
    {
        public DateTime Godzina { get; set; }
        public string Hodowca { get; set; }
        public int Sztuki { get; set; }
        public decimal WagaKg { get; set; }
        public decimal Cena { get; set; }
        public string Kierowca { get; set; }
    }

    /// <summary>
    /// Zakładka ZAMÓWIENIA - zamówienia klientów, statusy, trend
    /// </summary>
    public class DaneZamowienia
    {
        public int LiczbaZamowienDzis { get; set; }
        public decimal SumaKgDzis { get; set; }
        public decimal SumaWartoscDzis { get; set; }
        public int LiczbaZamowienJutro { get; set; }
        public decimal SumaKgJutro { get; set; }
        public decimal SumaWartoscJutro { get; set; }
        public List<ZamowienieStatusGrupa> StatusyDzis { get; set; } = new();
        public List<ZamowienieDzienneItem> TrendDzienny { get; set; } = new();
        public List<TopKlientItem> TopKlienci { get; set; } = new();
    }

    public class ZamowienieStatusGrupa
    {
        public string Status { get; set; }
        public int Liczba { get; set; }
        public decimal SumaKg { get; set; }
    }

    public class ZamowienieDzienneItem
    {
        public DateTime Data { get; set; }
        public int Liczba { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SumaWartosc { get; set; }
    }

    public class TopKlientItem
    {
        public string Nazwa { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SumaWartosc { get; set; }
        public int LiczbaZamowien { get; set; }
    }

    /// <summary>
    /// Zakładka PRODUKCJA - ubój, krojenie, LWP, wydajność
    /// </summary>
    public class DaneProdukcja
    {
        // Ubój (sPWU - przyjęcie wewnętrzne uboju)
        public decimal UbojDzisKg { get; set; }
        public decimal TuszkiAKg { get; set; }
        public decimal TuszkiBKg { get; set; }

        // Krojenie (LWP - likwidacja wyrobu/produkt)
        public decimal KrojenieDzisKg { get; set; }
        public decimal ElementyKg { get; set; }
        public decimal WydajnoscKrojeniaProcent { get; set; }

        // RWP - rozchód wewnętrzny produkcji
        public decimal RWPDzisKg { get; set; }

        // Plan
        public decimal PlanUbojuKg { get; set; }
        public int RealizacjaUbojuProcent => PlanUbojuKg > 0
            ? (int)(UbojDzisKg / PlanUbojuKg * 100) : 0;

        public List<ProdukcjaDziennaItem> TrendTygodniowy { get; set; } = new();
        public List<ProduktProdukcjiItem> TopProdukty { get; set; } = new();
    }

    public class ProdukcjaDziennaItem
    {
        public DateTime Data { get; set; }
        public string DzienNazwa { get; set; }
        public decimal UbojKg { get; set; }
        public decimal KrojenieKg { get; set; }
        public decimal LWPKg { get; set; }
    }

    public class ProduktProdukcjiItem
    {
        public string Kod { get; set; }
        public string Nazwa { get; set; }
        public decimal IloscKg { get; set; }
        public string TypDokumentu { get; set; }
    }

    /// <summary>
    /// Zakładka MAGAZYN - stany magazynowe mroźni
    /// </summary>
    public class DaneMagazyn
    {
        public decimal StanSwiezyKg { get; set; }
        public decimal StanMrozonyKg { get; set; }
        public decimal StanCaloscKg { get; set; }
        public decimal StanWartoscZl { get; set; }
        public int LiczbaPozycji { get; set; }

        public List<StanProduktItem> TopProdukty { get; set; } = new();
    }

    public class StanProduktItem
    {
        public string Kod { get; set; }
        public string Nazwa { get; set; }
        public decimal IloscKg { get; set; }
        public decimal WartoscZl { get; set; }
        public string Katalog { get; set; }
    }

    /// <summary>
    /// Zakładka TRANSPORT - kursy dnia, kierowcy
    /// </summary>
    public class DaneTransport
    {
        public int KursyDzis { get; set; }
        public int KursyPlanowane { get; set; }
        public int KursyWTrasie { get; set; }
        public int KursyZakonczone { get; set; }
        public int KierowcyAktywni { get; set; }
        public int PojazdyWUzyciu { get; set; }

        public List<KursItem> Kursy { get; set; } = new();
    }

    public class KursItem
    {
        public long KursID { get; set; }
        public string Kierowca { get; set; }
        public string Pojazd { get; set; }
        public string Status { get; set; }
        public string Trasa { get; set; }
        public int LiczbaLadunkow { get; set; }
    }

    /// <summary>
    /// Zakładka REKLAMACJE - otwarte, statusy, historia
    /// </summary>
    public class DaneReklamacje
    {
        public int NoweCount { get; set; }
        public int WTrakcieCount { get; set; }
        public int ZaakceptowaneCount { get; set; }
        public int OdrzuconeCount { get; set; }
        public int ZamknieteCount { get; set; }
        public int OtwarteRazem => NoweCount + WTrakcieCount;
        public decimal SumaKgOtwarte { get; set; }

        public List<ReklamacjaItem> OstatnieReklamacje { get; set; } = new();
    }

    public class ReklamacjaItem
    {
        public int Id { get; set; }
        public DateTime Data { get; set; }
        public string Kontrahent { get; set; }
        public string Status { get; set; }
        public string Opis { get; set; }
        public decimal IloscKg { get; set; }
    }

    /// <summary>
    /// Zakładka OPAKOWANIA - salda E2, H1
    /// </summary>
    public class DaneOpakowania
    {
        public decimal SaldoE2 { get; set; }
        public decimal SaldoH1 { get; set; }
        public decimal SaldoInne { get; set; }
        public int KontrahenciZeZwrotem { get; set; }
        public decimal WartoscOpakowan { get; set; }

        public List<OpakowanieSaldoItem> SaldaWgTypu { get; set; } = new();
    }

    public class OpakowanieSaldoItem
    {
        public string TypOpakowania { get; set; }
        public decimal Wydane { get; set; }
        public decimal Przyjete { get; set; }
        public decimal Saldo { get; set; }
    }

    /// <summary>
    /// Zakładka PLAN TYGODNIOWY - plan vs realizacja per dzień
    /// </summary>
    public class DanePlanTygodniowy
    {
        public decimal PlanTygodniaSumaKg { get; set; }
        public decimal RealizacjaTygodniaSumaKg { get; set; }
        public int RealizacjaProcent => PlanTygodniaSumaKg > 0
            ? (int)(RealizacjaTygodniaSumaKg / PlanTygodniaSumaKg * 100) : 0;

        public List<PlanDzienItem> Dni { get; set; } = new();
    }

    public class PlanDzienItem
    {
        public DateTime Data { get; set; }
        public string DzienTygodnia { get; set; }
        public decimal PlanKg { get; set; }
        public decimal RealizacjaKg { get; set; }
        public int ProcentRealizacji => PlanKg > 0
            ? (int)(RealizacjaKg / PlanKg * 100) : 0;
        public bool CzyDzisiaj { get; set; }
        public int LiczbaDostaw { get; set; }
    }

    // ════════════════════════════════════════════════════════════════════════
    // ZAMÓWIENIA - WIDOK SZCZEGÓŁOWY (klient + produkt + ilość)
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Szczegółowa pozycja zamówienia (klient + produkt + ilość)
    /// </summary>
    public class ZamowienieSzczegolyItem
    {
        public int ZamowienieId { get; set; }
        public string Klient { get; set; }
        public string Status { get; set; }
        public string Produkt { get; set; }
        public decimal IloscKg { get; set; }
        public string Cena { get; set; }
    }

    /// <summary>
    /// Kontener na szczegółowe dane zamówień (dziś + jutro)
    /// </summary>
    public class DaneZamowieniaSzczegoly
    {
        public List<ZamowienieSzczegolyItem> ZamowieniaDzis { get; set; } = new();
        public List<ZamowienieSzczegolyItem> ZamowieniaJutro { get; set; } = new();
        public List<string> UnikatoweProdukty { get; set; } = new();

        public int LiczbaDzis { get; set; }
        public decimal SumaKgDzis { get; set; }
        public int LiczbaJutro { get; set; }
        public decimal SumaKgJutro { get; set; }
        public decimal WartoscDzis { get; set; }
    }

    /// <summary>
    /// Zakładka ALERTY - operacyjne ostrzeżenia
    /// </summary>
    public class AlertItem
    {
        public string Typ { get; set; }
        public string Priorytet { get; set; }
        public string Tytul { get; set; }
        public string Opis { get; set; }
        public DateTime Data { get; set; }
        public string Ikona { get; set; }

        public string PriorytetKolor => Priorytet switch
        {
            "Krytyczny" => "#E74C3C",
            "Wysoki" => "#F39C12",
            "Sredni" => "#3498DB",
            _ => "#8B949E"
        };
    }
}
