using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;

namespace MauiApp1
{
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
                

            builder.Services.AddSingleton<MongoDbContext>(new MongoDbContext());

            builder.Services.AddTransient<LogIn>();
            builder.Services.AddTransient<SignUpPage>();
            builder.Services.AddTransient<OtpPage>();
            builder.Services.AddTransient<MyDatesPage>();
            builder.Services.AddTransient<PlayCupidMembersPage>();
            builder.Services.AddTransient<PlayCupidInvitePage>();
            builder.Services.AddTransient<ProfilePage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif



            return builder.Build();
        }
    }
}
