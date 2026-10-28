using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Principal;
using MossAgent.Platform.Windows.Processes;
using MossAgent.Platform.Windows.Sandbox;
using MossAgent.Tools.Abstractions.Processes;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

/// <summary>
/// Network capability coverage: the SECURITY_CAPABILITIES blob must carry the
/// well-known internet capability SIDs only when the spec allows outbound
/// network, and a no-capability container must be unable to open a socket.
/// </summary>
public sealed class SandboxNetworkTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void SecurityCapabilities_CarryInternetSidsOnlyWhenAllowed()
    {
        var container = new SecurityIdentifier(WindowsAppContainerNative.AllApplicationPackagesSid);
        var bytes = new byte[container.BinaryLength];
        container.GetBinaryForm(bytes, 0);

        using var allowed = WindowsSandboxCapabilities.Create(bytes, allowOutboundNetwork: true);
        using var denied = WindowsSandboxCapabilities.Create(bytes, allowOutboundNetwork: false);

        var allowedHeader = Marshal.PtrToStructure<WindowsSecurityCapabilities>(allowed.Pointer);
        var deniedHeader = Marshal.PtrToStructure<WindowsSecurityCapabilities>(denied.Pointer);
        Assert.Equal(3u, allowedHeader.CapabilityCount);
        Assert.Equal(["S-1-15-3-1", "S-1-15-3-2", "S-1-15-3-3"], ReadCapabilitySids(allowedHeader));
        Assert.Equal(0u, deniedHeader.CapabilityCount);
        Assert.Equal(nint.Zero, deniedHeader.Capabilities);
    }

    [Fact]
    public async Task NetworkDisabled_OutboundConnectIsDenied()
    {
        var root = CreateDirectory();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            var script = "try { $client = New-Object System.Net.Sockets.TcpClient; "
                + $"$client.Connect('127.0.0.1', {port}); [Console]::Out.Write('CONNECTED'); exit 0 }}"
                + " catch { exit 7 }";
            var spec = new ProcessIsolationSpec(SandboxKey(), [root], [], null,
                allowOutboundNetwork: false);

            await using var process = new WindowsTaskProcessLauncher().Start(new TaskProcessStartRequest(
                "powershell", root, ["-NoProfile", "-Command", script], Isolation: spec), Token);
            var output = process.StandardOutput.ReadToEndAsync(Token);
            await process.WaitForExitAsync(Token).WaitAsync(TimeSpan.FromSeconds(30), Token);

            Assert.Equal(7, process.ExitCode);
            Assert.DoesNotContain("CONNECTED", await output);
        }
        finally { await WindowsProcessTestDirectory.DeleteAsync(root); }
    }

    private static string[] ReadCapabilitySids(WindowsSecurityCapabilities header)
    {
        var result = new string[header.CapabilityCount];
        var entrySize = Marshal.SizeOf<WindowsSidAndAttributes>();
        for (var index = 0; index < result.Length; index++)
        {
            var entry = Marshal.PtrToStructure<WindowsSidAndAttributes>(
                header.Capabilities + index * entrySize);
            var identity = new SecurityIdentifier(entry.Sid);
            result[index] = identity.Value;
        }
        return result;
    }

    private static string SandboxKey() => $"mossagent-test-{Guid.NewGuid():N}"[..40];

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
