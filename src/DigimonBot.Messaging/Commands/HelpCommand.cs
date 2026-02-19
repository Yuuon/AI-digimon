namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 帮助命令
/// </summary>
public class HelpCommand : ICommand
{
    private readonly CommandRegistry _registry;

    public HelpCommand(CommandRegistry registry)
    {
        _registry = registry;
    }

    public string Name => "help";
    public string[] Aliases => new[] { "帮助", "h", "?" };
    public string Description => "显示帮助信息";

    public Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        var commands = _registry.GetAllCommands();
        
        var lines = new List<string>
        {
            "🤖 **数码宝贝Bot 指令列表**",
            "",
            "指令触发前缀：`/` 或 `！`",
            ""
        };

        foreach (var cmd in commands.Values.OrderBy(c => c.Name))
        {
            var aliases = cmd.Aliases.Length > 0 
                ? $"（别名：{string.Join(", ", cmd.Aliases)}）" 
                : "";
            lines.Add($"• `/{cmd.Name}`{aliases} - {cmd.Description}");
        }

        lines.Add("");
        lines.Add("💡 **使用提示**：");
        lines.Add("• 直接发送消息可与数码宝贝对话");
        lines.Add("• 数码宝贝会根据对话内容成长");
        lines.Add("• 积累足够的情感和Token后会进化");
        lines.Add("• 究极体之后将轮回重生");

        return Task.FromResult(new CommandResult 
        { 
            Success = true, 
            Message = string.Join("\n", lines) 
        });
    }
}
