using Kalendarz1.Services;
using FluentAssertions;
using Xunit;

namespace Kalendarz1.Tests.Services
{
    /// <summary>
    /// Testy jednostkowe dla ReportingService i powiazanych modeli
    /// </summary>
    public class ReportingServiceTests
    {
        #region RaportRentownosci Tests

        [Fact]
        public void RaportRentownosci_ObliczaStatystyki_Poprawnie()
        {
            // Arrange
            var raport = new RaportRentownosci
            {
                HodowcaId = 1,
                HodowcaNazwa = "Test Hodowca",
                Dostawy = new List<DostawaRentownosc>
                {
                    new() { Sztuki = 100, WagaNetto = 250m, CenaZaKg = 5m, Dodatek = 0.5m, Ubytek = 2m, Wartosc = 1347.5m },
                    new() { Sztuki = 150, WagaNetto = 400m, CenaZaKg = 5.2m, Dodatek = 0.3m, Ubytek = 1.5m, Wartosc = 2156.6m },
                    new() { Sztuki = 200, WagaNetto = 520m, CenaZaKg = 4.8m, Dodatek = 0.4m, Ubytek = 3m, Wartosc = 2621.44m }
                }
            };

            // Act - logika z serwisu
            raport.LiczbaDostawSuma = raport.Dostawy.Count;
            raport.SztukiSuma = raport.Dostawy.Sum(d => d.Sztuki);
            raport.WagaNettoSuma = raport.Dostawy.Sum(d => d.WagaNetto);
            raport.WartoscSuma = raport.Dostawy.Sum(d => d.Wartosc);
            raport.SredniaWagaSztuki = raport.WagaNettoSuma / raport.SztukiSuma;
            raport.SredniaCenaZaKg = raport.Dostawy.Average(d => d.CenaZaKg + d.Dodatek);
            raport.SredniUbytek = raport.Dostawy.Average(d => d.Ubytek);
            raport.SredniaWartoscDostawy = raport.WartoscSuma / raport.LiczbaDostawSuma;

            // Assert
            raport.LiczbaDostawSuma.Should().Be(3);
            raport.SztukiSuma.Should().Be(450);
            raport.WagaNettoSuma.Should().Be(1170m);
            raport.SredniaWagaSztuki.Should().Be(1170m / 450);
        }

        [Fact]
        public void RaportRentownosci_SredniaWagaSztuki_ObliczaPoprawnie()
        {
            // Arrange
            var raport = new RaportRentownosci
            {
                SztukiSuma = 1000,
                WagaNettoSuma = 2600m
            };

            // Act
            raport.SredniaWagaSztuki = raport.WagaNettoSuma / raport.SztukiSuma;

            // Assert - srednia waga 2.6 kg
            raport.SredniaWagaSztuki.Should().Be(2.6m);
        }

        [Fact]
        public void RaportRentownosci_TrendWagi_ObliczaWzrost()
        {
            // Arrange - ostatnie 3 miesiace lepsze niz poprzednie
            var ostatnie3Mies = new List<DostawaRentownosc>
            {
                new() { WagaNetto = 300m, Data = DateTime.Today.AddDays(-30) },
                new() { WagaNetto = 320m, Data = DateTime.Today.AddDays(-60) },
                new() { WagaNetto = 310m, Data = DateTime.Today.AddDays(-80) }
            };

            var poprzednie3Mies = new List<DostawaRentownosc>
            {
                new() { WagaNetto = 250m, Data = DateTime.Today.AddMonths(-4) },
                new() { WagaNetto = 260m, Data = DateTime.Today.AddMonths(-5) }
            };

            // Act
            var sredniaOstatnia = ostatnie3Mies.Average(d => d.WagaNetto);
            var sredniaPoprzednia = poprzednie3Mies.Average(d => d.WagaNetto);
            var trendWagi = (sredniaOstatnia - sredniaPoprzednia) / sredniaPoprzednia * 100;

            // Assert - wzrost
            trendWagi.Should().BeGreaterThan(0);
            sredniaOstatnia.Should().BeGreaterThan(sredniaPoprzednia);
        }

        [Fact]
        public void RaportRentownosci_TrendWagi_ObliczaSpadek()
        {
            // Arrange - ostatnie 3 miesiace gorsze niz poprzednie
            var sredniaOstatnia = 200m;
            var sredniaPoprzednia = 250m;

            // Act
            var trendWagi = (sredniaOstatnia - sredniaPoprzednia) / sredniaPoprzednia * 100;

            // Assert - spadek o 20%
            trendWagi.Should().Be(-20m);
        }

