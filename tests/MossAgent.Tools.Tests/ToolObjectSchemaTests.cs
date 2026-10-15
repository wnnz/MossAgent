using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ToolObjectSchemaTests
{
    [Theory]
    [InlineData("{}", true)]
    [InlineData("{\"count\":1}", true)]
    [InlineData("{\"count\":1,\"synthetic-secret\":\"synthetic-secret\"}", false)]
    public async Task ClosedObject_RejectsUndeclaredFieldsBeforeApproval(string arguments, bool accepted)
    {
        var fixture = new ToolSchemaFixture("""
            {"type":"object","properties":{"count":{"type":"integer"}},"additionalProperties":false}
            """);
        var result = await fixture.ExecuteAsync(arguments);

        fixture.AssertOutcome(result, accepted);
        Assert.DoesNotContain("synthetic-secret", result.Summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"type\":\"object\",\"additionalProperties\":false}", "{}", true)]
    [InlineData("{\"type\":\"object\",\"additionalProperties\":false}", "{\"extra\":1}", false)]
    [InlineData("{\"type\":\"object\",\"additionalProperties\":true}", "{\"extra\":1}", true)]
    [InlineData("{\"type\":\"object\"}", "{\"extra\":1}", true)]
    [InlineData("{\"additionalProperties\":false}", "1", true)]
    public async Task AdditionalProperties_UsesSchemaDefaultsWithoutInventingProperties(
        string schema, string arguments, bool accepted)
    {
        var fixture = new ToolSchemaFixture(schema);
        fixture.AssertOutcome(await fixture.ExecuteAsync(arguments), accepted);
    }

    [Theory]
    [InlineData("{\"known\":\"ok\",\"extra\":2}", true)]
    [InlineData("{\"known\":\"ok\",\"extra\":0}", false)]
    [InlineData("{\"known\":\"ok\",\"extra\":\"synthetic-secret\"}", false)]
    public async Task AdditionalPropertySchema_ValidatesOnlyUnmatchedFields(string arguments, bool accepted)
    {
        var fixture = new ToolSchemaFixture("""
            {"type":"object","properties":{"known":{"type":"string"}},
             "additionalProperties":{"type":"integer","minimum":1}}
            """);
        fixture.AssertOutcome(await fixture.ExecuteAsync(arguments), accepted);
    }

    [Theory]
    [InlineData("{\"config\":{\"name\":\"ok\"}}", true)]
    [InlineData("{\"config\":{\"name\":\"ok\",\"extra\":true}}", false)]
    [InlineData("{\"config\":{}}", false)]
    public async Task NestedClosedObject_ValidatesRequiredAndAdditionalProperties(string arguments, bool accepted)
    {
        var fixture = new ToolSchemaFixture("""
            {"type":"object","properties":{"config":{"type":"object","required":["name"],
             "properties":{"name":{"type":"string"}},"additionalProperties":false}}}
            """);
        fixture.AssertOutcome(await fixture.ExecuteAsync(arguments), accepted);
    }

    [Theory]
    [InlineData("{\"type\":\"object\",\"additionalProperties\":1}")]
    [InlineData("{\"type\":\"object\",\"additionalProperties\":null}")]
    [InlineData("{\"type\":\"object\",\"additionalProperties\":{\"type\":123}}")]
    [InlineData("{\"type\":\"object\",\"properties\":{\"optional\":{\"type\":123}}}")]
    public async Task InvalidUnvisitedSubschema_FailsClosedBeforeApproval(string schema)
    {
        var fixture = new ToolSchemaFixture(schema);
        fixture.AssertOutcome(await fixture.ExecuteAsync("{}"), false, "tool_schema_invalid");
    }
}
