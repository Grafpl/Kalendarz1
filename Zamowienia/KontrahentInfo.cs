// Plik: KontrahentInfo.cs
using System;
namespace Kalendarz1
{
    public class KontrahentInfo
    {
        public string Id { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public string KodPocztowy { get; set; } = "";
        public string Miejscowosc { get; set; } = "";
        public string NIP { get; set; } = "";
        public string Handlowiec { get; set; } = "";
        public DateTime? OstatnieZamowienie { get; set; }
    }
}