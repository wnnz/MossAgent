using Xunit;

namespace MossAgent.App.Tests;

[CollectionDefinition(AllocationMeasurementCollection.Name, DisableParallelization = true)]
public sealed class AllocationMeasurementCollection
{
    public const string Name = "Isolated allocation measurements";
}
