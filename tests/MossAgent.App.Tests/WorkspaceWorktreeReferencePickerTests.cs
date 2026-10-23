using MossAgent.App.ViewModels;
using MossAgent.Application.Git;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceWorktreeReferencePickerTests
{
    [Fact]
    public void UnavailableDiscovery_KeepsManualInputWithoutPicker()
    {
        var environment = new WorkspaceTaskEnvironmentViewModel();
        environment.SetContext(CreateProject(), null);
        Assert.False(environment.References.IsAvailable);
        environment.IsPickerOpen = true;
        Assert.True(environment.IsPickerOpen);
        environment.BaseReference = "manual";
        Assert.Equal("manual", environment.BaseReference);
    }

    [Fact]
    public async Task OpeningPicker_LoadsLocalReferences()
    {
        var discovery = new StubDiscovery();
        var environment = new WorkspaceTaskEnvironmentViewModel(discovery);
        environment.SetContext(CreateProject(), null);

        environment.IsPickerOpen = true;
        await WaitUntilAsync(() => environment.References.Items.Count == 2 && !environment.References.IsBusy);

        Assert.Equal("ref-0", environment.References.Items[0].Name);
        Assert.Equal("ref-0 · 分支", environment.References.Items[0].Display);
        Assert.StartsWith("aaaaaaaa", environment.References.Items[0].Preview);
        Assert.Null(environment.References.StatusText);
        Assert.False(environment.References.HasStatus);
    }

    [Fact]
    public async Task SelectingReference_FillsManualInputWithFullRefName()
    {
        var discovery = new StubDiscovery();
        var environment = new WorkspaceTaskEnvironmentViewModel(discovery);
        environment.SetContext(CreateProject(), null);
        await environment.References.RefreshAsync();

        environment.References.SelectedItem = environment.References.Items[1];

        Assert.Equal("refs/heads/ref-1", environment.BaseReference);
    }

    [Fact]
    public async Task SearchText_ResetsPagingAndRefreshes()
    {
        var discovery = new StubDiscovery();
        var environment = new WorkspaceTaskEnvironmentViewModel(discovery);
        environment.References.SearchDelay = TimeSpan.Zero;
        environment.SetContext(CreateProject(), null);
        await environment.References.RefreshAsync();

        environment.References.SearchText = "feat";
        await WaitUntilAsync(() => discovery.Requests.Any(request => request.Search == "feat"));

        Assert.Equal(0, discovery.Requests[^1].Skip);
    }

    [Fact]
    public async Task PagingCommands_AdvanceWithinResults()
    {
        var discovery = new StubDiscovery((_, skip) => Task.FromResult(
            skip == 0 ? StubDiscovery.Page(20, 0, hasMore: true) : StubDiscovery.Page(5, skip)));
        var environment = new WorkspaceTaskEnvironmentViewModel(discovery);
        environment.SetContext(CreateProject(), null);
        await environment.References.RefreshAsync();

        Assert.True(environment.References.HasMore);
        await environment.References.NextPageCommand.ExecuteAsync(null);
        Assert.Equal(20, discovery.Requests[^1].Skip);
        Assert.Equal(5, environment.References.Items.Count);
        await environment.References.PreviousPageCommand.ExecuteAsync(null);
        Assert.Equal(0, discovery.Requests[^1].Skip);
        Assert.Null(environment.References.SelectedItem);
    }

    [Fact]
    public async Task LockedEnvironment_DoesNotLoadOrSelect()
    {
        var discovery = new StubDiscovery();
        var environment = new WorkspaceTaskEnvironmentViewModel(discovery);
        environment.SetContext(CreateProject(), null);
        environment.SetLocked(true);

        environment.IsPickerOpen = true;
        await environment.References.RefreshAsync();

        Assert.False(environment.IsPickerOpen);
        Assert.Empty(discovery.Requests);
        environment.References.SelectedItem = new WorkspaceWorktreeReferenceItemViewModel(StubDiscovery.Reference(0));
        Assert.Equal(string.Empty, environment.BaseReference);
    }

    [Fact]
    public async Task ExistingTaskContext_DoesNotLoad()
    {
        var discovery = new StubDiscovery();
        var environment = new WorkspaceTaskEnvironmentViewModel(discovery);
        var project = CreateProject();
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid(), project.Id, "existing", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Completed, now, now, null);
        environment.SetContext(project, task);

        environment.IsPickerOpen = true;
        await environment.References.RefreshAsync();

        Assert.False(environment.CanChoose);
        Assert.Empty(discovery.Requests);
    }

    [Fact]
    public async Task ContextChange_ClearsSearchAndItems()
    {
        var discovery = new StubDiscovery();
        var environment = new WorkspaceTaskEnvironmentViewModel(discovery);
        environment.SetContext(CreateProject(), null);
        await environment.References.RefreshAsync();
        Assert.NotEmpty(environment.References.Items);

        environment.SetContext(CreateProject(), null);

        Assert.Empty(environment.References.Items);
        Assert.Equal(string.Empty, environment.References.SearchText);
        Assert.Null(environment.References.SelectedItem);
    }

    [Fact]
    public async Task DiscoveryFailure_ShowsStatusAndKeepsManualInput()
    {
        var discovery = new StubDiscovery((_, _) =>
            Task.FromException<GitWorktreeReferencePage>(new InvalidOperationException("synthetic")));
        var environment = new WorkspaceTaskEnvironmentViewModel(discovery);
        environment.SetContext(CreateProject(), null);
        await environment.References.RefreshAsync();

        Assert.Empty(environment.References.Items);
        Assert.Equal(WorkspaceText.Get("TaskEnvironmentReferenceFailure"), environment.References.StatusText);
        environment.BaseReference = "manual";
        Assert.Equal("manual", environment.BaseReference);
    }

    [Fact]
    public async Task UnauthorizedFailure_ShowsAuthorizationStatus()
    {
        var discovery = new StubDiscovery((_, _) =>
            Task.FromException<GitWorktreeReferencePage>(new UnauthorizedAccessException("synthetic")));
        var environment = new WorkspaceTaskEnvironmentViewModel(discovery);
        environment.SetContext(CreateProject(), null);
        await environment.References.RefreshAsync();

        Assert.Equal(WorkspaceText.Get("TaskEnvironmentReferenceAuthorizationFailure"), environment.References.StatusText);
    }

    [Fact]
    public async Task EmptyResult_ShowsEmptyStatus()
    {
        var discovery = new StubDiscovery((_, _) =>
            Task.FromResult(new GitWorktreeReferencePage([], 0, false, false)));
        var environment = new WorkspaceTaskEnvironmentViewModel(discovery);
        environment.SetContext(CreateProject(), null);
        await environment.References.RefreshAsync();

        Assert.Equal(WorkspaceText.Get("TaskEnvironmentReferenceEmpty"), environment.References.StatusText);
    }

    [Fact]
    public async Task TruncatedPage_ShowsTruncatedStatus()
    {
        var discovery = new StubDiscovery((_, _) =>
            Task.FromResult(StubDiscovery.Page(20, 0, truncated: true)));
        var environment = new WorkspaceTaskEnvironmentViewModel(discovery);
        environment.SetContext(CreateProject(), null);
        await environment.References.RefreshAsync();

        Assert.Equal(WorkspaceText.Get("TaskEnvironmentReferenceTruncated"), environment.References.StatusText);
    }

    private static ProjectProfile CreateProject() => new(Guid.NewGuid(), "project", "project-root",
        ["project-root"], DateTimeOffset.UtcNow);

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        for (var attempt = 0; attempt < 200 && !predicate(); attempt++)
            await Task.Delay(10, TestContext.Current.CancellationToken);
        Assert.True(predicate());
    }

    private sealed class StubDiscovery : IGitWorktreeReferenceDiscovery
    {
        private readonly Func<string?, int, Task<GitWorktreeReferencePage>>? _handler;

        public StubDiscovery(Func<string?, int, Task<GitWorktreeReferencePage>>? handler = null) => _handler = handler;

        public List<(string? Search, int Skip, int Take)> Requests { get; } = [];

        public Task<GitWorktreeReferencePage> SearchAsync(ProjectProfile project, string? search, int skip, int take,
            CancellationToken cancellationToken)
        {
            Requests.Add((search, skip, take));
            return _handler?.Invoke(search, skip) ?? Task.FromResult(Page());
        }

        public static GitWorktreeReference Reference(int index) => new($"ref-{index}", $"refs/heads/ref-{index}",
            GitWorktreeReferenceKind.Branch, new string('a', 40), "subject");

        public static GitWorktreeReferencePage Page(int count = 2, int skip = 0, bool hasMore = false,
            bool truncated = false)
        {
            var items = Enumerable.Range(0, count).Select(index => Reference(skip + index)).ToArray();
            return new GitWorktreeReferencePage(items, skip + count, hasMore, truncated);
        }
    }
}
