using System.Runtime.InteropServices;

namespace MossAgent.Platform.Windows.Sandbox;

/// <summary>
/// Native entry points for named (unregistered-package) AppContainer profiles
/// and the proc-thread attributes needed to start a process inside one.
/// </summary>
internal static class WindowsAppContainerNative
{
    public const uint ProcessAttributeSecurityCapabilities = 0x00020009;
    public const uint SecurityGroupEnabled = 0x00000020;
    public const int ErrorAlreadyExists = unchecked((int)0x800700B7);
    public const string AllApplicationPackagesSid = "S-1-15-2-1";
    public const string AllRestrictedApplicationPackagesSid = "S-1-15-2-2";

    public static void RequireSuccess(int hresult, string operation)
    {
        if (hresult < 0)
            throw new System.ComponentModel.Win32Exception(hresult, operation);
    }

    [DllImport("userenv.dll", EntryPoint = "CreateAppContainerProfile", CharSet = CharSet.Unicode)]
    public static extern int CreateAppContainerProfile(string name, string displayName,
        string? description, nint capabilities, uint capabilityCount, out nint appContainerSid);

    [DllImport("userenv.dll", EntryPoint = "DeriveAppContainerSidFromAppContainerName", CharSet = CharSet.Unicode)]
    public static extern int DeriveAppContainerSidFromAppContainerName(string name, out nint appContainerSid);

    [DllImport("userenv.dll", EntryPoint = "DeleteAppContainerProfile", CharSet = CharSet.Unicode)]
    public static extern int DeleteAppContainerProfile(string name);

    [DllImport("advapi32.dll", EntryPoint = "ConvertSidToStringSidW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ConvertSidToStringSid(nint sid, out nint stringSid);

    [DllImport("kernel32.dll")]
    public static extern nint LocalFree(nint memory);
}
