namespace DigimonBot.Core.Models.Tavern;

/// <summary>
/// SillyTavern 角色卡数据模型
/// </summary>
public class TavernCharacter
{
    /// <summary>角色名称</summary>
    public string Name { get; set; } = "";
    
    /// <summary>角色描述/背景故事</summary>
    public string Description { get; set; } = "";
    
    /// <summary>性格特点</summary>
    public string Personality { get; set; } = "";
    
    /// <summary>场景设定</summary>
    public string Scenario { get; set; } = "";
    
    /// <summary>开场白（第一条消息）</summary>
    public string FirstMessage { get; set; } = "";
    
    /// <summary>对话示例</summary>
    public string MessageExample { get; set; } = "";
    
    /// <summary>创作者备注</summary>
    public string? CreatorComment { get; set; }
    
    /// <summary>标签</summary>
    public List<string> Tags { get; set; } = new();
    
    /// <summary>创作者</summary>
    public string? Creator { get; set; }
    
    /// <summary>角色版本</summary>
    public string? CharacterVersion { get; set; }
    
    /// <summary>角色头像/封面（Base64或路径）</summary>
    public string? CoverImage { get; set; }
    
    /// <summary>扩展数据</summary>
    public TavernExtensions? Extensions { get; set; }
    
    /// <summary>源文件路径</summary>
    public string? SourcePath { get; set; }
    
    /// <summary>是否为PNG角色卡</summary>
    public bool IsPngCard { get; set; }
    
    /// <summary>构建系统提示词</summary>
    public string BuildSystemPrompt()
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine($"你的名字是{Name}。");
        
        if (!string.IsNullOrEmpty(Description))
        {
            sb.AppendLine($"背景设定：{Description}");
        }
        
        if (!string.IsNullOrEmpty(Personality))
        {
            sb.AppendLine($"性格特点：{Personality}");
        }
        
        if (!string.IsNullOrEmpty(Scenario))
        {
            sb.AppendLine($"当前场景：{Scenario}");
        }
        
        if (!string.IsNullOrEmpty(MessageExample))
        {
            sb.AppendLine($"对话示例：{MessageExample}");
        }
        
        sb.AppendLine($"请记住你的人设，以{Name}的身份回应用户。");
        
        return sb.ToString();
    }
}

/// <summary>
/// 角色卡扩展数据
/// </summary>
public class TavernExtensions
{
    /// <summary>深度提示词</summary>
    public object? DepthPrompt { get; set; }
    
    /// <summary>话痨程度 (0-1)</summary>
    public double Talkativeness { get; set; } = 0.5;
    
    /// <summary>是否收藏</summary>
    public bool Favorite { get; set; }
}
