using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using DigimonBot.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 酒馆模式开关指令
/// </summary>
public class TavernToggleCommand : ICommand
{
    private readonly ITavernService _tavernService;
    private readonly AdminConfig _adminConfig;
    private readonly ILogger<TavernToggleCommand> _logger;

    public TavernToggleCommand(
        ITavernService tavernService,
        AdminConfig adminConfig,
        ILogger<TavernToggleCommand> logger)
    {
        _tavernService = tavernService;
        _adminConfig = adminConfig;
        _logger = logger;
    }

    public string Name => "tavern";
    public string[] Aliases => new[] { "酒馆" };
    public string Description => "【管理员】开启/关闭酒馆模式";

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

        var arg = context.Args.Length > 0 ? context.Args[0].ToLower() : "";
        
        bool newState;
        string message;
        
        switch (arg)
        {
            case "on":
            case "开启":
            case "开":
                _tavernService.Enable();
                newState = true;
                message = "🍺 **酒馆模式已开启**\n\n现在可以加载角色并进行角色扮演对话了。\n使用 `/listchar` 查看可用角色，使用 `/loadchar [角色名]` 加载角色。";
                break;
                
            case "off":
            case "关闭":
            case "关":
                _tavernService.Disable();
                newState = false;
                message = "🍺 **酒馆模式已关闭**\n\n返回普通数码宝贝对话模式。";
                break;
                
            default:
                // 切换状态
                newState = _tavernService.Toggle();
                message = newState 
                    ? "🍺 **酒馆模式已开启**\n\n现在可以加载角色并进行角色扮演对话了。" 
                    : "🍺 **酒馆模式已关闭**\n\n返回普通数码宝贝对话模式。";
                break;
        }

        _logger.LogInformation("管理员 {UserId} 切换酒馆模式: {State}", context.OriginalUserId, newState);

        return Task.FromResult(new CommandResult
        {
            Success = true,
            Message = message
        });
    }
}

/// <summary>
/// 列出角色指令
/// </summary>
public class ListCharactersCommand : ICommand
{
    private readonly ITavernService _tavernService;

    public ListCharactersCommand(ITavernService tavernService)
    {
        _tavernService = tavernService;
    }

    public string Name => "listchar";
    public string[] Aliases => new[] { "角色列表", "charlist" };
    public string Description => "列出可用的酒馆角色";

    public Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        var characters = _tavernService.GetAvailableCharacters().ToList();
        
        if (characters.Count == 0)
        {
            return Task.FromResult(new CommandResult
            {
                Success = false,
                Message = "📂 **没有找到角色卡**\n\n请将角色卡文件（.png 或 .json）放入 `Data/Characters` 目录。"
            });
        }

        var lines = new List<string> { "📚 **可用角色列表**\n" };
        
        for (int i = 0; i < characters.Count; i++)
        {
            var charInfo = characters[i];
            var tags = charInfo.Tags.Count > 0 
                ? string.Join(", ", charInfo.Tags.Take(3)) 
                : "无标签";
            
            lines.Add($"{i + 1}. **{charInfo.Name}** ({charInfo.Format})");
            lines.Add($"   文件名: `{charInfo.FileName}`");
            if (!string.IsNullOrEmpty(tags))
            {
                lines.Add($"   标签: {tags}");
            }
            lines.Add("");
        }
        
        lines.Add("💡 **使用方式**: `/loadchar [文件名或角色名]`");

        return Task.FromResult(new CommandResult
        {
            Success = true,
            Message = string.Join("\n", lines)
        });
    }
}

/// <summary>
/// 加载角色指令
/// </summary>
public class LoadCharacterCommand : ICommand
{
    private readonly ITavernService _tavernService;
    private readonly ILogger<LoadCharacterCommand> _logger;

    public LoadCharacterCommand(
        ITavernService tavernService,
        ILogger<LoadCharacterCommand> logger)
    {
        _tavernService = tavernService;
        _logger = logger;
    }

    public string Name => "loadchar";
    public string[] Aliases => new[] { "加载角色", "load" };
    public string Description => "加载指定的酒馆角色";

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        if (context.Args.Length == 0)
        {
            return new CommandResult
            {
                Success = false,
                Message = "❌ 请指定要加载的角色名称。\n使用 `/listchar` 查看可用角色。"
            };
        }

        var characterName = string.Join(" ", context.Args);
        
        var success = await _tavernService.LoadCharacterAsync(characterName);
        
        if (!success)
        {
            return new CommandResult
            {
                Success = false,
                Message = $"❌ 加载角色失败: 找不到 `{characterName}`\n\n请使用 `/listchar` 查看可用角色，并确保输入正确的文件名或角色名。"
            };
        }

        var character = _tavernService.CurrentCharacter;
        
        _logger.LogInformation("用户 {UserId} 加载角色: {CharacterName}", 
            context.OriginalUserId, character?.Name);

        var message = $"✅ **角色加载成功！**\n\n" +
                     $"🎭 **{character?.Name}**\n";
        
        if (!string.IsNullOrEmpty(character?.Creator))
        {
            message += $"作者: {character.Creator}\n";
        }
        
        if (character?.Tags.Count > 0)
        {
            message += $"标签: {string.Join(", ", character.Tags)}\n";
        }
        
        message += $"\n💬 **开场白**:\n{character?.FirstMessage ?? "（无开场白）"}\n\n" +
                  $"现在可以使用 `@Bot /酒馆对话 [内容]` 与角色对话了！";

        return new CommandResult
        {
            Success = true,
            Message = message
        };
    }
}

/// <summary>
/// 酒馆对话指令
/// </summary>
public class TavernChatCommand : ICommand
{
    private readonly ITavernService _tavernService;
    private readonly ILogger<TavernChatCommand> _logger;

    public TavernChatCommand(
        ITavernService tavernService,
        ILogger<TavernChatCommand> logger)
    {
        _tavernService = tavernService;
        _logger = logger;
    }

    public string Name => "tavernchat";
    public string[] Aliases => new[] { "酒馆对话", "tc" };
    public string Description => "与当前加载的酒馆角色对话";

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        if (!_tavernService.IsEnabled)
        {
            return new CommandResult
            {
                Success = false,
                Message = "🍺 酒馆模式未开启。请联系管理员使用 `/酒馆 on` 开启。"
            };
        }

        if (!_tavernService.HasCharacterLoaded())
        {
            return new CommandResult
            {
                Success = false,
                Message = "🎭 没有加载角色。请使用 `/listchar` 查看角色，然后使用 `/loadchar [角色名]` 加载。"
            };
        }

        // 获取用户输入（从 Args 重建，因为 Message 已经去除了前缀）
        var userMessage = string.Join(" ", context.Args).Trim();
        
        if (string.IsNullOrEmpty(userMessage))
        {
            return new CommandResult
            {
                Success = false,
                Message = "💬 请输入要发送给角色的内容。\n使用: `@Bot /酒馆对话 你好！`"
            };
        }

        // 生成回复
        var response = await _tavernService.GenerateResponseAsync(userMessage, context.UserName);
        
        var characterName = _tavernService.CurrentCharacter?.Name ?? "角色";
        
        return new CommandResult
        {
            Success = true,
            Message = $"**{characterName}**: {response}"
        };
    }
}
