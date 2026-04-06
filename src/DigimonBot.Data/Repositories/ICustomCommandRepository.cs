using DigimonBot.Core.Models;

namespace DigimonBot.Data.Repositories;

/// <summary>
/// 自定义命令仓库接口
/// </summary>
public interface ICustomCommandRepository
{
    Task<CustomCommand> CreateAsync(CustomCommand command);
    Task<CustomCommand?> GetByNameAsync(string name);
    Task<CustomCommand?> GetByAliasAsync(string alias);
    Task<bool> ExistsAsync(string name, string[]? aliases = null);
    Task<IEnumerable<CustomCommand>> ListAsync();
    Task UpdateUsageAsync(int id);
    Task DeleteAsync(int id);
}
