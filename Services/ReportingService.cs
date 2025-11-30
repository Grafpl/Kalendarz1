using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace Kalendarz1.Services
{
    /// <summary>
    /// Serwis raportów analitycznych dla działu rozliczeń
    /// Obsługuje: rentowność hodowców, ranking, straty, efektywność kierowców
    /// </summary>
    public class ReportingService
    {
        private readonly string _connectionString;

        // Domyślny connection string do LibraNet
        private const string DEFAULT_CONNECTION_STRING =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public ReportingService(string connectionString = null)
        {
            _connectionString = connectionString ?? DEFAULT_CONNECTION_STRING;
        }

        #region 30. Raport rentowności hodowcy

        /// <summary>
        /// Pobiera raport rentowności dla konkretnego hodowcy
        /// </summary>
        public RaportRentownosci GetRentownoscHodowcy(int hodowcaId, DateTime? odDaty = null, DateTime? doDaty = null)
        {
            odDaty ??= DateTime.Today.AddYears(-1);
            doDaty ??= DateTime.Today;

            var raport = new RaportRentownosci
            {
                HodowcaId = hodowcaId,
                OkresOd = odDaty.Value,
                OkresDo = doDaty.Value,
                Dostawy = new List<DostawaRentownosc>()
            };

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // Pobierz dane hodowcy
                    var cmdHodowca = new SqlCommand(@"
                        SELECT c.Name, c.City, c.Address
                        FROM [LibraNet].[dbo].[Customer] c
                        WHERE c.GID = @gid", conn);
                    cmdHodowca.Parameters.AddWithValue("@gid", hodowcaId);

                    using (var reader = cmdHodowca.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            raport.HodowcaNazwa = reader["Name"]?.ToString();
                            raport.HodowcaMiasto = reader["City"]?.ToString();
                        }
                    }

                    // Pobierz wszystkie dostawy
                    var cmdDostawy = new SqlCommand(@"
                        SELECT
                            fc.ID,
                            fc.DataPrzyjecia,
                            fc.LumQnt as Sztuki,
                            fc.NettoWeight as WagaNetto,
                            fc.Price as CenaZaKg,
                            fc.Addition as Dodatek,
                            fc.Loss as Ubytek,
                            (fc.Price + ISNULL(fc.Addition, 0)) * fc.NettoWeight * (1 - ISNULL(fc.Loss, 0) / 100.0) as Wartosc
                        FROM [LibraNet].[dbo].[FarmerCalc] fc
                        WHERE fc.CustomerGID = @gid
                          AND fc.DataPrzyjecia BETWEEN @od AND @do
                        ORDER BY fc.DataPrzyjecia DESC", conn);

                    cmdDostawy.Parameters.AddWithValue("@gid", hodowcaId);
                    cmdDostawy.Parameters.AddWithValue("@od", odDaty.Value);
                    cmdDostawy.Parameters.AddWithValue("@do", doDaty.Value);

                    using (var reader = cmdDostawy.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            raport.Dostawy.Add(new DostawaRentownosc
                            {
                                Id = (int)reader["ID"],
                                Data = reader["DataPrzyjecia"] as DateTime? ?? DateTime.MinValue,
                                Sztuki = Convert.ToInt32(reader["Sztuki"]),
                                WagaNetto = Convert.ToDecimal(reader["WagaNetto"]),
                                CenaZaKg = Convert.ToDecimal(reader["CenaZaKg"]),
                                Dodatek = reader["Dodatek"] as decimal? ?? 0,
                                Ubytek = reader["Ubytek"] as decimal? ?? 0,
                                Wartosc = Convert.ToDecimal(reader["Wartosc"])
                            });
                        }
                    }
                }

                // Oblicz statystyki
                if (raport.Dostawy.Any())
                {
                    raport.LiczbaDostawSuma = raport.Dostawy.Count;
                    raport.SztukiSuma = raport.Dostawy.Sum(d => d.Sztuki);
                    raport.WagaNettoSuma = raport.Dostawy.Sum(d => d.WagaNetto);
                    raport.WartoscSuma = raport.Dostawy.Sum(d => d.Wartosc);
                    raport.SredniaWagaSztuki = raport.WagaNettoSuma / raport.SztukiSuma;
                    raport.SredniaCenaZaKg = raport.Dostawy.Average(d => d.CenaZaKg + d.Dodatek);
                    raport.SredniUbytek = raport.Dostawy.Average(d => d.Ubytek);
                    raport.SredniaWartoscDostawy = raport.WartoscSuma / raport.LiczbaDostawSuma;

                    // Trend (porównanie ostatnich 3 miesięcy z poprzednimi)
                    var ostatnie3Mies = raport.Dostawy
                        .Where(d => d.Data >= DateTime.Today.AddMonths(-3))
                        .ToList();
                    var poprzednie3Mies = raport.Dostawy
                        .Where(d => d.Data >= DateTime.Today.AddMonths(-6) && d.Data < DateTime.Today.AddMonths(-3))
                        .ToList();

                    if (ostatnie3Mies.Any() && poprzednie3Mies.Any())
                    {
                        var sredniaOstatnia = ostatnie3Mies.Average(d => d.WagaNetto);
                        var sredniaPoprzednia = poprzednie3Mies.Average(d => d.WagaNetto);
                        raport.TrendWagi = (sredniaOstatnia - sredniaPoprzednia) / sredniaPoprzednia * 100;
                    }
                }
            }
            catch (Exception ex)
            {
                raport.Blad = ex.Message;
            }

            return raport;
        }

        #endregion

        #region 34. Ranking hodowców

        /// <summary>
        /// Pobiera ranking hodowców według wybranego kryterium
        /// </summary>
        public List<RankingHodowcy> GetRankingHodowcow(
            RankingKryterium kryterium = RankingKryterium.Wartosc,
            DateTime? odDaty = null,
            DateTime? doDaty = null,
            int top = 50)
        {
            odDaty ??= DateTime.Today.AddYears(-1);
            doDaty ??= DateTime.Today;

            var ranking = new List<RankingHodowcy>();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string orderBy = kryterium switch
                    {
                        RankingKryterium.Wartosc => "WartoscSuma DESC",
                        RankingKryterium.Waga => "WagaSuma DESC",
                        RankingKryterium.Sztuki => "SztukiSuma DESC",
                        RankingKryterium.SredniaWagaSztuki => "SredniaWagaSztuki DESC",
                        RankingKryterium.LiczbaDostawL => "LiczbaDostaw DESC",
                        RankingKryterium.NajnizszaStrata => "SredniUbytek ASC",
                        _ => "WartoscSuma DESC"
                    };

                    var cmd = new SqlCommand($@"
                        SELECT TOP {top}
                            c.GID as HodowcaId,
                            c.Name as Nazwa,
                            c.City as Miasto,
                            COUNT(*) as LiczbaDostaw,
                            SUM(fc.LumQnt) as SztukiSuma,
                            SUM(fc.NettoWeight) as WagaSuma,
                            SUM((fc.Price + ISNULL(fc.Addition, 0)) * fc.NettoWeight * (1 - ISNULL(fc.Loss, 0) / 100.0)) as WartoscSuma,
                            AVG(fc.NettoWeight / NULLIF(fc.LumQnt, 0)) as SredniaWagaSztuki,
                            AVG(fc.Price + ISNULL(fc.Addition, 0)) as SredniaCena,
                            AVG(ISNULL(fc.Loss, 0)) as SredniUbytek
                        FROM [LibraNet].[dbo].[FarmerCalc] fc
                        JOIN [LibraNet].[dbo].[Customer] c ON fc.CustomerGID = c.GID
                        WHERE fc.DataPrzyjecia BETWEEN @od AND @do
                        GROUP BY c.GID, c.Name, c.City
                        HAVING COUNT(*) > 0
                        ORDER BY {orderBy}", conn);

                    cmd.Parameters.AddWithValue("@od", odDaty.Value);
                    cmd.Parameters.AddWithValue("@do", doDaty.Value);

                    int pozycja = 1;
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ranking.Add(new RankingHodowcy
                            {
                                Pozycja = pozycja++,
                                HodowcaId = (int)reader["HodowcaId"],
                                Nazwa = reader["Nazwa"]?.ToString(),
                                Miasto = reader["Miasto"]?.ToString(),
                                LiczbaDostaw = Convert.ToInt32(reader["LiczbaDostaw"]),
                                SztukiSuma = Convert.ToInt32(reader["SztukiSuma"]),
                                WagaSuma = Convert.ToDecimal(reader["WagaSuma"]),
                                WartoscSuma = Convert.ToDecimal(reader["WartoscSuma"]),
                                SredniaWagaSztuki = reader["SredniaWagaSztuki"] as decimal? ?? 0,
                                SredniaCena = reader["SredniaCena"] as decimal? ?? 0,
                                SredniUbytek = reader["SredniUbytek"] as decimal? ?? 0
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd rankingu: {ex.Message}");
            }

            return ranking;
        }

        public enum RankingKryterium
        {
            Wartosc,
            Waga,
            Sztuki,
            SredniaWagaSztuki,
            LiczbaDostawL,
            NajnizszaStrata
        }

        #endregion

        #region 37. Raport strat

        /// <summary>
        /// Generuje raport strat (ubytki, padnięcia, odrzucenia)
        /// </summary>
        public RaportStrat GetRaportStrat(DateTime? odDaty = null, DateTime? doDaty = null)
        {
            odDaty ??= DateTime.Today.AddMonths(-1);
            doDaty ??= DateTime.Today;

            var raport = new RaportStrat
            {
                OkresOd = odDaty.Value,
                OkresDo = doDaty.Value,
                StratyPoHodowcach = new List<StrataHodowca>(),
                StratyPoDniach = new List<StrataDzien>()
            };

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // Straty według hodowców
                    var cmdHodowcy = new SqlCommand(@"
                        SELECT
                            c.Name as Hodowca,
                            c.City as Miasto,
                            COUNT(*) as LiczbaDostaw,
                            SUM(fc.LumQnt) as SztukiRazem,
                            SUM(fc.NettoWeight) as WagaRazem,
                            AVG(ISNULL(fc.Loss, 0)) as SredniUbytek,
                            SUM(fc.NettoWeight * ISNULL(fc.Loss, 0) / 100.0) as StrataWagowaKg,
                            SUM((fc.Price + ISNULL(fc.Addition, 0)) * fc.NettoWeight * ISNULL(fc.Loss, 0) / 100.0) as StrataKwotowa,
                            SUM(fc.IncDeadConf) as Padniete
                        FROM [LibraNet].[dbo].[FarmerCalc] fc
                        JOIN [LibraNet].[dbo].[Customer] c ON fc.CustomerGID = c.GID
                        WHERE fc.DataPrzyjecia BETWEEN @od AND @do
                        GROUP BY c.Name, c.City
                        HAVING AVG(ISNULL(fc.Loss, 0)) > 0 OR SUM(fc.IncDeadConf) > 0
                        ORDER BY SUM((fc.Price + ISNULL(fc.Addition, 0)) * fc.NettoWeight * ISNULL(fc.Loss, 0) / 100.0) DESC", conn);

                    cmdHodowcy.Parameters.AddWithValue("@od", odDaty.Value);
                    cmdHodowcy.Parameters.AddWithValue("@do", doDaty.Value);

                    using (var reader = cmdHodowcy.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            raport.StratyPoHodowcach.Add(new StrataHodowca
                            {
                                Hodowca = reader["Hodowca"]?.ToString(),
                                Miasto = reader["Miasto"]?.ToString(),
                                LiczbaDostaw = Convert.ToInt32(reader["LiczbaDostaw"]),
                                SztukiRazem = Convert.ToInt32(reader["SztukiRazem"]),
                                WagaRazem = Convert.ToDecimal(reader["WagaRazem"]),
                                SredniUbytekProcent = Convert.ToDecimal(reader["SredniUbytek"]),
                                StrataWagowaKg = Convert.ToDecimal(reader["StrataWagowaKg"]),
                                StrataKwotowa = Convert.ToDecimal(reader["StrataKwotowa"]),
                                Padniete = Convert.ToInt32(reader["Padniete"])
                            });
                        }
                    }

                    // Straty według dni
                    var cmdDni = new SqlCommand(@"
                        SELECT
                            CAST(fc.DataPrzyjecia as DATE) as Dzien,
                            COUNT(*) as LiczbaDostaw,
                            SUM(fc.LumQnt) as Sztuki,
                            AVG(ISNULL(fc.Loss, 0)) as SredniUbytek,
                            SUM(fc.NettoWeight * ISNULL(fc.Loss, 0) / 100.0) as StrataKg,
                            SUM(fc.IncDeadConf) as Padniete
                        FROM [LibraNet].[dbo].[FarmerCalc] fc
                        WHERE fc.DataPrzyjecia BETWEEN @od AND @do
                        GROUP BY CAST(fc.DataPrzyjecia as DATE)
                        ORDER BY CAST(fc.DataPrzyjecia as DATE)", conn);

                    cmdDni.Parameters.AddWithValue("@od", odDaty.Value);
                    cmdDni.Parameters.AddWithValue("@do", doDaty.Value);

                    using (var reader = cmdDni.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            raport.StratyPoDniach.Add(new StrataDzien
                            {
                                Dzien = (DateTime)reader["Dzien"],
                                LiczbaDostaw = Convert.ToInt32(reader["LiczbaDostaw"]),
                                Sztuki = Convert.ToInt32(reader["Sztuki"]),
                                SredniUbytek = Convert.ToDecimal(reader["SredniUbytek"]),
                                StrataKg = Convert.ToDecimal(reader["StrataKg"]),
                                Padniete = Convert.ToInt32(reader["Padniete"])
                            });
                        }
                    }

                    // Podsumowanie
                    raport.SumaStrataKg = raport.StratyPoHodowcach.Sum(s => s.StrataWagowaKg);
                    raport.SumaStrataZl = raport.StratyPoHodowcach.Sum(s => s.StrataKwotowa);
                    raport.SumaPadniete = raport.StratyPoHodowcach.Sum(s => s.Padniete);
                    raport.SredniUbytekOgolem = raport.StratyPoHodowcach.Any()
                        ? raport.StratyPoHodowcach.Average(s => s.SredniUbytekProcent) : 0;
                }
            }
            catch (Exception ex)
            {
                raport.Blad = ex.Message;
            }

            return raport;
        }

        #endregion

        #region 39. Efektywność kierowców

        /// <summary>
        /// Generuje raport efektywności kierowców
        /// </summary>
        public List<EfektywnoscKierowcy> GetEfektywnoscKierowcow(DateTime? odDaty = null, DateTime? doDaty = null)
        {
            odDaty ??= DateTime.Today.AddMonths(-1);
            doDaty ??= DateTime.Today;

            var wyniki = new List<EfektywnoscKierowcy>();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand(@"
                        SELECT
                            d.Name as Kierowca,
                            COUNT(DISTINCT fc.ID) as LiczbaKursow,
                            COUNT(DISTINCT fc.CustomerGID) as LiczbaHodowcow,
                            SUM(fc.LumQnt) as SztukiRazem,
                            SUM(fc.NettoWeight) as WagaRazem,
                            SUM((fc.Price + ISNULL(fc.Addition, 0)) * fc.NettoWeight) as WartoscRazem,
                            AVG(ISNULL(fc.Loss, 0)) as SredniUbytek,
                            SUM(fc.IncDeadConf) as PadnieteRazem,
                            MIN(fc.DataPrzyjecia) as PierwszyKurs,
                            MAX(fc.DataPrzyjecia) as OstatniKurs,
                            AVG(DATEDIFF(MINUTE, fc.Przyjazd, fc.ZalaLoadedDT)) as SredniCzasZaladunkuMin
                        FROM [LibraNet].[dbo].[FarmerCalc] fc
                        JOIN [LibraNet].[dbo].[Drivers] d ON fc.DriverGID = d.GID
                        WHERE fc.DataPrzyjecia BETWEEN @od AND @do
                          AND d.Name IS NOT NULL
                        GROUP BY d.Name
                        ORDER BY SUM(fc.NettoWeight) DESC", conn);

                    cmd.Parameters.AddWithValue("@od", odDaty.Value);
                    cmd.Parameters.AddWithValue("@do", doDaty.Value);

                    using (var reader = cmd.ExecuteReader())
                    {
                        int pozycja = 1;
                        while (reader.Read())
                        {
                            var kierowca = new EfektywnoscKierowcy
                            {
                                Pozycja = pozycja++,
                                Kierowca = reader["Kierowca"]?.ToString(),
                                LiczbaKursow = Convert.ToInt32(reader["LiczbaKursow"]),
                                LiczbaHodowcow = Convert.ToInt32(reader["LiczbaHodowcow"]),
                                SztukiRazem = Convert.ToInt32(reader["SztukiRazem"]),
                                WagaRazem = Convert.ToDecimal(reader["WagaRazem"]),
                                WartoscRazem = Convert.ToDecimal(reader["WartoscRazem"]),
                                SredniUbytek = Convert.ToDecimal(reader["SredniUbytek"]),
                                PadnieteRazem = Convert.ToInt32(reader["PadnieteRazem"]),
                                SredniCzasZaladunkuMin = reader["SredniCzasZaladunkuMin"] as int? ?? 0
                            };

                            // Oblicz średnią wagę na kurs
                            kierowca.SredniaWagaNaKurs = kierowca.LiczbaKursow > 0
                                ? kierowca.WagaRazem / kierowca.LiczbaKursow : 0;

                            // Oblicz % padnięć
                            kierowca.ProcentPadniec = kierowca.SztukiRazem > 0
                                ? (decimal)kierowca.PadnieteRazem / kierowca.SztukiRazem * 100 : 0;

                            wyniki.Add(kierowca);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd efektywności kierowców: {ex.Message}");
            }

            return wyniki;
        }

        #endregion
    }

    #region Data Models

    public class RaportRentownosci
    {
        public int HodowcaId { get; set; }
        public string HodowcaNazwa { get; set; }
        public string HodowcaMiasto { get; set; }
        public DateTime OkresOd { get; set; }
        public DateTime OkresDo { get; set; }

        public List<DostawaRentownosc> Dostawy { get; set; }

        // Statystyki
        public int LiczbaDostawSuma { get; set; }
        public int SztukiSuma { get; set; }
        public decimal WagaNettoSuma { get; set; }
        public decimal WartoscSuma { get; set; }
        public decimal SredniaWagaSztuki { get; set; }
        public decimal SredniaCenaZaKg { get; set; }
        public decimal SredniUbytek { get; set; }
        public decimal SredniaWartoscDostawy { get; set; }
        public decimal TrendWagi { get; set; } // % zmiany w ostatnich 3 miesiącach

        public string Blad { get; set; }
    }

    public class DostawaRentownosc
    {
        public int Id { get; set; }
        public DateTime Data { get; set; }
        public int Sztuki { get; set; }
        public decimal WagaNetto { get; set; }
        public decimal CenaZaKg { get; set; }
        public decimal Dodatek { get; set; }
        public decimal Ubytek { get; set; }
        public decimal Wartosc { get; set; }
    }

    public class RankingHodowcy
    {
        public int Pozycja { get; set; }
        public int HodowcaId { get; set; }
        public string Nazwa { get; set; }
        public string Miasto { get; set; }
        public int LiczbaDostaw { get; set; }
        public int SztukiSuma { get; set; }
        public decimal WagaSuma { get; set; }
        public decimal WartoscSuma { get; set; }
        public decimal SredniaWagaSztuki { get; set; }
        public decimal SredniaCena { get; set; }
        public decimal SredniUbytek { get; set; }
    }

    public class RaportStrat
    {
        public DateTime OkresOd { get; set; }
        public DateTime OkresDo { get; set; }
        public List<StrataHodowca> StratyPoHodowcach { get; set; }
        public List<StrataDzien> StratyPoDniach { get; set; }

        // Podsumowanie
        public decimal SumaStrataKg { get; set; }
        public decimal SumaStrataZl { get; set; }
        public int SumaPadniete { get; set; }
        public decimal SredniUbytekOgolem { get; set; }

        public string Blad { get; set; }
    }

    public class StrataHodowca
    {
        public string Hodowca { get; set; }
        public string Miasto { get; set; }
        public int LiczbaDostaw { get; set; }
        public int SztukiRazem { get; set; }
        public decimal WagaRazem { get; set; }
        public decimal SredniUbytekProcent { get; set; }
        public decimal StrataWagowaKg { get; set; }
        public decimal StrataKwotowa { get; set; }
        public int Padniete { get; set; }
    }

    public class StrataDzien
    {
        public DateTime Dzien { get; set; }
        public int LiczbaDostaw { get; set; }
        public int Sztuki { get; set; }
        public decimal SredniUbytek { get; set; }
        public decimal StrataKg { get; set; }
        public int Padniete { get; set; }
    }

    public class EfektywnoscKierowcy
    {
        public int Pozycja { get; set; }
        public string Kierowca { get; set; }
        public int LiczbaKursow { get; set; }
        public int LiczbaHodowcow { get; set; }
        public int SztukiRazem { get; set; }
        public decimal WagaRazem { get; set; }
        public decimal WartoscRazem { get; set; }
        public decimal SredniaWagaNaKurs { get; set; }
        public decimal SredniUbytek { get; set; }
        public int PadnieteRazem { get; set; }
        public decimal ProcentPadniec { get; set; }
        public int SredniCzasZaladunkuMin { get; set; }
    }

    #endregion
}
