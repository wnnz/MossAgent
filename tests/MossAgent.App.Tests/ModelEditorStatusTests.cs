using Avalonia.Headless;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ModelEditorStatusTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task DiscoveryMessageDoesNotAppearInNewOrExistingEditor(bool dark, bool create) => _session.DispatchAsync(async () =>
    {
        using var fixture = new ModelEditorStatusFixture(dark);
        await fixture.Models.FetchModelsForProviderCommand.ExecuteAsync(fixture.Models.ProviderGroups[0]);
        var pageStatus = fixture.Models.Status;
        Assert.Contains("已同步", pageStatus);

        fixture.OpenEditor(create);

        Assert.Empty(fixture.FooterStatus);
        Assert.Equal(pageStatus, fixture.Models.Status);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task SaveSuccessDoesNotAppearWhenEditorReopens(bool dark, bool create) => _session.DispatchAsync(async () =>
    {
        using var fixture = new ModelEditorStatusFixture(dark);
        fixture.OpenEditor(false);
        await fixture.Models.SaveModelCommand.ExecuteAsync(null);
        Assert.False(fixture.Models.IsFormOpen);
        Assert.Contains("已更新", fixture.Models.Status);

        fixture.OpenEditor(create);

        Assert.Empty(fixture.FooterStatus);
        Assert.Contains("已更新", fixture.Models.Status);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task DelayedDiscoveryKeepsItsPageResultAndCannotReplaceEditorError(bool dark, bool fail) => _session.DispatchAsync(async () =>
    {
        var discovery = new DeferredModelDiscoveryService();
        using var fixture = new ModelEditorStatusFixture(dark, discovery);
        var fetch = fixture.Models.FetchModelsForProviderCommand.ExecuteAsync(fixture.Models.ProviderGroups[0]);
        await discovery.Started.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        fixture.OpenEditor(true);
        fixture.Models.DisplayName = "current-draft";
        var editorError = await fixture.ShowValidationErrorAsync();
        if (fail) discovery.Fail(new IOException("fixture-secret-api-key-password"));
        else discovery.Complete([new("discovered", "Discovered")]);
        await fetch;

        Assert.Equal(editorError, fixture.FooterStatus);
        Assert.Equal("current-draft", fixture.Models.DisplayName);
        Assert.Equal(fixture.Repository.Store.Providers[1].Id, fixture.Models.SelectedProvider?.Id);
        Assert.Equal(fail, fixture.Models.HasStatusError);
        Assert.DoesNotContain("fixture-secret", fixture.Models.Status);
        Assert.NotEqual(editorError, fixture.Models.Status);
        Assert.Equal(fail ? 1 : 2, fixture.Repository.Store.Models.Count);
        Assert.DoesNotContain(fixture.Repository.Store.Models,
            model => model.ProviderId == fixture.Repository.Store.Providers[1].Id);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task DeletingAnotherModelCannotReplaceEditorError(bool dark, bool fail) => _session.DispatchAsync(async () =>
    {
        using var fixture = new ModelEditorStatusFixture(dark);
        fixture.OpenEditor(true);
        var editorError = await fixture.ShowValidationErrorAsync();
        if (fail) fixture.Repository.FailingOperation = "delete-model";

        await fixture.Models.DeleteProviderModelCommand.ExecuteAsync(fixture.Repository.Store.Models[0]);

        Assert.Equal(editorError, fixture.FooterStatus);
        Assert.True(fixture.Models.IsFormOpen);
        Assert.Equal(fail, fixture.Models.HasStatusError);
        Assert.DoesNotContain("fixture-secret", fixture.Models.Status);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task LateSaveDoesNotPublishToReopenedEditor(bool dark, bool fail) => _session.DispatchAsync(async () =>
    {
        using var fixture = new ModelEditorStatusFixture(dark);
        fixture.OpenEditor(false);
        if (fail) fixture.Repository.FailingOperation = "save-model";
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Repository.BeforeWriteAsync = token => pending.Task.WaitAsync(token);
        var save = fixture.Models.SaveModelCommand.ExecuteAsync(null);
        try
        {
            fixture.Models.CloseFormCommand.Execute(null);
            fixture.Models.OpenCreateFormCommand.Execute(null);
            fixture.Models.DisplayName = "new-draft";
        }
        finally { pending.TrySetResult(); await save; }

        Assert.Empty(fixture.FooterStatus);
        Assert.Equal("new-draft", fixture.Models.DisplayName);
        Assert.True(fixture.Models.IsFormOpen);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SaveFailureStaysActionableAndRetryDoesNotLeakIntoNextEditor(bool dark) => _session.DispatchAsync(async () =>
    {
        using var fixture = new ModelEditorStatusFixture(dark);
        fixture.OpenEditor(false);
        fixture.Repository.FailingOperation = "save-model";
        await fixture.Models.SaveModelCommand.ExecuteAsync(null);
        Assert.Contains("无法保存", fixture.FooterStatus);
        Assert.DoesNotContain("fixture-secret", fixture.FooterStatus);
        Assert.True(fixture.Models.IsFormOpen);

        fixture.Pane.IsVisible = false;
        fixture.Pane.IsVisible = true;
        Assert.Contains("无法保存", fixture.FooterStatus);
        fixture.Repository.FailingOperation = null;
        await fixture.Models.SaveModelCommand.ExecuteAsync(null);
        fixture.OpenEditor(false);

        Assert.Empty(fixture.FooterStatus);
        Assert.Contains("已更新", fixture.Models.Status);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task EditorEntryPointsClearOnlyThePreviousDraftError(bool dark) => _session.DispatchAsync(async () =>
    {
        using var fixture = new ModelEditorStatusFixture(dark);
        fixture.OpenEditor(false);
        await fixture.ShowValidationErrorAsync();
        fixture.Models.NewModelCommand.Execute(null);
        Assert.Empty(fixture.FooterStatus);
        fixture.Models.ModelId = "new-model";
        await fixture.Models.SaveModelCommand.ExecuteAsync(null);
        fixture.Models.SelectedModel = fixture.Repository.Store.Models[0];
        fixture.Models.OpenEditFormCommand.Execute(null);
        Assert.Empty(fixture.FooterStatus);
        await fixture.ShowValidationErrorAsync();
        fixture.Models.CloseFormCommand.Execute(null);
        fixture.Models.OpenEditFormCommand.Execute(null);
        Assert.Empty(fixture.FooterStatus);
    }, TestContext.Current.CancellationToken);
}
