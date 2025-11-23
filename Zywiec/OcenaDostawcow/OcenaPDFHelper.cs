using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    /// <summary>
    /// Pomocnik do masowego generowania PDF i dodatkowych operacji
    /// </summary>
    public class OcenaPDFHelper
    {
        private const string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        #region Generowanie pustych formularzy dla wszystkich dostawców

        /// <summary>
        /// Generuje puste formularze PDF dla wszystkich aktywnych dostawców
        /// </summary>
        public static List<string> GenerujPusteFormularzeWszyscy(string folderWyjsciowy)
        {
            var wygenerowanePliki = new List<string>();

            try
            {
                // Pobierz listę dostawców z bazy
                var dostawcy = PobierzAktywnychDostawcow();

                // Utwórz folder jeśli nie istnieje
                Directory.CreateDirectory(folderWyjsciowy);

                var generator = new OcenaPDFGenerator();

                foreach (var dostawca in dostawcy)
                {
                    try
                    {
                        string nazwaPliku = $"Formularz_{dostawca.ID}_{dostawca.ShortName}_{DateTime.Now:yyyyMMdd}.pdf";
                        string sciezka = Path.Combine(folderWyjsciowy, nazwaPliku);

                        generator.GenerujPdf(
                            sciezkaDoPliku: sciezka,
                            numerRaportu: "",
                            dataOceny: DateTime.Now,
                            dostawcaNazwa: dostawca.Name,
                            dostawcaId: dostawca.ID,
                            samoocena: null,
                            listaKontrolna: null,
                            dokumentacja: false,
                            p1_5: 0,
                            p6_20: 0,
                            pRazem: 0,
                            uwagi: "",
                            czyPustyFormularz: true
                        );

                        wygenerowanePliki.Add(sciezka);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Błąd generowania dla {dostawca.Name}: {ex.Message}");
                    }
                }

                return wygenerowanePliki;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd pobierania dostawców: {ex.Message}");
                return wygenerowanePliki;
            }
        }

        /// <summary>
        /// Generuje pusty formularz dla jednego dostawcy
        /// </summary>
        public static string GenerujPustyFormularzDlaDostawcy(string dostawcaId, string folderWyjsciowy)
        {
            try
            {
                var dostawca = PobierzDostawce(dostawcaId);
                if (dostawca == null)
                {
                    throw new Exception($"Nie znaleziono dostawcy o ID: {dostawcaId}");
                }

                Directory.CreateDirectory(folderWyjsciowy);

                string nazwaPliku = $"Formularz_{dostawca.ID}_{DateTime.Now:yyyyMMdd}.pdf";
                string sciezka = Path.Combine(folderWyjsciowy, nazwaPliku);

                var generator = new OcenaPDFGenerator();
                generator.GenerujPdf(
                    sciezkaDoPliku: sciezka,
                    numerRaportu: "",
                    dataOceny: DateTime.Now,
                    dostawcaNazwa: dostawca.Name,
                    dostawcaId: dostawca.ID,
                    samoocena: null,
                    listaKontrolna: null,
                    dokumentacja: false,
                    p1_5: 0,
                    p6_20: 0,
                    pRazem: 0,
                    uwagi: "",
                    czyPustyFormularz: true
                );

                return sciezka;
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd generowania pustego formularza: {ex.Message}", ex);
            }
        }

        #endregion

        #region Generowanie z porównaniem i statystykami

        /// <summary>
        /// Generuje raport z pełną analizą (porównanie, statystyki, rekomendacje)
        /// </summary>
        public static string GenerujRaportZAnaliza(
            string sciezkaDoPliku,
            string numerRaportu,
            DateTime dataOceny,
            string dostawcaId,
            bool[] samoocena,
            bool[] listaKontrolna,
            bool dokumentacja,
            int p1_5,
            int p6_20,
            int pRazem,
            string uwagi)
        {
            try
            {
                // Pobierz dane dostawcy
                var dostawca = PobierzDostawce(dostawcaId);
                if (dostawca == null)
                {
                    throw new Exception($"Nie znaleziono dostawcy o ID: {dostawcaId}");
                }

                // Pobierz poprzednią ocenę
                var poprzedniaOcena = PobierzPoprzedniaOcene(dostawcaId, dataOceny);

                // Oblicz statystyki
                var statystyki = ObliczStatystykiDostawcy(dostawcaId);

                // Generuj PDF z rozszerzonymi opcjami
                var generator = new OcenaPDFGenerator();
                generator.GenerujPdfRozszerzony(
                    sciezkaDoPliku: sciezkaDoPliku,
                    numerRaportu: numerRaportu,
                    dataOceny: dataOceny,
                    dostawcaNazwa: dostawca.Name,
                    dostawcaId: dostawcaId,
                    samoocena: samoocena,
                    listaKontrolna: listaKontrolna,
                    dokumentacja: dokumentacja,
                    p1_5: p1_5,
                    p6_20: p6_20,
                    pRazem: pRazem,
                    uwagi: uwagi,
                    czyPustyFormularz: false,
                    watermark: null,
                    pokazKodQR: true,
                    poprzedniaOcena: poprzedniaOcena,
                    statystyki: statystyki
                );

                return sciezkaDoPliku;
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd generowania raportu z analizą: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generuje raport z watermarkiem (DRAFT, KOPIA, ANULOWANO)
        /// </summary>
        public static string GenerujRaportZWatermarkiem(
            string sciezkaDoPliku,
            string numerRaportu,
            DateTime dataOceny,
            string dostawcaId,
            bool[] samoocena,
            bool[] listaKontrolna,
            bool dokumentacja,
            int p1_5,
            int p6_20,
            int pRazem,
            string uwagi,
            string typWatermark)  // "DRAFT", "KOPIA", "ANULOWANO"
        {
            try
            {
                var dostawca = PobierzDostawce(dostawcaId);
                if (dostawca == null)
                {
                    throw new Exception($"Nie znaleziono dostawcy o ID: {dostawcaId}");
                }

                var generator = new OcenaPDFGenerator();
                generator.GenerujPdfRozszerzony(
                    sciezkaDoPliku: sciezkaDoPliku,
                    numerRaportu: numerRaportu,
                    dataOceny: dataOceny,
                    dostawcaNazwa: dostawca.Name,
                    dostawcaId: dostawcaId,
                    samoocena: samoocena,
                    listaKontrolna: listaKontrolna,
                    dokumentacja: dokumentacja,
                    p1_5: p1_5,
                    p6_20: p6_20,
                    pRazem: pRazem,
                    uwagi: uwagi,
                    czyPustyFormularz: false,
                    watermark: typWatermark,
                    pokazKodQR: false,
                    poprzedniaOcena: null,
                    statystyki: null
                );

                return sciezkaDoPliku;
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd generowania raportu z watermarkiem: {ex.Message}", ex);
            }
        }

        #endregion

        #region Pomocnicze metody bazy danych

        private static List<DostawcaInfo> PobierzAktywnychDostawcow()
        {
            var dostawcy = new List<DostawcaInfo>();

            try
            {
                using var connection = new SqlConnection(connectionString);
                using var command = new SqlCommand(@"
                    SELECT ID, Name, ShortName 
                    FROM [dbo].[Dostawcy] 
                    WHERE Active = 1
                    ORDER BY Name", connection);

                connection.Open();
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    dostawcy.Add(new DostawcaInfo
                    {
                        ID = reader["ID"]?.ToString() ?? "",
                        Name = reader["Name"]?.ToString() ?? "",
                        ShortName = reader["ShortName"]?.ToString() ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd pobierania dostawców: {ex.Message}");
            }

            return dostawcy;
        }

        private static DostawcaInfo PobierzDostawce(string dostawcaId)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                using var command = new SqlCommand(@"
                    SELECT ID, Name, ShortName 
                    FROM [dbo].[Dostawcy] 
                    WHERE ID = @DostawcaID", connection);

                command.Parameters.AddWithValue("@DostawcaID", dostawcaId);

                connection.Open();
                using var reader = command.ExecuteReader();

                if (reader.Read())
                {
                    return new DostawcaInfo
                    {
                        ID = reader["ID"]?.ToString() ?? "",
                        Name = reader["Name"]?.ToString() ?? "",
                        ShortName = reader["ShortName"]?.ToString() ?? ""
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd pobierania dostawcy: {ex.Message}");
            }

            return null;
        }

        private static OcenaPorownanieData PobierzPoprzedniaOcene(string dostawcaId, DateTime aktualnaData)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                using var command = new SqlCommand(@"
                    SELECT TOP 1 
                        DataOceny, 
                        PunktyRazem,
                        CASE 
                            WHEN PunktyRazem >= 30 THEN 'POZYTYWNA'
                            WHEN PunktyRazem >= 20 THEN 'WARUNKOWO POZYTYWNA'
                            ELSE 'NEGATYWNA'
                        END AS Ocena
                    FROM [dbo].[OcenyDostawcow]
                    WHERE DostawcaID = @DostawcaID 
                        AND DataOceny < @AktualnaData
                    ORDER BY DataOceny DESC", connection);

                command.Parameters.AddWithValue("@DostawcaID", dostawcaId);
                command.Parameters.AddWithValue("@AktualnaData", aktualnaData);

                connection.Open();
                using var reader = command.ExecuteReader();

                if (reader.Read())
                {
                    return new OcenaPorownanieData
                    {
                        DataOceny = Convert.ToDateTime(reader["DataOceny"]),
                        PunktyRazem = Convert.ToInt32(reader["PunktyRazem"]),
                        Ocena = reader["Ocena"]?.ToString() ?? ""
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd pobierania poprzedniej oceny: {ex.Message}");
            }

            return null;
        }

        private static StatystykiDostawcy ObliczStatystykiDostawcy(string dostawcaId)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                using var command = new SqlCommand(@"
                    SELECT 
                        COUNT(*) AS LiczbaOcen,
                        AVG(CAST(PunktyRazem AS FLOAT)) AS SredniaPunktow,
                        MAX(PunktyRazem) AS NajwyzszaOcena,
                        MIN(PunktyRazem) AS NajnizszaOcena
                    FROM [dbo].[OcenyDostawcow]
                    WHERE DostawcaID = @DostawcaID 
                        AND DataOceny >= DATEADD(MONTH, -12, GETDATE())", connection);

                command.Parameters.AddWithValue("@DostawcaID", dostawcaId);

                connection.Open();
                using var reader = command.ExecuteReader();

                if (reader.Read())
                {
                    int liczbaOcen = Convert.ToInt32(reader["LiczbaOcen"]);
                    if (liczbaOcen == 0) return null;

                    double srednia = Convert.ToDouble(reader["SredniaPunktow"]);
                    int najwyzsza = Convert.ToInt32(reader["NajwyzszaOcena"]);
                    int najnizsza = Convert.ToInt32(reader["NajnizszaOcena"]);

                    // Oblicz trend
                    string trend = ObliczTrend(dostawcaId);

                    // Oblicz stabilność
                    double roznica = najwyzsza - najnizsza;
                    string stabilnosc = roznica <= 5 ? "wysoka" : (roznica <= 10 ? "średnia" : "niska");

                    return new StatystykiDostawcy
                    {
                        LiczbaOcen = liczbaOcen,
                        SredniaPunktow = srednia,
                        NajwyzszaOcena = najwyzsza,
                        NajnizszaOcena = najnizsza,
                        Trend = trend,
                        Stabilnosc = stabilnosc
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd obliczania statystyk: {ex.Message}");
            }

            return null;
        }

        private static string ObliczTrend(string dostawcaId)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                using var command = new SqlCommand(@"
                    SELECT TOP 5 PunktyRazem
                    FROM [dbo].[OcenyDostawcow]
                    WHERE DostawcaID = @DostawcaID
                    ORDER BY DataOceny DESC", connection);

                command.Parameters.AddWithValue("@DostawcaID", dostawcaId);

                connection.Open();
                var punkty = new List<int>();

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    punkty.Add(Convert.ToInt32(reader["PunktyRazem"]));
                }

                if (punkty.Count < 3) return "brak danych";

                // Prosty algorytm trendu - porównanie średniej z pierwszej i drugiej połowy
                int polowa = punkty.Count / 2;
                double sredniaStarsza = punkty.Take(polowa).Average();
                double sredniaNowsza = punkty.Skip(polowa).Average();

                if (sredniaNowsza > sredniaStarsza + 2) return "wyraźnie wzrostowy";
                if (sredniaNowsza > sredniaStarsza) return "lekko wzrostowy";
                if (sredniaNowsza < sredniaStarsza - 2) return "wyraźnie spadkowy";
                if (sredniaNowsza < sredniaStarsza) return "lekko spadkowy";
                return "stabilny";
            }
            catch
            {
                return "brak danych";
            }
        }

        #endregion

        #region Eksport do CSV

        /// <summary>
        /// Eksportuje dane oceny do CSV (dla Excela)
        /// </summary>
        public static string EksportujDoCSV(
            string dostawcaId,
            DateTime dataOd,
            DateTime dataDo,
            string sciezkaDoPliku)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                using var command = new SqlCommand(@"
                    SELECT 
                        d.Name AS Dostawca,
                        d.ShortName AS Skrot,
                        o.DataOceny,
                        o.NumerRaportu,
                        o.PunktySekcja1_5,
                        o.PunktySekcja6_20,
                        o.PunktyRazem,
                        CASE 
                            WHEN o.PunktyRazem >= 30 THEN 'POZYTYWNA'
                            WHEN o.PunktyRazem >= 20 THEN 'WARUNKOWO'
                            ELSE 'NEGATYWNA'
                        END AS Ocena,
                        o.Uwagi,
                        o.DataUtworzenia
                    FROM [dbo].[OcenyDostawcow] o
                    INNER JOIN [dbo].[Dostawcy] d ON o.DostawcaID = d.ID
                    WHERE o.DostawcaID = @DostawcaID
                        AND o.DataOceny BETWEEN @DataOd AND @DataDo
                    ORDER BY o.DataOceny DESC", connection);

                command.Parameters.AddWithValue("@DostawcaID", dostawcaId);
                command.Parameters.AddWithValue("@DataOd", dataOd);
                command.Parameters.AddWithValue("@DataDo", dataDo);

                connection.Open();
                using var reader = command.ExecuteReader();

                using var writer = new StreamWriter(sciezkaDoPliku, false, System.Text.Encoding.UTF8);

                // Nagłówek
                writer.WriteLine("Dostawca;Skrót;Data Oceny;Nr Raportu;Pkt 1-5;Pkt 6-20;Suma;Ocena;Uwagi;Data Utworzenia");

                // Dane
                while (reader.Read())
                {
                    writer.WriteLine($"{reader["Dostawca"]};{reader["Skrot"]};{reader["DataOceny"]:yyyy-MM-dd};" +
                        $"{reader["NumerRaportu"]};{reader["PunktySekcja1_5"]};{reader["PunktySekcja6_20"]};" +
                        $"{reader["PunktyRazem"]};{reader["Ocena"]};{reader["Uwagi"]?.ToString().Replace(";", ",")};" +
                        $"{reader["DataUtworzenia"]:yyyy-MM-dd HH:mm}");
                }

                return sciezkaDoPliku;
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd eksportu do CSV: {ex.Message}", ex);
            }
        }

        #endregion

        #region Klasy pomocnicze

        private class DostawcaInfo
        {
            public string ID { get; set; }
            public string Name { get; set; }
            public string ShortName { get; set; }
        }

        #endregion
    }
}
