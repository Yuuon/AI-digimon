using DigimonBot.AI.Services;
using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using DigimonBot.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 状态查询命令 - 支持查看他人数据（白名单限定）
/// </summary>
public class StatusCommand : ICommand
{
    private readonly IDigimonManager _digimonManager;
    private readonly IDigimonRepository _repository;
    private readonly IEvolutionEngine _evolutionEngine;
    private readonly List<string> _whitelist;
    private readonly ILogger<StatusCommand> _logger;

    public StatusCommand(
        IDigimonManager digimonManager, 
        IDigimonRepository repository, 
        IEvolutionEngine evolutionEngine,
        AdminConfig adminConfig,
        ILogger<StatusCommand> logger)
    {
        _digimonManager = digimonManager;
        _repository = repository;
        _evolutionEngine = evolutionEngine;
        _whitelist = adminConfig.Whitelist ?? new List<string>();
        _logger = logger;
    }

    public string Name => "status";
    public string[] Aliases => new[] { "状态", "s" };
    public string Description => "查看数码宝贝状态（可加QQ号/@他人查看他人数据）";

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        // 判断是否要查看他人数据
        var isViewingOthers = !string.IsNullOrEmpty(context.TargetUserId) && 
                              context.TargetUserId != context.UserId;

        // 如果要查看他人数据，检查权限
        if (isViewingOthers)
        {
            if (!context.IsGroupMessage)
            {
                return new CommandResult 
                { 
                    Success = false, 
                    Message = "❌ 查看他人数据功能仅限群聊中使用。"
                };
            }

            if (!IsWhitelisted(context.OriginalUserId))
            {
                _logger.LogWarning("用户 {UserId} 尝试查看他人数据，但不在白名单中", context.OriginalUserId);
                return new CommandResult 
                { 
                    Success = false, 
                    Message = "❌ 你没有权限查看他人的数码宝贝数据。"
                };
            }
        }

        // 确定要查询的用户ID
        var targetUserId = isViewingOthers ? context.TargetUserId! : context.UserId;
        var targetOriginalId = isViewingOthers ? context.TargetOriginalUserId! : context.OriginalUserId;

        var digimon = await _digimonManager.GetOrCreateAsync(targetUserId);
        
        var definition = _repository.GetById(digimon.CurrentDigimonId);
        
        if (definition == null)
        {
            return new CommandResult { Success = false, Message = "出错了，找不到数码宝贝数据。" };
        }

        var progress = _evolutionEngine.GetProgress(digimon, definition);

        // 构建显示名称
        var displayName = isViewingOthers 
            ? $"[QQ:{targetOriginalId}]的{definition.Name}"
            : definition.Name;

        var prefix = context.ShouldAddPrefix && !isViewingOthers && !string.IsNullOrWhiteSpace(context.UserName) 
            ? $"[{context.UserName}]的"
            : "";

        var message = $"""
        📊 **{prefix}{displayName}** 的状态
        
        🏷️ 阶段：{definition.Stage.ToDisplayName()}
        💭 性格：{definition.Personality.ToDisplayName()}
        
        ❤️ 情感属性：
        • 勇气：{digimon.Emotions.Courage}
        • 友情：{digimon.Emotions.Friendship}
        • 爱心：{digimon.Emotions.Love}
        • 知识：{digimon.Emotions.Knowledge}
        
        📈 进化进度：
        • Token消耗：{progress.CurrentTokens}/{progress.RequiredTokens} ({progress.TokenProgressPercent:F1}%)
        • 情感达成：{progress.EmotionProgressPercent:F1}%
        {(progress.IsReadyForEvolution ? "✨ **进化准备就绪！继续对话即可触发进化**" : "")}
        """;

        return new CommandResult { Success = true, Message = message };
    }

    /// <summary>
    /// 检查用户是否在白名单中
    /// </summary>
    private bool IsWhitelisted(string userId)
    {
        if (_whitelist == null || _whitelist.Count == 0)
        {
            return false;
        }
        return _whitelist.Contains(userId);
    }
}
