using System.Security.AccessControl;
using System.Security.Principal;
using MossAgent.Platform.Windows.Sandbox;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

/// <summary>DACL bookkeeping of the grant ledger: dedup, grace retention and cleanup.</summary>
public sealed class SandboxLedgerTests
{
    [Fact]
    public async Task ReacquireSameSidAndPath_DoesNotDuplicateAce()
    {
        var root = CreateDirectory();
        try
        {
            using var ledger = new WindowsSandboxGrantLedger();
            var (sidBytes, sidString, profile) = WindowsAppContainerIdentity.Ensure($"mossagent-test-{Guid.NewGuid():N}"[..40]);
            var identity = new SecurityIdentifier(sidBytes, 0);
            await using var first = ledger.Acquire(sidString, identity, [(root, true)], profile);
            await using var second = ledger.Acquire(sidString, identity, [(root, true)], profile);

            Assert.Equal(1, CountAces(root, sidString));
        }
        finally { await WindowsProcessTestDirectory.DeleteAsync(root); }
    }

    [Fact]
    public async Task Release_KeepsAceDuringGrace_ThenSweepRemovesIt()
    {
        var root = CreateDirectory();
        try
        {
            using var ledger = new WindowsSandboxGrantLedger();
            var (sidBytes, sidString, profile) = WindowsAppContainerIdentity.Ensure($"mossagent-test-{Guid.NewGuid():N}"[..40]);
            var identity = new SecurityIdentifier(sidBytes, 0);
            var release = ledger.Acquire(sidString, identity, [(root, true)], profile);
            Assert.Equal(1, CountAces(root, sidString));

            await release.DisposeAsync();
            Assert.Equal(1, CountAces(root, sidString));

            Assert.Equal(1, ledger.SweepNow(TimeSpan.Zero));
            Assert.Equal(0, CountAces(root, sidString));
        }
        finally { await WindowsProcessTestDirectory.DeleteAsync(root); }
    }

    [Fact]
    public async Task Dispose_RemovesManagedAces()
    {
        var root = CreateDirectory();
        try
        {
            var (_, sidString, profile) = WindowsAppContainerIdentity.Ensure($"mossagent-test-{Guid.NewGuid():N}"[..40]);
            var ledger = new WindowsSandboxGrantLedger();
            var release = ledger.Acquire(sidString, new SecurityIdentifier(sidString), [(root, true)], profile);
            Assert.Equal(1, CountAces(root, sidString));

            ledger.Dispose();
            await release.DisposeAsync();

            Assert.Equal(0, CountAces(root, sidString));
        }
        finally { await WindowsProcessTestDirectory.DeleteAsync(root); }
    }

    [Fact]
    public async Task FailedAcquire_ReleasesPartialGrants()
    {
        var root = CreateDirectory();
        var missing = Path.Combine(CreateDirectory(), "missing");
        try
        {
            using var ledger = new WindowsSandboxGrantLedger();
            var (sidBytes, sidString, profile) = WindowsAppContainerIdentity.Ensure($"mossagent-test-{Guid.NewGuid():N}"[..40]);
            var identity = new SecurityIdentifier(sidBytes, 0);

            Assert.Throws<InvalidOperationException>(() => ledger.Acquire(sidString, identity,
                [(root, true), (missing, true)], profile));

            Assert.Equal(0, CountAces(root, sidString));
        }
        finally
        {
            await WindowsProcessTestDirectory.DeleteAsync(root);
            await WindowsProcessTestDirectory.DeleteAsync(Path.GetDirectoryName(missing)!);
        }
    }

    private static int CountAces(string path, string sidString)
    {
        var identity = new SecurityIdentifier(sidString);
        var security = new DirectoryInfo(path).GetAccessControl();
        return security.GetAccessRules(true, false, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .Count(rule => rule.IdentityReference.Equals(identity)
                && rule.AccessControlType == AccessControlType.Allow);
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
