using System.Net.Http;
using MauiApp1.Services;

namespace MauiApp1.Pages;

public partial class AuthPage : ContentPage
{
    private bool _isSignUp = true;

    public AuthPage()
    {
        InitializeComponent();
    }

    private void OnSignUpMode(object sender, EventArgs e) => SetMode(true);
    private void OnSignInMode(object sender, EventArgs e) => SetMode(false);

    private void SetMode(bool signUp)
    {
        _isSignUp = signUp;
        SignUpOnlyFields.IsVisible = signUp;
        PhoneField.IsVisible = signUp;
        Heading.Text = signUp ? "Create your account" : "Welcome back";
        Subheading.Text = signUp ? "Real people, trusted introductions, and no endless swiping." : "Your people and conversations are waiting.";
        ContinueButton.Text = signUp ? "Create account" : "Sign in";
        var secondary = (Style)Application.Current!.Resources["SecondaryButton"];
        SignUpTab.Style = signUp ? null : secondary;
        SignInTab.Style = signUp ? secondary : null;
        ErrorLabel.IsVisible = false;
    }

    private async void OnContinueClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        var email = EmailEntry.Text?.Trim() ?? "";
        var password = PasswordEntry.Text ?? "";

        if (!email.Contains('@') || password.Length < 10 || (_isSignUp && string.IsNullOrWhiteSpace(NameEntry.Text)))
        {
            ShowError("Enter a valid email and a password of at least 10 characters.");
            return;
        }

        ContinueButton.IsEnabled = false;
        ContinueButton.Text = "Connecting…";
        try
        {
            var api = AppServices.Get<MingleApiClient>();
            ApiResult result = _isSignUp
                ? await api.RegisterAsync(NameEntry.Text!.Trim(), email, PhoneEntry.Text?.Trim() ?? "", password)
                : await api.LoginAsync(email, password);

            if (!result.Success)
            {
                ShowError(result.Message);
                return;
            }

            var state = AppServices.Get<AppState>();
            var user = result.User ?? await api.GetMeAsync();
            if (user is null)
            {
                ShowError("Your account was created, but the profile could not be loaded.");
                return;
            }
            state.ApplyCurrentUser(user, resetCollections: true);
            if (!user.ProfileCompleted)
            {
                await Navigation.PushAsync(new ProfileSetupPage(isOnboarding: true));
            }
            else if (!user.ContactsOnboarded)
            {
                await Navigation.PushAsync(new ContactsOnboardingPage());
            }
            else
            {
                ((App)Application.Current!).OpenDashboard();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ShowError("The local API is offline. Start Mingle.Api, or return and open the product preview.");
        }
        finally
        {
            ContinueButton.IsEnabled = true;
            ContinueButton.Text = _isSignUp ? "Create account" : "Sign in";
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
