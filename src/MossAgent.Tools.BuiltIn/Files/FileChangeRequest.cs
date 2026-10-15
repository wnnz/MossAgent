using System.Text.Json;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Files;

internal static class FileChangeRequest
{
    public const string InputSchema = """
        {"type":"object","required":["change"],"additionalProperties":false,"properties":{"change":{
        "type":"object","required":["Path","AfterSha256"],"additionalProperties":false,"properties":{
        "Path":{"type":"string","minLength":1},"BeforeSha256":{"type":["string","null"]},
        "AfterSha256":{"type":["string","null"],"pattern":"^[0-9a-fA-F]{64}$"},
        "AfterDeleted":{"type":"boolean"},
        "BeforeSnapshot":{"anyOf":[{"type":"null"},{"$ref":"#/$defs/snapshot"}]},
        "AfterSnapshot":{"anyOf":[{"type":"null"},{"$ref":"#/$defs/snapshot"}]}}}},
        "$defs":{"snapshot":{"type":"object","required":["FileName","Length","Sha256"],"additionalProperties":false,
        "properties":{"FileName":{"type":"string","pattern":"^edit-[0-9a-fA-F]{32}\\.snapshot$"},
        "Length":{"type":"integer","minimum":0,"maximum":2097152},
        "Sha256":{"type":"string","pattern":"^[0-9a-fA-F]{64}$"}}}}}
        """;

    public static ToolFileChange Validate(ToolRequest request, ToolExecutionContext context, AuthorizedPathResolver paths)
    {
        var change = request.Arguments.GetProperty("change").Deserialize<ToolFileChange>();
        if (change is null || !change.IsValid) throw new ArgumentException();
        if (context.ReviewedChange != change)
            throw new UnauthorizedAccessException(FileToolText.Get("ReviewChangeUntrusted"));
        paths.Resolve(change.Path, context);
        paths.Resolve(context.WorkingDirectory, context);
        if (context.ArtifactDirectory is not null) FileChangeSnapshotStorage.DirectoryFor(context);
        return change;
    }
}
