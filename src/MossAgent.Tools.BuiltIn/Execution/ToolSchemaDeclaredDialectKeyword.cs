using System.Text.Json;
using Json.Schema;

namespace MossAgent.Tools.BuiltIn.Execution;

internal sealed class ToolSchemaDeclaredDialectKeyword(Dialect dialect) : IKeywordHandler
{
    public string Name => "$schema";

    public object? ValidateKeywordValue(JsonElement value) =>
        ReferenceEquals(dialect, ToolSchemaDialect.Select(value.GetString()).Dialect) ? null : throw new InvalidDataException();

    public void BuildSubschemas(KeywordData keyword, BuildContext context) { }
    public KeywordEvaluation Evaluate(KeywordData keyword, EvaluationContext context) => KeywordEvaluation.Ignore;
}
