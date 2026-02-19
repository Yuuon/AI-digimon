namespace DigimonBot.Core.Models;

/// <summary>
/// 数码宝贝定义（从配置加载）
/// </summary>
public class DigimonDefinition
{
    /// <summary>唯一标识</summary>
    public string Id { get; set; } = "";
    
    /// <summary>显示名称</summary>
    public string Name { get; set; } = "";
    
    /// <summary>成长阶段</summary>
    public DigimonStage Stage { get; set; }
    
    /// <summary>性格类型</summary>
    public DigimonPersonality Personality { get; set; }
    
    /// <summary>基础系统提示词</summary>
    public string BasePrompt { get; set; } = "";
    
    /// <summary>形象描述（用于生成图片等）</summary>
    public string Appearance { get; set; } = "";
    
    /// <summary>进化选项列表</summary>
    public List<EvolutionOption> NextEvolutions { get; set; } = new();
}

/// <summary>
/// 进化选项
/// </summary>
public class EvolutionOption
{
    /// <summary>目标数码宝贝ID</summary>
    public string TargetId { get; set; } = "";
    
    /// <summary>进化要求（情感属性）</summary>
    public EmotionValues Requirements { get; set; } = new();
    
    /// <summary>所需Token数量（进化阈值）</summary>
    public int MinTokens { get; set; }
    
    /// <summary>优先级（数值越高越优先）</summary>
    public int Priority { get; set; }
    
    /// <summary>进化描述（用于通知用户）</summary>
    public string Description { get; set; } = "";
    
    /// <summary>
    /// 计算进化条件的复杂度
    /// </summary>
    public int CalculateComplexity() => Requirements.CalculateComplexity();
}

/// <summary>
/// 用户当前的数码宝贝实例
/// </summary>
public class UserDigimon
{
    /// <summary>关联的用户ID</summary>
    public string UserId { get; set; } = "";
    
    /// <summary>当前数码宝贝定义ID</summary>
    public string CurrentDigimonId { get; set; } = "";
    
    /// <summary>当前情感属性值</summary>
    public EmotionValues Emotions { get; set; } = new();
    
    /// <summary>累计消耗的Token数（用于进化判定）</summary>
    public int TotalTokensConsumed { get; set; }
    
    /// <summary>对话历史（用于上下文）</summary>
    public List<ChatMessage> ChatHistory { get; set; } = new();
    
    /// <summary>孵化时间</summary>
    public DateTime HatchTime { get; set; }
    
    /// <summary>最后互动时间</summary>
    public DateTime LastInteractionTime { get; set; }
    
    /// <summary>
    /// 创建新的幼年期数码宝贝（从蛋开始）
    /// </summary>
    public static UserDigimon CreateNew(string userId, string babyDigimonId)
    {
        return new UserDigimon
        {
            UserId = userId,
            CurrentDigimonId = babyDigimonId,
            Emotions = new EmotionValues(),
            TotalTokensConsumed = 0,
            HatchTime = DateTime.Now,
            LastInteractionTime = DateTime.Now
        };
    }
}

/// <summary>
/// 聊天消息记录
/// </summary>
public class ChatMessage
{
    public DateTime Timestamp { get; set; }
    public bool IsFromUser { get; set; }
    public string Content { get; set; } = "";
    public int TokensConsumed { get; set; }
    public EmotionAnalysis? EmotionDelta { get; set; }
}

/// <summary>
/// 情感分析结果
/// </summary>
public class EmotionAnalysis
{
    public int CourageDelta { get; set; }
    public int FriendshipDelta { get; set; }
    public int LoveDelta { get; set; }
    public int KnowledgeDelta { get; set; }
    public string Reasoning { get; set; } = "";
}
