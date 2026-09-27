using System.ComponentModel.DataAnnotations;
using System.Threading.RateLimiting;
using Mingle.Api.Domain;
using Mingle.Api.Infrastructure;
using Mingle.Api.Notifications;
using Mingle.Api.Security;
using Microsoft.AspNetCore.HttpOverrides;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // The API runs behind the hosting provider's dynamic reverse proxies.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<IInvitationNotifier, DevelopmentInvitationNotifier>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("api", context =>
        RateLimitPartition.GetTokenBucketLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 120,
                TokensPerPeriod = 60,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                AutoReplenishment = true,
                QueueLimit = 0
            }));
});

var mongoConnection = builder.Configuration["Mongo:ConnectionString"];
var databaseName = builder.Configuration["Mongo:Database"] ?? "MingleApp";
var useInMemory = builder.Configuration.GetValue<bool>("Storage:UseInMemory");
if (useInMemory || string.IsNullOrWhiteSpace(mongoConnection))
{
    builder.Services.AddSingleton<IMingleStore, InMemoryMingleStore>();
}
else
{
    builder.Services.AddSingleton<IMingleStore>(_ => new MongoMingleStore(mongoConnection, databaseName));
}

var app = builder.Build();
app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    try
    {
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.XFrameOptions = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers.CacheControl = "no-store";
        await next();
    }
    catch (Exception exception) when (exception is MongoException or TimeoutException)
    {
        app.Logger.LogError(exception, "The persistence service is unavailable.");
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "The service is temporarily unavailable. Please try again shortly."
            });
        }
    }
});
app.UseRateLimiter();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapGet("/health", async (IMingleStore store, CancellationToken cancellationToken) =>
{
    var connected = await store.CanConnectAsync(cancellationToken);
    return Results.Json(new
    {
        status = connected ? "healthy" : "unhealthy",
        persistence = useInMemory || string.IsNullOrWhiteSpace(mongoConnection) ? "in-memory-development" : "mongodb",
        utc = DateTimeOffset.UtcNow
    }, statusCode: connected ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});

app.MapPost("/api/auth/register", async (
    RegisterRequest request,
    IMingleStore store,
    PasswordHasher passwordHasher,
    CancellationToken cancellationToken) =>
{
    var name = request.Name?.Trim() ?? "";
    var email = request.Email?.Trim().ToLowerInvariant() ?? "";
    var phone = request.Phone ?? "";
    if (name.Length is < 2 or > 80 || !new EmailAddressAttribute().IsValid(email) ||
        string.IsNullOrWhiteSpace(request.Password) || request.Password.Length is < 10 or > 200 ||
        new string(phone.Where(char.IsDigit).ToArray()).Length is < 8 or > 15)
    {
        return Results.BadRequest(new { message = "Provide a valid name, email, phone number, and password of at least 10 characters." });
    }

    if (await store.FindUserByEmailAsync(email, cancellationToken) is not null)
    {
        return Results.Conflict(new { message = "An account already exists for this email." });
    }

    var user = new UserDocument
    {
        Name = name,
        Email = email,
        PhoneHash = TokenService.HashPhone(phone),
        PasswordHash = passwordHasher.Hash(request.Password),
        ReferralCode = CreateReferralCode(name),
        Points = 50
    };

    try
    {
        await store.CreateUserAsync(user, cancellationToken);
    }
    catch (Exception ex) when (ex is MongoWriteException or InvalidOperationException)
    {
        return Results.Conflict(new { message = "That account already exists." });
    }

    return Results.Ok(await CreateSessionResponseAsync(user, store, cancellationToken));
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/login", async (
    LoginRequest request,
    IMingleStore store,
    PasswordHasher passwordHasher,
    CancellationToken cancellationToken) =>
{
    var email = request.Email?.Trim().ToLowerInvariant() ?? "";
    var user = await store.FindUserByEmailAsync(email, cancellationToken);
    if (user is null || !passwordHasher.Verify(request.Password ?? "", user.PasswordHash))
    {
        await Task.Delay(Random.Shared.Next(120, 280), cancellationToken);
        return Results.Json(new { message = "Email or password is incorrect." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    return Results.Ok(await CreateSessionResponseAsync(user, store, cancellationToken));
}).RequireRateLimiting("auth");

var secured = app.MapGroup("/api").RequireRateLimiting("api");

secured.MapGet("/me", async (HttpContext context, IMingleStore store, CancellationToken cancellationToken) =>
{
    var user = await AuthenticateAsync(context, store, cancellationToken);
    return user is null
        ? Results.Unauthorized()
        : Results.Ok(ToCurrentUser(user));
});

secured.MapPut("/me/profile", async (
    HttpContext context,
    ProfileUpdateRequest request,
    IMingleStore store,
    CancellationToken cancellationToken) =>
{
    var user = await AuthenticateAsync(context, store, cancellationToken);
    if (user is null) return Results.Unauthorized();

    var name = request.Name?.Trim() ?? "";
    var city = request.City?.Trim() ?? "";
    var occupation = request.Occupation?.Trim() ?? "";
    var bio = request.Bio?.Trim() ?? "";
    var age = GetAge(request.BirthDate);
    if (name.Length is < 2 or > 80 || city.Length is < 2 or > 100 || occupation.Length is < 2 or > 120 ||
        bio.Length is < 10 or > 800 || age is < 18 or > 100)
    {
        return Results.BadRequest(new { message = "Complete every field. Members must be 18 or older." });
    }

    user.Name = name;
    user.BirthDate = request.BirthDate.ToUniversalTime();
    user.City = city;
    user.Occupation = occupation;
    user.Bio = bio;
    user.ProfileCompleted = true;
    await store.UpdateUserAsync(user, cancellationToken);
    return Results.Ok(ToCurrentUser(user));
});

secured.MapPut("/me/photo", async (
    HttpContext context,
    IMingleStore store,
    CancellationToken cancellationToken) =>
{
    var user = await AuthenticateAsync(context, store, cancellationToken);
    if (user is null) return Results.Unauthorized();
    var contentType = context.Request.ContentType?.Split(';')[0].ToLowerInvariant() ?? "";
    if (contentType is not ("image/jpeg" or "image/png"))
        return Results.BadRequest(new { message = "Choose a JPEG or PNG profile photo." });
    if (context.Request.ContentLength is null or <= 0 or > 5_000_000)
        return Results.BadRequest(new { message = "Profile photos must be smaller than 5 MB." });

    await using var memory = new MemoryStream((int)context.Request.ContentLength.Value);
    await context.Request.Body.CopyToAsync(memory, cancellationToken);
    var data = memory.ToArray();
    var validMagic = contentType == "image/jpeg"
        ? data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF
        : data.Length > 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47;
    if (!validMagic) return Results.BadRequest(new { message = "The selected file is not a valid image." });

    await store.SaveProfilePhotoAsync(user.Id, data, contentType, cancellationToken);
    user.HasProfilePhoto = true;
    await store.UpdateUserAsync(user, cancellationToken);
    return Results.NoContent();
});

secured.MapGet("/profiles/{id}/photo", async (
    string id,
    HttpContext context,
    IMingleStore store,
    CancellationToken cancellationToken) =>
{
    var user = await AuthenticateAsync(context, store, cancellationToken);
    if (user is null) return Results.Unauthorized();
    var target = await store.FindUserByIdAsync(id, cancellationToken);
    if (target is null) return Results.NotFound();
    if (id != user.Id)
    {
        var circle = await store.GetContactsAsync(user.Id, cancellationToken);
        var matches = await store.GetMatchesForUserAsync(user.Id, cancellationToken);
        var isInCircle = circle?.PhoneHashes.Contains(target.PhoneHash, StringComparer.OrdinalIgnoreCase) == true;
        var hasMatch = matches.Any(x => x.FirstUserId == id || x.SecondUserId == id);
        if (!isInCircle && !hasMatch) return Results.Forbid();
    }
    var photo = await store.GetProfilePhotoAsync(id, cancellationToken);
    return photo is null ? Results.NotFound() : Results.File(photo.Value.Data, photo.Value.ContentType);
});

secured.MapPost("/contacts/sync", async (
    HttpContext context,
    ContactSyncRequest request,
    IMingleStore store,
    CancellationToken cancellationToken) =>
{
    var user = await AuthenticateAsync(context, store, cancellationToken);
    if (user is null) return Results.Unauthorized();

    var selected = (request.Contacts ?? [])
        .Where(x => !string.IsNullOrWhiteSpace(x.DisplayName) && x.PhoneHash?.Length == 64 && x.PhoneHash.All(Uri.IsHexDigit))
        .GroupBy(x => x.PhoneHash, StringComparer.OrdinalIgnoreCase)
        .Select(x => new SavedContactDocument
        {
            DisplayName = x.First().DisplayName.Trim()[..Math.Min(x.First().DisplayName.Trim().Length, 100)],
            PhoneHash = x.Key.ToLowerInvariant()
        })
        .Take(1000)
        .ToList();
    var hashes = selected.Select(x => x.PhoneHash).ToList();
    await store.SaveContactsAsync(new ContactBookDocument
    {
        UserId = user.Id,
        PhoneHashes = hashes,
        Contacts = selected,
        UpdatedAt = DateTimeOffset.UtcNow
    }, cancellationToken);
    user.ContactsOnboarded = true;
    await store.UpdateUserAsync(user, cancellationToken);
    var registered = await store.FindUsersByPhoneHashesAsync(hashes, cancellationToken);
    var registeredByHash = registered.Where(x => x.Id != user.Id).ToDictionary(x => x.PhoneHash, StringComparer.OrdinalIgnoreCase);
    return Results.Ok(new
    {
        synced = selected.Count,
        contacts = selected.Select(x => ToDiscoveredContact(x, registeredByHash.GetValueOrDefault(x.PhoneHash)))
    });
});

secured.MapGet("/contacts", async (HttpContext context, IMingleStore store, CancellationToken cancellationToken) =>
{
    var user = await AuthenticateAsync(context, store, cancellationToken);
    if (user is null) return Results.Unauthorized();
    var book = await store.GetContactsAsync(user.Id, cancellationToken);
    if (book is null) return Results.Ok(Array.Empty<object>());
    var contacts = book.Contacts.Count > 0
        ? book.Contacts
        : book.PhoneHashes.Select((x, index) => new SavedContactDocument { DisplayName = $"Contact {index + 1}", PhoneHash = x }).ToList();
    var registered = await store.FindUsersByPhoneHashesAsync(contacts.Select(x => x.PhoneHash), cancellationToken);
    var registeredByHash = registered.Where(x => x.Id != user.Id).ToDictionary(x => x.PhoneHash, StringComparer.OrdinalIgnoreCase);
    return Results.Ok(contacts.Select(x => ToDiscoveredContact(x, registeredByHash.GetValueOrDefault(x.PhoneHash))));
});

secured.MapPost("/invites", async (
    HttpContext context,
    InviteRequest request,
    IMingleStore store,
    IInvitationNotifier notifier,
    CancellationToken cancellationToken) =>
{
    var user = await AuthenticateAsync(context, store, cancellationToken);
    if (user is null) return Results.Unauthorized();
    var channel = request.Channel?.Trim().ToLowerInvariant() ?? "";
    if (channel is not ("sms" or "whatsapp") || request.PhoneHash?.Length != 64)
        return Results.BadRequest(new { message = "Channel must be sms or whatsapp, with a valid recipient hash." });

    var link = "https://mingle.example/join/" + user.ReferralCode;
    var receipt = await notifier.SendAsync(channel, request.PhoneHash, link, cancellationToken);
    return Results.Accepted(value: receipt);
});

secured.MapGet("/matches", async (HttpContext context, IMingleStore store, CancellationToken cancellationToken) =>
{
    var user = await AuthenticateAsync(context, store, cancellationToken);
    if (user is null) return Results.Unauthorized();
    var matches = await store.GetMatchesForUserAsync(user.Id, cancellationToken);
    var received = new List<object>();
    foreach (var match in matches.Where(x => x.FirstUserId == user.Id || x.SecondUserId == user.Id))
    {
        var otherId = match.FirstUserId == user.Id ? match.SecondUserId : match.FirstUserId;
        var other = await store.FindUserByIdAsync(otherId, cancellationToken);
        var matchmaker = await store.FindUserByIdAsync(match.MatchmakerUserId, cancellationToken);
        if (other is null) continue;
        received.Add(new
        {
            match.Id,
            state = match.State.ToString().ToLowerInvariant(),
            myAccepted = match.FirstUserId == user.Id ? match.FirstAccepted : match.SecondAccepted,
            otherAccepted = match.FirstUserId == user.Id ? match.SecondAccepted : match.FirstAccepted,
            matchedBy = matchmaker?.Name ?? "A friend",
            person = ToPublicUser(other),
            match.CreatedAt,
            match.UpdatedAt
        });
    }
    return Results.Ok(received);
});

secured.MapPost("/matches", async (
    HttpContext context,
    ProposeMatchRequest request,
    IMingleStore store,
    IInvitationNotifier notifier,
    CancellationToken cancellationToken) =>
{
    var matchmaker = await AuthenticateAsync(context, store, cancellationToken);
    if (matchmaker is null) return Results.Unauthorized();
    if (request.FirstUserId == request.SecondUserId ||
        request.FirstUserId == matchmaker.Id || request.SecondUserId == matchmaker.Id)
        return Results.BadRequest(new { message = "Choose two different friends from your network." });

    var first = await store.FindUserByIdAsync(request.FirstUserId, cancellationToken);
    var second = await store.FindUserByIdAsync(request.SecondUserId, cancellationToken);
    if (first is null || second is null) return Results.NotFound(new { message = "One or both friends are not registered." });

    var circle = await store.GetContactsAsync(matchmaker.Id, cancellationToken);
    var circleHashes = circle?.PhoneHashes.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
    if (!circleHashes.Contains(first.PhoneHash) || !circleHashes.Contains(second.PhoneHash))
        return Results.Forbid();
    if (await store.FindActiveMatchBetweenAsync(first.Id, second.Id, cancellationToken) is not null)
        return Results.Conflict(new { message = "These friends already have an active introduction." });

    var match = await store.CreateMatchAsync(new MatchDocument
    {
        MatchmakerUserId = matchmaker.Id,
        FirstUserId = first.Id,
        SecondUserId = second.Id
    }, cancellationToken);
    var link = "https://mingle.example/match/" + match.Id;
    await Task.WhenAll(
        notifier.SendAsync("sms", first.PhoneHash, link, cancellationToken),
        notifier.SendAsync("whatsapp", first.PhoneHash, link, cancellationToken),
        notifier.SendAsync("sms", second.PhoneHash, link, cancellationToken),
        notifier.SendAsync("whatsapp", second.PhoneHash, link, cancellationToken));
    return Results.Created($"/api/matches/{match.Id}", match);
});

secured.MapPost("/matches/{id}/respond", async (
    string id,
    HttpContext context,
    RespondToMatchRequest request,
    IMingleStore store,
    CancellationToken cancellationToken) =>
{
    var user = await AuthenticateAsync(context, store, cancellationToken);
    if (user is null) return Results.Unauthorized();
    var match = await store.FindMatchAsync(id, cancellationToken);
    if (match is null) return Results.NotFound();
    if (user.Id != match.FirstUserId && user.Id != match.SecondUserId) return Results.Forbid();

    if (!request.Accept)
    {
        match.State = MatchState.Declined;
    }
    else
    {
        if (user.Id == match.FirstUserId) match.FirstAccepted = true;
        if (user.Id == match.SecondUserId) match.SecondAccepted = true;
        if (match.FirstAccepted && match.SecondAccepted)
        {
            match.State = MatchState.Accepted;
            await store.AddPointsAsync(match.MatchmakerUserId, 100, cancellationToken);
        }
    }
    match.UpdatedAt = DateTimeOffset.UtcNow;
    await store.UpdateMatchAsync(match, cancellationToken);
    return Results.Ok(match);
});

secured.MapGet("/matches/{id}/messages", async (
    string id, HttpContext context, IMingleStore store, CancellationToken cancellationToken) =>
{
    var auth = await GetAcceptedMatchAsync(id, context, store, cancellationToken);
    return auth.Match is null
        ? auth.Failure!
        : Results.Ok(await store.GetMessagesAsync(id, cancellationToken));
});

secured.MapPost("/matches/{id}/messages", async (
    string id,
    HttpContext context,
    SendMessageRequest request,
    IMingleStore store,
    CancellationToken cancellationToken) =>
{
    var auth = await GetAcceptedMatchAsync(id, context, store, cancellationToken);
    if (auth.Match is null) return auth.Failure!;
    var text = request.Text?.Trim() ?? "";
    if (text.Length is < 1 or > 2000) return Results.BadRequest(new { message = "Message must be 1–2000 characters." });
    var message = new MessageDocument { MatchId = id, SenderUserId = auth.User!.Id, Text = text };
    await store.AddMessageAsync(message, cancellationToken);
    return Results.Created($"/api/matches/{id}/messages/{message.Id}", message);
});

secured.MapPost("/matches/{id}/date-plans", async (
    string id,
    HttpContext context,
    PlanDateRequest request,
    IMingleStore store,
    CancellationToken cancellationToken) =>
{
    var auth = await GetAcceptedMatchAsync(id, context, store, cancellationToken);
    if (auth.Match is null) return auth.Failure!;
    if (request.StartsAt <= DateTimeOffset.UtcNow || string.IsNullOrWhiteSpace(request.Venue) || request.Venue.Length > 200)
        return Results.BadRequest(new { message = "Choose a future time and a valid venue." });
    var plan = new DatePlanDocument
    {
        MatchId = id,
        ProposedByUserId = auth.User!.Id,
        StartsAt = request.StartsAt,
        Venue = request.Venue.Trim(),
        Note = (request.Note ?? "").Trim()[..Math.Min((request.Note ?? "").Trim().Length, 500)]
    };
    await store.SaveDatePlanAsync(plan, cancellationToken);
    return Results.Created($"/api/matches/{id}/date-plans/{plan.Id}", plan);
});

app.Run();

static async Task<object> CreateSessionResponseAsync(UserDocument user, IMingleStore store, CancellationToken cancellationToken)
{
    var token = TokenService.CreateToken();
    var expiry = DateTimeOffset.UtcNow.AddDays(30);
    await store.SaveSessionAsync(new SessionDocument
    {
        UserId = user.Id,
        TokenHash = TokenService.Hash(token),
        ExpiresAt = expiry
    }, cancellationToken);
    return new { accessToken = token, expiresAt = expiry, message = "Welcome to Mingle", user = ToCurrentUser(user) };
}

static async Task<UserDocument?> AuthenticateAsync(HttpContext context, IMingleStore store, CancellationToken cancellationToken)
{
    var authorization = context.Request.Headers.Authorization.ToString();
    if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
    var token = authorization["Bearer ".Length..].Trim();
    return token.Length < 32 ? null : await store.FindUserBySessionHashAsync(TokenService.Hash(token), cancellationToken);
}

static async Task<(UserDocument? User, MatchDocument? Match, IResult? Failure)> GetAcceptedMatchAsync(
    string id, HttpContext context, IMingleStore store, CancellationToken cancellationToken)
{
    var user = await AuthenticateAsync(context, store, cancellationToken);
    if (user is null) return (null, null, Results.Unauthorized());
    var match = await store.FindMatchAsync(id, cancellationToken);
    if (match is null) return (user, null, Results.NotFound());
    if (user.Id != match.FirstUserId && user.Id != match.SecondUserId) return (user, null, Results.Forbid());
    if (match.State != MatchState.Accepted && match.State != MatchState.Successful)
        return (user, null, Results.Json(new { message = "Chat opens after both people accept." }, statusCode: StatusCodes.Status409Conflict));
    return (user, match, null);
}

static string CreateReferralCode(string name)
{
    var prefix = new string(name.Where(char.IsLetterOrDigit).Take(6).ToArray()).ToUpperInvariant();
    return $"M-{prefix}-{Random.Shared.Next(1000, 9999)}";
}

static object ToCurrentUser(UserDocument user) => new
{
    user.Id,
    user.Name,
    user.Email,
    user.ReferralCode,
    user.Points,
    user.ContactsOnboarded,
    user.ProfileCompleted,
    user.BirthDate,
    user.City,
    user.Occupation,
    user.Bio,
    user.HasProfilePhoto
};

static object ToPublicUser(UserDocument user) => new
{
    user.Id,
    user.Name,
    age = user.BirthDate is null ? 0 : GetAge(user.BirthDate.Value),
    user.City,
    user.Occupation,
    user.Bio,
    user.HasProfilePhoto
};

static object ToDiscoveredContact(SavedContactDocument contact, UserDocument? member) => new
{
    contact.DisplayName,
    onMingle = member is not null,
    member = member is null ? null : ToPublicUser(member)
};

static int GetAge(DateTimeOffset birthDate)
{
    var today = DateTime.UtcNow.Date;
    var birthday = birthDate.UtcDateTime.Date;
    var age = today.Year - birthday.Year;
    if (birthday > today.AddYears(-age)) age--;
    return age;
}

public partial class Program;
