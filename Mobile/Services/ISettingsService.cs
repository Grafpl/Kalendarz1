namespace KalendarzMobile.Services;

/// <summary>
/// Serwis do zarządzania ustawieniami aplikacji
/// </summary>
public interface ISettingsService
{
    string ApiBaseUrl { get; set; }
    string? AuthToken { get; set; }
    string? CurrentUserId { get; set; }
    string? CurrentUserName { get; set; }
    bool IsLoggedIn { get; }

    Task SaveAsync();
    Task LoadAsync();
    void Clear();
}

public class SettingsService : ISettingsService
{
    private const string API_URL_KEY = "api_base_url";
    private const string AUTH_TOKEN_KEY = "auth_token";
    private const string USER_ID_KEY = "user_id";
    private const string USER_NAME_KEY = "user_name";

    // Domyślny URL API - do zmiany na faktyczny adres serwera
    private const string DEFAULT_API_URL = "http://192.168.0.109:5000/api";

    public string ApiBaseUrl
    {
        get => Preferences.Get(API_URL_KEY, DEFAULT_API_URL);
        set => Preferences.Set(API_URL_KEY, value);
    }

    public string? AuthToken
    {
        get => Preferences.Get(AUTH_TOKEN_KEY, null);
        set
        {
            if (value != null)
                Preferences.Set(AUTH_TOKEN_KEY, value);
            else
                Preferences.Remove(AUTH_TOKEN_KEY);
        }
    }

    public string? CurrentUserId
    {
        get => Preferences.Get(USER_ID_KEY, null);
        set
        {
            if (value != null)
                Preferences.Set(USER_ID_KEY, value);
            else
                Preferences.Remove(USER_ID_KEY);
        }
    }

    public string? CurrentUserName
    {
        get => Preferences.Get(USER_NAME_KEY, null);
        set
        {
            if (value != null)
                Preferences.Set(USER_NAME_KEY, value);
            else
                Preferences.Remove(USER_NAME_KEY);
        }
    }

    public bool IsLoggedIn => !string.IsNullOrEmpty(AuthToken);

    public Task SaveAsync()
    {
        // Preferences są automatycznie zapisywane
        return Task.CompletedTask;
    }

    public Task LoadAsync()
    {
        // Preferences są automatycznie ładowane
        return Task.CompletedTask;
    }

    public void Clear()
    {
        Preferences.Remove(AUTH_TOKEN_KEY);
        Preferences.Remove(USER_ID_KEY);
        Preferences.Remove(USER_NAME_KEY);
    }
}
