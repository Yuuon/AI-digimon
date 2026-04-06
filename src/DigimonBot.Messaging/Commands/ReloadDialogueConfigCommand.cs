using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 重新加载对话配置命令
/// </summary>
public class ReloadDialogueConfigCommand : ICommand
{
    private readonly IDialogueConfigService _configService;
    private readonly AdminConfig _adminConfig;
    private readonly ILogger<ReloadDialogueConfigCommand> _logger;

    public ReloadDialogueConfigCommand(
        IDialogueConfigService configService,
        AdminConfig adminConfig,
        ILogger<ReloadDialogueConfigCommand> logger)
    {
        _configService = configService;
        _adminConfig = adminConfig;
        _logger = logger;
    }

    public string Name => "reloaddialogue";
    public string[] Aliases => new[] { "重载对话配置", "reloadd" };
    public string Description => "【管理员】重新加载数码兽对话配置文件";

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
            _logger.LogInformation("用户 {UserId} 请求重新加载对话配置: {Path}", 
                context.OriginalUserId, configPath);

            if (!_configService.ReloadConfig())
            {
                return Task.FromResult(new CommandResult
                {
                    Success = false,
                    Message = $"❌ 重新加载对话配置失败，请检查配置文件是否存在且格式正确。\n路径: `{configPath}`"
                });
            }

            var config = _configService.Config;
            var message = $"""
                ✅ **数码兽对话配置已重新加载**

                **配置文件**: `{configPath}`

                **当前配置**:
                • 战斗提示词: {(string.IsNullOrEmpty(config.Battle.AttackPrompt) ? "❌" : "✅")}
                • 签到问候语: {config.CheckIn.GreetingTemplates.Count} 条
                • 进化公告: {(string.IsNullOrEmpty(config.Evolution.EvolutionAnnouncement) ? "❌" : "✅")}
                • 闲置提示: {config.Idle.IdlePrompts.Count} 条
                • 情感响应: {(string.IsNullOrEmpty(config.EmotionResponses.HighCourage) ? "❌" : "✅")}

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
            _logger.LogError(ex, "重新加载对话配置失败");
            return Task.FromResult(new CommandResult
            {
                Success = false,
                Message = $"❌ 重新加载配置时发生错误: {ex.Message}"
            });
        }
    }
}
