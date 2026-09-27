using MauiApp1.Pages;
using MauiApp1.Services;

namespace MauiApp1;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Light;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new NavigationPage(new WelcomePage())
        {
            BarBackgroundColor = Colors.Transparent,
            BarTextColor = Color.FromArgb("#201A2B")
        });
    }

    public void OpenDashboard()
    {
        SetRootPage(new AppShell());
    }

    public void RestartOnboarding()
    {
        Preferences.Default.Remove("mingle_onboarding_complete");
        SecureStorage.Default.Remove("mingle_access_token");
        SetRootPage(new NavigationPage(new WelcomePage()));
    }

    private void SetRootPage(Page page)
    {
        if (Windows.Count == 0)
        {
            return;
        }

        Windows[0].Page = page;
    }
}
