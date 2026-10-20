using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestInspectionLifecycleTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task FilePaginationKeepsLocalPagesAndClearsThePreviousPatch() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync();
        fixture.Source.Transport.RespondAsync = (request, token) =>
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/files", StringComparison.Ordinal)) return PullRequestInspectionUiFixture.RespondAsync(request, token);
            var body = request.RequestUri.Query.EndsWith("page=1", StringComparison.Ordinal)
                ? "[" + string.Join(',', Enumerable.Range(1, 20).Select(index => PullRequestTestData.File(path: $"file-{index}.cs"))) + "]" : "[]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        };
        var files = fixture.Model.Inspection.Files;
        await files.LoadCommand.ExecuteAsync(null);
        Assert.Equal(20, files.Items.Count); Assert.True(files.NextCommand.CanExecute(null));
        await files.NextCommand.ExecuteAsync(null);
        Assert.Equal(2, files.Page); Assert.True(files.IsEmpty);
        Assert.Null(files.SelectedItem); Assert.False(files.NextCommand.CanExecute(null));
        Assert.Equal(PullRequestText.Get("SelectFile"), fixture.Model.Inspection.PatchHint);
        await files.PreviousCommand.ExecuteAsync(null);
        Assert.Equal(1, files.Page); Assert.Equal(20, files.Items.Count);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ChangedHeadAfterTheFileResponseClearsAlreadyLoadedComments() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync();
        var inspection = fixture.Model.Inspection;
        await inspection.Conversation.LoadCommand.ExecuteAsync(null);
        var summaries = 0;
        fixture.Source.Transport.RespondAsync = (request, token) =>
        {
            if (request.RequestUri!.Query.Length > 0) return PullRequestInspectionUiFixture.RespondAsync(request, token);
            var body = ++summaries == 2 ? PullRequestTestData.Item().Replace(new string('b', 40), new string('c', 40), StringComparison.Ordinal) : PullRequestTestData.Item();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        };
        await inspection.Files.LoadCommand.ExecuteAsync(null);
        Assert.Equal(2, summaries);
        Assert.Empty(inspection.Files.Items); Assert.Empty(inspection.Conversation.Items);
        Assert.Equal(PullRequestText.Get("RevisionChanged"), inspection.Status);
        fixture.Source.Transport.RespondAsync = PullRequestInspectionUiFixture.RespondAsync;
        await fixture.Model.DetailCommand.ExecuteAsync(null);
        Assert.True(inspection.Files.LoadCommand.CanExecute(null));
        await inspection.Files.LoadCommand.ExecuteAsync(null);
        Assert.Single(inspection.Files.Items);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DetachedOperationStillAwaitsTransportCleanupWithoutPublishingLateData() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Source.Transport.RespondAsync = async (request, token) =>
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/files", StringComparison.Ordinal)) return await PullRequestInspectionUiFixture.RespondAsync(request, token);
            entered.TrySetResult(); await release.Task;
            return new(HttpStatusCode.OK) { Content = new StringContent($"[{PullRequestTestData.File()}]") };
        };
        var work = fixture.Model.Inspection.Files.LoadCommand.ExecuteAsync(null);
        try
        {
            await entered.Task.WaitAsync(fixture.Token);
            fixture.Model.Leave();
            Assert.False(work.IsCompleted);
            Assert.Empty(fixture.Model.Inspection.Files.Items);
        }
        finally { release.TrySetResult(); await work; }
        Assert.Equal(4, fixture.Source.Transport.DisposedHandlers);
        Assert.Empty(fixture.Model.Inspection.Files.Items);
        Assert.Null(fixture.Model.Detail);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, 526, 601)]
    [InlineData(true, 980, 740)]
    public Task FullPrPageUsesWideDetailAndRetainsCompactControls(bool dark, int width, int height) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync();
        await fixture.Model.Inspection.Files.LoadCommand.ExecuteAsync(null);
        var view = new PullRequestsView { DataContext = fixture.Model };
        var window = new Window { Content = view, Width = width, Height = height, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            var columns = Assert.Single(view.GetVisualDescendants().OfType<AppResponsiveColumns>());
            Assert.Equal(width < 636, columns.IsCompact);
            if (!columns.IsCompact) Assert.True(columns.Children[1].Bounds.Width > columns.Children[0].Bounds.Width);
            else
            {
                Assert.False(columns.Children[0].IsVisible); Assert.Equal(columns.Bounds.Height, columns.Children[1].Bounds.Height);
                var back = Assert.Single(view.GetVisualDescendants().OfType<AppButton>(), button => button.Command == fixture.Model.BackCommand);
                Assert.True(back.IsEffectivelyVisible);
                Assert.InRange(back.TranslatePoint(default, window)!.Value.Y, 0, height - back.Bounds.Height);
            }
            Assert.Contains(view.GetVisualDescendants().OfType<PullRequestInspectionView>(), control => control.IsEffectivelyVisible);
            using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
            if (Environment.GetEnvironmentVariable("MOSSAGENT_UI_EVIDENCE_DIRECTORY") is { Length: > 0 } folder)
            { Directory.CreateDirectory(folder); frame.Save(Path.Combine(folder, $"pr-page-{dark}-{width}-{height}.png"), PngBitmapEncoderOptions.Default); }
            if (columns.IsCompact)
            {
                fixture.Model.BackCommand.Execute(null); window.UpdateLayout();
                Assert.True(columns.Children[0].IsVisible); Assert.False(columns.Children[1].IsVisible);
                Assert.Null(fixture.Model.Detail); Assert.Empty(fixture.Model.Inspection.Files.Items);
            }
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
