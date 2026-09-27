using System.Collections.Concurrent;
using Mingle.Api.Domain;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace Mingle.Api.Infrastructure;

public interface IMingleStore
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
    Task<UserDocument?> FindUserByEmailAsync(string email, CancellationToken cancellationToken);
    Task<UserDocument?> FindUserByIdAsync(string id, CancellationToken cancellationToken);
    Task<UserDocument?> FindUserBySessionHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserDocument>> FindUsersByPhoneHashesAsync(IEnumerable<string> phoneHashes, CancellationToken cancellationToken);
    Task CreateUserAsync(UserDocument user, CancellationToken cancellationToken);
    Task UpdateUserAsync(UserDocument user, CancellationToken cancellationToken);
    Task SaveProfilePhotoAsync(string userId, byte[] data, string contentType, CancellationToken cancellationToken);
    Task<(byte[] Data, string ContentType)?> GetProfilePhotoAsync(string userId, CancellationToken cancellationToken);
    Task SaveSessionAsync(SessionDocument session, CancellationToken cancellationToken);
    Task SaveContactsAsync(ContactBookDocument contacts, CancellationToken cancellationToken);
    Task<ContactBookDocument?> GetContactsAsync(string userId, CancellationToken cancellationToken);
    Task<MatchDocument> CreateMatchAsync(MatchDocument match, CancellationToken cancellationToken);
    Task<MatchDocument?> FindMatchAsync(string id, CancellationToken cancellationToken);
    Task<MatchDocument?> FindActiveMatchBetweenAsync(string firstUserId, string secondUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MatchDocument>> GetMatchesForUserAsync(string userId, CancellationToken cancellationToken);
    Task UpdateMatchAsync(MatchDocument match, CancellationToken cancellationToken);
    Task AddPointsAsync(string userId, int amount, CancellationToken cancellationToken);
    Task AddMessageAsync(MessageDocument message, CancellationToken cancellationToken);
    Task<IReadOnlyList<MessageDocument>> GetMessagesAsync(string matchId, CancellationToken cancellationToken);
    Task SaveDatePlanAsync(DatePlanDocument plan, CancellationToken cancellationToken);
}

