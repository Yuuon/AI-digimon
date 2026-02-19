using DigimonBot.Core.Services;
using Microsoft.Extensions.Options;

namespace DigimonBot.Host.Configs;

/// <summary>
/// 群聊模式配置实现
/// </summary>
public class GroupModeConfig : IGroupModeConfig
{
    private readonly AppSettings _settings;

    public GroupModeConfig(IOptions<AppSettings> settings)
    {
        _settings = settings.Value;
    }

    public string GroupDigimonMode => _settings.QQBot.NapCat.GroupDigimonMode;
}
