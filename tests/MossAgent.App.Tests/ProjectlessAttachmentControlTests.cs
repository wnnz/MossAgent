using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ProjectlessAttachmentControlTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ProjectlessAttachControlHasAnAccessibleNameAndKeyboardSelectionWorksInBothThemes(bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(withProject: false);
        fixture.Workspace.ComposerText = string.Empty;
        await fixture.Workspace.Drafts.FlushAsync();
        var path = Path.Combine(fixture.DataRoot, "keyboard-selected.txt");
        await File.WriteAllTextAsync(path, "keyboard-selected content", fixture.Token);
        var picker = new WorkspaceAttachmentOperationProbe { Paths = [path] };
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: picker, conversationDrafts: fixture.Drafts);
        await workspace.LoadAsync();
        var composer = new WorkspaceComposerView { DataContext = workspace };
        var window = new AppWindow { Width = 800, Height = 600, Content = composer,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var button = Assert.Single(composer.GetVisualDescendants().OfType<AppIconButton>(), candidate => candidate.Classes.Contains("composer-attach"));
            Assert.True(button.IsEffectivelyVisible);
            Assert.True(button.IsEffectivelyEnabled);
            Assert.Equal(workspace.Attachments.AddFilesLabel, AutomationProperties.GetName(button));
            Assert.Null(button.CommandParameter);
            Assert.True(button.Focus());
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            await fixture.WaitUntilAsync(() => workspace.Attachments.Items.Count == 1);
            Assert.Equal(1, picker.PickerCalls);
            Assert.Equal("keyboard-selected content", Assert.Single(workspace.Attachments.Items).Content.Content);
            Assert.Empty(fixture.Server.Requests);
            await workspace.ShutdownAsync();
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
