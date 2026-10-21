using System.Globalization;
using System.Resources;

namespace MossAgent.Platform.Windows.Resources;

internal static class WindowsFileText
{
    private static readonly ResourceManager Resources = new(
        "MossAgent.Platform.Windows.Resources.WindowsFiles", typeof(WindowsFileText).Assembly);

    public static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture)!;
    public static string Format(string key, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, Get(key), arguments);

    public static string ReadHandleDenied => Resources.GetString("ReadHandleDenied", CultureInfo.CurrentUICulture)!;
    public static string WriteHandleDenied => Resources.GetString("WriteHandleDenied", CultureInfo.CurrentUICulture)!;
    public static string WriteMetadataDenied => Resources.GetString("WriteMetadataDenied", CultureInfo.CurrentUICulture)!;
    public static string DeleteHandleDenied => Resources.GetString("DeleteHandleDenied", CultureInfo.CurrentUICulture)!;
    public static string DeleteFileRequired => Resources.GetString("DeleteFileRequired", CultureInfo.CurrentUICulture)!;
    public static string DeleteRecursiveRequired => Resources.GetString("DeleteRecursiveRequired", CultureInfo.CurrentUICulture)!;
    public static string DeleteTreeLimit => Resources.GetString("DeleteTreeLimit", CultureInfo.CurrentUICulture)!;
    public static string DeleteNotFound => Resources.GetString("DeleteNotFound", CultureInfo.CurrentUICulture)!;
    public static string DeleteMetadataDenied => Resources.GetString("DeleteMetadataDenied", CultureInfo.CurrentUICulture)!;
}
