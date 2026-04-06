using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 重新加载性格配置命令
/// </summary>
public class ReloadPersonalityConfigCommand : ICommand
{
    private readonly IPersonalityConfigService _configService;
    private readonly AdminConfig _adminConfig;
    private readonly ILogger<ReloadPersonalityConfigCommand> _logger;

    public ReloadPersonalityConfigCommand(
        IPersonalityConfigService configService,
        AdminConfig adminConfig,
        ILogger<ReloadPersonalityConfigCommand> logger)
    {
        _configService = configService;
        _adminConfig = adminConfig;
        _logger = logger;
    }

    public string Name => "reloadpersonality";
    public string[] Aliases => new[] { "重载性格配置", "reloadp" };
    public string Description => "【管理员】重新加载数码兽性格配置文件";

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
            _logger.LogInformation("用户 {UserId} 请求重新加载性格配置: {Path}", 
                context.OriginalUserId, configPath);

            if (!_configService.ReloadConfig())
            {
                return Task.FromResult(new CommandResult
                {
                    Success = false,
                    Message = $"❌ 重新加载性格配置失败，请检查配置文件是否存在且格式正确。\n路径: `{configPath}`"
                });
            }

            var config = _configService.Config;
            var personalities = _configService.GetAllPersonalities();
            var list = string.Join("\n", personalities.Take(10).Select(p => $"  • {p.Key}: {p.Value.Name}"));

            var message = $"""
                ✅ **数码兽性格配置已重新加载**

                **配置文件**: `{configPath}`
                **默认性格**: {config.DefaultPersonality}
                **性格数量**: {personalities.Count}

                **可用性格**:
                {list}
                {(personalities.Count > 10 ? "  ..." : "")}

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
            _logger.LogError(ex, "重新加载性格配置失败");
            return Task.FromResult(new CommandResult
            {
                Success = false,
                Message = $"❌ 重新加载配置时发生错误: {ex.Message}"
            });
        }
    }
}
