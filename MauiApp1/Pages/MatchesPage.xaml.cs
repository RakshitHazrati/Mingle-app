using MauiApp1.Models;
using MauiApp1.Services;

namespace MauiApp1.Pages;

public partial class MatchesPage : ContentPage
{
    private readonly AppState _state = AppServices.Get<AppState>();
    private static readonly MatchCard EmptyMatch = new()
    {
        Person = new PersonCard { Name = "New match", Image = "profile_placeholder.png" },
        MatchedBy = "A friend"
    };
    public MatchCard PrimaryMatch => _state.Matches.FirstOrDefault() ?? EmptyMatch;

    public MatchesPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_state.IsPreviewMode)
        {
            try
            {
                var api = AppServices.Get<MingleApiClient>();
                var matches = await api.GetMatchesAsync();
                var photos = new Dictionary<string, string?>();
                foreach (var match in matches.Where(x => x.Person.HasProfilePhoto))
                    photos[match.Person.Id] = await api.GetProfilePhotoPathAsync(match.Person.Id);
                _state.ReplaceMatches(matches, photos);
                BindingContext = null;
                BindingContext = this;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                await DisplayAlert("Couldn’t refresh matches", ex.Message, "OK");
            }
        }
        PointsLabel.Text = $"{_state.Points} pts";
        MatchStatusLabel.Text = PrimaryMatch.StatusLabel;
        MatchCard.IsVisible = _state.Matches.Count > 0;
        EmptyMatches.IsVisible = _state.Matches.Count == 0;
    }

    private async void OnViewMatchClicked(object sender, EventArgs e)
    {
        if (_state.Matches.Count == 0) return;
        await Navigation.PushAsync(new MatchDetailPage(PrimaryMatch));
    }

    private async void OnPassClicked(object sender, EventArgs e)
    {
        if (_state.Matches.Count == 0) return;
        var pass = await DisplayAlert("Pass for now?", "This introduction will stay available for 24 hours.", "Pass", "Keep it");
        if (pass)
        {
            PrimaryMatch.Status = MatchStatus.Declined;
            MatchStatusLabel.Text = PrimaryMatch.StatusLabel;
        }
    }
}
