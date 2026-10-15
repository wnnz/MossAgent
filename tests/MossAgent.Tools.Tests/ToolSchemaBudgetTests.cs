using System.Text.Json;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ToolSchemaBudgetTests
{
    [Theory]
    [InlineData(262_144, true)]
    [InlineData(262_145, false)]
    public async Task SchemaCharacterBudget_IsEnforcedBeforeApproval(int length, bool accepted)
    {
        var fixture = new ToolSchemaFixture("true".PadRight(length));
        fixture.AssertOutcome(await fixture.ExecuteAsync("{}"), accepted, "tool_schema_invalid");
    }

    [Theory]
    [InlineData(2_097_152, true)]
    [InlineData(2_097_153, false)]
    public async Task ArgumentCharacterBudget_IsEnforcedBeforeApproval(int length, bool accepted)
    {
        var fixture = new ToolSchemaFixture("true");
        fixture.AssertOutcome(await fixture.ExecuteAsync(JsonSerializer.Serialize(new string('a', length - 2))), accepted);
    }

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public async Task ArgumentDepthBudget_AlsoAppliesToExternallyParsedValues(int depth, bool accepted)
    {
        using var document = JsonDocument.Parse(new string('[', depth) + "0" + new string(']', depth),
            new JsonDocumentOptions { MaxDepth = 128 });
        var fixture = new ToolSchemaFixture("true");
        fixture.AssertOutcome(await fixture.ExecuteAsync(document.RootElement), accepted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OverlongPatterns_AreRejectedEvenWithoutMatchingInput(bool propertyName)
    {
        var pattern = new string('a', 4_097);
        var schema = propertyName
            ? JsonSerializer.Serialize(new { patternProperties = new Dictionary<string, bool> { [pattern] = true } })
            : JsonSerializer.Serialize(new { pattern });
        var fixture = new ToolSchemaFixture(schema);
        fixture.AssertOutcome(await fixture.ExecuteAsync("{}"), false, "tool_schema_invalid");
    }

    [Fact]
    public async Task CancelledValidation_CompletesAuditWithoutApprovalOrExecution()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        var fixture = new ToolSchemaFixture("true");
        fixture.AssertOutcome(await fixture.ExecuteAsync("{}", cancellation.Token), false, "tool_cancelled");
        Assert.Null(fixture.Audit.Completion!.ApprovalGranted);
    }

    [Fact]
    public async Task RootResourceId_StillSupportsLocalPointersAndOrdinaryPatternProperties()
    {
        var fixture = new ToolSchemaFixture("""
            {"$id":"https://example.invalid/root","type":"object","patternProperties":{"^name$":{"$ref":"#/$defs/name"}},
             "$defs":{"name":{"type":"string","pattern":"^[a-z]+$"}},"additionalProperties":false}
            """);
        fixture.AssertOutcome(await fixture.ExecuteAsync("""{"name":"safe"}"""), true);
    }
}
