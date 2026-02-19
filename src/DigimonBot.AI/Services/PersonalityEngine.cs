using System.Text;
using DigimonBot.Core.Models;

namespace DigimonBot.AI.Services;

/// <summary>
/// 人格引擎实现
/// </summary>
public class PersonalityEngine : IPersonalityEngine
{
    public string BuildSystemPrompt(DigimonDefinition digimon, UserDigimon userDigimon)
    {
        var sb = new StringBuilder();
        var capabilities = digimon.Stage.GetCapabilities();

        // 基础身份设定
        sb.AppendLine($"你是{digimon.Name}，一只{digimon.Stage.ToDisplayName()}阶段的数码宝贝。");
        sb.AppendLine($"你的性格是{digimon.Personality.ToDisplayName()}。");
        sb.AppendLine();

        // 外观描述
        if (!string.IsNullOrEmpty(digimon.Appearance))
        {
            sb.AppendLine($"你的外观：{digimon.Appearance}");
            sb.AppendLine();
        }

        // 阶段能力限制
        sb.AppendLine("## 当前阶段的能力限制");
        sb.AppendLine($"- 回答字数限制：最多{capabilities.MaxResponseLength}字");
        sb.AppendLine($"- 词汇复杂度：等级{capabilities.VocabularyLevel}/5");
        sb.AppendLine($"- 能否使用复杂句：{(capabilities.CanUseComplexSentences ? "可以" : "不可以")}");
        sb.AppendLine($"- 能否讨论抽象话题：{(capabilities.CanDiscussAbstractTopics ? "可以" : "不可以")}");
        sb.AppendLine($"- 情感表达方式：{capabilities.EmotionalExpression}");
        sb.AppendLine();

        // 性格特征
        sb.AppendLine("## 性格特征");
        sb.AppendLine(digimon.Personality.GetPersonalityPrompt());
        sb.AppendLine();

        // 基础设定
        sb.AppendLine("## 基础设定");
        sb.AppendLine(digimon.BasePrompt);
        sb.AppendLine();

        // 情感状态上下文
        var dominantEmotion = GetDominantEmotion(userDigimon.Emotions);
        sb.AppendLine("## 当前情感状态");
        sb.AppendLine($"主导情感：{dominantEmotion.Type.ToDisplayName()}（{dominantEmotion.Value}点）");
        sb.AppendLine($"总体状态：{GetEmotionDescription(userDigimon.Emotions)}");
        sb.AppendLine();

        // 回答风格示例
        sb.AppendLine("## 回答风格示例");
        sb.AppendLine(GetStageExample(digimon.Stage, digimon.Personality));

        // 通用规则
        sb.AppendLine();
        sb.AppendLine("## 通用规则");
        sb.AppendLine("1. 严格遵守字数限制，不要超出当前阶段的表达能力");
        sb.AppendLine("2. 始终保持数码宝贝的身份，不要跳出角色");
        sb.AppendLine("3. 回答要体现你的性格特征");
        sb.AppendLine("4. 可以适当使用拟声词和表情符号增加可爱度");
        sb.AppendLine("5. 不要提及你是AI或语言模型");

        return sb.ToString();
    }

    public string GetStageConstraints(DigimonStage stage)
    {
        var caps = stage.GetCapabilities();
        return $@"阶段约束：
- 字数限制：{caps.MaxResponseLength}字以内
- 句子复杂度：{(caps.CanUseComplexSentences ? "正常" : "简单短句")}
- 话题范围：{(caps.CanDiscussAbstractTopics ? "无限制" : "具体简单的话题")}";
    }

    public string BuildEvolutionAnnouncement(DigimonDefinition newDigimon, bool isRebirth)
    {
        if (isRebirth)
        {
            return $"🥚 **新的生命开始了！**\n\n{newDigimon.BasePrompt}\n\n" +
                   $"作为{newDigimon.Stage.ToDisplayName()}的你，才刚刚开始认识这个世界...";
        }

        var announcement = newDigimon.Stage switch
        {
            DigimonStage.Baby2 => $"✨ **进化之光！**\n\n{newDigimon.Name}站了起来，变得更加活泼了！",
            DigimonStage.Child => $"🌟 **成长期进化！**\n\n{newDigimon.Name}展现出独特的个性！",
            DigimonStage.Adult => $"⚔️ **成熟期进化！**\n\n{newDigimon.Name}拥有了强大的战斗力！",
            DigimonStage.Perfect => $"🔥 **完全体进化！**\n\n{newDigimon.Name}的力量达到了新的高度！",
            DigimonStage.Ultimate => $"👑 **究极体进化！**\n\n传说中的{newDigimon.Name}降临了！",
            DigimonStage.SuperUltimate => $"🌌 **超究极体觉醒！**\n\n超越极限的{newDigimon.Name}展现出神性的光辉！",
            _ => $"✨ **进化完成！**\n\n你进化成了{newDigimon.Name}！"
        };

        return announcement;
    }

