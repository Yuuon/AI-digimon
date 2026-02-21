using DigimonBot.Core.Models.Tavern;

namespace DigimonBot.Core.Services;

/// <summary>
/// 酒馆服务接口
/// </summary>
public interface ITavernService
{
    /// <summary>
    /// 酒馆模式是否开启
    /// </summary>
    bool IsEnabled { get; }
    
    /// <summary>
    /// 当前加载的角色
    /// </summary>
    TavernCharacter? CurrentCharacter { get; }
    
    /// <summary>
    /// 角色卡目录
    /// </summary>
    string CharacterDirectory { get; }
    
    /// <summary>
    /// 开启酒馆模式
    /// </summary>
    void Enable();
    
    /// <summary>
    /// 关闭酒馆模式
    /// </summary>
    void Disable();
    
    /// <summary>
    /// 切换酒馆模式
    /// </summary>
    bool Toggle();
    
    /// <summary>
    /// 加载角色
    /// </summary>
    Task<bool> LoadCharacterAsync(string characterName);
    
    /// <summary>
    /// 卸载当前角色
    /// </summary>
    void UnloadCharacter();
    
    /// <summary>
    /// 获取可用角色列表（异步）
    /// </summary>
    Task<IEnumerable<CharacterInfo>> GetAvailableCharactersAsync();
    
    /// <summary>
    /// 获取可用角色列表（同步）
    /// </summary>
    IEnumerable<CharacterInfo> GetAvailableCharacters();
    
    /// <summary>
    /// 生成酒馆对话回复
    /// </summary>
    Task<string> GenerateResponseAsync(string userMessage, string userName);
    
    /// <summary>
    /// 生成群聊总结后的角色回复
    /// </summary>
    Task<string> GenerateSummaryResponseAsync(string summary, string keywords);
    
    /// <summary>
    /// 检查是否有角色已加载
    /// </summary>
    bool HasCharacterLoaded();
}

/// <summary>
/// 角色信息（用于列表显示）
/// </summary>
public class CharacterInfo
{
    /// <summary>文件名（不含扩展名）</summary>
    public string FileName { get; set; } = "";
    
    /// <summary>角色名称</summary>
    public string Name { get; set; } = "";
    
    /// <summary>文件路径</summary>
    public string FilePath { get; set; } = "";
    
    /// <summary>文件格式：PNG 或 JSON</summary>
    public string Format { get; set; } = "";
    
    /// <summary>标签</summary>
    public List<string> Tags { get; set; } = new();
}
