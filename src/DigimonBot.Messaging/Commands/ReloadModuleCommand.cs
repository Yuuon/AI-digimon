using DigimonBot.Core.Models;
using DigimonBot.Core.Modules;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// Command to reload modules at runtime (hot-fix support).
/// Admin-only command: /reload [module_name | all]
/// </summary>
public class ReloadModuleCommand : ICommand
{
    private readonly ModuleManager _moduleManager;
    private readonly AdminConfig _adminConfig;
    private readonly ILogger<ReloadModuleCommand> _logger;

    public string Name => "reload";
    public string[] Aliases => new[] { "reloadmod" };
    public string Description => "热重载模块 - /reload [模块名|all]";

    public ReloadModuleCommand(
        ModuleManager moduleManager,
        AdminConfig adminConfig,
        ILogger<ReloadModuleCommand> logger)
    {
        _moduleManager = moduleManager;
        _adminConfig = adminConfig;
        _logger = logger;
    }

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        // Admin-only check
        if (!_adminConfig.Whitelist.Contains(context.OriginalUserId))
        {
            return new CommandResult
            {
                Success = false,
                Message = "⚠️ 仅管理员可以使用此命令"
            };
        }

        var target = context.Args.Length > 0 ? context.Args[0] : "all";

        if (target.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            await _moduleManager.ReloadAllAsync();
            var modules = _moduleManager.Modules;
            var moduleList = string.Join(", ", modules.Select(m => m.Name));
            return new CommandResult
            {
                Success = true,
                Message = $"✅ 已重载所有模块 ({modules.Count}): {moduleList}"
            };
        }

        if (target.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            var modules = _moduleManager.Modules;
            var lines = modules.Select(m => $"• **{m.Name}** - {m.Description}");
            return new CommandResult
            {
                Success = true,
                Message = $"📦 已加载模块 ({modules.Count}):\n{string.Join("\n", lines)}"
            };
        }

        var success = await _moduleManager.ReloadModuleAsync(target);
        if (success)
        {
            return new CommandResult
            {
                Success = true,
                Message = $"✅ 模块 '{target}' 已重载"
            };
        }
        else
        {
            var available = string.Join(", ", _moduleManager.Modules.Select(m => m.Name));
            return new CommandResult
            {
                Success = false,
                Message = $"❌ 未找到模块 '{target}'\n可用模块: {available}"
            };
        }
    }
}
