using Kalendarz1.Customer360.Models;
using System;
using System.Collections.Generic;

namespace Kalendarz1.Customer360.Services
{
    /// <summary>Migawka danych karty klienta — wejście do eksportu PDF.</summary>
    public class Customer360Snapshot
    {
        public int KlientId { get; set; }
        public string Nazwa { get; set; } = "";
        public string NIP { get; set; } = "";
        public string Adres { get; set; } = "";
        public string Handlowiec { get; set; } = "";
        public DateTime Wygenerowano { get; set; } = DateTime.Now;

        public KlientKpi? Kpi { get; set; }
        public Customer360Score? Score { get; set; }
        public List<MonthlyStats> Obrot { get; set; } = new();
        public List<TopTowarItem> TopTowary { get; set; } = new();
        public List<string> Alerty { get; set; } = new();
    }
}
