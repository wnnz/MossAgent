namespace MossAgent.Domain;

public sealed record SourceRepositoryScope(Guid GrantId, Guid AccountId, long AccountRevision, string Owner, string Repository)
{
    public static string ValidateSegment(string value, bool owner = false)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100 || value is "." or ".."
            || value.Any(character => !char.IsAsciiLetterOrDigit(character)
                && character != '-' && (owner || character is not ('_' or '.'))))
            throw new ArgumentException("Invalid repository identifier.", nameof(value));
        return value.ToLowerInvariant();
    }
}
