using System;

namespace Kalendarz1.Zywiec.Kalendarz
{
    public class NotatkaModel
    {
        public int NotatkaID { get; set; }
        public int? ParentNotatkaID { get; set; }
        public DateTime DataUtworzenia { get; set; }
        public string DataOdbioru { get; set; }
        public string Dostawca { get; set; }
        public string KtoDodal { get; set; }
        public string KtoDodal_ID { get; set; }
        public string Tresc { get; set; }

        // #27 Wątki: wyciąg z treści rodzica (dla belki "↳ odp. do...")
        public string ParentSnippet { get; set; }
        public bool IsReply => ParentNotatkaID.HasValue;
        public string ReplyHeaderDisplay => IsReply ? $"↳ odp. do: {ParentSnippet}" : "";
    }

    public class ZmianaDostawyModel
    {
        public DateTime DataZmiany { get; set; }
        public string UserID { get; set; }
        public string UserName { get; set; }
        public string NazwaPola { get; set; }
        public string StaraWartosc { get; set; }
        public string NowaWartosc { get; set; }
    }

    public class RankingModel
    {
        public int Pozycja { get; set; }
        public string Dostawca { get; set; }
        public string SredniaWaga { get; set; }
        public int LiczbaD { get; set; }
        public int Punkty { get; set; }
    }
}
