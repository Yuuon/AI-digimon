using System.Collections.Concurrent;
using DigimonBot.Core.Models;
using DigimonBot.Core.Services;

namespace DigimonBot.Data.Repositories;

/// <summary>
/// 内存中数码宝贝管理器（无需持久化，重启重置）
/// </summary>
public class InMemoryDigimonManager : IDigimonManager
{
    private readonly ConcurrentDictionary<string, UserDigimon> _userDigimons = new();
    private readonly IDigimonRepository _repository;

    public InMemoryDigimonManager(IDigimonRepository repository)
    {
        _repository = repository;
    }

    public Task<UserDigimon> GetOrCreateAsync(string userId)
    {
        var digimon = _userDigimons.GetOrAdd(userId, _ =>
        {
            var defaultEgg = _repository.GetDefaultEgg();
            return UserDigimon.CreateNew(userId, defaultEgg.Id);
        });

        return Task.FromResult(digimon);
    }

    public Task<UserDigimon?> GetAsync(string userId)
    {
        _userDigimons.TryGetValue(userId, out var digimon);
        return Task.FromResult(digimon);
    }

    public Task SaveAsync(UserDigimon digimon)
    {
        _userDigimons[digimon.UserId] = digimon;
        return Task.CompletedTask;
    }

    public Task<UserDigimon> ResetAsync(string userId)
    {
        var defaultEgg = _repository.GetDefaultEgg();
        var newDigimon = UserDigimon.CreateNew(userId, defaultEgg.Id);
        _userDigimons[userId] = newDigimon;
        return Task.FromResult(newDigimon);
    }

    public Task UpdateDigimonAsync(string userId, string newDigimonId)
    {
        if (_userDigimons.TryGetValue(userId, out var digimon))
        {
            digimon.CurrentDigimonId = newDigimonId;
        }
        return Task.CompletedTask;
    }

    public Task RecordConversationAsync(string userId, string userMessage, string aiResponse, int tokensConsumed, EmotionAnalysis? emotionAnalysis)
    {
        if (_userDigimons.TryGetValue(userId, out var digimon))
        {
            digimon.TotalTokensConsumed += tokensConsumed;
            digimon.LastInteractionTime = DateTime.Now;
            
            digimon.ChatHistory.Add(new ChatMessage
            {
                Timestamp = DateTime.Now,
                IsFromUser = true,
                Content = userMessage,
                TokensConsumed = 0
            });
            
            digimon.ChatHistory.Add(new ChatMessage
            {
                Timestamp = DateTime.Now,
                IsFromUser = false,
                Content = aiResponse,
                TokensConsumed = tokensConsumed,
                EmotionDelta = emotionAnalysis
            });

            // 限制历史记录长度
            if (digimon.ChatHistory.Count > 100)
            {
                digimon.ChatHistory = digimon.ChatHistory.TakeLast(50).ToList();
            }
        }
        
        return Task.CompletedTask;
    }
}
