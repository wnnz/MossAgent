namespace MossAgent.App.ViewModels;

public sealed record CodePreviewAnnotation(long Id, int Line, string Side, string Author, string Content, int? StartLine = null);
