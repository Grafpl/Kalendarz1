using System;
using System.Collections.Generic;
using System.Reflection;
using Kalendarz1.Kartoteka.Models;

namespace Kalendarz1.Kartoteka.Features.Historia
{
    public static class ChangeTracker
    {
        // Pola, które śledzimy w OdbiorcaHandlowca
        private static readonly HashSet<string> _sledzonePola = new(StringComparer.OrdinalIgnoreCase)
        {
            "OsobaKontaktowa", "TelefonKontakt", "EmailKontakt",
            "Asortyment", "PreferencjePakowania", "PreferencjeJakosci", "PreferencjeDostawy",
            "PreferowanyDzienDostawy", "PreferowanaGodzinaDostawy", "AdresDostawyInny",
            "Trasa", "KategoriaHandlowca", "Notatki"
        };

        // Przyjazne nazwy pól do wyświetlania
        private static readonly Dictionary<string, string> _nazwyPol = new(StringComparer.OrdinalIgnoreCase)
        {
            ["OsobaKontaktowa"] = "Osoba kontaktowa",
            ["TelefonKontakt"] = "Telefon kontaktowy",
            ["EmailKontakt"] = "Email kontaktowy",
            ["Asortyment"] = "Asortyment",
            ["PreferencjePakowania"] = "Preferencje pakowania",
            ["PreferencjeJakosci"] = "Preferencje jakości",
            ["PreferencjeDostawy"] = "Preferencje dostawy",
            ["PreferowanyDzienDostawy"] = "Preferowany dzień dostawy",
            ["PreferowanaGodzinaDostawy"] = "Preferowana godzina dostawy",
            ["AdresDostawyInny"] = "Alternatywny adres dostawy",
            ["Trasa"] = "Trasa",
            ["KategoriaHandlowca"] = "Kategoria",
            ["Notatki"] = "Notatki"
        };

        public static string PobierzNazwePola(string nazwaPola)
        {
            return _nazwyPol.TryGetValue(nazwaPola, out var nazwa) ? nazwa : nazwaPola;
        }

        public static List<ZmianaPola> PorownajObiekty<T>(T stary, T nowy)
        {
            var zmiany = new List<ZmianaPola>();
            if (stary == null || nowy == null) return zmiany;

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (!_sledzonePola.Contains(prop.Name)) continue;
                if (!prop.CanRead) continue;

                var staraWartosc = prop.GetValue(stary)?.ToString()?.Trim() ?? "";
                var nowaWartosc = prop.GetValue(nowy)?.ToString()?.Trim() ?? "";

                if (!string.Equals(staraWartosc, nowaWartosc, StringComparison.Ordinal))
                {
                    zmiany.Add(new ZmianaPola
                    {
                        NazwaPola = prop.Name,
                        StaraWartosc = staraWartosc,
                        NowaWartosc = nowaWartosc
                    });
                }
            }

            return zmiany;
        }

        public static T KlonujObiekt<T>(T source) where T : new()
        {
            if (source == null) return default;

            var clone = new T();
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (!prop.CanRead || !prop.CanWrite) continue;
                if (!_sledzonePola.Contains(prop.Name)) continue;

                prop.SetValue(clone, prop.GetValue(source));
            }

            return clone;
        }
    }
}
