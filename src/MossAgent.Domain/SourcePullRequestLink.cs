using System.Globalization;

namespace MossAgent.Domain;

public sealed record SourcePullRequestLink(Uri Origin, string Owner, string Repository, int Number)
{
    public static SourcePullRequestLink Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096
            || value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character))
            || value.Contains('\\') || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0
            || uri.Host.Length == 0 || uri.Host.EndsWith('.'))
            throw new ArgumentException("Invalid pull request URL.");

        var suffix = value.IndexOfAny(['?', '#']);
        var location = suffix < 0 ? value : value[..suffix];
        if (location.Contains('%') || location.Split('/').Any(segment => segment is "." or ".."))
            throw new ArgumentException("Invalid pull request URL.");
        var path = uri.AbsolutePath;
        var parts = (path.EndsWith('/') ? path[..^1] : path).Split('/');
        if (parts.Length is < 5 or > 7 || parts[0].Length != 0 || parts[3] != "pull"
            || !int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0
            || (parts.Length >= 6 && parts[5] is not ("files" or "changes" or "commits" or "checks"))
            || (parts.Length == 7 && (parts[5] == "checks" || parts[6].Length is not (40 or 64)
                || parts[6].Any(character => !char.IsAsciiHexDigit(character)))))
            throw new ArgumentException("Invalid pull request URL.");
        return new(new Uri(uri.GetLeftPart(UriPartial.Authority)),
            SourceRepositoryScope.ValidateSegment(parts[1], true), SourceRepositoryScope.ValidateSegment(parts[2]), number);
    }

    public bool MatchesEndpoint(Uri apiEndpoint)
    {
        var endpoint = GitHubApiEndpoint.Parse(apiEndpoint.AbsoluteUri);
        var webHost = endpoint.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase) ? "github.com" : endpoint.Host;
        return Origin.Host.Equals(webHost, StringComparison.OrdinalIgnoreCase) && Origin.Port == endpoint.Port;
    }
}
