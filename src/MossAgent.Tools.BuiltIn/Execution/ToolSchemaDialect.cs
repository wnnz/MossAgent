using Json.Schema;

namespace MossAgent.Tools.BuiltIn.Execution;

internal static class ToolSchemaDialect
{
    public static (Dialect Dialect, JsonSchema MetaSchema) Select(string? dialect) => dialect?.TrimEnd('#') switch
    {
        "http://json-schema.org/draft-06/schema" or "https://json-schema.org/draft-06/schema" => (Dialect.Draft06, MetaSchemas.Draft6),
        "http://json-schema.org/draft-07/schema" or "https://json-schema.org/draft-07/schema" => (Dialect.Draft07, MetaSchemas.Draft7),
        "https://json-schema.org/draft/2019-09/schema" => (Dialect.Draft201909, MetaSchemas.Draft201909),
        null or "https://json-schema.org/draft/2020-12/schema" => (Dialect.Draft202012, MetaSchemas.Draft202012),
        _ => throw new InvalidDataException()
    };

    public static BuildOptions Create(Dialect dialect, CancellationToken cancellationToken)
    {
        IKeywordHandler[] bounded = [new ToolSchemaStringLengthKeyword(true, cancellationToken),
            new ToolSchemaStringLengthKeyword(false, cancellationToken), new ToolSchemaPatternKeyword(),
            new ToolSchemaPatternPropertiesKeyword(), new ToolSchemaDeclaredDialectKeyword(dialect)];
        return new BuildOptions
        {
            Dialect = dialect.With(bounded),
            SchemaRegistry = new SchemaRegistry { Fetch = (_, _) => throw new InvalidDataException() }
        };
    }
}
