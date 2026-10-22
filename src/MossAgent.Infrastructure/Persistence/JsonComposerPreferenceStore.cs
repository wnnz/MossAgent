using System.Text.Json;
using System.Text.Json.Nodes;
using MossAgent.Application.Settings;

namespace MossAgent.Infrastructure.Persistence;

public sealed class JsonComposerPreferenceStore(AppDataPaths paths) : IComposerPreferenceStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path = Path.Combine(paths.Root, "composer-settings.json");

    public async Task<ComposerSendMode> LoadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var content = await ReadAsync(cancellationToken).ConfigureAwait(false);
            return content is null ? ComposerSendMode.EnterSends : Parse(content);
        }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(ComposerSendMode mode, CancellationToken cancellationToken)
    {
        Validate(mode);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? temporary = null;
        try
        {
            var previous = await ReadAsync(cancellationToken).ConfigureAwait(false);
            if (previous is not null) Parse(previous);
            var settings = previous is null ? new JsonObject() : JsonNode.Parse(previous)!.AsObject();
            settings[nameof(ComposerSendMode)] = mode.ToString();
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
        try { return await File.ReadAllTextAsync(_path, cancellationToken).ConfigureAwait(false); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static ComposerSendMode Parse(string content)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(nameof(ComposerSendMode), out var value)
            || value.ValueKind != JsonValueKind.String
            || !Enum.TryParse<ComposerSendMode>(value.GetString(), out var mode))
            throw new InvalidDataException("Invalid composer preferences.");
        Validate(mode);
        return mode;
    }

    private static void Validate(ComposerSendMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new InvalidDataException("Invalid composer send mode.");
    }
}
