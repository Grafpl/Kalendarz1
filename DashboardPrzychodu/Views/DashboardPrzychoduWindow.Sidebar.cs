using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Kalendarz1.DashboardPrzychodu.Models;
using Kalendarz1.DashboardPrzychodu.Theme;

namespace Kalendarz1.DashboardPrzychodu.Views
{
    /// <summary>
    /// #11 - Sidebar: deterministyczny kolor hodowcy (#15 hash FNV-1a) + stacked bar realizacji dnia.
    /// AssignHodowcaColors mapuje nazwy hodowcow na kolory z palety, oznacza pierwszy/ostatni wiersz
    /// grupy dla DataGrid separatorow i wykrywa "aktywne karty" do pulsowania.
    /// </summary>
    public partial class DashboardPrzychoduWindow
    {
        /// <summary>
        /// Przypisuje deterministyczny kolor hodowcom na podstawie hash nazwy (#15).
        /// Ten sam hodowca = ten sam kolor zawsze (miedzy sesjami).
        /// </summary>
        private void AssignHodowcaColors()
        {
            _hodowcaColorMap.Clear();

            // Mapuj kolory dla WSZYSTKICH hodowców - z FarmerCalc + z HarmonogramDostaw.
            // Inaczej hodowcy z harmonogramu którzy nie dostarczyli aut (np. Łapiak Monika
            // gdy rodzeństwo zmieniło decyzję) mają HodowcaKolor=null → nazwa karty
            // niewidoczna (czarny tekst na ciemnym tle).
            var uniqueNames = _dostawy
                .Select(d => (d.Hodowca ?? "").Trim())
                .Concat(_postepyHarmonogramow.Select(h => (h.Hodowca ?? "").Trim()))
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Deterministyczny FNV-1a hash → indeks palety.
            // Kolizje: linear probing - drugi hodowca z tym samym slotem dostaje sasiedni wolny.
            var usedIndices = new HashSet<int>();
            foreach (var name in uniqueNames)
            {
                int paletteSize = DashboardBrushes.HodowcaPalette.Length;
                int idx = (DashboardBrushes.DeterministicHash(name) & 0x7FFFFFFF) % paletteSize;
                int probe = 0;
                while (usedIndices.Contains(idx) && probe < paletteSize)
                {
                    idx = (idx + 1) % paletteSize;
                    probe++;
                }
                usedIndices.Add(idx);
                _hodowcaColorMap[name] = DashboardBrushes.HodowcaPalette[idx];
            }

            // Pierwszy/ostatni wiersz grupy hodowcy dla DataGrid (separatory + POZOSTALO)
            var ostatniIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _dostawy.Count; i++)
            {
                var key = (_dostawy[i].Hodowca ?? "").Trim();
                if (!string.IsNullOrEmpty(key))
                    ostatniIndex[key] = i;
            }

            var pierwszyIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _dostawy.Count; i++)
            {
                var key = (_dostawy[i].Hodowca ?? "").Trim();
                if (!string.IsNullOrEmpty(key) && !pierwszyIndex.ContainsKey(key))
                    pierwszyIndex[key] = i;
            }

            for (int i = 0; i < _dostawy.Count; i++)
            {
                var d = _dostawy[i];
                var key = (d.Hodowca ?? "").Trim();

                d.OstatniWierszHodowcy = ostatniIndex.TryGetValue(key, out int li) && li == i;
                d.PierwszyWierszGrupy = i > 0 && pierwszyIndex.TryGetValue(key, out int fi) && fi == i;

                if (_hodowcaColorMap.TryGetValue(key, out var brush))
                {
                    d.HodowcaKolor = brush;

                    // Tlo wiersza = blend (base dark 8% + status tint)
                    var srcColor = ((SolidColorBrush)brush).Color;
                    byte sR, sG, sB;
                    byte statusAlpha;
                    switch (d.Status)
                    {
                        case StatusDostawy.Zwazony:
                            sR = 34; sG = 197; sB = 94; statusAlpha = 12; break;
                        case StatusDostawy.BruttoWpisane:
                            sR = 249; sG = 115; sB = 22; statusAlpha = 10; break;
                        default: // Oczekuje
                            sR = 239; sG = 68; sB = 68; statusAlpha = 10; break;
                    }
                    byte bR = (byte)(28 + (srcColor.R - 28) * 0.08 + (sR - 28) * statusAlpha / 255.0);
                    byte bG = (byte)(25 + (srcColor.G - 25) * 0.08 + (sG - 25) * statusAlpha / 255.0);
                    byte bB = (byte)(23 + (srcColor.B - 23) * 0.08 + (sB - 23) * statusAlpha / 255.0);

                    var bgBrush = new SolidColorBrush(Color.FromRgb(bR, bG, bB));
                    bgBrush.Freeze();
                    d.HodowcaKolorTlo = bgBrush;
                }
            }

            // Wykryj aktywne karty (zmiana AutaZwazone od ostatniego refresh) + sortuj.
            // Sortowanie wg kolejności wjazdu: pierwszy hodowca w DataGrid (najmniejszy NrKursu)
            // dostaje swoja karte na pierwszym miejscu w sidebarze.
            var noweAuta = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Mapuj LpDostawy → najmniejszy NrKursu (kolejność wjazdu na rampę)
            var kolejnoscWjazdu = _dostawy
                .Where(d => d.LpDostawy.HasValue)
                .GroupBy(d => d.LpDostawy.Value)
                .ToDictionary(g => g.Key, g => g.Min(d => d.NrKursu));

            var posortowane = _postepyHarmonogramow
                .OrderBy(h => kolejnoscWjazdu.TryGetValue(h.LpDostawy, out var nr) ? nr : int.MaxValue)
                .ThenBy(h => h.Hodowca)
                .ToList();
            _postepyHarmonogramow.Clear();
            foreach (var h in posortowane)
            {
                var key = (h.Hodowca ?? "").Trim();
                if (_hodowcaColorMap.TryGetValue(key, out var brush))
                    h.HodowcaKolor = brush;

                bool aktywna = false;
                if (_poprzednieAutaZwazone.TryGetValue(key, out int poprzednio))
                    aktywna = h.AutaZwazone > poprzednio;
                h.JestAktywna = aktywna;

                noweAuta[key] = h.AutaZwazone;
                _postepyHarmonogramow.Add(h);
            }
            _poprzednieAutaZwazone = noweAuta;
        }
    }
}
