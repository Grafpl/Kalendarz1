using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.PulpitZarzadu.Services
{
    public class KpiValue
    {
        public string Label { get; set; } = "";
        public string Value { get; set; } = "";
        public string SubText { get; set; } = "";
        public double? ChangePercent { get; set; }
    }

    public class KpiSection
    {
        public string Title { get; set; } = "";
        public List<KpiValue> Items { get; set; } = new();
        public List<(string Label, double Value)> ChartData { get; set; } = new();
        public bool HasError { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

    public static class PulpitDataService
    {
        private static readonly string ConnHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private static readonly string ConnLibraNet = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private static readonly string ConnTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private static readonly string ConnUnicard = @"Server=192.168.0.23\SQLEXPRESS;Database=UNISYSTEM;User Id=sa;Password=UniRCPAdmin123$;TrustServerCertificate=True";

        private const int MagazynMroznia = 65552;

        public static async Task<KpiSection> LoadMagazynMrozniAsync()
        {
            var section = new KpiSection { Title = "MAGAZYN MROZNI" };
            try
            {
                using var conn = new SqlConnection(ConnHandel);
                await conn.OpenAsync();

                // Stan mrozni total
                decimal stanTotal = 0;
                string sqlStan = @"SELECT CAST(ABS(SUM(iloscwp)) AS DECIMAL(18,1)) AS Stan
                    FROM [HANDEL].[HM].[MZ]
                    WHERE [data] >= '2023-01-01' AND [data] <= @Dzis
                      AND [magazyn] = @Mag AND typ = '0'";
                using (var cmd = new SqlCommand(sqlStan, conn))
                {
                    cmd.Parameters.AddWithValue("@Dzis", DateTime.Today);
                    cmd.Parameters.AddWithValue("@Mag", MagazynMroznia);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                        stanTotal = Convert.ToDecimal(result);
                }
                section.Items.Add(new KpiValue { Label = "Stan mrozni", Value = $"{stanTotal:#,##0} kg" });

                // Dzisiejsze wydania / przyjecia
                decimal wydaniaDzis = 0, przyjeciaDzis = 0;
                string sqlDzis = @"SELECT
                    SUM(CASE WHEN MZ.ilosc > 0 THEN MZ.ilosc ELSE 0 END) AS Wydano,
                    ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) AS Przyjeto
                    FROM [HANDEL].[HM].[MG]
                    JOIN [HANDEL].[HM].[MZ] ON MG.ID = MZ.super
                    WHERE MG.magazyn = @Mag AND CAST(MG.Data AS DATE) = @Dzis";
                using (var cmd = new SqlCommand(sqlDzis, conn))
                {
                    cmd.Parameters.AddWithValue("@Dzis", DateTime.Today);
                    cmd.Parameters.AddWithValue("@Mag", MagazynMroznia);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        wydaniaDzis = reader.IsDBNull(0) ? 0 : Convert.ToDecimal(reader.GetValue(0));
                        przyjeciaDzis = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1));
                    }
                }
                section.Items.Add(new KpiValue { Label = "Wydania dzis", Value = $"{wydaniaDzis:#,##0} kg" });
                section.Items.Add(new KpiValue { Label = "Przyjecia dzis", Value = $"{przyjeciaDzis:#,##0} kg" });

                // Chart: stan ostatnie 7 dni
                string sqlChart = @"SELECT CAST(MG.Data AS DATE) AS Dzien,
                    CAST(ABS(SUM(MZ.iloscwp)) AS DECIMAL(18,0)) AS Stan
                    FROM [HANDEL].[HM].[MG]
                    JOIN [HANDEL].[HM].[MZ] ON MG.ID = MZ.super
                    WHERE MG.magazyn = @Mag AND MG.Data >= @Od AND MG.Data <= @Do AND MZ.typ = '0'
                    GROUP BY CAST(MG.Data AS DATE)
                    ORDER BY Dzien";
                using (var cmd = new SqlCommand(sqlChart, conn))
                {
                    cmd.Parameters.AddWithValue("@Mag", MagazynMroznia);
                    cmd.Parameters.AddWithValue("@Od", DateTime.Today.AddDays(-6));
                    cmd.Parameters.AddWithValue("@Do", DateTime.Today);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var dzien = reader.GetDateTime(0);
                        var stan = Convert.ToDouble(reader.GetDecimal(1));
                        section.ChartData.Add((dzien.ToString("dd.MM"), stan));
                    }
                }
            }
            catch (Exception ex)
            {
                section.HasError = true;
                section.ErrorMessage = ex.Message;
            }
            return section;
        }

        public static async Task<KpiSection> LoadZamowieniaAsync()
        {
            var section = new KpiSection { Title = "ZAMOWIENIA" };
            try
            {
                using var conn = new SqlConnection(ConnLibraNet);
                await conn.OpenAsync();

                // Zamowienia dzisiaj
                int zamDzis = 0;
                decimal zamDzisKg = 0;
                string sql = @"SELECT COUNT(*) AS Szt, ISNULL(SUM(Ilosc), 0) AS Kg
                    FROM Zamowienia WHERE CAST(DataZamowienia AS DATE) = @Dzis";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Dzis", DateTime.Today);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        zamDzis = reader.GetInt32(0);
                        zamDzisKg = reader.GetDecimal(1);
                    }
                }
                section.Items.Add(new KpiValue { Label = "Zamowienia dzis", Value = $"{zamDzis} szt", SubText = $"{zamDzisKg:#,##0} kg" });

                // Zamowienia na jutro
                int zamJutro = 0;
                using (var cmd = new SqlCommand(@"SELECT COUNT(*) FROM Zamowienia WHERE CAST(DataRealizacji AS DATE) = @Jutro", conn))
                {
                    cmd.Parameters.AddWithValue("@Jutro", DateTime.Today.AddDays(1));
                    var result = await cmd.ExecuteScalarAsync();
                    zamJutro = result != null ? Convert.ToInt32(result) : 0;
                }
                section.Items.Add(new KpiValue { Label = "Na jutro", Value = $"{zamJutro} szt" });
            }
            catch (Exception ex)
            {
                section.HasError = true;
                section.ErrorMessage = ex.Message;
            }
            return section;
        }

        public static async Task<KpiSection> LoadSprzedazAsync()
        {
            var section = new KpiSection { Title = "SPRZEDAZ" };
            try
            {
                using var conn = new SqlConnection(ConnLibraNet);
                await conn.OpenAsync();

                var poczatekMiesiaca = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var poczatekPoprzedniego = poczatekMiesiaca.AddMonths(-1);

                // Sprzedaz biezacy miesiac
                decimal sprzKg = 0, sprzPln = 0;
                string sql = @"SELECT ISNULL(SUM(Ilosc), 0) AS Kg, ISNULL(SUM(WartoscNetto), 0) AS PLN
                    FROM DokumentySprzedazy
                    WHERE DataDokumentu >= @Od AND DataDokumentu < @Do AND TypDokumentu = 'FV'";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Od", poczatekMiesiaca);
                    cmd.Parameters.AddWithValue("@Do", DateTime.Today.AddDays(1));
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        sprzKg = reader.GetDecimal(0);
                        sprzPln = reader.GetDecimal(1);
                    }
                }
                section.Items.Add(new KpiValue { Label = "Sprzedaz miesiac", Value = $"{sprzKg:#,##0} kg", SubText = $"{sprzPln:#,##0} PLN" });

                // Porownanie z poprzednim
                decimal sprzPoprzedni = 0;
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Od", poczatekPoprzedniego);
                    cmd.Parameters.AddWithValue("@Do", poczatekMiesiaca);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        sprzPoprzedni = reader.GetDecimal(1);
                    }
                }
                double zmiana = sprzPoprzedni > 0 ? (double)((sprzPln - sprzPoprzedni) / sprzPoprzedni * 100) : 0;
                section.Items.Add(new KpiValue { Label = "vs poprzedni", Value = $"{zmiana:+0.0;-0.0}%", ChangePercent = zmiana });
            }
            catch (Exception ex)
            {
                section.HasError = true;
                section.ErrorMessage = ex.Message;
            }
            return section;
        }

        public static async Task<KpiSection> LoadProdukcjaAsync()
        {
            var section = new KpiSection { Title = "PRODUKCJA" };
            try
            {
                using var conn = new SqlConnection(ConnHandel);
                await conn.OpenAsync();

                // Przychod zywca dzisiejszy
                decimal przychodZywca = 0;
                string sql = @"SELECT CAST(ISNULL(SUM(ABS(iloscwp)), 0) AS DECIMAL(18,0))
                    FROM [HANDEL].[HM].[MZ]
                    JOIN [HANDEL].[HM].[MG] ON MG.ID = MZ.super
                    WHERE CAST(MG.Data AS DATE) = @Dzis AND MZ.ilosc < 0 AND MG.typ = '0'";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Dzis", DateTime.Today);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                        przychodZywca = Convert.ToDecimal(result);
                }
                section.Items.Add(new KpiValue { Label = "Przychod zywca", Value = $"{przychodZywca:#,##0} kg" });
            }
            catch (Exception ex)
            {
                section.HasError = true;
                section.ErrorMessage = ex.Message;
            }
            return section;
        }

        public static async Task<KpiSection> LoadTransportAsync()
        {
            var section = new KpiSection { Title = "TRANSPORT" };
            try
            {
                using var conn = new SqlConnection(ConnTransport);
                await conn.OpenAsync();

                // Kursy zaplanowane na dzis
                int kursy = 0;
                using (var cmd = new SqlCommand(@"SELECT COUNT(*) FROM Kursy WHERE CAST(DataKursu AS DATE) = @Dzis", conn))
                {
                    cmd.Parameters.AddWithValue("@Dzis", DateTime.Today);
                    var result = await cmd.ExecuteScalarAsync();
                    kursy = result != null ? Convert.ToInt32(result) : 0;
                }
                section.Items.Add(new KpiValue { Label = "Kursy dzis", Value = $"{kursy}" });

                // Klienci do obslugi
                int klienci = 0;
                using (var cmd = new SqlCommand(@"SELECT COUNT(DISTINCT IDOdbiorcy) FROM KursyPozycje KP
                    JOIN Kursy K ON K.ID = KP.IDKursu WHERE CAST(K.DataKursu AS DATE) = @Dzis", conn))
                {
                    cmd.Parameters.AddWithValue("@Dzis", DateTime.Today);
                    var result = await cmd.ExecuteScalarAsync();
                    klienci = result != null ? Convert.ToInt32(result) : 0;
                }
                section.Items.Add(new KpiValue { Label = "Klienci", Value = $"{klienci}" });
            }
            catch (Exception ex)
            {
                section.HasError = true;
                section.ErrorMessage = ex.Message;
            }
            return section;
        }

        public static async Task<KpiSection> LoadHrFrekwencjaAsync()
        {
            var section = new KpiSection { Title = "HR / FREKWENCJA" };
            try
            {
                using var conn = new SqlConnection(ConnUnicard);
                await conn.OpenAsync();

                // Obecnych pracownikow dzis (unikalne wejscia)
                int obecnych = 0;
                string sql = @"SELECT COUNT(DISTINCT KDINAR_EMPLOYEE_ID)
                    FROM V_KDINAR_ALL_REGISTRATIONS
                    WHERE CAST(KDINAR_REGISTRTN_DATETIME AS DATE) = @Dzis
                      AND KDINAR_REGISTRTN_TYPE = 'WEJSCIE'
                      AND KDINAR_EMPLOYEE_ID IS NOT NULL";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Dzis", DateTime.Today);
                    var result = await cmd.ExecuteScalarAsync();
                    obecnych = result != null ? Convert.ToInt32(result) : 0;
                }
                section.Items.Add(new KpiValue { Label = "Obecnych dzis", Value = $"{obecnych}" });

                // Lacznie pracownikow aktywnych
                int lacznie = 0;
                using (var cmd = new SqlCommand(@"SELECT COUNT(*) FROM V_RCINE_EMPLOYEES WHERE RCINE_EMPLOYEE_IS_ACTIVE = 1", conn))
                {
                    var result = await cmd.ExecuteScalarAsync();
                    lacznie = result != null ? Convert.ToInt32(result) : 0;
                }
                section.Items.Add(new KpiValue { Label = "Aktywnych", Value = $"{lacznie}" });

                // Frekwencja
                double frekwencja = lacznie > 0 ? Math.Round((double)obecnych / lacznie * 100, 1) : 0;
                section.Items.Add(new KpiValue { Label = "Frekwencja", Value = $"{frekwencja}%", ChangePercent = frekwencja });
            }
            catch (Exception ex)
            {
                section.HasError = true;
                section.ErrorMessage = ex.Message;
            }
            return section;
        }
    }
}
