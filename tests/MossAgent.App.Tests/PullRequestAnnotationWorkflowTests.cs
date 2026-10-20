using System.Net;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestAnnotationWorkflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 526, "RIGHT")]
    [InlineData(true, 526, "LEFT")]
    [InlineData(false, 980, "LEFT")]
    [InlineData(true, 980, "RIGHT")]
    public Task KeyboardLocationShowsAndFocusesTheValidatedInlineCard(bool dark, int width, string side) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync();
        fixture.Source.Transport.RespondAsync = (request, token) => request.RequestUri!.AbsolutePath.EndsWith("/comments", StringComparison.Ordinal)
            ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"[{PullRequestTestData.Comment(side: side)}]") })
            : PullRequestInspectionUiFixture.RespondAsync(request, token);
        var model = fixture.Model.Inspection;
        await model.Files.LoadCommand.ExecuteAsync(null); await model.Inline.LoadCommand.ExecuteAsync(null);
        model.SelectedSection = model.Sections.Single(section => section.Key == "inline");
        var view = new PullRequestInspectionView { DataContext = model };
        var window = new Window { Width = width, Height = 640, Content = new AppScrollViewer { Content = view },
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            var anchor = Assert.Single(model.Annotations.Comments);
            Assert.Equal(PullRequestCommentLocationState.Current, anchor.State);
            var requests = fixture.Source.Transport.RequestCount;
            var button = Assert.Single(view.GetVisualDescendants().OfType<AppButton>(), item => item.Command == anchor.LocateCommand);
            Assert.True(button.Focus(NavigationMethod.Tab));
            button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
            button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
            window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.True(model.IsFiles); Assert.Equal(requests, fixture.Source.Transport.RequestCount);
            Assert.False(Assert.Single(view.GetVisualDescendants().OfType<PullRequestInlineCommentsView>()).IsEffectivelyVisible);
            var preview = Assert.Single(view.GetVisualDescendants().OfType<AppCodePreview>());
            Assert.Equal(side, Assert.Single(preview.Annotations).Side);
            Assert.Equal(11, preview.AnnotationSelection!.Id);
            var row = Assert.Single(preview.GetVisualDescendants().OfType<AppBorder>(), item => item.Classes.Contains("annotation-target"));
            Assert.True(Assert.IsType<CodePreviewLine>(row.DataContext).IsAnnotationTarget);
            var card = Assert.Single(row.GetVisualDescendants().OfType<AppCard>());
            Assert.Same(card, window.FocusManager!.GetFocusedElement());
            Assert.Contains(card.GetVisualDescendants().OfType<AppSelectableText>(), text => text.Text == "Untrusted comment");
            using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
            if (Environment.GetEnvironmentVariable("MOSSAGENT_UI_EVIDENCE_DIRECTORY") is { Length: > 0 } folder)
            { Directory.CreateDirectory(folder); frame.Save(Path.Combine(folder, $"pr-inline-{dark}-{width}-{side}.png"), PngBitmapEncoderOptions.Default); }
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("history", PullRequestCommentLocationState.Historical)]
    [InlineData("line", PullRequestCommentLocationState.InvalidLine)]
    [InlineData("side", PullRequestCommentLocationState.MissingLine)]
    [InlineData("path", PullRequestCommentLocationState.FileNotLoaded)]
    [InlineData("missing", PullRequestCommentLocationState.MissingLine)]
    [InlineData("patch", PullRequestCommentLocationState.PatchUnavailable)]
    public Task UnlocatableCommentsKeepTheirTextWithoutMarkers(string kind, PullRequestCommentLocationState expected) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync();
        fixture.Source.Transport.RespondAsync = (request, token) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/comments", StringComparison.Ordinal)) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent($"[{PullRequestTestData.Comment(commit: kind == "history" ? new string('c', 40) : null,
                line: kind == "missing" ? null : kind == "line" ? 99 : 1, side: kind == "side" ? null : "RIGHT", path: kind == "path" ? "SRC/sample.cs" : "src/sample.cs")}]") });
            if (kind == "patch" && path.EndsWith("/files", StringComparison.Ordinal)) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent($"[{PullRequestTestData.File(patch: "@@ -1 +1 @@\n-old")}]") });
            return PullRequestInspectionUiFixture.RespondAsync(request, token);
        };
        var model = fixture.Model.Inspection;
        await model.Files.LoadCommand.ExecuteAsync(null); await model.Inline.LoadCommand.ExecuteAsync(null);
        var anchor = Assert.Single(model.Annotations.Comments);
        Assert.Equal(expected, anchor.State); Assert.Equal("Untrusted comment", anchor.Comment.Body);
        Assert.False(anchor.LocateCommand.CanExecute(null)); Assert.Empty(model.Annotations.FileAnnotations);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task FileAndCommentPagesNeverKeepStaleAnchors() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync();
        fixture.Source.Transport.RespondAsync = (request, token) =>
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/files", StringComparison.Ordinal)) return PullRequestInspectionUiFixture.RespondAsync(request, token);
            var files = request.RequestUri.Query.EndsWith("page=1", StringComparison.Ordinal) ? "[" + string.Join(',',
                Enumerable.Range(1, 20).Select(index => PullRequestTestData.File(path: index == 1 ? "src/sample.cs" : $"other-{index}.cs"))) + "]" : "[]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(files) });
        };
        var model = fixture.Model.Inspection;
        await model.Inline.LoadCommand.ExecuteAsync(null);
        Assert.Equal(PullRequestCommentLocationState.FileNotLoaded, Assert.Single(model.Annotations.Comments).State);
        await model.Files.LoadCommand.ExecuteAsync(null);
        var old = Assert.Single(model.Annotations.Comments);
        Assert.Single(model.Annotations.FileAnnotations); Assert.True(old.LocateCommand.CanExecute(null));
        await model.Files.NextCommand.ExecuteAsync(null);
        Assert.Empty(model.Annotations.FileAnnotations); Assert.False(old.LocateCommand.CanExecute(null));
        Assert.Equal(PullRequestCommentLocationState.FileNotLoaded, Assert.Single(model.Annotations.Comments).State);
        await model.Files.PreviousCommand.ExecuteAsync(null);
        Assert.Single(model.Annotations.FileAnnotations);
        fixture.Model.Leave();
        Assert.Empty(model.Annotations.Comments); Assert.Empty(model.Annotations.FileAnnotations); Assert.Null(model.Annotations.Selection);
    }, TestContext.Current.CancellationToken);
}
