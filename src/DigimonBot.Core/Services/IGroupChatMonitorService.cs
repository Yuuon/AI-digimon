namespace DigimonBot.Core.Services;

/// <summary>
/// 群聊监测服务接口
/// </summary>
public interface IGroupChatMonitorService
{
    /// <summary>
    /// 添加消息到监测队列
    /// </summary>
    void AddMessage(long groupId, string userId, string userName, string content);
    
    /// <summary>
    /// 检查是否应该触发总结（高频关键词检测）
    /// </summary>
    (bool shouldTrigger, string keywords, string summary)? CheckForTrigger(long groupId);
    
    /// <summary>
    /// 生成群聊总结
    /// </summary>
    Task<string> GenerateSummaryAsync(long groupId);
    
    /// <summary>
    /// 清空指定群的记录
    /// </summary>
    void ClearGroupHistory(long groupId);
    
    /// <summary>
    /// 获取群的监测状态（用于调试）
    /// </summary>
    GroupMonitorStatus GetGroupStatus(long groupId);
    
    /// <summary>
    /// 记录触发时间（启动冷却）
    /// </summary>
    void RecordTriggerTime(long groupId);
}

/// <summary>
/// 群监测状态
/// </summary>
public class GroupMonitorStatus
{
    /// <summary>消息数量</summary>
    public int MessageCount { get; set; }
    
    /// <summary>Top关键词</summary>
    public Dictionary<string, int> TopKeywords { get; set; } = new();
    
    /// <summary>是否有高频关键词</summary>
    public bool HasHighFreqKeyword { get; set; }
    
    /// <summary>是否在冷却中</summary>
    public bool IsInCooldown { get; set; }
    
    /// <summary>剩余冷却秒数</summary>
    public int CooldownSeconds { get; set; }
    
    /// <summary>是否可以触发</summary>
    public bool CanTrigger { get; set; }
}

/// <summary>
/// 群聊消息记录
/// </summary>
public class GroupChatMessage
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; }
}
