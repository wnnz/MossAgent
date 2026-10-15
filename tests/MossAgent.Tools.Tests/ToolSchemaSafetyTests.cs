using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ToolSchemaSafetyTests
{
    [Theory]
    [InlineData("{\"$ref\":\"https://example.invalid/schema\"}")]
    [InlineData("{\"$ref\":\"file:///C:/credentials\"}")]
    [InlineData("{\"$ref\":\"#\"}")]
    [InlineData("{\"$defs\":{\"first\":{\"$ref\":\"#/$defs/second\"},\"second\":{\"$ref\":\"#/$defs/first\"}}}")]
    [InlineData("{\"$schema\":\"https://example.invalid/schema\"}")]
    [InlineData("{\"$ref\":\"#named\",\"$defs\":{\"value\":{\"$anchor\":\"named\"}}}")]
    [InlineData("{\"$dynamicRef\":\"#/$defs/value\",\"$defs\":{\"value\":true}}")]
    [InlineData("{\"$recursiveRef\":\"#/$defs/value\",\"$defs\":{\"value\":true}}")]
    [InlineData("{\"$defs\":{\"value\":{\"$id\":\"nested\",\"$ref\":\"#/$defs/local\",\"$defs\":{\"local\":true}}}}")]
    [InlineData("{\"properties\":{\"unused\":{\"$id\":\"https://example.invalid/nested\"}}}")]
    [InlineData("{\"$defs\":{\"value\":{\"$schema\":\"http://json-schema.org/draft-07/schema#\"}}}")]
    [InlineData("{\"pattern\":\"[invalid\"}")]
    [InlineData("{\"patternProperties\":{\"[invalid\":true}}")]
    [InlineData("{\"type\":[\"object\",123]}")]
    [InlineData("{\"properties\":{\"optional\":{\"minimum\":\"synthetic-secret\"}}}")]
    public async Task UnsafeOrMalformedSchema_IsRejectedWithoutExecutionOrDisclosure(string schema)
    {
        var fixture = new ToolSchemaFixture(schema);
        var result = await fixture.ExecuteAsync("{}");
        fixture.AssertOutcome(result, false, "tool_schema_invalid");
        Assert.DoesNotContain("synthetic-secret", result.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("credentials", result.Summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CatastrophicRegex_HasABoundedMatchAndCannotReachApproval(bool propertyName)
    {
        var fixture = new ToolSchemaFixture(propertyName
            ? """{"type":"object","patternProperties":{"^(a+)+$":{"type":"string"}}} """
            : """{"type":"string","pattern":"^(a+)+$"} """);
        var text = new string('a', 10_000) + "!";
        var arguments = propertyName ? JsonSerializer.Serialize(new Dictionary<string, string> { [text] = "value" })
            : JsonSerializer.Serialize(text);
        var timer = Stopwatch.StartNew();
        fixture.AssertOutcome(await fixture.ExecuteAsync(arguments), false);
        Assert.InRange(timer.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(3));
    }

    [Theory]
    [InlineData("http://json-schema.org/draft-06/schema#")]
    [InlineData("http://json-schema.org/draft-07/schema#")]
    [InlineData("https://json-schema.org/draft/2019-09/schema")]
    [InlineData("https://json-schema.org/draft/2020-12/schema")]
    public async Task DeclaredDialects_KeepBoundedPatternsAndUnicodeScalarLengths(string dialect)
    {
        var schema = JsonSerializer.Serialize(new
        {
            @schema = dialect,
            type = "string",
            minLength = 2,
            maxLength = 2,
            pattern = "^e.*$"
        }).Replace("\"schema\":", "\"$schema\":", StringComparison.Ordinal);
        var fixture = new ToolSchemaFixture(schema);
        fixture.AssertOutcome(await fixture.ExecuteAsync(JsonSerializer.Serialize("e\u0301")), true);
    }

    [Theory]
    [InlineData(1_023, true)]
    [InlineData(1_024, false)]
    public async Task SchemaExpansionLimit_IsEnforcedBeforeApproval(int children, bool accepted)
    {
        var fixture = new ToolSchemaFixture(JsonSerializer.Serialize(new { allOf = Enumerable.Repeat(true, children) }));
        fixture.AssertOutcome(await fixture.ExecuteAsync("{}"), accepted, "tool_schema_invalid");
    }

    [Theory]
    [InlineData(8_191, true)]
    [InlineData(8_192, false)]
    public async Task ArgumentStructureLimit_IsEnforcedBeforeApproval(int children, bool accepted)
    {
        var fixture = new ToolSchemaFixture("true");
        fixture.AssertOutcome(await fixture.ExecuteAsync(JsonSerializer.Serialize(Enumerable.Repeat(0, children))), accepted);
    }

    [Fact]
    public async Task LiteralData_IsNotMistakenForAnExternalSchemaReference()
    {
        var fixture = new ToolSchemaFixture("""{"const":{"$ref":"https://example.invalid/data"}}""");
        fixture.AssertOutcome(await fixture.ExecuteAsync("""{"$ref":"https://example.invalid/data"}"""), true);
    }
}