public sealed class MongoMingleStore : IMingleStore
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<UserDocument> _users;
    private readonly IMongoCollection<SessionDocument> _sessions;
    private readonly IMongoCollection<ContactBookDocument> _contacts;
    private readonly IMongoCollection<MatchDocument> _matches;
    private readonly IMongoCollection<MessageDocument> _messages;
    private readonly IMongoCollection<DatePlanDocument> _datePlans;
    private readonly GridFSBucket _profilePhotos;
    private readonly SemaphoreSlim _initializeLock = new(1, 1);
    private bool _initialized;

    public MongoMingleStore(string connectionString, string databaseName)
    {
        var settings = MongoClientSettings.FromConnectionString(connectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        _database = new MongoClient(settings).GetDatabase(databaseName);
        _users = _database.GetCollection<UserDocument>("users_v2");
        _sessions = _database.GetCollection<SessionDocument>("sessions_v2");
        _contacts = _database.GetCollection<ContactBookDocument>("contacts_v2");
        _matches = _database.GetCollection<MatchDocument>("matches_v2");
        _messages = _database.GetCollection<MessageDocument>("messages_v2");
        _datePlans = _database.GetCollection<DatePlanDocument>("date_plans_v2");
        _profilePhotos = new GridFSBucket(_database, new GridFSBucketOptions { BucketName = "profile_photos" });
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
            if (_initialized) return true;

            await _initializeLock.WaitAsync(cancellationToken);
            try
            {
                if (!_initialized)
                {
                    await EnsureIndexesAsync();
                    _initialized = true;
                }
            }
            finally
            {
                _initializeLock.Release();
            }
            return true;
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            return false;
        }
    }

    private async Task EnsureIndexesAsync()
    {
        try
        {
            await _users.Indexes.DropOneAsync("PhoneHash_1");
        }
        catch (MongoCommandException exception) when (exception.CodeName == "IndexNotFound")
        {
        }
        await _users.Indexes.CreateManyAsync([
            new CreateIndexModel<UserDocument>(Builders<UserDocument>.IndexKeys.Ascending(x => x.Email), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<UserDocument>(Builders<UserDocument>.IndexKeys.Ascending(x => x.PhoneHash), new CreateIndexOptions { Unique = true, Name = "ux_users_phone_hash" }),
            new CreateIndexModel<UserDocument>(Builders<UserDocument>.IndexKeys.Ascending(x => x.ReferralCode), new CreateIndexOptions { Unique = true })
        ]);
        await _sessions.Indexes.CreateManyAsync([
            new CreateIndexModel<SessionDocument>(Builders<SessionDocument>.IndexKeys.Ascending(x => x.TokenHash), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<SessionDocument>(Builders<SessionDocument>.IndexKeys.Ascending(x => x.ExpiresAt), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero })
        ]);
        await _messages.Indexes.CreateOneAsync(new CreateIndexModel<MessageDocument>(
            Builders<MessageDocument>.IndexKeys.Ascending(x => x.MatchId).Ascending(x => x.SentAt)));
    }

    public async Task<UserDocument?> FindUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return await _users.Find(x => x.Email == email).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<UserDocument?> FindUserByIdAsync(string id, CancellationToken cancellationToken)
    {
        return await _users.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<UserDocument?> FindUserBySessionHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var session = await _sessions.Find(x => x.TokenHash == tokenHash && x.ExpiresAt > DateTimeOffset.UtcNow)
            .FirstOrDefaultAsync(cancellationToken);
        return session is null ? null : await FindUserByIdAsync(session.UserId, cancellationToken);
    }

    public async Task<IReadOnlyList<UserDocument>> FindUsersByPhoneHashesAsync(IEnumerable<string> phoneHashes, CancellationToken cancellationToken) =>
        await _users.Find(Builders<UserDocument>.Filter.In(x => x.PhoneHash, phoneHashes)).ToListAsync(cancellationToken);

    public Task CreateUserAsync(UserDocument user, CancellationToken cancellationToken) =>
        _users.InsertOneAsync(user, cancellationToken: cancellationToken);

    public Task UpdateUserAsync(UserDocument user, CancellationToken cancellationToken) =>
        _users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: cancellationToken);

    public async Task SaveProfilePhotoAsync(string userId, byte[] data, string contentType, CancellationToken cancellationToken)
    {
        var existing = await _profilePhotos.Find(Builders<GridFSFileInfo>.Filter.Eq(x => x.Filename, userId))
            .ToListAsync(cancellationToken);
        foreach (var file in existing)
            await _profilePhotos.DeleteAsync(file.Id, cancellationToken);
        await _profilePhotos.UploadFromBytesAsync(userId, data,
            new GridFSUploadOptions { Metadata = new BsonDocument("contentType", contentType) }, cancellationToken);
    }

    public async Task<(byte[] Data, string ContentType)?> GetProfilePhotoAsync(string userId, CancellationToken cancellationToken)
    {
        var filter = Builders<GridFSFileInfo>.Filter.Eq(x => x.Filename, userId);
        var options = new GridFSFindOptions
        {
            Sort = Builders<GridFSFileInfo>.Sort.Descending(x => x.UploadDateTime),
            Limit = 1
        };
        using var cursor = await _profilePhotos.FindAsync(filter, options, cancellationToken);
        var file = await cursor.FirstOrDefaultAsync(cancellationToken);
        if (file is null) return null;
        var data = await _profilePhotos.DownloadAsBytesAsync(file.Id, cancellationToken: cancellationToken);
        return (data, file.Metadata?.GetValue("contentType", "image/jpeg").AsString ?? "image/jpeg");
    }

    public Task SaveSessionAsync(SessionDocument session, CancellationToken cancellationToken) =>
        _sessions.InsertOneAsync(session, cancellationToken: cancellationToken);

    public Task SaveContactsAsync(ContactBookDocument contacts, CancellationToken cancellationToken) =>
        _contacts.ReplaceOneAsync(x => x.UserId == contacts.UserId, contacts, new ReplaceOptions { IsUpsert = true }, cancellationToken);

    public async Task<ContactBookDocument?> GetContactsAsync(string userId, CancellationToken cancellationToken) =>
        await _contacts.Find(x => x.UserId == userId).FirstOrDefaultAsync(cancellationToken);

    public async Task<MatchDocument> CreateMatchAsync(MatchDocument match, CancellationToken cancellationToken)
    {
        await _matches.InsertOneAsync(match, cancellationToken: cancellationToken);
        return match;
    }

    public async Task<MatchDocument?> FindMatchAsync(string id, CancellationToken cancellationToken)
    {
        return await _matches.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<MatchDocument?> FindActiveMatchBetweenAsync(string firstUserId, string secondUserId, CancellationToken cancellationToken)
    {
        var pair = Builders<MatchDocument>.Filter.Or(
            Builders<MatchDocument>.Filter.And(
                Builders<MatchDocument>.Filter.Eq(x => x.FirstUserId, firstUserId),
                Builders<MatchDocument>.Filter.Eq(x => x.SecondUserId, secondUserId)),
            Builders<MatchDocument>.Filter.And(
                Builders<MatchDocument>.Filter.Eq(x => x.FirstUserId, secondUserId),
                Builders<MatchDocument>.Filter.Eq(x => x.SecondUserId, firstUserId)));
        var active = Builders<MatchDocument>.Filter.In(x => x.State,
            new[] { MatchState.Pending, MatchState.Accepted, MatchState.Successful });
        return await _matches.Find(Builders<MatchDocument>.Filter.And(pair, active)).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MatchDocument>> GetMatchesForUserAsync(string userId, CancellationToken cancellationToken) =>
        await _matches.Find(x => x.FirstUserId == userId || x.SecondUserId == userId || x.MatchmakerUserId == userId)
            .SortByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);

    public Task UpdateMatchAsync(MatchDocument match, CancellationToken cancellationToken) =>
        _matches.ReplaceOneAsync(x => x.Id == match.Id, match, cancellationToken: cancellationToken);

    public Task AddPointsAsync(string userId, int amount, CancellationToken cancellationToken) =>
        _users.UpdateOneAsync(x => x.Id == userId, Builders<UserDocument>.Update.Inc(x => x.Points, amount), cancellationToken: cancellationToken);

    public Task AddMessageAsync(MessageDocument message, CancellationToken cancellationToken) =>
        _messages.InsertOneAsync(message, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<MessageDocument>> GetMessagesAsync(string matchId, CancellationToken cancellationToken) =>
        await _messages.Find(x => x.MatchId == matchId).SortBy(x => x.SentAt).Limit(250).ToListAsync(cancellationToken);

    public Task SaveDatePlanAsync(DatePlanDocument plan, CancellationToken cancellationToken) =>
        _datePlans.InsertOneAsync(plan, cancellationToken: cancellationToken);
}

public sealed class InMemoryMingleStore : IMingleStore
{
    private readonly ConcurrentDictionary<string, UserDocument> _users = new();
    private readonly ConcurrentDictionary<string, SessionDocument> _sessions = new();
    private readonly ConcurrentDictionary<string, ContactBookDocument> _contacts = new();
    private readonly ConcurrentDictionary<string, MatchDocument> _matches = new();
    private readonly ConcurrentDictionary<string, List<MessageDocument>> _messages = new();
    private readonly ConcurrentDictionary<string, DatePlanDocument> _datePlans = new();
    private readonly ConcurrentDictionary<string, (byte[] Data, string ContentType)> _profilePhotos = new();

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<UserDocument?> FindUserByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(_users.Values.FirstOrDefault(x => x.Email == email));

    public Task<UserDocument?> FindUserByIdAsync(string id, CancellationToken cancellationToken) =>
        Task.FromResult(_users.GetValueOrDefault(id));

    public Task<UserDocument?> FindUserBySessionHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var session = _sessions.Values.FirstOrDefault(x => x.TokenHash == tokenHash && x.ExpiresAt > DateTimeOffset.UtcNow);
        return Task.FromResult(session is null ? null : _users.GetValueOrDefault(session.UserId));
    }

    public Task<IReadOnlyList<UserDocument>> FindUsersByPhoneHashesAsync(IEnumerable<string> phoneHashes, CancellationToken cancellationToken)
    {
        var lookup = phoneHashes.ToHashSet(StringComparer.Ordinal);
        return Task.FromResult<IReadOnlyList<UserDocument>>(_users.Values.Where(x => lookup.Contains(x.PhoneHash)).ToList());
    }

    public Task CreateUserAsync(UserDocument user, CancellationToken cancellationToken)
    {
        if (_users.Values.Any(x => x.Email == user.Email)) throw new InvalidOperationException("Email already registered.");
        _users[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task UpdateUserAsync(UserDocument user, CancellationToken cancellationToken)
    {
        _users[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task SaveProfilePhotoAsync(string userId, byte[] data, string contentType, CancellationToken cancellationToken)
    {
        _profilePhotos[userId] = (data, contentType);
        return Task.CompletedTask;
    }

    public Task<(byte[] Data, string ContentType)?> GetProfilePhotoAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult<(byte[] Data, string ContentType)?>(_profilePhotos.TryGetValue(userId, out var photo) ? photo : null);

    public Task SaveSessionAsync(SessionDocument session, CancellationToken cancellationToken)
    {
        _sessions[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task SaveContactsAsync(ContactBookDocument contacts, CancellationToken cancellationToken)
    {
        _contacts[contacts.UserId] = contacts;
        return Task.CompletedTask;
    }

    public Task<ContactBookDocument?> GetContactsAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult(_contacts.GetValueOrDefault(userId));

    public Task<MatchDocument> CreateMatchAsync(MatchDocument match, CancellationToken cancellationToken)
    {
        _matches[match.Id] = match;
        return Task.FromResult(match);
    }

    public Task<MatchDocument?> FindMatchAsync(string id, CancellationToken cancellationToken) =>
        Task.FromResult(_matches.GetValueOrDefault(id));

    public Task<MatchDocument?> FindActiveMatchBetweenAsync(string firstUserId, string secondUserId, CancellationToken cancellationToken) =>
        Task.FromResult(_matches.Values.FirstOrDefault(x =>
            ((x.FirstUserId == firstUserId && x.SecondUserId == secondUserId) ||
             (x.FirstUserId == secondUserId && x.SecondUserId == firstUserId)) &&
            x.State is MatchState.Pending or MatchState.Accepted or MatchState.Successful));

    public Task<IReadOnlyList<MatchDocument>> GetMatchesForUserAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MatchDocument>>(_matches.Values
            .Where(x => x.FirstUserId == userId || x.SecondUserId == userId || x.MatchmakerUserId == userId)
            .OrderByDescending(x => x.CreatedAt).ToList());

    public Task UpdateMatchAsync(MatchDocument match, CancellationToken cancellationToken)
    {
        _matches[match.Id] = match;
        return Task.CompletedTask;
    }

    public Task AddPointsAsync(string userId, int amount, CancellationToken cancellationToken)
    {
        if (_users.TryGetValue(userId, out var user)) user.Points += amount;
        return Task.CompletedTask;
    }

    public Task AddMessageAsync(MessageDocument message, CancellationToken cancellationToken)
    {
        _messages.GetOrAdd(message.MatchId, _ => []).Add(message);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MessageDocument>> GetMessagesAsync(string matchId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MessageDocument>>(_messages.GetValueOrDefault(matchId, []).OrderBy(x => x.SentAt).ToList());

    public Task SaveDatePlanAsync(DatePlanDocument plan, CancellationToken cancellationToken)
    {
        _datePlans[plan.Id] = plan;
        return Task.CompletedTask;
    }
}
