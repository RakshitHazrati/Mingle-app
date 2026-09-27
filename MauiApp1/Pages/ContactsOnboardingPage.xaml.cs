using MauiApp1.Models;
using MauiApp1.Services;

namespace MauiApp1.Pages;

public partial class ContactsOnboardingPage : ContentPage
{
    private readonly AppState _state = AppServices.Get<AppState>();
    private readonly List<DeviceContactCard> _deviceContacts = [];

    public ContactsOnboardingPage()
    {
        InitializeComponent();
    }

    private async void OnLoadContactsClicked(object sender, EventArgs e)
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.ContactsRead>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.ContactsRead>();
            }
            if (status != PermissionStatus.Granted)
            {
                await DisplayAlert("Contacts permission", "Mingle needs contact access to show the device picker. You can continue without it or share your circle code.", "OK");
                return;
            }

            LoadContactsButton.IsEnabled = false;
            LoadContactsButton.Text = "Reading contacts...";
            var contacts = await Contacts.Default.GetAllAsync();
            _deviceContacts.Clear();
            _deviceContacts.AddRange((contacts ?? [])
                .SelectMany(contact => contact.Phones
                    .Where(phone => !string.IsNullOrWhiteSpace(phone.PhoneNumber))
                    .Select(phone => new DeviceContactCard
                    {
                        DisplayName = string.IsNullOrWhiteSpace(contact.DisplayName) ? "Unnamed contact" : contact.DisplayName,
                        PhoneNumber = phone.PhoneNumber,
                        Initials = GetInitials(contact.DisplayName)
                    }))
                .GroupBy(x => new string(x.PhoneNumber.Where(char.IsDigit).ToArray()))
                .Where(x => x.Key.Length >= 8)
                .Select(x => x.First())
                .OrderBy(x => x.DisplayName)
                .Take(1000));

            ContactsList.ItemsSource = _deviceContacts;
            EmptyState.IsVisible = _deviceContacts.Count == 0;
            ContactsList.IsVisible = _deviceContacts.Count > 0;
            if (_deviceContacts.Count == 0)
            {
                await DisplayAlert("No contacts found", "Add contacts to this device, then try again.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Contacts unavailable", ex.Message, "OK");
        }
        finally
        {
            LoadContactsButton.IsEnabled = true;
            LoadContactsButton.Text = "Refresh contacts";
        }
    }

    private async void OnShareClicked(object sender, EventArgs e)
    {
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = "Join my Mingle circle",
            Text = $"Join my trusted circle on Mingle with code {_state.ReferralCode}: https://mingle.example/join/{_state.ReferralCode}"
        });
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var count = e.CurrentSelection.Count;
        SelectionLabel.Text = count == 0 ? "No contacts selected" : $"{count} contact{(count == 1 ? "" : "s")} selected";
        ContinueButton.IsEnabled = count > 0;
    }

    private async void OnContinueClicked(object sender, EventArgs e)
    {
        var selected = ContactsList.SelectedItems.Cast<DeviceContactCard>()
            .Select(x => new DeviceContactSelection(x.DisplayName, x.PhoneNumber)).ToList();
        if (selected.Count == 0) return;

        ContinueButton.IsEnabled = false;
        ContinueButton.Text = "Finding friends on Mingle...";
        try
        {
            var result = await AppServices.Get<MingleApiClient>().SyncSelectedContactsAsync(selected);
            _state.ReplaceContacts(result.Contacts);
            Preferences.Default.Set("mingle_onboarding_complete", true);
            var onMingle = result.Contacts.Count(x => x.OnMingle);
            await DisplayAlert("Circle created", $"Saved {result.Synced} selected contacts. {onMingle} {(onMingle == 1 ? "is" : "are")} already on Mingle.", "Continue");
            ((App)Application.Current!).OpenDashboard();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            await DisplayAlert("Couldn’t sync contacts", ex.Message, "Try again");
        }
        finally
        {
            ContinueButton.IsEnabled = true;
            ContinueButton.Text = "Add selected contacts";
        }
    }

    private void OnSkipClicked(object sender, EventArgs e) => ((App)Application.Current!).OpenDashboard();

    private static string GetInitials(string? name)
    {
        var initials = string.Join("", (name ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => char.ToUpperInvariant(x[0])));
        return string.IsNullOrWhiteSpace(initials) ? "?" : initials;
    }
}
