using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.App.Tests;

internal sealed class TrackedArtifactReadValidator : IFileReadHandleValidator
{
    private readonly List<string> _stacks = [];

    public string? ArtifactRoot { get; set; }
    public bool Refuse { get; set; }
    public Action? BeforeArtifactRead { get; set; }
    public int ArtifactReads => _stacks.Count;
    public IReadOnlyList<string> Stacks => _stacks;
    public FileStream? ArtifactStream { get; private set; }

    public void Validate(FileStream stream, IReadOnlyList<string> roots)
    {
        if (roots.Count == 1 && roots[0].Equals(ArtifactRoot, StringComparison.OrdinalIgnoreCase))
        {
            Xunit.Assert.Equal(0, stream.Position);
            _stacks.Add(Environment.StackTrace);
            ArtifactStream = stream;
            BeforeArtifactRead?.Invoke();
            if (Refuse) throw new UnauthorizedAccessException("synthetic private diagnostics must remain hidden");
        }
        new WindowsFileReadHandleValidator().Validate(stream, roots);
    }
}
