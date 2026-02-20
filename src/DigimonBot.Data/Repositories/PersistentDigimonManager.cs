using System.Collections.Concurrent;
using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using DigimonBot.Data.Repositories.Sqlite;

namespace DigimonBot.Data.Repositories;

/// <summary>
/// 持久化的数码宝贝管理器
/// - 核心状态（情感值、Token等）持久化到 SQLite
/// - 对话历史保持在内存中（重启后清空）
/// </summary>
public class PersistentDigimonManager : IDigimonManager
{
    private readonly IDigimonStateRepository _stateRepository;
    private readonly IDigimonRepository _digimonRepository;
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _chatHistories = new();
    private readonly int _goldTokenDivisor;

    public PersistentDigimonManager(
        IDigimonStateRepository stateRepository,
        IDigimonRepository digimonRepository,
        int goldTokenDivisor = 10)
    {
        _stateRepository = stateRepository;
        _digimonRepository = digimonRepository;
        _goldTokenDivisor = goldTokenDivisor <= 0 ? 10 : goldTokenDivisor;
    }

    public async Task<UserDigimon> GetOrCreateAsync(string userId)
    {
        // 从数据库获取或创建状态
        var state = await _stateRepository.GetOrCreateAsync(
            userId, 
            null, 
            _digimonRepository.GetDefaultEgg().Id);

        // 获取或创建内存中的对话历史
        var chatHistory = _chatHistories.GetOrAdd(userId, _ => new List<ChatMessage>());

        // 组合成完整的 UserDigimon 对象
        return MapToUserDigimon(state, chatHistory);
    }

    public async Task<UserDigimon?> GetAsync(string userId)
    {
        var state = await _stateRepository.GetAsync(userId);
        if (state == null)
            return null;

        var chatHistory = _chatHistories.GetOrAdd(userId, _ => new List<ChatMessage>());
        return MapToUserDigimon(state, chatHistory);
    }

    public async Task SaveAsync(UserDigimon digimon)
    {
        try
        {
            // 将 UserDigimon 转换为状态并保存
            var state = MapToState(digimon);
            
            await _stateRepository.SaveAsync(state);

            // 更新内存中的对话历史
            _chatHistories[digimon.UserId] = digimon.ChatHistory;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] SaveAsync 异常: {ex.Message}");
            throw;
        }
    }

    public async Task<UserDigimon> ResetAsync(string userId)
    {
        // 重置状态
        var newState = await _stateRepository.ResetAsync(
            userId, 
            null, 
            _digimonRepository.GetDefaultEgg().Id);

        // 清空对话历史
        _chatHistories.TryRemove(userId, out _);
        var newChatHistory = _chatHistories.GetOrAdd(userId, _ => new List<ChatMessage>());

        return MapToUserDigimon(newState, newChatHistory);
    }

    public async Task UpdateDigimonAsync(string userId, string newDigimonId)
    {
        await _stateRepository.UpdateDigimonAsync(userId, newDigimonId);
    }

    public async Task RecordConversationAsync(
        string userId, 
        string userMessage, 
        string aiResponse, 
        int tokensConsumed, 
        EmotionAnalysis? emotionAnalysis)
    {
        // 计算金币：Token消耗 / 配置的分母
        int goldEarned = tokensConsumed / _goldTokenDivisor;
        if (goldEarned < 1 && tokensConsumed > 0)
            goldEarned = 1; // 至少获得1金币
        
        // 记录到数据库（同时增加 Token 和金币）
        await _stateRepository.RecordConversationAsync(
            userId, 
            tokensConsumed, 
            null, 
            goldEarned);

        // 记录到内存中的对话历史
        if (_chatHistories.TryGetValue(userId, out var chatHistory))
        {
            chatHistory.Add(new ChatMessage
            {
                Timestamp = DateTime.Now,
                IsFromUser = true,
                Content = userMessage,
                TokensConsumed = 0
            });

            chatHistory.Add(new ChatMessage
            {
                Timestamp = DateTime.Now,
                IsFromUser = false,
                Content = aiResponse,
                TokensConsumed = tokensConsumed,
                EmotionDelta = emotionAnalysis
            });

            // 限制历史记录长度
            if (chatHistory.Count > 100)
            {
                // 保留最后50条
                var newHistory = chatHistory.TakeLast(50).ToList();
                _chatHistories[userId] = newHistory;
            }
        }
    }

    /// <summary>
    /// 将数据库状态映射为完整的 UserDigimon 对象
    /// </summary>
    private static UserDigimon MapToUserDigimon(UserDigimonState state, List<ChatMessage> chatHistory)
    {
        return new UserDigimon
        {
            UserId = state.UserId,
            CurrentDigimonId = state.CurrentDigimonId,
            Emotions = state.GetEmotions(),
            TotalTokensConsumed = state.TotalTokensConsumed,
            HatchTime = state.HatchTime,
            LastInteractionTime = state.LastInteractionTime,
            ChatHistory = chatHistory
        };
    }

    /// <summary>
    /// 将 UserDigimon 转换为数据库状态
    /// </summary>
    private static UserDigimonState MapToState(UserDigimon digimon)
    {
        return new UserDigimonState
        {
            UserId = digimon.UserId,
            GroupId = "",  // 使用空字符串代替 null
            CurrentDigimonId = digimon.CurrentDigimonId,
            Courage = digimon.Emotions.Courage,
            Friendship = digimon.Emotions.Friendship,
            Love = digimon.Emotions.Love,
            Knowledge = digimon.Emotions.Knowledge,
            TotalTokensConsumed = digimon.TotalTokensConsumed,
            HatchTime = digimon.HatchTime,
            LastInteractionTime = digimon.LastInteractionTime
        };
    }
}
