namespace DigimonBot.Core.Services;

/// <summary>
/// 群聊模式配置接口
/// </summary>
public interface IGroupModeConfig
{
    /// <summary>
    /// 群聊数码兽模式：Separate（各自培养）/ Shared（共同培养）
    /// </summary>
    string GroupDigimonMode { get; }
}
