namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 命令接口
/// </summary>
public interface ICommand
{
    /// <summary>
    /// 命令名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 命令别名
    /// </summary>
    string[] Aliases { get; }
    
    /// <summary>
    /// 命令描述
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// 执行命令
    /// </summary>
    Task<CommandResult> ExecuteAsync(CommandContext context);
}

/// <summary>
/// 命令执行结果
/// </summary>
public class CommandResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public bool IsAiResponseNeeded => false;
}

/// <summary>
/// 命令上下文
/// </summary>
public class CommandContext
{
    /// <summary>带群聊隔离的UserId</summary>
    public string UserId { get; set; } = "";
    /// <summary>原始UserId</summary>
    public string OriginalUserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Message { get; set; } = "";
    public string[] Args { get; set; } = Array.Empty<string>();
    public long GroupId { get; set; }
    public bool IsGroupMessage { get; set; }
    /// <summary>是否需要添加用户前缀（各自培养模式）</summary>
    public bool ShouldAddPrefix { get; set; }
    /// <summary>消息中@提及的用户ID列表（纯QQ号）</summary>
    public List<string> MentionedUserIds { get; set; } = new();
    /// <summary>目标用户ID（用于查看他人数据，带群聊隔离）</summary>
    public string? TargetUserId { get; set; }
    /// <summary>目标用户原始ID（纯QQ号）</summary>
    public string? TargetOriginalUserId { get; set; }
}
