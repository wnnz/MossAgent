using System.Text.Json;
using System.Text.RegularExpressions;
using Json.Schema.Keywords;

namespace MossAgent.Tools.BuiltIn.Execution;

internal sealed class ToolSchemaPatternKeyword : PatternKeyword
{
    public override object ValidateKeywordValue(JsonElement value) => CreateRegex(value.GetString()!);

    internal static Regex CreateRegex(string pattern)
    {
        if (pattern.Length > 4_096) throw new InvalidDataException();
        return new Regex(pattern, RegexOptions.ECMAScript | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(25));
    }
}
