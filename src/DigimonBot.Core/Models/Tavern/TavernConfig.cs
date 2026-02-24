namespace DigimonBot.Core.Models.Tavern;

/// <summary>
/// 酒馆系统配置
/// </summary>
public class TavernConfig
{
    /// <summary>
    /// 配置文件路径
    /// </summary>
    public const string DefaultConfigPath = "Data/tavern_config.json";

    /// <summary>
    /// 群聊监测配置
    /// </summary>
    public MonitorConfig Monitor { get; set; } = new();

    /// <summary>
    /// 自主发言配置
    /// </summary>
    public AutoSpeakConfig AutoSpeak { get; set; } = new();

    /// <summary>
    /// AI生成配置
    /// </summary>
    public GenerationConfig Generation { get; set; } = new();

    /// <summary>
    /// 特别关注配置
    /// </summary>
    public SpecialFocusConfig SpecialFocus { get; set; } = new();

    /// <summary>
    /// 角色卡目录路径
    /// </summary>
    public string CharacterDirectory { get; set; } = "Data/Characters";

    /// <summary>
    /// 是否启用调试日志
    /// </summary>
    public bool EnableDebugLog { get; set; } = true;
}

/// <summary>
/// 群聊监测配置
/// </summary>
public class MonitorConfig
{
    /// <summary>
    /// 最大保留消息数
    /// </summary>
    public int MaxMessageCount { get; set; } = 20;

    /// <summary>
    /// 触发检测所需的最小消息数
    /// </summary>
    public int MinMessageCount { get; set; } = 3;

    /// <summary>
    /// 关键词出现阈值（达到此次数视为高频）
    /// </summary>
    public int KeywordThreshold { get; set; } = 2;

    /// <summary>
    /// 触发间隔（分钟）
    /// </summary>
    public int TriggerIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// 关键词最小长度
    /// </summary>
    public int MinKeywordLength { get; set; } = 2;

    /// <summary>
    /// 关键词最大长度
    /// </summary>
    public int MaxKeywordLength { get; set; } = 10;

    /// <summary>
    /// 停用词列表
    /// </summary>
    public List<string> StopWords { get; set; } = new()
    {
        "的", "了", "是", "我", "你", "他", "她", "它", "们",
        "在", "有", "和", "就", "都", "而", "及", "与", "或",
        "但是", "一个", "没有", "这个", "那个", "可以", "的话",
        "还是", "或者", "如果", "因为", "所以", "虽然", "一下"
    };
}

/// <summary>
/// 自主发言配置
/// </summary>
public class AutoSpeakConfig
{
    /// <summary>
    /// 是否启用自主发言
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 发言前缀模板（支持 {CharacterName} 占位符）
    /// </summary>
    public string MessagePrefix { get; set; } = "🎭 **{CharacterName}**（听到你们讨论得热烈，忍不住插话）\n\n";

    /// <summary>
    /// 发言场景描述（用于AI提示词）
    /// </summary>
    public string ScenarioTemplate { get; set; } = "群聊正在讨论：{Keywords}，请根据这个话题插话参与讨论";

    /// <summary>
    /// 连续发言间隔（秒）
    /// </summary>
    public int MinIntervalSeconds { get; set; } = 10;
}

/// <summary>
/// AI生成配置
/// </summary>
public class GenerationConfig
{
    /// <summary>
    /// 总结生成提示词模板
    /// </summary>
    public string SummaryPromptTemplate { get; set; } = """
        请总结以下群聊对话的主要内容：

        {Conversation}

        请用2-3句话简洁概括讨论的主题和要点。
        """;

    /// <summary>
    /// 最大Token数（总结）
    /// </summary>
    public int SummaryMaxTokens { get; set; } = 200;

    /// <summary>
    /// 最大Token数（回复）
    /// </summary>
    public int ResponseMaxTokens { get; set; } = 500;

    /// <summary>
    /// 温度参数（创造性）
    /// </summary>
    public double Temperature { get; set; } = 0.8;

    /// <summary>
    /// 对话历史最大条数
    /// </summary>
    public int MaxHistoryLength { get; set; } = 20;
}

/// <summary>
/// 特别关注配置
/// </summary>
public class SpecialFocusConfig
{
    /// <summary>
    /// 是否启用特别关注功能
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 特别关注用户QQ号列表
    /// </summary>
    public List<string> UserIds { get; set; } = new();

    /// <summary>
    /// 触发冷却时间（分钟）
    /// </summary>
    public int CooldownMinutes { get; set; } = 3;

    /// <summary>
    /// 是否要求用户@Bot才回复
    /// </summary>
    public bool RequireMention { get; set; } = false;

    /// <summary>
    /// 消息前缀模板（支持 {CharacterName} 和 {UserName} 占位符）
    /// </summary>
    public string MessagePrefix { get; set; } = "🎭 **{CharacterName}**（注意到{UserName}的发言）\n\n";

    /// <summary>
    /// 回复场景描述模板（用于AI提示词）
    /// </summary>
    public string ScenarioTemplate { get; set; } = "{UserName}对你说：{Message}\n\n请根据这段话进行回复。注意保持你的人设和性格特点，回复要自然、有针对性。";
}
