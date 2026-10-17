using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ProjectEditorUiStateTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ValidationError_RemainsVisibleAndCanBeCorrectedWithoutLosingDirectories(bool dark) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await ProjectEditorViewportFixture.CreateAsync(dark, 526, 601, 1.25);
            fixture.Open();
            var original = Assert.Single(fixture.Model.Workspace.Catalog.Projects);
            var input = Assert.Single(fixture.Content.GetVisualDescendants().OfType<AppInput>());
            input.Focus();
            input.SelectAll();
            fixture.Popup.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.None, null);
            fixture.Popup.KeyRelease(Key.Back, RawInputModifiers.None, PhysicalKey.None, null);
            fixture.Press(fixture.SaveButton);
            await fixture.Editor.AddCommand.ExecutionTask!;
            fixture.Flush();
            Assert.True(fixture.Flyout.IsOpen);
            Assert.True(fixture.Editor.HasStatusError);
            Assert.True(fixture.Editor.HasStatus);
            Assert.Equal(24, fixture.Editor.Directories.Count);
            var status = Assert.Single(fixture.Content.GetVisualDescendants().OfType<AppText>(),
                text => text.Text == fixture.Editor.Status);
            Assert.True(status.IsEffectivelyVisible);
            Assert.Contains("status-error", status.Classes);
            fixture.AssertHit(fixture.SaveButton);
            fixture.SaveEvidence($"project-editor-validation-{dark}");
            var unchanged = Assert.Single(await fixture.Repository.GetProjectsAsync(TestContext.Current.CancellationToken));
            Assert.Equal(original.Name, unchanged.Name);
            input.Focus();
            fixture.Popup.KeyTextInput("修正后的项目名");
            fixture.Press(fixture.SaveButton);
            await fixture.Editor.AddCommand.ExecutionTask!;
            fixture.Flush();
            Assert.False(fixture.Flyout.IsOpen);
            var saved = Assert.Single(await fixture.Repository.GetProjectsAsync(TestContext.Current.CancellationToken));
            Assert.Equal("修正后的项目名", saved.Name);
            Assert.Equal(original.AuthorizedDirectories, saved.AuthorizedDirectories);
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task LockedProject_DisablesMutationButRetainsVisibleCancelAndDraft(bool dark) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await ProjectEditorViewportFixture.CreateAsync(dark, 526, 601, 1);
            fixture.Open();
            fixture.Model.Workspace.Catalog.IsLocked = true;
            fixture.Flush();
            Assert.False(fixture.SaveButton.IsEffectivelyEnabled);
            var add = Assert.Single(fixture.Content.GetVisualDescendants().OfType<AppActionButton>());
            Assert.False(add.IsEffectivelyEnabled);
            var cancel = Assert.Single(fixture.Content.GetVisualDescendants().OfType<AppButton>(),
                button => Equals(button.Content, ProjectEditorText.Cancel));
            fixture.AssertHit(cancel);
            fixture.Press(cancel);
            Assert.False(fixture.Flyout.IsOpen);
            Assert.Equal(0, fixture.Picker.Calls);
            Assert.Equal(24, fixture.Editor.Directories.Count);
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task KeyboardNavigation_CyclesWithinEditorWithoutMovingFocusBehindIt(bool dark) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await ProjectEditorViewportFixture.CreateAsync(dark, 526, 601, 1, count: 2);
            fixture.Open();
            var input = Assert.Single(fixture.Content.GetVisualDescendants().OfType<AppInput>());
            Assert.Equal(ProjectEditorText.Name, AutomationProperties.GetName(input));
            fixture.SaveButton.Focus(NavigationMethod.Tab);
            fixture.Popup.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.None, null);
            fixture.Popup.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.None, null);
            fixture.Flush();
            Assert.Same(input, fixture.Popup.FocusManager!.GetFocusedElement());
            fixture.Popup.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.None, null);
            fixture.Popup.KeyRelease(Key.Tab, RawInputModifiers.Shift, PhysicalKey.None, null);
            fixture.Flush();
            Assert.Same(fixture.SaveButton, fixture.Popup.FocusManager!.GetFocusedElement());
            fixture.AssertHit(fixture.SaveButton);
        }, TestContext.Current.CancellationToken);
}
