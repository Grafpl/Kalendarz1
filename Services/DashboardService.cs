using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace Kalendarz1.Services
{
    /// <summary>
    /// Dashboard CEO - kluczowe wskaźniki KPI dla zarządu
    /// Zapewnia szybki przegląd stanu firmy
    /// </summary>
    public class DashboardService
    {
        private readonly string _connectionString;

        // Domyślny connection string do LibraNet
        private const string DEFAULT_CONNECTION_STRING =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public DashboardService(string connectionString = null)
        {
            _connectionString = connectionString ?? DEFAULT_CONNECTION_STRING;
        }

        /// <summary>
        /// Pobiera kompletny dashboard z wszystkimi KPI
        /// </summary>
        public DashboardData GetDashboard()
        {
            var dashboard = new DashboardData
            {
                DataWygenerowania = DateTime.Now,
                KpiDzisiaj = GetKpiDzisiaj(),
                KpiTydzien = GetKpiOkres(DateTime.Today.AddDays(-7), DateTime.Today),
                KpiMiesiac = GetKpiOkres(DateTime.Today.AddMonths(-1), DateTime.Today),
                KpiRok = GetKpiOkres(DateTime.Today.AddYears(-1), DateTime.Today),
                TopHodowcyMiesiac = GetTopHodowcy(5, DateTime.Today.AddMonths(-1)),
                OstatnieDostawy = GetOstatnieDostawy(10),
                TrendTygodniowy = GetTrendTygodniowy(),
                AlertyOperacyjne = GetAlerty()
            };

            return dashboard;
        }

        /// <summary>
        /// KPI za dzisiaj
        /// </summary>
        public KpiData GetKpiDzisiaj()
        {
            return GetKpiOkres(DateTime.Today, DateTime.Today.AddDays(1).AddSeconds(-1));
        }

        /// <summary>
        /// KPI za wybrany okres
        /// </summary>
        public KpiData GetKpiOkres(DateTime od, DateTime do_)
        {
            var kpi = new KpiData
            {
                OkresOd = od,
                OkresDo = do_
            };

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand(@"
                        SELECT
                            COUNT(*) as LiczbaDostawSuma,
                            COUNT(DISTINCT CustomerGID) as LiczbaHodowcow,
                            ISNULL(SUM(LumQnt), 0) as SztukiSuma,
                            ISNULL(SUM(NettoWeight), 0) as WagaNettoSuma,
                            ISNULL(SUM((Price + ISNULL(Addition, 0)) * NettoWeight * (1 - ISNULL(Loss, 0) / 100.0)), 0) as WartoscSuma,
                            ISNULL(AVG(Price + ISNULL(Addition, 0)), 0) as SredniaCena,
                            ISNULL(AVG(ISNULL(Loss, 0)), 0) as SredniUbytek,
                            ISNULL(SUM(IncDeadConf), 0) as Padniete,
                            COUNT(DISTINCT DriverGID) as AktywniKierowcy
                        FROM [LibraNet].[dbo].[FarmerCalc]
                        WHERE DataPrzyjecia BETWEEN @od AND @do", conn);

                    cmd.Parameters.AddWithValue("@od", od);
                    cmd.Parameters.AddWithValue("@do", do_);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            kpi.LiczbaDostawSuma = Convert.ToInt32(reader["LiczbaDostawSuma"]);
                            kpi.LiczbaHodowcow = Convert.ToInt32(reader["LiczbaHodowcow"]);
                            kpi.SztukiSuma = Convert.ToInt32(reader["SztukiSuma"]);
                            kpi.WagaNettoSuma = Convert.ToDecimal(reader["WagaNettoSuma"]);
                            kpi.WartoscSuma = Convert.ToDecimal(reader["WartoscSuma"]);
                            kpi.SredniaCena = Convert.ToDecimal(reader["SredniaCena"]);
                            kpi.SredniUbytek = Convert.ToDecimal(reader["SredniUbytek"]);
                            kpi.Padniete = Convert.ToInt32(reader["Padniete"]);
                            kpi.AktywniKierowcy = Convert.ToInt32(reader["AktywniKierowcy"]);
                        }
                    }

                    // Oblicz średnią wagę sztuki
                    kpi.SredniaWagaSztuki = kpi.SztukiSuma > 0
                        ? kpi.WagaNettoSuma / kpi.SztukiSuma : 0;

                    // Pobierz porównanie z poprzednim okresem (ta sama długość)
                    var dlugoscOkresu = (do_ - od).TotalDays;
                    var poprzedniOd = od.AddDays(-dlugoscOkresu);
                    var poprzedniDo = od.AddSeconds(-1);

                    var cmdPoprz = new SqlCommand(@"
                        SELECT
                            ISNULL(SUM(NettoWeight), 0) as WagaPoprzednia,
                            ISNULL(SUM((Price + ISNULL(Addition, 0)) * NettoWeight * (1 - ISNULL(Loss, 0) / 100.0)), 0) as WartoscPoprzednia
                        FROM [LibraNet].[dbo].[FarmerCalc]
                        WHERE DataPrzyjecia BETWEEN @od AND @do", conn);

                    cmdPoprz.Parameters.AddWithValue("@od", poprzedniOd);
                    cmdPoprz.Parameters.AddWithValue("@do", poprzedniDo);

                    using (var reader = cmdPoprz.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var wagaPoprz = Convert.ToDecimal(reader["WagaPoprzednia"]);
                            var wartoscPoprz = Convert.ToDecimal(reader["WartoscPoprzednia"]);

                            kpi.ZmianaWagiProcent = wagaPoprz > 0
                                ? (kpi.WagaNettoSuma - wagaPoprz) / wagaPoprz * 100 : 0;
                            kpi.ZmianaWartosciProcent = wartoscPoprz > 0
                                ? (kpi.WartoscSuma - wartoscPoprz) / wartoscPoprz * 100 : 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd KPI: {ex.Message}");
            }

            return kpi;
        }

        /// <summary>
        /// Top N hodowców za okres
        /// </summary>
        public List<TopHodowca> GetTopHodowcy(int top, DateTime? odDaty = null)
        {
            odDaty ??= DateTime.Today.AddMonths(-1);
            var wyniki = new List<TopHodowca>();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand($@"
                        SELECT TOP {top}
                            c.Name as Nazwa,
                            c.City as Miasto,
                            SUM(fc.NettoWeight) as WagaSuma,
                            SUM((fc.Price + ISNULL(fc.Addition, 0)) * fc.NettoWeight) as WartoscSuma,
                            COUNT(*) as LiczbaDostaw
                        FROM [LibraNet].[dbo].[FarmerCalc] fc
                        JOIN [LibraNet].[dbo].[Customer] c ON fc.CustomerGID = c.GID
                        WHERE fc.DataPrzyjecia >= @od
                        GROUP BY c.Name, c.City
                        ORDER BY SUM(fc.NettoWeight) DESC", conn);

                    cmd.Parameters.AddWithValue("@od", odDaty.Value);

                    int poz = 1;
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            wyniki.Add(new TopHodowca
                            {
                                Pozycja = poz++,
                                Nazwa = reader["Nazwa"]?.ToString(),
                                Miasto = reader["Miasto"]?.ToString(),
                                WagaSuma = Convert.ToDecimal(reader["WagaSuma"]),
                                WartoscSuma = Convert.ToDecimal(reader["WartoscSuma"]),
                                LiczbaDostaw = Convert.ToInt32(reader["LiczbaDostaw"])
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd Top Hodowcy: {ex.Message}");
            }

            return wyniki;
        }

        /// <summary>
        /// Ostatnie N dostaw
        /// </summary>
        public List<OstatniaDostawaInfo> GetOstatnieDostawy(int limit = 10)
        {
            var wyniki = new List<OstatniaDostawaInfo>();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand($@"
                        SELECT TOP {limit}
                            fc.ID,
                            fc.DataPrzyjecia,
                            c.Name as Hodowca,
                            fc.LumQnt as Sztuki,
                            fc.NettoWeight as Waga,
                            (fc.Price + ISNULL(fc.Addition, 0)) * fc.NettoWeight * (1 - ISNULL(fc.Loss, 0) / 100.0) as Wartosc,
                            d.Name as Kierowca
                        FROM [LibraNet].[dbo].[FarmerCalc] fc
                        JOIN [LibraNet].[dbo].[Customer] c ON fc.CustomerGID = c.GID
                        LEFT JOIN [LibraNet].[dbo].[Drivers] d ON fc.DriverGID = d.GID
                        ORDER BY fc.DataPrzyjecia DESC, fc.ID DESC", conn);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            wyniki.Add(new OstatniaDostawaInfo
                            {
                                Id = Convert.ToInt32(reader["ID"]),
                                Data = reader["DataPrzyjecia"] as DateTime? ?? DateTime.MinValue,
                                Hodowca = reader["Hodowca"]?.ToString(),
                                Sztuki = Convert.ToInt32(reader["Sztuki"]),
                                Waga = Convert.ToDecimal(reader["Waga"]),
                                Wartosc = Convert.ToDecimal(reader["Wartosc"]),
                                Kierowca = reader["Kierowca"]?.ToString()
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd Ostatnie Dostawy: {ex.Message}");
            }

            return wyniki;
        }

        /// <summary>
        /// Trend tygodniowy (ostatnie 8 tygodni)
        /// </summary>
        public List<TrendTygodniowy> GetTrendTygodniowy()
        {
            var wyniki = new List<TrendTygodniowy>();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand(@"
                        SELECT
                            DATEPART(YEAR, DataPrzyjecia) as Rok,
                            DATEPART(WEEK, DataPrzyjecia) as Tydzien,
                            MIN(DataPrzyjecia) as PoczatekTygodnia,
                            COUNT(*) as LiczbaDostaw,
                            SUM(LumQnt) as Sztuki,
                            SUM(NettoWeight) as Waga,
                            SUM((Price + ISNULL(Addition, 0)) * NettoWeight * (1 - ISNULL(Loss, 0) / 100.0)) as Wartosc,
                            AVG(Price + ISNULL(Addition, 0)) as SredniaCena,
                            AVG(ISNULL(Loss, 0)) as SredniUbytek
                        FROM [LibraNet].[dbo].[FarmerCalc]
                        WHERE DataPrzyjecia >= DATEADD(WEEK, -8, GETDATE())
                        GROUP BY DATEPART(YEAR, DataPrzyjecia), DATEPART(WEEK, DataPrzyjecia)
                        ORDER BY Rok, Tydzien", conn);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            wyniki.Add(new TrendTygodniowy
                            {
                                Rok = Convert.ToInt32(reader["Rok"]),
                                NumerTygodnia = Convert.ToInt32(reader["Tydzien"]),
                                PoczatekTygodnia = reader["PoczatekTygodnia"] as DateTime? ?? DateTime.MinValue,
                                LiczbaDostaw = Convert.ToInt32(reader["LiczbaDostaw"]),
                                Sztuki = Convert.ToInt32(reader["Sztuki"]),
                                Waga = Convert.ToDecimal(reader["Waga"]),
                                Wartosc = Convert.ToDecimal(reader["Wartosc"]),
                                SredniaCena = Convert.ToDecimal(reader["SredniaCena"]),
                                SredniUbytek = Convert.ToDecimal(reader["SredniUbytek"])
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd Trend: {ex.Message}");
            }

            return wyniki;
        }

        /// <summary>
        /// Pobiera alerty operacyjne
        /// </summary>
        public List<AlertOperacyjny> GetAlerty()
        {
            var alerty = new List<AlertOperacyjny>();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // Alert: Wysoki ubytek (>5%)
                    var cmdUbytek = new SqlCommand(@"
                        SELECT TOP 5
                            c.Name as Hodowca,
                            fc.DataPrzyjecia,
                            fc.Loss as Ubytek
                        FROM [LibraNet].[dbo].[FarmerCalc] fc
                        JOIN [LibraNet].[dbo].[Customer] c ON fc.CustomerGID = c.GID
                        WHERE fc.Loss > 5
                          AND fc.DataPrzyjecia >= DATEADD(DAY, -7, GETDATE())
                        ORDER BY fc.Loss DESC", conn);

                    using (var reader = cmdUbytek.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            alerty.Add(new AlertOperacyjny
                            {
                                Typ = AlertTyp.WysokiUbytek,
                                Priorytet = AlertPriorytet.Wysoki,
                                Tytul = "Wysoki ubytek",
                                Opis = $"{reader["Hodowca"]} - ubytek {reader["Ubytek"]:F1}%",
                                Data = reader["DataPrzyjecia"] as DateTime? ?? DateTime.MinValue
                            });
                        }
                    }

                    // Alert: Padnięcia powyżej normy
                    var cmdPadniecia = new SqlCommand(@"
                        SELECT TOP 5
                            c.Name as Hodowca,
                            fc.DataPrzyjecia,
                            fc.IncDeadConf as Padniete,
                            fc.LumQnt as Sztuki
                        FROM [LibraNet].[dbo].[FarmerCalc] fc
                        JOIN [LibraNet].[dbo].[Customer] c ON fc.CustomerGID = c.GID
                        WHERE fc.IncDeadConf > 0
                          AND fc.DataPrzyjecia >= DATEADD(DAY, -7, GETDATE())
                        ORDER BY fc.IncDeadConf DESC", conn);

                    using (var reader = cmdPadniecia.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var padniete = Convert.ToInt32(reader["Padniete"]);
                            var sztuki = Convert.ToInt32(reader["Sztuki"]);
                            var procent = sztuki > 0 ? (decimal)padniete / sztuki * 100 : 0;

                            if (procent > 1) // Powyżej 1% to alert
                            {
                                alerty.Add(new AlertOperacyjny
                                {
                                    Typ = AlertTyp.Padniecia,
                                    Priorytet = procent > 3 ? AlertPriorytet.Krytyczny : AlertPriorytet.Wysoki,
                                    Tytul = "Padnięcia",
                                    Opis = $"{reader["Hodowca"]} - {padniete} szt ({procent:F1}%)",
                                    Data = reader["DataPrzyjecia"] as DateTime? ?? DateTime.MinValue
                                });
                            }
                        }
                    }

                    // Alert: Brak dostaw od stałego dostawcy (>14 dni)
                    var cmdBrakDostaw = new SqlCommand(@"
                        SELECT
                            c.Name as Hodowca,
                            MAX(fc.DataPrzyjecia) as OstatniaDostwa,
                            COUNT(*) as LiczbaDostawHistorycznie
                        FROM [LibraNet].[dbo].[FarmerCalc] fc
                        JOIN [LibraNet].[dbo].[Customer] c ON fc.CustomerGID = c.GID
                        GROUP BY c.Name
                        HAVING COUNT(*) >= 10
                           AND MAX(fc.DataPrzyjecia) < DATEADD(DAY, -14, GETDATE())
                           AND MAX(fc.DataPrzyjecia) > DATEADD(MONTH, -3, GETDATE())", conn);

                    using (var reader = cmdBrakDostaw.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var ostatnia = reader["OstatniaDostwa"] as DateTime? ?? DateTime.MinValue;
                            var dniTemu = (DateTime.Today - ostatnia.Date).Days;

                            alerty.Add(new AlertOperacyjny
                            {
                                Typ = AlertTyp.BrakDostaw,
                                Priorytet = AlertPriorytet.Sredni,
                                Tytul = "Brak dostaw",
                                Opis = $"{reader["Hodowca"]} - ostatnia dostawa {dniTemu} dni temu",
                                Data = ostatnia
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd Alerty: {ex.Message}");
            }

            // Sortuj według priorytetu
            return alerty.OrderByDescending(a => (int)a.Priorytet).ThenByDescending(a => a.Data).ToList();
        }
    }

    #region Data Models

    public class DashboardData
    {
        public DateTime DataWygenerowania { get; set; }
        public KpiData KpiDzisiaj { get; set; }
        public KpiData KpiTydzien { get; set; }
        public KpiData KpiMiesiac { get; set; }
        public KpiData KpiRok { get; set; }
        public List<TopHodowca> TopHodowcyMiesiac { get; set; }
        public List<OstatniaDostawaInfo> OstatnieDostawy { get; set; }
        public List<TrendTygodniowy> TrendTygodniowy { get; set; }
        public List<AlertOperacyjny> AlertyOperacyjne { get; set; }
    }

    public class KpiData
    {
        public DateTime OkresOd { get; set; }
        public DateTime OkresDo { get; set; }

        public int LiczbaDostawSuma { get; set; }
        public int LiczbaHodowcow { get; set; }
        public int SztukiSuma { get; set; }
        public decimal WagaNettoSuma { get; set; }
        public decimal WartoscSuma { get; set; }
        public decimal SredniaCena { get; set; }
        public decimal SredniaWagaSztuki { get; set; }
        public decimal SredniUbytek { get; set; }
        public int Padniete { get; set; }
        public int AktywniKierowcy { get; set; }

        // Porównanie z poprzednim okresem
        public decimal ZmianaWagiProcent { get; set; }
        public decimal ZmianaWartosciProcent { get; set; }

        public string WartoscFormatowana => $"{WartoscSuma:N0} zł";
        public string WagaFormatowana => $"{WagaNettoSuma:N0} kg";
    }

    public class TopHodowca
    {
        public int Pozycja { get; set; }
        public string Nazwa { get; set; }
        public string Miasto { get; set; }
        public decimal WagaSuma { get; set; }
        public decimal WartoscSuma { get; set; }
        public int LiczbaDostaw { get; set; }
    }

    public class OstatniaDostawaInfo
    {
        public int Id { get; set; }
        public DateTime Data { get; set; }
        public string Hodowca { get; set; }
        public int Sztuki { get; set; }
        public decimal Waga { get; set; }
        public decimal Wartosc { get; set; }
        public string Kierowca { get; set; }
    }

    public class TrendTygodniowy
    {
        public int Rok { get; set; }
        public int NumerTygodnia { get; set; }
        public DateTime PoczatekTygodnia { get; set; }
        public int LiczbaDostaw { get; set; }
        public int Sztuki { get; set; }
        public decimal Waga { get; set; }
        public decimal Wartosc { get; set; }
        public decimal SredniaCena { get; set; }
        public decimal SredniUbytek { get; set; }

        public string Etykieta => $"Tydzień {NumerTygodnia}";
    }

    public class AlertOperacyjny
    {
        public AlertTyp Typ { get; set; }
        public AlertPriorytet Priorytet { get; set; }
        public string Tytul { get; set; }
        public string Opis { get; set; }
        public DateTime Data { get; set; }

        public string IkonaTypu => Typ switch
        {
            AlertTyp.WysokiUbytek => "⚠️",
            AlertTyp.Padniecia => "💀",
            AlertTyp.BrakDostaw => "📭",
            AlertTyp.NiskaJakosc => "👎",
            _ => "ℹ️"
        };
    }

    public enum AlertTyp
    {
        WysokiUbytek,
        Padniecia,
        BrakDostaw,
        NiskaJakosc,
        Inne
    }

    public enum AlertPriorytet
    {
        Niski = 1,
        Sredni = 2,
        Wysoki = 3,
        Krytyczny = 4
    }

    #endregion
}
