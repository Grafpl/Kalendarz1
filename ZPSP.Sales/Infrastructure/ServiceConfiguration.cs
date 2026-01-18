using System;
using Microsoft.Extensions.DependencyInjection;
using ZPSP.Sales.Repositories;
using ZPSP.Sales.Repositories.Interfaces;
using ZPSP.Sales.Services;
using ZPSP.Sales.Services.Interfaces;

namespace ZPSP.Sales.Infrastructure
{
    /// <summary>
    /// Konfiguracja Dependency Injection dla modułu sprzedaży.
    /// Używa Microsoft.Extensions.DependencyInjection.
    /// </summary>
    public static class ServiceConfiguration
    {
        /// <summary>
        /// Rejestruje wszystkie serwisy modułu sprzedaży w kontenerze DI
        /// </summary>
        /// <param name="services">Kolekcja serwisów</param>
        /// <returns>Kolekcja serwisów z zarejestrowanymi zależnościami</returns>
        public static IServiceCollection AddSalesModule(this IServiceCollection services)
        {
            // Repozytoria - Scoped (per request/okno)
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<ICustomerRepository, CustomerRepository>();
            services.AddScoped<ITransportRepository, TransportRepository>();

            // Serwisy - Scoped
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<IProductService, ProductService>();

            // Cache - Singleton (jeden dla całej aplikacji)
            services.AddSingleton<ICacheService, CacheService>();

            return services;
        }

        /// <summary>
        /// Tworzy prosty kontener serwisów (dla aplikacji bez pełnego DI)
        /// </summary>
        public static IServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddSalesModule();
            return services.BuildServiceProvider();
        }
    }

    /// <summary>
    /// Prosty Service Locator dla scenariuszy gdzie pełne DI nie jest możliwe.
    /// UWAGA: Preferuj wstrzykiwanie przez konstruktor zamiast Service Locator.
    /// </summary>
    public static class ServiceLocator
    {
        private static IServiceProvider _serviceProvider;

        /// <summary>
        /// Inicjalizuje ServiceLocator z kontenerem serwisów
        /// </summary>
        public static void Initialize(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Inicjalizuje ServiceLocator z domyślną konfiguracją
        /// </summary>
        public static void Initialize()
        {
            _serviceProvider = ServiceConfiguration.CreateServiceProvider();
        }

        /// <summary>
        /// Pobiera serwis z kontenera
        /// </summary>
        /// <typeparam name="T">Typ serwisu</typeparam>
        public static T GetService<T>() where T : class
        {
            if (_serviceProvider == null)
                throw new InvalidOperationException("ServiceLocator nie został zainicjalizowany. Wywołaj Initialize() przed użyciem.");

            return _serviceProvider.GetService<T>();
        }

        /// <summary>
        /// Pobiera wymagany serwis z kontenera (rzuca wyjątek jeśli nie znaleziony)
        /// </summary>
        /// <typeparam name="T">Typ serwisu</typeparam>
        public static T GetRequiredService<T>() where T : class
        {
            if (_serviceProvider == null)
                throw new InvalidOperationException("ServiceLocator nie został zainicjalizowany. Wywołaj Initialize() przed użyciem.");

            return _serviceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// Tworzy nowy scope dla serwisów Scoped
        /// </summary>
        public static IServiceScope CreateScope()
        {
            if (_serviceProvider == null)
                throw new InvalidOperationException("ServiceLocator nie został zainicjalizowany.");

            return _serviceProvider.CreateScope();
        }
    }
}
