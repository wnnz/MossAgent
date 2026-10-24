using System.Text.Json;
using System.Text.Json.Nodes;
using MossAgent.Application.Settings;

namespace MossAgent.Infrastructure.Persistence;

public sealed class JsonComposerPreferenceStore(AppDataPaths paths) : IComposerPreferenceStore
{
    private const int MaximumSize = 16 * 1024;
    private const string Key = "DefaultRunningAction";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path = Path.Combine(paths.Root, "composer-settings.json");

    public async Task<ComposerRunningAction> LoadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var content = await ReadAsync(cancellationToken).ConfigureAwait(false);
            return content is null ? ComposerRunningAction.Queue : Parse(content);
        }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(ComposerRunningAction action, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(action)) throw new InvalidDataException("Invalid composer running action.");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? temporary = null;
        try
        {
            var previous = await ReadAsync(cancellationToken).ConfigureAwait(false);
            if (previous is not null) Parse(previous);
            var settings = previous is null ? new JsonObject() : JsonNode.Parse(previous)!.AsObject();
            settings[Key] = action.ToString();
            var directory = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(directory);
            temporary = Path.Combine(directory, $".composer-settings-{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(temporary, settings.ToJsonString(), cancellationToken).ConfigureAwait(false);
            if (previous != await ReadAsync(cancellationToken).ConfigureAwait(false))
                throw new IOException("Composer preferences changed during the write.");
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            try { if (temporary is not null && File.Exists(temporary)) File.Delete(temporary); }
            finally { _gate.Release(); }
        }
    }

    private async Task<string?> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var info = new FileInfo(_path);
            if (!info.Exists) return null;
            if (info.Length > MaximumSize) throw new InvalidDataException("Composer preferences too large.");
            return await File.ReadAllTextAsync(_path, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static ComposerRunningAction Parse(string content)
    {
        if (content.Length > MaximumSize) throw new InvalidDataException("Composer preferences too large.");
        var root = JsonNode.Parse(content, new JsonNodeOptions(),
            new JsonDocumentOptions { MaxDepth = 8 }) as JsonObject;
        if (root is null
            || !root.TryGetPropertyValue(Key, out var value)
            || value is not JsonValue text
            || !text.TryGetValue<string>(out var name)
            || !Enum.TryParse<ComposerRunningAction>(name, out var action)
            || !Enum.IsDefined(action))
            throw new InvalidDataException("Invalid composer preferences.");
        return action;
    }
}
