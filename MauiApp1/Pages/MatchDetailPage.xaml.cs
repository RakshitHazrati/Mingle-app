using MauiApp1.Models;
using MauiApp1.Services;

namespace MauiApp1.Pages;

public partial class MatchDetailPage : ContentPage
{
    private readonly MatchCard _match;

    public MatchDetailPage(MatchCard match)
    {
        InitializeComponent();
        _match = match;
        BindingContext = match;
        UpdateState();
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Navigation.PopAsync();

    private async void OnAcceptClicked(object sender, EventArgs e)
    {
        if (!AppServices.Get<AppState>().IsPreviewMode)
        {
            try
            {
                await AppServices.Get<MingleApiClient>().RespondToMatchAsync(_match.Id, accept: true);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                await DisplayAlert("Couldn’t accept", ex.Message, "Try again");
                return;
            }
        }
        _match.Status = MatchStatus.Accepted;
        AppServices.Get<AppState>().Activity.Insert(0, $"You accepted the introduction to {_match.Person.Name}.");
        UpdateState();
        await DisplayAlert("It’s a Mingle ✨", $"You can now message {_match.Person.Name} and plan something easy.", "Say hello");
    }

    private async void OnDeclineClicked(object sender, EventArgs e)
    {
        if (await DisplayAlert("Pass on this match?", "We’ll let your friend know privately—no awkward details.", "Pass", "Cancel"))
        {
            if (!AppServices.Get<AppState>().IsPreviewMode)
            {
                try
                {
                    await AppServices.Get<MingleApiClient>().RespondToMatchAsync(_match.Id, accept: false);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    await DisplayAlert("Couldn’t update the match", ex.Message, "Try again");
                    return;
                }
            }
            _match.Status = MatchStatus.Declined;
            await Navigation.PopAsync();
        }
    }

    private async void OnMessageClicked(object sender, EventArgs e)
    {
        var conversation = AppServices.Get<AppState>().GetConversation(_match.Person, _match.Id);
        await Navigation.PushAsync(new ChatPage(conversation));
    }

    private async void OnPlanDateClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new PlanDatePage(_match.Person));
    }

    private void UpdateState()
    {
        var connected = _match.Status is MatchStatus.Accepted or MatchStatus.Successful;
        DecisionButtons.IsVisible = !connected;
        ConnectedButtons.IsVisible = connected;
        ResponseLabel.Text = connected ? "You’re connected" : "Would you like to connect?";
    }
}
