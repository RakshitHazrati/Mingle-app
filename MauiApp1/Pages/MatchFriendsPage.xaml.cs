using MauiApp1.Models;
using MauiApp1.Services;

namespace MauiApp1.Pages;

public partial class MatchFriendsPage : ContentPage
{
    private readonly AppState _state = AppServices.Get<AppState>();
    private List<PersonCard> _joinedFriends = [];

    public MatchFriendsPage()
    {
        InitializeComponent();
        RefreshBindings();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_state.IsPreviewMode)
        {
            try
            {
                _state.ReplaceContacts(await AppServices.Get<MingleApiClient>().GetContactsAsync());
                RefreshBindings();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                await DisplayAlert("Couldn’t refresh your circle", ex.Message, "OK");
            }
        }
        PointsLabel.Text = $"✦ {_state.Points} points";
    }

    private async void OnMatchClicked(object sender, EventArgs e)
    {
        if (FirstFriendPicker.SelectedIndex < 0 || SecondFriendPicker.SelectedIndex < 0)
        {
            await DisplayAlert("Pick two friends", "Choose one person in each field to create an introduction.", "OK");
            return;
        }

        var first = _joinedFriends[FirstFriendPicker.SelectedIndex];
        var second = _joinedFriends[SecondFriendPicker.SelectedIndex];

        if (first.Id == second.Id)
        {
            await DisplayAlert("Choose two people", "A great introduction needs two different friends.", "OK");
            return;
        }

        if (_state.IsPreviewMode)
        {
            _state.ProposeMatch(first, second);
        }
        else
        {
            try
            {
                await AppServices.Get<MingleApiClient>().ProposeMatchAsync(first.Id, second.Id);
                _state.ProposeMatch(first, second);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                await DisplayAlert("Introduction not sent", ex.Message, "Try again");
                return;
            }
        }
        FirstFriendPicker.SelectedItem = null;
        SecondFriendPicker.SelectedItem = null;
        await DisplayAlert("Introduction sent ✨",
            $"{first.Name} and {second.Name} will each receive an in-app invite. SMS and WhatsApp delivery are represented by backend provider placeholders in this build.",
            "Done");
    }

    private void RefreshBindings()
    {
        _joinedFriends = _state.Friends.Where(x => x.HasJoined).ToList();
        var names = _joinedFriends.Select(x => x.Name).ToList();
        FirstFriendPicker.ItemsSource = names;
        SecondFriendPicker.ItemsSource = names;
        FriendsList.ItemsSource = null;
        FriendsList.ItemsSource = _state.Friends;
    }

    private async void OnInviteClicked(object sender, EventArgs e)
    {
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = "Join my Mingle circle",
            Text = $"I trust your taste. Join my Mingle circle with {_state.ReferralCode}: https://mingle.example/join/{_state.ReferralCode}"
        });
    }
}
