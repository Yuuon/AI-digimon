using DigimonBot.Core.Models;

namespace DigimonBot.Core.Services;

/// <summary>
/// 战斗服务接口
/// </summary>
public interface IBattleService
{
    /// <summary>
    /// 执行数码兽之间的战斗
    /// </summary>
    /// <param name="attacker">攻击方状态</param>
    /// <param name="attackerDef">攻击方定义</param>
    /// <param name="target">被攻击方状态</param>
    /// <param name="targetDef">被攻击方定义</param>
    /// <returns>战斗结果</returns>
    Task<BattleResult> BattleDigimonAsync(
        UserDigimonState attacker, 
        DigimonDefinition attackerDef,
        UserDigimonState target, 
        DigimonDefinition targetDef);

    /// <summary>
    /// 执行数码兽攻击物体
    /// </summary>
    /// <param name="attacker">攻击方状态</param>
    /// <param name="attackerDef">攻击方定义</param>
    /// <param name="targetDescription">攻击目标描述</param>
    /// <returns>战斗结果</returns>
    Task<BattleResult> BattleObjectAsync(
        UserDigimonState attacker, 
        DigimonDefinition attackerDef,
        string targetDescription);

    /// <summary>
    /// 检查用户是否处于被保护状态（刚被攻击过）
    /// </summary>
    Task<bool> IsUnderProtectionAsync(string userId, string? groupId = null);

    /// <summary>
    /// 设置用户的保护状态
    /// </summary>
    Task SetProtectionAsync(string userId, string? groupId = null);
}

/// <summary>
/// 战斗结果
/// </summary>
public class BattleResult
{
    /// <summary>是否成功执行战斗</summary>
    public bool Success { get; set; }

    /// <summary>旁白描述文本</summary>
    public string Narrative { get; set; } = "";

    /// <summary>获胜方用户ID（null表示平局或无明确胜负）</summary>
    public string? WinnerUserId { get; set; }

    /// <summary>攻击方情感变化</summary>
    public EmotionChanges AttackerEmotionChanges { get; set; } = new();

    /// <summary>被攻击方情感变化（如果是数码兽对战）</summary>
    public EmotionChanges? TargetEmotionChanges { get; set; }

    /// <summary>使用的Token数（旁白消耗，不计入成长）</summary>
    public int TokensConsumed { get; set; }
}

/// <summary>
/// 情感变化
/// </summary>
public class EmotionChanges
{
    public int CourageDelta { get; set; }
    public int FriendshipDelta { get; set; }
    public int LoveDelta { get; set; }
    public int KnowledgeDelta { get; set; }

    public bool HasChanges => CourageDelta != 0 || FriendshipDelta != 0 || 
                              LoveDelta != 0 || KnowledgeDelta != 0;

    public override string ToString()
    {
        var changes = new List<string>();
        if (CourageDelta != 0) changes.Add($"勇气{(CourageDelta > 0 ? "+" : "")}{CourageDelta}");
        if (FriendshipDelta != 0) changes.Add($"友情{(FriendshipDelta > 0 ? "+" : "")}{FriendshipDelta}");
        if (LoveDelta != 0) changes.Add($"爱心{(LoveDelta > 0 ? "+" : "")}{LoveDelta}");
        if (KnowledgeDelta != 0) changes.Add($"知识{(KnowledgeDelta > 0 ? "+" : "")}{KnowledgeDelta}");
        return string.Join("、", changes);
    }
}
