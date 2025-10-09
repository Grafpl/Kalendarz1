// =============== MarkerClusterer.cs - NOWY PLIK ===============
using GMap.NET;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kalendarz1
{
    public class MarkerClusterer
    {
        private readonly double clusterDistance;

        public MarkerClusterer(double distance = 50)
        {
            clusterDistance = distance;
        }

        public List<MarkerCluster> CreateClusters(List<OdbiorcaDto> odbiorcy, double zoomLevel)
        {
            var clusters = new List<MarkerCluster>();
            var processed = new HashSet<int>();

            // Dostosuj odległość klastrowania do poziomu zoom
            double adjustedDistance = clusterDistance / Math.Pow(2, zoomLevel - 6);

            foreach (var odbiorca in odbiorcy)
            {
                if (!odbiorca.Latitude.HasValue || !odbiorca.Longitude.HasValue)
                    continue;

                if (processed.Contains(odbiorca.Id))
                    continue;

                var cluster = new MarkerCluster
                {
                    Position = new PointLatLng(odbiorca.Latitude.Value, odbiorca.Longitude.Value),
                    Items = new List<OdbiorcaDto> { odbiorca }
                };
                processed.Add(odbiorca.Id);

                // Znajdź pobliskie punkty
                foreach (var other in odbiorcy.Where(o => !processed.Contains(o.Id)))
                {
                    if (!other.Latitude.HasValue || !other.Longitude.HasValue)
                        continue;

                    double distance = CalculateDistance(
                        cluster.Position.Lat, cluster.Position.Lng,
                        other.Latitude.Value, other.Longitude.Value);

                    if (distance < adjustedDistance)
                    {
                        cluster.Items.Add(other);
                        processed.Add(other.Id);
                    }
                }

                // Przelicz centrum klastra
                if (cluster.Items.Count > 1)
                {
                    double avgLat = cluster.Items.Average(i => i.Latitude.Value);
                    double avgLng = cluster.Items.Average(i => i.Longitude.Value);
                    cluster.Position = new PointLatLng(avgLat, avgLng);
                }

                clusters.Add(cluster);
            }

            return clusters;
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            // Uproszczona kalkulacja odległości w pikselach
            double dLat = Math.Abs(lat1 - lat2);
            double dLon = Math.Abs(lon1 - lon2);
            return Math.Sqrt(dLat * dLat + dLon * dLon) * 111; // przybliżone km
        }
    }

    public class MarkerCluster
    {
        public PointLatLng Position { get; set; }
        public List<OdbiorcaDto> Items { get; set; }
    }
}