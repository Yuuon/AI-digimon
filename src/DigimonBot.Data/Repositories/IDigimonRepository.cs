using DigimonBot.Core.Models;

namespace DigimonBot.Data.Repositories;

/// <summary>
/// 数码宝贝数据仓库接口
/// </summary>
public interface IDigimonRepository
{
    /// <summary>
    /// 获取所有数码宝贝定义
    /// </summary>
    IReadOnlyDictionary<string, DigimonDefinition> GetAll();
    
    /// <summary>
    /// 根据ID获取数码宝贝定义
    /// </summary>
    DigimonDefinition? GetById(string id);
    
    /// <summary>
    /// 获取指定阶段的数码宝贝列表
    /// </summary>
    List<DigimonDefinition> GetByStage(DigimonStage stage);
    
    /// <summary>
    /// 获取默认的初始数码宝贝（蛋）
    /// </summary>
    DigimonDefinition GetDefaultEgg();
    
    /// <summary>
    /// 重新加载数据（配置文件变更时）
    /// </summary>
    Task ReloadAsync();
}
