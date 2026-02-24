using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using DigimonBot.AI.Services;
using DigimonBot.Core.Models;
using DigimonBot.Core.Models.Tavern;
using DigimonBot.Core.Services;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Data.Services;

/// <summary>
/// 群聊监测服务实现
/// </summary>
public class GroupChatMonitorService : IGroupChatMonitorService
{
    private readonly ConcurrentDictionary<long, List<GroupChatMessage>> _groupMessages = new();
    private readonly ConcurrentDictionary<long, DateTime> _lastTriggerTime = new();
    private readonly IAIClient _aiClient;
    private readonly ILogger<GroupChatMonitorService> _logger;
    private readonly ITavernConfigService _configService;

    // 当前配置（通过配置服务获取）
    private MonitorConfig Config => _configService.Config.Monitor;

    public GroupChatMonitorService(
        IAIClient aiClient,
        ILogger<GroupChatMonitorService> logger,
        ITavernConfigService configService)
    {
        _aiClient = aiClient;
        _logger = logger;
        _configService = configService;

        // 订阅配置变更事件
        _configService.OnConfigChanged += (_, _) =>
        {
            _logger.LogInformation("[监测服务] 配置已重新加载");
        };
    }

    public void AddMessage(long groupId, string userId, string userName, string content)
    {
        _logger.LogInformation("[监测服务] 尝试添加消息: Group={GroupId}, User={User}, Content={Content}", 
            groupId, userName, content.Length > 20 ? content[..20] + "..." : content);
        
        var messages = _groupMessages.GetOrAdd(groupId, _ => new List<GroupChatMessage>());
        
        lock (messages)
        {
            messages.Add(new GroupChatMessage
            {
                UserId = userId,
                UserName = userName,
                Content = content,
                Timestamp = DateTime.Now
            });
            
            // 只保留最近的消息（从配置读取）
            if (messages.Count > Config.MaxMessageCount)
            {
                messages.RemoveAt(0);
            }
            
            _logger.LogInformation("[监测服务] 消息已添加: Group={GroupId}, 当前总数: {Count}", groupId, messages.Count);
        }
    }

    public (bool shouldTrigger, string keywords, string summary)? CheckForTrigger(long groupId)
    {
        // 从配置读取最小消息数
        if (!_groupMessages.TryGetValue(groupId, out var messages) || messages.Count < Config.MinMessageCount)
        {
            return null;
        }

        // 检查触发间隔（从配置读取）
        if (_lastTriggerTime.TryGetValue(groupId, out var lastTime))
        {
            if (DateTime.Now - lastTime < TimeSpan.FromMinutes(Config.TriggerIntervalMinutes))
            {
                return null;
            }
        }

        lock (messages)
        {
            // 分析高频关键词
            var keywords = ExtractKeywords(messages);
            if (keywords.Count == 0)
            {
                return null;
            }

            // 检查是否有高频关键词（从配置读取阈值）
            var highFreqKeywords = keywords.Where(kvp => kvp.Value >= Config.KeywordThreshold).ToList();
            if (highFreqKeywords.Count == 0)
            {
                return null;
            }

            var keywordStr = string.Join(", ", highFreqKeywords.Select(kvp => $"{kvp.Key}({kvp.Value}次)"));
            
            _logger.LogInformation("群 {GroupId} 触发关键词检测: {Keywords}", groupId, keywordStr);
            
            // 返回触发信息，但summary留空，让调用者异步生成
            return (true, keywordStr, string.Empty);
        }
    }

    public async Task<string> GenerateSummaryAsync(long groupId)
    {
        if (!_groupMessages.TryGetValue(groupId, out var messages) || messages.Count == 0)
        {
            return "没有聊天记录";
        }

        try
        {
            var conversation = string.Join("\n", messages.Select(m => $"{m.UserName}: {m.Content}"));
            
            var prompt = $"""
                请总结以下群聊对话的主要内容：

                {conversation}

                总结要求：
                1. 简要概括讨论的主题
                2. 提及主要发言者和他们的观点
                3. 控制在100字以内
                """;

            var chatMessages = new List<ChatMessage>
            {
                new()
                {
                    Timestamp = DateTime.Now,
                    IsFromUser = true,
                    Content = prompt
                }
            };

            var response = await _aiClient.ChatAsync(chatMessages, "你是一个擅长总结的助手。");
            return response.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成群聊总结失败");
            return "生成总结失败";
        }
    }

