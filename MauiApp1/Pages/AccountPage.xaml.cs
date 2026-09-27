using MauiApp1.Services;

namespace MauiApp1.Pages;

public partial class AccountPage : ContentPage
{
    private readonly AppState _state = AppServices.Get<AppState>();

    public AccountPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        NameLabel.Text = _state.CurrentUserName;
        InitialsLabel.Text = string.Join("", _state.CurrentUserName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(x => char.ToUpperInvariant(x[0])));
        PointsLabel.Text = _state.Points.ToString();
        ReferralLabel.Text = _state.ReferralCode;
        FriendsCountLabel.Text = _state.Friends.Count.ToString();
        IntroducedCountLabel.Text = _state.Activity.Count.ToString();
        ProfileSummaryLabel.Text = _state.ProfileCompleted
            ? $"{_state.Occupation} · {_state.City}"
            : "Complete your profile";
        ProfileProgress.Progress = _state.ProfileCompleted ? 1 : 0.25;
        if (_state.HasProfilePhoto)
        {
            var path = _state.ProfileImagePath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                try
                {
                    path = await AppServices.Get<MingleApiClient>().GetProfilePhotoPathAsync(_state.CurrentUserId);
                    if (!string.IsNullOrWhiteSpace(path)) _state.SetProfilePhotoPath(path);
                }
                catch (HttpRequestException)
                {
                }
            }
            if (!string.IsNullOrWhiteSpace(path))
            {
                AccountProfileImage.Source = ImageSource.FromFile(path);
                AccountProfileImage.IsVisible = true;
                InitialsLabel.IsVisible = false;
            }
        }
    }

    private async void OnEditProfileClicked(object sender, EventArgs e) =>
        await Navigation.PushAsync(new ProfileSetupPage(isOnboarding: false));

    private async void OnCopyClicked(object sender, EventArgs e)
    {
        await Clipboard.Default.SetTextAsync(_state.ReferralCode);
        await DisplayAlert("Copied", "Your Mingle circle code is ready to share.", "OK");
    }

    private async void OnShareClicked(object sender, EventArgs e)
    {
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = "Join my Mingle circle",
            Text = $"Join my trusted circle on Mingle using {_state.ReferralCode}: https://mingle.example/join/{_state.ReferralCode}"
        });
    }

    private async void OnRestartClicked(object sender, EventArgs e)
    {
        if (await DisplayAlert("Restart onboarding?", "This clears the local preview session only.", "Restart", "Cancel"))
        {
            ((App)Application.Current!).RestartOnboarding();
        }
    }
}
