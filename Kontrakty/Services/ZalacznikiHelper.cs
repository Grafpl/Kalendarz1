using System;
using System.IO;
using System.Threading.Tasks;

namespace Kalendarz1.Kontrakty.Services
{
    /// <summary>
    /// Upload skanu/aneksu na udział + rejestracja w dbo.KontraktyZalaczniki.
    /// Ścieżka: \\192.168.0.170\Install\UmowyZakupu\&lt;rok&gt;\Skany\&lt;NrUmowy&gt;_&lt;Typ&gt;_&lt;timestamp&gt;.pdf
    /// Timestamp w nazwie — kolejny skan NIE nadpisuje poprzedniego.
    /// </summary>
    public static class ZalacznikiHelper
    {
        public const string Root = @"\\192.168.0.170\Install\UmowyZakupu";

        public static string SanitizeNumer(string numer)
        {
            string s = (numer ?? "").Replace('/', '-');
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Trim();
        }

        /// <summary>Kopiuje plik na udział i zapisuje wpis w KontraktyZalaczniki. Zwraca ścieżkę docelową (UNC).</summary>
        public static async Task<string> UploadAsync(
            KontraktyService svc, int kontraktId, int? wersjaId,
            string numerKontraktu, int rok, string typZalacznika,
            string zrodloPath, string userId, string? opis = null)
        {
            if (!File.Exists(zrodloPath))
                throw new FileNotFoundException("Nie znaleziono pliku źródłowego: " + zrodloPath, zrodloPath);

            string folder = Path.Combine(Root, rok.ToString(), "Skany");
            Directory.CreateDirectory(folder);

            string ext = Path.GetExtension(zrodloPath);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".pdf";
            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string nazwaDocelowa = $"{SanitizeNumer(numerKontraktu)}_{typZalacznika}_{ts}{ext}";
            string docel = Path.Combine(folder, nazwaDocelowa);

            File.Copy(zrodloPath, docel, overwrite: false);

            await svc.AddZalacznikAsync(kontraktId, wersjaId, typZalacznika,
                Path.GetFileName(zrodloPath), docel, userId, opis);

            return docel;
        }
    }
}
