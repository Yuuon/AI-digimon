using DigimonBot.AI.Services;
using DigimonBot.Core.Services;
using DigimonBot.Data.Repositories;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 进化路线查询命令
/// </summary>
public class EvolutionPathCommand : ICommand
{
    private readonly IDigimonManager _digimonManager;
    private readonly IDigimonRepository _repository;
    private readonly IEvolutionEngine _evolutionEngine;

    public EvolutionPathCommand(IDigimonManager digimonManager, IDigimonRepository repository, IEvolutionEngine evolutionEngine)
    {
        _digimonManager = digimonManager;
        _repository = repository;
        _evolutionEngine = evolutionEngine;
    }

    public string Name => "path";
    public string[] Aliases => new[] { "进化路线", "evo", "p" };
    public string Description => "查看可能的进化路线";

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        var digimon = await _digimonManager.GetOrCreateAsync(context.UserId);
        var definition = _repository.GetById(digimon.CurrentDigimonId);
        
        if (definition == null)
        {
            return new CommandResult { Success = false, Message = "出错了，找不到数码宝贝数据。" };
        }

        var possibleEvos = _evolutionEngine.GetPossibleEvolutions(digimon, definition, _repository.GetAll());
        
        // 构建前缀
        var prefix = context.ShouldAddPrefix && !string.IsNullOrWhiteSpace(context.UserName) 
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
                    Message = $"🌟 {prefix}**{definition.Name}** 已经是最终形态！\n继续培养将触发'轮回进化'，回到幼年期重新开始新的旅程。" 
                };
            }
            return new CommandResult { Success = true, Message = "当前阶段没有可查询的进化路线。" };
        }

        var lines = new List<string> { $"🔮 {prefix}**{definition.Name}** 可能的进化路线：" };
        
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
}
