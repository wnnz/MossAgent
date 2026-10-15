using System.Globalization;
using System.Resources;

namespace MossAgent.Providers;

internal static class ProviderText
{
    private static readonly ResourceManager Resources = new("MossAgent.Providers.Resources.ProviderStrings", typeof(ProviderText).Assembly);

    public static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
