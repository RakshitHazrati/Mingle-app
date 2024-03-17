namespace MauiApp1.Views;

public partial class HomeBottomNavBar : ContentView
{
	public HomeBottomNavBar()
	{
		InitializeComponent();
	}
    private async void OnClickMyDatez(object sender, EventArgs e)
    {
        var myDatez = Handler.MauiContext.Services.GetService<MyDatesPage>();
        await Navigation.PushAsync(myDatez);
    }
    private async void OnClickPlayCupid(object sender, EventArgs e)
    {
        var playCupidmembers = Handler.MauiContext.Services.GetService<PlayCupidMembersPage>();
        await Navigation.PushAsync(playCupidmembers);
    }
    private async void OnClickInbox(object sender, EventArgs e)
    {
        var playCupidmembers = Handler.MauiContext.Services.GetService<PlayCupidMembersPage>();
        await Navigation.PushAsync(playCupidmembers);
    }
}