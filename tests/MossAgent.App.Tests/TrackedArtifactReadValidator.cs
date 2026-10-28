using MossAgent.Infrastructure.Persistence;
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

    /// <summary>Reads that came through the artifact content path itself. Other authorized
    /// consumers — e.g. change-statistics snapshot reads — may legitimately open files under
    /// the artifact root in the same window and must not count against the preview contract.</summary>
    public int PreviewReads => _stacks.Count(stack => stack.Contains(nameof(TaskArtifactContentReader)));
    public IReadOnlyList<string> Stacks => _stacks;
    public FileStream? ArtifactStream { get; private set; }

    public void Validate(FileStream stream, IReadOnlyList<string> roots)
    {
        if (roots.Count == 1 && roots[0].Equals(ArtifactRoot, StringComparison.OrdinalIgnoreCase))
        {
            Xunit.Assert.Equal(0, stream.Position);
            var stack = Environment.StackTrace;
            _stacks.Add(stack);
            // Preview-path state (stream handle and the cancel hook) tracks only the pane's
            // artifact read; an unrelated authorized open (change statistics, snapshot
            // validation) must not pre-fire the cancellation or claim the tracked handle.
            if (stack.Contains(nameof(TaskArtifactContentReader)))
            {
                ArtifactStream = stream;
                BeforeArtifactRead?.Invoke();
            }
            if (Refuse) throw new UnauthorizedAccessException("synthetic private diagnostics must remain hidden");
        }
        new WindowsFileReadHandleValidator().Validate(stream, roots);
    }
}
