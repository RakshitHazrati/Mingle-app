using MauiApp1.Services;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace MauiApp1;

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

        builder.Services.AddSingleton<AppState>();
        var apiBaseUrl = typeof(MauiProgram).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "MingleApiBaseUrl")
            .Value ?? throw new InvalidOperationException("The Mingle API URL is not configured.");

        builder.Services.AddSingleton(new HttpClient
        {
            BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(12)
        });
        builder.Services.AddSingleton<MingleApiClient>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        AppServices.Provider = app.Services;
        return app;
    }
}
