using System.Text.Json;
using Json.Schema;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ToolSchemaIsolationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("http://json-schema.org/draft-06/schema#")]
    [InlineData("http://json-schema.org/draft-07/schema#")]
    [InlineData("https://json-schema.org/draft/2019-09/schema")]
    [InlineData("https://json-schema.org/draft/2020-12/schema")]
    public async Task RepeatedIds_AreInvocationOwnedAndNeverEnterTheGlobalRegistry(string? dialect)
    {
        var identifier = new Uri($"https://example.invalid/schemas/{Guid.NewGuid():N}");
        var fetch = SchemaRegistry.Global.Fetch;
        foreach (var expected in new[] { 1, 2, 1 })
        {
            var fixture = new ToolSchemaFixture(Schema(identifier, dialect, expected));
            fixture.AssertOutcome(await fixture.ExecuteAsync(JsonSerializer.Serialize(new { value = expected })), true);
            Assert.Same(fetch, SchemaRegistry.Global.Fetch);
            Assert.Null(SchemaRegistry.Global.Get(identifier));
        }
        var rejected = new ToolSchemaFixture(Schema(identifier, dialect, 2));
        rejected.AssertOutcome(await rejected.ExecuteAsync("{\"value\":1}"), false);
        Assert.Same(fetch, SchemaRegistry.Global.Fetch);
        Assert.Null(SchemaRegistry.Global.Get(identifier));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("http://json-schema.org/draft-06/schema#")]
    [InlineData("http://json-schema.org/draft-07/schema#")]
    [InlineData("https://json-schema.org/draft/2019-09/schema")]
    [InlineData("https://json-schema.org/draft/2020-12/schema")]
    public async Task ParallelInvocations_IsolateRootIdsAndResults(string? dialect)
    {
        var identifier = new Uri($"https://example.invalid/schemas/{Guid.NewGuid():N}");
        var fetch = SchemaRegistry.Global.Fetch;
        await Task.WhenAll(Enumerable.Range(0, 128).Select(index => Task.Run(async () =>
        {
            var accepted = index % 3 != 0;
            var fixture = new ToolSchemaFixture(Schema(identifier, dialect, index));
            var result = await fixture.ExecuteAsync(JsonSerializer.Serialize(new { value = accepted ? index : -1 }));
            fixture.AssertOutcome(result, accepted);
        }, TestContext.Current.CancellationToken)));
        Assert.Same(fetch, SchemaRegistry.Global.Fetch);
        Assert.Null(SchemaRegistry.Global.Get(identifier));
    }

    private static string Schema(Uri identifier, string? dialect, int expected)
    {
        var schema = new Dictionary<string, object>
        {
            ["$id"] = identifier.AbsoluteUri,
            ["type"] = "object",
            ["properties"] = new { value = new Dictionary<string, string> { ["$ref"] = "#/definitions/value" } },
            ["definitions"] = new { value = new { @const = expected } },
            ["required"] = new[] { "value" },
            ["additionalProperties"] = false
        };
        if (dialect is not null) schema["$schema"] = dialect;
        return JsonSerializer.Serialize(schema);
    }
}
