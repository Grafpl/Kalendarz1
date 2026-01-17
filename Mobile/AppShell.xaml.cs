using KalendarzMobile.Views;

namespace KalendarzMobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Rejestracja tras nawigacji
        Routing.RegisterRoute(nameof(ZamowienieDetailPage), typeof(ZamowienieDetailPage));
    }
}
