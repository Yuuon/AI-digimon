using DigimonBot.Core.Models;

namespace DigimonBot.Core.Services;

/// <summary>
/// 情感追踪器接口
/// </summary>
public interface IEmotionTracker
{
    /// <summary>
    /// 应用情感分析结果到用户数码宝贝
    /// </summary>
    Task ApplyEmotionAnalysisAsync(UserDigimon userDigimon, EmotionAnalysis analysis, string reason);
    
    /// <summary>
    /// 获取当前情感状态描述
    /// </summary>
    string GetEmotionDescription(EmotionValues emotions);
    
    /// <summary>
    /// 获取主导情感
    /// </summary>
    (EmotionType Type, int Value) GetDominantEmotion(EmotionValues emotions);
    
    /// <summary>
    /// 获取情感变化提示（用于AI上下文）
    /// </summary>
    string GetEmotionContextHint(EmotionValues emotions, DigimonPersonality personality);
}
