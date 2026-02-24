using DigimonBot.Core.Models.Tavern;

namespace DigimonBot.Core.Services;

/// <summary>
/// 酒馆配置服务接口
/// </summary>
public interface ITavernConfigService
{
    /// <summary>
    /// 当前配置
    /// </summary>
    TavernConfig Config { get; }

    /// <summary>
    /// 从文件加载配置
    /// </summary>
    /// <param name="filePath">配置文件路径，null则使用默认路径</param>
    /// <returns>是否成功加载</returns>
    bool LoadConfig(string? filePath = null);

    /// <summary>
    /// 保存配置到文件
    /// </summary>
    /// <param name="filePath">配置文件路径，null则使用默认路径</param>
    /// <returns>是否成功保存</returns>
    bool SaveConfig(string? filePath = null);

    /// <summary>
    /// 重新加载配置
    /// </summary>
    /// <returns>是否成功重新加载</returns>
    bool ReloadConfig();

    /// <summary>
    /// 获取配置文件路径
    /// </summary>
    string GetConfigFilePath();

    /// <summary>
    /// 配置变更事件
    /// </summary>
    event EventHandler? OnConfigChanged;
    
    /// <summary>
    /// 更新配置（热更新）
    /// </summary>
    /// <param name="updateAction">更新操作</param>
    /// <returns>是否成功</returns>
    bool UpdateConfig(Action<TavernConfig> updateAction);
}
