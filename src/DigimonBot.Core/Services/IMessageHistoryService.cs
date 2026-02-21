namespace DigimonBot.Core.Services;

/// <summary>
/// 消息历史服务接口
/// </summary>
public interface IMessageHistoryService
{
    /// <summary>
    /// 添加消息到历史记录
    /// </summary>
    void AddMessage(string userId, long groupId, MessageEntry entry);
    
    /// <summary>
    /// 获取用户/群组的最近消息
    /// </summary>
    IReadOnlyList<MessageEntry> GetRecentMessages(string userId, long groupId, int count = 10);
    
    /// <summary>
    /// 清理过期消息
    /// </summary>
    void Cleanup(TimeSpan maxAge);
}

/// <summary>
/// 消息条目
/// </summary>
public class MessageEntry
{
    /// <summary>消息内容</summary>
    public string Content { get; set; } = "";
    
    /// <summary>消息类型：text, image, file等</summary>
    public string Type { get; set; } = "text";
    
    /// <summary>图片URL（如果是图片消息）</summary>
    public string? ImageUrl { get; set; }
    
    /// <summary>图片文件标识（用于调用get_image API）</summary>
    public string? ImageFile { get; set; }
    
    /// <summary>发送时间</summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>是否来自Bot</summary>
    public bool IsFromBot { get; set; }
    
    /// <summary>原始消息数据（用于扩展）</summary>
    public object? RawData { get; set; }
}
