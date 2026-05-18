using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;

namespace Kalendarz1.Admin.Models
{
    // Custom szablon uprawnień. Może być "stanowisko" (Zakupowiec, Magazynier) lub
    // mniejszy zestaw modułów do komponowania (Faktury, Kalendarz dostaw).
    // Przechowywany w tabeli LibraNet.dbo.PermissionTemplates.
    public class PermissionTemplate : INotifyPropertyChanged
    {
        public int Id { get; set; }

        private string _name = "";
        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } }
        }

        private string _description = "";
        public string Description
        {
            get => _description;
            set { if (_description != value) { _description = value; OnPropertyChanged(nameof(Description)); } }
        }

        // Lista kluczy modułów (np. "DaneHodowcy,DokumentyZakupu,...") — przechowywana w DB jako CSV
        private List<string> _moduleKeys = new();
        public List<string> ModuleKeys
        {
            get => _moduleKeys;
            set { _moduleKeys = value ?? new List<string>(); OnPropertyChanged(nameof(ModuleKeys)); OnPropertyChanged(nameof(ModuleCount)); }
        }

        public int ModuleCount => _moduleKeys.Count;

        private string _icon = "📋";
        public string Icon
        {
            get => _icon;
            set { if (_icon != value) { _icon = value; OnPropertyChanged(nameof(Icon)); } }
        }

        private string _color = "#3B82F6";
        public string Color
        {
            get => _color;
            set { if (_color != value) { _color = value; OnPropertyChanged(nameof(Color)); OnPropertyChanged(nameof(ColorBrush)); } }
        }

        // Skonwertowany hex → SolidColorBrush dla bindingów XAML (Border.Background w karcie).
        public SolidColorBrush ColorBrush
        {
            get
            {
                try
                {
                    var c = (System.Windows.Media.Color)ColorConverter.ConvertFromString(_color);
                    var b = new SolidColorBrush(c);
                    b.Freeze();
                    return b;
                }
                catch
                {
                    return Brushes.SteelBlue;
                }
            }
        }

        // Stan zaznaczenia w liście szablonów (wizualne podświetlenie wybranego)
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); } }
        }

        public DateTime? CreatedAt { get; set; }
        public string CreatedBy { get; set; } = "";

        // CSV → List
        public static List<string> ParseModuleKeys(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new List<string>();
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      .Select(s => s.Trim())
                      .Where(s => s.Length > 0)
                      .ToList();
        }

        // List → CSV
        public string ModuleKeysCsv => string.Join(",", _moduleKeys);

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
