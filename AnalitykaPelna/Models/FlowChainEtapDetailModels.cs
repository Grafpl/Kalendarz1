using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Kalendarz1.AnalitykaPelna.Models
{
    /// <summary>
    /// Pełen szczegół jednego etapu łańcucha produkcji — wszystkie dokumenty, towary,
    /// per dzień i per magazyn. Otwierany po kliknięciu w kafelek łańcucha.
    /// </summary>
    public class FlowChainEtapDetail
    {
        public string EtapNazwa { get; set; } = "";
        public string EtapIkona { get; set; } = "";
        public string EtapKolor { get; set; } = "#94A3B8";
        public string EtapOpis { get; set; } = ""; // np. "Tuszki/podroby z linii uboju (sPWU)"
        public DateTime DataOd { get; set; }
        public DateTime DataDo { get; set; }

        public decimal SumaKg { get; set; }
        public int LiczbaDokumentow { get; set; }
        public int LiczbaPozycji { get; set; }
        public int LiczbaDni => (DataDo.Date - DataOd.Date).Days + 1;
        public decimal SredniaKgDzien => LiczbaDni > 0 ? SumaKg / LiczbaDni : 0;

        public List<FlowChainDokument> Dokumenty { get; set; } = new();
        public List<FlowChainTowar> Towary { get; set; } = new();
        public List<FlowChainDzien> PerDzien { get; set; } = new();
        public List<FlowChainMagazyn> PerMagazyn { get; set; } = new();
        public List<FlowChainKontrahent> Kontrahenci { get; set; } = new();
    }

    /// <summary>Pojedynczy dokument Symfonii w etapie.</summary>
    public class FlowChainDokument
    {
        public int Id { get; set; }
        public string Kod { get; set; } = "";
        public DateTime Data { get; set; }
        public string Seria { get; set; } = "";
        public int? MagazynId { get; set; }
        public string MagazynNazwa { get; set; } = "";
        public int? MagazynDoId { get; set; }   // dla MM-: docelowy
        public string MagazynDoNazwa { get; set; } = "";
        public int? KhId { get; set; }
        public string Kontrahent { get; set; } = "";
        public decimal Kg { get; set; }
        public int LiczbaPozycji { get; set; }
        public string Opis { get; set; } = "";
    }

    /// <summary>Towar zagregowany dla etapu.</summary>
    public class FlowChainTowar
    {
        public int TwId { get; set; }
        public string Kod { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public int Katalog { get; set; }
        public string Kategoria { get; set; } = "";
        public decimal Kg { get; set; }
        public int LiczbaDok { get; set; }
        public decimal ProcentUdzialu { get; set; }
        public ImageSource? ImageSource { get; set; }
        public Visibility ImageVisibility => ImageSource != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility PlaceholderVisibility => ImageSource == null ? Visibility.Visible : Visibility.Collapsed;
        public string PlaceholderEmoji
        {
            get
            {
                return Katalog switch
                {
                    65882 => "🐔",
                    67094 => "🗑",
                    67095 => "🥩",
                    67104 => "🍗",
                    67153 => "❄",
                    _ => "📦"
                };
            }
        }
    }

    /// <summary>Agregacja dzienna.</summary>
    public class FlowChainDzien
    {
        public DateTime Data { get; set; }
        public decimal Kg { get; set; }
        public int LiczbaDok { get; set; }
        public string DataFormatted => Data.ToString("dd.MM.yyyy (ddd)");
    }

    /// <summary>Agregacja per magazyn źródłowy.</summary>
    public class FlowChainMagazyn
    {
        public int? Id { get; set; }
        public string Nazwa { get; set; } = "";
        public decimal Kg { get; set; }
        public int LiczbaDok { get; set; }
        public decimal ProcentUdzialu { get; set; }
    }

    /// <summary>Agregacja per kontrahent (dla sPZ od hodowców i sWZ do klientów).</summary>
    public class FlowChainKontrahent
    {
        public int? KhId { get; set; }
        public string Nazwa { get; set; } = "";
        public decimal Kg { get; set; }
        public int LiczbaDok { get; set; }
        public decimal ProcentUdzialu { get; set; }
    }
}
