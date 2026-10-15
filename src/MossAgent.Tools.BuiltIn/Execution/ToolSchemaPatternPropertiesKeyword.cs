using System.Text.Json;
using System.Text.RegularExpressions;
using Json.Schema.Keywords;

namespace MossAgent.Tools.BuiltIn.Execution;

internal sealed class ToolSchemaPatternPropertiesKeyword : PatternPropertiesKeyword
{
    public override object ValidateKeywordValue(JsonElement value)
    {
        var patterns = new Dictionary<string, Regex>();
        foreach (var property in value.EnumerateObject())
            patterns.Add(property.Name, ToolSchemaPatternKeyword.CreateRegex(property.Name));
        return patterns;
    }
}
