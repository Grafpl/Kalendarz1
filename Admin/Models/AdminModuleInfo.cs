using System.ComponentModel;

namespace Kalendarz1.Admin.Models
{
    // Model modułu (kafelka uprawnień). HasAccess jest bindowane do CheckBox.IsChecked.
    public class AdminModuleInfo : INotifyPropertyChanged
    {
        public string Key { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public string Icon { get; set; } = "";

        private bool _hasAccess;
        public bool HasAccess
        {
            get => _hasAccess;
            set { if (_hasAccess != value) { _hasAccess = value; OnPropertyChanged(nameof(HasAccess)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
