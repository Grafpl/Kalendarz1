using System;
using System.Collections.Generic;
using System.Globalization;

namespace Kalendarz1.AnalitykaPelna.Models
{
    public enum WidokPrognozy { Towary, Odbiorcy, Handlowcy }

    public class PrognozaWiersz
    {
        public string Klucz { get; set; } = "";   // KodTowaru / NazwaOdbiorcy / Handlowiec
        public string Etykieta { get; set; } = "";
        public Dictionary<int, decimal> SredniePerDzienTygodnia { get; set; } = new(); // 1=Pn..7=Nd

        public decimal Pn => Get(1);
        public decimal Wt => Get(2);
        public decimal Sr => Get(3);
        public decimal Cz => Get(4);
        public decimal Pt => Get(5);
        public decimal So => Get(6);
        public decimal Nd => Get(7);

        public decimal SumaTygodnia
            => Pn + Wt + Sr + Cz + Pt + So + Nd;

        public decimal Get(int dzienTygodnia)
            => SredniePerDzienTygodnia.TryGetValue(dzienTygodnia, out var v) ? v : 0m;
    }

    public class PrognozaPodsumowanie
    {
        public DateTime DataOdAnaliza { get; set; }
        public DateTime DataDoAnaliza { get; set; }
        public int LiczbaTygodni { get; set; }
        public decimal SredniaTygodniowa { get; set; }
        public string DzienMaxNazwa { get; set; } = "";
        public decimal DzienMaxKg { get; set; }
        public string DzienMinNazwa { get; set; } = "";
        public decimal DzienMinKg { get; set; }

        public static string DzienNazwa(int dzien)
        {
            // SQL Server: DATEPART(WEEKDAY,...) zależne od @@DATEFIRST.
            // Ten model przyjmuje normalizację 1=Pn..7=Nd (zgodnie z ISO).
            return dzien switch
            {
                1 => "Poniedziałek",
                2 => "Wtorek",
                3 => "Środa",
                4 => "Czwartek",
                5 => "Piątek",
                6 => "Sobota",
                7 => "Niedziela",
                _ => "?"
            };
        }
    }
}
