namespace MossAgent.Application.Persistence;

public sealed class ProxyInUseException : InvalidOperationException
{
    public ProxyInUseException() : base("The proxy is still referenced by saved configuration.")
    {
    }
}
