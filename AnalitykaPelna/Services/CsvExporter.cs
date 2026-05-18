using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace Kalendarz1.AnalitykaPelna.Services
{
    /// <summary>
    /// Wspólny eksporter CSV. Reflection-based — wykrywa wszystkie publiczne właściwości i serializuje.
    /// Format: UTF-8 BOM, separator ';' (Excel-friendly w Polsce), CRLF, escape cudzysłowy.
    /// </summary>
    public static class CsvExporter
    {
        public static bool Eksportuj<T>(IEnumerable<T> dane, string sugerowanaNazwa,
            string[]? wybraneKolumny = null, Window? owner = null)
        {
            var lista = dane?.ToList();
            if (lista == null || lista.Count == 0)
            {
                MessageBox.Show("Brak danych do eksportu.", "Eksport CSV",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            var dlg = new SaveFileDialog
            {
                FileName = SanityzujNazwe(sugerowanaNazwa) + "_" + DateTime.Now.ToString("yyyy-MM-dd_HHmm") + ".csv",
                Filter = "Plik CSV (*.csv)|*.csv",
                DefaultExt = "csv"
            };
            if (owner != null) dlg.InitialDirectory = AnalitykaSettings.OstatniFolderEksportu ?? "";

            if (dlg.ShowDialog(owner) != true) return false;

            try
            {
                var props = WybierzWlasciwosci<T>(wybraneKolumny);
                var sb = new StringBuilder();

                // Nagłówek
                sb.AppendLine(string.Join(";", props.Select(p => Csv(p.Name))));

                // Wiersze
                foreach (var item in lista)
                {
                    var komorki = props.Select(p => FormatujWartosc(p.GetValue(item)));
                    sb.AppendLine(string.Join(";", komorki));
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                AnalitykaSettings.OstatniFolderEksportu = Path.GetDirectoryName(dlg.FileName);
                AnalitykaSettings.Zapisz();

                MessageBox.Show($"Zapisano {lista.Count} wierszy do:\n{dlg.FileName}", "Eksport CSV",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Eksport CSV",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private static List<PropertyInfo> WybierzWlasciwosci<T>(string[]? whitelist)
        {
            var all = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0)
                .Where(p => p.PropertyType.IsPrimitive || p.PropertyType == typeof(string)
                    || p.PropertyType == typeof(decimal) || p.PropertyType == typeof(DateTime)
                    || p.PropertyType == typeof(bool) || Nullable.GetUnderlyingType(p.PropertyType) != null)
                .ToList();

            if (whitelist == null || whitelist.Length == 0) return all;
            return whitelist.Select(n => all.FirstOrDefault(p => p.Name == n))
                .Where(p => p != null).Cast<PropertyInfo>().ToList();
        }

        private static string FormatujWartosc(object? v)
        {
            if (v == null) return "";
            return v switch
            {
                DateTime dt => dt.TimeOfDay == TimeSpan.Zero
                    ? dt.ToString("yyyy-MM-dd")
                    : dt.ToString("yyyy-MM-dd HH:mm:ss"),
                decimal d => d.ToString("F2", CultureInfo.InvariantCulture),
                double db => db.ToString("F2", CultureInfo.InvariantCulture),
                float f => f.ToString("F2", CultureInfo.InvariantCulture),
                bool b => b ? "1" : "0",
                _ => Csv(v.ToString() ?? "")
            };
        }

        private static string Csv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(';') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        private static string SanityzujNazwe(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Eksport";
            foreach (char c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s;
        }
    }
}
