namespace TallerControl.Application.Push;

public sealed record PushSubscriptionRequest(
    string Endpoint,
    string P256Dh,
    string Auth);
