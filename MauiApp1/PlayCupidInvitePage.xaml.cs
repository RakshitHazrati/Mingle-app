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
    private async void OnStartLoadFriends()
    {
        StackLayout stackLayout = new StackLayout
        {

        };

        //var t = ShareText("Hi Hello!");
        IAsyncEnumerable<Contact> c = GetContactNames();
        await foreach (Contact item in c)
        {

            string inviteButtonXAML = $"<Button Text=\"{item.DisplayName}\" Margin=\"5\" />";
            Button inviteButton = new Button().LoadFromXaml(inviteButtonXAML);
            inviteButton.Clicked += async (sender, args) => await SendSMSAsync(item);
            //Friends.Add(inviteButton);

        }
        

    }
}