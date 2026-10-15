using System.Text;
using System.Text.Json;
using Json.Schema;

namespace MossAgent.Tools.BuiltIn.Execution;

internal sealed class ToolSchemaStringLengthKeyword(bool minimum, CancellationToken cancellationToken) : IKeywordHandler
{
    public string Name => minimum ? "minLength" : "maxLength";

    public object ValidateKeywordValue(JsonElement value) =>
        value.TryGetInt64(out var length) && length >= 0 ? length : throw new InvalidDataException();

    public void BuildSubschemas(KeywordData keyword, BuildContext context) { }

    public KeywordEvaluation Evaluate(KeywordData keyword, EvaluationContext context)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Instance.ValueKind != JsonValueKind.String) return KeywordEvaluation.Ignore;
        var length = 0L;
        foreach (var rune in context.Instance.GetString()!.EnumerateRunes())
        {
            if (length % 256 == 0) cancellationToken.ThrowIfCancellationRequested();
            length++;
        }
        var limit = (long)keyword.Value!;
        return new KeywordEvaluation { Keyword = Name, IsValid = minimum ? length >= limit : length <= limit };
    }
}
