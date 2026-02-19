using DigimonBot.Core.Models;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Core.Services;

/// <summary>
/// 情感追踪器实现
/// </summary>
public class EmotionTracker : IEmotionTracker
{
    /// <summary>
    /// 单次情感变化的最大值（防止一次对话变化过大）
    /// </summary>
    private const int MAX_SINGLE_DELTA = 10;
    
    private readonly ILogger<EmotionTracker> _logger;

    public EmotionTracker(ILogger<EmotionTracker> logger)
    {
        _logger = logger;
    }

    public Task ApplyEmotionAnalysisAsync(UserDigimon userDigimon, EmotionAnalysis analysis, string reason)
    {
        // 限制单次变化幅度
        var courageDelta = Math.Clamp(analysis.CourageDelta, -MAX_SINGLE_DELTA, MAX_SINGLE_DELTA);
        var friendshipDelta = Math.Clamp(analysis.FriendshipDelta, -MAX_SINGLE_DELTA, MAX_SINGLE_DELTA);
        var loveDelta = Math.Clamp(analysis.LoveDelta, -MAX_SINGLE_DELTA, MAX_SINGLE_DELTA);
        var knowledgeDelta = Math.Clamp(analysis.KnowledgeDelta, -MAX_SINGLE_DELTA, MAX_SINGLE_DELTA);

        userDigimon.Emotions.Courage += courageDelta;
        userDigimon.Emotions.Friendship += friendshipDelta;
        userDigimon.Emotions.Love += loveDelta;
        userDigimon.Emotions.Knowledge += knowledgeDelta;

        // 确保情感值不会为负
        userDigimon.Emotions.Courage = Math.Max(0, userDigimon.Emotions.Courage);
        userDigimon.Emotions.Friendship = Math.Max(0, userDigimon.Emotions.Friendship);
        userDigimon.Emotions.Love = Math.Max(0, userDigimon.Emotions.Love);
        userDigimon.Emotions.Knowledge = Math.Max(0, userDigimon.Emotions.Knowledge);

        return Task.CompletedTask;
    }

    public string GetEmotionDescription(EmotionValues emotions)
    {
        var parts = new List<string>();
        
        if (emotions.Courage > 30) parts.Add("充满勇气");
        else if (emotions.Courage > 10) parts.Add("有些勇敢");
        
        if (emotions.Friendship > 30) parts.Add("非常重视友情");
        else if (emotions.Friendship > 10) parts.Add("珍视伙伴");
        
        if (emotions.Love > 30) parts.Add("充满爱心");
        else if (emotions.Love > 10) parts.Add("温柔体贴");
        
        if (emotions.Knowledge > 30) parts.Add("博学多识");
        else if (emotions.Knowledge > 10) parts.Add("好奇心旺盛");

        if (parts.Count == 0)
            return "还很天真，情感正在萌芽";

        return string.Join("，", parts);
    }

    public (EmotionType Type, int Value) GetDominantEmotion(EmotionValues emotions)
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

    public string GetEmotionContextHint(EmotionValues emotions, DigimonPersonality personality)
    {
        var dominant = GetDominantEmotion(emotions);
        var affinity = personality.GetAffinityEmotion();
        
        var hints = new List<string>();

        // 基于主导情感的倾向
        hints.Add(dominant.Type switch
        {
            EmotionType.Courage => "最近经历了很多挑战，你变得更加勇敢了。",
            EmotionType.Friendship => "和伙伴们的羁绊让你变得更强大。",
            EmotionType.Love => "温柔的心让你能够治愈他人。",
            EmotionType.Knowledge => "不断的学习让你获得了智慧。",
            _ => ""
        });

        // 性格与情感的共鸣
        if (affinity.HasValue && emotions.GetValue(affinity.Value) > 20)
        {
            hints.Add($"作为{GetPersonalityName(personality)}，你在{GetEmotionName(affinity.Value)}方面的成长尤为显著。");
        }

        return string.Join(" ", hints);
    }

    private static string GetPersonalityName(DigimonPersonality personality) => personality switch
    {
        DigimonPersonality.Brave => "勇敢的数码宝贝",
        DigimonPersonality.Friendly => "友善的数码宝贝",
        DigimonPersonality.Gentle => "温柔的数码宝贝",
        DigimonPersonality.Curious => "好奇的数码宝贝",
        DigimonPersonality.Mischievous => "调皮的数码宝贝",
        DigimonPersonality.Calm => "冷静的数码宝贝",
        _ => "数码宝贝"
    };

    private static string GetEmotionName(EmotionType emotion) => emotion switch
    {
        EmotionType.Courage => "勇气",
        EmotionType.Friendship => "友情",
        EmotionType.Love => "爱心",
        EmotionType.Knowledge => "知识",
        _ => ""
    };
}