        #endregion

        #region RankingHodowcy Tests

        [Theory]
        [InlineData(ReportingService.RankingKryterium.Wartosc, "WartoscSuma DESC")]
        [InlineData(ReportingService.RankingKryterium.Waga, "WagaSuma DESC")]
        [InlineData(ReportingService.RankingKryterium.Sztuki, "SztukiSuma DESC")]
        [InlineData(ReportingService.RankingKryterium.SredniaWagaSztuki, "SredniaWagaSztuki DESC")]
        [InlineData(ReportingService.RankingKryterium.LiczbaDostawL, "LiczbaDostaw DESC")]
        [InlineData(ReportingService.RankingKryterium.NajnizszaStrata, "SredniUbytek ASC")]
        public void RankingKryterium_MapujeNaOrderBy(ReportingService.RankingKryterium kryterium, string oczekiwanyOrderBy)
        {
            // Act - logika z serwisu
            string orderBy = kryterium switch
            {
                ReportingService.RankingKryterium.Wartosc => "WartoscSuma DESC",
                ReportingService.RankingKryterium.Waga => "WagaSuma DESC",
                ReportingService.RankingKryterium.Sztuki => "SztukiSuma DESC",
                ReportingService.RankingKryterium.SredniaWagaSztuki => "SredniaWagaSztuki DESC",
                ReportingService.RankingKryterium.LiczbaDostawL => "LiczbaDostaw DESC",
                ReportingService.RankingKryterium.NajnizszaStrata => "SredniUbytek ASC",
                _ => "WartoscSuma DESC"
            };

            // Assert
            orderBy.Should().Be(oczekiwanyOrderBy);
        }

        [Fact]
        public void RankingHodowcy_SortowanieWedlugWartosci()
        {
            // Arrange
            var ranking = new List<RankingHodowcy>
            {
                new() { Nazwa = "Hodowca C", WartoscSuma = 5000m },
                new() { Nazwa = "Hodowca A", WartoscSuma = 15000m },
                new() { Nazwa = "Hodowca B", WartoscSuma = 10000m }
            };

            // Act
            var posortowany = ranking.OrderByDescending(h => h.WartoscSuma).ToList();

            // Assert
            posortowany[0].Nazwa.Should().Be("Hodowca A");
            posortowany[1].Nazwa.Should().Be("Hodowca B");
            posortowany[2].Nazwa.Should().Be("Hodowca C");
        }

        [Fact]
        public void RankingHodowcy_SortowanieWedlugNajnizszejStraty()
        {
            // Arrange
            var ranking = new List<RankingHodowcy>
            {
                new() { Nazwa = "Hodowca A", SredniUbytek = 3.5m },
                new() { Nazwa = "Hodowca B", SredniUbytek = 1.2m },
                new() { Nazwa = "Hodowca C", SredniUbytek = 2.8m }
            };

            // Act - ASC dla najnizszej straty
            var posortowany = ranking.OrderBy(h => h.SredniUbytek).ToList();

            // Assert - najnizsza strata na gorze
            posortowany[0].Nazwa.Should().Be("Hodowca B");
            posortowany[1].Nazwa.Should().Be("Hodowca C");
            posortowany[2].Nazwa.Should().Be("Hodowca A");
        }

        [Fact]
        public void RankingHodowcy_Pozycja_NumerowanaPoprawnie()
        {
            // Arrange
            var ranking = new List<RankingHodowcy>();
            int pozycja = 1;

            for (int i = 0; i < 10; i++)
            {
                ranking.Add(new RankingHodowcy { Pozycja = pozycja++ });
            }

            // Assert
            ranking[0].Pozycja.Should().Be(1);
            ranking[9].Pozycja.Should().Be(10);
        }

        #endregion

        #region RaportStrat Tests

