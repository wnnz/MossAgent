namespace MossAgent.Tools.Abstractions.Files;

public interface IFileReadHandleValidator
{
    /// <summary>Reject an opened file outside the captured roots before any content is read.</summary>
    /// <exception cref="UnauthorizedAccessException">The handle cannot be verified in scope.</exception>
    void Validate(FileStream stream, IReadOnlyList<string> authorizedRoots);
}
