using System.Security.Cryptography;
using System.Text;

namespace MossAgent.Browser;

/// <summary>
/// Hashes journaled browser action targets down to a 16-hex-character prefix. Selectors
/// are hashed verbatim; navigations are hashed by normalized scheme://host[:port] origin
/// only, so userinfo, paths, queries and typed input never reach the journal.
/// </summary>
internal static class BrowserTargetHash
{
    public static string ForSelector(string selector) => Hash(selector);

    public static string ForUri(Uri uri) => Hash(Origin(uri));

    private static string Origin(Uri uri) =>
        $"{uri.Scheme}://{uri.Host}{(uri.IsDefaultPort ? string.Empty : $":{uri.Port}")}";

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];
}
