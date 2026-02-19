using DigimonBot.Core.Services;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 重置命令 - 重新开始
/// </summary>
public class ResetCommand : ICommand
{
    private readonly IDigimonManager _digimonManager;

    public ResetCommand(IDigimonManager digimonManager)
    {
        _digimonManager = digimonManager;
    }

    public string Name => "reset";
    public string[] Aliases => new[] { "重置", "r" };
    public string Description => "重置数码宝贝，从蛋开始";

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        await _digimonManager.ResetAsync(context.UserId);
        
        // 构建前缀
        var prefix = context.ShouldAddPrefix && !string.IsNullOrWhiteSpace(context.UserName) 
            ? $"[{context.UserName}]的" 
            : "";
        
        return new CommandResult 
        { 
            Success = true, 
            Message = $"🥚 {prefix}**重置完成！**\n\n一颗新的数码蛋出现在你面前...\n轻轻抚摸它，等待孵化吧！" 
        };
    }
}
