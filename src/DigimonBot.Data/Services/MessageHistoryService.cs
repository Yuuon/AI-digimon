using System.Collections.Concurrent;
using DigimonBot.Core.Services;

namespace DigimonBot.Data.Services;

/// <summary>
/// 消息历史服务实现
/// </summary>
public class MessageHistoryService : IMessageHistoryService
{
    // 使用ConcurrentDictionary保证线程安全
    // Key: 群组ID（私聊使用用户ID）
    // Value: 该群组的消息列表
    private readonly ConcurrentDictionary<string, List<MessageEntry>> _history = new();
    private readonly int _maxHistoryPerChat = 50; // 每个聊天最多保留50条消息

    public void AddMessage(string userId, long groupId, MessageEntry entry)
    {
        var key = GetKey(userId, groupId);
        
        var messages = _history.GetOrAdd(key, _ => new List<MessageEntry>());
        
        lock (messages)
        {
            messages.Add(entry);
            
            // 限制历史记录数量
            if (messages.Count > _maxHistoryPerChat)
            {
                messages.RemoveAt(0);
            }
        }
    }

    public IReadOnlyList<MessageEntry> GetRecentMessages(string userId, long groupId, int count = 10)
    {
        var key = GetKey(userId, groupId);
        
        if (!_history.TryGetValue(key, out var messages))
        {
            return new List<MessageEntry>();
        }
        
        lock (messages)
        {
            return messages
                .OrderByDescending(m => m.Timestamp)
                .Take(count)
                .ToList();
        }
    }

    public void Cleanup(TimeSpan maxAge)
    {
        var cutoff = DateTime.Now - maxAge;
        
        foreach (var key in _history.Keys)
        {
            if (_history.TryGetValue(key, out var messages))
            {
                lock (messages)
                {
                    messages.RemoveAll(m => m.Timestamp < cutoff);
                }
            }
        }
    }

    private static string GetKey(string userId, long groupId)
    {
        // 群聊使用群组ID，私聊使用用户ID
        return groupId > 0 ? $"g:{groupId}" : $"u:{userId}";
    }
}
