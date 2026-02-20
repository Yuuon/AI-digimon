using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using DigimonBot.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 攻击指令 - 让数码兽攻击其他用户或物体
/// </summary>
public class AttackCommand : ICommand
{
    private readonly IDigimonManager _digimonManager;
    private readonly IDigimonStateRepository _stateRepository;
    private readonly IDigimonRepository _digimonRepository;
    private readonly IBattleService _battleService;
    private readonly ILogger<AttackCommand> _logger;

    public AttackCommand(
        IDigimonManager digimonManager,
        IDigimonStateRepository stateRepository,
        IDigimonRepository digimonRepository,
        IBattleService battleService,
        ILogger<AttackCommand> logger)
    {
        _digimonManager = digimonManager;
        _stateRepository = stateRepository;
        _digimonRepository = digimonRepository;
        _battleService = battleService;
        _logger = logger;
    }

    public string Name => "attack";
    public string[] Aliases => new[] { "攻击", "a", "fight" };
    public string Description => "命令数码兽攻击目标（@用户 或 物体描述）";

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        // 获取攻击方数码兽
        var attacker = await _stateRepository.GetOrCreateAsync(context.UserId);
        var attackerDef = _digimonRepository.GetById(attacker.CurrentDigimonId);
        
        if (attackerDef == null)
        {
            return new CommandResult 
            { 
                Success = false, 
                Message = "❌ 无法获取你的数码兽信息。" 
            };
        }