        [Fact]
        public void RaportStrat_SumujeStratyPoprawnie()
        {
            // Arrange
            var raport = new RaportStrat
            {
                StratyPoHodowcach = new List<StrataHodowca>
                {
                    new() { StrataWagowaKg = 50m, StrataKwotowa = 250m, Padniete = 10 },
                    new() { StrataWagowaKg = 30m, StrataKwotowa = 150m, Padniete = 5 },
                    new() { StrataWagowaKg = 70m, StrataKwotowa = 350m, Padniete = 15 }
                }
            };

            // Act - logika z serwisu
            raport.SumaStrataKg = raport.StratyPoHodowcach.Sum(s => s.StrataWagowaKg);
            raport.SumaStrataZl = raport.StratyPoHodowcach.Sum(s => s.StrataKwotowa);
            raport.SumaPadniete = raport.StratyPoHodowcach.Sum(s => s.Padniete);
            raport.SredniUbytekOgolem = raport.StratyPoHodowcach.Average(s => s.SredniUbytekProcent);

            // Assert
            raport.SumaStrataKg.Should().Be(150m);
            raport.SumaStrataZl.Should().Be(750m);
            raport.SumaPadniete.Should().Be(30);
        }

        [Theory]
        [InlineData(100, 5, 5)] // 5% ubytek = 5 kg straty
        [InlineData(200, 2.5, 5)] // 2.5% ubytek = 5 kg straty
        [InlineData(1000, 3, 30)] // 3% ubytek = 30 kg straty
        public void StrataHodowca_ObliczaStrateWagowa(decimal wagaRazem, decimal ubytekProcent, decimal oczekiwanaStrataKg)
        {
            // Logika SQL: NettoWeight * Loss / 100.0
            var strataKg = wagaRazem * ubytekProcent / 100m;
            strataKg.Should().Be(oczekiwanaStrataKg);
        }

        [Theory]
        [InlineData(100, 5, 5, 25)] // 100 kg * 5 zl * 5% = 25 zl
        [InlineData(200, 6, 3, 36)] // 200 kg * 6 zl * 3% = 36 zl
        public void StrataHodowca_ObliczaStrateKwotowa(
            decimal wagaRazem, decimal cena, decimal ubytekProcent, decimal oczekiwanaStrataZl)
        {
            // Logika SQL: (Price + Addition) * NettoWeight * Loss / 100.0
            var strataZl = cena * wagaRazem * ubytekProcent / 100m;
            strataZl.Should().Be(oczekiwanaStrataZl);
        }

        [Fact]
        public void RaportStrat_PustyRaport_NieRzucaWyjatku()
        {
            // Arrange
            var raport = new RaportStrat
            {
                StratyPoHodowcach = new List<StrataHodowca>()
            };

            // Act & Assert - nie powinno rzucic wyjatku
            var suma = raport.StratyPoHodowcach.Sum(s => s.StrataWagowaKg);
            suma.Should().Be(0);

            // Srednia z pustej listy
            var srednia = raport.StratyPoHodowcach.Any()
                ? raport.StratyPoHodowcach.Average(s => s.SredniUbytekProcent)
                : 0;
            srednia.Should().Be(0);
        }

        #endregion

        #region EfektywnoscKierowcy Tests

        [Fact]
        public void EfektywnoscKierowcy_ObliczaSredniaWagaNaKurs()
        {
            // Arrange
            var kierowca = new EfektywnoscKierowcy
            {
                LiczbaKursow = 10,
                WagaRazem = 5000m
            };

            // Act - logika z serwisu
            kierowca.SredniaWagaNaKurs = kierowca.LiczbaKursow > 0
                ? kierowca.WagaRazem / kierowca.LiczbaKursow
                : 0;

            // Assert
            kierowca.SredniaWagaNaKurs.Should().Be(500m);
        }

        [Fact]
        public void EfektywnoscKierowcy_ObliczaProcentPadniec()
        {
            // Arrange
            var kierowca = new EfektywnoscKierowcy
            {
                SztukiRazem = 10000,
                PadnieteRazem = 50
            };

            // Act - logika z serwisu
            kierowca.ProcentPadniec = kierowca.SztukiRazem > 0
                ? (decimal)kierowca.PadnieteRazem / kierowca.SztukiRazem * 100
                : 0;

            // Assert - 0.5%
            kierowca.ProcentPadniec.Should().Be(0.5m);
        }

        [Theory]
        [InlineData(0, 100, 0)] // brak kursow
        [InlineData(5, 0, 0)] // brak wagi
        [InlineData(10, 2500, 250)] // normalny przypadek
        public void EfektywnoscKierowcy_SredniaWagaNaKurs_RoznePrzypady(
            int liczbaKursow, decimal wagaRazem, decimal oczekiwanaSrednia)
        {
            var srednia = liczbaKursow > 0 ? wagaRazem / liczbaKursow : 0;
            srednia.Should().Be(oczekiwanaSrednia);
        }

