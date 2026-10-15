using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class CompactInspectorInteractionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task AutomaticFullWidthUsesRealHeaderStateAndCloseWithoutSubmittingDraft(bool dark, bool filePreview) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
            var project = fixture.Workspace.Catalog.SelectedProject!;
            var created = DateTimeOffset.UtcNow;
            var task = new AgentTask(Guid.NewGuid(), project.Id, "Compact inspector", ApprovalPolicy.ReadOnly,
                AgentTaskStatus.Completed, created, created, null);
            await fixture.Tasks.SaveTaskAsync(task, fixture.Token);
            fixture.Workspace.Catalog.UpsertTask(task);
            if (filePreview)
            {
                var path = Path.Combine(project.PrimaryDirectory, "compact-preview.txt");
                await File.WriteAllTextAsync(path, "Synthetic compact preview", fixture.Token);
                await fixture.Workspace.FilePreview.OpenAsync(path, fixture.Token);
                Assert.True(fixture.Workspace.FilePreview.Active!.IsReady);
            }
            fixture.Main.IsRightPanelOpen = true;
            var inspector = new InspectorPane { DataContext = fixture.Main };
            var chat = new WorkspaceChatView { DataContext = fixture.Workspace };
            var panes = new AppWorkspacePanes { PrimaryPane = chat, InspectorPane = inspector };
            panes.Bind(AppWorkspacePanes.IsInspectorOpenProperty,
                new Binding(nameof(MainWindowViewModel.IsRightPanelOpen)) { Source = fixture.Main, Mode = BindingMode.TwoWay });
            var window = new AppWindow
            {
                DataContext = fixture.Main,
                Content = panes,
                Width = 1000,
                Height = 601,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                Assert.True(chat.IsEffectivelyVisible);
                window.Width = 470;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.True(panes.IsInspectorFullWidth);
                Assert.False(panes.IsInspectorExpanded);
                Assert.False(chat.IsEffectivelyVisible);
                Assert.Equal(470, inspector.Bounds.Width, 3);
                var close = GetCloseButton(inspector, filePreview);
                var draft = fixture.Workspace.ComposerText;
                Assert.True(close.Focus());
                close.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
                close.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
                window.UpdateLayout();
                Assert.False(fixture.Main.IsRightPanelOpen);
                Assert.False(panes.IsInspectorFullWidth);
                Assert.True(chat.IsEffectivelyVisible);
                Assert.Equal(draft, fixture.Workspace.ComposerText);
                Assert.Empty(fixture.Server.Requests);
                Assert.Same(chat, panes.PrimaryPane);
                Assert.Same(inspector, panes.InspectorPane);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static AppIconButton GetCloseButton(InspectorPane inspector, bool filePreview)
    {
        if (filePreview)
        {
            var preview = Assert.Single(inspector.GetVisualDescendants().OfType<AppFilePreview>());
            Assert.True(preview.IsExpanded);
            Assert.True(preview.IsEffectivelyVisible);
            var restore = preview.FindControl<AppIconButton>("RestoreButton")!;
            Assert.True(restore.IsEffectivelyVisible);
            Assert.False(restore.IsEffectivelyEnabled);
            return preview.FindControl<AppIconButton>("CloseButton")!;
        }
        var header = Assert.Single(inspector.GetVisualDescendants().OfType<AppInspectorHeader>());
        Assert.True(header.IsExpanded);
        Assert.True(header.FindControl<AppIconButton>("ExpansionButton")!.IsAlternateIcon);
        Assert.False(header.FindControl<AppIconButton>("ExpansionButton")!.IsEffectivelyEnabled);
        return header.FindControl<AppIconButton>("CloseButton")!;
    }
}
