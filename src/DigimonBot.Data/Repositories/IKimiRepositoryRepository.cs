using DigimonBot.Core.Models.Kimi;

namespace DigimonBot.Data.Repositories;

/// <summary>
/// Kimi仓库数据仓库接口
/// </summary>
public interface IKimiRepositoryRepository
{
    /// <summary>
    /// 创建新仓库
    /// </summary>
    Task<KimiRepository> CreateAsync(string name, string path);

    /// <summary>
    /// 根据名称获取仓库
    /// </summary>
    Task<KimiRepository?> GetByNameAsync(string name);

    /// <summary>
    /// 获取所有仓库
    /// </summary>
    Task<IEnumerable<KimiRepository>> GetAllAsync();

    /// <summary>
    /// 设置指定仓库为活动状态（自动清除其他活动仓库）
    /// </summary>
    Task SetActiveAsync(string name);

    /// <summary>
    /// 获取当前活动仓库
    /// </summary>
    Task<KimiRepository?> GetActiveAsync();

    /// <summary>
    /// 更新仓库最后使用时间
    /// </summary>
    Task UpdateLastUsedAsync(string name);

    /// <summary>
    /// 增加仓库会话计数
    /// </summary>
    Task IncrementSessionCountAsync(string name);
}
