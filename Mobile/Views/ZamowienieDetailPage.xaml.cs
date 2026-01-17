using KalendarzMobile.ViewModels;

namespace KalendarzMobile.Views;

public partial class ZamowienieDetailPage : ContentPage
{
    public ZamowienieDetailPage(ZamowienieDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
