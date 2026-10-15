using System.Text.Json.Serialization;

namespace MossAgent.Tools.Abstractions;

public sealed record ToolFileChange(string Path, string? BeforeSha256, string? AfterSha256,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ToolFileSnapshot? BeforeSnapshot = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ToolFileSnapshot? AfterSnapshot = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool AfterDeleted = false)
{
    [JsonIgnore]
    public bool IsValid => !string.IsNullOrWhiteSpace(Path) && !Path.Contains('\0')
        && (BeforeSha256 is not null || AfterSha256 is not null)
        && (AfterSha256 is null ? AfterDeleted : !AfterDeleted && IsSha256(AfterSha256))
        && (BeforeSha256 is null || IsSha256(BeforeSha256))
        && Matches(BeforeSnapshot, BeforeSha256) && Matches(AfterSnapshot, AfterSha256);

    [JsonIgnore]
    public int OutputCharacterCount => Path.Length + (BeforeSha256?.Length ?? 0) + (AfterSha256?.Length ?? 0)
        + (BeforeSnapshot?.OutputCharacterCount ?? 0) + (AfterSnapshot?.OutputCharacterCount ?? 0) + (AfterDeleted ? 4 : 0);

    private static bool Matches(ToolFileSnapshot? snapshot, string? hash) => snapshot is null
        || snapshot.IsValid && string.Equals(snapshot.Sha256, hash, StringComparison.OrdinalIgnoreCase);

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
}
