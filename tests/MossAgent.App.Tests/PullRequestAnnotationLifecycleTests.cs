using System.Net;
using Avalonia.Headless;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestAnnotationLifecycleTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ObservedHeadOrGrantChangesRemoveAllAnnotations(bool revoke) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync();
        var model = fixture.Model.Inspection;
        await model.Files.LoadCommand.ExecuteAsync(null); await model.Inline.LoadCommand.ExecuteAsync(null);
        var old = Assert.Single(model.Annotations.Comments);
        Assert.Single(model.Annotations.FileAnnotations);
        var scope = fixture.Model.SelectedRepository!.Scope;
        fixture.Source.Transport.RespondAsync = async (_, token) =>
        {
            if (revoke) await fixture.Source.Grants.RevokeAsync(scope.GrantId, token);
            var body = revoke ? PullRequestTestData.Item() : PullRequestTestData.Item().Replace(new string('b', 40), new string('c', 40), StringComparison.Ordinal);
            return new(HttpStatusCode.OK) { Content = new StringContent(body) };
        };
        await model.Runs.LoadCommand.ExecuteAsync(null);
        Assert.Empty(model.Annotations.FileAnnotations); Assert.Empty(model.Annotations.Comments);
        Assert.False(old.LocateCommand.CanExecute(null)); Assert.Null(model.Annotations.Selection);
        Assert.False(model.Files.LoadCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CommentPaginationInvalidatesCardsAndOldNavigationCommands() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync();
        fixture.Source.Transport.RespondAsync = (request, token) =>
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/comments", StringComparison.Ordinal)) return PullRequestInspectionUiFixture.RespondAsync(request, token);
            var body = request.RequestUri.Query.EndsWith("page=1", StringComparison.Ordinal) ? "[" + string.Join(',',
                Enumerable.Range(1, 20).Select(index => PullRequestTestData.Comment(id: index))) + "]" : "[]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        };
        var model = fixture.Model.Inspection;
        await model.Files.LoadCommand.ExecuteAsync(null); await model.Inline.LoadCommand.ExecuteAsync(null);
        Assert.Equal(20, model.Annotations.FileAnnotations.Count);
        var old = model.Annotations.Comments[0];
        old.LocateCommand.Execute(null); var sequence = model.Annotations.Selection!.Sequence;
        old.LocateCommand.Execute(null); Assert.True(model.Annotations.Selection!.Sequence > sequence);
        await model.Inline.NextCommand.ExecuteAsync(null);
        Assert.Empty(model.Annotations.FileAnnotations); Assert.Empty(model.Annotations.Comments);
        Assert.False(old.LocateCommand.CanExecute(null)); Assert.Null(model.Annotations.Selection);
    }, TestContext.Current.CancellationToken);
    [Fact]
    public Task MultiLineMetadataReachesTheVerifiedEndLineAnnotation() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestInspectionUiFixture.CreateAsync();
        fixture.Source.Transport.RespondAsync = (request, token) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = path.EndsWith("/files", StringComparison.Ordinal)
                ? $"[{PullRequestTestData.File("@@ -1,2 +1,2 @@\n-one\n-two\n+first\n+second", additions: 2, deletions: 2)}]"
                : path.EndsWith("/comments", StringComparison.Ordinal)
                    ? $"[{PullRequestTestData.Comment(line: 2, start: 1, startSide: "RIGHT")}]" : null;
            return body is null ? PullRequestInspectionUiFixture.RespondAsync(request, token)
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        };
        var model = fixture.Model.Inspection;
        await model.Files.LoadCommand.ExecuteAsync(null); await model.Inline.LoadCommand.ExecuteAsync(null);
        var anchor = Assert.Single(model.Annotations.Comments);
        Assert.Equal(PullRequestCommentLocationState.Current, anchor.State);
        Assert.Contains("1", anchor.LocationLabel, StringComparison.Ordinal); Assert.Contains("2", anchor.LocationLabel, StringComparison.Ordinal);
        var annotation = Assert.Single(model.Annotations.FileAnnotations);
        Assert.Equal(1, annotation.StartLine); Assert.Equal(2, annotation.Line);
        anchor.LocateCommand.Execute(null); Assert.True(model.IsFiles); Assert.Equal(annotation.Id, model.Annotations.Selection!.Id);
    }, TestContext.Current.CancellationToken);
}