        // 判断攻击类型：@用户 或 物体描述
        // 优先检查是否有@提及（因为消息解析时@段会被单独处理）
        if (context.MentionedUserIds.Count > 0)
        {
            // 有@提及，攻击指定用户
            return await AttackUserAsync(context, attacker, attackerDef, context.Args.Length > 0 ? context.Args[0] : "");
        }
        else if (context.Args.Length > 0)
        {
            // 检查参数是否可能是QQ号（纯数字）
            var targetArg = context.Args[0];
            if (long.TryParse(targetArg, out _))
            {
                return await AttackUserAsync(context, attacker, attackerDef, targetArg);
            }
            else
            {
                // 攻击物体（所有参数拼接为描述）
                var targetDescription = string.Join(" ", context.Args);
                return await AttackObjectAsync(context, attacker, attackerDef, targetDescription);
            }
        }
        else
        {
            return new CommandResult 
            { 
                Success = false, 
                Message = GetHelpMessage() 
            };
        }
    }

    private async Task<CommandResult> AttackUserAsync(
        CommandContext context, 
        UserDigimonState attacker, 
        DigimonDefinition attackerDef,
        string targetArg)
    {
        // 解析目标用户ID
        string? targetUserId = null;
        string? targetOriginalId = null;

        // 从@提及获取（优先）
        if (context.MentionedUserIds.Count > 0)
        {
            targetOriginalId = context.MentionedUserIds[0];
            targetUserId = GenerateUserId(targetOriginalId, context.GroupId);
        }
        // 尝试解析QQ号
        else if (long.TryParse(targetArg.TrimStart('@'), out var targetQQ))
        {
            targetOriginalId = targetQQ.ToString();
            targetUserId = GenerateUserId(targetOriginalId, context.GroupId);
        }

        if (string.IsNullOrEmpty(targetUserId))
        {
            return new CommandResult 
            { 
                Success = false, 
                Message = "❌ 无法识别攻击目标。请@目标用户或输入QQ号。" 
            };
        }

        // 不能攻击自己
        if (targetUserId == context.UserId)
        {
            return new CommandResult 
            { 
                Success = false, 
                Message = "❌ 不能攻击自己的数码兽！" 
            };
        }

        // 获取被攻击方数码兽
        var target = await _stateRepository.GetAsync(targetUserId);
        if (target == null)
        {
            return new CommandResult 
            { 
                Success = false, 
                Message = $"❌ 目标用户还没有数码兽！" 
            };
        }

        var targetDef = _digimonRepository.GetById(target.CurrentDigimonId);
        if (targetDef == null)
        {
            return new CommandResult 
            { 
                Success = false, 
                Message = "❌ 无法获取目标数码兽信息。" 
            };
        }

        // 检查保护机制
        var isProtected = await _battleService.IsUnderProtectionAsync(targetUserId);
        if (isProtected)
        {
            return new CommandResult 
            { 
                Success = false, 
                Message = "🛡️ 目标数码兽刚经历过战斗，处于保护状态中，暂时无法被攻击。" 
            };
        }

        // 执行战斗
        var result = await _battleService.BattleDigimonAsync(
            attacker, attackerDef, target, targetDef);

        if (!result.Success)
        {
            return new CommandResult 
            { 
                Success = false, 
                Message = "❌ 战斗生成失败，请稍后再试。" 
            };
        }

        // 应用情感变化
        await ApplyEmotionChanges(attacker, result.AttackerEmotionChanges);
        await ApplyEmotionChanges(target, result.TargetEmotionChanges);

        // 设置保护状态
        await _battleService.SetProtectionAsync(targetUserId);

        // 构建显示名称
        var attackerName = context.ShouldAddPrefix && !string.IsNullOrWhiteSpace(context.UserName)
            ? $"[{context.UserName}]的{attackerDef.Name}"
            : attackerDef.Name;
        
        var targetDisplayName = $"[QQ:{targetOriginalId}]的{targetDef.Name}";

        // 构建结果消息
        var lines = new List<string>
        {
            "⚔️ **战斗开始！**",
            "",
            $"{attackerName} VS {targetDisplayName}",
            "",
            "📖 **战斗过程**",
            result.Narrative,
            "",
            "📊 **战斗影响**"
        };

        if (result.AttackerEmotionChanges.HasChanges)
        {
            lines.Add($"{attackerName}: {result.AttackerEmotionChanges}");
        }
        if (result.TargetEmotionChanges?.HasChanges == true)
        {
            lines.Add($"{targetDisplayName}: {result.TargetEmotionChanges}");
        }

        if (result.WinnerUserId == attacker.UserId)
        {
            lines.Add("🏆 **战斗结果：攻击方获胜！**");
        }
        else if (result.WinnerUserId == target.UserId)
        {
            lines.Add("🏆 **战斗结果：防御方获胜！**");
        }
        else
        {
            lines.Add("🤝 **战斗结果：平局！**");
        }

        lines.Add("");
        lines.Add("🛡️ 被攻击方已进入5分钟保护状态");

        _logger.LogInformation("用户 {Attacker} 攻击了 {Target}，结果：{Result}", 
            context.UserId, targetUserId, result.WinnerUserId ?? "平局");

        return new CommandResult 
        { 
            Success = true, 
            Message = string.Join("\n", lines) 
        };
    }

    private async Task<CommandResult> AttackObjectAsync(
        CommandContext context, 
        UserDigimonState attacker, 
        DigimonDefinition attackerDef,
        string targetDescription)
    {
        // 执行攻击物体的战斗
        var result = await _battleService.BattleObjectAsync(
            attacker, attackerDef, targetDescription);

        if (!result.Success)
        {
            return new CommandResult 
            { 
                Success = false, 
                Message = "❌ 攻击过程生成失败，请稍后再试。" 
            };
        }

        // 应用情感变化（仅攻击方）
        await ApplyEmotionChanges(attacker, result.AttackerEmotionChanges);

        // 构建显示名称
        var attackerName = context.ShouldAddPrefix && !string.IsNullOrWhiteSpace(context.UserName)
            ? $"[{context.UserName}]的{attackerDef.Name}"
            : attackerDef.Name;

        // 构建结果消息
        var lines = new List<string>
        {
            "⚔️ **攻击行动！**",
            "",
            $"{attackerName} 攻击了 **{targetDescription}**",
            "",
            "📖 **过程描述**",
            result.Narrative,
            ""
        };

        if (result.AttackerEmotionChanges.HasChanges)
        {
            lines.Add($"📊 **情感变化**: {result.AttackerEmotionChanges}");
        }

        _logger.LogInformation("用户 {UserId} 的数码兽攻击了物体：{Target}", 
            context.UserId, targetDescription);

        return new CommandResult 
        { 
            Success = true, 
            Message = string.Join("\n", lines) 
        };
    }

    private static string GenerateUserId(string originalUserId, long? groupId)
    {
        // 群聊模式下拼接用户ID
        if (groupId.HasValue && groupId.Value > 0)
        {
            return $"{originalUserId}@g{groupId.Value}";
        }
        return originalUserId;
    }

    private async Task ApplyEmotionChanges(UserDigimonState state, EmotionChanges? changes)
    {
        if (changes == null || !changes.HasChanges)
            return;

        var oldCourage = state.Courage;
        var oldFriendship = state.Friendship;
        var oldLove = state.Love;
        var oldKnowledge = state.Knowledge;

        state.Courage = Math.Max(0, state.Courage + changes.CourageDelta);
        state.Friendship = Math.Max(0, state.Friendship + changes.FriendshipDelta);
        state.Love = Math.Max(0, state.Love + changes.LoveDelta);
        state.Knowledge = Math.Max(0, state.Knowledge + changes.KnowledgeDelta);

        await _stateRepository.SaveAsync(state);
    }

    private static string GetHelpMessage()
    {
        return """
            ⚔️ **攻击指令**
            
            用法：
            • `/attack @用户` - 攻击指定用户的数码兽
            • `/attack QQ号` - 攻击指定QQ号的数码兽
            • `/attack 物体描述` - 让数码兽攻击物体
            
            示例：
            • `/attack @小明` - 攻击小明的数码兽
            • `/attack 123456789` - 攻击QQ号为123456789的数码兽
            • `/attack 大石头` - 让数码兽攻击大石头
            • `/attack 路边的野狗` - 让数码兽攻击野狗
            
            💡 **说明**：
            • 被攻击的数码兽会进入5分钟保护状态
            • 攻击会消耗AI Token但不计入成长
            • 战斗结果会影响双方的情感属性
            """;
    }
}
