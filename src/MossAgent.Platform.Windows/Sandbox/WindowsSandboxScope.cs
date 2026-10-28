using System.Security.AccessControl;
using System.Security.Principal;
using MossAgent.Tools.Abstractions.Processes;

namespace MossAgent.Platform.Windows.Sandbox;

/// <summary>
/// Prepared state for one sandboxed launch: the container identity, the acquired
/// DACL grants, the env overlay and the native capabilities blob. The scope stays
/// alive until the returned process is disposed, at which point grants release.
/// </summary>
internal sealed class WindowsSandboxScope : IAsyncDisposable
{
    private readonly WindowsSandboxCapabilities _capabilities;
    private readonly IAsyncDisposable _release;

    private WindowsSandboxScope(WindowsSandboxCapabilities capabilities, IAsyncDisposable release,
        IReadOnlyDictionary<string, string?> environmentOverlay)
    {
        _capabilities = capabilities;
        _release = release;
        EnvironmentOverlay = environmentOverlay;
    }

    public nint SecurityCapabilities => _capabilities.Pointer;
    public IReadOnlyDictionary<string, string?> EnvironmentOverlay { get; }

    public static WindowsSandboxScope Prepare(ProcessIsolationSpec spec, string executable,
        WindowsSandboxGrantLedger ledger)
    {
        var (sidBytes, sidString, profileName) = WindowsAppContainerIdentity.Ensure(spec.SandboxKey);
        var identity = new SecurityIdentifier(sidBytes, 0);
        var grants = new List<(string Path, bool Writable)>(spec.WritableRoots.Count + spec.ReadOnlyPaths.Count + 2);
        grants.AddRange(spec.WritableRoots.Select(root => (root, true)));
        grants.AddRange(spec.ReadOnlyPaths.Select(path => (path, false)));
        if (spec.PrivateDirectory is { } privateDirectory)
        {
            try { Directory.CreateDirectory(privateDirectory); }
            catch (Exception failure) { throw new InvalidOperationException($"沙箱授权失败：{privateDirectory}", failure); }
            grants.Add((privateDirectory, true));
        }
        if (ExecutableNeedsGrant(executable))
        {
            var directory = Path.GetDirectoryName(executable);
            if (directory is not null)
                grants.Add((directory, false));
        }
        var release = ledger.Acquire(sidString, identity, grants, profileName);
        try
        {
            var capabilities = WindowsSandboxCapabilities.Create(sidBytes, spec.AllowOutboundNetwork);
            return new WindowsSandboxScope(capabilities, release, CreateOverlay(spec));
        }
        catch
        {
            release.DisposeAsync().GetAwaiter().GetResult();
            throw;
        }
    }

    private static IReadOnlyDictionary<string, string?> CreateOverlay(ProcessIsolationSpec spec) =>
        spec.PrivateDirectory is { } directory
            ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            { ["TMP"] = directory, ["TEMP"] = directory }
            : new Dictionary<string, string?>();

    private static bool ExecutableNeedsGrant(string path)
    {
        try
        {
            var security = new FileInfo(path).GetAccessControl();
            return !security.GetAccessRules(true, true, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>()
                .Any(rule => rule.AccessControlType == AccessControlType.Allow && IsPackageSid(rule.IdentityReference));
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static bool IsPackageSid(IdentityReference reference)
    {
        var value = reference.Value;
        return value == WindowsAppContainerNative.AllApplicationPackagesSid
            || value == WindowsAppContainerNative.AllRestrictedApplicationPackagesSid;
    }

    public async ValueTask DisposeAsync()
    {
        _capabilities.Dispose();
        await _release.DisposeAsync().ConfigureAwait(false);
    }
}
