using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Kalendarz1.AnalitykaPelna.Services
{
    public static class AnalitykaConfig
    {
        public static string ConnLibraNet { get; private set; } =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public static string ConnHandel { get; private set; } =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        public static string KatalogMieso { get; private set; } = "67095";
        public static string MagazynUbojnia { get; private set; } = "65554";
        public static int ZmianaDziennaStart { get; private set; } = 5;
        public static int ZmianaNocnaStart { get; private set; } = 21;
        public static double NormaWydajnosciProc { get; private set; } = 78.0;
        public static double NormaPodrobowProc { get; private set; } = 1.75;
        public static double TolerancjaWydajnosciProc { get; private set; } = 2.0;
        public static int LiveRefreshSekund { get; private set; } = 60;
        public static int PrognozaTygodni { get; private set; } = 8;
        public static int DomyslnyZakresDni { get; private set; } = 7;
        public static int TopNRanking { get; private set; } = 10;
        public static int HeatmapaDniWstecz { get; private set; } = 14;

        private static bool _zaladowano;
        private static readonly object _lock = new();

        public static void ZaladujJesliTrzeba()
        {
            if (_zaladowano) return;
            lock (_lock)
            {
                if (_zaladowano) return;
                Zaladuj();
                _zaladowano = true;
            }
        }

        private static void Zaladuj()
        {
            try
            {
                var sciezka = ZnajdzPlikKonfig();
                if (sciezka == null) return;

                var json = JObject.Parse(File.ReadAllText(sciezka));

                ConnLibraNet = json["ConnectionStrings"]?["LibraNet"]?.Value<string>() ?? ConnLibraNet;
                ConnHandel = json["ConnectionStrings"]?["Handel"]?.Value<string>() ?? ConnHandel;

                var ap = json["AnalitykaPelna"];
                if (ap == null) return;

                KatalogMieso = ap["KatalogMieso"]?.Value<string>() ?? KatalogMieso;
                MagazynUbojnia = ap["MagazynUbojnia"]?.Value<string>() ?? MagazynUbojnia;
                ZmianaDziennaStart = ap["ZmianaDziennaStart"]?.Value<int>() ?? ZmianaDziennaStart;
                ZmianaNocnaStart = ap["ZmianaNocnaStart"]?.Value<int>() ?? ZmianaNocnaStart;
                NormaWydajnosciProc = ap["NormaWydajnosciProc"]?.Value<double>() ?? NormaWydajnosciProc;
                NormaPodrobowProc = ap["NormaPodrobowProc"]?.Value<double>() ?? NormaPodrobowProc;
                TolerancjaWydajnosciProc = ap["TolerancjaWydajnosciProc"]?.Value<double>() ?? TolerancjaWydajnosciProc;
                LiveRefreshSekund = ap["LiveRefreshSekund"]?.Value<int>() ?? LiveRefreshSekund;
                PrognozaTygodni = ap["PrognozaTygodni"]?.Value<int>() ?? PrognozaTygodni;
                DomyslnyZakresDni = ap["DomyslnyZakresDni"]?.Value<int>() ?? DomyslnyZakresDni;
                TopNRanking = ap["TopNRanking"]?.Value<int>() ?? TopNRanking;
                HeatmapaDniWstecz = ap["HeatmapaDniWstecz"]?.Value<int>() ?? HeatmapaDniWstecz;
            }
            catch
            {
                // Fallback do hardcoded — config jest opcjonalny
            }
        }

        private static string? ZnajdzPlikKonfig()
        {
            var kandydaci = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
                Path.Combine(Environment.CurrentDirectory, "appsettings.json")
            };
            foreach (var k in kandydaci)
                if (File.Exists(k)) return k;
            return null;
        }
    }
}
