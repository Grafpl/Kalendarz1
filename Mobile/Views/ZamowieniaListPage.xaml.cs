using KalendarzMobile.ViewModels;

namespace KalendarzMobile.Views;

public partial class ZamowieniaListPage : ContentPage
{
    private readonly ZamowieniaListViewModel _viewModel;

    public ZamowieniaListPage(ZamowieniaListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Załaduj dane przy pierwszym wyświetleniu
        if (_viewModel.Zamowienia.Count == 0)
        {
            await _viewModel.LoadZamowieniaCommand.ExecuteAsync(null);
        }
    }
}

/// <summary>
/// Behavior do obsługi zdarzeń jako commands
/// </summary>
public class EventToCommandBehavior : Behavior<Picker>
{
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(EventToCommandBehavior));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public string? EventName { get; set; }

    protected override void OnAttachedTo(Picker picker)
    {
        base.OnAttachedTo(picker);
        picker.SelectedIndexChanged += OnSelectedIndexChanged;
    }

    protected override void OnDetachingFrom(Picker picker)
    {
        picker.SelectedIndexChanged -= OnSelectedIndexChanged;
        base.OnDetachingFrom(picker);
    }

    private void OnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (Command?.CanExecute(null) == true)
        {
            Command.Execute(null);
        }
    }
}
