using Microsoft.Extensions.Logging;
using KalendarzMobile.Services;
using KalendarzMobile.ViewModels;
using KalendarzMobile.Views;

namespace KalendarzMobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Services
        builder.Services.AddSingleton<IZamowieniaService, ZamowieniaService>();
        builder.Services.AddSingleton<ISettingsService, SettingsService>();

        // ViewModels
        builder.Services.AddTransient<ZamowieniaListViewModel>();
        builder.Services.AddTransient<ZamowienieDetailViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Views
        builder.Services.AddTransient<ZamowieniaListPage>();
        builder.Services.AddTransient<ZamowienieDetailPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
