using System.Text;
using DigimonBot.Data.Repositories;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 列出所有自定义命令
/// </summary>
public class ListCustomCommandsCommand : ICommand
{
    private readonly ICustomCommandRepository _repository;

    public ListCustomCommandsCommand(ICustomCommandRepository repository)
    {
        _repository = repository;
    }

    public string Name => "customcmds";
    public string[] Aliases => new[] { "customs", "cmds" };
    public string Description => "列出所有自定义命令";

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        var commands = (await _repository.ListAsync()).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("📋 **自定义命令列表**");
        sb.AppendLine();

        if (commands.Count == 0)
        {
            sb.AppendLine("暂无自定义命令");
            sb.AppendLine("使用 /kimi 创建你的第一个命令！");
        }
        else
        {
            foreach (var cmd in commands)
            {
                var aliasText = cmd.Aliases.Length > 0
                    ? $" (别名: {string.Join(", ", cmd.Aliases)})"
                    : "";
                var whitelistBadge = cmd.RequiresWhitelist ? " 🔒" : "";

                sb.AppendLine($"• **/{cmd.Name}**{aliasText}{whitelistBadge}");
                if (!string.IsNullOrEmpty(cmd.Description))
                {
                    sb.AppendLine($"  {cmd.Description}");
                }
            }
        }

        return new CommandResult
        {
            Success = true,
            Message = sb.ToString()
        };
    }
}
