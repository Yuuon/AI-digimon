using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 特别关注管理命令
/// </summary>
public class SpecialFocusCommand : ICommand
{
    private readonly ITavernConfigService _configService;
    private readonly AdminConfig _adminConfig;
    private readonly ILogger<SpecialFocusCommand> _logger;

    public SpecialFocusCommand(
        ITavernConfigService configService,
        AdminConfig adminConfig,
        ILogger<SpecialFocusCommand> logger)
    {
        _configService = configService;
        _adminConfig = adminConfig;
        _logger = logger;
    }

    public string Name => "specialfocus";
    public string[] Aliases => new[] { "特别关注", "sf" };
    public string Description => "【管理员】管理特别关注用户列表";

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

        var args = context.Args;
        if (args.Length == 0)
        {
            return Task.FromResult(ShowStatus());
        }

        var action = args[0].ToLower();
        
        return action switch
        {
            "add" or "添加" => AddUser(args, context.OriginalUserId),
            "remove" or "删除" or "del" => RemoveUser(args, context.OriginalUserId),
            "list" or "列表" or "ls" => ShowList(),
            "clear" or "清空" => ClearList(context.OriginalUserId),
            "enable" or "开启" => ToggleEnabled(true),
            "disable" or "关闭" => ToggleEnabled(false),
            "cooldown" or "冷却" => SetCooldown(args),
            "mention" or "要求@" => ToggleMention(args),
            _ => Task.FromResult(new CommandResult
            {
                Success = false,
                Message = "❌ 未知操作。可用操作：add/remove/list/clear/enable/disable/cooldown/mention"
            })
        };
    }

    private CommandResult ShowStatus()
    {
        var config = _configService.Config.SpecialFocus;
        var userList = config.UserIds.Count > 0 
            ? string.Join("\n", config.UserIds.Select((id, i) => $"  {i + 1}. `{id}`"))
            : "  (暂无)";

        return new CommandResult
        {
            Success = true,
            Message = $"""
                📋 **特别关注设置**

                **状态**: {(config.Enabled ? "✅ 启用" : "❌ 禁用")}
                **冷却时间**: {config.CooldownMinutes} 分钟
                **要求@Bot**: {(config.RequireMention ? "✅ 是" : "❌ 否")}
                **关注用户数**: {config.UserIds.Count}

                **用户列表**:
                {userList}

                **使用示例**:
                `/sf add 123456789` - 添加QQ号到关注列表
                `/sf add 123456789@g757123426` - 仅关注指定群内的用户
                `/sf remove 123456789` - 移除用户
                `/sf cooldown 5` - 设置冷却时间为5分钟
                `/sf enable` - 启用特别关注
                `/sf mention on` - 要求必须@Bot才回复
                """
        };
    }

    private Task<CommandResult> AddUser(string[] args, string adminId)
    {
        if (args.Length < 2)
        {
            return Task.FromResult(new CommandResult
            {
                Success = false,
                Message = "❌ 请提供要添加的QQ号。\n使用: `/sf add 123456789` 或 `/sf add 123456789@g757123426`"
            });
        }

        var userId = args[1];
        
        // 验证QQ号格式
        if (!userId.Contains('@') && !long.TryParse(userId, out _))
        {
            return Task.FromResult(new CommandResult
            {
                Success = false,
                Message = "❌ 无效的QQ号格式。"
            });
        }

        _configService.UpdateConfig(config =>
        {
            if (!config.SpecialFocus.UserIds.Contains(userId))
            {
                config.SpecialFocus.UserIds.Add(userId);
            }
        });

        _logger.LogInformation("管理员 {Admin} 添加特别关注用户: {UserId}", adminId, userId);

        return Task.FromResult(new CommandResult
        {
            Success = true,
            Message = $"✅ 已添加特别关注用户: `{userId}`"
        });
    }

    private Task<CommandResult> RemoveUser(string[] args, string adminId)
    {
        if (args.Length < 2)
        {
            return Task.FromResult(new CommandResult
            {
                Success = false,
                Message = "❌ 请提供要移除的QQ号。"
            });
        }

        var userId = args[1];
        
        _configService.UpdateConfig(config =>
        {
            config.SpecialFocus.UserIds.Remove(userId);
        });

        _logger.LogInformation("管理员 {Admin} 移除特别关注用户: {UserId}", adminId, userId);

        return Task.FromResult(new CommandResult
        {
            Success = true,
            Message = $"✅ 已移除特别关注用户: `{userId}`"
        });
    }

    private Task<CommandResult> ShowList()
    {
        var config = _configService.Config.SpecialFocus;
        
        if (config.UserIds.Count == 0)
        {
            return Task.FromResult(new CommandResult
            {
                Success = true,
                Message = "📋 **特别关注列表**\n\n暂无关注用户。\n\n使用 `/sf add <QQ号>` 添加。"
            });
        }

        var list = string.Join("\n", config.UserIds.Select((id, i) =>
        {
            var suffix = id.Contains('@') ? " (指定群)" : " (所有群)";
            return $"  {i + 1}. `{id}`{suffix}";
        }));

        return Task.FromResult(new CommandResult
        {
            Success = true,
            Message = $"""
                📋 **特别关注列表** ({config.UserIds.Count}人)

                {list}

                使用 `/sf remove <QQ号>` 移除用户
                """
        });
    }

    private Task<CommandResult> ClearList(string adminId)
    {
        _configService.UpdateConfig(config =>
        {
            config.SpecialFocus.UserIds.Clear();
        });

        _logger.LogInformation("管理员 {Admin} 清空特别关注列表", adminId);

        return Task.FromResult(new CommandResult
        {
            Success = true,
            Message = "✅ 已清空特别关注列表。"
        });
    }

    private Task<CommandResult> ToggleEnabled(bool enabled)
    {
        _configService.UpdateConfig(config =>
        {
            config.SpecialFocus.Enabled = enabled;
        });

        return Task.FromResult(new CommandResult
        {
            Success = true,
            Message = enabled 
                ? "✅ 特别关注功能已**启用**。"
                : "❌ 特别关注功能已**禁用**。"
        });
    }

    private Task<CommandResult> SetCooldown(string[] args)
    {
        if (args.Length < 2 || !int.TryParse(args[1], out var minutes) || minutes < 1)
        {
            return Task.FromResult(new CommandResult
            {
                Success = false,
                Message = "❌ 请提供有效的冷却时间（分钟，至少1分钟）。\n使用: `/sf cooldown 3`"
            });
        }

        _configService.UpdateConfig(config =>
        {
            config.SpecialFocus.CooldownMinutes = minutes;
        });

        return Task.FromResult(new CommandResult
        {
            Success = true,
            Message = $"✅ 特别关注冷却时间已设置为 **{minutes} 分钟**。"
        });
    }

    private Task<CommandResult> ToggleMention(string[] args)
    {
        if (args.Length < 2)
        {
            var current = _configService.Config.SpecialFocus.RequireMention;
            return Task.FromResult(new CommandResult
            {
                Success = true,
                Message = $"📋 当前设置：要求@Bot = {(current ? "✅ 是" : "❌ 否")}\n\n使用 `/sf mention on/off` 切换。"
            });
        }

        var requireMention = args[1].ToLower() switch
        {
            "on" or "true" or "yes" or "1" => true,
            "off" or "false" or "no" or "0" => false,
            _ => _configService.Config.SpecialFocus.RequireMention
        };

        _configService.UpdateConfig(config =>
        {
            config.SpecialFocus.RequireMention = requireMention;
        });

        return Task.FromResult(new CommandResult
        {
            Success = true,
            Message = requireMention
                ? "✅ 已设置为：**必须@Bot才回复**关注用户的发言。"
                : "✅ 已设置为：**无需@Bot**即可回复关注用户的发言。"
        });
    }
}
