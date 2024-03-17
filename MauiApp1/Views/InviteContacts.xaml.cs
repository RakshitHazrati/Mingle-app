using System.Diagnostics;

namespace MauiApp1.Views;

public partial class InviteContacts : ContentView
{
    public string Name { get; private set; }
    public string phone { get; private set; }
    public static IEnumerable<Contact> contacts { get; private set; }
    public static IEnumerable<ContactDetails> contactDetails { get; private set; }
    //public static IEnumerable<InviteContacts> inviteContacts { get; private set; }
    public InviteContacts()
	{
		InitializeComponent();
        OnStartLoadFriends();

    }


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

        //var t = ShareText("Hi Hello!");
        IAsyncEnumerable<Contact> c = GetContactNames();
        List<Contact> cl = new List<Contact>();
        List<ContactDetails> iC = new List<ContactDetails>();

        await foreach (var contact in c)
        {
            Debug.WriteLine("ccdcd : " + contact.DisplayName);
            cl.Add(contact);
            ContactDetails invite = new ContactDetails(contact.DisplayName, contact.Phones[0].ToString());
            iC.Add(invite);
        }
        contacts = cl;
        contactDetails = iC;        


    }
}