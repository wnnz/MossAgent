using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

internal static class ProjectAuthorizationUiInput
{
    public static void EnsureNavigation(ProjectAuthorizationUiFixture fixture)
    {
        var pane = fixture.Window.FindControl<AppNavigationPanes>("WorkspaceNavigation")!;
        if (!pane.IsNavigationVisible) Press(fixture, Assert.Single(fixture.Window.GetVisualDescendants().OfType<AppIconButton>(),
            button => button.Label == "切换侧边栏" && button.IsEffectivelyVisible));
    }

    public static void Press(ProjectAuthorizationUiFixture fixture, AppButton button)
    {
        button.BringIntoView();
        ProviderUiWorkflowInput.Flush(fixture.Window);
        Assert.True(button.IsEffectivelyVisible);
        Assert.True(button.IsEffectivelyEnabled);
        Assert.True(button.Focus(NavigationMethod.Tab));
        ProviderUiWorkflowInput.Flush(fixture.Window);
        var inputRoot = Assert.IsAssignableFrom<TopLevel>(TopLevel.GetTopLevel(button));
        inputRoot.UpdateLayout();
        var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), inputRoot)!.Value;
        Assert.True(new Rect(inputRoot.ClientSize).Contains(point));
        var hit = Assert.IsAssignableFrom<Visual>(inputRoot.InputHitTest(point));
        Assert.True(ReferenceEquals(button, hit) || button.IsVisualAncestorOf(hit),
            $"Expected {button}, hit {hit} at {point} in {inputRoot.ClientSize}; bounds {button.Bounds}.");
        inputRoot.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        inputRoot.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        ProviderUiWorkflowInput.Flush(fixture.Window);
    }

    public static Control EditorContent(ProjectAuthorizationUiFixture fixture)
    {
        var sidebar = Assert.Single(fixture.Window.GetVisualDescendants().OfType<MossAgent.App.Views.WorkspaceSidebar>());
        var trigger = Assert.Single(sidebar.GetVisualDescendants().OfType<AppIconButton>(), button => button.Label == "添加项目");
        return Assert.IsAssignableFrom<Control>(Assert.IsType<AppFlyout>(trigger.Flyout).Content);
    }

    public static async Task<ProjectProfile> CreateProjectAsync(ProjectAuthorizationUiFixture fixture, bool multipleRoots)
    {
        EnsureNavigation(fixture);
        var trigger = Assert.Single(fixture.Window.GetVisualDescendants().OfType<AppIconButton>(), button => button.Label == "添加项目");
        Press(fixture, trigger);
        Assert.True(fixture.Editor.IsOpen);
        var input = Assert.Single(EditorContent(fixture).GetVisualDescendants().OfType<AppInput>());
        Assert.True(input.Focus(NavigationMethod.Tab));
        Assert.IsAssignableFrom<TopLevel>(TopLevel.GetTopLevel(input)).KeyTextInput("授权项目");
        Assert.Equal("授权项目", fixture.Editor.Name);
        await AddDirectoryAsync(fixture, fixture.PrimaryRoot);
        if (multipleRoots)
        {
            await AddDirectoryAsync(fixture, fixture.AdditionalRoot);
            var secondary = fixture.Editor.Directories[1];
            Press(fixture, Assert.Single(EditorContent(fixture).GetVisualDescendants().OfType<AppButton>(),
                button => button.Classes.Contains("set-primary") && ReferenceEquals(button.DataContext, secondary)));
        }
        await SaveEditorAsync(fixture);
        var project = Assert.Single(await fixture.Repository.GetProjectsAsync(fixture.Token), item => item.Name == "授权项目");
        Assert.Equal(multipleRoots ? fixture.AdditionalRoot : fixture.PrimaryRoot, project.PrimaryDirectory);
        Assert.Equal(multipleRoots ? new[] { fixture.AdditionalRoot, fixture.PrimaryRoot } : [fixture.PrimaryRoot], project.AuthorizedDirectories);
        return project;
    }

    public static async Task AddDirectoryAsync(ProjectAuthorizationUiFixture fixture, string path)
    {
        fixture.Picker.Results.Enqueue(path);
        Press(fixture, Assert.Single(EditorContent(fixture).GetVisualDescendants().OfType<AppActionButton>()));
        await fixture.Editor.AddDirectoryCommand.ExecutionTask!.WaitAsync(fixture.Token);
        ProviderUiWorkflowInput.Flush(fixture.Window);
        Assert.True(fixture.Editor.IsOpen);
        Assert.False(fixture.Editor.HasStatusError, fixture.Editor.Status);
    }

    public static async Task SaveEditorAsync(ProjectAuthorizationUiFixture fixture)
    {
        Press(fixture, Assert.Single(EditorContent(fixture).GetVisualDescendants().OfType<AppButton>(),
            button => ReferenceEquals(button.Command, fixture.Editor.AddCommand)));
        await fixture.Editor.AddCommand.ExecutionTask!.WaitAsync(fixture.Token);
        ProviderUiWorkflowInput.Flush(fixture.Window);
        Assert.False(fixture.Editor.IsOpen, fixture.Editor.Status);
    }

    public static void OpenEditor(ProjectAuthorizationUiFixture fixture, Guid projectId)
    {
        EnsureNavigation(fixture);
        SaveEvidence(fixture, "navigation-before-summary");
        var row = Assert.Single(fixture.Window.GetVisualDescendants().OfType<AppProjectNavigationRow>(),
            control => control.ActionTarget is ProjectProfile project && project.Id == projectId);
        Press(fixture, row.FindControl<AppActionButton>("SelectProjectButton")!);
        Assert.True(row.IsSummaryOpen, $"Summary closed after selecting project {projectId} in {fixture.Window.ClientSize}.");
        SaveEvidence(fixture, "summary-before-edit");
        Press(fixture, Assert.Single(row.SummaryContent!.GetVisualDescendants().OfType<AppActionButton>(),
            button => button.Label == "编辑项目"));
        Assert.True(fixture.Editor.IsOpen, $"Editor closed after activating summary in {fixture.Window.ClientSize}.");
    }

    public static void RemoveDirectory(ProjectAuthorizationUiFixture fixture, string path)
    {
        var directory = Assert.Single(fixture.Editor.Directories, item => item.Path == path);
        Press(fixture, Assert.Single(EditorContent(fixture).GetVisualDescendants().OfType<AppIconButton>(),
            button => ReferenceEquals(button.DataContext, directory)));
    }

    public static async Task SelectTaskAsync(ProjectAuthorizationUiFixture fixture, Guid taskId)
    {
        EnsureNavigation(fixture);
        await fixture.WaitUntilAsync(() => fixture.Window.GetVisualDescendants().OfType<AppTaskNavigationRow>()
            .Any(row => row.IsEffectivelyVisible && row.CommandParameter is AgentTask task && task.Id == taskId));
        var row = fixture.Window.GetVisualDescendants().OfType<AppTaskNavigationRow>()
            .First(control => control.IsEffectivelyVisible && control.CommandParameter is AgentTask task && task.Id == taskId);
        Press(fixture, row.FindControl<AppButton>("SelectTaskButton")!);
        await fixture.WaitUntilAsync(() => fixture.Main.Workspace.Catalog.SelectedTask?.Id == taskId
            && fixture.Main.Workspace.Messages.Count > 0);
    }

    public static void SelectProject(ProjectAuthorizationUiFixture fixture, Guid projectId)
    {
        EnsureNavigation(fixture);
        var row = Assert.Single(fixture.Window.GetVisualDescendants().OfType<AppProjectNavigationRow>(),
            control => control.ActionTarget is ProjectProfile project && project.Id == projectId);
        Press(fixture, row.FindControl<AppActionButton>("SelectProjectButton")!);
        if (row.FindControl<AppActionButton>("SelectProjectButton")!.Flyout is AppFlyout summary) summary.Hide();
        ProviderUiWorkflowInput.Flush(fixture.Window);
        Assert.Equal(projectId, fixture.Main.Workspace.Catalog.SelectedProject?.Id);
    }

    public static void Start(ProjectAuthorizationUiFixture fixture, bool newTask)
    {
        if (newTask)
        {
            EnsureNavigation(fixture);
            Press(fixture, Assert.Single(fixture.Window.GetVisualDescendants().OfType<AppActionButton>(),
                button => ReferenceEquals(button.Command, fixture.Main.Workspace.Catalog.NewTaskCommand)));
        }
        var pane = fixture.Window.FindControl<AppNavigationPanes>("WorkspaceNavigation")!;
        if (pane.IsOverlayOpen) Press(fixture, Assert.Single(fixture.Window.GetVisualDescendants().OfType<AppIconButton>(),
            button => button.Label == "切换侧边栏" && button.IsEffectivelyVisible));
        fixture.Main.Workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.AskEveryTime);
        var composer = Assert.Single(fixture.Window.GetVisualDescendants().OfType<AppInput>(), input => input.Classes.Contains("composer-input"));
        ProviderUiWorkflowInput.Type(fixture.Window, composer, "验证当前项目的文件授权范围。");
        Press(fixture, ProviderUiWorkflowInput.SendButton(fixture.Window));
    }

    public static void SaveEvidence(ProjectAuthorizationUiFixture fixture, string stage) =>
        SubagentApprovalInteraction.Save(fixture.Window,
            $"project-auth-{fixture.IsExpansion}-{fixture.Window.ActualThemeVariant}-{fixture.Window.ClientSize.Width}-{stage}");
}
