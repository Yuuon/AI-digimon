using DigimonBot.Core.Events;
using DigimonBot.Core.Models;

namespace DigimonBot.Core.Services;

/// <summary>
/// 进化引擎接口
/// </summary>
public interface IEvolutionEngine
{
    /// <summary>
    /// 检查并执行进化
    /// </summary>
    /// <param name="userDigimon">用户数码宝贝实例</param>
    /// <param name="digimonDb">数码宝贝数据库</param>
    /// <returns>进化结果，如果没有进化则返回null</returns>
    Task<EvolutionResult?> CheckAndEvolveAsync(UserDigimon userDigimon, IReadOnlyDictionary<string, DigimonDefinition> digimonDb);
    
    /// <summary>
    /// 获取当前进化进度
    /// </summary>
    EvolutionProgress GetProgress(UserDigimon userDigimon, DigimonDefinition currentDef);
    
    /// <summary>
    /// 预测可能的进化路径
    /// </summary>
    List<PossibleEvolution> GetPossibleEvolutions(UserDigimon userDigimon, DigimonDefinition currentDef, IReadOnlyDictionary<string, DigimonDefinition> digimonDb);
}

/// <summary>
/// 进化结果
/// </summary>
public class EvolutionResult
{
    public bool Success { get; set; }
    public string OldDigimonId { get; set; } = "";
    public string NewDigimonId { get; set; } = "";
    public string NewDigimonName { get; set; } = "";
    public bool IsRebirth { get; set; }
    public string Message { get; set; } = "";
}

/// <summary>
/// 进化进度
/// </summary>
public class EvolutionProgress
{
    public int CurrentTokens { get; set; }
    public int RequiredTokens { get; set; }
    public double TokenProgressPercent => RequiredTokens > 0 
        ? Math.Min((double)CurrentTokens / RequiredTokens * 100, 100) 
        : 100;
    
    public EmotionValues CurrentEmotions { get; set; } = new();
    public EmotionValues? RequiredEmotions { get; set; }
    public double EmotionProgressPercent { get; set; }
    
    public bool IsReadyForEvolution => TokenProgressPercent >= 100 && EmotionProgressPercent >= 100;
}

/// <summary>
/// 可能的进化选项
/// </summary>
public class PossibleEvolution
{
    public string TargetId { get; set; } = "";
    public string TargetName { get; set; } = "";
    public int RequiredTokens { get; set; }
    public EmotionValues RequiredEmotions { get; set; } = new();
    public double CurrentMatchPercent { get; set; }
    public int Priority { get; set; }
}
