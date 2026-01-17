using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KalendarzMobile.Models;
using KalendarzMobile.Services;

namespace KalendarzMobile.ViewModels;

/// <summary>
/// ViewModel dla szczegółów zamówienia
/// </summary>
[QueryProperty(nameof(Zamowienie), "Zamowienie")]
public partial class ZamowienieDetailViewModel : BaseViewModel
{
    private readonly IZamowieniaService _zamowieniaService;

    [ObservableProperty]
    private Zamowienie? _zamowienie;

    [ObservableProperty]
    private ObservableCollection<ZamowienieTowa> _towary = new();

    [ObservableProperty]
    private decimal _sumaKg;

    [ObservableProperty]
    private decimal _sumaWartosci;

    [ObservableProperty]
    private int _sumaPojemnikow;

    [ObservableProperty]
    private decimal _sumaPalet;

    public ZamowienieDetailViewModel(IZamowieniaService zamowieniaService)
    {
        _zamowieniaService = zamowieniaService;
        Title = "Szczegóły Zamówienia";
    }

    partial void OnZamowienieChanged(Zamowienie? value)
    {
        if (value != null)
        {
            Title = $"Zamówienie #{value.Id}";
            LoadTowary();
            CalculateSummary();
        }
    }

    private void LoadTowary()
    {
        Towary.Clear();
        if (Zamowienie?.Towary != null)
        {
            foreach (var towar in Zamowienie.Towary)
            {
                Towary.Add(towar);
            }
        }
    }

    private void CalculateSummary()
    {
        if (Zamowienie == null)
            return;

        SumaKg = Zamowienie.SumaKg;
        SumaWartosci = Zamowienie.SumaWartosci;
        SumaPojemnikow = Zamowienie.LiczbaPojemnikow;
        SumaPalet = Zamowienie.LiczbaPalet;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (Zamowienie == null)
            return;

        await ExecuteAsync(async () =>
        {
            var refreshed = await _zamowieniaService.GetZamowienieAsync(Zamowienie.Id);
            if (refreshed != null)
            {
                Zamowienie = refreshed;
            }
        }, "Nie udało się odświeżyć zamówienia");
    }

    [RelayCommand]
    private async Task CallClientAsync()
    {
        // W przyszłości - pobierz numer telefonu klienta i zadzwoń
        await Shell.Current.DisplayAlert(
            "Telefon do klienta",
            $"Dzwonienie do: {Zamowienie?.KlientNazwa}",
            "OK");
    }

    [RelayCommand]
    private async Task ShareAsync()
    {
        if (Zamowienie == null)
            return;

        var text = $"Zamówienie #{Zamowienie.Id}\n" +
                   $"Klient: {Zamowienie.KlientNazwa}\n" +
                   $"Data przyjazdu: {Zamowienie.DataPrzyjazduFormatted}\n" +
                   $"Podsumowanie: {Zamowienie.PodsumowanieKrotkie}\n" +
                   $"Wartość: {SumaWartosci:N2} {Zamowienie.Waluta}";

        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Text = text,
            Title = $"Zamówienie #{Zamowienie.Id}"
        });
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
