namespace MossAgent.Application.Attachments;

public sealed record DocumentTextExtraction(string Text, int PageCount, int TotalPages, bool Truncated);
