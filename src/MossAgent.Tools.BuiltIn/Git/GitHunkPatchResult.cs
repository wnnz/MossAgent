namespace MossAgent.Tools.BuiltIn.Git;

public sealed record GitHunkPatchResult(string Patch, int SelectedHunks, int TotalHunks);
