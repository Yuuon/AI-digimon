namespace DigimonBot.Core.Models.Kimi;

/// <summary>
/// Kimi仓库实体
/// </summary>
public class KimiRepository
{
    /// <summary>
    /// 仓库ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 仓库名称
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 仓库路径
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// 是否为当前活动仓库
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 最后使用时间
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// 会话计数
    /// </summary>
    public int SessionCount { get; set; }
}