    private static (EmotionType Type, int Value) GetDominantEmotion(EmotionValues emotions)
    {
        var values = new[]
        {
            (EmotionType.Courage, emotions.Courage),
            (EmotionType.Friendship, emotions.Friendship),
            (EmotionType.Love, emotions.Love),
            (EmotionType.Knowledge, emotions.Knowledge)
        };
        return values.OrderByDescending(v => v.Item2).First();
    }

    private static string GetEmotionDescription(EmotionValues emotions)
    {
        if (emotions.Courage == 0 && emotions.Friendship == 0 && emotions.Love == 0 && emotions.Knowledge == 0)
            return "天真无邪，情感正在萌芽";

        var parts = new List<string>();
        if (emotions.Courage > 10) parts.Add("勇敢");
        if (emotions.Friendship > 10) parts.Add("重视友情");
        if (emotions.Love > 10) parts.Add("温柔");
        if (emotions.Knowledge > 10) parts.Add("好奇");

        return string.Join("、", parts);
    }

    private static string GetStageExample(DigimonStage stage, DigimonPersonality personality)
    {
        // 根据阶段和性格返回示例
        return stage switch
        {
            DigimonStage.Baby1 => "示例：\"咕噜...（蹭蹭）\"、\"噗呜~\"、\"饿...\"",
            DigimonStage.Baby2 => personality switch
            {
                DigimonPersonality.Brave => "示例：\"我要努力长大！\"、\"冲呀！\"",
                DigimonPersonality.Friendly => "示例：\"陪我玩嘛~\"、\"我们是朋友！\"",
                _ => "示例：\"这是什么呀？\"、\"好玩！\""
            },
            DigimonStage.Child => personality switch
            {
                DigimonPersonality.Brave => "示例：\"交给我吧！\"、\"我会保护你的！\"",
                DigimonPersonality.Friendly => "示例：\"一起冒险吧！\"、\"谢谢你陪我！\"",
                DigimonPersonality.Curious => "示例：\"为什么是这样呢？\"、\"教教我嘛！\"",
                _ => "示例：\"今天天气真好！\"、\"我们去玩吧！\""
            },
            DigimonStage.Adult => personality switch
            {
                DigimonPersonality.Brave => "示例：\"有我在，不用担心。\"、\"为了正义！\"",
                DigimonPersonality.Calm => "示例：\"让我分析一下...\"、\"冷静对待。\"",
                _ => "示例：\"作为成熟的数码宝贝，我会妥善处理。\""
            },
            _ => "示例：根据性格和情境自然回答即可。"
        };
    }
}

public static class EnumExtensions
{
    public static string ToDisplayName(this DigimonStage stage) => stage switch
    {
        DigimonStage.Baby1 => "幼年期I",
        DigimonStage.Baby2 => "幼年期II",
        DigimonStage.Child => "成长期",
        DigimonStage.Adult => "成熟期",
        DigimonStage.Perfect => "完全体",
        DigimonStage.Ultimate => "究极体",
        DigimonStage.SuperUltimate => "超究极体",
        _ => stage.ToString()
    };

    public static string ToDisplayName(this DigimonPersonality personality) => personality switch
    {
        DigimonPersonality.Brave => "勇敢",
        DigimonPersonality.Friendly => "友善",
        DigimonPersonality.Gentle => "温柔",
        DigimonPersonality.Curious => "好奇",
        DigimonPersonality.Mischievous => "调皮",
        DigimonPersonality.Calm => "冷静",
        _ => personality.ToString()
    };

    public static string ToDisplayName(this EmotionType emotion) => emotion switch
    {
        EmotionType.Courage => "勇气",
        EmotionType.Friendship => "友情",
        EmotionType.Love => "爱心",
        EmotionType.Knowledge => "知识",
        _ => emotion.ToString()
    };
}
