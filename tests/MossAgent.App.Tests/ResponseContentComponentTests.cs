using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ResponseContentComponentTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, "Light")]
    [InlineData(true, "Light")]
    [InlineData(false, "Dark")]
    [InlineData(true, "Dark")]
    public Task SharedBlocks_PreserveTypographyCommandsAndLiveUpdates(bool compact, string theme) =>
        _session.Dispatch(() =>
        {
            var blocks = new ObservableCollection<object>(ChatContentParser.Parse("Hello **world**.\n\n```csharp\nint value = 42;\n```"));
            var view = new AppResponseContent { Blocks = blocks };
            if (compact) view.Classes.Add("compact");
            var window = new Window
            {
                Width = 420,
                Height = 500,
                Content = view,
                RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                var body = Assert.Single(view.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>(),
                    text => text.Classes.Contains("response-body") && text.IsEffectivelyVisible);
                Assert.Equal(compact ? 12.5 : 14, body.FontSize);
                Assert.Equal(compact ? 19 : 23, body.LineHeight);
                var button = Assert.Single(view.GetVisualDescendants().OfType<AppIconButton>());
                Assert.False(button.IsEffectivelyEnabled);
                Assert.Equal("复制代码", button.Label);
                var copied = new List<string?>();
                var allowed = false;
                var command = new RelayCommand<string>(copied.Add, _ => allowed);
                view.CopyCodeCommand = command;
                window.UpdateLayout();
                Assert.False(button.IsEffectivelyEnabled);
                allowed = true;
                command.NotifyCanExecuteChanged();
                window.UpdateLayout();
                Assert.True(button.IsEffectivelyEnabled);
                Assert.True(button.Focus());
                window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                Assert.Equal("int value = 42;", Assert.Single(copied));
                view.CopyCodeCommand = null;
                window.UpdateLayout();
                Assert.False(button.IsEffectivelyEnabled);
                var viewer = Assert.Single(view.GetVisualDescendants().OfType<AppScrollViewer>());
                Assert.Equal(ScrollBarVisibility.Auto, viewer.HorizontalScrollBarVisibility);
                Assert.Equal(360, viewer.MaxHeight);
                blocks.Clear();
                foreach (var block in ChatContentParser.Parse("Updated")) blocks.Add(block);
                window.UpdateLayout();
                Assert.Empty(view.GetVisualDescendants().OfType<SyntaxHighlightedSelectableTextBlock>());
                Assert.Contains(view.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>(), text => text.Markdown == "Updated");
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
}