    public void ClearGroupHistory(long groupId)
    {
        _groupMessages.TryRemove(groupId, out _);
        _lastTriggerTime.TryRemove(groupId, out _);
    }

    public GroupMonitorStatus GetGroupStatus(long groupId)
    {
        _logger.LogInformation("[监测服务] 查询群状态: Group={GroupId}", groupId);
        
        var status = new GroupMonitorStatus();
        
        // 获取消息
        if (_groupMessages.TryGetValue(groupId, out var messages))
        {
            status.MessageCount = messages.Count;
            _logger.LogInformation("[监测服务] 找到群记录: Group={GroupId}, 消息数={Count}", groupId, messages.Count);
            
            lock (messages)
            {
                var keywords = ExtractKeywords(messages);
                status.TopKeywords = keywords.OrderByDescending(kvp => kvp.Value).Take(5).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                status.HasHighFreqKeyword = keywords.Any(kvp => kvp.Value >= Config.KeywordThreshold);
            }
        }
        else
        {
            _logger.LogWarning("[监测服务] 未找到群记录: Group={GroupId}，当前记录群数: {TotalGroups}", 
                groupId, _groupMessages.Count);
        }
        
        // 检查冷却
        if (_lastTriggerTime.TryGetValue(groupId, out var lastTime))
        {
            var elapsed = DateTime.Now - lastTime;
            var cooldown = TimeSpan.FromMinutes(Config.TriggerIntervalMinutes);
            if (elapsed < cooldown)
            {
                status.IsInCooldown = true;
                status.CooldownSeconds = (int)(cooldown - elapsed).TotalSeconds;
            }
        }
        
        // 检查是否可以触发（基本条件，从配置读取）
        status.CanTrigger = status.MessageCount >= Config.MinMessageCount && 
                           status.HasHighFreqKeyword && 
                           !status.IsInCooldown;
        
        return status;
    }

    public void RecordTriggerTime(long groupId)
    {
        _lastTriggerTime[groupId] = DateTime.Now;
        _logger.LogInformation("[监测服务] 群 {GroupId} 触发时间已记录，开始 {Minutes} 分钟冷却", 
            groupId, Config.TriggerIntervalMinutes);
    }

    /// <summary>
    /// 提取关键词（支持子串匹配，如"再测试"匹配"测试"）
    /// </summary>
    private Dictionary<string, int> ExtractKeywords(List<GroupChatMessage> messages)
    {
        // 第一步：收集所有候选词（完整词汇）
        var candidates = new HashSet<string>();
        var allContents = new List<string>();
        
        foreach (var msg in messages)
        {
            var content = msg.Content;
            if (string.IsNullOrWhiteSpace(content)) continue;
            allContents.Add(content);
            
            // 移除标点
            content = Regex.Replace(content, @"[^\w\s\u4e00-\u9fa5]", " ");
            
            // 按空格分割获取候选词
            var tokens = content.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var token in tokens)
            {
                var word = token.Trim();
                // 从配置读取关键词长度限制
                if (word.Length < Config.MinKeywordLength || word.Length > Config.MaxKeywordLength) continue;
                if (IsStopWord(word)) continue;
                candidates.Add(word);
            }
        }
        
        // 第二步：统计每个候选词在所有消息中的出现次数（包括作为子串）
        var wordCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var candidate in candidates)
        {
            int count = 0;
            foreach (var content in allContents)
            {
                if (content.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }
            
            // 从配置读取阈值
            if (count >= Config.KeywordThreshold)
            {
                wordCounts[candidate] = count;
            }
        }

        return wordCounts
            .OrderByDescending(kvp => kvp.Value)
            .Take(10)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// 判断是否停用词（使用配置中的停用词列表）
    /// </summary>
    private bool IsStopWord(string word)
    {
        return Config.StopWords.Contains(word) || word.All(char.IsDigit);
    }
}
