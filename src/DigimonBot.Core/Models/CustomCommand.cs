namespace DigimonBot.Core.Models;

/// <summary>
/// 自定义命令实体
/// </summary>
public class CustomCommand
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string[] Aliases { get; set; } = Array.Empty<string>();
    public string BinaryPath { get; set; } = "";
    public string OwnerUserId { get; set; } = "";
    public bool RequiresWhitelist { get; set; }
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int UseCount { get; set; }
}
