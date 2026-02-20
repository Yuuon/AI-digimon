using DigimonBot.Core.Models;

namespace DigimonBot.Data.Repositories;

/// <summary>
/// 用户经济数据仓库接口
/// </summary>
public interface IUserDataRepository
{
    /// <summary>
    /// 获取用户经济数据，不存在则返回 null
    /// </summary>
    Task<UserEconomy?> GetAsync(string userId);
    
    /// <summary>
    /// 获取或创建用户经济数据
    /// </summary>
    Task<UserEconomy> GetOrCreateAsync(string userId);
    
    /// <summary>
    /// 增加金币
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="amount">数量（必须为正数）</param>
    Task AddGoldAsync(string userId, int amount);
    
    /// <summary>
    /// 扣减金币
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="amount">数量（必须为正数）</param>
    /// <returns>是否成功（余额不足时返回 false）</returns>
    Task<bool> DeductGoldAsync(string userId, int amount);
    
    /// <summary>
    /// 直接设置金币数量（管理命令用）
    /// </summary>
    Task SetGoldAsync(string userId, long amount);
    
    /// <summary>
    /// 更新每日奖励领取时间
    /// </summary>
    Task UpdateDailyRewardTimeAsync(string userId, DateTime time);
}
