namespace MossAgent.Application.SourceControl;

public enum SourceConnectionFailure
{
    AccountUnavailable,
    AccountChanged,
    ProxyUnavailable,
    InvalidCredential,
    Forbidden,
    RateLimited,
    RedirectDenied,
    InvalidResponse,
    ServiceUnavailable,
    TransportFailure,
    TimedOut
}
