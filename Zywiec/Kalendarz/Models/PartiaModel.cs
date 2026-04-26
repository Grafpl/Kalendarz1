namespace Kalendarz1.Zywiec.Kalendarz
{
    public class PartiaModel
    {
        public string Partia { get; set; }
        public string PartiaFull { get; set; }
        public string Dostawca { get; set; }
        public decimal Srednia { get; set; }
        public decimal Zywiec { get; set; }
        public decimal Roznica { get; set; }
        public int? Skrzydla { get; set; }
        public int? Nogi { get; set; }
        public int? Oparzenia { get; set; }
        public decimal? KlasaB { get; set; }
        public decimal? Przekarmienie { get; set; }
        public int PhotoCount { get; set; }
        public string FolderPath { get; set; }

        public string SredniaDisplay => Srednia > 0 ? $"{Srednia:0.00} poj" : "";
        public string ZywiecDisplay => Zywiec > 0 ? $"{Zywiec:0.00} kg" : "";
        public string RoznicaDisplay => $"{Roznica:0.00} kg";
        public string SkrzydlaDisplay => Skrzydla.HasValue ? $"{Skrzydla} pkt" : "";
        public string NogiDisplay => Nogi.HasValue ? $"{Nogi} pkt" : "";
        public string OparzeniaDisplay => Oparzenia.HasValue ? $"{Oparzenia} pkt" : "";
        public string KlasaBDisplay => KlasaB.HasValue ? $"{KlasaB:0.##} %" : "";
        public string PrzekarmienieDisplay => Przekarmienie.HasValue ? $"{Przekarmienie:0.00} kg" : "";
        public string ZdjeciaLink => PhotoCount > 0 ? $"Zdjęcia ({PhotoCount})" : "";
    }

    public class PojemnoscTuszkiModel
    {
        public int Pojemnosc { get; set; }
        public int Palety { get; set; }
    }
}
