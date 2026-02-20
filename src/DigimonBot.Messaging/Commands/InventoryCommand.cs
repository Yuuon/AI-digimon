using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using DigimonBot.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 背包命令 - 查看物品
/// </summary>
public class InventoryCommand : ICommand
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IItemRepository _itemRepository;
    private readonly IDigimonManager _digimonManager;
    private readonly ILogger<InventoryCommand> _logger;

    public InventoryCommand(
        IInventoryRepository inventoryRepository,
        IItemRepository itemRepository,
        IDigimonManager digimonManager,
        ILogger<InventoryCommand> logger)
    {
        _inventoryRepository = inventoryRepository;
        _itemRepository = itemRepository;
        _digimonManager = digimonManager;
        _logger = logger;
    }

    public string Name => "inventory";
    public string[] Aliases => new[] { "背包", "inv", "bag", "i" };
    public string Description => "查看背包中的物品";

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        var inventory = await _inventoryRepository.GetInventoryAsync(context.UserId);
        var digimon = await _digimonManager.GetOrCreateAsync(context.UserId);
        var definition = await GetDigimonDefinitionAsync(digimon.CurrentDigimonId);

        if (inventory.Count == 0)
        {
            return new CommandResult 
            { 
                Success = true, 
                Message = $"🎒 **{definition?.Name ?? "你的"}的背包**\n\n背包是空的~\n去 `/shop` 购买一些物品吧！" 
            };
        }

        var lines = new List<string>
        {
            $"🎒 **{definition?.Name ?? "你的"}的背包**",
            ""
        };

        foreach (var userItem in inventory)
        {
            var itemDef = _itemRepository.GetById(userItem.ItemId);
            if (itemDef != null)
            {
                lines.Add($"• **{itemDef.Name}** x{userItem.Quantity}");
                lines.Add($"  ID: `{itemDef.Id}` | {itemDef.Description}");
                lines.Add("");
            }
        }

        lines.Add("💡 使用物品：`/use <物品ID>`");
        lines.Add("💡 示例：`/use courage_cookie`");

        return new CommandResult 
        { 
            Success = true, 
            Message = string.Join("\n", lines) 
        };
    }

    private async Task<DigimonDefinition?> GetDigimonDefinitionAsync(string digimonId)
    {
        // 由于 DigimonMessageHandler 中 definition 是从 IDigimonRepository 获取的
        // 这里我们需要通过其他方式获取，或者简单返回 null
        // 实际项目中可能需要重构，暂时返回 null
        return null;
    }
}
