using MossAgent.App.Services;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class FilePickerPdfFilterTests
{
    [Fact]
    public void PickerFiltersIncludePdfAndKeepAllFilesDefault()
    {
        var filters = AvaloniaFilePickerService.CreateFilters();
        Assert.Contains(filters, filter => filter.Patterns?.Contains("*.pdf") == true);
        Assert.All(filters, filter => Assert.False(string.IsNullOrWhiteSpace(filter.Name)));
    }
}
