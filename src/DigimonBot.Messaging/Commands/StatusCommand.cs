using DigimonBot.AI.Services;
using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using DigimonBot.Data.Repositories;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 状态查询命令
/// </summary>
public class StatusCommand : ICommand
{
    private readonly IDigimonManager _digimonManager;
    private readonly IDigimonRepository _repository;
    private readonly IEvolutionEngine _evolutionEngine;

    public StatusCommand(IDigimonManager digimonManager, IDigimonRepository repository, IEvolutionEngine evolutionEngine)
    {
        _digimonManager = digimonManager;
        _repository = repository;
        _evolutionEngine = evolutionEngine;
    }

    public string Name => "status";
    public string[] Aliases => new[] { "状态", "s" };
    public string Description => "查看当前数码宝贝状态";

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        var digimon = await _digimonManager.GetOrCreateAsync(context.UserId);
        var definition = _repository.GetById(digimon.CurrentDigimonId);
        
        if (definition == null)
        {
            return new CommandResult { Success = false, Message = "出错了，找不到数码宝贝数据。" };
        }

        var progress = _evolutionEngine.GetProgress(digimon, definition);
        
        // 构建前缀
        var prefix = context.ShouldAddPrefix && !string.IsNullOrWhiteSpace(context.UserName) 
            ? $"[{context.UserName}]的" 
            : "";

        var message = $"""
        📊 {prefix}**{definition.Name}** 的状态
        
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
}
