namespace MauiApp1;

public partial class PlayCupidMembersPage : ContentPage
{
	public PlayCupidMembersPage()
	{
		InitializeComponent();
	}
    private async void OnClickProfile(object sender, EventArgs e)
    {
        var profilePage = Handler.MauiContext.Services.GetService<ProfilePage>();
        await Navigation.PushAsync(profilePage);
    }
}