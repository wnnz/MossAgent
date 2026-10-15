using System.Globalization;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ModalAvailableExtentConverterTests
{
    [Theory]
    [InlineData(596, 548)]
    [InlineData(988, 940)]
    [InlineData(100, 52)]
    [InlineData(470, 422)]
    [InlineData(0, 0)]
    [InlineData(-20, 0)]
    [InlineData(double.NaN, 520)]
    [InlineData(double.PositiveInfinity, 520)]
    public void Convert_ReservesOuterModalMargin(double viewport, double expected)
    {
        var converter = new ModalAvailableExtentConverter();

        Assert.Equal(expected, converter.Convert(viewport, typeof(double), null, CultureInfo.InvariantCulture));
    }
}
