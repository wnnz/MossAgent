using System.Runtime.InteropServices;
using System.Security.Principal;

namespace MossAgent.Platform.Windows.Sandbox;

/// <summary>
/// Unmanaged SECURITY_CAPABILITIES blob handed to the proc-thread attribute list.
/// The block packs the struct, the SID_AND_ATTRIBUTES array and the SID bytes so a
/// single pointer stays valid until CreateProcess returns; Dispose frees it.
/// </summary>
internal sealed class WindowsSandboxCapabilities : IDisposable
{
    private static readonly string[] OutboundNetworkSids = ["S-1-15-3-1", "S-1-15-3-2", "S-1-15-3-3"];
    private nint _block;

    private WindowsSandboxCapabilities(nint block) => _block = block;

    public nint Pointer => _block;

    public static WindowsSandboxCapabilities Create(byte[] appContainerSid, bool allowOutboundNetwork)
    {
        var capabilities = allowOutboundNetwork ? OutboundNetworkSids : [];
        var capabilityBytes = capabilities.Select(ToBytes).ToArray();
        var headerSize = Marshal.SizeOf<WindowsSecurityCapabilities>();
        var entrySize = Marshal.SizeOf<WindowsSidAndAttributes>();
        var sidBase = headerSize + entrySize * capabilityBytes.Length;
        var total = sidBase + appContainerSid.Length + capabilityBytes.Sum(item => item.Length);
        var block = Marshal.AllocHGlobal(total);
        try
        {
            Marshal.Copy(appContainerSid, 0, block + sidBase, appContainerSid.Length);
            var cursor = block + sidBase + appContainerSid.Length;
            for (var index = 0; index < capabilityBytes.Length; index++)
            {
                Marshal.Copy(capabilityBytes[index], 0, cursor, capabilityBytes[index].Length);
                Marshal.StructureToPtr(new WindowsSidAndAttributes
                { Sid = cursor, Attributes = WindowsAppContainerNative.SecurityGroupEnabled },
                    block + headerSize + index * entrySize, false);
                cursor += capabilityBytes[index].Length;
            }
            Marshal.StructureToPtr(new WindowsSecurityCapabilities
            {
                AppContainerSid = block + sidBase,
                Capabilities = capabilityBytes.Length == 0 ? 0 : block + headerSize,
                CapabilityCount = (uint)capabilityBytes.Length
            }, block, false);
            return new WindowsSandboxCapabilities(block);
        }
        catch
        {
            Marshal.FreeHGlobal(block);
            throw;
        }
    }

    private static byte[] ToBytes(string sid)
    {
        var identity = new SecurityIdentifier(sid);
        var bytes = new byte[identity.BinaryLength];
        identity.GetBinaryForm(bytes, 0);
        return bytes;
    }

    public void Dispose()
    {
        if (_block == 0) return;
        Marshal.FreeHGlobal(_block);
        _block = 0;
    }
}
