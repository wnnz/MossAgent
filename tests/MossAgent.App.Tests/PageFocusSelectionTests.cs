using Avalonia.Controls;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PageFocusSelectionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void FirstAvailableFieldWinsWithoutChangingVisualOrder(int fieldKind)
    {
        Control first = fieldKind switch
        {
            0 => new AppInput(),
            1 => new AppComboBox(),
            _ => new AppNumericUpDown()
        };
        var remembered = new AppButton();
        var hidden = new Grid { IsVisible = false, Children = { new AppInput() } };
        var disabled = new Grid { IsEnabled = false, Children = { new AppInput() } };
        var page = new Grid
        {
            Children = { remembered, hidden, disabled, first, new AppInput(), new AppComboBox() }
        };

        Assert.Same(first, AppPageSwitcher.SelectFocusTarget(page, null));
        Assert.Same(remembered, AppPageSwitcher.SelectFocusTarget(page, remembered));
        first.IsVisible = false;
        Assert.Same(page.Children[4], AppPageSwitcher.SelectFocusTarget(page, first));
    }

    [Fact]
    public void FocusablePagePrecedesItsDescendantsAsFallback()
    {
        var page = new Grid { Focusable = true, Children = { new AppButton() } };
        Assert.Same(page, AppPageSwitcher.SelectFocusTarget(page, null));
        var input = new AppInput();
        page.Children.Add(input);
        Assert.Same(input, AppPageSwitcher.SelectFocusTarget(page, null));
        Assert.Same(page, AppPageSwitcher.SelectFocusTarget(page, page));
    }

    [Fact]
    public void FirstField_DoesNotMaterializeTheRemainingPage()
    {
        var input = new AppInput();
        var page = new Grid { Children = { new AppButton(), input } };
        for (var index = 0; index < 256; index++) page.Children.Add(new AppButton());
        for (var index = 0; index < 32; index++) AppPageSwitcher.SelectFocusTarget(page, null);

        var before = GC.GetAllocatedBytesForCurrentThread();
        Control? selected = null;
        for (var index = 0; index < 128; index++) selected = AppPageSwitcher.SelectFocusTarget(page, null);
        var allocatedPerSelection = (GC.GetAllocatedBytesForCurrentThread() - before) / 128;

        Assert.Same(input, selected);
        Assert.InRange(allocatedPerSelection, 0, 1024);
    }
}
