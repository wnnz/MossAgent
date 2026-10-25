using System.Globalization;
using System.Resources;

namespace MossAgent.App.Resources;

public static class AttachmentChipText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.WorkspaceStrings", typeof(AttachmentChipText).Assembly);

    public static string RemoveLabel => Get("AttachmentRemoveLabel");

    public static string OpenLabel => Get("AttachmentOpenLabel");

    public static string AccessibleName(string path, string size) => string.IsNullOrWhiteSpace(size)
        ? path
        : string.Format(CultureInfo.CurrentUICulture, Get("AttachmentAccessibleNameFormat"), path, size);

    private static string Get(string key) => Labels.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing workspace resource: {key}");
}
