namespace Kalendarz1.Shared.Geo
{
    /// <summary>
    /// Centralna prawda o lokalizacjach geograficznych firmy.
    /// Przed użyciem nowych konsumentów: 3 historyczne stałe w MapaFloty/Transport/EtaService
    /// rozjeżdżały się do ~6 km. Powód rozjazdu nie został potwierdzony.
    /// TODO: zweryfikować z danymi GPS Webfleet (gdzie pojazdy faktycznie startują/kończą)
    /// i zredukować do jednej kanonicznej lokalizacji, jeśli rozjazd jest błędem.
    /// </summary>
    public static class FirmaLokalizacje
    {
        /// <summary>Ubojnia, Koziołki 40. Używana w MapaFloty (wszystkie 8 plików).</summary>
        public static readonly GeoPoint UbojniaKoziolki40 = new(51.86857, 19.79476);

        /// <summary>Historyczna stała z Transport/TransportMapaWindow.cs (źródło nieznane, ~5 km od ubojni).</summary>
        public static readonly GeoPoint BazaTransportuLegacy = new(51.907335, 19.678605);

        /// <summary>Historyczna stała z Transport/Services/EtaService.cs (etykieta "Koziołki 40" w kodzie).</summary>
        public static readonly GeoPoint EtaBazaLegacy = new(51.9148, 19.8089);

        /// <summary>Promień geofence bazy (m). Pojazd wewnątrz = "na bazie".</summary>
        public const double GeofencePromienM = 1500;
    }

    public readonly struct GeoPoint
    {
        public double Lat { get; }
        public double Lon { get; }

        public GeoPoint(double lat, double lon)
        {
            Lat = lat;
            Lon = lon;
        }
    }
}
