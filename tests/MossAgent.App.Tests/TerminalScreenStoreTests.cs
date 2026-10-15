using MossAgent.App.Terminal;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class TerminalScreenStoreTests
{
    private static ToolExecutionContext Context() => new(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);
    private static TerminalSessionSnapshot State(Guid task, Guid session, long revision) => new(task, session, revision, TerminalSessionState.Running);
    private static string Text(TerminalFrame? frame) => string.Concat(frame!.Rows.SelectMany(row => row).Select(cell => cell.Text));

    [Fact]
    public void OnlyAdmittedTasksAreCollectedAndSwitchingRestoresBackgroundOutput()
    {
        using var store = new TerminalScreenStore(false);
        var first = Context();
        var second = Context();
        Assert.False(store.Append(new(first.TaskId, "not admitted")));
        store.Attach(first, null);
        Assert.True(store.Append(new(first.TaskId, "first")));
        var original = store.Capture()!;
        var view = original.ViewId;
        Assert.True(store.IsCurrent(original));
        store.Attach(second, null);
        Assert.True(store.Append(new(first.TaskId, " background")));
        Assert.True(store.Append(new(second.TaskId, "second")));
        Assert.Equal("second", Text(store.Capture()));
        Assert.False(store.IsCurrent(original));
        store.Attach(first, null);
        Assert.Equal(view, store.Capture()!.ViewId);
        Assert.True(store.IsCurrent(original));
        Assert.Equal("first background", Text(store.Capture()));
    }

    [Fact]
    public void SessionReplacementRejectsLateOrUntaggedOutputAndOlderMetadata()
    {
        using var store = new TerminalScreenStore(true);
        var context = Context();
        var old = Guid.NewGuid();
        var current = Guid.NewGuid();
        store.Attach(context, State(context.TaskId, old, 1));
        Assert.True(store.Append(new(context.TaskId, "old", old)));
        var original = store.Capture()!;
        var view = original.ViewId;
        Assert.True(store.ApplyState(State(context.TaskId, current, 2)));
        Assert.False(store.IsCurrent(original));
        Assert.False(store.ApplyState(State(context.TaskId, old, 1)));
        Assert.False(store.Append(new(context.TaskId, "late", old)));
        Assert.False(store.Append(new(context.TaskId, "untagged")));
        Assert.True(store.Append(new(context.TaskId, "current", current)));
        Assert.NotEqual(view, store.Capture()!.ViewId);
        Assert.Equal("current", Text(store.Capture()));
    }

    [Theory]
    [InlineData("directory")]
    [InlineData("roots")]
    [InlineData("policy")]
    public void AuthorizationScopeChangeDiscardsHistoryAndBlocksPriorSession(string change)
    {
        using var store = new TerminalScreenStore(true);
        var context = Context();
        var old = Guid.NewGuid();
        var state = State(context.TaskId, old, 1);
        store.Attach(context, state);
        store.Append(new(context.TaskId, "old scope", old));
        var next = change switch
        {
            "directory" => context with { WorkingDirectory = "D:\\other" },
            "roots" => context with { AuthorizedRoots = ["D:\\other"] },
            _ => context with { ApprovalPolicy = ApprovalPolicy.ReadOnly }
        };
        store.Attach(next, state);
        Assert.Equal(string.Empty, Text(store.Capture()));
        Assert.False(store.Append(new(context.TaskId, "late old scope", old), state));
        var replacement = Guid.NewGuid();
        Assert.True(store.Append(new(context.TaskId, "new scope", replacement), State(context.TaskId, replacement, 2)));
        Assert.Equal("new scope", Text(store.Capture()));
    }

    [Fact]
    public void LegacySourceCannotRepopulateChangedScopeWithUntaggedOutput()
    {
        using var store = new TerminalScreenStore(false);
        var context = Context();
        store.Attach(context, null);
        store.Append(new(context.TaskId, "old"));
        store.Attach(context with { AuthorizedRoots = [] }, null);
        Assert.False(store.Append(new(context.TaskId, "late")));
        Assert.Equal(string.Empty, Text(store.Capture()));
    }

    [Fact]
    public void CacheEvictionAndDisposalRejectLateOutput()
    {
        using var store = new TerminalScreenStore(false);
        var first = Context();
        store.Attach(first, null);
        store.Append(new(first.TaskId, "first"));
        for (var index = 0; index < TerminalScreenStore.MaximumScreens; index++) store.Attach(Context(), null);
        Assert.False(store.Append(new(first.TaskId, "evicted")));
        store.Attach(first, null);
        Assert.Equal(string.Empty, Text(store.Capture()));
        store.Dispose();
        Assert.Null(store.Capture());
        Assert.False(store.Append(new(first.TaskId, "disposed")));
        store.Attach(first, null);
        Assert.Null(store.Capture());
    }

    [Fact]
    public async Task ConcurrentProducerChunksAreSerializedWithoutLosingCharacters()
    {
        using var store = new TerminalScreenStore(false);
        var context = Context();
        store.Attach(context, null);
        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => store.Append(new(context.TaskId, new string('x', 100))), TestContext.Current.CancellationToken)));
        Assert.Equal(3200, Text(store.Capture()).Length);
    }

    [Fact]
    public void AuthorizationsAreCopiedInsteadOfRetainingMutableRootLists()
    {
        using var store = new TerminalScreenStore(false);
        var roots = new List<string> { "D:\\synthetic" };
        var context = Context() with { AuthorizedRoots = roots };
        store.Attach(context, null);
        store.Append(new(context.TaskId, "old"));
        roots.Clear();
        store.Attach(context, null);
        Assert.Equal(string.Empty, Text(store.Capture()));
        Assert.False(store.Append(new(context.TaskId, "late")));
    }
}
