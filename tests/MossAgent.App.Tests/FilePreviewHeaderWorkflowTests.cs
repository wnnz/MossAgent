using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class FilePreviewHeaderWorkflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 660)]
    [InlineData(true, 660)]
    [InlineData(false, 1600)]
    [InlineData(true, 1600)]
    public Task HeaderActionsBindRealPickerExpansionAndCloseWithoutReplacingPreview(bool dark, int width) => _session.DispatchAsync(async () =>
    {
        var picker = new FilePreviewPickerProbe { Pick = _ => Task.FromResult<IReadOnlyList<string>>(["selected.cs"]) };
        var executor = new InspectorProbeToolExecutor { Execute = (_, _, _) => Task.FromResult(new ToolResult(true, "preview", Artifacts: [new("file", "snapshot", "text/plain", 0, "hash")])) };
        using var pane = new FilePreviewPaneViewModel(executor, new PreviewProbeArtifactReader(), picker);
        pane.Attach(new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.ReadOnly));
        await pane.OpenAsync("existing.cs", TestContext.Current.CancellationToken);
        var preview = new AppFilePreview { DataContext = pane };
        var panes = new AppWorkspacePanes { PrimaryPane = new AppInput { Text = "draft" }, InspectorPane = preview, IsInspectorOpen = true };
        preview.Bind(AppFilePreview.IsExpandedProperty, new Binding(nameof(AppWorkspacePanes.IsInspectorFullWidth)) { Source = panes });
        preview.Bind(AppFilePreview.IsExpansionRequestedProperty, new Binding(nameof(AppWorkspacePanes.IsInspectorExpanded)) { Source = panes });
        preview.ExpansionCommand = panes.ToggleInspectorExpandedCommand;
        preview.ClosePaneCommand = new RelayCommand(() => panes.IsInspectorOpen = false);
        var window = new AppWindow { Width = width, Height = 500, Content = panes, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            var newTab = preview.FindControl<AppIconButton>("NewTabButton")!;
            var expand = preview.FindControl<AppIconButton>("ExpandButton")!;
            var restore = preview.FindControl<AppIconButton>("RestoreButton")!;
            var close = preview.FindControl<AppIconButton>("CloseButton")!;
            Assert.All(new[] { newTab, expand, close }, button => Assert.Equal(32, button.Bounds.Height));
            var menu = (AppFlyout)newTab.Flyout!;
            menu.ShowAt(newTab);
            Dispatcher.UIThread.RunJobs();
            var actions = ((AppCard)menu.Content!).GetVisualDescendants().OfType<AppActionButton>().ToArray();
            Assert.Equal(2, actions.Length);
            Assert.Same(pane.FilePicker.AddFilesCommand, actions[0].Command);
            await ((IAsyncRelayCommand)actions[0].Command!).ExecuteAsync(null);
            Assert.Equal("selected.cs", pane.Active!.Name);
            Assert.False(menu.IsOpen);
            var active = pane.Active;
            expand.Command!.Execute(null);
            window.UpdateLayout();
            Assert.True(preview.IsExpanded);
            Assert.False(expand.IsEffectivelyVisible);
            Assert.True(restore.IsEffectivelyVisible);
            Assert.Equal(width, preview.Bounds.Width, 3);
            restore.Command!.Execute(null);
            window.UpdateLayout();
            Assert.False(preview.IsExpanded);
            Assert.Same(active, pane.Active);
            close.Command!.Execute(null);
            window.UpdateLayout();
            Assert.False(panes.IsInspectorOpen);
            Assert.False(panes.IsInspectorExpanded);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task MissingExpansionCommandIsClearlyUnavailable() => _session.Dispatch(() =>
    {
        var preview = new AppFilePreview();
        var window = new AppWindow { Content = preview, Width = 600, Height = 400 };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.False(preview.FindControl<AppIconButton>("ExpandButton")!.IsEffectivelyEnabled);
            Assert.False(preview.FindControl<AppIconButton>("NewTabButton")!.IsEffectivelyEnabled);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("back")]
    [InlineData("context")]
    [InlineData("ancestor-hidden")]
    [InlineData("ancestor-disabled")]
    [InlineData("detach")]
    public Task PreviewOwnershipChangeRestoresPrimaryPane(string change) => _session.DispatchAsync(async () =>
    {
        using var fixture = new FilePreviewActionFixture();
        await fixture.OpenAsync();
        var preview = new AppFilePreview { DataContext = fixture.Pane };
        preview.Bind(Avalonia.Visual.IsVisibleProperty, new Binding(nameof(FilePreviewPaneViewModel.IsOpen)) { Source = fixture.Pane });
        var host = new AppBorder { Child = preview };
        var draft = new AppInput { Text = "unchanged draft" };
        var panes = new AppWorkspacePanes { PrimaryPane = draft, InspectorPane = host, IsInspectorOpen = true };
        preview.Bind(AppFilePreview.IsExpandedProperty, new Binding(nameof(AppWorkspacePanes.IsInspectorFullWidth)) { Source = panes });
        preview.Bind(AppFilePreview.IsExpansionRequestedProperty, new Binding(nameof(AppWorkspacePanes.IsInspectorExpanded)) { Source = panes });
        preview.ExpansionCommand = panes.ToggleInspectorExpandedCommand;
        var window = new AppWindow { Content = panes, Width = 1000, Height = 500 };
        window.Show();
        try
        {
            panes.ToggleInspectorExpandedCommand.Execute(null);
            window.UpdateLayout();
            Assert.True(panes.IsInspectorExpanded);
            switch (change)
            {
                case "back":
                    var newTab = preview.FindControl<AppIconButton>("NewTabButton")!;
                    newTab.Flyout!.ShowAt(newTab);
                    Dispatcher.UIThread.RunJobs();
                    preview.FindControl<AppActionButton>("BackButton")!.Command!.Execute(null);
                    break;
                case "context": preview.DataContext = null; break;
                case "ancestor-hidden": host.IsVisible = false; break;
                case "ancestor-disabled": host.IsEnabled = false; break;
                case "detach": host.Child = null; break;
            }
            window.UpdateLayout();
            Assert.False(panes.IsInspectorExpanded);
            Assert.True(draft.IsEffectivelyVisible);
            Assert.Same(draft, panes.PrimaryPane);
            Assert.Equal("unchanged draft", draft.Text);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

}
