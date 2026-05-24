using System;

namespace Kalendarz1.DOA
{
    /// <summary>
    /// Pojedynczy rekord padłych — CZYTANY z FarmerCalc (specyfikacja drobiu).
    /// DeclI1 = SztukiDek (zadeklarowane), DeclI2 = Padle. To źródło prawdy edytowane
    /// w module Specyfikacji Drobiu (WidokSpecyfikacje). DOA NIE wpisuje, tylko analizuje.
    /// </summary>
    public class DOARekord
    {
        public DateTime Data { get; set; }              // FarmerCalc.CalcDate
        public int CarLp { get; set; }                  // numer samochodu w dniu
        public string Partia { get; set; } = "";
        public string Hodowca { get; set; } = "";
        public int SztukiDek { get; set; }              // DeclI1
        public int Padle { get; set; }                  // DeclI2

        public decimal ProcentDOA =>
            SztukiDek > 0 ? (decimal)Padle / SztukiDek * 100m : 0m;

        public string DataFormatted => Data.ToString("dd.MM.yyyy");
        public string ProcentFormatted => $"{ProcentDOA:N2}%";
        public string SztukiFormatted => $"{Padle:N0} / {SztukiDek:N0}";

        public string Status =>
            SztukiDek <= 0 ? "—" :
            ProcentDOA <= 0.20m ? "✓ OK" :
            ProcentDOA <= 0.50m ? "⚠ Podwyższone" : "🚨 Przekroczone";

        public string StatusKolor =>
            SztukiDek <= 0 ? "#94A3B8" :
            ProcentDOA <= 0.20m ? "#10B981" :
            ProcentDOA <= 0.50m ? "#F59E0B" : "#DC2626";
    }

    /// <summary>Pozycja rankingu hodowców po średnim DOA (z FarmerCalc).</summary>
    public class DOAHodowca
    {
        public int Pozycja { get; set; }
        public string? HodowcaId { get; set; }          // CustomerGID
        public string Hodowca { get; set; } = "";
        public int LiczbaPartii { get; set; }
        public long SumaSztukDek { get; set; }          // SUM(DeclI1)
        public long SumaPadlych { get; set; }           // SUM(DeclI2)

        public decimal SredniProcDOA =>
            SumaSztukDek > 0 ? (decimal)SumaPadlych / SumaSztukDek * 100m : 0m;

        public string ProcentFormatted => $"{SredniProcDOA:N2}%";
        public string PadleFormatted => $"{SumaPadlych:N0} / {SumaSztukDek:N0}";

        public string Status =>
            SredniProcDOA <= 0.20m ? "✓ OK" :
            SredniProcDOA <= 0.50m ? "⚠ Podwyższone" : "🚨 Powyżej normy";
        public string StatusKolor =>
            SredniProcDOA <= 0.20m ? "#10B981" :
            SredniProcDOA <= 0.50m ? "#F59E0B" : "#DC2626";
    }
}
