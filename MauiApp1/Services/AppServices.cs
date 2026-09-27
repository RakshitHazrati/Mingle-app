using Microsoft.Extensions.DependencyInjection;

namespace MauiApp1.Services;

public static class AppServices
{
    public static IServiceProvider Provider { get; set; } = default!;

    public static T Get<T>() where T : notnull =>
        Provider.GetRequiredService<T>();
}
