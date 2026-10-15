namespace MossAgent.Application.Networking;

public sealed class ProviderProxyUnavailableException : InvalidOperationException
{
    public ProviderProxyUnavailableException()
        : base("The selected provider proxy is missing or disabled.")
    {
    }
}
