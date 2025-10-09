using System;
using System.Collections.Generic;
using System.Linq;

namespace Kalendarz1
{
    public class DuplicateAddressGrouper
    {
        private const double COORDINATE_TOLERANCE = 0.00001; // około 1 metr

        public List<List<OdbiorcaDto>> GroupByLocation(List<OdbiorcaDto> odbiorcy)
        {
            if (odbiorcy == null || odbiorcy.Count == 0)
                return new List<List<OdbiorcaDto>>();

            var groups = new List<List<OdbiorcaDto>>();
            var processed = new HashSet<int>();

            foreach (var odbiorca in odbiorcy)
            {
                if (processed.Contains(odbiorca.Id))
                    continue;

                var group = new List<OdbiorcaDto> { odbiorca };
                processed.Add(odbiorca.Id);

                // Znajdź wszystkich z tym samym adresem lub bardzo bliskimi koordynatami
                foreach (var other in odbiorcy.Where(o => o.Id != odbiorca.Id && !processed.Contains(o.Id)))
                {
                    bool sameAddress = !string.IsNullOrEmpty(odbiorca.AdresPelny) &&
                                      odbiorca.AdresPelny == other.AdresPelny;
                    bool sameCoords = false;

                    if (odbiorca.Latitude.HasValue && odbiorca.Longitude.HasValue &&
                        other.Latitude.HasValue && other.Longitude.HasValue)
                    {
                        double latDiff = Math.Abs(odbiorca.Latitude.Value - other.Latitude.Value);
                        double lngDiff = Math.Abs(odbiorca.Longitude.Value - other.Longitude.Value);
                        sameCoords = latDiff < COORDINATE_TOLERANCE && lngDiff < COORDINATE_TOLERANCE;
                    }

                    if (sameAddress || sameCoords)
                    {
                        group.Add(other);
                        processed.Add(other.Id);
                    }
                }

                groups.Add(group);
            }

            return groups;
        }

        public List<AddressDuplicate> GetDuplicates(List<OdbiorcaDto> odbiorcy)
        {
            var groups = GroupByLocation(odbiorcy);
            var duplicates = new List<AddressDuplicate>();

            foreach (var group in groups.Where(g => g.Count > 1))
            {
                duplicates.Add(new AddressDuplicate
                {
                    Address = group.First().AdresPelny,
                    Count = group.Count,
                    Contractors = group.Select(o => o.Nazwa).ToList(),
                    Latitude = group.First().Latitude,
                    Longitude = group.First().Longitude
                });
            }

            return duplicates.OrderByDescending(d => d.Count).ToList();
        }
    }

    public class AddressDuplicate
    {
        public string Address { get; set; }
        public int Count { get; set; }
        public List<string> Contractors { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}