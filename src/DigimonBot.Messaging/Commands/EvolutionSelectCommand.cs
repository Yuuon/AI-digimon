using DigimonBot.AI.Services;
using DigimonBot.Core.Events;
using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using DigimonBot.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 选择进化分支命令
/// </summary>
public class EvolutionSelectCommand : ICommand
{
    private readonly IDigimonManager _digimonManager;
    private readonly IDigimonRepository _repository;
    private readonly IEvolutionEngine _evolutionEngine;
    private readonly IEventPublisher _eventPublisher;
    private readonly IPersonalityEngine _personalityEngine;
    private readonly ILogger<EvolutionSelectCommand> _logger;

    public EvolutionSelectCommand(
        IDigimonManager digimonManager,
        IDigimonRepository repository,
        IEvolutionEngine evolutionEngine,
        IEventPublisher eventPublisher,
        IPersonalityEngine personalityEngine,
        ILogger<EvolutionSelectCommand> logger)
    {
        _digimonManager = digimonManager;
        _repository = repository;
        _evolutionEngine = evolutionEngine;
        _eventPublisher = eventPublisher;
        _personalityEngine = personalityEngine;
        _logger = logger;
    }

    public string Name => "evoselect";
    public string[] Aliases => new[] { "进化选择", "evos", "es" };
    public string Description => "选择特定的进化分支";

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        try
        {
            // 检查参数
            if (context.Args.Length == 0 || !int.TryParse(context.Args[0], out int selection))
            {
                return new CommandResult
                {
                    Success = false,
                    Message = "❌ 请提供要选择的进化分支序号。\n使用：`/evoselect 1`\n先使用 `/evolist` 查看可用选项。"
                };
            }

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

            // 获取可进化列表
            var availableEvolutions = _evolutionEngine.GetAvailableEvolutions(digimon, definition, _repository.GetAll());

            if (availableEvolutions.Count == 0)
            {
                return new CommandResult
                {
                    Success = false,
                    Message = $"📊 **{definition.Name}** 目前没有满足条件的进化分支。\n请先使用 `/evolist` 查看进化条件。"
                };
            }

            // 检查序号是否有效
            if (selection < 1 || selection > availableEvolutions.Count)
            {
                return new CommandResult
                {
                    Success = false,
                    Message = $"❌ 无效的序号。请输入 1 到 {availableEvolutions.Count} 之间的数字。\n使用 `/evolist` 查看可用选项。"
                };
            }

            // 获取选择的进化分支
            var selectedEvo = availableEvolutions[selection - 1];

            // 执行进化
            var evolutionResult = await _evolutionEngine.SelectEvolutionAsync(
                digimon, definition, selectedEvo.TargetId, _repository.GetAll());

            if (evolutionResult == null || !evolutionResult.Success)
            {
                return new CommandResult
                {
                    Success = false,
                    Message = "❌ 进化失败，请检查是否满足进化条件。"
                };
            }

            // 更新内存中的对象
            digimon.CurrentDigimonId = evolutionResult.NewDigimonId;

            // 如果是重生，重置Token和情感
            if (evolutionResult.IsRebirth)
            {
                digimon.TotalTokensConsumed = 0;
                digimon.Emotions = new EmotionValues();
                await _digimonManager.SaveAsync(digimon);
                _logger.LogInformation("User {UserId} 重生完成，状态已重置", context.UserId);
            }

            // 更新数据库
            await _digimonManager.UpdateDigimonAsync(context.UserId, evolutionResult.NewDigimonId);

            _logger.LogInformation("User {UserId} 选择进化: {Old} -> {New}",
                context.UserId, evolutionResult.OldDigimonId, evolutionResult.NewDigimonId);

            // 发布进化事件
            _eventPublisher.PublishEvolution(new EvolutionEventArgs
            {
                UserId = context.UserId,
                OldDigimonId = evolutionResult.OldDigimonId,
                NewDigimonId = evolutionResult.NewDigimonId,
                NewDigimonName = evolutionResult.NewDigimonName,
                IsRebirth = evolutionResult.IsRebirth,
                EvolutionDescription = evolutionResult.Message
            });

            // 生成进化公告
            var newDef = _repository.GetById(evolutionResult.NewDigimonId);
            var announcement = _personalityEngine.BuildEvolutionAnnouncement(newDef!, evolutionResult.IsRebirth);

            return new CommandResult
            {
                Success = true,
                Message = $"{announcement}\n\n{evolutionResult.Message}",
                EvolutionOccurred = true,
                EvolutionMessage = evolutionResult.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "选择进化分支失败");
            return new CommandResult
            {
                Success = false,
                Message = "❌ 进化时发生错误，请稍后再试。"
            };
        }
    }
}
