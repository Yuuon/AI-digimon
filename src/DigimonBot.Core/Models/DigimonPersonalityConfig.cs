namespace DigimonBot.Core.Models;

/// <summary>
/// 数码兽性格配置
/// </summary>
public class DigimonPersonalityConfig
{
    /// <summary>
    /// 配置文件路径
    /// </summary>
    public const string DefaultConfigPath = "Data/digimon_personalities.json";

    /// <summary>
    /// 性格定义列表
    /// </summary>
    public Dictionary<string, PersonalityDefinition> Personalities { get; set; } = new();

    /// <summary>
    /// 默认性格
    /// </summary>
    public string DefaultPersonality { get; set; } = "Brave";
}

/// <summary>
/// 性格定义
/// </summary>
public class PersonalityDefinition
{
    /// <summary>
    /// 性格名称（中文）
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 性格描述
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// 系统提示词
    /// </summary>
    public string SystemPrompt { get; set; } = "";

    /// <summary>
    /// 亲和情感类型（可选）
    /// </summary>
    public string? AffinityEmotion { get; set; }
}
