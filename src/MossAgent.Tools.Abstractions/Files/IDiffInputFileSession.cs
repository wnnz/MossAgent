namespace MossAgent.Tools.Abstractions.Files;

/// <summary>Owns an immutable temporary input and its directory identity until disposal.</summary>
public interface IDiffInputFileSession : IDisposable
{
    string Path { get; }
}
