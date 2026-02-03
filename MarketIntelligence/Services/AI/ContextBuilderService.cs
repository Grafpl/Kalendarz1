using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.MarketIntelligence.Services.AI
{
    /// <summary>
    /// Serwis budujący kontekst biznesowy z baz danych
    /// Pobiera dane o klientach, dostawcach, cenach do analizy AI
    /// </summary>
    public class ContextBuilderService
    {
        private readonly string _libraNetConnectionString;
        private readonly string _sageConnectionString;

        // Cache for context (rebuild daily)
        private BusinessContext _cachedContext;
        private DateTime _lastBuildTime = DateTime.MinValue;
        private readonly TimeSpan _cacheLifetime = TimeSpan.FromHours(4);

        public ContextBuilderService(
            string libraNetConnectionString = null,
            string sageConnectionString = null)
        {
            _libraNetConnectionString = libraNetConnectionString ??
                "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

            _sageConnectionString = sageConnectionString ??
                "Server=192.168.0.112;Database=Handel;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        }

        /// <summary>
        /// Pobierz aktualny kontekst biznesowy (z cache lub świeży)
        /// </summary>
        public async Task<BusinessContext> GetContextAsync(bool forceRefresh = false)
        {
            if (!forceRefresh &&
                _cachedContext != null &&
                DateTime.Now - _lastBuildTime < _cacheLifetime)
            {
                return _cachedContext;
            }

            _cachedContext = await BuildContextAsync();
            _lastBuildTime = DateTime.Now;

            return _cachedContext;
        }

        /// <summary>
        /// Zbuduj pełny kontekst biznesowy z baz danych
        /// </summary>
        public async Task<BusinessContext> BuildContextAsync()
        {
            var context = new BusinessContext
            {
                Company = GetCompanyInfo(),
                Competitors = GetCompetitors()
            };

            // Run database queries in parallel
            var topCustomersTask = GetTopCustomersAsync();
            var currentPricesTask = GetCurrentPricesAsync();
            var topSuppliersTask = GetTopSuppliersAsync();

            try
            {
                await Task.WhenAll(topCustomersTask, currentPricesTask, topSuppliersTask);

                context.TopCustomers = topCustomersTask.Result;
                context.CurrentPrices = currentPricesTask.Result;
                context.TopSuppliers = topSuppliersTask.Result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ContextBuilder] Error building context: {ex.Message}");
            }

            return context;
        }

        #region Company Info (static)

        private CompanyInfo GetCompanyInfo()
        {
            return new CompanyInfo
            {
                Name = "Ubojnia Drobiu Piórkowscy",
                Location = "Brzeziny",
                Voivodeship = "łódzkie",
                DailyCapacity = 70000,
                Specialization = "kurczak brojler"
            };
        }

        private List<string> GetCompetitors()
        {
            return new List<string>
            {
                "Cedrob",
                "SuperDrob",
                "Drosed",
                "Animex",
                "Indykpol",
                "Drobimex",
                "Wipasz",
                "Konspol",
                "Farmio",
                "Sokołów",
                "Morliny"
            };
        }

        #endregion

        #region Top Customers (from Sage)

        private async Task<List<CustomerInfo>> GetTopCustomersAsync()
        {
            var customers = new List<CustomerInfo>();

            try
            {
                using var conn = new SqlConnection(_sageConnectionString);
                await conn.OpenAsync();

                // Query top customers by volume in last 30 days
                var sql = @"
                    SELECT TOP 10
                        c.Name1 AS Klient,
                        c.City AS Miasto,
                        ISNULL(SUM(ABS(CAST(mz.ilosc AS DECIMAL(18,2)))), 0) AS WolumenKg,
                        ISNULL(cc.CDim_Handlowiec_Val, '') AS Handlowiec
                    FROM [HM].[MG] mg WITH (NOLOCK)
                    JOIN [HM].[MZ] mz WITH (NOLOCK) ON mg.id = mz.super
                    JOIN [SSCommon].[STContractors] c WITH (NOLOCK) ON mg.khid = c.Id
                    LEFT JOIN [SSCommon].[ContractorClassification] cc WITH (NOLOCK) ON c.Id = cc.ElementId
                    WHERE mg.seria LIKE 'sWZ%'
                      AND mg.data >= DATEADD(day, -30, GETDATE())
                    GROUP BY c.Name1, c.City, cc.CDim_Handlowiec_Val
                    ORDER BY WolumenKg DESC";

                using var cmd = new SqlCommand(sql, conn);
                cmd.CommandTimeout = 30;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    customers.Add(new CustomerInfo
                    {
                        Name = reader.GetString(0),
                        VolumePallets = reader.GetDecimal(2) / 500, // Approx conversion to pallets
                        SalesRep = reader.IsDBNull(3) ? "" : reader.GetString(3)
                    });
                }

                Debug.WriteLine($"[ContextBuilder] Loaded {customers.Count} top customers");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ContextBuilder] Error loading customers: {ex.Message}");

                // Return default customers if DB fails
                customers = GetDefaultCustomers();
            }

            return customers;
        }

        private List<CustomerInfo> GetDefaultCustomers()
        {
            return new List<CustomerInfo>
            {
                new CustomerInfo { Name = "Biedronka", VolumePallets = 150, SalesRep = "Nowak" },
                new CustomerInfo { Name = "Lidl", VolumePallets = 120, SalesRep = "Kowalski" },
                new CustomerInfo { Name = "Kaufland", VolumePallets = 80, SalesRep = "Nowak" },
                new CustomerInfo { Name = "Auchan", VolumePallets = 60, SalesRep = "Wiśniewski" },
                new CustomerInfo { Name = "Carrefour", VolumePallets = 50, SalesRep = "Wiśniewski" }
            };
        }

        #endregion

        #region Top Suppliers (from LibraNet)

        private async Task<List<SupplierInfo>> GetTopSuppliersAsync()
        {
            var suppliers = new List<SupplierInfo>();

            try
            {
                using var conn = new SqlConnection(_libraNetConnectionString);
                await conn.OpenAsync();

                // Try to find suppliers/farmers table
                // This may need adjustment based on actual table structure
                var sql = @"
                    SELECT TOP 10
                        ISNULL(h.Nazwa, '') AS Nazwa,
                        ISNULL(h.Miejscowosc, '') AS Miejscowosc,
                        ISNULL(h.Kategoria, 'B') AS Kategoria,
                        ISNULL(h.OdlegloscKm, 50) AS OdlegloscKm
                    FROM Hodowcy h WITH (NOLOCK)
                    WHERE h.Aktywny = 1
                    ORDER BY h.Kategoria, h.OdlegloscKm";

                using var cmd = new SqlCommand(sql, conn);
                cmd.CommandTimeout = 30;

                try
                {
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        suppliers.Add(new SupplierInfo
                        {
                            Name = reader.GetString(0),
                            Location = reader.GetString(1),
                            Category = reader.GetString(2),
                            DistanceKm = (int)reader.GetDouble(3)
                        });
                    }
                }
                catch (SqlException)
                {
                    // Table doesn't exist - use defaults
                    Debug.WriteLine("[ContextBuilder] Hodowcy table not found, using defaults");
                    suppliers = GetDefaultSuppliers();
                }

                Debug.WriteLine($"[ContextBuilder] Loaded {suppliers.Count} suppliers");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ContextBuilder] Error loading suppliers: {ex.Message}");
                suppliers = GetDefaultSuppliers();
            }

            return suppliers;
        }

        private List<SupplierInfo> GetDefaultSuppliers()
        {
            return new List<SupplierInfo>
            {
                new SupplierInfo { Name = "Ferma Kowalski", Location = "Tomaszów", Category = "A", DistanceKm = 45 },
                new SupplierInfo { Name = "Hodowla Nowak", Location = "Rawa", Category = "A", DistanceKm = 30 },
                new SupplierInfo { Name = "Drobex", Location = "Skierniewice", Category = "B", DistanceKm = 35 },
                new SupplierInfo { Name = "Ferma Wiśniewski", Location = "Łódź", Category = "B", DistanceKm = 40 },
                new SupplierInfo { Name = "Brojler Plus", Location = "Koluszki", Category = "A", DistanceKm = 20 }
            };
        }

        #endregion

        #region Current Prices (from LibraNet)

        private async Task<List<PriceInfo>> GetCurrentPricesAsync()
        {
            var prices = new List<PriceInfo>();

            try
            {
                using var conn = new SqlConnection(_libraNetConnectionString);
                await conn.OpenAsync();

                // Get average selling prices from recent orders
                var sql = @"
                    SELECT
                        tw.nazwa AS Produkt,
                        AVG(CAST(REPLACE(REPLACE(zmt.Cena, ',', '.'), ' ', '') AS DECIMAL(10,2))) AS SredniaCena,
                        'kg' AS Jednostka,
                        MAX(zm.DataUboju) AS Data
                    FROM ZamowieniaMiesoTowar zmt WITH (NOLOCK)
                    JOIN ZamowieniaMieso zm WITH (NOLOCK) ON zmt.ZamowienieId = zm.Id
                    OUTER APPLY (
                        SELECT TOP 1 nazwa
                        FROM [192.168.0.112].[Handel].[HM].[TW] tw WITH (NOLOCK)
                        WHERE tw.kod = zmt.KodTowaru
                    ) tw
                    WHERE zm.DataUboju >= DATEADD(day, -7, GETDATE())
                      AND zmt.Cena IS NOT NULL
                      AND zmt.Cena != ''
                      AND ISNUMERIC(REPLACE(REPLACE(zmt.Cena, ',', '.'), ' ', '')) = 1
                    GROUP BY tw.nazwa
                    HAVING AVG(CAST(REPLACE(REPLACE(zmt.Cena, ',', '.'), ' ', '') AS DECIMAL(10,2))) > 0
                    ORDER BY SredniaCena DESC";

                using var cmd = new SqlCommand(sql, conn);
                cmd.CommandTimeout = 30;

                try
                {
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var productName = reader.IsDBNull(0) ? "Nieznany" : reader.GetString(0);
                        prices.Add(new PriceInfo
                        {
                            Product = productName,
                            Price = reader.GetDecimal(1),
                            Unit = reader.GetString(2),
                            Date = reader.GetDateTime(3)
                        });
                    }
                }
                catch (SqlException ex)
                {
                    Debug.WriteLine($"[ContextBuilder] Price query error: {ex.Message}");
                    prices = GetDefaultPrices();
                }

                Debug.WriteLine($"[ContextBuilder] Loaded {prices.Count} current prices");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ContextBuilder] Error loading prices: {ex.Message}");
                prices = GetDefaultPrices();
            }

            // Ensure we have at least default prices
            if (!prices.Any())
            {
                prices = GetDefaultPrices();
            }

            return prices;
        }

        private List<PriceInfo> GetDefaultPrices()
        {
            return new List<PriceInfo>
            {
                new PriceInfo { Product = "Żywiec kurczak", Price = 5.85m, Unit = "kg", Date = DateTime.Today },
                new PriceInfo { Product = "Tuszka kurczaka", Price = 12.50m, Unit = "kg", Date = DateTime.Today },
                new PriceInfo { Product = "Filet z piersi", Price = 18.90m, Unit = "kg", Date = DateTime.Today },
                new PriceInfo { Product = "Udko kurczaka", Price = 9.50m, Unit = "kg", Date = DateTime.Today },
                new PriceInfo { Product = "Skrzydło", Price = 8.20m, Unit = "kg", Date = DateTime.Today },
                new PriceInfo { Product = "Podudzie", Price = 7.80m, Unit = "kg", Date = DateTime.Today }
            };
        }

        #endregion

        #region Market Prices (API + Scraping)

        /// <summary>
        /// Pobierz aktualne kursy walut z NBP
        /// </summary>
        public async Task<Dictionary<string, decimal>> GetExchangeRatesAsync()
        {
            var rates = new Dictionary<string, decimal>();

            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                // EUR
                var eurResponse = await client.GetStringAsync("http://api.nbp.pl/api/exchangerates/rates/a/eur/?format=json");
                var eurRate = ExtractRateFromNbpResponse(eurResponse);
                if (eurRate > 0) rates["EUR"] = eurRate;

                // USD
                var usdResponse = await client.GetStringAsync("http://api.nbp.pl/api/exchangerates/rates/a/usd/?format=json");
                var usdRate = ExtractRateFromNbpResponse(usdResponse);
                if (usdRate > 0) rates["USD"] = usdRate;

                Debug.WriteLine($"[ContextBuilder] NBP rates: EUR={rates.GetValueOrDefault("EUR"):F4}, USD={rates.GetValueOrDefault("USD"):F4}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ContextBuilder] Error fetching NBP rates: {ex.Message}");

                // Default rates
                rates["EUR"] = 4.35m;
                rates["USD"] = 4.00m;
            }

            return rates;
        }

        private decimal ExtractRateFromNbpResponse(string json)
        {
            try
            {
                // Simple JSON parsing without full deserialization
                var midMatch = System.Text.RegularExpressions.Regex.Match(json, @"""mid""\s*:\s*(\d+\.?\d*)");
                if (midMatch.Success)
                {
                    return decimal.Parse(midMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            catch { }
            return 0;
        }

        #endregion

        #region Extended Context for Specific Analysis

        /// <summary>
        /// Pobierz rozszerzony kontekst dla analizy HPAI
        /// </summary>
        public async Task<HpaiContext> GetHpaiContextAsync()
        {
            var context = new HpaiContext
            {
                CompanyLocation = "Brzeziny, łódzkie",
                NearbyVoivodeships = new List<string> { "łódzkie", "mazowieckie", "świętokrzyskie", "wielkopolskie" },
                RiskFactors = new List<string>
                {
                    "Bliskość szlaków migracji ptaków",
                    "Wysoka gęstość hodowli w regionie",
                    "Sezon jesienno-zimowy (wyższe ryzyko)"
                },
                PreventiveMeasures = new List<string>
                {
                    "Bioasekuracja na fermach dostawców",
                    "Monitoring zdrowia stad",
                    "Izolacja od dzikiego ptactwa"
                }
            };

            // Load supplier locations for risk assessment
            var suppliers = await GetTopSuppliersAsync();
            context.SupplierLocations = suppliers
                .Select(s => $"{s.Name} ({s.Location})")
                .ToList();

            return context;
        }

        /// <summary>
        /// Pobierz kontekst cenowy dla analizy rynku
        /// </summary>
        public async Task<PricingContext> GetPricingContextAsync()
        {
            var context = new PricingContext
            {
                CurrentPrices = await GetCurrentPricesAsync(),
                ExchangeRates = await GetExchangeRatesAsync()
            };

            // Historical comparison (placeholder - would need historical data)
            context.PriceChanges = new Dictionary<string, decimal>
            {
                { "Żywiec kurczak", 0.05m }, // +5% vs last week
                { "Filet z piersi", 0.02m }, // +2%
                { "Kukurydza MATIF", -0.03m } // -3%
            };

            return context;
        }

        #endregion
    }

    #region Extended Context Models

    public class HpaiContext
    {
        public string CompanyLocation { get; set; }
        public List<string> NearbyVoivodeships { get; set; } = new();
        public List<string> SupplierLocations { get; set; } = new();
        public List<string> RiskFactors { get; set; } = new();
        public List<string> PreventiveMeasures { get; set; } = new();
    }

    public class PricingContext
    {
        public List<PriceInfo> CurrentPrices { get; set; } = new();
        public Dictionary<string, decimal> ExchangeRates { get; set; } = new();
        public Dictionary<string, decimal> PriceChanges { get; set; } = new();
    }

    #endregion
}
