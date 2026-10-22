namespace MossAgent.Application.Authorization;

public sealed class AuthorizationResourceChangedException()
    : Exception("Authorization resource registrations changed before confirmation.");
