namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 命令注册表
/// </summary>
public class CommandRegistry
{
    private readonly Dictionary<string, ICommand> _commands = new(StringComparer.OrdinalIgnoreCase);

    public void Register(ICommand command)
    {
        _commands[command.Name] = command;
        foreach (var alias in command.Aliases)
        {
            _commands[alias] = command;
        }
    }

    public bool TryGetCommand(string name, out ICommand? command)
    {
        return _commands.TryGetValue(name, out command);
    }

    public IReadOnlyDictionary<string, ICommand> GetAllCommands()
    {
        // 去重返回（别名只返回主名）
        return _commands.Values.Distinct().ToDictionary(c => c.Name);
    }
}
