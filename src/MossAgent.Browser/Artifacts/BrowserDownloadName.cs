namespace MossAgent.Browser.Artifacts;

internal sealed record BrowserDownloadName(string DisplayName, string Extension, string MediaType)
{
    public static BrowserDownloadName FromSuggested(string suggested)
    {
        var name = suggested.Replace('\\', '/').Split('/')[^1];
        name = new string(name.Where(character => !char.IsControl(character)).Take(160).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(name) || name is "." or "..") name = "download.bin";
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (extension.Length is < 2 or > 16 || extension[1..].Any(character => !char.IsAsciiLetterOrDigit(character))) extension = ".bin";
        return new(name, extension, extension switch
        {
            ".json" => "application/json",
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".txt" or ".md" or ".csv" => "text/plain",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        });
    }
}
