using DigimonBot.Core.Models;

namespace DigimonBot.Core.Services;

/// <summary>
/// 数码宝贝管理器接口
/// </summary>
public interface IDigimonManager
{
    /// <summary>
    /// 获取或创建用户的数码宝贝
    /// </summary>
    Task<UserDigimon> GetOrCreateAsync(string userId);
    
    /// <summary>
    /// 获取用户当前数码宝贝
    /// </summary>
    Task<UserDigimon?> GetAsync(string userId);
    
    /// <summary>
    /// 保存数码宝贝状态
    /// </summary>
    Task SaveAsync(UserDigimon digimon);
    
    /// <summary>
    /// 重置用户的数码宝贝（回到蛋状态）
    /// </summary>
    Task<UserDigimon> ResetAsync(string userId);
    
    /// <summary>
    /// 更新数码宝贝（进化后）
    /// </summary>
    Task UpdateDigimonAsync(string userId, string newDigimonId);
    
    /// <summary>
    /// 记录对话并更新Token消耗
    /// </summary>
    Task RecordConversationAsync(string userId, string userMessage, string aiResponse, int tokensConsumed, EmotionAnalysis? emotionAnalysis);
}
