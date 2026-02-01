using System;

namespace Kalendarz1.Kartoteka.Features.Przypomnienia
{
    public static class PrzypomnienieRules
    {
        public static int DniMiedzyKontaktami(string kategoria) => kategoria switch
        {
            "A" => 7,
            "B" => 14,
            "C" => 30,
            "D" => 60,
            _ => 30
        };

        public static int[] DniPrzedTerminem = { 3, 1, 0 };
        public static int[] DniPoTerminie = { 1, 7, 14, 30 };

        public static int PriorytetDlaTypu(string typ) => typ switch
        {
            "PLATNOSC_PRZETERMINOWANA" => 1,
            "LIMIT_ALERT" => 1,
            "PLATNOSC_TERMIN" => 2,
            "NIEAKTYWNY_KLIENT" => 2,
            "KONTAKT_CYKLICZNY" => 3,
            "URODZINY" => 3,
            "ROCZNICA" => 3,
            "CUSTOM" => 3,
            _ => 3
        };

        public static int DniNieaktywnosciAlert(string kategoria) => kategoria switch
        {
            "A" => 14,
            "B" => 21,
            "C" => 45,
            "D" => 90,
            _ => 30
        };

        public static double ProcentLimituAlert = 80.0;
    }
}
