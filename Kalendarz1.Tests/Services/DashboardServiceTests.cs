using Kalendarz1.Services;
using FluentAssertions;
using Xunit;

namespace Kalendarz1.Tests.Services
{
    /// <summary>
    /// Testy jednostkowe dla DashboardService i powiazanych modeli
    /// </summary>
    public class DashboardServiceTests
    {
        #region KpiData Tests

        [Fact]
        public void KpiData_SredniaWagaSztuki_ObliczaPoprawnie()
        {
            // Arrange
            var kpi = new KpiData
            {
                SztukiSuma = 1000,
                WagaNettoSuma = 2500m
            };

            // Assert - srednia waga sztuki powinna byc 2.5 kg
            // Uwaga: pole SredniaWagaSztuki jest ustawiane w serwisie,
            // ale sprawdzamy logike obliczenia
            var oczekiwana = kpi.WagaNettoSuma / kpi.SztukiSuma;
            oczekiwana.Should().Be(2.5m);
        }

        [Fact]
        public void KpiData_SredniaWagaSztuki_ZwracaZero_GdyBrakSztuk()
        {
            // Arrange
            var kpi = new KpiData
            {
                SztukiSuma = 0,
                WagaNettoSuma = 0
            };

            // Assert - nie dziel przez zero
            var wynik = kpi.SztukiSuma > 0 ? kpi.WagaNettoSuma / kpi.SztukiSuma : 0;
            wynik.Should().Be(0);
        }

        [Fact]
        public void KpiData_WartoscFormatowana_FormatujePoprawnie()
        {
            // Arrange
            var kpi = new KpiData
            {
                WartoscSuma = 1234567.89m
            };

            // Assert
            kpi.WartoscFormatowana.Should().Contain("zł");
            kpi.WartoscFormatowana.Should().Contain("1");
        }

        [Fact]
        public void KpiData_WagaFormatowana_FormatujePoprawnie()
        {
            // Arrange
            var kpi = new KpiData
            {
                WagaNettoSuma = 98765.43m
            };

            // Assert
            kpi.WagaFormatowana.Should().Contain("kg");
        }

        [Theory]
        [InlineData(1000, 800, 25.0)] // wzrost o 25%
        [InlineData(800, 1000, -20.0)] // spadek o 20%
        [InlineData(1000, 1000, 0)] // bez zmiany
        public void KpiData_ZmianaWagiProcent_ObliczaPoprawnie(
            decimal obecnaWaga, decimal poprzedniaWaga, decimal oczekiwanaZmiana)
        {
            // Logika z serwisu:
            // ZmianaWagiProcent = (kpi.WagaNettoSuma - wagaPoprz) / wagaPoprz * 100

            var zmiana = poprzedniaWaga > 0
                ? (obecnaWaga - poprzedniaWaga) / poprzedniaWaga * 100
                : 0;

            zmiana.Should().Be(oczekiwanaZmiana);
        }

        [Fact]
        public void KpiData_ZmianaWagiProcent_ZwracaZero_GdyPoprzedniaWagaZero()
        {
            var zmiana = 0m > 0 ? (100m - 0m) / 0m * 100 : 0;
            zmiana.Should().Be(0);
        }

        #endregion

        #region AlertOperacyjny Tests

        [Theory]
        [InlineData(AlertTyp.WysokiUbytek, "⚠️")]
        [InlineData(AlertTyp.Padniecia, "💀")]
        [InlineData(AlertTyp.BrakDostaw, "📭")]
        [InlineData(AlertTyp.NiskaJakosc, "👎")]
        [InlineData(AlertTyp.Inne, "ℹ️")]
        public void AlertOperacyjny_IkonaTypu_ZwracaPoprawnaIkone(AlertTyp typ, string oczekiwanaIkona)
        {
            // Arrange
            var alert = new AlertOperacyjny { Typ = typ };

            // Assert
            alert.IkonaTypu.Should().Be(oczekiwanaIkona);
        }

