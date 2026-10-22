using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MossAgent.App.Controls;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Application.Authorization;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class ProjectEditorViewportFixture : IAsyncDisposable
{
    private readonly string _root;
    private readonly ServiceProvider _services;

    private ProjectEditorViewportFixture(string root, ServiceProvider services, MainWindowViewModel model, ProjectEditorPickerProbe picker,
        bool dark, int width, int height, double scale)
    {
        _root = root;
        _services = services;
        Model = model;
        Picker = picker;
        Window = new MainWindow
        {
            DataContext = model,
            Width = width,
            Height = height,
            WindowState = WindowState.Normal,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        Window.Show();
        Window.SetRenderScaling(scale);
        Flush();
        var navigation = Window.FindControl<AppNavigationPanes>("WorkspaceNavigation")!;
        if (!navigation.IsNavigationVisible) navigation.ToggleNavigationCommand.Execute(null);
        Flush();
        var sidebar = Assert.Single(Window.GetVisualDescendants().OfType<WorkspaceSidebar>());
        Trigger = Assert.Single(sidebar.GetVisualDescendants().OfType<AppIconButton>(), button => button.Label == "添加项目");
        Flyout = Assert.IsType<AppFlyout>(Trigger.Flyout);
    }

    public MainWindow Window { get; }
    public MainWindowViewModel Model { get; }
    public ProjectEditorPickerProbe Picker { get; }
    public IWorkspaceRepository Repository => _services.GetRequiredService<IWorkspaceRepository>();
    public string DataRoot => _root;
    public WorkspaceProjectEditorViewModel Editor => Model.Workspace.Catalog.ProjectEditor;
    public AppIconButton Trigger { get; }
    public AppFlyout Flyout { get; }
    public Control Content => Assert.IsAssignableFrom<Control>(Flyout.Content);
    public TopLevel Popup => Assert.IsAssignableFrom<TopLevel>(TopLevel.GetTopLevel(Content));
    public AppButton SaveButton => Assert.Single(Content.GetVisualDescendants().OfType<AppButton>(),
        button => Equals(button.Content, Editor.SubmitLabel));

    public static async Task<ProjectEditorViewportFixture> CreateAsync(bool dark, int width, int height, double scale,
        int count = 24, bool staleResource = false, bool failedWorktreeIntent = false)
    {
        var root = Directory.CreateTempSubdirectory("MossAgent-project-viewport-").FullName;
        var picker = new ProjectEditorPickerProbe();
        var services = AppComposition.CreateServiceCollection(root)
            .Replace(ServiceDescriptor.Singleton<IFolderPickerService>(picker)).BuildServiceProvider(validateScopes: true);
        try
        {
            var token = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(token);
            var directories = Enumerable.Range(0, count).Select(index =>
                Directory.CreateDirectory(Path.Combine(root, $"source-{index:00}-a-long-project-folder-name")).FullName).ToArray();
            var project = new ProjectProfile(Guid.NewGuid(), "多目录项目", directories[0], directories, DateTimeOffset.UtcNow);
            await services.GetRequiredService<IWorkspaceRepository>().SaveProjectAsync(project, token);
            if (staleResource)
            {
                var registry = services.GetRequiredService<IAuthorizationResourceRegistry>();
                var leaseId = Guid.NewGuid();
                await registry.RegisterAsync(leaseId, project.Id, project.AuthorizationRevision, token);
                await using var connection = services.GetRequiredService<SqliteConnectionFactory>().Create();
                await connection.OpenAsync(token);
                await using var command = connection.CreateCommand();
                command.CommandText = "UPDATE authorization_resource_leases SET heartbeat_at=$heartbeat WHERE lease_id=$lease;";
                command.Parameters.AddWithValue("$heartbeat", DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"));
                command.Parameters.AddWithValue("$lease", leaseId.ToString());
                await command.ExecuteNonQueryAsync(token);
            }
            if (failedWorktreeIntent)
            {
                var taskId = Guid.NewGuid();
                var target = Path.Combine(root, "worktrees", taskId.ToString("N"));
                var intents = new SqliteWorktreeCreationIntentStore(
                    services.GetRequiredService<SqliteConnectionFactory>(), TimeProvider.System);
                await intents.BeginAsync(project.Id, taskId, project.PrimaryDirectory, null, target, token);
                await intents.MarkFailedAsync(taskId, target, "task_submission_failed", token);
            }
            var model = services.GetRequiredService<MainWindowViewModel>();
            await model.Workspace.Catalog.ReloadAsync();
            return new ProjectEditorViewportFixture(root, services, model, picker, dark, width, height, scale);
        }
        catch
        {
            await services.DisposeAsync();
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
            throw;
        }
    }

    public void Open()
    {
        Model.Workspace.Catalog.EditProjectCommand.Execute(Assert.Single(Model.Workspace.Catalog.ProjectNavigation));
        Flush();
        Assert.True(Flyout.IsOpen);
        Assert.True(Editor.IsOpen);
    }

    public void Flush()
    {
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
        TopLevel.GetTopLevel(Flyout?.Content as Visual)?.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    public void Press(Control control)
    {
        control.BringIntoView();
        Flush();
        Assert.True(control.IsEffectivelyEnabled);
        Assert.True(control.Focus(NavigationMethod.Tab));
        var inputRoot = Assert.IsAssignableFrom<TopLevel>(TopLevel.GetTopLevel(control));
        inputRoot.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.None, null);
        inputRoot.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.None, null);
        Flush();
    }

    public void SaveEvidence(string name)
    {
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_VISUAL_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        Flush();
        using var bitmap = Popup.CaptureRenderedFrame();
        Assert.NotNull(bitmap);
        bitmap.Save(Path.Combine(directory, name + ".png"), PngBitmapEncoderOptions.Default);
    }

    public void AssertHit(Control control)
    {
        var center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), Popup)!.Value;
        var hit = Assert.IsAssignableFrom<Visual>(Popup.InputHitTest(center));
        Assert.True(ReferenceEquals(control, hit) || control.IsVisualAncestorOf(hit), $"{control} is obscured at {center}.");
    }

    public async ValueTask DisposeAsync()
    {
        Flyout.Hide();
        Window.Close();
        await _services.DisposeAsync();
        SqliteTestPool.Clear(_root);
        Directory.Delete(_root, recursive: true);
    }
}
