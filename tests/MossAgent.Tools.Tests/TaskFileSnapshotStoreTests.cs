using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class TaskFileSnapshotStoreTests
{
    [Fact]
    public void StoresOnlyVersionMetadataAndRejectsCrossTaskReuse()
    {
        var taskId = Guid.NewGuid();
        var store = new TaskFileSnapshotStore(taskId);
        var original = new FileSnapshot("metadata-only");
        store.Record(taskId, "/project/file", original);

        Assert.Same(original, store.Get(taskId, "/project/file"));
        Assert.Throws<InvalidOperationException>(() => store.Get(Guid.NewGuid(), "/project/file"));
        Assert.Throws<InvalidOperationException>(() => store.Record(Guid.NewGuid(), "/project/file", new FileSnapshot(null)));
        Assert.Throws<InvalidOperationException>(() => store.Forget(Guid.NewGuid(), "/project"));
        Assert.Same(original, store.Get(taskId, "/project/file"));
    }

    [Fact]
    public void ForgetInvalidatesOnlyRequestedPathAndItsDescendants()
    {
        var taskId = Guid.NewGuid();
        var store = new TaskFileSnapshotStore(taskId);
        foreach (var path in new[] { "/project/folder", "/project/folder/file", "/project/folder-other/file" })
            store.Record(taskId, path, new FileSnapshot(path));

        store.Forget(taskId, "/project/folder");

        Assert.Null(store.Get(taskId, "/project/folder"));
        Assert.Null(store.Get(taskId, "/project/folder/file"));
        Assert.NotNull(store.Get(taskId, "/project/folder-other/file"));
    }

    [Fact]
    public void CapacityLimitFailsClosedWithoutEvictingEarlierReads()
    {
        var taskId = Guid.NewGuid();
        var store = new TaskFileSnapshotStore(taskId);
        for (var index = 0; index < 4096; index++) store.Record(taskId, $"/{index}", new FileSnapshot("version"));

        Assert.Throws<InvalidOperationException>(() => store.Record(taskId, "/overflow", new FileSnapshot("version")));
        Assert.NotNull(store.Get(taskId, "/0"));
        store.Record(taskId, "/0", new FileSnapshot("updated"));
        Assert.Equal("updated", store.Get(taskId, "/0")?.Sha256);
    }
}
