using MauiApp1.Views;

namespace MauiApp1;

public partial class PlayCupidInvitePage : ContentPage
{
	public PlayCupidInvitePage()
	{
		InitializeComponent();
        OnStartLoadFriends();

    }

    private async void OnShareClicked(object sender, EventArgs e)
	{
        await ShareText("https://www.google.com/");
	}
    public async Task ShareText(string text)
    {
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Text = text,
            Title = "Mingle"
        });
    }
    private async void OnCopyClicked(object sender, EventArgs e)
    {
         await SetClipboardButton_Clicked(sender, e);
    }
    private async Task SetClipboardButton_Clicked(object sender, EventArgs e) =>
    await Clipboard.Default.SetTextAsync(appLink.Text);



    public async IAsyncEnumerable<Contact> GetContactNames()
    {
        var contacts = await Contacts.Default.GetAllAsync();

        // No contacts
        if (contacts == null)
            yield break;

        foreach (var contact in contacts)

            yield return contact;
    }
    async Task SendSMSAsync(Contact c)
    {
        if (Sms.Default.IsComposeSupported)
        {
            string[] recipients = new[] { c.Phones[0].ToString() };
            string text = "Hello, from Mingle.";

            var message = new SmsMessage(text, recipients);

            await Sms.Default.ComposeAsync(message);
        }
    }
    private  void OnStartLoadFriends()
    {
     
        //var t = ShareText("Hi Hello!");
        /*IAsyncEnumerable<Contact> c = GetContactNames();
        await foreach (Contact item in c)
        {

            string inviteButtonXAML = $"<HorizontalStackLayout Margin=\"0,20,20,0\">                        <Grid HorizontalOptions=\"Center\"  >                           <Grid.ColumnDefinitions >                              <ColumnDefinition Width=\"*\" />\r\n                                 <ColumnDefinition Width=\"200\" />                             <ColumnDefinition Width=\"*\" />                            </Grid.ColumnDefinitions>                             <Image                                 Grid.Column=\"0\"                            Source=\"profile_placeholder.png\"                                 HeightRequest=\"30\"                                 WidthRequest=\"30\"                                    Margin=\"30,0,30,0\"                                 HorizontalOptions=\"Start\" />                            <Label                            Grid.Column=\"1\"                                Text=\"{item.DisplayName}\"                              FontSize=\"15\"                             HorizontalOptions=\"Start\"                            VerticalOptions=\"CenterAndExpand\"/>                   <Button                            Grid.Column=\"2\"                                 Text=\"Invite\"                                     BackgroundColor=\"#8023EB\"                                TextColor=\"White\"                                  CornerRadius=\"7\"                                     Margin=\"0,0,0,0\"                                         WidthRequest=\"80\"                                         HeightRequest=\"40\"                                      />                    </Grid>                    </HorizontalStackLayout>";
            Button inviteButton = new Button().LoadFromXaml(inviteButtonXAML);
            inviteButton.Clicked += async (sender, args) => await SendSMSAsync(item);
            Friends.Add(inviteButton);

        }*/


    }

    private void Button_Clicked(object sender, EventArgs e)
    {

    }
}