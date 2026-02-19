namespace DigimonBot.Core.Models;

/// <summary>
/// 数码宝贝成长阶段
/// </summary>
public enum DigimonStage
{
    /// <summary>幼年期I - 刚孵化的形态，回答极其简单</summary>
    Baby1,
    /// <summary>幼年期II - 学会基本交流</summary>
    Baby2,
    /// <summary>成长期 - 开始有个性</summary>
    Child,
    /// <summary>成熟期 - 完全战斗能力</summary>
    Adult,
    /// <summary>完全体 - 强大战力</summary>
    Perfect,
    /// <summary>究极体 - 巅峰形态</summary>
    Ultimate,
    /// <summary>超究极体 - 超越极限</summary>
    SuperUltimate
}

public static class DigimonStageExtensions
{
    /// <summary>
    /// 获取阶段的基础能力限制（影响AI回答）
    /// </summary>
    public static StageCapabilities GetCapabilities(this DigimonStage stage) => stage switch
    {
        DigimonStage.Baby1 => new StageCapabilities
        {
            MaxResponseLength = 20,
            VocabularyLevel = 1,
            CanUseComplexSentences = false,
            CanDiscussAbstractTopics = false,
            EmotionalExpression = "婴儿般的简单词汇，拟声词"
        },
        DigimonStage.Baby2 => new StageCapabilities
        {
            MaxResponseLength = 50,
            VocabularyLevel = 2,
            CanUseComplexSentences = false,
            CanDiscussAbstractTopics = false,
            EmotionalExpression = "简单短句，带有童趣"
        },
        DigimonStage.Child => new StageCapabilities
        {
            MaxResponseLength = 150,
            VocabularyLevel = 3,
            CanUseComplexSentences = true,
            CanDiscussAbstractTopics = false,
            EmotionalExpression = "活泼好动，开始展现性格特征"
        },
        DigimonStage.Adult => new StageCapabilities
        {
            MaxResponseLength = 300,
            VocabularyLevel = 4,
            CanUseComplexSentences = true,
            CanDiscussAbstractTopics = true,
            EmotionalExpression = "成熟稳重，性格特征明显"
        },
        DigimonStage.Perfect => new StageCapabilities
        {
            MaxResponseLength = 400,
            VocabularyLevel = 5,
            CanUseComplexSentences = true,
            CanDiscussAbstractTopics = true,
            EmotionalExpression = "强大自信，富有智慧"
        },
        DigimonStage.Ultimate => new StageCapabilities
        {
            MaxResponseLength = 500,
            VocabularyLevel = 5,
            CanUseComplexSentences = true,
            CanDiscussAbstractTopics = true,
            EmotionalExpression = "巅峰存在，兼具力量与智慧"
        },
        DigimonStage.SuperUltimate => new StageCapabilities
        {
            MaxResponseLength = 600,
            VocabularyLevel = 5,
            CanUseComplexSentences = true,
            CanDiscussAbstractTopics = true,
            EmotionalExpression = "超越常理，近乎神性的存在"
        },
        _ => new StageCapabilities()
    };

    /// <summary>
    /// 是否是最终形态（进化后会返回蛋）
    /// </summary>
    public static bool IsFinalForm(this DigimonStage stage) => 
        stage == DigimonStage.Ultimate || stage == DigimonStage.SuperUltimate;
}

/// <summary>
/// 阶段能力限制
/// </summary>
public class StageCapabilities
{
    /// <summary>最大回答字数</summary>
    public int MaxResponseLength { get; set; }
    /// <summary>词汇复杂度等级 1-5</summary>
    public int VocabularyLevel { get; set; }
    /// <summary>能否使用复杂句</summary>
    public bool CanUseComplexSentences { get; set; }
    /// <summary>能否讨论抽象话题</summary>
    public bool CanDiscussAbstractTopics { get; set; }
    /// <summary>情感表达方式描述</summary>
    public string EmotionalExpression { get; set; } = "";
}
