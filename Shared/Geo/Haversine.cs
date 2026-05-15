using System;

namespace Kalendarz1.Shared.Geo
{
    /// <summary>
    /// Odległość po wielkim kole (Haversine). Zwraca metry.
    /// Używana m.in. w ConflictDetectionService (NEARBY_ORDER, FOREIGN_ADDRESS)
    /// i KursMonitorService (proximity 500m → "Dotarl", 1000m → "Obsluzony").
    /// </summary>
    public static class Haversine
    {
        private const double EarthRadiusM = 6371000.0;

        public static double DistanceM(double lat1, double lon1, double lat2, double lon2)
        {
            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                  + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return EarthRadiusM * c;
        }

        public static double DistanceM(GeoPoint a, GeoPoint b)
            => DistanceM(a.Lat, a.Lon, b.Lat, b.Lon);

        public static double DistanceKm(GeoPoint a, GeoPoint b)
            => DistanceM(a, b) / 1000.0;

        private static double ToRad(double deg) => deg * Math.PI / 180.0;
    }
}
