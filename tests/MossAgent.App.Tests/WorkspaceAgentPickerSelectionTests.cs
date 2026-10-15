using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceAgentPickerSelectionTests
{
    [Fact]
    public async Task RemovedSelection_RemainsUnavailableUntilAnExplicitChoiceOrRestoration()
    {
        using var fixture = await WorkspaceAgentPickerTestFixture.CreateAsync();
        fixture.Picker.Select(fixture.First);
        await fixture.Repository.DeleteAsync(fixture.First.Id, TestContext.Current.CancellationToken);

        await fixture.Picker.ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(fixture.First.Id, fixture.Picker.SelectedProfile?.Id);
        Assert.True(fixture.Picker.IsSelectedUnavailable);
        Assert.True(fixture.Picker.HasCatalogNotice);
        Assert.Contains("不可用", fixture.Picker.SelectedLabel, StringComparison.Ordinal);
        Assert.False(fixture.Picker.CanUseSelection);
        Assert.False(fixture.Picker.SelectCommand.CanExecute(fixture.First));
        fixture.Picker.SelectCommand.Execute(fixture.First);
        fixture.Picker.SelectCommand.Execute(null);
        Assert.Null(fixture.Picker.SelectedProfile);
        Assert.Equal("默认", fixture.Models.SelectedReasoning);
        Assert.True(fixture.Picker.CanUseSelection);
        await fixture.Repository.SaveAsync(fixture.First, TestContext.Current.CancellationToken);
        await fixture.Picker.ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Null(fixture.Picker.SelectedProfile);
    }

    [Fact]
    public async Task Restoration_RebindsThePreferredIdentityToTheCurrentSnapshot()
    {
        using var fixture = await WorkspaceAgentPickerTestFixture.CreateAsync();
        fixture.Picker.Select(fixture.First);
        await fixture.Repository.SaveAsync(fixture.First with { IsEnabled = false }, TestContext.Current.CancellationToken);
        await fixture.Picker.ReloadAsync(TestContext.Current.CancellationToken);
        var restored = fixture.First with { Name = "restored", ReasoningEffort = "ultra" };
        await fixture.Repository.SaveAsync(restored, TestContext.Current.CancellationToken);

        await fixture.Picker.ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(restored, fixture.Picker.SelectedProfile);
        Assert.True(fixture.Picker.CanUseSelection);
        Assert.Equal("ultra", fixture.Models.EffectiveReasoningEffort);
    }

    [Fact]
    public async Task StaleAndUnknownMenuItems_CannotRestoreAnOldConfiguration()
    {
        using var fixture = await WorkspaceAgentPickerTestFixture.CreateAsync();
        var current = fixture.First with
        {
            SystemPrompt = "current",
            ModelProfileId = fixture.Second.ModelProfileId,
            ReasoningEffort = "ultra"
        };
        await fixture.Repository.SaveAsync(current, TestContext.Current.CancellationToken);
        await fixture.Picker.ReloadAsync(TestContext.Current.CancellationToken);

        fixture.Picker.SelectCommand.Execute(fixture.First);

        Assert.Equal(current, fixture.Picker.SelectedProfile);
        Assert.Equal(fixture.Second.ModelProfileId, fixture.Models.SelectedItem?.Model.Id);
        fixture.Picker.SelectCommand.Execute(fixture.First with { Id = Guid.NewGuid() });
        Assert.Equal(current, fixture.Picker.SelectedProfile);
    }

    [Fact]
    public async Task UnchangedRefresh_PreservesTheRowsAndSelectedReference()
    {
        using var fixture = await WorkspaceAgentPickerTestFixture.CreateAsync();
        fixture.Picker.Select(fixture.First);
        var selected = fixture.Picker.SelectedProfile;
        var changes = 0;
        fixture.Picker.Items.CollectionChanged += (_, _) => changes++;
        fixture.Repository.Loader = _ => Task.FromResult<IReadOnlyList<AgentProfile>>
            ([fixture.First with { }, fixture.Second with { }]);

        await fixture.Picker.ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, changes);
        Assert.Same(selected, fixture.Picker.SelectedProfile);
        Assert.True(fixture.Picker.CanUseSelection);
    }

    [Fact]
    public async Task ReadFailure_KeepsTheRowsButBlocksEvenTheDefaultSelectionAndHidesExceptionDetails()
    {
        using var fixture = await WorkspaceAgentPickerTestFixture.CreateAsync();
        var rows = fixture.Picker.Items.ToArray();
        fixture.Repository.Loader = _ => throw new InvalidOperationException("fixture-secret-error");

        await fixture.Picker.ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(rows, fixture.Picker.Items);
        Assert.False(fixture.Picker.CanUseSelection);
        Assert.True(fixture.Picker.HasLoadError);
        Assert.DoesNotContain("fixture-secret-error", fixture.Picker.CatalogNotice, StringComparison.Ordinal);
        fixture.Repository.Loader = null;
        await fixture.Picker.RefreshCommand.ExecuteAsync(null);
        Assert.True(fixture.Picker.CanUseSelection);
    }

    [Fact]
    public async Task EveryEnabledAgent_IsAvailableRatherThanSilentlyTruncatedToSixteen()
    {
        using var fixture = await WorkspaceAgentPickerTestFixture.CreateAsync();
        for (var index = 0; index < 28; index++)
            await fixture.Repository.SaveAsync(fixture.First with { Id = Guid.NewGuid(), Name = $"agent-{index}" },
                TestContext.Current.CancellationToken);

        await fixture.Picker.ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(30, fixture.Picker.Items.Count);
        var last = fixture.Picker.Items[^1];
        fixture.Picker.SelectCommand.Execute(last);
        Assert.Same(last, fixture.Picker.SelectedProfile);
        Assert.True(fixture.Picker.CanUseSelection);
    }

    [Fact]
    public async Task ExplicitModelChoice_ClearsTheAgentEvenWhenChoosingTheSameModel()
    {
        using var fixture = await WorkspaceAgentPickerTestFixture.CreateAsync();
        fixture.Picker.Select(fixture.First);

        Assert.True(fixture.Models.TrySelect(fixture.First.ProviderId, fixture.First.ModelProfileId));

        Assert.Null(fixture.Picker.SelectedProfile);
        await fixture.Picker.ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Null(fixture.Picker.SelectedProfile);
    }
}
