using MauiApp1.Services;

namespace MauiApp1.Pages;

public partial class WelcomePage : ContentPage
{
    public WelcomePage()
    {
        InitializeComponent();
    }

    private async void OnGetStartedClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new AuthPage());
    }

    private void OnPreviewClicked(object sender, EventArgs e)
    {
        AppServices.Get<AppState>().IsPreviewMode = true;
        ((App)Application.Current!).OpenDashboard();
    }
}
