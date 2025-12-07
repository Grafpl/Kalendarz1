using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Kalendarz1.OfertaCenowa
{
    /// <summary>
    /// Manager do zarządzania ulubionymi odbiorcami per operator
    /// </summary>
    public class UlubieniOdbiorcyManager
    {
        private readonly string _folderPath;
        private Dictionary<string, HashSet<string>> _ulubieni = new();

        public UlubieniOdbiorcyManager()
        {
            _folderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Kalendarz1",
                "UlubieniOdbiorcy");

            Directory.CreateDirectory(_folderPath);
        }

        private string GetFilePath(string operatorId)
        {
            return Path.Combine(_folderPath, $"ulubieni_{operatorId}.json");
        }

        /// <summary>
        /// Wczytuje listę ulubionych dla operatora
        /// </summary>
        public HashSet<string> WczytajUlubionych(string operatorId)
        {
            if (_ulubieni.ContainsKey(operatorId))
                return _ulubieni[operatorId];

            var filePath = GetFilePath(operatorId);
            if (File.Exists(filePath))
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    var lista = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                    _ulubieni[operatorId] = new HashSet<string>(lista);
                }
                catch
                {
                    _ulubieni[operatorId] = new HashSet<string>();
                }
            }
            else
            {
                _ulubieni[operatorId] = new HashSet<string>();
            }

            return _ulubieni[operatorId];
        }

        /// <summary>
        /// Zapisuje listę ulubionych
        /// </summary>
        private void ZapiszUlubionych(string operatorId)
        {
            var filePath = GetFilePath(operatorId);
            var lista = new List<string>(_ulubieni[operatorId]);
            var json = JsonSerializer.Serialize(lista, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Sprawdza czy odbiorca jest ulubiony
        /// </summary>
        public bool CzyUlubiony(string operatorId, string odbiorcaId, string zrodlo)
        {
            var ulubieni = WczytajUlubionych(operatorId);
            var klucz = $"{zrodlo}_{odbiorcaId}";
            return ulubieni.Contains(klucz);
        }

        /// <summary>
        /// Dodaje/usuwa odbiorcę z ulubionych
        /// </summary>
        public bool PrzelaczUlubiony(string operatorId, string odbiorcaId, string zrodlo)
        {
            var ulubieni = WczytajUlubionych(operatorId);
            var klucz = $"{zrodlo}_{odbiorcaId}";

            bool terazUlubiony;
            if (ulubieni.Contains(klucz))
            {
                ulubieni.Remove(klucz);
                terazUlubiony = false;
            }
            else
            {
                ulubieni.Add(klucz);
                terazUlubiony = true;
            }

            ZapiszUlubionych(operatorId);
            return terazUlubiony;
        }

        /// <summary>
        /// Dodaje do ulubionych
        /// </summary>
        public void DodajDoUlubionych(string operatorId, string odbiorcaId, string zrodlo)
        {
            var ulubieni = WczytajUlubionych(operatorId);
            var klucz = $"{zrodlo}_{odbiorcaId}";
            if (!ulubieni.Contains(klucz))
            {
                ulubieni.Add(klucz);
                ZapiszUlubionych(operatorId);
            }
        }

        /// <summary>
        /// Usuwa z ulubionych
        /// </summary>
        public void UsunZUlubionych(string operatorId, string odbiorcaId, string zrodlo)
        {
            var ulubieni = WczytajUlubionych(operatorId);
            var klucz = $"{zrodlo}_{odbiorcaId}";
            if (ulubieni.Contains(klucz))
            {
                ulubieni.Remove(klucz);
                ZapiszUlubionych(operatorId);
            }
        }
    }
}
