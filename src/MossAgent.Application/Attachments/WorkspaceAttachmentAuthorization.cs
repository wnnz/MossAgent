using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MossAgent.Domain;

namespace MossAgent.Application.Attachments;

public static class WorkspaceAttachmentAuthorization
{
    public static string CreateSelection(string fullPath)
    {
        if (!Path.IsPathFullyQualified(fullPath)) throw new ArgumentException("Selected file paths must be absolute.", nameof(fullPath));
        var path = Path.GetFullPath(fullPath).ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("explicit-file-selection:v1\0" + path)));
    }

    public static string Create(ProjectProfile project)
    {
        var directories = project.AuthorizedDirectories.Prepend(project.PrimaryDirectory)
            .Select(path => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)).ToUpperInvariant()).ToArray();
        var serialized = JsonSerializer.Serialize(new { project.Id, directories });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized)));
    }
}
