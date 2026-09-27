using MauiApp1.Models;
using MauiApp1.Services;

namespace MauiApp1.Pages;

public partial class ChatPage : ContentPage
{
    private readonly Conversation _conversation;
    private readonly AppState _state = AppServices.Get<AppState>();
    private CancellationTokenSource? _polling;

    public ChatPage(Conversation conversation)
    {
        InitializeComponent();
        _conversation = conversation;
        BindingContext = conversation;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_state.IsPreviewMode)
        {
            _polling = new CancellationTokenSource();
            await RefreshMessagesAsync(_polling.Token, showError: true);
            _ = PollMessagesAsync(_polling.Token);
        }
        if (_conversation.Messages.Count > 0)
        {
            MessagesList.ScrollTo(_conversation.Messages[^1], position: ScrollToPosition.End, animate: false);
        }
    }

    protected override void OnDisappearing()
    {
        _polling?.Cancel();
        _polling?.Dispose();
        _polling = null;
        base.OnDisappearing();
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Navigation.PopAsync();

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        var text = MessageEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!_state.IsPreviewMode)
        {
            try
            {
                await AppServices.Get<MingleApiClient>().SendMessageAsync(_conversation.Id, text);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                await DisplayAlert("Message not sent", ex.Message, "OK");
                return;
            }
        }
        var message = new ChatMessage { Text = text, IsMine = true, SentAt = DateTimeOffset.Now };
        _conversation.Messages.Add(message);
        MessageEntry.Text = "";
        MessagesList.ScrollTo(message, position: ScrollToPosition.End, animate: true);
    }

    private async Task PollMessagesAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(4));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await RefreshMessagesAsync(cancellationToken, showError: false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RefreshMessagesAsync(CancellationToken cancellationToken, bool showError)
    {
        try
        {
            var messages = await AppServices.Get<MingleApiClient>().GetMessagesAsync(_conversation.Id, cancellationToken);
            if (messages.Count == _conversation.Messages.Count) return;
            _conversation.Messages.Clear();
            foreach (var message in messages)
            {
                _conversation.Messages.Add(new ChatMessage
                {
                    Text = message.Text,
                    IsMine = message.SenderUserId == _state.CurrentUserId,
                    SentAt = message.SentAt
                });
            }
            if (_conversation.Messages.Count > 0)
                MessagesList.ScrollTo(_conversation.Messages[^1], position: ScrollToPosition.End, animate: false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (showError && ex is not TaskCanceledException)
                await DisplayAlert("Chat is waiting", "Chat opens after both people accept the introduction.", "OK");
        }
    }

    private async void OnPlanClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new PlanDatePage(_conversation.Person));
    }
}
