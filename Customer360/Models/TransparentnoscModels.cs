using System;
using System.Collections.Generic;

namespace Kalendarz1.Customer360.Models
{
    /// <summary>Wszystkie sygnaly transparentnosci odbiorcy — co jest NIE OK z klientem.</summary>
    public class TransparentnoscDane
    {
        // Klasyfikacja ryzyka (hero)
        public KlasyfikacjaRyzyka Klasyfikacja { get; set; } = new();

        // 6 KPI tile dla zakladki "Sygnaly i alerty"
        public int LiczbaAnulowanych { get; set; }
        public decimal SumaKgAnulowanych { get; set; }
        public decimal SumaWartoscAnulowanych { get; set; }
        public decimal ProcAnulowanych { get; set; }  // % wszystkich zamowien

        public int LiczbaReklamacji { get; set; }
        public decimal WartoscReklamacji { get; set; }
        public decimal ProcReklamacjiObrotu { get; set; }
        public int LiczbaReklamacjiOtwartych { get; set; }  // pulsujace — wymaga akcji

        public decimal SredniaRealizacjaProc { get; set; }  // niedotrzymanie srednie %
        public int LiczbaPozycjiUcietych { get; set; }
        public decimal SumaKgUcietych { get; set; }

        public int LiczbaKorektMinus { get; set; }
        public decimal SumaKorektMinus { get; set; }

        public int LiczbaZmianTerminow { get; set; }
        public decimal SredniePrzesuniecieTerminowDni { get; set; }

        public decimal Przeterminowane { get; set; }
        public int MaxDniOpoznienia { get; set; }

        // Listy
        public List<ReklamacjaSzczegoly> Reklamacje { get; set; } = new();
        public List<KorektaSygnalu> Korekty { get; set; } = new();
        public List<ZmianaTerminu> ZmianyTerminow { get; set; } = new();
        public List<TopProblematycznyTowar> TopProblematyczne { get; set; } = new();
        public List<IncydentTransparentnosci> Timeline { get; set; } = new();

        // Wzorce
        public Dictionary<int, int> AnulacjeWgMiesiaca { get; set; } = new();  // klucz: 1-12, wartosc: liczba
        public string TrendReklamacji { get; set; } = "";  // ▲ rosnie / ▼ spada / ▬ stabilnie
        public string RekomendacjaTekst { get; set; } = "";
        public string RekomendacjaPoziom { get; set; } = "";  // INFO / WARNING / CRITICAL
    }

    public class KlasyfikacjaRyzyka
    {
        public string Litera { get; set; } = "?";  // A/B/C/D
        public string Kategoria { get; set; } = "Brak danych";
        public string KolorHex { get; set; } = "#6B7280";
        public int TotalScore { get; set; }   // 0-100, gdzie 0 = brak ryzyka

        // 4 sub-wskazniki (0-100, 0 = brak ryzyka)
        public int RiskReputacyjny { get; set; }
        public int RiskFinansowy { get; set; }
        public int RiskOperacyjny { get; set; }
        public int RiskKomunikacyjny { get; set; }

        public string OpisReputacyjny { get; set; } = "";
        public string OpisFinansowy { get; set; } = "";
        public string OpisOperacyjny { get; set; } = "";
        public string OpisKomunikacyjny { get; set; } = "";
    }

    public class ReklamacjaSzczegoly
    {
        public int Id { get; set; }
        public DateTime DataZgloszenia { get; set; }
        public string NumerDokumentu { get; set; } = "";
        public string Opis { get; set; } = "";
        public decimal SumaKg { get; set; }
        public decimal Kwota { get; set; }
        public string Status { get; set; } = "";
        public string StatusV2 { get; set; } = "";
        public string StatusV2Etykieta { get; set; } = "";
        public string TypReklamacji { get; set; } = "";
        public string Priorytet { get; set; } = "";
        public string ZrodloZgloszenia { get; set; } = "";
        public string PrzyczynaGlowna { get; set; } = "";
        public string AkcjeNaprawcze { get; set; } = "";
        public string OsobaRozpatrujaca { get; set; } = "";
        public DateTime? DataZakonczenia { get; set; }
        public int DniOdZgloszenia => (int)(DateTime.Today - DataZgloszenia.Date).TotalDays;
        public bool Otwarta => string.IsNullOrEmpty(StatusV2) || StatusV2 == "ZGLOSZONA" || StatusV2 == "W_ANALIZIE";
        public bool SlaPrzekroczone => Otwarta && DniOdZgloszenia >= 7;
        public string StatusKolor => StatusV2 switch
        {
            "ZGLOSZONA" => "#DC2626",   // czerwony
            "W_ANALIZIE" => "#F59E0B",  // pomaranczowy
            "ZASADNA" => "#16A34A",     // zielony
            "POWIAZANA" => "#16A34A",
            "ZAMKNIETA" => "#64748B",   // szary
            "ODRZUCONA" => "#94A3B8",
            _ => "#94A3B8"
        };
    }

    public class KorektaSygnalu
    {
        public int Id { get; set; }
        public string NumerDokumentu { get; set; } = "";
        public DateTime Data { get; set; }
        public string TypDk { get; set; } = "";  // FKS / FKR
        public decimal Walbrutto { get; set; }   // ujemna = minus
        public int IdDokumentuOryginalnego { get; set; }
        public string NumerOryginalu { get; set; } = "";
        public decimal SumaKg { get; set; }
        public bool JestMinus => Walbrutto < 0;
    }

    public class ZmianaTerminu
    {
        public int IdFaktury { get; set; }
        public string NumerFaktury { get; set; } = "";
        public DateTime DataFaktury { get; set; }
        public DateTime TerminPierwotny { get; set; }
        public DateTime TerminAktualny { get; set; }
        public int PrzesunieceDni => (int)(TerminAktualny.Date - TerminPierwotny.Date).TotalDays;
        public decimal KwotaFaktury { get; set; }
    }

    public class TopProblematycznyTowar
    {
        public int KodTowaru { get; set; }
        public string Nazwa { get; set; } = "";
        public int LiczbaAnulacji { get; set; }
        public int LiczbaReklamacji { get; set; }
        public decimal SumaKgUcietych { get; set; }
        public int RiskScore { get; set; }   // 0-100
    }

    public class IncydentTransparentnosci
    {
        public DateTime Data { get; set; }
        public string Typ { get; set; } = "";   // Anulacja / Reklamacja / Korekta / ZmianaTerminu
        public string Ikona { get; set; } = "";
        public string Opis { get; set; } = "";
        public decimal Kwota { get; set; }
        public string KolorHex { get; set; } = "#64748B";
    }
}
