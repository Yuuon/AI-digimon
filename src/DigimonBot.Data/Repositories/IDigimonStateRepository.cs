using DigimonBot.Core.Models;

namespace DigimonBot.Data.Repositories;

/// <summary>
/// 用户数码宝贝状态仓库接口（替代原有的 IDigimonManager）
/// </summary>
public interface IDigimonStateRepository
{
    /// <summary>
    /// 获取或创建数码宝贝状态
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="groupId">群ID（可选，私聊时为空）</param>
    /// <param name="defaultDigimonId">默认数码宝贝ID（新建时使用）</param>
    Task<UserDigimonState> GetOrCreateAsync(string userId, string? groupId = null, string? defaultDigimonId = null);
    
    /// <summary>
    /// 获取数码宝贝状态
    /// </summary>
    Task<UserDigimonState?> GetAsync(string userId, string? groupId = null);
    
    /// <summary>
    /// 保存数码宝贝状态
    /// </summary>
    Task SaveAsync(UserDigimonState state);
    
    /// <summary>
    /// 重置数码宝贝（回到初始状态）
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="groupId">群ID（可选）</param>
    /// <param name="defaultDigimonId">重置后的数码宝贝ID</param>
    Task<UserDigimonState> ResetAsync(string userId, string? groupId = null, string? defaultDigimonId = null);
    
    /// <summary>
    /// 更新当前数码宝贝（进化后使用）
    /// </summary>
    Task UpdateDigimonAsync(string userId, string newDigimonId, string? groupId = null);
    
    /// <summary>
    /// 更新情感值
    /// </summary>
    Task UpdateEmotionsAsync(string userId, EmotionValues emotions, string? groupId = null);
    
    /// <summary>
    /// 记录对话并更新 Token 消耗，同时获得金币
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="tokensConsumed">消耗的Token数</param>
    /// <param name="groupId">群ID（可选）</param>
    /// <param name="goldPerToken">每Token获得的金币数</param>
    /// <returns>获得的金币数量</returns>
    Task<int> RecordConversationAsync(string userId, int tokensConsumed, string? groupId = null, int goldPerToken = 1);
    
    /// <summary>
    /// 获取所有用户的数码宝贝状态（用于后台任务等）
    /// </summary>
    Task<IReadOnlyList<UserDigimonState>> GetAllAsync();
}
