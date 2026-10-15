using System.Reflection;

namespace MossAgent.Browser.Tests;

public class PlaywrightApiProxy : DispatchProxy
{
    private Func<MethodInfo, object?[]?, object?> _invoke = null!;

    internal static T Create<T>(Func<MethodInfo, object?[]?, object?> invoke) where T : class
    {
        var proxy = Create<T, PlaywrightApiProxy>();
        ((PlaywrightApiProxy)(object)proxy)._invoke = invoke;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? arguments) =>
        _invoke(targetMethod ?? throw new ArgumentNullException(nameof(targetMethod)), arguments);
}
