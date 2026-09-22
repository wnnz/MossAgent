using CodingAgent.Domain.Enums;

namespace CodingAgent.Domain.Entities;

/// <summary>记忆条目（global/project 两级）。</summary>
public class MemoryItem
{
    public int Id { get; set; }
    public MemoryScope Scope { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Tags { get; set; }
    public DateTime UpdatedAt { get; set; }
}
