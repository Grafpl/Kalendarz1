using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KalendarzMobile.Models;
using KalendarzMobile.Services;
using KalendarzMobile.Views;

namespace KalendarzMobile.ViewModels;

/// <summary>
/// ViewModel dla listy zamówień
/// </summary>
public partial class ZamowieniaListViewModel : BaseViewModel
{
    private readonly IZamowieniaService _zamowieniaService;

    [ObservableProperty]
    private ObservableCollection<Zamowienie> _zamowienia = new();

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _selectedStatus;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private DzienneStatystyki? _statystyki;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private bool _hasMore;

    // Dostępne statusy do filtrowania
    public List<string> StatusyDoWyboru { get; } = new()
    {
        "Wszystkie",
        "Nowe",
        "Oczekuje",
        "W realizacji",
        "Zrealizowane",
        "Anulowane"
    };

    public ZamowieniaListViewModel(IZamowieniaService zamowieniaService)
    {
        _zamowieniaService = zamowieniaService;
        Title = "Zamówienia Klientów";
    }

    [RelayCommand]
    private async Task LoadZamowieniaAsync()
    {
        await ExecuteAsync(async () =>
        {
            var filter = new ZamowieniaFilter
            {
                DataOd = SelectedDate.Date,
                DataDo = SelectedDate.Date.AddDays(1).AddSeconds(-1),
                Limit = 50
            };

            // Filtr statusu
            if (!string.IsNullOrEmpty(SelectedStatus) && SelectedStatus != "Wszystkie")
            {
                filter.Status = SelectedStatus;
            }

            // Wyszukiwanie
            if (!string.IsNullOrEmpty(SearchText))
            {
                filter.Szukaj = SearchText;
            }

            var response = await _zamowieniaService.GetZamowieniaAsync(filter);

            Zamowienia.Clear();
            foreach (var z in response.Zamowienia)
            {
                Zamowienia.Add(z);
            }

            TotalCount = response.Total;
            HasMore = response.HasMore;

            // Pobierz statystyki
            Statystyki = await _zamowieniaService.GetStatystykiDniaAsync(SelectedDate);

        }, "Nie udało się pobrać zamówień");
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadZamowieniaAsync();
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task GoToDetailAsync(Zamowienie zamowienie)
    {
        if (zamowienie == null)
            return;

        await Shell.Current.GoToAsync(nameof(ZamowienieDetailPage), true, new Dictionary<string, object>
        {
            { "Zamowienie", zamowienie }
        });
    }

    [RelayCommand]
    private async Task GoToPreviousDayAsync()
    {
        SelectedDate = SelectedDate.AddDays(-1);
        await LoadZamowieniaAsync();
    }

    [RelayCommand]
    private async Task GoToNextDayAsync()
    {
        SelectedDate = SelectedDate.AddDays(1);
        await LoadZamowieniaAsync();
    }

    [RelayCommand]
    private async Task GoToTodayAsync()
    {
        SelectedDate = DateTime.Today;
        await LoadZamowieniaAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await LoadZamowieniaAsync();
    }

    [RelayCommand]
    private async Task FilterByStatusAsync(string status)
    {
        SelectedStatus = status;
        await LoadZamowieniaAsync();
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!HasMore || IsBusy)
            return;

        await ExecuteAsync(async () =>
        {
            var filter = new ZamowieniaFilter
            {
                DataOd = SelectedDate.Date,
                DataDo = SelectedDate.Date.AddDays(1).AddSeconds(-1),
                Offset = Zamowienia.Count,
                Limit = 50
            };

            if (!string.IsNullOrEmpty(SelectedStatus) && SelectedStatus != "Wszystkie")
            {
                filter.Status = SelectedStatus;
            }

            if (!string.IsNullOrEmpty(SearchText))
            {
                filter.Szukaj = SearchText;
            }

            var response = await _zamowieniaService.GetZamowieniaAsync(filter);

            foreach (var z in response.Zamowienia)
            {
                Zamowienia.Add(z);
            }

            HasMore = response.HasMore;

        }, "Nie udało się pobrać więcej zamówień");
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        // Automatyczne przeładowanie przy zmianie daty
        MainThread.BeginInvokeOnMainThread(async () => await LoadZamowieniaAsync());
    }

    partial void OnSearchTextChanged(string value)
    {
        // Debounce - odczekaj 500ms po ostatnim znaku
        // W produkcji użyć CancellationToken
    }
}
