using DigimonBot.Core.Models;

namespace DigimonBot.Data.Repositories;

/// <summary>
/// 签到记录仓库接口
/// </summary>
public interface ICheckInRepository
{
    /// <summary>
    /// 获取用户签到记录
    /// </summary>
    Task<CheckInRecord?> GetAsync(string userId);
    
    /// <summary>
    /// 获取或创建签到记录
    /// </summary>
    Task<CheckInRecord> GetOrCreateAsync(string userId);
    
    /// <summary>
    /// 更新签到记录
    /// </summary>
    Task UpdateAsync(CheckInRecord record);
    
    /// <summary>
    /// 检查今天是否已经签到
    /// </summary>
    Task<bool> HasCheckedInTodayAsync(string userId);
    
    /// <summary>
    /// 执行签到并返回更新后的记录
    /// </summary>
    Task<CheckInRecord> CheckInAsync(string userId);
}
