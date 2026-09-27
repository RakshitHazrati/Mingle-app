using System.Collections.ObjectModel;
using MauiApp1.Models;

namespace MauiApp1.Services;

public sealed class AppState
{
    public string CurrentUserName { get; set; } = "Riya";
    public string CurrentUserId { get; private set; } = "";
    public string ReferralCode { get; private set; } = "MINGLE-RIYA";
    public int Points { get; private set; } = 240;
    public bool ContactsOnboarded { get; private set; }
    public bool ProfileCompleted { get; private set; }
    public DateTimeOffset? BirthDate { get; private set; }
    public string City { get; private set; } = "Bengaluru";
    public string Occupation { get; private set; } = "";
    public string Bio { get; private set; } = "";
    public bool HasProfilePhoto { get; private set; }
    public string? ProfileImagePath { get; private set; }
    public bool IsPreviewMode { get; set; } = true;
    public ObservableCollection<PersonCard> Friends { get; } = [];
    public ObservableCollection<MatchCard> Matches { get; } = [];
    public ObservableCollection<Conversation> Conversations { get; } = [];
    public ObservableCollection<string> Activity { get; } = [];

    public AppState()
    {
        SeedPreviewData();
    }

    public void AddPoints(int points)
    {
        Points += points;
    }

    public void ApplyCurrentUser(CurrentUserDto user, bool resetCollections = false)
    {
        CurrentUserId = user.Id;
        CurrentUserName = user.Name;
        ReferralCode = user.ReferralCode;
        Points = user.Points;
        ContactsOnboarded = user.ContactsOnboarded;
        ProfileCompleted = user.ProfileCompleted;
        BirthDate = user.BirthDate;
        City = user.City;
        Occupation = user.Occupation;
        Bio = user.Bio;
        HasProfilePhoto = user.HasProfilePhoto;
        IsPreviewMode = false;
        if (!resetCollections) return;
        Friends.Clear();
        Matches.Clear();
        Conversations.Clear();
        Activity.Clear();
    }

    public void SetProfilePhotoPath(string path)
    {
        ProfileImagePath = path;
        HasProfilePhoto = true;
    }

    public void ReplaceContacts(IEnumerable<DiscoveredContactDto> contacts)
    {
        Friends.Clear();
        var accents = new[] { "#6D3BFF", "#FF5C8A", "#22A06B", "#F59E0B", "#5B8DEF" };
        var index = 0;
        foreach (var contact in contacts)
        {
            var person = contact.Member;
            var name = person?.Name ?? contact.DisplayName;
            Friends.Add(new PersonCard
            {
                Id = person?.Id ?? $"contact-{index}",
                Name = name,
                Age = person?.Age ?? 0,
                City = person?.City ?? "From your contacts",
                Occupation = person?.Occupation ?? "",
                Bio = person?.Bio ?? "",
                Initials = GetInitials(name),
                HasJoined = contact.OnMingle,
                Accent = accents[index++ % accents.Length]
            });
        }
    }

    public void ReplaceMatches(IEnumerable<MatchDto> matches, IReadOnlyDictionary<string, string?>? photoPaths = null)
    {
        Matches.Clear();
        foreach (var item in matches)
        {
            var person = item.Person;
            Matches.Add(new MatchCard
            {
                Id = item.Id,
                Person = new PersonCard
                {
                    Id = person.Id,
                    Name = person.Name,
                    Age = person.Age,
                    City = person.City,
                    Occupation = person.Occupation,
                    Bio = person.Bio,
                    Image = photoPaths?.GetValueOrDefault(person.Id) ?? "profile_placeholder.png",
                    Initials = GetInitials(person.Name),
                    HasJoined = true
                },
                MatchedBy = item.MatchedBy,
                MatchedByInitials = GetInitials(item.MatchedBy),
                Status = item.State switch
                {
                    "accepted" => MatchStatus.Accepted,
                    "successful" => MatchStatus.Successful,
                    "declined" or "cancelled" => MatchStatus.Declined,
                    _ => MatchStatus.Pending
                },
                Reason = $"{item.MatchedBy} thinks you two should meet."
            });
        }
    }

    public Conversation GetConversation(PersonCard person, string? matchId = null)
    {
        var existing = Conversations.FirstOrDefault(x => x.Person.Id == person.Id);
        if (existing is not null)
        {
            return existing;
        }

        var conversation = new Conversation { Id = matchId ?? Guid.NewGuid().ToString("N"), Person = person };
        Conversations.Add(conversation);
        return conversation;
    }

    public void ProposeMatch(PersonCard first, PersonCard second)
    {
        Activity.Insert(0, $"You introduced {first.Name} and {second.Name}. We’ll let you know when they respond.");
    }

    private void SeedPreviewData()
    {
        var anaya = new PersonCard
        {
            Id = "anaya",
            Name = "Anaya",
            Age = 28,
            City = "Bengaluru",
            Occupation = "Product designer",
            Bio = "Always looking for the city’s best coffee, a good live gig, and someone who laughs easily.",
            Image = "demo_profile_anaya.png",
            Initials = "AS",
            Accent = "#FF5C8A",
            HasJoined = true
        };

        Friends.Add(new PersonCard { Id = "aarav", Name = "Aarav", Initials = "AK", City = "Bengaluru", HasJoined = true, Accent = "#6D3BFF" });
        Friends.Add(new PersonCard { Id = "meera", Name = "Meera", Initials = "MR", City = "Bengaluru", HasJoined = true, Accent = "#FF5C8A" });
        Friends.Add(new PersonCard { Id = "kabir", Name = "Kabir", Initials = "KS", City = "Mumbai", HasJoined = true, Accent = "#22A06B" });
        Friends.Add(new PersonCard { Id = "sana", Name = "Sana", Initials = "SA", City = "Pune", HasJoined = false, Accent = "#F59E0B" });
        Friends.Add(new PersonCard { Id = "dev", Name = "Dev", Initials = "DM", City = "Delhi", HasJoined = false, Accent = "#5B8DEF" });

        Matches.Add(new MatchCard
        {
            Id = "match-anaya",
            Person = anaya,
            MatchedBy = "Neha",
            MatchedByInitials = "NK",
            Status = MatchStatus.Pending,
            Reason = "You both love slow Sundays, live music and hunting down tiny cafés."
        });

        var conversation = new Conversation { Id = "conversation-anaya", Person = anaya };
        conversation.Messages.Add(new ChatMessage { Text = "Hey! Neha has excellent timing 😄", IsMine = false, SentAt = DateTimeOffset.Now.AddMinutes(-18) });
        conversation.Messages.Add(new ChatMessage { Text = "She really does. Nice to meet you!", IsMine = true, SentAt = DateTimeOffset.Now.AddMinutes(-15) });
        Conversations.Add(conversation);

        Activity.Add("Neha thinks you and Anaya should meet.");
        Activity.Add("Aarav joined your Mingle network.");
    }

    private static string GetInitials(string name) => string.Join("", name
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Take(2)
        .Select(x => char.ToUpperInvariant(x[0])));
}
