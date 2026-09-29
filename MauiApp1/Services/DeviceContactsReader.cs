using MauiApp1.Models;

namespace MauiApp1.Services;

/// <summary>
/// Reads only the contact fields Mingle needs. On Android this deliberately uses
/// one native cursor instead of Essentials Contacts.GetAllAsync(), which expands
/// every rich contact record and becomes very slow on large address books.
/// </summary>
public sealed class DeviceContactsReader
{
    public Task<List<DeviceContactCard>> ReadAsync(CancellationToken cancellationToken)
    {
#if ANDROID
        return Task.Run(() => ReadAndroid(cancellationToken), cancellationToken);
#else
        return ReadWithEssentialsAsync(cancellationToken);
#endif
    }

#if ANDROID
    private static List<DeviceContactCard> ReadAndroid(CancellationToken cancellationToken)
    {
        var cards = new List<DeviceContactCard>();
        var uniqueNumbers = new HashSet<string>(StringComparer.Ordinal);
        var resolver = Android.App.Application.Context.ContentResolver;
        var uri = Android.Provider.ContactsContract.CommonDataKinds.Phone.ContentUri;
        if (resolver is null || uri is null)
        {
            return cards;
        }

        string[] projection = ["display_name", "data1", "data4"];
        using var cursor = resolver.Query(
            uri,
            projection,
            selection: null,
            selectionArgs: null,
            sortOrder: "display_name COLLATE LOCALIZED ASC");
        if (cursor is null)
        {
            return cards;
        }

        var nameIndex = cursor.GetColumnIndex("display_name");
        var numberIndex = cursor.GetColumnIndex("data1");
        var normalizedIndex = cursor.GetColumnIndex("data4");

        while (cursor.MoveToNext())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var phoneNumber = numberIndex >= 0 ? cursor.GetString(numberIndex)?.Trim() : null;
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                continue;
            }

            var providerNormalized = normalizedIndex >= 0 ? cursor.GetString(normalizedIndex) : null;
            var normalized = NormalizePhone(providerNormalized ?? phoneNumber);
            if (normalized.Length < 8 || !uniqueNumbers.Add(normalized))
            {
                continue;
            }

            var displayName = nameIndex >= 0 ? cursor.GetString(nameIndex)?.Trim() : null;
            displayName = string.IsNullOrWhiteSpace(displayName) ? "Unnamed contact" : displayName;
            cards.Add(CreateCard(displayName, phoneNumber));
        }

        return cards;
    }
#else
    private static async Task<List<DeviceContactCard>> ReadWithEssentialsAsync(
        CancellationToken cancellationToken)
    {
        var contacts = await Contacts.Default.GetAllAsync(cancellationToken);
        return await Task.Run(() =>
        {
            var cards = new List<DeviceContactCard>();
            var uniqueNumbers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var contact in contacts ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();
                var displayName = string.IsNullOrWhiteSpace(contact.DisplayName)
                    ? "Unnamed contact"
                    : contact.DisplayName.Trim();

                foreach (var phone in contact.Phones ?? [])
                {
                    var phoneNumber = phone.PhoneNumber?.Trim();
                    if (string.IsNullOrWhiteSpace(phoneNumber))
                    {
                        continue;
                    }

                    var normalized = NormalizePhone(phoneNumber);
                    if (normalized.Length >= 8 && uniqueNumbers.Add(normalized))
                    {
                        cards.Add(CreateCard(displayName, phoneNumber));
                    }
                }
            }

            cards.Sort((left, right) =>
                StringComparer.CurrentCultureIgnoreCase.Compare(left.DisplayName, right.DisplayName));
            return cards;
        }, cancellationToken);
    }
#endif

    private static DeviceContactCard CreateCard(string displayName, string phoneNumber) => new()
    {
        DisplayName = displayName,
        PhoneNumber = phoneNumber,
        Initials = GetInitials(displayName)
    };

    private static string NormalizePhone(string value) =>
        new(value.Where(char.IsDigit).ToArray());

    private static string GetInitials(string? name)
    {
        var initials = string.Join("", (name ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0])));
        return string.IsNullOrWhiteSpace(initials) ? "?" : initials;
    }
}
