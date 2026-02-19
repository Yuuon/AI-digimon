using DigimonBot.Core.Models;

namespace DigimonBot.Core.Services;

/// <summary>
/// 进化引擎实现
/// </summary>
public class EvolutionEngine : IEvolutionEngine
{
    /// <summary>
    /// 接近进化阈值的通知百分比（80%时通知）
    /// </summary>
    private const double APPROACHING_THRESHOLD = 0.8;

    public Task<EvolutionResult?> CheckAndEvolveAsync(
        UserDigimon userDigimon, 
        IReadOnlyDictionary<string, DigimonDefinition> digimonDb)
    {
        if (!digimonDb.TryGetValue(userDigimon.CurrentDigimonId, out var currentDef))
        {
            return Task.FromResult<EvolutionResult?>(null);
        }

        // 检查是否满足任何进化条件
        var possibleEvolutions = new List<(EvolutionOption Option, double MatchScore)>();

        foreach (var evoOption in currentDef.NextEvolutions)
        {
            // 检查Token要求
            if (userDigimon.TotalTokensConsumed < evoOption.MinTokens)
                continue;

            // 检查情感要求
            if (!userDigimon.Emotions.MeetsRequirements(evoOption.Requirements))
                continue;

            // 计算匹配度
            var matchScore = userDigimon.Emotions.CalculateMatchScore(evoOption.Requirements);
            possibleEvolutions.Add((evoOption, matchScore));
        }

        if (possibleEvolutions.Count == 0)
        {
            return Task.FromResult<EvolutionResult?>(null);
        }

        // 按优先级排序：复杂度（多样性）> 优先级字段 > 匹配度
        var bestEvolution = possibleEvolutions
            .OrderByDescending(e => e.Option.CalculateComplexity())
            .ThenByDescending(e => e.Option.Priority)
            .ThenByDescending(e => e.MatchScore)
            .First();

        var targetId = bestEvolution.Option.TargetId;
        
        // 检查是否是最终形态后的重生
        bool isRebirth = currentDef.Stage.IsFinalForm();
        
        if (isRebirth)
        {
            // 最终形态进化后回到蛋
            targetId = GetDefaultEggId(digimonDb);
        }

        if (!digimonDb.TryGetValue(targetId, out var newDef))
        {
            return Task.FromResult<EvolutionResult?>(null);
        }

        var result = new EvolutionResult
        {
            Success = true,
            OldDigimonId = userDigimon.CurrentDigimonId,
            NewDigimonId = targetId,
            NewDigimonName = newDef.Name,
            IsRebirth = isRebirth,
            Message = isRebirth 
                ? $"{currentDef.Name}完成了它的使命，化作光芒回归数码世界...一颗新的数码蛋诞生了！"
                : $"恭喜！{currentDef.Name}进化成了{newDef.Name}！{bestEvolution.Option.Description}"
        };

        return Task.FromResult<EvolutionResult?>(result);
    }

    public EvolutionProgress GetProgress(UserDigimon userDigimon, DigimonDefinition currentDef)
    {
        var progress = new EvolutionProgress
        {
            CurrentTokens = userDigimon.TotalTokensConsumed,
            CurrentEmotions = userDigimon.Emotions.Clone()
        };

        if (currentDef.NextEvolutions.Count == 0)
        {
            progress.RequiredTokens = int.MaxValue;
            progress.EmotionProgressPercent = 100;
            return progress;
        }

        // 找出当前最接近的进化路径
        var closestEvo = currentDef.NextEvolutions
            .OrderBy(e => Math.Max(0, e.MinTokens - userDigimon.TotalTokensConsumed))
            .ThenByDescending(e => userDigimon.Emotions.CalculateMatchScore(e.Requirements))
            .First();

        progress.RequiredTokens = closestEvo.MinTokens;
        progress.RequiredEmotions = closestEvo.Requirements;
        progress.EmotionProgressPercent = 
            userDigimon.Emotions.CalculateMatchScore(closestEvo.Requirements) * 100;

        return progress;
    }

    public List<PossibleEvolution> GetPossibleEvolutions(UserDigimon userDigimon, DigimonDefinition currentDef, IReadOnlyDictionary<string, DigimonDefinition> digimonDb)
    {
        var result = new List<PossibleEvolution>();

        foreach (var option in currentDef.NextEvolutions)
        {
            if (!digimonDb.TryGetValue(option.TargetId, out var targetDef))
                continue;

            result.Add(new PossibleEvolution
            {
                TargetId = option.TargetId,
                TargetName = targetDef.Name,
                RequiredTokens = option.MinTokens,
                RequiredEmotions = option.Requirements,
                CurrentMatchPercent = userDigimon.Emotions.CalculateMatchScore(option.Requirements) * 100,
                Priority = option.Priority
            });
        }

        return result.OrderByDescending(e => e.Priority).ToList();
    }

    private string GetDefaultEggId(IReadOnlyDictionary<string, DigimonDefinition> digimonDb)
    {
        // 找到幼年期I的数码宝贝作为重生起点
        var egg = digimonDb.Values.FirstOrDefault(d => d.Stage == DigimonStage.Baby1);
        return egg?.Id ?? "botamon";
    }

    private static IReadOnlyDictionary<string, DigimonDefinition> digimonDb => 
        throw new InvalidOperationException("Should be injected");
}
