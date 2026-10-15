using Avalonia.Headless;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationListOwnershipTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData("edit", false)]
    [InlineData("toggle", false)]
    [InlineData("remove", false)]
    [InlineData("edit", true)]
    [InlineData("toggle", true)]
    [InlineData("remove", true)]
    public Task ForeignAndReplacedRows_CannotInvokeCommandsEvenDirectly(string action, bool replaced) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var repository = new AutomationListProbeRepository(fixture.Automations);
        using var page = new AutomationViewModel(repository, fixture.Scheduler, fixture.Configurations, fixture.Tasks, fixture.Workspace.Catalog);
        await fixture.Automations.SaveAsync(Schedule(fixture), fixture.Token);
        await page.ReloadAsync();
        var current = Assert.Single(page.Items);
        var foreign = replaced ? current : new AutomationItemViewModel(current.Schedule, current.ProjectLabel, current.ModelLabel, false);
        if (replaced) await page.ReloadAsync();
        var command = action switch { "edit" => page.EditCommand, "toggle" => page.ToggleCommand, _ => null };
        if (command is not null)
        {
            var available = command.CanExecute(foreign);
            await command.ExecuteAsync(foreign);
            Assert.False(available);
        }
        else { var available = page.RemoveCommand.CanExecute(foreign); page.RemoveCommand.Execute(foreign); Assert.False(available); }
        Assert.False(page.Editor.IsOpen);
        Assert.False(page.IsDeleteOpen);
        Assert.Equal(0, repository.WriteCount);
        Assert.False(Assert.Single(await fixture.Automations.GetAllAsync(fixture.Token)).IsEnabled);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Selection_RejectsForeignReferencesAndRebindsCurrentSnapshot() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var page = fixture.Main.Pages.Automations!;
        await fixture.Automations.SaveAsync(Schedule(fixture), fixture.Token);
        await page.ReloadAsync();
        var original = Assert.Single(page.Items);
        page.SelectedItem = new AutomationItemViewModel(original.Schedule, original.ProjectLabel, original.ModelLabel, false);
        Assert.Same(original, page.SelectedItem);
        await page.ReloadAsync();
        Assert.Same(Assert.Single(page.Items), page.SelectedItem);
        Assert.NotSame(original, page.SelectedItem);
        Assert.False(page.ToggleCommand.CanExecute(original));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ChangedDeleteTarget_InvalidatesConfirmationWithoutArchiving() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var repository = new AutomationListProbeRepository(fixture.Automations);
        using var page = new AutomationViewModel(repository, fixture.Scheduler, fixture.Configurations, fixture.Tasks, fixture.Workspace.Catalog);
        await fixture.Automations.SaveAsync(Schedule(fixture), fixture.Token);
        await page.ReloadAsync();
        var original = Assert.Single(page.Items);
        page.RemoveCommand.Execute(original);
        Assert.True(page.IsDeleteOpen);
        await fixture.Automations.SaveAsync(original.Schedule with { Name = "外部修改" }, fixture.Token);
        await page.ReloadAsync();
        var available = page.ConfirmDeleteCommand.CanExecute(null);
        await page.ConfirmDeleteCommand.ExecuteAsync(null);
        Assert.False(available);
        Assert.False(page.IsDeleteOpen);
        Assert.Equal(0, repository.WriteCount);
        Assert.Equal("外部修改", Assert.Single(await fixture.Automations.GetAllAsync(fixture.Token)).Name);
    }, TestContext.Current.CancellationToken);

    internal static AutomationSchedule Schedule(WorkspaceModelSelectionFixture fixture) => new(Guid.NewGuid(), "只读合成计划",
        "只读检查。", fixture.Workspace.Catalog.SelectedProject!.Id, fixture.SelectedProvider.Id, fixture.SelectedModel.Id,
        AutomationFrequency.Daily, 1, 9, 0, DayOfWeek.Monday, TimeZoneInfo.Local.Id, ApprovalPolicy.ReadOnly, 30, false,
        DateTimeOffset.UtcNow.AddDays(1));
}
