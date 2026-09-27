using MauiApp1.Models;
using MauiApp1.Services;

namespace MauiApp1.Pages;

public partial class InboxPage : ContentPage
{
    public InboxPage()
    {
        InitializeComponent();
        ConversationList.ItemsSource = AppServices.Get<AppState>().Conversations;
    }

    private async void OnConversationSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Conversation conversation)
        {
            return;
        }

        ConversationList.SelectedItem = null;
        await Navigation.PushAsync(new ChatPage(conversation));
    }
}
