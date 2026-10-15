using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ToolComposedSchemaTests
{
    [Theory]
    [InlineData("true", "null", true)]
    [InlineData("false", "{}", false)]
    [InlineData("{\"const\":\"fixed\"}", "\"other\"", false)]
    [InlineData("{\"const\":{\"name\":\"ok\"}}", "{\"name\":\"ok\"}", true)]
    [InlineData("{\"type\":\"integer\"}", "1.0", true)]
    [InlineData("{\"type\":\"integer\"}", "1.5", false)]
    [InlineData("{\"anyOf\":[{\"type\":\"string\"},{\"type\":\"null\"}]}", "1", false)]
    [InlineData("{\"anyOf\":[{\"type\":\"string\"},{\"type\":\"null\"}]}", "null", true)]
    [InlineData("{\"oneOf\":[{\"type\":\"number\"},{\"type\":\"integer\"}]}", "1", false)]
    [InlineData("{\"allOf\":[{\"minimum\":1},{\"maximum\":3}]}", "4", false)]
    [InlineData("{\"not\":{\"type\":\"string\"}}", "\"text\"", false)]
    [InlineData("{\"type\":\"array\",\"minItems\":1}", "[]", false)]
    [InlineData("{\"type\":\"array\",\"maxItems\":1}", "[1,2]", false)]
    [InlineData("{\"type\":\"array\",\"uniqueItems\":true}", "[1,1.0]", false)]
    [InlineData("{\"type\":\"array\",\"items\":false}", "[1]", false)]
    [InlineData("{\"type\":\"number\",\"exclusiveMinimum\":1}", "1", false)]
    [InlineData("{\"type\":\"number\",\"multipleOf\":0.1}", "0.3", true)]
    [InlineData("{\"type\":\"number\",\"multipleOf\":0.1}", "0.35", false)]
    public async Task StandardAssertions_AreEnforcedByTheSameExecutionPipeline(
        string schema, string arguments, bool accepted)
    {
        var fixture = new ToolSchemaFixture(schema);
        fixture.AssertOutcome(await fixture.ExecuteAsync(arguments), accepted);
    }

    [Theory]
    [InlineData("\"yes\"", true)]
    [InlineData("\"no\"", false)]
    public async Task LocalDefinitionReference_ResolvesWithoutNetworkAccess(string arguments, bool accepted)
    {
        var fixture = new ToolSchemaFixture("""
            {"$defs":{"allowed":{"enum":["yes"]}},"$ref":"#/$defs/allowed"}
            """);
        fixture.AssertOutcome(await fixture.ExecuteAsync(arguments), accepted);
    }
}
