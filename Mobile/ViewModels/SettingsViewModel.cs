using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KalendarzMobile.Services;

namespace KalendarzMobile.ViewModels;

/// <summary>
/// ViewModel dla ustawień aplikacji
/// </summary>
public partial class SettingsViewModel : BaseViewModel
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string _apiUrl = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string? _userName;

    [ObservableProperty]
    private string _appVersion = "1.0.0";

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        Title = "Ustawienia";
        LoadSettings();
    }

    private void LoadSettings()
    {
        ApiUrl = _settingsService.ApiBaseUrl;
        UserName = _settingsService.CurrentUserName;
        IsConnected = _settingsService.IsLoggedIn;
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        await ExecuteAsync(async () =>
        {
            _settingsService.ApiBaseUrl = ApiUrl;
            await _settingsService.SaveAsync();

            await Shell.Current.DisplayAlert("Zapisano", "Ustawienia zostały zapisane", "OK");
        }, "Nie udało się zapisać ustawień");
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        await ExecuteAsync(async () =>
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var response = await client.GetAsync($"{ApiUrl}/health");

            if (response.IsSuccessStatusCode)
            {
                IsConnected = true;
                await Shell.Current.DisplayAlert("Sukces", "Połączenie z serwerem działa poprawnie", "OK");
            }
            else
            {
                IsConnected = false;
                await Shell.Current.DisplayAlert("Błąd", $"Serwer zwrócił kod: {response.StatusCode}", "OK");
            }
        }, "Nie udało się połączyć z serwerem");
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "Wylogowanie",
            "Czy na pewno chcesz się wylogować?",
            "Tak",
            "Nie");

        if (confirm)
        {
            _settingsService.Clear();
            UserName = null;
            IsConnected = false;

            await Shell.Current.DisplayAlert("Wylogowano", "Zostałeś wylogowany", "OK");
        }
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "Wyczyść pamięć podręczną",
            "Czy na pewno chcesz wyczyścić pamięć podręczną?",
            "Tak",
            "Nie");

        if (confirm)
        {
            // Tutaj dodać logikę czyszczenia cache
            await Shell.Current.DisplayAlert("Gotowe", "Pamięć podręczna została wyczyszczona", "OK");
        }
    }

    [RelayCommand]
    private async Task ShowAboutAsync()
    {
        await Shell.Current.DisplayAlert(
            "O aplikacji",
            $"Kalendarz Zamówień Mobile\nWersja: {AppVersion}\n\n© 2024 Piórkowscy",
            "OK");
    }
}
