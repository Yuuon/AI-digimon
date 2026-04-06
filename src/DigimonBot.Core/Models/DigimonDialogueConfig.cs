namespace DigimonBot.Core.Models;

/// <summary>
/// 数码兽对话内容配置
/// </summary>
public class DigimonDialogueConfig
{
    /// <summary>
    /// 配置文件路径
    /// </summary>
    public const string DefaultConfigPath = "Data/digimon_dialogue_config.json";

    /// <summary>
    /// 战斗相关对话
    /// </summary>
    public BattleDialogue Battle { get; set; } = new();

    /// <summary>
    /// 签到相关对话
    /// </summary>
    public CheckInDialogue CheckIn { get; set; } = new();

    /// <summary>
    /// 进化相关对话
    /// </summary>
    public EvolutionDialogue Evolution { get; set; } = new();

    /// <summary>
    /// 闲置时对话
    /// </summary>
    public IdleDialogue Idle { get; set; } = new();

    /// <summary>
    /// 情感响应
    /// </summary>
    public EmotionResponses EmotionResponses { get; set; } = new();
}

public class BattleDialogue
{
    // 数码兽角色扮演用提示词（用于简单回复）
    public string AttackPrompt { get; set; } = "";
    public string DefensePrompt { get; set; } = "";
    public string VictoryPrompt { get; set; } = "";
    public string DefeatPrompt { get; set; } = "";
    public string ToObjectPrompt { get; set; } = "";
    
    // AI旁白系统提示词（用于生成详细战斗描述）
    public string BattleSystemPrompt { get; set; } = "";
    public string BattleUserPromptTemplate { get; set; } = "";
    public string BattleObjectSystemPrompt { get; set; } = "";
    public string BattleObjectUserPromptTemplate { get; set; } = "";
}

public class CheckInDialogue
{
    public List<string> GreetingTemplates { get; set; } = new();
    public List<string> StreakEncouragement { get; set; } = new();
    public List<string> ReceivedGiftResponse { get; set; } = new();
}

public class EvolutionDialogue
{
    public string EvolutionAnnouncement { get; set; } = "";
    public string RebirthAnnouncement { get; set; } = "";
    public string PostEvolutionGreeting { get; set; } = "";
}

public class IdleDialogue
{
    public List<string> IdlePrompts { get; set; } = new();
}

public class EmotionResponses
{
    public string HighCourage { get; set; } = "";
    public string HighFriendship { get; set; } = "";
    public string HighLove { get; set; } = "";
    public string HighKnowledge { get; set; } = "";
}
