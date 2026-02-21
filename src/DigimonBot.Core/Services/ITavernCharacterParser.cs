using DigimonBot.Core.Models.Tavern;

namespace DigimonBot.Core.Services;

/// <summary>
/// 酒馆角色卡解析器接口
/// </summary>
public interface ITavernCharacterParser
{
    /// <summary>
    /// 解析角色卡文件（自动检测格式）
    /// </summary>
    Task<TavernCharacter?> ParseAsync(string filePath);
    
    /// <summary>
    /// 解析 PNG 角色卡
    /// </summary>
    Task<TavernCharacter?> ParsePngAsync(string filePath);
    
    /// <summary>
    /// 解析 JSON 角色卡
    /// </summary>
    Task<TavernCharacter?> ParseJsonAsync(string filePath);
    
    /// <summary>
    /// 获取支持的角色卡文件列表
    /// </summary>
    IEnumerable<string> GetCharacterFiles(string directory);
}