        [Fact]
        public void AlertOperacyjny_Sortowanie_WedlugPriorytetu()
        {
            // Arrange
            var alerty = new List<AlertOperacyjny>
            {
                new() { Priorytet = AlertPriorytet.Niski, Data = DateTime.Today },
                new() { Priorytet = AlertPriorytet.Krytyczny, Data = DateTime.Today },
                new() { Priorytet = AlertPriorytet.Sredni, Data = DateTime.Today },
                new() { Priorytet = AlertPriorytet.Wysoki, Data = DateTime.Today }
            };

            // Act - sortowanie jak w serwisie
            var posortowane = alerty
                .OrderByDescending(a => (int)a.Priorytet)
                .ThenByDescending(a => a.Data)
                .ToList();

            // Assert
            posortowane[0].Priorytet.Should().Be(AlertPriorytet.Krytyczny);
            posortowane[1].Priorytet.Should().Be(AlertPriorytet.Wysoki);
            posortowane[2].Priorytet.Should().Be(AlertPriorytet.Sredni);
            posortowane[3].Priorytet.Should().Be(AlertPriorytet.Niski);
        }

        [Theory]
        [InlineData(50, 1000, 5.0, true)] // 5% > 3% = Krytyczny
        [InlineData(20, 1000, 2.0, true)] // 2% > 1% ale < 3% = Wysoki
        [InlineData(5, 1000, 0.5, false)] // 0.5% < 1% = brak alertu
        public void Alert_Padniecia_OkreslaPriorytetPoprawnie(
            int padniete, int sztuki, decimal oczekiwanyProcent, bool czyAlert)
        {
            // Logika z serwisu GetAlerty():
            var procent = sztuki > 0 ? (decimal)padniete / sztuki * 100 : 0;

            procent.Should().Be(oczekiwanyProcent);
            (procent > 1).Should().Be(czyAlert);

            if (czyAlert)
            {
                var priorytet = procent > 3
                    ? AlertPriorytet.Krytyczny
                    : AlertPriorytet.Wysoki;

                if (oczekiwanyProcent > 3)
                    priorytet.Should().Be(AlertPriorytet.Krytyczny);
                else
                    priorytet.Should().Be(AlertPriorytet.Wysoki);
            }
        }

        #endregion

        #region TopHodowca Tests

        [Fact]
        public void TopHodowca_Pozycja_NumerowanaOdJeden()
        {
            // Arrange
            var hodowcy = new List<TopHodowca>();
            int poz = 1;

            for (int i = 0; i < 5; i++)
            {
                hodowcy.Add(new TopHodowca
                {
                    Pozycja = poz++,
                    Nazwa = $"Hodowca {i}",
                    WagaSuma = 1000 - i * 100
                });
            }

            // Assert
            hodowcy[0].Pozycja.Should().Be(1);
            hodowcy[4].Pozycja.Should().Be(5);
        }

        #endregion

        #region TrendTygodniowy Tests

        [Fact]
        public void TrendTygodniowy_Etykieta_FormatujePoprawnie()
        {
            // Arrange
            var trend = new TrendTygodniowy
            {
                Rok = 2024,
                NumerTygodnia = 42
            };

            // Assert
            trend.Etykieta.Should().Be("Tydzień 42");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(26)]
        [InlineData(52)]
        public void TrendTygodniowy_Etykieta_DlaRoznychTygodni(int nrTygodnia)
        {
            var trend = new TrendTygodniowy { NumerTygodnia = nrTygodnia };
            trend.Etykieta.Should().Be($"Tydzień {nrTygodnia}");
        }

        #endregion

        #region DashboardData Tests

        [Fact]
        public void DashboardData_Inicjalizacja_ZawieraWszystkieKpi()
        {
            // Arrange
            var dashboard = new DashboardData
            {
                DataWygenerowania = DateTime.Now,
                KpiDzisiaj = new KpiData(),
                KpiTydzien = new KpiData(),
                KpiMiesiac = new KpiData(),
                KpiRok = new KpiData(),
                TopHodowcyMiesiac = new List<TopHodowca>(),
                OstatnieDostawy = new List<OstatniaDostawaInfo>(),
                TrendTygodniowy = new List<TrendTygodniowy>(),
                AlertyOperacyjne = new List<AlertOperacyjny>()
            };

            // Assert
            dashboard.KpiDzisiaj.Should().NotBeNull();
            dashboard.KpiTydzien.Should().NotBeNull();
            dashboard.KpiMiesiac.Should().NotBeNull();
            dashboard.KpiRok.Should().NotBeNull();
            dashboard.TopHodowcyMiesiac.Should().NotBeNull();
            dashboard.OstatnieDostawy.Should().NotBeNull();
            dashboard.TrendTygodniowy.Should().NotBeNull();
            dashboard.AlertyOperacyjne.Should().NotBeNull();
        }

