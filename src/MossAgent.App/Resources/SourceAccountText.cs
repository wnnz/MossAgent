using System.Globalization;
using System.Resources;

namespace MossAgent.App.Resources;

public static class SourceAccountText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.SourceAccountStrings", typeof(SourceAccountText).Assembly);

    public static string Get(string key) => Labels.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing source account resource: {key}");

    public static string Title => Get(nameof(Title));
    public static string Description => Get(nameof(Description));
    public static string Refresh => Get(nameof(Refresh));
    public static string Add => Get(nameof(Add));
    public static string Edit => Get(nameof(Edit));
    public static string Verify => Get(nameof(Verify));
    public static string Empty => Get(nameof(Empty));
    public static string EmptyDescription => Get(nameof(EmptyDescription));
    public static string DialogTitle => Get(nameof(DialogTitle));
    public static string Close => Get(nameof(Close));
    public static string Name => Get(nameof(Name));
    public static string Endpoint => Get(nameof(Endpoint));
    public static string EndpointHint => Get(nameof(EndpointHint));
    public static string Token => Get(nameof(Token));
    public static string SecretNotice => Get(nameof(SecretNotice));
    public static string Proxy => Get(nameof(Proxy));
    public static string Enabled => Get(nameof(Enabled));
    public static string Save => Get(nameof(Save));
    public static string Delete => Get(nameof(Delete));
    public static string Cancel => Get(nameof(Cancel));
}
