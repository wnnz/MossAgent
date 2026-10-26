using System.Text.Json;
using Avalonia.Headless;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestInspectionSectionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();
    private static PullRequestRevision Revision => new(7, new string('a', 40), new string('b', 40));
    private static ToolExecutionContext Context() => new(Guid.NewGuid(), string.Empty, [], ApprovalPolicy.ReadOnly)
    { SourceScope = new(Guid.NewGuid(), Guid.NewGuid(), 1, "fixture", "repo", false) };

    [Fact]
    public Task SectionsExposeAllSevenPagesInOrderAndRouteCurrentPage() => _session.DispatchAsync(async () =>
    {
        var executor = new InspectorProbeToolExecutor();
        await using var inspection = new PullRequestInspectionViewModel(executor);
        Assert.Equal(new[] { "files", "conversation", "inline", "reviews", "activity", "runs", "statuses" },
            inspection.Sections.Select(section => section.Key).ToArray());
        Assert.All(inspection.Sections, section => Assert.False(string.IsNullOrWhiteSpace(section.Name)));
        inspection.SelectedSection = inspection.Sections[3];
        Assert.True(inspection.IsReviews); Assert.False(inspection.IsActivity); Assert.False(inspection.IsChecks);
        Assert.Same(inspection.Reviews, inspection.CurrentPage);
        inspection.SelectedSection = inspection.Sections[4];
        Assert.True(inspection.IsActivity); Assert.False(inspection.IsReviews);
        Assert.Same(inspection.Activity, inspection.CurrentPage);
        inspection.SelectedSection = inspection.Sections[0];
        Assert.Same(inspection.Files, inspection.CurrentPage);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AttachEnablesAndClearResetsReviewAndActivityReads() => _session.DispatchAsync(async () =>
    {
        var calls = new List<string>();
        var executor = new InspectorProbeToolExecutor { Execute = (request, context, token) =>
        {
            calls.Add(request.ToolName);
            object page = request.ToolName == "source.pr_reviews"
                ? new PullRequestInspectionPage<PullRequestReview>(Revision,
                    [new PullRequestReview(31, "reviewer", "APPROVED", "Looks fine", null, null)], 1, false)
                : new PullRequestInspectionPage<PullRequestActivity>(Revision,
                    [new PullRequestActivity(41, "labeled", "actor", "bug", null, null)], 1, false);
            return Task.FromResult(ToolResult.Success("synthetic", JsonSerializer.Serialize(page)));
        } };
        await using var inspection = new PullRequestInspectionViewModel(executor);
        Assert.False(inspection.Reviews.LoadCommand.CanExecute(null));
        inspection.Attach(Context(), Revision);
        Assert.True(inspection.Reviews.LoadCommand.CanExecute(null));
        Assert.True(inspection.Activity.LoadCommand.CanExecute(null));
        await inspection.Reviews.LoadCommand.ExecuteAsync(null);
        await inspection.Activity.LoadCommand.ExecuteAsync(null);
        Assert.Equal("APPROVED", Assert.Single(inspection.Reviews.Items).State);
        Assert.Equal("labeled", Assert.Single(inspection.Activity.Items).Event);
        Assert.Equal(new[] { "source.pr_reviews", "source.pr_activity" }, calls);
        inspection.Clear();
        Assert.Empty(inspection.Reviews.Items); Assert.Empty(inspection.Activity.Items);
        Assert.False(inspection.Reviews.LoadCommand.CanExecute(null));
        Assert.False(inspection.Activity.LoadCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RevisionFailureClearsEverySectionAndReportsStatus() => _session.DispatchAsync(async () =>
    {
        var executor = new InspectorProbeToolExecutor { Execute = (request, context, token) =>
            Task.FromResult(ToolResult.Failure("synthetic", "source_revision_changed")) };
        await using var inspection = new PullRequestInspectionViewModel(executor);
        inspection.Attach(Context(), Revision);
        await inspection.Reviews.LoadCommand.ExecuteAsync(null);
        Assert.False(inspection.IsActive);
        Assert.Empty(inspection.Reviews.Items);
        Assert.Equal(PullRequestText.Get("RevisionChanged"), inspection.Status);
    }, TestContext.Current.CancellationToken);
}
