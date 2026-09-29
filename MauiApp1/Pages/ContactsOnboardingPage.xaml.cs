using MauiApp1.Models;
using MauiApp1.Services;

namespace MauiApp1.Pages;

public partial class ContactsOnboardingPage : ContentPage
{
    private readonly AppState _state = AppServices.Get<AppState>();
    private readonly DeviceContactsReader _contactsReader = AppServices.Get<DeviceContactsReader>();
    private readonly List<DeviceContactCard> _deviceContacts = [];
    private CancellationTokenSource? _loadCancellation;
    private CancellationTokenSource? _searchCancellation;
    private bool _isLoading;

    public ContactsOnboardingPage()
    {
        InitializeComponent();
    }

    private async void OnLoadContactsClicked(object sender, EventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

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

            _isLoading = true;
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = new CancellationTokenSource();
            var cancellationToken = _loadCancellation.Token;

            LoadContactsButton.IsEnabled = false;
            LoadContactsButton.Text = "Reading contacts…";
            LoadingOverlay.IsVisible = true;
            EmptyState.IsVisible = false;

            var cards = await _contactsReader.ReadAsync(cancellationToken);

            _deviceContacts.Clear();
            _deviceContacts.AddRange(cards);

            ContactsList.ItemsSource = _deviceContacts;
            EmptyState.IsVisible = _deviceContacts.Count == 0;
            ContactsList.IsVisible = _deviceContacts.Count > 0;
            ContactSearch.IsVisible = _deviceContacts.Count > 0;
            ContactSummaryLabel.Text = _deviceContacts.Count == 0
                ? "No contacts with a usable phone number were found."
                : $"{_deviceContacts.Count:N0} contacts ready · choose only people you trust.";
            if (_deviceContacts.Count == 0)
            {
                await DisplayAlert("No contacts found", "Add contacts to this device, then try again.", "OK");
            }
        }
        catch (OperationCanceledException)
        {
            // Leaving the page while a large address book is loading is expected.
        }
        catch (Exception ex)
        {
            await DisplayAlert("Contacts unavailable", ex.Message, "OK");
        }
        finally
        {
            _isLoading = false;
            LoadingOverlay.IsVisible = false;
            LoadContactsButton.IsEnabled = true;
            LoadContactsButton.Text = "Refresh contacts";
        }
    }

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();
        var cancellationToken = _searchCancellation.Token;

        try
        {
            await Task.Delay(180, cancellationToken);
            var query = e.NewTextValue?.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                ContactsList.ItemsSource = _deviceContacts;
                return;
            }

            var filtered = await Task.Run(() => _deviceContacts
                .Where(contact => contact.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                    || contact.PhoneNumber.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList(), cancellationToken);
            ContactsList.ItemsSource = filtered;
        }
        catch (OperationCanceledException)
        {
            // A newer search replaced this one.
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

    protected override void OnDisappearing()
    {
        _loadCancellation?.Cancel();
        _searchCancellation?.Cancel();
        base.OnDisappearing();
    }

}
