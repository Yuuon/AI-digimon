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
        // 获取所有可进化选项
        if (!digimonDb.TryGetValue(userDigimon.CurrentDigimonId, out var currentDef))
        {
            return Task.FromResult<EvolutionResult?>(null);
        }

        var availableEvolutions = GetAvailableEvolutions(userDigimon, currentDef, digimonDb);

        // 如果没有可进化选项，返回null
        if (availableEvolutions.Count == 0)
        {
            return Task.FromResult<EvolutionResult?>(null);
        }

        // 如果只有一个可进化选项，自动选择
        if (availableEvolutions.Count == 1)
        {
            return ExecuteEvolutionAsync(userDigimon, currentDef, availableEvolutions[0].TargetId, digimonDb);
        }

        // 多个可进化选项，不自动选择，返回null（等待用户手动选择）
        return Task.FromResult<EvolutionResult?>(null);
    }

    /// <summary>
    /// 获取所有满足条件的可进化选项
    /// </summary>
    public List<AvailableEvolution> GetAvailableEvolutions(UserDigimon userDigimon, DigimonDefinition currentDef, IReadOnlyDictionary<string, DigimonDefinition> digimonDb)
    {
        var result = new List<AvailableEvolution>();
        bool isRebirth = currentDef.Stage.IsFinalForm();

        foreach (var evoOption in currentDef.NextEvolutions)
        {
            // 检查Token要求
            if (userDigimon.TotalTokensConsumed < evoOption.MinTokens)
                continue;

            // 检查情感要求
            if (!userDigimon.Emotions.MeetsRequirements(evoOption.Requirements))
                continue;

            // 获取目标定义
            var targetId = isRebirth ? GetDefaultEggId(digimonDb) : evoOption.TargetId;
            if (!digimonDb.TryGetValue(targetId, out var targetDef))
                continue;

            // 计算匹配度
            var matchScore = userDigimon.Emotions.CalculateMatchScore(evoOption.Requirements);

            result.Add(new AvailableEvolution
            {
                TargetId = targetId,
                TargetName = targetDef.Name,
                Description = evoOption.Description,
                RequiredTokens = evoOption.MinTokens,
                RequiredEmotions = evoOption.Requirements,
                MatchScore = matchScore,
                Priority = evoOption.Priority,
                IsRebirth = isRebirth
            });
        }

        // 按优先级排序：复杂度 > 优先级字段 > 匹配度
        return result
            .OrderByDescending(e => digimonDb.TryGetValue(e.TargetId, out var def) ? def.NextEvolutions.Count : 0)
            .ThenByDescending(e => e.Priority)
            .ThenByDescending(e => e.MatchScore)
            .ToList();
    }

    /// <summary>
    /// 选择并执行特定进化分支
    /// </summary>
    public Task<EvolutionResult?> SelectEvolutionAsync(UserDigimon userDigimon, DigimonDefinition currentDef, string targetId, IReadOnlyDictionary<string, DigimonDefinition> digimonDb)
    {
        // 验证是否满足进化条件
        var availableEvolutions = GetAvailableEvolutions(userDigimon, currentDef, digimonDb);
        var selectedEvo = availableEvolutions.FirstOrDefault(e => e.TargetId.Equals(targetId, StringComparison.OrdinalIgnoreCase));

        if (selectedEvo == null)
        {
            // 检查是否是重生的特殊情况
            if (currentDef.Stage.IsFinalForm())
            {
                var eggId = GetDefaultEggId(digimonDb);
                if (targetId.Equals(eggId, StringComparison.OrdinalIgnoreCase) && availableEvolutions.Count > 0)
                {
                    return ExecuteEvolutionAsync(userDigimon, currentDef, eggId, digimonDb);
                }
            }
            return Task.FromResult<EvolutionResult?>(null);
        }

        return ExecuteEvolutionAsync(userDigimon, currentDef, targetId, digimonDb);
    }

    /// <summary>
    /// 执行进化
    /// </summary>
    private Task<EvolutionResult?> ExecuteEvolutionAsync(UserDigimon userDigimon, DigimonDefinition currentDef, string targetId, IReadOnlyDictionary<string, DigimonDefinition> digimonDb)
    {
        bool isRebirth = currentDef.Stage.IsFinalForm();
        
        // 如果是重生，强制使用蛋的ID
        if (isRebirth)
        {
            targetId = GetDefaultEggId(digimonDb);
        }

        if (!digimonDb.TryGetValue(targetId, out var newDef))
        {
            return Task.FromResult<EvolutionResult?>(null);
        }

        // 找到对应的进化选项以获取描述
        var evoOption = currentDef.NextEvolutions.FirstOrDefault(e => e.TargetId == targetId || isRebirth);

        var result = new EvolutionResult
        {
            Success = true,
            OldDigimonId = userDigimon.CurrentDigimonId,
            NewDigimonId = targetId,
            NewDigimonName = newDef.Name,
            IsRebirth = isRebirth,
            Message = isRebirth 
                ? $"{currentDef.Name}完成了它的使命，化作光芒回归数码世界...一颗新的数码蛋诞生了！"
                : $"恭喜！{currentDef.Name}进化成了{newDef.Name}！{evoOption?.Description ?? "获得了新的力量！"}"
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
            .FirstOrDefault();

        if (closestEvo == null)
        {
            progress.RequiredTokens = int.MaxValue;
            progress.EmotionProgressPercent = 0;
            return progress;
        }

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
