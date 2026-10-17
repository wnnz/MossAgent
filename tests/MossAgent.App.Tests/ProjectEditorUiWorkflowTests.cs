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
public sealed class ProjectEditorUiWorkflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NewProjectTrigger_AfterDismissedEditStartsCreateInsteadOfEditingOldProject(bool dark) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await ProjectEditorViewportFixture.CreateAsync(dark, 526, 601, 1, count: 2);
            fixture.Open();
            fixture.Editor.Name = "未保存的编辑";
            fixture.Popup.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.None, null);
            fixture.Flush();
            Assert.False(fixture.Flyout.IsOpen);
            fixture.Press(fixture.Trigger);
            Assert.True(fixture.Flyout.IsOpen);
            Assert.Equal(SettingsText.Get("ProjectCreateTitle"), fixture.Editor.EditorTitle);
            Assert.Empty(fixture.Editor.Name);
            Assert.Empty(fixture.Editor.Directories);
            Assert.True(Assert.Single(fixture.Content.GetVisualDescendants().OfType<AppText>(),
                text => text.Text == ProjectEditorText.EmptyDirectories).IsEffectivelyVisible);
            fixture.SaveEvidence($"project-editor-empty-{dark}");
            var path = Directory.CreateDirectory(Path.Combine(fixture.DataRoot, "new-project-root")).FullName;
            fixture.Picker.Results.Enqueue(path);
            fixture.Press(Assert.Single(fixture.Content.GetVisualDescendants().OfType<AppActionButton>()));
            await fixture.Editor.AddDirectoryCommand.ExecutionTask!;
            fixture.Flush();
            Assert.Equal(path, Assert.Single(fixture.Editor.Directories).Path);
            Assert.Equal("new-project-root", fixture.Editor.Name);
            fixture.SaveEvidence($"project-editor-create-{dark}");
            fixture.Press(fixture.SaveButton);
            await fixture.Editor.AddCommand.ExecutionTask!;
            fixture.Flush();
            Assert.False(fixture.Flyout.IsOpen);
            var projects = await fixture.Repository.GetProjectsAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, projects.Count);
            var created = Assert.Single(projects, project => project.Name == "new-project-root");
            Assert.Equal(path, Assert.Single(created.AuthorizedDirectories));
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DirectoryActions_SaveOrderedRootsThroughRealCatalogAndSqlite(bool dark) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await ProjectEditorViewportFixture.CreateAsync(dark, 526, 601, 1);
            fixture.Open();
            var original = Assert.Single(fixture.Model.Workspace.Catalog.Projects);
            var last = fixture.Editor.Directories[^1];
            var primary = Assert.Single(fixture.Content.GetVisualDescendants().OfType<AppButton>(),
                button => ReferenceEquals(button.DataContext, last) && button.Classes.Contains("set-primary"));
            primary.BringIntoView();
            primary.Focus(NavigationMethod.Tab);
            fixture.Flush();
            fixture.AssertHit(primary);
            fixture.Press(primary);
            Assert.Same(last, fixture.Editor.Directories[0]);
            fixture.AssertHit(fixture.SaveButton);
            var removed = fixture.Editor.Directories[^1];
            var remove = Assert.Single(fixture.Content.GetVisualDescendants().OfType<AppIconButton>(),
                button => ReferenceEquals(button.DataContext, removed));
            fixture.Press(remove);
            Assert.DoesNotContain(removed, fixture.Editor.Directories);
            var input = Assert.Single(fixture.Content.GetVisualDescendants().OfType<AppInput>());
            Assert.Equal(ProjectEditorText.Name, AutomationProperties.GetName(input));
            input.Focus();
            input.SelectAll();
            fixture.Popup.KeyTextInput("已更新的多目录项目");
            fixture.Press(fixture.SaveButton);
            await fixture.Editor.AddCommand.ExecutionTask!;
            fixture.Flush();
            Assert.False(fixture.Flyout.IsOpen);
            var saved = Assert.Single(await fixture.Repository.GetProjectsAsync(TestContext.Current.CancellationToken));
            Assert.Equal(original.Id, saved.Id);
            Assert.Equal(original.CreatedAt, saved.CreatedAt);
            Assert.Equal("已更新的多目录项目", saved.Name);
            Assert.Equal(last.Path, saved.PrimaryDirectory);
            Assert.Equal(23, saved.AuthorizedDirectories.Count);
            Assert.DoesNotContain(removed.Path, saved.AuthorizedDirectories);
            var catalog = Assert.Single(fixture.Model.Workspace.Catalog.Projects);
            Assert.Equal(saved.Id, catalog.Id);
            Assert.Equal(saved.Name, catalog.Name);
            Assert.Equal(saved.AuthorizedDirectories, catalog.AuthorizedDirectories);
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CancelledFolderSelectionAndEditorCancel_DoNotWriteProject(bool dark) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await ProjectEditorViewportFixture.CreateAsync(dark, 526, 601, 1, count: 2);
            fixture.Open();
            var original = Assert.Single(fixture.Model.Workspace.Catalog.Projects);
            fixture.Picker.Results.Enqueue(null);
            var add = Assert.Single(fixture.Content.GetVisualDescendants().OfType<AppActionButton>());
            fixture.Press(add);
            await fixture.Editor.AddDirectoryCommand.ExecutionTask!;
            fixture.Flush();
            Assert.True(fixture.Flyout.IsOpen);
            Assert.Equal(1, fixture.Picker.Calls);
            Assert.Equal(2, fixture.Editor.Directories.Count);
            fixture.Editor.Name = "不应写入";
            var cancel = Assert.Single(fixture.Content.GetVisualDescendants().OfType<AppButton>(),
                button => Equals(button.Content, ProjectEditorText.Cancel));
            fixture.Press(cancel);
            Assert.False(fixture.Flyout.IsOpen);
            var saved = Assert.Single(await fixture.Repository.GetProjectsAsync(TestContext.Current.CancellationToken));
            Assert.Equal(original.Name, saved.Name);
            Assert.Equal(original.AuthorizedDirectories, saved.AuthorizedDirectories);
            Assert.Equal("不应写入", fixture.Editor.Name);
        }, TestContext.Current.CancellationToken);
}
