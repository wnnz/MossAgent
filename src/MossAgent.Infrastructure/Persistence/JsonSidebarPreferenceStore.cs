using System.Text.Json;
using System.Text.Json.Nodes;
using MossAgent.Application.Settings;

namespace MossAgent.Infrastructure.Persistence;

public sealed class JsonSidebarPreferenceStore(AppDataPaths paths) : ISidebarPreferenceStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path = Path.Combine(paths.Root, "sidebar-settings.json");

    public async Task<SidebarPreference> LoadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var content = await ReadAsync(cancellationToken).ConfigureAwait(false);
            return content is null ? new(SidebarProjectSortOrder.UpdatedAt, []) : Parse(content);
        }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(SidebarPreference preference, CancellationToken cancellationToken)
    {
        Validate(preference);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? temporary = null;
        try
        {
            var previous = await ReadAsync(cancellationToken).ConfigureAwait(false);
            if (previous is not null) Parse(previous);
            var settings = previous is null ? new JsonObject() : JsonNode.Parse(previous)!.AsObject();
            settings[nameof(SidebarPreference.ProjectSortOrder)] = preference.ProjectSortOrder.ToString();
            settings[nameof(SidebarPreference.ManualProjectOrder)] = new JsonArray(
                preference.ManualProjectOrder.Select(id => JsonValue.Create(id.ToString())).ToArray<JsonNode?>());
            var directory = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(directory);
            temporary = Path.Combine(directory, $".sidebar-settings-{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(temporary, settings.ToJsonString(), cancellationToken).ConfigureAwait(false);
            if (previous != await ReadAsync(cancellationToken).ConfigureAwait(false))
                throw new IOException("Sidebar preferences changed during the write.");
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

    private static SidebarPreference Parse(string content)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(nameof(SidebarPreference.ProjectSortOrder), out var order)
            || order.ValueKind != JsonValueKind.String
            || !Enum.TryParse<SidebarProjectSortOrder>(order.GetString(), out var sortOrder)
            || !Enum.IsDefined(sortOrder)
            || !root.TryGetProperty(nameof(SidebarPreference.ManualProjectOrder), out var ids)
            || ids.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Invalid sidebar preferences.");
        var manualOrder = new List<Guid>();
        foreach (var id in ids.EnumerateArray())
        {
            if (id.ValueKind != JsonValueKind.String || !Guid.TryParse(id.GetString(), out var projectId))
                throw new InvalidDataException("Invalid sidebar project identity.");
            manualOrder.Add(projectId);
        }
        var preference = new SidebarPreference(sortOrder, manualOrder);
        Validate(preference);
        return preference;
    }

    private static void Validate(SidebarPreference preference)
    {
        if (!Enum.IsDefined(preference.ProjectSortOrder)
            || preference.ManualProjectOrder.Count > 10000
            || preference.ManualProjectOrder.Contains(Guid.Empty)
            || preference.ManualProjectOrder.Distinct().Count() != preference.ManualProjectOrder.Count)
            throw new InvalidDataException("Invalid sidebar preferences.");
    }
}
