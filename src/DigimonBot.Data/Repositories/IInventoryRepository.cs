using DigimonBot.Core.Models;

namespace DigimonBot.Data.Repositories;

/// <summary>
/// 用户背包仓库接口
/// </summary>
public interface IInventoryRepository
{
    /// <summary>
    /// 获取用户背包中的所有物品
    /// </summary>
    Task<IReadOnlyList<UserItem>> GetInventoryAsync(string userId);
    
    /// <summary>
    /// 获取特定物品的数量
    /// </summary>
    Task<int> GetItemQuantityAsync(string userId, string itemId);
    
    /// <summary>
    /// 添加物品到背包
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="itemId">物品ID</param>
    /// <param name="quantity">数量（必须为正数）</param>
    Task AddItemAsync(string userId, string itemId, int quantity = 1);
    
    /// <summary>
    /// 从背包移除物品
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="itemId">物品ID</param>
    /// <param name="quantity">数量（必须为正数）</param>
    /// <returns>是否成功（数量不足时返回 false）</returns>
    Task<bool> RemoveItemAsync(string userId, string itemId, int quantity = 1);
    
    /// <summary>
    /// 检查是否拥有足够数量的物品
    /// </summary>
    Task<bool> HasItemAsync(string userId, string itemId, int quantity = 1);
    
    /// <summary>
    /// 使用物品（移除一个并返回是否成功）
    /// </summary>
    Task<bool> UseItemAsync(string userId, string itemId);
}
