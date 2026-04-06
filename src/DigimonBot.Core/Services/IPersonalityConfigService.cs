using DigimonBot.Core.Models;

namespace DigimonBot.Core.Services;

/// <summary>
/// 性格配置服务接口
/// </summary>
public interface IPersonalityConfigService
{
    /// <summary>
    /// 当前配置
    /// </summary>
    DigimonPersonalityConfig Config { get; }

    /// <summary>
    /// 从文件加载配置
    /// </summary>
    bool LoadConfig(string? filePath = null);

    /// <summary>
    /// 保存配置到文件
    /// </summary>
    bool SaveConfig(string? filePath = null);

    /// <summary>
    /// 重新加载配置
    /// </summary>
    bool ReloadConfig();

    /// <summary>
    /// 获取配置文件路径
    /// </summary>
    string GetConfigFilePath();

    /// <summary>
    /// 获取性格定义
    /// </summary>
    PersonalityDefinition? GetPersonality(string personalityKey);

    /// <summary>
    /// 获取性格的系统提示词
    /// </summary>
    string GetPersonalityPrompt(string personalityKey);

    /// <summary>
    /// 获取所有可用性格
    /// </summary>
    Dictionary<string, PersonalityDefinition> GetAllPersonalities();

    /// <summary>
    /// 检查性格是否存在
    /// </summary>
    bool PersonalityExists(string personalityKey);

    /// <summary>
    /// 获取默认性格
    /// </summary>
    string GetDefaultPersonality();

    /// <summary>
    /// 配置变更事件
    /// </summary>
    event EventHandler? OnConfigChanged;
}
