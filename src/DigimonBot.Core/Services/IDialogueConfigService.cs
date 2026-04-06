using DigimonBot.Core.Models;

namespace DigimonBot.Core.Services;

/// <summary>
/// 数码兽对话配置服务接口
/// </summary>
public interface IDialogueConfigService
{
    /// <summary>
    /// 当前配置
    /// </summary>
    DigimonDialogueConfig Config { get; }

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
    /// 配置变更事件
    /// </summary>
    event EventHandler? OnConfigChanged;
}
