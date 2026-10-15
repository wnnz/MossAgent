using Xunit;

namespace MossAgent.Platform.Windows.Tests;

internal static class WindowsProcessTestDirectory
{
    public static async Task DeleteAsync(string root)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { Directory.Delete(root, recursive: true); return; }
            catch (IOException) when (attempt < 50)
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
        }
    }
}
