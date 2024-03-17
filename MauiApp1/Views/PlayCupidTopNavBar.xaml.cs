namespace MauiApp1.Views;

public partial class PlayCupidTopNavBar : ContentView
{
	public PlayCupidTopNavBar()
	{
		InitializeComponent();
	}
    private async void OnMemberClicked(object sender, EventArgs e)
    {
        var playCupidmembers = Handler.MauiContext.Services.GetService<PlayCupidMembersPage>();
        await Navigation.PushAsync(playCupidmembers);
    }
    private async void OnInviteClicked(object sender, EventArgs e)
    {
        var playCupidinvite = Handler.MauiContext.Services.GetService<PlayCupidInvitePage>();
        await Navigation.PushAsync(playCupidinvite);
    }
   

}