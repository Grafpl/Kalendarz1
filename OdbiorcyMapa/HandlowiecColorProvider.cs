using System;
using System.Collections.Generic;
using System.Drawing;

namespace Kalendarz1
{
    public class HandlowiecColorProvider
    {
        private readonly Dictionary<string, Color> colorMap = new Dictionary<string, Color>();
        private readonly List<Color> availableColors = new List<Color>
        {
            ColorTranslator.FromHtml("#e74c3c"), // czerwony
            ColorTranslator.FromHtml("#3498db"), // niebieski
            ColorTranslator.FromHtml("#2ecc71"), // zielony
            ColorTranslator.FromHtml("#f39c12"), // pomarańczowy
            ColorTranslator.FromHtml("#9b59b6"), // fioletowy
            ColorTranslator.FromHtml("#1abc9c"), // turkusowy
            ColorTranslator.FromHtml("#e67e22"), // ciemny pomarańczowy
            ColorTranslator.FromHtml("#95a5a6"), // szary
            ColorTranslator.FromHtml("#d35400"), // ceglasty
            ColorTranslator.FromHtml("#27ae60"), // ciemny zielony
            ColorTranslator.FromHtml("#2980b9"), // ciemny niebieski
            ColorTranslator.FromHtml("#8e44ad"), // ciemny fiolet
        };

        private int colorIndex = 0;

        public Color GetColor(string handlowiec)
        {
            if (string.IsNullOrEmpty(handlowiec))
                return Color.Gray;

            if (!colorMap.ContainsKey(handlowiec))
            {
                colorMap[handlowiec] = availableColors[colorIndex % availableColors.Count];
                colorIndex++;
            }
            return colorMap[handlowiec];
        }

        public void Reset()
        {
            colorMap.Clear();
            colorIndex = 0;
        }

        public Dictionary<string, Color> GetAllAssignedColors()
        {
            return new Dictionary<string, Color>(colorMap);
        }
    }
}