using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using DigimonBot.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 使用物品命令
/// </summary>
public class UseItemCommand : ICommand
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IItemRepository _itemRepository;
    private readonly IDigimonManager _digimonManager;
    private readonly ILogger<UseItemCommand> _logger;

    public UseItemCommand(
        IInventoryRepository inventoryRepository,
        IItemRepository itemRepository,
        IDigimonManager digimonManager,
        ILogger<UseItemCommand> logger)
    {
        _inventoryRepository = inventoryRepository;
        _itemRepository = itemRepository;
        _digimonManager = digimonManager;
        _logger = logger;
    }

    public string Name => "use";
    public string[] Aliases => new[] { "使用", "eat", "吃" };
    public string Description => "使用背包中的物品";

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        if (context.Args.Length == 0)
        {
            return new CommandResult 
            { 
                Success = false, 
                Message = "❌ 请指定要使用的物品ID\n示例：`/use courage_cookie`" 
            };
        }

        var itemId = context.Args[0].ToLower();

        // 查找物品定义
        var itemDef = _itemRepository.GetById(itemId);
        if (itemDef == null)
        {
            // 尝试通过名称查找
            itemDef = _itemRepository.GetAll()
                .Values
                .FirstOrDefault(i => i.Name.Equals(itemId, StringComparison.OrdinalIgnoreCase));
            
            if (itemDef == null)
            {
                return new CommandResult 
                { 
                    Success = false, 
                    Message = $"❌ 找不到物品：{itemId}" 
                };
            }
        }

        // 检查是否拥有该物品
        var hasItem = await _inventoryRepository.HasItemAsync(context.UserId, itemDef.Id);
        if (!hasItem)
        {
            return new CommandResult 
            { 
                Success = false, 
                Message = $"❌ 你的背包中没有 **{itemDef.Name}**\n使用 `/inventory` 查看背包物品。" 
            };
        }

        // 获取用户数码宝贝
        var digimon = await _digimonManager.GetOrCreateAsync(context.UserId);

        // 应用物品效果
        var effectMessages = new List<string>();
        foreach (var effect in itemDef.Effects)
        {
            var (message, applied) = ApplyEffect(digimon, effect.Key, effect.Value);
            if (applied)
            {
                effectMessages.Add(message);
            }
        }

        if (effectMessages.Count == 0)
        {
            return new CommandResult 
            { 
                Success = false, 
                Message = $"❌ **{itemDef.Name}** 没有效果或无法使用。" 
            };
        }

        // 消耗物品
        var success = await _inventoryRepository.UseItemAsync(context.UserId, itemDef.Id);
        if (!success)
        {
            return new CommandResult 
            { 
                Success = false, 
                Message = "❌ 使用物品失败，请稍后再试。" 
            };
        }

        // 保存数码宝贝状态
        await _digimonManager.SaveAsync(digimon);

        _logger.LogInformation("用户 {UserId} 使用了 {ItemId}", context.UserId, itemDef.Id);

        var resultMessage = $"✅ **{itemDef.Name}** 使用成功！\n\n" + string.Join("\n", effectMessages);

        // 检查是否触发了进化
        // 这里可以添加进化检查逻辑

        return new CommandResult 
        { 
            Success = true, 
            Message = resultMessage 
        };
    }

    private (string message, bool applied) ApplyEffect(UserDigimon digimon, string effectType, int value)
    {
        switch (effectType.ToLower())
        {
            case "courage":
            case "勇气":
                var oldCourage = digimon.Emotions.Courage;
                digimon.Emotions.Courage += value;
                return ($"❤️ 勇气：{oldCourage} → {digimon.Emotions.Courage} (+{value})", true);

            case "friendship":
            case "友情":
                var oldFriendship = digimon.Emotions.Friendship;
                digimon.Emotions.Friendship += value;
                return ($"💛 友情：{oldFriendship} → {digimon.Emotions.Friendship} (+{value})", true);

            case "love":
            case "爱心":
                var oldLove = digimon.Emotions.Love;
                digimon.Emotions.Love += value;
                return ($"💗 爱心：{oldLove} → {digimon.Emotions.Love} (+{value})", true);

            case "knowledge":
            case "知识":
                var oldKnowledge = digimon.Emotions.Knowledge;
                digimon.Emotions.Knowledge += value;
                return ($"💙 知识：{oldKnowledge} → {digimon.Emotions.Knowledge} (+{value})", true);

            case "gold":
            case "金币":
                // 金币效果需要特殊处理，这里仅返回消息
                return ($"💰 获得 {value} 金币", true);

            default:
                return ($"未知效果：{effectType}", false);
        }
    }
}
