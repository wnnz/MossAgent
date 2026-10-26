using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.SourceControl;

internal static class PullRequestInspectionRequest
{
    public static string Schema(string? kinds = null, int maximumPage = 1000)
    {
        var kind = kinds is null ? string.Empty : ",\"kind\":{\"type\":\"string\",\"enum\":" + kinds + "}";
        var required = kinds is null ? string.Empty : ",\"kind\"";
        return $$"""
            {"type":"object","properties":{"number":{"type":"integer","minimum":1,"maximum":2147483647},"base_revision":{"type":"string","minLength":40,"maxLength":64},"head_revision":{"type":"string","minLength":40,"maxLength":64},"page":{"type":"integer","minimum":1,"maximum":{{maximumPage}}}{{kind}}},"required":["number","base_revision","head_revision","page"{{required}}],"additionalProperties":false}
            """;
    }

    public static string CommentSchema() => """
        {"type":"object","properties":{"number":{"type":"integer","minimum":1,"maximum":2147483647},"base_revision":{"type":"string","minLength":40,"maxLength":64},"head_revision":{"type":"string","minLength":40,"maxLength":64},"body":{"type":"string","minLength":1,"maxLength":4096}},"required":["number","base_revision","head_revision","body"],"additionalProperties":false}
        """;

    public static string CloseSchema() => """
        {"type":"object","properties":{"number":{"type":"integer","minimum":1,"maximum":2147483647},"base_revision":{"type":"string","minLength":40,"maxLength":64},"head_revision":{"type":"string","minLength":40,"maxLength":64}},"required":["number","base_revision","head_revision"],"additionalProperties":false}
        """;

    public static string ReviewSchema() => """
        {"type":"object","properties":{"number":{"type":"integer","minimum":1,"maximum":2147483647},"base_revision":{"type":"string","minLength":40,"maxLength":64},"head_revision":{"type":"string","minLength":40,"maxLength":64},"decision":{"type":"string","enum":["approve","request_changes","comment"]},"body":{"type":"string","maxLength":4096}},"required":["number","base_revision","head_revision","decision"],"additionalProperties":false}
        """;

    public static string MergeSchema() => """
        {"type":"object","properties":{"number":{"type":"integer","minimum":1,"maximum":2147483647},"base_revision":{"type":"string","minLength":40,"maxLength":64},"head_revision":{"type":"string","minLength":40,"maxLength":64},"method":{"type":"string","enum":["merge","squash","rebase"]}},"required":["number","base_revision","head_revision","method"],"additionalProperties":false}
        """;

    public static PullRequestRevision Revision(ToolRequest request, ToolExecutionContext context)
    {
        var revision = new PullRequestRevision(request.Arguments.GetProperty("number").GetInt32(),
            request.Arguments.GetProperty("base_revision").GetString()!, request.Arguments.GetProperty("head_revision").GetString()!);
        revision.Validate();
        if (context.SourceReviewRevision is { } expected
            && (expected.Number != revision.Number
                || !expected.BaseRevision.Equals(revision.BaseRevision, StringComparison.OrdinalIgnoreCase)
                || !expected.HeadRevision.Equals(revision.HeadRevision, StringComparison.OrdinalIgnoreCase)))
            throw new SourceReviewException("source_scope_denied");
        return revision;
    }

    public static int Number(ToolRequest request, ToolExecutionContext context)
    {
        var number = request.Arguments.GetProperty("number").GetInt32();
        if (context.SourceReviewRevision is { } expected)
        {
            expected.Validate();
            if (expected.Number != number) throw new SourceReviewException("source_scope_denied");
        }
        return number;
    }

    public static SourceRepositoryScope Scope(ToolExecutionContext context) =>
        context.SourceScope ?? throw new SourceReviewException("source_scope_denied");

    public static ToolResult Complete<T>(PullRequestInspectionPage<T> page, ToolExecutionContext context)
    {
        var result = ToolResult.Success(ToolExecutionText.Get("SourceReadCompleted"), JsonSerializer.Serialize(page));
        return result.OutputCharacterCount <= context.MaximumOutputCharacters ? result
            : SourceReadFailure.Create("source_output_limit");
    }
}
