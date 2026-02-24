using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 重新加载酒馆配置命令
/// </summary>
public class ReloadTavernConfigCommand : ICommand
{
    private readonly ITavernConfigService _configService;
    private readonly AdminConfig _adminConfig;
    private readonly ILogger<ReloadTavernConfigCommand> _logger;

    public ReloadTavernConfigCommand(
        ITavernConfigService configService,
        AdminConfig adminConfig,
        ILogger<ReloadTavernConfigCommand> logger)
    {
        _configService = configService;
        _adminConfig = adminConfig;
        _logger = logger;
    }

    public string Name => "reloadtavern";
    public string[] Aliases => new[] { "重载酒馆配置", "reloadt" };
    public string Description => "【管理员】重新加载酒馆配置文件";

    public Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        // 检查管理员权限
        if (!_adminConfig.Whitelist.Contains(context.OriginalUserId))
        {
            return Task.FromResult(new CommandResult
            {
                Success = false,
                Message = "❌ 你没有权限使用此指令。"
            });
        }

        try
        {
            var configPath = _configService.GetConfigFilePath();
            _logger.LogInformation("用户 {UserId} 请求重新加载酒馆配置: {Path}", 
                context.OriginalUserId, configPath);

            if (!_configService.ReloadConfig())
            {
                return Task.FromResult(new CommandResult
                {
                    Success = false,
                    Message = $"❌ 重新加载配置失败，请检查配置文件是否存在且格式正确。\n路径: `{configPath}`"
                });
            }

            var config = _configService.Config;
            var message = $"""
                ✅ **酒馆配置已重新加载**

                **配置文件**: `{configPath}`

                **当前配置**:
                • 监测消息数: {config.Monitor.MinMessageCount} 条
                • 关键词阈值: {config.Monitor.KeywordThreshold} 次
                • 触发间隔: {config.Monitor.TriggerIntervalMinutes} 分钟
                • 自主发言: {(config.AutoSpeak.Enabled ? "✅ 启用" : "❌ 禁用")}
                • 角色目录: `{config.CharacterDirectory}`
                • 调试日志: {(config.EnableDebugLog ? "✅ 启用" : "❌ 禁用")}

                配置已生效，无需重启程序。
                """;

            return Task.FromResult(new CommandResult
            {
                Success = true,
                Message = message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重新加载酒馆配置失败");
            return Task.FromResult(new CommandResult
            {
                Success = false,
                Message = $"❌ 重新加载配置时发生错误: {ex.Message}"
            });
        }
    }
}
