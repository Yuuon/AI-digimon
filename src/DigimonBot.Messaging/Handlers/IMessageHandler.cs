namespace DigimonBot.Messaging.Handlers;

/// <summary>
/// 消息处理器接口
/// </summary>
public interface IMessageHandler
{
    /// <summary>
    /// 处理收到的消息
    /// </summary>
    Task<MessageResult> HandleMessageAsync(MessageContext context);
}

/// <summary>
/// 消息上下文
/// </summary>
public class MessageContext
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Content { get; set; } = "";
    public long GroupId { get; set; }
    public bool IsGroupMessage { get; set; }
    public DateTime Timestamp { get; set; }
    public MessageSource Source { get; set; }
    /// <summary>消息中@提及的用户ID列表（纯QQ号，不包含Bot）</summary>
    public List<string> MentionedUserIds { get; set; } = new();
}

public enum MessageSource
{
    Private,
    Group,
    Temp
}

/// <summary>
/// 消息处理结果
/// </summary>
public class MessageResult
{
    public bool Handled { get; set; }
    public string? Response { get; set; }
    public bool IsCommand { get; set; }
    public bool EvolutionOccurred { get; set; }
    public string? EvolutionMessage { get; set; }
}
