using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AtomicWriteCompositionTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task SavedModelThroughAgentUsesRegisteredSessionAndPersistsResult(bool replace, bool deny)
    {
        var sessions = new TrackedAtomicWriteSessions(deny);
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(replace, "beta\n", configure: services =>
        {
            Assert.Equal(typeof(WindowsAtomicFileWriteSessionFactory),
                Assert.Single(services, service => service.ServiceType == typeof(IAtomicFileWriteSessionFactory)).ImplementationType);
            services.Replace(ServiceDescriptor.Singleton<IAtomicFileWriteSessionFactory>(sessions));
        });
        fixture.Start(ApprovalPolicy.FullAccess);
        await fixture.WaitForCompletionAsync();

        Assert.Equal(1, sessions.Creates - sessions.ArtifactCreates);
        Assert.Equal(deny ? 0 : 2, sessions.ArtifactCreates);
        Assert.Equal(deny ? "alpha\n" : "beta\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        Assert.Equal("outside sentinel", await File.ReadAllTextAsync(fixture.OutsidePath, fixture.Token));
        await fixture.AssertPersistedAsync(deny ? "path_denied" : null, true, AgentTaskStatus.Completed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnsupportedMetadataRefusalIsVisibleAndPersistedForNativeFactory(bool replace)
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(replace, "beta\n");
        await File.WriteAllTextAsync(fixture.FilePath + ":metadata", "retained stream", fixture.Token);
        fixture.Start(ApprovalPolicy.FullAccess);
        await fixture.WaitForCompletionAsync();
        await fixture.AssertPersistedAsync("file_write_metadata_unsupported", true, AgentTaskStatus.Completed);
        Assert.Contains("附加数据流", Assert.Single(fixture.Workspace.Messages, message => message.ToolCallId == "file-edit").Tool!.Summary);
        Assert.Equal("alpha\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        Assert.Equal("retained stream", await File.ReadAllTextAsync(fixture.FilePath + ":metadata", fixture.Token));
    }
}
