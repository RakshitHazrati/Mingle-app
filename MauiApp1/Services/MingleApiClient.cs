using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MauiApp1.Services;

public sealed class MingleApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private string? _accessToken;

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public Task<ApiResult> RegisterAsync(string name, string email, string phone, string password, CancellationToken cancellationToken = default) =>
        AuthenticateAsync("api/auth/register", new { name, email, phone, password }, cancellationToken);

    public Task<ApiResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default) =>
        AuthenticateAsync("api/auth/login", new { email, password }, cancellationToken);

    public async Task<CurrentUserDto?> GetMeAsync(CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        return await _httpClient.GetFromJsonAsync<CurrentUserDto>("api/me", _json, cancellationToken);
    }

    public async Task<CurrentUserDto?> UpdateProfileAsync(
        string name, DateTime birthDate, string city, string occupation, string bio,
        CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        // DatePicker can return DateTimeKind.Local on Android. A birthday is a
        // calendar date, not a local instant, so explicitly remove the device
        // timezone before representing it at midnight UTC for the API.
        var birthDateUtc = new DateTimeOffset(
            DateTime.SpecifyKind(birthDate.Date, DateTimeKind.Unspecified),
            TimeSpan.Zero);
        using var response = await _httpClient.PutAsJsonAsync("api/me/profile", new
        {
            name,
            birthDate = birthDateUtc,
            city,
            occupation,
            bio
        }, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CurrentUserDto>(_json, cancellationToken);
    }

    public async Task UploadProfilePhotoAsync(byte[] data, string contentType, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var content = new ByteArrayContent(data);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var response = await _httpClient.PutAsync("api/me/photo", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<string?> GetProfilePhotoPathAsync(string userId, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await _httpClient.GetAsync($"api/profiles/{userId}/photo", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        var extension = response.Content.Headers.ContentType?.MediaType == "image/png" ? ".png" : ".jpg";
        var path = Path.Combine(FileSystem.CacheDirectory, $"mingle-profile-{userId}{extension}");
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(path);
        await input.CopyToAsync(output, cancellationToken);
        return path;
    }

    public async Task<ContactSyncResult> SyncSelectedContactsAsync(
        IEnumerable<DeviceContactSelection> contacts,
        CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        var selected = contacts.Select(x => new
        {
            displayName = x.DisplayName,
            phoneHash = Hash(NormalizePhone(x.PhoneNumber))
        }).ToArray();
        using var response = await _httpClient.PostAsJsonAsync("api/contacts/sync", new { contacts = selected }, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ContactSyncResult>(_json, cancellationToken)
            ?? new ContactSyncResult(0, []);
    }

    public async Task<IReadOnlyList<DiscoveredContactDto>> GetContactsAsync(CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        return await _httpClient.GetFromJsonAsync<List<DiscoveredContactDto>>("api/contacts", _json, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<MatchDto>> GetMatchesAsync(CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        return await _httpClient.GetFromJsonAsync<List<MatchDto>>("api/matches", _json, cancellationToken) ?? [];
    }

    public async Task ProposeMatchAsync(string firstUserId, string secondUserId, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await _httpClient.PostAsJsonAsync("api/matches", new { firstUserId, secondUserId }, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task RespondToMatchAsync(string matchId, bool accept, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await _httpClient.PostAsJsonAsync($"api/matches/{matchId}/respond", new { accept }, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<MessageDto>> GetMessagesAsync(string matchId, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        return await _httpClient.GetFromJsonAsync<List<MessageDto>>($"api/matches/{matchId}/messages", _json, cancellationToken) ?? [];
    }

    public async Task SendMessageAsync(string matchId, string text, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await _httpClient.PostAsJsonAsync($"api/matches/{matchId}/messages", new { text }, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<ApiResult> AuthenticateAsync(string path, object request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(path, request, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>(_json, cancellationToken);
        if (!response.IsSuccessStatusCode || payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            return new ApiResult(false, payload?.Message ?? "Unable to continue. Please check your details.", null);
        }

        _accessToken = payload.AccessToken;
        await SecureStorage.Default.SetAsync("mingle_access_token", _accessToken);
        return new ApiResult(true, payload.Message ?? "Welcome to Mingle", payload.User);
    }

    private async Task ApplyTokenAsync()
    {
        _accessToken ??= await SecureStorage.Default.GetAsync("mingle_access_token");
        _httpClient.DefaultRequestHeaders.Authorization =
            string.IsNullOrWhiteSpace(_accessToken) ? null : new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: cancellationToken);
        throw new HttpRequestException(error?.Message ?? $"Mingle API returned {(int)response.StatusCode}.");
    }

    private static string NormalizePhone(string value) => new(value.Where(char.IsDigit).ToArray());
    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record AuthResponse(string? AccessToken, string? Message, CurrentUserDto? User);
    private sealed record ApiError(string? Message);
}

public sealed record ApiResult(bool Success, string Message, CurrentUserDto? User);
public sealed record CurrentUserDto(
    string Id, string Name, string Email, string ReferralCode, int Points,
    bool ContactsOnboarded, bool ProfileCompleted, DateTimeOffset? BirthDate,
    string City, string Occupation, string Bio, bool HasProfilePhoto);
public sealed record DeviceContactSelection(string DisplayName, string PhoneNumber);
public sealed record PublicUserDto(string Id, string Name, int Age, string City, string Occupation, string Bio, bool HasProfilePhoto);
public sealed record DiscoveredContactDto(string DisplayName, bool OnMingle, PublicUserDto? Member);
public sealed record ContactSyncResult(int Synced, IReadOnlyList<DiscoveredContactDto> Contacts);
public sealed record MatchDto(
    string Id, string State, bool MyAccepted, bool OtherAccepted, string MatchedBy,
    PublicUserDto Person, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record MessageDto(string Id, string MatchId, string SenderUserId, string Text, DateTimeOffset SentAt);