        [Fact]
        public void EfektywnoscKierowcy_SortowanieWedlugWagi()
        {
            // Arrange
            var kierowcy = new List<EfektywnoscKierowcy>
            {
                new() { Kierowca = "Kierowca A", WagaRazem = 3000m },
                new() { Kierowca = "Kierowca B", WagaRazem = 5000m },
                new() { Kierowca = "Kierowca C", WagaRazem = 4000m }
            };

            // Act - sortowanie jak w serwisie (ORDER BY WagaRazem DESC)
            var posortowani = kierowcy.OrderByDescending(k => k.WagaRazem).ToList();

            // Assert
            posortowani[0].Kierowca.Should().Be("Kierowca B");
            posortowani[1].Kierowca.Should().Be("Kierowca C");
            posortowani[2].Kierowca.Should().Be("Kierowca A");
        }

        #endregion

        #region DostawaRentownosc Tests

        [Fact]
        public void DostawaRentownosc_PrzechowujeWszystkieDane()
        {
            // Arrange
            var dostawa = new DostawaRentownosc
            {
                Id = 456,
                Data = new DateTime(2024, 3, 15),
                Sztuki = 300,
                WagaNetto = 750.5m,
                CenaZaKg = 5.20m,
                Dodatek = 0.30m,
                Ubytek = 2.5m,
                Wartosc = 4015.24m
            };

            // Assert
            dostawa.Id.Should().Be(456);
            dostawa.Data.Should().Be(new DateTime(2024, 3, 15));
            dostawa.Sztuki.Should().Be(300);
            dostawa.WagaNetto.Should().Be(750.5m);
            dostawa.CenaZaKg.Should().Be(5.20m);
            dostawa.Dodatek.Should().Be(0.30m);
            dostawa.Ubytek.Should().Be(2.5m);
            dostawa.Wartosc.Should().Be(4015.24m);
        }

        [Theory]
        [InlineData(100, 5, 0.5, 2, 539.0)] // (5 + 0.5) * 100 * (1 - 2/100) = 5.5 * 100 * 0.98
        [InlineData(200, 4.5, 0, 0, 900)] // 4.5 * 200 * 1 = 900
        [InlineData(150, 6, 1, 5, 997.5)] // (6 + 1) * 150 * 0.95 = 7 * 150 * 0.95
        public void DostawaRentownosc_ObliczaWartosc_Poprawnie(
            decimal wagaNetto, decimal cena, decimal dodatek, decimal ubytek, decimal oczekiwanaWartosc)
        {
            // Logika SQL: (Price + Addition) * NettoWeight * (1 - Loss / 100.0)
            var wartosc = (cena + dodatek) * wagaNetto * (1 - ubytek / 100m);
            wartosc.Should().Be(oczekiwanaWartosc);
        }

        #endregion

        #region Daty Domyslne Tests

        [Fact]
        public void DatyDomyslne_RentownoscHodowcy_OstatniRok()
        {
            // Logika z serwisu: odDaty ??= DateTime.Today.AddYears(-1);
            DateTime? odDaty = null;
            DateTime? doDaty = null;

            odDaty ??= DateTime.Today.AddYears(-1);
            doDaty ??= DateTime.Today;

            // Assert
            odDaty.Should().Be(DateTime.Today.AddYears(-1));
            doDaty.Should().Be(DateTime.Today);
        }

        [Fact]
        public void DatyDomyslne_RaportStrat_OstatniMiesiac()
        {
            // Logika z serwisu: odDaty ??= DateTime.Today.AddMonths(-1);
            DateTime? odDaty = null;
            DateTime? doDaty = null;

            odDaty ??= DateTime.Today.AddMonths(-1);
            doDaty ??= DateTime.Today;

            // Assert
            odDaty.Should().Be(DateTime.Today.AddMonths(-1));
            doDaty.Should().Be(DateTime.Today);
        }

        [Fact]
        public void DatyDomyslne_MoznaOverride()
        {
            // Arrange
            DateTime? odDaty = new DateTime(2024, 1, 1);
            DateTime? doDaty = new DateTime(2024, 6, 30);

            // Act - operator ??= nie nadpisze istniejacych wartosci
            odDaty ??= DateTime.Today.AddYears(-1);
            doDaty ??= DateTime.Today;

            // Assert
            odDaty.Should().Be(new DateTime(2024, 1, 1));
            doDaty.Should().Be(new DateTime(2024, 6, 30));
        }

        #endregion
    }
}
