using DigimonBot.Core.Models;

namespace DigimonBot.AI.Services;

/// <summary>
/// 人格引擎接口 - 构建系统提示词
/// </summary>
public interface IPersonalityEngine
{
    /// <summary>
    /// 构建系统提示词
    /// </summary>
    string BuildSystemPrompt(DigimonDefinition digimon, UserDigimon userDigimon);
    
    /// <summary>
    /// 获取阶段特定的回答约束
    /// </summary>
    string GetStageConstraints(DigimonStage stage);
    
    /// <summary>
    /// 构建进化通知消息
    /// </summary>
    string BuildEvolutionAnnouncement(DigimonDefinition newDigimon, bool isRebirth);
}
