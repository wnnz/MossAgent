using System.Security.Cryptography;
using MossAgent.Browser.Resources;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Browser.Artifacts;

internal sealed class BrowserArtifactWriter(IArtifactFileSessionFactory sessions)
{
    public async Task<ToolArtifact> ScreenshotAsync(ToolExecutionContext context, bool fullPage, CancellationToken token)
    {
        var browser = BrowserArtifactScope.Require(context);
        await BrowserArtifactScope.ValidateAsync(context, token);
        using var output = await sessions.CreateAsync(context, "browser", ".png", token);
        var bytes = await browser.ScreenshotAsync(fullPage, token);
        if (bytes.Length > BrowserArtifactLimits.MaximumBytes)
            throw new FileMutationSessionException(BrowserToolText.Get("ArtifactTooLarge"), "browser_artifact_too_large");
        if (bytes.Length == 0) throw new FileMutationSessionException(BrowserToolText.Get("ScreenshotEmpty"), "browser_screenshot_empty");
        await output.Stream.WriteAsync(bytes, token);
        await output.CompleteAsync(token);
        await BrowserArtifactScope.ValidateAsync(context, token);
        var artifact = new ToolArtifact(Path.GetFileName(output.Path), output.Path, "image/png", bytes.LongLength,
            Convert.ToHexString(SHA256.HashData(bytes)));
        output.Retain();
        return artifact;
    }

    public async Task<ToolArtifact> DownloadAsync(ToolExecutionContext context, string selector, CancellationToken token)
    {
        var browser = BrowserArtifactScope.Require(context);
        await BrowserArtifactScope.ValidateAsync(context, token);
        IArtifactFileSession? output = null;
        try
        {
            ToolArtifact artifact;
            using (var staging = await sessions.CreateAsync(context, "browser-transfer", ".bin", token))
            {
                using var bounded = new BoundedBrowserArtifactStream(staging.Stream);
                var info = await browser.DownloadAsync(selector, bounded, token);
                await bounded.FlushAsync(token);
                await BrowserArtifactScope.ValidateAsync(context, token);
                var name = BrowserDownloadName.FromSuggested(info.SuggestedFileName);
                output = await sessions.CreateAsync(context, "browser-download", name.Extension, token);
                staging.Stream.Position = 0;
                await staging.Stream.CopyToAsync(output.Stream, token);
                await output.Stream.FlushAsync(token);
                output.Stream.Position = 0;
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(output.Stream, token));
                await output.CompleteAsync(token);
                artifact = new(name.DisplayName, output.Path, name.MediaType, output.Stream.Length, hash);
            }
            await BrowserArtifactScope.ValidateAsync(context, token);
            output.Retain();
            return artifact;
        }
        finally { output?.Dispose(); }
    }
}
