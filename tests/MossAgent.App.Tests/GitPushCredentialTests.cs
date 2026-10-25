using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class GitPushCredentialTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();
    private static ToolExecutionContext Context() => new(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);
    private static ToolResult Preview(GitRemoteCredentialMethod method) => ToolResult.Success("preview",
        JsonSerializer.Serialize(new GitPushPreview(new string('a', 64), new string('b', 40), "feature/source", "origin",
            "feature/review", "https://example.invalid", ["origin"], null, method), JsonSerializerOptions.Web));

    [Theory]
    [InlineData(GitRemoteCredentialMethod.Local, "GitPushCredentialMethodLocal", false)]
    [InlineData(GitRemoteCredentialMethod.Ssh, "GitPushCredentialMethodSsh", false)]
    [InlineData(GitRemoteCredentialMethod.Https, "GitPushCredentialMethodHttps", false)]
    [InlineData(GitRemoteCredentialMethod.Unsupported, "GitPushCredentialMethodUnsupported", false)]
    [InlineData(GitRemoteCredentialMethod.Embedded, "GitPushCredentialMethodEmbedded", true)]
    public Task CredentialSummaryShowsTheLocalizedMethodAndWarning(GitRemoteCredentialMethod method, string key, bool warning) =>
        _session.DispatchAsync(async () =>
        {
            var executor = new InspectorProbeToolExecutor { Execute = (_, _, _) => Task.FromResult(Preview(method)) };
            await using var model = new GitPushViewModel(executor, _ => Task.CompletedTask);
            model.Attach(Context());
            await model.OpenCommand.ExecuteAsync(null);
            Assert.True(model.HasCredential);
            Assert.Equal(warning, model.HasCredentialWarning);
            Assert.Equal(InspectorText.Format("GitPushCredential", InspectorText.Get(key))
                + (warning ? " " + InspectorText.Get("GitPushCredentialEmbedded") : string.Empty), model.CredentialSummary);
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task LegacyPreviewWithoutCredentialMethodReadsAsUnknown() => _session.DispatchAsync(async () =>
    {
        var json = "{\"state\":\"" + new string('a', 64) + "\",\"head\":\"" + new string('b', 40)
            + "\",\"branch\":\"feature/source\",\"remote\":\"origin\",\"targetBranch\":\"feature/review\","
            + "\"destination\":\"https://example.invalid\",\"remotes\":[\"origin\"],\"remoteHead\":null}";
        var executor = new InspectorProbeToolExecutor { Execute = (_, _, _) => Task.FromResult(ToolResult.Success("preview", json)) };
        await using var model = new GitPushViewModel(executor, _ => Task.CompletedTask);
        model.Attach(Context());
        await model.OpenCommand.ExecuteAsync(null);
        Assert.True(model.HasPreview);
        Assert.True(model.CanPush);
        Assert.False(model.HasCredentialWarning);
        Assert.Equal(InspectorText.Format("GitPushCredential", InspectorText.Get("GitPushCredentialMethodUnsupported")),
            model.CredentialSummary);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DialogShowsTheCredentialLineWithTheEmbeddedWarningStyle() => _session.DispatchAsync(async () =>
    {
        var executor = new InspectorProbeToolExecutor { Execute = (_, _, _) => Task.FromResult(Preview(GitRemoteCredentialMethod.Embedded)) };
        await using var model = new GitPushViewModel(executor, _ => Task.CompletedTask);
        model.Attach(Context());
        var dialog = new GitPushDialog { DataContext = model };
        var window = new AppWindow { Content = dialog, Width = 640, Height = 560 };
        window.Show();
        try
        {
            await model.OpenCommand.ExecuteAsync(null);
            var line = dialog.FindControl<AppText>("PushCredentialText")!;
            Assert.True(line.IsVisible);
            Assert.Equal(model.CredentialSummary, line.Text);
            Assert.Contains("status-warning", line.Classes);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
