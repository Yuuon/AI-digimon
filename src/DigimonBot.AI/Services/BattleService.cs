using System.Collections.Concurrent;
using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using Microsoft.Extensions.Logging;

namespace DigimonBot.AI.Services;

/// <summary>
/// 战斗服务实现
/// </summary>
public class BattleService : IBattleService
{
    private readonly IAIClient _aiClient;
    private readonly ILogger<BattleService> _logger;
    private readonly ConcurrentDictionary<string, DateTime> _protectionMap = new();
    private readonly int _protectionSeconds;

    public BattleService(
        IAIClient aiClient,
        ILogger<BattleService> logger,
        int protectionSeconds = 300) // 默认5分钟
    {
        _aiClient = aiClient;
        _logger = logger;
        _protectionSeconds = protectionSeconds;
    }

    public async Task<BattleResult> BattleDigimonAsync(
        UserDigimonState attacker, 
        DigimonDefinition attackerDef,
        UserDigimonState target, 
        DigimonDefinition targetDef)
    {
        try
        {
            // 构建战斗场景提示词
            var prompt = BuildBattlePrompt(attackerDef, targetDef, attacker, target);
            
            // 调用AI生成战斗旁白
            var systemPrompt = "你是一位战斗旁白叙述者。用生动的中文描述数码宝贝之间的战斗过程。" +
                             "描述要包含战斗的起因、过程、高潮和结果。" +
                             "根据双方的性格和阶段差异，自然地展现战斗的激烈程度。" +
                             "结尾处用一行简短总结战斗结果（谁获胜或平局）。";

            var messages = new List<ChatMessage>
            {
                new() { IsFromUser = true, Content = prompt }
            };

            var response = await _aiClient.ChatAsync(messages, systemPrompt);

            // 解析战斗结果和情感变化
            var (winner, attackerChanges, targetChanges) = ParseBattleResult(
                response.Content, attacker.UserId, target.UserId);

            return new BattleResult
            {
                Success = true,
                Narrative = response.Content,
                WinnerUserId = winner,
                AttackerEmotionChanges = attackerChanges,
                TargetEmotionChanges = targetChanges,
                TokensConsumed = response.TotalTokens // 旁白消耗，不计入成长
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成战斗旁白失败");
            return new BattleResult 
            { 
                Success = false, 
                Narrative = "战斗过程生成失败..." 
            };
        }
    }

    public async Task<BattleResult> BattleObjectAsync(
        UserDigimonState attacker, 
        DigimonDefinition attackerDef,
        string targetDescription)
    {
        try
        {
            // 构建攻击物体的提示词
            var prompt = $"""
                请描述一场数码宝贝攻击物体的场景：
                
                攻击方：{attackerDef.Name}（{attackerDef.Stage.ToDisplayName()}）
                性格：{attackerDef.Personality.ToDisplayName()}
                形象：{attackerDef.Appearance}
                
                攻击目标：{targetDescription}
                
                请用生动的中文描述这个场景：
                1. {attackerDef.Name}为什么攻击这个目标
                2. 攻击的过程
                3. 攻击的结果（成功破坏、留下痕迹、完全无效等）
                4. 攻击后{attackerDef.Name}的反应和情感变化
                
                在描述中自然地体现情感变化：
                - 主动攻击体现勇气
                - 与物体互动体现好奇心（知识）或玩乐心态（友情）
                - 破坏后的反应体现爱心（如果后悔）或满足（如果成功）
                """;

            var systemPrompt = "你是一位场景旁白叙述者。用生动的中文描述数码宝贝与物体互动的过程。" +
                             "根据数码宝贝的性格和阶段，自然地展现其行为的合理性。" +
                             "结尾处用一行简短总结攻击结果和情感影响。";

            var messages = new List<ChatMessage>
            {
                new() { IsFromUser = true, Content = prompt }
            };

            var response = await _aiClient.ChatAsync(messages, systemPrompt);

            // 解析情感变化
            var changes = ParseObjectBattleEmotions(response.Content);

            return new BattleResult
            {
                Success = true,
                Narrative = response.Content,
                AttackerEmotionChanges = changes,
                TokensConsumed = response.TotalTokens
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成攻击旁白失败");
            return new BattleResult 
            { 
                Success = false, 
                Narrative = "攻击过程生成失败..." 
            };
        }
    }

    public Task<bool> IsUnderProtectionAsync(string userId, string? groupId = null)
    {
        var key = GetProtectionKey(userId, groupId);
        
        if (_protectionMap.TryGetValue(key, out var protectedUntil))
        {
            if (DateTime.Now < protectedUntil)
            {
                return Task.FromResult(true);
            }
            // 保护期已过，移除
            _protectionMap.TryRemove(key, out _);
        }
        
        return Task.FromResult(false);
    }

    public Task SetProtectionAsync(string userId, string? groupId = null)
    {
        var key = GetProtectionKey(userId, groupId);
        var protectedUntil = DateTime.Now.AddSeconds(_protectionSeconds);
        _protectionMap[key] = protectedUntil;
        return Task.CompletedTask;
    }

    private static string GetProtectionKey(string userId, string? groupId)
    {
        return string.IsNullOrEmpty(groupId) ? userId : $"{userId}@{groupId}";
    }

    private string BuildBattlePrompt(
        DigimonDefinition attackerDef, 
        DigimonDefinition targetDef,
        UserDigimonState attacker,
        UserDigimonState target)
    {
        return $"""
            请描述一场数码宝贝之间的战斗：
            
            攻击方：{attackerDef.Name}（{attackerDef.Stage.ToDisplayName()}）
            性格：{attackerDef.Personality.ToDisplayName()}
            形象：{attackerDef.Appearance}
            当前情感：勇气{attacker.Courage}、友情{attacker.Friendship}、爱心{attacker.Love}、知识{attacker.Knowledge}
            
            被攻击方：{targetDef.Name}（{targetDef.Stage.ToDisplayName()}）
            性格：{targetDef.Personality.ToDisplayName()}
            形象：{targetDef.Appearance}
            当前情感：勇气{target.Courage}、友情{target.Friendship}、爱心{target.Love}、知识{target.Knowledge}
            
            请用生动的中文描述这场战斗：
            1. 战斗的起因（攻击方为什么要攻击）
            2. 战斗的过程（双方使用的招式和策略）
            3. 战斗的高潮（关键时刻）
            4. 战斗的结果（谁获胜，或平局）
            
            在描述中自然地体现双方的情感变化：
            - 主动攻击体现勇气增加
            - 战斗中的互动体现友情变化（合作或竞争）
            - 胜利或失败后的反应体现爱心或知识的变化
            """;
    }

    private (string? winner, EmotionChanges attackerChanges, EmotionChanges targetChanges) ParseBattleResult(
        string narrative, string attackerUserId, string targetUserId)
    {
        // 简单的启发式解析
        var attackerChanges = new EmotionChanges();
        var targetChanges = new EmotionChanges();
        string? winner = null;

        var lowerNarrative = narrative.ToLower();

        // 判断胜负
        if (lowerNarrative.Contains("胜利") || lowerNarrative.Contains("获胜") || 
            lowerNarrative.Contains("打败了") || lowerNarrative.Contains("战胜了"))
        {
            // 简单判断哪方获胜（根据描述中的位置）
            var attackerPos = lowerNarrative.IndexOf("攻击方");
            var victoryPos = lowerNarrative.IndexOf("胜利");
            var defeatPos = lowerNarrative.IndexOf("失败");
            
            if (victoryPos > attackerPos && (defeatPos < attackerPos || defeatPos == -1))
            {
                winner = attackerUserId;
                attackerChanges.CourageDelta = new Random().Next(2, 5);
                targetChanges.CourageDelta = new Random().Next(-2, 1);
            }
            else
            {
                winner = targetUserId;
                attackerChanges.CourageDelta = new Random().Next(-1, 2);
                targetChanges.CourageDelta = new Random().Next(2, 5);
            }
        }
        else if (lowerNarrative.Contains("平局") || lowerNarrative.Contains("不分胜负"))
        {
            winner = null;
            attackerChanges.FriendshipDelta = new Random().Next(1, 4);
            targetChanges.FriendshipDelta = new Random().Next(1, 4);
        }
        else
        {
            // 默认双方都有少许变化
            attackerChanges.CourageDelta = new Random().Next(1, 3);
            targetChanges.KnowledgeDelta = new Random().Next(1, 3);
        }

        return (winner, attackerChanges, targetChanges);
    }

    private EmotionChanges ParseObjectBattleEmotions(string narrative)
    {
        var changes = new EmotionChanges();
        var lowerNarrative = narrative.ToLower();

        // 根据描述中的关键词判断情感变化
        if (lowerNarrative.Contains("勇敢") || lowerNarrative.Contains("勇气") || 
            lowerNarrative.Contains("果断") || lowerNarrative.Contains("无畏"))
        {
            changes.CourageDelta = new Random().Next(1, 4);
        }

        if (lowerNarrative.Contains("好奇") || lowerNarrative.Contains("探索") || 
            lowerNarrative.Contains("发现") || lowerNarrative.Contains("学习"))
        {
            changes.KnowledgeDelta = new Random().Next(1, 4);
        }

        if (lowerNarrative.Contains("玩耍") || lowerNarrative.Contains("开心") || 
            lowerNarrative.Contains("愉快") || lowerNarrative.Contains("满足"))
        {
            changes.FriendshipDelta = new Random().Next(1, 4);
        }

        if (lowerNarrative.Contains("温柔") || lowerNarrative.Contains("小心") || 
            lowerNarrative.Contains("爱护") || lowerNarrative.Contains("后悔"))
        {
            changes.LoveDelta = new Random().Next(1, 4);
        }

        // 如果没有明显变化，给一点勇气（因为是主动攻击）
        if (!changes.HasChanges)
        {
            changes.CourageDelta = new Random().Next(1, 3);
        }

        return changes;
    }
}
