using System.Globalization;
using System.Text.Json.Serialization;

namespace MossAgent.Tools.Abstractions;

public sealed record ToolFileSnapshot(string FileName, long Length, string Sha256)
{
    public const long MaximumBytes = 2 * 1024 * 1024;

    [JsonIgnore]
    public bool IsValid => FileName is { Length: 46 } && FileName.StartsWith("edit-", StringComparison.Ordinal)
        && FileName.EndsWith(".snapshot", StringComparison.Ordinal)
        && FileName.AsSpan(5, 32).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0
        && Length is >= 0 and <= MaximumBytes
        && Sha256 is { Length: 64 } && Sha256.All(char.IsAsciiHexDigit);

    [JsonIgnore]
    public int OutputCharacterCount => FileName.Length + Sha256.Length + Length.ToString(CultureInfo.InvariantCulture).Length;
}
