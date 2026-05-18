using System;
using System.Collections.Generic;

namespace Kalendarz1.AnalitykaPelna.Models
{
    public class FiltryAnaliz
    {
        public DateTime DataOd { get; set; } = DateTime.Today.AddDays(-7);
        public DateTime DataDo { get; set; } = DateTime.Today;

        // Filtry wspólne (Plan, Bilans, Realizacja, Wydajność)
        public int? TowarIdHandel { get; set; }       // HM.TW.id (w Handel)
        public string? TowarIdLibra { get; set; }     // Article.ID (w LibraNet, np. "40" dla kurczaka)
        public List<int> OdbiorcyIds { get; set; } = new();
        public List<string> Handlowcy { get; set; } = new();

        // Filtry tylko Realizacja (In0E)
        public string? OperatorID { get; set; }
        public int? TerminalId { get; set; }
        public string? Partia { get; set; }
        public int? KlasaKurczaka { get; set; }
        public int? GodzinaOd { get; set; }
        public int? GodzinaDo { get; set; }
        public string? Dostawca { get; set; }

        public bool UkryjKorekty { get; set; }
        public int LiczbaTygodniPrognozy { get; set; } = 8;

        /// <summary>
        /// Lista katalogów HM.TW.katalog do uwzględnienia w bilansach.
        /// Pusta lista = bez filtra (wszystko). Niepusta = tylko wybrane katalogi.
        /// Typowe produkcyjne: 65882=Żywiec, 67094=Odpady, 67095=Mięso świeże, 67104=Mięso inne, 67153=Mrożone.
        /// Wyłączane: 65559=Opakowania, 65547=Karma magazyn (towary nieprodukcyjne / sklepowe).
        /// </summary>
        public List<int> KatalogiTowarow { get; set; } = new();
    }

    public class TowarComboItem
    {
        public int IdHandel { get; set; }
        public string KodHandel { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public string DisplayText => IdHandel == 0 ? KodHandel : $"{KodHandel}  {Nazwa}";
    }

    public class HodowcaComboItem
    {
        public string CustomerID { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public override string ToString() => CustomerName;
    }

    public class OperatorComboItem
    {
        public string OperatorID { get; set; } = "";
        public string Wagowy { get; set; } = "";
        public override string ToString() => Wagowy;
    }
}
