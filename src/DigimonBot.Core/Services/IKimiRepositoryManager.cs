using DigimonBot.Core.Models.Kimi;

namespace DigimonBot.Core.Services;

/// <summary>
/// Kimi仓库管理器接口 - 管理仓库生命周期
/// </summary>
public interface IKimiRepositoryManager
{
    /// <summary>
    /// 创建新仓库（含git init和README生成）
    /// </summary>
    /// <param name="name">仓库名称（为空则自动生成）</param>
    /// <param name="userId">创建者用户ID</param>
    Task<KimiRepository> CreateRepositoryAsync(string? name, string userId);

    /// <summary>
    /// 列出所有仓库
    /// </summary>
    Task<IEnumerable<KimiRepository>> ListRepositoriesAsync();

    /// <summary>
    /// 切换到指定仓库
    /// </summary>
    /// <param name="name">仓库名称</param>
    /// <returns>是否切换成功</returns>
    Task<bool> SwitchRepositoryAsync(string name);

    /// <summary>
    /// 获取当前活动仓库
    /// </summary>
    Task<KimiRepository?> GetActiveRepositoryAsync();

    /// <summary>
    /// 确保存在可用仓库（没有则自动创建）
    /// </summary>
    /// <param name="userId">用户ID</param>
    Task<KimiRepository> EnsureRepositoryExistsAsync(string userId);
}
