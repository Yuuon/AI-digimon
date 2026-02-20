using DigimonBot.Core.Models;

namespace DigimonBot.Data.Repositories;

/// <summary>
/// 物品定义仓库接口
/// </summary>
public interface IItemRepository
{
    /// <summary>
    /// 获取所有物品定义
    /// </summary>
    IReadOnlyDictionary<string, ItemDefinition> GetAll();
    
    /// <summary>
    /// 根据ID获取物品定义
    /// </summary>
    ItemDefinition? GetById(string id);
    
    /// <summary>
    /// 获取商店出售的物品列表
    /// </summary>
    IEnumerable<ItemDefinition> GetShopItems();
    
    /// <summary>
    /// 获取特定类型的物品
    /// </summary>
    IEnumerable<ItemDefinition> GetByType(string type);
    
    /// <summary>
    /// 重新加载数据
    /// </summary>
    Task ReloadAsync();
}
