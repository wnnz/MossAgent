using System.Runtime.InteropServices;
using System.Security.Principal;

namespace MossAgent.Platform.Windows.Sandbox;

/// <summary>
/// Resolves (and lazily creates) the named AppContainer profile a sandbox key maps to.
/// A bare derived SID is not accepted by CreateProcess: the profile must exist so the
/// kernel can create the container's named-object directory at process start.
/// </summary>
internal static class WindowsAppContainerIdentity
{
    private const int MaximumNameLength = 50;

    public static (byte[] SidBytes, string SidString, string ProfileName) Ensure(string sandboxKey)
    {
        var name = sandboxKey.Length <= MaximumNameLength ? sandboxKey : sandboxKey[..MaximumNameLength];
        // A null description is rejected with E_INVALIDARG on this API.
        var result = WindowsAppContainerNative.CreateAppContainerProfile(name, name, name, 0, 0, out var sid);
        if (result == WindowsAppContainerNative.ErrorAlreadyExists)
            result = WindowsAppContainerNative.DeriveAppContainerSidFromAppContainerName(name, out sid);
        try
        {
            WindowsAppContainerNative.RequireSuccess(result, "CreateAppContainerProfile");
            if (sid == 0)
                throw new InvalidOperationException("沙箱标识解析失败：未能取得容器 SID。");
            var sidString = ConvertToString(sid);
            var identity = new SecurityIdentifier(sidString);
            var bytes = new byte[identity.BinaryLength];
            identity.GetBinaryForm(bytes, 0);
            return (bytes, sidString, name);
        }
        finally
        {
            if (sid != 0) WindowsAppContainerNative.LocalFree(sid);
        }
    }

    public static void DeleteProfile(string profileName)
    {
        try { WindowsAppContainerNative.DeleteAppContainerProfile(profileName); }
        catch (Exception) { /* best effort: an orphaned profile only leaves registry residue */ }
    }

    private static string ConvertToString(nint sid)
    {
        if (!WindowsAppContainerNative.ConvertSidToStringSid(sid, out var text))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
        try { return Marshal.PtrToStringUni(text) ?? throw new InvalidOperationException("沙箱标识解析失败：SID 字符串为空。"); }
        finally { WindowsAppContainerNative.LocalFree(text); }
    }
}
