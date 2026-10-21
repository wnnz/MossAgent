using MossAgent.Browser.Resources;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser.Artifacts;

internal static class BrowserArtifactScope
{
    public static IBrowserSession Require(ToolExecutionContext context) => context.BrowserSession
        ?? throw new InvalidOperationException(BrowserToolText.Get("SessionRequired"));

    public static async Task ValidateAsync(ToolExecutionContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        await context.ValidateWorkspaceAsync(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
    }
}
