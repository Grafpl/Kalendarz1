using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kalendarz1.Transport.Services
{
    /// <summary>
    /// Oblicza ETA (szacowany czas przyjazdu) dla kolejnych punktów trasy.
    /// Baza → Punkt1 → Punkt2 → ... → Powrót
    /// </summary>
    public class EtaService
    {
        // Ubojnia Koziołki 40, 95-061 Dmosin
        public const double BaseLat = 51.9148;
        public const double BaseLng = 19.8089;

        /// <summary>Średnia prędkość [km/h]</summary>
        public const int AvgSpeedKmh = 60;

        /// <summary>Czas załadunku w bazie [min]</summary>
        public const int LoadMinutes = 30;

        /// <summary>Czas rozładunku u klienta [min]</summary>
        public const int UnloadMinutes = 30;

        /// <summary>
        /// Mnożnik dystansu — Haversine daje linię prostą,
        /// drogi są dłuższe o ok. 30%.
        /// </summary>
        public const double RoadFactor = 1.3;

        /// <summary>Dane wejściowe jednego przystanku</summary>
        public class StopInput
        {
            public int Kolejnosc { get; set; }
            public string NazwaKlienta { get; set; } = "";
            public double Latitude { get; set; }
            public double Longitude { get; set; }
        }

        /// <summary>Wynik ETA dla jednego przystanku</summary>
        public class StopEta
        {
            public int Kolejnosc { get; set; }
            public string NazwaKlienta { get; set; } = "";

            /// <summary>Dystans od poprzedniego punktu [km] (Haversine × RoadFactor)</summary>
            public double DistanceFromPrevKm { get; set; }

            /// <summary>Czas jazdy od poprzedniego punktu</summary>
            public TimeSpan DriveTime { get; set; }

            /// <summary>Szacowany czas przyjazdu</summary>
            public TimeSpan Eta { get; set; }

            /// <summary>Szacowany czas odjazdu (po rozładunku)</summary>
            public TimeSpan DepartureAfterUnload { get; set; }

            /// <summary>Czy mamy współrzędne GPS dla tego punktu</summary>
            public bool HasCoordinates { get; set; }
        }

        /// <summary>Pełny wynik obliczeń trasy</summary>
        public class RouteEtaResult
        {
            public List<StopEta> Stops { get; set; } = new();

            /// <summary>Łączny dystans trasy (bez powrotu) [km]</summary>
            public double TotalDistanceKm { get; set; }

            /// <summary>Dystans powrotny do bazy [km]</summary>
            public double ReturnDistanceKm { get; set; }

            /// <summary>Szacowany czas powrotu do bazy</summary>
            public TimeSpan EstimatedReturnTime { get; set; }

            /// <summary>Łączny czas trasy</summary>
            public TimeSpan TotalDuration { get; set; }
        }

        /// <summary>
        /// Oblicza ETA dla każdego przystanku na trasie.
        /// </summary>
        /// <param name="departureTime">Godzina wyjazdu z bazy</param>
        /// <param name="stops">Przystanki w kolejności Kolejnosc</param>
        public RouteEtaResult Calculate(TimeSpan departureTime, IEnumerable<StopInput> stops)
        {
            var result = new RouteEtaResult();
            var sorted = stops.OrderBy(s => s.Kolejnosc).ToList();

            if (sorted.Count == 0)
                return result;

            // Punkt startowy — baza + czas załadunku
            double prevLat = BaseLat;
            double prevLng = BaseLng;
            var currentTime = departureTime.Add(TimeSpan.FromMinutes(LoadMinutes));

            double totalDistKm = 0;

            foreach (var stop in sorted)
            {
                bool hasCoords = stop.Latitude != 0 && stop.Longitude != 0;

                double distKm;
                TimeSpan driveTime;

                if (hasCoords)
                {
                    distKm = HaversineKm(prevLat, prevLng, stop.Latitude, stop.Longitude) * RoadFactor;
                    driveTime = TimeSpan.FromHours(distKm / AvgSpeedKmh);
                }
                else
                {
                    // Brak współrzędnych — zakładamy 30 min jazdy
                    distKm = 30;
                    driveTime = TimeSpan.FromMinutes(30);
                }

                var eta = currentTime.Add(driveTime);
                var departure = eta.Add(TimeSpan.FromMinutes(UnloadMinutes));

                result.Stops.Add(new StopEta
                {
                    Kolejnosc = stop.Kolejnosc,
                    NazwaKlienta = stop.NazwaKlienta,
                    DistanceFromPrevKm = Math.Round(distKm, 1),
                    DriveTime = driveTime,
                    Eta = eta,
                    DepartureAfterUnload = departure,
                    HasCoordinates = hasCoords
                });

                totalDistKm += distKm;

                if (hasCoords)
                {
                    prevLat = stop.Latitude;
                    prevLng = stop.Longitude;
                }

                currentTime = departure;
            }

            result.TotalDistanceKm = Math.Round(totalDistKm, 1);

            // Powrót do bazy z ostatniego punktu z koordynatami
            var lastWithCoords = sorted.LastOrDefault(s => s.Latitude != 0 && s.Longitude != 0);
            if (lastWithCoords != null)
            {
                double retDist = HaversineKm(lastWithCoords.Latitude, lastWithCoords.Longitude, BaseLat, BaseLng) * RoadFactor;
                result.ReturnDistanceKm = Math.Round(retDist, 1);
                result.EstimatedReturnTime = currentTime.Add(TimeSpan.FromHours(retDist / AvgSpeedKmh));
            }
            else
            {
                result.ReturnDistanceKm = 0;
                result.EstimatedReturnTime = currentTime.Add(TimeSpan.FromMinutes(30));
            }

            result.TotalDuration = result.EstimatedReturnTime - departureTime;

            return result;
        }

        /// <summary>
        /// Odległość Haversine między dwoma punktami [km].
        /// </summary>
        public static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
        {
            const double R = 6371.0; // promień Ziemi w km

            double dLat = ToRad(lat2 - lat1);
            double dLng = ToRad(lng2 - lng1);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                       Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double ToRad(double deg) => deg * Math.PI / 180.0;
    }
}
