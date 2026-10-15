using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProxySelectionTests
{
    [Theory]
    [InlineData("直连")]
    [InlineData("Direct connection")]
    public void EmptyCatalog_ContainsOnlyTheProvidedDirectLabel(string directLabel)
    {
        var option = Assert.Single(ProxySelection.FromProfiles([], directLabel));

        Assert.Null(option.Id);
        Assert.Equal(directLabel, option.Name);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Catalog_FiltersOnlyByEnabledState(bool isEnabled, bool isDefault)
    {
        var profile = CreateProfile("proxy") with { IsEnabled = isEnabled, IsDefault = isDefault };
        var options = ProxySelection.FromProfiles([profile], "直连").ToArray();

        Assert.Equal(isEnabled ? 2 : 1, options.Length);
        Assert.Null(options[0].Id);
        if (isEnabled) Assert.Equal(new ProxySelection(profile.Id, profile.Name), options[1]);
    }

    [Fact]
    public void Catalog_PreservesOrderAndDuplicateOccurrences()
    {
        var first = CreateProfile("same-name");
        var disabled = CreateProfile("disabled") with { IsEnabled = false };
        var second = CreateProfile("same-name") with { IsDefault = true };
        var profiles = new[] { first, disabled, second, first };

        var options = ProxySelection.FromProfiles(profiles, "直连").ToArray();

        Assert.Equal([null, first.Id, second.Id, first.Id], options.Select(option => option.Id));
        Assert.Equal(["直连", first.Name, second.Name, first.Name], options.Select(option => option.Name));
        Assert.Same(disabled, profiles[1]);
        Assert.False(first.IsDefault);
    }

    [Fact]
    public void RepeatedEnumeration_ProducesIndependentOptionsWithoutCachingCredentials()
    {
        var profile = CreateProfile("proxy");
        var projection = ProxySelection.FromProfiles([profile], "直连");
        var first = projection.ToArray();

        var second = projection.ToArray();

        Assert.Equal(first, second);
        Assert.NotSame(first[0], second[0]);
        Assert.NotSame(first[1], second[1]);
        Assert.Equal(new ProxySelection(profile.Id, profile.Name), second[1]);
        Assert.DoesNotContain("fixture-password", second[1].ToString());
        Assert.DoesNotContain("fixture-user", second[1].ToString());
    }

    private static ProxyProfile CreateProfile(string name) => new(
        Guid.NewGuid(), name, ProxyProtocol.Http, "127.0.0.1", 8080,
        "fixture-user", "fixture-password", false, true);
}
