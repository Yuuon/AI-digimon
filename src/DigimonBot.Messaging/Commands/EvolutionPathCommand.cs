using DigimonBot.AI.Services;
using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using DigimonBot.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 进化路线查询命令 - 支持查看他人数据（白名单限定）
/// </summary>
public class EvolutionPathCommand : ICommand
{
    private readonly IDigimonManager _digimonManager;
    private readonly IDigimonRepository _repository;
    private readonly IEvolutionEngine _evolutionEngine;
    private readonly List<string> _whitelist;
    private readonly ILogger<EvolutionPathCommand> _logger;

    public EvolutionPathCommand(
        IDigimonManager digimonManager, 
        IDigimonRepository repository, 
        IEvolutionEngine evolutionEngine,
        AdminConfig adminConfig,
        ILogger<EvolutionPathCommand> logger)
    {
        _digimonManager = digimonManager;
        _repository = repository;
        _evolutionEngine = evolutionEngine;
        _whitelist = adminConfig.Whitelist ?? new List<string>();
        _logger = logger;
    }

    public string Name => "path";
    public string[] Aliases => new[] { "进化路线", "evo", "p" };
    public string Description => "查看可能的进化路线（可加QQ号/@他人查看他人数据）";

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
                _logger.LogWarning("用户 {UserId} 尝试查看他人进化路线，但不在白名单中", context.OriginalUserId);
                return new CommandResult 
                { 
                    Success = false, 
                    Message = "❌ 你没有权限查看他人的进化路线。"
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

        var possibleEvos = _evolutionEngine.GetPossibleEvolutions(digimon, definition, _repository.GetAll());
        
        // 构建显示名称
        var displayName = isViewingOthers 
            ? $"[QQ:{targetOriginalId}]的{definition.Name}"
            : definition.Name;

        var prefix = context.ShouldAddPrefix && !isViewingOthers && !string.IsNullOrWhiteSpace(context.UserName) 
            ? $"[{context.UserName}]的" 
            : "";

        if (possibleEvos.Count == 0)
        {
            var isFinal = definition.Stage.ToString().Contains("Ultimate");
            if (isFinal)
            {
                return new CommandResult 
                { 
                    Success = true, 
                    Message = $"🌟 {prefix}**{displayName}** 已经是最终形态！\n继续培养将触发'轮回进化'，回到幼年期重新开始新的旅程。" 
                };
            }
            return new CommandResult { Success = true, Message = "当前阶段没有可查询的进化路线。" };
        }

        var lines = new List<string> { $"🔮 {prefix}**{displayName}** 可能的进化路线：" };
        
        foreach (var evo in possibleEvos)
        {
            var reqEmotions = new List<string>();
            if (evo.RequiredEmotions.Courage > 0) reqEmotions.Add($"勇气{evo.RequiredEmotions.Courage}");
            if (evo.RequiredEmotions.Friendship > 0) reqEmotions.Add($"友情{evo.RequiredEmotions.Friendship}");
            if (evo.RequiredEmotions.Love > 0) reqEmotions.Add($"爱心{evo.RequiredEmotions.Love}");
            if (evo.RequiredEmotions.Knowledge > 0) reqEmotions.Add($"知识{evo.RequiredEmotions.Knowledge}");

            lines.Add($"""
            
            ➡️ **{evo.TargetName}**
            进度：{evo.CurrentMatchPercent:F0}% | 需要Token：{evo.RequiredTokens}
            需求：{string.Join(", ", reqEmotions)}
            """);
        }

        return new CommandResult { Success = true, Message = string.Join("\n", lines) };
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
