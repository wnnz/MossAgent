namespace MossAgent.Providers.Streaming;

internal sealed class SseInputLimitException(string code, string resource)
    : Exception(ProviderText.Get(resource))
{
    public string Code { get; } = code;
}
