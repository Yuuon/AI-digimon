using DigimonBot.Core.Models;
using DigimonBot.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 商店命令 - 查看商品和购买
/// </summary>
public class ShopCommand : ICommand
{
    private readonly IItemRepository _itemRepository;
    private readonly IUserDataRepository _userDataRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ILogger<ShopCommand> _logger;

    public ShopCommand(
        IItemRepository itemRepository,
        IUserDataRepository userDataRepository,
        IInventoryRepository inventoryRepository,
        ILogger<ShopCommand> logger)
    {
        _itemRepository = itemRepository;
        _userDataRepository = userDataRepository;
        _inventoryRepository = inventoryRepository;
        _logger = logger;
    }

    public string Name => "shop";
    public string[] Aliases => new[] { "商店", "buy", "购买" };
    public string Description => "查看商店商品或购买物品";

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        // 无参数时显示商店列表
        if (context.Args.Length == 0)
        {
            return await ShowShopAsync(context);
        }

        // 有参数时处理购买
        return await BuyItemAsync(context);
    }

    private async Task<CommandResult> ShowShopAsync(CommandContext context)
    {
        var shopItems = _itemRepository.GetShopItems().ToList();
        
        if (shopItems.Count == 0)
        {
            return new CommandResult 
            { 
                Success = true, 
                Message = "🏪 商店暂时缺货，请稍后再来~" 
            };
        }

        // 获取用户金币
        var userEconomy = await _userDataRepository.GetOrCreateAsync(context.UserId);

        var lines = new List<string>
        {
            "🏪 **数码商店**",
            "",
            $"💰 你的金币：{userEconomy.Gold}",
            "",
            "商品列表："
        };

        int index = 1;
        foreach (var item in shopItems.OrderBy(i => i.Price))
        {
            lines.Add($"{index}. **{item.Name}** - {item.Price}金币");
            lines.Add($"   {item.Description}");
            lines.Add("");
            index++;
        }

        lines.Add("📖 购买方式：`/shop <物品ID>` 或 `/buy <物品ID>`");
        lines.Add("💡 示例：`/shop courage_cookie`");

        return new CommandResult 
        { 
            Success = true, 
            Message = string.Join("\n", lines) 
        };
    }

    private async Task<CommandResult> BuyItemAsync(CommandContext context)
    {
        var itemId = context.Args[0].ToLower();
        
        // 查找物品
        var item = _itemRepository.GetById(itemId);
        if (item == null)
        {
            // 尝试通过名称查找
            item = _itemRepository.GetShopItems()
                .FirstOrDefault(i => i.Name.Equals(itemId, StringComparison.OrdinalIgnoreCase));
            
            if (item == null)
            {
                return new CommandResult 
                { 
                    Success = false, 
                    Message = $"❌ 找不到物品：{itemId}\n请使用 `/shop` 查看可购买的物品列表。" 
                };
            }
        }

        if (item.Price <= 0)
        {
            return new CommandResult 
            { 
                Success = false, 
                Message = $"❌ {item.Name} 无法购买。" 
            };
        }

        // 检查金币
        var userEconomy = await _userDataRepository.GetOrCreateAsync(context.UserId);
        if (userEconomy.Gold < item.Price)
        {
            return new CommandResult 
            { 
                Success = false, 
                Message = $"❌ 金币不足！\n{item.Name} 需要 {item.Price} 金币，你只有 {userEconomy.Gold} 金币。" 
            };
        }

        // 扣减金币并添加物品
        var success = await _userDataRepository.DeductGoldAsync(context.UserId, item.Price);
        if (!success)
        {
            return new CommandResult 
            { 
                Success = false, 
                Message = "❌ 购买失败，请稍后再试。" 
            };
        }

        await _inventoryRepository.AddItemAsync(context.UserId, item.Id);

        _logger.LogInformation("用户 {UserId} 购买了 {ItemId}，花费 {Price} 金币", 
            context.UserId, item.Id, item.Price);

        return new CommandResult 
        { 
            Success = true, 
            Message = $"✅ 购买成功！\n\n你获得了 **{item.Name}**\n💰 花费：{item.Price} 金币\n💰 剩余：{userEconomy.Gold - item.Price} 金币\n\n使用 `/inventory` 查看背包，使用 `/use {item.Id}` 使用物品。" 
        };
    }
}
