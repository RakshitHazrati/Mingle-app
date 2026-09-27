using System.Collections.ObjectModel;

namespace MauiApp1.Models;

public enum MatchStatus
{
    Pending,
    Accepted,
    Declined,
    Successful
}

public sealed class PersonCard
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "";
    public int Age { get; init; }
    public string City { get; init; } = "";
    public string Occupation { get; init; } = "";
    public string Bio { get; init; } = "";
    public string Image { get; init; } = "profile_placeholder.png";
    public string Initials { get; init; } = "";
    public string Accent { get; init; } = "#6D3BFF";
    public bool HasJoined { get; set; }
    public string DisplayName => Age > 0 ? $"{Name}, {Age}" : Name;
    public string MembershipStatus => HasJoined ? "On Mingle" : "Invite pending";
    public string Subtitle => string.Join(" · ", new[] { Occupation, City }.Where(x => !string.IsNullOrWhiteSpace(x)));
}

public sealed class MatchCard
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public PersonCard Person { get; init; } = new();
    public string MatchedBy { get; init; } = "";
    public string MatchedByInitials { get; init; } = "";
    public MatchStatus Status { get; set; }
    public string Reason { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string StatusLabel => Status switch
    {
        MatchStatus.Accepted => "You both said yes",
        MatchStatus.Successful => "Match successful",
        MatchStatus.Declined => "Passed",
        _ => "Waiting for you"
    };
}

public sealed class Conversation
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public PersonCard Person { get; init; } = new();
    public ObservableCollection<ChatMessage> Messages { get; } = [];
    public string LastMessage => Messages.LastOrDefault()?.Text ?? "Start the conversation";
    public string LastMessageTime => Messages.LastOrDefault()?.SentAt.ToLocalTime().ToString("h:mm tt") ?? "";
}

public sealed class ChatMessage
{
    public string Text { get; init; } = "";
    public bool IsMine { get; init; }
    public DateTimeOffset SentAt { get; init; } = DateTimeOffset.UtcNow;
    public string BubbleColor => IsMine ? "#6D3BFF" : "#FFFFFF";
    public string TextColor => IsMine ? "#FFFFFF" : "#201A2B";
    public LayoutOptions Alignment => IsMine ? LayoutOptions.End : LayoutOptions.Start;
}

public sealed class DatePlan
{
    public DateTime Date { get; init; }
    public TimeSpan Time { get; init; }
    public string Venue { get; init; } = "";
    public string Note { get; init; } = "";
}

public sealed class DeviceContactCard
{
    public string DisplayName { get; init; } = "";
    public string PhoneNumber { get; init; } = "";
    public string Initials { get; init; } = "";
    public string MaskedPhone => PhoneNumber.Length <= 4 ? PhoneNumber : $"•••• {new string(PhoneNumber.Where(char.IsDigit).TakeLast(4).ToArray())}";
}
