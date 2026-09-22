namespace CodingAgent.Domain.Entities;

/// <summary>技能（Markdown 正文），系统提示词只列索引，按名加载正文。</summary>
public class Skill
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
