using System.Text.Json;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.Services;

public sealed class WorkspaceFileChangeStatisticsService(IToolExecutor tools)
{
    public async Task<ToolFileChangeStatistics?> ReadAsync(ToolFileChange change, ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var request = new ToolRequest($"statistics-{Guid.NewGuid():N}", "filesystem.change_statistics",
            JsonSerializer.SerializeToElement(new { change }));
        var result = await tools.ExecuteAsync(request, context with
        {
            ReviewedChange = change,
            MaximumOutputCharacters = 1024,
            Timeout = TimeSpan.FromSeconds(5)
        }, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.IsSuccess || result.Content is not { Length: <= 1024 } content) return null;
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2 || !root.TryGetProperty("Added", out var added)
                || !root.TryGetProperty("Removed", out var removed) || !added.TryGetInt32(out var addedCount)
                || !removed.TryGetInt32(out var removedCount) || addedCount is < 0 or > 2_097_152
                || removedCount is < 0 or > 2_097_152) return null;
            return new(addedCount, removedCount);
        }
        catch (JsonException) { return null; }
        catch (InvalidOperationException) { return null; }
    }
}
