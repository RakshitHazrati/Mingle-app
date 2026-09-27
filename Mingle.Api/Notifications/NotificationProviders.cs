namespace Mingle.Api.Notifications;

public sealed record DeliveryReceipt(string Channel, string Status, string ProviderReference);

public interface IInvitationNotifier
{
    Task<DeliveryReceipt> SendAsync(string channel, string recipientPhoneHash, string appLink, CancellationToken cancellationToken);
}

public sealed class DevelopmentInvitationNotifier(ILogger<DevelopmentInvitationNotifier> logger) : IInvitationNotifier
{
    public Task<DeliveryReceipt> SendAsync(string channel, string recipientPhoneHash, string appLink, CancellationToken cancellationToken)
    {
        // Replace with verified SMS and WhatsApp Business providers. Credentials stay on the server.
        logger.LogInformation("Development notification queued via {Channel} for hash suffix {Suffix}. Link: {Link}",
            channel, recipientPhoneHash.Length > 6 ? recipientPhoneHash[^6..] : "hidden", appLink);
        return Task.FromResult(new DeliveryReceipt(channel, "development-placeholder", Guid.NewGuid().ToString("N")));
    }
}
