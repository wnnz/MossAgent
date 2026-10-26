using System.Net;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestInspectionWorkflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 526, 601)]
    [InlineData(true, 526, 601)]
    [InlineData(false, 980, 740)]
    [InlineData(true, 980, 740)]
    public Task ProductionPipelineRendersVersionBoundFilesCommentsAndChecks(bool dark, int width, int height) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync();
        var inspection = fixture.Model.Inspection;
        var view = new PullRequestInspectionView { DataContext = inspection };
        var window = new Window { Content = new AppScrollViewer { Content = view }, Width = width, Height = height,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            Press(view, inspection.Files.LoadCommand);
            await inspection.Files.LoadCommand.ExecutionTask!;
            Assert.Equal("src/sample.cs", Assert.Single(inspection.Files.Items).Path);
            Assert.Equal(PullRequestText.Get("PatchAvailable"), inspection.PatchHint);
            window.UpdateLayout();
            var preview = Assert.Single(view.GetVisualDescendants().OfType<AppCodePreview>());
            Assert.Contains("+new", preview.Code, StringComparison.Ordinal);
            Assert.True(preview.IsDiff);
            foreach (var section in inspection.Sections.Skip(1))
            {
                inspection.SelectedSection = section;
                var command = section.Key switch
                {
                    "conversation" or "inline" => inspection.CurrentComments.LoadCommand,
                    "reviews" => inspection.Reviews.LoadCommand,
                    "activity" => inspection.Activity.LoadCommand,
                    _ => inspection.CurrentChecks.LoadCommand
                };
                window.UpdateLayout(); Press(view, command); await command.ExecutionTask!;
                switch (section.Key)
                {
                    case "conversation" or "inline": Assert.Equal("Untrusted comment", Assert.Single(inspection.CurrentComments.Items).Body); break;
                    case "reviews": Assert.Equal("Untrusted review", Assert.Single(inspection.Reviews.Items).Body); break;
                    case "activity": Assert.Equal("labeled", Assert.Single(inspection.Activity.Items).Event); break;
                    default: Assert.Single(inspection.CurrentChecks.Items); break;
                }
            }
            var auditId = await fixture.Source.ReviewSessions.GetOrCreateAsync(fixture.Model.SelectedRepository!.Scope, fixture.Token);
            var records = await fixture.Source.Audit.GetRecentAsync(auditId, 20, fixture.Token);
            Assert.Equal(9, records.Count);
            Assert.All(records, record => { Assert.True(record.IsSuccess); Assert.NotNull(record.CompletedAt); });
            Assert.Equal(23, fixture.Source.Transport.RequestCount);
            inspection.SelectedSection = inspection.Sections[0]; window.UpdateLayout();
            var selector = Assert.Single(view.GetVisualDescendants().OfType<AppComboBox>(),
                box => AutomationProperties.GetName(box) == PullRequestText.Inspection);
            Assert.True(selector.Focus(NavigationMethod.Tab));
            var origin = selector.TranslatePoint(default, window)!.Value;
            Assert.True(new Rect(window.ClientSize).Contains(new Rect(origin, selector.Bounds.Size)));
            using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
            if (Environment.GetEnvironmentVariable("MOSSAGENT_UI_EVIDENCE_DIRECTORY") is { Length: > 0 } folder)
            { Directory.CreateDirectory(folder); frame.Save(Path.Combine(folder, $"pr-inspection-{dark}-{width}-{height}.png"), PngBitmapEncoderOptions.Default); }
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RevisionOrScopeChangeClearsEverySectionAndRequiresFreshDetails(bool revoke) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync();
        var inspection = fixture.Model.Inspection;
        await inspection.Conversation.LoadCommand.ExecuteAsync(null);
        Assert.Single(inspection.Conversation.Items);
        var scope = fixture.Model.SelectedRepository!.Scope;
        fixture.Source.Transport.RespondAsync = async (request, token) =>
        {
            if (revoke) await fixture.Source.Grants.RevokeAsync(scope.GrantId, token);
            var body = revoke ? PullRequestTestData.Item() : PullRequestTestData.Item().Replace(new string('b', 40), new string('c', 40), StringComparison.Ordinal);
            return new(HttpStatusCode.OK) { Content = new StringContent(body) };
        };
        await inspection.Files.LoadCommand.ExecuteAsync(null);
        Assert.Empty(inspection.Files.Items); Assert.Empty(inspection.Conversation.Items);
        Assert.False(inspection.Files.LoadCommand.CanExecute(null));
        Assert.False(inspection.Runs.LoadCommand.CanExecute(null));
        Assert.Equal(PullRequestText.Get(revoke ? "ScopeDenied" : "RevisionChanged"), inspection.Status);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("leave")]
    [InlineData("select")]
    [InlineData("cancel")]
    public Task InflightInspectionIsCancelledAndCannotRepopulateAfterNavigation(string action) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync();
        var inspection = fixture.Model.Inspection;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleaned = false;
        fixture.Source.Transport.RespondAsync = async (request, token) =>
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/files", StringComparison.Ordinal)) return await PullRequestInspectionUiFixture.RespondAsync(request, token);
            entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { cleaned = true; }
            return new(HttpStatusCode.OK);
        };
        var work = inspection.Files.LoadCommand.ExecuteAsync(null);
        await entered.Task.WaitAsync(fixture.Token);
        if (action == "leave") fixture.Model.Leave();
        else if (action == "select") fixture.Model.SelectedItem = null;
        else inspection.Files.CancelCommand.Execute(null);
        await work;
        Assert.True(cleaned);
        Assert.False(inspection.Files.IsBusy); Assert.Empty(inspection.Files.Items);
        Assert.Equal(4, fixture.Source.Transport.DisposedHandlers);
        Assert.Equal(action == "cancel", inspection.Files.LoadCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    private static void Press(Control view, System.Windows.Input.ICommand command)
    {
        var button = Assert.Single(view.GetVisualDescendants().OfType<AppButton>(), button => button.Command == command);
        Assert.True(button.IsEffectivelyEnabled); Assert.True(button.Focus(NavigationMethod.Tab));
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
