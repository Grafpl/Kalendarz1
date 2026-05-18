using System.Collections.Generic;

namespace Kalendarz1.AnalitykaPelna.Models
{
    /// <summary>
    /// Komplet danych dla widoku "Łańcuch Graficzny" — summary łańcucha + WSZYSTKIE towary
    /// per etap (nie tylko top N). Używane do wizualizacji bez tabel.
    /// </summary>
    public class FlowChainGraficznyData
    {
        public FlowChainSummary Summary { get; set; } = new();
        public List<FlowChainTowar> Zywiec { get; set; } = new();
        public List<FlowChainTowar> Uboj { get; set; } = new();
        public List<FlowChainTowar> Produkcja { get; set; } = new();
        public List<FlowChainTowar> Dystrybucja { get; set; } = new();
        public List<FlowChainTowar> Klienci { get; set; } = new();
        public List<FlowChainTowar> Mroznia { get; set; } = new();
        public List<FlowChainTowar> Masarnia { get; set; } = new();
        public List<FlowChainTowar> Karma { get; set; } = new();
        public List<FlowChainTowar> Odpady { get; set; } = new();
        public List<FlowChainTowar> RozchodKrojenia { get; set; } = new();
    }
}