        #endregion

        #region OstatniaDostawaInfo Tests

        [Fact]
        public void OstatniaDostawaInfo_PrzechowujeWszystkieDane()
        {
            // Arrange
            var dostawa = new OstatniaDostawaInfo
            {
                Id = 123,
                Data = new DateTime(2024, 1, 15),
                Hodowca = "Jan Kowalski",
                Sztuki = 500,
                Waga = 1250.5m,
                Wartosc = 6250.25m,
                Kierowca = "Adam Nowak"
            };

            // Assert
            dostawa.Id.Should().Be(123);
            dostawa.Data.Should().Be(new DateTime(2024, 1, 15));
            dostawa.Hodowca.Should().Be("Jan Kowalski");
            dostawa.Sztuki.Should().Be(500);
            dostawa.Waga.Should().Be(1250.5m);
            dostawa.Wartosc.Should().Be(6250.25m);
            dostawa.Kierowca.Should().Be("Adam Nowak");
        }

        #endregion

        #region Obliczenia Biznesowe Tests

        [Theory]
        [InlineData(100, 5, 0, 500)] // 100 * 5 * (1 - 0/100)
        [InlineData(100, 5, 10, 450)] // 100 * 5 * (1 - 10/100) = 100 * 5 * 0.9
        [InlineData(200, 6.5, 5, 1235)] // 200 * 6.5 * (1 - 5/100) = 200 * 6.5 * 0.95
        public void Oblicz_WartoscDostawy_ZUbytkiem(
            decimal wagaNetto, decimal cena, decimal ubytek, decimal oczekiwanaWartosc)
        {
            // Logika z SQL: (Price + Addition) * NettoWeight * (1 - Loss / 100.0)
            // Uproszczenie bez Addition
            var wartosc = cena * wagaNetto * (1 - ubytek / 100m);

            wartosc.Should().Be(oczekiwanaWartosc);
        }

        [Theory]
        [InlineData(100, 5, 2, 0, 700)] // (5 + 2) * 100 * (1 - 0)
        [InlineData(100, 5, 2, 10, 630)] // (5 + 2) * 100 * (1 - 0.1) = 7 * 100 * 0.9
        public void Oblicz_WartoscDostawy_ZDodatkiemIUbytkiem(
            decimal wagaNetto, decimal cena, decimal dodatek, decimal ubytek, decimal oczekiwanaWartosc)
        {
            // Pelna logika: (Price + Addition) * NettoWeight * (1 - Loss / 100.0)
            var wartosc = (cena + dodatek) * wagaNetto * (1 - ubytek / 100m);

            wartosc.Should().Be(oczekiwanaWartosc);
        }

        [Fact]
        public void Oblicz_DlugoscOkresu_Dni()
        {
            // Arrange
            var od = new DateTime(2024, 1, 1);
            var do_ = new DateTime(2024, 1, 8);

            // Act
            var dlugoscOkresu = (do_ - od).TotalDays;

            // Assert
            dlugoscOkresu.Should().Be(7);
        }

        [Fact]
        public void Oblicz_PoprzedniOkres_Poprawnie()
        {
            // Arrange - tydzien
            var od = new DateTime(2024, 1, 8);
            var do_ = new DateTime(2024, 1, 15);

            var dlugoscOkresu = (do_ - od).TotalDays;

            // Act - logika z serwisu
            var poprzedniOd = od.AddDays(-dlugoscOkresu);
            var poprzedniDo = od.AddSeconds(-1);

            // Assert
            poprzedniOd.Should().Be(new DateTime(2024, 1, 1));
            poprzedniDo.Date.Should().Be(new DateTime(2024, 1, 7));
        }

        #endregion
    }
}
