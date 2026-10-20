namespace MossAgent.Domain;

public static class GitHubApiEndpoint
{
    public static Uri Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048
            || value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character))
            || value.Contains('\\') || value.Contains('%')
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0
            || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || uri.Host.Length == 0 || uri.Host.EndsWith('.')
            || value.Split('/').Any(segment => segment is "." or ".."))
            throw new ArgumentException("Invalid GitHub API endpoint.");

        var path = uri.AbsolutePath.TrimEnd('/');
        if (path is not ("" or "/api/v3")
            || uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || (uri.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase)
                && (path.Length != 0 || !uri.IsDefaultPort)))
            throw new ArgumentException("Invalid GitHub API endpoint.");

        return new Uri(uri.GetLeftPart(UriPartial.Authority) + path + "/");
    }
}
