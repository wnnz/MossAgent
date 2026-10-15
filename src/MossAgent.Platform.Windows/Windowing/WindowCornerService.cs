using System.Runtime.InteropServices;

namespace MossAgent.Platform.Windows.Windowing;

public sealed partial class WindowCornerService
{
    private const uint DwmWindowCornerPreference = 33;
    private const int RoundedCornerPreference = 2;

    public void ApplyRoundedCorners(nint windowHandle)
    {
        if (windowHandle == 0)
        {
            return;
        }

        var preference = RoundedCornerPreference;
        _ = DwmSetWindowAttribute(
            windowHandle,
            DwmWindowCornerPreference,
            ref preference,
            sizeof(int));
    }

    [LibraryImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
    private static partial int DwmSetWindowAttribute(
        nint windowHandle,
        uint attribute,
        ref int value,
        uint valueSize);
}
