using System.Collections.ObjectModel;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AgentModelSelectionTests
{
    [Fact]
    public void EmptyCatalog_DoesNotPreventSelectingTheFirstAvailableModel()
    {
        var selection = new AgentModelSelection();
        ObservableCollection<AgentModelOption> options = [];
        var model = CreateOption("first");

        Assert.Null(selection.Publish(options, [], allowInitialSelection: true));
        Assert.Same(model, selection.Publish(options, [model], allowInitialSelection: true));
        Assert.True(selection.HasPreferredModel);
    }

    [Fact]
    public void ExistingProfile_DoesNotReceiveAnAutomaticFallback()
    {
        var selection = new AgentModelSelection();
        ObservableCollection<AgentModelOption> options = [];
        var model = CreateOption("first");

        Assert.Null(selection.Publish(options, [model], allowInitialSelection: false));
        Assert.Null(selection.Publish(options, [model], allowInitialSelection: true));
        Assert.False(selection.HasPreferredModel);
    }

    [Fact]
    public void SelectedModel_IsPreservedWhenTheCatalogOrderChanges()
    {
        var selection = new AgentModelSelection();
        ObservableCollection<AgentModelOption> options = [];
        var first = CreateOption("first");
        var second = CreateOption("second");
        selection.Publish(options, [first, second], allowInitialSelection: true);
        Assert.True(selection.Select(second));

        Assert.Same(second, selection.Publish(options, [second, first], allowInitialSelection: true));
        Assert.Equal([second, first], options);
    }

    [Fact]
    public void MissingSelection_IsNotReplacedByAnotherProvidersModel()
    {
        var selection = new AgentModelSelection();
        ObservableCollection<AgentModelOption> options = [];
        var preferred = CreateOption("preferred");
        var foreign = CreateOption("foreign", modelId: preferred.Model.Id);
        var alternative = CreateOption("alternative");
        selection.Publish(options, [preferred], allowInitialSelection: true);

        Assert.Null(selection.Publish(options, [foreign, alternative], allowInitialSelection: true));
        Assert.Null(selection.Publish(options, [foreign, alternative], allowInitialSelection: true));
        Assert.True(selection.HasPreferredModel);
        Assert.Same(preferred, selection.Publish(options, [foreign, preferred, alternative], allowInitialSelection: true));
    }

    [Fact]
    public void ExplicitlyClearedSelection_IsNotAutomaticallyRestored()
    {
        var selection = new AgentModelSelection();
        ObservableCollection<AgentModelOption> options = [];
        var model = CreateOption("first");
        selection.Publish(options, [model], allowInitialSelection: true);

        Assert.True(selection.Select(null));
        Assert.Null(selection.Publish(options, [], allowInitialSelection: true));
        Assert.Null(selection.Publish(options, [model], allowInitialSelection: true));
        Assert.False(selection.HasPreferredModel);
    }

    [Fact]
    public void EqualCatalog_PreservesReferencesWithoutCollectionNotifications()
    {
        var selection = new AgentModelSelection();
        ObservableCollection<AgentModelOption> options = [];
        var original = CreateOption("first");
        selection.Publish(options, [original], allowInitialSelection: true);
        var changes = 0;
        options.CollectionChanged += (_, _) => changes++;
        var equal = original with { Model = original.Model with { } };

        Assert.NotSame(original, equal);
        Assert.Same(original, selection.Publish(options, [equal], allowInitialSelection: true));
        Assert.Same(original, Assert.Single(options));
        Assert.Equal(0, changes);
    }

    [Fact]
    public void ChangedCatalog_RefreshesTheSelectedSnapshot()
    {
        var selection = new AgentModelSelection();
        ObservableCollection<AgentModelOption> options = [];
        var original = CreateOption("first");
        selection.Publish(options, [original], allowInitialSelection: true);
        var updated = original with { Model = original.Model with { DisplayName = "updated", ContextLength = 24576 } };

        Assert.Same(updated, selection.Publish(options, [updated], allowInitialSelection: true));
        Assert.Same(updated, Assert.Single(options));
        Assert.False(selection.Select(updated));
    }

    [Fact]
    public void Restore_RemembersAnUnavailableProfileModelUntilItReturns()
    {
        var selection = new AgentModelSelection();
        ObservableCollection<AgentModelOption> options = [];
        var preferred = CreateOption("preferred");
        var alternative = CreateOption("alternative");

        Assert.Null(selection.Restore(preferred.Model.ProviderId, preferred.Model.Id, options));
        Assert.Null(selection.Publish(options, [alternative], allowInitialSelection: true));
        Assert.Same(preferred, selection.Publish(options, [alternative, preferred], allowInitialSelection: true));
    }

    [Fact]
    public void Restore_UsesTheExistingCollectionReference()
    {
        var selection = new AgentModelSelection();
        var model = CreateOption("preferred");
        ObservableCollection<AgentModelOption> options = [CreateOption("alternative"), model];

        Assert.Same(model, selection.Restore(model.Model.ProviderId, model.Model.Id, options));
    }

    [Fact]
    public void IndependentEditors_DoNotShareSelectionState()
    {
        var first = new AgentModelSelection();
        var second = new AgentModelSelection();
        var preferred = CreateOption("preferred");
        var alternative = CreateOption("alternative");
        first.Select(preferred);
        second.Select(alternative);

        Assert.Same(preferred, first.Publish([], [preferred, alternative], allowInitialSelection: true));
        Assert.Same(alternative, second.Publish([], [preferred, alternative], allowInitialSelection: true));
    }

    [Fact]
    public void Select_ReportsOnlyIdentityChanges()
    {
        var selection = new AgentModelSelection();
        var model = CreateOption("preferred");

        Assert.False(selection.Select(null));
        Assert.True(selection.Select(model));
        Assert.False(selection.Select(model with { ProviderName = "renamed" }));
        Assert.True(selection.Select(CreateOption("foreign", modelId: model.Model.Id)));
        Assert.True(selection.Select(null));
        Assert.False(selection.Select(null));
    }

    private static AgentModelOption CreateOption(string name, Guid? modelId = null) =>
        new(new ModelProfile(modelId ?? Guid.NewGuid(), Guid.NewGuid(), name, name, 16384, 2048, "medium",
            null, null, false, false, false, true, "{}"), "fixture", ProviderProtocol.OpenAiResponses);
}
