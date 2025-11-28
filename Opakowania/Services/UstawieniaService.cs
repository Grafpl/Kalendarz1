using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Kalendarz1.Opakowania.Models;

namespace Kalendarz1.Opakowania.Services
{
    /// <summary>
    /// Serwis do zarządzania ustawieniami użytkownika
    /// </summary>
    public class UstawieniaService
    {
        private static readonly string _sciezkaUstawien = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Kalendarz1",
            "OpakowaniaSettings.json"
        );

        private UstawieniaUzytkownika _ustawienia;

        public UstawieniaUzytkownika Ustawienia => _ustawienia;

        public UstawieniaService()
        {
            _ustawienia = new UstawieniaUzytkownika();
        }

        /// <summary>
        /// Wczytuje ustawienia z pliku
        /// </summary>
        public async Task<UstawieniaUzytkownika> WczytajUstawieniaAsync()
        {
            try
            {
                if (File.Exists(_sciezkaUstawien))
                {
                    string json = await File.ReadAllTextAsync(_sciezkaUstawien);
                    _ustawienia = JsonSerializer.Deserialize<UstawieniaUzytkownika>(json) ?? new UstawieniaUzytkownika();
                }
                else
                {
                    _ustawienia = new UstawieniaUzytkownika();
                }

                // Zastosuj progi do modelu SaldoOpakowania
                SaldoOpakowania.ProgOstrzezenia = _ustawienia.ProgOstrzezenia;
                SaldoOpakowania.ProgKrytyczny = _ustawienia.ProgKrytyczny;

                return _ustawienia;
            }
            catch (Exception)
            {
                _ustawienia = new UstawieniaUzytkownika();
                return _ustawienia;
            }
        }

        /// <summary>
        /// Zapisuje ustawienia do pliku
        /// </summary>
        public async Task ZapiszUstawieniaAsync(UstawieniaUzytkownika ustawienia = null)
        {
            try
            {
                if (ustawienia != null)
                    _ustawienia = ustawienia;

                // Upewnij się że folder istnieje
                string folder = Path.GetDirectoryName(_sciezkaUstawien);
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_ustawienia, options);
                await File.WriteAllTextAsync(_sciezkaUstawien, json);

                // Zastosuj progi do modelu SaldoOpakowania
                SaldoOpakowania.ProgOstrzezenia = _ustawienia.ProgOstrzezenia;
                SaldoOpakowania.ProgKrytyczny = _ustawienia.ProgKrytyczny;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd zapisu ustawień: {ex.Message}");
            }
        }

        /// <summary>
        /// Resetuje ustawienia do domyślnych
        /// </summary>
        public async Task ResetujUstawieniaAsync()
        {
            _ustawienia = new UstawieniaUzytkownika();
            await ZapiszUstawieniaAsync();
        }

        /// <summary>
        /// Aktualizuje progi i zapisuje
        /// </summary>
        public async Task UstawProgiAsync(int progOstrzezenia, int progKrytyczny)
        {
            _ustawienia.ProgOstrzezenia = progOstrzezenia;
            _ustawienia.ProgKrytyczny = progKrytyczny;
            SaldoOpakowania.ProgOstrzezenia = progOstrzezenia;
            SaldoOpakowania.ProgKrytyczny = progKrytyczny;
            await ZapiszUstawieniaAsync();
        }

        /// <summary>
        /// Pobiera domyślne daty (poprzedni tydzień: od poniedziałku do niedzieli)
        /// </summary>
        public static (DateTime DataOd, DateTime DataDo) GetDomyslnyOkres()
        {
            var dzisiaj = DateTime.Today;
            
            // Znajdź poprzedni poniedziałek
            int daysFromMonday = ((int)dzisiaj.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var tenPoniedzialek = dzisiaj.AddDays(-daysFromMonday);
            
            // Poprzedni tydzień
            var poprzedniPoniedzialek = tenPoniedzialek.AddDays(-7);
            var poprzedniaNiedziela = tenPoniedzialek.AddDays(-1);

            return (poprzedniPoniedzialek, poprzedniaNiedziela);
        }

        /// <summary>
        /// Pobiera domyślny zakres dat jako DateDo (dzisiaj) dla widoku sald
        /// </summary>
        public static DateTime GetDomyslnaDataDo()
        {
            return DateTime.Today;
        }
    }
}
