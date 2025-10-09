using System.Collections.Generic;

namespace Kalendarz1
{
    public class OdbiorcaDto
    {
        public int Id { get; set; }
        public string Nazwa { get; set; }
        public string AdresPelny { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int HandlowiecId { get; set; }
        public string HandlowiecNazwa { get; set; }
    }

    public class OdbiorcaFilter
    {
        public List<string> Handlowcy { get; set; }
        public string SearchText { get; set; }
    }
}