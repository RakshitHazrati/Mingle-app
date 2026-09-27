namespace Mingle.Api.Domain;

public sealed class UserDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PhoneHash { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string ReferralCode { get; set; } = "";
    public int Points { get; set; }
    public bool ContactsOnboarded { get; set; }
    public DateTimeOffset? BirthDate { get; set; }
    public string City { get; set; } = "";
    public string Occupation { get; set; } = "";
    public string Bio { get; set; } = "";
    public bool ProfileCompleted { get; set; }
    public bool HasProfilePhoto { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SessionDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ContactBookDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = "";
    public List<string> PhoneHashes { get; set; } = [];
    public List<SavedContactDocument> Contacts { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SavedContactDocument
{
    public string DisplayName { get; set; } = "";
    public string PhoneHash { get; set; } = "";
}

public enum MatchState { Pending, Accepted, Declined, Successful, Cancelled }

public sealed class MatchDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string MatchmakerUserId { get; set; } = "";
    public string FirstUserId { get; set; } = "";
    public string SecondUserId { get; set; } = "";
    public bool FirstAccepted { get; set; }
    public bool SecondAccepted { get; set; }
    public MatchState State { get; set; } = MatchState.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class MessageDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string MatchId { get; set; } = "";
    public string SenderUserId { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DatePlanDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string MatchId { get; set; } = "";
    public string ProposedByUserId { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public string Venue { get; set; } = "";
    public string Note { get; set; } = "";
    public string Status { get; set; } = "proposed";
}

public sealed record RegisterRequest(string Name, string Email, string Phone, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record ProfileUpdateRequest(string Name, DateTimeOffset BirthDate, string City, string Occupation, string Bio);
public sealed record SelectedContactRequest(string DisplayName, string PhoneHash);
public sealed record ContactSyncRequest(IReadOnlyCollection<SelectedContactRequest> Contacts);
public sealed record InviteRequest(string PhoneHash, string Channel);
public sealed record ProposeMatchRequest(string FirstUserId, string SecondUserId);
public sealed record RespondToMatchRequest(bool Accept);
public sealed record SendMessageRequest(string Text);
public sealed record PlanDateRequest(DateTimeOffset StartsAt, string Venue, string? Note);
