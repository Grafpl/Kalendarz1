using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Kalendarz1.Admin.Models
{
    // Model użytkownika dla panelu uprawnień. Implementuje INotifyPropertyChanged
    // żeby UI mogło reagować na zmiany (np. odświeżenie avatara, licznik uprawnień,
    // ostatnie logowanie). Avatary i logowania ładowane są lazy w AdminPermissionsService.
    public class AdminUserInfo : INotifyPropertyChanged
    {
        public string ID { get; set; } = "";

        private string _name = "";
        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); OnPropertyChanged(nameof(Initials)); } }
        }

        private int _enabledCount;
        public int EnabledCount
        {
            get => _enabledCount;
            set { if (_enabledCount != value) { _enabledCount = value; OnPropertyChanged(nameof(EnabledCount)); OnPropertyChanged(nameof(EnabledCountText)); } }
        }

        public string EnabledCountText => $"{EnabledCount} uprawnień";

        public string Initials
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_name)) return "?";
                var parts = _name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1) return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpper() : parts[0].ToUpper();
                return $"{parts[0][0]}{parts[parts.Length - 1][0]}".ToUpper();
            }
        }

        // ── Selected state (do wizualizacji zaznaczonej karty) ───────────────
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); } }
        }

        // ── Avatar ─────────────────────────────────────────────────────────────
        private BitmapImage? _avatarSource;
        public BitmapImage? AvatarSource
        {
            get => _avatarSource;
            set
            {
                if (_avatarSource != value)
                {
                    _avatarSource = value;
                    OnPropertyChanged(nameof(AvatarSource));
                    OnPropertyChanged(nameof(HasAvatar));
                    OnPropertyChanged(nameof(InitialsVisibility));
                    OnPropertyChanged(nameof(AvatarVisibility));
                }
            }
        }

        public bool HasAvatar => _avatarSource != null;
        public Visibility AvatarVisibility => _avatarSource != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility InitialsVisibility => _avatarSource == null ? Visibility.Visible : Visibility.Collapsed;

        // ── Ostatnie logowanie ─────────────────────────────────────────────────
        private DateTime? _lastLogin;
        public DateTime? LastLogin
        {
            get => _lastLogin;
            set
            {
                if (_lastLogin != value)
                {
                    _lastLogin = value;
                    OnPropertyChanged(nameof(LastLogin));
                    OnPropertyChanged(nameof(LastLoginText));
                    OnPropertyChanged(nameof(LastLoginShortText));
                }
            }
        }

        // "wczoraj" / "3 dni temu" / "2 tyg. temu" / "nigdy"
        public string LastLoginText
        {
            get
            {
                if (!_lastLogin.HasValue) return "Brak logowań";
                var diff = DateTime.Now - _lastLogin.Value;
                if (diff.TotalMinutes < 1) return "przed chwilą";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min temu";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} godz. temu";
                if (diff.TotalDays < 2) return "wczoraj";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} dni temu";
                if (diff.TotalDays < 30) return $"{(int)(diff.TotalDays / 7)} tyg. temu";
                if (diff.TotalDays < 365) return $"{(int)(diff.TotalDays / 30)} mies. temu";
                return $"{(int)(diff.TotalDays / 365)} lat temu";
            }
        }

        // Krótki format do karty: "12.05" / "5d" / "wczoraj"
        public string LastLoginShortText
        {
            get
            {
                if (!_lastLogin.HasValue) return "—";
                var diff = DateTime.Now - _lastLogin.Value;
                if (diff.TotalHours < 24) return _lastLogin.Value.ToString("HH:mm");
                if (diff.TotalDays < 2) return "wczoraj";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d temu";
                return _lastLogin.Value.ToString("dd.MM.yyyy");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
