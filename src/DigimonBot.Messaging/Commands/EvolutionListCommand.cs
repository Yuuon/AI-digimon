using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using DigimonBot.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 显示可进化分支列表命令
/// </summary>
public class EvolutionListCommand : ICommand
{
    private readonly IDigimonManager _digimonManager;
    private readonly IDigimonRepository _repository;
    private readonly IEvolutionEngine _evolutionEngine;
    private readonly ILogger<EvolutionListCommand> _logger;

    public EvolutionListCommand(
        IDigimonManager digimonManager,
        IDigimonRepository repository,
        IEvolutionEngine evolutionEngine,
        ILogger<EvolutionListCommand> logger)
    {
        _digimonManager = digimonManager;
        _repository = repository;
        _evolutionEngine = evolutionEngine;
        _logger = logger;
    }

    public string Name => "evolist";
    public string[] Aliases => new[] { "进化列表", "evo", "el" };
    public string Description => "查看当前可进化的分支选项";

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        try
        {
            var digimon = await _digimonManager.GetOrCreateAsync(context.UserId);
            var definition = _repository.GetById(digimon.CurrentDigimonId);

            if (definition == null)
            {
                return new CommandResult
                {
                    Success = false,
                    Message = "❌ 出错了，找不到数码宝贝数据。"
                };
            }

            // 获取所有可进化选项
            var availableEvolutions = _evolutionEngine.GetAvailableEvolutions(digimon, definition, _repository.GetAll());

            if (availableEvolutions.Count == 0)
            {
                // 检查进化进度
                var progress = _evolutionEngine.GetProgress(digimon, definition);
                
                if (!progress.IsReadyForEvolution)
                {
                    return new CommandResult
                    {
                        Success = false,
                        Message = $"📊 **{definition.Name}** 的进化条件尚未满足\n\n" +
                                  $"当前进度：\n" +
                                  $"• Token消耗：{progress.CurrentTokens}/{progress.RequiredTokens} ({progress.TokenProgressPercent:F1}%)\n" +
                                  $"• 情感达成：{progress.EmotionProgressPercent:F1}%\n\n" +
                                  $"继续与数码宝贝互动，提升情感值和Token消耗吧！"
                    };
                }

                return new CommandResult
                {
                    Success = false,
                    Message = $"📊 **{definition.Name}** 目前无可用的进化分支。"
                };
            }

            // 构建进化选项列表
            var options = new List<string>();
            for (int i = 0; i < availableEvolutions.Count; i++)
            {
                var evo = availableEvolutions[i];
                var emotions = FormatEmotions(evo.RequiredEmotions);
                
                options.Add($"""
                    **{i + 1}. {evo.TargetName}**
                    {evo.Description}
                    • 需要Token：{evo.RequiredTokens}
                    • 需要情感：{emotions}
                    • 匹配度：{evo.MatchScore * 100:F1}%
                    """)
                ;
            }

            var message = $"""
                🌟 **{definition.Name}** 可以进化了！

                检测到 **{availableEvolutions.Count}** 个可进化分支：

                {string.Join("\n\n", options)}

                使用 `/evoselect <序号>` 选择想要进化的分支
                例如：`/evoselect 1`
                """;

            return new CommandResult
            {
                Success = true,
                Message = message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取进化列表失败");
            return new CommandResult
            {
                Success = false,
                Message = "❌ 获取进化列表时发生错误。"
            };
        }
    }

    private static string FormatEmotions(EmotionValues emotions)
    {
        if (emotions.Courage == 0 && emotions.Friendship == 0 && emotions.Love == 0 && emotions.Knowledge == 0)
        {
            return "无特殊要求";
        }

        var parts = new List<string>();
        if (emotions.Courage > 0) parts.Add($"勇气{emotions.Courage}");
        if (emotions.Friendship > 0) parts.Add($"友情{emotions.Friendship}");
        if (emotions.Love > 0) parts.Add($"爱心{emotions.Love}");
        if (emotions.Knowledge > 0) parts.Add($"知识{emotions.Knowledge}");

        return string.Join("、", parts);
    }
}
